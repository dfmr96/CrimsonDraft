# Sistema de Salud

## Filosofia

No hay barra de vida. No hay numeros visibles de HP. El jugador lee el estado de sus operadores a traves de **signos vitales** — el mismo ECG, BPM y presion arterial que ya existen en el prototipo. La salud se siente, no se lee.

Dos recursos independientes. Dos vias de muerte. Dos items para tratarlos. Y una droga que lo enmascara todo a cambio de destruir al operador permanentemente.

---

## Modelo de Salud — Dos Recursos, Dos Muertes

### Variables internas por operador

| Variable | Rango | Descripcion |
|---|---|---|
| **`hp`** | 0-100 | Integridad fisica. Baja con impactos directos y hemorragia. Se cura con IFAK |
| **`hp_max`** | 100 | Maximo (fijo) |
| **`bleed_rate`** | 0-3 | Nivel de hemorragia activa. 0=ninguna, 1=leve, 2=moderada, 3=severa |
| **`systolic`** | 40-140 | Presion arterial sistolica. Baja con hemorragia. Normal ~120 |
| **`diastolic`** | 30-90 | Presion arterial diastolica. Normal ~80 |
| **`krk_exposure`** | 0-100 | Exposicion acumulada al Krokonil. Solo sube. Nunca baja |
| **`krk_mask_timer`** | float | Turnos restantes de enmascaramiento activo. 0 = sin efecto |

### Dos vias de muerte

| Via | Condicion | Causa tipica |
|---|---|---|
| **Muerte por trauma** | `hp <= 0` | Muchos impactos directos, o hemorragia no tratada drenando HP |
| **Muerte por shock** | `systolic <= 40` | Hemorragia prolongada sin torniquete. La presion se desploma |

Ambas causan **permadeath** — el operador muere, se pierde su arma y habilidades permanentemente.

---

## Daño e Impacto

### Cuando un operador recibe un impacto

```
Impacto → HP baja (daño directo) + probabilidad de hemorragia (segun zona)
```

**Daño directo:** Cada impacto reduce `hp` inmediatamente. El calculo de daño ya existe (daño_base_arma x multiplicador_zona x multiplicador_precision x factor_proteccion).

**Probabilidad de hemorragia:** Ademas del daño directo, ciertos impactos inician o agravan una hemorragia:

| Zona impactada | Probabilidad | Nivel que inicia |
|---|---|---|
| Cabeza | 80% | 2 (moderada) |
| Torso | 60% | 2 (moderada) |
| Brazos | 40% | 1 (leve) |
| Piernas | 50% | 1 (leve) |

Si ya hay hemorragia activa y recibe otro impacto que causa sangrado, el nivel **sube** (1→2, 2→3). Nunca baja sola.

---

## Hemorragia

### Drenaje por tick

La hemorragia drena **HP y presion simultaneamente** cada tick (turno de combate o intervalo en exploracion):

| Nivel | Nombre | HP / tick | Sistolica / tick |
|---|---|---|---|
| 0 | Sin hemorragia | 0 | 0 |
| 1 | Leve | -1 | -2 |
| 2 | Moderada | -3 | -5 |
| 3 | Severa | -6 | -10 |

### Compensacion y descompensacion

El corazon intenta compensar la perdida de sangre:

| Fase | Sistolica | BPM | Señal para el jugador |
|---|---|---|---|
| Compensacion | 60-100 | Sube (hasta ~160) | "El corazon pelea por sobrevivir" |
| Descompensacion | 40-60 | Empieza a **bajar** | "Se acabó el tiempo" — muerte inminente |
| Muerte | ≤ 40 | Linea plana | Permadeath |

**La señal clave:** cuando el BPM deja de subir y empieza a caer, el jugador sabe instintivamente que el operador se muere. No necesita leer numeros.

### Umbrales de presion arterial

| Sistolica | Estado | Efecto |
|---|---|---|
| 120-140 | Normal / estres leve | Sin penalizacion |
| 100-119 | Hipotension leve | BPM sube como compensacion |
| 80-99 | Hipotension moderada | BPM alto, distracciones QTE activas |
| 60-79 | Shock compensado | BPM maximo (~160), QTE severamente degradado |
| 40-59 | Shock descompensado | BPM cae, presion oscila. Ultimos turnos |
| ≤ 40 | **Muerte** | ECG linea plana. Permadeath |

