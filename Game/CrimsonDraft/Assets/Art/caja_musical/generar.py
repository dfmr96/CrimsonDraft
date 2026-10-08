"""Generador del puzzle de la caja musical.

Lee melodia.json y genera:
  audio/slotN_opcionK.wav   4 slots x 3 opciones (una correcta)
  audio/pista_completa.wav  melodia correcta entera (la pista que el jugador escucha en el barco)
  audio/pista_sin_cuerda.wav la pista ralentizandose y desafinando (caja quedandose sin cuerda)
  midi/...                  lo mismo en MIDI para editar en un DAW
  solucion.json             cual opcion es la correcta en cada slot

Uso: python3 generar.py
Requiere: numpy, scipy, mido
"""
import json, os, random
import numpy as np
from scipy.io import wavfile
from scipy.signal import fftconvolve, butter, lfilter, iirpeak
import mido

SR = 44100
BASE = os.path.dirname(os.path.abspath(__file__))
cfg = json.load(open(os.path.join(BASE, "melodia.json"), encoding="utf-8"))
BPM = cfg["tempo_bpm"]
CORCHEA = 60.0 / BPM / 2
COMPAS = CORCHEA * 8
COLA = 2.5  # segundos de resonancia despues del compas

# ---------- notas ----------
NOMBRES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
ESCALA = ["A", "B", "C", "D", "E", "F", "G#"]  # la menor armonica


def midi_num(n):
    nombre, octava = n[:-1], int(n[-1])
    v = NOMBRES[nombre[0]] + (1 if "#" in nombre else 0)
    return 12 * (octava + 1) + v


def a_grado(n):
    nombre, octava = n[:-1], int(n[-1])
    if nombre == "G":
        nombre = "G#"
    idx = ESCALA.index(nombre)
    oct_rel = octava - (1 if idx >= 2 else 0)  # la escala arranca en A, C cambia de octava
    return oct_rel * 7 + idx


def de_grado(g):
    oct_rel, idx = divmod(g, 7)
    nombre = ESCALA[idx]
    octava = oct_rel + (1 if idx >= 2 else 0)
    return f"{nombre}{octava}"


def variante(notas, tipo):
    g = [a_grado(n) for n in notas]
    primero = g[0]
    nuevo = list(g)
    for i in range(1, len(g) - 1):
        if tipo == "inversion":
            nuevo[i] = 2 * primero - g[i]
        elif tipo == "desplazada":
            nuevo[i] = g[i] + 2
    if tipo == "una_nota":
        nuevo[3] = g[3] + 2 if g[3] + 2 != g[2] else g[3] - 2
    return [de_grado(x) for x in nuevo]


ACORDES = {"Am": ["A2", "E3", "A3"], "E": ["E2", "B2", "G#3"], "C": ["C3", "G3", "C4"],
           "G": ["G2", "D3", "B3"], "Dm": ["D3", "A3", "D4"], "F": ["F2", "C3", "A3"]}


def acompanamiento(acordes):
    """Mano izquierda de caja musical: arpegio lento, 4 notas por medio compas."""
    ev = []
    for mitad, ac in enumerate(acordes):
        t0 = mitad * 4
        b, q, o = ACORDES[ac]
        for k, n in enumerate([b, q, o, q]):
            ev.append((t0 + k, n, 1, 0.33))
    return ev


def eventos_segmento(seg, notas):
    ev, t = [], 0
    for n, d in zip(notas, seg["duraciones"]):
        ev.append((t, n, d, 0.9))
        t += d
    return ev + acompanamiento(seg["acordes"])


# ---------- sintesis de caja musical antigua ----------
# Ajustes del timbre. Subir BRILLO o DESAFINE para un sonido mas viejo y metalico.
BRILLO = 1.0          # volumen de los parciales agudos de la lengueta
DESAFINE_CENTS = 7    # cuanto puede estar desafinada cada lengueta (afinacion vieja)
BATIDO_CENTS = 4      # diferencia entre las dos lenguetas gemelas (efecto "trino")
RUIDO_MECANISMO = 0.012

