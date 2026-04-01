"""
export_sfx.py — Modern SFX export: layered synthesis + filtering + FM + soft clipping.
Salida: Prototype/sfx/<nombre>.wav  (44100 Hz, stereo, 16-bit)
Dependencias: numpy, scipy
"""

import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SR = 44100
OUT_DIR = os.path.join(os.path.dirname(__file__), "sfx")


# ── DSP helpers ───────────────────────────────────────────────────────────────

def _ms(milliseconds):
    return int(SR * milliseconds / 1000)


def _lpf(x, cutoff, order=4):
    cutoff = min(cutoff, SR / 2 - 50)
    sos = butter(order, cutoff / (SR / 2), btype="low", output="sos")
    return sosfilt(sos, x)


def _hpf(x, cutoff, order=4):
    cutoff = max(cutoff, 10)
    sos = butter(order, cutoff / (SR / 2), btype="high", output="sos")
    return sosfilt(sos, x)


def _bpf(x, lo, hi, order=4):
    lo = max(lo, 20)
    hi = min(hi, SR / 2 - 50)
    sos = butter(order, [lo / (SR / 2), hi / (SR / 2)], btype="band", output="sos")
    return sosfilt(sos, x)


def _exp_decay(n, rate):
    """Exponential decay envelope (higher rate = faster decay)."""
    return np.exp(-rate * np.arange(n) / SR)


def _adsr(n, atk_ms, dec_ms, sus_level, rel_ms):
    a = _ms(atk_ms); d = _ms(dec_ms); r = _ms(rel_ms)
    s_len = max(0, n - a - d - r)
    env = np.concatenate([
        np.linspace(0, 1,         max(1, a)),
        np.linspace(1, sus_level, max(1, d)),
        np.full(s_len, sus_level),
        np.linspace(sus_level, 0, max(1, r)),
    ])
    if len(env) < n:
        env = np.pad(env, (0, n - len(env)))
    return env[:n]


def _soft_clip(x, drive=1.5):
    denom = np.tanh(drive)
    return np.tanh(x * drive) / (denom if denom > 1e-9 else 1.0)


def _norm(x, peak=0.88):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 1e-9 else x


def _mix(n, *layers):
    """Mezcla pares (signal_array, gain) en un buffer de n samples."""
    out = np.zeros(n)
    for sig, gain in layers:
        k = min(len(sig), n)
        out[:k] += sig[:k] * gain
    return out


def _fade_in(x, dur_ms=2):
    n = min(_ms(dur_ms), len(x))
    x[:n] *= np.linspace(0, 1, n)
    return x


def _stereo(mono):
    return np.column_stack((mono, mono))


# ── FM synthesis ──────────────────────────────────────────────────────────────

def _fm(n, carrier, mod_ratio, mod_depth, envelope=None):
    """FM synthesis: sig = sin(2π·fc·t + depth·sin(2π·fm·t))"""
    t = np.arange(n) / SR
    fm_freq = carrier * mod_ratio
    sig = np.sin(2 * np.pi * carrier * t + mod_depth * np.sin(2 * np.pi * fm_freq * t))
    if envelope is not None:
        sig *= envelope
    return sig


def _fm_ping(carrier, mod_ratio, mod_depth, dur_ms, atk_ms=2, rel_ms=None, vol=0.65):
    n = _ms(dur_ms)
    rel = rel_ms if rel_ms is not None else dur_ms * 0.6
    env = _adsr(n, atk_ms, dur_ms * 0.15, 0.3, rel)
    sig = _fm(n, carrier, mod_ratio, mod_depth, env)
    return _norm(sig) * vol


# ── Gunshots ──────────────────────────────────────────────────────────────────

