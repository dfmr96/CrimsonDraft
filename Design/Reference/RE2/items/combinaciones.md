# Combinaciones de ítems

> Verificado en `LeonU.exe`: lista en VA `0x547D60`, justo antes de la tabla de atributos.

## Entrada (4 bytes)

`[ítem con el que se combina] [tipo] [ítem resultado] [parámetro]`

## Tipos

| Tipo | Efecto | Ejemplos |
|---|---|---|
| **5** | Pasar cantidad al otro ítem: **recargar** o **juntar pilas** | Handgun + Handgun Ammo; Ammo + Ammo; Ink Ribbon + Ink Ribbon |
| **6** | Igual, en sentido inverso (munición elegida primero) | Handgun Ammo + Handgun |
| **7 / 8** | **Cambiar la granada cargada**: el ID del lanzagranadas cambia | GL (explosiva) + Flame Rounds → GL (incendiaria) |
| **4** | **Fusionar**: se consumen los dos | Handgun + Parts → Custom Handgun; Detonator + C4; Jaguar Stone L + R |
| **11** | **Mezcla de hierbas** | G+G, G+R, G+B, GG+G, GG+B, GR+B, GB+G, GB+R |
| 10 | Granadas explosivas → flame/acid (ningún ítem de Leon lo usa) | — |

## Datos

- La munición de pistola tiene 6 combinaciones: 5 pistolas + ella misma.
- Las mezclas finales (GGG, GGB, GRB) y el First Aid Spray no combinan. **No existe R+B.**
- La munición se carga combinando en cualquier orden; los IDs < 30 van por la rama de equipar/recargar.
