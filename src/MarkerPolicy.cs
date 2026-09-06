namespace MinimapZoom;

[Flags]
public enum MarkerCategory
{
    None = 0,
    Shops = 1,
    Services = 2,
    AvailableSideQuests = 4,
    AvailableLevequests = 8,
    Fates = 16,
    TripleTriad = 32,
    Aetherytes = 64,
    Party = 128,
    Enemies = 256,
    All = 511,
}

internal static class MarkerPolicy
{
    public static readonly (MarkerCategory Category, string Label)[] Categories =
    [
        (MarkerCategory.Shops, "Marchands"),
        (MarkerCategory.Services, "Services : réparation, marché, servants…"),
        (MarkerCategory.AvailableSideQuests, "Quêtes secondaires disponibles"),
        (MarkerCategory.AvailableLevequests, "Mandats disponibles"),
        (MarkerCategory.Fates, "Icônes d’ALÉA"),
        (MarkerCategory.TripleTriad, "Triple Triade"),
        (MarkerCategory.Aetherytes, "Éthérites et réseau urbain"),
        (MarkerCategory.Party, "Groupe et alliance"),
        (MarkerCategory.Enemies, "Ennemis"),
    ];

    // Identifiers are game data, not texture paths or translated UI labels.
    // Unknown icons and ongoing quest objectives have no filter category and remain visible.
    public static MarkerCategory Classify(uint icon) => icon switch
    {
        60412 or 60935 or 60987 or 63919 => MarkerCategory.Shops,
        60434 or 60425 or 60426 or 60560 or 60570 or 60551 or 60910 or 60581 => MarkerCategory.Services,
        71021 or 71022 or 71031 or 71032 or 71111 or 70965 or 70966 or 70967 or 70968 or
            70985 or 70986 or 70987 or 70988 => MarkerCategory.AvailableSideQuests,
        71041 or 71081 or 71082 => MarkerCategory.AvailableLevequests,
        >= 60501 and <= 60508 or 60458 => MarkerCategory.Fates,
        71101 or 71102 => MarkerCategory.TripleTriad,
        60453 or 60430 or 60760 or 60959 => MarkerCategory.Aetherytes,
        60421 or 60358 => MarkerCategory.Party,
        60004 or 60422 or 60401 => MarkerCategory.Enemies,
        _ => MarkerCategory.None,
    };

    public static bool ShouldHide(uint primary, uint secondary, MarkerCategory hidden, bool hideAll)
    {
        if (hideAll) return true;
        // Mixed markers are only removed if every non-empty icon belongs to a hidden category.
        // A quest overlay on a merchant must not disappear just because shops are hidden.
        bool Matches(uint icon) => icon != 0 && (Classify(icon) & hidden) != 0;
        return (primary != 0 || secondary != 0) &&
            (primary == 0 || Matches(primary)) && (secondary == 0 || Matches(secondary));
    }

    public static float Scale(float value) => float.IsFinite(value) ? Math.Clamp(value, 0.5f, 2f) : 1f;

    public static bool IsArea(uint icon) => icon is >= 60495 and <= 60498 or
        >= 60541 and <= 60547;
}
