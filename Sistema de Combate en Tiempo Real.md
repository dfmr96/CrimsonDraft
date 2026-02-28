# Sistema de Combate en Tiempo Real

## Concepto Central

El combate ocurre en **tiempo real**. No hay turnos ni barra ATB — cada acción **ocupa** al personaje por una duración determinada, durante la cual no puede recibir nuevas órdenes. Los enemigos atacan en sus propios timers independientemente de lo que haga el jugador.

El sistema no tiene barra de espera. El tiempo de ocupación de cada acción ES el cooldown.

> La única restricción para disparar no es tiempo de espera — es munición. El jugador siempre puede disparar si tiene balas. Esto crea la tensión central: disparar es siempre una opción, pero cada disparo es un recurso que no vuelve.

---

## Acciones y Tiempos de Ocupacion

| Accion | Ocupacion | Determinada por |
|---|---|---|
| **Disparar** | Duracion del QTE (~2-3s) | Velocidad de barra del arma |
| **Recargar** | Tiempo de recarga | Tipo de arma |
| **Cambiar arma** | Muy corto (~0.5s) | Fijo |
| **Seleccion de municion** | Incluido en recarga | — |

La velocidad del QTE (y por tanto el tiempo de ocupacion del disparo) varia por arma:
- Pistola: barra lenta → QTE mas largo → mayor ocupacion por disparo
- Rifle: barra rapida → QTE corto → menor ocupacion por disparo
- Escopeta: barra lenta + perdigones multiples → QTE largo, alto potencial

---

## Estados del Personaje

Cada miembro del party puede estar en uno de estos estados en cualquier momento:

| Estado | Descripcion |
|---|---|
| **Libre** | Disponible, puede recibir ordenes del jugador |
| **Ocupado — QTE** | QTE activo, barra en movimiento, no puede recibir ordenes |
| **Ocupado — Recarga** | Barra de progreso de recarga corriendo |
| **Ocupado — Cambio** | Animacion corta de cambio de arma |
| **Cargador vacio** | Libre pero no puede disparar, necesita orden de recargar |
| **Muerto** | Fuera del combate permanentemente |

Los personajes en cualquier estado **pueden recibir daño** normalmente. Un personaje ignorado muere si los enemigos lo golpean suficiente.

---

## Control del Party

El jugador controla **un personaje a la vez**. Mientras controla a uno, los demas quedan en **idle** — no atacan, no recargan, no actuan por si solos.

### Flujo de seleccion

1. El jugador ve todos los personajes en pantalla con su estado visible
2. Selecciona a quien quiere controlar (cambio rapido entre miembros)
3. Emite la orden: disparar, recargar, cambiar arma
4. El personaje entra en estado **OCUPADO** y ejecuta la accion
5. El jugador puede cambiar de personaje inmediatamente, sin esperar que el primero termine

### Lo que esto permite

- Iniciar un QTE con Mateo → cambiar a un SEAL para ordenarle recargar → volver a Mateo para ver el resultado del QTE
- Dar ordenes en rapida sucesion a diferentes personajes para coordinar el party
- Ignorar a un personaje hasta que su cargador quede vacio — y pagar las consecuencias

---

## Comportamiento de Enemigos

Cada enemigo tiene su propio **timer de ataque**, invisible para el jugador. Cuando el timer se completa, el enemigo ejecuta su ataque contra el personaje del party mas cercano o mas amenazante segun su fase de deterioro.

### Timers por tipo de enemigo

| Tipo | Frecuencia de ataque | Comportamiento |
|---|---|---|
| Tripulacion civil | Lento (~5s) | Erratico, a veces no ataca |
| Seguridad del barco | Medio (~3s) | Usa cobertura, apunta |
| Infectado Fase 1 | Lento (~4s) | Cuerpo a cuerpo, se acerca |
| Infectado Fase 2 | Rapido (~2s) | Embiste, ignora dolor |
| Infectado Fase 3 | Medio (~3s) | Usa armas si las tiene |
| Rusos reactivados | Medio-lento (~3.5s) | Disparan, buscan cobertura |

### Reglas del ataque enemigo

- **El ataque enemigo no interrumpe al jugador.** Si el jugador esta en medio de un QTE cuando un enemigo ataca, el QTE continua. El daño se aplica al HP y los efectos visuales del QTE se agravan (mas shake, mas vignette).
- **Los enemigos no usan QTE.** Su daño es automatico y fijo. La skill expression del jugador es en el ataque, no en la defensa. Esquivar no existe.
- **Recibir daño es inevitable.** Solo se puede minimizar priorizando bien.

