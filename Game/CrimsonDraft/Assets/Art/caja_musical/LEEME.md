# Caja musical - Bayu-bayushki-bayu

## Contenido
- `melodia.json`: la melodia editable. Cada linea de la cancion es un segmento de 1 compas (4/4, 72 BPM, 3.33 s).
- `generar.py`: regenera todo a partir de `melodia.json` (`python3 generar.py`, requiere numpy, scipy y mido).
- `audio/slotN_opcionK.wav`: las 12 piezas del puzzle (4 slots x 3 opciones).
- `audio/pista_completa.wav`: la melodia correcta entera, para la pista que el jugador escucha en el barco.
- `audio/pista_sin_cuerda.wav`: la misma pista frenandose y desafinando, como una caja que se queda sin cuerda.
- `audio/mecanismo_loop.wav`: zumbido y roce del mecanismo, opcional, en loop y a bajo volumen. Ningun otro audio lo incluye.
- `midi/`: todo en MIDI (programa General MIDI 10, Music Box) para reemplazar el sonido en un DAW.
- `solucion.json`: opcion correcta por slot y que tipo de error tiene cada variante.

## Corregir la melodia
Las notas son una transcripcion aproximada del estilo de la cancion. Compararlas con la referencia y editar
`notas` en `melodia.json`. Reglas: 7 notas por segmento, misma primera y ultima nota en todas las opciones
(las variantes se calculan solas). Escala de La menor armonica. Despues correr `python3 generar.py`.

## Reproduccion en el motor
Cada WAV dura 1 compas + 2.5 s de resonancia. Para que suene continuo, no esperar a que termine el archivo:
arrancar el segmento N en el instante (N-1) x 3.3333 s, dejando que las colas se superpongan.
En Unity: `AudioSource.PlayScheduled(AudioSettings.dspTime + offset)`, un AudioSource por slot.

## Tipos de error
- inversion: el dibujo de la frase espejado (facil de notar)
- desplazada: la misma forma una tercera arriba (media)
- una_nota: solo cambia la 4ta nota (dificil, solo en el slot 3)

## Timbre
Al principio de la seccion de sintesis en `generar.py` hay cuatro perillas: `BRILLO` (cuanto metal),
`DESAFINE_CENTS` (afinacion vieja), `BATIDO_CENTS` (trino entre lenguetas gemelas) y `RUIDO_MECANISMO`.
