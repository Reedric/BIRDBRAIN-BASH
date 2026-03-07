using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerSelectMenu : MonoBehaviour
{
    [Header("Player Select Menu")]
    [SerializeField] private GameObject playerSelectMenu;

    [Header("Input Select Menu")]
    [SerializeField] private GameObject inputSelectMenu;

    private int numPlayers;
    private List<bool> isKBMInput = new List<bool>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (playerSelectMenu == null)
        {
            Debug.LogError("You need to assign the player select menu in the inspector for PlayerSelectMenu.cs");
        }

        if (inputSelectMenu == null)
        {
            Debug.LogError("You need to assign the input select menu in the inspector for PlayerSelectMenu.cs");
        }
    }

    public void OnePlayer()
    {
        // Set number of players to 1
        numPlayers = 1;

        // Transition to Input Select Menu
        ShowInputMenu();
    }

    public void TwoPlayer()
    {
        // Set number of players to 2
        numPlayers = 2;

        // Transition to Input Select Menu
        ShowInputMenu();
    }

    public void ThreePlayer()
    {
        // Set number of players to 3
        numPlayers = 3;

        // Transition to Input Select Menu
        ShowInputMenu();
    }

    public void FourPlayer()
    {
        // Set number of players to 4
        numPlayers = 4;

        // Transition to Input Select Menu
        ShowInputMenu();
    }

    private void ShowInputMenu()
    {
        // Hide the Player Select Menu
        playerSelectMenu.SetActive(false);

        // Set the title for player one
        inputSelectMenu.transform.Find("Title").gameObject.GetComponent<TMP_Text>().text = "Player 1 Input";

        // Show the Input Select Menu
        inputSelectMenu.SetActive(true);
    }

    public void AddKeyboardInput()
    {
        // Add a keyboard input to the list of inputs
        isKBMInput.Add(true);

        // Disable the keyboard option
        inputSelectMenu.transform.Find("Keyboard").gameObject.GetComponent<Button>().interactable = false;

        // Check to see if all players have input
        CheckAllPlayersAssigned();
    }

    public void AddControllerInput()
    {
        // Add a controller input to the list of inputs
        isKBMInput.Add(false);

        // Check to see fi all players have input
        CheckAllPlayersAssigned();
    }

    private void CheckAllPlayersAssigned()
    {
        // If all players have been assigned an input, go to start the game
        if (isKBMInput.Count == numPlayers)
        {
            // Hide the input select menu
            inputSelectMenu.SetActive(false);
            
            // Share the input information with the multiplayer manager
            DataTransferManager.isKBMInput = isKBMInput;

            // Go to the game scene
            SceneManager.LoadScene("RodericM2");
        }
        else
        {
            // Change the title for the next player
            inputSelectMenu.transform.Find("Title").gameObject.GetComponent<TMP_Text>().text = $"Player {isKBMInput.Count + 1} Input";
        }
    }
}
