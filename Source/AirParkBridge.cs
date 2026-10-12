using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AirParkStability
{
    internal static class AirParkBridge
    {
        public static bool Present { get; private set; }
        public static bool Ready { get; private set; }
        public static string Error { get; private set; }
        private static Type moduleType, settingsType;
        private static FieldInfo parked, autoPark, toolbarOpen, toolbarRect, toolbarControl;
        private static FieldInfo parkPosition, parkRotation;
        private static FieldInfo[] vectorFields;
        private static MethodInfo toggle, restore, customParams, updateTexture;
        private static Game settingsGame;
        private static object settings;
        private static Harmony harmony;
        public static readonly OperationPermit Permit = new OperationPermit();

        public static void Initialize()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "AirParkContinued").ToArray();
            if (assemblies.Length == 0) { Debug.Log("[AirParkStability] AirPark absent; inactive."); return; }
            Present = true;
            try
            {
                if (assemblies.Length != 1) throw new InvalidOperationException("Duplicate AirPark assemblies.");
                var asm = assemblies[0];
                moduleType = asm.GetType("AirPark.AirPark", true);
                settingsType = asm.GetType("AirPark.AirParkSettings", true);
                var toolbar = asm.GetType("AirPark.AirParkToolbar", true);
                parked = RequireField(moduleType, "Parked", typeof(bool));
                parkPosition = RequireField(moduleType, "ParkPosition", typeof(Vector3d));
                parkRotation = RequireField(moduleType, "ParkRotation", typeof(Quaternion));
                vectorFields = new[] { parkPosition,
                    RequireField(moduleType, "ParkVelocity", typeof(Vector3d)),
                    RequireField(moduleType, "ParkAcceleration", typeof(Vector3d)),
                    RequireField(moduleType, "ParkAngularVelocity", typeof(Vector3d)) };
                autoPark = RequireField(settingsType, "autoPark", typeof(bool));
                toggle = RequireMethod(moduleType, "TogglePark");
                restore = RequireMethod(moduleType, "RestoreVesselState");
                harmony = new Harmony("daniel.airpark.stability");
                // Install transition guards first. Never remove successful guards after a later error.
                Patch(RequireMethod(moduleType, "ParkVessel"), "ParkPrefix");
                Patch(restore, "RestorePrefix");
                Patch(RequireMethod(moduleType, "setVesselStill"), "StillPrefix");
                Patch(toggle, "TogglePrefix");
                Patch(RequireMethod(moduleType, "FixedUpdate"), "FixedPrefix");
                Patch(RequireMethod(moduleType, "ToggleAutoPark"), "AutoPrefix");
                PatchPostfix(AccessTools.Method(typeof(PartModule), "Load", new[] { typeof(ConfigNode) }), "LoadPostfix");
                PatchPostfix(AccessTools.Method(typeof(PartModule), "Save", new[] { typeof(ConfigNode) }), "SavePostfix");

                customParams = typeof(GameParameters).GetMethods().Single(m => m.Name == "CustomParams" &&
                    m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(settingsType);
                toolbarOpen = RequireField(toolbar, "toolbarGuiEnabled", typeof(bool));
                toolbarRect = RequireField(toolbar, "toolbarRect", typeof(Rect));
                toolbarControl = AccessTools.Field(toolbar, "toolbarControl");
                updateTexture = RequireMethod(toolbar, "SetAutoParkTexture");
                Patch(RequireMethod(toolbar, "OnGUI"), "ToolbarGuiPrefix");
                Patch(AccessTools.Method(toolbar, "ToolbarWindow", new[] { typeof(int) }), "ToolbarWindowPrefix");
                Patch(AccessTools.Method(settingsType, "Enabled", new[] { typeof(MemberInfo), typeof(GameParameters) }), "SettingsPrefix");
                Patch(AccessTools.Method(settingsType, "Interactible", new[] { typeof(MemberInfo), typeof(GameParameters) }), "SettingsPrefix");
                if (asm.GetName().Version != new Version(1, 7, 1, 9))
                    throw new InvalidOperationException("Only audited AirPark 1.7.1.9 is supported; found " + asm.GetName().Version);
                Ready = true;
                Debug.Log("[AirParkStability] Guards installed for AirPark 1.7.1.9; AutoPark disabled.");
            }
            catch (Exception ex) { Fault(ex); }
        }

        public static void Fault(Exception ex)
        {
            Ready = false;
            string message = ex.GetBaseException().Message;
            if (Error == message) return;
            Error = message;
            Debug.LogError("[AirParkStability] Integration fault; new parking blocked: " + ex);
        }

        private static FieldInfo RequireField(Type type, string name, Type fieldType)
        {
            var field = AccessTools.Field(type, name);
            if (field == null || field.FieldType != fieldType) throw new MissingFieldException(type.FullName, name);
            return field;
        }

        private static MethodInfo RequireMethod(Type type, string name)
        {
            var method = AccessTools.Method(type, name, Type.EmptyTypes);
            if (method == null || method.ReturnType != typeof(void)) throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private static void Patch(MethodInfo original, string prefix)
        {
            if (original == null) throw new MissingMethodException(prefix);
            harmony.Patch(original, prefix: new HarmonyMethod(typeof(AirParkPatches), prefix) { priority = Priority.First });
        }

        private static void PatchPostfix(MethodInfo original, string postfix)
        {
            if (original == null) throw new MissingMethodException(postfix);
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(AirParkPatches), postfix));
        }

        public static void ReadVectors(PartModule module, ConfigNode node)
        {
            ParkedPose.Forget(module);
            foreach (var field in vectorFields)
            {
                if (!node.HasValue(field.Name)) continue;
                double x, y, z;
                if (!VectorPersistence.TryRead(node.GetValue(field.Name), out x, out y, out z))
                    throw new FormatException("Invalid saved AirPark vector: " + field.Name);
                field.SetValue(module, new Vector3d(x, y, z));
            }
        }

        public static void WriteVectors(PartModule module, ConfigNode node)
        {
            foreach (var field in vectorFields)
            {
                var value = (Vector3d)field.GetValue(module);
                node.SetValue(field.Name, VectorPersistence.Write(value.x, value.y, value.z), true);
            }
        }

        public static void SetParkedPose(PartModule module, Vector3d position, Quaternion rotation)
        {
            if (!StabilityLimits.Finite(position.x) || !StabilityLimits.Finite(position.y) || !StabilityLimits.Finite(position.z))
                throw new InvalidOperationException("Invalid world parking position.");
            parkPosition.SetValue(module, position);
            parkRotation.SetValue(module, rotation);
        }

        public static bool IsModule(PartModule module) { return module != null && module.GetType() == moduleType; }
        public static bool IsParked(PartModule module) { return IsModule(module) && (bool)parked.GetValue(module); }

        public static List<PartModule> Modules(Vessel v)
        {
            var result = new List<PartModule>();
            if (v != null && v.loaded)
                foreach (var p in v.parts)
                    foreach (PartModule m in p.Modules)
                        if (IsModule(m)) result.Add(m);
            return result;
        }

        public static PartModule ControlModule(Vessel v)
        {
            var modules = Modules(v);
            // Multiple command parts have AirPark. A parked sibling must take precedence.
            return modules.FirstOrDefault(IsParked) ?? modules.FirstOrDefault();
        }

        public static void DisableAutoPark()
        {
            if (!Present || customParams == null || HighLogic.CurrentGame == null) return;
            if (!ReferenceEquals(settingsGame, HighLogic.CurrentGame))
            {
                settingsGame = HighLogic.CurrentGame;
                settings = customParams.Invoke(settingsGame.Parameters, null);
            }
            if (settings == null || !(bool)autoPark.GetValue(settings)) return;
            autoPark.SetValue(settings, false);
            // AirPark can call this before constructing the toolbar; avoid its null dereference.
            if (toolbarControl != null && toolbarControl.GetValue(null) != null && updateTexture != null)
                updateTexture.Invoke(null, null);
        }

        public static bool ToolbarVisible { get { return toolbarOpen != null && (bool)toolbarOpen.GetValue(null); } }

        public static void ResizeToolbar(object toolbar)
        {
            if (toolbarRect == null) return;
            var rect = (Rect)toolbarRect.GetValue(toolbar);
            rect.width = 360;
            rect.height = AirParkStabilityAddon.Instance != null && AirParkStabilityAddon.Instance.Checking ? 330 : 180;
            toolbarRect.SetValue(toolbar, rect);
        }

        public static void ParkOriginal(PartModule module)
        {
            ParkedPose.Forget(module);
            Permit.Invoke(module, OriginalOperation.Park, () => toggle.Invoke(module, null));
            if (IsParked(module)) ParkedPose.Prepare(module, true);
        }

        public static void UnparkOriginal(PartModule module)
        {
            // Reuse the exact restoration routine without TogglePark's unrelated orbital restriction.
            // Confirmation may arrive before the first parked FixedUpdate in a newly loaded scene.
            if (!ParkedPose.Prepare(module, true)) throw new InvalidOperationException("KSP has not finished reconstructing the parked vessel.");
            Permit.Invoke(module, OriginalOperation.Unpark, () => restore.Invoke(module, null));
            if (!IsParked(module)) ParkedPose.Forget(module);
        }
    }
}
