namespace MinimapZoom;

internal static class FramePixels
{
    public static int Thickness(SquareFrameStyle style, int value) => value > 0 ? Math.Clamp(value, 1, 8) :
        style switch { SquareFrameStyle.Bold => 5, SquareFrameStyle.Double => 1, SquareFrameStyle.Corners => 3, _ => 2 };
    // Transparent BGRA texture, fitted to the native map collision rectangle.
    public static byte[] Create(int width, int height, int left, int top, int right, int bottom,
        SquareFrameStyle style, uint color, int thickness = 0, int cornerLength = 22)
    {
        if (width is < 1 or > 1024 || height is < 1 or > 1024 || left < 0 || top < 0 ||
            right > width || bottom > height || right - left < 16 || bottom - top < 16 || !Enum.IsDefined(style))
            throw new NotSupportedException("Dimensions du cadre incompatibles.");
        var pixels = new byte[width * height * 4];
        var stroke = Thickness(style, thickness);
        var corner = Math.Clamp(cornerLength, 8, Math.Min(64, Math.Min(right - left, bottom - top) / 2));
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var dx = Math.Min(x - left, right - 1 - x);
            var dy = Math.Min(y - top, bottom - 1 - y);
            var edge = Math.Min(dx, dy);
            var draw = style switch
            {
                SquareFrameStyle.Thin or SquareFrameStyle.Bold => edge < stroke,
                SquareFrameStyle.Double => edge < stroke || (edge >= stroke + 2 && edge < stroke * 2 + 2),
                SquareFrameStyle.Corners => (dx < stroke && dy < corner) || (dy < stroke && dx < corner),
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
