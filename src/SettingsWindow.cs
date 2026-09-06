using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;

namespace MinimapZoom;

internal sealed class SettingsWindow : Window, IDisposable
{
    private readonly SettingsPanel panel = new();
    private readonly Func<ViewState> state;
    private readonly Func<WindowPreferences> preferences;
    private readonly Func<string> diagnostic;
    private readonly Func<bool> compatible;
    private readonly SettingsActions actions;
    private readonly IFontAtlas atlas;
    private IFontHandle? font;
    private IDisposable? pushedFont;
    private SettingsTheme? theme;
    private string fontStatus = "Police active : Dalamud";
    private WindowTypeface? requestedTypeface;
    private float requestedSize;

    public SettingsWindow(IFontAtlas atlas, Func<ViewState> state, Func<WindowPreferences> preferences,
        SettingsActions actions, Func<string> diagnostic, Func<bool> compatible) : base(BuildIdentity.WindowTitle)
    {
        this.atlas = atlas;
        this.state = state;
        this.preferences = preferences;
        this.actions = actions;
        this.diagnostic = diagnostic;
        this.compatible = compatible;
        Size = new Vector2(550, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360, 320), MaximumSize = new Vector2(1200, 1600) };
        SyncFont(preferences());
    }

    // Invoked once at initialization and on preference changes, outside Draw.
    public void SyncFont(WindowPreferences settings)
    {
        if (requestedTypeface == settings.Typeface && requestedSize == settings.FontSize) return;
        requestedTypeface = settings.Typeface;
        requestedSize = settings.FontSize;
        IFontHandle replacement;
        var name = settings.Typeface switch { WindowTypeface.Expressway => "Expressway", WindowTypeface.SegoeUI => "Segoe UI", _ => "Dalamud" };
        try
        {
            var family = settings.Typeface == WindowTypeface.Dalamud ? null : IFontFamilyId.ListSystemFonts(false)
                .OfType<SystemFontFamilyId>().FirstOrDefault(f => f.EnglishName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (family != null)
            {
                var spec = new SingleFontSpec { FontId = family.Fonts[family.FindBestMatch(400, 5, 0)], SizePx = settings.FontSize };
                replacement = spec.CreateFontHandle(atlas);
                fontStatus = $"Police active : {name}";
            }
            else
            {
                replacement = DefaultFont(settings.FontSize);
                fontStatus = settings.Typeface == WindowTypeface.Dalamud ? "Police active : Dalamud" : $"{name} absente : repli sur Dalamud.";
            }
        }
        catch
        {
            replacement = DefaultFont(settings.FontSize);
            fontStatus = "Police indisponible : repli sur Dalamud.";
        }
        var previous = font;
        font = replacement;
        previous?.Dispose();
    }

    private IFontHandle DefaultFont(float size) => atlas.NewDelegateFontHandle(step =>
        step.OnPreBuild(toolkit => toolkit.AddDalamudDefaultFont(size)));

    public override void PreDraw()
    {
        pushedFont = font?.Push();
        try { theme = new SettingsTheme(preferences()); }
        catch { pushedFont?.Dispose(); pushedFont = null; throw; }
    }

    public override void Draw() => panel.Draw(state(), preferences(), actions,
        font?.LoadException != null ? "Échec du chargement de la police : repli Dalamud." : fontStatus,
        diagnostic(), compatible());

    public override void PostDraw()
    {
        theme?.Dispose(); theme = null;
        pushedFont?.Dispose(); pushedFont = null;
    }

    public void Dispose() { PostDraw(); font?.Dispose(); font = null; }
}
