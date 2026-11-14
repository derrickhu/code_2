using System.Collections.Generic;
using UnityEngine;

namespace Liangshan
{
    public static class Pal
    {
        public static readonly Color Ink = Hex("1A2938");
        public static readonly Color SubInk = Hex("4A6278");
        public static readonly Color Muted = Hex("748CA2");
        public static readonly Color Edge = Hex("22364A");
        public static readonly Color Cream = Hex("F4F8FC");
        public static readonly Color Parchment = Hex("EAF1F8");
        public static readonly Color ParchmentDeep = Hex("D5E2EE");
        public static readonly Color Gold = Hex("FFD042");
        public static readonly Color GoldDeep = Hex("C48A14");
        public static readonly Color Amber = Hex("F5BA36");
        public static readonly Color Champagne = Hex("FFE699");
        public static readonly Color Orange = Hex("F29B2E");
        public static readonly Color Red = Hex("E54545");
        public static readonly Color RedDeep = Hex("841C22");
        public static readonly Color Green = Hex("3CC468");
        public static readonly Color GreenDeep = Hex("1E7A40");
        public static readonly Color Teal = Hex("2EB8A6");
        public static readonly Color TealDeep = Hex("196B68");
        public static readonly Color Azure = Hex("2B88C4");
        public static readonly Color Blue = Hex("3E8CE4");
        public static readonly Color Purple = Hex("8E5CE0");
        /// <summary>禁用按钮等冷灰蓝。饱和度低于 0.35，Ui.BtnSkin 会选灰色皮肤。</summary>
        public static readonly Color Slate = Hex("6E7E8E");
        public static readonly Color SlateDeep = Hex("182838");
        /// <summary>局外彩色底、铭牌上的深海蓝描边。</summary>
        public static readonly Color Navy = Hex("14304D");
        public static readonly Color Shadow = new Color(0.04f, 0.08f, 0.14f, 0.36f);

        /// <summary>星级色：一星白、二星绿、三星蓝紫、满星金。</summary>
        public static readonly Color[] Star = { Hex("F4F1EA"), Hex("7BE36A"), Hex("8FA8FF"), Hex("FFD24A") };
        /// <summary>多层血条：首层暖金橙（对标参考图清晰暖橙血条），其后鲜红、紫晶、天青、翠绿逐层轮转。</summary>
        public static readonly Color[] HpLayer = { Hex("F59B23"), Hex("EB4934"), Hex("9A63E6"), Hex("38A6E8"), Hex("5CC948"), Hex("F2C431") };

