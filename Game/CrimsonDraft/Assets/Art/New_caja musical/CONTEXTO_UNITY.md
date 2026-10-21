# Contexto para implementar: puzzle de la caja musical

## Que es
Puzzle de un survival horror estilo Resident Evil clasico (RE1-3). Ocurre en un barco ruso. Hay una caja musical
con una bailarina que toca una cancion de cuna rusa ("Bayu-bayushki-bayu") pero esta desarmada: la melodia
esta dividida en 4 segmentos (slots) y cada slot tiene 3 opciones posibles, de las cuales solo una es correcta.
El jugador escucha la melodia correcta en otro lugar del barco (pista) y tiene que reconstruirla de oido.
Inspiracion directa: la caja musical de la Clock Tower de Resident Evil 3: Nemesis (1999).

## Archivos de audio (carpeta audio/)
- `slot1_opcion1.wav` ... `slot4_opcion3.wav`: 12 clips. Cada uno dura 5.83 s:
  3.333 s de musica (1 compas a 72 BPM) + 2.5 s de cola de resonancia.
- `pista_completa.wav` (15.8 s): la melodia correcta entera. Es la pista que se escucha en otra parte del barco
  (gramofono, otra caja, etc).
- `pista_sin_cuerda.wav` (19.4 s): la misma melodia frenandose y desafinando. Opcional, para ambientacion
  o para cuando la caja se queda sin cuerda.
- `mecanismo_loop.wav` (5.5 s): ruido del mecanismo, OPCIONAL. Si se usa, en loop y a volumen muy bajo.
  Ningun otro audio lo incluye.

## Solucion
slot1 = opcion 1, slot2 = opcion 2, slot3 = opcion 3, slot4 = opcion 1.
Opcion 1 = perno cerca de la bailarina, 2 = medio, 3 = lejos. Cada fila tiene un icono que remite a un cuadro de la sala
(I cuna, II vela, III lobo, IV dientes); la distancia del perno es la distancia entre la figura del cuadro y el nino.
Tambien esta en `solucion.json` (campo "solucion"). Conviene leer la solucion de ahi o de un ScriptableObject,
no hardcodearla, porque los audios se pueden regenerar con otro orden.

## Reproduccion encadenada (importante)
Los segmentos NO se reproducen uno despues de que termina el anterior. El segmento N arranca exactamente
(N-1) x 3.3333 s despues del inicio, y su cola se superpone con el siguiente. Si se espera a que termine
cada clip, la melodia queda cortada con silencios.
Usar `AudioSource.PlayScheduled(AudioSettings.dspTime + offset)` con un AudioSource por slot (4 en total),
para que la sincronizacion sea exacta. No usar Invoke ni corrutinas con WaitForSeconds para el timing.
Constante: duracion de compas = 3.3333 s (60 / 72 * 4).

## Configuracion de import recomendada
Para los 12 clips de slot: Load Type = Decompress On Load, Preload Audio Data = activado
(son cortos y necesitan arrancar sin demora). Las pistas largas pueden ir en Compressed In Memory.
`mecanismo_loop.wav`: activar Loop en el AudioSource.

## Comportamiento esperado
- El jugador interactua con la caja y entra a una vista de cerca (como el panel de RE3).
- Por cada slot puede ciclar entre las 3 opciones; al cambiar, se reproduce solo ese segmento para previsualizar.
- Un boton/palanca "dar cuerda" reproduce los 4 slots seleccionados encadenados.
- Si los 4 son correctos: la melodia suena completa, la bailarina gira y se abre un compartimento con la recompensa.
- Si no: la melodia suena con los errores y no pasa nada (o la bailarina se traba). Sin penalizacion.
- Las 81 combinaciones son posibles; no hay limite de intentos.

## Estructura sugerida en el proyecto
Assets/Audio/CajaMusical/  -> los .wav y solucion.json
El resto del paquete (generar.py, melodia.json, midi/) son herramientas para regenerar el audio y no van dentro
de Assets.
