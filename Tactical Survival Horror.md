# Tactical Survival Horror

## Definicion del Genero

**Tactical Survival Horror** — un genero donde el horror no viene de lo sobrenatural sino del **desgaste sistematico**: recursos que se agotan, cuerpos que se deterioran, y decisiones tacticas con consecuencias irreversibles. El combate existe como **costo**, no como progresion. La pregunta central del jugador no es "soy lo suficientemente fuerte?" sino **"puedo permitirme pelear?"**

### Los tres ejes

1. **Tactico** — Cada accion de combate requiere lectura del enemigo, seleccion de municion, y ejecucion con skill expression. No hay ataques genericos.
2. **Survival** — Los recursos son finitos y justificados narrativamente. No hay reabastecimiento magico. Cada bala es una micro-decision economica.
3. **Horror** — El horror es fisico y politico, no sobrenatural. Los enemigos son humanos destruidos. El sistema que los destruyo es el verdadero antagonista.

---

## Pilares de Diseño

### Pilar 1: El combate es costo, no recompensa

Combatir gasta recursos irrecuperables (municion, salud, integridad del party). La progresion no es de empoderamiento sino de desgaste — el jugador termina el juego mas debil que como empezo. El combate a veces es obligatorio, a veces evitable, y a veces un error. El sistema de objetivos reactivos refuerza esto: ignorar enemigos puede escalarlos, pero eliminarlos consume recursos que no vuelven.

### Pilar 2: Agencia bajo presion

El jugador siempre tiene control sobre sus acciones, pero ese control se degrada con el estado fisico del operador. El QTE bidimensional da agencia real (el jugador apunta, no un dado), pero la vibracion por latido, las distracciones visuales y la dispersion por vida erosionan esa agencia progresivamente. Nunca se le quita el control al jugador — se le hace mas dificil ejercerlo.

### Pilar 3: Consecuencias irreversibles

No hay revive, no hay recarga magica, no hay segunda oportunidad. Los personajes mueren permanentemente y sus armas se pierden con ellos. La municion gastada no vuelve. La exposicion al Krokonil no se revierte facilmente. Cada decision tiene peso porque no se puede deshacer.

### Pilar 4: Horror tangible

Los enemigos no son monstruos — son humanos en colapso neuroquimico. La proteccion que llevan es equipamiento militar real, no stats magicos. Las armas son reales con balistica real. El horror viene de la plausibilidad: todo lo que ocurre *podria* pasar. El verdadero antagonista no es biologico sino politico.

### Pilar 5: Informacion como recurso

La lectura visual del enemigo (proteccion, fase de deterioro, zona expuesta) es una habilidad del jugador, no un stat del personaje. La proteccion es visible, las hitboxes son geometria real, y la decision de donde apuntar y con que municion es informada. El jugador que lee bien al enemigo gasta menos recursos.

---

## Reglas del Genero

Reglas concretas que funcionan como guia para cualquier decision de diseño. Si un sistema nuevo viola alguna de estas reglas, no pertenece al genero.

### Reglas de combate

- Todo disparo consume municion fisica. No hay ataques "gratis" excepto el sidearm (que existe solo para evitar softlocks, no como recurso infinito).
- El jugador siempre dispara — nunca falla por timing. La calidad del disparo depende de su ejecucion, no de un dado.
- El daño del jugador se siente en el gameplay, no solo en un numero. Un operador herido apunta peor, ve peor, y tiembla mas.
- Cada arma tiene identidad mecanica (patron de recoil, dispersion, sonido). No hay armas genericas con stats intercambiables.

### Reglas de recursos

- Todo recurso tiene justificacion narrativa. Si no puede explicarse por que esta ahi, no esta.
- No hay tiendas, drops aleatorios, ni puntos de reabastecimiento convenientes.
- La municion es finita a nivel global — lo que existe en el barco es todo lo que hay.
- Cada recarga es una decision tactica (que tipo de municion cargar, cuando hacerlo).

### Reglas de horror

- Los enemigos son humanos. Siempre. Su comportamiento se explica por quimica, no por magia.
- La muerte de personajes es permanente y tiene impacto mecanico (se pierde su arma y habilidades).
- El deterioro del jugador es progresivo y visible — no hay estados binarios de "bien" y "muerto".
- El horror politico se comunica a traves de sistemas y descubrimientos, no de dialogos expositivos.

### Reglas de informacion

- La proteccion enemiga es visible. El jugador nunca deberia sentir que perdio por informacion oculta.
- El feedback de combate es inmediato y claro: el sonido, el visual y el numero comunican la calidad del disparo antes de que el jugador necesite leer texto.
- Los sabotajes y conspiraciones se siembran con pistas mecanicas (ausencias, coincidencias) antes de revelarse narrativamente.

