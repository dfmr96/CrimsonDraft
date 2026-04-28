---
estado: revision
ultima-revision: 2026-04-02
tags:
  - game-design
---

# Sistema de Movimiento

Describe el movimiento de exploración del personaje en tercera persona: velocidades, sprint, animaciones y cámara.

---

## Diseño

### Concepto Central

El movimiento de exploración es cuantizado en 8 direcciones — el personaje siempre mira hacia donde el jugador empuja. Sin strafe, sin apuntar libre. La referencia es Zelda clásico adaptado a un personaje 3D en vista de tercera persona.

Hay dos velocidades: caminar y correr. Correr requiere mantener un botón presionado. Es una elección deliberada, no el estado por defecto.

> Correr no es gratuito. Es una decisión táctica con consecuencias: más ruido, más exposición. El jugador en tensión caminará porque la situación lo exige.

### Controles

| Acción | Teclado | Gamepad |
|---|---|---|
| Mover | WASD / Flechas | Stick izquierdo / D-pad |
| Sprint (mantener) | V | ButtonWest (X Xbox / Cuadrado PS) |
| Interactuar | F / E | Botón Sur |
| Abrir inventario | Tab | Select |
| Pausa | Esc | Start |

No existe input de ratón en ninguna pantalla del juego.

### Velocidades

| Parámetro | Valor | Condición |
|---|---|---|
| `walkSpeed` | 4 unidades/s | Sprint no presionado |
| `runSpeed` | 7 unidades/s | Sprint presionado |

La velocidad se aplica como `Rigidbody.linearVelocity` en `FixedUpdate`. No hay aceleración gradual — el cambio de velocidad es inmediato.

### Cuantización del Input

El input análogo del stick (Vector2 continuo) se procesa según dispositivo:

- **Gamepad:** el vector se normaliza directamente. Permite las 8 direcciones con cualquier ángulo.
- **Teclado:** se aplica `Quantize8Way` — cada eje se redondea a -1, 0 o +1 y el resultado se normaliza. Garantiza que WASD produzca exactamente las 8 direcciones cardinales e intercarddinales.

**Zona muerta:** se descarta cualquier input con `magnitud² < 0.01`. Elimina drift de gamepad y evita transiciones de animación espurias al soltar el stick.

### Física

| Propiedad | Configuración |
|---|---|
| Tipo | Rigidbody 3D, Dynamic |
| Gravedad | Activada |
| Freeze Rotation | X, Y, Z activados |
| Colisionador | CapsuleCollider |

La rotación del personaje se asigna directamente a `transform.forward` — la física no controla la orientación.

---

## Diseño — Animación

### Parámetro del Animator

| Parámetro | Tipo | Valores | Semántica |
|---|---|---|---|
| `Speed` | Float | 0 / 0.5 / 1.0 | 0 = Idle, 0.5 = Walk, 1.0 = Run |

El parámetro se escribe de forma discreta desde `PlayerController` cada `FixedUpdate`. Unity no interpola el valor — se asigna el destino directo.

### Blend Tree — LocomotionBlend

Estado único en el Animator: `LocomotionBlend` (1D Blend Tree). Es el estado default.

| Threshold | Clip | Fuente FBX |
|---|---|---|
| 0.0 | Breathing Idle | `HumanoidBase_Overlapping@Breathing Idle.fbx` |
| 0.5 | Walking | `HumanoidBase_Overlapping@Walking.fbx` |
| 1.0 | Running | `HumanoidBase_Overlapping@Running (1).fbx` |

La interpolación entre clips la gestiona Unity internamente — no hay transiciones de estado explícitas. Al pasar de Walk a Run, el blend tree mezcla ambos clips durante la transición de `Speed`.

---

## Diseño — Cámara

La cámara sigue al jugador mediante **Cinemachine** en tercera persona:

| Componente | Configuración |
|---|---|
| CinemachineCamera | Follow y LookAt apuntan al Transform del Player |
| Binding Mode | World Space |

La cámara mantiene una posición relativa fija al personaje. Sin zoom ni shake en esta fase.

---

## Intención

El movimiento cuantizado y el sprint como elección consciente refuerzan el tono del juego. Caminar es el estado normal de un operativo bajo control. Correr es emergencia.

> En Silent Hill caminas porque tienes miedo. En Crimson Draft caminas porque eres un operativo disciplinado. El sprint es una decisión táctica, no un reflejo.

La diferencia entre walkSpeed (4) y runSpeed (7) es suficiente para sentir urgencia sin convertir el sprint en un dash. El personaje nunca teleporta — el movimiento es siempre legible.

---

## Pendiente

- [ ] Sonido de pasos diferenciado por velocidad (caminar vs correr)
- [ ] Sonido de pasos diferenciado por superficie
- [ ] Animación de agacharse / cubrirse (mecánica de sigilo futura)
- [ ] Detección de interactivo cercano (highlight de objeto)
- [ ] Animación de apertura de puertas

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Combate en Tiempo Real]] | Ver [[Mecanicas de Supervivencia]]
