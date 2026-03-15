using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    [Header("Penguin")]
    [SerializeField] private GameObject penguinKBM;
    [SerializeField] private GameObject penguinC;
    [SerializeField] private GameObject penguinAI;

    [Header("Seagull")]
    [SerializeField] private GameObject seagullKBM;
    [SerializeField] private GameObject seagullC;
    [SerializeField] private GameObject seagullAI;

    [Header("Lovebird")]
    [SerializeField] private GameObject lovebirdKBM;
    [SerializeField] private GameObject lovebirdC;
    [SerializeField] private GameObject lovebirdAI;

    [Header("Toucan_chibi")]
    [SerializeField] private GameObject toucanKBM;
    [SerializeField] private GameObject toucanC;
    [SerializeField] private GameObject toucanAI;

    [Header("Pukeko")]
    [SerializeField] private GameObject pukekoKBM;
    [SerializeField] private GameObject pukekoC;
    [SerializeField] private GameObject pukekoAI;

    [Header("Scissortail")]
    [SerializeField] private GameObject scissortailKBM;
    [SerializeField] private GameObject scissortailC;
    [SerializeField] private GameObject scissortailAI;

    [Header("Dodo")]
    [SerializeField] private GameObject dodoKBM;
    [SerializeField] private GameObject dodoC;
    [SerializeField] private GameObject dodoAI;

    [Header("Pelican")]
    [SerializeField] private GameObject pelicanKBM;
    [SerializeField] private GameObject pelicanC;
    [SerializeField] private GameObject pelicanAI;

    [HideInInspector] public GameObject PenguinKBM => penguinKBM;
    [HideInInspector] public GameObject PenguinC => penguinC;
    [HideInInspector] public GameObject PenguinAI => penguinAI;
    [HideInInspector] public GameObject SeagullKBM => seagullKBM;
    [HideInInspector] public GameObject SeagullC => seagullC;
    [HideInInspector] public GameObject SeagullAI => seagullAI;
    [HideInInspector] public GameObject LovebirdKBM => lovebirdKBM;
    [HideInInspector] public GameObject LovebirdC => lovebirdC;
    [HideInInspector] public GameObject LovebirdAI => lovebirdAI;
    [HideInInspector] public GameObject ToucanKBM => toucanKBM;
    [HideInInspector] public GameObject ToucanC => toucanC;
    [HideInInspector] public GameObject ToucanAI => toucanAI;
    [HideInInspector] public GameObject PukekoKBM => pukekoKBM;
    [HideInInspector] public GameObject PukekoC => pukekoC;
    [HideInInspector] public GameObject PukekoAI => pukekoAI;
    [HideInInspector] public GameObject ScissortailKBM => scissortailKBM;
    [HideInInspector] public GameObject ScissortailC => scissortailC;
    [HideInInspector] public GameObject ScissortailAI => scissortailAI;
    [HideInInspector] public GameObject DodoKBM => dodoKBM;
    [HideInInspector] public GameObject DodoC => dodoC;
    [HideInInspector] public GameObject DodoAI => dodoAI;

    [HideInInspector] public GameObject PelicanKBM => pelicanKBM;
    [HideInInspector] public GameObject PelicanC => pelicanC;
    [HideInInspector] public GameObject PelicanAI => pelicanAI;
}
