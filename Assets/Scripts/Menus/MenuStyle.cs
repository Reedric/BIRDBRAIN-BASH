using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shared look of the menus that get built in code, based on the art of the ones that don't:
/// rounded shapes with a cream edge and a bright fill, on the paper part of the menu background.
/// </summary>
public static class MenuStyle
{
    public static readonly Color TextColor = new(0.3f, 0.16f, 0.1f);
    public static readonly Color AccentColor = new(0.93f, 0.45f, 0.15f);
    public static readonly Color CreamColor = new(0.95f, 0.93f, 0.8f);
    public static readonly Color PinkColor = new(0.89f, 0.35f, 0.7f);
    public static readonly Color BlueColor = new(0.2f, 0.56f, 1f);
    public static readonly Color GreenColor = new(0.2f, 0.72f, 0.3f);

    private const float pillEdgeWidth = 8f;

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
    private static Sprite GetRoundedSprite()
    {
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

    // A button in the same style as the art for the Local and Online buttons.
    public static Button AddPillButton(Transform parent, TMP_FontAsset font, string label, Color color, Vector2 position, Vector2 size, float fontSize)
    {
        Image edge = AddRoundedImage(parent, label, CreamColor, position, size, size.y);

        Vector2 fillSize = size - Vector2.one * pillEdgeWidth * 2f;
        Image fill = AddRoundedImage(edge.transform, "Fill", color, Vector2.zero, fillSize, fillSize.y);

        Button button = edge.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;

        // Dimmed until it's selected, since tinting can only ever make a color darker
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.86f, 0.86f, 0.86f);
        colors.highlightedColor = colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        TMP_Text text = AddText(edge.transform, font, label, fontSize, Vector2.zero, fillSize, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
        text.color = Color.white;
        text.outlineColor = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f);
        text.outlineWidth = 0.25f;

        return button;
    }
}
