using UnityEngine;

public static class HealthBarSprites
{
    private const int Supersample = 4;

    // Cached procedural sprites
    private static Sprite _frame;
    private static Sprite _fill;
    private static Sprite _glow;
    private static Sprite _highlight;
    private static Sprite _sweep;
    private static Sprite _track;
    private static Sprite _white;
    private static Sprite _particle;
    private static Sprite _icon;
    private static Sprite _transparent;
    private static Sprite _vignette;

    // Zombie
    private static Sprite _zombieFrame;
    private static Sprite _zombieFill;
    private static Sprite _zombieGlow;
    private static Sprite _zombieIcon;
    private static Sprite _zombieBloodDrop;

    // Spider
    private static Sprite _spiderFrame;
    private static Sprite _spiderFill;
    private static Sprite _spiderGlow;
    private static Sprite _spiderIcon;
    private static Sprite _spiderAcidDrop;

    // Boss (Naga)
    private static Sprite _bossFrame;
    private static Sprite _bossFill;
    private static Sprite _bossGlow;
    private static Sprite _bossEndCapLeft;
    private static Sprite _bossEndCapRight;
    private static Sprite _bossNamePlate;
    private static Sprite _bossSmokePuff;

    public static void ClearCache()
    {
        _frame = null; _fill = null; _glow = null; _highlight = null; _sweep = null;
        _track = null; _white = null; _particle = null; _icon = null; _transparent = null; _vignette = null;
        _zombieFrame = null; _zombieFill = null; _zombieGlow = null; _zombieIcon = null; _zombieBloodDrop = null;
        _spiderFrame = null; _spiderFill = null; _spiderGlow = null; _spiderIcon = null; _spiderAcidDrop = null;
        _bossFrame = null; _bossFill = null; _bossGlow = null; _bossEndCapLeft = null; _bossEndCapRight = null;
        _bossNamePlate = null; _bossSmokePuff = null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PLAYER SPRITES
    // ═══════════════════════════════════════════════════════════════════════════

    public static Sprite GetPlayerFrame()
    {
        if (_frame == null)
            _frame = CreatePlayerFrame();
        return _frame;
    }

    public static Sprite GetPlayerFill()
    {
        if (_fill == null)
            _fill = CreateGradientSprite(520, 26, new[]
            {
                new Color32(255, 90, 110, 255), // #ff5a6e (top)
                new Color32(196, 21, 47, 255),  // #c4152f (mid)
                new Color32(122, 10, 29, 255)   // #7a0a1d (bottom)
            });
        return _fill;
    }

    public static Sprite GetPlayerGlow()
    {
        if (_glow == null)
            _glow = CreatePlayerGlow();
        return _glow;
    }

    public static Sprite GetPlayerHighlight()
    {
        if (_highlight == null)
            _highlight = CreateVerticalAlphaSprite(520, 11, 0.65f, 0.05f);
        return _highlight;
    }

    public static Sprite GetPlayerSweep()
    {
        if (_sweep == null)
            _sweep = CreateSweepSprite();
        return _sweep;
    }

    public static Sprite GetPlayerIcon()
    {
        if (_icon == null)
            _icon = CreatePlayerIcon();
        return _icon;
    }

    public static Sprite GetPlayerVignette()
    {
        if (_vignette == null)
            _vignette = CreatePlayerVignette();
        return _vignette;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ZOMBIE SPRITES
    // ═══════════════════════════════════════════════════════════════════════════

    public static Sprite GetZombieFrame()
    {
        if (_zombieFrame == null)
            _zombieFrame = CreateZombieFrame();
        return _zombieFrame;
    }

    public static Sprite GetZombieFill()
    {
        if (_zombieFill == null)
            _zombieFill = CreateGradientSprite(332, 16, new[]
            {
                new Color32(176, 24, 24, 255), // #b01818
                new Color32(77, 7, 7, 255)     // #4d0707
            });
        return _zombieFill;
    }

    public static Sprite GetZombieGlow()
    {
        if (_zombieGlow == null)
            _zombieGlow = CreateZombieGlow();
        return _zombieGlow;
    }

    public static Sprite GetZombieIcon()
    {
        if (_zombieIcon == null)
            _zombieIcon = CreateZombieIcon();
        return _zombieIcon;
    }

    public static Sprite GetZombieBloodDrop()
    {
        if (_zombieBloodDrop == null)
            _zombieBloodDrop = CreateZombieBloodDrop();
        return _zombieBloodDrop;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SPIDER SPRITES
    // ═══════════════════════════════════════════════════════════════════════════

    public static Sprite GetSpiderFrame()
    {
        if (_spiderFrame == null)
            _spiderFrame = CreateSpiderFrame();
        return _spiderFrame;
    }

    public static Sprite GetSpiderFill()
    {
        if (_spiderFill == null)
            _spiderFill = CreateGradientSprite(376, 24, new[]
            {
                new Color32(139, 255, 122, 255), // #8bff7a (top)
                new Color32(37, 196, 59, 255),   // #25c43b (mid)
                new Color32(13, 107, 32, 255)    // #0d6b20 (bottom)
            });
        return _spiderFill;
    }

    public static Sprite GetSpiderGlow()
    {
        if (_spiderGlow == null)
            _spiderGlow = CreateSpiderGlow();
        return _spiderGlow;
    }

    public static Sprite GetSpiderIcon()
    {
        if (_spiderIcon == null)
            _spiderIcon = CreateSpiderIcon();
        return _spiderIcon;
    }

    public static Sprite GetSpiderAcidDrop()
    {
        if (_spiderAcidDrop == null)
            _spiderAcidDrop = CreateSpiderAcidDrop();
        return _spiderAcidDrop;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BOSS (NAGA) SPRITES
    // ═══════════════════════════════════════════════════════════════════════════

    public static Sprite GetBossFrame()
    {
        if (_bossFrame == null)
            _bossFrame = CreateBossFrame();
        return _bossFrame;
    }

    public static Sprite GetBossFill()
    {
        if (_bossFill == null)
            _bossFill = CreateBossFill();
        return _bossFill;
    }

    public static Sprite GetBossGlow()
    {
        if (_bossGlow == null)
            _bossGlow = CreateBossGlow();
        return _bossGlow;
    }

    public static Sprite GetBossEndCapLeft()
    {
        if (_bossEndCapLeft == null)
            _bossEndCapLeft = CreateSnakeHead(false);
        return _bossEndCapLeft;
    }

    public static Sprite GetBossEndCapRight()
    {
        if (_bossEndCapRight == null)
            _bossEndCapRight = CreateSnakeHead(true);
        return _bossEndCapRight;
    }

    public static Sprite GetBossNamePlate()
    {
        if (_bossNamePlate == null)
            _bossNamePlate = CreateBossNamePlate();
        return _bossNamePlate;
    }

    public static Sprite GetBossSmokePuff()
    {
        if (_bossSmokePuff == null)
            _bossSmokePuff = CreateBossSmokePuff();
        return _bossSmokePuff;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  COMMON UTILITY SPRITES
    // ═══════════════════════════════════════════════════════════════════════════

    public static Sprite GetTrack()
    {
        if (_track == null)
            _track = CreateSolidSprite(2, 2, new Color32(12, 16, 22, 255));
        return _track;
    }

    public static Sprite GetWhite()
    {
        if (_white == null)
            _white = CreateSolidSprite(2, 2, new Color32(255, 255, 255, 255));
        return _white;
    }

    public static Sprite GetParticle()
    {
        if (_particle == null)
            _particle = CreateCircleSprite();
        return _particle;
    }

    public static Sprite GetTransparent()
    {
        if (_transparent == null)
            _transparent = CreateSolidSprite(1, 1, new Color32(255, 255, 255, 0));
        return _transparent;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PROCEDURAL GENERATORS — PLAYER
    // ═══════════════════════════════════════════════════════════════════════════

    private static Sprite CreatePlayerFrame()
    {
        RasterCanvas raster = new RasterCanvas(600, 80);
        Vector2[] outer = { new Vector2(6, 4), new Vector2(576, 4), new Vector2(596, 76), new Vector2(6, 76) };
        Vector2[] hole = { new Vector2(44, 28), new Vector2(564, 28), new Vector2(564, 54), new Vector2(44, 54) };
        
        raster.FillPolygonWithHole(outer, hole, (x, y) => SteelGradient(y));
        raster.StrokeClosedPolygon(outer, 1f, new Color32(5, 7, 10, 255));
        raster.StrokeClosedPolygon(hole, 1f, new Color32(5, 7, 10, 255));

        for (int y = 0; y < 80; y += 8)
        {
            for (int x = 0; x < 600; x += 8)
            {
                raster.FillRectClippedToPolygon(x, y, 4, 4, outer, new Color32(0, 0, 0, 70), hole);
                raster.FillRectClippedToPolygon(x + 4, y + 4, 4, 4, outer, new Color32(0, 0, 0, 70), hole);
            }
        }

        raster.FillRect(44, 24, 520, 2, new Color32(47, 216, 255, 255));
        raster.FillRect(44, 56, 520, 2, new Color32(47, 216, 255, 255));

        for (int i = 0; i < 26; i++)
        {
            int x = 52 + i * 20;
            int y = i % 5 == 0 ? 64 : 67;
            raster.StrokeLine(new Vector2(x, y), new Vector2(x, 72), 1.5f, new Color32(11, 13, 16, 255));
        }

        raster.StrokePolyline(new[] { new Vector2(546, 9), new Vector2(555, 15), new Vector2(546, 21) }, 2.5f, new Color32(47, 216, 255, 255));
        return raster.ToSprite();
    }

    private static Color32 SteelGradient(float y)
    {
        float t = y / 80f;
        if (t < 0.33f)
            return Color32.Lerp(new Color32(212, 216, 222, 255), new Color32(138, 145, 157, 255), t / 0.33f);
        if (t < 0.66f)
            return Color32.Lerp(new Color32(138, 145, 157, 255), new Color32(58, 63, 72, 255), (t - 0.33f) / 0.33f);
        return Color32.Lerp(new Color32(58, 63, 72, 255), new Color32(11, 13, 16, 255), (t - 0.66f) / 0.34f);
    }

    private static Sprite CreatePlayerGlow()
    {
        RasterCanvas raster = new RasterCanvas(536, 42);
        for (int y = 0; y < 42; y++)
        {
            for (int x = 0; x < 536; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(8f - x, x - 528f));
                float dy = Mathf.Max(0f, Mathf.Max(8f - y, y - 34f));
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.Clamp01(distance / 7f);
                if (x >= 8f && x < 528f && y >= 8f && y < 34f)
                    alpha = 1f;
                raster.SetPixel(x, y, new Color32(255, 255, 255, (byte)(alpha * 255f)));
            }
        }
        return raster.ToSprite();
    }

    private static Sprite CreatePlayerIcon()
    {
        RasterCanvas raster = new RasterCanvas(20, 20);
        raster.FillCircle(10, 10, 9.5f, new Color32(75, 82, 93, 255));
        raster.StrokeCircle(10, 10, 9.5f, 2f, new Color32(11, 13, 16, 255));
        raster.FillCircle(10, 10, 3.5f, new Color32(47, 216, 255, 255));
        return raster.ToSprite();
    }

    private static Sprite CreatePlayerVignette()
    {
        RasterCanvas raster = new RasterCanvas(320, 180);
        const float edgePixels = 110f / 6f;
        for (int y = 0; y < 180; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                float edgeDistance = Mathf.Min(Mathf.Min(x, 319 - x), Mathf.Min(y, 179 - y));
                float alpha = 1f - Mathf.Clamp01(edgeDistance / edgePixels);
                alpha = alpha * alpha;
                raster.SetPixel(x, y, new Color32(255, 20, 40, (byte)(alpha * 255f)));
            }
        }
        return raster.ToSprite();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PROCEDURAL GENERATORS — ZOMBIE
    // ═══════════════════════════════════════════════════════════════════════════

    private static Sprite CreateZombieFrame()
    {
        RasterCanvas raster = new RasterCanvas(400, 44);
        Vector2[] outer =
        {
            new Vector2(4, 8), new Vector2(30, 3), new Vector2(70, 9), new Vector2(120, 2),
            new Vector2(180, 8), new Vector2(240, 3), new Vector2(300, 9), new Vector2(360, 3),
            new Vector2(396, 9), new Vector2(398, 22), new Vector2(393, 37), new Vector2(350, 41),
            new Vector2(290, 36), new Vector2(230, 42), new Vector2(170, 37), new Vector2(110, 42),
            new Vector2(50, 37), new Vector2(8, 41), new Vector2(2, 24)
        };
        Vector2[] hole = { new Vector2(46, 14), new Vector2(378, 14), new Vector2(378, 30), new Vector2(46, 30) };
        raster.FillPolygonWithHole(outer, hole, (x, y) => RustGradient(y));
        raster.StrokeClosedPolygon(outer, 1f, new Color32(20, 12, 5, 255));
        raster.StrokeClosedPolygon(hole, 1f, new Color32(20, 12, 5, 255));

        for (int y = 0; y < 44; y += 11)
        {
            for (int x = 0; x < 400; x += 14)
            {
                raster.FillCircleClippedToPolygon(x + 3, y + 3, 2f, outer, hole, new Color32(26, 12, 4, 179));
                raster.FillCircleClippedToPolygon(x + 10, y + 8, 1.3f, outer, hole, new Color32(26, 12, 4, 179));
            }
        }

        Color32 darkCrack = new Color32(31, 16, 5, 255);
        Color32 toxicCrack = new Color32(154, 255, 58, 255);
        StrokeCrack(raster, new[] { new Vector2(72, 4), new Vector2(77, 8), new Vector2(73, 11), new Vector2(78, 13) }, darkCrack, toxicCrack);
        StrokeCrack(raster, new[] { new Vector2(250, 4), new Vector2(245, 8), new Vector2(250, 11), new Vector2(246, 13) }, darkCrack, toxicCrack);
        StrokeCrack(raster, new[] { new Vector2(140, 31), new Vector2(145, 35), new Vector2(141, 38), new Vector2(146, 40) }, darkCrack, toxicCrack);
        StrokeCrack(raster, new[] { new Vector2(310, 31), new Vector2(306, 35), new Vector2(311, 38) }, darkCrack, toxicCrack);

        Color32 blood = new Color32(107, 15, 15, 217);
        int[] bloodXs = { 120, 200, 286, 360 };
        for (int i = 0; i < bloodXs.Length; i++)
        {
            raster.FillEllipse(bloodXs[i], 7, 2.5f, 1.8f, blood);
            raster.FillEllipse(bloodXs[i], 36, 2.5f, 2.5f, blood);
            raster.FillRect(bloodXs[i] - 1f, 7f, 2f, 3f, blood);
        }
        return raster.ToSprite();
    }

    private static void StrokeCrack(RasterCanvas raster, Vector2[] points, Color32 dark, Color32 toxic)
    {
        raster.StrokePolyline(points, 2.8f, dark);
        raster.StrokePolyline(points, 1.1f, toxic);
    }

    private static Color32 RustGradient(float y)
    {
        float t = y / 44f;
        if (t < 0.5f)
            return Color32.Lerp(new Color32(154, 98, 56, 255), new Color32(91, 52, 24, 255), t * 2f);
        return Color32.Lerp(new Color32(91, 52, 24, 255), new Color32(43, 23, 9, 255), (t - 0.5f) * 2f);
    }

    private static Sprite CreateZombieGlow()
    {
        RasterCanvas raster = new RasterCanvas(348, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 348; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(8f - x, x - 340f));
                float dy = Mathf.Max(0f, Mathf.Max(8f - y, y - 24f));
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.Clamp01(distance / 7f);
                if (x >= 8f && x < 340f && y >= 8f && y < 24f)
                    alpha = 1f;
                raster.SetPixel(x, y, new Color32(255, 255, 255, (byte)(alpha * 255f)));
            }
        }
        return raster.ToSprite();
    }

    private static Sprite CreateZombieIcon()
    {
        RasterCanvas raster = new RasterCanvas(24, 26);
        raster.FillEllipse(12, 12, 11f, 12f, new Color32(232, 220, 192, 255));
        raster.FillCircle(7.5f, 11, 3f, new Color32(31, 24, 17, 255));
        raster.FillCircle(16.5f, 11, 3f, new Color32(31, 24, 17, 255));
        raster.FillPolygon(
            new[] { new Vector2(12, 15), new Vector2(10, 19), new Vector2(14, 19) },
            new Color32(31, 24, 17, 255));
        raster.FillRect(5, 20, 14, 5, new Color32(232, 220, 192, 255));
        for (int x = 8; x <= 16; x += 4)
            raster.StrokeLine(new Vector2(x, 20), new Vector2(x, 25), 1f, new Color32(31, 24, 17, 255));
        return raster.ToSprite();
    }

    private static Sprite CreateZombieBloodDrop()
    {
        RasterCanvas raster = new RasterCanvas(8, 10);
        raster.FillEllipse(4, 6, 2.5f, 3.5f, new Color32(122, 10, 10, 255));
        raster.FillPolygon(
            new[] { new Vector2(4, 0), new Vector2(1.7f, 6), new Vector2(6.3f, 6) },
            new Color32(122, 10, 10, 255));
        return raster.ToSprite();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PROCEDURAL GENERATORS — SPIDER
    // ═══════════════════════════════════════════════════════════════════════════

    private static Sprite CreateSpiderFrame()
    {
        RasterCanvas raster = new RasterCanvas(480, 80);
        Vector2[] outer =
        {
            new Vector2(0, 30), new Vector2(22, 4), new Vector2(458, 4),
            new Vector2(480, 30), new Vector2(458, 56), new Vector2(22, 56)
        };
        Vector2[] hole = { new Vector2(52, 18), new Vector2(428, 18), new Vector2(428, 42), new Vector2(52, 42) };

        // Chitin gradient: #3d434b -> #101216 -> #020303
        raster.FillPolygonWithHole(outer, hole, (x, y) => ChitinGradient(y));

        // Gloss polygon (22,4)(458,4)(470,18)(10,18) white .10
        Vector2[] gloss = { new Vector2(22, 4), new Vector2(458, 4), new Vector2(470, 18), new Vector2(10, 18) };
        raster.FillPolygon(gloss, new Color32(255, 255, 255, 26));

        // Dark outer border so the hexagonal box is crisp and clearly visible without green outline
        Color32 darkOuterBorder = new Color32(12, 15, 18, 255);
        raster.StrokeClosedPolygon(outer, 1.8f, darkOuterBorder);

        // Window outline 1px dark metallic
        raster.StrokeClosedPolygon(hole, 1.0f, new Color32(28, 34, 40, 220));

        // Web lines pale #d6f0d6 alpha .6
        Color32 web = new Color32(214, 240, 214, 153);
        raster.StrokeLine(new Vector2(22, 4), new Vector2(52, 18), 1.0f, web);
        raster.StrokeLine(new Vector2(22, 56), new Vector2(52, 42), 1.0f, web);
        raster.StrokeLine(new Vector2(458, 4), new Vector2(428, 18), 1.0f, web);
        raster.StrokeLine(new Vector2(458, 56), new Vector2(428, 42), 1.0f, web);
        raster.StrokeLine(new Vector2(10, 18), new Vector2(52, 24), 1.0f, web);
        raster.StrokeLine(new Vector2(470, 18), new Vector2(428, 24), 1.0f, web);

        // Arcs: quadratic (52,18) ctrl (240,6) (428,18) and (52,42) ctrl (240,54) (428,42)
        raster.StrokeBezier(new Vector2(52, 18), new Vector2(240, 6), new Vector2(428, 18), 1.0f, web);
        raster.StrokeBezier(new Vector2(52, 42), new Vector2(240, 54), new Vector2(428, 42), 1.0f, web);

        // Fangs: triangles (208,55)(218,55)(213,76) and (262,55)(272,55)(267,76), fill #e6ffe9, outline dark
        Color32 fangFill = new Color32(230, 255, 233, 255);
        Vector2[] fang1 = { new Vector2(208, 55), new Vector2(218, 55), new Vector2(213, 76) };
        Vector2[] fang2 = { new Vector2(262, 55), new Vector2(272, 55), new Vector2(267, 76) };
        raster.FillPolygon(fang1, fangFill);
        raster.StrokeClosedPolygon(fang1, 1.2f, darkOuterBorder);
        raster.FillPolygon(fang2, fangFill);
        raster.StrokeClosedPolygon(fang2, 1.2f, darkOuterBorder);

        return raster.ToSprite();
    }

    private static Color32 ChitinGradient(float y)
    {
        float t = Mathf.Clamp01(y / 60f);
        if (t < 0.5f)
            return Color32.Lerp(new Color32(61, 67, 75, 255), new Color32(16, 18, 22, 255), t * 2f);
        return Color32.Lerp(new Color32(16, 18, 22, 255), new Color32(2, 3, 3, 255), (t - 0.5f) * 2f);
    }

    private static Sprite CreateSpiderGlow()
    {
        RasterCanvas raster = new RasterCanvas(392, 40);
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 392; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(8f - x, x - 384f));
                float dy = Mathf.Max(0f, Mathf.Max(8f - y, y - 32f));
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.Clamp01(distance / 7f);
                if (x >= 8f && x < 384f && y >= 8f && y < 32f)
                    alpha = 1f;
                raster.SetPixel(x, y, new Color32(255, 255, 255, (byte)(alpha * 255f)));
            }
        }
        return raster.ToSprite();
    }

    private static Sprite CreateSpiderIcon()
    {
        RasterCanvas raster = new RasterCanvas(24, 24);
        Color32 neon = new Color32(56, 255, 90, 255);

        // Body ellipse rx 5 ry 6.5
        raster.FillEllipse(12, 14, 4.5f, 6.0f, neon);
        // Head circle r 3.2 at (12, 6)
        raster.FillCircle(12, 6, 3.0f, neon);

        // 8 Spider Legs
        raster.StrokeLine(new Vector2(8, 11), new Vector2(2, 7), 1.2f, neon);
        raster.StrokeLine(new Vector2(8, 13), new Vector2(2, 13), 1.2f, neon);
        raster.StrokeLine(new Vector2(8, 15), new Vector2(3, 19), 1.2f, neon);
        raster.StrokeLine(new Vector2(9, 17), new Vector2(5, 23), 1.2f, neon);

        raster.StrokeLine(new Vector2(16, 11), new Vector2(22, 7), 1.2f, neon);
        raster.StrokeLine(new Vector2(16, 13), new Vector2(22, 13), 1.2f, neon);
        raster.StrokeLine(new Vector2(16, 15), new Vector2(21, 19), 1.2f, neon);
        raster.StrokeLine(new Vector2(15, 17), new Vector2(19, 23), 1.2f, neon);

        return raster.ToSprite();
    }

    private static Sprite CreateSpiderAcidDrop()
    {
        RasterCanvas raster = new RasterCanvas(8, 8);
        raster.FillCircle(4, 4, 3.5f, new Color32(139, 255, 122, 255));
        return raster.ToSprite();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PROCEDURAL GENERATORS — BOSS (NAGA — GUARDIAN OF THE HEART)
    // ═══════════════════════════════════════════════════════════════════════════

    private static Sprite CreateBossFrame()
    {
        RasterCanvas raster = new RasterCanvas(700, 96);
        Vector2[] outer =
        {
            new Vector2(70, 10), new Vector2(630, 10), new Vector2(640, 20),
            new Vector2(640, 76), new Vector2(630, 86), new Vector2(70, 86),
            new Vector2(60, 76), new Vector2(60, 20)
        };
        Vector2[] hole = { new Vector2(100, 34), new Vector2(600, 34), new Vector2(600, 62), new Vector2(100, 62) };

        // Gold gradient #fff1a8 -> #e2b93c(.33) -> #8f6a14(.66) -> #4a3508(1.0)
        raster.FillPolygonWithHole(outer, hole, (x, y) => BossGoldGradient(y));
        raster.StrokeClosedPolygon(outer, 1.5f, new Color32(74, 53, 8, 255));
        raster.StrokeClosedPolygon(hole, 1.2f, new Color32(143, 106, 20, 255));

        // Serpent-scale pattern overlay: 10x8 tile, arc from (0,8) ctrl (5,0) to (10,8), stroke #4a3508 1px alpha .7
        Color32 scaleColor = new Color32(74, 53, 8, 178);
        for (int y = 10; y < 86; y += 8)
        {
            int rowOffset = ((y / 8) % 2 == 0) ? 0 : 5;
            for (int x = 60; x < 640; x += 10)
            {
                int ox = x + rowOffset;
                raster.StrokeBezierClipped(new Vector2(ox, y + 8), new Vector2(ox + 5, y), new Vector2(ox + 10, y + 8),
                    1f, scaleColor, outer, hole);
            }
        }

        // Bright gold lines #ffe68a: rect (100,31,500x2) and (100,62,500x2)
        Color32 brightGold = new Color32(255, 230, 138, 255);
        raster.FillRect(100, 31, 500, 2, brightGold);
        raster.FillRect(100, 62, 500, 2, brightGold);

        // Phase notches #ffb82e 2px wide, y 30..66, at x=174, 349, 524 (75/50/25% HP)
        Color32 notchColor = new Color32(255, 184, 46, 255);
        raster.FillRect(174, 30, 2, 36, notchColor);
        raster.FillRect(349, 30, 2, 36, notchColor);
        raster.FillRect(524, 30, 2, 36, notchColor);

        // Red jewel diamond top center: (350,0)(364,12)(350,24)(336,12), fill #e0112b, stroke #ffe68a 2px, inner #ff6b78 .6
        Vector2[] jewel = { new Vector2(350, 0), new Vector2(364, 12), new Vector2(350, 24), new Vector2(336, 12) };
        raster.FillPolygon(jewel, new Color32(224, 17, 43, 255));
        raster.StrokeClosedPolygon(jewel, 2f, brightGold);
        Vector2[] innerJewel = { new Vector2(350, 4), new Vector2(358, 12), new Vector2(350, 20), new Vector2(342, 12) };
        raster.FillPolygon(innerJewel, new Color32(255, 107, 120, 153));

        return raster.ToSprite();
    }

    private static Color32 BossGoldGradient(float y)
    {
        float t = Mathf.Clamp01((y - 10f) / 76f);
        if (t < 0.33f)
            return Color32.Lerp(new Color32(255, 241, 168, 255), new Color32(226, 185, 60, 255), t / 0.33f);
        if (t < 0.66f)
            return Color32.Lerp(new Color32(226, 185, 60, 255), new Color32(143, 106, 20, 255), (t - 0.33f) / 0.33f);
        return Color32.Lerp(new Color32(143, 106, 20, 255), new Color32(74, 53, 8, 255), (t - 0.66f) / 0.34f);
    }

    private static Sprite CreateSnakeHead(bool mirror)
    {
        RasterCanvas raster = new RasterCanvas(76, 96);
        Vector2[] headPoly =
        {
            new Vector2(72, 16), new Vector2(40, 8), new Vector2(6, 22),
            new Vector2(3, 48), new Vector2(6, 74), new Vector2(40, 88), new Vector2(72, 80)
        };

        if (mirror)
        {
            for (int i = 0; i < headPoly.Length; i++)
                headPoly[i] = new Vector2(76f - headPoly[i].x, headPoly[i].y);
        }

        // Gold base fill
        raster.FillPolygon(headPoly, (x, y) => BossGoldGradient(y));
        raster.StrokeClosedPolygon(headPoly, 1.5f, new Color32(74, 53, 8, 255));

        // Serpent scale pattern
        Color32 scaleColor = new Color32(74, 53, 8, 178);
        for (int y = 14; y < 82; y += 8)
        {
            for (int x = 4; x < 72; x += 10)
            {
                raster.StrokeBezierClipped(new Vector2(x, y + 8), new Vector2(x + 5, y), new Vector2(x + 10, y + 8),
                    1f, scaleColor, headPoly, new Vector2[0]);
            }
        }

        // Brow line: (16,32) quad ctrl (40,22) (66,28) #4a3508 2px
        Vector2 p0 = new Vector2(16, 32), p1 = new Vector2(40, 22), p2 = new Vector2(66, 28);
        if (mirror)
        {
            p0 = new Vector2(76f - p0.x, p0.y);
            p1 = new Vector2(76f - p1.x, p1.y);
            p2 = new Vector2(76f - p2.x, p2.y);
        }
        raster.StrokeBezier(p0, p1, p2, 2.0f, new Color32(74, 53, 8, 255));

        // Red Eye ellipse center (36,42) rx 7 ry 4.5 #ff1f2d with slit pupil rx 1.3 ry 4 #120000
        float eyeX = mirror ? (76f - 36f) : 36f;
        raster.FillEllipse(eyeX, 42f, 7f, 4.5f, new Color32(255, 31, 45, 255));
        raster.FillEllipse(eyeX, 42f, 1.3f, 4.0f, new Color32(18, 0, 0, 255));

        // Fangs: white triangles (36,72)(41,72)(38.5,90) and (52,74)(57,74)(54.5,92)
        Vector2[] fang1 = { new Vector2(36, 72), new Vector2(41, 72), new Vector2(38.5f, 90) };
        Vector2[] fang2 = { new Vector2(52, 74), new Vector2(57, 74), new Vector2(54.5f, 92) };
        if (mirror)
        {
            for (int i = 0; i < 3; i++)
            {
                fang1[i] = new Vector2(76f - fang1[i].x, fang1[i].y);
                fang2[i] = new Vector2(76f - fang2[i].x, fang2[i].y);
            }
        }
        Color32 white = new Color32(255, 255, 255, 255);
        raster.FillPolygon(fang1, white);
        raster.StrokeClosedPolygon(fang1, 1f, new Color32(74, 53, 8, 255));
        raster.FillPolygon(fang2, white);
        raster.StrokeClosedPolygon(fang2, 1f, new Color32(74, 53, 8, 255));

        return raster.ToSprite();
    }

    private static Sprite CreateBossFill()
    {
        // Fill: #1fae7a -> #0a5f45 -> #02150f + 2px molten gold inner stroke #ffb82e alpha .6 inset 1px
        RasterCanvas raster = new RasterCanvas(500, 28);
        Color32[] stops =
        {
            new Color32(31, 174, 122, 255), // #1fae7a (top)
            new Color32(10, 95, 69, 255),   // #0a5f45 (mid)
            new Color32(2, 21, 15, 255)     // #02150f (bottom)
        };
        for (int y = 0; y < 28; y++)
        {
            float t = (float)y / 27f;
            Color32 c = t < 0.5f
                ? Color32.Lerp(stops[0], stops[1], t * 2f)
                : Color32.Lerp(stops[1], stops[2], (t - 0.5f) * 2f);
            raster.FillRect(0, y, 500, 1, c);
        }

        // 2px molten gold inner stroke #ffb82e alpha .6 inset 1px
        Color32 goldStroke = new Color32(255, 184, 46, 153);
        raster.StrokeClosedPolygon(new[]
        {
            new Vector2(1, 1), new Vector2(499, 1), new Vector2(499, 27), new Vector2(1, 27)
        }, 2f, goldStroke);

        return raster.ToSprite();
    }

    private static Sprite CreateBossGlow()
    {
        RasterCanvas raster = new RasterCanvas(516, 44);
        for (int y = 0; y < 44; y++)
        {
            for (int x = 0; x < 516; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(8f - x, x - 508f));
                float dy = Mathf.Max(0f, Mathf.Max(8f - y, y - 36f));
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.Clamp01(distance / 7f);
                if (x >= 8f && x < 508f && y >= 8f && y < 36f)
                    alpha = 1f;
                raster.SetPixel(x, y, new Color32(255, 255, 255, (byte)(alpha * 255f)));
            }
        }
        return raster.ToSprite();
    }

    private static Sprite CreateBossNamePlate()
    {
        // Opaque dark gradient plate #1c1409 -> #060402, 1px #8f6a14 border
        RasterCanvas raster = new RasterCanvas(500, 38);
        for (int y = 0; y < 38; y++)
        {
            float t = (float)y / 37f;
            Color32 c = Color32.Lerp(new Color32(28, 20, 9, 255), new Color32(6, 4, 2, 255), t);
            raster.FillRect(0, y, 500, 1, c);
        }
        raster.StrokeClosedPolygon(new[]
        {
            new Vector2(0, 0), new Vector2(500, 0), new Vector2(500, 38), new Vector2(0, 38)
        }, 1.5f, new Color32(143, 106, 20, 255));
        return raster.ToSprite();
    }

    private static Sprite CreateBossSmokePuff()
    {
        RasterCanvas raster = new RasterCanvas(12, 12);
        raster.FillCircle(6, 6, 5.5f, new Color32(180, 180, 180, 255));
        return raster.ToSprite();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SHARED HELPERS & COMMON SPRITE GENERATORS
    // ═══════════════════════════════════════════════════════════════════════════

    private static Sprite CreateGradientSprite(int width, int height, Color32[] stops)
    {
        RasterCanvas raster = new RasterCanvas(width, height);
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / Mathf.Max(1, height - 1);
            float stopPosition = t * (stops.Length - 1);
            int stopIndex = Mathf.Min(Mathf.FloorToInt(stopPosition), stops.Length - 2);
            Color32 color = Color32.Lerp(stops[stopIndex], stops[stopIndex + 1], stopPosition - stopIndex);
            raster.FillRect(0, y, width, 1, color);
        }
        return raster.ToSprite();
    }

    private static Sprite CreateVerticalAlphaSprite(int width, int height, float topAlpha, float bottomAlpha)
    {
        RasterCanvas raster = new RasterCanvas(width, height);
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / Mathf.Max(1, height - 1);
            byte alpha = (byte)(Mathf.Lerp(topAlpha, bottomAlpha, t) * 255f);
            raster.FillRect(0, y, width, 1, new Color32(255, 255, 255, alpha));
        }
        return raster.ToSprite();
    }

