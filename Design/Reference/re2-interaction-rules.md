# Resident Evil 2 — Reglas de Interacción con Ítems (Referencia)

> **Fuente**: Resident Evil 2 (1998), versión PC de GOG. Se desensamblaron los scripts SCD
> de las **250 salas del escenario de Leon** (`Pl0/Rdu/*.RDT`) junto con sus mensajes en inglés.
> Este documento describe cuándo RE2 usa un ítem automáticamente, cuándo pregunta Sí/No
> y cuándo obliga a usarlo desde el inventario, y abstrae esos casos en arquetipos.

---

## Los tres ejes

Toda interacción con ítems se describe con tres decisiones independientes:

| Eje | Opciones |
|---|---|
| **Inicio**: dónde empieza | Interactuar en el mundo · Usar desde inventario · Examinar en inventario · Combinar en inventario |
| **Resolución**: cómo se elige el ítem | **Automática** (el juego lo encuentra y lo usa) · **Confirmada** (el juego lo encuentra y pregunta Sí/No) · **Elegida** (el jugador lo selecciona en el inventario) |
| **Resultado**: qué produce | Otorga ítem · Otorga documento · Transforma ítem · Consume ítem · Cambia el mundo |

"Convertirse en documento" es solo un **resultado**. Lo que distingue a los arquetipos es la **resolución**.

---

## Regla de resolución implícita en RE2

Se aplica en orden; la primera que se cumple decide.

```
1. ¿El interactuable no necesita ítem?
     → Sin ítem. Si la acción es irreversible o mueve al jugador → Sí/No.
2. ¿Es una puerta con llave?
     → Automática.
3. ¿Elegir el ítem correcto ES el puzzle, o el ítem es una pieza que encaja en un mecanismo?
     → Elegida (desde inventario).
4. En cualquier otro caso (herramienta, llave de un mueble, recurso del entorno)
     → Confirmada (detecta + Sí/No).
```

**Criterio de fondo:** la resolución automática solo se usa cuando no hay nada que deducir. La elegida protege la deducción del jugador. La confirmada protege sus recursos.

---

## Arquetipos

### 1. Examinable

Objeto que solo describe. Sin ítem, sin pregunta.

- **Resolución:** sin ítem
- **Resultado:** texto
- **RE2:** AOT de tipo `SCE_MESSAGE`. "It's too dangerous to go back outside."

### 2. Acción ambiental

Mecanismo que el jugador acciona sin ítem: interruptor, escalera, palanca, panel.

- **Resolución:** sin ítem, **siempre con Sí/No**
- **Resultado:** cambia el mundo o mueve al jugador
- **RE2:** "It's a switch. Will you push it?", "Will you go down the ladder?", "A lever to fill and drain water. Will you move it?"

### 3. Recogible

Ítem o documento en el mundo que pasa al jugador.

- **Resolución:** confirmada
- **Resultado:** otorga ítem o documento
- **RE2:** AOT de tipo `SCE_ITEM`; los mapas preguntan "A police station map. Will you take it?"

### 4. Cerradura de puerta

Puerta bloqueada por una llave específica.

- **Resolución:** **automática**
- **Resultado:** cambia el mundo
- **RE2:** solo 14 de ~310 puertas piden llave (Spade, Diamond, Heart, Club, Cabin, Power Room, Master, Platform, Umbrella Card). La llave está en los datos de la puerta (`door_aot_se`) y ningún script de sala la gestiona: es lógica del motor.

### 5. Cerradura de herramienta

Algo que se abre con una herramienta o una llave que **no es de puerta**: ganzúa, llave pequeña, llave de un mueble.

- **Resolución:** **confirmada**. El script comprueba si el jugador lleva el ítem (`keep_item_ck`), pregunta "Will you use the X?" y al aceptar muestra "You used the X."
- **Resultado:** cambia el mundo y consume el ítem
- **RE2:**
  - "The lock is broken. Will you use the {lockpick}?"
  - "The warehouse door is locked. Will you use the {small key}?" / "{lockpick}?"
  - "Will you use the {special key}?" (taquilla del cuarto oscuro)
  - "Will you use the {Weapon Box Key}?"
  - "Will you use the {master key}?"

> La **Master Key** abre una puerta (automática) y además un panel (confirmada). **La resolución la decide el destino, no el ítem.**

### 6. Receptáculo

Mecanismo o puzzle que exige uno o varios ítems concretos: pedestal, ranura, panel de fusibles.

- **Resolución:** **elegida**. El jugador usa el ítem desde el inventario frente al receptáculo y el script compara `V_USED_ITEM`.
- **Resultado:** consume el ítem (`sce_item_lost`) y cambia el mundo al completarse
- **Mismo punto para mirar y para usar:** si el jugador lo revisa con las manos vacías, `V_USED_ITEM` vale 0 y se muestra la descripción con la pista y el progreso.
  - Pista: "An old fountain. Something is written here. 'To obtain the key to open your heart, I'll wait for the Unicorn, the beautiful beast.'"
  - Descripción: "Clearance identification device. A wolf and an eagle are etched on the device."
  - Progreso: "The wolf medal is in place." / "The two medals are already in place."
- **RE2** (unas 20 salas): Red Jewel, Unicorn Medal, Eagle/Wolf Medal, las cuatro Plugs de ajedrez, Valve Handle, Crank, Cog, Lighter, Red Card, Main Fuse, Fuse Case, Cord, Manhole Opener, MO Disk, Joint Plugs, rollos de película, Up/Down Key.
- **No siempre se consume:** la Valve Handle (3 salas) y el Lighter (5 salas) se usan desde el inventario pero ningún script los quita. Son **herramientas reutilizables** con resolución elegida.

> Las **Up/Down Key** se llaman "llave" pero van en un panel, así que funcionan como receptáculo. **El nombre del ítem no define su arquetipo; lo define dónde se usa.**

### 7. Transformador

Lugar del mundo que convierte un ítem en otro ítem o en un documento.

