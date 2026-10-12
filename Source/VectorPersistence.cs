using System;
using System.Globalization;

namespace AirParkStability
{
    // KSP's BaseField reader supports Unity Vector3, but not Vector3d.
    // Accept AirPark's legacy ToString syntax and write round-trip invariant doubles.
    internal static class VectorPersistence
    {
        public static bool TryRead(string text, out double x, out double y, out double z)
        {
            x = y = z = 0;
            if (text == null) return false;
            var values = text.Trim().Trim('[', ']', '(', ')').Split(',');
            return values.Length == 3 && Read(values[0], out x) && Read(values[1], out y) && Read(values[2], out z);
        }

        private static bool Read(string text, out double value)
        {
            return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                StabilityLimits.Finite(value);
        }

        public static string Write(double x, double y, double z)
        {
            if (!StabilityLimits.Finite(x) || !StabilityLimits.Finite(y) || !StabilityLimits.Finite(z))
                throw new ArgumentException("Cannot persist a nonfinite AirPark vector.");
            return x.ToString("R", CultureInfo.InvariantCulture) + "," + y.ToString("R", CultureInfo.InvariantCulture) +
                "," + z.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
