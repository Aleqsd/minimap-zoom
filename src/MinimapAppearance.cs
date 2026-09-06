using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MinimapZoom;

internal unsafe delegate Texture* CreateMaskTexture(int width, int height, byte[] pixels);
internal unsafe delegate void ReleaseMaskTexture(Texture* texture);

internal sealed unsafe class MinimapAppearance
{
    private const uint DrawDisabled = 1u << 18;
    private const uint EllipticalCollision = 1u << 23;
    private readonly CreateMaskTexture createTexture;
    private readonly ReleaseMaskTexture releaseTexture;
    private readonly Dictionary<nint, VisibilitySnapshot> hiddenMarkers = [];
    private readonly Dictionary<nint, VisibilitySnapshot> hiddenBorders = [];
    private readonly Dictionary<nint, VisibilitySnapshot> hiddenDecorations = [];
    private readonly Dictionary<nint, ScaleSnapshot> scaledMarkers = [];
    private readonly MinimapFrame frame;
    private readonly record struct ScaleSnapshot(float X, float Y, float AppliedX, float AppliedY);
    private readonly record struct VisibilitySnapshot(bool Visible, bool DrawDisabled);
    private nint owner;
    private AtkImageNode* mask;
    private AtkCollisionNode* collision;
    private AtkUldPartsList* originalParts;
    private ushort originalPartId;
    private AtkUldPartsList* squareParts;
    private Texture* squareTexture;
    private bool originalEllipticalCollision;

    public MinimapAppearance() : this(CreateTexture, texture => texture->DecRef()) { }

    internal MinimapAppearance(CreateMaskTexture createTexture, ReleaseMaskTexture releaseTexture)
    {
        this.createTexture = createTexture;
        this.releaseTexture = releaseTexture;
        frame = new MinimapFrame(createTexture, releaseTexture);
    }

    public bool IsSquare => squareParts != null;
    public bool HasHiddenMarkers => hiddenMarkers.Count != 0;

    public void Apply(AddonNaviMap* addon, bool square, bool hideMarkers) => Apply(addon,
        AppearanceSettings.Default with { Square = square, HideMarkers = hideMarkers, FrameStyle = SquareFrameStyle.None });

    public void Apply(AddonNaviMap* addon, AppearanceSettings settings)
    {
        if (addon == null) return;
        if (owner != 0 && owner != (nint)addon)
            throw new InvalidOperationException("La mini-carte a changé sans notification de fermeture.");
        settings = settings.Normalize();
        if (!settings.HasOverrides)
        {
            Restore(addon);
            return;
        }
        owner = (nint)addon;
        RestoreMarkerOverrides(addon);
        if (settings.HideSunMoon) Hide((AtkResNode*)addon->Sun, hiddenDecorations);
        if (settings.HideWeather) HideControl(FindNode(addon, 14));
        if (settings.HideButtons)
        {
            HideControl(FindNode(addon, 2)); // Zoom in.
            HideControl(FindNode(addon, 3)); // Zoom out.
            HideControl(FindNode(addon, 4)); // Lock north.
        }

        if (settings.Square)
        {
            if (squareParts == null) AttachSquareMask(addon);
            if (mask != addon->Mask || collision != addon->MainCollision || mask->PartsList != squareParts)
                throw new InvalidOperationException("Un autre réglage a remplacé le masque de la mini-carte.");
            collision->DrawFlags &= ~EllipticalCollision;
        }
        else RestoreSquareMask();

        RestoreHidden(hiddenBorders);
        if (settings.Square || settings.HideFrame) Hide(FindNode(addon, 13), hiddenBorders);
        if (settings.Square && !settings.HideFrame && settings.FrameStyle != SquareFrameStyle.None)
        {
            frame.Apply((AtkImageNode*)FindNode(addon, 15), addon->MainCollision, settings.FrameStyle, settings.FrameColor);
        }
        else
        {
            frame.Restore();
            if (settings.Square || settings.HideFrame) Hide(FindNode(addon, 15), hiddenBorders);
        }

        // Slot 100 is the player. Pair each regular icon with its separate edge-arrow group.
        for (var index = 0; index < 100; index++)
        {
            ref var marker = ref addon->NaviMap.NaviMapMarkers[index];
            var node = (AtkResNode*)marker.ComponentNode;
            var edge = (AtkResNode*)GetEdgeMarker(addon, index);
            if (MarkerPolicy.ShouldHide(marker.IconId, marker.SecondaryIconId, settings.HiddenCategories, settings.HideMarkers))
            {
                Hide(node, hiddenMarkers);
                Hide(edge, hiddenMarkers);
                // The renderer flattens component children into its draw list. Hiding only
                // the component does not reliably hide its images, counters and edge arrows.
                for (var child = 0; child < 5; child++)
                {
                    Hide(GetMarkerChild(addon, index, child, false), hiddenMarkers);
                    Hide(GetMarkerChild(addon, index, child, true), hiddenMarkers);
                }
            }
            // Native world-size markers describe areas, not icon sizes. Preserve their footprint.
            var worldSized = (marker.SubtextOrientation & 0xF) == 12 ||
                (marker.Unknown1C != 0f && (marker.SubtextOrientation & 0x1000) == 0);
            if (!worldSized && !MarkerPolicy.IsArea(marker.IconId) && !MarkerPolicy.IsArea(marker.SecondaryIconId))
            {
                Scale(node, settings.MarkerScale);
                Scale(edge, settings.MarkerScale);
            }
        }
        Scale((AtkResNode*)addon->NaviMap.PlayerPin, settings.PlayerScale);
        Scale((AtkResNode*)addon->NaviMap.NaviMapMarkers[100].ComponentNode, settings.PlayerScale);
    }

