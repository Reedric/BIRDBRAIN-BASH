using UnityEngine;
using UnityEngine.UIElements;

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
        
        UpdatePPSDisplay();
        UpdateBestOfSetsDisplay();
    }

    private void OnGoButtonClicked()
    {
        // Start the match with the selected settings
        Debug.Log($"Starting match with {_pps} points per set and best of {_bestOfSets} sets.");
        
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
        if (_pps <= MaxPPS) {
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
        if (_bestOfSets <= MaxBestOfSets)
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
}
