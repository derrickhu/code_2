"""构建主页 v8 和谐宋韵青白石舞台与克制内敛 UI 套件。

核心美学准则（60-30-10 视觉秩序）：
1. 石材色温全局统一：将前景城台（home_deck.png）与四连点将石台（h_podium4.png）从偏冷紫钢蓝校准至与远景城墙/石阶（home_road.jpg）完全同源的暖灰青白石色温；将点将台顶面刺眼的科幻霓虹青光圈柔化为温润低饱和宋代青玉石刻凹盘。
2. 点将台前壁「战力 + 羁绊」克制内嵌：抹平前壁中央杂乱竖向砖缝，雕出与石台同材质的原石浅凹横槽（h_podium_groove.png），配低调半透明青灰玉石签（h_bond_tag.png）与温润双环玉扣小标（ic_bond_knot.png），零黑底金框、零抢戏黄标，完全融入石台结构层。
3. 三宝箱承托槽同质化（h_chest_mat.png）：去掉突兀的大红黄边地毯，改为沉入桥面的同色系浅凹青石承盘。
4. 全场唯一鎏金焦点「开始挑战」（h_start_hex.png）+ 青白石切关三角箭头（h_stage_arrow.png）：切关箭头改用与桥面和顶底栏呼应的冷白青石银边质感，把全场唯一的鎏金视觉重心留给「开始挑战」。
"""
import os
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART_DIR = os.path.join(ROOT, "Assets/Resources/Art")
UI_DIR = os.path.join(ART_DIR, "ui")
NOBG_DIR = "/Users/huyi/dk_proj/game_assets/code_2/assets/nobg/home_v6"
FINAL_DIR = "/Users/huyi/dk_proj/game_assets/code_2/assets/final/home_v6"

SS = 4


def rgb(h: str) -> np.ndarray:
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)


def defringe_white(im: Image.Image, strength: float = 0.85) -> Image.Image:
    arr = np.array(im, dtype=np.float32)
    rgb_c = arr[:, :, :3]
    a = arr[:, :, 3:4] / 255.0
    edge = (a > 0.02) & (a < 0.96)
    unmixed = np.clip((rgb_c - (1.0 - a) * 250.0 * strength) / np.maximum(a, 0.12), 0.0, 255.0)
    rgb_out = np.where(edge, unmixed, rgb_c)
    a_clean = np.clip((a - 0.05) / 0.95, 0.0, 1.0)
    out = np.zeros_like(arr, dtype=np.uint8)
    out[:, :, :3] = np.round(rgb_out).astype(np.uint8)
    out[:, :, 3] = np.round(a_clean[:, :, 0] * 255.0).astype(np.uint8)
    return Image.fromarray(out)


def harmonize_deck_stone(im: Image.Image) -> Image.Image:
    """将偏冷钢蓝的城台石面校准至与 home_road 城墙/石路一致的宋韵暖灰青白石色温，并做上暖下沉的自然天光过渡。"""
    arr = np.array(im, dtype=np.float32)
    c = arr[:, :, :3]
    a = arr[:, :, 3]
    h, w = a.shape
    yn = np.linspace(0.0, 1.0, h, dtype=np.float32)[:, None, None]

    lum = 0.299 * c[:, :, 0] + 0.587 * c[:, :, 1] + 0.114 * c[:, :, 2]
    # 压低原本过强的冷紫蓝偏色（保留约 32% 原始微妙冷暖笔触变化，避免死灰）
    neutral_dev = c - lum[:, :, None]
    c_calm = lum[:, :, None] + neutral_dev * 0.34

    # 注入梁山青白石温润色调：上沿衔接阳光石路略偏暖米灰 (+7, +4, -5)，向下渐入城台青石微荫 (+1, +2, +4)
    warm_top = np.array([9.0, 5.5, -5.5], dtype=np.float32)[None, None, :]
    cool_bot = np.array([1.5, 2.5, 4.0], dtype=np.float32)[None, None, :]
    tint = warm_top * (1.0 - yn) + cool_bot * yn

    # 整体明度微提 3% 衔接石路阳光感
    c_out = np.clip(c_calm * 1.03 + tint, 0.0, 255.0)
    out = np.zeros_like(arr, dtype=np.uint8)
    out[:, :, :3] = np.round(c_out).astype(np.uint8)
    out[:, :, 3] = np.round(a).astype(np.uint8)
    return Image.fromarray(out)


