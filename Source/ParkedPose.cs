using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AirParkStability
{
    // Only prepares AirPark's own pose fields; the original still performs the freeze/restore.
    // No saved Unity world coordinate is ever reused as an anchor in a new scene.
    internal static class ParkedPose
    {
        private sealed class Anchor
        {
            public CelestialBody Body;
            public double Latitude, Longitude, Altitude;
            public Quaternion SurfaceRotation;
        }

        private static readonly ConditionalWeakTable<PartModule, Anchor> anchors = new ConditionalWeakTable<PartModule, Anchor>();

        public static void Forget(PartModule module) { if (!ReferenceEquals(module, null)) anchors.Remove(module); }

        public static bool Prepare(PartModule module, bool parked)
        {
            var vessel = module.vessel;
            if (!HighLogic.LoadedSceneIsFlight || vessel == null || !vessel.loaded || vessel.packed ||
                !FlightGlobals.ready || vessel.HoldPhysics || vessel.mainBody == null ||
                vessel.vesselTransform == null || vessel.mainBody.bodyTransform == null)
            {
                Forget(module);
                return false;
            }
            Anchor anchor;
            if (!anchors.TryGetValue(module, out anchor))
            {
                // Fresh PARK's first setVesselStill uses the snapshot just taken by TogglePark.
                // Capture an anchor only once the original has actually set Parked=true.
                if (!parked) return true;
                var body = vessel.mainBody;
                double latitude, longitude, altitude;
                body.GetLatLonAlt(vessel.vesselTransform.position, out latitude, out longitude, out altitude);
                if (!StabilityLimits.Finite(latitude) || !StabilityLimits.Finite(longitude) || !StabilityLimits.Finite(altitude))
                    throw new InvalidOperationException("Invalid reconstructed KSP parking coordinates.");
                anchor = new Anchor
                {
                    Body = body, Latitude = latitude, Longitude = longitude, Altitude = altitude,
                    SurfaceRotation = Quaternion.Inverse(body.bodyTransform.rotation) * vessel.vesselTransform.rotation
                };
                anchors.Add(module, anchor);
                Debug.Log("[AirParkStability] Parking anchor reconstructed: vessel=" + vessel.vesselName +
                    " body=" + body.bodyName + " lat=" + latitude + " lon=" + longitude + " alt=" + altitude);
            }
            if (vessel.mainBody != anchor.Body)
                throw new InvalidOperationException("Parked vessel changed celestial body.");
            // Recompute in the current world frame before every original freeze, including confirmed UNPARK.
            // Floating-origin shifts and body rotation cannot make yesterday's world coordinates valid.
            AirParkBridge.SetParkedPose(module,
                anchor.Body.GetWorldSurfacePosition(anchor.Latitude, anchor.Longitude, anchor.Altitude),
                anchor.Body.bodyTransform.rotation * anchor.SurfaceRotation);
            return true;
        }
    }
}