---

## Transicion desde Free Roam

El combate ocurre en una **escena separada cargada de forma aditiva** sobre la escena de exploración. La escena de navegación permanece activa en segundo plano durante todo el combate.

### Inicio del combate

El combate se inicia por **colisión de proximidad**: cada enemigo en el mapa de exploración tiene un **trigger de área**. Cuando el jugador entra en ese área:

1. El input de exploración se desactiva — el jugador no puede moverse
2. La escena de combate se carga **additively** — la navegación queda congelada en fondo
3. Los personajes del party aparecen a la **izquierda**, los enemigos a la **derecha**
4. El **QTE bidimensional** ocupa el **centro** de la pantalla
5. Los **comandos, HP, presion arterial y municion** se muestran en la **zona inferior**
6. Los timers de todos los enemigos comienzan desde 0
7. Todos los personajes del party quedan en estado **Libre** desde el primer frame
8. No hay periodo de gracia — el jugador puede actuar inmediatamente

### Fin del combate

| Condicion | Resultado |
|---|---|
| Todos los enemigos eliminados | **Victoria** — se descarga la escena de combate, vuelve exploración |
| Mateo muere | **Game over** (o checkpoint segun implementacion) |
| Muere un aliado | Combate continua sin ese personaje, permanentemente |

Al terminar el combate (victoria), la escena de combate se descarga y el input de exploración se reactiva exactamente donde quedó.

No hay movimiento táctico durante el combate — la táctica es en la selección de acciones, no en el posicionamiento.

---

## El Loop de Tension

Con multiples personajes activos y enemigos atacando independientemente, el jugador enfrenta constantemente el problema de la atencion:

```
Mateo en QTE (2s ocupado)
  └─ SEAL 1: cargador vacio → necesita recarga
  └─ SEAL 2: HP bajo → enemigo le apunta
  └─ Enemigo A: timer a punto de completarse
  └─ Enemigo B: ya ataco, timer reiniciando
```

### La trampa del disparo facil

Como disparar siempre esta disponible, el jugador inexperto va a disparar con el personaje activo continuamente en lugar de rotar. Esto resulta en:

- El personaje activo gasta toda su municion rapidamente
- Los personajes idle tienen cargadores llenos pero nadie los controla
- Al necesitar recargar, el jugador pierde tiempo que los enemigos usan para atacar

### La decision optima

Rotar entre personajes: disparar con uno, y mientras espera el QTE cambiar al otro para recargar o dar ordenes. Rotar requiere disciplina y lectura constante del estado del party — exactamente la skill que el sistema quiere ensenar.

---

## Integracion con Sistemas Existentes

El QTE bidimensional ya disenado se inserta como la **resolucion del disparo**. El flujo completo de un ataque:

```
Jugador selecciona personaje
  → elige "Disparar" + cantidad de balas
  → se activa el QTE bidimensional (eje Y → eje X)
  → durante el QTE el tiempo del combate sigue corriendo
  → se resuelve impacto con dispersion 3 capas + armor + tipo de municion
  → QTE termina → personaje libre de nuevo
```

### Como se amplifican las mecanicas existentes

| Mecanica | Impacto en combate en tiempo real |
|---|---|
| **Heartbeat + distracciones visuales** | El jugador herido hace peor el QTE mientras el tiempo real presiona desde afuera |
| **Armor por capas** | Decide si gasta el QTE en un headshot dificil o un torso facil pero bloqueado |
| **RIP vs FMJ en recarga** | La recarga implica una decision de tipo de municion mientras los enemigos siguen atacando |
| **Dispersion por vida** | A HP bajo el QTE se degrada justo cuando mas presion hay |
| **Muerte permanente de aliados** | Perder a un personaje reduce las acciones disponibles para siempre |

---

## Condiciones de Fin de Combate

| Condicion | Resultado |
|---|---|
| Todos los enemigos eliminados | **Victoria** — vuelve a free roam |
| Mateo muere | **Game over** (o checkpoint segun implementacion) |
| Muere un aliado | Combate continua sin ese personaje, sin su arma, permanentemente |

La muerte de un aliado no termina el combate, pero lo hace mecanicamente mas dificil para el resto del juego. Esto es intencional — ver [[Diseño de Combate y Armas#Muertes por desgaste]].

---

Volver a [[Crimson Draft]]