def harmonize_podium_stone(im: Image.Image) -> Image.Image:
    """
    1) 将点将石台校准至与前景城台同源的沉稳青灰石色温；
    2) 将刺眼的科幻霓虹青光圈柔化为古朴温润的低饱和宋代青玉石刻圆环；
    3) 将前壁中央竖向碎石缝平滑收整为一块完整连贯的青石匾额面，供战力与羁绊自然镌刻其上。
    """
    arr = np.array(im, dtype=np.float32)
    c = arr[:, :, :3]
    a = arr[:, :, 3]
    h, w = a.shape

    # 识别原本高饱和霓虹青光圈像素 (B,G 远高于 R)
    neon = np.clip(((c[:, :, 2] - c[:, :, 0]) * 0.6 + (c[:, :, 1] - c[:, :, 0]) * 0.5 - 26.0) / 75.0, 0.0, 1.0)

    lum = 0.299 * c[:, :, 0] + 0.587 * c[:, :, 1] + 0.114 * c[:, :, 2]
    stone_base = lum[:, :, None] + (c - lum[:, :, None]) * 0.28 + np.array([4.5, 4.0, -1.5], dtype=np.float32)[None, None, :]

    # 温润青玉色（低饱和灰青玉，不再刺眼发光抢角色焦点）
    jade_col = lum[:, :, None] * 0.80 + np.array([18.0, 42.0, 44.0], dtype=np.float32)[None, None, :]
    c_toned = stone_base * (1.0 - neon[:, :, None] * 0.82) + jade_col * (neon[:, :, None] * 0.82)

    # 平滑收整前壁中央竖缝区域（y: 59%..88%, x: 16%..84%），消除贴字背后的碎裂竖黑缝
    y_idx, x_idx = np.mgrid[0:h, 0:w].astype(np.float32)
    yn = y_idx / max(1, h - 1)
    xn = x_idx / max(1, w - 1)
    apron_mask = (
        np.clip((yn - 0.59) / 0.03, 0.0, 1.0)
        * np.clip((0.875 - yn) / 0.03, 0.0, 1.0)
        * np.clip((xn - 0.15) / 0.05, 0.0, 1.0)
        * np.clip((0.85 - xn) / 0.05, 0.0, 1.0)
        * (a > 200).astype(np.float32)
    )
    # 用水平中值平滑石壁底色取代竖向黑缝
    row_med = np.percentile(c_toned[:, int(w * 0.20):int(w * 0.80), :], 58, axis=1, keepdims=True)
    row_med = np.broadcast_to(row_med, c_toned.shape)
    c_clean = c_toned * (1.0 - apron_mask[:, :, None] * 0.86) + row_med * (apron_mask[:, :, None] * 0.86)

    out = np.zeros_like(arr, dtype=np.uint8)
    out[:, :, :3] = np.clip(np.round(c_clean), 0, 255).astype(np.uint8)
    out[:, :, 3] = np.round(a).astype(np.uint8)
    return Image.fromarray(out)


def sdf_convex_poly(w: int, h: int, verts) -> np.ndarray:
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    pts = np.array(verts, dtype=np.float32)
    n = len(pts)
    max_half = np.full((h, w), -1e9, dtype=np.float32)
    min_seg = np.full((h, w), 1e9, dtype=np.float32)
    for i in range(n):
        ax, ay = pts[i]
        bx, by = pts[(i + 1) % n]
        ex, ey = bx - ax, by - ay
        el = float(np.hypot(ex, ey)) + 1e-6
        nx, ny = ey / el, -ex / el
        signed_plane = (x - ax) * nx + (y - ay) * ny
        max_half = np.maximum(max_half, signed_plane)
        t = np.clip(((x - ax) * ex + (y - ay) * ey) / (el * el), 0.0, 1.0)
        d_seg = np.hypot(x - (ax + t * ex), y - (ay + t * ey))
        min_seg = np.minimum(min_seg, d_seg)
    return np.where(max_half > 0.0, min_seg, max_half)


def hex_plaque_sdf(w: int, h: int, pad_x: float, pad_y: float, tip_dx: float, round_r: float) -> np.ndarray:
    L = pad_x + round_r
    R = (w - 1) - pad_x - round_r
    T = pad_y + round_r
    B = (h - 1) - pad_y - round_r
    my = (h - 1) * 0.5
    verts = [
        (L + tip_dx, T),
        (R - tip_dx, T),
        (R, my),
        (R - tip_dx, B),
        (L + tip_dx, B),
        (L, my),
    ]
    return sdf_convex_poly(w, h, verts) - round_r


