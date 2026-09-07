namespace MinimapZoom;

[Serializable]
public sealed record MinimapProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Personnel";
    public float Zoom { get; set; } = ZoomPolicy.Minimum;
    public AppearanceSettings Appearance { get; set; } = AppearanceSettings.Default;
}

[Serializable]
public sealed record ZoneProfileRule(uint TerritoryId, string ZoneName, string ProfileId);

// Owns persistent profile edits on the framework thread. Views receive copies.
internal sealed class ProfileStore
{
    public const int MaximumProfiles = 20;
    private readonly Configuration config;
    public string ActiveId { get; private set; }
    public MinimapProfile Active => config.Profiles.First(p => p.Id == ActiveId);

    public ProfileStore(Configuration config)
    {
        this.config = config;
        config.Profiles ??= [];
        config.Profiles = config.Profiles.Where(p => p != null).Take(MaximumProfiles).ToList();
        var ids = new HashSet<string>();
        foreach (var p in config.Profiles)
        {
            if (string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id))
            { p.Id = Guid.NewGuid().ToString("N"); ids.Add(p.Id); }
            p.Name = Name(p.Name); p.Zoom = ZoomPolicy.Extended(p.Zoom); p.Appearance = p.Appearance.Normalize();
        }
        if (config.Profiles.Count == 0)
        {
            config.Profiles.Add(new() { Name = "Personnel", Zoom = ZoomPolicy.Extended(config.LastZoom), Appearance = config.Appearance });
            config.Profiles.AddRange(BuiltIns());
        }
        if (!config.Profiles.Any(p => p.Id == config.ManualProfileId)) config.ManualProfileId = config.Profiles[0].Id;
        ActiveId = config.ManualProfileId;
        config.ZoneRules = (config.ZoneRules ?? []).Where(r => r != null && r.TerritoryId != 0 &&
            config.Profiles.Any(p => p.Id == r.ProfileId)).GroupBy(r => r.TerritoryId).Select(g => g.Last()).ToList();
        config.Shortcut = (config.Shortcut ?? new()).Normalize();
        config.Version = 5;
    }

    public static string Name(string? text)
    {
        var name = new string((text ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim().Replace("##", "");
        return string.IsNullOrWhiteSpace(name) ? "Personnel" : name[..Math.Min(name.Length, 40)];
    }

    public void Remember(float zoom, AppearanceSettings appearance)
    { Active.Zoom = ZoomPolicy.Extended(zoom); Active.Appearance = appearance.Normalize(); }

    public bool Select(string id, bool manual)
    {
        if (!config.Profiles.Any(p => p.Id == id)) return false;
        if (manual) { config.ManualProfileId = id; config.AutomaticProfiles = false; }
        var changed = ActiveId != id; ActiveId = id; return changed;
    }

    public bool ForZone(uint territoryId)
    {
        var id = config.AutomaticProfiles ? config.ZoneRules.FirstOrDefault(r => r.TerritoryId == territoryId)?.ProfileId : null;
        return Select(id ?? config.ManualProfileId, false);
    }

    public void Duplicate(string name)
    {
        if (config.Profiles.Count >= MaximumProfiles) return;
        var copy = Active with { Id = Guid.NewGuid().ToString("N"), Name = Name(name) };
        config.Profiles.Add(copy); Select(copy.Id, true);
    }

    public void DeleteActive()
    {
        if (config.Profiles.Count <= 1) return;
        var id = ActiveId;
        config.Profiles.RemoveAll(p => p.Id == id);
        config.ZoneRules.RemoveAll(r => r.ProfileId == id);
        if (config.ManualProfileId == id) config.ManualProfileId = config.Profiles[0].Id;
        Select(config.ManualProfileId, true);
    }

    public void Bind(uint territoryId, string zoneName, string? profileId)
    {
        if (territoryId == 0) return;
        config.ZoneRules.RemoveAll(r => r.TerritoryId == territoryId);
        if (config.Profiles.Any(p => p.Id == profileId)) config.ZoneRules.Add(new(territoryId, zoneName, profileId!));
    }

    private static MinimapProfile[] BuiltIns() =>
    [
        new() { Name = "Exploration", Zoom = 0.25f, Appearance = AppearanceSettings.Default with
            { Square = true, FrameStyle = SquareFrameStyle.Corners, HideButtons = true, ReduceCrowding = true } },
        new() { Name = "Ville", Zoom = 0.5f, Appearance = AppearanceSettings.Default with
            { Square = true, HiddenCategories = MarkerPresets.Hidden(MarkerPreset.Travel), ReduceCrowding = true } },
        new() { Name = "Minimaliste", Zoom = 0.35f, Appearance = AppearanceSettings.Default with
            { Square = true, HideFrame = true, HideSunMoon = true, HideWeather = true, HideButtons = true,
              HideCardinals = true, HideCoordinates = true, MapOpacity = 0.8f } },
    ];
}

public enum MarkerPreset { All, Travel, Quests }

internal static class MarkerPresets
{
    public static MarkerCategory Hidden(MarkerPreset preset) => preset switch
    {
        MarkerPreset.Travel => MarkerCategory.Shops | MarkerCategory.AvailableSideQuests | MarkerCategory.AvailableLevequests |
            MarkerCategory.Fates | MarkerCategory.TripleTriad,
        MarkerPreset.Quests => MarkerCategory.Shops | MarkerCategory.Services | MarkerCategory.AvailableLevequests |
            MarkerCategory.TripleTriad,
        _ => MarkerCategory.None,
    };
}
