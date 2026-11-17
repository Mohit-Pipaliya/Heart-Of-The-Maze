using UnityEngine;

/// <summary>
/// Universal Modern Gold-Bordered Yellow HUD Banner for all prompts in HEART OF THE MAZE.
/// Provides a sleek, AAA cinematic interaction prompt styled with:
///  - Dark semi-transparent background box
///  - High-contrast golden yellow border accents (top & bottom)
///  - Crisp bold golden yellow typography centered at the bottom of the screen
/// </summary>
public static class GameHUDPrompt
{
    private static GUIStyle _cachedStyle;

    /// <summary>
    /// Draws the golden yellow HUD banner box at bottom center of screen.
    /// </summary>
    /// <param name="msg">Message string (e.g. "[E] Pick Up M4")</param>
    /// <param name="customWidth">Width override (0 = auto-scale based on text length)</param>
    /// <param name="height">Box height in pixels (default 48)</param>
    /// <param name="bottomOffset">Pixels from bottom of screen (default 35)</param>
    public static void DrawPromptBox(string msg, float customWidth = 0f, float height = 48f, float bottomOffset = 35f)
    {
        if (string.IsNullOrEmpty(msg)) return;

        // Auto-scale width to fit text comfortably (minimum 340px)
        float w = (customWidth > 0f) ? customWidth : Mathf.Clamp(msg.Length * 13f + 60f, 340f, 760f);
        float h = height;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - h - bottomOffset;

        // Save original GUI state
        Color origColor = GUI.color;
        int origDepth = GUI.depth;
        GUI.depth = -1000;

        // 1. Semi-transparent dark background (using DrawTexture with whiteTexture for guaranteed rendering)
        GUI.color = new Color(0.04f, 0.04f, 0.06f, 0.92f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // 2. High-contrast golden yellow border accents (top & bottom 3px)
        GUI.color = new Color(1f, 0.84f, 0.1f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, 3f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, y + h - 3f, w, 3f), Texture2D.whiteTexture);

        // Side accents (thin 2px gold edges for extra polish)
        GUI.color = new Color(1f, 0.84f, 0.1f, 0.65f);
        GUI.DrawTexture(new Rect(x, y, 2f, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + w - 2f, y, 2f, h), Texture2D.whiteTexture);

        // 3. Crisp bold golden yellow text
        if (_cachedStyle == null || _cachedStyle.normal == null)
        {
            _cachedStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 20,
                fontStyle = FontStyle.Bold
            };
        }
        _cachedStyle.normal.textColor = new Color(1f, 0.88f, 0.15f, 1f);

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, h), msg, _cachedStyle);

        // Restore GUI state
        GUI.color = origColor;
        GUI.depth = origDepth;
    }

    /// <summary>
    /// Draws an interactive AAA Puzzle Alignment HUD with a visual target gauge, moving needle,
    /// dynamic alignment percentage, stand-specific elemental theming, and clear direction indicators.
    /// </summary>
    public static void DrawAlignmentHUD(float currentAngle, float signedAngle, float thresholdAngle, bool isAligned, bool isMovingCloser, bool isKeyPressed, string standTitle = "", Color accentColor = default)
    {
        float w = 600f;
        float h = 94f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - h - 35f;

        Color origColor = GUI.color;
        int origDepth = GUI.depth;
        GUI.depth = -1000;

        Color themeAccent = (accentColor.a > 0.05f) ? accentColor : new Color(1f, 0.84f, 0.1f, 1f);

        // 1. Semi-transparent dark background
        GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // 2. High-contrast elemental border accents
        Color borderColor = isAligned ? new Color(0.1f, 1f, 0.45f, 1f) : themeAccent;
        GUI.color = borderColor;
        GUI.DrawTexture(new Rect(x, y, w, 3f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, y + h - 3f, w, 3f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, y, 2f, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + w - 2f, y, 2f, h), Texture2D.whiteTexture);

        // Optional Top Altar Badge (e.g. "[ STAND 1 • SOLAR ALTAR ]")
        float contentStartY = y + 5f;
        if (!string.IsNullOrEmpty(standTitle))
        {
            GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            badgeStyle.normal.textColor = Color.Lerp(themeAccent, Color.white, 0.4f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, contentStartY, w, 16f), $"❖  {standTitle.ToUpper()}  ❖", badgeStyle);
            contentStartY += 14f;
        }

        // 3. Direction Hint / Status Title
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };

        string headerText;
        if (isAligned)
        {
            headerStyle.normal.textColor = new Color(0.2f, 1f, 0.5f, 1f);
            headerText = "✓ PERFECTLY ALIGNED! UNLOCKING DOOR...";
        }
        else if (isKeyPressed && isMovingCloser)
        {
            headerStyle.normal.textColor = Color.Lerp(themeAccent, Color.white, 0.35f);
            headerText = "★ Resonance Increasing! Keep Rotating...";
        }
        else if (isKeyPressed && !isMovingCloser)
        {
            headerStyle.normal.textColor = new Color(1f, 0.45f, 0.3f, 1f);
            headerText = (signedAngle < 0f) ? "◄ Turn other way! Rotate Left [A]" : "Turn other way! Rotate Right [D] ►";
        }
        else
        {
            headerStyle.normal.textColor = themeAccent;
            headerText = (signedAngle < 0f) ? "◄ Rotate Left [A] to Match Elemental Crest" : "Rotate Right [D] to Match Elemental Crest ►";
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(x, contentStartY, w, 22f), headerText, headerStyle);

        // 4. Center Alignment Track Bar
        float trackW = 440f;
        float trackH = 10f;
        float trackX = x + (w - trackW) * 0.5f;
        float trackY = contentStartY + 24f;

        // Track background groove
        GUI.color = new Color(0.10f, 0.12f, 0.16f, 1f);
        GUI.DrawTexture(new Rect(trackX, trackY, trackW, trackH), Texture2D.whiteTexture);

        // Success zone in center (green target zone)
        float tolerancePx = Mathf.Max(16f, (thresholdAngle / 90f) * (trackW * 0.5f));
        float centerTrackX = trackX + trackW * 0.5f;
        GUI.color = new Color(0.1f, 0.85f, 0.35f, 0.35f);
        GUI.DrawTexture(new Rect(centerTrackX - tolerancePx, trackY, tolerancePx * 2f, trackH), Texture2D.whiteTexture);

        // Elemental Target Notch (Fixed destination at center)
        GUI.color = themeAccent;
        GUI.DrawTexture(new Rect(centerTrackX - 2f, trackY - 5f, 4f, trackH + 10f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerTrackX - 6f, trackY - 8f, 12f, 3f), Texture2D.whiteTexture);

        // Moving Needle (Current plate angle relative to target)
        float norm = Mathf.Clamp(signedAngle / 90f, -1f, 1f);
        float needleX = centerTrackX + (norm * (trackW * 0.5f));

        Color needleColor = isAligned
            ? new Color(0.2f, 1f, 0.4f, 1f)
            : (currentAngle <= 25f ? Color.Lerp(themeAccent, Color.white, 0.4f) : new Color(0.35f, 0.95f, 1f, 1f));

        GUI.color = needleColor;
        GUI.DrawTexture(new Rect(needleX - 4f, trackY - 4f, 8f, trackH + 8f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(needleX - 2f, trackY - 8f, 4f, trackH + 16f), Texture2D.whiteTexture);

        // 5. Bottom Info Bar: Left/Right key cues + Angle distance
        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Normal
        };

        subStyle.alignment = TextAnchor.MiddleLeft;
        subStyle.normal.textColor = new Color(0.7f, 0.75f, 0.85f, 0.9f);
        GUI.color = Color.white;
        GUI.Label(new Rect(trackX, trackY + 14f, 110f, 20f), "[A] ◄ Turn Left", subStyle);

        subStyle.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(trackX + trackW - 110f, trackY + 14f, 110f, 20f), "Turn Right ► [D]", subStyle);

        subStyle.alignment = TextAnchor.MiddleCenter;
        float percent = Mathf.Clamp01(1f - (currentAngle / 180f)) * 100f;
        subStyle.normal.textColor = needleColor;
        string statusText = isAligned ? "100% ALIGNED" : $"{percent:F0}% Aligned ({currentAngle:F0}° to Lock)";
        GUI.Label(new Rect(trackX, trackY + 14f, trackW, 20f), statusText, subStyle);

        // Restore GUI state
        GUI.color = origColor;
        GUI.depth = origDepth;
    }
}
