---
estado: borrador
ultima-revision: 2026-03-05
tags:
  - game-design
---

# Sistema ECG de Operadores

Este documento define el ECG táctico para lectura de salud en UI. El alcance de este MVP es un solo operador y solo señales de **HP + BPM**.

---

## Diseño

El ECG se implementa como un **widget de Canvas** que dibuja una línea dinámica. El sistema comunica estado del operador sin barra de vida explícita, alineado con [[Sistema de Salud]].

### Alcance del MVP

| Variable | Incluida en MVP | Nota |
|---|---|---|
| HP (`hp`, `hp_max`) | Sí | Afecta color y amplitud |
| BPM | Sí | Afecta velocidad del trazo |
| Presión (`systolic/diastolic`) | No | Se integra en fase posterior |
| Hemorragia | No directa | Solo impacta de forma indirecta cuando altere HP/BPM |

### Modelo visual

| Canal visual | Entrada | Regla |
|---|---|---|
| Color del trazo | `hp_ratio` | Verde → amarillo → rojo |
| Amplitud | `hp_ratio` | Disminuye al bajar HP |
| Velocidad horizontal | `bpm` | `beat_ms = 60000 / bpm` |
| Estado inactivo | `isActive` | Panel apagado, sin trazo |

### Reglas numéricas del MVP

| Parámetro | Valor |
|---|---|
| Rango BPM | `60..160` (clamp) |
| Ventana visible | `2.5` latidos en el ancho del widget |
| Amplitud | `amp = h * (0.12 + 0.30 * hp_ratio)` |
| Umbral color 1 | `hp_ratio > 0.6` = verde |
| Umbral color 2 | `0.3 < hp_ratio <= 0.6` = gradiente verde→amarillo |
| Umbral color 3 | `hp_ratio <= 0.3` = gradiente amarillo→rojo |
| Pixelado inicial | cuantización `2 px` en X/Y |
| Grosor de línea | `2 px` |

### Curva base del latido

La forma de onda replica la lógica del prototipo para mantener continuidad de lectura por parte del jugador.

```text
function ecg_sample(phase_0_1):
  if 0.10 < phase < 0.14: return -sin(...) * 0.20
  if 0.14 < phase < 0.19: return  sin(...) * 1.00
  if 0.19 < phase < 0.24: return -sin(...) * 0.25
  if 0.32 < phase < 0.52: return  sin(...) * 0.28
  return 0.0
```

### API de UI (nivel diseño)

| Método | Propósito |
|---|---|
| `SetVitals(hpRatio, bpm, isActive)` | Actualiza estado visual del ECG |
| `SetPixelStyle(stepPx, thicknessPx)` | Ajusta estilo retro/pixelado |

### Criterios de aceptación del MVP

1. El ECG se dibuja dinámicamente en un `GameObject` dentro del Canvas.
2. El ritmo visual cambia en tiempo real al modificar BPM.
3. El color y la amplitud cambian al modificar HP.
4. El estado inactivo apaga el trazo.
5. El look pixelado es consistente con HUD retro del proyecto.

---

## Intención

El ECG no es decoración. Es el canal principal de lectura fisiológica del operador.

> El jugador no debe pensar en números de HP, debe sentir urgencia clínica en la interfaz.

Este MVP valida tres cosas antes de agregar presión arterial:
- Que la animación de latido se percibe creíble.
- Que el código visual por color transmite gravedad en menos de un segundo.
- Que el estilo pixelado convive con la estética general del HUD.

La presión arterial se incorpora después para habilitar la lectura avanzada de shock y la contradicción deliberada de diseño: operador aparentemente “verde” pero hemodinámicamente inestable.

---

## Pendiente

- [ ] Integrar presión arterial (`systolic/diastolic`) al widget.
- [ ] Definir comportamiento de línea plana para muerte por shock.
- [ ] Escalar de 1 operador MVP a vista multi-operador.
- [ ] Vincular el widget al sistema real de salud (sin driver de debug).

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Salud]] | Ver [[Sistema de Inventario]] | Ver [[Diseño de Combate y Armas]]
