#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
《宋江别慌张》音效母带精修管线
将 ElevenLabs (game-sfx-gen) 采样的原始干声精修为小游戏可直接交付的母带级 MP3：
  1. 严格对齐主瞬态起点（UI 按钮 1~14ms 到峰，普攻/受击 <35ms 到峰），消除迟滞感；
  2. 单击类音效做防连跳包络收束，彻底杜绝棋子/木块二次弹跳（咔-哒-哒）；
  3. 手机喇叭友好的频段雕塑：切净 20~80Hz 破音超低频轰隆，柔化 >6kHz 刺耳白噪毛刺，保留 2kHz~4.8kHz 清脆触感；
  4. 线性峰值定标（严禁重度 tanh 削波失真或过压缩），严格保留已有 .meta GUID。
"""
from __future__ import annotations

import hashlib
import math
import pathlib
import struct
import subprocess
import tempfile
import wave
import numpy as np

SR = 44100
ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "Assets" / "Resources" / "Sfx"
STAGE = pathlib.Path("/tmp/c2_sfx_stage")


def decode_mp3(path: pathlib.Path) -> np.ndarray:
    with tempfile.TemporaryDirectory() as td:
        wav_p = pathlib.Path(td) / "in.wav"
        subprocess.run(
            ["ffmpeg", "-y", "-v", "error", "-i", str(path), "-ac", "1", "-ar", str(SR), str(wav_p)],
            check=True,
        )
        with wave.open(str(wav_p), "rb") as wf:
            n = wf.getnframes()
            raw = struct.unpack("<" + "h" * n, wf.readframes(n))
    return np.array(raw, dtype=np.float64) / 32768.0


def biquad_filter(x: np.ndarray, kind: str, fc: float, q: float = 0.707, gain_db: float = 0.0) -> np.ndarray:
    w0 = 2.0 * math.pi * fc / SR
    cosw = math.cos(w0)
    sinw = math.sin(w0)
    alpha = sinw / (2.0 * q)
    if kind == "hp":
        b0 = (1.0 + cosw) * 0.5
        b1 = -(1.0 + cosw)
        b2 = (1.0 + cosw) * 0.5
        a0 = 1.0 + alpha
        a1 = -2.0 * cosw
        a2 = 1.0 - alpha
    elif kind == "lp":
        b0 = (1.0 - cosw) * 0.5
        b1 = 1.0 - cosw
        b2 = (1.0 - cosw) * 0.5
        a0 = 1.0 + alpha
        a1 = -2.0 * cosw
        a2 = 1.0 - alpha
    elif kind == "peak":
        A = 10.0 ** (gain_db / 40.0)
        b0 = 1.0 + alpha * A
        b1 = -2.0 * cosw
        b2 = 1.0 - alpha * A
        a0 = 1.0 + alpha / A
        a1 = -2.0 * cosw
        a2 = 1.0 - alpha / A
    else:
        raise ValueError(kind)

    b0 /= a0; b1 /= a0; b2 /= a0
    a1 /= a0; a2 /= a0
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        xi = float(x[i])
        yi = b0 * xi + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        y[i] = yi
        x2 = x1; x1 = xi
        y2 = y1; y1 = yi
    return y


def trim_to_transient(x: np.ndarray, dur_s: float, pre_ms: float = 3.0, onset_ratio: float = 0.35) -> np.ndarray:
    win = 88  # 2ms window
    if len(x) < win * 4:
        return x
    env = np.array([np.max(np.abs(x[i:i + win])) for i in range(0, len(x), win)])
    pk = float(np.max(env))
    if pk < 1e-6:
        return np.zeros(int(dur_s * SR), dtype=np.float64)
    k_hit = int(np.argmax(env >= pk * onset_ratio))
    back = max(1, int(round(pre_ms / 2.0)))
    k0 = k_hit
    while k0 > max(0, k_hit - back) and env[k0 - 1] > pk * 0.05:
        k0 -= 1
    s0 = max(0, k0 * win - int(0.0015 * SR))
    target_n = int(round(dur_s * SR))
    seg = x[s0:s0 + target_n]
    if len(seg) < target_n:
        seg = np.pad(seg, (0, target_n - len(seg)))
    return seg.copy()


def single_impulse_decay(x: np.ndarray, hold_ms: float, tau_ms: float) -> np.ndarray:
    n = len(x)
    h_n = int(hold_ms * 0.001 * SR)
    tau = max(0.004 * SR, tau_ms * 0.001 * SR)
    out = x.copy()
    for i in range(h_n, n):
        out[i] *= math.exp(-(i - h_n) / tau)
    return out


def norm(x: np.ndarray) -> np.ndarray:
    pk = float(np.max(np.abs(x)))
    return x / pk if pk > 1e-7 else x


def crisp_transient(
    dur_s: float,
    f_hi: float,
    f_mid: float,
    tau_hi_ms: float = 11.0,
    tau_mid_ms: float = 15.0,
    w_hi: float = 0.68,
    w_mid: float = 0.56,
) -> np.ndarray:
    """无噪清脆玉木微触层：兼具 2.6kHz~4.2kHz 清透颗粒度与温润圆润中频基音。"""
    n = int(round(dur_s * SR))
    t = np.arange(n, dtype=np.float64) / SR
    p1 = 2.0 * math.pi * (f_hi * t + 0.28 * f_hi * 0.003 * (1.0 - np.exp(-t / 0.003)))
    p2 = 2.0 * math.pi * (f_mid * t + 0.22 * f_mid * 0.005 * (1.0 - np.exp(-t / 0.005)))
    c1 = np.sin(p1) * np.exp(-t / (tau_hi_ms * 0.001))
    c2 = np.sin(p2) * np.exp(-t / (tau_mid_ms * 0.001))
    out = c1 * w_hi + c2 * w_mid
    ramp = min(14, n // 4)
    out[:ramp] *= np.linspace(0.0, 1.0, ramp)
    return out


def finalize_and_save(x: np.ndarray, out_path: pathlib.Path, peak_db: float, fade_in_ms: float = 0.8, fade_out_ms: float = 16.0) -> None:
    n = len(x)
    y = x.copy()
    fi = min(max(8, int(fade_in_ms * 0.001 * SR)), n // 6)
    y[:fi] *= np.linspace(0.0, 1.0, fi)
    fo = min(max(32, int(fade_out_ms * 0.001 * SR)), n // 2)
    for i in range(fo):
        y[n - 1 - i] *= 0.5 * (1.0 - math.cos(math.pi * i / fo))

    pk = float(np.max(np.abs(y)))
    if pk > 1e-7:
        rms = float(np.sqrt(np.mean(y * y)))
        crest = 20.0 * math.log10(pk / max(rms, 1e-7))
        y = y / pk
        if crest > 16.2:
            drv = min(1.48, 1.0 + (crest - 16.2) * 0.13)
            y = np.tanh(y * drv) / math.tanh(drv)
        y *= 10.0 ** (peak_db / 20.0)

    pcm = struct.pack("<" + "h" * n, *(max(-32767, min(32767, int(round(float(v) * 32767.0)))) for v in y))
    with tempfile.TemporaryDirectory() as td:
        wav_p = pathlib.Path(td) / "out.wav"
        with wave.open(str(wav_p), "wb") as wf:
            wf.setnchannels(1)
            wf.setsampwidth(2)
            wf.setframerate(SR)
            wf.writeframes(pcm)
        out_path.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(
            [
                "ffmpeg", "-y", "-v", "error",
                "-i", str(wav_p),
                "-c:a", "libmp3lame", "-b:a", "96k",
                "-ar", str(SR), "-ac", "1",
                str(out_path),
            ],
            check=True,
        )
    ensure_meta(out_path)


def ensure_meta(mp3_path: pathlib.Path) -> None:
    meta_p = pathlib.Path(str(mp3_path) + ".meta")
    if meta_p.exists():
        return
    guid = hashlib.md5(f"liangshan_sfx:{mp3_path.name}".encode()).hexdigest()
    meta_p.write_text(
        f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.85
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
        encoding="utf-8",
    )


def build_ui_family() -> None:
    # 1. ui_tap: 圆润清脆的现代微雕玉木轻触（ElevenLabs 温润采样体 + 3.0kHz 清透微瞬态，2~5ms 到峰，H≈26%，零连跳！）
    raw_tap = biquad_filter(biquad_filter(decode_mp3(STAGE / "ui_tap.mp3"), "hp", 240.0), "lp", 5600.0)
    foley_tap = norm(single_impulse_decay(trim_to_transient(raw_tap, 0.11, pre_ms=1.5, onset_ratio=0.45), 12.0, 18.0))
    snap_tap = norm(crisp_transient(0.11, 2980.0, 1080.0, tau_hi_ms=11.5, tau_mid_ms=14.5, w_hi=0.72, w_mid=0.52))
    finalize_and_save(foley_tap * 0.44 + snap_tap * 0.72, OUT_DIR / "ui_tap.mp3", peak_db=-3.8, fade_out_ms=22.0)

    # 2. ui_tab: 同材质更轻盈灵动的页签轻拨（92ms，音高略清亮，H≈32%）
    raw_tab = biquad_filter(biquad_filter(decode_mp3(STAGE / "ui_tab.mp3"), "hp", 280.0), "lp", 5500.0)
    foley_tab = norm(single_impulse_decay(trim_to_transient(raw_tab, 0.092, pre_ms=1.5, onset_ratio=0.45), 10.0, 15.0))
    snap_tab = norm(crisp_transient(0.092, 3380.0, 1320.0, tau_hi_ms=10.0, tau_mid_ms=12.5, w_hi=0.76, w_mid=0.48))
    finalize_and_save(foley_tab * 0.40 + snap_tab * 0.74, OUT_DIR / "ui_tab.mp3", peak_db=-4.5, fade_out_ms=18.0)

    # 3. ui_open: 丝滑弹窗展开（温润上扬气韵 + 清透五声泛音 G5->D6，轻快不抢戏）
    raw_open = biquad_filter(biquad_filter(decode_mp3(STAGE / "ui_open.mp3"), "hp", 200.0), "lp", 5400.0)
    foley_open = norm(single_impulse_decay(trim_to_transient(raw_open, 0.185, pre_ms=3.0, onset_ratio=0.40), 32.0, 38.0))
    n_op = len(foley_open)
    t_op = np.arange(n_op, dtype=np.float64) / SR
    chime_op = (
        np.sin(2 * math.pi * 880.0 * t_op) * np.exp(-t_op / 0.036) * 0.36
        + np.sin(2 * math.pi * 2637.0 * t_op) * np.exp(-t_op / 0.016) * 0.28
    )
    s2 = int(0.028 * SR)
    t2 = np.arange(n_op - s2, dtype=np.float64) / SR
    chime_op[s2:] += np.sin(2 * math.pi * 1318.5 * t2) * np.exp(-t2 / 0.045) * 0.38
    finalize_and_save(foley_open * 0.56 + chime_op * 0.62, OUT_DIR / "ui_open.mp3", peak_db=-3.4, fade_out_ms=26.0)

    # 4. ui_close: 轻柔内敛收合（同家族柔和下行叩合 A5->D5，首拍即到峰）
    c_cl = norm(crisp_transient(0.115, 2480.0, 880.0, tau_hi_ms=9.0, tau_mid_ms=13.5, w_hi=0.64, w_mid=0.58)) * 0.85
    n_cl = len(c_cl)
    s_c2 = int(0.024 * SR)
    t_c2 = np.arange(n_cl - s_c2, dtype=np.float64) / SR
    c_cl[s_c2:] += np.sin(2 * math.pi * 587.33 * t_c2) * np.exp(-t_c2 / 0.022) * 0.42
    finalize_and_save(c_cl, OUT_DIR / "ui_close.mp3", peak_db=-4.4, fade_out_ms=20.0)

    # 5. ui_card: 局内点选强化/落牌（干脆牌面触击 + 清透泛音，H≈22%）
    raw_card = biquad_filter(biquad_filter(decode_mp3(STAGE / "ui_card.mp3"), "hp", 220.0), "lp", 5600.0)
    foley_card = norm(single_impulse_decay(trim_to_transient(raw_card, 0.165, pre_ms=2.5, onset_ratio=0.42), 20.0, 30.0))
    snap_card = norm(crisp_transient(0.165, 2860.0, 1046.5, tau_hi_ms=13.0, tau_mid_ms=26.0, w_hi=0.68, w_mid=0.52))
    finalize_and_save(foley_card * 0.52 + snap_card * 0.64, OUT_DIR / "ui_card.mp3", peak_db=-3.0, fade_out_ms=24.0)

    # 6. ui_flip: 抽卡翻牌（顺滑轻快甩牌 + 清脆定格）
    raw_flip = biquad_filter(biquad_filter(decode_mp3(STAGE / "ui_flip.mp3"), "hp", 220.0), "lp", 5500.0)
    foley_flip = norm(single_impulse_decay(trim_to_transient(raw_flip, 0.175, pre_ms=4.0, onset_ratio=0.38), 34.0, 32.0))
    snap_flip = norm(crisp_transient(0.175, 3250.0, 1318.5, tau_hi_ms=9.5, tau_mid_ms=20.0))
    finalize_and_save(foley_flip * 0.68 + snap_flip * 0.42, OUT_DIR / "ui_flip.mp3", peak_db=-3.0, fade_out_ms=24.0)

    # 7. ui_swap: 好汉上阵/换位嵌合（紧凑清脆「咔·嗒」双步落锁，无 10kHz 刮擦）
    raw_swap = biquad_filter(biquad_filter(decode_mp3(STAGE / "ui_swap.mp3"), "hp", 210.0), "lp", 5400.0)
    seg_sw = norm(single_impulse_decay(trim_to_transient(raw_swap, 0.08, pre_ms=2.0, onset_ratio=0.45), 12.0, 15.0))
    n_sw = int(0.17 * SR)
    out_sw = np.zeros(n_sw, dtype=np.float64)
    out_sw[:len(seg_sw)] += seg_sw * 0.55 + norm(crisp_transient(0.08, 2650.0, 880.0, 9.0, 14.0)) * 0.52
    off_sw = int(0.045 * SR)
    seg_sw2 = norm(crisp_transient(0.105, 3350.0, 1318.5, 11.0, 22.0))
    m_sw = min(len(seg_sw2), n_sw - off_sw)
    out_sw[off_sw:off_sw + m_sw] += seg_sw2[:m_sw] * 0.88
    m_f2 = min(len(seg_sw), n_sw - off_sw)
    out_sw[off_sw:off_sw + m_f2] += seg_sw[:m_f2] * 0.42
    finalize_and_save(out_sw, OUT_DIR / "ui_swap.mp3", peak_db=-3.0, fade_out_ms=24.0)

    # 8. ui_coin: 黄金铜钱悦耳双响（甜美 2.2kHz~4.2kHz 金属清音，彻底消除 10kHz 针刺耳鸣）
    raw_coin = biquad_filter(biquad_filter(decode_mp3(STAGE / "ui_coin.mp3"), "hp", 450.0), "lp", 5500.0)
    foley_c = norm(single_impulse_decay(trim_to_transient(raw_coin, 0.27, pre_ms=3.0, onset_ratio=0.40), 42.0, 52.0))
    n_cn = int(0.27 * SR)
    t_cn = np.arange(n_cn, dtype=np.float64) / SR
    bell1 = (np.sin(2 * math.pi * 2093.0 * t_cn) * 0.65 + np.sin(2 * math.pi * 3136.0 * t_cn) * 0.35) * np.exp(-t_cn / 0.042)
    bell2 = np.zeros(n_cn, dtype=np.float64)
    off_c = int(0.050 * SR)
    tc2 = np.arange(n_cn - off_c, dtype=np.float64) / SR
    bell2[off_c:] = (np.sin(2 * math.pi * 2637.0 * tc2) * 0.68 + np.sin(2 * math.pi * 3951.0 * tc2) * 0.32) * np.exp(-tc2 / 0.056)
    finalize_and_save(foley_c * 0.36 + (bell1 * 0.76 + bell2 * 0.88), OUT_DIR / "ui_coin.mp3", peak_db=-2.8, fade_out_ms=30.0)

    # 9. ui_deny: 银两不足轻快双木叩（首拍最大，2ms 到峰，温和下行，手机外放清晰不闷）
    n_dn = int(0.195 * SR)
    out_dn = np.zeros(n_dn, dtype=np.float64)
    k1 = crisp_transient(0.088, 2280.0, 466.16, tau_hi_ms=8.5, tau_mid_ms=19.0, w_hi=0.52, w_mid=0.68)
    k2 = crisp_transient(0.105, 1820.0, 349.23, tau_hi_ms=8.5, tau_mid_ms=22.0, w_hi=0.48, w_mid=0.72)
    out_dn[:len(k1)] += norm(k1) * 1.00
    off_d = int(0.068 * SR)
    out_dn[off_d:off_d + len(k2)] += norm(k2)[:n_dn - off_d] * 0.82
    finalize_and_save(out_dn, OUT_DIR / "ui_deny.mp3", peak_db=-3.2, fade_out_ms=24.0)

    # 10. join: 局内核心「召唤」按钮（棋子劲脆落盘 + 英雄登场五声清亮泛音，pk<=12ms）
    raw_join = biquad_filter(biquad_filter(decode_mp3(STAGE / "join.mp3"), "hp", 150.0), "lp", 5600.0)
    foley_j = norm(single_impulse_decay(trim_to_transient(raw_join, 0.26, pre_ms=2.0, onset_ratio=0.45), 34.0, 45.0))
    n_jn = len(foley_j)
    t_jn = np.arange(n_jn, dtype=np.float64) / SR
    thump_j = np.sin(2 * math.pi * (105.0 * t_jn + 85.0 * 0.012 * (1.0 - np.exp(-t_jn / 0.012)))) * np.exp(-t_jn / 0.028) * 0.48
    snap_j = crisp_transient(0.26, 2950.0, 1174.66, tau_hi_ms=12.0, tau_mid_ms=42.0, w_hi=0.52, w_mid=0.42)
    off_j = int(0.035 * SR)
    tj2 = np.arange(n_jn - off_j, dtype=np.float64) / SR
    snap_j[off_j:] += np.sin(2 * math.pi * 1760.0 * tj2) * np.exp(-tj2 / 0.062) * 0.36
    finalize_and_save(foley_j * 0.52 + thump_j + snap_j, OUT_DIR / "join.mp3", peak_db=-2.2, fade_out_ms=28.0)

    # 11. ui_forge: 铁匠铺锻造（扎实铁锤敲砧 + 百炼钢鸣，无 8.8kHz 刺耳嘶声）
    raw_fg = biquad_filter(biquad_filter(decode_mp3(STAGE / "ui_forge.mp3"), "hp", 135.0), "lp", 5500.0)
    foley_fg = norm(single_impulse_decay(trim_to_transient(raw_fg, 0.36, pre_ms=2.5, onset_ratio=0.45), 50.0, 72.0))
    n_fg = len(foley_fg)
    t_fg = np.arange(n_fg, dtype=np.float64) / SR
    anvil = (
        np.sin(2 * math.pi * 1568.0 * t_fg) * np.exp(-t_fg / 0.090) * 0.52
        + np.sin(2 * math.pi * 2349.3 * t_fg) * np.exp(-t_fg / 0.068) * 0.42
        + np.sin(2 * math.pi * (115.0 * t_fg + 90.0 * 0.014 * (1.0 - np.exp(-t_fg / 0.014)))) * np.exp(-t_fg / 0.034) * 0.44
    )
    finalize_and_save(foley_fg * 0.56 + anvil * 0.64, OUT_DIR / "ui_forge.mp3", peak_db=-2.2, fade_out_ms=34.0)

    # 12. ui_drum & ult_drum: 战鼓鼓槌脆击皮面(400Hz~3kHz) + 紧实牛皮鼓腔(108Hz~240Hz)，手机外放清晰震撼
    slap_raw = biquad_filter(biquad_filter(decode_mp3(STAGE / "ult_drum.mp3"), "hp", 220.0), "lp", 5400.0)
    one_slap = norm(single_impulse_decay(trim_to_transient(slap_raw, 0.20, pre_ms=2.0, onset_ratio=0.45), 24.0, 32.0))

    def make_drum_hit(dur_s: float, f_end: float = 106.0) -> np.ndarray:
        n_d = int(dur_s * SR)
        t_d = np.arange(n_d, dtype=np.float64) / SR
        body = (
            np.sin(2 * math.pi * (f_end * t_d + 78.0 * 0.018 * (1.0 - np.exp(-t_d / 0.018)))) * np.exp(-t_d / 0.068) * 0.56
            + np.sin(2 * math.pi * (f_end * 2.15 * t_d + 110.0 * 0.014 * (1.0 - np.exp(-t_d / 0.014)))) * np.exp(-t_d / 0.048) * 0.46
            + np.sin(2 * math.pi * (f_end * 3.40 * t_d + 140.0 * 0.010 * (1.0 - np.exp(-t_d / 0.010)))) * np.exp(-t_d / 0.028) * 0.34
        )
        out_d = body
        m_s = min(len(one_slap), n_d)
        out_d[:m_s] += one_slap[:m_s] * 0.92
        return out_d

    finalize_and_save(make_drum_hit(0.36, 108.0), OUT_DIR / "ui_drum.mp3", peak_db=-1.8, fade_out_ms=36.0)

    n_ud = int(0.84 * SR)
    out_ud = np.zeros(n_ud, dtype=np.float64)
    for t_off, g, fe in [(0.0, 0.80, 112.0), (0.165, 0.88, 106.0), (0.345, 1.00, 100.0)]:
        h = make_drum_hit(0.45, fe) * g
        i0 = int(t_off * SR)
        m = min(len(h), n_ud - i0)
        out_ud[i0:i0 + m] += h[:m]
    finalize_and_save(out_ud, OUT_DIR / "ult_drum.mp3", peak_db=-1.5, fade_out_ms=46.0)


def master_combat_foley() -> None:
    COMBAT_SPECS = [
        ("bow",           0.24, 140.0, 5600.0, -2.2,  4.0, 0.44, (40.0, 50.0),  2.0),
        ("arrow_hit",     0.20, 145.0, 5400.0, -2.0,  3.0, 0.45, (26.0, 34.0),  5.0),
        ("arrow_pierce",  0.22, 135.0, 5600.0, -2.0,  3.0, 0.45, (30.0, 38.0),  3.0),
        ("arrow_blast",   0.26, 120.0, 5600.0, -1.8,  3.0, 0.45, (34.0, 48.0),  2.5),
        ("blade_hit",     0.22, 140.0, 5500.0, -1.8,  3.0, 0.48, (28.0, 36.0),  2.0),
        ("glaive_cleave", 0.34, 105.0, 5600.0, -1.6,  4.0, 0.42, (52.0, 65.0),  0.0),
        ("axe",           0.26, 130.0, 5400.0, -2.0,  4.0, 0.48, (36.0, 48.0),  3.5),
        ("axe_chop",      0.26, 115.0, 5500.0, -1.8,  3.0, 0.45, (38.0, 48.0),  1.5),
        ("fist_swing",    0.19, 150.0, 5200.0, -2.4,  4.0, 0.42, (30.0, 36.0),  1.5),
        ("fist",          0.23, 115.0, 5200.0, -1.6,  3.0, 0.45, (32.0, 40.0),  4.5),
        ("hammer",        0.32, 105.0, 5400.0, -1.6,  3.0, 0.45, (44.0, 58.0),  4.5),
        ("stone",         0.25, 115.0, 5200.0, -2.0,  3.0, 0.45, (38.0, 46.0),  5.0),
        ("fuse_throw",    0.23, 150.0, 5400.0, -2.2,  4.0, 0.44, (38.0, 44.0),  3.5),
        ("mace_cast",     0.28, 125.0, 5400.0, -2.0,  4.0, 0.48, (36.0, 50.0),  4.5),
        ("cannon_fire",   0.30, 100.0, 5500.0, -1.8,  4.0, 0.45, (46.0, 60.0),  0.0),
        ("oil_jar",       0.40, 115.0, 5500.0, -1.8,  4.0, 0.42, (78.0, 82.0),  0.0),
        ("fire_beam",     0.34, 130.0, 5400.0, -2.2,  4.0, 0.46, (52.0, 65.0),  3.5),
        ("thunder_cast",  0.26, 150.0, 5600.0, -2.2,  4.0, 0.42, (46.0, 50.0),  0.0),
        ("spell_cast",    0.32, 160.0, 5600.0, -2.4,  4.0, 0.40, (62.0, 66.0),  0.0),
        ("spell_wind",    0.30, 165.0, 5400.0, -2.4,  5.0, 0.42, (56.0, 58.0),  5.0),
        ("water",         0.28, 150.0, 5400.0, -2.2,  3.0, 0.45, (30.0, 42.0),  1.5),
        ("array_seal",    0.36, 125.0, 5600.0, -2.0,  4.0, 0.42, (60.0, 74.0),  0.0),
        ("poison_puff",   0.26, 155.0, 5000.0, -2.3,  4.0, 0.40, (46.0, 52.0),  3.5),
        ("net_cast",      0.28, 150.0, 5400.0, -2.2,  4.0, 0.42, (50.0, 56.0),  0.0),
        ("ice_shatter",   0.40, 135.0, 5800.0, -1.6,  4.0, 0.42, (68.0, 78.0),  1.5),
        ("crit",          0.22, 135.0, 5600.0, -1.8,  3.0, 0.45, (34.0, 42.0),  0.0),
        ("kill_pop",      0.13, 165.0, 4800.0, -3.5,  2.5, 0.45, (18.0, 24.0),  2.5),
        ("lord_slash",    0.54, 105.0, 5600.0, -1.3,  6.0, 0.38, (110.0, 115.0), 1.5),
        ("ult_arrow",     0.78,  95.0, 5800.0, -1.2, 10.0, 0.35, (220.0, 170.0), 0.0),
        ("ult_cannon",    0.92,  90.0, 5600.0, -1.2,  8.0, 0.35, (380.0, 210.0), 4.5),
        ("ult_wind",      0.88, 105.0, 5600.0, -1.4, 12.0, 0.35, (320.0, 200.0), 1.5),
    ]

    for name, dur_s, hp_hz, lp_hz, pk_db, pre_ms, onset_r, (hold_ms, tau_ms), mid_db in COMBAT_SPECS:
        x = decode_mp3(STAGE / f"{name}.mp3")
        x = biquad_filter(biquad_filter(x, "hp", hp_hz, 0.707), "hp", hp_hz * 0.85, 0.707)
        x = biquad_filter(biquad_filter(x, "lp", lp_hz, 0.707), "lp", lp_hz * 1.08, 0.707)
        if mid_db > 0.1:
            x = biquad_filter(x, "peak", 1350.0, 0.85, mid_db)
        seg = trim_to_transient(x, dur_s, pre_ms=pre_ms, onset_ratio=onset_r)
        seg = single_impulse_decay(seg, hold_ms, tau_ms)

        if name == "crit":
            t = np.arange(len(seg), dtype=np.float64) / SR
            punch = np.sin(2 * math.pi * (130.0 * t + 110.0 * 0.010 * (1.0 - np.exp(-t / 0.010)))) * np.exp(-t / 0.028)
            seg = norm(seg) * 0.72 + punch * 0.52
        elif name == "lord_slash":
            raw_ls = biquad_filter(biquad_filter(decode_mp3(STAGE / "lord_slash.mp3"), "hp", 125.0), "lp", 5500.0)
            pk_i = int(np.argmax(np.abs(raw_ls)))
            s_ls = max(0, pk_i - int(0.025 * SR))
            n_ls = int(dur_s * SR)
            wind_ls = raw_ls[s_ls:s_ls + n_ls]
            if len(wind_ls) < n_ls:
                wind_ls = np.pad(wind_ls, (0, n_ls - len(wind_ls)))
            wind_ls = single_impulse_decay(norm(wind_ls), 95.0, 110.0)
            t = np.arange(n_ls, dtype=np.float64) / SR
            ring = (
                np.sin(2 * math.pi * 1174.66 * t) * np.exp(-t / 0.10) * 0.46
                + np.sin(2 * math.pi * 1760.0 * t) * np.exp(-t / 0.075) * 0.32
                + np.sin(2 * math.pi * (105.0 * t + 95.0 * 0.018 * (1.0 - np.exp(-t / 0.018)))) * np.exp(-t / 0.070) * 0.52
            )
            seg = wind_ls * 0.65 + ring * 0.60

        finalize_and_save(seg, OUT_DIR / f"{name}.mp3", peak_db=pk_db, fade_out_ms=26.0)

    # 燕青机巧三连弩 crossbow
    cb_raw = biquad_filter(biquad_filter(decode_mp3(STAGE / "crossbow.mp3"), "hp", 160.0), "lp", 5600.0)
    one_bolt = norm(single_impulse_decay(trim_to_transient(cb_raw, 0.11, pre_ms=2.0, onset_ratio=0.45), 18.0, 22.0))
    n_cb = int(0.26 * SR)
    burst = np.zeros(n_cb, dtype=np.float64)
    for t_off, g, rate in [(0.000, 1.00, 0.98), (0.062, 0.90, 1.03), (0.124, 0.94, 1.08)]:
        idx_src = np.arange(0, len(one_bolt) - 1, rate)
        i0 = idx_src.astype(np.int64)
        fr = idx_src - i0
        resampled = (one_bolt[i0] * (1.0 - fr) + one_bolt[i0 + 1] * fr) * g
        b0 = int(t_off * SR)
        m = min(len(resampled), n_cb - b0)
        burst[b0:b0 + m] += resampled[:m]
    finalize_and_save(burst, OUT_DIR / "crossbow.mp3", peak_db=-2.0, fade_out_ms=22.0)

    # 额外清理存量音效中的轻微前导静音或低频堆积（ui_reveal / ui_talent / ult_smash）
    for trim_name, dur_keep in [("ui_reveal", 0.82), ("ui_talent", 0.65)]:
        p = OUT_DIR / f"{trim_name}.mp3"
        if p.exists():
            rx = decode_mp3(p)
            seg_t = trim_to_transient(rx, dur_keep, pre_ms=3.0, onset_ratio=0.25)
            finalize_and_save(seg_t, p, peak_db=-1.6, fade_out_ms=30.0)

    p_smash = OUT_DIR / "ult_smash.mp3"
    if p_smash.exists():
        rx = decode_mp3(p_smash)
        rx = biquad_filter(biquad_filter(rx, "hp", 95.0, 0.707), "peak", 1250.0, 0.8, 5.5)
        finalize_and_save(rx, p_smash, peak_db=-1.2, fade_out_ms=40.0)


if __name__ == "__main__":
    build_ui_family()
    master_combat_foley()
    print("[master_all_sfx] 全部 UI 与战斗音效精修完成！")
