using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Sends the host's visual and audio feedback to clients. Abilities, AudioManager, HUDManager, and HitEffects
/// all keep running on the host only and report what they did through here.
///
/// Anything made with NetworkFx.Instantiate that has a NetworkObject (birds, dodos, mini 31RD) just gets spawned.
/// Everything else (VFX, coins, fish, etc.) gets a copy on each client that only shows the visuals and follows the host's one.
///
/// All of this can still be called in a local match or on a client, it just won't do anything extra there.
/// </summary>
public class NetworkFx : NetworkBehaviour
{
    public enum Sound : byte
    {
        Scoring,
        BallPlayerInteraction,
        BallNetHit,
        BallGroundHit,
        BuffStart,
        BuffEnd,
        DebuffStart,
        DebuffEnd
    }

    public enum HudEvent : byte
    {
        TriggerOffensiveCooldown,
        TriggerDefensiveCooldown,
        UpdateOffensiveCooldown,
        UpdateDefensiveCooldown,
        ResetOffensiveCooldown,
        ResetDefensiveCooldown,
        SetOffensiveArmed,
        SetDefensiveIconForBird,
        RefreshPlayerCard
    }

    public static NetworkFx Instance { get; private set; }

    private const double countdownBuffer = 2.0; // Seconds of "Waiting for players..." once the birds have been spawned

    private static NetworkFxRegistry registry;
    private static Dictionary<GameObject, int> prefabIds;

    private readonly Dictionary<int, GameObject> visuals = new(); // Client: visual copies by id
    private readonly Dictionary<GameObject, List<Renderer>> hiddenRenderers = new(); // Client: renderers turned off by SetVisible
    private int nextMirrorId = 1; // Host: id for the next mirrored object

    private static bool CanForward => NetworkSession.IsServer && Instance != null && Instance.IsSpawned;

    void Awake()
    {
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    // Objects

    public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        GameObject instance = parent != null
            ? Object.Instantiate(prefab, position, rotation, parent)
            : Object.Instantiate(prefab, position, rotation);

        ShareWithClients(prefab, instance, NetworkManager.ServerClientId);
        return instance;
    }

    public static GameObject Instantiate(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null;

        GameObject instance = Object.Instantiate(prefab, parent);
        ShareWithClients(prefab, instance, NetworkManager.ServerClientId);
        return instance;
    }

    public static new T Instantiate<T>(T prefab, Transform parent) where T : Component
    {
        return prefab == null ? null : Instantiate(prefab.gameObject, parent).GetComponent<T>();
    }

    // Same as Instantiate for a networked prefab, but owned by a client so that client can control it.
    public static GameObject InstantiateOwned(GameObject prefab, Vector3 position, Quaternion rotation, ulong ownerClientId)
    {
        if (prefab == null) return null;

        GameObject instance = Object.Instantiate(prefab, position, rotation);
        ShareWithClients(prefab, instance, ownerClientId);
        return instance;
    }

    private static void ShareWithClients(GameObject prefab, GameObject instance, ulong ownerClientId)
    {
        if (!CanForward) return;

        if (instance.TryGetComponent(out NetworkObject netObj))
        {
            netObj.SpawnWithOwnership(ownerClientId, true);
            return;
        }

        int prefabId = GetPrefabId(prefab);
        if (prefabId < 0)
        {
            Debug.LogWarning($"[NetworkFx] {prefab.name} is not in the registry, so clients will not see it. Run BIRDBRAIN > Apply Network Setup.");
            return;
        }

        instance.AddComponent<NetworkFxMirror>().BeginPrefab(prefabId);
    }

    // Shows a LineRenderer on clients and keeps its points and visibility up to date.
    public static void MirrorLine(GameObject lineObject)
    {
        if (!CanForward || lineObject == null || lineObject.TryGetComponent<NetworkFxMirror>(out _)) return;
        lineObject.AddComponent<NetworkFxMirror>().BeginLine();
    }

    // Activates or deactivates a spawned object on the host and shows or hides it on clients. Clients keep
    // it active so that it stays ready for network updates.
    public static void SetActive(GameObject target, bool active)
    {
        target.SetActive(active);
        SetVisible(target, active);
    }