def process_stage_art():
    os.makedirs(FINAL_DIR, exist_ok=True)

    # 1. home_deck.png
    deck = defringe_white(Image.open(os.path.join(NOBG_DIR, "home_deck.png")).convert("RGBA"))
    deck = harmonize_deck_stone(deck)
    da = np.array(deck)[:, :, 3]
    ys = np.where(da > 16)[0]
    y0 = max(0, int(ys.min()) - 6)
    deck_crop = deck.crop((0, y0, deck.width, deck.height))
    target_w = 1080
    target_h = int(round(deck_crop.height * target_w / deck_crop.width))
    deck_final = deck_crop.resize((target_w, target_h), Image.LANCZOS)
    deck_final.save(os.path.join(FINAL_DIR, "home_deck.png"))
    deck_final.save(os.path.join(ART_DIR, "home_deck.png"))
    print(f"Saved harmonized home_deck.png: {deck_final.size}")

    # 2. h_podium4.png
    pod_raw = defringe_white(Image.open(os.path.join(NOBG_DIR, "h_podium4.png")).convert("RGBA"))
    pa = np.array(pod_raw)
    alpha = pa[:, :, 3]
    ys, xs = np.where(alpha > 24)
    x0, x1 = int(xs.min()), int(xs.max())
    y0, y1 = int(ys.min()), int(ys.max())

    rgb_f = pa[:, :, :3].astype(np.float32)
    cyan = (rgb_f[:, :, 2] - rgb_f[:, :, 0]) + (rgb_f[:, :, 1] - rgb_f[:, :, 0]) * 0.7
    cyan[alpha < 180] = 0
    cys, cxs = np.where(cyan > np.percentile(cyan[alpha > 180], 95))
    slot_cxs = []
    for k in range(4):
        qx0 = x0 + (x1 - x0) * k / 4.0
        qx1 = x0 + (x1 - x0) * (k + 1) / 4.0
        m = (cxs >= qx0) & (cxs < qx1)
        slot_cxs.append(float(cxs[m].mean()))
    true_cx = 0.5 * (slot_cxs[1] + slot_cxs[2])
    half_w = int(np.ceil(max(true_cx - x0, x1 - true_cx))) + 14
    cx_i = int(round(true_cx))
    crop_box = (max(0, cx_i - half_w), max(0, y0 - 10), min(pod_raw.width, cx_i + half_w), min(pod_raw.height, y1 + 18))
    pod_crop = harmonize_podium_stone(pod_raw.crop(crop_box))

    pw, ph = pod_crop.size
    sh_alpha = pod_crop.split()[3]
    sh_blur = sh_alpha.filter(ImageFilter.GaussianBlur(radius=7.0))
    sh_arr = np.array(sh_blur, dtype=np.float32) * 0.36
    canvas_arr = np.zeros((ph, pw, 4), dtype=np.uint8)
    canvas_arr[:, :, :3] = [24, 30, 38]
    canvas_arr[7:, :, 3] = np.clip(np.round(sh_arr[:-7, :]), 0, 255).astype(np.uint8)
    pod_with_sh = Image.fromarray(canvas_arr)
    pod_with_sh.alpha_composite(pod_crop, (0, 0))

    out_w = 760
    out_h = int(round(ph * out_w / pw))
    pod_final = pod_with_sh.resize((out_w, out_h), Image.LANCZOS)
    pod_final.save(os.path.join(FINAL_DIR, "h_podium4.png"))
    pod_final.save(os.path.join(UI_DIR, "h_podium4.png"))
    print(f"Saved harmonized h_podium4.png: {pod_final.size}")


