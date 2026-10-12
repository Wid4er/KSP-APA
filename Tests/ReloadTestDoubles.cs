// Standalone models for the verified APIs used by ParkedPose. They simulate coordinate
// changes; they are not KSP or a proof of Unity/Harmony runtime acceptance.
using System;
using N = System.Numerics;

public struct Vector3d
{
    public double x, y, z;
    public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
    public static implicit operator Vector3d(UnityEngine.Vector3 v) { return new Vector3d(v.x, v.y, v.z); }
    public static implicit operator UnityEngine.Vector3(Vector3d v) { return new UnityEngine.Vector3((float)v.x, (float)v.y, (float)v.z); }
}

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public struct Quaternion
    {
        public N.Quaternion Value;
        public static Quaternion Inverse(Quaternion q) { return new Quaternion { Value = N.Quaternion.Inverse(q.Value) }; }
        public static Quaternion operator *(Quaternion a, Quaternion b) { return new Quaternion { Value = a.Value * b.Value }; }
    }
    public class Transform
    {
        public Vector3 position;
        public Quaternion rotation = new Quaternion { Value = N.Quaternion.Identity };
    }
    public static class Debug { public static void Log(object message) {} }
}

public static class HighLogic { public static bool LoadedSceneIsFlight = true; }
public static class FlightGlobals { public static bool ready = true; }
public class Vessel
{
    public bool loaded = true, packed;
    public bool HoldPhysics { get; set; }
    public CelestialBody mainBody;
    public string vesselName = "aPARKar";
    public UnityEngine.Transform vesselTransform = new UnityEngine.Transform();
}
public class PartModule
{
    public Vessel vessel;
    public Vector3d ParkPosition; // Simulated private fields of the original AirPark.
    public UnityEngine.Quaternion ParkRotation;
}
public class CelestialBody
{
    public string bodyName = "Kerbin";
    public UnityEngine.Transform bodyTransform = new UnityEngine.Transform();
    public Vector3d Position;
    public double Radius = 600000;
    public Vector3d GetWorldSurfacePosition(double lat, double lon, double alt)
    {
        lat *= Math.PI / 180; lon *= Math.PI / 180;
        double r = Radius + alt;
        var surface = new N.Vector3((float)(r * Math.Cos(lat) * Math.Cos(lon)), (float)(r * Math.Sin(lat)), (float)(r * Math.Cos(lat) * Math.Sin(lon)));
        var world = N.Vector3.Transform(surface, bodyTransform.rotation.Value);
        return new Vector3d(world.X + Position.x, world.Y + Position.y, world.Z + Position.z);
    }
    public void GetLatLonAlt(Vector3d world, out double lat, out double lon, out double alt)
    {
        var relative = new N.Vector3((float)(world.x - Position.x), (float)(world.y - Position.y), (float)(world.z - Position.z));
        var surface = N.Vector3.Transform(relative, N.Quaternion.Inverse(bodyTransform.rotation.Value));
        double r = Math.Sqrt((double)surface.X * surface.X + (double)surface.Y * surface.Y + (double)surface.Z * surface.Z);
        lat = Math.Asin(surface.Y / r) * 180 / Math.PI;
        lon = Math.Atan2(surface.Z, surface.X) * 180 / Math.PI;
        alt = r - Radius;
    }
}

namespace AirParkStability
{
    internal static class AirParkBridge
    {
        public static void SetParkedPose(PartModule module, Vector3d position, UnityEngine.Quaternion rotation)
        { module.ParkPosition = position; module.ParkRotation = rotation; }
    }
}
