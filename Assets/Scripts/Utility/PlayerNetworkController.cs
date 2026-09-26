using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerNetworkController : NetworkBehaviour
{
    /// <summary>
    /// Struct for player input data.
    /// ServerRPCs don't know how to deserialize custom structs, so you must explicitly tell it how to.
    /// </summary>
    struct PlayerInputData : INetworkSerializable
    {
        public Vector2 move;
        public bool jumpPressed;
        public bool defActionPressed;
        public bool offActionPressed;
        public bool blockPressed;
        public Vector2 interactionDirection;
        public bool pausePressed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref move);
            serializer.SerializeValue(ref jumpPressed);
            serializer.SerializeValue(ref defActionPressed);
            serializer.SerializeValue(ref offActionPressed);
            serializer.SerializeValue(ref blockPressed);
            serializer.SerializeValue(ref interactionDirection);
            serializer.SerializeValue(ref pausePressed);
        }
    }

    private CharacterMovement characterMovement;
    private PlayerInput input;
    private BallInteract ballInteract;

    public override void OnNetworkSpawn()
    {        
        characterMovement = GetComponent<CharacterMovement>();
        input = GetComponent<PlayerInput>();
        ballInteract = GetComponent<BallInteract>();

        // REMOVE THIS ONCE CONTROLLER ASSIGNMENT HAS BEEN NETWORKED
        if (Gamepad.all.Count > 0)
        {
            input.SwitchCurrentControlScheme(Gamepad.all[input.playerIndex]);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // If not the owner of this networked object, don't do anything
        if (!IsOwner) return;

        PlayerInputData inputData = ReadInput();
        SendInputServerRpc(inputData);
    }

    /// <summary>
    /// Send client information over to server for processing.
    /// </summary>
    /// <param name="inputData">Input data for the client</param>
    [ServerRpc]
    void SendInputServerRpc(PlayerInputData inputData)
    {
        // Character movement 
        characterMovement.SetInput(inputData.move);
        if (inputData.jumpPressed) characterMovement.Jump();

        // Character interaction
        if (inputData.defActionPressed) ballInteract.DefActionPressed();
        if (inputData.offActionPressed) ballInteract.OffActionPressed();
        if (inputData.blockPressed) ballInteract.BlockPressed();
        ballInteract.SetDirection(inputData.interactionDirection);

        // UI interaction
        if (inputData.pausePressed && !PauseMenu.Instance.GameIsPaused)
        {
            PauseMenu.Instance.pausedPlayerID = ballInteract.playerID;
            PauseMenu.Instance.inputModule.actionsAsset = input.actions;
            PauseMenu.Instance.Pause();
        }
        else if (inputData.pausePressed && PauseMenu.Instance.GameIsPaused && PauseMenu.Instance.pausedPlayerID == ballInteract.playerID)
            PauseMenu.Instance.Resume();
    }


    /// <summary>
    /// Reads the player input component and assigns variables accordingly.
    /// </summary>
    /// <returns></returns>
    private PlayerInputData ReadInput()
    {
        return new PlayerInputData
        {
            move = input.actions["Move"].ReadValue<Vector2>(),
            jumpPressed = input.actions["Jump"].WasPressedThisFrame(),
            defActionPressed = input.actions["Defensive Action"].WasPressedThisFrame(),
            offActionPressed = input.actions["Offensive Action"].WasPressedThisFrame(),
            blockPressed = input.actions["Block"].WasPressedThisFrame(),
            interactionDirection = input.actions["Direction"].ReadValue<Vector2>(),
            pausePressed = input.actions["Pause"].WasPressedThisFrame()
        };
    }
}
