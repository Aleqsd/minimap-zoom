using Dalamud.Hooking;
using Dalamud.Plugin.Services;

namespace MinimapZoom;

internal sealed class NativeMarkerRange : IDisposable
{
    private delegate void UpdateNaviMap(nint hud, uint flags);
    private delegate void CollectMapMarkers(nint map, nint vector, byte isMinimap, nint position,
        float radius, byte flag6, byte flag7);
    private readonly Hook<UpdateNaviMap> update;
    private readonly Hook<CollectMapMarkers> collect;
    private readonly Func<float> getZoom;
    private readonly Action<Exception> onError;
    [ThreadStatic] private static MarkerRangeScope scope;
    public long ExpansionCount { get; private set; }

    public NativeMarkerRange(ISigScanner scanner, IGameInteropProvider interop, Func<float> getZoom, Action<Exception> onError)
    {
        this.getZoom = getZoom;
        this.onError = onError;
        var updateAddress = scanner.ScanText(NativeContracts.UpdateNaviMap);
        var collectAddress = scanner.ScanText(NativeContracts.CollectMapMarkers);
        update = interop.HookFromAddress<UpdateNaviMap>(updateAddress, OnUpdate);
        try { collect = interop.HookFromAddress<CollectMapMarkers>(collectAddress, OnCollect); }
        catch { update.Dispose(); throw; }
    }

    private void OnUpdate(nint hud, uint flags)
    {
        var zoom = 0.5f;
        try { zoom = getZoom(); }
        catch (Exception exception) { onError(exception); }
        var parent = scope;
        scope = new MarkerRangeScope(hud, zoom);
        try { update.Original(hud, flags); }
        finally { scope.Restore(); scope = parent; }
    }

    private void OnCollect(nint map, nint vector, byte isMinimap, nint position, float radius, byte flag6, byte flag7)
    {
        var expanded = radius;
        try
        {
            expanded = scope.Expand(vector, isMinimap, radius);
            if (expanded != radius) ExpansionCount++;
        }
        catch (Exception exception) { onError(exception); }
        collect.Original(map, vector, isMinimap, position, expanded, flag6, flag7);
    }

    public void Enable()
    {
        collect.Enable();
        try { update.Enable(); }
        catch { collect.Disable(); throw; }
    }

    public void Disable() { update.Disable(); collect.Disable(); }
    public void Dispose() { update.Dispose(); collect.Dispose(); }
}
