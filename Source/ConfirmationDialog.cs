using System;
using ClickThroughFix;
using UnityEngine;

namespace AirParkStability
{
    internal sealed class ConfirmationDialog
    {
        private readonly int windowId = Guid.NewGuid().GetHashCode();
        private Rect rect = new Rect(0, 0, 420, 190);
        private GUIStyle wrapped;

        public void Draw(AirParkStabilityAddon addon)
        {
            if (wrapped == null) wrapped = new GUIStyle(HighLogic.Skin.label) { wordWrap = true };
            rect.x = Math.Max(0, (Screen.width - rect.width) * 0.5f);
            rect.y = Math.Max(0, (Screen.height - rect.height) * 0.5f);
            rect = ClickThruBlocker.GUILayoutWindow(windowId, rect, id =>
            {
                GUILayout.Label("This vessel will resume normal physics. Make sure its engines and stabilization systems are ready.", wrapped);
                GUILayout.Label("Game paused.");
                if (!addon.CanConfirmUnpark) GUILayout.Label("Cancel and retry when physics is ready at 1x.");
                GUILayout.BeginVertical();
                if (GUILayout.Button("Cancel")) addon.CancelUnpark();
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && addon.CanConfirmUnpark;
                if (GUILayout.Button("Confirm Unpark")) addon.ConfirmUnpark();
                GUI.enabled = enabled;
                GUILayout.EndVertical();
            }, "Confirm Unpark");
        }
    }
}
