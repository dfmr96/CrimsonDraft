# Guardado (máquina de escribir + Ink Ribbon)

> Verificado en `LeonU.exe`.

## Recurso

- **Ink Ribbon** (ID 21): pila de hasta 250; cada máquina de escribir consume 1.
- Es un AOT normal con Sí/No: "Will you use the Ink Ribbon?" → [AOT](../interaccion/aot.md).
- 28 máquinas de escribir y 25 baúles en las salas de Leon (recuento por tipo de AOT).

## Flujo en el código

1. Al aceptar, **la tinta se gasta antes de elegir ranura** (`0x4C5FCE`).
2. Antes de eso, el inventario se copia a una copia de seguridad (`0x999334`).
3. Si el jugador **cancela** en la pantalla de ranuras, se restaura la copia (`0x4C6397`): **no se pierde tinta**.
4. El contador de guardados (`0x998FBC`) alimenta el [rango](rango-y-desbloqueos.md). Que solo suba al guardar de verdad (y no al cancelar) es deducido, no verificado.

Qué se guarda (bloque de `0x598` bytes con los flags de progreso, el inventario y la posición) y en qué archivo → [hitos](hitos.md).

## Observación de diseño

El patrón "gastar primero, restaurar si cancela" evita tener que pasar el ítem por la pantalla de guardado: la pantalla solo sabe "guardar" o "volver".