# Parciales de una barra de acero empotrada en un extremo (viga en voladizo):
# 1 : 6.27 : 17.55 : 34.39. Son los que dan el "tin" de vidrio/metal.
PARCIALES = [  # (ratio, amplitud, decaimiento en s)
    (1.0,   1.00, 2.2),
    (2.0,   0.10, 0.9),   # leve no linealidad del pin
    (6.27,  0.42, 0.55),
    (17.55, 0.22, 0.16),
    (34.39, 0.10, 0.06),
]


def tono(freq, dur, vel):
    n = int(SR * dur)
    t = np.arange(n) / SR
    # cada nota tiene su propia desafinacion fija, como una lengueta vieja
    r = np.random.default_rng(int(freq * 1000))
    freq = freq * 2 ** (r.uniform(-DESAFINE_CENTS, DESAFINE_CENTS) / 1200)
    escala = (440 / freq) ** 0.35  # las agudas se apagan antes
    s = np.zeros(n)
    for k, (ratio, amp, tau) in enumerate(PARCIALES):
        f = freq * ratio
        if f > SR / 2.2:
            continue
        a = amp * (BRILLO if k >= 2 else 1.0)
        env = np.exp(-t / (tau * escala))
        fase = r.uniform(0, 2 * np.pi)
        s += a * env * np.sin(2 * np.pi * f * t + fase)
        if k == 0:  # lengueta gemela levemente desafinada: produce el batido
            f2 = f * 2 ** (BATIDO_CENTS / 1200)
            s += 0.55 * a * env * np.sin(2 * np.pi * f2 * t)
    ataque = np.minimum(1, t / 0.0008)
    # pin del cilindro golpeando la lengueta: chasquido metalico corto
    click = r.standard_normal(n) * np.exp(-t / 0.0025) * 0.18
    click = np.diff(click, prepend=0)  # lo hace mas agudo y seco
    return (s * ataque + click) * vel


def mecanismo(n):
    """Zumbido del regulador de aire y roce del cilindro, siempre igual."""
    r = np.random.default_rng(99)
    t = np.arange(n) / SR
    ruido = r.standard_normal(n)
    b, a = butter(2, [1800 / (SR / 2), 5000 / (SR / 2)], btype="band")
    roce = lfilter(b, a, ruido)
    b, a = butter(2, 140 / (SR / 2), btype="low")
    zumbido = lfilter(b, a, ruido) * 3
    return (roce + zumbido) * RUIDO_MECANISMO


def cuerpo(x):
    """Caja de madera chica: sin graves, resonancias en medios y agudos."""
    b, a = butter(2, 220 / (SR / 2), btype="high")
    x = lfilter(b, a, x)
    out = x.copy()
    for f0, q, g in [(780, 4, 0.5), (2500, 3, 0.35), (4100, 5, 0.2)]:
        b, a = iirpeak(f0 / (SR / 2), q)
        out += g * lfilter(b, a, x)
    return out


def render(eventos, largo, rate=None):
    buf = np.zeros(int(SR * largo))
    for t, n, d, vel in eventos:
        inicio = t * CORCHEA if rate is None else rate(t)
        f = 440 * 2 ** ((midi_num(n) - 69) / 12)
        s = tono(f, 3.0, vel)
        i = int(inicio * SR)
        fin = min(len(buf), i + len(s))
        buf[i:fin] += s[: fin - i]
    return buf


def reverb(x, mezcla=0.2):
    n = int(SR * 1.4)
    t = np.arange(n) / SR
    rng = np.random.default_rng(7)
    ir = rng.standard_normal(n) * np.exp(-t / 0.3)
    ir /= np.sqrt(np.sum(ir ** 2))
    mojado = fftconvolve(x, ir)[: len(x)]
    return (1 - mezcla) * x + mezcla * mojado


def guardar_wav(nombre, x, pico=0.85, con_mecanismo=False):
    x = cuerpo(x / (np.max(np.abs(x)) + 1e-9))
    x = x / (np.max(np.abs(x)) + 1e-9)
    if con_mecanismo:
        x = x + mecanismo(len(x))
    x = reverb(x)
    x = x / (np.max(np.abs(x)) + 1e-9) * pico
    wavfile.write(nombre, SR, (x * 32767).astype(np.int16))


