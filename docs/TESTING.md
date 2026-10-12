# Validación

El usuario informa de funcionamiento correcto en su instalación personal de KSP. Esto no implica validación exhaustiva con todas las naves, cuerpos celestes o combinaciones de mods.

La versión 0.1.2 dispone de:

- **16 grupos de pruebas de lógica:** estabilidad, umbrales, pausas y warp, muestras inválidas, permisos de transición y pausa de confirmación.
- **8 grupos de regresiones de recarga:** posición cero, altitud, carga incompleta, cambios de origen y rotación, rails, UNPARK y serialización. Usan modelos de coordenadas de KSP/Unity, no una simulación real del juego.
- **44 comprobaciones de ensamblados:** APIs y rutas de AirPark/KSP, persistencia, ancla, pausa y ausencia de escrituras de movimiento por el addon.

Ejecuta `sh test.sh` con `KSP_ROOT` configurado. La compilación debe terminar sin errores ni avisos.

En una instalación nueva, comprobar PARK desde toolbar, menú y grupo de acción; reinicio del contador al superar ambos límites; cancelación; recarga desde Tracking Station; confirmación centrada con pausa; Cancel; Confirm Unpark; y AutoPark bloqueado.

El addon conserva la restauración de velocidad original de AirPark. Los cambios de referencia, gran altitud y cuerpos de mods necesitan pruebas específicas. La corrección de posición no puede recuperar una nave ya guardada en una ubicación geográfica incorrecta; en ese caso se necesita un guardado anterior válido.