---

## Lectura del ECG — Sin Numeros Visibles

El jugador lee el estado de cada operador a traves de un unico monitor de ECG con tres canales de informacion:

| Propiedad del ECG | Comunica |
|---|---|
| **Color** (verde → amarillo → rojo) | HP. Verde = sano. Rojo = HP critico |
| **Numero de presion** (ej: "120/80") | Presion arterial. Cae con hemorragia |
| **Velocidad (BPM)** | Estres general. Sube con daño y hemorragia |

### Lectura combinada

| Situacion | Color ECG | Presion | BPM | Lectura del jugador |
|---|---|---|---|---|
| Sano | Verde | 120/80 | 72 | "Esta bien" |
| Trauma alto, sin hemorragia | Rojo | 120/80 | 110 | "Dañado pero estable. Necesita IFAK" |
| Trauma bajo, hemorragia severa | Verde | 75/50 | 140 | "No esta tan dañado pero se desangra. Torniquete YA" |
| Trauma alto + hemorragia | Rojo | 65/45 | 155→bajando | "Necesita todo, ahora" |
| Krokonil activo | Verde perfecto | 118/76 | 68 | "Todo bien"... o demasiado bien? |

---

## Items Medicos

### Catalogo

| Item | Efecto | Sobre HP | Sobre hemorragia | Sobre presion | Tamaño inv. | Escasez |
|---|---|---|---|---|---|---|
| **Torniquete CAT** | Detiene hemorragia | No cura HP | Nivel → 0 | Deja de caer + recupera lento | 1x1 | Moderada |
| **IFAK** | Cura trauma | +20 HP (progresivo, ~3 ticks) | No detiene hemorragia | No afecta | 1x2 | Alta |
| **Microdosis Krokonil** | Enmascara todo (ver seccion Krokonil) | No cura | No detiene | No recupera | 1x1 | Moderada-alta |

### Justificacion narrativa — donde se encuentran

| Fuente | Items |
|---|---|
| Equipo propio (inicio) | 2-3 torniquetes + 1-2 IFAKs por operador |
| Cadaveres rusos | Torniquetes, IFAKs ocasionales |
| Enfermeria del barco | IFAKs, torniquetes extras |
| Cargamento Krokonil | Viales de microdosis (contenedores, zonas contaminadas) |
| Kit del agente CIA | IFAKs premium (se pierden al revelarse como traidor) |

### Decision tactica

Los items compiten por espacio en el inventario 4x4:
- Torniquetes son pequeños (1x1) pero solo sirven para hemorragia
- IFAKs son mas grandes (1x2) pero solo curan HP
- Ambos compiten con municion por espacio
- El jugador que carga puro IFAK muere desangrado
- El jugador que carga puro torniquete muere por trauma

### La trampa del IFAK

Un jugador puede usar IFAKs para mantener su HP alto (ECG verde) mientras ignora una hemorragia. La presion sigue cayendo por detras. Muere con ECG verde y presion en 40 — "estaba sano pero se desangró".

---

## Krokonil como Item — El Engaño al Jugador

### Fase 1: "Regulador metabolico" (Acto I final / Acto II)

El jugador encuentra viales entre equipamiento ruso o en la enfermeria. Un documento los presenta como herramienta tactica:

> **DOCUMENTO:** *"Regulador Metabolico KRK-NL v2.3"*
> - Estabiliza signos vitales
> - Suprime respuesta al dolor
> - Aumenta capacidad de respuesta tactica
> - Duracion: 4-5 minutos
> - *"Aprobado para uso operacional — Ministerio de Defensa"*

**UI del inventario (antes de la revelacion):**

| Campo | Texto mostrado |
|---|---|
| Nombre | Regulador KRK-NL |
| Descripcion | Estabilizador de campo. Protege de daño letal temporalmente |
| Efecto | Inmunidad temporal. ECG estable. Suprime penalizaciones |
| Advertencia | Ninguna |

El jugador lo usa libremente como super-item.

### Fase 2: "Algo no esta bien" (Acto II tardio / Acto III)

