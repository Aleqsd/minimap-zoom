using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace MinimapZoom;

internal sealed class SettingsTheme : IDisposable
{
    private int colors;
    private int variables;
    public static float Scale => ImGui.GetFontSize() / 17f;

    public SettingsTheme()
    {
        var scale = Scale;
        Var(ImGuiStyleVar.WindowPadding, new Vector2(16 * scale));
        Var(ImGuiStyleVar.FramePadding, new Vector2(8, 5) * scale);
        Var(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8) * scale);
        Var(ImGuiStyleVar.CellPadding, new Vector2(4, 2) * scale);
        Var(ImGuiStyleVar.WindowRounding, 3 * scale);
        Var(ImGuiStyleVar.FrameRounding, 2 * scale);
        var bg = new Vector4(0x11 / 255f, 0x11 / 255f, 0x11 / 255f, 0.95f);
        var accent = Color(0xFF00B9F7);
        var surface = Color(0xFF292929);
        var hover = Vector4.Lerp(surface, accent, 0.20f); hover.W = 1;
        Push(ImGuiCol.WindowBg, bg);
        Push(ImGuiCol.Text, Vector4.One);
        Push(ImGuiCol.TextDisabled, new Vector4(0.72f, 0.72f, 0.72f, 1));
        foreach (var color in new[] { ImGuiCol.TitleBg, ImGuiCol.TitleBgActive, ImGuiCol.Tab, ImGuiCol.FrameBg,
            ImGuiCol.Button, ImGuiCol.Header }) Push(color, surface);
        foreach (var color in new[] { ImGuiCol.TabHovered, ImGuiCol.FrameBgHovered, ImGuiCol.ButtonHovered,
            ImGuiCol.HeaderHovered }) Push(color, hover);
        foreach (var color in new[] { ImGuiCol.TabActive, ImGuiCol.FrameBgActive, ImGuiCol.ButtonActive,
            ImGuiCol.HeaderActive }) Push(color, Vector4.Lerp(surface, accent, 0.35f));
        foreach (var color in new[] { ImGuiCol.CheckMark, ImGuiCol.SliderGrab, ImGuiCol.SliderGrabActive }) Push(color, accent);
        Push(ImGuiCol.Separator, new Vector4(0.23f, 0.23f, 0.23f, 1));
        Push(ImGuiCol.Border, new Vector4(0.23f, 0.23f, 0.23f, 0.7f));
    }

    public static Vector4 Color(uint argb) => new((byte)(argb >> 16) / 255f, (byte)(argb >> 8) / 255f,
        (byte)argb / 255f, (byte)(argb >> 24) / 255f);
    public static uint Pack(Vector4 c)
    {
        static uint Channel(float v) => (uint)MathF.Round(Math.Clamp(v, 0, 1) * 255);
        return Channel(c.W) << 24 | Channel(c.X) << 16 | Channel(c.Y) << 8 | Channel(c.Z);
    }

    public static void Label(string text)
    {
        var size = ImGui.CalcTextSize(text);
        var position = ImGui.GetCursorScreenPos();
        position += new Vector2(4) * Scale;
        var draw = ImGui.GetWindowDrawList();
        draw.AddText(position, ImGui.GetColorU32(ImGuiCol.Text), text);
        ImGui.Dummy(new Vector2(size.X + 8 * Scale, size.Y + 8 * Scale));
    }

    private void Push(ImGuiCol key, Vector4 color) { ImGui.PushStyleColor(key, color); colors++; }
    private void Var(ImGuiStyleVar key, Vector2 value) { ImGui.PushStyleVar(key, value); variables++; }
    private void Var(ImGuiStyleVar key, float value) { ImGui.PushStyleVar(key, value); variables++; }
    public void Dispose()
    {
        if (colors > 0) ImGui.PopStyleColor(colors);
        if (variables > 0) ImGui.PopStyleVar(variables);
        colors = variables = 0;
    }
}
