# Crimson Dialogues — contexto de la historia

Survival horror. Los diálogos son los de radio (códec) y, más adelante, escenas cara a cara entre
los cuatro.

## La premisa

**El Mariner**, un petrolero, es detectado navegando en aguas estadounidenses con un calado
incorrecto: lleva más peso del que debería. A primera vista parece cargado de crudo.

En realidad es un **laboratorio flotante**. Adentro se desarrolla una droga que convierte a las
personas en pseudo-zombies. El barco operaba bajo **control ruso**, usando una droga de origen
**estadounidense**, con la CIA en el medio manteniendo todo en secreto. Una falla a bordo hizo que
la droga se esparciera por el buque.

La sospecha oficial, y lo único que le dicen al equipo, es **contrabando de crudo**. Un equipo de
SEALs es enviado junto a la Guardia Costera a decomisar el barco. Entran de noche.

## Lo que ya pasó

- El equipo de la Guardia Costera —**cinco hombres**— sube por estribor y es **aniquilado dentro
  del barco**. Los ataca la tripulación infectada: **desarmados**, sin cubrirse y sin gritar, así
  que no los leen como amenaza hasta que ya los tienen encima. Les disparan y **no caen**. Dura
  menos de dos minutos.
- **Deer (Ethan) es el único sobreviviente**, y **el único que lo vio**. Los SEALs entraron por
  otro lado y no presenciaron nada: solo lo encuentran a él. Todo lo que el equipo (y el jugador)
  sabe de los infectados viene de su relato.
- La cubierta exterior está **vacía**: ni un cuerpo. Lo que pasó, pasó adentro.
- **Vulture (Darius) se separa** y va al puente para evaluar la situación.
- **Deer, Sterna y Bear** tienen la misión de entrar al deck y ver la situación.

## La misión real (lo que no se dice)

Vulture no está ahí para decomisar nada. Está ahí para **recuperar la información que tenía un
agente infiltrado a bordo, llamado Adrian**. Entraba, lo recuperaba, se retiraba.

**Adrián está vivo, muy mal herido.** Y Vulture **necesita que muera** para llevarse la
información: no le sirve vivo. No tiene que matarlo necesariamente — basta con no ayudarlo, con
llegar tarde, con elegir otra ruta. Eso es peor, y es mucho más fácil de disimular.

**El problema es Deer.** Vulture sabe que su propio equipo no va a revelar nada. Pero Deer es un
testigo que no controla, y por lo que ve de él, **no lo ve guardando secretos**. Un operador que
pregunta `¿quién autoriza?` por una orden menor es exactamente el tipo de persona que va a
declarar todo después.

Deer pasó de ser un sobreviviente al que hay que proteger a ser **una variable sin resolver**.

## Quién sabe qué

| | Vulture | Deer | Sterna | Bear |
|---|---|---|---|---|
| El briefing (crudo ilegal) | sabe que es falso | lo cree | lo cree | lo cree |
| Que hay un laboratorio | sí | no | no | no |
| CIA / Rusia detrás | sí | no | no | no |
| Que existe Adrián | **solo él** | no | no | no |
| El objetivo real | sí | no | no | no |

**Sterna y Bear no saben nada.** Son leales, no cómplices. No revelarían nada porque no hay nada
que revelar — y Vulture les miente a ellos también. Esto es más potente que hacerlos cómplices:
cuando Vulture maniobre, **los cuatro van a estar a oscuras y solo el jugador va a sospechar**.

Consecuencia práctica: **nadie puede decir el nombre "Adrián" en voz alta** salvo Vulture, y solo
cuando está solo o hablando hacia arriba. Si alguno de los otros tres lo encuentra herido, lo
encuentra como "un hombre vivo en el laboratorio" — y ahí se arma el peor problema posible para
Vulture, porque Bear no va a dejar morir a nadie y Deer va a querer evacuarlo.

## El motor dramático

El survival horror es la presión; el conflicto real es de información.

1. **Vulture tiene que operar su misión secreta a la vista de un testigo.** Cada orden suya ahora
   necesita una justificación táctica que la cubra. Va a mandar al grupo lejos del puente, va a
   explicar más de lo que acostumbra, y **explicarse es lo único que Vulture nunca hace** — ahí es
   donde se lo nota.
2. **Deer ya lo está midiendo sin saber qué está midiendo.** Acaba de ver morir a toda su unidad en
   una operación cuya información era incorrecta. Su fe en el sistema es su mayor fortaleza y su
   mayor debilidad: va a buscar que el sistema le explique qué pasó, no va a sospechar una
   conspiración. Todavía.
3. **Bear es el riesgo que Vulture no puede medir.** Deer pide autorización y obedece; Bear
   directamente no ejecuta lo que le parece mal, y su "llamada" no se negocia. Si en algún momento
   hay que elegir entre la información y una persona, Bear es el que se planta.
4. **Sterna es el instrumento preferido y el único que lo va a notar antes.** Está afuera y arriba:
   es la que puede ver a Vulture moverse por donde dijo que no iba a ir.

## Idioma

**Los diálogos (`text` en los JSON) se escriben en inglés**, para que pasen directo al juego. Los
títulos de escena también. **Toda la documentación —este archivo, las fichas de personaje, el
README— sigue en castellano**, porque es material de trabajo, no contenido del juego.

