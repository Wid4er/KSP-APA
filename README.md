> **AI involvement disclosure:** ChatGPT-6.1 was involved in the development of this project.

# KSP-APA — AirPark Stability

**AirPark Stability 0.1.2** adds a hover stability check to [AirPark Continued](https://github.com/linuxgurugamer/AirPark) for Kerbal Space Program.

## What does it do?

Before parking, your vessel must remain stable for **10 consecutive seconds**, with:

- Surface speed below **1.5 m/s**.
- Absolute vertical speed below **0.5 m/s**.
- Atmospheric flight with active physics at **1×** time warp.

If either speed reaches or exceeds its limit, the timer resets. The panel shows progress and lets you cancel. Normal physics continues throughout the check: your engines or airship envelopes must provide the lift.

Once the check succeeds, the addon uses AirPark's original parking mechanism. To unpark, **Confirm Unpark** appears in the center of the screen and pauses the game. Its buttons are stacked vertically: **Cancel** keeps the vessel parked; **Confirm Unpark** resumes the game and unparks it.

It also fixes AirPark's position persistence when returning from the Tracking Station and keeps **AutoPark disabled** so it cannot bypass the checks.

## Requirements

- **Kerbal Space Program 1.12.x**; developed and tested in a KSP 1.12.5 installation.
- **AirPark Continued 1.7.1.9**, with its dependencies: ModuleManager, ToolbarController, SpaceTuxLibrary and ClickThroughBlocker.
- **HarmonyKSP**; the integration has been checked with Harmony 2.2.1.0.

**HLAirshipsCore is optional.** If installed, its buoyancy continues to work during the check. Release the HL anchor before requesting PARK.

Dependencies are not included in the download.

## Installation and use

1. Install the requirements above.
2. Download the ZIP from [Releases](https://github.com/Wid4er/KSP-APA/releases).
3. Copy its `GameData` folder into your KSP directory. The DLL should be at `GameData/APA/Plugins/AirParkStability.dll`.
4. Restart KSP. Use AirPark's toolbar, part menu or action group controls.

Install only one copy of APA.

## Configuration

With KSP closed, edit `GameData/APA/Settings.cfg`:

| Parameter | Default value |
| --- | ---: |
| `maxSurfaceSpeed` | 1.5 m/s |
| `maxVerticalSpeed` | 0.5 m/s |
| `requiredSeconds` | 10.0 s |
| `debugLogging` | false |

Use positive values and a decimal point. Atmospheric flight and active physics at 1× remain mandatory.

For build and test instructions, see [BUILD.md](docs/BUILD.md). Validation scope is described in [TESTING.md](docs/TESTING.md).
