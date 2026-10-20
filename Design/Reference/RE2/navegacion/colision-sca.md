# Colisión del escenario (SCA)

> Verificado en la Beta 2: `Sca_ck_hit` (`0x80048644`), sus tablas de formas y el SCA de la sala `103` leído en RAM. El formato coincide con los RDT de PC (`re2sca.py`).
> Nombres de función de `SCA.C` (`MAIN.SYM`).

## Qué es

**SCA** es la **colisión fija de la sala**: la lista de formas que bloquean a personajes y objetos (paredes, columnas, muebles, bordes, rampas, escaleras). Es el nombre del bloque 6 del RDT y del archivo de código que lo procesa en la Beta (`SCA.C`, funciones `Sca_*`).

- **Las siglas no aparecen desarrolladas** en nada de lo revisado (símbolos, cabeceras, rutas).
- Hipótesis (no verificada): "*SCene Atari*". *Atari* (当たり) es "colisión" en la jerga de Capcom (`Disp_atari`, "OBJ ATARI DISP" en el [menú de depuración](../beta2/menu-debug.md)), y el archivo de los AOT se llama `SCE_AT.C`.
- Los objetos móviles (cajas, escaleras de mano) **no** están en el SCA: llevan su propia caja de colisión → [objetos empujables](objetos-empujables.md).

## Dónde está

Bloque **6** del RDT → [RDT](../formatos/rdt.md). En la Beta, el puntero se obtiene en RAM como `[[0x800C0C9C] + 0x20]`.

## Cabecera (16 bytes)

| Offset | Campo |
|---|---|
| `+0` / `+2` | `Cx`, `Cz` (s16): centro de la sala para dividirla en cuadrantes |
| `+4` | `u32` número de registros **incluida la cabecera** (entradas = n − 1) |
| `+8` | 8 bytes sin identificar (el primero parece un s32 negativo, ¿techo?) |

## Entrada (16 bytes)

| Offset | Campo |
|---|---|
| `+0` / `+2` | X, Z de la esquina (s16) |
| `+4` / `+6` | Ancho (X), fondo (Z) (u16) |
| `+8` | **Tipo**: 4 bits bajos = **forma**; bits altos = con qué choca (máscaras que pasa el que llama: `tipo & incluir ≠ 0` y `tipo & excluir = 0`) |
| `+0xA` | **Cuadrantes** que toca (4 bits bajos); los bits altos (`0x9xxx`) no se usan aquí |
| `+0xC` | **Máscara de plantas** (bit n = planta n) |

## Formas (`tipo & 0xF`)

| Valor | Forma | Función de choque | Resultado de la comprobación |
|---|---|---|---|
| 0 | Caja | `Sca_hit_box` | Sólido |
| 1–4 | Triángulo rectángulo (4 orientaciones) | `Sca_hit_naname_a`…`d` | Según la forma |
| 5 | Rombo (*hishi*) | `Sca_hit_hishi` | Según la forma |
| 6 | Círculo | `Sca_hit_circle` | Según la forma |
| 7 / 8 | Óvalo alargado en X / en Z (*koban*) | `Sca_hit_koban_x` / `_z` | Sólido |
| 9 | Caja: **borde al que se puede subir** | `Sca_hit_box` | Sólido + bit `2` |
| 10 | Caja: **desnivel para bajar o caer** | `Sca_hit_box` | Bit `4` |
| 11 | **Rampa** | `Sca_hit_slope` | Bit `8` (altura con `Sca_get_slope_high`) |
| 12 | **Escalera** | `Sca_hit_box` | Bit `0x10`; guarda la entrada en `0x800C9C40` para `Sca_hit_stairs` |
| 13 | Curva | `Sca_hit_curve` | — |

Los bits 2 y 4 son los que usa [subir y bajar](subir-y-bajar.md); el bit 4 también hace caer a las [cajas empujadas](objetos-empujables.md).

## Algoritmo (`Sca_ck_hit`)

1. **Fase amplia por cuadrantes** (`Sca_get_area`): `bit = 1 << ((x < Cx) + 2·(z < Cz))`. Solo se prueban las entradas cuyo campo de cuadrantes contiene ese bit.
2. Para cada entrada: rectángulo envolvente ampliado con el radio del personaje (`+0x90` / `+0x92` de la entidad); si el radio X y Z difieren, se interpola según el ángulo hacia la entrada (`ratan2` + `rcos`/`rsin`).
3. Filtros: tipo (incluir/excluir) y planta (`1 << planta`).
4. **Modo resolver**: llama a la función de la forma, que empuja al personaje fuera; el desplazamiento se aplica a la posición (`+0x38`/`+0x40`) y se marca el bit `0x4000` en la entidad.
5. **Modo comprobar**: solo acumula los bits de la tabla anterior y devuelve la suma.

Los objetos móviles **no están en el SCA**: llevan su propia caja en la entidad → [objetos empujables](objetos-empujables.md).

## Otras funciones de `SCA.C`

| Función | Uso |
|---|---|
| `Sca_get_ground` | Altura del suelo en un punto (rampas, escaleras, curvas, objetos) |
| `Sca_ck_line` | **Línea de visión contra el escenario**. La usan el apuntado (`Weapon_at_ck`), la cabeza del jugador (`Pl_neck`), el límite al retroceder (`Lim_dir_ck`), enemigos (`Em_ck`, `Root_ck`) y el sonido 3D |
| `Sca_get_dir` / `Sca_get_pos` (`_super`) | Dirección y posición sobre superficies especiales |

## Ejemplo: sala `103` de la Beta (la de la escalera de mano)

Centro (−23600, −20801), 17 entradas: cajas (paredes), óvalos y círculos (columnas o muebles) y cajas con máscaras de planta altas (`0x1FF`) para la zona de la escalera.
