using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EndScreen : MonoBehaviour
{
    public RawImage readyIndicator; // Ready indicator for this player on the end game screen
    private PlayerInput playerInput; // Input for the player

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    // Update is called once per frame
    void Update()
    {
        // If game has ended, indicator is not enabled, and player just pressed trigger
        if (GameManager.Instance.gameState == GameManager.GameState.GameOver && !readyIndicator.enabled
            && playerInput.actions.FindAction("Offensive Action").WasPressedThisFrame())
        {
            // Indicate that the player is readied on both screen to score manager
            readyIndicator.enabled = true;
            if (gameObject == GameManager.Instance.leftPlayer1)
            {
                ScoreManager.Instance.readiedUp[0] = true;
            }
            else if (gameObject == GameManager.Instance.leftPlayer2)
            {
                ScoreManager.Instance.readiedUp[1] = true;
            }
            else if (gameObject == GameManager.Instance.rightPlayer1)
            {
                ScoreManager.Instance.readiedUp[2] = true;
            }
            else if (gameObject == GameManager.Instance.rightPlayer2)
            {
                ScoreManager.Instance.readiedUp[3] = true;
            }
            
            // See if this player was the last one to ready up
            ScoreManager.Instance.CheckReturnToMenu();
        }
    }
}
