using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string characterSelectSceneName = "AlexaCharSelect";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayButton()
    {
        // Load the character select scene
        SceneManager.LoadScene(characterSelectSceneName);
    }

    public void Quit()
    {
        // Quit the game
        Application.Quit();
    }
}