def build_podium_crest_suite():
    """
    原石浅雕内嵌风格（克制、温润、零黑金抢戏）：
    - h_podium_crest.png (540x62, 9-slice 48,0,48,0)：与石台前壁同材质的浅凹石刻槽（上沿凿刻内阴、下沿青白石亮唇边，无金边无黑底）
    - h_bond_pill.png    (260x42, 9-slice 22,0,22,0)：低调温润的青瓷石青半透明微凸胶囊签，跟全局 h_capsule / 侧坞完全同调
    - h_bond_pill_lip.png(264x46, 9-slice 24,0,24,0)：极轻薄石青底沿
    - ic_bond_knot.png   (64x64)：素雅温润的青玉浅金双环纹
    - ic_bond_go.png     (40x40)：低调冷瓷浅青小箭头（不再用刺眼亮黄播放钮）
    """
    # --- 1) 原石浅凹石刻槽 h_podium_crest.png (540x62) ---
    bw, bh = 540, 62
    w, h = bw * SS, bh * SS
    pad_x, pad_y = 4.0 * SS, 4.0 * SS
    tip_dx = 12.0 * SS
    round_r = 10.0 * SS

    d = hex_plaque_sdf(w, h, pad_x, pad_y, tip_dx, round_r)
    alpha = np.clip(0.5 - d, 0.0, 1.0)
    d_in = d + 2.4 * SS
    m_in = np.clip(0.5 - d_in, 0.0, 1.0)

    y_idx, _ = np.mgrid[0:h, 0:w].astype(np.float32)
    yn = y_idx / (h - 1)
    tr = np.clip(yn, 0.0, 1.0)[:, :, None]

    # 外唇边：上沿深石缝色 (#414850)，下沿受光青白石亮沿 (#A4AAA8)，呈现真实凿入石壁的阴刻石槽！
    rim_col = rgb("3F464E") * (1.0 - tr) + rgb("A7ACA8") * tr

    # 槽内底面：沉稳深青灰石面 (#4A535C -> #56606A)，比前壁石面略深半调，稳稳托住浅色字又绝不发黑刺眼
    basin_col = rgb("46505A") * (1.0 - tr) + rgb("54606B") * tr
    # 顶部 3.5px 凿刻内凹阴影
    d_down = np.roll(d_in, int(round(3.4 * SS)), axis=0)
    d_down[:int(round(3.4 * SS)), :] = 999.0
    top_sh = np.clip(0.5 - d_in, 0.0, 1.0) * np.clip(d_down + 0.5, 0.0, 1.0) * np.clip(1.0 - yn * 2.2, 0.0, 1.0) * 0.46
    basin_col = basin_col * (1.0 - top_sh[:, :, None]) + rgb("2B333B")[None, None, :] * top_sh[:, :, None]

    col = rim_col * (1.0 - m_in[:, :, None]) + basin_col * m_in[:, :, None]
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[:, :, :3] = np.clip(np.round(col), 0, 255).astype(np.uint8)
    # 92% 不透明度让底层青石肌理自然透出少许，彻底融为一体
    rgba[:, :, 3] = np.clip(np.round(alpha * 236.0), 0, 255).astype(np.uint8)
    Image.fromarray(rgba).resize((bw, bh), Image.LANCZOS).save(os.path.join(UI_DIR, "h_podium_crest.png"))

    # --- 2) 温润宋瓷青灰胶囊签 h_bond_pill.png (260x42) + lip (264x46) ---
    pw, ph = 260, 42
    W2, H2 = pw * SS, ph * SS
    r2 = 18.5 * SS
    pad2 = 1.5 * SS
    st2 = 1.6 * SS
    rim2 = 1.5 * SS

    y2, x2 = np.mgrid[0:H2, 0:W2].astype(np.float32)
    cx2, cy2 = (W2 - 1) * 0.5, (H2 - 1) * 0.5
    hx2 = (W2 - 2.0 * pad2) * 0.5 - r2
    hy2 = (H2 - 2.0 * pad2) * 0.5 - r2
    d2 = np.hypot(np.maximum(np.abs(x2 - cx2) - hx2, 0.0), np.maximum(np.abs(y2 - cy2) - hy2, 0.0)) - r2
    a2 = np.clip(0.5 - d2, 0.0, 1.0)
    m2_rim = np.clip(0.5 - (d2 + st2), 0.0, 1.0)
    m2_in = np.clip(0.5 - (d2 + st2 + rim2), 0.0, 1.0)

    yn2 = y2 / (H2 - 1)
    t2 = np.clip(yn2, 0.0, 1.0)[:, :, None]
    # 柔和天青银瓷细边 + 沉稳霁青石内面（与顶栏关卡统领条 h_capsule 同一套色相，绝不跳脱）
    c_rim2 = rgb("B4D2E0") * (1.0 - t2) + rgb("6B8FA3") * t2
    c_in2 = rgb("35546B") * (1.0 - t2) + rgb("263E52") * t2
    gloss2 = np.clip((0.46 - yn2) * 8.0, 0.0, 1.0) * 0.10
    c_in2 = c_in2 + (255.0 - c_in2) * gloss2[:, :, None]
    fill2 = c_rim2 * (1.0 - m2_in[:, :, None]) + c_in2 * m2_in[:, :, None]
    col2 = rgb("1E2D3B")[None, None, :] * (1.0 - m2_rim[:, :, None]) + fill2 * m2_rim[:, :, None]

    p_rgba = np.zeros((H2, W2, 4), dtype=np.uint8)
    p_rgba[:, :, :3] = np.clip(np.round(col2), 0, 255).astype(np.uint8)
    p_rgba[:, :, 3] = np.clip(np.round(a2 * 242.0), 0, 255).astype(np.uint8)
    Image.fromarray(p_rgba).resize((pw, ph), Image.LANCZOS).save(os.path.join(UI_DIR, "h_bond_pill.png"))

    lpw, lph = pw + 4, ph + 4
    LW2, LH2 = lpw * SS, lph * SS
    ly2, lx2 = np.mgrid[0:LH2, 0:LW2].astype(np.float32)
    lhx2 = (pw - 3.0) * 0.5 * SS - r2
    lhy2 = (ph - 3.0) * 0.5 * SS - r2
    ld2 = np.hypot(np.maximum(np.abs(lx2 - (LW2 - 1) * 0.5) - lhx2, 0.0), np.maximum(np.abs(ly2 - (LH2 - 1) * 0.5) - lhy2, 0.0)) - r2
    la2 = np.clip(0.5 - ld2, 0.0, 1.0)
    lp_rgba = np.zeros((LH2, LW2, 4), dtype=np.uint8)
    lp_rgba[:, :, :3] = [24, 38, 50]
    lp_rgba[:, :, 3] = np.clip(np.round(la2 * 180.0), 0, 255).astype(np.uint8)
    Image.fromarray(lp_rgba).resize((lpw, lph), Image.LANCZOS).save(os.path.join(UI_DIR, "h_bond_pill_lip.png"))

    # --- 3) 素雅双环青玉扣 ic_bond_knot.png (64x64) ---
    ks = 64 * SS
    ky, kx = np.mgrid[0:ks, 0:ks].astype(np.float32)
    ku = (kx + 0.5) / ks * 2.0 - 1.0
    kv = (ky + 0.5) / ks * 2.0 - 1.0
    d_rg = np.abs(np.hypot(ku + 0.21, kv - 0.02) - 0.42) - 0.115
    d_rj = np.abs(np.hypot(ku - 0.21, kv + 0.02) - 0.42) - 0.115
    stk = 0.048
    ag = np.clip(0.5 - d_rg * (ks * 0.5), 0.0, 1.0)
    mg = np.clip(0.5 - (d_rg + stk) * (ks * 0.5), 0.0, 1.0)
    aj = np.clip(0.5 - d_rj * (ks * 0.5), 0.0, 1.0)
    mj = np.clip(0.5 - (d_rj + stk) * (ks * 0.5), 0.0, 1.0)
    tg = np.clip((kv + 0.6) / 1.2, 0.0, 1.0)[:, :, None]
    cg = rgb("1E2D38")[None, None, :] * (1.0 - mg[:, :, None]) + (rgb("F5E4B0") * (1.0 - tg) + rgb("C9A45C") * tg) * mg[:, :, None]
    cj = rgb("182E34")[None, None, :] * (1.0 - mj[:, :, None]) + (rgb("B8EFE2") * (1.0 - tg) + rgb("64B5A6") * tg) * mj[:, :, None]
    gold_over = kv < 0.0
    a_out = np.clip(ag + aj - ag * aj, 0.0, 1.0)
    over_a = np.where(gold_over, ag, aj)[:, :, None]
    under_a = np.where(gold_over, aj, ag)[:, :, None]
    over_c = np.where(gold_over[:, :, None], cg, cj)
    under_c = np.where(gold_over[:, :, None], cj, cg)
    comp_c = (over_c * over_a + under_c * under_a * (1.0 - over_a)) / np.maximum(a_out[:, :, None], 1e-4)
    k_rgba = np.zeros((ks, ks, 4), dtype=np.uint8)
    k_rgba[:, :, :3] = np.clip(np.round(comp_c), 0, 255).astype(np.uint8)
    k_rgba[:, :, 3] = np.clip(np.round(a_out * 255.0), 0, 255).astype(np.uint8)
    Image.fromarray(k_rgba).resize((64, 64), Image.LANCZOS).save(os.path.join(UI_DIR, "ic_bond_knot.png"))

    # --- 4) 低调天青瓷小右向尖角 ic_bond_go.png (40x40) ---
    gs = 40 * SS
    tri_pts = [(15.0 * SS, 10.5 * SS), (27.5 * SS, 20.0 * SS), (15.0 * SS, 29.5 * SS)]
    d_tri = sdf_convex_poly(gs, gs, tri_pts) - 1.8 * SS
    a_tri = np.clip(0.5 - d_tri, 0.0, 1.0)
    m_tri = np.clip(0.5 - (d_tri + 1.8 * SS), 0.0, 1.0)
    g_col = rgb("1A2C3B")[None, None, :] * (1.0 - m_tri[:, :, None]) + rgb("CFE8F2")[None, None, :] * m_tri[:, :, None]
    g_rgba = np.zeros((gs, gs, 4), dtype=np.uint8)
    g_rgba[:, :, :3] = np.clip(np.round(g_col), 0, 255).astype(np.uint8)
    g_rgba[:, :, 3] = np.clip(np.round(a_tri * 235.0), 0, 255).astype(np.uint8)
    Image.fromarray(g_rgba).resize((40, 40), Image.LANCZOS).save(os.path.join(UI_DIR, "ic_bond_go.png"))
    print("Saved stone-inlaid h_podium_crest, h_bond_pill, ic_bond_knot, ic_bond_go")


