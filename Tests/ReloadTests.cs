using System;
using System.Globalization;
using AirParkStability;
using N = System.Numerics;
using UnityEngine;

internal static class ReloadTests
{
    private static int passed;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Run(string name, Action test) { HighLogic.LoadedSceneIsFlight = FlightGlobals.ready = true; test(); passed++; Console.WriteLine("PASS " + name); }
    private static bool Near(Vector3d a, Vector3d b) { return Math.Abs(a.x - b.x) < 0.2 && Math.Abs(a.y - b.y) < 0.2 && Math.Abs(a.z - b.z) < 0.2; }
    private static PartModule Loaded()
    {
        var body = new CelestialBody();
        var module = new PartModule { vessel = new Vessel { mainBody = body } };
        // A correctly reconstructed KSP vessel with AirPark's loaded world vector still zero.
        module.vessel.vesselTransform.position = body.GetWorldSurfacePosition(-0.172438, -74.421032, 16099.844932);
        return module;
    }
    private static void OriginalFreeze(PartModule module)
    {
        module.vessel.vesselTransform.position = module.ParkPosition;
        module.vessel.vesselTransform.rotation = module.ParkRotation;
    }

    public static void Main()
    {
        Run("Reproduces legacy zero teleport; reloaded pose keeps saved altitude", () => {
            var broken = Loaded(); var expected = (Vector3d)broken.vessel.vesselTransform.position;
            OriginalFreeze(broken); Check(Near(broken.vessel.vesselTransform.position, new Vector3d()), "Baseline not reproduced");
            var fixedModule = Loaded();
            Check(ParkedPose.Prepare(fixedModule, true), "Loaded pose not ready");
            OriginalFreeze(fixedModule); Check(Near(fixedModule.vessel.vesselTransform.position, expected), "Reload teleported to stale/zero coordinates");
            double lat, lon, alt; fixedModule.vessel.mainBody.GetLatLonAlt(fixedModule.ParkPosition, out lat, out lon, out alt);
            Check(Math.Abs(alt - 16099.844932) < 0.2, "Lost saved altitude");
        });
        Run("Packed, physics-held and incomplete load never permit original freeze", () => {
            var m = Loaded(); m.vessel.packed = true;
            Check(!ParkedPose.Prepare(m, true), "Packed freeze allowed");
            m.vessel.packed = false; m.vessel.HoldPhysics = true;
            Check(!ParkedPose.Prepare(m, true), "Physics hold freeze allowed");
            m.vessel.HoldPhysics = false; FlightGlobals.ready = false;
            Check(!ParkedPose.Prepare(m, true), "Premature freeze allowed");
            FlightGlobals.ready = true; Check(ParkedPose.Prepare(m, true), "Ready vessel could not resume maintenance");
        });
        Run("Floating-origin shift preserves surface position", () => {
            var m = Loaded(); var body = m.vessel.mainBody;
            Check(ParkedPose.Prepare(m, true), "Initial anchor failed");
            var old = m.ParkPosition;
            body.Position = new Vector3d(500000, -200000, 750000);
            Check(ParkedPose.Prepare(m, true), "Shifted anchor failed");
            Check(Near(m.ParkPosition, new Vector3d(old.x + 500000, old.y - 200000, old.z + 750000)), "Reused previous world's position");
        });
        Run("Body rotation preserves location and surface-relative orientation", () => {
            var m = Loaded(); var body = m.vessel.mainBody;
            var initial = new Quaternion { Value = N.Quaternion.CreateFromYawPitchRoll(0.2f, -0.3f, 0.6f) };
            m.vessel.vesselTransform.rotation = initial;
            Check(ParkedPose.Prepare(m, true), "Initial anchor failed");
            body.bodyTransform.rotation = new Quaternion { Value = N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitY, 0.75f) };
            Check(ParkedPose.Prepare(m, true), "Rotation maintenance failed");
            Check(Near(m.ParkPosition, body.GetWorldSurfacePosition(-0.172438, -74.421032, 16099.844932)), "Position did not rotate with body");
            var expected = (body.bodyTransform.rotation * initial).Value;
            Check(Math.Abs(N.Quaternion.Dot(m.ParkRotation.Value, expected)) > 0.99999, "Orientation did not rotate with body");
        });
        Run("Rails reload discards old scene anchor", () => {
            var m = Loaded(); Check(ParkedPose.Prepare(m, true), "Initial anchor failed");
            m.vessel.packed = true; Check(!ParkedPose.Prepare(m, true), "Rails should block");
            m.vessel.packed = false;
            var desired = m.vessel.mainBody.GetWorldSurfacePosition(35, 120, 4000);
            m.vessel.vesselTransform.position = desired;
            Check(ParkedPose.Prepare(m, true) && Near(m.ParkPosition, desired), "Kept anchor from prior reconstructed vessel");
        });
        Run("Fresh PARK snapshot untouched; confirmed UNPARK uses current anchored pose", () => {
            var m = Loaded(); var snapshot = new Vector3d(12, -45, 78); m.ParkPosition = snapshot;
            Check(ParkedPose.Prepare(m, false) && Near(m.ParkPosition, snapshot), "Changed fresh original snapshot");
            Check(ParkedPose.Prepare(m, true), "Post-PARK anchor failed");
            var old = m.ParkPosition; m.vessel.mainBody.Position = new Vector3d(1000, 0, 0);
            Check(ParkedPose.Prepare(m, false) && Near(m.ParkPosition, new Vector3d(old.x + 1000, old.y, old.z)), "Confirmed restore reused a stale pose after flag cleared");
            ParkedPose.Forget(m); m.ParkPosition = snapshot;
            Check(ParkedPose.Prepare(m, false) && Near(m.ParkPosition, snapshot), "Forgotten anchor leaked into next operation");
        });
        Run("Vessels never share anchors", () => {
            var a = Loaded(); var b = Loaded();
            b.vessel.vesselTransform.position = b.vessel.mainBody.GetWorldSurfacePosition(10, 45, 1000);
            Check(ParkedPose.Prepare(a, true) && ParkedPose.Prepare(b, true), "Independent anchors failed");
            Check(!Near(a.ParkPosition, b.ParkPosition), "Anchor transferred between vessels");
        });
        Run("Saved Vector3d values round-trip independently of current culture", () => {
            var culture = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");
                const double px = -32.9238479281234, py = -58.3493847928, pz = 134.3449283747;
                double x, y, z;
                Check(VectorPersistence.TryRead(VectorPersistence.Write(px, py, pz), out x, out y, out z) && x == px && y == py && z == pz, "Precision/culture lost");
                Check(VectorPersistence.TryRead("[-32.9, -58.3, 134.3]", out x, out y, out z) && x == -32.9 && y == -58.3 && z == 134.3, "Legacy brackets rejected");
                Check(VectorPersistence.TryRead("(1.2E-6, 2, -3)", out x, out y, out z), "Legacy parentheses rejected");
                foreach (var value in new[] { "NaN,0,0", "1,Infinity,0", "1,2", "garbage" })
                    Check(!VectorPersistence.TryRead(value, out x, out y, out z), "Invalid vector accepted");
            } finally { CultureInfo.CurrentCulture = culture; }
        });
        Console.WriteLine(passed + " reload regression groups passed (simulated KSP/Unity coordinates).");
    }
}
