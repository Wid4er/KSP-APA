# Building

Requirements: .NET SDK 8, a KSP 1.12.x installation, HarmonyKSP and ClickThroughBlocker. The addon DLL targets .NET Framework 4.7.2 with C# 7.3.

Linux:

```sh
export KSP_ROOT='/path/to/Kerbal Space Program'
sh build.sh
sh test.sh
```

Set `DOTNET` to specify the SDK executable. `build.sh` copies the DLL to `APA/Plugins`; if APA is linked to GameData, this updates the active installation.

Windows, PowerShell:

```powershell
$env:KSP_ROOT = 'C:\Games\Kerbal Space Program'
dotnet build AirParkStability.csproj -c Release
Copy-Item bin/Release/net472/AirParkStability.dll APA/Plugins/AirParkStability.dll
dotnet run --project Tests/LogicTests.csproj -c Release
dotnet run --project Tests/ReloadTests.csproj -c Release
dotnet run --project Tests/AssemblyAudit.csproj -c Release -- $env:KSP_ROOT (Get-Location).Path
```

The project detects `KSP_x64_Data/Managed` or `KSP_Data/Managed`. You can also override `KspRoot`, `KspManaged`, `HarmonyPath`, `ClickThroughPath` and `FrameworkPathOverride` using `-p:Property=Value`.

References to the game and other mods use `Private=false`; their DLLs are not redistributed. AirPark and HLAirshipsCore are integrated through reflection. AirPark integration is restricted to the audited version 1.7.1.9.

## Release package

From a checkout with your changes committed:

```sh
python3 tools/package_release.py --output dist
```

The script reads files from the current commit, checks that only the addon's DLL is included, and creates `AirParkStability-<version>.zip` and its SHA256 checksum. The ZIP contains `GameData/APA`, README and public documentation; it excludes source, tests, logs, saves and dependencies.

Before publishing another version, update `Source/AssemblyInfo.cs` and `APA/AirParkStability.version`, build, run the tests and commit the verified DLL. GitHub does not need to download KSP libraries.
