# Rango final y desbloqueos

> Verificado en `LeonU.exe`: función de rango en `0x4B0980`.

## Cálculo del rango

Se suman **tres puntuaciones** parciales:

1. Tiempo de partida (en minutos).
2. Número de guardados.
3. Número de First Aid Sprays usados.

Umbrales sobre la suma:

| Rango | Puntuación mínima |
|---|---|
| A | ≥ 269 |
| B | ≥ 199 |
| C | ≥ 144 |
| D | ≥ 99 |
| E | menor |

- **Penalización de −50** si está activo el flag `0x9944D0 & 0x800000` (modo con ayudas; deducido).
- **Las hierbas no penalizan**: solo el spray suma al contador (→ [curación](../items/curacion.md)).
- Contador de guardados: `0x998FBC` → [guardado](guardado.md).

## Desbloqueos (rango A o B, más una condición)

| Arma | Condición extra | Flag (`0x9A0188`) |
|---|---|---|
| **Rocket Launcher** (infinito) | Tiempo ≤ 2:30 | `0x20` |
| **SMG** (infinita) | Sin usar sprays; en el escenario B | `0x10` |
| **Gatling Gun** | Sin guardar | `0x40` |

- El flag `0x6942A0` **bloquea los desbloqueos** (modo fácil / modo especial; deducido).
- Bit `0x80`: marca de "nuevo" para mostrar el aviso de desbloqueo.
- Al empezar partida, estos flags meten las armas en el inventario → [munición infinita](../items/municion-infinita.md).
