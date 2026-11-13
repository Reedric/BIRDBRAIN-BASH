using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shared look of the menus that get built in code, based on the art of the ones that don't: the starry big
/// buttons, and rounded shapes with a cream edge and a bright fill, on the paper part of the menu background.
/// </summary>
public static class MenuStyle
{
    public static readonly Color TextColor = new(0.3f, 0.16f, 0.1f);
    public static readonly Color AccentColor = new(0.93f, 0.45f, 0.15f);
    public static readonly Color CreamColor = new(0.95f, 0.93f, 0.8f);
    public static readonly Color PinkColor = new(0.89f, 0.35f, 0.7f);
    public static readonly Color BlueColor = new(0.2f, 0.56f, 1f);
    public static readonly Color GreenColor = new(0.2f, 0.72f, 0.3f);

    private static Sprite roundedSprite;

    // The color of each player's cursor art.
    public static Color GetPlayerColor(int playerIndex)
    {
        return playerIndex switch
        {
            0 => BlueColor,
            1 => new Color(0.15f, 0.87f, 0.25f),
            2 => new Color(0.87f, 0.14f, 0.56f),
            3 => new Color(0.92f, 0.87f, 0.1f),
            _ => Color.white
        };
    }

    // Rounded images don't catch the mouse, since most are only decoration. This makes one that's part of a
    // button, row or text field catch it, so that the mouse can point at and click that control.
    public static void MakeClickable(Image image)
    {
        image.raycastTarget = true;
    }

    // Rounds the corners of an image. Passing its height gives it full semicircles for ends.
    public static void MakeRounded(Image image, float cornerDiameter)
    {
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;

        // Sizes the sprite's corners to half of the diameter
        image.pixelsPerUnitMultiplier = image.sprite.border.y / (cornerDiameter / 2f);
    }

    // A white circle that can stretch without warping its corners. Made in code so there's no extra image to keep track of.
    // Resources/MenuRounded, so that rounded images built into a scene in the editor keep their sprite. It's made
    // here instead if that's missing, which works while playing but can't be saved into a scene.
    private static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;

        roundedSprite = Resources.Load<Sprite>("MenuRounded");
        if (roundedSprite != null) return roundedSprite;