- **Sterna sigue mezclando idiomas.** Con el inglés de base, su francés e italiano resaltan todavía
  más, y **el español se suma a sus lenguas de mezcla** (vivió en España). Detalle en su ficha.
- **Tratamientos que distinguen a cada uno, y no se intercambian nunca:**
  Deer dice `sir`. Bear dice `boss`. Sterna no usa ninguno — lo llama `Vulture` a secas.
- **Vocabulario naval real:** manifold, aft ladder, starboard, port side, hull, bridge,
  accommodation block, berths, hatches *dogged shut*, draft, B Deck.

## Reglas de escritura que salen del contexto

- **La ignorancia es asimétrica — nadie sabe lo mismo.** Antes de escribir cada conversación,
  fijar quién sabe qué en ese momento. Es la fuente principal de tensión.
- **Nadie dice "zombie".** No saben qué es. Hablan de lo que ven: `no caen`, `siguen viniendo`,
  `no reacciona al dolor`, `le di dos veces`, `los ojos`. Deer lo diría en términos de síntoma;
  Bear en términos de resistencia estructural; Sterna describiría cómo se mueven. Vulture lo
  llamaría `tripulación hostil` y seguiría — nombrar el horror sería perder el control.
- **El briefing que recibieron era de contrabando de crudo**, no de drogas. Los mandaron por un
  calado que no cerraba: el barco pesa más de lo que declara. La primera mitad de los diálogos está
  envenenada por esa información falsa, y los personajes la citan de buena fe. Ojo con el
  vocabulario: hablan de **carga, barriles, calado, manifiesto, decomiso** — nunca de narcóticos.
- **Es de noche, en un petrolero, con la Guardia Costera muerta en la cubierta.** La radio es lo
  único que los conecta. La estática, los cortes y los silencios son parte del diálogo: un
  personaje que no contesta es un evento.
- **Las escenas cara a cara se escriben distinto:** sin protocolo, sin confirmaciones, con
  interrupciones. Por radio nadie se interrumpe; en persona sí.

## Punto de vista

**El jugador controla al grupo de tres: Deer, Sterna y Bear.** Vulture llama desde afuera.

En los JSON eso es `"rightIds": ["deer", "sterna", "bear"]` y `"leftId": "vulture"`: los tres
comparten el retrato de la derecha y se alternan según quién habla, con el nombre cambiando arriba.
Vulture queda siempre del otro lado de la radio — **es el de afuera, visualmente, en cada
conversación.** Eso trabaja a favor de la historia sin que nadie lo diga.

## Muertes y variantes

**Deer, Sterna y Bear pueden morir.** Cada conversación tiene que funcionar con cualquier
combinación de sobrevivientes (7 posibles). El formato está en el README; acá va lo narrativo:

- **Vulture no muere nunca.** Es el único que está garantizado en todas las variantes, y por eso es
  el que sostiene la escena cuando el grupo se reduce. A medida que caen, sus conversaciones pasan
  de dar órdenes a tres a hablarle a uno — y eso lo vuelve más íntimo y más amenazante sin cambiar
  una palabra de su carácter.
- **Hablarle a un muerto.** El recurso que mejor funciona: alguien llama por costumbre, nadie
  contesta, y la línea siguiente recoge el silencio. Vulture lo haría con un `...Copiado.` seco.
  Bear es el que peor lo llevaría — él lleva la cuenta de la gente, así que para él olvidarse de
  que alguien murió es una falla personal.
- **Qué se pierde al morir cada uno:**
  - **Sin Sterna:** nadie tiene ojos. Se pierde la información de lo que viene, el conteo en
    francés y el único humor de la radio. Las escenas se vuelven más ciegas y más secas.
  - **Sin Bear:** nadie abre puertas y nadie pregunta dónde está cada uno. Se pierde la calma. Y
    Vulture pierde la única variable que no podía controlar — **le conviene.**
  - **Sin Deer:** Vulture **deja de tener un problema.** Si Deer cae, la misión secreta se le
    destraba sola. Eso convierte cada muerte de Deer en una variante donde Vulture está
    notablemente menos tenso, y el jugador debería poder percibirlo.
- **Las acotaciones que siempre están, cuando faltan, se notan.** Si Sterna picaba a Bear en todas
  las conversaciones y de golpe nadie lo hace, el silencio hace el trabajo. Conviene darle a cada
  uno un par de intervenciones recurrentes justamente para que su ausencia pese.

## Convención en los JSON

- **Conversaciones por radio:** `freq` lleva la frecuencia (`38.75`).
- **Escenas cara a cara:** `freq` lleva la ubicación en mayúsculas (`DECK 02`, `PUENTE`,
  `SALA DE BOMBAS`), que es lo que se muestra en el display de la radio.

## Pendiente de definir

- ¿Por qué Adrián no le sirve vivo? (¿hablaría? ¿la información tiene que ser irrastreable?
  ¿lo conoce personalmente?) — define cuánta culpa carga Vulture.
- ¿Qué es físicamente "la información"? ¿Está en Adrián o él sabe dónde está?
- ¿Cuánto dura la noche? Un reloj (marea, bomba, rescate a las 05:00) le daría presión a cada
  conversación.
