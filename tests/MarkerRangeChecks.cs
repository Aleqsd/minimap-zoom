using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using MinimapZoom;

internal static unsafe class MarkerRangeChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("Extended zoom admits a fixed icon rejected by native distance culling", () =>
        {
            using var fixture = new Fixture();
            var scope = new MarkerRangeScope(fixture.Hud, 0.25f);
            const float distance = 280f;
            Require(distance * distance > *fixture.Squared, "Fixture is already within native range");
            var eventRadius = scope.Expand(fixture.Vector, 1, 200f);
            Require(distance * distance <= *fixture.Squared && *fixture.Radius == 350f && eventRadius == 400f,
                "Zoom did not extend static and event marker distances");
            Require(*(uint*)(fixture.Hud + 0x4DC8) == 100, "Native marker capacity changed");
            scope.Restore();
            Require(*fixture.Radius == 175f && *fixture.Squared == 175f * 175f, "Native distances were not restored");
        });
        check("Range expansion preserves map scaling and never compounds across updates", () =>
        {
            using var fixture = new Fixture();
            for (var i = 0; i < 200; i++)
            {
                *fixture.Radius = 87.5f; // Native 175 / map scale 2.
                *fixture.Squared = 87.5f * 87.5f;
                var scope = new MarkerRangeScope(fixture.Hud, 0.25f);
                scope.Expand(fixture.Vector, 1, 200f);
                scope.Expand(fixture.Vector, 1, 200f);
                Require(*fixture.Radius == 175f && *fixture.Squared == 175f * 175f, "Repeated call compounded scale");
                scope.Restore();
                scope.Restore();
                Require(*fixture.Radius == 87.5f && *fixture.Squared == 87.5f * 87.5f, "Map scale lost");
            }
        });
        check("World map, unrelated vectors, native zoom and invalid inputs retain native ranges", () =>
        {
            using var fixture = new Fixture();
            foreach (var zoom in new[] { 0.5f, 0.75f, 2f, float.NaN, float.PositiveInfinity, 0f, -1f })
            {
                var scope = new MarkerRangeScope(fixture.Hud, zoom);
                Require(scope.Expand(fixture.Vector, 1, 200f) == 200f && *fixture.Radius == 175f, "Native/invalid zoom changed range");
            }
            var extended = new MarkerRangeScope(fixture.Hud, 0.25f);
            Require(extended.Expand(fixture.Vector, 0, 200f) == 200f, "World map changed");
            Require(extended.Expand(fixture.Vector + 24, 1, 200f) == 200f, "Unrelated vector changed");
            *fixture.Radius = float.NaN;
            Require(extended.Expand(fixture.Vector, 1, 200f) == 200f && float.IsNaN(*fixture.Radius), "Invalid native radius overwritten");
            Require(MarkerRangeScope.Multiplier(float.Epsilon) == 2f, "Multiplier exceeded the supported bound");
        });
        check("Range scope restores on exceptional exit and preserves newer native values", () =>
        {
            using var fixture = new Fixture();
            var scope = new MarkerRangeScope(fixture.Hud, 0.25f);
            try
            {
                try { scope.Expand(fixture.Vector, 1, 200f); throw new InvalidOperationException("Simulated exit"); }
                finally { scope.Restore(); }
            }
            catch (InvalidOperationException) { }
            Require(*fixture.Radius == 175f && *fixture.Squared == 30625f, "Exceptional exit leaked expanded radius");
            scope = new MarkerRangeScope(fixture.Hud, 0.25f);
            scope.Expand(fixture.Vector, 1, 200f);
            *fixture.Radius = 120f;
            *fixture.Squared = 14400f;
            scope.Restore();
            Require(*fixture.Radius == 120f && *fixture.Squared == 14400f, "Newer native values overwritten");
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        public nint Hud { get; } = (nint)NativeMemory.AllocZeroed((nuint)sizeof(AgentHUD));
        public nint Vector => Hud + MarkerRangeScope.MarkerVectorOffset;
        public float* Radius => (float*)(Hud + MarkerRangeScope.RadiusOffset);
        public float* Squared => (float*)(Hud + MarkerRangeScope.SquaredRadiusOffset);
        public Fixture()
        {
            *Radius = 175f;
            *Squared = 175f * 175f;
            *(uint*)(Hud + 0x4DC8) = 100;
        }
        public void Dispose() => NativeMemory.Free((void*)Hud);
    }
}
