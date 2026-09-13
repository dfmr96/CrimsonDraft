---
estado: revision
ultima-revision: 2026-09-01
tags:
  - game-design
---

# Sistema de Movimiento

Describe el movimiento de exploración del personaje en tercera persona: esquemas de control (Modern / Classic), velocidades, sprint, animaciones y cámara.

---

## Diseño

### Concepto Central

El juego usa **cámaras fijas por zona/sala**, en la línea de Resident Evil clásico — no una cámara que sigue libremente al jugador. Sobre esa base de cámaras fijas, el jugador elige entre dos esquemas de control desde Settings, intercambiables en cualquier momento (incluso a mitad de partida, desde el menú de Pausa):

- **Modern** — movimiento relativo a la cámara activa. Es el esquema por defecto.
- **Classic** — Tank Controls, movimiento relativo al propio personaje.

Ambos esquemas leen el mismo input físico (stick / WASD); lo único que cambia es cómo se interpreta ese vector. No hay strafe libre ni apuntado libre del movimiento en ninguno de los dos esquemas.

> La referencia directa para ambos esquemas es el control "Alternativo/Moderno" y el control "Original/Tank" del remaster de Steam de Resident Evil (2015) — el mismo juego ofrece los dos, intercambiables desde el menú, sobre el mismo sistema de cámaras fijas.

Hay dos velocidades: caminar y correr. Correr requiere mantener un botón presionado. Es una elección deliberada, no el estado por defecto.

> Correr no es gratuito. Es una decisión táctica con consecuencias: más ruido, más exposición. El jugador en tensión caminará porque la situación lo exige.

### Esquema Modern (implementado)

El personaje siempre mira hacia donde se mueve. La dirección de movimiento se calcula contra la cámara fija activa: adelante del stick es la normal de vista de la cámara (proyectada al plano horizontal), no un eje de mundo fijo.

**El problema que resuelve la base "sostenida":** con múltiples cámaras fijas cubriendo la misma sala, si la dirección de movimiento se recalculara contra la cámara nueva apenas ocurre el corte, sostener el stick en la misma posición durante un cambio de cámara podría invertir la dirección de marcha del personaje sin que el jugador haya tocado nada.

**Solución — "norte" sostenido:** mientras el jugador mantenga el vector del stick aproximadamente igual (dentro de una zona muerta angular), la base de movimiento (la cámara contra la que se calcula "adelante") se conserva sin importar cuántos cortes de cámara ocurran mientras tanto. Recién cuando el vector sostenido cambia más allá de esa zona muerta — o cuando se suelta el stick y se vuelve a presionar — el sistema vuelve a medir contra la cámara que esté activa en ese instante y fija una nueva base.

| Parámetro | Valor | Nota |
|---|---|---|
| Zona muerta de cambio de dirección | 15° | Placeholder, pendiente de pasada de feel. Por debajo de este ángulo el sistema lo trata como jitter, no como un giro deliberado |

### Esquema Classic — Tank Controls (implementado)

Movimiento relativo al propio personaje, no a la cámara. El jugador gira al operador sobre su propio eje y avanza/retrocede en la dirección hacia la que ya está mirando.

- **Eje horizontal del stick:** gira al personaje sobre su eje Y a una velocidad angular fija (proporcional a la deflexión en gamepad; a tasa completa en teclado). Girar en el lugar sin tocar el eje vertical es válido — el personaje rota sin desplazarse.
- **Eje vertical del stick:** avanza en la dirección que el personaje ya está mirando (adelante) o retrocede (atrás). No hay strafe.
- **Retroceder siempre es caminando** — nunca corriendo, aunque Sprint esté presionado. Fiel al REmake original: retroceder rápido de un enemigo nunca fue una opción en la saga clásica.
- **Apuntando, el personaje no gira.** El eje horizontal del stick se ignora por completo mientras el jugador tiene el arma en alto — evita que el cuerpo rote solo mientras se apunta.

| Parámetro | Valor | Nota |
|---|---|---|
| Velocidad de giro | 180°/s | Placeholder, pendiente de pasada de feel |

### Selector de Esquema

Se elige desde **Settings → General → "Control"**, un valor persistente (se recuerda entre sesiones) con dos posiciones: Modern / Classic. El cambio se aplica en caliente — si se cambia desde el menú de Pausa durante una partida en curso, el esquema nuevo rige desde el siguiente frame, sin necesidad de recargar la escena.

### Controles

| Acción | Teclado | Gamepad |
|---|---|---|
| Mover / Girar (según esquema) | WASD / Flechas | Stick izquierdo / D-pad |
| Sprint (mantener) | V | ButtonWest (X Xbox / Cuadrado PS) |
| Interactuar | F / E | Botón Sur |
| Abrir inventario | Tab | Select |
| Pausa | Esc | Start |

No existe input de ratón en ninguna pantalla del juego.

### Velocidades

