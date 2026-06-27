using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// TODO: Team Select
// Also TODO: Find some way to get the settings to the ScoreManager

[RequireComponent(typeof(UIDocument))]
/// <summary>
/// Manages the match settings menu for selecting game parameters.
/// 
/// *When changing the BotDifficulty, the numbers on the left and right arrows must be changed to the
/// new number of difficulties, and the modulo in the click handlers must be updated to match that number.*
/// </summary>
public class MatchSettingsMenu : MonoBehaviour
{
    private UIDocument _uiDocument;
    private Button _goButton;

    // Points per set elements
    private Button _pointsPerSetArrowLeft;
    private Button _pointsPerSetArrowRight;
    private Label _pointsPerSetText;
    public int PointsPerSet = 2;
    [SerializeField] private int MaxPPS = 25;

    // Best of sets elements
    private Button _bestOfSetsArrowLeft;
    private Button _bestOfSetsArrowRight;
    private Label _bestOfSetsText;
    public int BestOfSets = 3;
    [SerializeField] private int MaxBestOfSets = 7;

    // Final set points elements
    private VisualElement _finalSetPointsContainer;
    private Button _finalSetPointsArrowLeft;
    private Button _finalSetPointsArrowRight;
    private Label _finalSetPointsText;
    public int FinalSetPoints = 1;
    [SerializeField] private int MaxFinalSetPoints = 25;

    // Bot difficulty elements
    private Button _botDifficultyArrowLeft;
    private Button _botDifficultyArrowRight;
    private Label _botDifficultyText;
    private BotDifficulty _currentBotDifficulty = BotDifficulty.Easy;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        _goButton = _uiDocument.rootVisualElement.Q<Button>("GoButton");

        // Points per set UI elements
        _pointsPerSetArrowLeft = _uiDocument.rootVisualElement.Q<Button>("PointsPerSetArrowLeft");
        _pointsPerSetArrowRight = _uiDocument.rootVisualElement.Q<Button>("PointsPerSetArrowRight");
        _pointsPerSetText = _uiDocument.rootVisualElement.Q<Label>("PointsPerSetText");

        // Best of sets UI elements
        _bestOfSetsArrowLeft = _uiDocument.rootVisualElement.Q<Button>("BestOfSetsArrowLeft");
        _bestOfSetsArrowRight = _uiDocument.rootVisualElement.Q<Button>("BestOfSetsArrowRight");
        _bestOfSetsText = _uiDocument.rootVisualElement.Q<Label>("BestOfSetsText");

        // Bot difficulty UI elements
        _botDifficultyArrowLeft = _uiDocument.rootVisualElement.Q<Button>("BotDifficultyArrowLeft");
        _botDifficultyArrowRight = _uiDocument.rootVisualElement.Q<Button>("BotDifficultyArrowRight");
        _botDifficultyText = _uiDocument.rootVisualElement.Q<Label>("BotDifficultyText");

        // Final set points UI elements
        _finalSetPointsContainer = _uiDocument.rootVisualElement.Q<VisualElement>("FinalSetPoints");
        _finalSetPointsArrowLeft = _uiDocument.rootVisualElement.Q<Button>("FinalSetPointsArrowLeft");
        _finalSetPointsArrowRight = _uiDocument.rootVisualElement.Q<Button>("FinalSetPointsArrowRight");
        _finalSetPointsText = _uiDocument.rootVisualElement.Q<Label>("FinalSetPointsText");

        // click event handlers
        _goButton.clicked += OnGoButtonClicked;
        _pointsPerSetArrowLeft.clicked += OnPointsPerSetArrowLeftClicked;
        _pointsPerSetArrowRight.clicked += OnPointsPerSetArrowRightClicked;
        _bestOfSetsArrowLeft.clicked += OnBestOfSetsArrowLeftClicked;
        _bestOfSetsArrowRight.clicked += OnBestOfSetsArrowRightClicked;
        _botDifficultyArrowLeft.clicked += OnBotDifficultyArrowLeftClicked;
        _botDifficultyArrowRight.clicked += OnBotDifficultyArrowRightClicked;
        _finalSetPointsArrowLeft.clicked += OnFinalSetPointsArrowLeftClicked;
        _finalSetPointsArrowRight.clicked += OnFinalSetPointsArrowRightClicked;