El medico SEAL nota anomalias. Lineas de dialogo despues de usar Krokonil:
- *"Esos viales... le revise las pupilas a [operador]. Algo no cuadra."*
- *"Su pulso base subio. No deberia estar asi despues de un estabilizador."*
- *"[Operador] me pidio otro vial. Dijo que se sentia raro sin el."*

Mecanicamente: temblor sutil nuevo en operadores con varias dosis. Sin explicacion en UI.

### Fase 3: "Es la misma droga" (Acto III)

El medico confirma tras analizar los viales:

> *"Es el mismo compuesto. Los suplementos de los contenedores, los viales que estamos usando, el concentrado que destruyo a la tripulacion... es todo Krokonil. Diferentes dosis."*
>
> *"Cada vez que usamos esos viales... les hicimos a nuestros operadores lo mismo que les hicieron a ellos."*

**La UI se actualiza:**

| Campo | Antes | Despues |
|---|---|---|
| Nombre | Regulador KRK-NL | **Microdosis Krokonil** |
| Descripcion | Estabilizador de campo | **Droga neuroquimica. Enmascara sintomas. NO CURA** |
| Efecto | Inmunidad temporal | Inmunidad temporal. **Exposicion acumulativa irreversible** |
| Advertencia | Ninguna | **Exposicion: [X]/100. Dependencia >30. Degradacion >70** |

Por primera vez aparece el contador de `krk_exposure` por operador.

### Fase 4: "No puedo dejar de usarlo" (Acto III-IV)

El jugador sabe la verdad pero el dilema empeora:
- Operadores dependientes sufren abstinencia sin dosis
- La permadeath sigue siendo real
- El Krokonil sigue siendo el unico seguro contra ella

---

## Mecanica del Krokonil — Efecto Completo

### Durante la microdosis activa (4-5 turnos)

| Parametro | Efecto |
|---|---|
| HP | **Congelado** — no puede bajar. Impactos no causan daño |
| Presion | **Congelada** — no puede bajar. Hemorragia no drena presion |
| Hemorragia | Sigue activa internamente pero no drena nada |
| Color ECG | Verde forzado |
| Presion mostrada | 118/76 forzado |
| Penalizaciones QTE | Eliminadas (sin vibracion, sin distracciones, dispersion como 100% HP) |
| Muerte | **Imposible** mientras el efecto esta activo |
| `krk_exposure` | +15 permanente (irreversible) |

### Cuando el efecto expira

| Parametro | Que pasa |
|---|---|
| HP | Se revela el valor real (el que tenia al activar). Vuelve a recibir daño |
| Presion | Se revela el valor real. Hemorragia vuelve a drenar si no se trato |
| Penalizaciones QTE | Vuelven todas de golpe |
| Muerte | **No puede morir al expirar.** Los valores se revelan pero no bajan del valor congelado |

### La ventana de oportunidad

El Krokonil compra 4-5 turnos de invulnerabilidad. Esa ventana es para:
- Terminar el combate sin riesgo de perder al personaje
- Aplicar torniquete/IFAK al operador protegido
- Tratar a otros operadores mientras este esta seguro

Si el jugador usa la ventana para curarse → sale vivo. Si solo dispara sin curarse → al expirar tiene el mismo HP, la misma hemorragia, y la proxima bala puede matarlo.

---

## Exposicion y Consecuencias a Largo Plazo

`krk_exposure` sube +15 por cada microdosis. Nunca baja.

### Umbrales de exposicion

| Umbral | Nombre | Sin dosis activa | Con dosis activa |
|---|---|---|---|
| 0-30 | **Limpio** | Sin efectos secundarios | Funciona normal |
| 31-50 | **Dependencia leve** | Temblor sutil (+2px vibracion base permanente) | Funciona normal |
| 51-70 | **Dependencia severa** | Abstinencia: BPM erratico (±30), presion oscila (±15), dispersion L1 x1.5, ECG con extrasistoles | Funciona pero duracion reducida (3 turnos) |
| 71-100 | **Degradacion permanente** | BPM base +15 permanente, dispersion L1 x1.2 permanente, ruido basal en ECG | El Krokonil ya no enmascara completamente — artefactos visibles |

### Progresion tipica

