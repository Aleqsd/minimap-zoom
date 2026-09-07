using Dalamud.Configuration;

namespace MinimapZoom;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 5;
    public float LastZoom { get; set; } = ZoomPolicy.Minimum;
    public bool SquareMinimap { get; set; }
    public bool HideMarkers { get; set; }
    public MarkerCategory HiddenCategories { get; set; }
    public float MarkerScale { get; set; } = 1f;
    public float PlayerScale { get; set; } = 1f;
    public bool HideFrame { get; set; }
    public bool HideSunMoon { get; set; }
    public bool HideWeather { get; set; }
    public bool HideButtons { get; set; }
    public SquareFrameStyle FrameStyle { get; set; } = SquareFrameStyle.Thin;
    public uint FrameColor { get; set; } = 0xFFE4D5B7;
    public bool EnableZoomOnStartup { get; set; }
    public bool OpenWindowOnStartup { get; set; }
    public bool HideCardinals { get; set; }
    public bool HideCoordinates { get; set; }
    public float MapOpacity { get; set; } = 1f;
    public int FrameThickness { get; set; }
    public int CornerLength { get; set; } = 22;
    public bool ReduceCrowding { get; set; }
    public List<MinimapProfile> Profiles { get; set; } = [];
    public string ManualProfileId { get; set; } = "";
    public bool AutomaticProfiles { get; set; }
    public List<ZoneProfileRule> ZoneRules { get; set; } = [];
    public ZoomShortcut Shortcut { get; set; } = new();
    public bool EffectsSuspended { get; set; }

    internal AppearanceSettings Appearance => new AppearanceSettings(SquareMinimap, HideMarkers,
        HiddenCategories, MarkerScale, PlayerScale, HideFrame, FrameStyle, FrameColor,
        HideSunMoon, HideWeather, HideButtons, HideCardinals, HideCoordinates, MapOpacity,
        FrameThickness, CornerLength, ReduceCrowding).Normalize();

    internal void SetAppearance(AppearanceSettings value)
    {
        var p = value.Normalize();
        SquareMinimap = p.Square; HideMarkers = p.HideMarkers; HiddenCategories = p.HiddenCategories;
        MarkerScale = p.MarkerScale; PlayerScale = p.PlayerScale; HideFrame = p.HideFrame;
        HideSunMoon = p.HideSunMoon; HideWeather = p.HideWeather; HideButtons = p.HideButtons;
        FrameStyle = p.FrameStyle; FrameColor = p.FrameColor; HideCardinals = p.HideCardinals;
        HideCoordinates = p.HideCoordinates; MapOpacity = p.MapOpacity;
        FrameThickness = p.FrameThickness; CornerLength = p.CornerLength; ReduceCrowding = p.ReduceCrowding;
    }
}