    // Called before native Update and before each application. Never multiply last frame's override.
    public void RestoreMarkerOverrides(AddonNaviMap* addon)
    {
        if (owner == 0 || owner != (nint)addon) return;
        RestoreHidden(hiddenMarkers);
        RestoreHidden(hiddenDecorations);
        foreach (var (address, snapshot) in scaledMarkers)
        {
            var node = (AtkResNode*)address;
            // If the game already refreshed a node, its newer scale takes precedence.
            if (node->ScaleX == snapshot.AppliedX && node->ScaleY == snapshot.AppliedY)
                SetScale(node, snapshot.X, snapshot.Y);
        }
        scaledMarkers.Clear();
    }

    private void Scale(AtkResNode* node, float multiplier)
    {
        if (node == null || multiplier == 1f || scaledMarkers.ContainsKey((nint)node) ||
            !float.IsFinite(node->ScaleX) || !float.IsFinite(node->ScaleY) || node->ScaleX <= 0 || node->ScaleY <= 0) return;
        var x = node->ScaleX * multiplier;
        var y = node->ScaleY * multiplier;
        if (!float.IsFinite(x) || !float.IsFinite(y)) return;
        scaledMarkers.Add((nint)node, new(node->ScaleX, node->ScaleY, x, y));
        SetScale(node, x, y);
    }

    private static void SetScale(AtkResNode* node, float x, float y)
    {
        node->ScaleX = x;
        node->ScaleY = y;
        // Same transform/dirty flags as this client's AtkResNode.SetScale (RVA 0x6621A0).
        node->DrawFlags |= 1;
        node->DrawFlags = x != 1f || y != 1f || node->Rotation != 0f
            ? node->DrawFlags | 4u : node->DrawFlags & ~4u;
    }

    internal static AtkComponentNode* GetEdgeMarker(AddonNaviMap* addon, int index)
    {
        if ((uint)index >= 100) throw new ArgumentOutOfRangeException(nameof(index));
        // Verified from the supported client's marker refresh routine, see docs/native-mapping.md.
        return *(AtkComponentNode**)((byte*)addon + 0x25C0 + index * 0x30);
    }

