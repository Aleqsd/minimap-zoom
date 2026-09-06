using System.Runtime.InteropServices;
using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using MinimapZoom;

internal static unsafe class AppearanceChecks
{
    private const uint Hidden = 1u << 18;
    private const uint Elliptical = 1u << 23;

    public static void Run(Action<string, Action> check)
    {
        check("Square mask covers the collision rectangle, with opaque black margins", () =>
        {
            var pixels = SquareMaskPixels.Create(176, 176, 11f / 176, 10f / 176, 167f / 176, 166f / 176);
            Require(pixels[(10 * 176 + 11) * 4] == 255, "Top-left corner must be covered");
            Require(pixels[(165 * 176 + 166) * 4] == 255, "Bottom-right corner must be covered");
            Require(pixels[(9 * 176 + 11) * 4] == 0 && pixels[(10 * 176 + 10) * 4] == 0, "Margin must be black");
            Require(Enumerable.Range(0, 176 * 176).All(i => pixels[i * 4 + 3] == 255), "Alpha must remain opaque");
        });
        check("Unsupported mask geometry is rejected before allocation", () =>
        {
            foreach (var size in new[] { 0, -1, 4096 })
            {
                var rejected = false;
                try { SquareMaskPixels.Create(size, 176, 0, 0, 1, 1); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "Invalid dimensions accepted");
            }
        });
        check("Square mode swaps only a private mask asset and preserves all UVs", () =>
        {
            using var fixture = new Fixture();
            fixture.Appearance.Apply(fixture.Addon, true, false);
            var part = fixture.Mask->PartsList->Parts[0];
            Require(fixture.Mask->PartsList != fixture.OriginalParts && fixture.Mask->PartId == 0, "Private list not installed");
            Require(part.U == 0 && part.V == 0 && part.Width == 176 && part.Height == 176, "UVs changed");
            Require(part.UldAsset != fixture.OriginalParts->Parts[1].UldAsset, "Shared asset mutated");
            Require(fixture.OriginalParts->Parts[1].UldAsset->AtkTexture.KernelTexture == fixture.OriginalTexture, "Original texture overwritten");
            Require((fixture.Collision->DrawFlags & Elliptical) == 0, "Click area remains circular");
            Require((fixture.Border->DrawFlags & Hidden) != 0, "Round border remains visible");
        });
        check("Marker hiding includes edge arrows and preserves the player", () =>
        {
            using var fixture = new Fixture();
            fixture.Appearance.Apply(fixture.Addon, false, true);
            Require((fixture.Marker->DrawFlags & Hidden) != 0 && (fixture.Edge->DrawFlags & Hidden) != 0, "Marker or edge arrow remains visible");
            Require((fixture.Player->DrawFlags & Hidden) == 0, "Player pin hidden");
            Require(fixture.Mask->PartsList == fixture.OriginalParts, "Hiding markers changed the mask");
            Require(fixture.Created == 0, "Hiding markers allocated a GPU texture");
        });
        check("Square shape and marker visibility can be toggled independently", () =>
        {
            using var fixture = new Fixture();
            fixture.Appearance.Apply(fixture.Addon, true, true);
            fixture.Appearance.Apply(fixture.Addon, false, true);
            Require(fixture.Mask->PartsList == fixture.OriginalParts && (fixture.Marker->DrawFlags & Hidden) != 0, "Disabling square also restored markers");
            fixture.Appearance.Apply(fixture.Addon, true, false);
            Require(fixture.Mask->PartsList != fixture.OriginalParts && (fixture.Marker->DrawFlags & Hidden) == 0, "Disabling markers also restored the square");
        });
        check("Sun and moon decoration hides independently and restores around native updates", () =>
        {
            using var fixture = new Fixture();
            var settings = AppearanceSettings.Default with { HideSunMoon = true };
            fixture.Appearance.Apply(fixture.Addon, settings);
            Require((fixture.Sun->NodeFlags & NodeFlags.Visible) == 0 && (fixture.Sun->DrawFlags & Hidden) != 0,
                "Day/night decoration remains visible");
            Require((fixture.Marker->NodeFlags & NodeFlags.Visible) != 0 && fixture.Created == 0 &&
                (fixture.Frame->DrawFlags & Hidden) == 0, "Decoration toggle changed markers or frame");
            fixture.Appearance.RestoreMarkerOverrides(fixture.Addon);
            fixture.Sun->Rotation = 2f; // Native clock animation continues.
            fixture.Appearance.Apply(fixture.Addon, settings);
            fixture.Appearance.Apply(fixture.Addon, AppearanceSettings.Default);
            Require((fixture.Sun->NodeFlags & NodeFlags.Visible) != 0 && (fixture.Sun->DrawFlags & Hidden) == 0 &&
                fixture.Sun->Rotation == 2f, "Decoration restoration lost visibility or native animation");
        });
        check("Hiding reaches every flattened icon, label, area and edge child and restores visibility", () =>
        {
            using var fixture = new Fixture();
            fixture.MarkerChildren[4].NodeFlags &= ~NodeFlags.Visible;
            fixture.EdgeChildren[4].DrawFlags |= Hidden;
            fixture.Appearance.Apply(fixture.Addon, false, true);
            for (var i = 0; i < 5; i++)
            {
                Require((fixture.MarkerChildren[i].NodeFlags & NodeFlags.Visible) == 0 &&
                    (fixture.MarkerChildren[i].DrawFlags & Hidden) != 0, "Marker child remains drawable");
                Require((fixture.EdgeChildren[i].NodeFlags & NodeFlags.Visible) == 0 &&
                    (fixture.EdgeChildren[i].DrawFlags & Hidden) != 0, "Edge child remains drawable");
            }
            Require((fixture.Player->NodeFlags & NodeFlags.Visible) != 0, "Player visibility changed");
            fixture.Appearance.RestoreMarkerOverrides(fixture.Addon);
            Require((fixture.MarkerChildren[0].NodeFlags & NodeFlags.Visible) != 0 &&
                (fixture.MarkerChildren[0].DrawFlags & Hidden) == 0, "Visible icon was not restored");
            Require((fixture.MarkerChildren[4].NodeFlags & NodeFlags.Visible) == 0 &&
                (fixture.EdgeChildren[4].DrawFlags & Hidden) != 0, "Originally invisible child was revealed");
        });
        check("Late requested updates and slot reuse cannot undo masking or retain an old filter", () =>
        {
            using var fixture = new Fixture();
            var settings = AppearanceSettings.Default with { HiddenCategories = MarkerCategory.Shops };
            fixture.Addon->NaviMap.NaviMapMarkers[0].IconId = 60412;
            fixture.Appearance.Apply(fixture.Addon, settings); // PostUpdate.
            fixture.Appearance.RestoreMarkerOverrides(fixture.Addon); // PreRequestedUpdate.
            fixture.MarkerChildren[0].DrawFlags &= ~Hidden;
            fixture.MarkerChildren[0].NodeFlags |= NodeFlags.Visible; // Native requested update.
            fixture.Appearance.Apply(fixture.Addon, settings); // PostRequestedUpdate / PreDraw.
            Require((fixture.MarkerChildren[0].DrawFlags & Hidden) != 0 &&
                (fixture.MarkerChildren[0].NodeFlags & NodeFlags.Visible) == 0, "Late native update escaped masking");
            fixture.Appearance.RestoreMarkerOverrides(fixture.Addon);
            fixture.Addon->NaviMap.NaviMapMarkers[0].IconId = 60453;
            fixture.MarkerChildren[1].NodeFlags &= ~NodeFlags.Visible; // Unused secondary icon.
            fixture.Appearance.Apply(fixture.Addon, settings);
            Require((fixture.MarkerChildren[0].NodeFlags & NodeFlags.Visible) != 0 &&
                (fixture.MarkerChildren[1].NodeFlags & NodeFlags.Visible) == 0, "Slot reuse lost native visibility");
        });
        check("Repeated frames reuse the square mask without overwriting original state", () =>
        {
            using var fixture = new Fixture();
            for (var frame = 0; frame < 30; frame++) fixture.Appearance.Apply(fixture.Addon, true, true);
            Require(fixture.Created == 1, "Texture recreated every frame");
            fixture.Appearance.Restore(fixture.Addon);
            Require(fixture.Mask->PartsList == fixture.OriginalParts && fixture.Mask->PartId == 1, "Original mask state lost");
            Require((fixture.Collision->DrawFlags & Elliptical) != 0, "Original collision not restored");
            Require(fixture.Released == 1, "Texture not released exactly once");
        });
        check("Restoration preserves unrelated draw flags and already hidden nodes", () =>
        {
            using var fixture = new Fixture();
            fixture.Edge->DrawFlags |= Hidden;
            fixture.Appearance.Apply(fixture.Addon, false, true);
            fixture.Marker->DrawFlags |= 1u << 21;
            fixture.Appearance.Restore(fixture.Addon);
            Require((fixture.Edge->DrawFlags & Hidden) != 0, "An originally hidden edge was revealed");
            Require((fixture.Marker->DrawFlags & (1u << 21)) != 0 && (fixture.Marker->DrawFlags & Hidden) == 0, "Unrelated flags lost");
        });
        check("Mask is detached before resource release, and repeated teardown is harmless", () =>
        {
            using var fixture = new Fixture();
            fixture.Appearance.Apply(fixture.Addon, true, true);
            fixture.Appearance.Restore(fixture.Addon);
            fixture.Appearance.Restore(fixture.Addon);
            Require(fixture.Released == 1 && fixture.DetachedBeforeRelease, "Resource released before detachment or more than once");
        });
        check("Texture creation failure leaves the native mask and collision intact", () =>
        {
            using var fixture = new Fixture();
            var appearance = new MinimapAppearance((w, h, pixels) => null, texture => throw new Exception("Unexpected release"));
            var rejected = false;
            try { appearance.Apply(fixture.Addon, true, false); }
            catch (InvalidOperationException) { rejected = true; }
            appearance.Restore(fixture.Addon);
            Require(rejected && fixture.Mask->PartsList == fixture.OriginalParts && (fixture.Collision->DrawFlags & Elliptical) != 0, "Failed creation left partial changes");
        });
        check("Missing source texture is rejected without dereferencing a null pointer", () =>
        {
            using var fixture = new Fixture();
            fixture.OriginalParts->Parts[1].UldAsset->AtkTexture.KernelTexture = null;
            var rejected = false;
            try { fixture.Appearance.Apply(fixture.Addon, true, false); }
            catch (NotSupportedException) { rejected = true; }
            Require(rejected && fixture.Created == 0 && fixture.Mask->PartsList == fixture.OriginalParts, "Missing texture was not rejected");
        });
        check("Available quest filters preserve ongoing quests, MSQ, unlocks and unknown icons", () =>
        {
            foreach (var icon in new uint[] { 71021, 71022, 71031, 70965, 70987, 71111 })
                Require(MarkerPolicy.ShouldHide(icon, 0, MarkerCategory.AvailableSideQuests, false), $"Available quest {icon} not filtered");
            foreach (var icon in new uint[] { 71023, 71024, 71025, 70969, 70989, 71112, 71001, 71141, 999999 })
                Require(!MarkerPolicy.ShouldHide(icon, 0, MarkerCategory.All, false), $"Protected quest or unknown icon {icon} hidden");
            Require(!MarkerPolicy.ShouldHide(60412, 71001, MarkerCategory.Shops, false), "Merchant quest overlay hidden");
            Require(!MarkerPolicy.ShouldHide(999999, 60412, MarkerCategory.Shops, false), "Unknown primary icon hidden");
            Require(MarkerPolicy.ShouldHide(0, 60412, MarkerCategory.Shops, false), "Secondary-only shop was not filtered");
        });
        check("Selective hiding follows reused marker slots and their edge arrows", () =>
        {
            using var fixture = new Fixture();
            var settings = AppearanceSettings.Default with { HiddenCategories = MarkerCategory.Shops };
            fixture.Addon->NaviMap.NaviMapMarkers[0].IconId = 60412;
            fixture.Appearance.Apply(fixture.Addon, settings);
            Require((fixture.Marker->DrawFlags & Hidden) != 0 && (fixture.Edge->DrawFlags & Hidden) != 0, "Shop or edge remained visible");
            fixture.Addon->NaviMap.NaviMapMarkers[0].IconId = 60453;
            fixture.Appearance.Apply(fixture.Addon, settings);
            Require((fixture.Marker->DrawFlags & Hidden) == 0 && (fixture.Edge->DrawFlags & Hidden) == 0, "Reused slot kept stale shop filter");
        });
        check("Marker and player scales are independent of each other and the terrain", () =>
        {
            using var fixture = new Fixture();
            fixture.Marker->ScaleX = 0.8f;
            fixture.Marker->ScaleY = 1.2f;
            fixture.Addon->MarkerPositionScaling = 0.25f;
            fixture.Marker->X = 37f;
            fixture.Appearance.Apply(fixture.Addon, AppearanceSettings.Default with { MarkerScale = 0.5f, PlayerScale = 1.5f });
            Require(fixture.Marker->ScaleX == 0.4f && fixture.Marker->ScaleY == 0.6f, "Native nonuniform scale lost");
            Require(fixture.Edge->ScaleX == 0.5f && fixture.Player->ScaleX == 1.5f, "Edge or player scale incorrect");
            Require(fixture.Marker->X == 37f && fixture.Addon->MarkerPositionScaling == 0.25f, "Marker position or map zoom changed");
            Require((fixture.Marker->DrawFlags & 5) == 5, "Transform and dirty flags missing");
            fixture.Appearance.Restore(fixture.Addon);
            Require(fixture.Marker->ScaleX == 0.8f && fixture.Marker->ScaleY == 1.2f && fixture.Player->ScaleX == 1f, "Original scales not restored");
        });
        check("Repeated application never compounds icon or aliased player scales", () =>
        {
            using var fixture = new Fixture();
            var settings = AppearanceSettings.Default with { MarkerScale = 0.5f, PlayerScale = 2f };
            for (var frame = 0; frame < 200; frame++) fixture.Appearance.Apply(fixture.Addon, settings);
            Require(fixture.Marker->ScaleX == 0.5f && fixture.Player->ScaleX == 2f, "Scale accumulated across frames or player aliases");
            fixture.Appearance.RestoreMarkerOverrides(fixture.Addon);
            fixture.Marker->ScaleX = fixture.Marker->ScaleY = 1.25f; // Next native update.
            fixture.Appearance.Apply(fixture.Addon, settings);
            Require(fixture.Marker->ScaleX == 0.625f, "Fresh native scale ignored");
        });
        check("Newer native scale wins over a stale restoration snapshot", () =>
        {
            using var fixture = new Fixture();
            fixture.Appearance.Apply(fixture.Addon, AppearanceSettings.Default with { MarkerScale = 0.5f });
            fixture.Marker->ScaleX = fixture.Marker->ScaleY = 1.25f;
            fixture.Appearance.Restore(fixture.Addon);
            Require(fixture.Marker->ScaleX == 1.25f, "Restoration overwrote a newer native value");
        });
        check("Area overlays and native world-size markers retain their original extent", () =>
        {
            using var fixture = new Fixture();
            ref var marker = ref fixture.Addon->NaviMap.NaviMapMarkers[0];
            var settings = AppearanceSettings.Default with { MarkerScale = 0.5f };
            marker.IconId = 60495;
            fixture.Appearance.Apply(fixture.Addon, settings);
            Require(fixture.Marker->ScaleX == 1f && fixture.Edge->ScaleX == 1f, "Quest circle resized");
            marker.IconId = 999999;
            marker.SubtextOrientation = 12;
            fixture.Appearance.Apply(fixture.Addon, settings);
            Require(fixture.Marker->ScaleX == 1f, "Native area marker resized");
            marker.SubtextOrientation = 0;
            marker.Unknown1C = 2f;
            fixture.Appearance.Apply(fixture.Addon, settings);
            Require(fixture.Marker->ScaleX == 1f, "World-scaled marker resized");
        });
        check("Scale restoration retains rotation transforms and unrelated flags", () =>
        {
            using var fixture = new Fixture();
            fixture.Marker->Rotation = 0.5f;
            fixture.Marker->DrawFlags = 4 | (1u << 21);
            fixture.Appearance.Apply(fixture.Addon, AppearanceSettings.Default with { MarkerScale = 0.5f });
            fixture.Appearance.Restore(fixture.Addon);
            Require((fixture.Marker->DrawFlags & (4 | (1u << 21))) == (4 | (1u << 21)), "Rotation or unrelated flag lost");
        });
        check("Invalid scale and category settings normalize to bounded values", () =>
        {
            foreach (var value in new[] { float.NaN, float.PositiveInfinity, -1f, 0f, float.MaxValue })
            {
                var settings = (AppearanceSettings.Default with { MarkerScale = value, PlayerScale = value,
                    HiddenCategories = (MarkerCategory)(-1), FrameStyle = (SquareFrameStyle)99 }).Normalize();
                Require(float.IsFinite(settings.MarkerScale) && settings.MarkerScale is >= 0.5f and <= 2f, "Unsafe scale accepted");
                Require(settings.HiddenCategories == MarkerCategory.All && settings.FrameStyle == SquareFrameStyle.Thin, "Invalid enum preserved");
            }
        });
        check("Frame styles are distinct and leave the map center transparent", () =>
        {
            var signatures = new HashSet<string>();
            foreach (var style in Enum.GetValues<SquareFrameStyle>())
            {
                var pixels = FramePixels.Create(176, 176, 11, 7, 167, 163, style, 0x80E4D5B7);
                Require(pixels[(88 * 176 + 88) * 4 + 3] == 0 && pixels[3] == 0, "Frame covers center or outside margin");
                if (style != SquareFrameStyle.None)
                {
                    var offset = (7 * 176 + 11) * 4;
                    Require(pixels[offset] == 0xB7 && pixels[offset + 1] == 0xD5 && pixels[offset + 2] == 0xE4 && pixels[offset + 3] == 0x80, "Frame color/alpha swapped");
                }
                Require(signatures.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels))), "Two frame styles are identical");
            }
        });
        check("Round frame can be hidden independently and restored", () =>
        {
            using var fixture = new Fixture();
            fixture.Appearance.Apply(fixture.Addon, AppearanceSettings.Default with { HideFrame = true });
            Require((fixture.Border->DrawFlags & Hidden) != 0 && (fixture.Frame->DrawFlags & Hidden) != 0, "A round border remained visible");
            Require(fixture.Mask->PartsList == fixture.OriginalParts && fixture.Created == 0, "Hiding the frame allocated or changed shape");
            fixture.Appearance.Restore(fixture.Addon);
            Require((fixture.Border->DrawFlags & Hidden) == 0 && (fixture.Frame->DrawFlags & Hidden) == 0, "Round frame not restored");
        });
        check("Square frame uses private assets and reuses its texture until style changes", () =>
        {
            using var fixture = new Fixture();
            var settings = AppearanceSettings.Default with { Square = true, FrameStyle = SquareFrameStyle.Double };
            fixture.Appearance.Apply(fixture.Addon, settings);
            Require(fixture.Created == 2 && fixture.Frame->PartsList != fixture.OriginalParts, "Frame texture not attached");
            Require(fixture.Frame->PartsList->Parts[0].UldAsset != fixture.OriginalParts->Parts[1].UldAsset, "Shared border asset mutated");
            Require(fixture.Frame->PartsList->Parts[0].U == 0 && fixture.Frame->PartsList->Parts[0].Width == 176, "Frame UVs invalid");
            for (var frame = 0; frame < 30; frame++) fixture.Appearance.Apply(fixture.Addon, settings);
            Require(fixture.Created == 2, "Unchanged frame allocated every update");
            fixture.Appearance.Apply(fixture.Addon, settings with { FrameStyle = SquareFrameStyle.Corners });
            Require(fixture.Created == 3 && fixture.Released == 1 && fixture.FrameDetachedBeforeRelease, "Style replacement leaked or freed before detachment");
            fixture.Appearance.Restore(fixture.Addon);
            Require(fixture.Released == 3 && fixture.Frame->PartsList == fixture.OriginalParts && fixture.Frame->PartId == 1, "Frame not fully restored");
        });
        check("No-frame choice releases the square frame and preserves the square map", () =>
        {
            using var fixture = new Fixture();
            var settings = AppearanceSettings.Default with { Square = true };
            fixture.Appearance.Apply(fixture.Addon, settings);
            fixture.Appearance.Apply(fixture.Addon, settings with { HideFrame = true });
            Require(fixture.Mask->PartsList != fixture.OriginalParts && fixture.Frame->PartsList == fixture.OriginalParts && fixture.Released == 1, "Frame toggle removed square or leaked its texture");
            Require((fixture.Frame->DrawFlags & Hidden) != 0, "No-frame option still draws a border");
        });
        check("Frame creation failure can restore an already attached square mask", () =>
        {
            using var fixture = new Fixture();
            var count = 0;
            var appearance = new MinimapAppearance((w, h, p) => ++count == 1 ? fixture.OriginalTexture : null, texture => { });
            var rejected = false;
            try { appearance.Apply(fixture.Addon, AppearanceSettings.Default with { Square = true }); }
            catch (InvalidOperationException) { rejected = true; }
            appearance.Restore(fixture.Addon);
            Require(rejected && fixture.Mask->PartsList == fixture.OriginalParts && fixture.Frame->PartsList == fixture.OriginalParts, "Failed frame left private pointers installed");
        });
        check("Older configuration keeps zoom and appearance with quiet, opt-in startup defaults", () =>
        {
            var old = JsonSerializer.Deserialize<Configuration>("{\"Version\":2,\"LastZoom\":0.25,\"SquareMinimap\":true,\"HideMarkers\":true}")!;
            Require(old.LastZoom == 0.25f && old.Appearance.Square && old.Appearance.HideMarkers, "Old preferences lost");
            Require(old.Appearance.MarkerScale == 1f && old.Appearance.PlayerScale == 1f && !old.EnableZoomOnStartup && !old.OpenWindowOnStartup, "Migration changed scales or silently enabled zoom");
        });
        check("New preferences survive JSON save and reload", () =>
        {
            var config = new Configuration { HiddenCategories = MarkerCategory.Shops | MarkerCategory.AvailableSideQuests,
                MarkerScale = 0.75f, PlayerScale = 1.5f, HideFrame = true, HideSunMoon = true, FrameStyle = SquareFrameStyle.Corners,
                FrameColor = 0x80ABCDEF, EnableZoomOnStartup = true, OpenWindowOnStartup = false };
            var roundtrip = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;
            Require(roundtrip.Appearance == config.Appearance && roundtrip.EnableZoomOnStartup && !roundtrip.OpenWindowOnStartup, "New settings lost on reload");
        });
        check("Window migration keeps existing appearance and new users receive LMeter", () =>
        {
            Require(WindowPreferences.Resolve(null, false).Skin == WindowSkin.LMeter, "New skin default missing");
            var existing = WindowPreferences.Resolve(null, true, 20);
            Require(existing.Skin == WindowSkin.Dalamud && existing.Typeface == WindowTypeface.Dalamud && existing.FontSize == 20,
                "Existing Dalamud appearance changed");
            var saved = WindowPreferences.Preset(WindowSkin.Obsidienne) with { LabelOffsetX = 3, BackgroundOpacity = 0.6f };
            var config = new Configuration { LastZoom = 0.25f, HideSunMoon = true, WindowAppearance = saved };
            var reloaded = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;
            Require(WindowPreferences.Resolve(reloaded.WindowAppearance, true) == saved && reloaded.LastZoom == 0.25f &&
                reloaded.HideSunMoon, "Window preferences or independent native settings lost");
        });
        check("Malformed window preferences stay bounded and background opacity does not fade text", () =>
        {
            var value = new WindowPreferences { FontSize = float.NaN, Padding = 999, RowSpacing = -4, LabelOffsetX = 999,
                LabelOffsetY = float.NegativeInfinity, BackgroundOpacity = 0.4f, Text = 0x00123456 }.Normalize();
            Require(value.FontSize == 17 && value.Padding == 20 && value.RowSpacing == 4 && value.LabelOffsetX == 4 &&
                value.LabelOffsetY == 0 && value.BackgroundOpacity == 0.4f && value.Text == 0xFF123456,
                "Invalid preferences escaped bounds or text followed background alpha");
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<nint> allocations = [];
        public AddonNaviMap* Addon;
        public AtkImageNode* Mask;
        public AtkCollisionNode* Collision;
        public AtkResNode* Border;
        public AtkImageNode* Frame;
        public AtkImageNode* Sun;
        public AtkComponentNode* Marker;
        public AtkComponentNode* Edge;
        public AtkComponentNode* Player;
        public AtkResNode* MarkerChildren;
        public AtkResNode* EdgeChildren;
        public AtkUldPartsList* OriginalParts;
        public Texture* OriginalTexture;
        public MinimapAppearance Appearance;
        public int Created;
        public int Released;
        public bool DetachedBeforeRelease;
        public bool FrameDetachedBeforeRelease;

        public Fixture()
        {
            Addon = Allocate<AddonNaviMap>();
            Mask = Allocate<AtkImageNode>();
            Collision = Allocate<AtkCollisionNode>();
            Border = Allocate<AtkResNode>();
            Frame = Allocate<AtkImageNode>();
            Sun = Allocate<AtkImageNode>();
            Sun->NodeFlags = NodeFlags.Visible;
            Addon->Sun = Sun;
            Marker = Allocate<AtkComponentNode>();
            Edge = Allocate<AtkComponentNode>();
            Player = Allocate<AtkComponentNode>();
            MarkerChildren = Allocate<AtkResNode>(5);
            EdgeChildren = Allocate<AtkResNode>(5);
            Marker->NodeFlags = Edge->NodeFlags = Player->NodeFlags = NodeFlags.Visible;
            for (var i = 0; i < 5; i++)
            {
                MarkerChildren[i].NodeFlags = EdgeChildren[i].NodeFlags = NodeFlags.Visible;
                *(AtkResNode**)((byte*)Addon + 0x15F8 + i * 8) = MarkerChildren + i;
                *(AtkResNode**)((byte*)Addon + 0x25C8 + i * 8) = EdgeChildren + i;
            }
            Marker->ScaleX = Marker->ScaleY = Edge->ScaleX = Edge->ScaleY = Player->ScaleX = Player->ScaleY = 1f;
            OriginalParts = Allocate<AtkUldPartsList>();
            OriginalTexture = Allocate<Texture>();
            OriginalTexture->ActualWidth = OriginalTexture->ActualHeight = 176;
            var asset = Allocate<AtkUldAsset>();
            asset->AtkTexture.TextureType = TextureType.KernelTexture;
            asset->AtkTexture.KernelTexture = OriginalTexture;
            OriginalParts->Parts = Allocate<AtkUldPart>(2);
            OriginalParts->PartCount = 2;
            OriginalParts->Parts[1] = new AtkUldPart { UldAsset = asset, Width = 176, Height = 176 };
            Mask->PartsList = OriginalParts;
            Mask->PartId = 1;
            Mask->Width = Mask->Height = 176;
            Mask->X = 21;
            Mask->Y = 18;
            Collision->X = 32;
            Collision->Y = 28;
            Collision->Width = Collision->Height = 156;
            Collision->DrawFlags = Elliptical;
            Addon->Mask = Mask;
            Addon->MainCollision = Collision;
            Border->NodeId = 13;
            Frame->NodeId = 15;
            Frame->Type = NodeType.Image;
            Frame->Width = Frame->Height = 176;
            Frame->X = 21;
            Frame->Y = 21;
            Frame->ScaleX = Frame->ScaleY = 1;
            Frame->PartsList = OriginalParts;
            Frame->PartId = 1;
            var nodes = (AtkResNode**)Allocate<nint>(2);
            nodes[0] = Border;
            nodes[1] = (AtkResNode*)Frame;
            Addon->UldManager.NodeList = nodes;
            Addon->UldManager.NodeListCount = 2;
            Addon->NaviMap.NaviMapMarkers[0].ComponentNode = Marker;
            Addon->NaviMap.NaviMapMarkers[100].ComponentNode = Player;
            Addon->NaviMap.PlayerPin = Player;
            *(AtkComponentNode**)((byte*)Addon + 0x25C0) = Edge;
            Appearance = new MinimapAppearance((width, height, pixels) =>
            {
                Created++;
                Require(width == 176 && height == 176 && pixels.Length == 176 * 176 * 4, "Texture dimensions differ from logical mask size");
                var texture = Allocate<Texture>();
                texture->ActualWidth = (uint)width;
                texture->ActualHeight = (uint)height;
                return texture;
            }, texture =>
            {
                Released++;
                DetachedBeforeRelease = Mask->PartsList == OriginalParts;
                FrameDetachedBeforeRelease = Frame->PartsList == OriginalParts;
            });
        }

        private T* Allocate<T>(int count = 1) where T : unmanaged
        {
            var pointer = NativeMemory.AllocZeroed((nuint)(sizeof(T) * count));
            allocations.Add((nint)pointer);
            return (T*)pointer;
        }

        public void Dispose()
        {
            Appearance.Restore(Addon);
            foreach (var pointer in allocations) NativeMemory.Free((void*)pointer);
        }
    }
}
