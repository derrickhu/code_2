using Liangshan.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Liangshan
{
    /// <summary>主界面：梁山全景、关卡与首领预览、出战好汉、开始挑战、底部页签（切片里只开放出征）。</summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _start;

        public static HomeScreen Create()
        {
            var rt = Ui.Rect(Ui.Root, "Home");
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var h = rt.gameObject.AddComponent<HomeScreen>();
            h._root = rt;
            h.Build();
            return h;
        }

        RectTransform Top(float x, float y, Vector2 size, string name) => Ui.Top(_root, x, y + Ui.SafeTop, size, name);
        RectTransform Bottom(float x, float y, Vector2 size, string name) => Ui.Bottom(_root, x, y, size, name);

        void Build()
        {
            // 背景按高度铺满，底边对齐（码头在下）。
            var bg = Ui.Box(_root, new Vector2(1080, 1920), Vector2.zero, Color.white, Art.Img("home_bg"), "bg");
            bg.type = Image.Type.Simple;
            var brt = bg.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            float scale = Mathf.Max(1f, Ui.Height / 1920f);
            brt.sizeDelta = new Vector2(1080, 1920) * scale;
            var shade = Ui.Box(_root, new Vector2(1080, 700), Vector2.zero, new Color(0.05f, 0.03f, 0.02f, 0.55f), Art.VGrad, "shadeBottom");
            var srt = shade.rectTransform;
            srt.anchorMin = new Vector2(0, 0);
            srt.anchorMax = new Vector2(1, 0);
            srt.sizeDelta = new Vector2(0, 700);
            srt.anchoredPosition = new Vector2(0, 350);

            BuildTopBar();
            BuildTitle();
            BuildLevel();
            BuildLineup();
            BuildStart();
            BuildTabs();
        }

        void BuildTopBar()
        {
            var avatar = Top(-440, 80, new Vector2(130, 130), "avatar");
            Ui.Box(avatar, new Vector2(124, 124), Vector2.zero, Pal.Edge, Art.Rounded);
            Ui.Box(avatar, new Vector2(112, 112), Vector2.zero, Pal.Hex("C9935A"), Art.Rounded);
            Ui.Icon(avatar, "prop_songjiang", 120, new Vector2(0, 4));
            var lv = Ui.Box(avatar, new Vector2(52, 52), new Vector2(-48, -48), Pal.Edge, Art.Circle);
            Ui.Outlined(lv.transform, "1", 30, Color.white, Vector2.zero, new Vector2(52, 52), Pal.Ink, 2f);
            var name = Top(-260, 58, new Vector2(240, 50), "name");
            Ui.Outlined(name, "梁山寨主", 34, Color.white, Vector2.zero, new Vector2(240, 50), Pal.Ink, 2.5f, TextAnchor.MiddleLeft);
            var xp = Top(-262, 100, new Vector2(220, 18), "xp");
            Ui.Box(xp, new Vector2(220, 18), Vector2.zero, Pal.Ink.A(0.7f), Art.Pill);
            var f = Ui.Box(xp, new Vector2(70, 12), new Vector2(-75, 0), Pal.Green, Art.Pill);

            Currency(160, "ic_ingot", Profile.Ingots);
            Currency(410, "ic_coins", Profile.Copper);
        }

        void Currency(float x, string icon, int value)
        {
            var c = Top(x, 70, new Vector2(230, 64), "cur_" + icon);
            Ui.Box(c, new Vector2(210, 54), new Vector2(14, 0), Pal.Ink.A(0.7f), Art.Pill);
            Ui.Icon(c, icon, 78, new Vector2(-92, 2));
            Ui.Outlined(c, Ui.Num(value), 34, Color.white, new Vector2(24, 1), new Vector2(170, 54), Pal.Ink, 2f);
        }

        void BuildTitle()
        {
            var t = Top(0, 250, new Vector2(900, 200), "logo");
            var glow = Ui.Box(t, new Vector2(820, 260), new Vector2(0, -10), new Color(0.15f, 0.06f, 0.02f, 0.55f), Art.SoftDot);
            var title = Ui.Outlined(t, "守住梁山", 150, Pal.Hex("FFE08A"), new Vector2(0, 10), new Vector2(900, 190), Pal.Hex("5A1E0C"), 7f);
            var g = title.gameObject.AddComponent<Outline>();
            g.effectColor = Pal.Hex("2A0E04");
            g.effectDistance = new Vector2(10, -10);
            var sub = Top(0, 370, new Vector2(700, 60), "sub");
            Ui.Box(sub, new Vector2(520, 58), Vector2.zero, Pal.RedDeep.A(0.85f), Art.Pill);
            Ui.Outlined(sub, "水浒好汉 · 合成守寨", 36, Pal.Cream, new Vector2(0, 1), new Vector2(600, 58), Pal.Ink, 2f);
        }

        void BuildLevel()
        {
            // 首领立在山门前的路上，关卡名压在路面。
            var boss = Top(0, 640, new Vector2(400, 400), "boss");
            var aura = Ui.Box(boss, new Vector2(420, 220), new Vector2(0, -130), Pal.Red.A(0.35f), Art.SoftDot);
            Ui.Box(boss, new Vector2(280, 70), new Vector2(0, -170), new Color(0, 0, 0, 0.4f), Art.SoftDot);
            var icon = Ui.Icon(boss, "en_boss", 360, new Vector2(0, 0));
            Tween.Run(icon, 999f, k => icon.rectTransform.anchoredPosition = new Vector2(0, 6 * Mathf.Sin(k * 999f * 2f)));
            var tag = Top(0, 870, new Vector2(400, 70), "levelTag");
            Ui.Box(tag, new Vector2(360, 66), Vector2.zero, Pal.Edge.A(0.85f), Art.Pill);
            Ui.Box(tag, new Vector2(52, 52), new Vector2(-150, 0), Pal.Red, Art.Circle);
            Ui.Outlined(tag, "1", 32, Color.white, new Vector2(-150, 1), new Vector2(52, 52), Pal.RedDeep, 2f);
            Ui.Outlined(tag, $"{Defs.Levels[0].Name} · 首领 {Defs.Levels[0].BossName}", 32, Pal.Hex("FFD8C8"), new Vector2(26, 1), new Vector2(320, 60), Pal.Ink, 2f);

            var road = Top(0, 1010, new Vector2(700, 200), "roadName");
            var a = Ui.Label(road, "第 1 关", 54, new Color(0.35f, 0.22f, 0.1f, 0.55f), new Vector2(0, 40), new Vector2(600, 70), TextAnchor.MiddleCenter, Fonts.Heavy);
            var b = Ui.Label(road, Defs.Levels[0].Name, 96, new Color(0.35f, 0.22f, 0.1f, 0.55f), new Vector2(0, -40), new Vector2(700, 120), TextAnchor.MiddleCenter, Fonts.Heavy);

            var waves = Top(0, 1130, new Vector2(700, 50), "info");
            Ui.Box(waves, new Vector2(520, 50), Vector2.zero, Pal.Ink.A(0.55f), Art.Pill);
            Ui.Label(waves, "共 10 波 · 第 4 波首领 · 第 8 波狂暴", 28, Pal.Cream, Vector2.zero, new Vector2(520, 50));
        }

        void BuildLineup()
        {
            var row = Bottom(0, 610, new Vector2(900, 200), "lineup");
            Ui.Box(row, new Vector2(700, 46), new Vector2(0, 108), Pal.Ink.A(0.65f), Art.Pill);
            Ui.Label(row, "出战好汉", 30, Pal.Hex("FFE08A"), new Vector2(0, 108), new Vector2(300, 46), TextAnchor.MiddleCenter, Fonts.Heavy);
            for (int i = 0; i < Game.Lineup.Length; i++)
            {
                var d = Defs.Hero(Game.Lineup[i]);
                float x = (i - 1.5f) * 170;
                var tile = Ui.Rect(row, "hero" + i);
                tile.anchoredPosition = new Vector2(x, -6);
                tile.sizeDelta = new Vector2(156, 156);
                Ui.Box(tile, new Vector2(156, 156), new Vector2(0, -6), Pal.Shadow, Art.Rounded);
                Ui.Box(tile, new Vector2(156, 156), Vector2.zero, Pal.Hex("2A2F38"), Art.Rounded);
                var face = Ui.Box(tile, new Vector2(146, 146), Vector2.zero, Pal.Hex("A7AFBC"), Art.Rounded);
                Ui.Box(tile, new Vector2(120, 120), new Vector2(0, 3), Pal.Hex("6A7385"), Art.Rounded);
                Ui.Icon(tile, d.Art, 128, new Vector2(0, 8));
                var nameBg = Ui.Box(tile, new Vector2(130, 40), new Vector2(0, -66), Pal.Edge.A(0.9f), Art.Pill);
                Ui.Label(tile, d.Name, 28, Color.white, new Vector2(0, -66), new Vector2(140, 40), TextAnchor.MiddleCenter, Fonts.Heavy);
                var role = Ui.Box(tile, new Vector2(64, 34), new Vector2(50, 60), RoleColor(d.Kind), Art.Pill);
                Ui.Outlined(role.transform, d.Role, 22, Color.white, Vector2.zero, new Vector2(64, 34), Pal.Ink, 1.5f);
                var hd = d;
                Ui.Tap(face, () => Ui.Toast($"{hd.Name}「{hd.Nick}」 大招：{hd.UltName}", 200), tile);
                Tween.Pop(tile, 0.3f, 0.15f + i * 0.06f);
            }
        }

        static Color RoleColor(Role r) => r == Role.Archer ? Pal.GreenDeep : r == Role.Bomber ? Pal.Hex("C0601A") : r == Role.Mage ? Pal.Hex("3C6CC0") : Pal.RedDeep;

        void BuildStart()
        {
            _start = Ui.Btn(_root, "开始挑战", new Vector2(520, 150), Vector2.zero, Pal.Hex("F6B83A"), () => Game.I.StartBattle(), 64);
            _start.anchorMin = _start.anchorMax = new Vector2(0.5f, 0);
            _start.anchoredPosition = new Vector2(0, 340);
            var glow = Ui.Box(_start, new Vector2(680, 260), Vector2.zero, Pal.Gold.A(0.35f), Art.SoftDot, "glow");
            glow.transform.SetAsFirstSibling();
            Tween.Run(_start, 9999f, k =>
            {
                float t = k * 9999f;
                _start.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(t * 4f));
                glow.color = Pal.Gold.A(0.25f + 0.15f * Mathf.Sin(t * 4f));
            });
            if (Profile.Clears > 0)
            {
                var badge = Ui.Box(_start, new Vector2(150, 48), new Vector2(200, 70), Pal.Green, Art.Pill);
                Ui.Outlined(badge.transform, "已通关", 28, Color.white, Vector2.zero, new Vector2(150, 48), Pal.GreenDeep, 2f);
            }
        }

        void BuildTabs()
        {
            var bar = Ui.Rect(_root, "tabs");
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(1, 0);
            bar.sizeDelta = new Vector2(0, 190);
            bar.anchoredPosition = new Vector2(0, 95);
            var bgi = Ui.Box(bar, Vector2.zero, Vector2.zero, Pal.Hex("1E2530").A(0.96f), Art.White);
            var brt = bgi.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.sizeDelta = Vector2.zero;
            Ui.Box(bar, new Vector2(1100, 6), new Vector2(0, 92), Pal.Hex("C9935A"), Art.White);
            string[] names = { "商店", "好汉", "出征", "山寨", "聚义厅" };
            string[] icons = { "ic_chest", "hero_wusong", "ic_crown", "ic_wall", "prop_drum" };
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 216;
                bool on = i == 2;
                var tab = Ui.Rect(bar, "tab" + i);
                tab.anchoredPosition = new Vector2(x, 0);
                tab.sizeDelta = new Vector2(216, 190);
                var face = Ui.Box(tab, new Vector2(208, 176), new Vector2(0, -2), on ? Pal.Hex("3B4A60") : new Color(0, 0, 0, 0), Art.Rounded);
                face.raycastTarget = true;
                var ic = Ui.Icon(tab, icons[i], on ? 120 : 96, new Vector2(0, on ? 26 : 18));
                Ui.Outlined(tab, names[i], on ? 36 : 30, on ? Pal.Hex("FFE08A") : Pal.Hex("C8CED8"), new Vector2(0, -58), new Vector2(210, 50), Pal.Ink, 2f);
                if (!on)
                {
                    ic.color = new Color(0.62f, 0.64f, 0.68f);
                    Ui.Icon(tab, "ic_lock", 50, new Vector2(54, 50));
                    string n = names[i];
                    Ui.Tap(face, () => Ui.Toast($"「{n}」在垂直切片验收后开放", 200), tab);
                }
            }
        }
    }
}
