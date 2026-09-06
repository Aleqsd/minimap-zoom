using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace MinimapZoom;

internal sealed record ViewState(bool Enabled, bool Ready, float Zoom, string Message,
    AppearanceSettings Appearance, bool AutoZoom = false, bool OpenOnLoad = false);

internal sealed record SettingsActions(Action<float> Zoom, Action RestoreZoom, Action RestoreAll,
    Action<Func<AppearanceSettings, AppearanceSettings>> Appearance, Action<bool> AutoZoom,
    Action<bool> OpenOnLoad, Action<WindowPreferences> Window);

// Shared by the Dalamud window and the real ImGui offline preview.
internal sealed class SettingsPanel
{
    private static readonly string[] FrameNames = ["Aucun", "Fin", "Épais", "Double", "Angles"];
    private static readonly string[] Tabs = ["Carte", "Marqueurs", "Style", "Démarrage"];
    public int? SelectTab { get; set; }
    public int ActiveTab { get; private set; }

    public void Draw(ViewState state, WindowPreferences theme, SettingsActions actions, string fontStatus,
        string diagnostic, bool compatible = true)
    {
        var scale = SettingsTheme.Scale;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(pos, pos + new Vector2(3 * scale, ImGui.GetFontSize() + 8 * scale),
            ImGui.GetColorU32(state.Ready ? ImGuiCol.CheckMark : ImGuiCol.TextDisabled));
        SettingsTheme.Label(state.Message, theme);
        ImGui.Spacing();
        if (compatible && ImGui.BeginTabBar("SettingsTabs"))
        {
            for (var i = 0; i < Tabs.Length; i++)
            {
                if (!ImGui.BeginTabItem(Tabs[i], SelectTab == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None)) continue;
                ActiveTab = i;
                ImGui.Spacing();
                switch (i)
                {
                    case 0: Map(state, theme, actions); break;
                    case 1: Markers(state, theme, actions); break;
                    case 2: Style(theme, actions, fontStatus); break;
                    case 3: Startup(state, theme, actions); break;
                }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
            SelectTab = null;
        }
        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button("Restaurer la mini-carte")) actions.RestoreAll();
        ImGui.TextDisabled("Rond, cadre et marqueurs du jeu · /minizoom off");
        if (ImGui.CollapsingHeader("Diagnostic")) ImGui.TextWrapped(diagnostic);
    }

