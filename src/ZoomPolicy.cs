namespace MinimapZoom;

internal static class ZoomPolicy
{
    public const float Minimum = 0.25f;
    public const float NativeMinimum = 0.5f;
    public const float Maximum = 2f;
    public const float DefaultNative = 0.75f;

    public static float Extended(float zoom) =>
        float.IsFinite(zoom) ? Math.Clamp(zoom, Minimum, Maximum) : NativeMinimum;

    public static float Native(float zoom) =>
        float.IsFinite(zoom) ? Math.Clamp(zoom, NativeMinimum, Maximum) : DefaultNative;

    // The upper 16 bits also contain north-lock state; never replace them with a stale snapshot.
    public static int NativeParameter(int parameter, float zoom) =>
        (parameter & unchecked((int)0xFFFF0000)) | (int)MathF.Round(Native(zoom) * 100f);
}