| Dosis usadas | Exposure | Estado |
|---|---|---|
| 1 | 15 | Limpio |
| 2 | 30 | Al borde de dependencia |
| 3 | 45 | Dependiente leve — temblor sutil permanente |
| 4 | 60 | Dependiente severo — abstinencia real |
| 5 | 75 | Degradado permanentemente — daño irreversible |

---

## Escalada de Tentacion por Acto

| Acto | Situacion | Tentacion |
|---|---|---|
| I | Los 2 MSRT mueren por desgaste | Sin Krokonil. Las muertes establecen la regla de permadeath |
| II | Descubren "Regulador KRK-NL" | Primera tentacion: "si hubiera tenido esto, los MSRT habrian sobrevivido" |
| III | Revelacion: es Krokonil | El jugador ve el daño que ya causo. Pero los combates son mas duros |
| IV | Sin CIA, party debilitado | Tentacion maxima. Cada combate puede costar un SEAL |
| V | Carrera contra el reloj, misil en camino | La exposicion a largo plazo es irrelevante — todos van a morir. Uso libre? |

### Narrativa ludonarrativa

El jugador hace exactamente lo que hicieron los militares infectados del barco: empieza con una microdosis para aguantar, luego otra, luego necesita mas para funcionar, luego ya no puede parar. El sistema que destruyo a los enemigos es el mismo que el jugador usa para sobrevivir.

---

## Ejemplo Completo de Combate

```
Estado inicial: HP 100, Presion 120/80, Hemorragia 0, Exposure 15
                ECG: verde, limpio, 72 BPM

Turno 1 — Disparo al torso (25 daño, causa hemorragia):
  HP: 100 → 75
  Hemorragia: 0 → 2 (moderada)
  Presion: 120/80
  ECG: amarillo-verde, 90 BPM

Turno 2 — Sin tratamiento:
  HP: 75 → 72 (hemorragia drena -3)
  Presion: 120 → 115/77 (hemorragia drena -5)
  ECG: amarillo, 95 BPM

Turno 3 — Otro disparo en pierna (15 daño, hemorragia sube):
  HP: 72 → 54 (daño -15, hemorragia -3)
  Hemorragia: 2 → 3 (severa)
  Presion: 115 → 105/70 (hemorragia drena -10 ahora)
  ECG: naranja, 115 BPM

Turno 4 — Usa IFAK (cura +20 HP progresivo):
  HP: 54 → 68 (IFAK +20, hemorragia -6)
  Presion: 105 → 95/65 (sigue cayendo!)
  ECG: amarillo-verde (HP subio)... pero presion bajando
  El jugador SE SIENTE a salvo

Turno 5 — Sin torniquete:
  HP: 68 → 62 (hemorragia -6)
  Presion: 95 → 85/58
  ECG: amarillo... "85/58" visible

Turno 6 — Aplica TORNIQUETE:
  Hemorragia: 3 → 0
  HP: 62 (deja de drenar)
  Presion: 85/58 → deja de caer, recupera lento → 88/60...

Turno 7+:
  Presion se recupera lentamente: 88 → 91 → 94...
  HP estable en 62 (necesita otro IFAK para subir)
```

---

## Interaccion con Sistemas Existentes

### QTE y dispersion

El `hp` sigue alimentando las mecanicas de dispersion (L1) y distracciones visuales exactamente como estan definidas en [[Diseño de Combate y Armas]]:

- `damage_ratio = 1.0 - (hp / hp_max)` → controla vibracion, BPM, distracciones
- El Krokonil activo fuerza `damage_ratio = 0` (como si estuviera a 100% HP)

### Heartbeat

El sistema de heartbeat existente (lub-dub, SFX, vibracion en barras) se mantiene. El BPM se calcula igual pero ahora tambien responde a la presion arterial y la hemorragia.

### Muerte permanente

Compatible con el sistema de muerte permanente de [[Diseño de Combate y Armas]] — el operador que muere pierde su arma pesada y habilidades. El Krokonil es el unico item que puede prevenir la permadeath temporalmente.

### Inventario

Los items medicos (torniquete 1x1, IFAK 1x2, Krokonil 1x1) compiten por espacio en el inventario 4x4 de [[Sistema de Inventario]] junto con municion.

---

Volver a [[Crimson Draft]]
