# Curación ("Usar" un curativo)

> Verificado en `LeonU.exe`: `switch` sobre los IDs 35–46 en `0x4FE8A5` (tabla de saltos `0x4FEA8C`).

La curación es un **porcentaje de la vida máxima** (`0x994652`) y se suma a la vida actual (`0x994646`) con tope en el máximo (`0x4FEA3F`).

| Ítem | Cura | Veneno | Nota |
|---|---|---|---|
| First Aid Spray | 100% | — | **Suma 1 al contador de sprays (penaliza el rango)** |
| Green Herb | 25% | — | |
| Red Herb | — | — | No se puede usar sola (mensaje) |
| Blue Herb | 0 | Cura | |
| G+G | 50% | — | |
| G+R | 100% | — | Mismo efecto que el spray, sin penalizar el rango |
| G+B | 25% | Cura | |
| G+G+G | 100% | — | |
| G+G+B | 50% | Cura | |
| G+R+B | 100% | Cura | |

- Veneno: estado en `0x994708` (se pone a 0 al curar).
- IDs < 30 (armas, munición) → otra rama del mismo despachador: **"Usar" se decide por rango de ID**.
- Plantas de hierba azul en el escenario: se usan en el sitio con Sí/No, sin pasar por el inventario (8 salas).
