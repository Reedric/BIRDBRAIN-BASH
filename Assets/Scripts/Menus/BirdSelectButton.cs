using UnityEngine;
using UnityEngine.UI;


// Attached to each bird selection button on the character select canvas.
// When any player's cursor clicks this button, it registers their bird selection.
public class BirdSelectButton : MonoBehaviour
{
    // The index of the bird this button represents in CharacterSelectManager.availableBirds
    [SerializeField] private int birdIndex = 0;

    // Optional: highlight effect shown when cursor over this button
    [SerializeField] private Image highlightImage;

    // The color to show when highlighted
    [SerializeField] private Color highlightColor = Color.white;

    private Color originalColor;
    private bool[] playerHovering = new bool[4]; // Track which players are hovering

    private void Start()
    {
        if (highlightImage != null) originalColor = highlightImage.color;
    }

    private void Update()
    {
        // Check which players have their cursors over this button
        for (int i = 0; i < 4; ++i) playerHovering[i] = IsPlayerCursorOverButton(i);

        // Highlight if any player is hovering
        bool anyHovering = false;
        for (int i = 0; i < 4; ++i)
        {
            if (playerHovering[i])
            {
                anyHovering = true;
                break;
            }
        }

        if (highlightImage != null)
        {
            highlightImage.color = anyHovering ? highlightColor : originalColor;
        }
    }

    public void OnPressed(int playerIndex)
    {
        CharacterSelectManager manager = CharacterSelectManager.Instance;
        if (manager != null)
        {
            manager.SetPlayerBirdIndex(playerIndex, birdIndex);
            // Optional: visual feedback
            if (highlightImage != null) StartCoroutine(BriefFlash());
        }
    }

    public void OnPressed()
    {
        AviaryManager manager = AviaryManager.Instance;
        if (manager != null)
        {
            manager.SetPlayerBirdIndex(birdIndex);
            // Optional: visual feedback
            if (highlightImage != null) StartCoroutine(BriefFlash());
        }
    }

    private bool IsPlayerCursorOverButton(int playerIndex)
    {
        CharacterSelectManager manager = CharacterSelectManager.Instance;
        if (manager == null) return false;

        // This check could be bad, might want to replace with
        // rectTransformUtility or better bounds checking.
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null) return false;

        // For now, return false. The real check is done in HandlePlayerButtonPress
        // This is just a placeholder for visual feedback
        return false;
    }

    // Flash the button when selected.
    private System.Collections.IEnumerator BriefFlash()
    {
        if (highlightImage != null)
        {
            Color flash = Color.white;
            highlightImage.color = flash;
            yield return new WaitForSeconds(0.1f);
            highlightImage.color = highlightColor;
        }
    }
}
