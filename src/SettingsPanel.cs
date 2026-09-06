using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace MinimapZoom;

internal sealed record ViewState(bool Enabled, bool Ready, float Zoom, string Message,
    AppearanceSettings Appearance, bool AutoZoom = false, bool OpenOnLoad = false);

internal sealed record SettingsActions(Action<float> Zoom, Action RestoreZoom, Action RestoreAll,
    Action<Func<AppearanceSettings, AppearanceSettings>> Appearance, Action<bool> AutoZoom,
    Action<bool> OpenOnLoad);

// Shared by the Dalamud window and the real ImGui offline preview.
internal sealed class SettingsPanel
{
    private static readonly string[] FrameNames = ["Aucun", "Fin", "Épais", "Double", "Angles"];
    private static readonly string[] Tabs = ["Carte", "Marqueurs", "Démarrage"];
    public int? SelectTab { get; set; }
    public int ActiveTab { get; private set; }

    public void Draw(ViewState state, SettingsActions actions,
        string diagnostic, bool compatible = true)
    {
        var scale = SettingsTheme.Scale;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(pos, pos + new Vector2(3 * scale, ImGui.GetFontSize() + 8 * scale),
            ImGui.GetColorU32(state.Ready ? ImGuiCol.CheckMark : ImGuiCol.TextDisabled));
        SettingsTheme.Label(state.Message);
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
                    case 0: Map(state, actions); break;
                    case 1: Markers(state, actions); break;
                    case 2: Startup(state, actions); break;
                }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
            SelectTab = null;
        }
        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button("Restaurer la mini-carte")) actions.RestoreAll();
        ImGui.TextDisabled("Affichage du jeu · /minizoom off");
        if (ImGui.CollapsingHeader("Diagnostic")) ImGui.TextWrapped(diagnostic);
    }

    private static void Row(string label, Action control)
    {
        ImGui.PushID(label);
        if (ImGui.GetContentRegionAvail().X < 430 * SettingsTheme.Scale)
        {
            SettingsTheme.Label(label);
            ImGui.SetNextItemWidth(-1);
            control();
        }
        else if (ImGui.BeginTable("row", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("label", ImGuiTableColumnFlags.WidthStretch, 0.46f);
            ImGui.TableSetupColumn("control", ImGuiTableColumnFlags.WidthStretch, 0.54f);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            SettingsTheme.Label(label);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            control();
            ImGui.EndTable();
        }
        ImGui.PopID();
    }

    private static void Toggle(string label, bool value, Action<bool> change) =>
        Row(label, () => { if (ImGui.Checkbox("##toggle", ref value)) change(value); });

    private static void Map(ViewState s, SettingsActions a)
    {
        var zoom = s.Zoom;
        SettingsTheme.Label("Zoom de la mini-carte");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##zoom", ref zoom, 0.25f, 2, "%.2f")) a.Zoom(zoom);
        ImGui.TextDisabled("Voir plus loin à gauche · agrandir à droite");
        if (s.Enabled && ImGui.SmallButton("Rétablir le zoom du jeu")) a.RestoreZoom();
        ImGui.Spacing(); ImGui.Separator();
        Toggle("Mini-carte carrée", s.Appearance.Square, v => a.Appearance(p => p with { Square = v }));
        Toggle("Masquer le cadre", s.Appearance.HideFrame, v => a.Appearance(p => p with { HideFrame = v }));
        Toggle("Masquer soleil / lune", s.Appearance.HideSunMoon, v => a.Appearance(p => p with { HideSunMoon = v }));
        Toggle("Masquer la météo", s.Appearance.HideWeather, v => a.Appearance(p => p with { HideWeather = v }));
        Toggle("Masquer les boutons", s.Appearance.HideButtons, v => a.Appearance(p => p with { HideButtons = v }));
        ImGui.TextDisabled("Boutons : + / - et verrouillage du nord.");
        if (s.Appearance.Square && !s.Appearance.HideFrame)
        {
            Row("Cadre carré", () =>
            {
                var style = (int)s.Appearance.FrameStyle;
                if (ImGui.Combo("##frame", ref style, FrameNames, FrameNames.Length))
                    a.Appearance(p => p with { FrameStyle = (SquareFrameStyle)style });
            });
            if (s.Appearance.FrameStyle != SquareFrameStyle.None)
                ColorRow("Couleur du cadre", s.Appearance.FrameColor, v => a.Appearance(p => p with { FrameColor = v }), true);
        }
        ImGui.Spacing();
        ImGui.TextWrapped("Application immédiate. La molette permet aussi de régler le zoom.");
        ImGui.TextWrapped("La portée des icônes fixes suit le dézoom. Limite du jeu : 100 marqueurs, selon les données disponibles.");
    }

    private static void Markers(ViewState s, SettingsActions a)
    {
        SliderRow("Taille des icônes", s.Appearance.MarkerScale * 100, 50, 200, "%.0f %%",
            v => a.Appearance(p => p with { MarkerScale = v / 100 }));
        SliderRow("Votre personnage", s.Appearance.PlayerScale * 100, 50, 200, "%.0f %%",
            v => a.Appearance(p => p with { PlayerScale = v / 100 }));
        ImGui.Separator();
        Toggle("Tout masquer", s.Appearance.HideMarkers, v => a.Appearance(p => p with { HideMarkers = v }));
        ImGui.TextDisabled("Votre personnage reste visible.");
        ImGui.Spacing();
        SettingsTheme.Label("Masquer par catégorie");
        ImGui.BeginDisabled(s.Appearance.HideMarkers);
        foreach (var (category, label) in MarkerPolicy.Categories)
            Toggle(category == MarkerCategory.Services ? "Services" : label, (s.Appearance.HiddenCategories & category) != 0,
                v => a.Appearance(p => p with { HiddenCategories = v ? p.HiddenCategories | category : p.HiddenCategories & ~category }));
        ImGui.EndDisabled();
        ImGui.TextWrapped("Les filtres gardent les quêtes en cours, l’épopée, les quêtes bleues et les icônes inconnues.");
    }

    private static void Startup(ViewState s, SettingsActions a)
    {
        Toggle("Réactiver le zoom", s.AutoZoom, a.AutoZoom);
        ImGui.TextWrapped("Au chargement, applique le dernier zoom choisi dès que la mini-carte est disponible.");
        Toggle("Ouvrir les réglages", s.OpenOnLoad, a.OpenOnLoad);
        ImGui.TextWrapped("La commande /minizoom permet toujours de retrouver cette fenêtre.");
    }

    private static void SliderRow(string label, float value, float min, float max, string format,
        Action<float> change) => Row(label,
        () => { if (ImGui.SliderFloat("##value", ref value, min, max, format)) change(value); });

    private static void ColorRow(string label, uint value, Action<uint> change, bool alpha = false) =>
        Row(label, () =>
        {
            var color = SettingsTheme.Color(value);
            var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.AlphaPreviewHalf;
            if (!alpha) flags |= ImGuiColorEditFlags.NoAlpha;
            if (ImGui.ColorEdit4("##color", ref color, flags)) change(SettingsTheme.Pack(color));
        });
}
