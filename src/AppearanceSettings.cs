namespace MinimapZoom;

public enum SquareFrameStyle { None, Thin, Bold, Double, Corners }

public readonly record struct AppearanceSettings(
    bool Square, bool HideMarkers, MarkerCategory HiddenCategories,
    float MarkerScale, float PlayerScale, bool HideFrame, SquareFrameStyle FrameStyle, uint FrameColor,
    bool HideSunMoon = false, bool HideWeather = false, bool HideButtons = false,
    bool HideCardinals = false, bool HideCoordinates = false, float MapOpacity = 1f,
    int FrameThickness = 0, int CornerLength = 22, bool ReduceCrowding = false)
{
    public static AppearanceSettings Default => new(false, false, MarkerCategory.None, 1f, 1f,
        false, SquareFrameStyle.Thin, 0xFFE4D5B7);

    public AppearanceSettings Normalize() => this with
    {
        HiddenCategories = HiddenCategories & MarkerCategory.All,
        MarkerScale = MarkerPolicy.Scale(MarkerScale),
        PlayerScale = MarkerPolicy.Scale(PlayerScale),
        FrameStyle = Enum.IsDefined(FrameStyle) ? FrameStyle : SquareFrameStyle.Thin,
        MapOpacity = float.IsFinite(MapOpacity) ? Math.Clamp(MapOpacity, 0f, 1f) : 1f,
        FrameThickness = Math.Clamp(FrameThickness, 0, 8),
        CornerLength = Math.Clamp(CornerLength, 8, 64),
    };

    public bool HasOverrides => Square || HideMarkers || HiddenCategories != MarkerCategory.None ||
        MarkerScale != 1f || PlayerScale != 1f || HideFrame || HideSunMoon || HideWeather || HideButtons ||
        HideCardinals || HideCoordinates || MapOpacity != 1f || ReduceCrowding;
}
