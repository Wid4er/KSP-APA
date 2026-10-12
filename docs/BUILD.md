# Compilación

Requisitos: .NET SDK 8, una instalación de KSP 1.12.x, HarmonyKSP y ClickThroughBlocker. La DLL se compila para .NET Framework 4.7.2 con C# 7.3.

Linux:

```sh
export KSP_ROOT='/ruta/a/Kerbal Space Program'
sh build.sh
sh test.sh
```

`DOTNET` permite indicar el ejecutable del SDK. `build.sh` copia la DLL a `APA/Plugins`; si APA está enlazada a GameData, esa es la instalación activa.

Windows, PowerShell:

```powershell
$env:KSP_ROOT = 'C:\Games\Kerbal Space Program'
dotnet build AirParkStability.csproj -c Release
Copy-Item bin/Release/net472/AirParkStability.dll APA/Plugins/AirParkStability.dll
dotnet run --project Tests/LogicTests.csproj -c Release
dotnet run --project Tests/ReloadTests.csproj -c Release
dotnet run --project Tests/AssemblyAudit.csproj -c Release -- $env:KSP_ROOT (Get-Location).Path
```

El proyecto detecta `KSP_x64_Data/Managed` o `KSP_Data/Managed`. También admite `-p:KspRoot=...`, `KspManaged`, `HarmonyPath`, `ClickThroughPath` y `FrameworkPathOverride`.

Las referencias al juego y a los mods usan `Private=false`; no se redistribuyen sus DLL. AirPark y HLAirshipsCore se integran mediante reflexión. Los puntos de integración de AirPark se limitan a la versión 1.7.1.9 auditada.

## Paquete de release

Desde un checkout comprometido:

```sh
python3 tools/package_release.py --output dist
```

El script obtiene los archivos del commit actual, comprueba que solo exista la DLL propia y genera `AirParkStability-<version>.zip` y su SHA256. El ZIP contiene `GameData/APA`, README y documentación pública; excluye fuentes, pruebas, logs, guardados y dependencias.

Antes de publicar otra versión, actualiza `Source/AssemblyInfo.cs` y `APA/AirParkStability.version`, compila, ejecuta las pruebas y crea un commit con la DLL comprobada. GitHub no necesita descargar las bibliotecas de KSP.
