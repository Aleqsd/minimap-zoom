using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.UI;
using MinimapZoom;

internal static unsafe class Program
{
    private static int passed;

    private static void Main(string[] args)
    {
        Check("Installed FFXIVClientStructs matches every accessed minimap field", NativeLayout.Validate);
        Check("Extended input never yields zero, infinity or NaN", () =>
        {
            foreach (var input in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity, -1f, 0f, float.Epsilon, 0.25f, 0.5f, 2f, 999f })
            {
                var output = ZoomPolicy.Extended(input);
                Require(float.IsFinite(output) && output >= 0.25f && output <= 2f, $"Unsafe zoom from {input}");
            }
        });
        Check("Native restore cannot persist an extended zoom", () =>
        {
            Require(ZoomPolicy.Native(0.25f) == 0.5f, "Restore should respect the native minimum");
            Require(ZoomPolicy.Native(float.NaN) == 0.75f, "Invalid restore should use the constructor default");
            Require(ZoomPolicy.Native(0.75f) == 0.75f, "A valid original zoom must be preserved");
        });
        Check("Parameter restoration preserves all upper flags including north lock", () =>
        {
            foreach (var parameter in new[] { 25, 0x10019, unchecked((int)0xFFFF0019), 0xABCD })
            {
                var restored = ZoomPolicy.NativeParameter(parameter, 0.75f);
                Require((restored & unchecked((int)0xFFFF0000)) == (parameter & unchecked((int)0xFFFF0000)), "Upper bits changed");
                Require((restored & 0xFFFF) == 75, "Native zoom not encoded");
            }
        });
        Check("Extended zoom updates both coefficients, 100 markers, then the map", () =>
        {
            using var fixture = new MinimapFixture();
            fixture.Operations.Apply(fixture.Addon, 0.25f, 1, true);
            Require(fixture.Addon->MarkerPositionScaling == 0.25f && fixture.Addon->NaviMap.MarkerPositionScaling == 0.25f, "Coefficients disagree");
            Require(fixture.Calls[0] == "original:1", "Native initialization must run first");
            Require(fixture.MarkerIndices.SequenceEqual(Enumerable.Range(0, 100).Select(i => (uint)i)), "Marker loop differs from native loop");
            Require(fixture.Calls[^2] == "map" && fixture.Calls[^1] == "button:False", "Map must update after all markers and before button state");
        });
        Check("Native minimum still allows reaching the additional zoom-out step", () =>
        {
            using var fixture = new MinimapFixture();
            fixture.Operations.Apply(fixture.Addon, 0.5f, 1, true);
            Require(fixture.Calls.SequenceEqual(new[] { "original:1", "button:True" }), "Native minimum button remains disabled or refresh duplicated");
        });
        Check("Native zoom-in values keep their existing behavior", () =>
        {
            using var fixture = new MinimapFixture();
            fixture.Operations.Apply(fixture.Addon, 1.5f, 1, true);
            Require(fixture.Addon->MarkerPositionScaling == 1.5f, "Native zoom changed");
            Require(fixture.Calls.Count == 2 && fixture.MarkerIndices.Count == 0, "Native-range updates should not duplicate rendering");
        });
        Check("Initialization without refresh never touches marker nodes", () =>
        {
            using var fixture = new MinimapFixture();
            fixture.Operations.Apply(fixture.Addon, 0.25f, 0, true);
            Require(fixture.Calls.SequenceEqual(new[] { "original:0", "button:False" }), "Initialization unexpectedly refreshed nodes");
            Require(fixture.Addon->NaviMap.MarkerPositionScaling == 0.25f, "Initialization missed the internal coefficient");
        });
        Check("Disable restores original zoom using only the native render path", () =>
        {
            using var fixture = new MinimapFixture();
            fixture.Addon->MarkerPositionScaling = 0.25f;
            fixture.Operations.Apply(fixture.Addon, 0.75f, 1, false);
            Require(fixture.Calls.SequenceEqual(new[] { "original:1" }), "Disable changed native button handling");
            Require(fixture.Addon->MarkerPositionScaling == 0.75f && fixture.Addon->NaviMap.MarkerPositionScaling == 0.75f, "Restore did not synchronize coefficients");
        });
        Check("Repeated wheel-down at the new minimum never reaches a zero divisor", () =>
        {
            using var fixture = new MinimapFixture();
            fixture.Operations.Apply(fixture.Addon, 0f, 1, true);
            Require(fixture.Addon->MarkerPositionScaling == 0.25f && fixture.MapZoom == 0.25f, "Wheel overshoot escaped the bound");
        });
        Check("Malformed configuration is normalized before native execution", () =>
        {
            using var fixture = new MinimapFixture();
            fixture.Operations.Apply(fixture.Addon, float.NaN, 1, true);
            Require(float.IsFinite(fixture.OriginalInput) && fixture.OriginalInput == 0.5f, "NaN reached native code");
        });
        Check("Manifest identifies the local plugin and requires synchronous unloading", () =>
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MinimapZoom.json")));
            var root = manifest.RootElement;
            Require(root.GetProperty("InternalName").GetString() == "MinimapZoom", "Manifest name mismatch");
            Require(root.GetProperty("DalamudApiLevel").GetInt32() == 15, "Wrong API level");
            Require(!root.GetProperty("CanUnloadAsync").GetBoolean() && root.GetProperty("LoadSync").GetBoolean(), "Native hooks must have synchronized lifecycle");
            Require(root.GetProperty("Author").GetString() == "Aleqsd", "Wrong author");
        });

        AppearanceChecks.Run(Check);
        FeatureChecks.Run(Check);
        MarkerRangeChecks.Run(Check);

        if (args.Length == 2 && args[0] == "--game-exe") VerifyExecutable(args[1]);
        else if (args.Length != 0) throw new ArgumentException("Usage: --game-exe <ffxiv_dx11.exe>");
        else Console.WriteLine("Binary signature checks omitted: provide --game-exe for read-only static validation.");
        Console.WriteLine($"PASS: {passed} checks. No game process was opened or modified.");
    }

    private static void VerifyExecutable(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Check("Executable hash and version match the analyzed client", () =>
        {
            Require(Convert.ToHexString(SHA256.HashData(bytes)) == NativeContracts.ExecutableSha256, "Unsupported executable hash");
            Require(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "ffxivgame.ver")).Trim() == NativeContracts.GameVersion, "Unsupported version");
        });
        using var pe = new PEReader(new MemoryStream(bytes));
        var text = pe.PEHeaders.SectionHeaders.Single(s => s.Name == ".text");
        foreach (var (name, signature, expectedRva) in new[]
        {
            ("ApplyZoom", NativeContracts.ApplyZoom, 0x157BBF0),
            ("RefreshMarker", NativeContracts.RefreshMarker, 0x157BEF0),
            ("RefreshMap", NativeContracts.RefreshMap, 0x157C320),
            ("UpdateNaviMap", NativeContracts.UpdateNaviMap, 0xFE0460),
            ("CollectMapMarkers", NativeContracts.CollectMapMarkers, 0xFBCBF0),
        })
        {
            Check($"{name} signature is unique and matches the analyzed function", () =>
            {
                var pattern = signature.Split(' ').Select(token => token == "??" ? -1 : Convert.ToInt32(token, 16)).ToArray();
                var matches = new List<int>();
                for (var offset = text.PointerToRawData; offset <= text.PointerToRawData + text.SizeOfRawData - pattern.Length; offset++)
                {
                    if (bytes[offset] != pattern[0]) continue;
                    var match = true;
                    for (var index = 1; index < pattern.Length; index++)
                        if (pattern[index] >= 0 && bytes[offset + index] != pattern[index]) { match = false; break; }
                    if (match) matches.Add(offset - text.PointerToRawData + text.VirtualAddress);
                }
                Require(matches.Count == 1 && matches[0] == expectedRva, $"Unexpected matches: {string.Join(",", matches.Select(m => $"0x{m:X}"))}");
            });
        }
    }

    private static void Check(string name, Action action)
    {
        action();
        passed++;
        Console.WriteLine($"PASS {passed:00}: {name}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class MinimapFixture : IDisposable
    {
        public AddonNaviMap* Addon { get; } = (AddonNaviMap*)NativeMemory.AllocZeroed((nuint)sizeof(AddonNaviMap));
        public List<string> Calls { get; } = [];
        public List<uint> MarkerIndices { get; } = [];
        public float MapZoom { get; private set; }
        public float OriginalInput { get; private set; }
        public NativeZoomOperations Operations { get; }

        public MinimapFixture()
        {
            Operations = new NativeZoomOperations(
                (addon, refresh) =>
                {
                    Calls.Add($"original:{refresh}");
                    OriginalInput = addon->MarkerPositionScaling;
                    addon->MarkerPositionScaling = Math.Clamp(addon->MarkerPositionScaling, 0.5f, 2f);
                    addon->NaviMap.MarkerPositionScaling = addon->MarkerPositionScaling;
                },
                (addon, index, force) =>
                {
                    Require(force == 0 && addon->MarkerPositionScaling == addon->NaviMap.MarkerPositionScaling, "Markers refreshed with inconsistent state");
                    MarkerIndices.Add(index);
                    Calls.Add($"marker:{index}");
                },
                addon => { MapZoom = addon->NaviMap.MarkerPositionScaling; Calls.Add("map"); },
                (addon, enabled) => Calls.Add($"button:{enabled}"));
        }

        public void Dispose() => NativeMemory.Free(Addon);
    }
}