| Parámetro | Valor | Condición |
|---|---|---|
| `walkSpeed` | 4 unidades/s | Sprint no presionado, o retrocediendo en Classic |
| `runSpeed` | 7 unidades/s | Sprint presionado (Modern siempre; Classic sólo moviéndose hacia adelante) |

La velocidad se aplica como `Rigidbody.linearVelocity` en `FixedUpdate`. No hay aceleración gradual — el cambio de velocidad es inmediato.

### Cuantización del Input

Aplica sólo a **Modern**, donde los dos ejes del stick se combinan en una única dirección de mundo:

- **Gamepad:** el vector se normaliza directamente. Permite las 8 direcciones con cualquier ángulo.
- **Teclado:** se aplica `Quantize8Way` — cada eje se redondea a -1, 0 o +1 y el resultado se normaliza. Garantiza que WASD produzca exactamente las 8 direcciones cardinales e intercardinales.

**Zona muerta:** se descarta cualquier input con `magnitud² < 0.01`. Elimina drift de gamepad y evita transiciones de animación espurias al soltar el stick.

**Classic** no cuantiza — el eje horizontal (giro) y el vertical (avance/retroceso) son independientes entre sí, cada uno se usa directo.

### Física

| Propiedad | Configuración |
|---|---|
| Tipo | Rigidbody 3D, Dynamic |
| Gravedad | Activada |
| Freeze Rotation | X, Y, Z activados |
| Colisionador | CapsuleCollider |

La física no controla la orientación en ninguno de los dos esquemas. En Modern, la rotación se asigna directamente a `transform.forward` en la dirección de movimiento calculada (el personaje nunca "gira" gradualmente, salta a la orientación nueva). En Classic, la rotación es gradual — se acumula cuadro a cuadro a la velocidad de giro fija.

---

## Diseño — Animación

### Parámetro del Animator

| Parámetro | Tipo | Valores | Semántica |
|---|---|---|---|
| `Speed` | Float | 0 / 0.5 / 1.0 | 0 = Idle, 0.5 = Walk, 1.0 = Run |

El parámetro se escribe de forma discreta desde `PlayerController` cada `FixedUpdate`. Unity no interpola el valor — se asigna el destino directo. En Classic, retroceder siempre escribe 0.5 (Walk) aunque Sprint esté presionado.

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

La cámara **no sigue libremente al jugador**. Cada sala/zona tiene una o más cámaras fijas predefinidas (estilo Resident Evil clásico); al cruzar de una zona de cobertura a otra, la cámara activa cambia (corte, no interpolación de posición).

| Componente | Configuración |
|---|---|
| CinemachineBrain + CinemachineCamera por zona | Una cámara fija por ángulo de sala |
| Activación | Por trigger de zona — la cámara con mayor prioridad dentro del trigger que ocupa el jugador pasa a ser la activa |

El esquema **Modern** deriva su dirección de movimiento de la cámara activa (ver "Esquema Modern" arriba). El esquema **Classic** es independiente de la cámara — gira y avanza relativo a sí mismo, sin importar qué cámara esté activa; la cámara fija sólo enmarca la escena.

---

## Intención

El movimiento cuantizado y el sprint como elección consciente refuerzan el tono del juego. Caminar es el estado normal de un operativo bajo control. Correr es emergencia.

> En Silent Hill caminas porque tienes miedo. En Crimson Draft caminas porque eres un operativo disciplinado. El sprint es una decisión táctica, no un reflejo.

La diferencia entre walkSpeed (4) y runSpeed (7) es suficiente para sentir urgencia sin convertir el sprint en un dash. El personaje nunca teleporta — el movimiento es siempre legible.

**Por qué dos esquemas de control:** las cámaras fijas son parte de la identidad del género que el juego referencia, pero el control Tank es una barrera de entrada real para una audiencia moderna. Ofrecer ambos — igual que hizo Capcom con el remaster de 2015 — deja que cada jugador elija entre la fidelidad a la referencia (Classic) y la comodidad de un control contemporáneo (Modern) sin comprometer el diseño de cámaras fijas en sí, que es el pilar que no se negocia.

---

## Pendiente

- [x] Implementar `ClassicPlayerMovementStrategy` (Tank Controls) — ver spec [[2026-09-01-player-movement-control-scheme-design|Player Movement Control Scheme]]
- [x] Cablear la perilla "Control" del menú de Settings a la persistencia real
- [ ] Pasada de feel sobre la zona muerta angular de Modern (15°) y la velocidad de giro de Classic (180°/s)
- [ ] Sonido de pasos diferenciado por velocidad (caminar vs correr)
- [ ] Sonido de pasos diferenciado por superficie
- [ ] Animación de agacharse / cubrirse (mecánica de sigilo futura)
- [ ] Detección de interactivo cercano (highlight de objeto)
- [ ] Animación de apertura de puertas

---

Volver a [[Crimson Draft]] | Ver [[Sistema de Combate en Tiempo Real]] | Ver [[Mecanicas de Supervivencia]]