def build_chest_mat():
    """去掉突兀红地毯：改为与城台石面完全同质的【浅凹青白石承盘槽】，稳托三枚宝箱而零色块干扰。"""
    bw, bh = 456, 48
    w, h = bw * SS, bh * SS
    round_r = 9.0 * SS
    rim_w = 2.8 * SS

    verts = [
        (22.0 * SS, 8.0 * SS),
        ((bw - 22.0) * SS, 8.0 * SS),
        ((bw - 10.0) * SS, (bh - 10.0) * SS),
        (10.0 * SS, (bh - 10.0) * SS),
    ]
    d = sdf_convex_poly(w, h, verts) - round_r
    alpha = np.clip(0.5 - d, 0.0, 1.0)
    d_in = d + rim_w
    m_in = np.clip(0.5 - d_in, 0.0, 1.0)

    y_idx, _ = np.mgrid[0:h, 0:w].astype(np.float32)
    yn = y_idx / (h - 1)
    tr = np.clip((yn - 0.14) / 0.72, 0.0, 1.0)[:, :, None]

    # 上沿石刻阴线 (#64696C) -> 下沿受光亮青白石边 (#CECCC4)，槽内略沉半调青石面 (#878B8C -> #949796)
    rim_col = rgb("62676B") * (1.0 - tr) + rgb("D0CEC6") * tr
    in_col = rgb("808587") * (1.0 - tr) + rgb("929695") * tr

    d_down = np.roll(d_in, int(round(3.0 * SS)), axis=0)
    d_down[:int(round(3.0 * SS)), :] = 999.0
    in_sh = np.clip(0.5 - d_in, 0.0, 1.0) * np.clip(d_down + 0.5, 0.0, 1.0) * 0.35
    in_col = in_col * (1.0 - in_sh[:, :, None]) + rgb("585D62")[None, None, :] * in_sh[:, :, None]

    col = rim_col * (1.0 - m_in[:, :, None]) + in_col * m_in[:, :, None]
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[:, :, :3] = np.clip(np.round(col), 0, 255).astype(np.uint8)
    rgba[:, :, 3] = np.clip(np.round(alpha * 228.0), 0, 255).astype(np.uint8)
    Image.fromarray(rgba).resize((bw, bh), Image.LANCZOS).save(os.path.join(UI_DIR, "h_chest_mat.png"))
    print("Saved stone-recessed h_chest_mat.png")


