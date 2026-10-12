using System;
using System.Globalization;
using UnityEngine;

namespace AirParkStability
{
    internal sealed class StabilitySettings
    {
        public readonly StabilityLimits Limits;
        public readonly bool DebugLogging;

        private StabilitySettings(StabilityLimits limits, bool debug) { Limits = limits; DebugLogging = debug; }

        public static StabilitySettings Load()
        {
            var nodes = GameDatabase.Instance.GetConfigNodes("AIRPARK_STABILITY");
            var node = nodes.Length == 0 ? null : nodes[nodes.Length - 1];
            bool debug;
            bool.TryParse(node == null ? null : node.GetValue("debugLogging"), out debug);
            return new StabilitySettings(new StabilityLimits(
                Number(node, "maxSurfaceSpeed", 1.5), Number(node, "maxVerticalSpeed", 0.5),
                Number(node, "requiredSeconds", 10)), debug);
        }

        private static double Number(ConfigNode node, string key, double fallback)
        {
            if (node == null || !node.HasValue(key)) return fallback;
            double value;
            if (double.TryParse(node.GetValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                StabilityLimits.Finite(value) && value > 0) return value;
            Debug.LogWarning("[AirParkStability] Invalid " + key + "; using " + fallback.ToString(CultureInfo.InvariantCulture));
            return fallback;
        }
    }
}
