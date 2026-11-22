"""生成对标《永远的蔚蓝星球》主公剑气的全套高精度动态特效贴图（2x 超采样抗锯齿，零底色残留）：
1. fx_qi_dome_body.png    (512x512) : 饱满抛物线弹头流光瀑布主体（亮青 -> 蔚蓝 -> 皇家靛紫，内含纵向梳状流光槽，供 FxUber 实时 UV 流动与尾部噪波溶解）
2. fx_qi_dome_streaks.png (512x512) : 高速视差电光流丝层（Additive 叠加，以 1.7 倍速与主体错速流动形成真实内部湍流）
3. fx_qi_dome_sonic.png   (512x512) : 外圈暖金破空气浪弓弧 + 尾部紫电横裂隙（冷暖撞色，亮石板地面高对比）
4. fx_qi_crest_sheet.png  (2048x512): 4帧手绘感赛璐珞白金锯齿浪冠 + 正中天罡青钻印记（◇）逐帧跳动图集
5. fx_qi_flare_v.png      (256x256) : 修长十字星芒（竖放作拖尾星尘，横放作受击白炽镜头十字闪）
6. fx_qi_slash_red.png    (256x256) : 绯红弯月破甲斩痕（还原受击怪身上的红色弯月刀痕）
"""
import os
import numpy as np
from PIL import Image, ImageFilter

