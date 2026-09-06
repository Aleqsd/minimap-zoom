using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace MinimapZoom;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void ApplyZoomDelegate(AddonNaviMap* addon, byte refresh);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void RefreshMarkerDelegate(AddonNaviMap* addon, uint index, byte force);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void RefreshMapDelegate(AddonNaviMap* addon);

internal unsafe delegate void SetZoomOutDelegate(AddonNaviMap* addon, bool enabled);

// Native calls are injected so the actual update sequence can be checked on an isolated memory fixture.
internal sealed unsafe class NativeZoomOperations(
    ApplyZoomDelegate original,
    RefreshMarkerDelegate refreshMarker,
    RefreshMapDelegate refreshMap,
    SetZoomOutDelegate setZoomOut)
{
    public void Apply(AddonNaviMap* addon, float zoom, byte refresh, bool extended)
    {
        zoom = extended ? ZoomPolicy.Extended(zoom) : ZoomPolicy.Native(zoom);
        addon->MarkerPositionScaling = zoom;
        // Retain the original implementation for its native-range behavior and initialization.
        original(addon, refresh);
        if (extended && zoom < ZoomPolicy.NativeMinimum)
        {
            addon->MarkerPositionScaling = zoom;
            addon->NaviMap.MarkerPositionScaling = zoom;
            if (refresh != 0)
            {
                // Matches the original loop: 100 regular markers, then the map/player transforms.
                for (uint index = 0; index < 100; index++) refreshMarker(addon, index, 0);
                refreshMap(addon);
            }
        }

        // Native code disables zoom-out at 0.5; it must stay enabled to reach the extra step.
        if (extended) setZoomOut(addon, zoom > ZoomPolicy.Minimum);
    }
}
