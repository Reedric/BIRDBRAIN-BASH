using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System.Collections.Generic;

// TODO: AI Difficulty, Final Set Reduced Points, Team Select
// Also TODO: Find some way to get the settings to the ScoreManager

[RequireComponent(typeof(UIDocument))]
public class MatchSettingsMenu : MonoBehaviour
{
    private UIDocument _uiDocument;
    
    private Button _goButton;

    // Points per set elements
    private Button PPSArrowLeft;
    private Button PPSArrowRight;
    private Label PPSText;
    [SerializeField] private int _pps = 2;
    [SerializeField] private int MaxPPS = 25;

    // Best of sets elements
    private Button _bestOfSetsArrowLeft;
    private Button _bestOfSetsArrowRight;
    private Label _bestOfSetsText;
    [SerializeField] private int _bestOfSets = 3;
    [SerializeField] private int MaxBestOfSets = 7;

    // Bot difficulty elements
    private Button _botDifficultyArrowLeft;
    private Button _botDifficultyArrowRight;
    private Label _botDifficultyText;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();

        _goButton = _uiDocument.rootVisualElement.Q<Button>("GoButton");

        // Points per set UI elements
        PPSArrowLeft = _uiDocument.rootVisualElement.Q<Button>("PPSArrowLeft");
        PPSArrowRight = _uiDocument.rootVisualElement.Q<Button>("PPSArrowRight");
        PPSText = _uiDocument.rootVisualElement.Q<Label>("PPSText");

        // Best of sets UI elements
        _bestOfSetsArrowLeft = _uiDocument.rootVisualElement.Q<Button>("BestOfSetsArrowLeft");
        _bestOfSetsArrowRight = _uiDocument.rootVisualElement.Q<Button>("BestOfSetsArrowRight");
        _bestOfSetsText = _uiDocument.rootVisualElement.Q<Label>("BestOfSetsText");

        // click event handlers
        _goButton.clicked += OnGoButtonClicked;
        PPSArrowLeft.clicked += OnPPSArrowLeftClicked;
        PPSArrowRight.clicked += OnPPSArrowRightClicked;
        _bestOfSetsArrowLeft.clicked += OnBestOfSetsArrowLeftClicked;
        _bestOfSetsArrowRight.clicked += OnBestOfSetsArrowRightClicked;
        _botDifficultyArrowLeft.clicked += OnBotDifficultyArrowLeftClicked;
        _botDifficultyArrowRight.clicked += OnBotDifficultyArrowRightClicked;

        // Initialize displays
        UpdatePPSDisplay();
        UpdateBestOfSetsDisplay();
        UpdateBotDifficultyDisplay();
    }

    private void OnGoButtonClicked()
    {
        // Start the match with the selected settings
        Debug.Log($"Starting match with {_pps} points per set and best of {_bestOfSets} sets.");
        SceneManager.LoadScene("Game");
    }

    private void OnPPSArrowLeftClicked()
    {
        if (_pps > 1)
        {
            _pps--;
            UpdatePPSDisplay();
        }
    }

    private void OnPPSArrowRightClicked()
    {
        if (_pps < MaxPPS) {
            _pps++;
            UpdatePPSDisplay();
        }
    }

    private void OnBestOfSetsArrowLeftClicked()
    {
        if (_bestOfSets > 1)
        {
            _bestOfSets--;
            UpdateBestOfSetsDisplay();
        }
    }

    private void OnBestOfSetsArrowRightClicked()
    {
        if (_bestOfSets < MaxBestOfSets)
        {
            _bestOfSets++;
            UpdateBestOfSetsDisplay();
        }
    }

    private void UpdatePPSDisplay()
    {
        PPSText.text = _pps.ToString();
    }

    private void UpdateBestOfSetsDisplay()
    {
        _bestOfSetsText.text = _bestOfSets.ToString();
    }

    // Want to have the difficuties cycle
    private void OnBotDifficultyArrowLeftClicked()
    {
        
    }

    private void OnBotDifficultyArrowRightClicked()
    {
        
    }

    private void UpdateBotDifficultyDisplay()
    {
        
    }

    private enum BotDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    private readonly Dictionary<BotDifficulty, string> _botDifficultyLookup = new()
    {
        { BotDifficulty.Easy, "Easy" },
        { BotDifficulty.Medium, "Medium" },
        { BotDifficulty.Hard, "Hard" }
    };
}