    private static void Row(string label, WindowPreferences theme, Action control)
    {
        ImGui.PushID(label);
        if (ImGui.GetContentRegionAvail().X < 430 * SettingsTheme.Scale)
        {
            SettingsTheme.Label(label, theme);
            ImGui.SetNextItemWidth(-1);
            control();
        }
        else if (ImGui.BeginTable("row", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("label", ImGuiTableColumnFlags.WidthStretch, 0.46f);
            ImGui.TableSetupColumn("control", ImGuiTableColumnFlags.WidthStretch, 0.54f);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            SettingsTheme.Label(label, theme, true);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            control();
            ImGui.EndTable();
        }
        ImGui.PopID();
    }

    private static void Toggle(string label, bool value, WindowPreferences theme, Action<bool> change) =>
        Row(label, theme, () => { if (ImGui.Checkbox("##toggle", ref value)) change(value); });

    private static void Map(ViewState s, WindowPreferences t, SettingsActions a)
    {
        var zoom = s.Zoom;
        SettingsTheme.Label("Zoom de la mini-carte", t);
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##zoom", ref zoom, 0.25f, 2, "%.2f")) a.Zoom(zoom);
        ImGui.TextDisabled("Voir plus loin à gauche · agrandir à droite");
        if (s.Enabled && ImGui.SmallButton("Rétablir le zoom du jeu")) a.RestoreZoom();
        ImGui.Spacing(); ImGui.Separator();
        Toggle("Mini-carte carrée", s.Appearance.Square, t, v => a.Appearance(p => p with { Square = v }));
        Toggle("Masquer le cadre", s.Appearance.HideFrame, t, v => a.Appearance(p => p with { HideFrame = v }));
        Toggle("Masquer soleil / lune", s.Appearance.HideSunMoon, t, v => a.Appearance(p => p with { HideSunMoon = v }));
        if (s.Appearance.Square && !s.Appearance.HideFrame)
        {
            Row("Cadre carré", t, () =>
            {
                var style = (int)s.Appearance.FrameStyle;
                if (ImGui.Combo("##frame", ref style, FrameNames, FrameNames.Length))
                    a.Appearance(p => p with { FrameStyle = (SquareFrameStyle)style });
            });
            if (s.Appearance.FrameStyle != SquareFrameStyle.None)
                ColorRow("Couleur du cadre", s.Appearance.FrameColor, t, v => a.Appearance(p => p with { FrameColor = v }), true);
        }
        ImGui.Spacing();
        ImGui.TextWrapped("Application immédiate. Les boutons + / - et la molette restent utilisables.");
        ImGui.TextWrapped("La portée des icônes fixes suit le dézoom. Limite du jeu : 100 marqueurs, selon les données disponibles.");
    }

    private static void Markers(ViewState s, WindowPreferences t, SettingsActions a)
    {
        SliderRow("Taille des icônes", s.Appearance.MarkerScale * 100, 50, 200, "%.0f %%", t,
            v => a.Appearance(p => p with { MarkerScale = v / 100 }));
        SliderRow("Votre personnage", s.Appearance.PlayerScale * 100, 50, 200, "%.0f %%", t,
            v => a.Appearance(p => p with { PlayerScale = v / 100 }));
        ImGui.Separator();
        Toggle("Tout masquer", s.Appearance.HideMarkers, t, v => a.Appearance(p => p with { HideMarkers = v }));
        ImGui.TextDisabled("Votre personnage reste visible.");
        ImGui.Spacing();
        SettingsTheme.Label("Masquer par catégorie", t);
        ImGui.BeginDisabled(s.Appearance.HideMarkers);
        foreach (var (category, label) in MarkerPolicy.Categories)
            Toggle(category == MarkerCategory.Services ? "Services" : label, (s.Appearance.HiddenCategories & category) != 0, t,
                v => a.Appearance(p => p with { HiddenCategories = v ? p.HiddenCategories | category : p.HiddenCategories & ~category }));
        ImGui.EndDisabled();
        ImGui.TextWrapped("Les filtres gardent les quêtes en cours, l’épopée, les quêtes bleues et les icônes inconnues.");
    }

    private static void Style(WindowPreferences t, SettingsActions a, string fontStatus)
    {
        Row("Habillage", t, () =>
        {
            var skin = (int)t.Skin;
            if (ImGui.Combo("##skin", ref skin, new[] { "LMeter", "Obsidienne", "Dalamud" }, 3))
                a.Window(WindowPreferences.Preset((WindowSkin)skin) with { FontSize = t.FontSize });
        });
        Row("Police", t, () =>
        {
            var font = (int)t.Typeface;
            if (ImGui.Combo("##font", ref font, new[] { "Expressway", "Dalamud", "Segoe UI" }, 3))
                a.Window(t with { Typeface = (WindowTypeface)font });
        });
        ImGui.TextWrapped(fontStatus);
        SliderRow("Taille du texte", t.FontSize, 14, 26, "%.0f px", t, v => a.Window(t with { FontSize = MathF.Round(v) }));
        ImGui.BeginDisabled(t.Skin == WindowSkin.Dalamud);
        ColorRow("Fond", t.Background, t, v => a.Window(t with { Background = v }));
        SliderRow("Opacité du fond", t.BackgroundOpacity * 100, 15, 100, "%.0f %%", t, v => a.Window(t with { BackgroundOpacity = v / 100 }));
        ColorRow("Texte", t.Text, t, v => a.Window(t with { Text = v }));
        ColorRow("Accent", t.Accent, t, v => a.Window(t with { Accent = v }));
        ImGui.EndDisabled();
        if (t.Skin == WindowSkin.Dalamud) ImGui.TextWrapped("Les couleurs suivent Dalamud. Choisir LMeter ou Obsidienne pour les personnaliser.");
        Row("Relief des libellés", t, () =>
        {
            var relief = (int)t.Relief;
            if (ImGui.Combo("##relief", ref relief, new[] { "Aucun", "Ombre", "Contour" }, 3)) a.Window(t with { Relief = (LabelRelief)relief });
        });
        if (ImGui.CollapsingHeader("Espacements et alignement"))
        {
            SliderRow("Marges de la fenêtre", t.Padding, 8, 20, "%.0f px", t, v => a.Window(t with { Padding = v }));
            SliderRow("Espacement des lignes", t.RowSpacing, 4, 16, "%.0f px", t, v => a.Window(t with { RowSpacing = v }));
            SliderRow("Décalage libellés X", t.LabelOffsetX, -4, 4, "%.0f px", t, v => a.Window(t with { LabelOffsetX = v }));
            SliderRow("Décalage libellés Y", t.LabelOffsetY, -4, 4, "%.0f px", t, v => a.Window(t with { LabelOffsetY = v }));
            Toggle("Libellés alignés à droite", t.AlignLabelsRight, t, v => a.Window(t with { AlignLabelsRight = v }));
            ImGui.TextWrapped("Alignement à droite en vue à deux colonnes. La position de la mini-carte se règle dans l’éditeur ATH du jeu.");
        }
        if (ImGui.SmallButton("Restaurer cet habillage")) a.Window(WindowPreferences.Preset(t.Skin));
        ImGui.TextWrapped("Ces réglages concernent cette fenêtre. La couleur du cadre de la mini-carte reste dans Carte.");
    }

    private static void Startup(ViewState s, WindowPreferences t, SettingsActions a)
    {
        Toggle("Réactiver le zoom", s.AutoZoom, t, a.AutoZoom);
        ImGui.TextWrapped("Au chargement, applique le dernier zoom choisi dès que la mini-carte est disponible.");
        Toggle("Ouvrir les réglages", s.OpenOnLoad, t, a.OpenOnLoad);
        ImGui.TextWrapped("La commande /minizoom permet toujours de retrouver cette fenêtre.");
    }

    private static void SliderRow(string label, float value, float min, float max, string format,
        WindowPreferences t, Action<float> change) => Row(label, t,
        () => { if (ImGui.SliderFloat("##value", ref value, min, max, format)) change(value); });

    private static void ColorRow(string label, uint value, WindowPreferences t, Action<uint> change, bool alpha = false) =>
        Row(label, t, () =>
        {
            var color = SettingsTheme.Color(value);
            var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.AlphaPreviewHalf;
            if (!alpha) flags |= ImGuiColorEditFlags.NoAlpha;
            if (ImGui.ColorEdit4("##color", ref color, flags)) change(SettingsTheme.Pack(color));
        });
}
