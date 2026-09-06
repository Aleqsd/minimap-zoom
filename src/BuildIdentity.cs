namespace MinimapZoom;

internal static class BuildIdentity
{
    // Read the loaded assembly so the UI cannot report a newer source-file version.
    public static readonly string Version = typeof(BuildIdentity).Assembly.GetName().Version!.ToString(3);
    public static readonly string BuildId = typeof(BuildIdentity).Module.ModuleVersionId.ToString("N")[..8];
    public static readonly string WindowTitle = $"Minimap Zoom v{Version}###MinimapZoom";
}