---

## Como los Sistemas Refuerzan el Genero

### QTE Bidimensional → Agencia bajo presion

El ataque no es automatico ni aleatorio. El jugador elige donde apuntar en dos ejes con timing real. Esto elimina la frustracion del "95% miss" tipo XCOM y reemplaza los dados por skill expression. Pero esa agencia tiene un costo: ejecutar bien requiere concentracion, y el juego ataca esa concentracion cuando el operador esta herido.

### Dispersion de tres capas → Costo + Agencia

L1 (vida) castiga al jugador herido. L2 (arma) da identidad mecanica. L3 (recoil) penaliza la avaricia — rafagas largas pierden control. Las tres capas trabajan juntas para que cada disparo sea una negociacion entre lo que el jugador quiere y lo que su situacion permite.

### Sistema de rafaga y recoil → Costo

Cada disparo adicional en una rafaga es una apuesta: mas daño potencial pero menos precision y mas municion gastada. Los patrones de recoil son aprendibles (skill expression) pero nunca eliminables. El jugador ambicioso paga con MISS en los ultimos disparos.

### Distracciones visuales progresivas → Agencia bajo presion

Screen shake, viñeta de sangre, ruido estatico, ghost lines y parpadeo de silueta no son estetica — son penalizaciones reales que degradan la capacidad del jugador de leer el campo. Los umbrales escalonados (5% → 35% daño) crean una curva donde el jugador *siente* el deterioro gradualmente, no como un switch binario.

### Proteccion por capas → Informacion como recurso

La armadura no es un numero global. Es geometria visible: un casco que deja la mandibula expuesta, un chaleco con un hombro arrancado. El jugador que lee la proteccion y apunta a las zonas expuestas gasta menos balas que el que dispara al centro de masa por defecto.

### Municion real con tipos tacticos → Costo + Consecuencias + Informacion

Frangible vs FMJ no es "mejor o peor" — es situacional. Las RIP destrozan carne pero rebotan en placas. Las FMJ penetran proteccion pero hacen menos daño a carne expuesta. Al recargar, el jugador elige tipo de municion. Una mala eleccion se paga en balas desperdiciadas contra la proteccion equivocada.

### Muertes permanentes y party variable → Consecuencias irreversibles

Los MSRT mueren por desgaste acumulado en 4 encuentros. El CIA se pierde por traicion. Cada perdida es mecanica: se pierden armas, habilidades y opciones tacticas permanentemente. El jugador siente la ausencia en cada combate posterior.

### Krokonil como dilema moral → Costo + Horror tangible

Usar microdosis mejora punteria temporalmente pero aumenta el deterioro. El jugador usa la misma droga que destruyo a los enemigos que enfrenta. Eso es horror ludonarrativo: la mecanica cuenta la misma historia que la narrativa.

### Heartbeat sincronizado → Agencia + Horror

El latido no es decorativo. Afecta la posicion real de las barras QTE (mecanico), distorsiona la pantalla (visual) y suena en los oidos del jugador (sonoro). Los tres canales comunican el mismo mensaje: tu operador se esta muriendo. A 10% HP el jugador siente el panico fisicamente, no solo lo lee en un numero.

---

## El Loop Emocional

### El ciclo central del Tactical Survival Horror

```
Amenaza → Evaluacion → Decision → Ejecucion → Consecuencia → Ansiedad
    ↑                                                            |
    └────────────────────────────────────────────────────────────┘
```

1. **Amenaza** — El jugador encuentra enemigos. Lee su proteccion, su fase de deterioro, su cantidad.
2. **Evaluacion** — "Puedo evitarlos? Cuantas balas me cuesta? Que municion uso? Vale la pena un headshot o juego seguro al torso?"
3. **Decision** — Elige pelear, evitar, o gastar recursos premium. Elige arma, tipo de municion, cantidad de disparos.
4. **Ejecucion** — QTE bidimensional. El estado fisico del operador interfiere. El jugador ejecuta bajo presion real.
5. **Consecuencia** — Balas gastadas, daño recibido, personajes debilitados. Todo es permanente.
6. **Ansiedad** — "Me quedan 8 balas. El proximo encuentro puede ser peor. Deberia haber evitado ese combate."

### Lo que distingue este loop

- La ansiedad no se resuelve — se acumula. No hay momento de "ahora soy poderoso".
- Cada vuelta del loop deja al jugador con menos que la anterior.
- La *unica* forma de romper la espiral es tomar mejores decisiones tacticas, no grindear poder.
- El horror no esta en los sustos — esta en la curva descendente de recursos y la certeza creciente de que no van a alcanzar.

---

Volver a [[Crimson Draft]]
