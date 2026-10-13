# Puzzle de la caja musical - Diseno de la sala de arte

## Contexto
Survival horror estilo Resident Evil clasico (RE1-3). Ocurre en un barco ruso, en una sala de arte con cuadros
y esculturas. En la sala hay una caja musical con una bailarina (muneca) en el centro que toca la cancion de cuna
rusa "Bayu-bayushki-bayu". La melodia esta dividida en 4 segmentos. El jugador tiene que reconstruirla usando las
pistas de la sala (cuatro cuadros) y, como respaldo, su oido.

Referencias: caja musical de la Clock Tower de RE3 (1999) y galeria de los cuervos de RE1 (cuadros que cuentan
una secuencia, placa que da el orden, castigo diegetico si se falla).

## La caja
- 4 filas de pernos que salen desde la bailarina hacia afuera, separadas por lineas doradas.
- Cada fila tiene 3 pernos a distinta distancia del centro: cerca, medio y lejos.
- Cada fila tiene un icono grabado que remite a uno de los cuadros de la sala, y un numero romano (I a IV)
  en el borde exterior que indica el orden de la melodia.
- Una manivela al costado para dar cuerda y reproducir la secuencia.

## La regla
La muneca del centro representa al nino de la cancion. Cada fila pregunta:
**a que distancia del nino esta la figura del cuadro que indica su icono.**
El jugador mira el cuadro, ve a que distancia esta la figura, y elige el perno a esa misma distancia
de la bailarina.

La serie cuenta una historia de distancias cruzadas: la madre (la proteccion) se aleja y el lobo (la amenaza)
se acerca.

## Solucion

| Fila | Icono | Cuadro | Figura medida | Distancia | Opcion de audio |
|---|---|---|---|---|---|
| I | Cuna | I | La madre, inclinada sobre la cuna | Cerca | 1 |
| II | Vela | II | La madre, yendose con la vela | Medio | 2 |
| III | Lobo | III | El lobo, en el borde del bosque | Lejos | 3 |
| IV | Dientes | IV | El lobo, mordiendo | Cerca | 1 |

Opcion 1 = cerca, 2 = medio, 3 = lejos. Coincide con `solucion.json` y con los audios `slotN_opcionK.wav`.

## Placa de la entrada de la galeria
**"Kolybelnaya" (Lullaby). Four canvases by Irina Volkova, 1911.**
*"Hung in the order a mother sings them. The last verse is never sung kindly."*

Nota: "Volkova" viene de "volk", lobo en ruso. Cada marco lleva su numero romano (I a IV).

## Los cuadros
Todos muestran la misma habitacion de madera, de noche, para que se lean como una secuencia.

### I. "Bayu-bayushki-bayu" - Icono: cuna - Distancia: cerca
**Arte:** cuarto iluminado por una vela. La madre, con panuelo en la cabeza, inclinada sobre una cuna de madera
con balancines, tan cerca que su cara casi toca al bebe. La cuna en el centro, con la luz mas fuerte.
**Placa:** *I. "Bayu-bayushki-bayu" — Hush, little one, hush.*
**Texto al inspeccionar:** *"A mother bends over the cradle, so close her breath stirs the blanket. Nothing could reach him while she is there."*

### II. "Ne lozhisya na krayu" - Icono: vela - Distancia: medio
**Arte:** la misma habitacion. El nino duerme en el borde de la cama con una manito colgando (detalle del verso,
no es pista). La madre de espaldas, con la vela en la mano, mirando al nino por encima del hombro. Esta justo a
mitad de camino entre la cama y la puerta. Para que se lea claro: tres tablones de piso entre la cama y la puerta,
y ella parada en el del medio. La luz se va con ella; la cama queda medio en sombra.
**Placa:** *II. "Ne lozhisya na krayu" — Do not lie at the edge.*
**Texto al inspeccionar:** *"She takes the candle with her. Halfway to the door she turns back, as if she could still reach him from there. She cannot."*

### III. "Pridyot serenkiy volchok" - Icono: lobo - Distancia: lejos
**Arte:** vista desde adentro hacia la ventana escarchada. Afuera, en el borde del bosque nevado y bien al fondo,
un lobo gris camina hacia la casa. Es chico en el cuadro, pero sus ojos brillan. Debe quedar claro que esta lejos.
**Placa:** *III. "Pridyot serenkiy volchok" — The little grey wolf will come.*
**Texto al inspeccionar:** *"Far beyond the window, where the forest begins, something grey has started walking. It is still a long way off."*

### IV. "I ukhvatit za bochok" - Icono: dientes - Distancia: cerca
**Arte:** el mas perturbador. La vela del cuadro II apagada en el piso. El lobo sobre la cama, con las fauces
cerradas sobre el costado del nino, pegado a el. La madre no aparece.
**Placa:** *IV. "I ukhvatit za bochok" — And bite you on the side.*
**Texto al inspeccionar:** *"The candle is out. It is not far anymore. It is close enough to touch him."*

## Textos de la caja (sugeridos)
- Al inspeccionar la caja: *"A music box. A small dancer stands in the center, surrounded by four rows of pins. Each row bears a carved symbol."*
- Al inspeccionar cada fila: *"Row I. A cradle is carved at its end."* / *"Row II. A candle..."* / *"Row III. A wolf..."* / *"Row IV. Bared teeth..."*
- Al seleccionar un perno: reproducir solo el segmento de esa fila con esa opcion, para que el jugador pueda
  resolver tambien de oido.

## Feedback y castigo
- Correcto: la melodia suena completa, la bailarina gira y se abre el compartimento con la recompensa.
- Incorrecto: la melodia suena con los errores, la bailarina se traba y algo en el barco reacciona (golpe en el
  casco, luz que parpadea, criatura que se acerca). Sin dano automatico. Intentos ilimitados.
- La solucion es fija. Si en algun momento se aleatoriza, los cuadros tienen que cambiar con ella.

## Audio
Los audios estan en `audio/`. Detalles tecnicos (duraciones, encadenado con `PlayScheduled`, configuracion de
import) en `CONTEXTO_UNITY.md`. Para cambiar que perno es el correcto en cada fila: editar `posicion_correcta`
en `melodia.json` y correr `python3 generar.py`.

## Pendiente
- Pintar los 4 cuadros en pixel art.
- Grabar los iconos y numeros romanos en las filas de la caja.
- Definir la recompensa del compartimento.
- Ubicar en el barco la pista auditiva (`pista_completa.wav`), por ejemplo un gramofono.
