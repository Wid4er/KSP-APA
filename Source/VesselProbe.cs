using System;
using System.Reflection;
using UnityEngine;

namespace AirParkStability
{
    internal static class VesselProbe
    {
        public static bool OneX
        {
            get { return RateIsOneX && Time.timeScale == 1f; }
        }

        public static bool RateIsOneX { get { return TimeWarp.CurrentRateIndex == 0 && TimeWarp.CurrentRate == 1f; } }

        public static bool PhysicsAvailable(Vessel v, bool allowPaused = false)
        {
            return HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && (allowPaused || !FlightDriver.Pause) &&
                v != null && v == FlightGlobals.ActiveVessel && v.loaded &&
                !v.packed && !v.HoldPhysics && v.rootPart != null &&
                v.rootPart.rb != null && !v.rootPart.rb.isKinematic;
        }

        public static StabilitySample Read(Vessel v)
        {
            var body = v.mainBody;
            return new StabilitySample
            {
                // GetSrfVelocity returns this same srf_velocity vector but casts to float.
                SurfaceSpeed = v.srf_velocity.magnitude,
                VerticalSpeed = v.verticalSpeed,
                UniversalTime = Planetarium.GetUniversalTime(),
                PhysicsActive = PhysicsAvailable(v),
                AtOneX = OneX,
                Flying = v.situation == Vessel.Situations.FLYING && !v.Landed && !v.Splashed,
                AtmosphereValid = body != null && body.atmosphere && StabilityLimits.Finite(v.altitude) &&
                    v.altitude >= 0 && v.altitude < body.atmosphereDepth,
                Anchored = IsHLAnchored(v)
            };
        }

        private static bool IsHLAnchored(Vessel v)
        {
            // Optional integration: no reference to HLAirships and no changes to buoyancy.
            foreach (var part in v.parts)
                foreach (PartModule module in part.Modules)
                    if (module != null && module.GetType().FullName == "HLAirships.AnchorModule")
                    {
                        FieldInfo field = module.GetType().GetField("Anchored", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        // An incompatible anchor API cannot prove physical flight.
                        if (field == null || field.FieldType != typeof(bool) || (bool)field.GetValue(module)) return true;
                    }
            return false;
        }

        public static string InvalidReason(StabilitySample sample)
        {
            if (!sample.PhysicsActive) return "Active vessel physics required";
            if (!sample.AtOneX) return "Time warp must be 1x";
            if (sample.Anchored) return "Release the HLAirships anchor first";
            if (!sample.Flying) return "Vessel must be flying";
            if (!sample.AtmosphereValid) return "Vessel must be inside an atmosphere";
            return "Invalid physics sample";
        }
    }
}