        /// <summary>多层血条：(剩余层数, 当前层比例, 当前层颜色, 下一层颜色)。最后一层背后是暗底。</summary>
        public static (int layer, float frac, Color cur, Color next) Layers(float hp, float max, int n)
        {
            float per = max / n;
            int layer = Mathf.Clamp(Mathf.CeilToInt(hp / per - 1e-4f), 1, n);
            float frac = Mathf.Clamp01((hp - (layer - 1) * per) / per);
            var cur = HpLayer[(layer - 1) % HpLayer.Length];
            var next = layer > 1 ? HpLayer[(layer - 2) % HpLayer.Length] : Hex("3A1F1A");
            return (layer, frac, cur, next);
        }

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var c);
            return c;
        }

        public static Color A(this Color c, float a) { c.a = a; return c; }
        public static Color Mul(this Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }

    public static class Fonts
    {
        static Font _heavy, _bold;

        /// <summary>思源黑体 Heavy 子集：标题、按钮、数字。</summary>
        public static Font Heavy => _heavy ? _heavy : (_heavy = Load("Fonts/Heavy"));

        /// <summary>思源黑体 Bold 子集：正文。</summary>
        public static Font Bold => _bold ? _bold : (_bold = Load("Fonts/Bold"));

        static Font Load(string path)
        {
            var f = Resources.Load<Font>(path);
            if (f == null)
            {
                Debug.LogError("缺字体 " + path + "，先跑 tools/build_font.py");
                f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return f;
        }
    }

    /// <summary>手绘图从 Resources/Art 读；圆角块、圆、柔光点等程序化生成。</summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Rounded => Get("rounded", () => RoundRect(256, 64, 0));
        public static Sprite RoundedSmall => Get("rounded_s", () => RoundRect(256, 36, 0));
        /// <summary>怪物头顶利落微圆角扁框（对标参考图紧致直角感矩形条，杜绝肥厚大圆胶囊）。</summary>
        public static Sprite HpBarRect => Get("hpbar_rect", () => RoundRect(128, 12, 0));
        public static Sprite Pill => Get("pill", () => RoundRect(256, 126, 0));
        public static Sprite Circle => Get("circle", () => Disc(256, 0.02f));
        public static Sprite SoftDot => Get("softdot", () => Disc(128, 1f));
        /// <summary>贴地脚影：内圈紧实深影、外沿柔化，让角色和抛射物稳稳踩在石板上不发飘。</summary>
        public static Sprite GroundShadow => Get("groundshadow", GroundShadowTex);
        public static Sprite Ring => Get("ring", () => RingTex(256, 0.82f));
        public static Sprite White => Get("white", () => Solid(4));
        public static Sprite Hex6 => Get("hex", HexTex);
        /// <summary>高清描边翠绿升级箭头：对标主流竖版卡牌清爽角标，不糊边。</summary>
        public static Sprite UpArrow => Get("uparrow", UpArrowTex);
        public static Sprite VGrad => Get("vgrad", VGradTex);
        public static Sprite Coin => Img("ic_silver");
        public static Sprite Hole => Get("hole", HoleTex);
        public static Sprite RangeDisc => Get("range", RangeTex);
        /// <summary>同上一种暖黄填充，只要朝上的半圆。轴心在直径中点，缩放 1 时半径 1。</summary>
        public static Sprite RangeFan => Get("rangefan", RangeFanTex);

        public static Sprite Sparkle => Get("sparkle", SparkleTex);
        /// <summary>剑气：上拱的月牙，外沿锐利、往里渐隐，两头收尖。宽:高 = 4:1。</summary>
        public static Sprite Crescent => Get("crescent", CrescentTex);
        /// <summary>法术射线：横向长条，中间一道白热芯、两侧柔光，两头略收。宽:高 = 4:1。</summary>
        public static Sprite Beam => Get("beam", BeamTex);

        static Sprite BeamTex()
        {
            const int w = 256, h = 64;
            var t = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = Mathf.Abs((y + 0.5f) / h * 2 - 1);
                    float ends = Mathf.Clamp01(u / 0.06f) * Mathf.Clamp01((1 - u) / 0.04f);
                    float glow = Mathf.Exp(-v * v * 5f);
                    float core = Mathf.Exp(-v * v * 60f);
                    float a = Mathf.Clamp01(glow * 0.75f + core) * ends;
                    px[y * w + x] = new Color(1, 1, Mathf.Lerp(0.85f, 1f, core), a);
                }
            t.SetPixels(px);
            return Make(t);
        }

        static readonly Dictionary<string, Sprite[]> _frames = new Dictionary<string, Sprite[]>();

        /// <summary>
        /// 角色动画帧 name_0..3（同一方形画布，脚底贴底边）。没有帧图时退回单张立绘。
        /// 帧在图包里：包还没装上时先给立绘并开始下载，装上后再取就是帧。
        /// </summary>
        public static Sprite[] Frames(string name)
        {
            if (ArtPack.Has(name))
            {
                var c = ArtPack.Clip(name, name);
                if (c != null && c.Length > 0) return c;
                if (c == null) ArtPack.Ensure(name);
                return new[] { Img(name) };
            }
            if (_frames.TryGetValue(name, out var f)) return f;
            var list = new List<Sprite>();
            for (int i = 0; i < AnimMeta.Frames; i++)
            {
                if (Resources.Load<Sprite>("Art/" + name + "_" + i) == null) break;
                list.Add(Img(name + "_" + i));
            }
            if (list.Count == 0) list.Add(Img(name));
            return _frames[name] = list.ToArray();
        }

        /// <summary>一段循环帧 name_动作_0..N（画布与 name_0 相同）。没有就返回空数组。</summary>
        public static Sprite[] Clip(string name, string act)
        {
            string key = name + "_" + act;
            if (ArtPack.Has(name))
            {
                var c = ArtPack.Clip(name, key);
                if (c == null) ArtPack.Ensure(name);
                return c ?? new Sprite[0];
            }
            if (_frames.TryGetValue(key, out var f)) return f;
            var list = new List<Sprite>();
            for (int i = 0; i < 16; i++)
            {
                var s = Resources.Load<Sprite>("Art/" + key + "_" + i);
                if (s == null) break;
                list.Add(s);
            }
            return _frames[key] = list.ToArray();
        }

        /// <summary>走路循环帧。一个循环 = 左右各迈一步。</summary>
        public static Sprite[] WalkFrames(string name) => Clip(name, "walk");

        /// <summary>飞行循环帧。一个循环 = 扇翅一次。</summary>
        public static Sprite[] FlyFrames(string name) => Clip(name, "fly");

        /// <summary>
        /// 把“身体高度”换算成帧画布的世界边长，以及画布中心相对脚底的高度。
        /// 没有帧图时画布就是立绘本身（身体占满、脚底在底边）。
        /// </summary>
        public static (float Size, float CenterY) FrameBox(string name, float bodyHeight)
        {
            if (Animated(name) && AnimMeta.Info.TryGetValue(name, out var m))
            {
                float s = bodyHeight / m.Body;
                return (s, s * (0.5f - m.Foot));
            }
            return (bodyHeight, bodyHeight * 0.5f);
        }

        /// <summary>有帧画布（不管图包装没装上）。排版按这个算，帧晚到也不跳位置。</summary>
        public static bool Animated(string name) =>
            AnimMeta.Info.ContainsKey(name) && (ArtPack.Has(name) || Frames(name).Length > 1);

        /// <summary>图包装上后执行一次 apply（已装上就当场执行）；owner 先被销毁就不执行。</summary>
        public static void Late(Object owner, string pack, System.Action apply)
        {
            if (ArtPack.Ready(pack)) { apply(); return; }
            ArtPack.Ensure(pack, () => { if (owner) apply(); });
        }

        /// <summary>四角星闪：出手、命中的白色星光。</summary>
        static Sprite SparkleTex()
        {
            const int n = 128;
            var t = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / n * 2 - 1), v = Mathf.Abs((y + 0.5f) / n * 2 - 1);
                    float ray = Mathf.Pow(1 - u, 3) * Mathf.Exp(-v * 22f) + Mathf.Pow(1 - v, 3) * Mathf.Exp(-u * 22f);
                    float core = Mathf.Exp(-(u * u + v * v) * 18f);
                    float a = Mathf.Clamp01(ray + core);
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            return Make(t);
        }

        static Sprite CrescentTex()
        {
            const int w = 512, h = 128;
            var t = NewTex(w, h);
            var px = new Color[w * h];
            const float cy = -9.6f, r = 10.4f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2 - 1, v = (y + 0.5f) / h;
                    float d = Mathf.Sqrt(u * 4f * u * 4f + (v - cy) * (v - cy));
                    float e = d - r;
                    float th = 0.05f + 0.28f * (1 - u * u);
                    float a = e > 0 ? Mathf.Clamp01(1 - e / 0.03f) : Mathf.Exp(e / th);
                    a *= Mathf.Sqrt(Mathf.Clamp01(1 - Mathf.Abs(u)));
                    float hot = e > 0 ? 1 : Mathf.Exp(e / (th * 0.25f));
                    px[y * w + x] = new Color(1, 1, Mathf.Lerp(0.75f, 1f, hot), Mathf.Clamp01(a));
                }
            t.SetPixels(px);
            return Make(t);
        }

        /// <summary>攻击范围填充：均匀暖黄，靠边一圈内发光；描边由 LineRenderer 画，保证任意尺寸都清晰。</summary>
        static Sprite RangeTex()
        {
            const int n = 512;
            var t = NewTex(n, n);
            var px = new Color[n * n];
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    px[y * n + x] = RangeFill(Mathf.Sqrt(dx * dx + dy * dy) / r, r);
                }
            t.SetPixels(px);
            return Make(t);
        }

        static Color RangeFill(float d, float radiusPx)
        {
            if (d >= 1f) return Color.clear;
            var warm = new Color(1f, 0.78f, 0.32f);
            float edge = Mathf.Clamp01((d - 0.9f) / 0.1f);
            float a = (0.3f + 0.2f * edge * edge) * Mathf.Clamp01((1f - d) * radiusPx);
            return new Color(warm.r, warm.g, warm.b, a);
        }

        /// <summary>和 RangeDisc 同一套填充，裁成朝上的半圆。</summary>
        static Sprite RangeFanTex()
        {
            const int w = 512, h = 256;
            var t = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w * 2f - 1f;
                    float dy = (y + 0.5f) / h;
                    px[y * w + x] = RangeFill(Mathf.Sqrt(dx * dx + dy * dy), h);
                }
            t.SetPixels(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h, 0, SpriteMeshType.FullRect);
        }

        /// <summary>引导聚光：中间透明圆洞，边缘柔和过渡到全黑。四角之外由调用方补黑块。</summary>
        static Sprite HoleTex()
        {
            const int n = 256;
            var t = NewTex(n, n);
            var px = new Color[n * n];
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    px[y * n + x] = new Color(0, 0, 0, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.62f, 0.86f, d)));
                }
            t.SetPixels(px);
            return Make(t);
        }

        /// <summary>长边 = 1 个世界单位。图包里的图不进缓存，卸包后才能真正释放。</summary>
        public static Sprite Img(string name)
        {
            var p = ArtPack.Find(name);
            if (p) return p;
            if (Cache.TryGetValue("img_" + name, out var s) && s) return s;
            s = Resources.Load<Sprite>("Art/" + name);
            if (s == null)
            {
                if (ArtPack.Owns(name))
                {
                    ArtPack.Ensure(ArtPack.PackOf(name));
                    return Clear;
                }
                Debug.LogError("缺图 Art/" + name);
                s = Rounded;
            }
            return Cache["img_" + name] = s;
        }

        /// <summary>全透明占位：图包还没到时的背景。</summary>
        public static Sprite Clear => Get("clear", () =>
        {
            var t = NewTex(4, 4);
            t.SetPixels32(new Color32[16]);
            return Make(t);
        });

        /// <summary>界面皮肤 Art/ui/*：按 100 像素/单位重建并带九宫格边，图上 1 像素 = 1 设计像素。</summary>
        public static Sprite Skin(string name) => Get("skin_" + name, () =>
        {
            var s = Img("ui/" + name);
            return Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, SkinBorder(name));
        });

        /// <summary>左、下、右、上。横条类上下为 0，配合按高度缩放使用。</summary>
        static Vector4 SkinBorder(string n)
        {
            if (n.EndsWith("_lip") && n.StartsWith("btn_")) return new Vector4(46, 46, 46, 46);
            if (n.EndsWith("_well") && n.StartsWith("btn_")) return new Vector4(22, 22, 22, 22);
            if (n.StartsWith("btn_")) return new Vector4(38, 38, 38, 38);
            if (n == "bar_bg") return new Vector4(22, 0, 22, 0);
            if (n.StartsWith("bar_")) return new Vector4(18, 0, 18, 0);
            if (n.StartsWith("card_")) return new Vector4(52, 52, 52, 52);
            if (n.StartsWith("slot_")) return new Vector4(44, 44, 44, 44);
            if (n.StartsWith("eqcard_")) return new Vector4(44, 48, 44, 44);
            if (n == "banner_gold") return new Vector4(120, 0, 120, 0);
            if (n.StartsWith("banner")) return new Vector4(148, 0, 148, 0);
            if (n.StartsWith("rank_badge_")) return new Vector4(76, 0, 76, 0);
            if (n.StartsWith("vtab_")) return new Vector4(36, 36, 36, 36);
            switch (n)
            {
                case "rank_well": return new Vector4(48, 48, 48, 48);
                case "rank_row":
                case "rank_row_me": return new Vector4(44, 44, 44, 44);
                case "panel": return new Vector4(68, 68, 68, 68);
                case "quest_tray": return new Vector4(56, 42, 56, 52);
                case "modal_well": return new Vector4(44, 44, 44, 44);
                case "hint": return new Vector4(52, 52, 52, 52);
                case "dark": return new Vector4(48, 48, 48, 48);
                case "tab_on": return new Vector4(40, 40, 40, 40);
                case "tabbar": return new Vector4(40, 20, 40, 30);
                case "row":
                case "row_claim":
                case "row_done": return new Vector4(42, 42, 42, 42);
                case "power_crest": return new Vector4(96, 0, 96, 0);
                case "pill": return new Vector4(28, 0, 28, 0);
                case "plate": return new Vector4(50, 0, 50, 0);
                case "plaque": return new Vector4(176, 0, 176, 0);
                case "flame_bar": return new Vector4(60, 60, 230, 130);
                case "h_start": return new Vector4(54, 54, 54, 54);
                case "h_start_lip": return new Vector4(62, 62, 62, 62);
                case "forge_sel": return new Vector4(48, 48, 48, 48);
                case "forge_card": return new Vector4(44, 44, 44, 44);
                case "gacha_dock_gold": return new Vector4(52, 52, 52, 52);
                case "gacha_dock_purple": return new Vector4(52, 52, 52, 52);
                case "gacha_sum_pedestal": return new Vector4(56, 56, 56, 56);
                case "gacha_tray": return new Vector4(48, 36, 48, 48);
                case "gacha_oct_badge": return new Vector4(56, 0, 56, 0);
                case "h_tabbar": return new Vector4(24, 24, 24, 28);
                case "h_tile": return new Vector4(28, 28, 28, 28);
                case "h_capsule": return new Vector4(28, 0, 28, 0);
                case "h_plaque": return new Vector4(44, 0, 44, 0);
                case "h_ribbon": return new Vector4(96, 0, 96, 0);
                default: return Vector4.zero;
            }
        }

        static Sprite Get(string key, System.Func<Sprite> make)
        {
            if (!Cache.TryGetValue(key, out var s) || s == null) Cache[key] = s = make();
            return s;
        }

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        static Sprite Make(Texture2D t, Vector4 border = default)
        {
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), Mathf.Max(t.width, t.height), 0, SpriteMeshType.FullRect, border);
        }

        static Sprite Solid(int n)
        {
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px);
            return Make(t);
        }

        static Sprite RoundRect(int n, float radius, float ring)
        {
            var t = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (n - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (n - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (ring > 0) a *= Mathf.Clamp01(d + ring + 0.5f);
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            float b = radius + 2;
            return Make(t, new Vector4(b, b, b, b));
        }

        static Sprite Disc(int n, float soft)
        {
            var t = NewTex(n, n);
            var px = new Color[n * n];
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    float a = soft > 0.5f ? Mathf.Pow(Mathf.Clamp01(1 - d), 1.6f) : Mathf.Clamp01((1 - d) * r);
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px);
            return Make(t);
        }

        static Sprite GroundShadowTex()
        {
            const int n = 128;
            var t = NewTex(n, n);
            var px = new Color[n * n];
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    float core = 1f - Mathf.SmoothStep(0.42f, 0.98f, d);
                    px[y * n + x] = new Color(1, 1, 1, core);
                }
            t.SetPixels(px);
            return Make(t);
        }

        static Sprite RingTex(int n, float inner)
        {
            var t = NewTex(n, n);
            var px = new Color[n * n];
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    float a = Mathf.Clamp01((1 - d) * r * 0.5f) * Mathf.Clamp01((d - inner) * r * 0.5f);
                    float glow = Mathf.Clamp01(1 - Mathf.Abs(d - (1 + inner) * 0.5f) / ((1 - inner) * 0.5f));
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Max(a * 0.9f, glow * glow));
                }
            t.SetPixels(px);
            return Make(t);
        }

        /// <summary>尖顶六边形（倍速按钮与宝石镶孔）。256×256 高倍超采抗锯齿。</summary>
        static Sprite HexTex()
        {
            const int n = 256;
            var t = NewTex(n, n);
            var px = new Color[n * n];
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float px0 = Mathf.Abs(x + 0.5f - r) / r, py0 = Mathf.Abs(y + 0.5f - r) / r;
                    float d = Mathf.Max(px0 * 0.8660254f + py0 * 0.5f, py0) - 0.96f;
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(-d * r));
                }
            t.SetPixels(px);
            return Make(t);
        }

        /// <summary>翠绿描边上箭头角标（y=0 为底、y=n-1 为箭头尖）。</summary>
        static Sprite UpArrowTex()
        {
            const int n = 96;
            var t = NewTex(n, n);
            var px = new Color[n * n];
            Color dark = Pal.Hex("153B22");
            Color topCol = Pal.Hex("7CF892");
            Color botCol = Pal.Hex("2CC452");
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f; // -1..1
                    float v = (y + 0.5f) / n * 2f - 1f; // -1(bottom)..+1(top)
                    float ax = Mathf.Abs(u);

                    // Rounded stem: x in [-0.26, 0.26], y in [-0.72, 0.08]
                    float sx = ax - 0.24f, sy = Mathf.Max(-0.70f - v, v - 0.12f);
                    float dStem = Mathf.Sqrt(Mathf.Max(sx, 0f) * Mathf.Max(sx, 0f) + Mathf.Max(sy, 0f) * Mathf.Max(sy, 0f))
                                  + Mathf.Min(Mathf.Max(sx, sy), 0f) - 0.05f;

                    // Triangle head: apex at (0, 0.76), base at y = -0.02, half-width = 0.62
                    // Signed distance to isosceles triangle (upward)
                    float ty = 0.76f - v; // 0 at apex, 0.78 at base
                    float slope = (ax - ty * (0.62f / 0.78f)) * 0.78f;
                    float dBase = -0.02f - v;
                    float dHead = Mathf.Max(slope, dBase) - 0.04f;
                    if (v > 0.76f) dHead = Mathf.Sqrt(ax * ax + (v - 0.76f) * (v - 0.76f)) - 0.04f;

                    float d = Mathf.Min(dStem, dHead) * (n * 0.5f); // in pixels
                    float aOut = Mathf.Clamp01(0.5f - (d - 4.2f));
                    float aIn = Mathf.Clamp01(0.5f - d);
                    if (aOut <= 0.001f) { px[y * n + x] = Color.clear; continue; }

                    Color fill = Color.Lerp(botCol, topCol, Mathf.Clamp01((v + 0.75f) / 1.5f));
                    if (u < -0.02f && v > -0.1f) fill = Color.Lerp(fill, Color.white, 0.18f);
                    Color rgb = Color.Lerp(dark, fill, aIn);
                    px[y * n + x] = new Color(rgb.r, rgb.g, rgb.b, aOut);
                }
            t.SetPixels(px);
            return Make(t);
        }

        /// <summary>自上而下由透明到不透明，做压暗渐变。</summary>
        static Sprite VGradTex()
        {
            var t = NewTex(4, 64);
            var px = new Color[4 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 4; x++)
                    px[y * 4 + x] = new Color(1, 1, 1, 1 - y / 63f);
            t.SetPixels(px);
            return Make(t);
        }
    }
}
