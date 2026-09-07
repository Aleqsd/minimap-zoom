namespace MinimapZoom;

[Serializable]
public sealed record ZoomShortcut
{
    public bool Enabled { get; set; }
    public int Key { get; set; } = 0x75; // F6.
    public bool Control { get; set; } = true;
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public float Zoom { get; set; } = ZoomPolicy.Minimum;
    public ZoomShortcut Normalize() => this with { Key = IsSupported(Key) ? Key : 0x75, Zoom = ZoomPolicy.Extended(Zoom) };
    public static bool IsSupported(int key) => key is >= 0x70 and <= 0x7B or >= 0x41 and <= 0x5A;
    public static string KeyName(int key) => key is >= 0x70 and <= 0x7B ? $"F{key - 0x6F}" : ((char)key).ToString();
    public string Label => $"{(Control ? "Ctrl + " : "")}{(Alt ? "Alt + " : "")}{(Shift ? "Maj + " : "")}{KeyName(Key)}";
}

internal enum HoldTransition { None, Start, End }

// A fresh key press is required after focus loss, typing, a zone change or a settings edit.
// The temporary target is never persisted. The caller restores the captured enabled state.
internal sealed class TemporaryZoom
{
    private bool armed;
    public bool Active { get; private set; }
    public bool WasEnabled { get; private set; }
    public float Target { get; private set; }

    public HoldTransition Update(bool down, bool allowed, bool enabled, float zoom, float target)
    {
        if (!allowed || !down)
        {
            armed = allowed && !down;
            if (!Active) return HoldTransition.None;
            Active = false; return HoldTransition.End;
        }
        if (!armed || Active) return HoldTransition.None;
        armed = false; Active = true; WasEnabled = enabled;
        Target = Math.Min(ZoomPolicy.Extended(zoom), ZoomPolicy.Extended(target));
        return HoldTransition.Start;
    }

    public bool Cancel() { var active = Active; Active = false; armed = false; return active; }
}
