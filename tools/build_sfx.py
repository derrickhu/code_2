#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""《宋江别慌张》(code_2) 全量高品质商业级游戏音效合成与母带管线（对标《永远的蔚蓝星球》）。

设计原则：
  1. 告别旧版「2.2秒砖墙白噪块 / -54dB 极弱底噪被 Unity Normalize 放大 / 原始方波锯齿波蜂鸣」三大顽疾；
  2. 物理模态合成（牛皮大鼓贝塞尔膜模态、花梨木自由梁模态、百炼寒钢色散模态、青铜编钟双音模态、古筝五声波导拨弦、和田青玉/粗陶碎裂簇）+ 时变双二阶滤波器组（气动破风、烈焰爆燃、雷霆电弧、玄水激流）；
  3. 频段分层久听不累：高频连发的普攻/受击严格收束在中低频与温润中频（150Hz~5.5kHz 柔化滚降），召唤/合成短促爽脆可连点成鼓点，大招/奥义/寨主剑气具备「蓄势→爆发→余韵」三段戏剧张力；
  4. 全量对齐 32 位好汉「稀有·史诗·传说」三阶专属兵器与四技能体系（连弩、穿云箭、虎蹲重炮、猛火油罐、霹雳火陨、天罡雷链、青龙偃月刀、风雪冰碎、八阵图、替天行道、奥义觉醒、绝技触发）。

用法：
  python3 tools/build_sfx.py
依赖：
  Python 3 标准库 + 系统 ffmpeg (libmp3lame)
