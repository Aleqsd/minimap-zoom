namespace MinimapZoom;

public enum SquareFrameStyle { None, Thin, Bold, Double, Corners }

internal readonly record struct AppearanceSettings(
    bool Square, bool HideMarkers, MarkerCategory HiddenCategories,
    float MarkerScale, float PlayerScale, bool HideFrame, SquareFrameStyle FrameStyle, uint FrameColor,
    bool HideSunMoon = false)
{
    public static AppearanceSettings Default => new(false, false, MarkerCategory.None, 1f, 1f,
        false, SquareFrameStyle.Thin, 0xFFE4D5B7);

    public AppearanceSettings Normalize() => this with
    {
        HiddenCategories = HiddenCategories & MarkerCategory.All,
        MarkerScale = MarkerPolicy.Scale(MarkerScale),
        PlayerScale = MarkerPolicy.Scale(PlayerScale),
        FrameStyle = Enum.IsDefined(FrameStyle) ? FrameStyle : SquareFrameStyle.Thin,
    };

    public bool HasOverrides => Square || HideMarkers || HiddenCategories != MarkerCategory.None ||
        MarkerScale != 1f || PlayerScale != 1f || HideFrame || HideSunMoon;
}
