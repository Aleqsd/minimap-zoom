namespace MinimapZoom;

internal readonly record struct MarkerFootprint(float X, float Y, nint Parent, bool Visible, bool Secondary);

internal static class MarkerCrowding
{
    // Only shrink recognized secondary icons. Never move, remove or enlarge an icon.
    public static bool IsSecondary(uint primary, uint secondary)
    {
        const MarkerCategory allowed = MarkerCategory.Shops | MarkerCategory.Services |
            MarkerCategory.AvailableSideQuests | MarkerCategory.AvailableLevequests | MarkerCategory.TripleTriad;
        return MarkerPolicy.ShouldHide(primary, secondary, allowed, false);
    }

    public static float Scale(ReadOnlySpan<MarkerFootprint> markers, int index, float scale)
    {
        var point = markers[index];
        if (!point.Visible || !point.Secondary) return scale;
        var distance = float.MaxValue;
        for (var i = 0; i < markers.Length; i++)
        {
            var other = markers[i];
            if (i == index || !other.Visible || point.Parent != other.Parent) continue;
            var dx = point.X - other.X; var dy = point.Y - other.Y;
            distance = Math.Min(distance, dx * dx + dy * dy);
        }
        var factor = Math.Clamp(MathF.Sqrt(distance) / (20 * scale), 0.65f, 1f);
        return Math.Max(0.5f, scale * factor);
    }
}