    internal static AtkResNode* GetMarkerChild(AddonNaviMap* addon, int index, int child, bool edge)
    {
        if ((uint)index >= 100 || (uint)child >= 5) throw new ArgumentOutOfRangeException();
        // Pointer arrays populated by AddonNaviMap and used by RefreshMarker/marker assignment.
        var offset = edge ? 0x25C8 + index * 0x30 : 0x15F8 + index * 0x28;
        return *(AtkResNode**)((byte*)addon + offset + child * sizeof(nint));
    }

    private static AtkResNode* FindNode(AddonNaviMap* addon, uint nodeId)
    {
        var manager = &addon->UldManager;
        for (var index = 0; index < manager->NodeListCount; index++)
        {
            var node = manager->NodeList[index];
            if (node != null && node->NodeId == nodeId) return node;
        }
        return null;
    }

    private void HideControl(AtkResNode* node)
    {
        if (node == null || hiddenDecorations.ContainsKey((nint)node)) return;
        Hide(node, hiddenDecorations);
        if ((ushort)node->Type < 1000) return;
        var component = ((AtkComponentNode*)node)->Component;
        if (component == null) return;
        var manager = &component->UldManager;
        if (manager->NodeListCount > manager->NodeListSize ||
            (manager->NodeListCount != 0 && manager->NodeList == null))
            throw new NotSupportedException("Les contrôles de cette mini-carte ne sont pas compatibles.");
        // The renderer and hit testing use children directly. Keep component state intact,
        // including enabled/checked flags, and mask both visual and collision nodes.
        HideControl(manager->RootNode);
        HideControl(component->AtkResNode);
        for (var index = 0; index < manager->NodeListCount; index++)
            HideControl(manager->NodeList[index]);
    }

    private static void Hide(AtkResNode* node, Dictionary<nint, VisibilitySnapshot> snapshots)
    {
        if (node == null) return;
        snapshots.TryAdd((nint)node, new((node->NodeFlags & NodeFlags.Visible) != 0,
            (node->DrawFlags & DrawDisabled) != 0));
        node->NodeFlags &= ~NodeFlags.Visible;
        node->DrawFlags |= DrawDisabled | 1u;
    }

    private static void RestoreHidden(Dictionary<nint, VisibilitySnapshot> snapshots)
    {
        foreach (var (address, snapshot) in snapshots)
        {
            var node = (AtkResNode*)address;
            // Respect a newer native visibility change. PreUpdate/PreRequestedUpdate restore
            // our flags before the game reuses a slot or recomputes its distance culling.
            if ((node->NodeFlags & NodeFlags.Visible) == 0 && (node->DrawFlags & DrawDisabled) != 0)
            {
                if (snapshot.Visible) node->NodeFlags |= NodeFlags.Visible;
                if (!snapshot.DrawDisabled) node->DrawFlags &= ~DrawDisabled;
                node->DrawFlags |= 1u;
            }
        }
        snapshots.Clear();
    }

