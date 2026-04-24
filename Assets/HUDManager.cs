using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    // Ability Icon UI — replaces the plain RawImage references for abilities

    [System.Serializable]
    public class AbilityIconUI
    {
        public RawImage baseIcon;           // The actual ability icon texture
        public Image cooldownOverlay;       // Image set to Filled > Radial 360 > Top, dark color
        public TMP_Text cooldownText;       // Timer number shown during cooldown

        public void ResetImmediately()
        {
            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = 0f;
                cooldownOverlay.gameObject.SetActive(false);
            }
            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(false);
                cooldownText.text = "";
            }
        }

        // Call this to kick off the cooldown animation
        public IEnumerator RunCooldown(float duration)
        {
            SetOnCooldown(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float remaining = duration - elapsed;

                // fillAmount 1 = fully covered, 0 = fully revealed
                if (cooldownOverlay != null)
                    cooldownOverlay.fillAmount = 1f - (elapsed / duration);

                if (cooldownText != null)
                    cooldownText.text = Mathf.CeilToInt(remaining).ToString();

                yield return null;
            }

            SetOnCooldown(false);
        }

        private void SetOnCooldown(bool onCooldown)
        {
            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = onCooldown ? 1f : 0f;
                cooldownOverlay.gameObject.SetActive(onCooldown);
            }
            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(onCooldown);
                cooldownText.text = "";
            }
        }
    }

    // Player Card UI

    [System.Serializable]
    public class PlayerCardUI
    {
        public GameObject cardRoot;
        public TMP_Text playerNameText;
        public RawImage playerIcon;
        public AbilityIconUI offensiveAbility;
        public AbilityIconUI defensiveAbility;
    }

    [Header("Player Cards (Player 1 → 4)")]
    public PlayerCardUI player1Card;
    public PlayerCardUI player2Card;
    public PlayerCardUI player3Card;
    public PlayerCardUI player4Card;

    // Bird Inspector Fields

    [Header("Penguin")]
    [SerializeField] private string penguinDisplayName = "Penguin";
    [SerializeField] private Texture penguinPlayerIcon;
    [SerializeField] private Texture penguinOffensiveIcon;
    [SerializeField] private Texture penguinDefensiveIcon;

    [Header("Crow")]
    [SerializeField] private string crowDisplayName = "Crow";
    [SerializeField] private Texture crowPlayerIcon;
    [SerializeField] private Texture crowOffensiveIcon;
    [SerializeField] private Texture crowDefensiveIcon;

    [Header("Scissortail")]
    [SerializeField] private string scissortailDisplayName = "Scissortail";
    [SerializeField] private Texture scissortailPlayerIcon;
    [SerializeField] private Texture scissortailOffensiveIcon;
    [SerializeField] private Texture scissortailDefensiveIcon;

    [Header("Lovebird")]
    [SerializeField] private string lovebirdDisplayName = "Lovebird";
    [SerializeField] private Texture lovebirdPlayerIcon;
    [SerializeField] private Texture lovebirdOffensiveIcon;
    [SerializeField] private Texture lovebirdDefensiveIcon;

    [Header("Dodo")]
    [SerializeField] private string dodoDisplayName = "Dodo";
    [SerializeField] private Texture dodoPlayerIcon;
    [SerializeField] private Texture dodoOffensiveIcon;
    [SerializeField] private Texture dodoDefensiveIcon;

    [Header("Pelican")]
    [SerializeField] private string pelicanDisplayName = "Pelican";
    [SerializeField] private Texture pelicanPlayerIcon;
    [SerializeField] private Texture pelicanOffensiveIcon;
    [SerializeField] private Texture pelicanDefensiveIcon;

    [Header("Seagull")]
    [SerializeField] private string seagullDisplayName = "Seagull";
    [SerializeField] private Texture seagullPlayerIcon;
    [SerializeField] private Texture seagullOffensiveIcon;
    [SerializeField] private Texture seagullDefensiveIcon;

    [Header("Owl")]
    [SerializeField] private string owlDisplayName = "Owl";
    [SerializeField] private Texture owlPlayerIcon;
    [SerializeField] private Texture owlOffensiveIcon;
    [SerializeField] private Texture owlDefensiveIcon;

    [Header("Pukeko")]
    [SerializeField] private string pukekoDisplayName = "Pukeko";
    [SerializeField] private Texture pukekoPlayerIcon;
    [SerializeField] private Texture pukekoOffensiveIcon;
    [SerializeField] private Texture pukekoDefensiveIcon;

    [Header("Toucan")]
    [SerializeField] private string toucanDisplayName = "Toucan";
    [SerializeField] private Texture toucanPlayerIcon;
    [SerializeField] private Texture toucanOffensiveIcon;
    [SerializeField] private Texture toucanDefensiveIcon;

    [Header("Kiwi")]
    [SerializeField] private string kiwiDisplayName = "Kiwi";
    [SerializeField] private Texture kiwiPlayerIcon;
    [SerializeField] private Texture kiwiOffensiveIcon;
    [SerializeField] private Texture kiwiDefensiveIcon;

    [Header("Chicken")]
    [SerializeField] private string chickenDisplayName = "Chicken";
    [SerializeField] private Texture chickenPlayerIcon;
    [SerializeField] private Texture chickenOffensiveIcon;
    [SerializeField] private Texture chickenDefensiveIcon;

    [Header("Ostrich")]
    [SerializeField] private string ostrichDisplayName = "Ostrich";
    [SerializeField] private Texture ostrichPlayerIcon;
    [SerializeField] private Texture ostrichOffensiveIcon;
    [SerializeField] private Texture ostrichDefensiveIcon;


    private static HUDManager instance;
    public static HUDManager Instance => instance;

    // Tracks running cooldown coroutines so we can cancel them if needed
    private Coroutine[] offensiveCooldowns = new Coroutine[4];
    private Coroutine[] defensiveCooldowns = new Coroutine[4];

    private class BirdHUDData
    {
        public string displayName;
        public Texture playerIcon;
        public Texture offensiveIcon;
        public Texture defensiveIcon;
    }


    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }
    
    private void Start()
    {
        ResetAllCooldownIcons();
        PopulatePlayerCards();
    }

    private void ResetAllCooldownIcons()
    {
        foreach (PlayerCardUI card in GetOrderedCards())
        {
            if (card == null) continue;
            card.offensiveAbility?.ResetImmediately();
            card.defensiveAbility?.ResetImmediately();
        }
    }

    public void RegisterAICard(int playerIndex, BirdType birdType)
    {
        PlayerCardUI[] cards = GetOrderedCards();
        if (playerIndex < 0 || playerIndex >= cards.Length || cards[playerIndex] == null) return;

        BirdHUDData data = GetBirdHUDData(birdType);
        if (data != null)
            data.displayName = "[CPU] " + data.displayName;

        if (cards[playerIndex].cardRoot != null)
            cards[playerIndex].cardRoot.SetActive(true);

        ApplyBirdDataToCard(cards[playerIndex], data);
    }

    private void PopulatePlayerCards()
    {
        List<BirdType> selectedBirds = DataTransferManager.selectedBirds;

        if (selectedBirds == null || selectedBirds.Count == 0)
        {
            Debug.LogWarning("[HUDManager] DataTransferManager.selectedBirds is empty.");
            return;
        }

        PlayerCardUI[] cards = GetOrderedCards();

        for (int i = 0; i < cards.Length; i++)
        {
            PlayerCardUI card = cards[i];
            if (card == null) continue;

            bool playerIsActive = i < selectedBirds.Count;

            if (card.cardRoot != null)
                card.cardRoot.SetActive(playerIsActive);

            if (!playerIsActive) continue;

            BirdHUDData data = GetBirdHUDData(selectedBirds[i]);
            ApplyBirdDataToCard(card, data);
        }
    }

    private void ApplyBirdDataToCard(PlayerCardUI card, BirdHUDData data)
    {
        if (data == null) { Debug.LogWarning("[HUDManager] Missing bird HUD data."); return; }

        if (card.playerNameText != null)                    card.playerNameText.text = data.displayName;
        if (card.playerIcon != null)                        card.playerIcon.texture = data.playerIcon;
        if (card.offensiveAbility?.baseIcon != null)        card.offensiveAbility.baseIcon.texture = data.offensiveIcon;
        if (card.defensiveAbility?.baseIcon != null)        card.defensiveAbility.baseIcon.texture = data.defensiveIcon;
    }

    // Public Cooldown API — call these from bird ability scripts

    /// <summary>
    /// Triggers the offensive ability cooldown animation for a given player.
    /// Call this right when the player uses their offensive ability.
    /// Example: HUDManager.Instance.TriggerOffensiveCooldown(playerIndex, 8f);
    /// </summary>
    public void TriggerOffensiveCooldown(int playerIndex, float duration)
    {
        AbilityIconUI icon = GetOffensiveIcon(playerIndex);
        if (icon == null) return;

        if (offensiveCooldowns[playerIndex] != null)
            StopCoroutine(offensiveCooldowns[playerIndex]);

        offensiveCooldowns[playerIndex] = StartCoroutine(icon.RunCooldown(duration));
    }

    /// Triggers the defensive ability cooldown animation for a given player.
    /// Call this right when the player uses their defensive ability.
    /// Example: HUDManager.Instance.TriggerDefensiveCooldown(playerIndex, 5f);
    public void TriggerDefensiveCooldown(int playerIndex, float duration)
    {
        AbilityIconUI icon = GetDefensiveIcon(playerIndex);
        if (icon == null) return;

        if (defensiveCooldowns[playerIndex] != null)
            StopCoroutine(defensiveCooldowns[playerIndex]);

        defensiveCooldowns[playerIndex] = StartCoroutine(icon.RunCooldown(duration));
    }

    /// Cancels any running cooldown and resets the icon immediately.
    /// Useful if a cooldown reduction effect fires mid-cooldown.
    public void ResetOffensiveCooldown(int playerIndex)
    {
        if (offensiveCooldowns[playerIndex] != null)
            StopCoroutine(offensiveCooldowns[playerIndex]);

        AbilityIconUI icon = GetOffensiveIcon(playerIndex);
        icon?.ResetImmediately();
    }

    public void ResetDefensiveCooldown(int playerIndex)
    {
        if (defensiveCooldowns[playerIndex] != null)
            StopCoroutine(defensiveCooldowns[playerIndex]);

        AbilityIconUI icon = GetDefensiveIcon(playerIndex);
        icon?.ResetImmediately();
    }

    // Helpers

    private AbilityIconUI GetOffensiveIcon(int playerIndex)
    {
        PlayerCardUI[] cards = GetOrderedCards();
        if (playerIndex < 0 || playerIndex >= cards.Length || cards[playerIndex] == null) return null;
        return cards[playerIndex].offensiveAbility;
    }

    private AbilityIconUI GetDefensiveIcon(int playerIndex)
    {
        PlayerCardUI[] cards = GetOrderedCards();
        if (playerIndex < 0 || playerIndex >= cards.Length || cards[playerIndex] == null) return null;
        return cards[playerIndex].defensiveAbility;
    }

    private PlayerCardUI[] GetOrderedCards() =>
        new[] { player1Card, player2Card, player3Card, player4Card };

    public void RefreshPlayerCard(int playerIndex)
    {
        if (DataTransferManager.selectedBirds == null ||
            playerIndex < 0 ||
            playerIndex >= DataTransferManager.selectedBirds.Count) return;

        PlayerCardUI[] cards = GetOrderedCards();
        if (playerIndex >= cards.Length || cards[playerIndex] == null) return;

        ApplyBirdDataToCard(cards[playerIndex], GetBirdHUDData(DataTransferManager.selectedBirds[playerIndex]));
    }

    private BirdHUDData GetBirdHUDData(BirdType bird)
    {
        return bird switch
        {
            BirdType.PENGUIN     => new BirdHUDData { displayName = penguinDisplayName,     playerIcon = penguinPlayerIcon,     offensiveIcon = penguinOffensiveIcon,     defensiveIcon = penguinDefensiveIcon },
            BirdType.CROW        => new BirdHUDData { displayName = crowDisplayName,        playerIcon = crowPlayerIcon,        offensiveIcon = crowOffensiveIcon,        defensiveIcon = crowDefensiveIcon },
            BirdType.SCISSORTAIL => new BirdHUDData { displayName = scissortailDisplayName, playerIcon = scissortailPlayerIcon, offensiveIcon = scissortailOffensiveIcon, defensiveIcon = scissortailDefensiveIcon },
            BirdType.LOVEBIRD    => new BirdHUDData { displayName = lovebirdDisplayName,    playerIcon = lovebirdPlayerIcon,    offensiveIcon = lovebirdOffensiveIcon,    defensiveIcon = lovebirdDefensiveIcon },
            BirdType.DODO        => new BirdHUDData { displayName = dodoDisplayName,        playerIcon = dodoPlayerIcon,        offensiveIcon = dodoOffensiveIcon,        defensiveIcon = dodoDefensiveIcon },
            BirdType.PELICAN     => new BirdHUDData { displayName = pelicanDisplayName,     playerIcon = pelicanPlayerIcon,     offensiveIcon = pelicanOffensiveIcon,     defensiveIcon = pelicanDefensiveIcon },
            BirdType.SEAGULL     => new BirdHUDData { displayName = seagullDisplayName,     playerIcon = seagullPlayerIcon,     offensiveIcon = seagullOffensiveIcon,     defensiveIcon = seagullDefensiveIcon },
            BirdType.OWL         => new BirdHUDData { displayName = owlDisplayName,         playerIcon = owlPlayerIcon,         offensiveIcon = owlOffensiveIcon,         defensiveIcon = owlDefensiveIcon },
            BirdType.PUKEKO      => new BirdHUDData { displayName = pukekoDisplayName,      playerIcon = pukekoPlayerIcon,      offensiveIcon = pukekoOffensiveIcon,      defensiveIcon = pukekoDefensiveIcon },
            BirdType.TOUCAN      => new BirdHUDData { displayName = toucanDisplayName,      playerIcon = toucanPlayerIcon,      offensiveIcon = toucanOffensiveIcon,      defensiveIcon = toucanDefensiveIcon },
            BirdType.KIWI        => new BirdHUDData { displayName = kiwiDisplayName,        playerIcon = kiwiPlayerIcon,        offensiveIcon = kiwiOffensiveIcon,        defensiveIcon = kiwiDefensiveIcon },
            BirdType.CHICKEN     => new BirdHUDData { displayName = chickenDisplayName,        playerIcon = chickenPlayerIcon,        offensiveIcon = chickenOffensiveIcon,        defensiveIcon = chickenDefensiveIcon },
            BirdType.OSTRICH     => new BirdHUDData { displayName = ostrichDisplayName,        playerIcon = ostrichPlayerIcon,        offensiveIcon = ostrichOffensiveIcon,        defensiveIcon = ostrichDefensiveIcon },
            _                    => null
        };
    }
}