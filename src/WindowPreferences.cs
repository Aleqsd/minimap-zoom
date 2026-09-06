namespace MinimapZoom;

public enum WindowSkin { LMeter, Obsidienne, Dalamud }
public enum WindowTypeface { Expressway, Dalamud, SegoeUI }
public enum LabelRelief { None, Shadow, Outline }

public sealed record WindowPreferences
{
    public WindowSkin Skin { get; init; } = WindowSkin.LMeter;
    public WindowTypeface Typeface { get; init; } = WindowTypeface.Expressway;
    public float FontSize { get; init; } = 17;
    public uint Background { get; init; } = 0xFF111111;
    public float BackgroundOpacity { get; init; } = 0.95f;
    public uint Text { get; init; } = 0xFFFFFFFF;
    public uint Accent { get; init; } = 0xFF00B9F7;
    public LabelRelief Relief { get; init; } = LabelRelief.Outline;
    public float Padding { get; init; } = 12;
    public float RowSpacing { get; init; } = 8;
    public float LabelOffsetX { get; init; }
    public float LabelOffsetY { get; init; }
    public bool AlignLabelsRight { get; init; }

    public static WindowPreferences Resolve(WindowPreferences? saved, bool existing, float defaultFontSize = 17) =>
        (saved ?? (existing ? Preset(WindowSkin.Dalamud) with { FontSize = defaultFontSize } : new())).Normalize();

    public static WindowPreferences Preset(WindowSkin skin) => skin switch
    {
        WindowSkin.Obsidienne => new() { Skin = skin, Background = 0xFF151B1F, Accent = 0xFF81DFC2,
            Text = 0xFFEAF1EF, Relief = LabelRelief.Shadow, BackgroundOpacity = 0.97f },
        WindowSkin.Dalamud => new() { Skin = skin, Typeface = WindowTypeface.Dalamud, Relief = LabelRelief.None },
        _ => new(),
    };

    public WindowPreferences Normalize() => this with
    {
        Skin = Enum.IsDefined(Skin) ? Skin : WindowSkin.LMeter,
        Typeface = Enum.IsDefined(Typeface) ? Typeface : WindowTypeface.Dalamud,
        Relief = Enum.IsDefined(Relief) ? Relief : LabelRelief.None,
        FontSize = Clamp(FontSize, 14, 26, 17),
        BackgroundOpacity = Clamp(BackgroundOpacity, 0.15f, 1, 0.95f),
        Padding = Clamp(Padding, 8, 20, 12),
        RowSpacing = Clamp(RowSpacing, 4, 16, 8),
        LabelOffsetX = Clamp(LabelOffsetX, -4, 4, 0),
        LabelOffsetY = Clamp(LabelOffsetY, -4, 4, 0),
        Text = Text | 0xFF000000,
    };

    private static float Clamp(float value, float min, float max, float fallback) =>
        Math.Clamp(float.IsFinite(value) ? value : fallback, min, max);
}
