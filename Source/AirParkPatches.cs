using System;
using System.Reflection;
using UnityEngine;

namespace AirParkStability
{
    internal static class AirParkPatches
    {
        public static bool TogglePrefix(PartModule __instance)
        {
            try
            {
                if (AirParkBridge.Permit.ConsumeToggle(__instance)) return true;
                if (AirParkStabilityAddon.Instance != null) AirParkStabilityAddon.Instance.Request(__instance);
            }
            catch (Exception ex) { AirParkBridge.Fault(ex); }
            return false;
        }

        public static bool ParkPrefix(PartModule __instance)
        {
            try
            {
                if (AirParkBridge.IsParked(__instance)) return true; // Existing/saved parking maintenance.
                return AirParkBridge.Permit.ConsumeTransition(__instance, OriginalOperation.Park);
            }
            catch (Exception ex) { AirParkBridge.Fault(ex); return false; }
        }

        public static bool RestorePrefix(PartModule __instance)
        {
            try
            {
                if (AirParkBridge.Permit.ConsumeTransition(__instance, OriginalOperation.Unpark)) return true;
                if (AirParkBridge.IsParked(__instance) && AirParkStabilityAddon.Instance != null)
                    AirParkStabilityAddon.Instance.RequestUnpark(__instance);
            }
            catch (Exception ex) { AirParkBridge.Fault(ex); }
            return false;
        }

        public static bool StillPrefix(PartModule __instance)
        {
            try
            {
                bool parked = AirParkBridge.IsParked(__instance);
                if (!parked && !AirParkBridge.Permit.AllowsStill(__instance)) return false;
                return ParkedPose.Prepare(__instance, parked);
            }
            catch (Exception ex) { AirParkBridge.Fault(ex); return false; }
        }

        public static bool FixedPrefix(PartModule __instance)
        {
            try { AirParkBridge.DisableAutoPark(); }
            catch (Exception ex) { AirParkBridge.Fault(ex); return false; }
            if (__instance == null || __instance.vessel == null) return false;
            try
            {
                // Do not let parked FixedUpdate impose a stale/zero world pose before KSP finishes loading.
                return !AirParkBridge.IsParked(__instance) || ParkedPose.Prepare(__instance, true);
            }
            catch (Exception ex) { AirParkBridge.Fault(ex); return false; }
        }

        public static void LoadPostfix(PartModule __instance, ConfigNode node)
        {
            if (!AirParkBridge.IsModule(__instance)) return;
            try { AirParkBridge.ReadVectors(__instance, node); }
            catch (Exception ex) { AirParkBridge.Fault(ex); }
        }

        public static void SavePostfix(PartModule __instance, ConfigNode node)
        {
            if (!AirParkBridge.IsModule(__instance)) return;
            try { AirParkBridge.WriteVectors(__instance, node); }
            catch (Exception ex) { AirParkBridge.Fault(ex); }
        }

        public static bool AutoPrefix()
        {
            try { AirParkBridge.DisableAutoPark(); }
            catch (Exception ex) { AirParkBridge.Fault(ex); }
            ScreenMessages.PostScreenMessage("AirPark Stability: AutoPark is disabled", 3f, ScreenMessageStyle.UPPER_CENTER);
            return false;
        }

        public static bool SettingsPrefix(MemberInfo member, ref bool __result)
        {
            if (member == null || member.Name != "autoPark") return true;
            __result = false;
            return false;
        }

        public static void ToolbarGuiPrefix(object __instance) { AirParkBridge.ResizeToolbar(__instance); }

        public static bool ToolbarWindowPrefix()
        {
            if (AirParkStabilityAddon.Instance != null) StabilityUI.DrawToolbar(AirParkStabilityAddon.Instance);
            else GUILayout.Label("AirPark Stability unavailable; parking blocked");
            GUI.DragWindow();
            return false;
        }
    }
}
