# Daño de las armas

> Verificado en `LeonU.exe`: una única función aplica el daño a todos los enemigos, **`0x4D7720`**.

## Algoritmo

```
tabla    = punteros[0x543C48 + tipo_enemigo * 4]      ← una tabla de daño POR TIPO DE ENEMIGO
registro = tabla + (id_arma - 1) * 20                 ← 20 bytes por arma (IDs 1–19)
daño     = (registro.dword0 >> (10 * nivel)) & 0x3FF   ← 3 valores de 10 bits empaquetados
vida    -= daño   →   vida < 0 ? estado 3 ("morir") : estado 2 ("herido")
```

- **El daño depende de la combinación arma × enemigo**, no solo del arma.
- **Nivel (0–2):** lo elige quien llama. La explosión de la granada usa el nivel 1 (onda expansiva); el impacto directo, el 0. En pistola y escopeta los valores bajan con el nivel, lo que indica **distancia**: el bonus de la escopeta a quemarropa.
- Los otros 4 dwords de cada registro no se identificaron (probablemente retroceso o probabilidad de aturdir).
- Tipo de enemigo: `+0x08` de la entidad. Lista de enemigos activos: `0x994714`.
- La función de daño decide también la transición de estado → [IA](ia.md).

## Matriz (nivel 0 / 1 / 2; un solo número si coinciden)

| Enemigo | Knife | Handgun | Magnum | C.Magnum | Shotgun | C.Shotgun | GL Expl | GL Acid | GL Flame | Bowgun | Spark | SMG | Flamethr | Rocket | Gatling |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Zombi (todos) | 3 | 16/15/14 | 900 | 900 | 200/60/40 | 300/80/60 | 200/50/10 | 200/50/10 | 200/50/5 | 30 | 60 | 4 | 15 | 900 | 8 |
| Zombie Dog | 10 | 18/16/15 | 200 | 300 | 59/40/30 | 300/75/65 | 300/50/10 | 300/50/10 | 300/50/5 | 26 | 65 | 10 | 12 | 300 | 18 |
| Crow | 15 | 15 | 200 | 300 | 60/20/15 | 120/40/30 | 60/60/15 | 60/60/15 | 60/60/15 | 15 | 60 | 15 | 15 | 300 | 30 |
| Licker (Red) | 8 | 14/13/12 | 200 | 300 | 50/45/40 | 110/95/80 | 60/60/20 | 130/60/10 | 85/60/5 | 20 | 70 | 11 | 12 | 200 | 20 |
| Licker (Grey) | 8 | 14/13/12 | 80 | 100 | 35/33/30 | 60/55/50 | 60/60/10 | 100/100/15 | 80/60/5 | 20 | 50 | 11 | 11 | 200 | 20 |
| Alligator | 10 | 17/16/15 | 31 | 50 | 27/25/24 | 50/45/40 | 30 | 25 | 31/31/5 | 20 | 30 | 10 | 10 | 110 | 18 |
| Spider | 15 | 17/16/15 | 130 | 150 | 55/45/40 | 130/100/90 | 60/60/20 | 90/60/10 | 130/130/5 | 20 | 70 | 10 | 10 | 200 | 18 |
| G Embryo / G Adult | 15 | 20/19/18 | 105/100/95 | 120 | 65/60/55 | 80/75/70 | 60 | 121/121/10 | 80/80/5 | 18 | 60 | 10 | 3 | 201 | 17 |
| Cockroach | 15 | 16/15/14 | 200 | 300 | 300/40/20 | 300/60/40 | 60/60/15 | 60/60/10 | 60/60/10 | 15 | 40 | 7 | 40 | 300 | 7 |
| Tyrant 1 | 10 | 16 | 81/78/75 | 100 | 50/45/40 | 80/75/70 | 50 | 81/81/10 | 60/60/5 | 16 | 70 | 13 | 20 | 500 | 24 |
| Tyrant 2 | 10 | 16 | 80/78/75 | 100 | 50/45/40 | 80/75/70 | 50 | 101/101/10 | 60/60/5 | 16 | 60 | 13 | 20 | 500 | 24 |
| Zombie Arms | 5 | 16/15/14 | 300 | 300 | 300/30/20 | 300/50/30 | 60/60/20 | 60/60/10 | 60/60/10 | 20/17/15 | 40 | 7 | 40 | 40 | 14 |
| Ivy / Vines | 7 | 7 | 50/48/45 | 100 | 30/29/28 | 60/59/58 | 40 | 70/70/10 | 300/110/10 | 10 | 50 | 5 | 15 | 300 | 10 |
| Birkin 1 / 3 | 15 | 16/15/14 | 90/88/85 | 100 | 60/55/51 | 90/85/80 | 60 | 101/101/10 | 70/70/5 | 17 | 80 | 8 | 4 | 200 | 16 |
| Birkin 2 | 18 | 21/20/19 | 90/88/85 | 105 | 70/65/60 | 90/85/80 | 60 | 80/80/10 | 100/100/5 | 17 | 70 | 10 | 5 | 300 | 18 |
| Birkin 4 | 20 | 27/26/25 | 101/98/95 | 120 | 70/65/60 | 100/95/90 | 60 | 80/80/10 | 101/101/5 | 19 | 80 | 12 | 8 | 400 | 20 |
| Birkin 5 | 20 | 29/28/27 | 101/98/95 | 120 | 70/65/60 | 100/95/90 | 90 | 60/60/10 | 70/70/5 | 19 | 60 | 9 | 5 | 301 | 20 |
| Giant Moth | 10 | 11/10/9 | 60 | 100 | 40/38/35 | 70/65/60 | 50 | 60/60/10 | 101/101/5 | 10 | 40 | 10 | 12 | 300 | 20 |
| Maggots | 15 | 15 | 60 | 100 | 60/40/30 | 80/60/50 | 40 | 40/40/10 | 40/40/10 | 20/16/15 | 40 | 7 | 7 | 300 | 14 |

Handgun = las cinco pistolas (mismos valores). El cuchillo solo tiene el nivel 0. Volcado con `re2dmg_dump.py` / `re2dmg_table.py`.

## Lectura de diseño

- **La Magnum mata a cualquier zombi de un disparo (900)**, pero contra el cocodrilo solo hace 31: ese jefe se resuelve con el entorno.
- **Ivy es débil al fuego**: granada incendiaria 300 de impacto directo frente a 7 de la pistola.
- **El lanzallamas apenas daña al G (3) ni a Birkin (4–8)**: no hay un arma "mejor" en general.
- **Birkin recibe más daño de pistola en cada forma** (16 → 21 → 27 → 29): las formas finales premian al jugador que llega sin munición pesada.
- **El Rocket Launcher no siempre mata**: 500 contra el Tyrant, 40 contra los brazos de zombi.
