using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class AssemblyAudit
{
    private static int passed;
    private static void Check(bool condition, string name) {
        if (!condition) throw new Exception("FAIL " + name); passed++; Console.WriteLine("PASS " + name);
    }
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) { yield return t; foreach (var n in t.NestedTypes) foreach (var child in Types(n)) yield return child; }
    private static IEnumerable<MethodDefinition> Methods(AssemblyDefinition a) { return a.MainModule.Types.SelectMany(Types).SelectMany(t => t.Methods).Where(m => m.HasBody); }
    private static IEnumerable<string> Calls(MethodDefinition m) { return m.Body.Instructions.Where(i => i.Operand is MethodReference).Select(i => ((MethodReference)i.Operand).FullName); }
    private static void Route(AssemblyDefinition asm, string target, params string[] expected) {
        var found = Methods(asm).Where(m => Calls(m).Any(c => c.Contains(target))).Select(m => m.DeclaringType.FullName + "." + m.Name).Distinct().OrderBy(s => s).ToArray();
        Check(found.SequenceEqual(expected.OrderBy(s => s)), "Known callers of " + target + ": " + string.Join(", ", found));
    }
    private static TypeDefinition Type(AssemblyDefinition a, string name) { return a.MainModule.Types.Single(t => t.FullName == name); }

    public static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Arguments: KSP_ROOT project_root");
        string ksp = args[0], root = args[1];
        string managed = Path.Combine(ksp, "KSP_x64_Data", "Managed");
        if (!Directory.Exists(managed)) managed = Path.Combine(ksp, "KSP_Data", "Managed");
        var airpark = AssemblyDefinition.ReadAssembly(Path.Combine(ksp, "GameData/AirPark/Plugins/AirParkContinued.dll"));
        var game = AssemblyDefinition.ReadAssembly(Path.Combine(managed, "Assembly-CSharp.dll"));
        var addonPath = Path.Combine(root, "APA/Plugins/AirParkStability.dll");
        var addon = AssemblyDefinition.ReadAssembly(addonPath);
        var harmony = AssemblyDefinition.ReadAssembly(Path.Combine(ksp, "GameData/000_Harmony/0Harmony.dll"));
        Check(airpark.Name.Version == new Version(1, 7, 1, 9), "Audited AirPark 1.7.1.9");
        Check(harmony.Name.Version == new Version(2, 2, 1, 0), "Installed Harmony 2.2.1.0");
        var module = Type(airpark, "AirPark.AirPark");
        foreach (string name in new[] { "TogglePark", "ParkVessel", "RestoreVesselState", "setVesselStill", "FixedUpdate", "ToggleAutoPark" })
            Check(module.Methods.Any(m => m.Name == name && m.Parameters.Count == 0 && m.ReturnType.FullName == "System.Void"), "Patch target " + name);
        Check(module.Fields.Any(f => f.Name == "Parked" && f.IsPublic && f.FieldType.FullName == "System.Boolean"), "Parked field");
        Check(Type(airpark, "AirPark.AirParkSettings").Fields.Any(f => f.Name == "autoPark" && f.IsPublic), "AutoPark settings field");
        var toolbar = Type(airpark, "AirPark.AirParkToolbar");
        foreach (string name in new[] { "toolbarGuiEnabled", "toolbarRect", "toolbarControl" }) Check(toolbar.Fields.Any(f => f.Name == name), "Toolbar field " + name);
        foreach (string name in new[] { "Enabled", "Interactible" }) Check(Type(airpark, "AirPark.AirParkSettings").Methods.Any(m => m.Name == name && m.Parameters[0].Name == "member"), "Settings prefix parameter " + name);
        Route(airpark, "AirPark.AirPark::TogglePark()", "AirPark.AirPark.TogglePark_Event", "AirPark.AirPark.TogglePart_AG", "AirPark.AirParkToolbar.ToolbarWindow");
        Route(airpark, "AirPark.AirPark::ParkVessel()", "AirPark.AirPark.TogglePark", "AirPark.AirPark.FixedUpdate");
        Route(airpark, "AirPark.AirPark::RestoreVesselState()", "AirPark.AirPark.TogglePark", "AirPark.AirPark.FixedUpdate");
        Route(airpark, "AirPark.AirPark::setVesselStill()", "AirPark.AirPark.ParkVessel", "AirPark.AirPark.RestoreVesselState", "AirPark.AirPark.FixedUpdate");
        var flagWriters = Methods(airpark).Where(m => m.Body.Instructions.Any(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.FullName == "System.Boolean AirPark.AirPark::Parked")).Select(m => m.Name).OrderBy(n => n).ToArray();
        Check(flagWriters.SequenceEqual(new[] { "FixedUpdate", "ParkVessel", "RestoreVesselState" }), "All original Parked writers accounted for");

        var vessel = Type(game, "Vessel");
        var getSrf = vessel.Methods.Single(m => m.Name == "GetSrfVelocity");
        Check(getSrf.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "srf_velocity"), "GetSrfVelocity reads srf_velocity");
        var posVel = vessel.Methods.Single(m => m.Name == "UpdatePosVel");
        Check(Calls(posVel).Any(c => c.Contains("CelestialBody::getRFrmVelOrbit")) && posVel.Body.Instructions.Any(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.Name == "srf_velocity"), "KSP derives surface vector using planetary rotation");
        Check(Calls(module.Methods.Single(m => m.Name == "TogglePark")).Any(c => c.Contains("Vessel::GetSrfVelocity")), "Original AirPark snapshots surface velocity");
        Check(Calls(module.Methods.Single(m => m.Name == "RestoreVesselState")).Any(c => c.Contains("Vessel::SetWorldVelocity")), "Original unpark restores through SetWorldVelocity");
        Check(!addon.MainModule.AssemblyReferences.Any(r => r.Name == "AirParkContinued" || r.Name.StartsWith("HLA")), "No hard AirPark/HLA dependency");
        Check(!Methods(addon).SelectMany(Calls).Any(c => c.Contains("Vessel::SetWorldVelocity") || c.Contains("Vessel::SetPosition") || c.Contains("Vessel::SetRotation") || c.Contains("Rigidbody::AddForce") || c.Contains("Rigidbody::set_velocity")), "Addon never writes motion or adds forces");
        Check(!Methods(addon).SelectMany(m => m.Body.Instructions).Any(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.DeclaringType.FullName == "Vessel"), "Addon never writes Vessel fields");
        var probeCalls = Calls(Type(addon, "AirParkStability.VesselProbe").Methods.Single(m => m.Name == "PhysicsAvailable")).ToArray();
        Check(probeCalls.Any(c => c.Contains("FlightDriver::get_Pause")) && probeCalls.Any(c => c.Contains("Vessel::get_HoldPhysics")), "Uses verified public pause/physics properties");
        Check(Type(game, "FlightDriver").Methods.Any(m => m.Name == "SetPause" && m.IsPublic && m.IsStatic &&
            m.Parameters.Count == 2 && m.Parameters.All(p => p.ParameterType.FullName == "System.Boolean")), "Verified KSP pause API");
        var controller = Type(addon, "AirParkStability.AirParkStabilityAddon");
        var confirmEligibility = Calls(controller.Methods.Single(m => m.Name == "get_CanConfirmUnpark")).ToArray();
        Check(confirmEligibility.Any(c => c.Contains("VesselProbe::get_RateIsOneX")) && !confirmEligibility.Any(c => c.Contains("VesselProbe::get_OneX")), "Confirmation eligibility does not deadlock on paused timeScale");
        var confirmCalls = Calls(controller.Methods.Single(m => m.Name == "ConfirmUnpark")).ToArray();
        int closeIndex = Array.FindIndex(confirmCalls, c => c.Contains("::CloseConfirmation"));
        Check(closeIndex >= 0 && Array.FindIndex(confirmCalls, c => c.Contains("AirParkBridge::UnparkOriginal")) > closeIndex &&
              Array.FindIndex(confirmCalls, c => c.Contains("VesselProbe::get_OneX")) > closeIndex, "Unpark revalidates running physics after closing pause");
        Check(new[] { "SceneRequested", "GameLoaded", "VesselChanged", "VesselDestroyed", "VesselModified", "OnDestroy" }.All(name =>
            Calls(controller.Methods.Single(m => m.Name == name)).Any(c => c.Contains("::CloseConfirmation"))), "All confirmation lifecycle exits restore pause");
        var readField = Type(game, "BaseField").Methods.Single(m => m.Name == "ReadPvt");
        Check(!readField.Body.Instructions.Any(i => i.Operand is TypeReference t && t.FullName == "Vector3d"), "KSP BaseField reader has no Vector3d support");
        foreach (string name in new[] { "ParkPosition", "ParkVelocity", "ParkAcceleration", "ParkAngularVelocity" })
            Check(module.Fields.Any(f => f.Name == name && f.FieldType.FullName == "Vector3d"), "Unsupported original vector field " + name);
        foreach (string name in new[] { "Load", "Save" })
            Check(Type(game, "PartModule").Methods.Any(m => m.Name == name && m.Parameters.Count == 1 && m.Parameters[0].Name == "node"), "Verified persistence hook " + name);
        var patches = Type(addon, "AirParkStability.AirParkPatches");
        Check(Calls(patches.Methods.Single(m => m.Name == "StillPrefix")).Any(c => c.Contains("ParkedPose::Prepare")) &&
              Calls(patches.Methods.Single(m => m.Name == "FixedPrefix")).Any(c => c.Contains("ParkedPose::Prepare")), "Both freeze paths prepare current world pose");
        Check(Calls(patches.Methods.Single(m => m.Name == "LoadPostfix")).Any(c => c.Contains("AirParkBridge::ReadVectors")) &&
              Calls(patches.Methods.Single(m => m.Name == "SavePostfix")).Any(c => c.Contains("AirParkBridge::WriteVectors")), "Explicit AirPark vector load/save hooks");
        Check(Directory.GetFiles(Path.Combine(root, "APA"), "*.dll", SearchOption.AllDirectories).Length == 1, "GameData addon contains exactly its own DLL");
        var hlPath = Path.Combine(ksp, "GameData/HLAirshipsCore/Plugins/HLAirships.dll");
        if (File.Exists(hlPath)) {
            var hl = AssemblyDefinition.ReadAssembly(hlPath);
            var envelope = Type(hl, "HLAirships.HLEnvelopePartModule");
            Check(Calls(envelope.Methods.Single(m => m.Name == "envelopeUpdate")).Any(c => c.Contains("Rigidbody::AddForceAtPosition")), "HLA envelope applies real buoyant forces");
            Check(Type(hl, "HLAirships.AnchorModule").Fields.Any(f => f.Name == "Anchored" && f.IsPrivate && f.FieldType.FullName == "System.Boolean"), "Optional HLA private anchor flag verified");
        }
        foreach (string path in new[] { addonPath, Path.Combine(ksp, "GameData/AirPark/Plugins/AirParkContinued.dll"), Path.Combine(ksp, "GameData/000_Harmony/0Harmony.dll"), Path.Combine(managed, "Assembly-CSharp.dll") })
            Console.WriteLine("SHA256 " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() + " " + Path.GetFileName(path));
        Console.WriteLine(passed + " static assembly checks passed. These do not prove Unity runtime patching or flight acceptance.");
        return 0;
    }
}
