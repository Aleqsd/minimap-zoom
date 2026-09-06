using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MinimapZoom;

internal static unsafe class NativeLayout
{
    public static void Validate()
    {
        if (sizeof(AddonNaviMap) != 0x3A90 || sizeof(Atk2DNaviMap) != 0x1350 ||
            sizeof(AgentHUD) != 0x4EC8 ||
            Marshal.OffsetOf<AgentHUD>(nameof(AgentHUD.MapMarkers)) != MarkerRangeScope.MarkerVectorOffset ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.NodeFlags)) != 0xAE ||
            Marshal.OffsetOf<AtkComponentNode>(nameof(AtkComponentNode.Component)) != 0xC0 ||
            Marshal.OffsetOf<AtkComponentBase>(nameof(AtkComponentBase.UldManager)) != 0x08 ||
            Marshal.OffsetOf<AtkComponentBase>(nameof(AtkComponentBase.AtkResNode)) != 0xA0 ||
            Marshal.OffsetOf<AtkUldManager>(nameof(AtkUldManager.NodeListCount)) != 0x42 ||
            Marshal.OffsetOf<AtkUldManager>(nameof(AtkUldManager.NodeList)) != 0x50 ||
            Marshal.OffsetOf<AtkUldManager>(nameof(AtkUldManager.RootNode)) != 0x78 ||
            Marshal.OffsetOf<AtkUldManager>(nameof(AtkUldManager.NodeListSize)) != 0x84 ||
            (ushort)NodeFlags.Visible != 0x10 ||
            Marshal.OffsetOf<AddonNaviMap>(nameof(AddonNaviMap.MarkerPositionScaling)) != 0x3A78 ||
            Marshal.OffsetOf<AddonNaviMap>(nameof(AddonNaviMap.NaviMap)) != 0x238 ||
            Marshal.OffsetOf<AddonNaviMap>(nameof(AddonNaviMap.MapBase)) != 0x15D8 ||
            Marshal.OffsetOf<AddonNaviMap>(nameof(AddonNaviMap.MapImage)) != 0x15E0 ||
            Marshal.OffsetOf<AddonNaviMap>(nameof(AddonNaviMap.ZoomOutButton)) != 0x15C8 ||
            Marshal.OffsetOf<AddonNaviMap>(nameof(AddonNaviMap.Mask)) != 0x15F0 ||
            Marshal.OffsetOf<AddonNaviMap>(nameof(AddonNaviMap.Sun)) != 0x15A8 ||
            Marshal.OffsetOf<AddonNaviMap>(nameof(AddonNaviMap.MainCollision)) != 0x1590 ||
            Marshal.OffsetOf<AtkImageNode>(nameof(AtkImageNode.PartsList)) != 0xC0 ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.DrawFlags)) != 0xB0 ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.ScaleX)) != 0x4C ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.ScaleY)) != 0x50 ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.Rotation)) != 0x54 ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.Type)) != 0x40 ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.ParentNode)) != 0x20 ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.X)) != 0x44 ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.Y)) != 0x48 ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.Width)) != 0xA0 ||
            Marshal.OffsetOf<AtkResNode>(nameof(AtkResNode.Height)) != 0xA2 ||
            Marshal.OffsetOf<NaviMapMarker>(nameof(NaviMapMarker.IconId)) != 4 ||
            Marshal.OffsetOf<NaviMapMarker>(nameof(NaviMapMarker.SecondaryIconId)) != 8 ||
            Marshal.OffsetOf<NaviMapMarker>(nameof(NaviMapMarker.ComponentNode)) != 0x10 ||
            Marshal.OffsetOf<NaviMapMarker>(nameof(NaviMapMarker.SubtextOrientation)) != 0 ||
            Marshal.OffsetOf<NaviMapMarker>(nameof(NaviMapMarker.Unknown1C)) != 0x1C ||
            Marshal.OffsetOf<Atk2DMap>(nameof(Atk2DMap.PlayerPin)) != 8 ||
            sizeof(NaviMapMarker) != 0x30 || sizeof(AtkUldPart) != 0x10 || sizeof(AtkUldAsset) != 0x20 ||
            Marshal.OffsetOf<Atk2DMap>(nameof(Atk2DMap.MarkerPositionScaling)) != 0x2C ||
            Marshal.OffsetOf<AtkUnitBase>(nameof(AtkUnitBase.Param)) != 0x1A8)
            throw new NotSupportedException("Les structures Dalamud ne correspondent pas à la mini-carte analysée.");
    }
}
