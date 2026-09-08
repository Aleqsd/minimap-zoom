namespace MinimapZoom;

internal static class NativeContracts
{
    public const string GameVersion = "2026.09.01.0000.0000";
    public const string ExecutableSha256 = "C8CA32A9332924E19EC0FCA158ADC9A783656DD0A89D84168C0604476BB7640B";
    public const string SymbolCommit = "21898bf815f0e56e02b7dc08f0a3e24822c759d0";

    // Verified unique in the supported executable. No absolute process addresses or global float patches.
    public const string ApplyZoom = "40 57 48 83 EC 40 F3 0F 10 81 78 3A 00 00 48 8B F9 0F 29 7C 24 20 F3 0F 10 3D ?? ?? ?? ?? 0F 2F F8";
    public const string RefreshMarker = "40 53 55 56 57 41 54 41 56 41 57 48 83 EC 60 48 8B D9 8B C2 48 8D 3C 40 48 03 FF 48 8D 2C 80";
    public const string RefreshMap = "40 53 48 83 EC 70 48 8B D9 48 81 C1 38 02 00 00 E8 ?? ?? ?? ?? 48 83 BB D8 15 00 00 00";
    public const string UpdateNaviMap = "40 55 56 57 48 8D 6C 24 B0 48 81 EC 50 01 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 00 48 8B F1 89 54 24 68 B1 01";
    public const string CollectMapMarkers = "4C 89 4C 24 20 48 89 4C 24 08 53 55 56 41 55 48 81 EC 48 01 00 00 48 8B 02 49 8B E9 48 89 42 08 41 0F B6 D8";
}
