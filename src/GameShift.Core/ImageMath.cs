namespace GameShift.Core;

public static class ImageMath
{
    /// <summary>Размытие окном (2·radius+1), три прохода дают почти гауссово пятно. Края продолжают крайний пиксель, чтобы не было тёмной рамки.</summary>
    public static void BoxBlur(uint[] pixels, int width, int height, int radius, int passes)
    {
        if (width <= 0 || height <= 0 || pixels.Length != width * height)
            throw new ArgumentException("Размер массива не совпадает с размером картинки.", nameof(pixels));
        if (radius <= 0 || passes <= 0) return;

        var buffer = new uint[Math.Max(width, height)];
        for (var p = 0; p < passes; p++)
        {
            for (var y = 0; y < height; y++) Line(pixels, y * width, 1, width, radius, buffer);
            for (var x = 0; x < width; x++) Line(pixels, x, width, height, radius, buffer);
        }
    }

    static void Line(uint[] px, int start, int step, int length, int radius, uint[] buffer)
    {
        int At(int i) => start + Math.Clamp(i, 0, length - 1) * step;
        var window = 2 * radius + 1;
        long r = 0, g = 0, b = 0;
        for (var i = -radius; i <= radius; i++)
        {
            var c = px[At(i)];
            r += (c >> 16) & 0xFF;
            g += (c >> 8) & 0xFF;
            b += c & 0xFF;
        }
        for (var i = 0; i < length; i++)
        {
            buffer[i] = 0xFF000000u
                        | (uint)((r + window / 2) / window) << 16
                        | (uint)((g + window / 2) / window) << 8
                        | (uint)((b + window / 2) / window);
            var outgoing = px[At(i - radius)];
            var incoming = px[At(i + radius + 1)];
            r += (int)((incoming >> 16) & 0xFF) - (int)((outgoing >> 16) & 0xFF);
            g += (int)((incoming >> 8) & 0xFF) - (int)((outgoing >> 8) & 0xFF);
            b += (int)(incoming & 0xFF) - (int)(outgoing & 0xFF);
        }
        for (var i = 0; i < length; i++) px[start + i * step] = buffer[i];
    }
}
