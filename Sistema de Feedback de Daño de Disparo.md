---
estado: borrador
ultima-revision: 2026-03-04
tags:
  - game-design
---

# Sistema de Feedback de Daño de Disparo

Define cómo se presenta en UI el resultado de cada bala en combate: número de daño en impactos y texto `MISS` en fallos, anclado al `ShotMarker`.

---

## Diseño

### Objetivo del sistema

Entregar feedback inmediato y legible del resultado del disparo sin abrir paneles extra ni mover la atención fuera del área de apuntado.

### Regla de aparición

El feedback se crea **al resolver el disparo**, nunca al crear el `ShotMarker`.

| Momento | Resultado |
|---|---|
| Se crea `ShotMarker` | No aparece texto |
| Se resuelve disparo con impacto | Aparece número de daño |
| Se resuelve disparo con fallo | Aparece `MISS` |

### Ubicación y anclaje

El feedback aparece en **UI del AimView** y se posiciona relativo al `ShotMarker` del disparo resuelto.

| Variable | Valor de diseño |
|---|---|
| Espacio de render | UI (`AimView`) |
| Referencia de posición | `ShotMarker` del disparo |
| Offset inicial recomendado | `(0, +24)` px |
| Límite de textos simultáneos | 3 |

Cuando se supera el límite, el texto más antiguo se recicla o se elimina primero.

### Formato del texto

| Caso | Texto | Color sugerido |
|---|---|---|
| Impacto (`ShotZone != Miss`) | `-{daño_final}` | Blanco cálido |
| Fallo (`ShotZone = Miss`) | `MISS` | Gris claro |

Reglas de formato:
- No mostrar `CRIT` en esta iteración.
- El daño siempre se muestra como entero.
- El signo visual recomendado es prefijo negativo (`-18`) para reforzar pérdida de vida enemiga.

### Animación mínima (MVP)

Cada texto flotante sigue la misma secuencia temporal:

1. Aparece con opacidad 100%.
2. Se desplaza hacia arriba.
3. Desvanece hasta 0%.
4. Se destruye o vuelve al pool.

| Parámetro | Valor recomendado |
|---|---|
| Duración total | `0.60 s` |
| Desplazamiento vertical | `+18 px` |
| Escala inicial | `1.0` |
| Escala final | `1.0` |

### Reglas funcionales

- Si el disparo impacta y el daño final es `0`, igual se muestra `-0` para trazabilidad de reglas.
- Si el disparo es `Miss`, nunca se muestra número de daño.
- Cada disparo resuelto genera como máximo un texto.
- El sistema de feedback no altera cálculo de daño ni estado de combate.

### Casos borde

| Caso | Comportamiento esperado |
|---|---|
| Dos disparos resueltos en frames consecutivos | Ambos textos aparecen, respetando orden de resolución |
| El `ShotMarker` ya no está visible al resolver | El texto usa la última posición válida conocida del marker |
| Se alcanza límite de 3 textos | Se elimina/recicla el más antiguo |
| Cierre de combate durante animación | Los textos activos se limpian al cerrar la vista |

### Relación con otros sistemas

- Lee zona de impacto desde [[Sistema de Detección de Impacto]].
- Consume daño final desde [[Sistema de Combate en Tiempo Real#Salud de Enemigos (MVP)]].
- Vive dentro del flujo visual del [[Sistema de Dispersion y Apuntado]].

---

## Intención

> El jugador debe entender en menos de un segundo si esa bala cambió el estado del combate o se perdió.

El texto flotante cerca del `ShotMarker` mantiene el feedback en el mismo foco perceptivo del QTE. Esto reduce carga cognitiva y evita que el jugador busque información en otros paneles.

Mostrar `MISS` explícito vuelve legible el costo de error: una bala gastada sin efecto. Mostrar número en impactos confirma que la decisión sí produjo progreso.

No incluir `CRIT` en esta etapa evita ruido semántico hasta que exista un sistema crítico formal en el diseño de armas.

---

## Pendiente

- [ ] Definir paleta final (valores exactos) para accesibilidad y contraste en fondos claros/oscursos
- [ ] Definir si el tamaño de fuente escala con distancia percibida del objetivo en iteraciones futuras
- [ ] Validar legibilidad del texto con jitter visual alto en operadores heridos

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Combate en Tiempo Real]] | Ver [[Sistema de Detección de Impacto]] | Ver [[Sistema de Dispersion y Apuntado]]