    private void AttachSquareMask(AddonNaviMap* addon)
    {
        var sourceMask = addon->Mask;
        var sourceCollision = addon->MainCollision;
        if (sourceMask == null || sourceCollision == null || sourceMask->PartsList == null ||
            sourceMask->PartId >= sourceMask->PartsList->PartCount || sourceMask->PartsList->Parts == null ||
            sourceMask->Width == 0 || sourceMask->Height == 0 || sourceMask->ParentNode != sourceCollision->ParentNode)
            throw new NotSupportedException("Le masque de cette mini-carte n'est pas compatible.");
        var part = sourceMask->PartsList->Parts[sourceMask->PartId];
        if (part.UldAsset == null) throw new NotSupportedException("Texture du masque indisponible.");
        var atkTexture = &part.UldAsset->AtkTexture;
        if (atkTexture->TextureType is not (TextureType.Resource or TextureType.KernelTexture) ||
            (atkTexture->TextureType == TextureType.KernelTexture && atkTexture->KernelTexture == null) ||
            (atkTexture->TextureType == TextureType.Resource && atkTexture->Resource == null))
            throw new NotSupportedException("Texture du masque indisponible.");
        // Resource textures divide their physical dimensions by the HD scale; kernel textures do not.
        // Match the logical dimensions used by the UI so that the map's existing UVs stay unchanged.
        var width = checked((int)(atkTexture->TextureType == TextureType.KernelTexture
            ? atkTexture->KernelTexture->ActualWidth : atkTexture->GetTextureWidth()));
        var height = checked((int)(atkTexture->TextureType == TextureType.KernelTexture
            ? atkTexture->KernelTexture->ActualHeight : atkTexture->GetTextureHeight()));
        if (part.U != 0 || part.V != 0 || part.Width != width || part.Height != height)
            throw new NotSupportedException("Le masque utilise une texture personnalisée incompatible.");
        // Keep the existing UVs and render-target coordinates. Only the mask's RGB coverage changes.
        var pixels = SquareMaskPixels.Create(width, height,
            (sourceCollision->X - sourceMask->X) / sourceMask->Width,
            (sourceCollision->Y - sourceMask->Y) / sourceMask->Height,
            (sourceCollision->X + sourceCollision->Width - sourceMask->X) / sourceMask->Width,
            (sourceCollision->Y + sourceCollision->Height - sourceMask->Y) / sourceMask->Height);

        var texture = createTexture(width, height, pixels);
        if (texture == null) throw new InvalidOperationException("Création du masque carré impossible.");
        byte* allocation;
        try
        {
            allocation = (byte*)NativeMemory.AllocZeroed((nuint)(sizeof(AtkUldPartsList) + sizeof(AtkUldPart) + sizeof(AtkUldAsset)));
        }
        catch
        {
            releaseTexture(texture);
            throw;
        }
        var parts = (AtkUldPartsList*)allocation;
        var copiedPart = (AtkUldPart*)(allocation + sizeof(AtkUldPartsList));
        var copiedAsset = (AtkUldAsset*)(allocation + sizeof(AtkUldPartsList) + sizeof(AtkUldPart));
        *copiedAsset = *part.UldAsset;
        copiedAsset->AtkTexture.TextureType = TextureType.KernelTexture;
        copiedAsset->AtkTexture.KernelTexture = texture;
        *copiedPart = part;
        copiedPart->UldAsset = copiedAsset;
        parts->Id = sourceMask->PartsList->Id;
        parts->PartCount = 1;
        parts->Parts = copiedPart;

        mask = sourceMask;
        collision = sourceCollision;
        originalParts = mask->PartsList;
        originalPartId = mask->PartId;
        originalEllipticalCollision = (collision->DrawFlags & EllipticalCollision) != 0;
        squareTexture = texture;
        squareParts = parts;
        mask->PartsList = parts;
        mask->PartId = 0;
        mask->IsDirty = true;
    }

    private void RestoreSquareMask()
    {
        if (squareParts == null) return;
        if (mask->PartsList == squareParts)
        {
            mask->PartsList = originalParts;
            mask->PartId = originalPartId;
            mask->IsDirty = true;
        }
        collision->DrawFlags = originalEllipticalCollision
            ? collision->DrawFlags | EllipticalCollision : collision->DrawFlags & ~EllipticalCollision;
        NativeMemory.Free(squareParts);
        squareParts = null;
        var texture = squareTexture;
        squareTexture = null;
        releaseTexture(texture); // Native texture release is deferred by the renderer.
        mask = null;
        collision = null;
        originalParts = null;
    }

    public void Restore(AddonNaviMap* addon)
    {
        if (owner == 0 || owner != (nint)addon) return;
        RestoreMarkerOverrides(addon);
        frame.Restore();
        RestoreHidden(hiddenBorders);
        RestoreSquareMask();
        owner = 0;
    }

    private static Texture* CreateTexture(int width, int height, byte[] pixels)
    {
        var texture = Texture.CreateTexture2D(width, height, 1, TextureFormat.B8G8R8A8_UNORM, TextureFlags.TextureType2D | TextureFlags.Managed, 0);
        if (texture == null) return null;
        fixed (byte* data = pixels)
        {
            if (texture->InitializeContents(data)) return texture;
        }
        texture->DecRef();
        return null;
    }
}