- **Resolución:** elegida (hay que deducir qué ítem va)
- **Resultado:** consume el ítem y entrega el resultado con el diálogo de recogida normal
- **RE2:**
  - **Ítem → documento:** el cuarto oscuro. Ver [Cuarto oscuro](#cuarto-oscuro--ítem-que-se-convierte-en-documento).
  - **Ítem → ítem:** la caja de control de baja temperatura. Ver [Fuse Case → Main Fuse](#fuse-case--main-fuse-flujo-completo).

### 8. Almacén

Caja de objetos compartida. AOT de tipo `SCE_ITEMBOX`. Sin reglas de resolución propias.

### 9. Punto de guardado

Máquina de escribir que requiere un recurso (Ink Ribbon) para guardar.

- **Resolución:** confirmada. El motor detecta la cinta y pregunta antes de gastarla.
- **Resultado:** consume una unidad del recurso y abre el guardado
- **Sin el recurso:** solo describe. "An old typewriter. I could save my progress if I had an {ink ribbon}."
- **RE2:** AOT de tipo `SCE_SAVE` (28 salas)

### 10. Consumible personal

Ítem que se aplica sobre el personaje desde el inventario, sin ningún objetivo en el mundo: curativos y munición.

- **Inicio:** usar (o combinar) desde el inventario
- **Resolución:** elegida
- **Resultado:** consume el ítem y modifica el estado del personaje o del arma
- **RE2:** hierbas, First Aid Spray, munición. Lo resuelve el motor; no aparece en los scripts de sala.

### 11. Recurso del entorno

Recurso que se consume en el lugar, sin pasar por el inventario.

- **Resolución:** sin ítem, con Sí/No
- **Resultado:** efecto inmediato sobre el personaje
- **RE2:** plantas de hierba azul. "There are {Blue Herbs} growing here. Will you use them?" → "You used the {Blue Herb}." (8 salas)

---

## Cuándo hay pregunta Sí/No

| Situación | Sí/No | Ejemplo RE2 |
|---|---|---|
| El juego encontró el ítem por su cuenta (cerradura de herramienta) | **Sí** | "Will you use the {lockpick}?" |
| Recoger un mapa | **Sí** | "Will you take it?" |
| Movimiento que saca al jugador de la zona (escalera, bajar, saltar) | **Sí** | "Will you go down the ladder?", "An old dust shoot. Will you jump down?" |
| Acción ambiental irreversible o con consecuencia | **Sí** | "Will you turn on the switch?", "Will you release the electronic lock?" |
| Gastar un recurso del entorno | **Sí** | "There are Blue Herbs growing here. Will you use them?" |
| Equiparse o cambiar el estado del personaje | **Sí** | "Will you change your clothes?", "A side pack. Will you equip with it?" |
| Usar una computadora o un panel | **Sí** | "Will you use the computer?", "Will you turn the monitor on?" |
| Puerta con llave | **No**, es automática | — |
| Usar un ítem en un receptáculo desde el inventario | **No**, elegir el ítem ya es la confirmación | — |
| Examinar | **No** | — |

**Regla corta:** se pregunta cuando el juego actúa por el jugador o cuando la acción no se puede deshacer. No se pregunta cuando el jugador ya eligió explícitamente.

---

## Casos especiales

### Pareja de ítems: Joint Plugs

Al usar **una** de las dos Joint Plugs, el script comprueba que el jugador lleve **las dos** (`keep_item_ck` de ambas) y las coloca juntas con dos `sce_item_lost` seguidos. Si falta una, no consume nada y ejecuta otro procedimiento (mensaje).

- **Regla:** cuando un receptáculo necesita un conjunto de ítems que siempre van juntos, usar uno los coloca todos.

### Precondición del mundo: Red Card

Si la lectora no tiene energía, la tarjeta **no se consume** y aparece "The power to the card reader is off." Con energía, se consume y el mecanismo se activa.

- **Regla:** si la acción falla por una condición del mundo, el ítem nunca se pierde, y el mensaje explica qué falta.

### Cuarto oscuro: ítem que se convierte en documento

El jugador usa un rollo de película (Film A/B/C/D) desde el inventario en el cuarto oscuro → el rollo se consume (`sce_item_lost(FilmA)`) → el script dispara con `aot_on` un pickup "Developed Film A", que es un **documento**. Ese pickup está en (20000, 20000), fuera del alcance del jugador: no existe para recogerlo caminando, sino para **entregar el resultado con el diálogo de recogida normal**.

- **Regla:** cuando usar un ítem produce otro ítem o un documento, el resultado se entrega en el momento con el mismo flujo de recogida de siempre. No se crea un sistema de entrega aparte.

### Fuse Case → Main Fuse: flujo completo

Sala `60D0`. Es el transformador ítem → ítem, y su script muestra el ciclo completo de un receptáculo.

**1. Preparación de la sala (al entrar):**

| AOT | Tipo | Función |
|---|---|---|
| `AOT_2` | `SCE_ITEM` | La Fuse Case, que se recoge **en esta misma sala** |
| `AOT_4` | `SCE_EVENT` | La caja de control: al revisarla ejecuta el procedimiento de descripción |
| `AOT_6` | `SCE_FLAG_CHG` (`FG_USE`, ítem 77) | **Declara que aquí se puede usar la Fuse Case.** Ocupa el mismo rectángulo que `AOT_4`. |
| `AOT_1` | `SCE_ITEM` | La Main Fuse, en (32000, 32000): fuera del alcance, solo sirve para entregarla |

**2. Revisar la caja con las manos vacías:** si el puzzle no está resuelto (flag `FG_COMMON 119` = 0), muestra la pista: "I could set the superconductor fuse here if I had a {fuse case}."

**3. Usar la Fuse Case desde el inventario dentro de `AOT_6`:** el motor guarda el ítem en `V_USED_ITEM` y ejecuta el procedimiento de uso de la sala. El script comprueba que el puzzle no esté resuelto y que `V_USED_ITEM` = 77 (Fuse Case), y lanza la secuencia.

**4. Secuencia:**
1. Marca el puzzle como resuelto (`FG_COMMON 119` = 1) y bloquea el control del jugador.
2. Corte de cámara, y los modelos de la máquina se colocan en escena.
3. Animación del mecanismo: varios bucles de movimiento con pausas.
4. **`sce_item_lost(FuseCase)`**: la Fuse Case sale del inventario.
5. **`aot_on(AOT_1)`**: dispara el pickup de la Main Fuse, que aparece con el diálogo de recogida normal.
6. Vuelve la cámara, los modelos regresan fuera de escena y se devuelve el control.

**5. Si el jugador no se lleva la Main Fuse** (inventario lleno o la rechaza): revisar la caja otra vez, con el puzzle ya resuelto y el flag de recogida de la Main Fuse aún sin marcar, ejecuta `aot_on(AOT_1)` de nuevo y vuelve a ofrecerla. **El resultado nunca se pierde.**

- **Reglas:**
  - El ítem de entrada se consume justo antes de entregar el resultado, no al principio de la secuencia.
  - El receptáculo recuerda que ya fue resuelto, y revisarlo de nuevo ofrece lo pendiente.
  - En los documentos (cuarto oscuro) no hace falta volver a ofrecer el resultado, porque no ocupan inventario.

### Prioridad entre herramientas: Small Key vs Lockpick

En el almacén, si el jugador lleva la Small Key (consumible) y la ganzúa (reutilizable), el script gasta **la Small Key** (`sce_item_ck_lost(SmallKey, 1)`) y conserva la ganzúa.

- **Regla:** si varios ítems sirven, se gasta primero el consumible y se conserva el reutilizable.

### Doble entrada: Master Key en el panel

El panel que abre la Master Key acepta **las dos vías**: interactuar con él (el script detecta la llave y pregunta "Will you use the {master key}?") o usarla desde el inventario (un bucle espera `V_USED_ITEM` = Master Key). Las dos terminan en el mismo procedimiento.

- **Regla:** para una cerradura de herramienta, el jugador que abre el inventario por costumbre no debe quedar bloqueado. Las dos entradas llevan al mismo resultado.

---

## Modelo de ítems del motor (ejecutable)

RE2 **no tiene un campo de "clase"** para los ítems. Cada ítem es un ID (0–99 para ítems, 104+ para documentos) con un **registro de atributos de 8 bytes** y una **lista de combinaciones**. Lo que hace que una munición se comporte como munición y una hierba como hierba son esos datos.

Fuente: `LeonU.exe` (GOG). La tabla de atributos está en el offset de archivo `0x1462A8` (VA `0x547EA8`) y la de combinaciones justo antes, en VA `0x547D60`. Se encontró buscando la secuencia de capacidades de cargador de las armas por orden de ID.

### Registro de atributos (8 bytes por ítem)

| Byte | Campo | Valores |
|---|---|---|
| 0 | **Capacidad** | Armas: tamaño del cargador (Handgun Leon 18, Shotgun 5, Magnum 8). Munición, Ink Ribbon y Small Key: 250 (apilable). Armas o munición en porcentaje: 100. Ítems clave y curativos: 1 (no apilable). |
| 1 | **Nombre tras examinar** | Índice del nombre que el ítem adopta al examinarlo. 0 = no cambia. Spade Key 1, Diamond 2, Heart 4, Club 5, Red Jewel 6, Crank 7, Down Key 8, Up Key 9, Special Key 10. |
| 2 | **Contador en pantalla** | Bits bajos: color del número (granadas: flame 1, acid 2, explosive 3; munición común 3). **4 = sin número.** Bit `0x80`: se muestra como porcentaje (SMG, Sparkshot, Flamethrower y sus municiones). Bit `0x10`: arma infinita; el juego lo escribe en tiempo de ejecución (ver [Curiosidades](#curiosidades-del-código-del-ejecutable)). |
| 3 | **Cantidad de combinaciones** | Cuántas entradas tiene su lista de combinaciones |
| 4–7 | **Puntero a la lista de combinaciones** | Los ítems sin combinaciones apuntan a una entrada vacía compartida |

**Nombre genérico hasta examinar.** En la tabla de nombres del motor, las cuatro llaves de la comisaría se llaman "Precinct Key", las Up/Down Key "C. Panel Key", las mezclas "Mixed Herb" y los rollos "Film". El nombre específico aparece al examinar el ítem (byte 1).

### Entrada de combinación (4 bytes)

`[ítem con el que se combina] [tipo de combinación] [ítem resultado] [parámetro]`

| Tipo | Significado | Ejemplos |
|---|---|---|
| **5** | Pasar cantidad del ítem seleccionado al otro: **recargar** o **juntar pilas** | Handgun + Handgun Ammo → recarga; Handgun Ammo + Handgun Ammo → una pila; Ink Ribbon + Ink Ribbon |
| **6** | Lo mismo en sentido inverso (la munición elegida primero y el arma como destino) | Handgun Ammo + Handgun → recarga el arma |
| **7 / 8** | **Cambiar el tipo de granada cargada.** El ID del lanzagranadas cambia según la munición. | GL (Explosive) + Flame Rounds → GL (Flame) |
| **4** | **Fusionar en un ítem nuevo**: se consumen los dos | Handgun + Handgun Parts → Custom Handgun; Detonator + C4 → C4 con detonador; Jaguar Stone L + R → Jaguar Stone |
| **11** | **Mezcla de hierbas** (el parámetro es la variante de mezcla) | G + G → GG; G + R → GR; GB + R → GRB |
| 10 | Granadas explosivas → flame/acid. No la usa ningún ítem de la tabla de Leon (posiblemente sin uso). | — |

### Cómo se distingue cada tipo

| Tipo | Capacidad | Contador | Combinaciones |
|---|---|---|---|
| **Arma** | Tamaño del cargador | Número o % | Su munición (recargar) y sus Parts (mejorar) |
| **Munición** | 250 (100 si es %) | Número de color o % | Cada arma compatible (recargar) y ella misma (juntar pilas). La munición de pistola tiene 6 entradas: cinco pistolas más ella misma. |
| **Curativo** | 1 | Sin número | Hierbas: tipo 11 hacia la mezcla siguiente. Las mezclas finales (GGG, GGB, GRB) y el First Aid Spray no combinan. |
| **Ink Ribbon** | 250 | Número | Ella misma (juntar pilas) |
| **Small Key** | 250 | Número | Ninguna (apilable, pero no combina) |
| **Ítem clave / puzzle** | 1 | Sin número | Ninguna, salvo las piezas que se fusionan (Jaguar L+R, Detonator+C4) |

**Munición frente a hierba**, en concreto: la Handgun Ammo es apilable hasta 250, muestra un número en color, y sus combinaciones la cargan en cinco pistolas o la juntan con otra pila. La Green Herb no se apila (capacidad 1), no muestra número, y sus cinco combinaciones son todas de tipo 11 hacia las mezclas GG, GR, GB, GGG y GGB.

### Casilla de inventario y munición infinita

Cada casilla del inventario (y del baúl) ocupa 4 bytes: `[ID del ítem] [cantidad] [parte] [00]`.

- **Cantidad:** número de unidades, o porcentaje en armas de tipo % (SMG, Flamethrower, Gatling).
- **Parte:** las armas especiales grandes (Rocket Launcher, SMG) ocupan **dos casillas** contiguas marcadas `01` y `02`, cuyas cantidades bajan juntas. En el baúl se guardan como `03` y ocupan una sola casilla.
- **Munición infinita:** **cantidad `FF` (255)**. No es otro ítem ni otra clase: es la misma arma con un valor centinela en la cantidad. Un arma con `FF` nunca se vacía, y por eso tampoco se le puede cambiar el tipo de munición (lanzagranadas).

Encaja con la tabla de atributos: la capacidad máxima de cualquier pila es 250 (`FA`), así que los valores 251–255 nunca son cantidades reales y quedan libres como marcadores.

Fuente: [Resident Evil 2 — Editing Saved Games](http://alexfung.info/favorite/game/re2code.htm), que documenta el formato de casilla en la partida guardada. Ahí no se dice qué armas usan `FF` ni cuáles son las condiciones de desbloqueo, y este análisis tampoco las verificó.

**Qué hace "Usar":** no está en estas tablas sino en el código, y depende del rango de ID. Ver ["Usar" un curativo](#usar-un-curativo-0x4fe8a5).

**Mezclas posibles según la tabla:** G+G, G+R, G+B, GG+G, GG+B, GR+B, GB+G, GB+R. No existe R+B ni R+R.

---

## Curiosidades del código del ejecutable

Encontradas desensamblando el código de `LeonU.exe` (GOG) con Capstone y siguiendo las referencias a las tablas de ítems. Las direcciones son VA.

### Truco de munición infinita (`0x5092E0`)

Un detector de secuencias de botones con 9 pasos. Avanza un índice con cada pulsación correcta y vuelve a cero si se pulsa otra cosa. Traducida con el mapa de bits del mando de PlayStation, la secuencia es:

**Arriba, Arriba, Abajo, Abajo, Izquierda, Derecha, Izquierda, Derecha, R1**

Por el contexto de la llamada, se comprueba mientras está abierta la pantalla de ítems. Al completarla:

- **Activar:** marca como infinitas **todas las armas** (IDs 0–23) en un campo de bits global (`0x998FC4`).
- **Volver a introducirla:** lo desactiva, pero la **Gatling Gun sigue siendo infinita** (el código la vuelve a marcar).

Cada vez que se abre la pantalla de ítems, el juego recorre las armas (IDs 1–19) y copia ese campo de bits al bit `0x10` del byte de contador de la tabla de atributos. **El juego modifica su propia tabla de datos en tiempo de ejecución** para que el inventario muestre las armas infinitas.

### Armas de recompensa al empezar (`0x4B13FB`)

Al empezar una partida nueva, el juego lee unos flags de desbloqueo persistentes (`0x9A0188`) y escribe las armas **directamente en las casillas del inventario** (`0x999360`, 4 bytes por casilla), a partir de la tercera (después del cuchillo y la pistola):

| Flag | Arma | Cantidad |
|---|---|---|
| `0x10` | SMG | `FF` (infinita) |
| `0x20` | Rocket Launcher | `FF` (infinita) |
| `0x40` | Gatling Gun | 1, marcada infinita en el campo de bits |

Esto confirma en el código que la cantidad `FF` significa munición infinita.

### Cálculo del rango (`0x4B0980`)

El rango se calcula con **tres puntuaciones de 0 a 100** que se suman (máximo 300):

| Puntuación | Fórmula |
|---|---|
| **Tiempo** | 100 si terminas en **2 h 30 min o menos**. −10 por cada hora extra empezada. |
| **Partidas guardadas** | 100 − 5 por guardado. Con más de 19 guardados, 0. |
| **First Aid Spray usados** | 100 − 15 por spray. Con más de 5, 0. |

Un flag de partida resta además 50 puntos; no se identificó qué representa. Umbrales de rango:

| Puntos | Rango |
|---|---|
| ≥ 269 | A |
| ≥ 199 | B |
| ≥ 144 | C |
| ≥ 99 | D |
| < 99 | E |

**Las hierbas no penalizan el rango; el First Aid Spray sí.** Una mezcla G+R cura lo mismo que un spray (100%) pero no cuenta.

Existe otro modo, seleccionado por un flag, que puntúa el tiempo en minutos (desde 4 min, en pasos de 1 min). Seguramente es un minijuego.

### Condiciones de desbloqueo (misma función)

| Recompensa | Condición |
|---|---|
| **Rocket Launcher infinito** | Rango A o B **y** puntuación de tiempo 100 (≤ 2 h 30 min) |
| **SMG infinita** | Rango A o B **y** ningún First Aid Spray usado. Solo se evalúa con un flag que, por cómo se usa, parece ser el segundo escenario. |
| **Gatling Gun** | Rango A o B **y** ninguna partida guardada. Misma restricción de escenario. |

Si un modo concreto está activo (flag `0x6942A0`, sin identificar) no se evalúa ningún desbloqueo. La primera vez que se gana una recompensa se marca como nueva (bit `0x80`) para destacarla en la pantalla de resultados.

### Guardar gasta la cinta antes de elegir ranura (`0x4C5FCE`)

Al guardar, el juego resta una unidad a la cinta (y vacía la casilla si llega a 0) y suma 1 al contador de guardados **antes** de que elijas ranura. Si cancelas, restaura el inventario desde una copia de seguridad y resta 1 al contador: **nunca pierdes la cinta por cancelar**, y el rango no te penaliza.

### "Usar" un curativo (`0x4FE8A5`)

Un `switch` sobre los IDs 35–46. La curación se calcula sobre la **vida máxima** (no es un valor fijo) y se suma a la vida actual con tope en el máximo.

| Ítem | Cura | Veneno | Nota |
|---|---|---|---|
| First Aid Spray | 100% | — | Suma 1 al contador de sprays (rango) |
| Green Herb | 25% | — | |
| Red Herb | — | — | No se puede usar sola: muestra un mensaje |
| Blue Herb | 0 | Cura | |
| G+G | 50% | — | |
| G+R | 100% | — | |
| G+B | 25% | Cura | |
| G+G+G | 100% | — | |
| G+G+B | 50% | Cura | |
| G+R+B | 100% | Cura | |

Los IDs menores de 30 (armas y munición) van por otra rama del mismo despachador. **"Usar" se decide por rango de ID**, como se suponía.

### Vida del personaje

La estructura del jugador está en `0x9944F0` y es la misma que usan los enemigos. La **vida** está en `+0x156` y la **vida máxima** en `+0x162`.

- **Vida máxima = 200** para todos los personajes jugables, **excepto TOFU (ID `0x0D`), que tiene 400** (`0x4D8FA6`). TOFU solo lleva cuchillo, y el doble de vida es su compensación.
- Personajes identificados convirtiendo a PNG la textura de cada `PLxx.PLD`: `0x0C` HUNK, `0x0D` TOFU, `0x0E` Ada, `0x0F` Sherry.
- **Cambio de personaje (Ada / Sherry):** al pasar a controlar a la compañera, el juego guarda la vida y el veneno del protagonista y la compañera empieza con la vida al máximo. Al volver, restaura al protagonista tal como estaba (`0x4E2171`).
- **Compañera como PNJ:** cuando Sherry deja de ser controlable, su vida y vida máxima pasan a `FFFF` (−1): no puede morir. Al volver a controlarla recupera la vida guardada, con máximo 200 (`0x4EC48B`).
- **Opcode `heal` de los scripts de sala:** vida al máximo y quita el veneno (`0x4EC78B`).

### Vida de los enemigos

Los enemigos usan la misma estructura que el jugador: vida en `+0x156` y tipo de enemigo en `+8`.

**Zombi (`0x449590`):**

- La vida se elige **al azar** de una tabla de 16 valores: 80, 94, 128, 75, 60, 95, 58, 75, 50, 83, 79, 66, 80, 65, 82, 65 (entre 50 y 128).
- El índice es `(aleatorio >> (aleatorio & 3)) & 15`. El desplazamiento previo hace que **las primeras entradas salgan más a menudo**: la distribución no es uniforme.
- Con el flag `0x20` del estado de juego (probablemente el modo fácil) usa otra tabla: 54, 28, 54, 54, 70, 40, 54, 100, 40, 54, 70, 54, 54, 70, 54, 40.
- Sin ese flag, si una variable de progreso es menor que 4, el zombi recibe **+15** de vida.
- Los zombis de **patrón impar** (los que se arrastran) empiezan con **la mitad de vida**.
- Un tipo de aparición concreto recibe vida `FFFF`: no muere (probablemente decorado).
- En el modo especial del flag `0x6942A0` (el mismo que bloquea los desbloqueos; probablemente Extreme Battle), la vida se multiplica por 5, o por 3 según una variable de progreso. Otros enemigos usan ×1,5 o ×2 en ese modo.

Otros enemigos tienen sus propias tablas de vida aleatoria (por ejemplo `0x52FE18`, `0x530538`, `0x532270`) y a veces la reducen según flags de la historia.

### Daño de las armas (`0x4D7720`)

Una única función aplica el daño a todos los enemigos:

```
tabla    = punteros[0x543C48 + tipo_enemigo * 4]    ← una tabla de daño POR TIPO DE ENEMIGO
registro = tabla + (id_arma - 1) * 20               ← 20 bytes por arma (IDs 1–19)
daño     = (registro.dword0 >> (10 * nivel)) & 0x3FF  ← 3 valores de 10 bits empaquetados
vida    -= daño   →   vida < 0 ? estado "morir" : estado "herido"
```

- **El daño depende de la combinación arma × enemigo**, no solo del arma.
- **Los tres niveles** los elige quien llama a la función. La explosión de la granada la llama con el nivel 1 (la onda expansiva usa la segunda columna; el impacto directo, la primera). En pistola y escopeta los valores bajan de un nivel al siguiente, lo que indica distancia: el famoso **bonus de la escopeta a quemarropa**.
- Los otros 4 dwords de cada registro no se identificaron (probablemente retroceso o probabilidad de aturdir).

Daño por enemigo (cuando los tres niveles coinciden se muestra un solo número):

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

(Handgun = las cinco pistolas, que tienen los mismos valores. El cuchillo solo tiene el primer nivel.)

Lectura de diseño:

- **La Magnum mata a cualquier zombi de un disparo (900)**, pero contra el cocodrilo solo hace 31: por eso ese jefe se resuelve con el entorno.
- **Ivy es débil al fuego**: la granada incendiaria hace 300 de impacto directo, frente a 7 de la pistola.
- **El lanzallamas apenas daña al G (3) ni a Birkin (4–8)**: no hay un arma "mejor" en general, depende del enemigo.
- **Birkin recibe más daño de pistola en cada transformación** (16 → 21 → 27 → 29): las formas finales premian al jugador que llega sin munición pesada.
- **El Rocket Launcher no siempre mata**: 500 contra el Tyrant y solo 40 contra los brazos de zombi.

### IA de los enemigos

La IA es una **máquina de estados en tres niveles**, con tablas de punteros a funciones (zombi, tabla principal en `0x52F188`):

| Nivel | Campo | Contenido |
|---|---|---|
| Estado principal | `+4` | 0 = aparecer, 1 = comportamiento, 2 = recibir daño, 3 = morir; 4 y 7 son estados especiales |
| Patrón | `+0x10E & 0x3F` | Lo elige **el script de la sala** al colocar al enemigo (`sce_em_set`). Los pares son el zombi normal y los impares el que se arrastra (con la mitad de vida). |
| Subestado | `+5` | Dos tablas paralelas de 16 entradas: **decidir** (`0x52F1E8`, solo 4 subestados con lógica) y **ejecutar la acción** (`0x52F228`, 16 acciones) |

La función de daño también decide la transición: si la vida baja de 0 pasa a "morir"; si no, a "herido".

**Percepción del zombi en reposo (`0x44A1C0`):**

| Condición | Resultado |
|---|---|
| Distancia < 5000 y el jugador **fuera** del cono frontal de 90° | Se gira hacia el jugador (subestado 1) |
| Distancia < 3000, jugador dentro del cono de ~70° y haciendo ciertas acciones | 50% de probabilidad de lanzarse a agarrar (subestado 12) |
| Distancia < 2000, jugador dentro del cono de 90° y haciendo ciertas acciones | 50% de probabilidad de lanzarse a agarrar |

- La distancia está en unidades del juego (`+0x1F0`). Los conos se miden en una escala de 4096 = 360° (`0x400` = 90°, `0x320` ≈ 70°).
- **No hay raycast de línea de visión**: el zombi solo usa distancia, ángulo, el estado del jugador y azar.
- El "estado del jugador" es un byte de flags de acción (`0x9944EE`, máscaras `0x15` / `0x17`). No se identificó cada bit; por las máscaras, probablemente correr, disparar o estar en una acción concreta.
- Una condición extra (bit 8 de `+0x155`) habilita la percepción. Seguramente indica si el zombi está activo o despierto.

### FOV de las cámaras

Cada sala guarda sus cámaras fijas en el bloque RID del `.RDT`, 32 bytes por cámara: `u16 marca`, `u16 lente`, posición (`from` x/y/z), punto al que mira (`to` x/y/z) y un puntero a las máscaras del fondo.

Al cambiar de cámara (`0x4C46E9`, llamado desde el opcode `cut_chg`, cuyo manejador se localizó en la tabla de opcodes SCD de `0x544DE8`):

```
H = camara.lente >> 7            ← distancia de proyección (el equivalente de SetGeomScreen en PSX)
SetProjection(H)                 ← 0x449070
LookAt(camara.from, camara.to)   ← 0x5093F0
```

La pantalla lógica es de **320 × 240** (constante `0xF00140` en la misma zona), así que:

- **FOV vertical = 2 · atan(120 / H)**
- **FOV horizontal = 2 · atan(160 / H)**

Distribución de las 2511 cámaras del escenario de Leon:

| H | Cámaras | % | FOV vertical | FOV horizontal |
|---|---|---|---|---|
| 208 | 1121 | 44,6% | **60°** | 75,1° |
| 231 | 808 | 32,2% | **55°** | 69,4° |
| 257 | 328 | 13,1% | **50°** | 63,8° |
| 188 | 88 | 3,5% | 65° | 80,8° |
| 290 | 86 | 3,4% | 45° | 57,8° |
| 321 | 20 | 0,8% | 41° | 53,0° |
| 221 | 10 | 0,4% | 57° | 71,8° |
| 330 | 8 | 0,3% | 40° | 51,7° |
| 171 | 6 | 0,2% | 70° | 86,2° |
| 448 | 4 | 0,2% | 30° | 39,3° |
| 84 | 4 | 0,2% | 110° | 124,6° |
| Otros (10 valores) | 26 | 1,0% | 42°–85° | — |

- **El 90% de las cámaras usa 60°, 55° o 50° de FOV vertical.** El resto son casos puntuales: teleobjetivo de 30°–45° para planos dramáticos y gran angular de 65°–110° para espacios estrechos.
- **Los valores caen en grados enteros de FOV vertical**: los artistas elegían el FOV vertical en grados en su herramienta 3D y el juego lo guardaba como distancia de proyección (`lente / 128`).
- El campo `marca` vale 1 en exactamente una cámara por sala. No se identificó su función.
- La vista 3D del inventario usa H = 208 (`0x50818E`), es decir, también 60°.

### Cambio de cámara

El cambio de cámara es un **corte instantáneo** que se dispara cuando la posición del jugador entra en una **zona del suelo** (bloque RVD del `.RDT`, offset 8). No hay transiciones ni interpolación.

**Formato de cada zona (20 bytes):**

| Byte | Campo | Contenido |
|---|---|---|
| 0 | Activa | 0 = desactivada (el script puede cambiarla) |
| 1 | Planta | `FF` = cualquiera; si no, debe coincidir con la planta del jugador (salas con desniveles) |
| 2 | Cámara origen | La zona solo se evalúa mientras esta cámara está activa |
| 3 | Cámara destino | La cámara a la que se corta |
| 4–19 | Cuadrilátero | 4 puntos (x, z) de 16 bits en el plano del suelo |

La lista está **agrupada por cámara origen** y termina con `FFFFFFFF`.

**Cada frame (`0x4C4B60`):**

1. Recorre solo las zonas cuyo origen es la cámara actual.
2. Ignora las desactivadas y las de otra planta.
3. Comprueba si la posición del jugador (x, z) está dentro del cuadrilátero (`0x4C4C40`, prueba de signo con productos cruzados en cada lado).
4. En la primera que coincide, corta a la cámara destino: actualiza la proyección y el `LookAt` (ver [FOV de las cámaras](#fov-de-las-cámaras)) y el fondo prerenderizado correspondiente.

**El primer registro de cada cámara no es un disparador.** El bucle se lo salta: es el **área que cubre esa cámara** (en la sala `1000`, la cámara 1 tiene un primer registro grande y luego dos zonas pequeñas que llevan a las cámaras 0 y 2). El juego guarda el área de la cámara actual (`0x992C30`) y **algunos enemigos comprueban si su posición cae dentro**, es decir, si están en plano, y se comportan distinto fuera de él (`0x47041B`).

Ejemplo (sala `1000`, 9 cámaras en cadena): cada cámara tiene zonas pequeñas en los bordes de su área que llevan a la cámara vecina (`cam3 → cam2`, `cam3 → cam4`). Las zonas de ida y de vuelta entre dos cámaras se dibujan por separado y no coinciden (en esta sala incluso se solapan en parte). No se comprobó cómo evita el juego el parpadeo en esa franja.

**Control desde los scripts de sala** (manejadores en la tabla de opcodes SCD de `0x544DE8`):

| Opcode | Efecto |
|---|---|
| `cut_chg(n)` | Corta a la cámara `n` y **desactiva el cambio automático** (para escenas) |
| `cut_auto(on)` | Activa o desactiva el cambio automático por zonas |
| `cut_old` | Vuelve a la cámara anterior al último `cut_chg` y reactiva el cambio automático |
| `cut_be_set(cám, n, valor)` | Activa o desactiva la zona `n` de una cámara (por ejemplo, bloquear un ángulo hasta que pase algo) |
| `cut_replace(a, b)` | **Intercambia dos cámaras** en todas las zonas de la sala: el mismo espacio pasa a usar otra cámara (por ejemplo, una variante del fondo tras un evento) |

### Qué cámara se activa al entrar en una sala

**La decide la puerta.** Cada `door_aot_se` guarda, además de la sala de destino, la posición y orientación de llegada, la planta y **`NextCamera`**, la cámara con la que empieza la sala siguiente. `NextStage` cuenta desde 0 (0 = etapa 1).

Comprobación con datos: en **539 de las 552 puertas (97,6%)**, la `NextCamera` es justo la cámara cuya área (el primer registro RVD de esa cámara, ver [Cambio de cámara](#cambio-de-cámara)) contiene el punto de llegada. Las 13 restantes son casos especiales (cámara 11 en algunas transiciones, posiblemente escenas como ascensores) o cuadriláteros que la prueba geométrica no resolvió.

Al cargar la sala (`0x4C4600`), el juego toma la cámara que trae la puerta, guarda el área de esa cámara y desde el primer frame aplica el cambio por zonas normal. **No se calcula la cámara buscando en qué área está el jugador**: es un dato puesto a mano en cada puerta, coherente con las áreas.

### AOT: los "hitboxes" de interacción

Todo lo interactuable o disparable de una sala es una **AOT** (*Area Of Trigger*), declarada en el script de inicialización con `aot_set`, `door_aot_se`, `item_aot_set` o sus variantes `_4p`.

- **Máximo 32 por sala.** El juego tiene un array de 32 punteros (`0x992E50`) que se vacía al entrar.
- **No se copian:** cada entrada **apunta a los bytes del propio script**. Por eso `aot_reset` y `aot_on` pueden reescribir o disparar una AOT en caliente.
- **Forma:** rectángulo (x, z, ancho, fondo) o, en las variantes `_4p`, un cuadrilátero de 4 puntos. El manejador marca este caso con el bit `0x80` del campo SAT, y la prueba de contacto usa la rutina de rectángulo (`0x4C4CF0`) o la de cuadrilátero (`0x4C4C40`) según ese bit.
- **Planta:** la AOT solo afecta a quien está en su misma planta.
- **Tipo (SCE):** qué hace al activarse (puerta, ítem, mensaje, evento, cambio de flag, agua, guardado, baúl, daño…).
- **Quién y cómo la activa (SAT):** flags combinables.

| Flag SAT | Significado (por nombre y uso) |
|---|---|
| `SAT_PL` | La activa el jugador |
| `SAT_EM` | La activan los enemigos |
| `SAT_SPL` | La activa el compañero (Ada / Sherry) |
| `SAT_OB` | La activan objetos |
| `SAT_MANUAL` | Hace falta pulsar Acción |
| `SAT_FRONT` | Se comprueba el área **delante** del personaje (alcance de interacción) |
| `SAT_UNDER` | Se comprueba la posición **del propio personaje** (al pisarla) |
| (ninguno) | AOT inerte: solo se dispara desde el script (`aot_on`) |

Combinaciones en las 250 salas de Leon:

| Uso | SAT | Tipos | Cuántas |
|---|---|---|---|
| Interactuar con botón | `PL \| MANUAL \| FRONT` | Puertas, ítems, mensajes, eventos, guardado, baúles | ~1300 |
| Disparador al pisar | `PL \| UNDER` | Eventos, zonas de daño, cambios de flag | ~130 |
| Zona de "usar ítem aquí" | `PL \| FRONT` (sin `MANUAL`) | `FLAG_CHG` con `FG_USE` | 42 |
| Puerta que hay que pisar | `PL \| MANUAL \| UNDER` | Puertas (escaleras, huecos) | 21 |
| Zonas solo para enemigos | `EM \| UNDER` | `NORMAL` | 26 |
| Eventos del compañero | `SPL \| UNDER` | Eventos | 13 |
| Agua | `PL \| EM \| (SPL) \| OB \| UNDER` | `WATER` | 27 |
| Inertes, disparadas por script | `SAT_AUTO` | Ítems escondidos, puertas y AOT especiales | 28 |

Lectura de diseño:

- **Interactuar = botón + área delante.** El jugador tiene que mirar hacia el objeto.
- **Los disparadores de paso usan la posición de los pies**, no la dirección: entras y salta.
- **La misma AOT sirve para mirar y para usar ítems** porque la zona `FG_USE` (sin `MANUAL`) se superpone al evento con `MANUAL`.
- **Hay AOT solo para enemigos**: el script delimita zonas que solo les afectan a ellos.

### Restos de depuración

Se buscaron herramientas de depuración en `LeonU.exe` (GOG). **No hay un modo debug accesible**: Capcom lo eliminó antes de publicar.

- **Textos:** ninguna cadena de menú de depuración, ni en ASCII ni en las dos codificaciones del juego (la de mensajes y la de menús, que es ASCII − `0x24`; por ejemplo `)AIKNU` = "Memory"). Las cadenas técnicas que quedan son mensajes de error de **Marni**, la librería gráfica del port de PC (`Direct3D::ChangeMode`, `TMDObject::In`…), útiles solo para identificar funciones del motor.
- **Opcode `break_point` (`0x1C`) de los scripts:** existe en la tabla de opcodes, pero su manejador es el mismo que `nop`: solo avanza el puntero. Lo mismo pasa con `0x1E`, `0x1F`, `0x20` y `0x63`. Probablemente en las versiones de desarrollo detenían el script para depurarlo.
- **Trucos de botones:** el único confirmado es el de munición infinita (`0x5092E0`). Una búsqueda de otros detectores de secuencia por patrón de código no fue concluyente.
- **Dato colateral:** existe el texto **"Discard?"**: el motor pregunta si descartar un objeto, seguramente las llaves agotadas. No se verificó en qué momento lo muestra.

Para ver los datos de depuración (zonas de cámara, AOT) hay que reconstruirlos desde fuera, como hace el visor de salas.

### Herramientas

- Desensamblador: [Capstone](https://www.capstone-engine.org/) 5.0.3 en un entorno virtual aislado, barrido lineal de `.text` (360 448 instrucciones) más un índice de referencias cruzadas a direcciones de datos.
- Método: partir de una tabla conocida (atributos de ítems), listar el código que la lee o escribe y seguir las variables globales que aparecen.

---

## Ítems por categoría (escenario de Leon)

Para cada categoría: qué ítems la componen, a qué arquetipo pertenecen y cómo funciona su interacción.

- **(datos):** verificado en los scripts y pickups de las salas.
- **(ejecutable):** verificado en las tablas de ítems del `.exe` (ver [Modelo de ítems del motor](#modelo-de-ítems-del-motor-ejecutable)).
- **(motor):** comportamiento del juego publicado que resuelve el código del ejecutable y que no se verificó en este análisis.

Las cantidades son las que trae cada pickup en el mundo (datos).

### Armas

| Ítem | Pickups | Carga inicial |
|---|---|---|
| Knife | 1 | — |
| Shotgun | 3 | 5 |
| Magnum | 2 | 8 |
| Colt S.A.A. | 1 | 6 |
| SMG | 2 | 100 |
| Flamethrower | 1 | 100 |
| Rocket Launcher | 2 | 2 / 10 |

- **Arquetipo:** recogible → equipable
- **Cómo funciona:** se recogen como cualquier ítem y vienen cargadas (datos). Se equipan desde el inventario; solo hay un arma equipada a la vez (motor).

### Munición

| Ítem | Pickups | Cantidad por pickup |
|---|---|---|
| Handgun Ammo | 58 | 15, 30 |
| Shotgun Ammo | 28 | 7 |
| Magnum Ammo | 13 | 8, 16 |
| Flame Rounds (granada) | 4 | 6, 12 |
| Acid Rounds (granada) | 2 | 6 |
| Explosive Rounds (granada) | 1 | 6 |
| Bowgun Ammo | 3 | 36 |
| SMG Ammo | 1 | 100 |

(Hay además un pickup de 250 balas de pistola, seguramente de un modo extra.)

- **Arquetipo:** recogible → consumible personal
- **Cómo funciona:** cada tipo de munición viene en una cantidad fija por pickup (datos). Se carga combinándola con el arma en cualquier orden, y dos pilas del mismo tipo se juntan (ejecutable, combinaciones de tipo 5/6). Ningún script de sala menciona munición: su uso nunca depende del lugar.

### Curativos

| Ítem | Pickups | Efecto (ejecutable) |
|---|---|---|
| Green Herb | 37 | 25% de la vida máxima |
| Red Herb | 9 | No se puede usar sola; potencia a la verde |
| Blue Herb | 12 (+ 8 plantas) | Cura el veneno |
| First Aid Spray | 13 | 100%, pero penaliza el rango |

Mezclas que existen (ejecutable, combinaciones de tipo 11): GG, GGG, GR, GB, GGB, GRB. **No existe R+B.**

- **Arquetipo:** recogible → consumible personal; las plantas azules son recurso del entorno
- **Cómo funciona:**
  - Se usan desde el inventario sobre el personaje (motor).
  - Las hierbas se combinan entre sí en el inventario y la mezcla ocupa una sola casilla (ejecutable). Combinar es a la vez curar mejor y ahorrar espacio.
  - Las plantas de hierba azul se usan en el lugar con Sí/No y no entran al inventario (datos).

### Guardado y almacén

| Elemento | Datos |
|---|---|
| Ink Ribbon | 21 pickups, **3 usos cada uno** |
| Máquina de escribir (`SCE_SAVE`) | 28 salas |
| Baúl (`SCE_ITEMBOX`) | 25 salas, casi siempre en la misma sala que la máquina |

- **Arquetipo:** punto de guardado (máquina) y almacén (baúl)
- **Cómo funciona:**
  - Con cinta, la máquina pregunta si quieres guardar y gasta un uso (motor).
  - Sin cinta, solo describe: "I could save my progress if I had an {ink ribbon}." (datos)
  - Todos los baúles comparten el mismo contenido (motor). Por eso aparecen en las salas de guardado: el jugador reorganiza su inventario donde está a salvo.

### Mejoras

| Ítem | Interacción |
|---|---|
| Handgun Parts, Shotgun Parts, Magnum Parts | Se combinan con el arma → Custom Handgun / Shotgun / Magnum (ejecutable, combinación de tipo 4) |
| Side Pack | En el mundo, con Sí/No: "With a side pack, I can carry more items. Will you equip it?" (datos). Amplía el inventario. |

- **Arquetipo:** combinación (piezas) y acción ambiental con Sí/No (side pack)

### Llaves

Ver [Cerradura de puerta](#cerradura-de-puerta--automática) y [Cerradura de herramienta](#cerradura-de-herramienta--confirmada-síno) en el catálogo.

- **Llaves de puerta:** automáticas. Sus usos = número de cerraduras (datos).
- **Llaves de mueble o herramienta** (Small Key, Special Key, Weapon Box Key, Lockpick): detecta + Sí/No (datos).
- **"Llaves" de panel** (Up/Down Key): receptáculo, desde el inventario (datos).

### Ítems de puzzle

Ver [Receptáculo](#receptáculo--elegida-desde-inventario) y [Transformador](#transformador--elegida) en el catálogo. Siempre desde el inventario (datos). Se consumen, salvo las herramientas reutilizables (Valve Handle, Lighter).

### Mapas

- **No son ítems:** no ocupan inventario.
- **Interacción:** acción ambiental con Sí/No. "A {police station map}. Will you take it?" (datos)

### Documentos

- **Arquetipo:** recogible
- **Cómo funciona:** se recogen con el mismo mecanismo que los ítems (`item_aot_set`) pero van al archivo de documentos, no al inventario (datos). Ver la lista en el catálogo.

---

## Catálogo de ítems clave (escenario de Leon)

Generado cruzando todas las referencias a cada ítem en los scripts desensamblados. Las salas se indican con su ID de archivo (`ROOMxxxx`): primer dígito = etapa, dos siguientes = sala. El `.RDT` no guarda el nombre de la sala.

### Cerradura de puerta — automática

| Ítem | Puertas (sala) | Se obtiene en |
|---|---|---|
| Cabin Key | 1050 | 1050 |
| Spade Key | 1140, 2040 | 2000 |
| Diamond Key | 2070, 20C0 | 10B0, 1130 |
| Heart Key | 20B0 | 2020 |
| Club Key | 20C0, 20F0, 2110 | 3060, 30B0 |
| Power Room Key | 6030 | 6150 |
| Umbrella Key Card | 6140 | 60A0, 6150 |
| Master Key | 6060 (además, panel en 6010) | 6040 |
| Platform Key | 7000 | 7040 |

**La cantidad del pickup es el número de cerraduras que abre la llave:** Club Key trae 3 (3 puertas), Diamond Key 2 (2 puertas), Master Key 2 (una puerta y el panel), Umbrella Key Card 2. Ningún script de sala quita estas llaves: el contador y el descarte los maneja el motor.

### Cerradura de herramienta — confirmada (Sí/No)

| Ítem | Sala | Se consume | Notas |
|---|---|---|---|
| Small Key | 4010 | Sí | Tiene prioridad sobre la ganzúa |
| Lockpick | 2090, 4010 | No | Herramienta de Claire; los mensajes están en las salas compartidas |
| Special Key | 2080 | Sí | Taquilla del cuarto oscuro (trajes) |
| Weapon Box Key | 6150 | Sí | Caja de armas |
| Master Key | 6010 | Sí | Acepta también uso desde inventario (doble entrada) |

### Receptáculo — elegida (desde inventario)

| Ítem | Sala | Se consume | Notas |
|---|---|---|---|
| Red Jewel | 10B0 | Sí | Dos relieves: "There's a hole about the size of a fist." |
| Unicorn Medal | 2000 | Sí | Pista en la fuente de la misma sala |
| Eagle Medal / Wolf Medal | 4070 | Sí | Progreso parcial: "The wolf medal is in place." |
| Bishop / Rook / Knight / King Plug | 3030 | Sí | Cuatro piezas en un mismo receptáculo |
| Red Card | 2110 | Sí | Precondición: sin energía no se consume |
| Crank | 1110 | Sí | |
| Cog | 1110 | Sí | |
| Cord | 2040, 20F0 | Sí | |
| Manhole Opener | 21A0 | Sí | |
| Main Fuse | 6080, 6110 | Sí | |
| Up Key / Down Key | 5030 / 5040 | Sí | "Llaves" que van en un panel |
| MO Disk | 6160 | Sí | |
| Joint Plug Blue + Red | 7020 | Sí, las dos | Pareja: usar una coloca ambas |
| Valve Handle | 1080, 4080, 4090 | **No** | Herramienta reutilizable |
| Lighter | 2060, 20D0, 4030, 5010, 60A0 | **No** | Herramienta reutilizable |

### Transformador — elegida

| Ítem | Sala | Resultado |
|---|---|---|
| Film A / B / C / D | 2080 | Documento "Developed Film", entregado con el diálogo de recogida |
| Fuse Case | 60D0 | Ítem Main Fuse, entregado con el diálogo de recogida (y ofrecido de nuevo si no se toma) |

### Detección pasiva

| Ítem | Sala | Notas |
|---|---|---|
| Blue Card | 10B0 | El script reacciona a que el jugador la lleve (`keep_item_ck`) y dispara un evento, sin pregunta ni uso. No se confirmó qué evento es. |

### Documentos

Los documentos usan el **mismo mecanismo de recogida que los ítems** (`item_aot_set`); solo cambia a dónde van.

| Documento | Sala |
|---|---|
| Chris's Diary | 1150 |
| Memo to Leon | 2020 |
| Police Memorandum | 2010 |
| Operation Report 1 | 2060 |
| Mail to the Chief 2 | 3010 |
| Secretary's Diary A | 10F0 |
| Secretary's Diary B | 10E0 |
| Operation Report 2 | 2080 |
| User Registration | 60A0 |
| Patrol Report | 2030 |
| Watchman's Diary | 2100 |
| Chief's Diary | 21B0 |
| Sewer Manager Diary | 4090 |
| Sewer Manager Fax | 4030 |
| Lab Security Manual | 60A0 |
| P-Epsilon Report | 6030 |
| Developed Film A / B / C / D | 2080 (aparecen al revelar los rollos) |

### Sin uso en los scripts de Leon

Chemical (ACw-32), mezclas de hierbas, Photo Sherry, Photo Ada, Serpent Stone, Jaguar Stone (L/R), Eagle Stone, Detonator, C4, Vaccine / Vaccine Cart / Vaccine Base y G-Virus. Algunos se recogen en salas de Leon (Jaguar Stone R en 1110, Eagle Stone en 20E0, G-Virus en 60C0) pero su uso no aparece en ningún script de sala. Pueden pertenecer al escenario de Claire, resolverse en el motor (combinaciones) o usar un mecanismo que no se analizó.

---

## Lectura de diseño

RE2 aplica estas reglas con mucha consistencia: las puertas no hacen trabajar al jugador, las herramientas piden permiso antes de gastarse y los puzzles exigen que el jugador elija. Esa consistencia permite que el jugador aprenda el lenguaje del juego en la primera hora y lo use sin pensar el resto de la partida.

La resolución elegida protege la deducción: si el pedestal aceptara la medalla automáticamente, la pista de la fuente no serviría de nada. La resolución confirmada protege los recursos escasos.

Los casos especiales comparten una idea: **el juego nunca castiga al jugador por un detalle de interfaz**.

---

## Método y alcance

- **Formato:** documentado por [IntelOrca/biohazard-utils](https://github.com/IntelOrca/biohazard-utils) (`Rdt2.cs`, `Bio2ConstantTable.cs`, `Msg.cs`). En el `.RDT`, la tabla de offsets 14 son los mensajes en inglés, la 16 el script de inicialización y la 17 los procedimientos principales.
- **Señales clave en el bytecode:**
  - `V_USED_ITEM`: ítem que el jugador usó desde el inventario (receptáculos)
  - AOT `SCE_FLAG_CHG` del grupo `FG_USE` con el ID de un ítem: declara que ese ítem se puede usar en esa zona. Hay uno por cada ítem aceptado, y coinciden 1:1 con las salas que comparan `V_USED_ITEM`.
  - `aot_on(AOT_n)`: dispara un AOT desde el script como si el jugador lo hubiera activado. Sobre un pickup fuera del alcance, sirve para entregar un ítem.
  - `keep_item_ck`: ¿el jugador lleva el ítem? (detección automática)
  - `sce_item_lost` / `sce_item_ck_lost`: quitar el ítem
  - parámetro de llave de `door_aot_se`: llave de la puerta
  - byte `0xFB` en los mensajes: pregunta Sí/No
- **Fuera del alcance:**
  - El escenario de Claire no se analizó.
  - Del ejecutable se leyeron las tablas de datos y se siguieron las rutas de código descritas en [Curiosidades](#curiosidades-del-código-del-ejecutable). Siguen sin verificar: el uso automático de llaves en puertas (se infiere de que la llave está en los datos de la puerta sin script asociado) y el examen en el inventario.
- No se copió código ni assets del juego: solo se extrajeron reglas de diseño.
