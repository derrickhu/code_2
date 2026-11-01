#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把微信启动封面换成首页底图：顶上印游戏名，底下压一条暗带印健康游戏忠告，进度条让在忠告上面。

用法：导出微信包后由 WxMiniGame.Export 调用，也可以单独跑 python3 tools/wechat_loading_cover.py
依赖：Pillow；字体用 game_assets/code_2/fonts 里的思源黑体可变字体，找不到时退回系统冬青黑体。
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "Assets/Resources/Art/home_bg.jpg"
OUT = ROOT / "wechat-minigame/minigame/images/background.jpg"
GAME_JS = ROOT / "wechat-minigame/minigame/game.js"
FONT_VF = ROOT.parent / "game_assets/code_2/fonts/NotoSansSC-VF.ttf"
FONT_FALLBACK = Path("/System/Library/Fonts/Hiragino Sans GB.ttc")

GAME_NAME = "宋江别慌张"
TITLE = "健康游戏忠告"
LINES = [
    "抵制不良游戏，拒绝盗版游戏。",
    "注意自我保护，谨防受骗上当。",
    "适度游戏益脑，沉迷游戏伤身。",
    "合理安排时间，享受健康生活。",
]

W, H = 720, 1280
NAME_PX = 92
NAME_TOP = 150
ADVICE_TITLE_PX = 26
ADVICE_PX = 22
ADVICE_STEP = 32
ADVICE_BOTTOM = 40
BAND_H = 250
BAR_H = 8
# 进度条和加载文字从底边量，放在忠告暗带的上沿之上。
BAR_BOTTOM = BAND_H + 26
TEXT_BOTTOM = BAR_BOTTOM + BAR_H + 10
TEXT_H = 22

NAME_FILL = (255, 233, 168)
NAME_EDGE = (46, 26, 14)
ADVICE_FILL = (245, 236, 214)
BAR = "#D8452F"


def font(size, weight):
    if FONT_VF.exists():
        f = ImageFont.truetype(str(FONT_VF), size)
        try:
            f.set_variation_by_axes([weight])
        except (OSError, ValueError):
            pass
        return f
    return ImageFont.truetype(str(FONT_FALLBACK), size, index=1 if weight >= 700 else 0)


def centered(draw, text, face, top, fill, stroke=0, edge=None):
    box = draw.textbbox((0, 0), text, font=face, stroke_width=stroke)
    x = (W - (box[2] - box[0])) / 2 - box[0]
    draw.text((x, top - box[1]), text, font=face, fill=fill, stroke_width=stroke, stroke_fill=edge)


def compose():
    im = Image.open(SRC).convert("RGB").resize((W, H), Image.Resampling.LANCZOS)

    band = Image.new("RGBA", (W, BAND_H), (20, 14, 10, 190))
    base = im.convert("RGBA")
    base.alpha_composite(band, (0, H - BAND_H))
    im = base.convert("RGB")

    draw = ImageDraw.Draw(im)
    centered(draw, GAME_NAME, font(NAME_PX, 900), NAME_TOP, NAME_FILL, stroke=7, edge=NAME_EDGE)

    body = font(ADVICE_PX, 500)
    top = H - ADVICE_BOTTOM - ADVICE_STEP * len(LINES)
    centered(draw, TITLE, font(ADVICE_TITLE_PX, 800), top - ADVICE_TITLE_PX - 18, ADVICE_FILL)
    for i, line in enumerate(LINES):
        centered(draw, line, body, top + i * ADVICE_STEP, ADVICE_FILL)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    im.save(OUT, "JPEG", quality=86, optimize=True)
    print(f"封面 {OUT}  {OUT.stat().st_size // 1024}KB")


def patch_game_js():
    if not GAME_JS.exists():
        print("没有 game.js，跳过加载页配置")
        return
    js = GAME_JS.read_text(encoding="utf-8")
    start = js.find("loadingPageConfig:")
    if start < 0:
        raise SystemExit("game.js 里没有 loadingPageConfig")
    brace = js.find("{", start)
    depth = 0
    end = None
    for i in range(brace, len(js)):
        if js[i] == "{":
            depth += 1
        elif js[i] == "}":
            depth -= 1
            if depth == 0:
                end = i + 1
                break
    if end is None:
        raise SystemExit("loadingPageConfig 括号不配对")
    block = f"""loadingPageConfig: {{
                totalLaunchTime: 7000,
                animationDuration: 100,
                // 720x1280 首页底图，健康忠告印在底部暗带里，进度条停在暗带上面。
                designWidth: {W},
                designHeight: {H},
                scaleMode: scaleMode.exactFit,
                textConfig: {{
                    firstStartText: '首次加载请耐心等待',
                    downloadingText: ['正在加载资源'],
                    compilingText: '编译中',
                    initText: '初始化中',
                    completeText: '开始游戏',
                    textDuration: 1500,
                    style: {{
                        bottom: {TEXT_BOTTOM},
                        height: {TEXT_H},
                        width: 360,
                        lineHeight: {TEXT_H},
                        color: '#FFF1D0',
                        fontSize: 15,
                    }},
                }},
                barConfig: {{
                    style: {{
                        width: 300,
                        height: {BAR_H},
                        padding: 1,
                        bottom: {BAR_BOTTOM},
                        backgroundColor: '{BAR}',
                    }},
                }},
                iconConfig: {{
                    visible: false,
                    style: {{
                        width: 64,
                        height: 23,
                        bottom: 20,
                    }},
                }},
                materialConfig: {{
                    backgroundImage: 'images/background.jpg',
                    backgroundVideo: '',
                    iconImage: 'images/unity_logo.png',
                }},
            }}"""
    GAME_JS.write_text(js[:start] + block + js[end:], encoding="utf-8")
    print("已改 game.js：封面换成首页底图，进度条在健康忠告上方")


def main():
    if not SRC.exists():
        raise SystemExit(f"缺少 {SRC}")
    compose()
    patch_game_js()


if __name__ == "__main__":
    main()
