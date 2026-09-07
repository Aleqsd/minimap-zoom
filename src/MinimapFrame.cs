using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MinimapZoom;

internal sealed unsafe class MinimapFrame(CreateMaskTexture createTexture, ReleaseMaskTexture releaseTexture)
{
    private AtkImageNode* node;
    private AtkUldPartsList* originalParts;
    private ushort originalPartId;
    private AtkUldPartsList* privateParts;
    private Texture* texture;
    private FrameKey key;
    private readonly record struct FrameKey(int Width, int Height, int Left, int Top, int Right, int Bottom,
        SquareFrameStyle Style, uint Color, int Thickness, int CornerLength);

    public void Apply(AtkImageNode* target, AtkCollisionNode* collision, SquareFrameStyle style, uint color,
        int thickness = 0, int cornerLength = 22)
    {
        if (target == null || target->Type != NodeType.Image || collision == null ||
            target->ParentNode != collision->ParentNode || target->Rotation != 0f ||
            target->ScaleX != 1f || target->ScaleY != 1f)
            throw new NotSupportedException("Le cadre de cette mini-carte n'est pas compatible.");
        if (node != null && (node != target || node->PartsList != privateParts))
            throw new InvalidOperationException("Un autre réglage a remplacé le cadre de la mini-carte.");
        var next = new FrameKey(target->Width, target->Height,
            (int)MathF.Round(collision->X - target->X), (int)MathF.Round(collision->Y - target->Y),
            (int)MathF.Round(collision->X + collision->Width - target->X),
            (int)MathF.Round(collision->Y + collision->Height - target->Y), style, color, thickness, cornerLength);
        if (privateParts != null && key == next) return;
        Restore();
        if (target->PartsList == null || target->PartsList->Parts == null ||
            target->PartId >= target->PartsList->PartCount || target->PartsList->Parts[target->PartId].UldAsset == null)
            throw new NotSupportedException("Texture du cadre indisponible.");
        var pixels = FramePixels.Create(next.Width, next.Height, next.Left, next.Top, next.Right, next.Bottom,
            style, color, thickness, cornerLength);
        var created = createTexture(next.Width, next.Height, pixels);
        if (created == null) throw new InvalidOperationException("Création du cadre impossible.");
        byte* allocation;
        try { allocation = (byte*)NativeMemory.AllocZeroed((nuint)(sizeof(AtkUldPartsList) + sizeof(AtkUldPart) + sizeof(AtkUldAsset))); }
        catch { releaseTexture(created); throw; }
        var parts = (AtkUldPartsList*)allocation;
        var part = (AtkUldPart*)(allocation + sizeof(AtkUldPartsList));
        var asset = (AtkUldAsset*)(allocation + sizeof(AtkUldPartsList) + sizeof(AtkUldPart));
        *asset = *target->PartsList->Parts[target->PartId].UldAsset;
        asset->AtkTexture.TextureType = TextureType.KernelTexture;
        asset->AtkTexture.KernelTexture = created;
        part->Width = target->Width;
        part->Height = target->Height;
        part->UldAsset = asset;
        parts->Id = target->PartsList->Id;
        parts->PartCount = 1;
        parts->Parts = part;
        node = target;
        originalParts = target->PartsList;
        originalPartId = target->PartId;
        privateParts = parts;
        texture = created;
        key = next;
        target->PartsList = parts;
        target->PartId = 0;
        target->IsDirty = true;
    }

    public void Restore()
    {
        if (privateParts == null) return;
        if (node->PartsList == privateParts)
        {
            node->PartsList = originalParts;
            node->PartId = originalPartId;
            node->IsDirty = true;
        }
        NativeMemory.Free(privateParts);
        privateParts = null;
        releaseTexture(texture);
        texture = null;
        node = null;
        originalParts = null;
    }
}