    public static void SetVisible(GameObject target, bool visible)
    {
        // Abilities also get here while the match is shutting down, when their objects have already despawned
        if (CanForward && target.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
            Instance.SetVisibleRpc(netObj, visible);
    }

    // Moves the player's color-coded ground follower to a replacement object, like Phoenix's chickens.
    public static void RetargetFollowers(GameObject from, GameObject to)
    {
        if (CanForward && from.TryGetComponent(out NetworkObject fromNetObj) && to.TryGetComponent(out NetworkObject toNetObj))
            Instance.RetargetFollowersRpc(fromNetObj, toNetObj);
    }

    // Lets an ability run its own client-side visual code. See BirdAbility.OnNetworkEvent.
    public static void AbilityEvent(BirdAbility ability, int eventId, Vector3 argument = default)
    {
        if (!CanForward || !ability.TryGetComponent(out NetworkObject netObj)) return;

        int index = System.Array.IndexOf(ability.GetComponents<BirdAbility>(), ability);
        Instance.AbilityEventRpc(netObj, index, eventId, argument);
    }

    // Feedback from the managers

    public static void ForwardSound(Sound sound, float volume)
    {
        if (CanForward) Instance.PlaySoundRpc(sound, volume);
    }

    public static void ForwardBirdSound(BirdType birdType, SoundType soundType, float volume)
    {
        if (CanForward) Instance.PlayBirdSoundRpc(birdType, soundType, volume);
    }

    public static void ForwardStunLoop(GameObject bird, bool playing)
    {
        if (CanForward && bird.TryGetComponent(out NetworkObject netObj))
            Instance.SetStunLoopRpc(netObj, playing);
    }

    public static void ForwardHitEffect(HitEffects.HitType hitType, int playerIndex)
    {
        if (CanForward) Instance.PlayHitEffectRpc(hitType, playerIndex);
    }

    public static void ForwardHud(HudEvent hudEvent, int playerIndex, float first = 0f, float second = 0f, bool flag = false)
    {
        if (CanForward) Instance.HudEventRpc(hudEvent, playerIndex, first, second, flag);
    }

    // Starts the 3-2-1-GO on every machine at the same time, once the birds have been spawned.
    // The buffer gives this message time to reach everyone, and clients time to get through spawning the birds.
    public static void StartCountdownEverywhere()
    {
        if (CanForward) Instance.StartCountdownRpc(Instance.NetworkManager.ServerTime.Time + countdownBuffer);
    }

    // Called by NetworkFxMirror

    public int NextMirrorId() => nextMirrorId++;

    public void SendPrefabSpawn(int id, int prefabId, NetworkObjectReference parentObject, bool hasParentObject, int parentMirrorId,
        Vector3 position, Quaternion rotation, Vector3 scale)
    {
        SpawnPrefabVisualRpc(id, prefabId, parentObject, hasParentObject, parentMirrorId, position, rotation, scale);
    }

    public void SendLineSpawn(int id, LineRenderer line, Vector3[] points)
    {
        SpawnLineVisualRpc(id, line.startColor, line.endColor, line.startWidth, line.endWidth, line.enabled, points);
    }

    public void SendTransform(int id, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        UpdateVisualTransformRpc(id, position, rotation, scale);
    }

    public void SendLine(int id, bool visible, Vector3[] points)
    {
        UpdateLineVisualRpc(id, visible, points);
    }

    public void SendDestroy(int id)
    {
        DestroyVisualRpc(id);
    }

    // RPCs

    [Rpc(SendTo.NotServer)]
    private void SpawnPrefabVisualRpc(int id, int prefabId, NetworkObjectReference parentObject, bool hasParentObject, int parentMirrorId,
        Vector3 position, Quaternion rotation, Vector3 scale)
    {
        LoadRegistry();
        if (prefabId < 0 || prefabId >= registry.prefabs.Count) return;

        Transform parent = null;
        if (hasParentObject && parentObject.TryGet(out NetworkObject parentNetObj)) parent = parentNetObj.transform;
        else if (visuals.TryGetValue(parentMirrorId, out GameObject parentVisual) && parentVisual != null) parent = parentVisual.transform;

        visuals[id] = NetworkFxVisual.Create(registry.prefabs[prefabId], parent, position, rotation, scale);
    }

    [Rpc(SendTo.NotServer)]
    private void SpawnLineVisualRpc(int id, Color startColor, Color endColor, float startWidth, float endWidth, bool visible, Vector3[] points)
    {
        GameObject lineObject = new("NetworkFxLine");
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = startColor;
        line.endColor = endColor;
        line.startWidth = startWidth;
        line.endWidth = endWidth;
        line.enabled = visible;
        line.positionCount = points.Length;
        line.SetPositions(points);

        visuals[id] = lineObject;
    }

    [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
    private void UpdateVisualTransformRpc(int id, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (visuals.TryGetValue(id, out GameObject visual) && visual != null && visual.TryGetComponent(out NetworkFxVisual follower))
            follower.SetTarget(position, rotation, scale);
    }

    [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
    private void UpdateLineVisualRpc(int id, bool visible, Vector3[] points)
    {
        if (!visuals.TryGetValue(id, out GameObject visual) || visual == null || !visual.TryGetComponent(out LineRenderer line)) return;

        line.enabled = visible;
        line.positionCount = points.Length;
        line.SetPositions(points);
    }

    [Rpc(SendTo.NotServer)]
    private void DestroyVisualRpc(int id)
    {
        if (visuals.Remove(id, out GameObject visual) && visual != null)
            Destroy(visual);
    }

    [Rpc(SendTo.NotServer)]
    private void SetVisibleRpc(NetworkObjectReference target, bool visible)
    {
        if (!target.TryGet(out NetworkObject netObj)) return;
        GameObject obj = netObj.gameObject;

        if (visible)
        {
            if (hiddenRenderers.Remove(obj, out List<Renderer> renderers))
            {
                foreach (Renderer renderer in renderers)
                {
                    if (renderer != null) renderer.enabled = true;
                }
            }
            return;
        }

        if (hiddenRenderers.ContainsKey(obj)) return;

        // Only remember what was on, so that this doesn't turn on anything that was off before hiding
        List<Renderer> turnedOff = new();
        foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled) continue;
            renderer.enabled = false;
            turnedOff.Add(renderer);
        }
        hiddenRenderers[obj] = turnedOff;
    }

    [Rpc(SendTo.NotServer)]
    private void RetargetFollowersRpc(NetworkObjectReference from, NetworkObjectReference to)
    {
        if (!from.TryGet(out NetworkObject fromNetObj) || !to.TryGet(out NetworkObject toNetObj)) return;

        foreach (FollowObject follower in FindObjectsByType<FollowObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (follower.target == fromNetObj.transform)
                follower.target = toNetObj.transform;
        }
    }

    [Rpc(SendTo.NotServer)]
    private void AbilityEventRpc(NetworkObjectReference bird, int abilityIndex, int eventId, Vector3 argument)
    {
        if (!bird.TryGet(out NetworkObject netObj)) return;

        // The index can be out of range for an ability copied at runtime, which only exists on the host
        BirdAbility[] abilities = netObj.GetComponents<BirdAbility>();
        if (abilityIndex >= 0 && abilityIndex < abilities.Length)
            abilities[abilityIndex].OnNetworkEvent(eventId, argument);
    }

    [Rpc(SendTo.NotServer)]
    private void PlaySoundRpc(Sound sound, float volume)
    {
        switch (sound)
        {
            case Sound.Scoring: AudioManager.PlayScoringSound(volume); break;
            case Sound.BallPlayerInteraction: AudioManager.PlayBallPlayerInteractionSound(volume); break;
            case Sound.BallNetHit: AudioManager.PlayBallNetHitSound(volume); break;
            case Sound.BallGroundHit: AudioManager.PlayBallGroundHitSound(volume); break;
            case Sound.BuffStart: AudioManager.PlayBuffStartSound(volume); break;
            case Sound.BuffEnd: AudioManager.PlayBuffEndSound(volume); break;
            case Sound.DebuffStart: AudioManager.PlayDebuffStartSound(volume); break;
            case Sound.DebuffEnd: AudioManager.PlayDebuffEndSound(volume); break;
        }
    }

    [Rpc(SendTo.NotServer)]
    private void PlayBirdSoundRpc(BirdType birdType, SoundType soundType, float volume)
    {
        AudioManager.PlayBirdSound(birdType, soundType, volume);
    }

    [Rpc(SendTo.NotServer)]
    private void SetStunLoopRpc(NetworkObjectReference bird, bool playing)
    {
        if (!bird.TryGet(out NetworkObject netObj)) return;

        if (playing) AudioManager.PlayStunLoop(netObj.gameObject);
        else AudioManager.StopStunLoop(netObj.gameObject);
    }

    [Rpc(SendTo.NotServer)]
    private void PlayHitEffectRpc(HitEffects.HitType hitType, int playerIndex)
    {
        if (HitEffects.Instance != null) HitEffects.Instance.PlayEffect(hitType, playerIndex);
    }

    [Rpc(SendTo.NotServer)]
    private void HudEventRpc(HudEvent hudEvent, int playerIndex, float first, float second, bool flag)
    {
        HUDManager hud = HUDManager.Instance;
        if (hud == null) return;

        switch (hudEvent)
        {
            case HudEvent.TriggerOffensiveCooldown: hud.TriggerOffensiveCooldown(playerIndex, first); break;
            case HudEvent.TriggerDefensiveCooldown: hud.TriggerDefensiveCooldown(playerIndex, first); break;
            case HudEvent.UpdateOffensiveCooldown: hud.UpdateOffensiveCooldown(playerIndex, first, second); break;
            case HudEvent.UpdateDefensiveCooldown: hud.UpdateDefensiveCooldown(playerIndex, first, second); break;
            case HudEvent.ResetOffensiveCooldown: hud.ResetOffensiveCooldown(playerIndex); break;
            case HudEvent.ResetDefensiveCooldown: hud.ResetDefensiveCooldown(playerIndex); break;
            case HudEvent.SetOffensiveArmed: hud.SetOffensiveArmed(playerIndex, flag, second > 0f); break;
            case HudEvent.SetDefensiveIconForBird: hud.SetDefensiveIconForBird(playerIndex, (BirdType)(int)first); break;
            case HudEvent.RefreshPlayerCard: hud.RefreshPlayerCard(playerIndex); break;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StartCountdownRpc(double startTime)
    {
        countdown countdownScript = FindFirstObjectByType<countdown>();
        if (countdownScript != null) countdownScript.StartCountdownAt(startTime);
    }

    // Registry

    private static void LoadRegistry()
    {
        if (registry != null) return;

        registry = Resources.Load<NetworkFxRegistry>(NetworkFxRegistry.ResourcePath);
        if (registry == null) registry = ScriptableObject.CreateInstance<NetworkFxRegistry>();

        prefabIds = new Dictionary<GameObject, int>();
        for (int i = 0; i < registry.prefabs.Count; ++i)
        {
            if (registry.prefabs[i] != null) prefabIds[registry.prefabs[i]] = i;
        }
    }

    private static int GetPrefabId(GameObject prefab)
    {
        LoadRegistry();
        return prefabIds.TryGetValue(prefab, out int id) ? id : -1;
    }
}

/// <summary>
/// Added to the host's copy of an object to keep the matching copy on each client up to date.
/// </summary>
public class NetworkFxMirror : MonoBehaviour
{
    private const float sendInterval = 0.05f;

    private int mirrorId;
    private int prefabId = -1;
    private bool sentSpawn;
    private float nextSendTime;

    private LineRenderer line;
    private Vector3[] linePoints;
    private bool lastLineVisible;

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastScale;

    public int MirrorId => mirrorId;
    public bool SentSpawn => sentSpawn;

    public void BeginPrefab(int id)
    {
        prefabId = id;
        mirrorId = NetworkFx.Instance.NextMirrorId();
    }

    public void BeginLine()
    {
        line = GetComponent<LineRenderer>();
        mirrorId = NetworkFx.Instance.NextMirrorId();
    }

    // The spawn message waits until LateUpdate since most abilities adjust an object just after instantiating it
    void LateUpdate()
    {
        if (NetworkFx.Instance == null || !NetworkFx.Instance.IsSpawned) return;

        if (!sentSpawn)
        {
            TrySendSpawn();
            return;
        }

        if (Time.unscaledTime < nextSendTime) return;
        nextSendTime = Time.unscaledTime + sendInterval;

        if (line != null) SendLineIfChanged();
        else SendTransformIfChanged();
    }

    private void TrySendSpawn()
    {
        if (line != null)
        {
            linePoints = ReadLinePoints();
            lastLineVisible = line.enabled;
            NetworkFx.Instance.SendLineSpawn(mirrorId, line, linePoints);
            sentSpawn = true;
            return;
        }

        Transform parent = transform.parent;
        NetworkObjectReference parentObject = default;
        bool hasParentObject = false;
        int parentMirrorId = 0;

        if (parent != null)
        {
            if (parent.TryGetComponent(out NetworkFxMirror parentMirror))
            {
                if (!parentMirror.SentSpawn) return; // The client needs the parent first
                parentMirrorId = parentMirror.MirrorId;
            }
            else if (parent.TryGetComponent(out NetworkObject parentNetObj) && parentNetObj.IsSpawned)
            {
                parentObject = parentNetObj;
                hasParentObject = true;
            }
        }

        bool useLocalSpace = hasParentObject || parentMirrorId != 0;
        ReadTransform(useLocalSpace, out lastPosition, out lastRotation, out lastScale);
        NetworkFx.Instance.SendPrefabSpawn(mirrorId, prefabId, parentObject, hasParentObject, parentMirrorId, lastPosition, lastRotation, lastScale);
        sentSpawn = true;
    }

    private void SendTransformIfChanged()
    {
        bool useLocalSpace = transform.parent != null
            && (transform.parent.TryGetComponent<NetworkFxMirror>(out _) || transform.parent.TryGetComponent<NetworkObject>(out _));

        ReadTransform(useLocalSpace, out Vector3 position, out Quaternion rotation, out Vector3 scale);
        if (position == lastPosition && rotation == lastRotation && scale == lastScale) return;

        lastPosition = position;
        lastRotation = rotation;
        lastScale = scale;
        NetworkFx.Instance.SendTransform(mirrorId, position, rotation, scale);
    }

    private void SendLineIfChanged()
    {
        Vector3[] points = ReadLinePoints();
        bool changed = line.enabled != lastLineVisible || points.Length != linePoints.Length;

        for (int i = 0; !changed && i < points.Length; ++i)
            changed = points[i] != linePoints[i];

        if (!changed) return;

        linePoints = points;
        lastLineVisible = line.enabled;
        NetworkFx.Instance.SendLine(mirrorId, line.enabled, points);
    }

    private Vector3[] ReadLinePoints()
    {
        Vector3[] points = new Vector3[line.positionCount];
        line.GetPositions(points);
        return points;
    }

    private void ReadTransform(bool useLocalSpace, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        position = useLocalSpace ? transform.localPosition : transform.position;
        rotation = useLocalSpace ? transform.localRotation : transform.rotation;
        scale = transform.localScale;
    }

    void OnDestroy()
    {
        if (sentSpawn && NetworkFx.Instance != null && NetworkFx.Instance.IsSpawned)
            NetworkFx.Instance.SendDestroy(mirrorId);
    }
}

/// <summary>
/// The client's copy of a host object: all of the visuals and none of the gameplay.
/// </summary>
public class NetworkFxVisual : MonoBehaviour
{
    private const float followSpeed = 18f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;

    public static GameObject Create(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        // Instantiating under an inactive parent lets the gameplay scripts be removed before Awake can run
        GameObject holder = new("NetworkFxHolder");
        holder.SetActive(false);

        GameObject visual = Object.Instantiate(prefab, holder.transform);
        StripGameplay(visual);

        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = position;
        visual.transform.localRotation = rotation;
        visual.transform.localScale = scale;

        NetworkFxVisual follower = visual.AddComponent<NetworkFxVisual>();
        follower.SetTarget(position, rotation, scale);

        Destroy(holder);
        return visual;
    }

    private static void StripGameplay(GameObject visual)
    {
        MonoBehaviour[] behaviours = visual.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = behaviours.Length - 1; i >= 0; --i)
        {
            // UI and text components are how some of these objects draw themselves
            string typeNamespace = behaviours[i].GetType().Namespace ?? string.Empty;
            if (typeNamespace.StartsWith("TMPro") || typeNamespace.StartsWith("UnityEngine.UI")) continue;

            DestroyImmediate(behaviours[i]);
        }

        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (Rigidbody rb in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }

    public void SetTarget(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        targetPosition = position;
        targetRotation = rotation;
        targetScale = scale;
    }

    void Update()
    {
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, t);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, t);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
    }
}

public static class AnimatorNetworkExtensions
{
    // Use this instead of Animator.SetTrigger on anything spawned. NetworkAnimator syncs an Animator's
    // bools and floats on its own, but only sends a trigger if the trigger is set through it.
    public static void SetTriggerSynced(this Animator animator, string trigger)
    {
        NetworkAnimator networkAnimator = NetworkSession.IsServer ? animator.GetComponentInParent<NetworkAnimator>() : null;

        if (networkAnimator != null && networkAnimator.IsSpawned) networkAnimator.SetTrigger(trigger);
        else animator.SetTrigger(trigger);
    }
}