    private static Sprite CreateSweepSprite()
    {
        RasterCanvas raster = new RasterCanvas(70, 26);
        for (int x = 0; x < 70; x++)
        {
            float horizontal = 1f - Mathf.Abs((x - 34.5f) / 35f);
            float alpha = Mathf.Clamp01(horizontal) * 0.55f;
            raster.FillRect(x, 0, 1, 26, new Color32(255, 255, 255, (byte)(alpha * 255f)));
        }
        return raster.ToSprite();
    }

    private static Sprite CreateSolidSprite(int width, int height, Color32 color)
    {
        RasterCanvas raster = new RasterCanvas(width, height);
        raster.FillRect(0, 0, width, height, color);
        return raster.ToSprite();
    }

    private static Sprite CreateCircleSprite()
    {
        RasterCanvas raster = new RasterCanvas(8, 8);
        raster.FillCircle(4, 4, 3.8f, new Color32(255, 255, 255, 255));
        return raster.ToSprite();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  HIGH-PRECISION 4X SUPERSAMPLED CPU RASTERIZER (Scanline Even-Odd)
    // ═══════════════════════════════════════════════════════════════════════════

    private sealed class RasterCanvas
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int _scaledWidth;
        private readonly int _scaledHeight;
        private readonly Color32[] _pixels;

        public RasterCanvas(int width, int height)
        {
            _width = width;
            _height = height;
            _scaledWidth = width * Supersample;
            _scaledHeight = height * Supersample;
            _pixels = new Color32[_scaledWidth * _scaledHeight];
        }

        public void SetPixel(int x, int y, Color32 color)
        {
            int px = x * Supersample;
            int py = (_height - 1 - y) * Supersample;
            for (int sy = 0; sy < Supersample; sy++)
            {
                for (int sx = 0; sx < Supersample; sx++)
                    Blend(px + sx, py + sy, color);
            }
        }

        public void FillRect(float x, float y, float width, float height, Color32 color)
        {
            FillRectScaled(x, y, width, height, color);
        }

        public void FillPolygonWithHole(Vector2[] outer, Vector2[] hole, System.Func<float, float, Color32> colorAt)
        {
            float[] intersections = new float[outer.Length + hole.Length];
            int minY = Mathf.Max(0, Mathf.FloorToInt((_height - MaxY(outer)) * Supersample));
            int maxY = Mathf.Min(_scaledHeight, Mathf.CeilToInt((_height - MinY(outer)) * Supersample));
            for (int py = minY; py < maxY; py++)
            {
                float scanY = _height - (py + 0.5f) / Supersample;
                int count = AddScanlineIntersections(outer, scanY, intersections, 0);
                count = AddScanlineIntersections(hole, scanY, intersections, count);
                System.Array.Sort(intersections, 0, count);
                for (int i = 0; i + 1 < count; i += 2)
                {
                    float left = Mathf.Max(0f, intersections[i]);
                    float right = Mathf.Min(_width, intersections[i + 1]);
                    int minX = Mathf.Max(0, Mathf.FloorToInt(left * Supersample));
                    int maxX = Mathf.Min(_scaledWidth, Mathf.CeilToInt(right * Supersample));
                    for (int px = minX; px < maxX; px++)
                    {
                        float sampleX = (px + 0.5f) / Supersample;
                        if (sampleX >= left && sampleX < right)
                            Blend(px, py, colorAt(sampleX, scanY));
                    }
                }
            }
        }

        public void FillPolygon(Vector2[] polygon, Color32 color)
        {
            FillPolygonWithHole(polygon, new Vector2[0], (x, y) => color);
        }

        public void FillPolygon(Vector2[] polygon, System.Func<float, float, Color32> colorAt)
        {
            FillPolygonWithHole(polygon, new Vector2[0], colorAt);
        }

        public void FillCircleClippedToPolygon(float centerX, float centerY, float radius, Vector2[] outer, Vector2[] hole, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((centerX - radius) * Supersample));
            int maxX = Mathf.Min(_scaledWidth, Mathf.CeilToInt((centerX + radius) * Supersample));
            int minY = Mathf.Max(0, Mathf.FloorToInt((_height - centerY - radius) * Supersample));
            int maxY = Mathf.Min(_scaledHeight, Mathf.CeilToInt((_height - centerY + radius) * Supersample));
            float radiusSquared = radius * radius;
            for (int py = minY; py < maxY; py++)
            {
                float sampleY = _height - (py + 0.5f) / Supersample;
                for (int px = minX; px < maxX; px++)
                {
                    float sampleX = (px + 0.5f) / Supersample;
                    float dx = sampleX - centerX;
                    float dy = sampleY - centerY;
                    if (dx * dx + dy * dy <= radiusSquared &&
                        PointInPolygon(sampleX, sampleY, outer) && !PointInPolygon(sampleX, sampleY, hole))
                        Blend(px, py, color);
                }
            }
        }

