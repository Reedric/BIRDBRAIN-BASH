using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerNetworkController : NetworkBehaviour
{
    private const float minSendInterval = 1f / 30f; // How often input is sent, at most, in seconds

    /// <summary>
    /// Struct for player input data.
    /// ServerRPCs don't know how to deserialize custom structs, so you must explicitly tell it how to.
    /// </summary>
    struct PlayerInputData : INetworkSerializable
    {
        public Vector2 move;
        public bool jumpHeld;
        public bool defActionPressed;
        public bool offActionPressed;
        public bool blockPressed;
        public bool defAbilityPressed;
        public bool offAbilityPressed;
        public Vector2 interactionDirection;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref move);
            serializer.SerializeValue(ref jumpHeld);
            serializer.SerializeValue(ref defActionPressed);
            serializer.SerializeValue(ref offActionPressed);
            serializer.SerializeValue(ref blockPressed);
            serializer.SerializeValue(ref defAbilityPressed);
            serializer.SerializeValue(ref offAbilityPressed);
            serializer.SerializeValue(ref interactionDirection);
        }

        // Button presses only last one frame, so they cannot be dropped while waiting for the next send
        public bool HasButtonPress => defActionPressed || offActionPressed || blockPressed || defAbilityPressed || offAbilityPressed;

        public bool SameHeldInputAs(PlayerInputData other)
        {
            return move == other.move && jumpHeld == other.jumpHeld && interactionDirection == other.interactionDirection;
        }
    }

    private CharacterMovement characterMovement;
    private BallInteract ballInteract;
    private BirdAbilityInput birdAbilityInput;
    private EndScreen endScreen;

    private InputActionAsset actions; // This machine's copy of the input actions (owner only)
    private PlayerInputData pendingInput; // Input read since the last send
    private PlayerInputData lastSentInput;
    private float nextSendTime;

    // A client can own two of these at once (its hidden bird and Phoenix's chicken standing in for it),
    // and both see the same press of pause. Without this they'd open and then close the menu in one frame.
    private static int lastPauseToggleFrame = -1;

    public override void OnNetworkSpawn()
    {
        characterMovement = GetComponent<CharacterMovement>();
        ballInteract = GetComponent<BallInteract>();
        birdAbilityInput = GetComponent<BirdAbilityInput>();
        endScreen = GetComponent<EndScreen>();

        if (!IsOwner) return;

        // Online, each machine has one player, who can use whichever device they like. So rather than pairing
        // devices through PlayerInput, the owner reads from their own copy of the actions, which listens to all of them.
        actions = Instantiate(GetComponent<PlayerInput>().actions);
        actions.FindActionMap("Player").Enable();
        actions["Pause"].Enable(); // Pause is in the UI map, which online pausing doesn't otherwise need
    }

    public override void OnNetworkDespawn()
    {
        if (actions != null) Destroy(actions);
    }

    // When a player disconnects, the host takes over their bird, so stop whatever it was last told to do.
    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        if (!IsServer || current != NetworkManager.ServerClientId) return;

        if (characterMovement != null)
        {
            characterMovement.SetInput(Vector2.zero);
            characterMovement.SetJumpHeld(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // If not the owner of this networked object, don't do anything
        if (!IsOwner || actions == null) return;

        if (actions["Pause"].WasPressedThisFrame() && PauseMenu.Instance != null && lastPauseToggleFrame != Time.frameCount)
        {
            lastPauseToggleFrame = Time.frameCount;
            PauseMenu.Instance.ToggleOnlinePause();
        }

        // Pausing online only opens the menu for this player, so the bird has to stop moving
        bool paused = PauseMenu.Instance != null && PauseMenu.Instance.GameIsPaused;
        ReadInput(paused);

        bool heldInputChanged = !pendingInput.SameHeldInputAs(lastSentInput);
        if (!pendingInput.HasButtonPress && !heldInputChanged) return;
        if (!pendingInput.HasButtonPress && Time.unscaledTime < nextSendTime) return;

        SendInputServerRpc(pendingInput);

        lastSentInput = pendingInput;
        pendingInput = default;
        nextSendTime = Time.unscaledTime + minSendInterval;
    }

    /// <summary>
    /// Send client information over to server for processing.
    /// </summary>
    /// <param name="inputData">Input data for the client</param>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void SendInputServerRpc(PlayerInputData inputData)
    {
        // Phoenix's Burn Ball deactivates the bird while its owner controls a chicken instead
        if (!gameObject.activeInHierarchy) return;

        // Character movement
        if (characterMovement != null)
        {
            characterMovement.SetInput(inputData.move);
            characterMovement.SetJumpHeld(inputData.jumpHeld);
        }

        // Character interaction
        if (ballInteract != null)
        {
            if (inputData.defActionPressed) ballInteract.DefActionPressed();
            if (inputData.offActionPressed) ballInteract.OffActionPressed();
            if (inputData.blockPressed) ballInteract.BlockPressed();
            ballInteract.SetDirection(inputData.interactionDirection);
        }

        // Ability interaction. Phoenix's chicken has no abilities, so it has no BirdAbilityInput either
        if (birdAbilityInput != null)
        {
            if (inputData.defAbilityPressed) birdAbilityInput.ActivateDefensive();
            if (inputData.offAbilityPressed) birdAbilityInput.ActivateOffensive();
        }

        // End of game ready up
        if (inputData.offActionPressed && endScreen != null
            && GameManager.Instance.gameState == GameManager.GameState.GameOver)
            endScreen.ReadyUp();
    }

    /// <summary>
    /// Reads the actions and adds them to the input waiting to be sent.
    /// </summary>
    private void ReadInput(bool paused)
    {
        if (paused)
        {
            pendingInput.move = Vector2.zero;
            pendingInput.jumpHeld = false;
            pendingInput.interactionDirection = Vector2.zero;
            return;
        }

        pendingInput.move = actions["Move"].ReadValue<Vector2>();
        pendingInput.jumpHeld = actions["Jump"].IsPressed();
        pendingInput.interactionDirection = actions["Direction"].ReadValue<Vector2>();
        pendingInput.defActionPressed |= actions["Defensive Action"].WasPressedThisFrame();
        pendingInput.offActionPressed |= actions["Offensive Action"].WasPressedThisFrame();
        pendingInput.blockPressed |= actions["Block"].WasPressedThisFrame();
        pendingInput.defAbilityPressed |= actions["Defensive Ability"].WasPressedThisFrame();
        pendingInput.offAbilityPressed |= actions["Offensive Ability"].WasPressedThisFrame();
    }
}