def build_hex_start_button():
    """全场唯一温润宋韵鎏金主行动按钮（比例更精致协调 468x142，金泽温润不刺眼）。"""
    bw, bh = 468, 142
    margin = 8
    tip_dx = 30.0
    round_r = 12.0

    w, h = bw * SS, bh * SS
    pad = 2.2 * SS
    stroke_w = 3.0 * SS
    rim_w = 9.5 * SS

    d_outer = hex_plaque_sdf(w, h, pad, pad, tip_dx * SS, round_r * SS)
    alpha = np.clip(0.5 - d_outer, 0.0, 1.0)
    d_frame = d_outer + stroke_w
    frame_mask = np.clip(0.5 - d_frame, 0.0, 1.0)
    d_inner = d_frame + rim_w
    inner_mask = np.clip(0.5 - d_inner, 0.0, 1.0)

    y_idx, x_idx = np.mgrid[0:h, 0:w].astype(np.float32)
    y_norm = y_idx / (h - 1)
    x_norm = x_idx / (w - 1)
    t_rim = np.clip(y_norm, 0.0, 1.0)[:, :, None]

    c_rim_top, c_rim_mid, c_rim_bot = rgb("FFF2AA"), rgb("F7CE52"), rgb("D48E1A")
    rim_col = np.where(
        t_rim <= 0.5,
        c_rim_top * (1.0 - t_rim * 2.0) + c_rim_mid * (t_rim * 2.0),
        c_rim_mid * (1.0 - (t_rim - 0.5) * 2.0) + c_rim_bot * ((t_rim - 0.5) * 2.0),
    )
    side_facet = np.clip((np.abs(x_norm - 0.5) - 0.39) / 0.10, 0.0, 1.0) * (1.0 - np.abs(y_norm - 0.42))
    rim_col = rim_col + (255.0 - rim_col) * (side_facet[:, :, None] * 0.20)

    c_in_top, c_in_mid, c_in_bot = rgb("FFE06B"), rgb("F9C43C"), rgb("EAA420")
    in_col = np.where(
        t_rim <= 0.52,
        c_in_top * (1.0 - t_rim / 0.52) + c_in_mid * (t_rim / 0.52),
        c_in_mid * (1.0 - (t_rim - 0.52) / 0.48) + c_in_bot * ((t_rim - 0.52) / 0.48),
    )
    gloss = np.clip((0.44 - y_norm) * 8.0, 0.0, 1.0) * (0.12 + 0.07 * (1.0 - y_norm / 0.44))
    in_col = in_col + (255.0 - in_col) * gloss[:, :, None]

    tooth_period = 22.0 * SS
    tooth_wave = np.abs(((x_idx % tooth_period) / tooth_period) * 2.0 - 1.0)
    bot_dist = -d_inner
    in_bot_zone = np.clip((y_norm - 0.74) / 0.06, 0.0, 1.0)
    tooth_h = (2.0 + 6.5 * tooth_wave) * SS
    tooth_mask = np.clip(tooth_h - bot_dist, 0.0, 1.0) * in_bot_zone * 0.20
    in_col = in_col * (1.0 - tooth_mask[:, :, None]) + rgb("FFF09C")[None, None, :] * tooth_mask[:, :, None]

    groove = np.clip(1.15 - np.abs(d_inner) / (1.0 * SS), 0.0, 1.0)
    face_col = rim_col * (1.0 - inner_mask[:, :, None]) + in_col * inner_mask[:, :, None]
    face_col = face_col * (1.0 - groove[:, :, None] * 0.65) + rgb("C47E10")[None, None, :] * (groove[:, :, None] * 0.65)

    d_down = np.roll(d_frame, int(round(2.2 * SS)), axis=0)
    d_down[:int(round(2.2 * SS)), :] = 999.0
    top_hi = np.clip(0.5 - d_frame, 0.0, 1.0) * np.clip(d_down + 0.5, 0.0, 1.0) * np.clip(1.0 - y_norm * 2.8, 0.0, 1.0) * 0.75
    face_col = face_col + (255.0 - face_col) * top_hi[:, :, None]

    c_stroke = rgb("4A2B08")
    full_col = c_stroke[None, None, :] * (1.0 - frame_mask[:, :, None]) + face_col * frame_mask[:, :, None]

    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[:, :, :3] = np.clip(np.round(full_col), 0, 255).astype(np.uint8)
    rgba[:, :, 3] = np.clip(np.round(alpha * 255.0), 0, 255).astype(np.uint8)
    Image.fromarray(rgba).resize((bw, bh), Image.LANCZOS).save(os.path.join(UI_DIR, "h_start_hex.png"))

    tw, th = bw + margin * 2, bh + margin * 2
    lw, lh = tw * SS, th * SS
    lpad = (margin + 2.2) * SS
    d_lip = hex_plaque_sdf(lw, lh, lpad, lpad, tip_dx * SS, round_r * SS)
    l_alpha = np.clip(0.5 - d_lip, 0.0, 1.0)
    l_body = np.clip(0.5 - (d_lip + stroke_w), 0.0, 1.0)
    _, lx = np.mgrid[0:lh, 0:lw].astype(np.float32)
    side_shade = np.clip((np.abs(lx / (lw - 1) - 0.5) - 0.37) / 0.11, 0.0, 1.0)
    lip_fill = rgb("B87612")[None, None, :] * (1.0 - side_shade[:, :, None]) + rgb("8E560A")[None, None, :] * side_shade[:, :, None]
    lip_col = c_stroke[None, None, :] * (1.0 - l_body[:, :, None]) + lip_fill * l_body[:, :, None]
    l_rgba = np.zeros((lh, lw, 4), dtype=np.uint8)
    l_rgba[:, :, :3] = np.clip(np.round(lip_col), 0, 255).astype(np.uint8)
    l_rgba[:, :, 3] = np.clip(np.round(l_alpha * 255.0), 0, 255).astype(np.uint8)
    lip_im = Image.fromarray(l_rgba).resize((tw, th), Image.LANCZOS)

    sh_mask = Image.fromarray(np.clip(np.round(l_alpha * 255.0), 0, 255).astype(np.uint8)).resize((tw, th), Image.LANCZOS)
    sh_arr = np.array(sh_mask.filter(ImageFilter.GaussianBlur(radius=3.8)), dtype=np.float32) * 0.28
    sh_rgba = np.zeros((th, tw, 4), dtype=np.uint8)
    sh_rgba[:, :, :3] = [18, 26, 36]
    sh_rgba[3:, :, 3] = np.clip(np.round(sh_arr[:-3, :]), 0, 255).astype(np.uint8)
    lip_canvas = Image.fromarray(sh_rgba)
    lip_canvas.alpha_composite(lip_im, (0, 0))
    lip_canvas.save(os.path.join(UI_DIR, "h_start_hex_lip.png"))
    print("Saved refined h_start_hex.png & h_start_hex_lip.png")


