# Validation

The user reports that the mod works correctly in their personal KSP installation. This does not imply exhaustive validation with every vessel, celestial body or mod combination.

Version 0.1.2 includes:

- **16 groups of logic tests:** stability, thresholds, pause and time warp, invalid samples, transition permissions and the confirmation pause lifecycle.
- **8 groups of reload regression tests:** zero position, altitude, incomplete loading, origin shifts and rotation, rails, UNPARK and serialization. These use models of KSP/Unity coordinates, not an actual game simulation.
- **44 assembly checks:** AirPark/KSP APIs and call paths, persistence, parking anchors, pause and the absence of motion writes by the addon.

Run `sh test.sh` with `KSP_ROOT` configured. The build should finish without errors or warnings.

On a new installation, check PARK from the toolbar, part menu and action groups; timer resets at both speed limits; cancellation; reloading from the Tracking Station; the centered confirmation with pause; Cancel; Confirm Unpark; and blocked AutoPark.

The addon preserves AirPark's original velocity restoration. Reference frame changes, high altitude and bodies added by mods need specific testing. The position fix cannot recover a vessel already saved at an incorrect geographic location; in that case, an earlier valid save is required.
