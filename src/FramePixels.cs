namespace MinimapZoom;

internal static class FramePixels
{
    // Transparent BGRA texture, fitted to the native map collision rectangle.
    public static byte[] Create(int width, int height, int left, int top, int right, int bottom,
        SquareFrameStyle style, uint color)
    {
        if (width is < 1 or > 1024 || height is < 1 or > 1024 || left < 0 || top < 0 ||
            right > width || bottom > height || right - left < 16 || bottom - top < 16 || !Enum.IsDefined(style))
            throw new NotSupportedException("Dimensions du cadre incompatibles.");
        var pixels = new byte[width * height * 4];
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var dx = Math.Min(x - left, right - 1 - x);
            var dy = Math.Min(y - top, bottom - 1 - y);
            var edge = Math.Min(dx, dy);
            var draw = style switch
            {
                SquareFrameStyle.Thin => edge < 2,
                SquareFrameStyle.Bold => edge < 5,
                SquareFrameStyle.Double => edge < 1 || edge is >= 3 and < 4,
                SquareFrameStyle.Corners => (dx < 3 && dy < 22) || (dy < 3 && dx < 22),
                _ => false,
            };
            if (!draw) continue;
            var offset = (y * width + x) * 4;
            pixels[offset] = (byte)color;
            pixels[offset + 1] = (byte)(color >> 8);
            pixels[offset + 2] = (byte)(color >> 16);
            pixels[offset + 3] = (byte)(color >> 24);
        }
        return pixels;
    }
}