def _gunshot(sub_hz, sub_rate, crack_rate, body_hz_cut, total_ms,
             sub_w=0.65, crack_w=0.55, body_w=0.45, drive=2.2):
    """3 capas: sub boom + crack filtrado + body noise."""
    n = _ms(total_ms)
    t = np.arange(n) / SR

    # Sub boom: seno grave con decay exponencial + lowpass
    sub = np.sin(2 * np.pi * sub_hz * t) * _exp_decay(n, sub_rate)
    sub = _lpf(sub, min(sub_hz * 2.5, 350))

    # Crack: ruido blanco filtrado bandpass, decay rápido
    crack = np.random.normal(0, 1, n)
    crack = _bpf(crack, 800, 7000)
    crack *= _exp_decay(n, crack_rate)

    # Body: ruido mid con lowpass
    body = np.random.normal(0, 1, n)
    body = _bpf(body, 80, body_hz_cut)
    body *= _exp_decay(n, 10)

    mix = _mix(n, (sub, sub_w), (crack, crack_w), (body, body_w))
    mix = _fade_in(mix, dur_ms=1.5)
    return _norm(_soft_clip(mix, drive))


# ── Impactos ──────────────────────────────────────────────────────────────────

def _impact(thump_hz, thump_rate, crack_lo, crack_hi, crack_rate,
            total_ms, thump_w=0.7, crack_w=0.5):
    n = _ms(total_ms)
    t = np.arange(n) / SR

    thump = np.sin(2 * np.pi * thump_hz * t) * _exp_decay(n, thump_rate)
    thump = _lpf(thump, min(thump_hz * 3, 2000))

    crack = np.random.normal(0, 1, n)
    crack = _bpf(crack, crack_lo, crack_hi)
    crack *= _exp_decay(n, crack_rate)

    mix = _mix(n, (thump, thump_w), (crack, crack_w))
    mix = _fade_in(mix, 1.0)
    return _norm(mix) * 0.85


# ── Heartbeat ─────────────────────────────────────────────────────────────────

def _heartbeat(freq, dur_ms, decay_rate):
    n = _ms(dur_ms)
    t = np.arange(n) / SR
    sig = np.sin(2 * np.pi * freq * t) * _exp_decay(n, decay_rate)
    sig = _lpf(sig, freq * 4)
    sig = _fade_in(sig, 4)
    return _norm(sig) * 0.80


# ── Click mecánico ────────────────────────────────────────────────────────────

