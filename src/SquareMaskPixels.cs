namespace MinimapZoom;

internal static class SquareMaskPixels
{
    // Mask RGB controls coverage; its alpha remains opaque, as in the original NaviMap mask.
    public static byte[] Create(int width, int height, float left, float top, float right, float bottom)
    {
        if (width is < 1 or > 2048 || height is < 1 or > 2048 ||
            !float.IsFinite(left + top + right + bottom) || left < 0 || top < 0 ||
            right > 1 || bottom > 1 || left >= right || top >= bottom)
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensions du masque carré incompatibles.");
        var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            var inside = (x + 0.5f) / width >= left && (x + 0.5f) / width < right &&
                         (y + 0.5f) / height >= top && (y + 0.5f) / height < bottom;
            pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = inside ? (byte)255 : (byte)0;
            pixels[offset + 3] = 255;
        }
        return pixels;
    }
}
