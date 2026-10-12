using System;
using ClickThroughFix;
using UnityEngine;

namespace AirParkStability
{
    internal sealed class StabilityUI
    {
        private readonly int progressId = Guid.NewGuid().GetHashCode();
        private readonly ConfirmationDialog confirmation = new ConfirmationDialog();
        private Rect progressRect = new Rect(30, 180, 360, 300);

        public void Draw(AirParkStabilityAddon addon)
        {
            var skin = GUI.skin;
            GUI.skin = HighLogic.Skin;
            try
            {
                if (addon.Checking && !AirParkBridge.ToolbarVisible)
                    progressRect = ClickThruBlocker.GUILayoutWindow(progressId, progressRect,
                        id => { DrawProgress(addon); if (GUILayout.Button("Cancel Check")) addon.CancelCheck(); GUI.DragWindow(); },
                        "AirPark — Stability Check");
                if (addon.Confirming) confirmation.Draw(addon);
            }
            finally { GUI.skin = skin; }
        }

        public static void DrawToolbar(AirParkStabilityAddon addon)
        {
            var module = AirParkBridge.ControlModule(FlightGlobals.ActiveVessel);
            if (module == null) { GUILayout.Label("No AirPark Module Found"); return; }
            bool parked = AirParkBridge.IsParked(module);
            if (addon.Checking)
            {
                DrawProgress(addon);
                if (GUILayout.Button("Cancel Check")) addon.CancelCheck();
            }
            else
            {
                GUILayout.Label(parked && addon.Status != "Parking Successful" ? "Parked" : addon.Status);
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && (AirParkBridge.Ready || parked) && !addon.Confirming;
                if (GUILayout.Button(parked ? "Un-Park" : "Park Vessel")) addon.Request(module);
                GUI.enabled = enabled;
                GUILayout.Label(string.Format("Requires {0:0.0}s stable flight at 1x", addon.Settings.Limits.Seconds));
            }
            GUILayout.Label("AutoPark disabled by AirPark Stability");
            if (!AirParkBridge.Ready) GUILayout.Label("Integration fault: " + AirParkBridge.Error);
        }

        private static void DrawProgress(AirParkStabilityAddon addon)
        {
            var sample = addon.Sample;
            var limits = addon.Settings.Limits;
            GUILayout.Label("AirPark — Stability Check");
            GUILayout.Label(addon.Status);
            GUILayout.Label(string.Format("Surface Speed: {0:0.00} / {1:0.00} m/s", sample.SurfaceSpeed, limits.SurfaceSpeed));
            GUILayout.Label(string.Format("Vertical Speed: {0:0.00} / {1:0.00} m/s", Math.Abs(sample.VerticalSpeed), limits.VerticalSpeed));
            GUILayout.Label("Atmosphere: " + (sample.AtmosphereValid ? "Valid" : "Invalid"));
            GUILayout.Label("Flight Status: " + (sample.Flying ? "Flying" : "Invalid"));
            GUILayout.Label(string.Format("Stability: {0:0.0} / {1:0.0} seconds", addon.Monitor.StableSeconds, limits.Seconds));
            GUILayout.Label(string.Format("Progress: {0:0}%", addon.Monitor.Progress * 100));
            Rect bar = GUILayoutUtility.GetRect(320, 14);
            GUI.Box(bar, GUIContent.none);
            GUI.Box(new Rect(bar.x, bar.y, (float)(bar.width * addon.Monitor.Progress), bar.height), GUIContent.none);
        }
    }
}
