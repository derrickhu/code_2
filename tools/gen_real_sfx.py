#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
《宋江别慌张》真实物理拟音（Foley）批量生成与瞬态对齐母带脚本
==============================================================
为什么不用纯 math.sin 正弦波合成战斗音效：
  纯正弦波叠加缺少弓弦筋皮摩擦、竹木雕翎破风、铁镞入甲噗嗤、寒钢切风、陶罐碎裂泼油轰燃、
  战马铁蹄、牛角号角等真实声学瞬态与宽频材质肌理，听起来像电子合成器敲钟/敲木鱼。

本脚本调用 ElevenLabs Sound Effects v2 生成干声物理拟音，并针对动作游戏做三道精密母带处理：
  1. 宽窗口采样（0.8s~1.3s），避免 API 0.5s 下限掐断模型自然发声；
  2. 主瞬态前沿捕获（Onset Alignment）：自动剥离前段微弱蓄力杂音，保证普攻出手/命中在 8~18ms 内直达峰值；
  3. 45Hz 去直流高通（完整保留 60~200Hz 胸腔与木石肉感，绝不误切 200Hz）+ 余弦平滑收尾 + 精准 dBFS 峰值定标 + 原生 .meta GUID 同步。
"""

from __future__ import annotations

import concurrent.futures
import hashlib
import math
import os
import pathlib
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import time
import wave
from dataclasses import dataclass
from typing import List, Optional, Tuple

import requests

API_URL = "https://api.elevenlabs.io/v1/sound-generation"
MODEL_ID = "eleven_text_to_sound_v2"
SR = 44100

ROOT_DIR = pathlib.Path(__file__).resolve().parent.parent
OUT_DIR = ROOT_DIR / "Assets" / "Resources" / "Sfx"
CACHE_DIR = pathlib.Path("/tmp/c2_sfx_raw")


@dataclass
class FoleySpec:
    name: str
    prompt: str
    req_dur: float          # 向 ElevenLabs 请求的原始时长 (>= 0.6s 给足自然发声空间)
    max_dur: float          # 裁剪后的游戏目标最大时长 (s)
    min_dur: float          # 裁剪后的最小保底时长 (s)
    pre_roll_ms: float      # 主瞬态前沿往前最多保留多少毫秒前导 (普攻 10~20ms, 甩鞭/蓄力大招可放宽)
    peak_db: float          # 最终母带目标峰值 dBFS
    hp_hz: float = 45.0     # 高通滤波截止频率 (Hz)
    influence: float = 0.50 # 提示词贴合度
    onset_ratio: float = 0.30 # 主瞬态锁定的相对峰值门限 (命中类设 0.52 可直接切掉飞行前导直达撞击瞬间)


SPECS: List[FoleySpec] = [
    # ==================== 一、射箭、连弩、飞石、暗器家族 ====================
    FoleySpec(
        "bow",
        "Bow and arrow shot, taut bowstring release twang and fast feathered arrow whoosh flying away, crisp close-up foley, dry studio, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.28, min_dur=0.16, pre_roll_ms=18.0, peak_db=-2.2, hp_hz=55.0,
    ),
    FoleySpec(
        "crossbow",
        "Fast mechanical repeating crossbow firing three rapid arrows in a tight burst: crisp metallic string snap-whoosh, snap-whoosh, snap-whoosh, three equal-loudness fast arrow shots, dry close-up foley, no reverb, no music, no voice",
        req_dur=1.10, max_dur=0.36, min_dur=0.24, pre_roll_ms=14.0, peak_db=-2.0, hp_hz=65.0, influence=0.65,
    ),
    FoleySpec(
        "arrow_hit",
        "Single sharp arrow impact hitting thick leather armor and wooden shield: punchy iron arrowhead thwack embedding firmly into wood and leather, tight dry close-up foley, no bell ring, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.22, min_dur=0.12, pre_roll_ms=8.0, peak_db=-1.8, hp_hz=50.0, onset_ratio=0.55,
    ),
    FoleySpec(
        "arrow_pierce",
        "Sharp armor-piercing arrow puncturing straight through iron lamellar plate and thick leather: crisp punchy metallic pierce crunch and brief shaft snap, immediate impact foley, no incoming whoosh, no bell ring, no music, no voice",
        req_dur=0.80, max_dur=0.24, min_dur=0.14, pre_roll_ms=8.0, peak_db=-1.8, hp_hz=55.0, onset_ratio=0.55,
    ),
    FoleySpec(
        "arrow_blast",
        "Compact gunpowder explosive arrow detonating on impact: punchy fiery pop crack with crisp black-powder burst and flying sparks, tight energetic blast foley, no long rumble, no music, no voice",
        req_dur=0.95, max_dur=0.30, min_dur=0.18, pre_roll_ms=18.0, peak_db=-1.8, hp_hz=55.0,
    ),
    FoleySpec(
        "dart",
        "Quick forceful martial arts wrist throw whipping a heavy stone projectile through the air: tight snappy high-speed air whoosh swish, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.22, min_dur=0.11, pre_roll_ms=15.0, peak_db=-2.4, hp_hz=70.0,
    ),
    FoleySpec(
        "jade_ping",
        "Hard dense river stone ricocheting sharply off an iron helmet: crisp stone-on-iron clack impact with solid knock, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.24, min_dur=0.12, pre_roll_ms=10.0, peak_db=-2.0, hp_hz=65.0,
    ),

    # ==================== 二、近战兵刃、重器与护盾家族 ====================
    FoleySpec(
        "spear_thrust",
        "Heavy wooden-shaft steel spear thrusting forward rapidly: taut wood grip snap and sharp steel spearhead slicing straight through air, fast aggressive whoosh, dry foley, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.26, min_dur=0.14, pre_roll_ms=18.0, peak_db=-2.0, hp_hz=55.0,
    ),
    FoleySpec(
        "spear",
        "Heavy steel spearhead stabbing deep through iron armor and thick leather: sharp metal crunch puncture and solid meaty impact thud, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.26, min_dur=0.14, pre_roll_ms=10.0, peak_db=-1.6, hp_hz=48.0, onset_ratio=0.52,
    ),
    FoleySpec(
        "blade_swing",
        "Single fast sharp steel sabre slash cutting through air: crisp metallic blade swish whoosh, clean fast attack, dry foley, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.25, min_dur=0.12, pre_roll_ms=16.0, peak_db=-2.2, hp_hz=60.0,
    ),
    FoleySpec(
        "blade_hit",
        "Sharp steel sabre slicing into leather and chainmail armor: crisp metallic blade slash cut with meaty body impact, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.24, min_dur=0.13, pre_roll_ms=8.0, peak_db=-1.6, hp_hz=50.0, onset_ratio=0.52,
    ),
    FoleySpec(
        "glaive_cleave",
        "Massive heavy Chinese guandao halberd cleaving down into heavy armor and stone ground: powerful steel chop, armor crunch and deep earth thud, dry foley, no bell ring, no reverb, no music, no voice",
        req_dur=0.90, max_dur=0.40, min_dur=0.22, pre_roll_ms=16.0, peak_db=-1.2, hp_hz=38.0,
    ),
    FoleySpec(
        "axe",
        "Heavy iron battle axe thrown spinning rapidly through the air: two tight rhythmic low-pitched metallic rotational whooshes, aggressive close-up foley, no reverb, no music, no voice",
        req_dur=0.85, max_dur=0.34, min_dur=0.20, pre_roll_ms=25.0, peak_db=-2.0, hp_hz=50.0,
    ),
    FoleySpec(
        "axe_chop",
        "Heavy iron battle axe chopping violently into a wooden shield and leather armor: brutal wood splintering crunch and deep meaty chop thud, dry foley, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.30, min_dur=0.16, pre_roll_ms=12.0, peak_db=-1.5, hp_hz=42.0,
    ),
    FoleySpec(
        "fist_swing",
        "Powerful martial arts heavy punch swinging through air: tight cloth snap and deep low-mid air whoosh, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.22, min_dur=0.11, pre_roll_ms=15.0, peak_db=-2.4, hp_hz=48.0,
    ),
    FoleySpec(
        "fist",
        "Devastating martial arts heavy iron fist punch landing squarely on chest armor: deep punchy body thud with crisp leather smack impact, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.28, min_dur=0.14, pre_roll_ms=10.0, peak_db=-1.3, hp_hz=36.0,
    ),
    FoleySpec(
        "staff",
        "Heavy iron monk staff swinging forcefully through air with brass rings clinking briefly: deep heavy polearm whoosh and crisp metal ring rattle, dry foley, no reverb, no music, no voice",
        req_dur=0.85, max_dur=0.32, min_dur=0.18, pre_roll_ms=22.0, peak_db=-2.0, hp_hz=48.0,
    ),
    FoleySpec(
        "hammer",
        "Massive iron warhammer slamming hard into stone pavement: thunderous heavy metallic thud, rock cracking crunch and small gravel scatter, dry foley, no reverb, no music, no voice",
        req_dur=0.85, max_dur=0.38, min_dur=0.20, pre_roll_ms=12.0, peak_db=-1.3, hp_hz=36.0,
    ),
    FoleySpec(
        "shield_block",
        "Thick woven rattan and iron shield blocking a weapon strike: solid tight wooden-metallic parry thud knock, short dry foley, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.22, min_dur=0.10, pre_roll_ms=10.0, peak_db=-2.0, hp_hz=55.0,
    ),

    # ==================== 三、火器、投掷与爆破家族 ====================
    FoleySpec(
        "fuse_throw",
        "Throwing a heavy clay grenade with a lit black-powder fuse: crisp sparking fuse hiss and forceful arm throw air whoosh, dry foley, no explosion, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.30, min_dur=0.16, pre_roll_ms=30.0, peak_db=-2.2, hp_hz=60.0,
    ),
    FoleySpec(
        "mace_cast",
        "Swinging a blazing heavy iron spiked mace: sudden fiery flame flare whoosh combined with a weighty metallic weapon swing, aggressive dry foley, no reverb, no music, no voice",
        req_dur=0.85, max_dur=0.34, min_dur=0.18, pre_roll_ms=20.0, peak_db=-2.0, hp_hz=45.0,
    ),
    FoleySpec(
        "cannon_fire",
        "Antique bronze black-powder cannon firing a heavy iron ball: punchy gunpowder muzzle blast thump and brief outgoing cannonball whistle, dry foley, no long tail, no reverb, no music, no voice",
        req_dur=0.85, max_dur=0.36, min_dur=0.20, pre_roll_ms=12.0, peak_db=-1.6, hp_hz=36.0,
    ),
    FoleySpec(
        "cannon_boom",
        "Heavy black-powder cannonball exploding on battlefield dirt: powerful low-end concussive boom, sharp blast crack and dirt shrapnel scatter, tight dry game sfx, no long reverb, no music, no voice",
        req_dur=0.95, max_dur=0.52, min_dur=0.28, pre_roll_ms=12.0, peak_db=-1.0, hp_hz=30.0,
    ),
    FoleySpec(
        "fire",
        "Compact black-powder hand bomb exploding: sharp snappy blast crack followed by a tight fiery punchy boom, dry foley, no long reverb, no music, no voice",
        req_dur=0.85, max_dur=0.40, min_dur=0.22, pre_roll_ms=10.0, peak_db=-1.4, hp_hz=34.0,
    ),
    FoleySpec(
        "oil_jar",
        "Clay oil pot smashing on stone ground and instantly erupting into roaring flames: crisp ceramic pottery shatter immediately followed by a liquid fire flare whoosh, dry foley, no reverb, no music, no voice",
        req_dur=0.95, max_dur=0.48, min_dur=0.26, pre_roll_ms=14.0, peak_db=-1.4, hp_hz=36.0,
    ),
    FoleySpec(
        "meteor_hit",
        "Blazing fiery meteor warhammer slamming into rocky ground: massive earth-cracking boulder impact crunch with explosive fire burst, deep punchy low end, no long reverb, no music, no voice",
        req_dur=1.00, max_dur=0.55, min_dur=0.30, pre_roll_ms=14.0, peak_db=-1.0, hp_hz=30.0,
    ),

    # ==================== 四、法术与控制家族 ====================
    FoleySpec(
        "thunder_cast",
        "Crackling electric lightning spell gathering in hand: sharp high-voltage static zap snaps and brief energetic air hum, crisp dry sfx, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.30, min_dur=0.15, pre_roll_ms=18.0, peak_db=-2.2, hp_hz=65.0,
    ),
    FoleySpec(
        "thunder_hit",
        "Single sharp lightning bolt strike hitting target: instantaneous high-voltage electric whip crack snap followed by a tight punchy thunderclap boom, dry game sfx, no long tail, no music, no voice",
        req_dur=0.95, max_dur=0.46, min_dur=0.24, pre_roll_ms=10.0, peak_db=-1.4, hp_hz=34.0,
    ),
    FoleySpec(
        "spell_cast",
        "Taoist paper talisman snapping in air and igniting with a brief crisp bronze chime shimmer, focused quick spell cast, dry studio sfx, no reverb, no music, no voice",
        req_dur=0.85, max_dur=0.36, min_dur=0.18, pre_roll_ms=20.0, peak_db=-2.5, hp_hz=70.0,
    ),
    FoleySpec(
        "fire_beam",
        "Concentrated jet of flame roaring forward from a torch: intense fiery whoosh surge with crisp crackling embers, dry foley, no reverb, no music, no voice",
        req_dur=0.85, max_dur=0.40, min_dur=0.22, pre_roll_ms=20.0, peak_db=-2.2, hp_hz=45.0,
    ),
    FoleySpec(
        "water",
        "Forceful rushing water jet splashing hard on impact: crisp pressurized water surge gush and clean splash droplets, dry foley, no reverb, no music, no voice",
        req_dur=0.85, max_dur=0.38, min_dur=0.20, pre_roll_ms=18.0, peak_db=-2.2, hp_hz=55.0,
    ),
    FoleySpec(
        "gold_slam",
        "Heavy solid gold brick slamming down onto iron armor: dense weighty metallic impact thud with a short resonant golden ring, dry foley, no long tail, no music, no voice",
        req_dur=0.85, max_dur=0.36, min_dur=0.18, pre_roll_ms=12.0, peak_db=-1.6, hp_hz=42.0,
    ),
    FoleySpec(
        "spell_wind",
        "Sharp swirling wind gust cast from a sleeve: fast spiraling air whoosh burst, crisp dry foley, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.34, min_dur=0.18, pre_roll_ms=25.0, peak_db=-2.5, hp_hz=65.0,
    ),
    FoleySpec(
        "ult_whirl",
        "Tight spinning whirlwind vortex cutting through air: smooth rhythmic swirling wind blades whoosh, clean dry game sfx, no harsh white noise, no music, no voice",
        req_dur=0.95, max_dur=0.48, min_dur=0.26, pre_roll_ms=35.0, peak_db=-2.4, hp_hz=65.0,
    ),
    FoleySpec(
        "array_seal",
        "Wooden battle formation flag pole planting firmly into ground with a deep ancient bronze mechanism lock resonance, crisp dry game sfx, no music, no voice",
        req_dur=0.90, max_dur=0.44, min_dur=0.24, pre_roll_ms=18.0, peak_db=-2.0, hp_hz=45.0,
    ),
    FoleySpec(
        "poison_puff",
        "Cloth powder pouch bursting open on impact: soft tight pop followed by pressurized herbal dust cloud puffing out, crisp dry foley, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.34, min_dur=0.16, pre_roll_ms=14.0, peak_db=-2.3, hp_hz=60.0,
    ),
    FoleySpec(
        "whip",
        "Long leather bullwhip swinging fast with a rising air whoosh and snapping into a loud sharp whip crack at the end, punchy close-up foley, dry, no reverb, no music, no voice",
        req_dur=1.00, max_dur=0.30, min_dur=0.18, pre_roll_ms=140.0, peak_db=-2.0, hp_hz=65.0,
    ),
    FoleySpec(
        "lasso",
        "Thick heavy rope lasso whipping around armor and yanking tight with a loud taut leather-and-hemp cinch thud and creak, punchy close-up foley, dry, no music, no voice",
        req_dur=0.95, max_dur=0.30, min_dur=0.18, pre_roll_ms=35.0, peak_db=-1.8, hp_hz=55.0,
    ),
    FoleySpec(
        "net_cast",
        "Heavy weighted fishing net thrown wide open through the air: coarse rope net swoosh spreading out with small lead sinkers clinking softly, dry foley, no reverb, no music, no voice",
        req_dur=0.85, max_dur=0.32, min_dur=0.16, pre_roll_ms=25.0, peak_db=-2.4, hp_hz=60.0,
    ),

    # ==================== 五、主公剑气、传说大招、战马、号角、战鼓、受击状态 ====================
    FoleySpec(
        "lord_slash",
        "Ancient Chinese broadsword unsheathing with a clear metallic blade ring immediately followed by a powerful sweeping sword wind wave whoosh, heroic dry game sfx, no music, no voice",
        req_dur=1.10, max_dur=0.68, min_dur=0.36, pre_roll_ms=35.0, peak_db=-1.1, hp_hz=38.0,
    ),
    FoleySpec(
        "ult_arrow",
        "Epic giant warbow sniper shot: taut heavy bowstring creaking draw, thunderous bowstring snap, roaring high-speed arrow whoosh slicing air into a massive armor-shattering impact boom, powerful game ultimate sfx, no music, no voice",
        req_dur=1.60, max_dur=0.90, min_dur=0.58, pre_roll_ms=320.0, peak_db=-1.0, hp_hz=36.0,
    ),
    FoleySpec(
        "ult_cannon",
        "Incoming fiery meteors whistling down into three rapid massive earth-shaking black-powder explosions with rock debris, powerful game ultimate sfx, no music, no voice",
        req_dur=1.35, max_dur=1.06, min_dur=0.60, pre_roll_ms=160.0, peak_db=-0.9, hp_hz=28.0,
    ),
    FoleySpec(
        "ult_thunder",
        "Three rapid sky-splitting lightning bolt strikes: violent electric arc whip cracks with thunderous booming shockwaves, powerful game ultimate sfx, no music, no voice",
        req_dur=1.35, max_dur=1.12, min_dur=0.60, pre_roll_ms=25.0, peak_db=-0.9, hp_hz=30.0,
    ),
    FoleySpec(
        "ult_smash",
        "Colossal heavy halberd sweeping wide and slamming into earth with a ground-cracking shockwave and resonant steel blade impact, powerful martial arts ultimate sfx, no music, no voice",
        req_dur=1.25, max_dur=0.92, min_dur=0.48, pre_roll_ms=140.0, peak_db=-0.9, hp_hz=30.0,
    ),
    FoleySpec(
        "ult_water",
        "Howling icy blizzard wind sweeping fiercely with rapid crystalline frost freezing crackle, crisp game ultimate sfx, no music, no voice",
        req_dur=1.25, max_dur=0.96, min_dur=0.52, pre_roll_ms=50.0, peak_db=-1.2, hp_hz=36.0,
    ),
    FoleySpec(
        "ice_shatter",
        "Thick solid ice pillar shattering explosively into sharp crystalline ice shards: punchy freeze impact and crisp ice crunch scatter, dry foley, no reverb, no music, no voice",
        req_dur=0.95, max_dur=0.52, min_dur=0.26, pre_roll_ms=14.0, peak_db=-1.4, hp_hz=42.0,
    ),
    FoleySpec(
        "ult_wind",
        "Powerful roaring tornado vortex erupting upward with fierce swirling wind gusts and flying gravel, dramatic game ultimate sfx, no music, no voice",
        req_dur=1.25, max_dur=0.98, min_dur=0.52, pre_roll_ms=60.0, peak_db=-1.2, hp_hz=38.0,
    ),
    FoleySpec(
        "ult_drum",
        "Three powerful accelerating strikes on a giant traditional Chinese cowhide war drum: heavywooden mallet hits with deep booming drum resonance, dry percussion, no music, no voice",
        req_dur=1.25, max_dur=0.95, min_dur=0.50, pre_roll_ms=15.0, peak_db=-1.1, hp_hz=32.0,
    ),
    FoleySpec(
        "ui_drum",
        "Single powerful strike on a giant traditional Chinese cowhide war drum: crisp wooden mallet skin attack with deep resonant drum body boom, dry studio foley, no music, no voice",
        req_dur=0.90, max_dur=0.48, min_dur=0.25, pre_roll_ms=12.0, peak_db=-1.2, hp_hz=34.0,
    ),
    FoleySpec(
        "warn_horn",
        "Deep ancient Chinese ox-horn battle horn blown once: solemn urgent resonant brassy low horn call with natural breath swell, battlefield warning, no music, no voice",
        req_dur=1.25, max_dur=0.96, min_dur=0.52, pre_roll_ms=80.0, peak_db=-1.8, hp_hz=38.0,
    ),
    FoleySpec(
        "horse",
        "Warhorse galloping four brisk hoofbeats on hard packed earth road with subtle leather saddle movement, crisp dry foley, no horse neigh, no reverb, no music, no voice",
        req_dur=0.90, max_dur=0.50, min_dur=0.28, pre_roll_ms=18.0, peak_db=-2.0, hp_hz=48.0,
    ),
    FoleySpec(
        "stone",
        "Heavy stone boulder impacting a thick fortress stone wall: solid deep rock thud and small stone rubble crumble, dry foley, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.30, min_dur=0.16, pre_roll_ms=12.0, peak_db=-1.8, hp_hz=40.0,
    ),
    FoleySpec(
        "stun_bonk",
        "Heavy comical-yet-solid club bonk hitting an iron helmet with a resonant metallic clunk and brief dizzy ring, punchy game stun sfx, dry, no music, no voice",
        req_dur=0.90, max_dur=0.30, min_dur=0.18, pre_roll_ms=14.0, peak_db=-2.0, hp_hz=55.0,
    ),
    FoleySpec(
        "crit",
        "Sharp critical hit impact accent: crisp metallic blade flash ting layered with a tight punchy low impact thump, clean dry game sfx, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.24, min_dur=0.12, pre_roll_ms=10.0, peak_db=-1.8, hp_hz=50.0,
    ),
    FoleySpec(
        "kill_pop",
        "Subtle satisfying enemy defeat thud pop: soft tight low-mid leather armor drop pop, very short clean game feedback, no harsh highs, no reverb, no music, no voice",
        req_dur=0.70, max_dur=0.14, min_dur=0.07, pre_roll_ms=10.0, peak_db=-3.8, hp_hz=60.0,
    ),

    # ==================== 六、核心物理材质交互音（铁匠、铜钱、木玉棋、竹简、画轴、令牌） ====================
    FoleySpec(
        "ui_forge",
        "Blacksmith hammer striking hot iron on an anvil once with a clear metallic ring and brief water quench steam hiss, crisp dry foley, no music, no voice",
        req_dur=0.90, max_dur=0.44, min_dur=0.22, pre_roll_ms=10.0, peak_db=-1.6, hp_hz=50.0,
    ),
    FoleySpec(
        "ui_coin",
        "Handful of antique brass coins clinking crisply into a cloth coin pouch, bright pleasant metallic coin jingle, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.80, max_dur=0.30, min_dur=0.15, pre_roll_ms=10.0, peak_db=-2.4, hp_hz=90.0,
    ),
    FoleySpec(
        "ui_tap",
        "Single smooth jade Go stone tapping gently and crisply onto a polished rosewood board, short warm tactile UI click, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.65, max_dur=0.09, min_dur=0.045, pre_roll_ms=8.0, peak_db=-2.8, hp_hz=95.0,
    ),
    FoleySpec(
        "ui_tab",
        "Crisp bamboo slip bookmark flicking once cleanly, light snappy wooden tab switch UI sound, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.65, max_dur=0.11, min_dur=0.055, pre_roll_ms=10.0, peak_db=-3.2, hp_hz=95.0,
    ),
    FoleySpec(
        "ui_open",
        "Traditional silk paper scroll unrolling briskly with wooden roller click, clean warm UI panel open foley, dry, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.20, min_dur=0.10, pre_roll_ms=15.0, peak_db=-2.8, hp_hz=85.0,
    ),
    FoleySpec(
        "ui_close",
        "Wooden scroll rolling shut softly with a gentle low wood tap, clean UI panel close foley, dry, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.16, min_dur=0.08, pre_roll_ms=12.0, peak_db=-3.4, hp_hz=80.0,
    ),
    FoleySpec(
        "ui_card",
        "Solid carved wooden token placed firmly onto a stone board: quick air swish and crisp weighty wood-on-stone thud, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.16, min_dur=0.08, pre_roll_ms=10.0, peak_db=-2.8, hp_hz=75.0,
    ),
    FoleySpec(
        "ui_flip",
        "Crisp thick card whipped and flipped over rapidly in air: snappy paper-wood snap swish, dry close-up foley, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.20, min_dur=0.10, pre_roll_ms=12.0, peak_db=-2.6, hp_hz=85.0,
    ),
    FoleySpec(
        "ui_swap",
        "Two heavy bronze tally tokens sliding and clicking snugly together, crisp tactile swap foley, dry, no reverb, no music, no voice",
        req_dur=0.75, max_dur=0.18, min_dur=0.09, pre_roll_ms=12.0, peak_db=-2.8, hp_hz=85.0,
    ),
]


def load_api_key() -> str:
    env = os.environ.get("ELEVENLABS_API_KEY")
    if env:
        return env.strip()
    local = pathlib.Path("/Users/huyi/.cursor/skills/game-sfx-gen/api_key.local")
    if local.exists():
        return local.read_text(encoding="utf-8").strip()
    raise RuntimeError("未找到 ElevenLabs API Key")


def fetch_raw_mp3(spec: FoleySpec, api_key: str, force_api: bool = False) -> pathlib.Path:
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    cached = CACHE_DIR / f"{spec.name}.mp3"
    if cached.exists() and cached.stat().st_size > 1000 and not force_api:
        return cached

    body = {
        "text": spec.prompt,
        "model_id": MODEL_ID,
        "duration_seconds": max(0.5, spec.req_dur),
        "prompt_influence": spec.influence,
    }
    for attempt in range(3):
        resp = requests.post(
            API_URL,
            params={"output_format": "mp3_44100_128"},
            headers={"xi-api-key": api_key, "Content-Type": "application/json"},
            json=body,
            timeout=90,
        )
        if resp.status_code == 200 and resp.content:
            cached.write_bytes(resp.content)
            return cached
        if resp.status_code in (429, 500, 502, 503, 504) and attempt < 2:
            time.sleep(1.5 * (attempt + 1))
            continue
        raise RuntimeError(f"[{spec.name}] API {resp.status_code}: {resp.text[:240]}")
    raise RuntimeError(f"[{spec.name}] API 重试失败")


def master_foley(raw_mp3: pathlib.Path, spec: FoleySpec, out_mp3: pathlib.Path) -> Tuple[float, float, float, float]:
    """
    解码 -> 高通去直流 -> 主瞬态前沿对齐 -> 自然衰减收尾 -> 余弦淡出 -> 精准峰值定标 -> MP3 编码
    返回: (dur_s, peak_at_ms, rms_db, zcr_hz)
    """
    with tempfile.TemporaryDirectory() as td:
        wav_in = pathlib.Path(td) / "in.wav"
        subprocess.run(
            [
                "ffmpeg", "-y", "-v", "error",
                "-i", str(raw_mp3),
                "-af", f"highpass=f={spec.hp_hz:.1f},lowpass=f=13500",
                "-ac", "1", "-ar", str(SR),
                str(wav_in),
            ],
            check=True,
        )
        with wave.open(str(wav_in), "rb") as wf:
            n = wf.getnframes()
            samples = [s / 32768.0 for s in struct.unpack("<" + "h" * n, wf.readframes(n))]

        win = 220  # 5ms 窗口
        env = [max((abs(x) for x in samples[i:i + win]), default=0.0) for i in range(0, n, win)]
        peak = max(env, default=0.0)
        if peak < 1e-4:
            raise RuntimeError(f"[{spec.name}] 原始音频几乎静音 (peak={peak:.6f})")

        # 找首个进入主瞬态的窗口
        k_strong = next((k for k, v in enumerate(env) if v >= peak * spec.onset_ratio), 0)
        max_back = max(1, int(round(spec.pre_roll_ms / 5.0)))
        k_start = k_strong
        while k_start > max(0, k_strong - max_back) and env[k_start - 1] > peak * 0.035:
            k_start -= 1

        s_idx = max(0, k_start * win - int(0.003 * SR))

        # 从起点往后找有效能量末端 (-37 dB 相对主峰)，受限于 [min_dur, max_dur]
        max_len = int(round(spec.max_dur * SR))
        min_len = int(round(spec.min_dur * SR))
        max_win = min(len(env), k_start + int(math.ceil(spec.max_dur / 0.005)))
        k_end = k_strong
        for k in range(k_strong, max_win):
            if env[k] >= peak * 0.014:
                k_end = k

        e_idx = min(n, max(s_idx + min_len, min(s_idx + max_len, (k_end + 3) * win)))
        clip = samples[s_idx:e_idx]
        if len(clip) < int(0.035 * SR):
            clip = samples[:min(n, max_len)]

        # 1.2ms 微淡入 + 平滑余弦淡出（防止硬截断咔哒声）
        fi = min(int(0.0012 * SR), len(clip) // 6)
        for i in range(fi):
            clip[i] *= i / max(1, fi)
        fo = min(max(int(0.016 * SR), int(len(clip) * 0.18)), int(0.045 * SR), len(clip) // 2)
        for i in range(fo):
            clip[-1 - i] *= 0.5 * (1.0 - math.cos(math.pi * i / max(1, fo)))

        # 若为燕青机巧三连弩（crossbow），将单发真实弩矢按游戏内 60ms 连发节拍叠成颗粒分明的三连珠（咔咻·咔咻·咔咻！）
        if spec.name == "crossbow":
            s_len = min(len(clip), int(0.085 * SR))
            single = [clip[i] * math.exp(-i / (0.032 * SR)) for i in range(s_len)]
            burst_len = int(0.132 * SR) + min(len(clip), int(0.145 * SR))
            burst = [0.0] * burst_len
            # 前两发干脆收束留出清晰气口，第三发带完整箭羽破风余尾
            shots_cfg = [
                (0.000, 0.85, 0.97, single),
                (0.064, 0.92, 1.03, single),
                (0.128, 1.00, 1.09, clip[:min(len(clip), int(0.145 * SR))]),
            ]
            for t_off, amp_g, rate, src_arr in shots_cfg:
                base = int(t_off * SR)
                out_n = int(len(src_arr) / rate)
                for j in range(out_n):
                    if base + j >= burst_len:
                        break
                    src_p = j * rate
                    i0 = int(src_p)
                    fr = src_p - i0
                    v0 = src_arr[i0] if i0 < len(src_arr) else 0.0
                    v1 = src_arr[i0 + 1] if i0 + 1 < len(src_arr) else v0
                    burst[base + j] += (v0 + (v1 - v0) * fr) * amp_g
            clip = burst

        # 自适应波峰因数软限幅（消除单根微秒级针尖脉冲压低整体响度的问题，保证手机外放饱满扎实）
        c_peak = max((abs(x) for x in clip), default=1e-6)
        raw_rms = math.sqrt(sum(x * x for x in clip) / len(clip))
        crest_db = 20.0 * math.log10(max(c_peak / max(raw_rms, 1e-6), 1.0))
        # 正常物理拟音波峰因数在 11~14.5dB；若超过 14.5dB 说明存在窄尖刺，自动加大软膝盖驱动托起主体
        extra_drive = max(0.0, min(1.85, (crest_db - 14.2) * 0.24))
        drive = 1.18 + extra_drive
        target_lin = 10.0 ** (spec.peak_db / 20.0)
        norm_t = 1.0 / math.tanh(drive)
        inv_p = 1.0 / c_peak
        clip = [math.tanh((x * inv_p) * drive) * norm_t * target_lin for x in clip]

        # 统计声学质检指标
        dur_s = len(clip) / SR
        peak_idx = max(range(len(clip)), key=lambda idx: abs(clip[idx]))
        peak_at_ms = peak_idx / SR * 1000.0
        rms = math.sqrt(sum(x * x for x in clip) / len(clip))
        rms_db = 20.0 * math.log10(max(rms, 1e-6))
        zc = sum(1 for i in range(1, len(clip)) if (clip[i - 1] >= 0) != (clip[i] >= 0))
        zcr_hz = zc / (2.0 * max(dur_s, 1e-3))

        wav_out = pathlib.Path(td) / "out.wav"
        pcm = struct.pack("<" + "h" * len(clip), *(max(-32767, min(32767, int(round(x * 32767.0)))) for x in clip))
        with wave.open(str(wav_out), "wb") as wf:
            wf.setnchannels(1)
            wf.setsampwidth(2)
            wf.setframerate(SR)
            wf.writeframes(pcm)

        out_mp3.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(
            [
                "ffmpeg", "-y", "-v", "error",
                "-i", str(wav_out),
                "-c:a", "libmp3lame", "-b:a", "96k",
                "-ar", str(SR), "-ac", "1",
                str(out_mp3),
            ],
            check=True,
        )
        sync_meta(out_mp3)
        return dur_s, peak_at_ms, rms_db, zcr_hz


def sync_meta(mp3_path: pathlib.Path) -> None:
    meta_path = pathlib.Path(str(mp3_path) + ".meta")
    name = mp3_path.name
    if meta_path.exists():
        txt = meta_path.read_text(encoding="utf-8")
        m = re.search(r"^guid:\s*(.+)$", txt, flags=re.MULTILINE)
        guid = m.group(1).strip() if m else hashlib.md5(f"liangshan_sfx:{name}".encode()).hexdigest()
    else:
        guid = hashlib.md5(f"liangshan_sfx:{name}".encode()).hexdigest()

    meta_path.write_text(
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


def main() -> int:
    only_arg = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("-") else None
    force_api = "--force-api" in sys.argv
    wanted = {s.strip() for s in only_arg.split(",") if s.strip()} if only_arg else None

    # 复用刚才已验证满分的 raw_bow.mp3
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    seed_bow = pathlib.Path("/tmp/raw_bow.mp3")
    if seed_bow.exists() and not (CACHE_DIR / "bow.mp3").exists():
        shutil.copy2(seed_bow, CACHE_DIR / "bow.mp3")

    targets = [s for s in SPECS if wanted is None or s.name in wanted]
    api_key = load_api_key()
    print(f"[gen_real_sfx] 准备生成并母带定标 {len(targets)} 枚真实物理拟音 -> {OUT_DIR}")

    t0 = time.time()
    results = {}
    errors = []

    def worker(spec: FoleySpec):
        raw_p = fetch_raw_mp3(spec, api_key, force_api=force_api)
        out_p = OUT_DIR / f"{spec.name}.mp3"
        metrics = master_foley(raw_p, spec, out_p)
        return spec.name, out_p.stat().st_size, metrics

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        futs = {pool.submit(worker, s): s for s in targets}
        for idx, fut in enumerate(concurrent.futures.as_completed(futs), 1):
            sp = futs[fut]
            try:
                name, sz, (dur, p_ms, rms_db, zcr) = fut.result()
                results[name] = (sz, dur, p_ms, rms_db, zcr)
                print(
                    f"  [{idx:02d}/{len(targets):02d}] {name:<15} | "
                    f"dur={dur:5.2f}s | peak@{p_ms:5.0f}ms | RMS={rms_db:5.1f}dB | ZCR={zcr:5.0f}Hz | {sz/1024:4.1f}KB"
                )
            except Exception as exc:
                errors.append((sp.name, str(exc)))
                print(f"  [FAIL] {sp.name}: {exc}", file=sys.stderr)

    print(f"\n[gen_real_sfx] 完成 {len(results)}/{len(targets)} 枚，耗时 {time.time() - t0:.1f}s")
    if errors:
        for nm, msg in errors:
            print(f"  ERROR {nm}: {msg}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