ART = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art")


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / np.maximum(1e-6, e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def downsample(rgba_float, out_w, out_h):
    """预乘 Alpha 2x 超采样下采样，保证边缘纯净锐利无黑边。"""
    arr = np.clip(rgba_float, 0.0, 1.0)
    pm = np.concatenate([arr[..., :3] * arr[..., 3:4], arr[..., 3:4]], axis=-1)
    im = Image.fromarray(np.round(pm * 255.0).astype(np.uint8)).resize((out_w, out_h), Image.LANCZOS)
    rs = np.asarray(im, dtype=np.float32) / 255.0
    a = rs[..., 3:4]
    rgb = np.where(a > 1e-3, np.clip(rs[..., :3] / np.maximum(a, 1e-3), 0.0, 1.0), 0.0)
    a_clean = np.where(a < 0.008, 0.0, a)
    out = np.concatenate([rgb, a_clean], axis=-1)
    return Image.fromarray(np.round(out * 255.0).astype(np.uint8))


def make_dome_body_and_streaks():
    N = 1024  # 2x supersample -> 512x512
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / (N - 1.0)
    dx = xx - 0.5

    # 饱满抛物线弹头轮廓：在 y=0.095 处为圆润顶点，两侧顺滑收拢成流线水滴尾
    half_w = np.clip(0.355 - 0.055 * np.maximum(0.0, yy - 0.36) ** 1.25, 0.12, 0.36)
    xn = dx / half_w
    arch_y = 0.095 + 0.34 * (xn ** 2) + 0.06 * (xn ** 4)
    depth = yy - arch_y  # >0 表示在弹头锋刃后方

    inside_w = 1.0 - smoothstep(0.82, 1.03, np.abs(xn))
    front_in = smoothstep(-0.006, 0.020, depth)

    # 梳状纵向瀑布流光纹（多频叠加，让拖尾像高速梳开的能量瀑布，而非平涂色块）
    ang_warp = dx * (1.0 + 0.32 * yy)
    comb1 = 0.5 + 0.5 * np.sin(ang_warp * 78.0 + np.sin(yy * 9.0) * 0.5)
    comb2 = 0.5 + 0.5 * np.sin(ang_warp * 154.0 - 1.1)
    comb3 = 0.5 + 0.5 * np.cos(ang_warp * 38.0 + 0.4)
    ribbons = 0.52 * comb1 + 0.28 * comb2 + 0.20 * comb3

    # 尾部参差梳状消散：不同流道长度错落有致（匹配参考图底部一缕缕垂落的蓝紫流光）
    tail_len = 0.64 + 0.17 * comb3 + 0.09 * comb1 - 0.16 * (xn ** 2)
    tail_fade = 1.0 - smoothstep(tail_len * 0.48, tail_len, depth)

    # 四色阶高饱和过渡：白青锋 -> 亮电青 -> 宝石蔚蓝 -> 皇家靛紫
    c_ice = np.array([0.74, 0.99, 1.00], dtype=np.float32)
    c_cyan = np.array([0.04, 0.88, 1.00], dtype=np.float32)
    c_blue = np.array([0.05, 0.46, 0.98], dtype=np.float32)
    c_indigo = np.array([0.28, 0.18, 0.90], dtype=np.float32)
    c_violet = np.array([0.52, 0.18, 0.98], dtype=np.float32)

    k1 = smoothstep(0.00, 0.09, depth)[..., None]
    k2 = smoothstep(0.08, 0.30, depth)[..., None]
    k3 = smoothstep(0.28, 0.56, depth)[..., None]
    k4 = smoothstep(0.52, 0.80, depth)[..., None]

    col = c_ice * (1.0 - k1) + c_cyan * k1
    col = col * (1.0 - k2) + c_blue * k2
    col = col * (1.0 - k3) + c_indigo * k3
    col = col * (1.0 - k4) + c_violet * k4

    # 梳状亮纹提亮为电青白，暗槽沉入深海蓝，形成极强立体体积感
    streak_boost = (ribbons ** 1.7) * (1.0 - smoothstep(0.0, 0.62, depth)) * 0.52
    col = np.clip(col * (0.80 + 0.42 * ribbons[..., None]) + c_cyan * streak_boost[..., None] * 0.55, 0.0, 1.0)

    # 头部实、中腰通透（约0.78，可隐约透出地面石砖纹理）、尾部羽化归零
    core_op = np.Lerp = 0.95 - 0.24 * smoothstep(0.10, 0.45, depth)
    alpha = front_in * inside_w * tail_fade * core_op * (0.76 + 0.24 * ribbons)
    alpha = np.clip(alpha, 0.0, 0.96)

    body_rgba = np.dstack([col, alpha])
    downsample(body_rgba, 512, 512).save(os.path.join(ART, "fx_qi_dome_body.png"))

    # 第二张：高速视差电光流丝层（Additive 专用）
    fine1 = np.maximum(0.0, np.sin(ang_warp * 112.0 + 0.7)) ** 4.0
    fine2 = np.maximum(0.0, np.sin(ang_warp * 210.0 - 0.9)) ** 6.0
    filament = np.clip(fine1 * 0.72 + fine2 * 0.65, 0.0, 1.0)
    s_fade = smoothstep(0.01, 0.05, depth) * (1.0 - smoothstep(0.18, 0.66, depth)) * (1.0 - smoothstep(0.72, 0.98, np.abs(xn)))
    s_alpha = np.clip(filament * s_fade * 0.88, 0.0, 1.0)
    s_col = np.outer(np.ones(N * N, np.float32), np.array([0.48, 0.96, 1.0], np.float32)).reshape(N, N, 3)
    s_col = np.clip(s_col + (fine2 * s_fade)[..., None] * 0.45, 0.0, 1.0)
    downsample(np.dstack([s_col, s_alpha]), 512, 512).save(os.path.join(ART, "fx_qi_dome_streaks.png"))


def make_dome_sonic():
    """外圈暖金破空气浪弓弧 + 尾部亮紫横裂隙。"""
    N = 1024
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / (N - 1.0)
    dx = xx - 0.5

    # 比蓝主体略宽一圈的马赫激波外弧
    xn = dx / 0.435
    bow_y = 0.068 + 0.39 * (xn ** 2) + 0.05 * (xn ** 4)
    d_bow = yy - bow_y

    # 激波前缘锐利、向后柔和拖开，两侧翼弧最明显（匹配参考图左右两侧暖橙金破空气浪）
    ring = smoothstep(-0.012, 0.004, d_bow) * (1.0 - smoothstep(0.008, 0.085 + 0.06 * (xn ** 2), d_bow))
    wing_span = (1.0 - smoothstep(0.86, 1.06, np.abs(xn))) * (1.0 - smoothstep(0.36, 0.62, yy))
    # 再叠加第二道更淡的外扩气浪细线
    d_bow2 = yy - (0.048 + 0.43 * (xn ** 2))
    ring2 = smoothstep(-0.008, 0.003, d_bow2) * (1.0 - smoothstep(0.004, 0.036, d_bow2)) * (0.35 + 0.65 * np.abs(xn)) * (1.0 - smoothstep(0.30, 0.52, yy)) * (1.0 - smoothstep(0.92, 1.08, np.abs(xn)))

    gold_a = np.clip(ring * wing_span * 0.68 + ring2 * 0.42, 0.0, 0.85)
    c_gold_hi = np.array([1.00, 0.93, 0.70], dtype=np.float32)
    c_gold_lo = np.array([1.00, 0.66, 0.28], dtype=np.float32)
    t_wing = np.clip(np.abs(xn), 0.0, 1.0)[..., None]
    gold_col = c_gold_hi * (1.0 - t_wing * 0.65) + c_gold_lo * (t_wing * 0.65)

    # 尾部亮紫横裂隙光梭（参考图底部正中偏右那道横置亮紫能量缝！）
    rx = (xx - 0.53) / 0.085
    ry = (yy - 0.72) / 0.010
    rift = np.exp(-(rx * rx + ry * ry)) * (1.0 - smoothstep(0.85, 1.3, np.abs(rx)))
    rift_halo = np.exp(-((xx - 0.53) ** 2 / (0.13 ** 2) + (yy - 0.72) ** 2 / (0.024 ** 2))) * 0.45
    rift_a = np.clip(rift + rift_halo, 0.0, 0.95)
    rift_col = np.array([0.68, 0.22, 1.00], dtype=np.float32) * (1.0 - rift[..., None] * 0.55) + np.array([0.96, 0.82, 1.00], dtype=np.float32) * (rift[..., None] * 0.55)

    out_a = np.clip(gold_a + rift_a, 0.0, 1.0)
    w_rift = (rift_a / np.maximum(1e-5, gold_a + rift_a))[..., None]
    out_col = gold_col * (1.0 - w_rift) + rift_col * w_rift
    downsample(np.dstack([out_col, out_a]), 512, 512).save(os.path.join(ART, "fx_qi_dome_sonic.png"))


def make_crest_sheet():
    """2048x512 四帧逐帧跃动的白金皇冠锯齿锋刃 + 正中天罡青钻印记（◇）。"""
    N = 1024
    frames = []
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / (N - 1.0)
    dx = xx - 0.5
    xn = dx / 0.355
    arch_y = 0.090 + 0.345 * (xn ** 2) + 0.06 * (xn ** 4)

    for fi in range(4):
        phase = fi * (np.pi * 0.5)
        # 皇冠前缘：光滑抛物线顶弧；后缘：正中大主尖齿（托起青钻）+ 两侧灵动跳动的赛璐珞火焰锯齿！
        center_tooth = 0.056 * np.exp(-((dx / 0.058) ** 2))
        # 两侧对称但不死板的逐帧跃动浪齿
        side_wave1 = 0.026 * np.abs(np.cos(xn * np.pi * 4.5 + phase * 0.65 * np.sign(dx))) * smoothstep(0.08, 0.24, np.abs(xn))
        side_wave2 = 0.015 * np.sin(xn * np.pi * 9.0 - phase * 1.2) * smoothstep(0.12, 0.35, np.abs(xn))
        pulse = 1.0 + 0.06 * np.sin(phase)
        thick = (0.042 * (1.0 - 0.52 * (xn ** 2)) + center_tooth * pulse + side_wave1 + side_wave2) * (1.0 - smoothstep(0.84, 1.02, np.abs(xn)))

        d = yy - arch_y
        # 外侧柔金辉光晕
        outer_glow = np.exp(-((d - thick * 0.35) ** 2) / (0.026 ** 2)) * (1.0 - smoothstep(0.80, 1.04, np.abs(xn))) * 0.52
        # 赛璐珞锐利冠体
        in_crest = smoothstep(-0.005, 0.004, d) * (1.0 - smoothstep(thick - 0.006, thick + 0.005, d)) * (1.0 - smoothstep(0.88, 1.01, np.abs(xn)))
        # 紧贴锯齿下沿的一圈亮电青咬合边（还原参考图金白浪冠与青蓝主体的清澈交界）
        cyan_lip = smoothstep(thick - 0.008, thick + 0.002, d) * (1.0 - smoothstep(thick + 0.006, thick + 0.028, d)) * (1.0 - smoothstep(0.84, 0.99, np.abs(xn)))

        c_ivory = np.array([1.00, 0.99, 0.90], dtype=np.float32)
        c_gold = np.array([1.00, 0.86, 0.46], dtype=np.float32)
        c_cyan = np.array([0.10, 0.95, 1.00], dtype=np.float32)

        # 冠体边缘暖金、核心炽白
        rim_k = np.clip(smoothstep(0.0, 0.012, d) * (1.0 - smoothstep(thick - 0.016, thick, d)), 0.0, 1.0)[..., None]
        crest_col = c_gold * (1.0 - rim_k * 0.78) + c_ivory * (rim_k * 0.78)

        # 正中皇冠菱形天罡青钻印记（◇）：外框冰青蓝、内芯亮天青高光（1:1 还原参考图顶冠正中的菱形纹章！）
        dy_d = (yy - 0.124) / (0.025 * (1.0 + 0.05 * np.sin(phase)))
        dx_d = dx / (0.015 * (1.0 + 0.05 * np.sin(phase)))
        manhattan = np.abs(dx_d) + np.abs(dy_d)
        dia_outer = 1.0 - smoothstep(0.92, 1.08, manhattan)
        dia_inner = 1.0 - smoothstep(0.42, 0.58, manhattan)
        dia_ring = np.clip(dia_outer - dia_inner * 0.78, 0.0, 1.0)

        c_dia_ring = np.array([0.08, 0.78, 0.98], dtype=np.float32)
        c_dia_core = np.array([0.78, 0.99, 1.00], dtype=np.float32)
        dia_col = c_dia_ring * (1.0 - dia_inner[..., None]) + c_dia_core * dia_inner[..., None]

        # 合成单帧
        col = c_gold * outer_glow[..., None]
        a = outer_glow.copy()

        # 叠青唇边
        a_lip = cyan_lip * 0.92
        col = col * (1.0 - a_lip[..., None]) + c_cyan * a_lip[..., None]
        a = a + a_lip * (1.0 - a)

        # 叠白金锯齿冠体
        col = col * (1.0 - in_crest[..., None]) + crest_col * in_crest[..., None]
        a = a + in_crest * (1.0 - a)

        # 镶嵌正中菱形青钻印记
        col = col * (1.0 - dia_outer[..., None] * 0.95) + dia_col * (dia_outer[..., None] * 0.95)
        a = np.clip(np.maximum(a, dia_outer), 0.0, 1.0)

        # 还原非预乘色供 downsample 使用
        rgb_unpm = np.where(a[..., None] > 1e-4, np.clip(col / np.maximum(a[..., None], 1e-4), 0.0, 1.0), 0.0)
        # 对冠体内部直接使用纯色避免光晕除暗
        rgb_final = np.where(in_crest[..., None] > 0.5, crest_col * (1.0 - dia_outer[..., None] * 0.92) + dia_col * (dia_outer[..., None] * 0.92), rgb_unpm)
        frames.append(downsample(np.dstack([rgb_final, a]), 512, 512))

    sheet = Image.new("RGBA", (512 * 4, 512), (0, 0, 0, 0))
    for i, im in enumerate(frames):
        sheet.alpha_composite(im, (i * 512, 0))
    sheet.save(os.path.join(ART, "fx_qi_crest_sheet.png"))


def make_flare_and_red_slash():
    """修长十字镜头星芒 fx_qi_flare_v.png + 绯红弯月斩痕 fx_qi_slash_red.png。"""
    N = 512
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / (N - 1.0)
    dx = np.abs(xx - 0.5)
    dy = np.abs(yy - 0.5)

    # 修长竖向主芒 + 短锐横向副芒 + 中心菱形亮核（旋转 90° 即为参考图受击怪身上的横向白炽镜头闪！）
    v_spike = np.exp(-(dx / 0.016) ** 1.6) * np.maximum(0.0, 1.0 - dy / 0.47) ** 1.8
    h_spike = np.exp(-(dy / 0.015) ** 1.6) * np.maximum(0.0, 1.0 - dx / 0.24) ** 2.0
    core = np.exp(-((dx / 0.065) ** 2 + (dy / 0.085) ** 2))
    halo = np.exp(-((dx / 0.14) ** 2 + (dy / 0.22) ** 2)) * 0.36
    a = np.clip(v_spike + h_spike * 0.88 + core * 0.95 + halo, 0.0, 1.0)
    whiteness = np.clip(v_spike * 0.8 + h_spike * 0.8 + core, 0.0, 1.0)[..., None]
    col = np.array([0.38, 0.92, 1.00], np.float32) * (1.0 - whiteness) + np.array([1.0, 1.0, 1.0], np.float32) * whiteness
    downsample(np.dstack([col, a]), 256, 256).save(os.path.join(ART, "fx_qi_flare_v.png"))

    # 绯红弯月破甲斩痕（匹配参考图哥布林左侧那道鲜红凌厉弯月刀光！）
    sx = (xx - 0.5) * 2.0
    sy = (yy - 0.5) * 2.0
    r = np.sqrt(sx * sx + (sy + 0.55) ** 2)
    ang = np.arctan2(sy + 0.55, sx) - np.pi * 0.5  # 0 at top center
    span = 1.0 - smoothstep(0.55, 0.88, np.abs(ang))
    taper = np.maximum(0.0, 1.0 - (ang / 0.86) ** 2)
    r_outer = 0.92
    r_inner = 0.92 - 0.24 * taper
    slash = smoothstep(r_inner - 0.02, r_inner + 0.02, r) * (1.0 - smoothstep(r_outer - 0.015, r_outer + 0.015, r)) * span
    hot_edge = smoothstep(r_outer - 0.06, r_outer - 0.005, r) * slash
    s_col = np.array([0.98, 0.12, 0.22], np.float32) * (1.0 - hot_edge[..., None] * 0.65) + np.array([1.0, 0.72, 0.76], np.float32) * (hot_edge[..., None] * 0.65)
    downsample(np.dstack([s_col, np.clip(slash * 0.96, 0.0, 1.0)]), 256, 256).save(os.path.join(ART, "fx_qi_slash_red.png"))


if __name__ == "__main__":
    make_dome_body_and_streaks()
    make_dome_sonic()
    make_crest_sheet()
    make_flare_and_red_slash()
    print("GENERATED_QI_VFX_OK")
