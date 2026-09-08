using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace MinimapZoom;

internal sealed record ViewState(bool Enabled, bool Ready, float Zoom, string Message,
    AppearanceSettings Appearance, bool AutoZoom = false, bool OpenOnLoad = false,
    ProfileView? Profiles = null, ZoomShortcut? Shortcut = null, bool Temporary = false,
    float? SavedZoom = null, bool Suspended = false);
internal sealed record ProfileView(string ActiveId, string ManualId, MinimapProfile[] Items,
    bool Automatic, uint TerritoryId, string ZoneName, ZoneProfileRule[] Rules);
internal enum ProfileOperation { Select, Duplicate, Rename, Delete, Automatic, Bind, Unbind }
internal sealed record ProfileRequest(ProfileOperation Operation, string Value = "", uint TerritoryId = 0, bool Enabled = false);
internal sealed record SettingsActions(Action<float> Zoom, Action RestoreZoom, Action RestoreAll,
    Action<Func<AppearanceSettings, AppearanceSettings>> Appearance, Action<bool> AutoZoom,
    Action<bool> OpenOnLoad, Action<ProfileRequest>? Profiles = null, Action<ZoomShortcut>? Shortcut = null);

// Production widgets shared with the offline ImGui harness. All edits dispatch to the framework thread.
internal sealed class SettingsPanel
{
    private static readonly string[] Tabs = ["Carte", "Marqueurs", "Profils", "Utilisation"];
    private static readonly string[] Frames = ["Aucun", "Fin", "Épais", "Double", "Angles"];
    private static readonly string[] Shapes = ["Ronde", "Carrée"];
    private static readonly string[] Presets = ["Tous", "Déplacements", "Quêtes", "Personnalisé"];
    private static readonly int[] ShortcutKeys = Enumerable.Range(0x70, 12).Concat(Enumerable.Range(0x41, 26)).ToArray();
    private static readonly string[] KeyNames = ShortcutKeys.Select(ZoomShortcut.KeyName).ToArray();
    private string editingId = "";
    private string profileName = "";
    private string duplicateName = "";
    public int? SelectTab { get; set; }
    public int ActiveTab { get; private set; }
    // Optional instrumentation for real mouse-input verification; unused in game.
    internal Action<string, Vector2, Vector2>? RecordItem { get; set; }
    internal Action? ContentFrame { get; set; }

