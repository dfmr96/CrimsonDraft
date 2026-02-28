# Sistema de Movimiento

## Concepto Central

El movimiento de exploración es **4-direccional cardinal** — sin diagonales, sin strafe, sin apuntar. El personaje se mueve hacia donde el jugador empuja el stick y mira hacia esa dirección.

La referencia es Zelda clásico y Final Fantasy vista superior: movimiento libre pero cuantizado al eje dominante. El objetivo es legibilidad inmediata del estado del personaje y una sensación de peso deliberado coherente con el tono de survival horror.

> No hay botón de correr en la exploración. La velocidad única refuerza la tensión: el jugador nunca puede "huir rápido" de una situación, solo retroceder con el mismo ritmo.

---

## Controles

| Acción | Teclado | Gamepad |
|---|---|---|
| Mover | WASD / Flechas | Stick izquierdo / D-pad |
| Interactuar | F / E | Botón Sur |
| Abrir inventario | Tab | Select |
| Pausa | Esc | Start |

No existe input de ratón en ninguna pantalla del juego.

---

## Comportamiento del Movimiento

### Cuantización cardinal

El input análogo del stick (Vector2 continuo) se cuantiza al eje de mayor magnitud:

- Si `|X| ≥ |Y|` → el personaje se mueve en el eje horizontal puro (izquierda o derecha)
- Si `|Y| > |X|` → el personaje se mueve en el eje vertical puro (arriba o abajo)

Las diagonales se resuelven automáticamente: presionar W+D mueve hacia arriba si Y domina, o hacia la derecha si X domina. No existe movimiento diagonal.

### Zona muerta

Se descarta cualquier input con `magnitud² < 0.01`. Esto elimina drift de gamepad y evita animaciones de caminata espurias al soltar el stick.

### Velocidad

| Parámetro | Valor | Configurable |
|---|---|---|
| Velocidad de caminata | 4 unidades/s | Sí (Inspector) |

La velocidad se aplica como `Rigidbody2D.linearVelocity` en `FixedUpdate`. La física de Unity maneja las colisiones con paredes y obstáculos.

### Física

| Propiedad | Configuración |
|---|---|
| Body Type | Dynamic |
| Gravity Scale | 0 (vista superior) |
| Freeze Rotation Z | Activado |
| Colisionador | CapsuleCollider2D |

---

## Dirección y Animación

### Estados del Animator

El personaje tiene 8 estados de animación activos:

| Estado | Condición de entrada |
|---|---|
| **WalkDown** | IsMoving=true + FacingDirection=0 |
| **WalkUp** | IsMoving=true + FacingDirection=1 |
| **WalkLeft** | IsMoving=true + FacingDirection=2 |
| **WalkRight** | IsMoving=true + FacingDirection=3 |
| **IdleDown** | IsMoving=false + FacingDirection=0 |
| **IdleUp** | IsMoving=false + FacingDirection=1 |
| **IdleLeft** | IsMoving=false + FacingDirection=2 |
| **IdleRight** | IsMoving=false + FacingDirection=3 |

Todas las transiciones tienen `transitionDuration = 0` (corte directo, sin blend). El blend entre sprites pixel art produce artefactos visuales y no aporta nada al look del juego.

### Parámetros del Animator

| Parámetro | Tipo | Descripción |
|---|---|---|
| `IsMoving` | Bool | True si hay input activo |
| `FacingDirection` | Int | 0=Abajo, 1=Arriba, 2=Izquierda, 3=Derecha |

`FacingDirection` se preserva al soltar el input — el Idle muestra la dirección donde el personaje quedó mirando.

### Configuración de clips

| Propiedad | Valor |
|---|---|
| Frame rate | 8 fps |
| Loop | Activado |
| Frames por dirección | 4 |

---

## Cámara

La cámara sigue al jugador mediante **Cinemachine** con extensión **Pixel Perfect**:

| Componente | Función |
|---|---|
| CinemachineCamera | Follow y LookAt apuntan al Transform del Player |
| CinemachinePixelPerfect | Mantiene alineación pixel-perfect al seguir |
| PixelPerfectCamera | Renderizado ortográfico a resolución de referencia |

La cámara no tiene zoom ni shake configurado en esta fase. Se puede añadir un CinemachineImpulse para feedback de daño en el futuro.

---

## Pendiente

| Feature | Prioridad | Notas |
|---|---|---|
| Detección de interactivo cercano (highlight) | Alta | Necesita sistema de interacción |
| Animación de apertura de puertas | Alta | Clip dedicado o trigger en Animator |
| Sonido de pasos | Media | Programático, varía por superficie |
| Animación de agacharse / cubrirse | Media | Para mecánica de sigilo futura |
| Correr (si se decide añadir) | Baja | Requiere decisión de diseño |
