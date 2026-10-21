# Cambio de cámara por zonas

> Verificado en `LeonU.exe` y en los datos RVD. Visualizable con el visor y el overlay
> ([herramientas](../herramientas/herramientas.md)).

## Idea

Cada cámara tiene **su propia lista de cuadriláteros en el suelo**. Si el jugador entra en uno, se corta a la cámara de destino de ese cuadrilátero. **Solo se evalúan los de la cámara activa.** El corte es instantáneo, sin interpolación.

## Formato de zona (RVD, 20 bytes)

| Byte | Campo |
|---|---|
| 0 | Activa (0 = desactivada) |
| 1 | Planta (`FF` = cualquiera) |
| 2 | Cámara origen |
| 3 | Cámara destino |
| 4–19 | Cuadrilátero: 4 × (x, z) |

Agrupadas por cámara origen; la lista termina con `FFFFFFFF`.

## Bucle por frame (`0x4C4B60`)

1. Recorre las zonas cuyo origen es la cámara actual (`0x9944EA`).
2. Descarta las desactivadas y las de otra planta (planta del jugador: `0x9945F6`).
3. Prueba punto-en-cuadrilátero con la posición del jugador (`0x994528`) → `0x4C4C40` (signo de productos cruzados).
4. Si coincide, corta a la cámara destino (`0x4C4BE0`): actualiza la proyección, el `LookAt` y el fondo.

## El primer registro de cada cámara

**No es un disparador**: el bucle empieza en `base + 0x14` y se lo salta. Es **el área que cubre la cámara**.
- El juego guarda el área de la cámara actual en `0x992C30`.
- **Algunos enemigos comprueban si su posición cae dentro** (`0x47041B`): saben si están en plano.
- Coincide con la cámara que asigna una puerta → [entrada a sala](entrada-a-sala.md).

## Ejemplo (sala `1000`, cámara 3)

```
[ zona → cam4 ]      área de la cámara 3      [ zona → cam2 ]
x −14400..−7300                                x 200..7400
```

El mismo punto del suelo puede llevar a cámaras distintas según cuál esté activa.

## Control desde los scripts

| Opcode | Efecto |
|---|---|
| `cut_chg(n)` | Corta a `n` y **desactiva el cambio automático** (bit `0x100` de `0x9944D0`) |
| `cut_auto(on)` | Activa o desactiva el cambio por zonas |
| `cut_old` | Vuelve a la cámara anterior (`0x6A0469`) y reactiva el automático |
| `cut_be_set(cám, n, v)` | Activa o desactiva la zona `n` de una cámara |
| `cut_replace(a, b)` | Intercambia dos cámaras en todas las zonas de la sala |

## Pendiente

- Las zonas de ida y vuelta entre dos cámaras **se solapan** en algunas salas (en la `1000`, x 16400–18100). No se comprobó cómo evita el juego el parpadeo ahí. No es una franja de histéresis.