def guardar_midi(nombre, eventos):
    mid = mido.MidiFile(ticks_per_beat=480)
    tr = mido.MidiTrack(); mid.tracks.append(tr)
    tr.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(BPM)))
    tr.append(mido.Message("program_change", program=10))  # General MIDI: Music Box
    msgs = []
    for t, n, d, vel in eventos:
        tick = int(t * 240)
        msgs.append((tick, 1, mido.Message("note_on", note=midi_num(n), velocity=int(vel * 110))))
        msgs.append((tick + int(d * 240), 0, mido.Message("note_off", note=midi_num(n), velocity=0)))
    msgs.sort(key=lambda m: (m[0], m[1]))
    ultimo = 0
    for tick, _, m in msgs:
        m.time = tick - ultimo; ultimo = tick
        tr.append(m)
    mid.save(nombre)


# ---------- armado del puzzle ----------
os.makedirs(os.path.join(BASE, "audio"), exist_ok=True)
os.makedirs(os.path.join(BASE, "midi"), exist_ok=True)
rng = random.Random(cfg["semilla_orden"])
solucion = {}
completa = []

for s, seg in enumerate(cfg["segmentos"], 1):
    opciones = [("correcta", seg["notas"])]
    for tipo in cfg["errores_por_slot"][s - 1]:
        opciones.append((tipo, variante(seg["notas"], tipo)))
    for _, notas in opciones[1:]:
        assert notas != seg["notas"], f"slot {s}: una variante quedo igual a la correcta"
    assert opciones[1][1] != opciones[2][1], f"slot {s}: variantes repetidas"
    rng.shuffle(opciones)
    for k, (tipo, notas) in enumerate(opciones, 1):
        ev = eventos_segmento(seg, notas)
        guardar_wav(os.path.join(BASE, "audio", f"slot{s}_opcion{k}.wav"), render(ev, COMPAS + COLA))
        guardar_midi(os.path.join(BASE, "midi", f"slot{s}_opcion{k}.mid"), ev)
        if tipo == "correcta":
            solucion[f"slot{s}"] = k
        solucion.setdefault("detalle", {})[f"slot{s}_opcion{k}"] = {"tipo": tipo, "notas": notas}
    completa += [(t + (s - 1) * 8, n, d, v) for t, n, d, v in eventos_segmento(seg, seg["notas"])]

largo_total = COMPAS * 4 + COLA
guardar_wav(os.path.join(BASE, "audio", "pista_completa.wav"), render(completa, largo_total))
guardar_midi(os.path.join(BASE, "midi", "pista_completa.mid"), completa)

# version "sin cuerda": el tempo se frena y el tono cae hacia el final
total_c = 32
def tiempo_frenado(t):
    # integra un tempo que cae al 55% del original
    pasos = np.linspace(0, t, 200)
    factor = 1 + 0.8 * (pasos / total_c) ** 2
    return np.trapezoid(factor, pasos) * CORCHEA
buf = np.zeros(int(SR * (tiempo_frenado(total_c) + COLA)))
for t, n, d, v in completa:
    inicio = tiempo_frenado(t)
    desafine = 2 ** (-0.9 * (t / total_c) ** 3 / 12)
    f = 440 * 2 ** ((midi_num(n) - 69) / 12) * desafine
    s = tono(f, 3.0, v * (1 - 0.35 * t / total_c))
    i = int(inicio * SR); fin = min(len(buf), i + len(s))
    buf[i:fin] += s[: fin - i]
guardar_wav(os.path.join(BASE, "audio", "pista_sin_cuerda.wav"), buf)

# ruido del mecanismo aparte, en loop, para poner debajo de los segmentos en el motor
m = mecanismo(SR * 6)
fade = int(SR * 0.5)
m[:fade] = m[:fade] * np.linspace(0, 1, fade) + m[-fade:] * np.linspace(1, 0, fade)
m = m[:-fade]
wavfile.write(os.path.join(BASE, "audio", "mecanismo_loop.wav"), SR, (m / np.max(np.abs(m)) * 0.25 * 32767).astype(np.int16))

json.dump({"tempo_bpm": BPM, "duracion_compas_seg": round(COMPAS, 4),
           "solucion": {k: v for k, v in solucion.items() if k != "detalle"},
           "detalle": solucion["detalle"]},
          open(os.path.join(BASE, "solucion.json"), "w", encoding="utf-8"), indent=2, ensure_ascii=False)
print("Listo. Compas =", round(COMPAS, 3), "s. Solucion:", {k: v for k, v in solucion.items() if k != "detalle"})