        public void FillRectClippedToPolygon(float x, float y, float width, float height, Vector2[] outer, Color32 color, Vector2[] hole)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(x));
            int maxX = Mathf.Min(_width, Mathf.CeilToInt(x + width));
            int minY = Mathf.Max(0, Mathf.FloorToInt(y));
            int maxY = Mathf.Min(_height, Mathf.CeilToInt(y + height));
            for (int py = minY; py < maxY; py++)
            {
                for (int px = minX; px < maxX; px++)
                {
                    if (PointInPolygon(px + 0.5f, py + 0.5f, outer) && !PointInPolygon(px + 0.5f, py + 0.5f, hole))
                        FillRectScaled(px, py, 1f, 1f, color);
                }
            }
        }

        public void FillCircle(float centerX, float centerY, float radius, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((centerX - radius) * Supersample));
            int maxX = Mathf.Min(_scaledWidth, Mathf.CeilToInt((centerX + radius) * Supersample));
            int minY = Mathf.Max(0, Mathf.FloorToInt((_height - centerY - radius) * Supersample));
            int maxY = Mathf.Min(_scaledHeight, Mathf.CeilToInt((_height - centerY + radius) * Supersample));
            float radiusSquared = radius * radius;
            for (int py = minY; py < maxY; py++)
            {
                float sampleY = _height - (py + 0.5f) / Supersample;
                for (int px = minX; px < maxX; px++)
                {
                    float dx = (px + 0.5f) / Supersample - centerX;
                    float dy = sampleY - centerY;
                    if (dx * dx + dy * dy <= radiusSquared)
                        Blend(px, py, color);
                }
            }
        }

        public void FillEllipse(float centerX, float centerY, float radiusX, float radiusY, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((centerX - radiusX) * Supersample));
            int maxX = Mathf.Min(_scaledWidth, Mathf.CeilToInt((centerX + radiusX) * Supersample));
            int minY = Mathf.Max(0, Mathf.FloorToInt((_height - centerY - radiusY) * Supersample));
            int maxY = Mathf.Min(_scaledHeight, Mathf.CeilToInt((_height - centerY + radiusY) * Supersample));
            float inverseRadiusX = 1f / Mathf.Max(radiusX, 0.001f);
            float inverseRadiusY = 1f / Mathf.Max(radiusY, 0.001f);
            for (int py = minY; py < maxY; py++)
            {
                float sampleY = _height - (py + 0.5f) / Supersample;
                for (int px = minX; px < maxX; px++)
                {
                    float dx = ((px + 0.5f) / Supersample - centerX) * inverseRadiusX;
                    float dy = (sampleY - centerY) * inverseRadiusY;
                    if (dx * dx + dy * dy <= 1f)
                        Blend(px, py, color);
                }
            }
        }

        public void StrokeCircle(float centerX, float centerY, float radius, float thickness, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((centerX - radius - thickness) * Supersample));
            int maxX = Mathf.Min(_scaledWidth, Mathf.CeilToInt((centerX + radius + thickness) * Supersample));
            int minY = Mathf.Max(0, Mathf.FloorToInt((_height - centerY - radius - thickness) * Supersample));
            int maxY = Mathf.Min(_scaledHeight, Mathf.CeilToInt((_height - centerY + radius + thickness) * Supersample));
            float inner = Mathf.Max(0f, radius - thickness * 0.5f);
            float outer = radius + thickness * 0.5f;
            for (int py = minY; py < maxY; py++)
            {
                float sampleY = _height - (py + 0.5f) / Supersample;
                for (int px = minX; px < maxX; px++)
                {
                    float dx = (px + 0.5f) / Supersample - centerX;
                    float dy = sampleY - centerY;
                    float distance = dx * dx + dy * dy;
                    if (distance >= inner * inner && distance <= outer * outer)
                        Blend(px, py, color);
                }
            }
        }

        public void StrokePolyline(Vector2[] points, float thickness, Color32 color)
        {
            for (int i = 1; i < points.Length; i++)
                StrokeLine(points[i - 1], points[i], thickness, color);
        }

        public void StrokeClosedPolygon(Vector2[] points, float thickness, Color32 color)
        {
            StrokePolyline(points, thickness, color);
            StrokeLine(points[points.Length - 1], points[0], thickness, color);
        }

        public void StrokeBezier(Vector2 p0, Vector2 p1, Vector2 p2, float thickness, Color32 color)
        {
            const int segments = 24;
            Vector2 prev = p0;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float u = 1f - t;
                Vector2 curr = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                StrokeLine(prev, curr, thickness, color);
                prev = curr;
            }
        }

        public void StrokeBezierClipped(Vector2 p0, Vector2 p1, Vector2 p2, float thickness, Color32 color, Vector2[] outer, Vector2[] hole)
        {
            const int segments = 16;
            Vector2 prev = p0;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float u = 1f - t;
                Vector2 curr = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                if (PointInPolygon(curr.x, curr.y, outer) && !PointInPolygon(curr.x, curr.y, hole))
                    StrokeLine(prev, curr, thickness, color);
                prev = curr;
            }
        }

        public void StrokeLine(Vector2 start, Vector2 end, float thickness, Color32 color)
        {
            float radius = thickness * 0.5f;
            int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(start.x, end.x) - radius) * Supersample));
            int maxX = Mathf.Min(_scaledWidth, Mathf.CeilToInt((Mathf.Max(start.x, end.x) + radius) * Supersample));
            int minY = Mathf.Max(0, Mathf.FloorToInt((_height - Mathf.Max(start.y, end.y) - radius) * Supersample));
            int maxY = Mathf.Min(_scaledHeight, Mathf.CeilToInt((_height - Mathf.Min(start.y, end.y) + radius) * Supersample));
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            for (int py = minY; py < maxY; py++)
            {
                float sampleY = _height - (py + 0.5f) / Supersample;
                for (int px = minX; px < maxX; px++)
                {
                    Vector2 point = new Vector2((px + 0.5f) / Supersample, sampleY);
                    float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared) : 0f;
                    if ((point - (start + segment * t)).sqrMagnitude <= radius * radius)
                        Blend(px, py, color);
                }
            }
        }

        public Sprite ToSprite()
        {
            Texture2D texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false, false)
            {
                name = "HealthBar Procedural Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color32[] result = new Color32[_width * _height];
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int r = 0;
                    int g = 0;
                    int b = 0;
                    int a = 0;
                    int baseX = x * Supersample;
                    int baseY = y * Supersample;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            Color32 pixel = _pixels[(baseY + sy) * _scaledWidth + baseX + sx];
                            r += pixel.r;
                            g += pixel.g;
                            b += pixel.b;
                            a += pixel.a;
                        }
                    }
                    const int sampleCount = Supersample * Supersample;
                    result[y * _width + x] = new Color32((byte)(r / sampleCount), (byte)(g / sampleCount),
                        (byte)(b / sampleCount), (byte)(a / sampleCount));
                }
            }
            texture.SetPixels32(result);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, _width, _height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "HealthBar Procedural Sprite";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private void FillRectScaled(float x, float y, float width, float height, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(x * Supersample));
            int maxX = Mathf.Min(_scaledWidth, Mathf.CeilToInt((x + width) * Supersample));
            int minY = Mathf.Max(0, Mathf.FloorToInt((_height - y - height) * Supersample));
            int maxY = Mathf.Min(_scaledHeight, Mathf.CeilToInt((_height - y) * Supersample));
            for (int py = minY; py < maxY; py++)
            {
                for (int px = minX; px < maxX; px++)
                    Blend(px, py, color);
            }
        }

        private void Blend(int x, int y, Color32 source)
        {
            int index = y * _scaledWidth + x;
            if (source.a == 255)
            {
                _pixels[index] = source;
                return;
            }
            if (source.a == 0)
                return;

            Color32 destination = _pixels[index];
            float alpha = source.a / 255f;
            float destinationAlpha = destination.a / 255f;
            float outAlpha = alpha + destinationAlpha * (1f - alpha);
            if (outAlpha <= 0f)
                return;
            float inverse = 1f - alpha;
            _pixels[index] = new Color32(
                (byte)((source.r * alpha + destination.r * destinationAlpha * inverse) / outAlpha),
                (byte)((source.g * alpha + destination.g * destinationAlpha * inverse) / outAlpha),
                (byte)((source.b * alpha + destination.b * destinationAlpha * inverse) / outAlpha),
                (byte)(outAlpha * 255f));
        }

        private static int AddScanlineIntersections(Vector2[] polygon, float scanY, float[] intersections, int count)
        {
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[j];
                Vector2 b = polygon[i];
                if ((a.y > scanY) != (b.y > scanY))
                    intersections[count++] = a.x + (scanY - a.y) * (b.x - a.x) / (b.y - a.y);
            }
            return count;
        }

        private static bool PointInPolygon(float x, float y, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static float MinX(Vector2[] points)
        {
            float result = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
                result = Mathf.Min(result, points[i].x);
            return result;
        }

        private static float MaxX(Vector2[] points)
        {
            float result = float.MinValue;
            for (int i = 0; i < points.Length; i++)
                result = Mathf.Max(result, points[i].x);
            return result;
        }

        private static float MinY(Vector2[] points)
        {
            float result = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
                result = Mathf.Min(result, points[i].y);
            return result;
        }

        private static float MaxY(Vector2[] points)
        {
            float result = float.MinValue;
            for (int i = 0; i < points.Length; i++)
                result = Mathf.Max(result, points[i].y);
            return result;
        }
    }
}
