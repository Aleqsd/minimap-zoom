using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MinimapZoom;

internal sealed class SettingsWindow : Window, IDisposable
{
    private readonly SettingsPanel panel = new();
    private readonly Func<ViewState> state;
    private readonly Func<string> diagnostic;
    private readonly Func<bool> compatible;
    private readonly SettingsActions actions;
    private SettingsTheme? theme;

    public SettingsWindow(Func<ViewState> state, SettingsActions actions,
        Func<string> diagnostic, Func<bool> compatible) : base(BuildIdentity.WindowTitle)
    {
        this.state = state;
        this.actions = actions;
        this.diagnostic = diagnostic;
        this.compatible = compatible;
        Size = new Vector2(620, 820);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(380, 360), MaximumSize = new Vector2(1200, 1600) };
    }

    // Use Dalamud's current font and global scale; no plugin-specific font or style preferences.
    public override void PreDraw() => theme = new SettingsTheme();
    public override void Draw() => panel.Draw(state(), actions, diagnostic(), compatible());
    public override void PostDraw() { theme?.Dispose(); theme = null; }
    public void Dispose() => PostDraw();
}
