using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the prefab and scene setup that online play needs. Running it more than once is fine, so
/// just run it again after adding a new bird or an ability with a new prefab (BIRDBRAIN > Apply Network Setup).
/// </summary>
public static class NetworkSetupTool
{
    private const string birdPrefabFolder = "Assets/Characters/AnimatedPrefabs";
    private const string abilityPrefabFolder = "Assets/Prefab/AbilityPrefabs";
    private const string networkPrefabListPath = "Assets/DefaultNetworkPrefabs.asset";
    private const string networkManagerPrefabPath = "Assets/Prefab/Managers/Network Manager.prefab";
    private const string gameManagerPrefabPath = "Assets/Prefab/Managers/GameManager.prefab";
    private const string registryPath = "Assets/Resources/NetworkFxRegistry.asset";
    private const string mainMenuScenePath = "Assets/Scenes/Main/MainMenu.unity";
    private const string networkingScenePath = "Assets/Scenes/Roderic/Networking.unity";
    private const string characterSelectScenePath = "Assets/Scenes/Main/CharSelect.unity";

    private static readonly string[] gameScenePaths =
    {
        "Assets/Scenes/Main/Game_Beach.unity",
        "Assets/Scenes/Main/Game_Forest.unity",
        "Assets/Scenes/Main/Game_Iceberg.unity",
        "Assets/Scenes/Main/Game_Volcano.unity"
    };

    // Spawned by abilities and simulated by the host, like the birds themselves
    private static readonly string[] extraNetworkedPrefabNames = { "Mini31rd", "dodoaibuddy1", "dodoaibuddy2" };

    [MenuItem("BIRDBRAIN/Apply Network Setup")]
    public static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string startingScene = SceneManager.GetActiveScene().path;

        List<string> networkedPrefabPaths = FindNetworkedPrefabPaths();
        foreach (string path in networkedPrefabPaths)
            SetUpNetworkedPrefab(path);

        // Must come after saving, since a NetworkObject only gets its id once it is validated on the prefab asset
        foreach (string path in networkedPrefabPaths)
            AssignPrefabHash(path);

        SetUpManagerPrefabs();
        UpdateNetworkPrefabList(networkedPrefabPaths);
        UpdateFxRegistry(networkedPrefabPaths);

        foreach (string path in gameScenePaths)
            SetUpGameScene(path);

        RestoreHudReferences(gameScenePaths[1], gameScenePaths[0]);

