using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace MinimapZoom;

internal sealed unsafe class NativeMinimap : IDisposable
{
    private readonly Hook<ApplyZoomDelegate> hook;
    private readonly NativeZoomOperations operations;
    private readonly NativeMarkerRange range;
    public long RangeExpansionCount => range.ExpansionCount;

    public NativeMinimap(ISigScanner scanner, IGameInteropProvider interop, ApplyZoomDelegate detour,
        Func<float> getZoom, Action<Exception> onError)
    {
        ValidateClient(scanner.Module.FileName);
        NativeLayout.Validate();
        var applyAddress = scanner.ScanText(NativeContracts.ApplyZoom);
        var markerAddress = scanner.ScanText(NativeContracts.RefreshMarker);
        var mapAddress = scanner.ScanText(NativeContracts.RefreshMap);
        var refreshMarker = Marshal.GetDelegateForFunctionPointer<RefreshMarkerDelegate>(markerAddress);
        var refreshMap = Marshal.GetDelegateForFunctionPointer<RefreshMapDelegate>(mapAddress);
        hook = interop.HookFromAddress(applyAddress, detour);
        try { range = new NativeMarkerRange(scanner, interop, getZoom, onError); }
        catch { hook.Dispose(); throw; }
        operations = new NativeZoomOperations(hook.Original, refreshMarker, refreshMap, SetZoomOut);
    }

    public static void ValidateClient(string executable)
    {
        var versionPath = Path.Combine(Path.GetDirectoryName(executable)!, "ffxivgame.ver");
        var version = File.ReadAllText(versionPath).Trim();
        if (version != NativeContracts.GameVersion)
            throw new NotSupportedException($"Client {version} : cette version du prototype attend {NativeContracts.GameVersion}.");
        using var stream = File.OpenRead(executable);
        var digest = Convert.ToHexString(SHA256.HashData(stream));
        if (digest != NativeContracts.ExecutableSha256)
            throw new NotSupportedException("L'exécutable diffère de la version analysée. Une nouvelle vérification est nécessaire.");
    }

    public void Enable()
    {
        hook.Enable();
        try { range.Enable(); }
        catch { hook.Disable(); throw; }
    }
    public void Disable() { range.Disable(); hook.Disable(); }
    public void Original(AddonNaviMap* addon, byte refresh) => hook.Original(addon, refresh);

    public static bool IsReady(AddonNaviMap* addon) =>
        addon != null && addon->IsReady && addon->MapBase != null && addon->MapImage != null;

    public void Apply(AddonNaviMap* addon, float zoom, byte refresh, bool extended) =>
        operations.Apply(addon, zoom, refresh, extended);

    private static void SetZoomOut(AddonNaviMap* addon, bool enabled)
    {
        if (addon->ZoomOutButton != null) addon->ZoomOutButton->SetEnabledState(enabled);
    }

    public void Dispose() { range.Dispose(); hook.Dispose(); }
}