"""

import array
import hashlib
import math
import os
import random
import re
import struct
import subprocess
import tempfile
import time
import wave
from typing import Callable, List, Sequence, Tuple

SR = 44100
TWOPI = 2.0 * math.pi
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Sfx")

# ==================== 物理材质模态比 (f_ratio, amp, decay_scale) ====================

DRUM_MODES = [
    (1.000, 1.00, 1.00),
    (1.593, 0.52, 0.62),
    (2.136, 0.34, 0.45),
    (2.296, 0.24, 0.38),
    (2.653, 0.16, 0.28),
    (2.917, 0.11, 0.22),
]

WOOD_MODES = [
    (1.000, 1.00, 1.00),
    (2.756, 0.46, 0.45),
    (5.404, 0.18, 0.22),
]

JADE_MODES = [
    (1.000, 1.00, 1.00),
    (2.318, 0.58, 0.68),
    (3.862, 0.28, 0.42),
    (5.190, 0.12, 0.25),
]

COIN_MODES = [
    (1.000, 1.00, 1.00),
    (1.618, 0.68, 0.82),
    (2.414, 0.42, 0.58),
    (3.172, 0.22, 0.38),
]

STEEL_MODES = [
    (1.000, 1.00, 1.00),
    (1.868, 0.65, 0.78),
    (2.742, 0.45, 0.56),
    (3.915, 0.26, 0.38),
    (5.120, 0.14, 0.24),
]

BRONZE_BELL_MODES = [
    (1.000, 1.00, 1.00),
    (1.004, 0.72, 0.96),  # 双音微差拍温润余韵
    (1.498, 0.56, 0.74),
    (2.000, 0.44, 0.62),
    (2.472, 0.28, 0.44),
    (2.996, 0.18, 0.32),
]

GONG_MODES = [
    (1.000, 1.00, 1.00),
    (1.007, 0.82, 0.95),
    (1.242, 0.65, 0.84),
    (1.584, 0.52, 0.72),
    (1.985, 0.40, 0.58),
    (2.360, 0.30, 0.45),
    (2.890, 0.22, 0.36),
    (3.470, 0.15, 0.26),
]

# 中国传统五声音阶频率辅助 (以 D4/G4/C5 为根音，温润国风不刺耳)
NOTE_HZ = {
    "C3": 130.81, "D3": 146.83, "E3": 164.81, "G3": 196.00, "A3": 220.00,
    "C4": 261.63, "D4": 293.66, "E4": 329.63, "F4": 349.23, "G4": 392.00, "A4": 440.00, "B4": 493.88,
    "C5": 523.25, "D5": 587.33, "E5": 659.25, "F#5": 739.99, "G5": 783.99, "A5": 880.00, "B5": 987.77,
    "C6": 1046.50, "D6": 1174.66, "E6": 1318.51, "G6": 1567.98, "A6": 1760.00,
}


# ==================== 基础信号缓存与混音算子 ====================

def zeros(dur: float) -> array.array:
    return array.array("d", [0.0] * max(64, int(round(dur * SR))))


def mix_into(dst: array.array, src: array.array, offset_s: float = 0.0, gain: float = 1.0) -> None:
    start = int(round(offset_s * SR))
    n_dst = len(dst)
    n_src = len(src)
    i0 = max(0, -start)
    i1 = min(n_src, n_dst - start)
    if i0 >= i1:
        return
    for i in range(i0, i1):
        dst[start + i] += src[i] * gain


def mul_env(buf: array.array, attack_s: float, hold_s: float, release_s: float, curve: float = 1.6) -> array.array:
    n = len(buf)
    a_n = max(1, int(attack_s * SR))
    h_n = int(hold_s * SR)
    r_n = max(1, int(release_s * SR))
    for i in range(n):
        if i < a_n:
            g = (i / a_n) ** 0.7
        elif i < a_n + h_n:
            g = 1.0
        else:
            k = (i - a_n - h_n) / r_n
            g = (max(0.0, 1.0 - k)) ** curve if k < 1.0 else 0.0
        buf[i] *= g
    return buf


# ==================== 物理合成原语 ====================

def pitch_drop_body(
    dur: float,
    f_start: float,
    f_end: float,
    drop_tau: float,
    amp_tau: float,
    harm2: float = 0.22,
    drive: float = 1.0,
) -> array.array:
    """带瞬态俯冲包络的低频鼓腔/重击胸腔共鸣层（赋予拳拳到肉的扎实低频体感）。"""
    out = zeros(dur)
    n = len(out)
    phase = 0.0
    inv_sr = 1.0 / SR
    for i in range(n):
        t = i * inv_sr
        f = f_end + (f_start - f_end) * math.exp(-t / max(1e-4, drop_tau))
        phase += TWOPI * f * inv_sr
        env = math.exp(-t / max(1e-4, amp_tau))
        if i < 24:
            env *= i / 24.0
        v = math.sin(phase) + harm2 * math.sin(2.0 * phase + 0.3)
        if drive > 1.02:
            v = math.tanh(v * drive) / math.tanh(drive)
        out[i] = v * env
    return out


def modal_strike(
    dur: float,
    f0: float,
    modes: Sequence[Tuple[float, float, float]],
    base_tau: float,
    pitch_bend: float = 0.0,
    bend_tau: float = 0.025,
    attack_ms: float = 0.5,
    fm_amt: float = 0.0,
    fm_ratio: float = 2.41,
) -> array.array:
    """多模态物理材质敲击（鼓皮、硬木、寒钢、青铜、铜钱、青玉）。"""
    out = zeros(dur)
    n = len(out)
    inv_sr = 1.0 / SR
    att_n = max(1, int(attack_ms * 0.001 * SR))
    phases = [0.0] * len(modes)
    fm_phase = 0.0
    for i in range(n):
        t = i * inv_sr
        bend = 1.0 + pitch_bend * math.exp(-t / max(1e-4, bend_tau))
        att = (i / att_n) if i < att_n else 1.0
        fm_val = 0.0
        if fm_amt > 0.0:
            fm_phase += TWOPI * f0 * fm_ratio * inv_sr
            fm_val = fm_amt * math.exp(-t / (base_tau * 0.35)) * math.sin(fm_phase)
        acc = 0.0
        for m_idx, (ratio, amp, d_scale) in enumerate(modes):
            fk = f0 * ratio * bend
            if fk >= SR * 0.46:
                continue
            phases[m_idx] += TWOPI * fk * inv_sr
            tau_k = max(0.003, base_tau * d_scale)
            acc += amp * math.exp(-t / tau_k) * math.sin(phases[m_idx] + fm_val)
        out[i] = acc * att
    return out


def pluck_string(
    dur: float,
    freq: float,
    damping: float = 0.994,
    brightness: float = 0.65,
    pick_pos: float = 0.23,
    seed: int = 1,
) -> array.array:
    """Karplus-Strong 物理拨弦波导（弓弦崩响、连弩机弦、古筝琵琶五声清音）。"""
    out = zeros(dur)
    n = len(out)
    rng = random.Random(seed)
    period = max(4.0, SR / max(30.0, min(freq, 6000.0)))
    L = int(period)
    frac = period - L
    # 初始拨弦激励：带拾音点梳状滤波的平滑噪声脉冲
    raw = [rng.uniform(-1.0, 1.0) for _ in range(L + 2)]
    comb_off = max(1, int(L * pick_pos))
    delay = [0.0] * (L + 2)
    prev = 0.0
    for k in range(L + 2):
        v = raw[k] - 0.65 * raw[(k - comb_off) % (L + 2)]
        prev = prev + brightness * (v - prev)
        delay[k] = prev

    idx = 0
    lp_state = 0.0
    for i in range(n):
        nxt = (idx + 1) % L
        nxt2 = (idx + 2) % L
        # 分数延迟线性插值
        sample = (1.0 - frac) * delay[idx] + frac * delay[nxt]
        out[i] = sample
        avg = (1.0 - brightness * 0.5) * (0.5 * (delay[idx] + delay[nxt])) + (brightness * 0.5) * delay[idx]
        lp_state = avg * damping
        delay[idx] = lp_state
        idx = nxt
    return out


def noise_burst(
    dur: float,
    attack_s: float,
    decay_tau: float,
    seed: int = 7,
    pink: float = 0.65,
    sustain_amt: float = 0.0,
    sustain_dur: float = 0.0,
) -> array.array:
    """粉红/白混合湍流噪声源（带自然指数衰减包络，杜绝平顶砖墙噪声）。"""
    out = zeros(dur)
    n = len(out)
    rng = random.Random(seed)
    att_n = max(1, int(attack_s * SR))
    sus_n = int(sustain_dur * SR)
    b0 = b1 = b2 = 0.0
    inv_sr = 1.0 / SR
    for i in range(n):
        w = rng.uniform(-1.0, 1.0)
        b0 = 0.99765 * b0 + w * 0.0990460
        b1 = 0.96300 * b1 + w * 0.2965164
        b2 = 0.57000 * b2 + w * 1.0526913
        p = (b0 + b1 + b2 + w * 0.1848) * 0.28
        sig = pink * p + (1.0 - pink) * w
        if i < att_n:
            env = (i / att_n) ** 0.75
        else:
            dt = (i - att_n) * inv_sr
            env = (1.0 - sustain_amt) * math.exp(-dt / max(1e-4, decay_tau))
            if sustain_amt > 0.0:
                if i - att_n < sus_n:
                    env += sustain_amt
                else:
                    env += sustain_amt * math.exp(-(i - att_n - sus_n) * inv_sr / max(1e-4, decay_tau * 1.2))
        out[i] = sig * env
    return out


def biquad_sweep(
    src: array.array,
    mode: str,
    fc_fn: Callable[[float], float],
    q_fn: Callable[[float], float],
) -> array.array:
    """分块更新（每16采样=0.36ms）的时变 RBJ 双二阶滤波器（lp / hp / bp）。"""
    n = len(src)
    out = array.array("d", [0.0] * n)
    z1 = z2 = 0.0
    b0 = b1 = b2 = a1 = a2 = 0.0
    inv_sr = 1.0 / SR
    for i in range(n):
        if (i & 15) == 0:
            t = i * inv_sr
            fc = max(35.0, min(SR * 0.44, fc_fn(t)))
            q = max(0.35, min(18.0, q_fn(t)))
            w0 = TWOPI * fc * inv_sr
            cw = math.cos(w0)
            sw = math.sin(w0)
            alpha = sw / (2.0 * q)
            a0 = 1.0 + alpha
            inv_a0 = 1.0 / a0
            if mode == "lp":
                b1 = (1.0 - cw) * inv_a0
                b0 = b2 = 0.5 * b1
            elif mode == "hp":
                b0 = b2 = 0.5 * (1.0 + cw) * inv_a0
                b1 = -(1.0 + cw) * inv_a0
            else:  # bp (constant 0 dB peak gain or Q-scaled)
                b0 = alpha * inv_a0 * math.sqrt(q)
                b1 = 0.0
                b2 = -b0
            a1 = -2.0 * cw * inv_a0
            a2 = (1.0 - alpha) * inv_a0
        x = src[i]
        y = b0 * x + z1
        z1 = b1 * x - a1 * y + z2
        z2 = b2 * x - a2 * y
        out[i] = y
    return out


def whoosh_layer(
    dur: float,
    f_low: float,
    f_peak: float,
    f_end: float,
    peak_pos: float = 0.32,
    q: float = 2.4,
    seed: int = 11,
    pink: float = 0.6,
) -> array.array:
    """气动破风声（挥刀、刺枪、掷叉、风咒、扇风）：平滑弓形包络 + 共振峰扫频。"""
    rng = random.Random(seed)
    n = max(64, int(round(dur * SR)))
    raw = array.array("d", [0.0] * n)
    b0 = b1 = 0.0
    for i in range(n):
        u = i / max(1, n - 1)
        w = rng.uniform(-1.0, 1.0)
        b0 = 0.99 * b0 + w * 0.14
        b1 = 0.92 * b1 + w * 0.35
        sig = pink * (b0 + b1) * 0.5 + (1.0 - pink) * w
        if u < peak_pos:
            env = math.sin((u / max(1e-3, peak_pos)) * (math.pi * 0.5)) ** 1.25
        else:
            k = (u - peak_pos) / max(1e-3, 1.0 - peak_pos)
            env = (1.0 - k) ** 2.1
        raw[i] = sig * env

    def fc_fn(t: float) -> float:
        u = min(1.0, t / max(1e-3, dur))
        if u < peak_pos:
            s = u / max(1e-3, peak_pos)
            return f_low + (f_peak - f_low) * (s ** 0.85)
        s = (u - peak_pos) / max(1e-3, 1.0 - peak_pos)
        return f_peak + (f_end - f_peak) * (s ** 0.75)

    return biquad_sweep(raw, "bp", fc_fn, lambda _: q)


def scatter_grains(
    dur: float,
    count: int,
    t_min: float,
    t_max: float,
    f_min: float,
    f_max: float,
    grain_dur: float,
    modes: Sequence[Tuple[float, float, float]],
    seed: int = 23,
    amp_decay: float = 1.5,
) -> array.array:
    """离散粒子微碰撞簇（火星迸溅、碎石、冰晶碎裂、陶瓷碎片、禅杖铜环、散落铜钱）。"""
    out = zeros(dur)
    rng = random.Random(seed)
    for k in range(count):
        u = (k + rng.random() * 0.85) / max(1, count)
        t0 = t_min + (t_max - t_min) * (u ** 1.15)
        f0 = f_min * ((f_max / max(1.0, f_min)) ** rng.random())
        amp = ((1.0 - 0.65 * u) ** amp_decay) * rng.uniform(0.55, 1.0)
        g = modal_strike(grain_dur, f0, modes, grain_dur * 0.34, attack_ms=0.35)
        mix_into(out, g, t0, amp / math.sqrt(max(1, count * 0.45)))
    return out


# ==================== 录音棚级母带处理链 ====================

def master_clip(
    buf: array.array,
    peak_db: float = -2.0,
    hp_hz: float = 45.0,
    lp_hz: float = 8500.0,
    room_wet: float = 0.08,
    sat_drive: float = 1.22,
    tail_fade_ms: float = 18.0,
) -> array.array:
    """统一声学母带：去直流超低频 -> 护耳柔化高频 -> 早期空间反射 -> 磁带软饱和 -> 精准峰值定标。"""
    # 1. 高通滤除直流与浑浊超低频，低通消除数字毛刺
    clean = biquad_sweep(buf, "hp", lambda _: hp_hz, lambda _: 0.707)
    if lp_hz < SR * 0.43:
        clean = biquad_sweep(clean, "lp", lambda _: lp_hz, lambda _: 0.707)

    n = len(clean)
    # 2. 短促三抽头早期物理反射（赋予真实空间体积感，绝不拖泥带水）
    if room_wet > 0.005:
        taps = [(int(0.0113 * SR), 0.52), (int(0.0187 * SR), -0.34), (int(0.0289 * SR), 0.21)]
        wet = array.array("d", clean)
        for d_samp, g in taps:
            if d_samp < n:
                for i in range(d_samp, n):
                    wet[i] += clean[i - d_samp] * (g * room_wet)
        # 反射轻微低通吸音
        clean = wet

    # 3. 极短防咔哒起音窗 (0.45ms = 20 samples) + 尾部余弦归零窗
    ramp_in = min(20, n // 8)
    for i in range(ramp_in):
        clean[i] *= i / max(1, ramp_in)
    fade_n = min(max(32, int(tail_fade_ms * 0.001 * SR)), n // 3)
    for i in range(fade_n):
        idx = n - 1 - i
        clean[idx] *= 0.5 * (1.0 - math.cos(math.pi * i / max(1, fade_n)))

    # 4. 先归一化到 1.0，再做温润软膝盖饱和，最后缩放到精确目标峰值 dBFS
    cur_peak = max((abs(v) for v in clean), default=0.0)
    if cur_peak > 1e-9:
        inv_p = 1.0 / cur_peak
        target_lin = 10.0 ** (peak_db / 20.0)
        norm_tanh = 1.0 / math.tanh(sat_drive)
        for i in range(n):
            v = clean[i] * inv_p
            clean[i] = math.tanh(v * sat_drive) * norm_tanh * target_lin
    return clean


def export_mp3(buf: array.array, out_path: str) -> None:
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    n = len(buf)
    pcm = struct.pack("<" + "h" * n, *(max(-32767, min(32767, int(round(v * 32767.0)))) for v in buf))
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tf:
        wav_path = tf.name
    try:
        with wave.open(wav_path, "wb") as wf:
            wf.setnchannels(1)
            wf.setsampwidth(2)
            wf.setframerate(SR)
            wf.writeframes(pcm)
        subprocess.run(
            [
                "ffmpeg", "-y", "-v", "quiet",
                "-i", wav_path,
                "-c:a", "libmp3lame", "-q:a", "2",
                "-ar", str(SR), "-ac", "1",
                out_path,
            ],
            check=True,
        )
    finally:
        if os.path.exists(wav_path):
            os.remove(wav_path)


def sync_meta(mp3_path: str) -> None:
    """保留已有 .meta 的原生 Tuanjie GUID，修正 normalize=0 / 3D=0 / forceToMono=1 / preloadAudioData=1；新音效生成确定性 .meta。"""
    meta_path = mp3_path + ".meta"
    name = os.path.basename(mp3_path)
    if os.path.exists(meta_path):
        with open(meta_path, "r", encoding="utf-8") as f:
            txt = f.read()
        m = re.search(r"^guid:\s*(.+)$", txt, flags=re.MULTILINE)
        guid = m.group(1).strip() if m else hashlib.md5(f"liangshan_sfx:{name}".encode()).hexdigest()
    else:
        guid = hashlib.md5(f"liangshan_sfx:{name}".encode()).hexdigest()

    meta_content = f"""fileFormatVersion: 2
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
    quality: 1
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
"""
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(meta_content)


# ==================== 七十五枚高品质音效逐一精调设计 ====================

def build_all_recipes() -> List[Tuple[str, Callable[[], array.array]]]:
    recipes: List[Tuple[str, Callable[[], array.array]]] = []

    def reg(name: str):
        def deco(fn: Callable[[], array.array]):
            recipes.append((name, fn))
            return fn
        return deco

    # ---------- 一、核心交互与国风界面音效 ----------

    @reg("ui_tap")
    def _():
        # 温润花梨木 + 青玉棋子轻叩（零前导静音，干脆不刺耳）
        b = zeros(0.072)
        mix_into(b, modal_strike(0.068, 780.0, WOOD_MODES, 0.016, pitch_bend=0.18, bend_tau=0.004, attack_ms=0.25), 0.0, 0.85)
        mix_into(b, modal_strike(0.055, 1960.0, JADE_MODES, 0.011, attack_ms=0.2), 0.0005, 0.34)
        mix_into(b, pitch_drop_body(0.040, 260.0, 115.0, 0.008, 0.012), 0.0, 0.45)
        return master_clip(b, peak_db=-2.8, hp_hz=95.0, lp_hz=7200.0, room_wet=0.04, sat_drive=1.15, tail_fade_ms=12.0)

    @reg("ui_tab")
    def _():
        # 竹简/书签轻快翻切
        b = zeros(0.095)
        mix_into(b, whoosh_layer(0.065, 550.0, 1650.0, 900.0, peak_pos=0.28, q=2.2, seed=101), 0.0, 0.42)
        mix_into(b, modal_strike(0.075, 640.0, WOOD_MODES, 0.018, pitch_bend=0.12, attack_ms=0.3), 0.004, 0.72)
        mix_into(b, modal_strike(0.065, 960.0, WOOD_MODES, 0.015, attack_ms=0.3), 0.018, 0.55)
        return master_clip(b, peak_db=-3.4, hp_hz=100.0, lp_hz=6800.0, room_wet=0.05)

    @reg("ui_open")
    def _():
        # 绢帛画轴舒展上扬 + 木轴轻落 + 温玉双音
        b = zeros(0.18)
        mix_into(b, whoosh_layer(0.15, 340.0, 1450.0, 980.0, peak_pos=0.38, q=1.9, seed=102), 0.0, 0.55)
        mix_into(b, modal_strike(0.10, 480.0, WOOD_MODES, 0.022, pitch_bend=0.15), 0.003, 0.50)
        mix_into(b, modal_strike(0.14, NOTE_HZ["D5"], JADE_MODES, 0.045, attack_ms=0.6), 0.016, 0.36)
        mix_into(b, modal_strike(0.13, NOTE_HZ["A5"], JADE_MODES, 0.050, attack_ms=0.6), 0.042, 0.42)
        return master_clip(b, peak_db=-3.0, hp_hz=85.0, lp_hz=8200.0, room_wet=0.07)

    @reg("ui_close")
    def _():
        # 画轴轻拢收起（柔和下行五度木韵）
        b = zeros(0.15)
        mix_into(b, whoosh_layer(0.12, 1280.0, 620.0, 280.0, peak_pos=0.25, q=1.9, seed=103), 0.0, 0.50)
        mix_into(b, modal_strike(0.11, NOTE_HZ["G4"], WOOD_MODES, 0.028, attack_ms=0.4), 0.002, 0.55)
        mix_into(b, modal_strike(0.11, NOTE_HZ["D4"], WOOD_MODES, 0.032, attack_ms=0.4), 0.032, 0.65)
        return master_clip(b, peak_db=-3.6, hp_hz=80.0, lp_hz=6500.0, room_wet=0.05)

    @reg("ui_deny")
    def _():
        # 双声沉稳木梆轻叩（礼貌清晰，彻底取代刺耳方波蜂鸣）
        b = zeros(0.22)
        mix_into(b, modal_strike(0.11, 295.0, WOOD_MODES, 0.028, pitch_bend=0.08, attack_ms=0.4), 0.0, 0.82)
        mix_into(b, pitch_drop_body(0.08, 175.0, 98.0, 0.015, 0.025), 0.0, 0.45)
        mix_into(b, modal_strike(0.13, 220.0, WOOD_MODES, 0.034, pitch_bend=0.06, attack_ms=0.4), 0.078, 0.90)
        mix_into(b, pitch_drop_body(0.09, 140.0, 82.0, 0.018, 0.030), 0.078, 0.50)
        return master_clip(b, peak_db=-3.2, hp_hz=70.0, lp_hz=5200.0, room_wet=0.06)

    @reg("ui_swap")
    def _():
        # 阵容换位：双道轻捷破风 + 虎符石座嵌合清响
        b = zeros(0.17)
        mix_into(b, whoosh_layer(0.12, 420.0, 1580.0, 560.0, peak_pos=0.35, q=2.1, seed=104), 0.0, 0.60)
        mix_into(b, modal_strike(0.10, 560.0, WOOD_MODES, 0.020, attack_ms=0.3), 0.008, 0.55)
        mix_into(b, modal_strike(0.11, 840.0, JADE_MODES, 0.028, attack_ms=0.3), 0.048, 0.65)
        return master_clip(b, peak_db=-3.0, hp_hz=85.0, lp_hz=7500.0, room_wet=0.06)

    @reg("ui_card")
    def _():
        # 战场拖拽好汉移位 / 选强化牌：厚重木牌擦风 + 青石垛口落定闷响
        b = zeros(0.15)
        mix_into(b, whoosh_layer(0.09, 480.0, 1750.0, 620.0, peak_pos=0.30, q=2.0, seed=105), 0.0, 0.55)
        mix_into(b, pitch_drop_body(0.09, 210.0, 92.0, 0.014, 0.028), 0.018, 0.65)
        mix_into(b, modal_strike(0.11, 720.0, WOOD_MODES, 0.022, attack_ms=0.3), 0.018, 0.70)
        return master_clip(b, peak_db=-3.0, hp_hz=75.0, lp_hz=6800.0, room_wet=0.05)

    @reg("ui_flip")
    def _():
        # 聚义抽卡翻牌：零延迟劲道甩牌破风 + 牌面亮光清脆玉鸣
        b = zeros(0.19)
        mix_into(b, whoosh_layer(0.11, 520.0, 2200.0, 780.0, peak_pos=0.28, q=2.3, seed=106), 0.0, 0.72)
        mix_into(b, modal_strike(0.08, 860.0, WOOD_MODES, 0.016, attack_ms=0.25), 0.002, 0.55)
        mix_into(b, modal_strike(0.14, 1568.0, JADE_MODES, 0.045, attack_ms=0.4), 0.028, 0.48)
        return master_clip(b, peak_db=-2.8, hp_hz=90.0, lp_hz=8500.0, room_wet=0.06)

    @reg("ui_tick")
    def _():
        # 进度条跳数算珠轻响：短促圆润竹算珠声，高频连跳绝不扎耳
        b = zeros(0.055)
        mix_into(b, modal_strike(0.050, 1280.0, WOOD_MODES, 0.010, pitch_bend=0.10, attack_ms=0.25), 0.0, 0.85)
        mix_into(b, pitch_drop_body(0.028, 340.0, 160.0, 0.005, 0.008), 0.0, 0.35)
        return master_clip(b, peak_db=-4.8, hp_hz=120.0, lp_hz=6200.0, room_wet=0.03, tail_fade_ms=10.0)

    @reg("ui_reel")
    def _():
        # 生辰纲摇奖机滚轮棘轮轻响
        b = zeros(0.058)
        mix_into(b, modal_strike(0.052, 1080.0, WOOD_MODES, 0.011, pitch_bend=0.14, attack_ms=0.2), 0.0, 0.80)
        mix_into(b, modal_strike(0.040, 2350.0, COIN_MODES, 0.007, attack_ms=0.2), 0.001, 0.25)
        return master_clip(b, peak_db=-4.5, hp_hz=120.0, lp_hz=6800.0, room_wet=0.03, tail_fade_ms=10.0)

    @reg("ui_reel_stop")
    def _():
        # 摇奖滚轮定格落锁：扎实重木顿挫 + 黄铜卡扣咬合清响
        b = zeros(0.19)
        mix_into(b, pitch_drop_body(0.14, 220.0, 74.0, 0.018, 0.045, drive=1.3), 0.0, 0.85)
        mix_into(b, modal_strike(0.12, 540.0, WOOD_MODES, 0.024, attack_ms=0.3), 0.0, 0.72)
        mix_into(b, modal_strike(0.16, 1760.0, COIN_MODES, 0.042, attack_ms=0.3), 0.008, 0.52)
        return master_clip(b, peak_db=-2.0, hp_hz=55.0, lp_hz=8200.0, room_wet=0.06)

    @reg("ui_coin")
    def _():
        # 宋制铜钱碎银入袋：两枚银锭先后清脆碰响 + 钱袋温润衬底
        b = zeros(0.30)
        mix_into(b, pitch_drop_body(0.08, 280.0, 120.0, 0.012, 0.022), 0.0, 0.30)
        mix_into(b, modal_strike(0.22, 2350.0, COIN_MODES, 0.058, attack_ms=0.25, fm_amt=0.18), 0.0, 0.78)
        mix_into(b, modal_strike(0.24, 3136.0, COIN_MODES, 0.068, attack_ms=0.25, fm_amt=0.15), 0.048, 0.88)
        mix_into(b, modal_strike(0.18, 1568.0, JADE_MODES, 0.045, attack_ms=0.4), 0.048, 0.32)
        return master_clip(b, peak_db=-2.6, hp_hz=110.0, lp_hz=10500.0, room_wet=0.07)

    @reg("ui_drum")
    def _():
        # 聚义厅牛皮大鼓重击（用于点将擂鼓、杏黄旗聚义）：浑厚低频鼓膜俯冲 + 鼓槌皮面脆响
        b = zeros(0.45)
        mix_into(b, pitch_drop_body(0.42, 138.0, 56.0, 0.032, 0.135, harm2=0.30, drive=1.35), 0.0, 0.95)
        mix_into(b, modal_strike(0.42, 78.0, DRUM_MODES, 0.145, pitch_bend=0.32, bend_tau=0.022, attack_ms=0.4), 0.0, 0.75)
        # 鼓槌击皮瞬态
        snap = biquad_sweep(noise_burst(0.055, 0.0005, 0.011, seed=107), "bp", lambda _: 1450.0, lambda _: 1.6)
        mix_into(b, snap, 0.0, 0.58)
        mix_into(b, modal_strike(0.09, 420.0, WOOD_MODES, 0.020, attack_ms=0.3), 0.001, 0.42)
        return master_clip(b, peak_db=-1.2, hp_hz=34.0, lp_hz=6800.0, room_wet=0.10, sat_drive=1.30)

    @reg("ui_forge")
    def _():
        # 铁匠铺百炼淬火：铁锤重砸铁砧清越铿锵 + 炉火淬水微嘶
        b = zeros(0.42)
        mix_into(b, pitch_drop_body(0.16, 195.0, 68.0, 0.018, 0.048, drive=1.3), 0.0, 0.72)
        mix_into(b, modal_strike(0.38, 1480.0, STEEL_MODES, 0.115, attack_ms=0.25, fm_amt=0.45), 0.0, 0.92)
        mix_into(b, modal_strike(0.32, 2220.0, COIN_MODES, 0.085, attack_ms=0.25), 0.002, 0.48)
        quench = biquad_sweep(noise_burst(0.24, 0.012, 0.068, seed=108, pink=0.3), "bp", lambda t: 3600.0 - 1600.0 * t, lambda _: 1.8)
        mix_into(b, quench, 0.022, 0.24)
        return master_clip(b, peak_db=-1.6, hp_hz=50.0, lp_hz=9500.0, room_wet=0.09)

    @reg("join")
    def _():
        # 战鼓召唤好汉入阵（从旧版2.20s噪音砖块重制为0.34s劲脆战鼓+五声灵光！连按6下如擂战鼓般爽快！）
        b = zeros(0.35)
        mix_into(b, pitch_drop_body(0.28, 155.0, 64.0, 0.024, 0.088, harm2=0.26, drive=1.28), 0.0, 0.88)
        mix_into(b, modal_strike(0.30, 92.0, DRUM_MODES, 0.092, pitch_bend=0.26, bend_tau=0.018, attack_ms=0.35), 0.0, 0.68)
        snap = biquad_sweep(noise_burst(0.040, 0.0004, 0.009, seed=109), "bp", lambda _: 1600.0, lambda _: 1.8)
        mix_into(b, snap, 0.0, 0.45)
        # 紫金召唤光柱短促上扬双音 (D5 -> A5)
        mix_into(b, pluck_string(0.24, NOTE_HZ["D5"], damping=0.991, brightness=0.68, seed=110), 0.012, 0.36)
        mix_into(b, modal_strike(0.26, NOTE_HZ["A5"], JADE_MODES, 0.075, attack_ms=0.5), 0.042, 0.40)
        return master_clip(b, peak_db=-1.8, hp_hz=40.0, lp_hz=8200.0, room_wet=0.07, sat_drive=1.22)

    @reg("rank_up")
    def _():
        # 二合一升星觉醒（从旧版1.60s砖墙块重制为0.52s磁吸合体顿挫 + 五声宫商徵高叠清越和弦）
        b = zeros(0.54)
        mix_into(b, pitch_drop_body(0.18, 210.0, 78.0, 0.018, 0.055, drive=1.25), 0.0, 0.72)
        mix_into(b, whoosh_layer(0.14, 480.0, 2100.0, 1200.0, peak_pos=0.25, q=2.2, seed=111), 0.0, 0.42)
        notes = [("D5", 0.00, 0.62), ("F#5", 0.042, 0.66), ("A5", 0.084, 0.74), ("D6", 0.128, 0.88)]
        for nt, t0, g in notes:
            hz = NOTE_HZ[nt]
            mix_into(b, pluck_string(0.38, hz, damping=0.994, brightness=0.72, seed=112 + int(hz)), t0, g * 0.55)
            mix_into(b, modal_strike(0.38, hz, JADE_MODES, 0.115, attack_ms=0.4), t0, g * 0.48)
        return master_clip(b, peak_db=-1.6, hp_hz=48.0, lp_hz=9600.0, room_wet=0.09)

    @reg("ui_slot_add")
    def _():
        # 扩充阵位：青石台座落定 + 四阶五声编钟递升
        b = zeros(0.48)
        mix_into(b, pitch_drop_body(0.20, 180.0, 66.0, 0.020, 0.062, drive=1.3), 0.0, 0.80)
        mix_into(b, modal_strike(0.14, 380.0, WOOD_MODES, 0.028, attack_ms=0.3), 0.0, 0.55)
        for idx, nt in enumerate(["C5", "E5", "G5", "C6"]):
            t0 = 0.018 + idx * 0.052
            mix_into(b, modal_strike(0.30, NOTE_HZ[nt], BRONZE_BELL_MODES, 0.095, attack_ms=0.4), t0, 0.42 + idx * 0.06)
        return master_clip(b, peak_db=-2.0, hp_hz=45.0, lp_hz=9200.0, room_wet=0.08)

    @reg("kill_pop")
    def _():
        # 击退官军轻快反馈（取代刺耳白噪咔哒：微润低频气泡顿挫 + 轻甲微响，百连杀亦温润解压）
        b = zeros(0.10)
        mix_into(b, pitch_drop_body(0.085, 235.0, 82.0, 0.014, 0.026, harm2=0.18), 0.0, 0.85)
        mix_into(b, modal_strike(0.065, 590.0, WOOD_MODES, 0.013, pitch_bend=-0.15, bend_tau=0.010, attack_ms=0.3), 0.002, 0.48)
        mix_into(b, modal_strike(0.055, 1680.0, COIN_MODES, 0.009, attack_ms=0.3), 0.005, 0.16)
        return master_clip(b, peak_db=-4.2, hp_hz=65.0, lp_hz=5600.0, room_wet=0.03, tail_fade_ms=12.0)

    # ---------- 二、养成进阶、开箱大奖与号角预警 ----------

    @reg("ui_unlock")
    def _():
        # 解锁突破：铜锁清脆弹开 + 五声玉磬流光上行
        b = zeros(0.72)
        mix_into(b, modal_strike(0.14, 1420.0, STEEL_MODES, 0.032, attack_ms=0.25, fm_amt=0.25), 0.0, 0.65)
        mix_into(b, pitch_drop_body(0.16, 200.0, 80.0, 0.018, 0.045), 0.0, 0.50)
        for idx, nt in enumerate(["G4", "A4", "C5", "E5", "G5"]):
            t0 = 0.035 + idx * 0.058
            hz = NOTE_HZ[nt]
            mix_into(b, pluck_string(0.45, hz, damping=0.995, brightness=0.72, seed=120 + idx), t0, 0.45)
            mix_into(b, modal_strike(0.42, hz, JADE_MODES, 0.135, attack_ms=0.4), t0, 0.42)
        return master_clip(b, peak_db=-1.8, hp_hz=55.0, lp_hz=9800.0, room_wet=0.10)

    @reg("ui_talent")
    def _():
        # 兵书/星宿点亮：真气聚涌 + 奇门星盘清澈泛音
        b = zeros(0.68)
        mix_into(b, whoosh_layer(0.32, 260.0, 1380.0, 680.0, peak_pos=0.42, q=2.4, seed=125), 0.0, 0.48)
        for idx, nt in enumerate(["A4", "D5", "E5", "A5"]):
            t0 = 0.020 + idx * 0.062
            hz = NOTE_HZ[nt]
            mix_into(b, modal_strike(0.46, hz, BRONZE_BELL_MODES, 0.145, attack_ms=0.5), t0, 0.48)
            mix_into(b, pluck_string(0.42, hz * 2.0, damping=0.993, brightness=0.60, seed=130 + idx), t0 + 0.01, 0.20)
        return master_clip(b, peak_db=-1.8, hp_hz=55.0, lp_hz=9600.0, room_wet=0.11)

    @reg("ui_levelup")
    def _():
        # 好汉/寨主升级：战鼓定音 + 古筝五声刮奏扶摇直上 + 辉煌编钟齐鸣
        b = zeros(0.90)
        mix_into(b, pitch_drop_body(0.28, 150.0, 60.0, 0.025, 0.095, drive=1.25), 0.0, 0.78)
        mix_into(b, modal_strike(0.28, 88.0, DRUM_MODES, 0.095, pitch_bend=0.24, attack_ms=0.4), 0.0, 0.55)
        scale = ["C5", "D5", "E5", "G5", "A5", "C6"]
        for idx, nt in enumerate(scale):
            t0 = 0.024 + idx * 0.048
            hz = NOTE_HZ[nt]
            mix_into(b, pluck_string(0.55, hz, damping=0.996, brightness=0.76, seed=140 + idx), t0, 0.52)
            if idx in (2, 4, 5):
                mix_into(b, modal_strike(0.58, hz, BRONZE_BELL_MODES, 0.185, attack_ms=0.4), t0, 0.45)
        return master_clip(b, peak_db=-1.5, hp_hz=42.0, lp_hz=10200.0, room_wet=0.12)

    @reg("ui_reward")
    def _():
        # 领奖开箱：木箱开启脆响 + 碎银迸落 + 喜庆大三度五声和弦
        b = zeros(0.84)
        mix_into(b, pitch_drop_body(0.18, 210.0, 75.0, 0.018, 0.050), 0.0, 0.68)
        mix_into(b, modal_strike(0.12, 680.0, WOOD_MODES, 0.022, attack_ms=0.3), 0.0, 0.60)
        mix_into(b, scatter_grains(0.45, 7, 0.02, 0.24, 1950.0, 3400.0, 0.14, COIN_MODES, seed=145), 0.0, 0.55)
        for idx, nt in enumerate(["C5", "E5", "G5", "C6"]):
            t0 = 0.045 + idx * 0.055
            hz = NOTE_HZ[nt]
            mix_into(b, pluck_string(0.52, hz, damping=0.995, brightness=0.74, seed=150 + idx), t0, 0.48)
            mix_into(b, modal_strike(0.50, hz, JADE_MODES, 0.155, attack_ms=0.4), t0, 0.40)
        return master_clip(b, peak_db=-1.5, hp_hz=48.0, lp_hz=10200.0, room_wet=0.11)

    @reg("ui_reveal")
    def _():
        # 新好汉登场/满星揭晓：旌旗猎猎展卷 + 天罡金钟与古筝齐鸣
        b = zeros(0.86)
        mix_into(b, whoosh_layer(0.22, 320.0, 1850.0, 720.0, peak_pos=0.30, q=2.1, seed=155), 0.0, 0.62)
        mix_into(b, pitch_drop_body(0.26, 165.0, 62.0, 0.024, 0.085, drive=1.25), 0.035, 0.72)
        for idx, nt in enumerate(["D5", "A5", "D6"]):
            t0 = 0.040 + idx * 0.045
            hz = NOTE_HZ[nt]
            mix_into(b, modal_strike(0.68, hz, BRONZE_BELL_MODES, 0.22, attack_ms=0.4), t0, 0.56)
            mix_into(b, pluck_string(0.62, hz, damping=0.996, brightness=0.75, seed=160 + idx), t0, 0.46)
        return master_clip(b, peak_db=-1.5, hp_hz=45.0, lp_hz=10200.0, room_wet=0.12)

    @reg("ui_jackpot")
    def _():
        # 「超爽!!!」摇奖暴击/凯旋大奖：梁山庆功铜锣 + 战鼓 + 欢腾五声凯歌琶音
        b = zeros(1.06)
        mix_into(b, pitch_drop_body(0.35, 155.0, 54.0, 0.028, 0.12, drive=1.35), 0.0, 0.85)
        mix_into(b, modal_strike(0.85, 293.66, GONG_MODES, 0.28, attack_ms=0.5, fm_amt=0.32), 0.0, 0.52)
        fanfare = [("G4", 0.02), ("C5", 0.075), ("D5", 0.13), ("E5", 0.185), ("G5", 0.24), ("C6", 0.305)]
        for idx, (nt, t0) in enumerate(fanfare):
            hz = NOTE_HZ[nt]
            mix_into(b, pluck_string(0.65, hz, damping=0.996, brightness=0.78, seed=170 + idx), t0, 0.55)
            mix_into(b, modal_strike(0.62, hz, BRONZE_BELL_MODES, 0.195, attack_ms=0.4), t0, 0.42 + idx * 0.04)
        mix_into(b, scatter_grains(0.65, 8, 0.18, 0.52, 2100.0, 3600.0, 0.15, COIN_MODES, seed=178), 0.0, 0.38)
        return master_clip(b, peak_db=-1.2, hp_hz=40.0, lp_hz=10500.0, room_wet=0.13)

    @reg("awaken")
    def _():
        # 本命奥义觉醒（Ev.Awaken）：天罡金印镇地 + 双刃剑鸣破空 + 煌金五度圣辉
        b = zeros(0.88)
        mix_into(b, pitch_drop_body(0.32, 175.0, 50.0, 0.026, 0.115, drive=1.38), 0.0, 0.90)
        mix_into(b, whoosh_layer(0.24, 520.0, 2600.0, 950.0, peak_pos=0.24, q=2.8, seed=181), 0.01, 0.65)
        mix_into(b, modal_strike(0.45, 1318.5, STEEL_MODES, 0.14, attack_ms=0.25, fm_amt=0.28), 0.02, 0.58)
        for idx, nt in enumerate(["D4", "A4", "D5", "F#5", "A5", "D6"]):
            t0 = 0.03 + idx * 0.035
            mix_into(b, modal_strike(0.65, NOTE_HZ[nt], BRONZE_BELL_MODES, 0.21, attack_ms=0.4), t0, 0.38)
        return master_clip(b, peak_db=-1.2, hp_hz=38.0, lp_hz=10200.0, room_wet=0.12)

    @reg("sig_trigger")
    def _():
        # 10级本命绝技触发（Ev.Sig / Ev.Surge）：短促干脆紫金罡气震脉 + 双星清鸣（0.36s 不喧宾夺主）
        b = zeros(0.36)
        mix_into(b, pitch_drop_body(0.16, 190.0, 72.0, 0.018, 0.050, drive=1.2), 0.0, 0.65)
        mix_into(b, whoosh_layer(0.18, 460.0, 1950.0, 880.0, peak_pos=0.28, q=2.5, seed=185), 0.0, 0.50)
        mix_into(b, modal_strike(0.28, NOTE_HZ["E5"], JADE_MODES, 0.085, attack_ms=0.35), 0.012, 0.58)
        mix_into(b, modal_strike(0.30, NOTE_HZ["B5"], BRONZE_BELL_MODES, 0.095, attack_ms=0.35), 0.048, 0.62)
        return master_clip(b, peak_db=-2.5, hp_hz=55.0, lp_hz=9000.0, room_wet=0.08)

    @reg("warn_horn")
    def _():
        # 首领/精英/狂暴来袭预警（取代刺耳锯齿波：宋军低沉牛角号角 + 双声沉雷战鼓）
        b = zeros(0.94)
        n = len(b)
        inv_sr = 1.0 / SR
        horn = zeros(0.94)
        p1 = p2 = 0.0
        for i in range(n):
            t = i * inv_sr
            # 号角气息包络：缓起 -> 饱满 -> 收尾，微弱自然颤音
            swell = math.sin(min(1.0, t / 0.14) * math.pi * 0.5) if t < 0.14 else max(0.0, 1.0 - (t - 0.14) / 0.78) ** 1.45
            vib = 1.0 + 0.008 * math.sin(TWOPI * 5.2 * t)
            f = (130.81 if t < 0.08 else 146.83 - 16.0 * math.exp(-(t - 0.08) / 0.06)) * vib
            p1 += TWOPI * f * inv_sr
            p2 += TWOPI * (f * 1.498) * inv_sr
            # 铜管/牛角温润奇偶泛音列
            sig = (
                0.55 * math.sin(p1)
                + 0.42 * math.sin(2.0 * p1 + 0.2)
                + 0.28 * math.sin(3.0 * p1 + 0.5)
                + 0.15 * math.sin(4.0 * p1)
                + 0.24 * math.sin(p2)
            )
            horn[i] = math.tanh(sig * 1.4) * swell
        horn = biquad_sweep(horn, "lp", lambda t: 950.0 + 650.0 * math.sin(min(1.0, t / 0.85) * math.pi), lambda _: 1.4)
        mix_into(b, horn, 0.0, 0.82)
        mix_into(b, pitch_drop_body(0.32, 130.0, 52.0, 0.028, 0.11, drive=1.3), 0.0, 0.72)
        mix_into(b, pitch_drop_body(0.35, 125.0, 48.0, 0.028, 0.12, drive=1.3), 0.24, 0.65)
        return master_clip(b, peak_db=-2.0, hp_hz=38.0, lp_hz=4500.0, room_wet=0.14)

    # ---------- 三、三十两位好汉出手破风与施法家族 ----------

    @reg("bow")
    def _():
        # 牛角强弓崩弦 + 雕翎破风（从 -44dB 彻底重制：紧绷筋弦弹振 + 清爽箭羽擦风）
        b = zeros(0.19)
        mix_into(b, pluck_string(0.16, 188.0, damping=0.985, brightness=0.58, pick_pos=0.18, seed=201), 0.0, 0.88)
        mix_into(b, modal_strike(0.08, 410.0, WOOD_MODES, 0.018, pitch_bend=-0.12, attack_ms=0.3), 0.0, 0.55)
        mix_into(b, whoosh_layer(0.13, 850.0, 2650.0, 1150.0, peak_pos=0.22, q=2.5, seed=202, pink=0.45), 0.008, 0.72)
        return master_clip(b, peak_db=-2.6, hp_hz=80.0, lp_hz=6400.0, room_wet=0.05)

    @reg("crossbow")
    def _():
        # 传说·燕青机巧三连弩：青铜望山机括脆响 + 钢弓急崩 + 鎏金弩矢疾啸
        b = zeros(0.23)
        mix_into(b, modal_strike(0.06, 2150.0, STEEL_MODES, 0.011, attack_ms=0.2, fm_amt=0.25), 0.0, 0.68)
        mix_into(b, pluck_string(0.14, 295.0, damping=0.986, brightness=0.70, pick_pos=0.15, seed=205), 0.010, 0.82)
        for k in range(3):
            t0 = 0.012 + k * 0.036
            mix_into(b, whoosh_layer(0.085, 1100.0, 3200.0, 1400.0, peak_pos=0.20, q=2.8, seed=206 + k, pink=0.4), t0, 0.55)
        return master_clip(b, peak_db=-2.2, hp_hz=95.0, lp_hz=7200.0, room_wet=0.05)

    @reg("dart")
    def _():
        # 暗器/飞石/毒叉/掷瓮轻捷甩手破风
        b = zeros(0.17)
        mix_into(b, whoosh_layer(0.14, 680.0, 2450.0, 820.0, peak_pos=0.24, q=2.6, seed=210, pink=0.5), 0.0, 0.85)
        mix_into(b, pitch_drop_body(0.055, 340.0, 145.0, 0.010, 0.016), 0.004, 0.45)
        return master_clip(b, peak_db=-2.8, hp_hz=90.0, lp_hz=6200.0, room_wet=0.04)

    @reg("blade_swing")
    def _():
        # 快刀/偃月刀劈风：厚重破风弧 + 寒钢血槽微鸣
        b = zeros(0.24)
        mix_into(b, whoosh_layer(0.21, 320.0, 1820.0, 440.0, peak_pos=0.30, q=2.3, seed=215), 0.0, 0.90)
        mix_into(b, pitch_drop_body(0.11, 210.0, 88.0, 0.018, 0.036), 0.015, 0.52)
        mix_into(b, modal_strike(0.18, 1240.0, STEEL_MODES, 0.042, pitch_bend=0.08, attack_ms=1.5), 0.025, 0.28)
        return master_clip(b, peak_db=-2.3, hp_hz=65.0, lp_hz=6500.0, room_wet=0.06)

    @reg("spear_thrust")
    def _():
        # 丈八蛇矛/麒麟枪/钩镰枪突刺（消除原版26ms前导延迟！白蜡杆抖枪 + 锐利枪尖破空激波）
        b = zeros(0.23)
        mix_into(b, modal_strike(0.07, 460.0, WOOD_MODES, 0.016, attack_ms=0.25), 0.0, 0.62)
        mix_into(b, pitch_drop_body(0.10, 240.0, 95.0, 0.015, 0.032), 0.0, 0.55)
        mix_into(b, whoosh_layer(0.19, 420.0, 2480.0, 760.0, peak_pos=0.24, q=2.9, seed=220), 0.004, 0.88)
        mix_into(b, modal_strike(0.15, 1620.0, STEEL_MODES, 0.036, attack_ms=0.6), 0.018, 0.30)
        return master_clip(b, peak_db=-2.2, hp_hz=65.0, lp_hz=6800.0, room_wet=0.05)

    @reg("fist_swing")
    def _():
        # 武松重拳破风：浑厚低中频拳风压迫感
        b = zeros(0.19)
        mix_into(b, pitch_drop_body(0.13, 195.0, 72.0, 0.020, 0.045, drive=1.2), 0.0, 0.78)
        mix_into(b, whoosh_layer(0.17, 180.0, 960.0, 240.0, peak_pos=0.28, q=1.9, seed=225, pink=0.78), 0.0, 0.88)
        return master_clip(b, peak_db=-2.5, hp_hz=52.0, lp_hz=4800.0, room_wet=0.05)

    @reg("axe")
    def _():
        # 李逵旋风飞斧出手：沉重双板斧双周回旋呼啸
        b = zeros(0.29)
        mix_into(b, whoosh_layer(0.15, 220.0, 1180.0, 320.0, peak_pos=0.35, q=2.2, seed=230, pink=0.7), 0.0, 0.85)
        mix_into(b, whoosh_layer(0.15, 250.0, 1320.0, 340.0, peak_pos=0.35, q=2.2, seed=231, pink=0.7), 0.115, 0.90)
        mix_into(b, modal_strike(0.14, 780.0, STEEL_MODES, 0.032, attack_ms=0.8), 0.02, 0.28)
        return master_clip(b, peak_db=-2.2, hp_hz=58.0, lp_hz=5800.0, room_wet=0.06)

    @reg("staff")
    def _():
        # 鲁智深水磨禅杖腾空：六十二斤浑铁杖沉猛呼啸 + 杖头佛门铜环清脆晃响
        b = zeros(0.29)
        mix_into(b, pitch_drop_body(0.16, 170.0, 62.0, 0.024, 0.058), 0.0, 0.72)
        mix_into(b, whoosh_layer(0.24, 160.0, 860.0, 220.0, peak_pos=0.34, q=2.0, seed=235, pink=0.75), 0.0, 0.88)
        mix_into(b, scatter_grains(0.24, 5, 0.01, 0.14, 2350.0, 3550.0, 0.09, COIN_MODES, seed=236), 0.0, 0.36)
        return master_clip(b, peak_db=-2.2, hp_hz=50.0, lp_hz=7600.0, room_wet=0.06)

    @reg("whip")
    def _():
        # 扈三娘红锦缚龙索甩鞭：丝绸加速破空 + 鞭梢音爆脆啪
        b = zeros(0.25)
        mix_into(b, whoosh_layer(0.15, 380.0, 2600.0, 1400.0, peak_pos=0.68, q=2.6, seed=240), 0.0, 0.68)
        crack = biquad_sweep(noise_burst(0.065, 0.0004, 0.012, seed=241, pink=0.25), "bp", lambda t: 2800.0 - 1200.0 * (t / 0.065), lambda _: 1.7)
        mix_into(b, crack, 0.092, 0.95)
        mix_into(b, pitch_drop_body(0.06, 420.0, 140.0, 0.008, 0.016), 0.092, 0.52)
        return master_clip(b, peak_db=-2.2, hp_hz=75.0, lp_hz=7800.0, room_wet=0.06)

    @reg("net_cast")
    def _():
        # 阮小七撒开天罗地网：麻绳张网呼啸 + 坠网铁砣轻碰
        b = zeros(0.28)
        mix_into(b, whoosh_layer(0.24, 280.0, 1420.0, 480.0, peak_pos=0.36, q=1.8, seed=245), 0.0, 0.80)
        mix_into(b, scatter_grains(0.22, 4, 0.02, 0.14, 1350.0, 2200.0, 0.08, STEEL_MODES, seed=246), 0.0, 0.34)
        return master_clip(b, peak_db=-2.6, hp_hz=70.0, lp_hz=6400.0, room_wet=0.06)

    @reg("cannon_fire")
    def _():
        # 凌振虎蹲重炮出膛：青铜炮膛沉闷轰鸣 + 火药喷口 + 炮弹破空哨音
        b = zeros(0.32)
        mix_into(b, pitch_drop_body(0.24, 165.0, 46.0, 0.024, 0.085, drive=1.45), 0.0, 0.95)
        blast = biquad_sweep(noise_burst(0.20, 0.001, 0.048, seed=250, pink=0.72), "lp", lambda t: 2200.0 * math.exp(-t / 0.08) + 280.0, lambda _: 1.1)
        mix_into(b, blast, 0.0, 0.78)
        # 炮弹划空微哨
        whistle = pitch_drop_body(0.22, 1180.0, 580.0, 0.14, 0.11, harm2=0.08)
        mix_into(b, whistle, 0.035, 0.22)
        return master_clip(b, peak_db=-1.8, hp_hz=36.0, lp_hz=6200.0, room_wet=0.08, sat_drive=1.32)

    @reg("spell_cast")
    def _():
        # 道法符箓施法（公孙胜/马灵/朱武）：黄符抖燃 + 天罡五度灵韵聚气
        b = zeros(0.38)
        mix_into(b, whoosh_layer(0.26, 420.0, 1850.0, 920.0, peak_pos=0.38, q=2.5, seed=255), 0.0, 0.52)
        mix_into(b, modal_strike(0.32, NOTE_HZ["E5"], JADE_MODES, 0.095, attack_ms=2.0), 0.015, 0.45)
        mix_into(b, modal_strike(0.34, NOTE_HZ["B5"], BRONZE_BELL_MODES, 0.11, attack_ms=3.0), 0.055, 0.42)
        return master_clip(b, peak_db=-2.8, hp_hz=80.0, lp_hz=7800.0, room_wet=0.09)

    @reg("spell_wind")
    def _():
        # 风系法术/药粉挥洒（樊瑞/孙二娘）：幽谷旋风呼啸
        b = zeros(0.35)
        mix_into(b, whoosh_layer(0.33, 280.0, 1560.0, 420.0, peak_pos=0.36, q=3.2, seed=260), 0.0, 0.85)
        mix_into(b, whoosh_layer(0.30, 560.0, 2250.0, 780.0, peak_pos=0.45, q=3.6, seed=261), 0.025, 0.45)
        return master_clip(b, peak_db=-2.8, hp_hz=75.0, lp_hz=6500.0, room_wet=0.08)

    @reg("fire_beam")
    def _():
        # 乔道清火焰射线 / 秦明烈火出手：真火轰然腾起 + 木炭噼啪火星（温润不刺耳）
        b = zeros(0.38)
        roar = biquad_sweep(
            noise_burst(0.36, 0.018, 0.125, seed=265, pink=0.80, sustain_amt=0.22, sustain_dur=0.10),
            "bp",
            lambda t: 320.0 + 920.0 * math.sin(min(1.0, t / 0.34) * math.pi),
            lambda _: 1.5,
        )
        mix_into(b, roar, 0.0, 0.88)
        mix_into(b, pitch_drop_body(0.20, 155.0, 58.0, 0.030, 0.075), 0.0, 0.62)
        mix_into(b, scatter_grains(0.32, 6, 0.02, 0.24, 1400.0, 2900.0, 0.035, WOOD_MODES, seed=266), 0.0, 0.28)
        return master_clip(b, peak_db=-2.4, hp_hz=45.0, lp_hz=5800.0, room_wet=0.07)

    # ---------- 四、全品级物理兵刃与元素受击命中家族 ----------

    @reg("arrow_hit")
    def _():
        # 猎箭入甲闷响（解珍）：皮革木盾扎实受击 + 铁簇入肉顿挫
        b = zeros(0.18)
        mix_into(b, pitch_drop_body(0.12, 230.0, 78.0, 0.014, 0.034, drive=1.25), 0.0, 0.88)
        mix_into(b, modal_strike(0.09, 520.0, WOOD_MODES, 0.018, attack_ms=0.25), 0.0, 0.65)
        thwack = biquad_sweep(noise_burst(0.055, 0.0004, 0.011, seed=301), "bp", lambda _: 1750.0, lambda _: 1.9)
        mix_into(b, thwack, 0.0, 0.55)
        return master_clip(b, peak_db=-2.2, hp_hz=60.0, lp_hz=6000.0, room_wet=0.04)

    @reg("arrow_pierce")
    def _():
        # 花荣穿云银箭 / 燕青破甲金矢：锐利破甲贯穿 + 寒银裂空余韵
        b = zeros(0.23)
        mix_into(b, pitch_drop_body(0.13, 250.0, 86.0, 0.014, 0.036, drive=1.28), 0.0, 0.82)
        mix_into(b, modal_strike(0.18, 1520.0, STEEL_MODES, 0.044, pitch_bend=0.14, attack_ms=0.25, fm_amt=0.20), 0.0, 0.72)
        zing = whoosh_layer(0.14, 1400.0, 3100.0, 1650.0, peak_pos=0.18, q=3.4, seed=305, pink=0.35)
        mix_into(b, zing, 0.003, 0.52)
        return master_clip(b, peak_db=-2.0, hp_hz=65.0, lp_hz=7200.0, room_wet=0.05)

    @reg("blade_hit")
    def _():
        # 石秀快刀/宋江剑气斩击（消除5.6ms前导延迟）：利刃切甲脆响 + 扎实受击肉感
        b = zeros(0.23)
        mix_into(b, pitch_drop_body(0.15, 215.0, 72.0, 0.016, 0.042, drive=1.3), 0.0, 0.85)
        slash = biquad_sweep(noise_burst(0.095, 0.0004, 0.022, seed=310, pink=0.4), "bp", lambda t: 2450.0 - 950.0 * (t / 0.095), lambda _: 2.2)
        mix_into(b, slash, 0.0, 0.75)
        mix_into(b, modal_strike(0.19, 1380.0, STEEL_MODES, 0.045, attack_ms=0.25, fm_amt=0.22), 0.0, 0.62)
        return master_clip(b, peak_db=-1.8, hp_hz=55.0, lp_hz=6800.0, room_wet=0.05)

    @reg("glaive_cleave")
    def _():
        # 传说·关胜青龙偃月刀重劈：千钧裂地重斩 + 青龙吞口铜刀沉雄嗡鸣
        b = zeros(0.38)
        mix_into(b, pitch_drop_body(0.30, 175.0, 44.0, 0.026, 0.105, harm2=0.32, drive=1.42), 0.0, 0.95)
        cleave = biquad_sweep(noise_burst(0.16, 0.0006, 0.038, seed=315, pink=0.55), "bp", lambda t: 1850.0 - 900.0 * (t / 0.16), lambda _: 1.8)
        mix_into(b, cleave, 0.0, 0.78)
        mix_into(b, modal_strike(0.34, 880.0, STEEL_MODES, 0.095, pitch_bend=-0.06, attack_ms=0.3, fm_amt=0.30), 0.0, 0.68)
        mix_into(b, modal_strike(0.32, 1320.0, BRONZE_BELL_MODES, 0.088, attack_ms=0.4), 0.006, 0.38)
        return master_clip(b, peak_db=-1.2, hp_hz=38.0, lp_hz=6800.0, room_wet=0.08, sat_drive=1.30)

    @reg("axe_chop")
    def _():
        # 李逵板斧劈砍：凶悍破甲入骨闷响 + 盾木崩裂声
        b = zeros(0.27)
        mix_into(b, pitch_drop_body(0.22, 188.0, 52.0, 0.020, 0.072, harm2=0.28, drive=1.38), 0.0, 0.95)
        crunch = biquad_sweep(noise_burst(0.11, 0.0005, 0.026, seed=320, pink=0.6), "bp", lambda _: 1280.0, lambda _: 1.6)
        mix_into(b, crunch, 0.0, 0.72)
        mix_into(b, modal_strike(0.18, 680.0, STEEL_MODES, 0.042, attack_ms=0.3, fm_amt=0.25), 0.0, 0.58)
        mix_into(b, scatter_grains(0.18, 4, 0.01, 0.07, 480.0, 960.0, 0.05, WOOD_MODES, seed=321), 0.0, 0.42)
        return master_clip(b, peak_db=-1.6, hp_hz=44.0, lp_hz=6200.0, room_wet=0.06, sat_drive=1.28)

    @reg("spear")
    def _():
        # 长枪贯甲（林冲/卢俊义/徐宁）：寒钢枪锋穿透铁甲 + 枪杆震劲
        b = zeros(0.24)
        mix_into(b, pitch_drop_body(0.18, 225.0, 66.0, 0.018, 0.055, drive=1.32), 0.0, 0.88)
        pierce = biquad_sweep(noise_burst(0.085, 0.0004, 0.019, seed=325, pink=0.45), "bp", lambda t: 2200.0 - 800.0 * (t / 0.085), lambda _: 2.4)
        mix_into(b, pierce, 0.0, 0.72)
        mix_into(b, modal_strike(0.20, 1180.0, STEEL_MODES, 0.052, pitch_bend=0.10, attack_ms=0.25, fm_amt=0.24), 0.0, 0.65)
        return master_clip(b, peak_db=-1.8, hp_hz=52.0, lp_hz=6800.0, room_wet=0.05)

    @reg("fist")
    def _():
        # 武松打虎铁拳命中（从 -33dB 重制为崩山裂石级重拳胸腔共鸣！）
        b = zeros(0.26)
        mix_into(b, pitch_drop_body(0.24, 175.0, 42.0, 0.022, 0.088, harm2=0.35, drive=1.48), 0.0, 1.00)
        slap = biquad_sweep(noise_burst(0.080, 0.0004, 0.018, seed=330, pink=0.68), "bp", lambda _: 920.0, lambda _: 1.4)
        mix_into(b, slap, 0.0, 0.75)
        mix_into(b, modal_strike(0.14, 310.0, WOOD_MODES, 0.030, attack_ms=0.3), 0.002, 0.50)
        return master_clip(b, peak_db=-1.4, hp_hz=36.0, lp_hz=5200.0, room_wet=0.06, sat_drive=1.35)

    @reg("hammer")
    def _():
        # 鲁智深禅杖砸地 / 栾廷玉飞锤：浑铁重器震裂青石 + 禅杖铜环震响
        b = zeros(0.34)
        mix_into(b, pitch_drop_body(0.28, 160.0, 40.0, 0.025, 0.098, harm2=0.32, drive=1.45), 0.0, 0.96)
        rock = biquad_sweep(noise_burst(0.15, 0.0006, 0.036, seed=335, pink=0.65), "bp", lambda _: 1050.0, lambda _: 1.5)
        mix_into(b, rock, 0.0, 0.68)
        mix_into(b, modal_strike(0.26, 740.0, STEEL_MODES, 0.068, attack_ms=0.3, fm_amt=0.32), 0.0, 0.62)
        mix_into(b, scatter_grains(0.24, 5, 0.015, 0.11, 2100.0, 3300.0, 0.08, COIN_MODES, seed=336), 0.0, 0.30)
        return master_clip(b, peak_db=-1.4, hp_hz=36.0, lp_hz=6800.0, room_wet=0.08, sat_drive=1.32)

    @reg("stone")
    def _():
        # 寨墙受击/滚石撞击（从0.42 RMS白噪砖重制为真实条石闷撞+碎石散落）
        b = zeros(0.28)
        mix_into(b, pitch_drop_body(0.20, 155.0, 52.0, 0.020, 0.065, drive=1.32), 0.0, 0.88)
        impact = biquad_sweep(noise_burst(0.11, 0.0005, 0.025, seed=340, pink=0.72), "bp", lambda _: 820.0, lambda _: 1.5)
        mix_into(b, impact, 0.0, 0.68)
        mix_into(b, scatter_grains(0.22, 5, 0.01, 0.12, 420.0, 1280.0, 0.06, JADE_MODES, seed=341), 0.0, 0.45)
        return master_clip(b, peak_db=-2.0, hp_hz=42.0, lp_hz=5400.0, room_wet=0.06)

    @reg("jade_ping")
    def _():
        # 张清没羽箭·青玉飞蝗石弹射命中：清脆致密青石撞盔清响
        b = zeros(0.20)
        mix_into(b, pitch_drop_body(0.11, 280.0, 105.0, 0.012, 0.028, drive=1.2), 0.0, 0.75)
        mix_into(b, modal_strike(0.18, 1460.0, JADE_MODES, 0.042, pitch_bend=0.12, attack_ms=0.25), 0.0, 0.85)
        mix_into(b, modal_strike(0.15, 2380.0, JADE_MODES, 0.030, attack_ms=0.25), 0.004, 0.42)
        return master_clip(b, peak_db=-2.2, hp_hz=75.0, lp_hz=7800.0, room_wet=0.05)

    @reg("gold_slam")
    def _():
        # 马灵金砖镇魔天降：沉重金印砸地 + 纯金编钟泛音回荡
        b = zeros(0.36)
        mix_into(b, pitch_drop_body(0.24, 180.0, 50.0, 0.022, 0.082, drive=1.38), 0.0, 0.90)
        mix_into(b, modal_strike(0.32, 1046.5, BRONZE_BELL_MODES, 0.095, attack_ms=0.3, fm_amt=0.22), 0.0, 0.72)
        mix_into(b, modal_strike(0.28, 1568.0, COIN_MODES, 0.075, attack_ms=0.3), 0.005, 0.48)
        return master_clip(b, peak_db=-1.6, hp_hz=42.0, lp_hz=8200.0, room_wet=0.08)

    @reg("lasso")
    def _():
        # 扈三娘缚龙索命中捆缚：丝索骤然收紧绷响 + 日月双刀清脆追斩声
        b = zeros(0.28)
        mix_into(b, pluck_string(0.18, 245.0, damping=0.986, brightness=0.55, seed=350), 0.0, 0.72)
        cinch = whoosh_layer(0.14, 380.0, 1450.0, 620.0, peak_pos=0.25, q=3.0, seed=351)
        mix_into(b, cinch, 0.0, 0.62)
        mix_into(b, modal_strike(0.20, 1680.0, STEEL_MODES, 0.048, attack_ms=0.3, fm_amt=0.18), 0.035, 0.58)
        return master_clip(b, peak_db=-2.0, hp_hz=65.0, lp_hz=7400.0, room_wet=0.06)

    @reg("fire")
    def _():
        # 孔亮独火雷爆破（从平顶白噪砖重制为干脆黑火药起爆脆裂 + 低频气浪 + 散落火星）
        b = zeros(0.38)
        mix_into(b, pitch_drop_body(0.28, 165.0, 38.0, 0.024, 0.095, harm2=0.32, drive=1.50), 0.0, 0.96)
        crack = biquad_sweep(noise_burst(0.075, 0.0004, 0.018, seed=360, pink=0.35), "bp", lambda _: 1650.0, lambda _: 1.5)
        boom = biquad_sweep(noise_burst(0.34, 0.003, 0.088, seed=361, pink=0.82), "lp", lambda t: 1800.0 * math.exp(-t / 0.09) + 220.0, lambda _: 1.0)
        mix_into(b, crack, 0.0, 0.75)
        mix_into(b, boom, 0.004, 0.82)
        mix_into(b, scatter_grains(0.32, 5, 0.03, 0.22, 1200.0, 2600.0, 0.04, WOOD_MODES, seed=362), 0.0, 0.25)
        return master_clip(b, peak_db=-1.5, hp_hz=34.0, lp_hz=6000.0, room_wet=0.08, sat_drive=1.35)

    @reg("cannon_boom")
    def _():
        # 史诗·凌振霹雳重炮落地：大口径震地轰雷 + 铁壳破片飞溅
        b = zeros(0.48)
        mix_into(b, pitch_drop_body(0.40, 145.0, 32.0, 0.032, 0.135, harm2=0.36, drive=1.55), 0.0, 1.00)
        boom = biquad_sweep(noise_burst(0.44, 0.002, 0.115, seed=365, pink=0.85), "lp", lambda t: 2200.0 * math.exp(-t / 0.11) + 180.0, lambda _: 1.05)
        mix_into(b, boom, 0.0, 0.88)
        mix_into(b, modal_strike(0.22, 620.0, STEEL_MODES, 0.048, attack_ms=0.3, fm_amt=0.35), 0.0, 0.45)
        mix_into(b, scatter_grains(0.36, 6, 0.02, 0.22, 1100.0, 2400.0, 0.06, STEEL_MODES, seed=366), 0.0, 0.32)
        return master_clip(b, peak_db=-1.1, hp_hz=30.0, lp_hz=6200.0, room_wet=0.10, sat_drive=1.38)

    @reg("oil_jar")
    def _():
        # 史诗·魏定国猛火油罐：粗陶瓦瓮清脆碎裂 + 猛火油轰然爆燃铺地！
        b = zeros(0.45)
        # 陶瓮碎裂瞬态
        mix_into(b, modal_strike(0.14, 1180.0, JADE_MODES, 0.028, attack_ms=0.2), 0.0, 0.78)
        mix_into(b, scatter_grains(0.22, 8, 0.002, 0.095, 1350.0, 3400.0, 0.055, JADE_MODES, seed=370), 0.0, 0.72)
        # 猛火油轰燃火浪
        mix_into(b, pitch_drop_body(0.28, 150.0, 44.0, 0.028, 0.095, drive=1.35), 0.015, 0.82)
        blaze = biquad_sweep(
            noise_burst(0.40, 0.012, 0.12, seed=371, pink=0.75),
            "bp",
            lambda t: 1450.0 * math.exp(-t / 0.16) + 320.0,
            lambda _: 1.4,
        )
        mix_into(b, blaze, 0.012, 0.85)
        return master_clip(b, peak_db=-1.5, hp_hz=36.0, lp_hz=7200.0, room_wet=0.08, sat_drive=1.28)

    @reg("meteor_hit")
    def _():
        # 传说·秦明霹雳火陨：狼牙重锤陨石砸裂大地 + 熔岩烈焰喷发
        b = zeros(0.52)
        mix_into(b, pitch_drop_body(0.42, 165.0, 34.0, 0.030, 0.145, harm2=0.38, drive=1.55), 0.0, 1.00)
        mix_into(b, modal_strike(0.30, 690.0, STEEL_MODES, 0.075, attack_ms=0.3, fm_amt=0.36), 0.0, 0.62)
        magma = biquad_sweep(noise_burst(0.46, 0.002, 0.13, seed=375, pink=0.80), "lp", lambda t: 2600.0 * math.exp(-t / 0.12) + 210.0, lambda _: 1.15)
        mix_into(b, magma, 0.0, 0.88)
        mix_into(b, scatter_grains(0.38, 7, 0.02, 0.25, 650.0, 1850.0, 0.065, JADE_MODES, seed=376), 0.0, 0.36)
        return master_clip(b, peak_db=-1.0, hp_hz=30.0, lp_hz=6500.0, room_wet=0.10, sat_drive=1.38)

    @reg("thunder_hit")
    def _():
        # 传说·公孙胜天罡雷链命中（修复原版映射成木杖staff.mp3的大Bug！真实紫电撕裂噼啪+沉雷滚滚）
        b = zeros(0.44)
        # 主闪电电弧脆裂
        snap = biquad_sweep(noise_burst(0.085, 0.0003, 0.018, seed=380, pink=0.22), "bp", lambda t: 3200.0 - 1500.0 * (t / 0.085), lambda _: 1.6)
        mix_into(b, snap, 0.0, 0.92)
        # 分叉电弧跳电簇
        for k, dt in enumerate([0.018, 0.042, 0.075]):
            arc = biquad_sweep(noise_burst(0.045, 0.0003, 0.010, seed=381 + k, pink=0.2), "bp", lambda _: 2600.0 + k * 350.0, lambda _: 2.2)
            mix_into(b, arc, dt, 0.55 - k * 0.10)
        # 雷霆低频震荡滚雷
        mix_into(b, pitch_drop_body(0.36, 175.0, 44.0, 0.025, 0.115, harm2=0.35, drive=1.42), 0.004, 0.90)
        rumble = biquad_sweep(noise_burst(0.38, 0.008, 0.11, seed=385, pink=0.88), "lp", lambda t: 1100.0 * math.exp(-t / 0.12) + 160.0, lambda _: 1.2)
        mix_into(b, rumble, 0.010, 0.68)
        return master_clip(b, peak_db=-1.5, hp_hz=34.0, lp_hz=7600.0, room_wet=0.09, sat_drive=1.30)

    @reg("water")
    def _():
        # 单廷珪圣水激流 / 阮小七水花：清冽水浪拍击 + 湍流气泡翻涌
        b = zeros(0.36)
        mix_into(b, pitch_drop_body(0.16, 190.0, 74.0, 0.018, 0.048), 0.0, 0.62)
        splash = biquad_sweep(
            noise_burst(0.32, 0.004, 0.095, seed=390, pink=0.55),
            "bp",
            lambda t: 1650.0 - 850.0 * (t / 0.32) + 280.0 * math.sin(TWOPI * 19.0 * t),
            lambda _: 2.0,
        )
        mix_into(b, splash, 0.0, 0.82)
        # 水珠飞溅气泡颗粒
        mix_into(b, scatter_grains(0.30, 8, 0.01, 0.22, 680.0, 1950.0, 0.045, WOOD_MODES, seed=391), 0.0, 0.45)
        return master_clip(b, peak_db=-2.2, hp_hz=55.0, lp_hz=6800.0, room_wet=0.08)

    @reg("poison_puff")
    def _():
        # 孙二娘蒙汗药囊 / 解宝蝎毒：药囊噗声爆开 + 迷魂花雾轻嘶扩散
        b = zeros(0.34)
        mix_into(b, pitch_drop_body(0.13, 215.0, 82.0, 0.016, 0.038), 0.0, 0.72)
        puff = biquad_sweep(
            noise_burst(0.30, 0.008, 0.092, seed=395, pink=0.45),
            "bp",
            lambda t: 2100.0 * math.exp(-t / 0.14) + 520.0,
            lambda _: 2.2,
        )
        mix_into(b, puff, 0.004, 0.75)
        mix_into(b, modal_strike(0.24, 622.25, JADE_MODES, 0.065, pitch_bend=-0.06, attack_ms=1.5), 0.018, 0.28)
        return master_clip(b, peak_db=-2.5, hp_hz=60.0, lp_hz=6600.0, room_wet=0.07)

    @reg("array_seal")
    def _():
        # 朱武奇门八阵图落阵：阵旗插地 + 八卦青铜古磬共鸣镇锁
        b = zeros(0.44)
        mix_into(b, pitch_drop_body(0.20, 185.0, 62.0, 0.020, 0.062, drive=1.25), 0.0, 0.78)
        mix_into(b, whoosh_layer(0.22, 340.0, 1480.0, 520.0, peak_pos=0.25, q=2.4, seed=400), 0.0, 0.48)
        mix_into(b, modal_strike(0.40, NOTE_HZ["D5"], BRONZE_BELL_MODES, 0.125, attack_ms=0.4), 0.010, 0.62)
        mix_into(b, modal_strike(0.40, NOTE_HZ["A4"], BRONZE_BELL_MODES, 0.135, attack_ms=0.5), 0.035, 0.50)
        return master_clip(b, peak_db=-2.2, hp_hz=48.0, lp_hz=8200.0, room_wet=0.10)

    @reg("crit")
    def _():
        # 暴击高光点睛层（取代0.61 RMS方波破音：凌厉金铁破绽寒芒 + 紧凑低频重击，0.24s干净利落！）
        b = zeros(0.24)
        mix_into(b, pitch_drop_body(0.15, 240.0, 70.0, 0.015, 0.042, drive=1.32), 0.0, 0.82)
        mix_into(b, modal_strike(0.21, 1760.0, STEEL_MODES, 0.052, pitch_bend=0.12, attack_ms=0.2, fm_amt=0.24), 0.0, 0.80)
        mix_into(b, modal_strike(0.18, 2637.0, JADE_MODES, 0.042, attack_ms=0.2), 0.006, 0.48)
        return master_clip(b, peak_db=-2.0, hp_hz=58.0, lp_hz=8400.0, room_wet=0.05)

    # ---------- 五、辅助加持、寒冰碎裂与宋江寨主剑气 ----------

    @reg("magic_soft")
    def _():
        # 吴用运筹帷幄祝福：羽扇清风 + 温润五声星辉竖琴泛音
        b = zeros(0.58)
        mix_into(b, whoosh_layer(0.32, 360.0, 1450.0, 680.0, peak_pos=0.38, q=2.2, seed=410), 0.0, 0.38)
        for idx, nt in enumerate(["G4", "B4", "D5", "G5"]):
            t0 = 0.018 + idx * 0.052
            hz = NOTE_HZ[nt]
            mix_into(b, pluck_string(0.45, hz, damping=0.995, brightness=0.68, seed=415 + idx), t0, 0.48)
            mix_into(b, modal_strike(0.40, hz, JADE_MODES, 0.125, attack_ms=0.8), t0, 0.32)
        return master_clip(b, peak_db=-2.5, hp_hz=65.0, lp_hz=8800.0, room_wet=0.11)

    @reg("horse")
    def _():
        # 戴宗神行甲马 / 战骑奔袭：三拍矫健马蹄踏地（哒·哒·哒）+ 疾风掠耳
        b = zeros(0.48)
        for k, t0 in enumerate([0.0, 0.085, 0.195]):
            f_hoof = 520.0 + (k % 2) * 95.0
            mix_into(b, modal_strike(0.11, f_hoof, WOOD_MODES, 0.022, pitch_bend=-0.10, attack_ms=0.3), t0, 0.78)
            mix_into(b, pitch_drop_body(0.09, 185.0, 72.0, 0.014, 0.028), t0, 0.65)
        mix_into(b, whoosh_layer(0.38, 320.0, 1650.0, 580.0, peak_pos=0.45, q=2.1, seed=420), 0.03, 0.55)
        return master_clip(b, peak_db=-2.0, hp_hz=50.0, lp_hz=6800.0, room_wet=0.07)

    @reg("ice_shatter")
    def _():
        # 林冲风雪山神庙寒冰迸裂：凛冽寒风 + 冰凌晶柱清脆炸裂
        b = zeros(0.52)
        mix_into(b, pitch_drop_body(0.24, 175.0, 52.0, 0.022, 0.075, drive=1.3), 0.0, 0.78)
        mix_into(b, whoosh_layer(0.32, 620.0, 2850.0, 950.0, peak_pos=0.22, q=2.8, seed=425, pink=0.35), 0.0, 0.62)
        mix_into(b, scatter_grains(0.45, 11, 0.003, 0.24, 1850.0, 4600.0, 0.095, JADE_MODES, seed=426, amp_decay=1.2), 0.0, 0.88)
        return master_clip(b, peak_db=-1.5, hp_hz=45.0, lp_hz=9600.0, room_wet=0.10)

    @reg("lord_slash")
    def _():
        # 宋江「替天行道」主公剑气：宝剑出鞘清越龙吟 + 浩荡苍青剑气破浪横推！
        b = zeros(0.68)
        # 出鞘寒芒龙吟
        mix_into(b, modal_strike(0.48, 1174.66, STEEL_MODES, 0.145, pitch_bend=0.16, bend_tau=0.035, attack_ms=0.3, fm_amt=0.28), 0.0, 0.75)
        mix_into(b, modal_strike(0.45, 1760.0, BRONZE_BELL_MODES, 0.135, attack_ms=0.4), 0.015, 0.45)
        # 剑气破浪推进
        mix_into(b, pitch_drop_body(0.38, 180.0, 46.0, 0.032, 0.125, drive=1.38), 0.02, 0.88)
        wave_whoosh = whoosh_layer(0.58, 340.0, 2350.0, 520.0, peak_pos=0.28, q=2.6, seed=430)
        mix_into(b, wave_whoosh, 0.015, 0.90)
        return master_clip(b, peak_db=-1.1, hp_hz=38.0, lp_hz=8800.0, room_wet=0.11, sat_drive=1.28)

    # ---------- 六、传说好汉终极大招（三段式戏剧张力：蓄势 -> 轰击 -> 余韵） ----------

    @reg("ult_drum")
    def _():
        # 梁山聚义三通战鼓催阵（咚·咚·咚——！）
        b = zeros(0.95)
        hits = [(0.0, 0.72, 86.0), (0.16, 0.80, 84.0), (0.34, 1.00, 76.0)]
        for t0, amp, f0 in hits:
            mix_into(b, pitch_drop_body(0.45, f0 * 1.75, f0 * 0.68, 0.028, 0.135, drive=1.38), t0, amp * 0.92)
            mix_into(b, modal_strike(0.48, f0, DRUM_MODES, 0.15, pitch_bend=0.28, attack_ms=0.4), t0, amp * 0.72)
            snap = biquad_sweep(noise_burst(0.05, 0.0004, 0.011, seed=440 + int(f0)), "bp", lambda _: 1500.0, lambda _: 1.6)
            mix_into(b, snap, t0, amp * 0.50)
        return master_clip(b, peak_db=-1.2, hp_hz=32.0, lp_hz=6500.0, room_wet=0.12, sat_drive=1.30)

    @reg("ult_fire")
    def _():
        # 燕青大招「一箭封喉」：满弦蓄劲 -> 超音速金矢裂空音爆 -> 贯胸重创！
        b = zeros(0.88)
        # 0~0.16s 紧绷蓄弦
        mix_into(b, pluck_string(0.18, 220.0, damping=0.988, brightness=0.55, seed=445), 0.0, 0.55)
        mix_into(b, whoosh_layer(0.18, 450.0, 2200.0, 1800.0, peak_pos=0.75, q=3.0, seed=446), 0.0, 0.55)
        # 0.15s 音爆离弦 + 0.28s 封喉命中
        mix_into(b, whoosh_layer(0.28, 1200.0, 3800.0, 900.0, peak_pos=0.18, q=3.2, seed=447, pink=0.35), 0.14, 0.85)
        mix_into(b, pitch_drop_body(0.42, 210.0, 44.0, 0.028, 0.135, drive=1.45), 0.22, 0.96)
        mix_into(b, modal_strike(0.55, 1318.5, STEEL_MODES, 0.16, pitch_bend=0.15, attack_ms=0.25, fm_amt=0.30), 0.22, 0.78)
        return master_clip(b, peak_db=-1.0, hp_hz=34.0, lp_hz=8600.0, room_wet=0.11, sat_drive=1.32)

    @reg("ult_cannon")
    def _():
        # 秦明大招「霹雳天火」/ 城防轰炸：天火呼啸坠落 + 连环重型熔岩大爆破
        b = zeros(1.06)
        whistle = pitch_drop_body(0.24, 1350.0, 320.0, 0.15, 0.18, harm2=0.12)
        mix_into(b, whistle, 0.0, 0.36)
        for k, (t0, amp) in enumerate([(0.12, 0.92), (0.28, 0.85), (0.44, 1.00)]):
            mix_into(b, pitch_drop_body(0.45, 160.0 - k * 10.0, 32.0, 0.032, 0.15, harm2=0.38, drive=1.55), t0, amp)
            boom = biquad_sweep(noise_burst(0.52, 0.002, 0.135, seed=450 + k, pink=0.84), "lp", lambda t: 2400.0 * math.exp(-t / 0.13) + 180.0, lambda _: 1.1)
            mix_into(b, boom, t0, amp * 0.85)
        return master_clip(b, peak_db=-0.9, hp_hz=28.0, lp_hz=6400.0, room_wet=0.12, sat_drive=1.38)

    @reg("ult_thunder")
    def _():
        # 公孙胜大招「五雷天罡」：九霄惊雷撕裂苍穹 + 连环落雷 + 浩荡天威滚雷
        b = zeros(1.15)
        for k, (t0, amp) in enumerate([(0.0, 0.95), (0.14, 0.82), (0.29, 1.00)]):
            snap = biquad_sweep(noise_burst(0.10, 0.0003, 0.022, seed=460 + k, pink=0.22), "bp", lambda t: 3400.0 - 1600.0 * (t / 0.10), lambda _: 1.7)
            mix_into(b, snap, t0, amp * 0.92)
            mix_into(b, pitch_drop_body(0.45, 185.0, 38.0, 0.028, 0.145, harm2=0.36, drive=1.48), t0 + 0.005, amp * 0.92)
        thunder_tail = biquad_sweep(noise_burst(0.82, 0.02, 0.24, seed=465, pink=0.90), "lp", lambda t: 1250.0 * math.exp(-t / 0.25) + 135.0, lambda _: 1.25)
        mix_into(b, thunder_tail, 0.26, 0.78)
        return master_clip(b, peak_db=-0.9, hp_hz=30.0, lp_hz=8000.0, room_wet=0.13, sat_drive=1.35)

    @reg("ult_smash")
    def _():
        # 关胜「拖刀计」/ 卢俊义「麒麟踏阵」：神兵蓄力横扫千军 + 崩山裂地冲击波 + 龙吟刀枪余韵
        b = zeros(0.92)
        mix_into(b, whoosh_layer(0.28, 220.0, 1950.0, 480.0, peak_pos=0.45, q=2.4, seed=470), 0.0, 0.78)
        mix_into(b, pitch_drop_body(0.55, 175.0, 34.0, 0.035, 0.18, harm2=0.38, drive=1.52), 0.11, 1.00)
        quake = biquad_sweep(noise_burst(0.58, 0.003, 0.15, seed=471, pink=0.82), "lp", lambda t: 1950.0 * math.exp(-t / 0.14) + 180.0, lambda _: 1.15)
        mix_into(b, quake, 0.11, 0.82)
        mix_into(b, modal_strike(0.68, 784.0, STEEL_MODES, 0.19, attack_ms=0.3, fm_amt=0.32), 0.11, 0.68)
        return master_clip(b, peak_db=-0.9, hp_hz=30.0, lp_hz=7200.0, room_wet=0.12, sat_drive=1.35)

    @reg("ult_water")
    def _():
        # 林冲大招「风雪山神庙」：暴风雪呼啸席卷 + 万道寒冰凝结爆裂
        b = zeros(0.98)
        blizzard = whoosh_layer(0.85, 380.0, 2450.0, 520.0, peak_pos=0.32, q=3.2, seed=475, pink=0.45)
        mix_into(b, blizzard, 0.0, 0.82)
        mix_into(b, pitch_drop_body(0.42, 165.0, 44.0, 0.030, 0.14, drive=1.32), 0.14, 0.85)
        mix_into(b, scatter_grains(0.75, 14, 0.08, 0.58, 1650.0, 4400.0, 0.11, JADE_MODES, seed=476, amp_decay=1.1), 0.0, 0.80)
        return master_clip(b, peak_db=-1.2, hp_hz=36.0, lp_hz=9200.0, room_wet=0.13)

    @reg("ult_wind")
    def _():
        # 樊瑞大招「飞沙走石」：擎天龙卷风暴拔地而起 + 密集风刃切割呼啸
        b = zeros(1.00)
        vortex1 = whoosh_layer(0.94, 240.0, 1780.0, 360.0, peak_pos=0.38, q=3.5, seed=480, pink=0.62)
        vortex2 = whoosh_layer(0.82, 580.0, 2650.0, 680.0, peak_pos=0.52, q=4.0, seed=481, pink=0.48)
        mix_into(b, vortex1, 0.0, 0.88)
        mix_into(b, vortex2, 0.06, 0.62)
        mix_into(b, pitch_drop_body(0.48, 145.0, 46.0, 0.04, 0.18), 0.08, 0.72)
        mix_into(b, scatter_grains(0.70, 8, 0.10, 0.58, 520.0, 1450.0, 0.06, JADE_MODES, seed=482), 0.0, 0.35)
        return master_clip(b, peak_db=-1.2, hp_hz=38.0, lp_hz=7200.0, room_wet=0.12)

    @reg("ult_whirl")
    def _():
        # 樊瑞「旋风咒」持续绞杀风刃：温润中频旋风斩切，持续跳伤顺滑不噪
        b = zeros(0.52)
        mix_into(b, whoosh_layer(0.48, 320.0, 1680.0, 440.0, peak_pos=0.34, q=3.0, seed=485, pink=0.58), 0.0, 0.85)
        mix_into(b, whoosh_layer(0.32, 720.0, 2350.0, 820.0, peak_pos=0.45, q=3.4, seed=486, pink=0.45), 0.08, 0.48)
        return master_clip(b, peak_db=-2.4, hp_hz=68.0, lp_hz=6200.0, room_wet=0.08)

    @reg("ult_heal")
    def _():
        # 安道全「悬壶济世 / 妙手回春」：春风化雨 + 温润玉磬古筝五声甘霖和弦
        b = zeros(0.92)
        mix_into(b, whoosh_layer(0.52, 340.0, 1280.0, 560.0, peak_pos=0.40, q=2.0, seed=490, pink=0.65), 0.0, 0.36)
        chord = [("C5", 0.01), ("E5", 0.065), ("G5", 0.12), ("A5", 0.175), ("C6", 0.235), ("E6", 0.30)]
        for idx, (nt, t0) in enumerate(chord):
            hz = NOTE_HZ[nt]
            mix_into(b, pluck_string(0.58, hz, damping=0.996, brightness=0.66, seed=491 + idx), t0, 0.45)
            mix_into(b, modal_strike(0.55, hz, JADE_MODES, 0.175, attack_ms=1.0), t0, 0.38)
        return master_clip(b, peak_db=-1.8, hp_hz=65.0, lp_hz=9400.0, room_wet=0.14)

    return recipes


def main() -> None:
    t0 = time.time()
    recipes = build_all_recipes()
    print(f"[build_sfx] 开始合成并母带导出 {len(recipes)} 枚商业级音效 -> {OUT_DIR}")
    for name, fn in recipes:
        buf = fn()
        mp3_path = os.path.join(OUT_DIR, f"{name}.mp3")
        export_mp3(buf, mp3_path)
        sync_meta(mp3_path)
    elapsed = time.time() - t0
    print(f"[build_sfx] 全部 {len(recipes)} 枚音效及 .meta 配置已完成！耗时 {elapsed:.2f}s")


if __name__ == "__main__":
    main()
