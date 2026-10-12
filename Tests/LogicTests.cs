using System;
using AirParkStability;

internal static class LogicTests
{
    private static int passed;
    private static StabilitySample Stable(double time) { return new StabilitySample {
        PhysicsActive = true, AtOneX = true, Flying = true, AtmosphereValid = true,
        SurfaceSpeed = 0.82, VerticalSpeed = -0.12, UniversalTime = time }; }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static StabilityMonitor Monitor() { return new StabilityMonitor(new StabilityLimits()); }
    private static void Prime(StabilityMonitor m) { for (int i = 0; i <= 50; i++) m.Step(Stable(i * 0.1), 0.15); }

    public static int Main()
    {
        Run("Ten seconds consecutive, no instant parking", () => {
            var m = Monitor();
            for (int i = 0; i < 100; i++) Assert(m.Step(Stable(i * 0.1), 0.15) != CheckResult.Complete, "Premature completion");
            Assert(m.Step(Stable(10), 0.15) == CheckResult.Complete, "Did not complete");
        });
        Run("Repeated unstable intervals never accumulate", () => {
            var m = Monitor();
            for (int cycle = 0; cycle < 8; cycle++) {
                double start = cycle * 5.2;
                for (int i = 0; i <= 50; i++) Assert(m.Step(Stable(start + i * 0.1), 0.15) != CheckResult.Complete, "Accumulated earlier interval");
                var s = Stable(start + 5.1); s.SurfaceSpeed = 2;
                Assert(m.Step(s, 0.15) == CheckResult.Unstable, "Failed to reset cycle");
            }
        });
        Run("Both limits are strict and reset previous progress", () => {
            foreach (var pair in new[] { (1.5, 0.0), (1.51, 0.0), (0.9, 0.5), (0.9, -0.5), (0.9, -0.51) }) {
                var m = Monitor(); Prime(m); var s = Stable(5.1); s.SurfaceSpeed = pair.Item1; s.VerticalSpeed = pair.Item2;
                Assert(m.Step(s, 0.15) == CheckResult.Unstable && m.StableSeconds == 0, "No reset at limit");
                Assert(m.Step(Stable(5.2), 0.15) == CheckResult.Checking && m.StableSeconds == 0, "Accumulated failed interval");
            }
        });
        Run("Invalid flight, atmosphere, anchor, physics, warp each reset", () => {
            for (int field = 0; field < 5; field++) {
                var m = Monitor(); Prime(m); var s = Stable(5.1);
                if (field == 0) s.Flying = false;
                if (field == 1) s.AtmosphereValid = false;
                if (field == 2) s.Anchored = true;
                if (field == 3) s.PhysicsActive = false;
                if (field == 4) s.AtOneX = false;
                Assert(m.Step(s, 0.15) == CheckResult.Invalid && m.StableSeconds == 0, "Invalid sample retained time");
            }
        });
        Run("Pause continuity reset, no time gained on resume", () => {
            var m = Monitor(); Prime(m); m.Reset();
            m.Step(Stable(100), 0.15); Assert(m.StableSeconds == 0, "Counted pause");
        });
        Run("UT jump, rewind, duplicate sample cannot complete", () => {
            foreach (double time in new[] { 100.0, 4.0 }) {
                var m = Monitor(); Prime(m); m.Step(Stable(time), 0.15);
                Assert(m.StableSeconds == 0, "Clock discontinuity not reset");
                m.Step(Stable(time), 0.15); Assert(m.StableSeconds == 0, "Duplicate advanced clock");
            }
        });
        Run("Nonfinite speeds and time never authorize", () => {
            foreach (double n in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                for (int field = 0; field < 3; field++) {
                    var m = Monitor(); Prime(m); var s = Stable(5.1);
                    if (field == 0) s.SurfaceSpeed = n; if (field == 1) s.VerticalSpeed = n; if (field == 2) s.UniversalTime = n;
                    Assert(m.Step(s, 0.15) != CheckResult.Complete && m.StableSeconds == 0, "Accepted nonfinite sample");
                }
        });
        Run("Configured limits and duration apply", () => {
            var m = new StabilityMonitor(new StabilityLimits(2, 1, 2));
            for (int i = 0; i <= 20; i++) { var s = Stable(i * 0.1); s.SurfaceSpeed = 1.8; s.VerticalSpeed = -0.8; m.Step(s, 0.15); }
            Assert(m.StableSeconds >= 2 && m.Progress == 1, "Ignored custom limits");
            foreach (double n in new[] { 0.0, -1, double.NaN, double.PositiveInfinity }) {
                bool threw = false; try { new StabilityLimits(n, 1, 1); } catch (ArgumentOutOfRangeException) { threw = true; }
                Assert(threw, "Invalid config accepted");
            }
        });
        Run("Permits bind module, operation and method and expire immediately", () => {
            var p = new OperationPermit(); var a = new object(); var b = new object();
            Assert(!p.ConsumeToggle(a) && !p.ConsumeTransition(a, OriginalOperation.Park) && !p.AllowsStill(a), "Unscoped authorization");
            p.Invoke(a, OriginalOperation.Park, () => {
                Assert(!p.ConsumeToggle(b) && !p.ConsumeTransition(b, OriginalOperation.Park), "Other module bypass");
                Assert(p.ConsumeToggle(a) && !p.ConsumeToggle(a), "Toggle not one use");
                Assert(!p.ConsumeTransition(a, OriginalOperation.Unpark), "Wrong transition authorized");
                Assert(p.ConsumeTransition(a, OriginalOperation.Park) && !p.ConsumeTransition(a, OriginalOperation.Park), "Park not one use");
                Assert(p.AllowsStill(a) && !p.AllowsStill(b), "Incorrect still permission");
            });
            Assert(!p.AllowsStill(a) && !p.ConsumeTransition(a, OriginalOperation.Park), "Permit leaked");
        });
        Run("Unpark does not authorize park or toggle", () => {
            var p = new OperationPermit(); var a = new object();
            p.Invoke(a, OriginalOperation.Unpark, () => {
                Assert(!p.ConsumeToggle(a) && !p.ConsumeTransition(a, OriginalOperation.Park), "Unpark became park bypass");
                Assert(p.ConsumeTransition(a, OriginalOperation.Unpark), "Unpark not authorized");
            });
        });
        Run("Exception and recursion never leak permits", () => {
            var p = new OperationPermit(); var a = new object();
            try { p.Invoke(a, OriginalOperation.Park, () => { throw new Exception("Expected"); }); } catch { }
            Assert(!p.ConsumeToggle(a) && !p.AllowsStill(a), "Exception leaked permission");
            p.Invoke(a, OriginalOperation.Park, () => {
                bool rejected = false; try { p.Invoke(a, OriginalOperation.Park, () => {}); } catch (InvalidOperationException) { rejected = true; }
                Assert(rejected && p.ConsumeToggle(a), "Nested call damaged permit");
            });
        });
        Run("Confirmation pauses immediately and cancel resumes previous running state", () => {
            bool paused = false; var dialog = new ConfirmationPause(() => paused, p => paused = p);
            dialog.Begin(); Assert(paused, "Dialog did not pause");
            dialog.Close(); Assert(!paused, "Cancel left game paused");
        });
        Run("Cancel preserves a pre-existing pause; confirmation explicitly resumes", () => {
            bool paused = true; var dialog = new ConfirmationPause(() => paused, p => paused = p);
            dialog.Begin(); dialog.Close(); Assert(paused, "Cancel removed an existing pause");
            dialog.Begin(); dialog.Close(resumeForUnpark: true); Assert(!paused, "Confirm cannot resume original physics");
        });
        Run("Duplicate requests retain original pause ownership and close is idempotent", () => {
            bool paused = false; int changes = 0; var dialog = new ConfirmationPause(() => paused, p => { paused = p; changes++; });
            dialog.Begin(); dialog.Begin(); dialog.Close(); dialog.Close();
            Assert(!paused && changes == 2, "Duplicate request leaked or repeated pause");
        });
        Run("Dialog maintains pause until dismissed and releases it on scene cleanup", () => {
            bool paused = false; var dialog = new ConfirmationPause(() => paused, p => paused = p);
            dialog.Begin(); paused = false; dialog.Maintain(); Assert(paused, "Open dialog lost pause");
            dialog.Close(); dialog.Maintain(); Assert(!paused, "Closed scene dialog reacquired pause");
        });
        Run("Unpause callbacks cannot re-pause a confirmed dialog", () => {
            bool paused = false; ConfirmationPause dialog = null;
            dialog = new ConfirmationPause(() => paused, p => { paused = p; if (!p) dialog.Maintain(); });
            dialog.Begin(); dialog.Close(resumeForUnpark: true);
            Assert(!paused, "Unpause callback reacquired pause before restore");
        });
        Console.WriteLine(passed + " test groups passed."); return 0;
    }
}
