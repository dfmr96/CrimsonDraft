# Mapa de memoria: PC (`LeonU.exe`, GOG)

> VA con base `0x400000`. Para pasar a offset de archivo: `.text` = VA − `0x400000`; `.data` empieza en VA `0x522000` (archivo `0x120400`) → [ejecutables](../formatos/ejecutables.md).
> Todo verificado salvo lo marcado como deducido.

## Variables globales

| Dirección | Contenido |
|---|---|
| `0x9944D0` | Flags del estado de juego (bit `0x100` = cambio de cámara bloqueado; `0x800000` = penalización de rango, deducido) |
| `0x9944EA` | Cámara actual |
| `0x9944EB` | Variable de progreso (zombis +15 si < 4) |
| `0x9944EE` | Flags de acción del jugador (la percepción enemiga usa `& 0x15` / `& 0x17`) |
| `0x9944F0` | **Estructura del jugador** → [entidad](../enemigos/estructura-entidad.md) |
| `0x994528` | Posición del jugador (`+0x38`) |
| `0x9945F6` | Planta del jugador (`+0x106`) |
| `0x994646` | Vida actual (`+0x156`) |
| `0x994652` | Vida máxima (`+0x162`) |
| `0x994708` | Veneno |
| `0x994714` | Lista de enemigos |
| `0x992C30` | Área (cuadrilátero) de la cámara actual |
| `0x992E50` | 32 punteros a AOT de la sala |
| `0x998FBC` | Contador de guardados |
| `0x998FC4` | Campo de bits de armas infinitas |
| `0x999114` / `0x999116` / `0x999118` | Etapa / sala / cámara de entrada |
| `0x999334` | Copia de seguridad del inventario (guardado) |
| `0x999360` | **Inventario** (casillas de 4 bytes) |
| `0x9A0188` | Flags persistentes de desbloqueo (`0x10` SMG, `0x20` Rocket, `0x40` Gatling, `0x80` nuevo) |
| `0x6942A0` | Modo especial (bloquea desbloqueos, multiplica vida enemiga) |
| `0x6901D4` | Índice del detector del truco de botones |
| `0x69C540` | Interruptor del truco de munición infinita |
| `0x6A0469` | Cámara anterior (`cut_old`) |
| `0x688370` | Buffer con el nombre del archivo de sala (plantilla en `0x544B64`) |

## Tablas de datos

| Dirección | Contenido |
|---|---|
| `0x547D60` | Listas de combinaciones de ítems |
| `0x547EA8` | **Atributos de ítems** (8 bytes × ID) |
| `0x544DE8` | Manejadores de opcodes SCD (143) |
| `0x543C48` | Punteros a tablas de daño por tipo de enemigo |
| `0x52EFB8` / `0x52EFD8` | Vida del zombi (normal / fácil) |
| `0x52F188` | Estados principales del zombi |
| `0x52F1B0` | Patrones del zombi |
| `0x52F1E8` / `0x52F228` | Subestados del zombi (decidir / ejecutar) |
| `0x52FE18`, `0x530538`, `0x532270` | Tablas de vida de otros enemigos |

## Funciones

| Dirección | Función |
|---|---|
| `0x449070` | `SetProjection(H)` |
| `0x5093F0` | `LookAt` |
| `0x4C46E9` | Aplicar cámara (H = lente >> 7) |
| `0x4C4600` | Cargar sala / cámara de entrada |
| `0x4C4B60` | Bucle de cambio de cámara por zonas |
| `0x4C4BE0` | Cortar a una cámara |
| `0x4C4C40` / `0x4C4CF0` | Punto en cuadrilátero / en rectángulo |
| `0x4E7650` | Vaciar AOT al entrar en sala |
| `0x4E7AF0` | Manejador `nop` (también `break_point`) |
| `0x4E86C0` / `0x4E8720` / `0x4E8790` | `cut_chg` / `cut_old` / `cut_auto` |
| `0x4E87D0` / `0x4E8860` / `0x4E8990` | `cut_replace` / `cut_be_set` / `door_aot_se` |
| `0x4EC78B` | Opcode `heal` |
| `0x4EDA1F` | Bucle del agua (prueba de AOT por bit `0x80`) |
| `0x4B0980` | Rango final y desbloqueos |
| `0x4B13FB` | Armas de recompensa al empezar partida |
| `0x4C5FCE` / `0x4C6397` | Guardado: gastar tinta / restaurar al cancelar |
| `0x4D7720` | **Aplicar daño** |
| `0x4D8FA6` | Vida máxima por personaje (TOFU 400) |
| `0x4E2171` | Cambio a la compañera |
| `0x4EC48B` | Sherry inmortal como PNJ |
| `0x4FE8A5` | "Usar" curativo (switch IDs 35–46) |
| `0x5081EC` | Copiar bits de infinito a la tabla de atributos |
| `0x50787D` | Gatling infinita al empezar |
| `0x50818E` | Proyección del inventario 3D (H = 208) |
| `0x5092E0` | Detector del truco de botones |
| `0x449590` | Zombi: estado 0 (aparecer, vida) |
| `0x44A110` / `0x44A140` / `0x44A180` | Zombi: comportamiento / patrón par / patrón impar |
| `0x44A1C0` | Zombi: percepción |
| `0x44D3D0` / `0x450310` | Zombi: daño / muerte |
| `0x4ABBA0` | Prueba de cono (ángulo) |
| `0x47041B` | Enemigo comprueba si está dentro del área de cámara |