        const int size = 64;
        float radius = size / 2f;

        Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; ++y)
        {
            for (int x = 0; x < size; ++x)
            {
                // Fades over the last pixel so that the edge is smooth
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance)));
            }
        }
        texture.Apply();

        // Leaves the middle two pixels out of the corners, for it to stretch with
        float border = radius - 1f;
        roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        return roundedSprite;
    }

    public static TMP_Text AddText(Transform parent, TMP_FontAsset font, string message, float fontSize, Vector2 position, Vector2 size, Vector2 anchor, TextAlignmentOptions alignment)
    {
        GameObject textObject = TMP_DefaultControls.CreateText(new TMP_DefaultControls.Resources());
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (font != null) text.font = font;
        text.text = message;
        text.fontSize = fontSize;
        text.color = TextColor;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = false; // So that anything with arrows or typed in by a player can't get read as a tag
        return text;
    }

    // A rounded image, positioned from the center of its parent.
    public static Image AddRoundedImage(Transform parent, string name, Color color, Vector2 position, Vector2 size, float cornerDiameter)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        MakeRounded(image, cornerDiameter);
        return image;
    }

    // The colors the big buttons come in. Their blank images, font and label outlines are set in Resources/MenuArt.
    public enum PillColor { Yellow, Pink, Blue, Orange }

    // A new big button, positioned from the center of its parent
    public static Button AddPillButton(Transform parent, TMP_FontAsset font, string label, PillColor color, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject buttonObject = new(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Button button = buttonObject.GetComponent<Button>();
        MakePill(button, color, label, font, fontSize);
        return button;
    }

    // Turns an existing button into a big button, with its own label. Whatever it showed before (like a plain text
    // label) gets hidden. The button needs an Image to draw with. The font given is only used if MenuArt has no
    // button font.
    public static void MakePill(Button button, PillColor color, string label, TMP_FontAsset font, float fontSize)
    {
        foreach (Transform child in button.transform)
            child.gameObject.SetActive(false);

        RectTransform rect = (RectTransform)button.transform;
        float height = Mathf.Max(1f, rect.rect.height);

        MenuArt menuArt = MenuArt.Instance;
        MenuArt.PillArt pill = menuArt != null ? menuArt.GetPill(color) : null;
        Sprite pillSprite = pill != null ? pill.sprite : null;

        Image image = button.GetComponent<Image>();
        image.color = Color.white;

        // A button can have its own artwork instead (see MenuArt)
        MenuArt.ButtonArt customArt = menuArt != null ? menuArt.FindButtonArt(label) : null;
        if (customArt != null)
        {
            image.sprite = customArt.sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }
        else if (pillSprite != null)
        {
            image.sprite = pillSprite;
            image.type = Image.Type.Sliced;

            // Keeps the rounded ends round at any height
            image.pixelsPerUnitMultiplier = pillSprite.rect.height / height;
        }
        else
        {
            // Without the button's image, a plain rounded shape in its color
            MakeRounded(image, height);
            image.color = color switch
            {
                PillColor.Pink => PinkColor,
                PillColor.Blue => BlueColor,
                PillColor.Orange => AccentColor,
                _ => new Color(0.98f, 0.8f, 0.2f)
            };
        }
        MakeClickable(image);
        AddHighlight(button);

        if (customArt != null && customArt.artIncludesText) return;

        // Light lettering with a darker outline in the button's own color, like the lettering of the button art
        if (menuArt != null && menuArt.buttonFont != null) font = menuArt.buttonFont;

        Vector2 textSize = rect.rect.size - new Vector2(height, height * 0.2f);
        TMP_Text text = AddText(button.transform, font, label, fontSize, Vector2.zero, textSize, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.name = "Label";
        text.fontSizeMax = fontSize;

        Color outline;
        (text.color, outline) = color switch
        {
            PillColor.Pink => (new Color(0.99f, 0.8f, 0.95f), new Color(0.55f, 0.17f, 0.55f)),
            PillColor.Blue => (new Color(0.85f, 0.93f, 1f), new Color(0.13f, 0.33f, 0.7f)),
            PillColor.Orange => (new Color(1f, 0.88f, 0.6f), new Color(0.8f, 0.36f, 0.05f)),
            _ => (new Color(1f, 0.97f, 0.65f), new Color(0.86f, 0.55f, 0.08f))
        };

        // The outline comes from a material asset, so that it can be saved into a scene. A material only works
        // with the font it was made for.
        Material outlined = pill != null ? pill.labelMaterial : null;
        if (outlined != null && text.font != null && outlined.mainTexture == text.font.atlasTexture)
        {
            text.fontSharedMaterial = outlined;
        }
        else if (Application.isPlaying)
        {
            // This makes a material of the label's own, which only lasts while playing
            text.outlineColor = outline;
            text.outlineWidth = 0.25f;
        }
    }

    private const string highlightName = "Highlight";

    public static bool HasHighlight(Button button) => button.transform.Find(highlightName) != null;

    // Makes a big button light up a little when it's pointed at or selected. Every big button is otherwise its
    // art's own color. Tinting the art can only darken it, so the lightening is a see-through white shape over
    // it, which is what the button fades in and out. Does nothing more for a button that already has one.
    public static void AddHighlight(Button button)
    {
        Image image = button.GetComponent<Image>();
        image.color = Color.white;

        Transform existing = button.transform.Find(highlightName);
        Image highlight = existing != null ? existing.GetComponent<Image>() : null;
        if (existing != null) existing.gameObject.SetActive(true);
        if (highlight == null)
        {
            GameObject highlightObject = new(highlightName, typeof(RectTransform), typeof(Image));
            highlightObject.transform.SetParent(button.transform, false);
            highlightObject.transform.SetAsFirstSibling(); // Behind the label

            // Fills the button, apart from the art's see-through margin
            RectTransform buttonRect = (RectTransform)button.transform;
            float height = Mathf.Max(1f, buttonRect.rect.height);
            float margin = height * 0.06f;

            RectTransform rect = highlightObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);

            highlight = highlightObject.GetComponent<Image>();
            MakeRounded(highlight, height - margin * 2f);
        }
        highlight.color = Color.white;

        button.targetGraphic = highlight;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = colors.selectedColor = new Color(1f, 1f, 1f, 0.3f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.5f);
        colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.05f;
        button.colors = colors;
    }

    // Whether an image is already one of the big buttons, or a button's own art from MenuArt
    public static bool IsPill(Image image)
    {
        return MenuArt.Instance != null && MenuArt.Instance.IsButtonSprite(image.sprite);
    }
}