    public void Draw(ViewState state, SettingsActions actions, string diagnostic, bool compatible = true)
    {
        var current = state.Profiles?.Items.FirstOrDefault(p => p.Id == state.Profiles.ActiveId);
        if (compatible && state.Profiles is { } profiles)
            Row("Profil actif", () => ProfileCombo("active-profile", profiles.ActiveId, profiles,
                id => actions.Profiles?.Invoke(new(ProfileOperation.Select, id))));
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[compatible && state.Ready ? (int)ImGuiCol.CheckMark : (int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped(state.Message);
        ImGui.PopStyleColor();
        if (state.Profiles?.Automatic == true) Hint($"Profil automatique · {state.Profiles.ZoneName}");
        if (state.Suspended && ImGui.Button("Reprendre le profil")) actions.Zoom(state.SavedZoom ?? state.Zoom);
        ImGui.Spacing();
        if (compatible && ImGui.BeginTabBar("SettingsTabs", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            for (var i = 0; i < Tabs.Length; i++)
            {
                var open = ImGui.BeginTabItem(Tabs[i], SelectTab == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None);
                Mark("tab-" + i);
                if (!open) continue;
                ActiveTab = i;
                ImGui.Spacing();
                var height = Math.Max(60 * SettingsTheme.Scale, ImGui.GetContentRegionAvail().Y -
                    (ImGui.GetFontSize() * 2 + 20 * SettingsTheme.Scale));
                ImGui.BeginChild("content-" + i, new Vector2(0, height), false);
                ContentFrame?.Invoke();
                switch (i)
                {
                    case 0: Map(state, actions); break;
                    case 1: Markers(state, actions); break;
                    case 2: Profiles(state, actions); break;
                    case 3: Usage(state, actions, diagnostic); break;
                }
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
            SelectTab = null;
        }
        if (!compatible) ImGui.TextWrapped(diagnostic);
        ImGui.Spacing(); ImGui.Separator();
        Hint(!compatible ? "Effets désactivés · réglages conservés" : current == null ?
            "Application immédiate · sauvegarde automatique" : $"Modifications enregistrées dans « {current.Name} ».");
    }

    private void Mark(string id) => RecordItem?.Invoke(id, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextUnformatted(title);
        ImGui.SameLine();
        var position = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var middle = ImGui.GetFontSize() / 2;
        if (width > 8 * SettingsTheme.Scale)
            ImGui.GetWindowDrawList().AddLine(position + new Vector2(8 * SettingsTheme.Scale, middle),
                position + new Vector2(width, middle), ImGui.GetColorU32(ImGuiCol.Separator));
        ImGui.Dummy(new Vector2(Math.Max(0, width), ImGui.GetFontSize()));
        ImGui.Spacing();
    }

    private static void Hint(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private static void Row(string label, Action control)
    {
        ImGui.PushID(label);
        if (ImGui.GetContentRegionAvail().X < 440 * SettingsTheme.Scale)
        {
            ImGui.TextWrapped(label);
            ImGui.SetNextItemWidth(-1); control();
        }
        else if (ImGui.BeginTable("row", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("label", ImGuiTableColumnFlags.WidthStretch, 0.44f);
            ImGui.TableSetupColumn("control", ImGuiTableColumnFlags.WidthStretch, 0.56f);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.TextWrapped(label);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); control();
            ImGui.EndTable();
        }
        ImGui.PopID();
    }

    private void Toggle(string id, string label, bool value, Action<bool> change)
    {
        if (ImGui.Checkbox(label + "##" + id, ref value)) change(value);
        Mark(id);
    }

    private void Slider(string id, string label, float value, float min, float max, string format, Action<float> change) =>
        Row(label, () =>
        {
            if (ImGui.SliderFloat("##" + id, ref value, min, max, format, ImGuiSliderFlags.AlwaysClamp)) change(value);
            Mark(id);
        });

    private void Map(ViewState s, SettingsActions a)
    {
        Section("Vue de la carte");
        Slider("zoom", "Zoom du profil", s.SavedZoom ?? s.Zoom, 0.25f, 2, "%.2f", a.Zoom);
        Hint("Dézoomer à gauche · agrandir à droite. Ctrl + clic pour saisir une valeur.");
        if (!s.Enabled && ImGui.SmallButton("Appliquer le zoom mémorisé")) a.Zoom(s.SavedZoom ?? s.Zoom);
        Row("Forme", () =>
        {
            var shape = s.Appearance.Square ? 1 : 0;
            if (ImGui.Combo("##shape", ref shape, Shapes, Shapes.Length)) a.Appearance(p => p with { Square = shape == 1 });
            Mark("shape");
        });
        Slider("opacity", "Opacité du fond", s.Appearance.MapOpacity * 100, 0, 100, "%.0f %%",
            v => a.Appearance(p => p with { MapOpacity = v / 100 }));
        Hint("Les icônes et votre personnage gardent leur opacité.");

        Section("Cadre");
        Toggle("frame", "Masquer le cadre", s.Appearance.HideFrame, v => a.Appearance(p => p with { HideFrame = v }));
        if (s.Appearance.Square && !s.Appearance.HideFrame)
        {
            Row("Style", () =>
            {
                var style = (int)s.Appearance.FrameStyle;
                if (ImGui.Combo("##frame-style", ref style, Frames, Frames.Length))
                    a.Appearance(p => p with { FrameStyle = (SquareFrameStyle)style, FrameThickness = 0 });
            });
            if (s.Appearance.FrameStyle != SquareFrameStyle.None)
            {
                Row("Couleur et transparence", () =>
                {
                    var color = SettingsTheme.Color(s.Appearance.FrameColor);
                    if (ImGui.ColorEdit4("##frame-color", ref color, ImGuiColorEditFlags.NoInputs |
                        ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.AlphaPreviewHalf))
                        a.Appearance(p => p with { FrameColor = SettingsTheme.Pack(color) });
                });
                var frameOptions = ImGui.CollapsingHeader("Options du cadre");
                Mark("frame-options");
                if (frameOptions)
                {
                    Slider("thickness", "Épaisseur", FramePixels.Thickness(s.Appearance.FrameStyle, s.Appearance.FrameThickness),
                        1, 8, "%.0f px", v => a.Appearance(p => p with { FrameThickness = (int)MathF.Round(v) }));
                    if (s.Appearance.FrameStyle == SquareFrameStyle.Corners)
                        Slider("corners", "Longueur des angles", s.Appearance.CornerLength, 8, 64, "%.0f px",
                            v => a.Appearance(p => p with { CornerLength = (int)MathF.Round(v) }));
                    Hint("Dimensions logiques, mises à l’échelle avec votre HUD.");
                    ImGui.Spacing();
                    if (ImGui.Button("Discret")) a.Appearance(p => p with { FrameStyle = SquareFrameStyle.Thin,
                        FrameThickness = 1, FrameColor = 0xBFFFFFFF });
                    ImGui.SameLine();
                    if (ImGui.Button("Classique")) a.Appearance(p => p with { FrameStyle = SquareFrameStyle.Corners,
                        FrameThickness = 3, CornerLength = 22, FrameColor = 0xFFE4D5B7 });
                    ImGui.SameLine();
                    if (ImGui.Button("Contrasté")) a.Appearance(p => p with { FrameStyle = SquareFrameStyle.Double,
                        FrameThickness = 2, FrameColor = 0xFFFFFFFF });
                }
            }
        }
        else if (!s.Appearance.Square) Hint("La forme carrée permet de choisir le style et les dimensions du cadre.");

        Section("Éléments à masquer");
        var columns = ImGui.GetContentRegionAvail().X >= 520 * SettingsTheme.Scale ? 2 : 1;
        if (ImGui.BeginTable("decorations", columns, ImGuiTableFlags.SizingStretchSame))
        {
            void Item(string id, string label, bool value, Action<bool> change)
            { ImGui.TableNextColumn(); Toggle(id, label, value, change); }
            Item("cardinals", "Points cardinaux", s.Appearance.HideCardinals, v => a.Appearance(p => p with { HideCardinals = v }));
            Item("coordinates", "Coordonnées", s.Appearance.HideCoordinates, v => a.Appearance(p => p with { HideCoordinates = v }));
            Item("sun", "Soleil et lune", s.Appearance.HideSunMoon, v => a.Appearance(p => p with { HideSunMoon = v }));
            Item("weather", "Météo", s.Appearance.HideWeather, v => a.Appearance(p => p with { HideWeather = v }));
            Item("buttons", "Boutons + / - / nord", s.Appearance.HideButtons, v => a.Appearance(p => p with { HideButtons = v }));
            ImGui.EndTable();
        }
        if (ImGui.SmallButton("Tout masquer")) a.Appearance(p => p with
            { HideCardinals = true, HideCoordinates = true, HideSunMoon = true, HideWeather = true, HideButtons = true });
        ImGui.SameLine();
        if (ImGui.SmallButton("Tout réafficher")) a.Appearance(p => p with
            { HideCardinals = false, HideCoordinates = false, HideSunMoon = false, HideWeather = false, HideButtons = false });
    }

    private void Markers(ViewState s, SettingsActions a)
    {
        Section("Lisibilité");
        Slider("marker-scale", "Taille des icônes", s.Appearance.MarkerScale * 100, 50, 200, "%.0f %%",
            v => a.Appearance(p => p with { MarkerScale = v / 100 }));
        Slider("player-scale", "Votre personnage", s.Appearance.PlayerScale * 100, 50, 200, "%.0f %%",
            v => a.Appearance(p => p with { PlayerScale = v / 100 }));
        Toggle("crowding", "Réduire les icônes proches", s.Appearance.ReduceCrowding, v => a.Appearance(p => p with { ReduceCrowding = v }));
        Hint("Au zoom inférieur à 0,50, réduit les icônes secondaires trop proches. Les positions, objectifs en cours et repères importants sont conservés.");
        Section("Filtres");
        Row("Preset", () =>
        {
            var selected = 3;
            if (!s.Appearance.HideMarkers)
                for (var i = 0; i < 3; i++) if (s.Appearance.HiddenCategories == MarkerPresets.Hidden((MarkerPreset)i)) selected = i;
            if (ImGui.Combo("##marker-preset", ref selected, Presets, Presets.Length) && selected < 3)
                a.Appearance(p => p with { HideMarkers = false, HiddenCategories = MarkerPresets.Hidden((MarkerPreset)selected) });
            Mark("marker-preset");
        });
        Toggle("hide-all", "Tout masquer sauf votre personnage", s.Appearance.HideMarkers, v => a.Appearance(p => p with { HideMarkers = v }));
        ImGui.Spacing();
        Hint("Catégories à masquer");
        ImGui.BeginDisabled(s.Appearance.HideMarkers);
        var columns = ImGui.GetContentRegionAvail().X >= 540 * SettingsTheme.Scale ? 2 : 1;
        if (ImGui.BeginTable("categories", columns, ImGuiTableFlags.SizingStretchSame))
        {
        foreach (var (category, label) in MarkerPolicy.Categories)
        {
            ImGui.TableNextColumn();
            Toggle("category-" + category, category == MarkerCategory.Services ? "Services" : label,
                (s.Appearance.HiddenCategories & category) != 0,
                v => a.Appearance(p => p with { HiddenCategories = v ? p.HiddenCategories | category : p.HiddenCategories & ~category }));
        }
        ImGui.EndTable();
        }
        ImGui.EndDisabled();
        Hint("Les filtres préservent l’épopée, les quêtes bleues et en cours, ainsi que les icônes inconnues. Le masquage global les cache aussi.");
    }

    private void ProfileCombo(string id, string value, ProfileView profiles, Action<string> change)
    {
        var index = Array.FindIndex(profiles.Items, p => p.Id == value);
        var names = profiles.Items.Select(p => p.Name).ToArray();
        if (ImGui.Combo("##" + id, ref index, names, names.Length) && index >= 0) change(profiles.Items[index].Id);
        Mark(id);
    }

    private void Profiles(ViewState s, SettingsActions a)
    {
        if (s.Profiles is not { } p || a.Profiles == null) { Hint("Profils indisponibles."); return; }
        var active = p.Items.First(x => x.Id == p.ActiveId);
        if (editingId != p.ActiveId) { editingId = p.ActiveId; profileName = active.Name; duplicateName = active.Name + " — copie"; }
        Section("Profil sélectionné");
        Hint("Un profil mémorise le zoom, la forme, le fond, le cadre, les éléments masqués et les filtres. Les modifications sont sauvegardées automatiquement.");
        Row("Nom", () => { ImGui.InputText("##profile-name", ref profileName, 40); Mark("profile-name"); });
        ImGui.BeginDisabled(ProfileStore.Name(profileName) == active.Name);
        if (ImGui.Button("Renommer")) a.Profiles(new(ProfileOperation.Rename, profileName));
        Mark("rename"); ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(p.Items.Length >= ProfileStore.MaximumProfiles);
        if (ImGui.Button("Dupliquer…")) { duplicateName = active.Name + " — copie"; ImGui.OpenPopup("duplicate"); }
        Mark("duplicate"); ImGui.EndDisabled();
        if (ImGui.BeginPopup("duplicate"))
        {
            ImGui.TextUnformatted("Nom du nouveau profil");
            ImGui.SetNextItemWidth(230 * SettingsTheme.Scale);
            ImGui.InputText("##duplicate-name", ref duplicateName, 40);
            if (ImGui.Button("Créer la copie")) { a.Profiles(new(ProfileOperation.Duplicate, duplicateName)); ImGui.CloseCurrentPopup(); }
            Mark("create-copy");
            ImGui.EndPopup();
        }
        ImGui.Spacing();
        ImGui.BeginDisabled(p.Items.Length <= 1);
        if (ImGui.SmallButton("Supprimer ce profil…")) ImGui.OpenPopup("delete");
        ImGui.EndDisabled();
        if (ImGui.BeginPopup("delete"))
        {
            ImGui.TextWrapped($"Supprimer « {active.Name} » et ses associations de zone ?");
            if (ImGui.Button("Supprimer")) { a.Profiles(new(ProfileOperation.Delete)); ImGui.CloseCurrentPopup(); }
            ImGui.SameLine(); if (ImGui.Button("Annuler")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        Section("Changement selon la zone");
        Toggle("automatic", "Activer les profils automatiques", p.Automatic,
            v => a.Profiles(new(ProfileOperation.Automatic, Enabled: v)));
        Hint("Dans une zone sans association, revient au dernier profil choisi manuellement. Choisir un profil en haut repasse en mode manuel.");
        Row("Zone actuelle", () => ImGui.TextWrapped(p.ZoneName));
        ImGui.BeginDisabled(p.TerritoryId == 0);
        if (ImGui.Button("Associer le profil actif à cette zone")) a.Profiles(new(ProfileOperation.Bind, p.ActiveId));
        Mark("bind-zone"); ImGui.EndDisabled();
        if (p.Rules.Length == 0) Hint("Aucune zone associée. Choisissez un profil, puis associez-le à la zone actuelle.");
        foreach (var rule in p.Rules)
        {
            ImGui.PushID((int)rule.TerritoryId);
            ImGui.Separator();
            ImGui.TextWrapped(rule.ZoneName);
            Hint(p.Items.FirstOrDefault(x => x.Id == rule.ProfileId)?.Name ?? "Profil indisponible");
            if (ImGui.SmallButton("Retirer l’association")) a.Profiles(new(ProfileOperation.Unbind, TerritoryId: rule.TerritoryId));
            ImGui.PopID();
        }
    }

    private void Usage(ViewState s, SettingsActions a, string diagnostic)
    {
        var key = s.Shortcut ?? new ZoomShortcut();
        Section("Vue temporaire");
        Toggle("shortcut-enabled", "Dézoomer en maintenant une touche", key.Enabled, v => a.Shortcut?.Invoke(key with { Enabled = v }));
        ImGui.BeginDisabled(!key.Enabled);
        Row("Touche", () =>
        {
            var index = Array.IndexOf(ShortcutKeys, key.Key);
            if (ImGui.Combo("##shortcut-key", ref index, KeyNames, KeyNames.Length)) a.Shortcut?.Invoke(key with { Key = ShortcutKeys[index] });
        });
        Row("Modificateurs", () =>
        {
            var control = key.Control; var alt = key.Alt; var shift = key.Shift;
            if (ImGui.Checkbox("Ctrl", ref control)) a.Shortcut?.Invoke(key with { Control = control });
            ImGui.SameLine(); if (ImGui.Checkbox("Alt", ref alt)) a.Shortcut?.Invoke(key with { Alt = alt });
            ImGui.SameLine(); if (ImGui.Checkbox("Maj", ref shift)) a.Shortcut?.Invoke(key with { Shift = shift });
        });
        Slider("temporary-zoom", "Zoom temporaire", key.Zoom, 0.25f, 2, "%.2f", v => a.Shortcut?.Invoke(key with { Zoom = v }));
        ImGui.EndDisabled();
        Hint($"{key.Label} · relâchez pour retrouver votre vue. Inactif pendant la saisie et hors du jeu. Choisissez une combinaison libre. Le zoom reste au moins aussi éloigné que votre vue actuelle.");
        Section("Au chargement");
        Toggle("startup-zoom", "Réactiver le zoom du profil", s.AutoZoom, a.AutoZoom);
        Toggle("startup-settings", "Ouvrir les réglages", s.OpenOnLoad, a.OpenOnLoad);
        Hint("La commande /minizoom permet toujours de retrouver cette fenêtre.");
        Section("Restaurer l’affichage");
        if (ImGui.Button("Rétablir le zoom du jeu")) a.RestoreZoom();
        Mark("restore-zoom");
        Hint("Conserve l’apparence et désactive le zoom au chargement.");
        if (ImGui.Button("Restaurer toute la mini-carte")) a.RestoreAll();
        Mark("restore-all");
        Hint("Revient à l’affichage du jeu et suspend les profils automatiques. Vos profils restent enregistrés. /minizoom off");
        if (ImGui.CollapsingHeader("Diagnostic"))
        {
            Hint("Portée étendue : capacité native de 100 marqueurs, selon les données disponibles dans le client.");
            ImGui.TextWrapped(diagnostic);
        }
    }
}
