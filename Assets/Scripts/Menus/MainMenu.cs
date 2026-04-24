using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string characterSelectSceneName = "AlexaCharSelect";

    [Header("Credits")]
    [SerializeField] private GameObject creditsCanvas; //
    [SerializeField] private Animator creditsAnimator; // Animator on the scrolling text
    [SerializeField] private float creditsDuration = 30f;

    private bool isShowingCredits = false;

    public void PlayButton()
    {
        SceneManager.LoadScene(characterSelectSceneName);
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void CreditsButton()
    {
        if (isShowingCredits) return;

        StartCoroutine(PlayCredits());
    }

    private IEnumerator PlayCredits()
    {
        isShowingCredits = true;

        // Enable the credits canvas
        creditsCanvas.SetActive(true);

        // Start the animation
        if (creditsAnimator != null)
        {
            creditsAnimator.Play("ScrollCredits", 0, 0f);
        }

        // Wait for the credits to finish
        yield return new WaitForSeconds(creditsDuration);

        // Hide credits and return to menu state
        creditsCanvas.SetActive(false);

        isShowingCredits = false;
    }
}