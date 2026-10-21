# Mapa de memoria: Beta 2 (PlayStation)

> Direcciones de RAM de la Biohazard 2 Beta 2. El ejecutable se carga en `0x80010000`.
> Muchos nombres salen de `MAIN.SYM` → [disco y símbolos](../beta2/disco-y-simbolos.md).

## Variables globales

| Dirección | Contenido |
|---|---|
| `0x800BEB60` | Estructura global `G` (estado del juego) |
| `0x800C250C` | Flags de sistema |
| `0x800C250C` bit `0x2000` | "Empujando este frame" (lo pone `Om_move`, lo lee el jugador) |
| `0x800C252C` | Estructura del jugador (`G + 0x39CC`) |
| `0x800C2710` | Objeto sobre el que está subido el jugador (`+0x1E4`) |
| `0x800C2740` | ≠ 0 → estado Caution (probablemente veneno) |
| `0x800C2744` | Puntero al jugador |
| `0x800C2748` | Puntero al compañero |
| `0x800C274C` | 16 punteros a enemigos (vacío = `0x800C6B7C`) |
| `0x800C2C54` + i·`0x1F8` | Objetos de la sala (`G + 0x40F4`; fin en `[0x800C6B54]`) |
| `0x800C0C9C` | Puntero al RDT cargado (`+0x20` → SCA) |
| `0x800C9C40` | Última entrada de escalera del SCA tocada |
| `0x800C73D8` | Puertas |
| `0x800C6F08` / `0x800C6F0A` / `0x800C6F0C` | Etapa / sala / cámara |
| `0x800D1878` | Estructura `Game` (estado del juego) |

## Tablas de datos

| Dirección | Contenido |
|---|---|
| `0x80010FDC` | Tabla de saltos de `Member_set` (nombres de campos de entidad) |
| `0x8001087C` | Tabla de saltos de `mess_trans` (códigos `EE`–`FE`) |
| `0x80093C20` | Nombres de ítems (1233 bytes) |
| `0x800940F6` | Offsets de los nombres (136 × `u16`) |
| `0x80094206`+ | Mensajes del sistema en japonés (pregunta de recoger, etc.; sin traducir) |
| `0x8009CB38` | **Atributos de ítems** |
| `0x80097B78` / `0x80097BA8` | Estados del jugador: `pl_mvXX_pad` / `pl_mvXX` |
| `0x80010CAC` | Giro por estado de salud (40, 32, 16) |
| `0x800960D4` / `0x800960FC` | Distancias de prueba para subir / bajar |
| `0x8009A614` / `0x80010E9C` | Formas del SCA: resolver / comprobar |
| `0x8009DD00`–`0x8009DD66` | Tablas UV del menú de comandos (la principal en `0x8009DD40`) |

## Funciones

| Dirección | Función |
|---|---|
| `0x80030F88` | Manejador de `F9` (color del texto) |
| `0x8003A6C4` | `Pl_move` (despachador de estados del jugador) |
| `0x80034D44` | `Om_move` (objetos: empuje, caída) |
| `0x80035CAC` | `Oma_ck_oba` (personaje contra objeto) |
| `0x80036204` | `Oma_ck_front` (¿de frente al objeto?) |
| `0x80036364` | `Oma_pl_updown_ck` (subir / bajar) |
| `0x80048644` | `Sca_ck_hit` (colisión del escenario) |
| `0x800156CC` / `0x80016280` | `Add_speedXZ` / `Spd_get` (movimiento por animación) |
| `0x80050AB0` | `Cut_chg_main` (cambio de cámara) |
| `0x8006BD3C` | `St_disp_num` |
| `0x8006C0DC` | `print_num` |
| `0x80086E70` | `GetClut` |
| `0x80086F60` | `AddPrim` |
| `0x800136A4` | Opción JUMP del menú de depuración |
| `0x80013B10` | Stub vacío del resto de opciones del menú de depuración |

## Lectura en vivo

Con el servidor GDB de DuckStation → [conexión](../herramientas/duckstation-gdb.md). La RAM de PS1 es de 2 MB (`0x80000000`–`0x801FFFFF`).
