using Dalamud.Configuration;

namespace MinimapZoom;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public float LastZoom { get; set; } = ZoomPolicy.Minimum;
    public bool SquareMinimap { get; set; }
    public bool HideMarkers { get; set; }
    public MarkerCategory HiddenCategories { get; set; }
    public float MarkerScale { get; set; } = 1f;
    public float PlayerScale { get; set; } = 1f;
    public bool HideFrame { get; set; }
    public bool HideSunMoon { get; set; }
    public SquareFrameStyle FrameStyle { get; set; } = SquareFrameStyle.Thin;
    public uint FrameColor { get; set; } = 0xFFE4D5B7;
    public bool EnableZoomOnStartup { get; set; }
    public bool OpenWindowOnStartup { get; set; }
    public WindowPreferences? WindowAppearance { get; set; }

    internal AppearanceSettings Appearance => new AppearanceSettings(SquareMinimap, HideMarkers,
        HiddenCategories, MarkerScale, PlayerScale, HideFrame, FrameStyle, FrameColor, HideSunMoon).Normalize();
}
