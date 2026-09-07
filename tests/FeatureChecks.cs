using System.Text.Json;
using MinimapZoom;

internal static class FeatureChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("Profile migration preserves existing preferences without enabling new effects", () =>
        {
            var config = JsonSerializer.Deserialize<Configuration>("""{"Version":4,"LastZoom":0.7,"SquareMinimap":true,"HideWeather":true,"FrameStyle":4,"FrameColor":2158743023} """)!;
            var before = config.Appearance;
            var store = new ProfileStore(config);
            Require(store.Active.Appearance == before && store.Active.Zoom == 0.7f, "Migration changed the active map");
            Require(config.Profiles.Count == 4 && !config.AutomaticProfiles && !config.Shortcut.Enabled, "New behavior silently activated");
            Require(before.MapOpacity == 1 && !before.HideCardinals && !before.HideCoordinates, "New appearance defaults changed the map");
            new ProfileStore(config);
            Require(config.Profiles.Count == 4, "Repeated loading duplicated presets");
        });
        check("Profiles persist all settings with both serializers and are independent copies", () =>
        {
            var config = new Configuration(); var store = new ProfileStore(config);
            var originalId = store.ActiveId;
            var appearance = AppearanceSettings.Default with { HideCoordinates = true, HideCardinals = true, MapOpacity = 0.43f,
                FrameThickness = 7, CornerLength = 44, ReduceCrowding = true, HiddenCategories = MarkerCategory.Shops };
            store.Remember(0.4f, appearance); store.Duplicate("Voyage");
            store.Remember(0.6f, appearance with { MapOpacity = 0.8f });
            Require(config.Profiles.First(p => p.Id == originalId).Appearance.MapOpacity == 0.43f, "Duplicate shares mutable preferences");
            config.Shortcut = new() { Enabled = true, Key = 0x76, Control = false, Alt = true, Zoom = 0.3f };
            config.EffectsSuspended = true;
            foreach (var reload in new[] { JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!,
                Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(Newtonsoft.Json.JsonConvert.SerializeObject(config))! })
            {
                var restored = new ProfileStore(reload);
                Require(restored.Active.Appearance == store.Active.Appearance && restored.Active.Zoom == 0.6f,
                    "Profile preferences lost on reload");
                Require(reload.Shortcut == config.Shortcut, "Shortcut lost on reload");
                Require(reload.EffectsSuspended, "Restored native view was not preserved across reload");
            }
        });
        check("Automatic profiles return to manual fallback and manual selection pauses automation", () =>
        {
            var config = new Configuration(); var store = new ProfileStore(config);
            var manual = store.ActiveId; var city = config.Profiles[2].Id;
            store.Bind(100, "Ville fictive", city); config.AutomaticProfiles = true;
            Require(store.ForZone(100) && store.ActiveId == city, "Zone did not select its profile");
            store.Remember(0.65f, store.Active.Appearance);
            Require(store.ForZone(101) && store.ActiveId == manual, "Unbound zone did not restore fallback");
            Require(config.Profiles[2].Zoom == 0.65f && config.Profiles[0].Zoom == 0.25f, "Automatic edits leaked into fallback");
            store.ForZone(100); store.Select(manual, true);
            Require(!config.AutomaticProfiles && !store.ForZone(100), "Manual selection silently overridden");
        });
        check("Deleting profiles cleans only their rules and always leaves a valid fallback", () =>
        {
            var config = new Configuration(); var store = new ProfileStore(config);
            var deleted = store.ActiveId;
            store.Bind(100, "Zone A", deleted); store.Bind(101, "Zone B", config.Profiles[1].Id);
            store.DeleteActive();
            Require(!config.Profiles.Any(p => p.Id == deleted) && config.ZoneRules.Count == 1 &&
                config.Profiles.Any(p => p.Id == config.ManualProfileId), "Delete corrupted remaining profiles");
            while (config.Profiles.Count > 1) store.DeleteActive();
            store.DeleteActive(); Require(config.Profiles.Count == 1, "Deleted the last profile");
        });
        check("Malformed configurations normalize without unsafe values or dangling rules", () =>
        {
            var config = new Configuration { Profiles = [new() { Id = "same", Name = "##\n", Zoom = float.NaN },
                new() { Id = "same", Appearance = AppearanceSettings.Default with { MapOpacity = float.PositiveInfinity,
                    CornerLength = -1, FrameThickness = 999 } }], ZoneRules = [new(100, "X", "missing")], Shortcut = null! };
            _ = new ProfileStore(config);
            Require(config.Profiles.Select(p => p.Id).Distinct().Count() == 2 && config.ZoneRules.Count == 0, "Invalid identities survived");
            Require(config.Profiles[1].Appearance.MapOpacity == 1 && config.Profiles[1].Appearance.FrameThickness == 8 &&
                config.Profiles[1].Appearance.CornerLength == 8 && config.Shortcut != null, "Unsafe settings survived");
        });
        check("Temporary zoom restores enabled state and never zooms in", () =>
        {
            foreach (var enabled in new[] { false, true })
            {
                var hold = new TemporaryZoom();
                hold.Update(false, true, enabled, 0.75f, 0.25f);
                Require(hold.Update(true, true, enabled, 0.75f, 0.25f) == HoldTransition.Start && hold.Target == 0.25f &&
                    hold.WasEnabled == enabled, "Press did not capture the original state");
                Require(hold.Update(true, true, true, 0.25f, 0.5f) == HoldTransition.None && hold.Target == 0.25f, "Held input replaced its snapshot");
                Require(hold.Update(false, true, true, 0.25f, 0.25f) == HoldTransition.End && !hold.Active, "Release did not restore");
                hold.Update(true, true, enabled, 0.3f, 1f);
                Require(hold.Target == 0.3f, "Overview zoomed in");
            }
        });
        check("Focus loss, typing, cancellation and zone changes require a fresh shortcut press", () =>
        {
            var hold = new TemporaryZoom();
            Require(hold.Update(true, true, true, 1, 0.25f) == HoldTransition.None, "Held key activated on load");
            hold.Update(false, true, true, 1, 0.25f); hold.Update(true, true, true, 1, 0.25f);
            Require(hold.Update(false, false, true, 0.25f, 0.25f) == HoldTransition.End, "Loss of context did not end overview");
            Require(hold.Update(true, true, true, 1, 0.25f) == HoldTransition.None, "Regained focus reactivated a held key");
            hold.Update(false, true, true, 1, 0.25f); hold.Update(true, true, true, 1, 0.25f);
            Require(hold.Cancel() && !hold.Active, "Zone change failed to cancel");
            Require(hold.Update(true, true, true, 1, 0.25f) == HoldTransition.None, "Zone change reactivated a held key");
        });
        check("Crowding shrinks only secondary icons near visible peers with a common parent", () =>
        {
            MarkerFootprint[] points = [new(0, 0, 1, true, true), new(2, 2, 1, true, false), new(100, 100, 1, true, true)];
            Require(MarkerCrowding.Scale(points, 0, 1) == 0.65f && MarkerCrowding.Scale(points, 1, 1) == 1 &&
                MarkerCrowding.Scale(points, 2, 1) == 1, "Crowding changed a protected or distant icon");
            points[1] = points[1] with { Visible = false };
            Require(MarkerCrowding.Scale(points, 0, 1) == 1, "Hidden icon affected crowding");
            points[1] = points[1] with { Visible = true, Parent = 2 };
            Require(MarkerCrowding.Scale(points, 0, 1) == 1, "Compared different coordinate spaces");
            Require(MarkerCrowding.IsSecondary(60412, 0) && !MarkerCrowding.IsSecondary(60412, 999999) &&
                !MarkerCrowding.IsSecondary(60421, 0) && !MarkerCrowding.IsSecondary(60453, 0), "Quest, player or navigation protection lost");
        });
        check("Frame dimensions change stroke and corner length while keeping the center clear", () =>
        {
            static byte Alpha(byte[] pixels, int x, int y) => pixels[(y * 100 + x) * 4 + 3];
            var shortFrame = FramePixels.Create(100, 100, 0, 0, 100, 100, SquareFrameStyle.Corners, 0xFFFFFFFF, 2, 10);
            var longFrame = FramePixels.Create(100, 100, 0, 0, 100, 100, SquareFrameStyle.Corners, 0xFFFFFFFF, 5, 30);
            Require(Alpha(shortFrame, 1, 15) == 0 && Alpha(longFrame, 1, 15) == 255 && Alpha(longFrame, 4, 15) == 255 &&
                Alpha(longFrame, 5, 15) == 0 && Alpha(longFrame, 50, 50) == 0, "Custom frame geometry is incorrect");
        });
        check("Filter presets preserve quest overlays, player groups and navigation", () =>
        {
            var travel = MarkerPresets.Hidden(MarkerPreset.Travel);
            var quests = MarkerPresets.Hidden(MarkerPreset.Quests);
            Require(MarkerPolicy.ShouldHide(60412, 0, travel, false) && !MarkerPolicy.ShouldHide(60453, 0, travel, false) &&
                !MarkerPolicy.ShouldHide(60412, 999999, travel, false), "Travel preset hides a protected marker");
            Require(!MarkerPolicy.ShouldHide(71021, 0, quests, false) && MarkerPolicy.ShouldHide(60434, 0, quests, false), "Quest preset has unexpected categories");
        });
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
