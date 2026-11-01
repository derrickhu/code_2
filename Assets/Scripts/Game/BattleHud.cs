using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Liangshan.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Liangshan
{
    /// <summary>战斗界面：顶栏、墙血、底部召唤/强化、横幅、三选一、老虎机、暂停、统计、结算、新手引导。</summary>
    public sealed class BattleHud : MonoBehaviour
    {
        BattleView _v;
        Battle B => _v.B;
        RectTransform _root;
        float _st;

        Text _title, _wave, _timer, _speedText, _coins, _hpText, _leaderText;
        Text _sumLabel, _sumCost, _enhLabel, _enhCost;
        Image _hpFill, _hpGhost, _speedHex, _sumGlow, _enhGlow;
        RectTransform _coinPill, _sumBtn, _enhBtn, _hpBar, _wavePill;
        RectTransform _threat;
        Image _threatIcon;
        Text _threatText;
        RectTransform _bossBar;
        Image _bossFill;
        Text _bossHp;
        RectTransform _bubble;
        Text _bubbleText;
        float _bubbleT;
        RectTransform _info;
        float _infoT;
        float _shownHp = 1f, _ghostHp = 1f;
        int _shownCoins;
        Image[] _rage;
        float _rageT;

        RectTransform _modal;
        string _modalKind;
        float _reelsDelay;
        readonly Queue<Action> _banners = new Queue<Action>();
        /// <summary>横幅、波次字这类播报层；弹窗打开时整层隐藏，不透到弹窗上。</summary>
        RectTransform _announce;
        CanvasGroup _announceGroup;
        RectTransform _rageTag;
        float _bannerBusy;
        RectTransform _guide;
        int _guideStep = -1;

        public void PressSummon() => TrySummon();
        public void PressEnhance() => TryEnhance();
        public void ChooseCard(int i) => PickCard(i, _cards);
        public void OpenPanel(string kind) { if (kind == "pause") OpenPause(); else OpenStats(false); }
        public void ClosePanel() => CloseModal();
        List<RectTransform> _cards = new List<RectTransform>();

        public bool Blocking => _modal != null;
        public string ModalKind => _modal != null ? _modalKind : null;

        public static BattleHud Create(BattleView v)
        {
            var rt = Ui.Rect(Ui.Root, "BattleHud");
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var hud = rt.gameObject.AddComponent<BattleHud>();
            hud._v = v;
            hud._root = rt;
            hud.Build();
            return hud;
        }

        // ---------------- 搭建 ----------------

        void Build()
        {
            _st = Ui.SafeTop;
            BuildTop();
            BuildBottom();
            BuildRage();
            _announce = Ui.Rect(_root, "announce");
            _announce.anchorMin = Vector2.zero;
            _announce.anchorMax = Vector2.one;
            _announce.offsetMin = _announce.offsetMax = Vector2.zero;
            _announceGroup = _announce.gameObject.AddComponent<CanvasGroup>();
            _announceGroup.blocksRaycasts = false;
            _shownCoins = B.Coins;
            Banner(CloudConfig.GameName, B.Stage.Intro, Pal.TealDeep, null);
        }

        RectTransform TopRt(float x, float y, Vector2 size, string name) => Ui.Top(_root, x, y + _st, size, name);

        void BuildTop()
        {
            var title = TopRt(0, 62, new Vector2(700, 80), "title");
            _title = Ui.Outlined(title, B.Stage.Title, 52, Color.white, Vector2.zero, new Vector2(700, 80), Pal.Ink, 3.5f);
            _wavePill = TopRt(0, 128, new Vector2(270, 56), "wave");
            Ui.Box(_wavePill, new Vector2(270, 56), Vector2.zero, Pal.SlateDeep.A(0.75f), Art.Pill);
            _wave = Ui.Label(_wavePill, $"波次：1/{B.Stage.Waves}", 34, Pal.Cream, new Vector2(0, 1), new Vector2(270, 56));
            if (B.Stage.BuffRole.HasValue)
            {
                var buff = TopRt(-272, 128, new Vector2(226, 50), "buff");
                Ui.Box(buff, new Vector2(226, 46), Vector2.zero, Pal.Hex("2E5A2A").A(0.85f), Art.Pill);
                Ui.Outlined(buff, B.Stage.BuffText, 24, Pal.Hex("C8FFB0"), new Vector2(0, 1), new Vector2(222, 46), Pal.Ink, 1.5f);
            }

            // 狂暴后挂在波次下面的红色提示条
            _rageTag = TopRt(0, 186, new Vector2(300, 52), "rageTag");
            Ui.Box(_rageTag, new Vector2(300, 48), new Vector2(0, -3), Pal.Shadow, Art.Pill);
            Ui.Box(_rageTag, new Vector2(300, 48), Vector2.zero, Pal.Hex("5A1410"), Art.Pill);
            Ui.Box(_rageTag, new Vector2(290, 38), Vector2.zero, Pal.Hex("C5352A"), Art.Pill);
            Ui.Icon(_rageTag, "ic_rage", 62, new Vector2(-122, 6));
            Ui.Outlined(_rageTag, "敌军进入狂暴", 28, Color.white, new Vector2(18, 1), new Vector2(240, 44), Pal.Hex("5A1410"), 2f);
            _rageTag.gameObject.SetActive(false);

            // 左列：暂停、倍速、统计
            var pause = TopRt(-462, 82, new Vector2(100, 100), "pause");
            Ui.Box(pause, new Vector2(104, 104), new Vector2(0, -4), Pal.Shadow, Art.Hex6);
            Ui.Box(pause, new Vector2(100, 100), Vector2.zero, Pal.Hex("C9CFD8"), Art.Hex6);
            var pbg = Ui.Box(pause, new Vector2(88, 88), Vector2.zero, Pal.Hex("2E3540"), Art.Hex6);
            Ui.Box(pause, new Vector2(14, 40), new Vector2(-12, 0), Pal.Cream, Art.RoundedSmall);
            Ui.Box(pause, new Vector2(14, 40), new Vector2(12, 0), Pal.Cream, Art.RoundedSmall);
            Ui.Tap(pbg, OpenPause, pause);
            _timer = Ui.Outlined(TopRt(-462, 150, new Vector2(160, 50), "timer"), "0:00", 32, Color.white, Vector2.zero, new Vector2(160, 50), Pal.Ink, 2.5f);

            var speed = TopRt(-462, 240, new Vector2(110, 110), "speed");
            Ui.Box(speed, new Vector2(112, 112), new Vector2(0, -4), Pal.Shadow, Art.Hex6);
            _speedHex = Ui.Box(speed, new Vector2(104, 104), Vector2.zero, Pal.Hex("8A8F98"), Art.Hex6);
            _speedText = Ui.Outlined(speed, "×1", 38, Color.white, new Vector2(0, 2), new Vector2(110, 60), Pal.Ink, 2.5f);
            Ui.Tap(_speedHex, ToggleSpeed, speed);

            var stats = TopRt(-462, 352, new Vector2(100, 120), "stats");
            var sbg = Ui.Box(stats, new Vector2(88, 88), new Vector2(0, 12), Pal.SlateDeep.A(0.82f), Art.Rounded);
            for (int i = 0; i < 3; i++)
                Ui.Box(stats, new Vector2(14, 22 + i * 14), new Vector2(-20 + i * 20, 2 + i * 7), Pal.Cream, Art.RoundedSmall);
            Ui.Outlined(stats, "统计", 28, Color.white, new Vector2(0, -48), new Vector2(120, 40), Pal.Ink, 2f);
            Ui.Tap(sbg, () => OpenStats(false), stats);

            // 右列：下一个强敌
            _threat = TopRt(462, 90, new Vector2(120, 160), "threat");
            Ui.Box(_threat, new Vector2(108, 108), new Vector2(0, 14), Pal.Edge, Art.Rounded);
            Ui.Box(_threat, new Vector2(96, 96), new Vector2(0, 14), Pal.Hex("4E7BA6"), Art.Rounded);
            _threatIcon = Ui.Icon(_threat, Defs.Enemy(B.Stage.BossKind).Art, 92, new Vector2(0, 16));
            _threatText = Ui.Outlined(_threat, "第4波", 28, Color.white, new Vector2(0, -62), new Vector2(160, 40), Pal.Ink, 2f);

            // 首领血条
            _bossBar = TopRt(0, 266, new Vector2(700, 90), "bossBar");
            Ui.Box(_bossBar, new Vector2(600, 44), new Vector2(40, -6), Pal.Edge, Art.Pill);
            Ui.Box(_bossBar, new Vector2(588, 32), new Vector2(40, -6), Pal.Hex("3A1F1A"), Art.Pill);
            _bossFill = Ui.Box(_bossBar, new Vector2(588, 32), new Vector2(40, -6), Pal.Red, Art.Pill);
            _bossFill.rectTransform.pivot = new Vector2(0, 0.5f);
            _bossFill.rectTransform.anchoredPosition = new Vector2(40 - 294, -6);
            Ui.Box(_bossBar, new Vector2(104, 104), new Vector2(-292, 0), Pal.Edge, Art.Circle);
            Ui.Box(_bossBar, new Vector2(92, 92), new Vector2(-292, 0), Pal.RedDeep, Art.Circle);
            Ui.Icon(_bossBar, Defs.Enemy(B.Stage.BossKind).Art, 92, new Vector2(-292, 4));
            Ui.Outlined(_bossBar, "首领 " + B.Stage.BossName, 30, Pal.Hex("FFE2A0"), new Vector2(-120, 30), new Vector2(300, 40), Pal.Ink, 2.5f, TextAnchor.MiddleLeft);
            _bossHp = Ui.Outlined(_bossBar, "", 26, Color.white, new Vector2(40, -6), new Vector2(580, 40), Pal.Ink, 2f);
            _bossBar.gameObject.SetActive(false);
        }

        const float HpW = 924f, HpH = 34f;

        /// <summary>贴在填充条上的横条（随填充一起伸缩）：inset 为左右总内缩，y 为离中心的偏移。</summary>
        static void Strip(Transform parent, float inset, float h, float y, Color c)
        {
            var rt = Ui.Box(parent, new Vector2(0, h), new Vector2(0, y), c, Art.RoundedSmall).rectTransform;
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(inset, h);
            rt.anchoredPosition = new Vector2(0, y);
        }

        void BuildBottom()
        {
            // 墙血条：贴着墙脚，按世界坐标换算。
            _hpBar = Ui.Bottom(_root, 0, Ui.FromBottom(-6.72f), new Vector2(920, 50), "hp");
            // 参考：细深色描边 + 内凹暗槽（上沿压一道阴影），填充是小圆角的亮绿条，上半一道浅色高光、下沿一道暗边。
            Ui.Box(_hpBar, new Vector2(948, 58), new Vector2(0, -4), Color.black.A(0.35f), Art.RoundedSmall);
            Ui.Box(_hpBar, new Vector2(948, 58), Vector2.zero, Pal.Hex("14171C"), Art.RoundedSmall);
            Ui.Box(_hpBar, new Vector2(936, 46), Vector2.zero, Pal.Hex("2A2F37"), Art.RoundedSmall);
            Ui.Box(_hpBar, new Vector2(930, 10), new Vector2(0, 16), Color.black.A(0.35f), Art.RoundedSmall);
            _hpGhost = Ui.Box(_hpBar, new Vector2(HpW, HpH), Vector2.zero, Pal.Hex("F4ECD6"), Art.RoundedSmall);
            _hpFill = Ui.Box(_hpBar, new Vector2(HpW, HpH), Vector2.zero, Pal.Hex("45C531"), Art.RoundedSmall);
            Strip(_hpFill.transform, -10, 7, -12, Color.black.A(0.18f));
            Strip(_hpFill.transform, -12, 11, 7, new Color(0.85f, 1f, 0.6f, 0.42f));
            foreach (var img in new[] { _hpGhost, _hpFill })
            {
                img.rectTransform.pivot = new Vector2(0, 0.5f);
                img.rectTransform.anchoredPosition = new Vector2(-HpW * 0.5f, 0);
            }
            var heart = Ui.Icon(_hpBar, "ic_heart", 44, new Vector2(300, 46));
            _hpText = Ui.Outlined(_hpBar, "6000", 36, Color.white, new Vector2(400, 46), new Vector2(160, 50), Pal.Ink, 2.5f, TextAnchor.MiddleLeft);
            _hpText.rectTransform.anchoredPosition = new Vector2(410, 46);
            heart.rectTransform.anchoredPosition = new Vector2(305, 46);

            // 银两：参考里压在血条左下方，和血条之间留一道缝。
            _coinPill = Ui.Bottom(_root, -452, Ui.FromBottom(-7.5f), new Vector2(180, 70), "coins");
            Ui.Box(_coinPill, new Vector2(156, 54), new Vector2(12, 0), Pal.Hex("1E1712"), Art.Pill);
            Ui.Box(_coinPill, new Vector2(146, 44), new Vector2(12, 0), Pal.Hex("C08A52"), Art.Pill);
            Ui.Box(_coinPill, new Vector2(138, 36), new Vector2(12, -1), Pal.Hex("4E3420"), Art.Pill);
            Ui.Box(_coinPill, new Vector2(64, 64), new Vector2(-62, 0), Color.white, Art.Coin);
            _coins = Ui.Outlined(_coinPill, "100", 34, Color.white, new Vector2(28, 1), new Vector2(120, 56), Pal.Ink, 2.5f);

            // 底部按钮：位置跟着世界里的道具（离屏幕底边固定距离）。
            _enhBtn = BottomButton(-290, "强化", out _enhLabel, out _enhCost, out _enhGlow, () => TryEnhance(), _v.Book);
            _sumBtn = BottomButton(290, "召唤", out _sumLabel, out _sumCost, out _sumGlow, () => TrySummon(), _v.Drum);

            var leader = Ui.Bottom(_root, 0, 40, new Vector2(150, 56), "leader");
            Ui.Box(leader, new Vector2(150, 56), Vector2.zero, Pal.SlateDeep.A(0.85f), Art.Pill);
            _leaderText = Ui.Outlined(leader, "0/5", 34, Color.white, new Vector2(0, 1), new Vector2(150, 56), Pal.Ink, 2f);
            var leaderHit = Ui.Bottom(_root, 0, 150, new Vector2(240, 240), "leaderHit").gameObject.AddComponent<Image>();
            leaderHit.color = new Color(0, 0, 0, 0);
            Ui.Tap(leaderHit, () =>
            {
                Tween.Punch(_v.Leader, 0.2f, 0.1f);
                Say(B.Slots.Count >= Layout.MaxSlots ? "城墙已扩到最大" : $"再召唤 {B.SummonsToNextSlot} 次\n就能扩建城墙");
            }, null, false);

            _bubble = Ui.Bottom(_root, 0, 290, new Vector2(420, 120), "bubble");
            Ui.Box(_bubble, new Vector2(420, 112), new Vector2(0, 0), Pal.Edge, Art.Rounded);
            Ui.Box(_bubble, new Vector2(408, 100), new Vector2(0, 0), Pal.Cream, Art.Rounded);
            var tail = Ui.Box(_bubble, new Vector2(30, 30), new Vector2(0, -56), Pal.Cream, Art.RoundedSmall);
            tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            _bubbleText = Ui.Label(_bubble, "", 34, Pal.Ink, new Vector2(0, 2), new Vector2(400, 100), TextAnchor.MiddleCenter, Fonts.Heavy);
            _bubble.gameObject.SetActive(false);
        }

        RectTransform BottomButton(float x, string label, out Text lbl, out Text cost, out Image glow, Action onTap, Transform prop)
        {
            var hit = Ui.Bottom(_root, x, 150, new Vector2(300, 260), label + "Hit");
            var hitImg = hit.gameObject.AddComponent<Image>();
            hitImg.color = new Color(0, 0, 0, 0);
            var b = Ui.Bottom(_root, x, 46, new Vector2(250, 92), label);
            glow = Ui.Box(b, new Vector2(330, 150), new Vector2(0, 10), Pal.Gold.A(0), Art.SoftDot, "glow");
            // 参考：深棕木牌 + 细铜边，左侧压一枚银两，上名下价。
            Ui.Box(b, new Vector2(232, 86), new Vector2(0, -6), Pal.Shadow, Art.RoundedSmall);
            Ui.Box(b, new Vector2(232, 86), Vector2.zero, Pal.Hex("17110C"), Art.RoundedSmall);
            Ui.Box(b, new Vector2(222, 76), Vector2.zero, Pal.Hex("A87A48"), Art.RoundedSmall);
            Ui.Box(b, new Vector2(214, 68), Vector2.zero, Pal.Hex("3A2A1D"), Art.RoundedSmall);
            Ui.Box(b, new Vector2(206, 26), new Vector2(0, 17), Pal.Hex("4E3A28"), Art.RoundedSmall);
            lbl = Ui.Outlined(b, label, 34, Pal.Hex("FFE6A6"), new Vector2(18, 17), new Vector2(200, 44), Pal.Ink, 2f);
            Ui.Box(b, new Vector2(80, 80), new Vector2(-112, -4), Color.white, Art.Coin);
            cost = Ui.Outlined(b, "0", 32, Color.white, new Vector2(18, -18), new Vector2(200, 40), Pal.Ink, 2f);
            Ui.Tap(hitImg, () =>
            {
                if (prop) Tween.Punch(prop, 0.18f, 0.08f);
                onTap();
            }, b, false);
            return b;
        }

        void BuildRage()
        {
            _rage = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                var img = Ui.Box(_root, new Vector2(1080, 260), Vector2.zero, Pal.Red.A(0), Art.VGrad, "rage");
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0, i == 0 ? 1 : 0);
                rt.anchorMax = new Vector2(1, i == 0 ? 1 : 0);
                rt.sizeDelta = new Vector2(0, 260);
                rt.anchoredPosition = new Vector2(0, i == 0 ? -130 : 130);
                if (i == 0) rt.localRotation = Quaternion.Euler(0, 0, 180);
                img.transform.SetAsFirstSibling();
                _rage[i] = img;
            }
        }

        // ---------------- 操作 ----------------

        void TrySummon()
        {
            if (Blocking || B.Phase != Phase.Running) return;
            if (!B.HasEmptySlot())
            {
                Sfx.Play(Snd.Deny);
                Say("城墙站满了\n拖动相同好汉合成");
                return;
            }
            if (B.Coins < B.SummonCost)
            {
                Sfx.Play(Snd.Deny);
                Tween.Punch(_coinPill, 0.2f, 0.15f);
                Ui.Toast("银两不足", 260);
                return;
            }
            B.Summon();
            if (_guideStep == 0) NextGuide();
        }

        void TryEnhance()
        {
            if (Blocking || B.Phase != Phase.Running) return;
            if (B.FreeEnhances <= 0 && B.Coins < B.EnhanceCost)
            {
                Sfx.Play(Snd.Deny);
                Tween.Punch(_coinPill, 0.2f, 0.15f);
                Ui.Toast("银两不足", 260);
                return;
            }
            if (!B.Enhance())
            {
                Ui.Toast("所有强化都已拿满", 260);
                return;
            }
            Sfx.Play(Snd.Card);
            if (_guideStep == 2) NextGuide();
        }

        void ToggleSpeed()
        {
            if (Profile.S.Speed15 == 0 && _v.Auto == null)
            {
                Ui.Toast("首次通关后解锁 1.5 倍速", 200);
                return;
            }
            _v.Speed = _v.Speed > 1.01f ? 1f : 1.5f;
            PlayerPrefs.SetInt("speed15", _v.Speed > 1.01f ? 1 : 0);
        }

        // ---------------- 每帧 ----------------

        void Update()
        {
            if (_v == null || B == null) return;
            float dt = Time.unscaledDeltaTime;
            _wave.text = $"波次：{Mathf.Max(1, B.Wave)}/{B.Stage.Waves}";
            int sec = Mathf.FloorToInt(B.Time);
            _timer.text = $"{sec / 60}:{sec % 60:00}";
            bool fast = _v.Speed > 1.01f;
            _speedText.text = fast ? "×1.5" : "×1";
            _speedHex.color = fast ? Pal.Orange : Pal.Hex("8A8F98");

            _shownCoins = (int)Mathf.MoveTowards(_shownCoins, B.Coins, Mathf.Max(1, Mathf.Abs(B.Coins - _shownCoins) * 8f * dt + 1));
            _coins.text = Ui.Num(_shownCoins);

            float hp = B.WallMax > 0 ? Mathf.Clamp01(B.Wall / B.WallMax) : 0;
            _shownHp = Mathf.MoveTowards(_shownHp, hp, dt * 2f);
            _ghostHp = _ghostHp < _shownHp ? _shownHp : Mathf.MoveTowards(_ghostHp, _shownHp, dt * 0.35f);
            _hpFill.rectTransform.sizeDelta = new Vector2(HpW * Mathf.Max(0.03f, _shownHp), HpH);
            _hpGhost.rectTransform.sizeDelta = new Vector2(HpW * Mathf.Max(0.03f, _ghostHp), HpH);
            _hpFill.color = hp > 0.5f ? Pal.Hex("45C531") : hp > 0.25f ? Pal.Hex("E8B830") : Pal.Hex("E2483A");
            _hpText.text = Mathf.CeilToInt(B.Wall).ToString();

            bool canSum = B.Phase == Phase.Running && B.Coins >= B.SummonCost && B.HasEmptySlot();
            _sumCost.text = Ui.Num(B.SummonCost);
            _sumCost.color = B.Coins >= B.SummonCost ? Color.white : Pal.Hex("FF6E5E");
            _sumGlow.color = Pal.Gold.A(canSum ? 0.45f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 0);
            if (B.FreeEnhances > 0)
            {
                _enhCost.text = $"免费×{B.FreeEnhances}";
                _enhCost.color = Pal.Hex("8CFF7A");
            }
            else
            {
                _enhCost.text = Ui.Num(B.EnhanceCost);
                _enhCost.color = B.Coins >= B.EnhanceCost ? Color.white : Pal.Hex("FF6E5E");
            }
            bool canEnh = B.Phase == Phase.Running && (B.FreeEnhances > 0 || B.Coins >= B.EnhanceCost);
            _enhGlow.color = Pal.Hex("7FE0FF").A(canEnh ? 0.4f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f + 1f) : 0);
            _leaderText.text = B.Slots.Count >= Layout.MaxSlots ? "已满" : $"{Defs.SummonsPerSlot - B.SummonsToNextSlot}/{Defs.SummonsPerSlot}";

            UpdateThreat();
            UpdateBubble(dt);
            if (_info && ((_infoT -= dt) <= 0 || _infoSlot?.Unit == null)) CloseInfo();
            if (_rageT > 0)
            {
                float a = 0.5f + 0.18f * Mathf.Sin(Time.unscaledTime * 4f);
                foreach (var r in _rage) r.color = Pal.Red.A(a);
            }
            if (_bannerBusy > 0) _bannerBusy -= dt;
            else if (_banners.Count > 0 && !Blocking) _banners.Dequeue()();
            _announceGroup.alpha = Mathf.MoveTowards(_announceGroup.alpha, Blocking ? 0 : 1, dt * 8f);
            UpdatePhase(dt);
            UpdateGuide();
        }

        void UpdateThreat()
        {
            int w = B.Wave;
            string icon = null;
            int at = 0;
            for (int x = Mathf.Max(1, w); x <= B.Stage.Waves && icon == null; x++)
            {
                bool now = x == w;
                if (x == B.Stage.BossWave && (!now || B.Time - (w - 1) * Defs.WaveTime < 2.5f)) { icon = Defs.Enemy(B.Stage.BossKind).Art; at = x; }
                else if (Array.IndexOf(B.Stage.EliteWaves, x) >= 0 && (!now || B.Time - (w - 1) * Defs.WaveTime < 6f)) { icon = Defs.Enemy(B.EliteKind(x)).Art; at = x; }
                else if (Array.IndexOf(B.Stage.PorterWaves, x) >= 0 && (!now || B.Time - (w - 1) * Defs.WaveTime < 3f)) { icon = "en_porter"; at = x; }
            }
            if (icon == null)
            {
                _threat.gameObject.SetActive(false);
                return;
            }
            _threat.gameObject.SetActive(true);
            _threatIcon.sprite = Art.Img(icon);
            _threatText.text = at <= w ? "来了!" : $"第{at}波";
        }

        void UpdateBubble(float dt)
        {
            if (_bubbleT <= 0) return;
            _bubbleT -= dt;
            if (_bubbleT <= 0) _bubble.gameObject.SetActive(false);
        }

        public void Say(string s, float dur = 2.2f)
        {
            _bubbleText.text = s;
            _bubble.gameObject.SetActive(true);
            _bubble.SetAsLastSibling();
            _bubble.localScale = Vector3.one;
            Tween.Pop(_bubble, 0.25f);
            _bubbleT = dur;
        }

        // ---------------- 视图回调 ----------------

        public void OnSummon()
        {
            Tween.Punch(_sumBtn, 0.15f, 0.08f);
        }

        public void OnSlotAdded()
        {
            Say("兵团壮大\n扩建城墙！");
        }

        public void OnWallHit()
        {
            Tween.Punch(_hpBar, 0.12f, 0.025f);
        }

        public void PunchCoins() => Tween.Punch(_coinPill, 0.12f, 0.08f);

        public void OnWave(int w)
        {
            Tween.Punch(_wavePill, 0.25f, 0.2f);
            if (w == 1) return;
            Sfx.Play(Snd.Wave, 0.5f);
            var t = Ui.Outlined(_announce, $"第 {w} 波", 84, Pal.Hex("FFE08A"), Vector2.zero, new Vector2(600, 120), Pal.Ink, 4f);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -(_st + 420));
            Tween.Pop(rt, 0.3f);
            Tween.Run(t, 1.3f, k => t.color = t.color.A(k < 0.7f ? 1 : 1 - (k - 0.7f) / 0.3f), () => Destroy(t.gameObject));
        }

        public void Rage()
        {
            _rageT = 999f;
            if (_rageTag.gameObject.activeSelf) return;
            _rageTag.gameObject.SetActive(true);
            Tween.Pop(_rageTag, 0.35f);
        }

        public void SyncBoss(Enemy boss)
        {
            bool on = boss != null;
            if (_bossBar.gameObject.activeSelf != on)
            {
                _bossBar.gameObject.SetActive(on);
                if (on) Tween.Pop(_bossBar, 0.3f);
                else Sfx.PlayMusic(Bgm.Battle);
            }
            if (!on) return;
            float f = Mathf.Clamp01(boss.Hp / boss.MaxHp);
            _bossFill.rectTransform.sizeDelta = new Vector2(588 * Mathf.Max(0.03f, f), 32);
            _bossHp.text = $"{Ui.Num(Mathf.Max(0, boss.Hp))} / {Ui.Num(boss.MaxHp)}";
        }

        /// <summary>横幅：深色长条从右滑入，停一下再滑出。</summary>
        public void Banner(string title, string sub, Color accent, string icon, bool small = false)
        {
            _banners.Enqueue(() => ShowBanner(title, sub, accent, icon, small));
        }

        void ShowBanner(string title, string sub, Color accent, string icon, bool small)
        {
            _bannerBusy = 1.5f;
            var band = Ui.Rect(_announce, "banner");
            band.anchorMin = band.anchorMax = new Vector2(0.5f, 1f);
            band.sizeDelta = new Vector2(1080, small ? 170 : 220);
            band.anchoredPosition = new Vector2(0, -(_st + 640));
            var group = band.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Ui.Box(band, new Vector2(1200, small ? 170 : 220), Vector2.zero, new Color(0.06f, 0.04f, 0.03f, 0.78f), Art.White);
            Ui.Box(band, new Vector2(1200, 8), new Vector2(0, (small ? 85 : 110) - 4), accent, Art.White);
            Ui.Box(band, new Vector2(1200, 8), new Vector2(0, -(small ? 85 : 110) + 4), accent, Art.White);
            var content = Ui.Rect(band, "content");
            content.sizeDelta = band.sizeDelta;
            float tx = 0;
            if (icon != null)
            {
                Ui.Icon(content, icon, small ? 150 : 200, new Vector2(-330, 6));
                tx = 70;
            }
            Ui.Outlined(content, title, small ? 76 : 100, accent == Pal.TealDeep ? Pal.Hex("FFE9A8") : Pal.Hex("FFE08A"), new Vector2(tx, small ? 18 : 26), new Vector2(800, 130), accent.Mul(0.5f), 4.5f);
            Ui.Label(content, sub, small ? 32 : 36, Pal.Cream, new Vector2(tx, small ? -46 : -62), new Vector2(800, 50));
            Tween.Run(band, 1.5f, k =>
            {
                float x = k < 0.18f ? Mathf.Lerp(1100, 0, Tween.OutCubic(k / 0.18f)) : k > 0.85f ? Mathf.Lerp(0, -1100, Tween.InCubic((k - 0.85f) / 0.15f)) : 0;
                content.anchoredPosition = new Vector2(x, 0);
                group.alpha = k < 0.1f ? k / 0.1f : k > 0.9f ? (1 - k) / 0.1f : 1;
            }, () => Destroy(band.gameObject));
        }

        public void ChestDrop(Vector2 world)
        {
            _reelsDelay = 1.0f;
            var fx = WorldFx.I;
            fx.Burst(Art.SoftDot, world + new Vector2(0, 0.5f), 2.5f, 0.6f, 9500, Pal.Gold.A(0.9f));
            fx.Fly(Art.Img("ic_chest"), world + new Vector2(0, 0.4f), new Vector2(0, 1.2f), 1.2f, 0.85f, 1.5f);
            fx.Sparks(Art.Img("fx_star"), world + new Vector2(0, 0.4f), 10, 0.35f, 3.5f, Color.white, 0.6f, 9600, 2f);
            Sfx.Play(Snd.Jackpot, 0.5f);
        }

        Slot _infoSlot;
        const float InfoDur = 6f;

        /// <summary>
        /// 参考视频：点中好汉，顶部铺满一块深蓝信息面板（立绘卡 + 攻击力 + 定位 + 技能名/冷却/描述 + 六边形技能槽），
        /// 地面同时亮出攻击范围圈、所在格变蓝。点技能槽切换说明，点空地或超时收起。
        /// </summary>
        public void SlotInfo(Slot s, int tab = 0)
        {
            var u = s.Unit;
            if (u == null) return;
            bool fresh = _info == null || _infoSlot != s;
            if (_info) Destroy(_info.gameObject);
            _infoSlot = s;
            var d = u.Def;
            const float W = 1050, H = 318;
            _info = TopRt(0, 196, new Vector2(W, H), "info");
            Ui.Fill(_info, Color.clear, true, "eat");
            Ui.Box(_info, new Vector2(W, H), new Vector2(0, -8), Pal.Shadow, Art.Rounded);
            Ui.Box(_info, new Vector2(W, H), Vector2.zero, Pal.Hex("0E1622"), Art.Rounded);
            Ui.Box(_info, new Vector2(W - 10, H - 10), Vector2.zero, Pal.Hex("4B5E78"), Art.Rounded);
            Ui.Box(_info, new Vector2(W - 16, H - 16), Vector2.zero, Pal.Hex("1C2A3D"), Art.Rounded);

            // 立绘卡
            var starCol = Pal.Star[u.Star - 1];
            var card = new Vector2(-392, 0);
            Ui.Box(_info, new Vector2(232, 286), card + new Vector2(0, -4), Pal.Hex("0B1119"), Art.Rounded);
            Ui.Box(_info, new Vector2(226, 278), card, Pal.Hex("8E9BAC"), Art.Rounded);
            Ui.Box(_info, new Vector2(214, 266), card, Pal.Hex("B7C2CF"), Art.Rounded);
            Ui.Box(_info, new Vector2(214, 10), card + new Vector2(0, 128), starCol, Art.Pill);
            Ui.Box(_info, new Vector2(170, 40), card + new Vector2(0, -38), new Color(0, 0, 0, 0.12f), Art.Pill);
            Ui.Icon(_info, d.Art, 190, card + new Vector2(0, 26));
            for (int i = 0; i < u.Star; i++)
                Ui.Icon(_info, "ic_star", 34, card + new Vector2((i - (u.Star - 1) * 0.5f) * 30, -58));
            Ui.Box(_info, new Vector2(214, 62), card + new Vector2(0, -102), Pal.Hex("DDE3EA"), Art.Rounded);
            Ui.Label(_info, d.Name, 38, Pal.Hex("2B3442"), card + new Vector2(0, -102), new Vector2(214, 60), TextAnchor.MiddleCenter, Fonts.Heavy);

            // 第一行：攻击力 + 定位
            float atk = B.AtkPower(d.Id, u.Star);
            var row = new Vector2(132, 116);
            Ui.Box(_info, new Vector2(750, 62), row, Pal.Hex("121C2A"), Art.Rounded);
            Ui.Icon(_info, "ic_sword", 46, row + new Vector2(-330, 0));
            Ui.Label(_info, Ui.Num(atk), 38, Color.white, row + new Vector2(-200, 0), new Vector2(200, 56), TextAnchor.MiddleLeft, Fonts.Heavy);
            Ui.Box(_info, new Vector2(54, 54), row + new Vector2(90, 0), Pal.Hex("3D6FB8"), Art.Circle);
            Ui.Icon(_info, d.AtkIcon, 40, row + new Vector2(90, 0));
            Ui.Label(_info, $"{d.Role} · {d.RarityName}", 32, Pal.Hex("D7E4F5"), row + new Vector2(240, 2), new Vector2(240, 56), TextAnchor.MiddleLeft, Fonts.Heavy);
            Ui.Box(_info, new Vector2(150, 3), row + new Vector2(194, -22), Pal.Hex("D7E4F5").A(0.7f), Art.White);

            // 技能块
            var sk = Skill(u, tab);
            var blk = new Vector2(132, -38);
            Ui.Box(_info, new Vector2(750, 228), blk, Pal.Hex("121C2A"), Art.Rounded);
            Ui.Outlined(_info, sk.Name, 36, sk.Locked ? Pal.Hex("8E9BAC") : Color.white, blk + new Vector2(-170, 80), new Vector2(380, 50), Pal.Ink, 2f, TextAnchor.MiddleLeft);
            string cd = sk.Cd <= 0 ? "" : sk.Cut > 0.05f
                ? $"冷却：<color=#FF9A3A>{sk.Cd:0.0#}s</color><color=#7DE35A>-{sk.Cut:0.0#}s</color>"
                : $"冷却：<color=#FF9A3A>{sk.Cd:0.0#}s</color>";
            Ui.Label(_info, cd, 28, Pal.Hex("D7E4F5"), blk + new Vector2(200, 80), new Vector2(330, 44), TextAnchor.MiddleRight, Fonts.Heavy);
            Ui.Box(_info, new Vector2(716, 2), blk + new Vector2(0, 54), Color.white.A(0.1f), Art.White);
            Ui.Label(_info, sk.Desc, 27, Pal.Hex("D7E0EC"), blk + new Vector2(0, 0), new Vector2(712, 100), TextAnchor.UpperLeft);

            // 六边形技能槽：普攻 / 大招 / 被动 / 强化
            string[] icons = { d.AtkIcon, d.UltIcon, "ic_star", "ic_scroll" };
            for (int i = 0; i < 4; i++)
            {
                var info = Skill(u, i);
                var hp = new Vector2(112 + i * 124, -150);
                bool sel = i == tab;
                var edge = Ui.Box(_info, new Vector2(118, 118), hp, sel ? Pal.Hex("FFD24A") : Pal.Hex("0B1119"), Art.Hex6, "hex" + i);
                edge.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                var face = Ui.Box(_info, new Vector2(104, 104), hp, info.Locked ? Pal.Hex("3A4656") : sel ? Pal.Hex("2F79C9") : Pal.Hex("2A4C78"), Art.Hex6);
                face.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                var icon = Ui.Icon(_info, icons[i], 64, hp);
                if (info.Locked)
                {
                    icon.color = new Color(0.45f, 0.5f, 0.58f, 0.6f);
                    Ui.Icon(_info, "ic_lock", 50, hp + new Vector2(0, -4));
                }
                if (sel) Ui.Box(_info, new Vector2(34, 10), hp + new Vector2(0, 66), Pal.Hex("FFD24A"), Art.Pill);
                if (info.Lv > 0)
                {
                    Ui.Box(_info, new Vector2(36, 36), hp + new Vector2(42, -40), Pal.Hex("0B1119"), Art.Circle);
                    Ui.Label(_info, info.Lv.ToString(), 26, Color.white, hp + new Vector2(42, -40), new Vector2(36, 36), TextAnchor.MiddleCenter, Fonts.Heavy);
                }
                int k = i;
                Ui.Tap(edge, () => SlotInfo(s, k), edge.transform);
            }

            if (fresh) Tween.Pop(_info, 0.2f);
            _infoT = InfoDur;
            _v.ShowRange(s, InfoDur);
            _v.Select(s.Index);
        }

        struct SkillText
        {
            public string Name, Desc;
            public float Cd, Cut;
            public bool Locked;
            public int Lv;
        }

        static string O(object s) => $"<color=#FF9A3A>{s}</color>";

        SkillText Skill(Unit u, int tab)
        {
            var d = u.Def;
            var h = d.Id;
            var t = new SkillText();
            float spd = B.HeroSpeedMul(h);
            if (tab == 0)
            {
                float s = spd * Defs.StarSpeed[u.Star - 1];
                t.Name = d.AtkName;
                t.Cd = d.Interval;
                t.Cut = d.Interval - d.Interval / s;
                t.Lv = u.Star;
                t.Desc = HeroText.Atk(d, B) + "\n<color=#8E9BAC>" + HeroText.Tip(d) + "</color>";
            }
            else if (tab == 1)
            {
                float cd = B.UltCd(h);
                t.Name = d.UltName;
                t.Cd = d.UltCd;
                t.Cut = d.UltCd - cd / spd;
                t.Locked = !B.HasUlt(h);
                t.Lv = t.Locked ? 0 : 1;
                t.Desc = HeroText.Ult(d, B.Lo.UltPow[(int)h] * (1f + B.Stat(h, Eff.UltPow)));
            }
            else if (tab == 2)
            {
                t.Name = d.PassiveName;
                t.Locked = !B.Lo.Passive[(int)h];
                t.Desc = HeroText.Passive(d) + (t.Locked ? "\n<color=#8E9BAC>好汉升到 5 级解锁</color>" : "");
            }
            else
            {
                t.Name = "专属强化";
                var sb = new System.Text.StringBuilder();
                foreach (var p in Defs.Perks)
                {
                    int lv = B.Level(p.Id);
                    if (lv <= 0 || p.Hero != h) continue;
                    if (sb.Length > 0) sb.Append("  ");
                    sb.Append(p.Star ? "<color=#FFD24A>★</color>" : "").Append(p.Name).Append(O(" Lv" + lv));
                    t.Lv += lv;
                }
                t.Locked = t.Lv == 0;
                t.Desc = t.Locked ? "<color=#8E9BAC>强化三选一中选到他的专属强化后，在这里查看；合成满星可额外选一个满星技能</color>" : sb.ToString();
            }
            return t;
        }

        // ---------------- 状态切换 ----------------

        void UpdatePhase(float dt)
        {
            if (_reelsDelay > 0) _reelsDelay -= dt;
            if (_modal != null) return;
            switch (B.Phase)
            {
                case Phase.Choosing: OpenCards(); break;
                case Phase.Reels: if (_reelsDelay <= 0) OpenReels(); break;
                case Phase.Victory: OpenResult(true); break;
                case Phase.Defeat: OpenResult(false); break;
            }
        }

        RectTransform OpenModal(string kind, float dim = 0.7f)
        {
            CloseInfo();
            _modalKind = kind;
            var m = Ui.Rect(_root, "modal_" + kind);
            m.anchorMin = Vector2.zero;
            m.anchorMax = Vector2.one;
            m.offsetMin = m.offsetMax = Vector2.zero;
            var bg = Ui.Fill(m, new Color(0, 0, 0, dim), true, "dim");
            var g = m.gameObject.AddComponent<CanvasGroup>();
            Tween.Fade(g, 0, 1, 0.18f);
            _modal = m;
            var c = Ui.Rect(m, "content");
            c.sizeDelta = new Vector2(1080, 1920);
            return c;
        }

        void CloseModal(Action after = null)
        {
            var m = _modal;
            _modal = null;
            _modalKind = null;
            if (m == null) { after?.Invoke(); return; }
            var g = m.GetComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            Tween.Fade(g, 1, 0, 0.15f, () => Destroy(m.gameObject));
            after?.Invoke();
        }

        public bool InfoOpen => _info != null;

        public void CloseInfo()
        {
            if (_info == null) return;
            Destroy(_info.gameObject);
            _info = null;
            _infoSlot = null;
            _v.HideRange();
            _v.Select(-1);
        }

        static readonly Regex NumRx = new Regex(@"[+\-×]?\d+(\.\d+)?%?");

        static string Colorize(string s) => NumRx.Replace(s, m => m.Value.StartsWith("-")
            ? $"<color=#D8402E>{m.Value}</color>"
            : $"<color=#2E9A38>{m.Value}</color>");

        static string PerkIcon(PerkDef p) => Defs.Hero(p.Hero).Art;

        int Recommend(PerkDef[] c)
        {
            int best = 0;
            float bs = -1;
            for (int i = 0; i < c.Length; i++)
            {
                var p = c[i];
                float s = 1;
                foreach (var sl in B.Slots)
                    if (sl.Unit != null && sl.Unit.Hero == p.Hero) s += sl.Unit.Star;
                if (p.Eff == Eff.Dmg || p.Eff == Eff.Count || p.Eff == Eff.UltPow) s += 0.5f;
                if (s > bs) { bs = s; best = i; }
            }
            return best;
        }

        void OpenCards()
        {
            var c = OpenModal("cards", 0.72f);
            Sfx.Play(Snd.Card);
            bool star = B.StarPick.HasValue;
            if (star)
            {
                Sfx.Play(Snd.Jackpot, 0.7f);
                Ui.Box(c, new Vector2(900, 380), new Vector2(0, 560), Pal.Gold.A(0.4f), Art.SoftDot);
                Ui.Icon(c, Defs.Hero(B.StarPick.Value).Art, 230, new Vector2(0, 640));
            }
            var head = star
                ? Ui.Outlined(c, $"恭喜获得满星{Defs.Hero(B.StarPick.Value).Name}！\n请选择1个额外技能", 50, Pal.Hex("FFE27A"), new Vector2(0, 450), new Vector2(980, 140), Pal.Ink, 3f)
                : Ui.Outlined(c, "请选择", 64, Color.white, new Vector2(0, 470), new Vector2(400, 90), Pal.Ink, 3f);
            for (int s = -1; s <= 1 && !star; s += 2)
            {
                Ui.Box(c, new Vector2(220, 6), new Vector2(s * 290, 470), Pal.Hex("A9D6E6"), Art.Pill);
                var dia = Ui.Box(c, new Vector2(24, 24), new Vector2(s * 175, 470), Pal.Hex("A9D6E6"), Art.RoundedSmall);
                dia.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            }
            Tween.Pop(head.transform, 0.3f);
            var cards = new List<RectTransform>();
            _cards = cards;
            BuildCards(c, cards);

            var watch = Ui.Label(c, "", 32, Pal.Cream, new Vector2(0, -330), new Vector2(600, 50));
            RectTransform reroll = null;
            Action refresh = () =>
            {
                int left = B.RerolledOnce ? 0 : 1;
                watch.text = $"本局剩余观看次数 {left}/1";
                if (reroll) reroll.gameObject.SetActive(left > 0 && !star);
                watch.gameObject.SetActive(!star);
            };
            reroll = Ui.Btn(c, "刷新", new Vector2(300, 104), new Vector2(0, -425), Pal.Teal, () =>
            {
                if (B.RerolledOnce) return;
                FakeAd(() =>
                {
                    B.Reroll();
                    foreach (var x in cards) if (x) Destroy(x.gameObject);
                    cards.Clear();
                    BuildCards(c, cards);
                    refresh();
                });
            }, 46, "ic_ad");
            refresh();
            if (_v.Auto != null) Tween.Run(c, 1.4f, k => { }, () => { if (_modalKind == "cards") PickCard(AutoPick(), cards); });
        }

        int AutoPick()
        {
            return Recommend(B.Choices);
        }

        void BuildCards(RectTransform c, List<RectTransform> cards)
        {
            var ch = B.Choices;
            int rec = Recommend(ch);
            for (int i = 0; i < ch.Length; i++)
            {
                var p = ch[i];
                float x = (i - (ch.Length - 1) * 0.5f) * 336f;
                var card = Ui.Rect(c, "card" + i);
                card.sizeDelta = new Vector2(310, 500);
                card.anchoredPosition = new Vector2(x, 70);
                var hd = Defs.Hero(p.Hero);
                var roleC = Pal.Hex(Defs.RoleColors[(int)hd.Kind]);
                var headC = p.Star ? Pal.Hex("D9A21E") : roleC.Mul(0.85f);
                var edgeC = p.Star ? Pal.GoldDeep : Pal.Hex("2E3D52");
                if (p.Star) Ui.Box(card, new Vector2(370, 560), Vector2.zero, Pal.Gold.A(0.55f), Art.SoftDot, "aura");
                Ui.Box(card, new Vector2(318, 508), new Vector2(0, -8), Pal.Shadow, Art.Rounded);
                var face = Ui.Box(card, new Vector2(318, 508), Vector2.zero, edgeC, Art.Rounded);
                Ui.Box(card, new Vector2(302, 492), Vector2.zero, Pal.Hex("F6EEDC"), Art.Rounded);
                Ui.Box(card, new Vector2(302, 84), new Vector2(0, 204), headC, Art.Rounded);
                Ui.Box(card, new Vector2(302, 30), new Vector2(0, 177), headC, Art.White);
                Ui.Outlined(card, p.Name, 42, Color.white, new Vector2(0, 206), new Vector2(300, 70), headC.Mul(0.45f), 2.5f);
                Ui.Box(card, new Vector2(210, 60), new Vector2(0, -5), new Color(0, 0, 0, 0.12f), Art.SoftDot);
                Ui.Icon(card, PerkIcon(p), 200, new Vector2(0, 75));
                string who = $"{hd.Name} · {hd.Role}";
                Ui.Label(card, who, 28, Pal.Hex("8A6A48"), new Vector2(0, -32), new Vector2(280, 40), TextAnchor.MiddleCenter, Fonts.Heavy);
                Ui.Label(card, Colorize(p.Desc), 32, Pal.Hex("3A2C20"), new Vector2(0, -122), new Vector2(280, 130));
                if (p.Max > 1)
                    Ui.Label(card, $"等级 {B.Level(p.Id)}→{B.Level(p.Id) + 1}", 24, Pal.Hex("9A8670"), new Vector2(0, -205), new Vector2(280, 34));
                var gemC = p.Star ? Pal.Hex("D9A21E") : Pal.Hex("8FA6BF");
                var gem = Ui.Box(card, new Vector2(18, 18), new Vector2(0, -238), gemC, Art.RoundedSmall);
                gem.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                Ui.Box(card, new Vector2(240, 3), new Vector2(0, -238), gemC.A(0.5f), Art.White);
                var tag = Ui.Box(card, new Vector2(110, 50), new Vector2(118, 252), p.Star ? Pal.Red : roleC.Mul(0.7f), Art.Pill);
                tag.rectTransform.localRotation = Quaternion.Euler(0, 0, -12);
                Ui.Outlined(tag.transform, p.Star ? "满星" : "专属", 30, Color.white, Vector2.zero, new Vector2(110, 50), Pal.Ink, 2f);
                if (i == rec)
                {
                    var r = Ui.Rect(card, "recommend");
                    r.sizeDelta = new Vector2(170, 70);
                    r.anchoredPosition = new Vector2(90, -292);
                    Ui.Icon(r, "ic_thumb", 66, new Vector2(-46, 4));
                    Ui.Outlined(r, "推荐", 36, Pal.Hex("FFC23A"), new Vector2(26, 0), new Vector2(110, 60), Pal.Hex("6A2E06"), 2.5f);
                    Tween.Run(r, 99f, k => r.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(k * 99f * 5f)));
                }
                int idx = i;
                Ui.Tap(face, () => PickCard(idx, cards), card);
                Tween.Pop(card, 0.32f, 0.05f + i * 0.07f);
                cards.Add(card);
            }
        }

        void PickCard(int i, List<RectTransform> cards)
        {
            if (_modalKind != "cards" || B.Phase != Phase.Choosing) return;
            _modalKind = "picking";
            var p = B.Choices[i];
            Sfx.Play(Snd.Merge, 0.7f, 1.1f);
            for (int k = 0; k < cards.Count; k++)
            {
                var card = cards[k];
                if (k == i)
                {
                    Tween.Run(card, 0.35f, t => card.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t * Mathf.PI)));
                }
                else
                {
                    var g = card.gameObject.AddComponent<CanvasGroup>();
                    Tween.Fade(g, 1, 0, 0.2f);
                }
            }
            Tween.Run(_root, 0.4f, k => { }, () =>
            {
                CloseModal();
                B.Pick(i);
                Ui.Toast((p.Star ? "满星技能：" : "获得：") + p.Name, -200);
            });
        }

        // ---------------- 老虎机 ----------------

        static string ReelIcon(Reel r) => r == Reel.Silver ? "ic_silver" : r == Reel.Book ? "prop_book" : r == Reel.Banner ? "prop_drum" : "ic_wall";
        static string ReelName(Reel r) => r == Reel.Silver ? "银两" : r == Reel.Book ? "免费强化" : r == Reel.Banner ? "援兵" : "修墙";

        void OpenReels()
        {
            var c = OpenModal("reels", 0.75f);
            var panel = Ui.Panel(c, new Vector2(940, 760), new Vector2(0, 30), "reelPanel");
            Ui.Icon(c, "ic_chest_open", 260, new Vector2(0, 480));
            Ui.Ribbon(c, "智取生辰纲", new Vector2(0, 370), 600);
            Ui.Label(panel, B.NextIsJackpot ? "击败首领，必中四格相同！" : "四格相同 奖励翻倍！", 34, Pal.Hex("8A5A2A"), new Vector2(0, 230), new Vector2(800, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
            var icons = new Image[4];
            var names = new Text[4];
            var windows = new RectTransform[4];
            var order = new[] { Reel.Silver, Reel.Book, Reel.Banner, Reel.Wall };
            for (int i = 0; i < 4; i++)
            {
                float x = -315 + i * 210;
                var w = Ui.Rect(panel, "reel" + i);
                w.anchoredPosition = new Vector2(x, 40);
                w.sizeDelta = new Vector2(190, 250);
                Ui.Box(w, new Vector2(192, 252), Vector2.zero, Pal.Edge, Art.Rounded);
                Ui.Box(w, new Vector2(176, 236), Vector2.zero, Pal.Hex("FFF8E8"), Art.Rounded);
                Ui.Box(w, new Vector2(176, 50), new Vector2(0, 93), new Color(0, 0, 0, 0.08f), Art.Rounded);
                Ui.Box(w, new Vector2(176, 50), new Vector2(0, -93), new Color(0, 0, 0, 0.08f), Art.Rounded);
                icons[i] = Ui.Icon(w, ReelIcon(order[i]), 140, new Vector2(0, 10));
                names[i] = Ui.Label(w, "", 30, Pal.Ink, new Vector2(0, -92), new Vector2(180, 44), TextAnchor.MiddleCenter, Fonts.Heavy);
                windows[i] = w;
            }
            var result = Ui.Label(panel, "", 34, Pal.Hex("3A2C20"), new Vector2(0, -170), new Vector2(860, 90));
            RectTransform btn = null;
            bool spun = false;
            Reel[] r = null;
            Action collect = () =>
            {
                if (_modalKind != "reels") return;
                _modalKind = "collecting";
                CloseModal(() => B.ApplyReels(r));
                Sfx.Play(Snd.Coin, 0.8f);
            };
            Action spin = null;
            spin = () =>
            {
                if (spun) { collect(); return; }
                spun = true;
                btn.gameObject.SetActive(false);
                r = B.SpinReels();
                SpinAnim(icons, names, windows, r, () =>
                {
                    bool all = r[0] == r[1] && r[1] == r[2] && r[2] == r[3];
                    result.text = Summary(r, all);
                    if (all) Jackpot(c);
                    Ui.SetBtnText(btn, "收下");
                    btn.gameObject.SetActive(true);
                    Tween.Pop(btn, 0.25f);
                    if (_v.Auto != null) Tween.Run(c, 1.4f, k => { }, collect);
                });
            };
            btn = Ui.Btn(c, "开转", new Vector2(360, 120), new Vector2(0, -420), Pal.Orange, () => spin(), 54);
            Tween.Pop(panel, 0.3f);
            Sfx.Play(Snd.Jackpot, 0.4f);
            if (_v.Auto != null) Tween.Run(c, 0.9f, k => { }, () => spin());
        }

        string Summary(Reel[] r, bool all)
        {
            int mul = all ? 2 : 1;
            int silver = 0, books = 0, banners = 0, walls = 0;
            foreach (var x in r)
            {
                if (x == Reel.Silver) silver += B.ReelSilver * mul;
                else if (x == Reel.Book) books++;
                else if (x == Reel.Banner) banners += mul;
                else walls += 500 * mul;
            }
            var parts = new List<string>();
            if (silver > 0) parts.Add($"银两 <color=#2E9A38>+{silver}</color>");
            if (books > 0) parts.Add($"免费强化 <color=#2E9A38>+{books}</color>");
            if (banners > 0) parts.Add($"援兵 <color=#2E9A38>+{banners}</color>" + (all ? "（二星）" : ""));
            if (walls > 0) parts.Add($"寨墙 <color=#2E9A38>+{walls}</color>");
            return string.Join("   ", parts);
        }

        void SpinAnim(Image[] icons, Text[] names, RectTransform[] windows, Reel[] r, Action done)
        {
            float[] stopAt = { 0.9f, 1.25f, 1.6f, 1.95f };
            var stopped = new bool[4];
            float tick = 0;
            var owner = icons[0];
            Tween.Run(owner, 2.1f, k =>
            {
                float t = k * 2.1f;
                tick -= Time.unscaledDeltaTime;
                bool doTick = tick <= 0;
                if (doTick) tick = 0.06f;
                for (int i = 0; i < 4; i++)
                {
                    if (stopped[i]) continue;
                    if (t >= stopAt[i])
                    {
                        stopped[i] = true;
                        icons[i].sprite = Art.Img(ReelIcon(r[i]));
                        icons[i].rectTransform.anchoredPosition = new Vector2(0, 10);
                        names[i].text = ReelName(r[i]);
                        Tween.Punch(windows[i], 0.2f, 0.12f);
                        Sfx.Play(Snd.ReelStop, 0.8f);
                        continue;
                    }
                    if (doTick)
                    {
                        icons[i].sprite = Art.Img(ReelIcon((Reel)UnityEngine.Random.Range(0, 4)));
                        names[i].text = "";
                        if (i == 0) Sfx.Play(Snd.Reel, 0.35f);
                    }
                    icons[i].rectTransform.anchoredPosition = new Vector2(0, 10 + Mathf.Repeat(t * 1400f, 60f) - 30f);
                }
            }, done);
        }

        void Jackpot(RectTransform c)
        {
            Sfx.Play(Snd.Jackpot);
            var rays = Ui.Box(c, new Vector2(900, 900), new Vector2(0, 40), Pal.Gold.A(0.5f), Art.SoftDot);
            rays.transform.SetAsFirstSibling();
            var t = Ui.Outlined(c, "超爽!!!", 130, Pal.Hex("FFE15A"), new Vector2(0, 300), new Vector2(800, 180), Pal.Hex("A0360E"), 6f);
            t.rectTransform.localRotation = Quaternion.Euler(0, 0, 8);
            Tween.Pop(t.transform, 0.35f);
            Confetti(c, 40);
        }

        void Confetti(RectTransform parent, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var col = new[] { Pal.Gold, Pal.Red, Pal.Teal, Pal.Blue, Pal.Green, Color.white }[UnityEngine.Random.Range(0, 6)];
                var img = Ui.Box(parent, new Vector2(18, 30), Vector2.zero, col, Art.RoundedSmall, "confetti");
                var rt = img.rectTransform;
                var p0 = new Vector2(UnityEngine.Random.Range(-520f, 520f), UnityEngine.Random.Range(700f, 1100f));
                float spd = UnityEngine.Random.Range(500f, 900f), sway = UnityEngine.Random.Range(30f, 90f), rot = UnityEngine.Random.Range(200f, 700f);
                float delay = UnityEngine.Random.Range(0f, 0.8f);
                Tween.Run(img, 2.6f, k =>
                {
                    float t = k * 2.6f;
                    rt.anchoredPosition = p0 + new Vector2(Mathf.Sin(t * 4f + p0.x) * sway, -spd * t);
                    rt.localRotation = Quaternion.Euler(0, 0, rot * t);
                }, () => { if (img) Destroy(img.gameObject); }, delay);
            }
        }

        // ---------------- 暂停 / 统计 ----------------

        void OpenPause()
        {
            if (Blocking || B.Phase != Phase.Running) return;
            var c = OpenModal("pause");
            var panel = Ui.Panel(c, new Vector2(720, 780), new Vector2(0, 0));
            Ui.Ribbon(c, "暂停", new Vector2(0, 390), 420);
            Ui.Label(panel, $"{B.Stage.Title} · 第{Mathf.Max(1, B.Wave)}/{B.Stage.Waves}波", 34, Pal.Hex("6B5440"), new Vector2(0, 240), new Vector2(640, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Btn(panel, "继续战斗", new Vector2(460, 112), new Vector2(0, 120), Pal.Green, () => CloseModal(), 46);
            RectTransform snd = null;
            snd = Ui.Btn(panel, Sfx.On ? "音效：开" : "音效：关", new Vector2(460, 112), new Vector2(0, -10), Pal.Blue, () =>
            {
                Sfx.On = !Sfx.On;
                Sfx.Music = Sfx.On;
                Sfx.PlayMusic(Sfx.On ? Bgm.Battle : Bgm.None);
                Ui.SetBtnText(snd, Sfx.On ? "音效：开" : "音效：关");
            }, 42);
            Ui.Btn(panel, "重新开始", new Vector2(460, 112), new Vector2(0, -140), Pal.Orange, () => { CloseModal(); Game.I.Restart(); }, 42);
            Ui.Btn(panel, "返回主页", new Vector2(460, 112), new Vector2(0, -270), Pal.Red, () => { CloseModal(); Game.I.ShowHome(); }, 42);
            Tween.Pop(panel, 0.25f);
        }

        void OpenStats(bool fromResult)
        {
            if (Blocking) return;
            var c = OpenModal("stats");
            var panel = Ui.Panel(c, new Vector2(880, 820), Vector2.zero);
            Ui.Ribbon(c, "输出统计", new Vector2(0, 410), 520);
            StatRows(panel, new Vector2(0, 245), 780, true);
            int sec = Mathf.FloorToInt(B.Time);
            Ui.Label(panel, $"击杀 {B.Kills}    用时 {sec / 60}:{sec % 60:00}    合成 {_v.MergesDone} 次", 32, Pal.Hex("6B5440"), new Vector2(0, -240), new Vector2(800, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Btn(panel, "关闭", new Vector2(300, 104), new Vector2(0, -350), Pal.Green, () => CloseModal(), 44);
            Tween.Pop(panel, 0.25f);
        }

        /// <summary>按输出从高到低列出好汉，第一名带 MVP。</summary>
        void StatRows(RectTransform parent, Vector2 top, float width, bool light)
        {
            var heroes = new List<HeroId>(B.Lineup);
            heroes.Sort((a, b) => B.DamageBy[(int)b].CompareTo(B.DamageBy[(int)a]));
            float max = Mathf.Max(1, B.DamageBy[(int)heroes[0]]);
            for (int i = 0; i < heroes.Count; i++)
            {
                var h = heroes[i];
                var d = Defs.Hero(h);
                var y = top.y - i * 118;
                Ui.Box(parent, new Vector2(116, 116), new Vector2(-width * 0.5f + 70, y), Pal.Edge, Art.Rounded);
                Ui.Box(parent, new Vector2(104, 104), new Vector2(-width * 0.5f + 70, y), Pal.Hex("8FB3D6"), Art.Rounded);
                Ui.Icon(parent, d.Art, 100, new Vector2(-width * 0.5f + 70, y + 2));
                if (i == 0 && B.DamageBy[(int)h] > 0)
                {
                    var mvp = Ui.Outlined(parent, "MVP", 30, Pal.Hex("FFD84A"), new Vector2(-width * 0.5f + 52, y + 52), new Vector2(100, 40), Pal.Hex("8A3A0A"), 2.5f);
                    mvp.rectTransform.localRotation = Quaternion.Euler(0, 0, 10);
                }
                var tc = light ? Pal.Ink : Pal.Cream;
                Ui.Label(parent, d.Name, 36, tc, new Vector2(-width * 0.5f + 230, y + 24), new Vector2(160, 50), TextAnchor.MiddleLeft, Fonts.Heavy);
                float bw = width - 400;
                Ui.Box(parent, new Vector2(bw, 28), new Vector2(-width * 0.5f + 150 + bw * 0.5f, y - 22), light ? Pal.Hex("D8C8A6") : Pal.Hex("1E1610"), Art.Pill);
                var fill = Ui.Box(parent, new Vector2(bw * Mathf.Max(0.03f, B.DamageBy[(int)h] / max), 28), new Vector2(-width * 0.5f + 150, y - 22), RoleColor(h), Art.Pill);
                fill.rectTransform.pivot = new Vector2(0, 0.5f);
                var full = fill.rectTransform.sizeDelta.x;
                fill.rectTransform.sizeDelta = new Vector2(28, 28);
                Tween.Run(fill, 0.6f, k => fill.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(28, full, Tween.OutCubic(k)), 28), null, 0.1f + i * 0.08f);
                Ui.Label(parent, Ui.Num(B.DamageBy[(int)h]), 36, tc, new Vector2(width * 0.5f - 110, y - 2), new Vector2(200, 50), TextAnchor.MiddleRight, Fonts.Heavy);
            }
        }

        static Color RoleColor(HeroId h) => MetaUi.RoleColor(Defs.Hero(h).Kind);

        // ---------------- 结算 ----------------

        float _resultDelay = -1;

        void OpenResult(bool win)
        {
            if (_resultDelay < 0) { _resultDelay = win ? 1.6f : 1.2f; return; }
            _resultDelay -= Time.unscaledDeltaTime;
            if (_resultDelay > 0) return;
            _resultDelay = -1;
            var c = OpenModal(win ? "victory" : "defeat", 0.6f);
            Sfx.PlayMusic(win ? Bgm.Victory : Bgm.Defeat);
            var band = Ui.Rect(c, "band");
            band.sizeDelta = new Vector2(1080, 1060);
            band.anchoredPosition = new Vector2(0, -10);
            Ui.Box(band, new Vector2(1200, 1060), Vector2.zero, (win ? Pal.Hex("2A5F8F") : Pal.Hex("46566A")).A(0.96f), Art.White);
            Ui.Box(band, new Vector2(1200, 10), new Vector2(0, 525), win ? Pal.Gold : Pal.Hex("A8B4C4"), Art.White);
            Ui.Box(band, new Vector2(1200, 10), new Vector2(0, -525), win ? Pal.Gold : Pal.Hex("A8B4C4"), Art.White);
            if (win) Ui.Box(c, new Vector2(1000, 700), new Vector2(0, 560), Pal.Gold.A(0.45f), Art.SoftDot).transform.SetSiblingIndex(0);
            var crown = Ui.Icon(c, win ? "ic_crown" : "ic_wall", 250, new Vector2(0, 660));
            if (!win) crown.color = Pal.Hex("B0A6A0");
            Tween.Pop(crown.transform, 0.4f, 0.1f);
            var rib = Ui.Ribbon(c, win ? "战斗胜利" : "寨墙失守", new Vector2(0, 520), 560, win ? Pal.Hex("E0A022") : Pal.Hex("7A6A64"));
            Tween.Pop(rib, 0.35f);
            float hpPct = B.WallMax > 0 ? B.Wall / B.WallMax * 100f : 0;
            Ui.Label(band, B.Stage.Title, 40, Pal.Hex("FFD87A"), new Vector2(0, 390), new Vector2(700, 56), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Label(band, win ? $"剩余血量：{Mathf.RoundToInt(hpPct)}%" : $"坚守到 第{Mathf.Max(1, B.Wave)}/{B.Stage.Waves}波", 34, Pal.Hex("D6E6F6"), new Vector2(0, 336), new Vector2(700, 50));

            SectionTitle(band, "输出统计", 260);
            var heroes = new List<HeroId>(B.Lineup);
            heroes.Sort((a, b) => B.DamageBy[(int)b].CompareTo(B.DamageBy[(int)a]));
            for (int i = 0; i < heroes.Count; i++)
            {
                float x = i % 2 == 0 ? -250 : 200;
                float y = 160 - (i / 2) * 140;
                var d = Defs.Hero(heroes[i]);
                Ui.Box(band, new Vector2(124, 124), new Vector2(x - 100, y), Color.white, Art.Rounded);
                Ui.Box(band, new Vector2(112, 112), new Vector2(x - 100, y), Pal.Hex("8FB3D6"), Art.Rounded);
                Ui.Icon(band, d.Art, 108, new Vector2(x - 100, y + 2));
                if (i == 0)
                {
                    var mvp = Ui.Outlined(band, "MVP", 32, Pal.Hex("FFD84A"), new Vector2(x - 122, y + 56), new Vector2(110, 44), Pal.Hex("8A3A0A"), 2.5f);
                    mvp.rectTransform.localRotation = Quaternion.Euler(0, 0, 10);
                }
                Ui.Label(band, Ui.Num(B.DamageBy[(int)heroes[i]]), 44, Color.white, new Vector2(x + 70, y + 14), new Vector2(220, 56), TextAnchor.MiddleLeft, Fonts.Heavy);
                Ui.Label(band, d.Name, 28, Pal.Hex("BFD4EA"), new Vector2(x + 70, y - 30), new Vector2(220, 40), TextAnchor.MiddleLeft);
            }

            if (win)
            {
                var r = Game.I.Settle(B, true);
                for (int i = 0; i < 3 && B.Stage.Mode == StageMode.Main; i++)
                {
                    var st = Ui.Icon(band, "ic_star", i == 1 ? 96 : 76, new Vector2((i - 1) * 110, 470 + (i == 1 ? 14 : 0)));
                    if (i >= r.Stars) st.color = new Color(0.16f, 0.26f, 0.38f, 0.9f);
                    Tween.Pop(st.transform, 0.3f, 0.5f + i * 0.15f);
                }
                if (r.First)
                {
                    var tag = Ui.Box(band, new Vector2(130, 48), new Vector2(300, 390), Pal.Red, Art.Pill);
                    Ui.Outlined(tag.transform, "首通", 28, Color.white, Vector2.zero, new Vector2(130, 48), Pal.RedDeep, 2f);
                }
                SectionTitle(band, B.Stage.Mode == StageMode.Escort ? $"截获生辰纲 {B.PortersKilled} 担" : "通关奖励", -110);
                MetaUi.ItemRow(band, r.Items, new Vector2(0, -230), 170);
                if (r.Unlock.HasValue)
                {
                    var ud = Defs.Hero(r.Unlock.Value);
                    var ub = Ui.Box(c, new Vector2(760, 150), new Vector2(0, -470), Pal.Hex("5A2A8A").A(0.95f), Art.Rounded);
                    Ui.Icon(ub.transform, ud.Art, 150, new Vector2(-280, 12));
                    Ui.Outlined(ub.transform, $"新好汉入伙：{ud.Name}", 42, Pal.Hex("FFE08A"), new Vector2(60, 24), new Vector2(560, 60), Pal.Ink, 3f);
                    Ui.Label(ub.transform, $"「{ud.Nick}」{ud.Role} · 可在好汉页上阵", 28, Pal.Cream, new Vector2(60, -30), new Vector2(560, 44));
                    Tween.Pop(ub.transform, 0.4f, 0.8f);
                }
                Confetti(c, 60);
                Sfx.Play(Snd.Jackpot, 0.6f);
                bool doubled = false;
                RectTransform dbl = null;
                dbl = Ui.Btn(c, "双倍领取", new Vector2(320, 124), new Vector2(-350, -640), Pal.Teal, () =>
                {
                    if (doubled) return;
                    FakeAd(() =>
                    {
                        doubled = true;
                        Game.I.GiveAgain(r);
                        Ui.SetBtnText(dbl, "已双倍");
                        Ui.Toast("奖励已翻倍", 200);
                        Sfx.Play(Snd.Jackpot, 0.6f);
                    });
                }, 40, "ic_ad");
                bool hasNext = B.Stage.Mode == StageMode.Main && B.Stage.No < Defs.LevelCount;
                Ui.Btn(c, "返回", new Vector2(260, 124), new Vector2(hasNext ? 0 : 300, -640), Pal.Hex("A08A70"), () => { CloseModal(); Game.I.ShowHome(); }, 46);
                if (hasNext)
                    Ui.Btn(c, "下一关", new Vector2(300, 124), new Vector2(330, -640), Pal.Hex("F2B632"), () => { CloseModal(); Game.I.StartLevel(B.Stage.No + 1); }, 46);
                if (_v.Auto != null) Tween.Run(c, 3f, k => { }, () => Game.I.OnAutoDone(true));
            }
            else
            {
                SectionTitle(band, "败因提示", -110);
                Ui.Label(band, Tip(), 34, Pal.Cream, new Vector2(0, -210), new Vector2(880, 120));
                var pre = Game.I.Preview(B, false);
                if (pre.Items.Count > 0)
                {
                    Ui.Label(band, "离开可得", 28, Pal.Hex("C8B8A0"), new Vector2(-340, -330), new Vector2(200, 40));
                    MetaUi.ItemRow(band, pre.Items, new Vector2(60, -330), 110);
                }
                void Leave(int tab)
                {
                    Game.I.Settle(B, false);
                    CloseModal();
                    Game.I.ShowHome(tab);
                }
                if (!B.RevivedOnce)
                    Ui.Btn(c, "复活再战", new Vector2(340, 124), new Vector2(-340, -640), Pal.Teal, () =>
                    {
                        FakeAd(() =>
                        {
                            CloseModal();
                            B.Revive();
                            Sfx.PlayMusic(Bgm.Battle);
                        });
                    }, 44, "ic_ad");
                Ui.Btn(c, "去养成", new Vector2(280, 124), new Vector2(B.RevivedOnce ? -170 : 0, -640), Pal.Hex("F2B632"), () => Leave(1), 44);
                Ui.Btn(c, "返回", new Vector2(260, 124), new Vector2(B.RevivedOnce ? 170 : 330, -640), Pal.Hex("A08A70"), () => Leave(2), 44);
                if (_v.Auto != null) Tween.Run(c, 3f, k => { }, () => { Game.I.Settle(B, false); Game.I.OnAutoDone(false); });
            }
            Tween.Pop(band, 0.3f);
        }

        void SectionTitle(RectTransform parent, string s, float y)
        {
            var d = Ui.Box(parent, new Vector2(20, 20), new Vector2(-440, y), Pal.Hex("C8A870"), Art.RoundedSmall);
            d.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            Ui.Label(parent, s, 36, Pal.Hex("EAF3FC"), new Vector2(-260, y), new Vector2(320, 50), TextAnchor.MiddleLeft, Fonts.Heavy);
            Ui.Box(parent, new Vector2(900, 3), new Vector2(0, y - 36), Pal.Hex("7FA6CC").A(0.6f), Art.White);
        }

        string Tip()
        {
            int best = 0;
            foreach (var s in B.Slots) if (s.Unit != null) best = Mathf.Max(best, s.Unit.Star);
            if (best < 3) return "多把相同好汉拖到一起合成\n三星、满星好汉伤害成倍增长";
            int ults = 0;
            foreach (var h in B.Lineup) if (B.HasUlt(h)) ults++;
            if (ults < 2) return "点书案「强化」解锁好汉大招\n大招清群、打首领最有效";
            return "首领来之前多攒合成\n狂暴波前记得加固寨墙";
        }

        // ---------------- 模拟广告 ----------------

        void FakeAd(Action done) => Ads.Show(done);

        // ---------------- 新手引导 ----------------

        void UpdateGuide()
        {
            if (!_v.Tutorial || _v.Auto != null) return;
            if (_guideStep < 0) { _guideStep = 0; ShowGuide(); return; }
            if (Blocking) { if (_guide) _guide.gameObject.SetActive(false); return; }
            if (_guide) _guide.gameObject.SetActive(true);
            switch (_guideStep)
            {
                case 1:
                    if (_v.MergesDone > 0) NextGuide();
                    else if (_guide == null && _v.FindMergePair(out _, out _)) ShowGuide();
                    else if (_guide != null) MoveMergeGhost();
                    break;
                case 2:
                    if (_guide == null && (B.Coins >= B.EnhanceCost || B.FreeEnhances > 0)) ShowGuide();
                    break;
            }
            TickHand();
        }

        void NextGuide()
        {
            if (_guide) Destroy(_guide.gameObject);
            _guide = null;
            _guideStep++;
            if (_guideStep > 2)
            {
                _v.Tutorial = false;
                Profile.S.TutorialDone = 1;
                Profile.Save();
                return;
            }
            if (_guideStep == 1 && _v.FindMergePair(out _, out _)) ShowGuide();
        }

        Image _ghost;
        float _ghostT;
        RectTransform _hand;
        Image _handRipple;
        Vector2 _handAt;
        float _handT;
        bool _handDrag;

        static RectTransform BottomRect(Transform parent, Vector2 pos, Vector2 size, string name)
        {
            var rt = Ui.Rect(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        static Vector2 WorldToBottomUi(Vector3 w) => new Vector2(w.x * 100f, Ui.FromBottom(w.y));

        void ShowGuide()
        {
            if (_guide) Destroy(_guide.gameObject);
            _guide = Ui.Rect(_root, "guide");
            _guide.anchorMin = Vector2.zero;
            _guide.anchorMax = Vector2.one;
            _guide.offsetMin = _guide.offsetMax = Vector2.zero;
            var cg = _guide.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.alpha = 0;
            Tween.Run(_guide, 0.35f, k => cg.alpha = k);
            if (_guideStep == 0 || _guideStep == 2)
            {
                bool sum = _guideStep == 0;
                var prop = WorldToBottomUi((sum ? _v.Drum : _v.Book).position);
                Spotlight(new Vector2(prop.x, prop.y - 20), 460);
                Hand(prop + new Vector2(6, -6), false);
                GuideText(sum ? "点战鼓，召唤好汉上城墙！" : "点书案强化，三选一\n解锁好汉大招！", new Vector2(sum ? 70 : -70, prop.y + 330), !sum);
            }
            else
            {
                _ghost = Ui.Box(_guide, new Vector2(120, 120), Vector2.zero, Color.white.A(0.85f), Art.Img("hero_huarong"));
                _ghost.preserveAspect = true;
                _ghost.rectTransform.anchorMin = _ghost.rectTransform.anchorMax = new Vector2(0.5f, 0);
                Hand(Vector2.zero, true);
                _ghostT = 0;
                GuideText("按住好汉，拖到相同好汉上\n合成升星，伤害翻倍！", new Vector2(0, Ui.FromBottom(-1.4f)), false);
            }
        }

        /// <summary>全屏压暗，只在 center 处留一个柔边圆洞。</summary>
        void Spotlight(Vector2 c, float d)
        {
            var dim = new Color(0.02f, 0.03f, 0.05f, 0.62f);
            float h = Ui.Height + 400f, r = d * 0.5f;
            var hole = BottomRect(_guide, c, Vector2.one * d, "hole").gameObject.AddComponent<Image>();
            hole.sprite = Art.Hole;
            hole.color = dim;
            hole.raycastTarget = false;
            void Block(Vector2 min, Vector2 max)
            {
                var img = BottomRect(_guide, (min + max) * 0.5f, max - min, "dim").gameObject.AddComponent<Image>();
                img.color = dim;
                img.raycastTarget = false;
            }
            Block(new Vector2(-1400, -200), new Vector2(c.x - r, h));
            Block(new Vector2(c.x + r, -200), new Vector2(1400, h));
            Block(new Vector2(c.x - r, c.y + r), new Vector2(c.x + r, h));
            Block(new Vector2(c.x - r, -200), new Vector2(c.x + r, c.y - r));
        }

        /// <summary>白手套：指尖对准 at。drag=true 时由合成引导每帧挪位置，不做点按动画。</summary>
        void Hand(Vector2 at, bool drag)
        {
            _handRipple = Ui.Box(_guide, new Vector2(170, 170), Vector2.zero, Color.white.A(0), Art.Ring, "ripple");
            _handRipple.rectTransform.anchorMin = _handRipple.rectTransform.anchorMax = new Vector2(0.5f, 0);
            var img = Ui.Icon(_guide, "ic_hand", 150, Vector2.zero, "hand");
            _hand = img.rectTransform;
            _hand.anchorMin = _hand.anchorMax = new Vector2(0.5f, 0);
            _hand.pivot = new Vector2(0.13f, 0.93f);
            _handAt = at;
            _handDrag = drag;
            _handT = 0;
            TickHand();
        }

        void TickHand()
        {
            if (_hand == null) return;
            _handT += Time.unscaledDeltaTime;
            if (_handDrag)
            {
                _hand.anchoredPosition = _handAt;
                _hand.localScale = Vector3.one * 0.92f;
                _handRipple.color = Color.white.A(0);
                return;
            }
            float t = Mathf.Repeat(_handT, 1.2f) / 1.2f;
            float press = t < 0.35f ? Tween.OutCubic(t / 0.35f) : t < 0.6f ? 1f : 1f - Tween.OutCubic((t - 0.6f) / 0.4f);
            _hand.anchoredPosition = _handAt + new Vector2(50, -50) * (1 - press);
            _hand.localScale = Vector3.one * (t > 0.35f && t < 0.6f ? 0.9f : 1f);
            float rr = t < 0.35f ? 0f : (t - 0.35f) / 0.65f;
            _handRipple.rectTransform.anchoredPosition = _handAt;
            _handRipple.rectTransform.localScale = Vector3.one * (0.25f + 0.9f * rr);
            _handRipple.color = Color.white.A(t < 0.35f ? 0 : (1 - rr) * 0.9f);
        }

        /// <summary>宋江头像 + 奶白对话框。</summary>
        void GuideText(string s, Vector2 bottomPos, bool portraitRight)
        {
            var box = BottomRect(_guide, bottomPos, new Vector2(620, 150), "tip");
            Ui.Box(box, new Vector2(620, 150), new Vector2(0, -6), Pal.Shadow, Art.Rounded);
            Ui.Box(box, new Vector2(620, 150), Vector2.zero, Pal.Edge, Art.Rounded);
            Ui.Box(box, new Vector2(606, 136), Vector2.zero, Pal.Cream, Art.Rounded);
            float side = portraitRight ? 1 : -1;
            var face = Ui.Icon(box, "prop_songjiang", 170, new Vector2(side * 270, 40));
            if (portraitRight) face.rectTransform.localScale = new Vector3(-1, 1, 1);
            var tag = Ui.Box(box, new Vector2(110, 40), new Vector2(side * 262, -58), Pal.Hex("B8432F"), Art.Pill);
            Ui.Outlined(tag.transform, "宋江", 26, Color.white, new Vector2(0, 1), new Vector2(110, 40), Pal.Hex("5A1A10"), 2f);
            Ui.Label(box, s, 36, Pal.Ink, new Vector2(-side * 60, 0), new Vector2(470, 130), TextAnchor.MiddleCenter, Fonts.Heavy);
            Tween.Pop(box, 0.3f);
        }

        void MoveMergeGhost()
        {
            if (_ghost == null || !_v.FindMergePair(out var a, out var b))
            {
                if (_ghost) _ghost.gameObject.SetActive(false);
                if (_hand) _hand.gameObject.SetActive(false);
                return;
            }
            bool show = !_v.Dragging;
            _ghost.gameObject.SetActive(show);
            _hand.gameObject.SetActive(show);
            var ua = B.Slots[0];
            foreach (var s in B.Slots) if (s.Pos.ToV() == a) ua = s;
            if (ua.Unit != null) _ghost.sprite = Art.Img(ua.Unit.Def.Art);
            _ghostT += Time.unscaledDeltaTime;
            float k = Mathf.Repeat(_ghostT, 1.8f);
            float move = Mathf.Clamp01((k - 0.35f) / 0.9f);
            var pa = new Vector2(a.x * 100f, Ui.FromBottom(a.y));
            var pb = new Vector2(b.x * 100f, Ui.FromBottom(b.y));
            var p = Vector2.Lerp(pa, pb, Tween.OutCubic(move)) + new Vector2(0, 30);
            _ghost.rectTransform.anchoredPosition = p;
            float alpha = k < 0.25f ? k / 0.25f : k > 1.45f ? 1 - (k - 1.45f) / 0.35f : 1f;
            _ghost.color = Color.white.A(0.8f * Mathf.Clamp01(alpha));
            _handAt = p + new Vector2(10, -40);
            _hand.GetComponent<Image>().color = Color.white.A(Mathf.Clamp01(alpha));
        }
    }
}
