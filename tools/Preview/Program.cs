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
        ImGui.InitApi(native);
        var results = new List<object>();
        foreach (var (name, scale, width, tab, skin, ready, hidden) in new[]
        {
            ("map-100", 1f, 550, 0, WindowSkin.LMeter, true, false),
            ("map-150", 1.5f, 550, 0, WindowSkin.LMeter, true, false),
            ("map-200", 2f, 550, 0, WindowSkin.LMeter, true, false),
            ("map-small", 1f, 360, 0, WindowSkin.LMeter, true, false),
            ("map-fixed", 1f, 550, 0, WindowSkin.LMeter, true, false),
            ("map-minimum", 1f, 360, 0, WindowSkin.LMeter, true, false),
            ("markers", 1f, 550, 1, WindowSkin.LMeter, true, false),
            ("markers-hidden", 1f, 550, 1, WindowSkin.LMeter, true, true),
            ("style", 1f, 550, 2, WindowSkin.LMeter, true, false),
            ("startup", 1f, 550, 3, WindowSkin.LMeter, true, false),
            ("obsidienne", 1f, 550, 0, WindowSkin.Obsidienne, true, false),
            ("waiting", 1f, 550, 0, WindowSkin.LMeter, false, false),
            ("incompatible", 1f, 550, 0, WindowSkin.LMeter, false, true),
        })
        {
            var context = ImGui.CreateContext();
            try
            {
                ImGui.StyleColorsDark();
                ImGui.GetStyle().ScaleAllSizes(scale);
                var io = ImGui.GetIO();
                io.IniFilename = null;
                io.DeltaTime = 1f / 60;
                var pixelsWidth = (int)((width + 24) * scale);
                var pixelsHeight = (int)(1050 * scale);
                io.DisplaySize = new Vector2(pixelsWidth, pixelsHeight);
                // Font file is read from the operating system for this offline run only.
                // No font or Dalamud binary is copied into the repository or release.
                var config = ImGui.ImFontConfig();
                config.SizePixels = 17 * scale;
                var ranges = new ushort[] { 0x20, 0xFF, 0x2000, 0x206F, 0x2190, 0x21FF, 0 };
                fixed (ushort* glyphs = ranges)
                {
                    io.Fonts.AddFontFromFileTTF(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"), 17 * scale, config, glyphs);
                    io.Fonts.Build();
                }
                config.Destroy();
                byte* texture; int textureWidth, textureHeight;
                io.Fonts.GetTexDataAsRGBA32(0, &texture, &textureWidth, &textureHeight);
                io.Fonts.SetTexID(0, new ImTextureID(1UL));
                var theme = WindowPreferences.Preset(skin);
                var state = new ViewState(true, ready, 0.25f, ready ? "Zoom personnalisé actif" : hidden ?
                    "Client incompatible : effets désactivés" : "En attente de la mini-carte…",
                    AppearanceSettings.Default with { Square = true, FrameStyle = SquareFrameStyle.Corners, HideMarkers = hidden,
                        HiddenCategories = MarkerCategory.Shops, HideSunMoon = true });
                var panel = new SettingsPanel { SelectTab = tab };
                var actions = new SettingsActions(v => state = state with { Zoom = v }, () => state = state with { Enabled = false },
                    () => state = state with { Appearance = AppearanceSettings.Default },
                    change => state = state with { Appearance = change(state.Appearance).Normalize() },
                    v => state = state with { AutoZoom = v }, v => state = state with { OpenOnLoad = v }, v => theme = v.Normalize());
                float endY = 0, scrollMax = 0, scrollY = 0;
                // Warm up layout, tabs and auto-fitting before rendering the same production panel.
                void RenderFrame()
                {
                    ImGui.NewFrame();
                    using (new SettingsTheme(theme))
                    {
                        ImGui.SetNextWindowPos(new Vector2(12 * scale), ImGuiCond.Always);
                        var fixedHeight = name == "map-fixed" ? 620 : name == "map-minimum" ? 320 : 0;
                        ImGui.SetNextWindowSize(new Vector2(width * scale, fixedHeight * scale), ImGuiCond.Always);
                        ImGui.Begin("Minimap Zoom v0.4.0", (fixedHeight == 0 ? ImGuiWindowFlags.AlwaysAutoResize : ImGuiWindowFlags.None) | ImGuiWindowFlags.NoSavedSettings);
                        panel.Draw(state, theme, actions, "Aperçu hors jeu : Segoe UI. Expressway non fournie.",
                            "Aperçu ImGui hors jeu. Données fictives. Le rendu natif se valide en jeu.", !(hidden && !ready));
                        endY = ImGui.GetWindowPos().Y + ImGui.GetWindowHeight();
                        scrollMax = ImGui.GetScrollMaxY();
                        scrollY = ImGui.GetScrollY();
                        ImGui.End();
                    }
                    ImGui.Render();
                }
                for (var frame = 0; frame < 4; frame++) RenderFrame();
                if (panel.ActiveTab != tab && ready) throw new InvalidOperationException("Requested preview tab did not open");
                var height = Math.Min(pixelsHeight, (int)MathF.Ceiling(endY + 12 * scale));
                var output = Path.Combine(args[1], name + ".png");
                CpuRenderer.Save(ImGui.GetDrawData(), pixelsWidth, height, texture, textureWidth, textureHeight, output);
                void Click(float x, float y)
                {
                    io.AddMousePosEvent(x, y); RenderFrame();
                    io.AddMouseButtonEvent(0, true); RenderFrame();
                    io.AddMouseButtonEvent(0, false); RenderFrame();
                }
                if (name == "map-100")
                {
                    Click(283, 364);
                    Require(!state.Appearance.HideSunMoon, "Sun/moon checkbox did not dispatch its change");
                    Click(283, 323);
                    Require(state.Appearance.HideFrame && state.Appearance.FrameStyle == SquareFrameStyle.Corners, "Frame checkbox lost its style");
                    Click(500, 182);
                    Require(state.Zoom > 1.5f, "Zoom slider did not dispatch its selected value");
                    Click(115, 106);
                    Require(panel.ActiveTab == 1, "Marker tab did not respond");
                    Click(283, 236);
                    Require(state.Appearance.HideMarkers, "Global marker toggle did not respond");
                    Click(283, 341);
                    Require(state.Appearance.HiddenCategories == MarkerCategory.Shops, "Disabled category remained interactive");
                    Click(283, 236); Click(283, 341);
                    Require(!state.Appearance.HideMarkers && state.Appearance.HiddenCategories == MarkerCategory.None, "Category did not respond after enabling");
                    Console.WriteLine("PASS actual ImGui mouse input: sun/moon, frame, zoom, tabs, global mask and disabled/enabled category.");
                }
                if (name == "map-minimum")
                {
                    io.AddMousePosEvent(200, 250); RenderFrame();
                    io.AddMouseWheelEvent(0, -30);
                    for (var frame = 0; frame < 4; frame++) RenderFrame();
                    Require(scrollY > 0, "Mouse wheel did not scroll the small window");
                    CpuRenderer.Save(ImGui.GetDrawData(), pixelsWidth, height, texture, textureWidth, textureHeight,
                        Path.Combine(args[1], "map-minimum-bottom.png"));
                    Console.WriteLine("PASS actual ImGui mouse wheel: small window scrolls to the restoration controls.");
                }
                if (name == "map-minimum" && scrollMax <= 0) throw new InvalidOperationException("Small window has no scrolling");
                results.Add(new { name, scale, width = pixelsWidth, height, scrollMax, panel.ActiveTab, vertices = ImGui.GetDrawData().TotalVtxCount });
                Console.WriteLine($"Rendered {name}: {pixelsWidth}x{height}, tab {panel.ActiveTab}");
            }
            finally { ImGui.DestroyContext(context); }
        }
        File.WriteAllText(Path.Combine(args[1], "preview-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class NativeContext(string path) : INativeContext, IDisposable
    {
        private readonly nint module = NativeLibrary.Load(path);
        public nint GetProcAddress(string name) => NativeLibrary.GetExport(module, name);
        public bool TryGetProcAddress(string name, out nint address) => NativeLibrary.TryGetExport(module, name, out address);
        public bool IsExtensionSupported(string name) => false;
        public void Dispose() => NativeLibrary.Free(module);
    }
}