        // Initialize displays
        UpdatePointsPerSetDisplay();
        UpdateBestOfSetsDisplay();
        UpdateBotDifficultyDisplay();
        UpdateFinalSetPointsDisplay();
    }

    private void OnGoButtonClicked()
    {
        // Save selected settings into persistent GameSettings so Game scene can read them
        var gs = GameSettings.EnsureInstance();
        gs.PointsPerSet = PointsPerSet;
        gs.BestOfSets = BestOfSets;
        gs.FinalSetPoints = FinalSetPoints;
        gs.CurrentBotDifficulty = (GameSettings.BotDifficulty)(int)_currentBotDifficulty;

        Debug.Log($"Starting match with {PointsPerSet} points per set and best of {BestOfSets} sets.");
        SceneManager.LoadScene("CharSelect");
    }

    // Points per set click handlers -------------------------------------------------------------
    private void OnPointsPerSetArrowLeftClicked()
    {
        if (PointsPerSet > 1)
        {
            PointsPerSet--;
            UpdatePointsPerSetDisplay();
        }
    }

    private void OnPointsPerSetArrowRightClicked()
    {
        if (PointsPerSet < MaxPPS) {
            PointsPerSet++;
            UpdatePointsPerSetDisplay();
        }
    }

    private void UpdatePointsPerSetDisplay()
    {
        _pointsPerSetText.text = PointsPerSet.ToString();
    }

    // Best of sets click handlers -------------------------------------------------------------
    private void OnBestOfSetsArrowLeftClicked()
    {
        if (BestOfSets > 1)
        {
            BestOfSets -= 2;
            UpdateBestOfSetsDisplay();
            _finalSetPointsContainer.visible = true;
        }
    }

    private void OnBestOfSetsArrowRightClicked()
    {
        if (BestOfSets < MaxBestOfSets)
        {
            BestOfSets += 2;
            UpdateBestOfSetsDisplay();
        }

        if (BestOfSets < 2) _finalSetPointsContainer.visible = false;
    }

    private void UpdateBestOfSetsDisplay()
    {
        _bestOfSetsText.text = BestOfSets.ToString();
    }

    // Final set points click handlers -------------------------------------------------------------
    private void OnFinalSetPointsArrowLeftClicked()
    {
        if (FinalSetPoints > 1)
        {
            FinalSetPoints--;
            UpdateFinalSetPointsDisplay();
        }
    }

    private void OnFinalSetPointsArrowRightClicked()
    {
        if (FinalSetPoints < MaxPPS)
        {
            FinalSetPoints++;
            UpdateFinalSetPointsDisplay();
        }
    }

    private void UpdateFinalSetPointsDisplay()
    {
        _finalSetPointsText.text = FinalSetPoints.ToString();
    }

    // Bot difficulty click handlers -------------------------------------------------------------
    // Want to have the difficuties cycle
    private void OnBotDifficultyArrowLeftClicked()
    {
        _currentBotDifficulty = (BotDifficulty)(((int)_currentBotDifficulty - 1 + 4) % 4);
        UpdateBotDifficultyDisplay();
    }

    private void OnBotDifficultyArrowRightClicked()
    {
        _currentBotDifficulty = (BotDifficulty)(((int)_currentBotDifficulty + 1) % 4);
        UpdateBotDifficultyDisplay();
    }

    private void UpdateBotDifficultyDisplay()
    {
        _botDifficultyText.text = _botDifficultyLookup[_currentBotDifficulty];
    }

    private enum BotDifficulty
    {
        Easy,
        Medium,
        Hard,
        Impossible
    }

    private readonly Dictionary<BotDifficulty, string> _botDifficultyLookup = new()
    {
        { BotDifficulty.Easy, "Easy" },
        { BotDifficulty.Medium, "Medium" },
        { BotDifficulty.Hard, "Hard" },
        {BotDifficulty.Impossible, "Impossible"}
    };
}