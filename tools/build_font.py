"""把思源黑体可变字体定成两个字重，再裁成游戏内实际用到的字（扫描 Assets/Scripts 里的非 ASCII 字符 + ASCII），输出到 Resources/Fonts。

用法：python3 tools/build_font.py
依赖：fontTools
"""
import pathlib
import re
import shutil

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT.parent / "game_assets" / "code_2" / "fonts"
OUT = ROOT / "Assets" / "Resources" / "Fonts"
LICENSES = ROOT / "LICENSES"

# (输出名, 字重, 改名后的家族名)。OFL 字体改动后要换家族名。
# 字重说明：750（ExtraBold）笔画饱满但内白通透，配细描边不糊团；570（SemiBold）正文与标签清爽利落。
FONTS = [
    ("Heavy.ttf", 750, "Liangshan Heavy"),
    ("Bold.ttf", 570, "Liangshan Bold"),
]

EXTRA = (
    "，。！？、：；「」『』（）【】《》—…·★☆◆◇▲▼◀▶↑↓←→♂♀"
    "×÷＋－±％‰✓!?"
    "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
)


def collect_chars() -> str:
    chars = set(chr(c) for c in range(0x20, 0x7F))
    chars.update(EXTRA)
    for folder in ("Scripts", "Tests"):
        base = ROOT / "Assets" / folder
        if not base.exists():
            continue
        for p in base.rglob("*.cs"):
            for m in re.finditer(r"[^\x00-\x7F]", p.read_text(encoding="utf-8")):
                chars.add(m.group(0))
    return "".join(sorted(chars))


def rename(font: TTFont, family: str) -> None:
    ps = family.replace(" ", "")
    for rec in font["name"].names:
        if rec.nameID in (1, 4, 16, 21):
            rec.string = family
        elif rec.nameID in (3, 6, 20):
            rec.string = ps
        elif rec.nameID in (2, 17, 22):
            rec.string = "Regular"


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    LICENSES.mkdir(exist_ok=True)
    text = collect_chars()
    for dst, weight, family in FONTS:
        vf = TTFont(SRC / "NotoSansSC-VF.ttf")
        font = instancer.instantiateVariableFont(vf, {"wght": weight})
        cmap = font.getBestCmap()
        missing = [c for c in text if ord(c) not in cmap and not c.isspace()]
        opts = subset.Options()
        opts.layout_features = ["*"]
        opts.name_IDs = ["*"]
        opts.name_languages = ["*"]
        opts.notdef_outline = True
        sub = subset.Subsetter(opts)
        sub.populate(text=text)
        sub.subset(font)
        rename(font, family)
        font.save(OUT / dst)
        size = (OUT / dst).stat().st_size // 1024
        print(f"{dst}: {len(text)} 字, {size} KB, 缺字 {''.join(missing) or '无'}")
    shutil.copy(SRC / "OFL-NotoSansSC.txt", LICENSES / "OFL-NotoSansSC.txt")


if __name__ == "__main__":
    main()