        SetUpMenuScene(mainMenuScenePath, false);
        SetUpMenuScene(networkingScenePath, true);
        SetUpCharacterSelectScene();
        PutMainMenuFirstInBuild();

        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(startingScene) && !Application.isBatchMode)
            EditorSceneManager.OpenScene(startingScene);

        Debug.Log($"[NetworkSetupTool] Finished. {networkedPrefabPaths.Count} networked prefabs set up.");
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
        EditorApplication.Exit(0);
    }

    // Prefabs

    // The controller (C) and AI versions of each bird, and what abilities spawn with network components.
    // KBM versions are skipped because online players all use the controller version with any device.
    private static List<string> FindNetworkedPrefabPaths()
    {
        List<string> paths = new();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { birdPrefabFolder, abilityPrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string prefabName = Path.GetFileNameWithoutExtension(path);

            if (extraNetworkedPrefabNames.Contains(prefabName))
            {
                paths.Add(path);
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool isBird = prefab.GetComponent<CharacterMovement>() != null || prefab.GetComponent<AIBehavior>() != null;

            if (isBird && (prefabName.EndsWith("C") || prefabName.EndsWith("AI")))
                paths.Add(path);
        }

        paths.Sort();
        return paths;
    }

    private static void SetUpNetworkedPrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            AddIfMissing<NetworkObject>(root);
            AddIfMissing<NetworkTransform>(root);

            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                NetworkAnimator networkAnimator = AddIfMissing<NetworkAnimator>(root);
                SerializedObject serialized = new(networkAnimator);
                serialized.FindProperty("m_Animator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            // Humans send their input through this. An AI or companion is driven by the host instead.
            // Phoenix's chicken has an AIBehavior too, but it stands in for humans as well, and it's the only
            // prefab with both. PhoenixOffensive turns this off on the chickens that AI birds get.
            bool isHumanBird = root.GetComponent<CharacterMovement>() != null
                && root.GetComponent<BallInteract>() != null;
            if (isHumanBird) AddIfMissing<PlayerNetworkController>(root);

            AddIfMissing<NetworkClientLogicGuard>(root);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AssignPrefabHash(string path)
    {
        NetworkObject netObj = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NetworkObject>();

        typeof(NetworkObject)
            .GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(netObj, null);

        if (netObj.PrefabIdHash == 0)
            Debug.LogError($"[NetworkSetupTool] {path} did not get a network id, so it can't be spawned.");
    }

    private static void SetUpManagerPrefabs()
    {
        EditPrefab(gameManagerPrefabPath, root => AddIfMissing<NetworkFx>(root));
        EditPrefab(networkManagerPrefabPath, root => AddIfMissing<NetworkManagerGuard>(root));
    }

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpdateNetworkPrefabList(List<string> networkedPrefabPaths)
    {
        NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(networkPrefabListPath);
        HashSet<GameObject> existing = new(list.PrefabList.Select(entry => entry.Prefab));

        foreach (string path in networkedPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!existing.Contains(prefab)) list.Add(new NetworkPrefab { Prefab = prefab });
        }

        EditorUtility.SetDirty(list);
    }

    // Finds every prefab reachable from the birds and BuffsDebuffs so NetworkFx can refer to them by index.
    private static void UpdateFxRegistry(List<string> networkedPrefabPaths)
    {
        HashSet<GameObject> networked = new(networkedPrefabPaths.Select(AssetDatabase.LoadAssetAtPath<GameObject>));
        HashSet<GameObject> found = new();
        Queue<GameObject> toScan = new();

        // KBM birds use the same abilities, but those references are all also on the controller versions
        foreach (GameObject prefab in networked) toScan.Enqueue(prefab);

        // BuffsDebuffs lives in the scene rather than on a prefab
        foreach (string scenePath in gameScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (BuffsDebuffs buffs in root.GetComponentsInChildren<BuffsDebuffs>(true))
                    CollectPrefabReferences(buffs, networked, found, toScan);
            }
        }

        while (toScan.Count > 0)
        {
            foreach (MonoBehaviour behaviour in toScan.Dequeue().GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Assembly.GetName().Name == "Assembly-CSharp")
                    CollectPrefabReferences(behaviour, networked, found, toScan);
            }
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        NetworkFxRegistry registry = AssetDatabase.LoadAssetAtPath<NetworkFxRegistry>(registryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<NetworkFxRegistry>();
            AssetDatabase.CreateAsset(registry, registryPath);
        }

        // Keep the order stable so that adding a prefab doesn't reshuffle the whole file
        registry.prefabs = found.OrderBy(AssetDatabase.GetAssetPath).ToList();
        EditorUtility.SetDirty(registry);
    }

    private static void CollectPrefabReferences(MonoBehaviour behaviour, HashSet<GameObject> networked, HashSet<GameObject> found, Queue<GameObject> toScan)
    {
        SerializedProperty property = new SerializedObject(behaviour).GetIterator();

        while (property.NextVisible(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

            GameObject prefab = property.objectReferenceValue switch
            {
                GameObject gameObject => gameObject,
                Component component => component.gameObject,
                _ => null
            };

            if (prefab == null || !EditorUtility.IsPersistent(prefab) || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
                continue;

            prefab = prefab.transform.root.gameObject;

            if (!networked.Contains(prefab) && found.Add(prefab))
                toScan.Enqueue(prefab);
        }
    }

    // Scenes

    private static void SetUpGameScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        BallManager ball = FindInScene<BallManager>(scene);
        AddIfMissing<NetworkObject>(ball.gameObject);
        AddIfMissing<NetworkTransform>(ball.gameObject);
        AddIfMissing<NetworkClientLogicGuard>(ball.gameObject);

        // TestNetworkSpawn used to be on the ball, and its script has been deleted
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(ball.gameObject);

        // MultiplayerManager sends RPCs, which only work from a spawned object
        AddIfMissing<NetworkObject>(FindInScene<MultiplayerManager>(scene).gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Game_Beach uses the MultiplayerManager prefab, and a prefab can't keep references to scene objects, so
    // its HUD has no player cards. All of the game scenes share the same UI, so this finds the same objects
    // that Game_Forest's HUD references and assigns any reference that is empty. Assigned as prefab
    // overrides, which is why this has to be done per scene.
    private static void RestoreHudReferences(string sourceScenePath, string targetScenePath)
    {
        Scene sourceScene = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Single);
        SerializedProperty sourceProperty = new SerializedObject(FindInScene<HUDManager>(sourceScene)).GetIterator();

        List<(string propertyPath, string objectPath, System.Type type)> references = new();
        while (sourceProperty.NextVisible(true))
        {
            if (sourceProperty.propertyType != SerializedPropertyType.ObjectReference) continue;

            Object value = sourceProperty.objectReferenceValue;
            GameObject sceneObject = value is GameObject gameObject ? gameObject : (value as Component)?.gameObject;
            if (sceneObject == null || EditorUtility.IsPersistent(sceneObject)) continue;

            references.Add((sourceProperty.propertyPath, GetHierarchyPath(sceneObject.transform), value.GetType()));
        }

        Scene targetScene = EditorSceneManager.OpenScene(targetScenePath, OpenSceneMode.Single);
        SerializedObject targetHud = new(FindInScene<HUDManager>(targetScene));

        foreach ((string propertyPath, string objectPath, System.Type type) in references)
        {
            SerializedProperty property = targetHud.FindProperty(propertyPath);
            if (property == null || property.objectReferenceValue != null) continue;

            // Some duplicated objects are numbered differently from scene to scene, like "ArmOffensive2 (1)"
            GameObject match = FindByHierarchyPath(targetScene, objectPath)
                ?? FindByHierarchyPath(targetScene, System.Text.RegularExpressions.Regex.Replace(objectPath, @" \(\d+\)$", ""));

            if (match == null)
            {
                Debug.LogWarning($"[NetworkSetupTool] {targetScene.name} has no {objectPath} for the HUD's {propertyPath}.");
                continue;
            }

            property.objectReferenceValue = type == typeof(GameObject) ? match : match.GetComponent(type);
        }

        targetHud.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        return transform.parent == null ? transform.name : GetHierarchyPath(transform.parent) + "/" + transform.name;
    }

    private static GameObject FindByHierarchyPath(Scene scene, string path)
    {
        string[] names = path.Split('/');

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != names[0]) continue;

            Transform found = names.Length == 1 ? root.transform : root.transform.Find(string.Join("/", names.Skip(1)));
            if (found != null) return found.gameObject;
        }

        return null;
    }

    private static void SetUpMenuScene(string path, bool autoStartWhenFull)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        // Birds are spawned by MultiplayerManager with the player's chosen bird, so NGO shouldn't add one of its own
        NetworkManager manager = FindInScene<NetworkManager>(scene);
        AddIfMissing<NetworkManagerGuard>(manager.gameObject);
        SerializedObject serializedManager = new(manager);
        serializedManager.FindProperty("NetworkConfig.PlayerPrefab").objectReferenceValue = null;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();

        StartNetworkedLobbyForNumPlayers lobby = FindInScene<StartNetworkedLobbyForNumPlayers>(scene);
        if (lobby == null)
        {
            GameObject lobbyObject = new("Online Lobby");
            lobbyObject.AddComponent<NetworkObject>();
            lobby = lobbyObject.AddComponent<StartNetworkedLobbyForNumPlayers>();
            lobby.numHumanPlayers = 4;
        }
        lobby.autoStartWhenFull = autoStartWhenFull;
        EditorUtility.SetDirty(lobby);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // The lobby object in the main menu goes away with that scene, so this scene has its own for online games.
    private static void SetUpCharacterSelectScene()
    {
        Scene scene = EditorSceneManager.OpenScene(characterSelectScenePath, OpenSceneMode.Single);

        if (FindInScene<StartNetworkedLobbyForNumPlayers>(scene) == null)
        {
            GameObject lobbyObject = new("Online Lobby");
            lobbyObject.AddComponent<NetworkObject>();
            lobbyObject.AddComponent<StartNetworkedLobbyForNumPlayers>().numHumanPlayers = 4;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void PutMainMenuFirstInBuild()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        EditorBuildSettingsScene mainMenu = scenes.FirstOrDefault(scene => scene.path == mainMenuScenePath);
        if (mainMenu == null) return;

        scenes.Remove(mainMenu);
        scenes.Insert(0, mainMenu);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    private static T AddIfMissing<T>(GameObject target) where T : Component
    {
        return target.TryGetComponent(out T existing) ? existing : target.AddComponent<T>();
    }
}
