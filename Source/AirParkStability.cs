using System;
using System.Linq;
using UnityEngine;

namespace AirParkStability
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class StabilityBootstrap : MonoBehaviour
    {
        private void Start() { AirParkBridge.Initialize(); }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class AirParkStabilityAddon : MonoBehaviour
    {
        internal static AirParkStabilityAddon Instance { get; private set; }
        internal StabilitySettings Settings { get; private set; }
        internal StabilityMonitor Monitor { get; private set; }
        internal StabilitySample Sample { get; private set; }
        internal string Status { get; private set; }
        internal bool Checking { get { return !ReferenceEquals(checkingModule, null); } }
        internal bool Confirming { get { return !ReferenceEquals(confirmationVessel, null); } }
        private PartModule checkingModule;
        private Vessel checkingVessel, confirmationVessel;
        private Guid checkingId, confirmationId;
        private int requestFrame = -1;
        private bool sceneLeaving, uiVisible = true;
        private float successUntil;
        private StabilityUI ui;
        private ConfirmationPause confirmationPause;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
            Settings = StabilitySettings.Load();
            Monitor = new StabilityMonitor(Settings.Limits);
            Status = "Ready";
            ui = new StabilityUI();
            confirmationPause = new ConfirmationPause(() => FlightDriver.Pause, paused => FlightDriver.SetPause(paused, false));
            GameEvents.onVesselChange.Add(VesselChanged);
            GameEvents.onVesselDestroy.Add(VesselDestroyed);
            GameEvents.onVesselGoOnRails.Add(VesselOnRails);
            GameEvents.onVesselWasModified.Add(VesselModified);
            GameEvents.onGamePause.Add(ResetContinuity);
            GameEvents.onTimeWarpRateChanged.Add(WarpChanged);
            GameEvents.onGameSceneLoadRequested.Add(SceneRequested);
            GameEvents.onGameStateLoad.Add(GameLoaded);
            GameEvents.onHideUI.Add(HideUI);
            GameEvents.onShowUI.Add(ShowUI);
        }

        internal void Request(PartModule source)
        {
            if (!AirParkBridge.IsModule(source) || sceneLeaving || source.vessel == null ||
                source.vessel != FlightGlobals.ActiveVessel || !HighLogic.LoadedSceneIsFlight) return;
            var control = AirParkBridge.ControlModule(source.vessel);
            if (AirParkBridge.IsParked(control)) { RequestUnpark(control); return; }
            if (Checking)
            {
                // An action group can invoke several command parts in the same frame.
                // Treat that as one PARK request; a later toggle still cancels normally.
                if (requestFrame == Time.frameCount && checkingVessel == source.vessel) return;
                CancelCheck("Ready — Check cancelled");
                return;
            }
            if (!AirParkBridge.Ready) { Notify("Integration fault: " + AirParkBridge.Error); return; }
            CloseConfirmation();
            Sample = VesselProbe.Read(source.vessel);
            if (!Sample.Eligible) { Notify(VesselProbe.InvalidReason(Sample)); return; }
            checkingModule = source;
            checkingVessel = source.vessel;
            checkingId = checkingVessel.id;
            requestFrame = Time.frameCount;
            Monitor.Reset();
            Status = "Checking Stability";
            Trace("Stability check requested for " + checkingId);
        }

        internal void RequestUnpark(PartModule source)
        {
            if (sceneLeaving || !HighLogic.LoadedSceneIsFlight || source == null || source.vessel == null ||
                source.vessel != FlightGlobals.ActiveVessel || !AirParkBridge.IsParked(source)) return;
            if (Confirming && confirmationVessel == source.vessel && confirmationId == source.vessel.id) return;
            CloseConfirmation();
            CancelCheck("Parked");
            confirmationVessel = source.vessel;
            confirmationId = source.vessel.id;
            confirmationPause.Begin();
        }

        internal bool CanConfirmUnpark
        {
            get { return Confirming && confirmationVessel != null && !sceneLeaving && confirmationVessel.id == confirmationId &&
                VesselProbe.PhysicsAvailable(confirmationVessel, allowPaused: true) && VesselProbe.RateIsOneX; }
        }

        internal void ConfirmUnpark()
        {
            if (!CanConfirmUnpark) return;
            var vessel = confirmationVessel;
            var id = confirmationId;
            try
            {
                CloseConfirmation(resumeForUnpark: true);
                if (vessel == null || vessel.id != id || !VesselProbe.PhysicsAvailable(vessel) || !VesselProbe.OneX)
                { Notify("Physics not ready at 1x; vessel remains parked"); return; }
                var modules = AirParkBridge.Modules(vessel).Where(AirParkBridge.IsParked).ToArray();
                // Old saves may have more than one parked module; release all of them so none can refreeze it.
                foreach (var module in modules) AirParkBridge.UnparkOriginal(module);
                if (modules.Any(AirParkBridge.IsParked)) Notify("AirPark did not restore this vessel; inspect its saved state");
                else { Status = "Ready"; Trace("Unpark confirmed"); }
            }
            catch (Exception ex) { AirParkBridge.Fault(ex); Notify("Unpark failed: " + AirParkBridge.Error); }
        }

        internal void CancelUnpark() { CloseConfirmation(); Status = "Parked"; }

        private void CloseConfirmation(bool resumeForUnpark = false)
        {
            confirmationVessel = null;
            if (confirmationPause != null) confirmationPause.Close(resumeForUnpark);
        }

        internal void CancelCheck(string status = "Ready — Check cancelled")
        {
            checkingModule = null;
            checkingVessel = null;
            requestFrame = -1;
            if (Monitor != null) Monitor.Reset();
            Status = status;
        }

        private void FixedUpdate()
        {
            if (!Checking) return;
            try
            {
                if (!AirParkBridge.Ready || !SameCheckVessel()) { CancelCheck("Ready — Check cancelled"); return; }
                Sample = VesselProbe.Read(checkingVessel);
                var result = Monitor.Step(Sample, Time.fixedDeltaTime * 1.5);
                if (result == CheckResult.Invalid) { CancelCheck("Ready — " + VesselProbe.InvalidReason(Sample)); return; }
                Status = result == CheckResult.Unstable ? "Unstable — Timer Reset" : "Checking Stability";
                if (result != CheckResult.Complete) return;
                // A final independent eligibility read before issuing the one-use authorization.
                var finalSample = VesselProbe.Read(checkingVessel);
                if (!finalSample.Eligible || !StabilityLimits.Finite(finalSample.SurfaceSpeed) ||
                    !StabilityLimits.Finite(finalSample.VerticalSpeed) || finalSample.SurfaceSpeed < 0 ||
                    finalSample.SurfaceSpeed >= Settings.Limits.SurfaceSpeed ||
                    Math.Abs(finalSample.VerticalSpeed) >= Settings.Limits.VerticalSpeed) { Monitor.Reset(); return; }
                var module = checkingModule;
                CancelCheck();
                AirParkBridge.ParkOriginal(module);
                if (!AirParkBridge.IsParked(module)) { Notify("Original AirPark declined parking"); return; }
                Status = "Parking Successful";
                successUntil = Time.realtimeSinceStartup + 3;
                Trace("Stability passed; original AirPark parked vessel");
            }
            catch (Exception ex) { CancelCheck("Ready — Integration fault"); AirParkBridge.Fault(ex); }
        }

        private bool SameCheckVessel()
        {
            return !sceneLeaving && checkingModule != null && checkingVessel != null &&
                checkingVessel == FlightGlobals.ActiveVessel && checkingVessel.id == checkingId &&
                checkingModule.vessel == checkingVessel && !AirParkBridge.IsParked(AirParkBridge.ControlModule(checkingVessel));
        }

        private void Update()
        {
            if (!AirParkBridge.Present) return;
            try
            {
                AirParkBridge.DisableAutoPark();
                if (Checking && !SameCheckVessel()) CancelCheck();
                if (Checking && FlightDriver.Pause) ResetContinuity();
                if (Checking && ((!FlightDriver.Pause && !VesselProbe.OneX) || !checkingVessel.loaded || checkingVessel.packed || checkingVessel.HoldPhysics))
                    CancelCheck("Ready — Physics interrupted");
                if (Confirming && (confirmationVessel == null || confirmationVessel != FlightGlobals.ActiveVessel || confirmationVessel.id != confirmationId ||
                    !confirmationVessel.loaded || confirmationVessel.packed)) CloseConfirmation();
                if (Confirming) confirmationPause.Maintain();
                if (!Checking && !Confirming && Time.realtimeSinceStartup >= successUntil && Status == "Parking Successful")
                    Status = "Parked";
            }
            catch (Exception ex) { CancelCheck("Ready — Integration fault"); AirParkBridge.Fault(ex); }
        }

        private void OnGUI()
        {
            if ((uiVisible || Confirming) && !sceneLeaving && AirParkBridge.Present && ui != null) ui.Draw(this);
        }

        private void ResetContinuity() { if (Checking) { Monitor.Reset(); Status = "Unstable — Timer Reset"; } }
        private void WarpChanged() { if (Checking) CancelCheck("Ready — Time warp changed"); }
        private void SceneRequested(GameScenes scene) { sceneLeaving = true; CancelCheck(); CloseConfirmation(); }
        private void GameLoaded(ConfigNode node) { CancelCheck(); CloseConfirmation(); }
        private void VesselChanged(Vessel v) { CancelCheck(); CloseConfirmation(); }
        private void VesselDestroyed(Vessel v)
        {
            if (v == checkingVessel) CancelCheck();
            if (v == confirmationVessel) CloseConfirmation();
        }
        private void VesselOnRails(Vessel v) { VesselDestroyed(v); }
        private void VesselModified(Vessel v) { if (v == checkingVessel) ResetContinuity(); if (v == confirmationVessel) CloseConfirmation(); }
        private void HideUI() { uiVisible = false; }
        private void ShowUI() { uiVisible = true; }
        private void Trace(string message) { if (Settings.DebugLogging) Debug.Log("[AirParkStability] " + message); }
        private void Notify(string message)
        {
            Status = message;
            ScreenMessages.PostScreenMessage("AirPark Stability: " + message, 4f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            GameEvents.onVesselChange.Remove(VesselChanged);
            GameEvents.onVesselDestroy.Remove(VesselDestroyed);
            GameEvents.onVesselGoOnRails.Remove(VesselOnRails);
            GameEvents.onVesselWasModified.Remove(VesselModified);
            GameEvents.onGamePause.Remove(ResetContinuity);
            GameEvents.onTimeWarpRateChanged.Remove(WarpChanged);
            GameEvents.onGameSceneLoadRequested.Remove(SceneRequested);
            GameEvents.onGameStateLoad.Remove(GameLoaded);
            GameEvents.onHideUI.Remove(HideUI);
            GameEvents.onShowUI.Remove(ShowUI);
            CancelCheck();
            CloseConfirmation();
            Instance = null;
        }
    }
}
