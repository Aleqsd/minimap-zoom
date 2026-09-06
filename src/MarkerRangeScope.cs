namespace MinimapZoom;

// A scope lives only inside AgentHUD.UpdateNaviMap. No global constants, map scale,
// marker counts, coordinates or server data are changed.
internal unsafe struct MarkerRangeScope(nint hud, float zoom)
{
    internal const int MarkerVectorOffset = 0x4B30;
    internal const int RadiusOffset = 0x4E0C;
    internal const int SquaredRadiusOffset = 0x4E10;
    private readonly nint hud = hud;
    private readonly float multiplier = Multiplier(zoom);
    private bool applied;
    private float originalRadius;
    private float originalSquared;
    private float expandedRadius;
    private float expandedSquared;

    internal static float Multiplier(float zoom) => float.IsFinite(zoom) && zoom > 0
        ? Math.Clamp(0.5f / ZoomPolicy.Extended(zoom), 1f, 2f) : 1f;

    public float Expand(nint vector, byte isMinimap, float eventRadius)
    {
        if (hud == 0 || vector != hud + MarkerVectorOffset || isMinimap != 1 ||
            multiplier <= 1 || applied || !float.IsFinite(eventRadius) || eventRadius <= 0) return eventRadius;
        var radius = (float*)(hud + RadiusOffset);
        var squared = (float*)(hud + SquaredRadiusOffset);
        var nextRadius = *radius * multiplier;
        var nextSquared = *squared * multiplier * multiplier;
        var nextEvent = eventRadius * multiplier;
        if (!float.IsFinite(*radius) || *radius <= 0 || !float.IsFinite(*squared) || *squared <= 0 ||
            !float.IsFinite(nextRadius) || !float.IsFinite(nextSquared) || !float.IsFinite(nextEvent)) return eventRadius;
        // This call occurs just after the native 175 / map-scale calculation, before culling.
        originalRadius = *radius;
        originalSquared = *squared;
        expandedRadius = nextRadius;
        expandedSquared = nextSquared;
        *radius = nextRadius;
        *squared = nextSquared;
        applied = true;
        return nextEvent;
    }

    public void Restore()
    {
        if (!applied) return;
        var radius = (float*)(hud + RadiusOffset);
        var squared = (float*)(hud + SquaredRadiusOffset);
        if (*radius == expandedRadius) *radius = originalRadius;
        if (*squared == expandedSquared) *squared = originalSquared;
        applied = false;
    }
}
