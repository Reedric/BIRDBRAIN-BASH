using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Artwork for the menus that are built in code, kept in one asset (Resources/MenuArt) so it can be swapped out in
/// the Inspector without touching any code.
///
/// The big buttons use the blank button images (UI Assets/yellowbutton, pinkbutton, bluebutton and orangebutton) with
/// their label on top in the button font. Each color also has a material that gives its label a matching outline.
/// </summary>
[CreateAssetMenu(fileName = "MenuArt", menuName = "BIRDBRAIN/Menu Art")]
public class MenuArt : ScriptableObject
{
    [Serializable]
    public class PillArt
    {
        [Tooltip("The blank button image. Its sprite border keeps the rounded ends round when the button is stretched wider.")]
        public Sprite sprite;

        [Tooltip("A material preset of the button font, which outlines the label in this button's color")]
        public Material labelMaterial;
    }

    [Serializable]
    public class ButtonArt
    {
        [Tooltip("The button's label, exactly as it appears in the game (like \"Host Public Match\")")]
        public string label;

        [Tooltip("Artwork to use for this button instead of the blank button")]
        public Sprite sprite;

        [Tooltip("Ticked if the artwork has the button's lettering drawn into it, so the label isn't added on top")]
        public bool artIncludesText = true;
    }

    [Header("Big buttons")]
    [Tooltip("The font for the labels on the big buttons. Without one, they use the same font as the rest of the menu.")]
    public TMP_FontAsset buttonFont;

    public PillArt yellowButton = new();
    public PillArt pinkButton = new();
    public PillArt blueButton = new();
    public PillArt orangeButton = new();

    [Header("Controls screen (pause menu)")]
    [Tooltip("Shown on the Keyboard tab. Until this is set, the tab lists the keyboard controls instead.")]
    public Sprite keyboardControls;

    [Tooltip("Shown on the Controller tab. Until this is set, the tab lists the controller controls instead.")]
    public Sprite controllerControls;

    [Header("Buttons made in code")]
    [Tooltip("Custom artwork for particular buttons on the online screens and the controls screen. Any button not listed uses the blank button with its label on top.")]
    public List<ButtonArt> buttonArt = new();

    private static MenuArt instance;

    public static MenuArt Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<MenuArt>("MenuArt");
            return instance;
        }
    }

    public PillArt GetPill(MenuStyle.PillColor color)
    {
        return color switch
        {
            MenuStyle.PillColor.Pink => pinkButton,
            MenuStyle.PillColor.Blue => blueButton,
            MenuStyle.PillColor.Orange => orangeButton,
            _ => yellowButton
        };
    }

    // Whether a sprite is one of the big buttons' images, blank or custom
    public bool IsButtonSprite(Sprite sprite)
    {
        if (sprite == null) return false;

        foreach (PillArt pill in new[] { yellowButton, pinkButton, blueButton, orangeButton })
        {
            if (pill != null && pill.sprite == sprite) return true;
        }
        return buttonArt.Exists(entry => entry != null && entry.sprite == sprite);
    }

    // The custom artwork for a button, if it has any
    public ButtonArt FindButtonArt(string label)
    {
        return buttonArt.Find(entry => entry != null && entry.sprite != null && entry.label == label);
    }
}
