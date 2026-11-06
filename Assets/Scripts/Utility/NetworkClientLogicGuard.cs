using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The host runs all gameplay, so clients only need to display the result. On clients this turns off the
/// logic and physics of a spawned object, leaving its transform to be driven by the NetworkTransform.
/// Goes on every spawned bird, on the ball, and on anything else simulated by the host.
/// </summary>
public class NetworkClientLogicGuard : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Online input goes through PlayerNetworkController, on every machine. Left enabled, these would
        // fight over devices and PlayerInput's messages would fire abilities outside of the input RPC.
        if (TryGetComponent(out PlayerInput playerInput)) playerInput.enabled = false;
        if (TryGetComponent(out PukekoScript pukekoScript)) pukekoScript.enabled = false;

        if (IsServer) return;

        foreach (BirdAbility ability in GetComponentsInChildren<BirdAbility>(true)) ability.enabled = false;
        foreach (PassiveAbility ability in GetComponentsInChildren<PassiveAbility>(true)) ability.enabled = false;
        foreach (BirdAbilityController controller in GetComponentsInChildren<BirdAbilityController>(true)) controller.enabled = false;
        foreach (AIBehavior ai in GetComponentsInChildren<AIBehavior>(true)) ai.enabled = false;
        foreach (CharacterMovement movement in GetComponentsInChildren<CharacterMovement>(true)) movement.enabled = false;
        foreach (BallInteract ballInteract in GetComponentsInChildren<BallInteract>(true)) ballInteract.enabled = false;
        foreach (DodoCompanion companion in GetComponentsInChildren<DodoCompanion>(true)) companion.enabled = false;

        // Physics callbacks still reach disabled scripts, so collisions have to be off as well
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }
}
