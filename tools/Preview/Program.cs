using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using HexaGen.Runtime;
using MinimapZoom;

internal static unsafe class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: Preview <Dalamud directory> <output directory>");
        Directory.CreateDirectory(args[1]);
        using var native = new NativeContext(Path.Combine(args[0], "cimgui.dll"));
        // Manual bindings use LibraryImport while generated ones use InitApi. Both must
        // resolve to the same module/context, not to a second output-directory DLL.
        NativeLibrary.SetDllImportResolver(typeof(ImGui).Assembly, (name, _, _) => name == "cimgui" ? native.Module : nint.Zero);
        ImGui.InitApi(native);
        var results = new List<object>();
        var cases = new List<(string Name, float Scale, int Width, int Height, int Tab)>();
        string[] names = ["map", "markers", "profiles", "usage"];
        for (var tab = 0; tab < 4; tab++)
            foreach (var scale in new[] { 1f, 1.5f, 2f })
            {
                cases.Add(($"{names[tab]}-{scale * 100:0}", scale, 620, 820, tab));
                cases.Add(($"{names[tab]}-minimum-{scale * 100:0}", scale, 380, 360, tab));
            }
        foreach (var name in new[] { "legacy-settings", "native-frame-color", "waiting", "markers-hidden", "profiles-long", "suspended" })
            cases.Add((name, 1, 620, 820, name == "markers-hidden" ? 1 : name == "profiles-long" ? 2 : 0));
        foreach (var scale in new[] { 1f, 1.5f, 2f })
        {
            cases.Add(($"incompatible-{scale * 100:0}", scale, 620, 820, 0));
            cases.Add(($"incompatible-minimum-{scale * 100:0}", scale, 380, 360, 0));
        }
        string incompatibleMessage;
        try { ClientCompatibility.ValidateVersion("2026.09.02.0000.0000"); throw new InvalidOperationException("Unverified preview client accepted"); }
        catch (NotSupportedException error) { incompatibleMessage = error.Message; }
        string? baselineTheme = null;
        foreach (var (name, scale, width, windowHeight, tab) in cases)
        {
            var incompatible = name.StartsWith("incompatible", StringComparison.Ordinal);
            var context = ImGui.CreateContext();
            try
            {
                ImGui.StyleColorsDark(); ImGui.GetStyle().ScaleAllSizes(scale);
                var io = ImGui.GetIO(); io.IniFilename = null; io.DeltaTime = 1f / 60;
                var pixelsWidth = (int)((width + 24) * scale);
                var pixelsHeight = (int)((windowHeight + 24) * scale);
                io.DisplaySize = new Vector2(pixelsWidth, pixelsHeight);
                var fontConfig = ImGui.ImFontConfig(); fontConfig.SizePixels = 17 * scale;
                var ranges = new ushort[] { 0x20, 0xFF, 0x2000, 0x206F, 0x2190, 0x21FF, 0 };
                fixed (ushort* glyphs = ranges)
                {
                    io.Fonts.AddFontFromFileTTF(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"), 17 * scale, fontConfig, glyphs);
                    io.Fonts.Build();
                }
                fontConfig.Destroy();
                byte* texture; int textureWidth, textureHeight;
                io.Fonts.GetTexDataAsRGBA32(0, &texture, &textureWidth, &textureHeight);
                var atlas = (nint)texture; var atlasWidth = textureWidth; var atlasHeight = textureHeight;
                io.Fonts.SetTexID(0, new ImTextureID(1UL));
                var saved = new Configuration { LastZoom = 0.25f, SquareMinimap = true, FrameStyle = SquareFrameStyle.Corners,
                    HideMarkers = name == "markers-hidden", HiddenCategories = MarkerCategory.Shops, HideSunMoon = true };
                if (name == "legacy-settings")
                {
                    var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(saved))!;
                    json["WindowAppearance"] = System.Text.Json.Nodes.JsonNode.Parse("""
                        {"Skin":1,"Typeface":2,"FontSize":26,"Background":4294901760,"BackgroundOpacity":0.15,
                         "Text":4278255360,"Accent":4294902015,"Relief":2,"Padding":20,"RowSpacing":16,
                         "LabelOffsetX":4,"LabelOffsetY":-4,"AlignLabelsRight":true}
                        """);
                    saved = JsonSerializer.Deserialize<Configuration>(json.ToJsonString())!;
                }
                if (name == "native-frame-color") { saved.FrameColor = 0xFFEB5577; saved.FrameStyle = SquareFrameStyle.Double; }
                var store = new ProfileStore(saved);
                var territory = name == "waiting" ? 0u : 100u;
                var zoneName = territory == 0 ? "Hors jeu" : "Zone de démonstration";
                if (name == "profiles-long")
                {
                    store.Active.Name = "Exploration des contrées lointaines";
                    store.Bind(100, "Une zone au nom particulièrement long pour vérifier le retour à la ligne", store.ActiveId);
                }
                ProfileView ProfileView() => new(store.ActiveId, saved.ManualProfileId, saved.Profiles.Select(p => p with { }).ToArray(),
                    saved.AutomaticProfiles, territory, zoneName, saved.ZoneRules.ToArray());
                var ready = name != "waiting" && !incompatible;
                var state = new ViewState(!incompatible, ready, saved.LastZoom, ready ? "Zoom personnalisé actif" : incompatible ?
                    incompatibleMessage : "En attente de la mini-carte…", saved.Appearance,
                    Profiles: ProfileView(), Shortcut: saved.Shortcut, SavedZoom: saved.LastZoom);
                if (name == "suspended") state = state with { Suspended = true, Enabled = false, Message = "Affichage du jeu restauré" };
                var panel = new SettingsPanel { SelectTab = tab };
                var rects = new Dictionary<string, (Vector2 Min, Vector2 Max)>();
                panel.RecordItem = (id, min, max) => rects[id] = (min, max);
                void CommitAppearance(AppearanceSettings p)
                {
                    state = state with { Appearance = p.Normalize() };
                    store.Remember(state.SavedZoom ?? state.Zoom, state.Appearance);
                }
                var actions = new SettingsActions(v => { state = state with { Zoom = v, SavedZoom = v, Enabled = true }; store.Remember(v, state.Appearance); },
                    () => state = state with { Enabled = false }, () => state = state with { Suspended = true },
                    change => CommitAppearance(change(state.Appearance)), v => state = state with { AutoZoom = v },
                    v => state = state with { OpenOnLoad = v }, request =>
                    {
                        switch (request.Operation)
                        {
                            case ProfileOperation.Select: store.Select(request.Value, true); break;
                            case ProfileOperation.Duplicate: store.Duplicate(request.Value); break;
                            case ProfileOperation.Rename: store.Active.Name = ProfileStore.Name(request.Value); break;
                            case ProfileOperation.Delete: store.DeleteActive(); break;
                            case ProfileOperation.Automatic: saved.AutomaticProfiles = request.Enabled; store.ForZone(territory); break;
                            case ProfileOperation.Bind: store.Bind(territory, zoneName, request.Value); break;
                            case ProfileOperation.Unbind: store.Bind(request.TerritoryId, "", null); break;
                        }
                        state = state with { Appearance = store.Active.Appearance, Zoom = store.Active.Zoom,
                            SavedZoom = store.Active.Zoom, Profiles = ProfileView() };
                    }, key => state = state with { Shortcut = key });
                float scrollMax = 0, scrollY = 0; float? requestedScroll = null;
                float horizontalScroll = 0;
                panel.ContentFrame = () =>
                {
                    if (requestedScroll is { } requested) { ImGui.SetScrollY(requested); requestedScroll = null; }
                    scrollMax = ImGui.GetScrollMaxY(); scrollY = ImGui.GetScrollY(); horizontalScroll = ImGui.GetScrollMaxX();
                };
                void RenderFrame()
                {
                    ImGui.NewFrame();
                    using (new SettingsTheme())
                    {
                        var signature = $"{ImGui.GetStyle().WindowPadding}/{ImGui.GetStyle().ItemSpacing}/{ImGui.GetFontSize()}";
                        for (var color = 0; color < (int)ImGuiCol.Count; color++) signature += $"/{ImGui.GetStyle().Colors[color]}";
                        if (name == "map-100") baselineTheme = signature;
                        if (name is "legacy-settings" or "native-frame-color") Require(signature == baselineTheme, "Settings style changed with map preferences");
                        ImGui.SetNextWindowPos(new Vector2(12 * scale), ImGuiCond.Always);
                        ImGui.SetNextWindowSize(new Vector2(width, windowHeight) * scale, ImGuiCond.Always);
                        ImGui.Begin($"Minimap Zoom v{typeof(Program).Assembly.GetName().Version!.ToString(3)}", ImGuiWindowFlags.NoSavedSettings);
                        rects.Clear();
                        panel.Draw(state, actions, incompatible ?
                            $"Client détecté : 2026.09.02.0000.0000\nClient pris en charge : {NativeContracts.GameVersion}\n" +
                            "Mini-carte disponible : non\nCoefficient : 0,25\nVersion chargée : 0.5.1 · build exemple\n" +
                            "DLL : D:\\Plugins\\MinimapZoom\\MinimapZoom.dll\nPortée étendue : indisponible (client non validé)" :
                            "Aperçu ImGui hors jeu. Données fictives. Le rendu natif se valide en jeu.", !incompatible);
                        if (incompatible)
                        {
                            if (requestedScroll is { } requested) { ImGui.SetScrollY(requested); requestedScroll = null; }
                            scrollMax = ImGui.GetScrollMaxY(); scrollY = ImGui.GetScrollY(); horizontalScroll = ImGui.GetScrollMaxX();
                        }
                        ImGui.End();
                    }
                    ImGui.Render();
                }
                void Render() { for (var i = 0; i < 4; i++) RenderFrame(); }
                void Save(string suffix = "") => CpuRenderer.Save(ImGui.GetDrawData(), pixelsWidth, pixelsHeight,
                    (byte*)atlas, atlasWidth, atlasHeight, Path.Combine(args[1], name + suffix + ".png"));
                void Click(string id, float fraction = 0.5f)
                {
                    var r = rects[id]; var center = (r.Min + r.Max) / 2;
                    if (!id.StartsWith("tab-") && (center.Y < 165 * scale || center.Y > (windowHeight - 90) * scale))
                    {
                        requestedScroll = Math.Clamp(scrollY + center.Y - (windowHeight / 2) * scale, 0, scrollMax);
                        Render(); r = rects[id];
                    }
                    io.AddMousePosEvent(r.Min.X + (r.Max.X - r.Min.X) * fraction, (r.Min.Y + r.Max.Y) / 2); RenderFrame();
                    io.AddMouseButtonEvent(0, true); RenderFrame();
                    io.AddMouseButtonEvent(0, false); RenderFrame(); Render();
                }
                Render();
                Require(panel.ActiveTab == tab || incompatible, "Wrong preview tab");
                Require(horizontalScroll <= 1, "Unwanted horizontal scrolling");
                Save();
                if (name == "legacy-settings") Require(File.ReadAllBytes(Path.Combine(args[1], name + ".png"))
                    .SequenceEqual(File.ReadAllBytes(Path.Combine(args[1], "map-100.png"))), "Legacy settings changed pixels");
                if (scrollMax > 0)
                {
                    io.AddMousePosEvent(180 * scale, (windowHeight - 110) * scale); RenderFrame();
                    io.AddMouseWheelEvent(0, -50); Render();
                    Require(scrollY > 0, "Wheel did not scroll"); Save("-bottom");
                    requestedScroll = 0; Render();
                }
                if (name == "map-100")
                {
                    Click("frame-options"); Click("thickness", 0.85f); Click("corners", 0.8f);
                    Require(state.Appearance.FrameThickness >= 6 && state.Appearance.CornerLength > 40, "Frame dimensions input failed");
                    Save("-frame-options");
                    Click("frame-options");
                    Click("cardinals"); Click("coordinates"); Click("weather"); Click("buttons");
                    Require(state.Appearance.HideCardinals && state.Appearance.HideCoordinates && state.Appearance.HideWeather && state.Appearance.HideButtons,
                        "Decoration input did not dispatch independently");
                    Click("sun"); Require(!state.Appearance.HideSunMoon, "Sun checkbox did not respond");
                    Click("opacity", 0.4f); Require(state.Appearance.MapOpacity < 0.6f && state.Appearance.MarkerScale == 1, "Opacity control changed icons");
                    Click("zoom", 0.9f); Require(state.Zoom > 1.5f, "Zoom slider did not respond");
                    requestedScroll = 0; Render(); Click("tab-1"); Require(panel.ActiveTab == 1, "Tab input did not respond");
                    Click("hide-all"); Require(state.Appearance.HideMarkers, "Global filter did not respond");
                    Click("category-Shops"); Require(state.Appearance.HiddenCategories == MarkerCategory.Shops, "Disabled filter accepted clicks");
                    Click("hide-all"); Click("category-Shops"); Require(state.Appearance.HiddenCategories == MarkerCategory.None, "Re-enabled filter did not respond");
                    Console.WriteLine("PASS ImGui input: tabs, decorations, opacity, zoom and enabled/disabled marker filters.");
                }
                if (name == "profiles-100")
                {
                    Click("profile-name");
                    io.AddKeyEvent(ImGuiKey.ModCtrl, true); io.AddKeyEvent(ImGuiKey.A, true); RenderFrame();
                    io.AddKeyEvent(ImGuiKey.A, false); io.AddKeyEvent(ImGuiKey.ModCtrl, false); RenderFrame();
                    foreach (var ch in "Profil été") io.AddInputCharacter(ch);
                    Render(); Click("rename");
                    Require(store.Active.Name == "Profil été", "Profile rename or accented text input failed");
                    Click("duplicate"); Click("create-copy"); Require(saved.Profiles.Count == 5 && store.Active.Name.Contains("copie"), "Profile copy input failed");
                    Click("bind-zone"); Require(saved.ZoneRules.Count == 1, "Zone association input failed");
                    Click("automatic"); Require(saved.AutomaticProfiles, "Automatic profile input failed");
                    Save("-associated");
                    Console.WriteLine("PASS ImGui input: duplicate profile, bind current zone, enable automatic profiles.");
                }
                if (name == "usage-100")
                {
                    Click("shortcut-enabled"); Require(state.Shortcut!.Enabled, "Shortcut enable input failed");
                    Click("startup-zoom"); Require(state.AutoZoom, "Startup input failed");
                    Click("restore-all"); Require(state.Suspended, "Restoration input failed");
                    Console.WriteLine("PASS ImGui input: shortcut, startup and restoration.");
                }
                results.Add(new { name, scale, width = pixelsWidth, height = pixelsHeight, scrollMax, horizontalScroll });
                Console.WriteLine($"Rendered {name}: {pixelsWidth}x{pixelsHeight}");
            }
            finally { ImGui.DestroyContext(context); }
        }
        File.WriteAllText(Path.Combine(args[1], "preview-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class NativeContext(string path) : INativeContext, IDisposable
    {
        private readonly nint module = NativeLibrary.Load(path);
        public nint Module => module;
        public nint GetProcAddress(string name) => NativeLibrary.GetExport(module, name);
        public bool TryGetProcAddress(string name, out nint address) => NativeLibrary.TryGetExport(module, name, out address);
        public bool IsExtensionSupported(string name) => false;
        public void Dispose() => NativeLibrary.Free(module);
    }
}