def build_stage_arrow():
    """切关箭头改为与青石台面及宋瓷顶底栏一致的温润青白瓷石立体三角（不抢鎏金主按钮焦点）。"""
    bw, bh = 74, 84
    w, h = bw * SS, bh * SS
    round_r = 8.5 * SS
    stroke_w = 2.4 * SS
    sink = int(round(4.2 * SS))

    verts_face = [
        (15.0 * SS, (bh * 0.5 - 2.5) * SS),
        (57.0 * SS, 14.0 * SS),
        (57.0 * SS, (bh - 19.0) * SS),
    ]
    verts_lip = [(vx, vy + sink) for vx, vy in verts_face]

    d_face = sdf_convex_poly(w, h, verts_face) - round_r
    d_lip = sdf_convex_poly(w, h, verts_lip) - round_r
    a_face = np.clip(0.5 - d_face, 0.0, 1.0)
    m_face = np.clip(0.5 - (d_face + stroke_w), 0.0, 1.0)
    a_lip = np.clip(0.5 - d_lip, 0.0, 1.0)
    m_lip = np.clip(0.5 - (d_lip + stroke_w), 0.0, 1.0)

    y_idx, _ = np.mgrid[0:h, 0:w].astype(np.float32)
    yn = y_idx / (h - 1)
    t = np.clip((yn - 0.12) / 0.72, 0.0, 1.0)[:, :, None]

    c_stroke = rgb("2A3B4C")
    c_lip = rgb("7E8E9B")
    face_fill = rgb("F4F6F5") * (1.0 - t) + rgb("D3DAE0") * t
    d_down = np.roll(d_face + stroke_w, int(round(1.8 * SS)), axis=0)
    d_down[:int(round(1.8 * SS)), :] = 999.0
    hi = np.clip(0.5 - (d_face + stroke_w), 0.0, 1.0) * np.clip(d_down + 0.5, 0.0, 1.0) * 0.70
    face_fill = face_fill + (255.0 - face_fill) * hi[:, :, None]

    lip_col = c_stroke[None, None, :] * (1.0 - m_lip[:, :, None]) + c_lip[None, None, :] * m_lip[:, :, None]
    face_col = c_stroke[None, None, :] * (1.0 - m_face[:, :, None]) + face_fill * m_face[:, :, None]

    lip_rgba = np.zeros((h, w, 4), dtype=np.uint8)
    lip_rgba[:, :, :3] = np.clip(np.round(lip_col), 0, 255).astype(np.uint8)
    lip_rgba[:, :, 3] = np.clip(np.round(a_lip * 255.0), 0, 255).astype(np.uint8)
    lip_im = Image.fromarray(lip_rgba).resize((bw, bh), Image.LANCZOS)

    face_rgba = np.zeros((h, w, 4), dtype=np.uint8)
    face_rgba[:, :, :3] = np.clip(np.round(face_col), 0, 255).astype(np.uint8)
    face_rgba[:, :, 3] = np.clip(np.round(a_face * 255.0), 0, 255).astype(np.uint8)
    face_im = Image.fromarray(face_rgba).resize((bw, bh), Image.LANCZOS)

    sh_arr = np.array(lip_im.split()[3].filter(ImageFilter.GaussianBlur(radius=3.0)), dtype=np.float32) * 0.24
    canvas_arr = np.zeros((bh, bw, 4), dtype=np.uint8)
    canvas_arr[:, :, :3] = [18, 26, 36]
    canvas_arr[2:, :, 3] = np.clip(np.round(sh_arr[:-2, :]), 0, 255).astype(np.uint8)
    out = Image.fromarray(canvas_arr)
    out.alpha_composite(lip_im, (0, 0))
    out.alpha_composite(face_im, (0, 0))
    out.save(os.path.join(UI_DIR, "h_stage_arrow.png"))
    print("Saved harmonized stone-celadon h_stage_arrow.png")


if __name__ == "__main__":
    process_stage_art()
    build_podium_crest_suite()
    build_chest_mat()
    build_hex_start_button()
    build_stage_arrow()
