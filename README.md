> **AI involvement disclosure:** ChatGPT-6.1 was involved in the development of this project.

# KSP-APA — AirPark Stability

**AirPark Stability 0.1.2** añade una comprobación de vuelo estacionario a [AirPark Continued](https://github.com/linuxgurugamer/AirPark) para Kerbal Space Program.

## ¿Qué hace?

Antes de aparcar, la nave debe permanecer estable durante **10 segundos consecutivos**, con:

- Velocidad superficial inferior a **1,5 m/s**.
- Velocidad vertical absoluta inferior a **0,5 m/s**.
- Vuelo dentro de una atmósfera y física activa a **1×**.

Si una velocidad supera su límite, el contador vuelve a cero. El panel muestra el progreso y permite cancelar. La nave mantiene su física normal durante la comprobación: sus motores o envolventes deben proporcionar la sustentación.

Al completar la comprobación se utiliza el aparcamiento original de AirPark. Para desaparcar, **Confirm Unpark** aparece en el centro de la pantalla y pausa el juego. Los botones están en vertical: **Cancel** conserva la nave aparcada; **Confirm Unpark** reanuda el juego y desparca.

También corrige la persistencia de posición de AirPark al volver desde Tracking Station y mantiene **AutoPark desactivado** para que no evite las comprobaciones.

## Requisitos

- **Kerbal Space Program 1.12.x**; desarrollado y probado en una instalación de KSP 1.12.5.
- **AirPark Continued 1.7.1.9**, con sus dependencias: ModuleManager, ToolbarController, SpaceTuxLibrary y ClickThroughBlocker.
- **HarmonyKSP**; la integración se ha comprobado con Harmony 2.2.1.0.

**HLAirshipsCore es opcional.** Si lo utilizas, su sustentación sigue funcionando durante la comprobación. Suelta el anclaje HL antes de solicitar PARK.

Las dependencias no están incluidas en la descarga.

## Instalación y uso

1. Instala los requisitos anteriores.
2. Descarga el ZIP de [Releases](https://github.com/Wid4er/KSP-APA/releases).
3. Copia su carpeta `GameData` dentro de la carpeta de KSP. La DLL debe quedar en `GameData/APA/Plugins/AirParkStability.dll`.
4. Reinicia KSP. Usa los controles de AirPark en la barra, el menú de la pieza o un grupo de acción.

Instala una sola copia de APA.

## Configuración

Con KSP cerrado, edita `GameData/APA/Settings.cfg`:

| Parámetro | Valor predeterminado |
| --- | ---: |
| `maxSurfaceSpeed` | 1.5 m/s |
| `maxVerticalSpeed` | 0.5 m/s |
| `requiredSeconds` | 10.0 s |
| `debugLogging` | false |

Usa valores positivos y punto decimal. La atmósfera, el vuelo y la física a 1× siguen siendo obligatorios.

Para compilar o ejecutar las pruebas, consulta [BUILD.md](docs/BUILD.md). El alcance de la validación está en [TESTING.md](docs/TESTING.md).