def _click(center_hz, bw_hz, dur_ms, decay_rate=80):
    n = _ms(dur_ms)
    lo = max(20, center_hz - bw_hz // 2)
    hi = min(SR // 2 - 50, center_hz + bw_hz // 2)
    sig = np.random.normal(0, 1, n)
    sig = _bpf(sig, lo, hi)
    sig *= _exp_decay(n, decay_rate)
    sig = _fade_in(sig, 0.5)
    return _norm(sig) * 0.75


# ── Chirp (miss) ──────────────────────────────────────────────────────────────

def _chirp(f0, f1, dur_ms, vol=0.50):
    """Barrido de frecuencia descendente."""
    n = _ms(dur_ms)
    t = np.arange(n) / SR
    dur_s = dur_ms / 1000
    phase = 2 * np.pi * (f0 * t + (f1 - f0) / (2 * dur_s) * t ** 2)
    sig = np.sin(phase) * _adsr(n, 5, dur_ms * 0.15, 0.4, dur_ms * 0.55)
    return _norm(sig) * vol


# ── Sonidos compuestos ────────────────────────────────────────────────────────

def _weapon_switch():
    a = _fm_ping(620,  2.0, 2.0, dur_ms=50, atk_ms=2, rel_ms=32, vol=0.60)
    b = _fm_ping(980,  2.5, 1.5, dur_ms=50, atk_ms=2, rel_ms=32, vol=0.60)
    return np.concatenate([a, np.zeros(_ms(14)), b])


def _reload():
    c1 = _click(center_hz=2800, bw_hz=3000, dur_ms=28, decay_rate=90)
    c2 = _click(center_hz=1700, bw_hz=2000, dur_ms=34, decay_rate=70)
    return np.concatenate([c1, np.zeros(_ms(48)), c2])


def _timeout():
    a = _fm_ping(680, 1.5, 3.0, dur_ms=110, atk_ms=5, rel_ms=75, vol=0.70)
    b = _fm_ping(430, 1.5, 3.0, dur_ms=150, atk_ms=5, rel_ms=110, vol=0.70)
    return np.concatenate([a, np.zeros(_ms(20)), b])


# ── Build all sounds ──────────────────────────────────────────────────────────

def build_sounds():
    return {
        # Disparos
        "fire_pistol":   _gunshot(sub_hz=100, sub_rate=18, crack_rate=45, body_hz_cut=1800, total_ms=380, sub_w=0.60, crack_w=0.60, body_w=0.40, drive=2.0),
        "fire_smg":      _gunshot(sub_hz=130, sub_rate=25, crack_rate=55, body_hz_cut=2200, total_ms=280, sub_w=0.50, crack_w=0.70, body_w=0.40, drive=1.8),
        "fire_rifle":    _gunshot(sub_hz=80,  sub_rate=14, crack_rate=35, body_hz_cut=1500, total_ms=480, sub_w=0.75, crack_w=0.55, body_w=0.50, drive=2.4),
        "fire_shotgun":  _gunshot(sub_hz=60,  sub_rate=10, crack_rate=22, body_hz_cut=1200, total_ms=600, sub_w=0.85, crack_w=0.50, body_w=0.65, drive=2.8),

        # UI / Feedback
        "lock_y":        _fm_ping(carrier=820,  mod_ratio=2.0, mod_depth=2.5, dur_ms=90,  atk_ms=2, rel_ms=60,  vol=0.65),
        "lock_x":        _fm_ping(carrier=1050, mod_ratio=2.5, mod_depth=2.0, dur_ms=80,  atk_ms=2, rel_ms=55,  vol=0.65),
        "select_shot":   _fm_ping(carrier=1400, mod_ratio=3.0, mod_depth=1.5, dur_ms=50,  atk_ms=1, rel_ms=35,  vol=0.55),
        "weapon_switch": _weapon_switch(),
        "empty":         _click(center_hz=3500, bw_hz=4000, dur_ms=18, decay_rate=120),

        # Impactos
        "hit_critical":  _impact(thump_hz=180, thump_rate=20, crack_lo=1500, crack_hi=9000, crack_rate=35, total_ms=200, thump_w=0.60, crack_w=0.70),
        "hit_solid":     _impact(thump_hz=120, thump_rate=15, crack_lo=400,  crack_hi=4000, crack_rate=25, total_ms=180, thump_w=0.75, crack_w=0.50),
        "hit_graze":     _impact(thump_hz=200, thump_rate=30, crack_lo=800,  crack_hi=6000, crack_rate=50, total_ms=100, thump_w=0.40, crack_w=0.55),
        "miss":          _chirp(f0=600, f1=180, dur_ms=220, vol=0.50),
        "armor_absorb":  _impact(thump_hz=90,  thump_rate=10, crack_lo=150,  crack_hi=800,  crack_rate=12, total_ms=260, thump_w=0.85, crack_w=0.40),

        # Heartbeat
        "heartbeat_lub": _heartbeat(freq=48, dur_ms=160, decay_rate=18),
        "heartbeat_dub": _heartbeat(freq=38, dur_ms=130, decay_rate=22),

        # Mecánicos
        "reload":        _reload(),
        "timeout":       _timeout(),
    }


# ── Export ────────────────────────────────────────────────────────────────────

def export():
    os.makedirs(OUT_DIR, exist_ok=True)
    sounds = build_sounds()
    for name, mono in sounds.items():
        path = os.path.join(OUT_DIR, f"{name}.wav")
        wavfile.write(path, SR, _stereo((mono * 32767).astype(np.int16)))
        print(f"  {name}.wav  ({len(mono)} samples, {len(mono)/SR*1000:.0f} ms)")
    print(f"\n{len(sounds)} archivos exportados en: {OUT_DIR}")


if __name__ == "__main__":
    export()
