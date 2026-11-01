using System.Collections.Generic;
using Liangshan.Sim;
using UnityEngine;

namespace Liangshan
{
    /// <summary>战场表现：寨墙格子上的好汉、官军、弹道、命中特效、拖拽合成。规则全在 Sim.Battle，这里只读状态、播事件。</summary>
    public sealed class BattleView : MonoBehaviour
    {
        public Battle B { get; private set; }
        public BattleHud Hud { get; private set; }
        public Bot Auto;
        public float Speed = 1f;
        public bool Paused;
        public bool Tutorial;

        /// <summary>屏幕底边的世界 y。9:16 时是 -9.6，更高的屏更低。</summary>
        public float Bottom { get; private set; }
        public float PropY => Bottom + 1.55f;
        public static readonly Vector2 CoinTarget = new Vector2(-4.98f, -7.08f);

        const float WallW = 11.8f;
        static readonly Vector2 TileSize = new Vector2(1.24f, 1.16f);

        Transform _world;
        SpriteRenderer _wall;
        Color _wallColor = Color.white;
        float _wallFlash;
        readonly List<SlotView> _slots = new List<SlotView>();
        readonly Dictionary<Enemy, EnemyView> _enemies = new Dictionary<Enemy, EnemyView>();
        readonly Dictionary<Shot, ShotView> _shots = new Dictionary<Shot, ShotView>();
        readonly List<Enemy> _gone = new List<Enemy>();
        readonly List<Shot> _goneShots = new List<Shot>();
        readonly HashSet<Shot> _liveShots = new HashSet<Shot>();
        public Transform Book, Leader, Drum;
        int _coinsFlying;
        float _hitSfxGap;

        // 拖拽
        int _dragFrom = -1;
        bool _pressing;
        Vector2 _pressPos;
        float _pressTime;
        public float LastMergeTime = -99f;
        public int MergesDone;

        public void Begin(int seed, LevelDef stage, Loadout lo, bool tutorial)
        {
            Tutorial = tutorial;
            B = new Battle(seed, lo.Lineup, stage, lo);
            Bottom = -9.6f;
            _world = new GameObject("World").transform;
            _world.SetParent(transform, false);
            BuildGround();
            FitField();
            foreach (var s in B.Slots) AddSlotView(s, false);
            Hud = BattleHud.Create(this);
        }

        // ---------------- 场景搭建 ----------------

        const float BgSeam = -5.57f;
        const float BgSeamFromTop = 0.79f;
        const float StretchPivot = -2.6f;
        SpriteRenderer _bg;
        float _fitOrtho, _fitAspect;

        /// <summary>
        /// 背景按屏幕铺满（以墙脚接缝为锚等比放大）。高屏背景跟着变高，城门上移，
        /// 所以停止线以上的官军在渲染时同比例拉长，出生点始终对准城门。
        /// </summary>
        /// <summary>背景图的版式（1080×1920 像素坐标）：墙脚接缝、两个门洞的中心 x、门洞宽、门楣和门槛的 y。</summary>
        readonly struct BgLayout
        {
            public readonly string Suffix;
            public readonly float Seam, DoorL, DoorR, DoorW, DoorTop, DoorBottom;
            public BgLayout(string suffix, float seam, float doorL, float doorR, float doorW, float doorTop, float doorBottom)
            { Suffix = suffix; Seam = seam; DoorL = doorL; DoorR = doorR; DoorW = doorW; DoorTop = doorTop; DoorBottom = doorBottom; }
        }

        static readonly BgLayout StdBg = new BgLayout("", BgSeamFromTop, 132, 938, 156, 92, 256);
        // 长屏专用图：城门楼画在中间，长屏按高度铺满时两侧被裁掉也看得到整座门楼。
        static readonly BgLayout LongBg = new BgLayout("_long", 0.8307f, 150, 918, 62, 260, 356);

        /// <summary>章节背景是单张 9:16 图，长屏、标准屏共用一套版式。</summary>
        static readonly System.Collections.Generic.Dictionary<string, BgLayout> ChapterBg = new System.Collections.Generic.Dictionary<string, BgLayout>
        {
            ["battle_bg_ch2"] = new BgLayout("", 0.842f, 164, 904, 61, 266, 356),
            ["battle_bg_ch3"] = new BgLayout("", 0.83f, 157, 910, 56, 270, 352),
            ["battle_bg_ch4"] = new BgLayout("", 0.825f, 151, 920, 60, 259, 350),
            ["battle_bg_ch5"] = new BgLayout("", 0.83f, 159, 912, 62, 259, 350),
        };

        readonly GateDoor[] _doors = new GateDoor[2];
        public float GateOpen => _doors[0] == null ? 0f : Mathf.Max(_doors[0].Openness, _doors[1].Openness);

        void FitField()
        {
            var cam = Game.Cam;
            _fitOrtho = cam.orthographicSize;
            _fitAspect = cam.aspect;
            float camTop = cam.transform.localPosition.y + cam.orthographicSize;
            float camW = cam.orthographicSize * 2f * cam.aspect;
            float HeightFor(BgLayout l) => Mathf.Max(19.2f, camW * 1920f / 1080f, (camTop - BgSeam) / l.Seam);
            var lay = ChapterBg.TryGetValue(B.Stage.Bg, out var chLay) ? chLay : HeightFor(StdBg) / 19.2f > 1.12f ? LongBg : StdBg;
            float h = HeightFor(lay);
            float w = h * 1080f / 1920f;
            _bg.sprite = Art.Img(B.Stage.Bg + lay.Suffix);
            float cy = BgSeam + (lay.Seam - 0.5f) * h;
            _bg.transform.localPosition = new Vector2(0, cy);
            _bg.transform.localScale = Vector3.one * h;
            Vector2 Px(float x, float y) => new Vector2((x / 1080f - 0.5f) * w, cy + (0.5f - y / 1920f) * h);

            // 出生点（规则坐标 GateX, SpawnY）对准门槛：纵向按 Stretch 拉，横向按 XStretch 往门洞方向张开。
            var sill = Px(lay.DoorR, lay.DoorBottom);
            float gate = sill.y + 0.12f;
            V2Ext.Pivot = StretchPivot;
            V2Ext.Stretch = (gate - StretchPivot) / (Layout.SpawnY - StretchPivot);
            V2Ext.XStretch = sill.x / Layout.GateX[1];

            float doorW = lay.DoorW / 1080f * w, doorH = (lay.DoorBottom - lay.DoorTop) / 1920f * h;
            for (int i = 0; i < 2; i++)
            {
                var bottom = Px(i == 0 ? lay.DoorL : lay.DoorR, lay.DoorBottom);
                if (_doors[i] == null) _doors[i] = new GateDoor(_world);
                _doors[i].Place(bottom, doorW, doorH);
            }
        }

        /// <summary>城门：两扇门以外侧门轴为轴往里推开（横向压扁 + 变暗），出兵时开，出完一阵子再关上。</summary>
        sealed class GateDoor
        {
            readonly Transform _root, _hl, _hr;
            readonly SpriteRenderer _l, _r;
            float _open, _until = -99f, _w;
            Vector2 _bottom;

            public GateDoor(Transform parent)
            {
                _root = new GameObject("gate").transform;
                _root.SetParent(parent, false);
                var leaf = Art.Img("gate_leaf");
                _hl = new GameObject("hingeL").transform;
                _hl.SetParent(_root, false);
                _hr = new GameObject("hingeR").transform;
                _hr.SetParent(_root, false);
                _l = WorldFx.Spr(_hl, leaf, Vector2.zero, 1, -90, null, "leafL");
                _r = WorldFx.Spr(_hr, leaf, Vector2.zero, 1, -90, null, "leafR");
                _r.flipX = true;
            }

            public void Place(Vector2 bottom, float w, float h)
            {
                _bottom = bottom;
                _w = w;
                _root.localPosition = bottom;
                var sz = _l.sprite.bounds.size;
                float half = w * 0.5f;
                _hl.localPosition = new Vector2(-half, 0);
                _hr.localPosition = new Vector2(half, 0);
                foreach (var (sr, side) in new[] { (_l, 1f), (_r, -1f) })
                {
                    sr.transform.localScale = new Vector3(half / sz.x, h / sz.y, 1);
                    sr.transform.localPosition = new Vector2(side * half * 0.5f, h * 0.5f);
                }
            }

            public void Open(float secs) => _until = Mathf.Max(_until, Time.time + secs);
            public float Openness => _open;

            public void Tick()
            {
                bool want = Time.time < _until;
                float prev = _open;
                _open = Mathf.MoveTowards(_open, want ? 1f : 0f, Time.deltaTime * (want ? 3.2f : 1.8f));
                float k = _open * _open * (3f - 2f * _open);
                float sx = Mathf.Lerp(1f, 0.14f, k);
                _hl.localScale = new Vector3(sx, 1, 1);
                _hr.localScale = new Vector3(sx, 1, 1);
                var c = Color.Lerp(Color.white, new Color(0.42f, 0.38f, 0.36f), k);
                _l.color = _r.color = c;
                if (prev > 0.02f && _open <= 0f)
                    WorldFx.I.Dust(_bottom + new Vector2(0, -0.05f), _w * 1.1f, DustCol.A(0.45f), 0.45f, 95);
            }
        }

        void BuildGround()
        {
            _bg = WorldFx.Spr(_world, Art.Img(B.Stage.Bg), Vector2.zero, 1, -100, null, "bg");
            const float camW = 22f;

            // 墙后石板地：从墙脚铺到屏幕底。
            float floorTop = -6.25f;
            float floorBottom = Bottom - 0.5f;
            WorldFx.Block(_world, Art.White, new Vector2(0, (floorTop + floorBottom) * 0.5f), new Vector2(camW + 1, floorTop - floorBottom), 40, Pal.Hex("4A525E"), "floor");
            var rng = new System.Random(7);
            int row = 0;
            for (float y = floorTop - 0.5f; y > floorBottom - 0.6f; y -= 0.98f, row++)
            {
                float off = row % 2 == 0 ? 0 : 0.6f;
                for (float x = -camW * 0.5f - 1.2f + off; x < camW * 0.5f + 1.2f; x += 1.2f)
                {
                    float v = 0.9f + (float)rng.NextDouble() * 0.16f;
                    WorldFx.Block(_world, Art.RoundedSmall, new Vector2(x, y), new Vector2(1.12f, 0.9f), 41, Pal.Hex("7C8594").Mul(v), "stone");
                    WorldFx.Block(_world, Art.RoundedSmall, new Vector2(x, y + 0.36f), new Vector2(1.0f, 0.1f), 41, Pal.Hex("9AA2AF").Mul(v), "stoneLit");
                }
            }
            var shade = WorldFx.Block(_world, Art.VGrad, new Vector2(0, floorTop - 0.55f), new Vector2(camW + 1, 1.1f), 42, new Color(0, 0, 0, 0.0f), "shade");
            shade.color = new Color(0, 0, 0, 0.45f);
            shade.flipY = true;

            // 寨墙：走道对齐基础格那一行。
            float wallH = WallW * 289f / 1252f;
            float wallTop = -5.25f + 0.46f * wallH;
            _wall = WorldFx.Spr(_world, Art.Img("wall"), new Vector2(0, wallTop - wallH * 0.5f), WallW, 100, null, "wall");
            WorldFx.Block(_world, Art.SoftDot, new Vector2(0, -6.5f), new Vector2(WallW * 1.1f, 0.8f), 95, new Color(0, 0, 0, 0.35f), "wallShadow");

            // 底座三件：强化书案、宋江、召唤战鼓。
            float py = PropY;
            Pedestal(new Vector2(-2.9f, py - 0.62f), 1.9f);
            Pedestal(new Vector2(2.9f, py - 0.62f), 1.9f);
            WorldFx.Block(_world, Art.SoftDot, new Vector2(0, py - 1.2f), new Vector2(2.8f, 0.7f), 58, new Color(0, 0, 0, 0.45f), "throneShadow");
            WorldFx.Block(_world, Art.RoundedSmall, new Vector2(0, py - 0.5f), new Vector2(2.2f, 1.5f), 60, Pal.Hex("2B313A"), "throneEdge");
            WorldFx.Block(_world, Art.RoundedSmall, new Vector2(0, py - 0.46f), new Vector2(2.08f, 1.38f), 61, Pal.Hex("B3BAC6"), "throne");
            WorldFx.Block(_world, Art.RoundedSmall, new Vector2(0, py - 1.08f), new Vector2(2.08f, 0.22f), 62, Pal.Hex("858D9A"), "throneBase");
            WorldFx.Block(_world, Art.RoundedSmall, new Vector2(0, py - 0.38f), new Vector2(1.76f, 1.0f), 62, Pal.Hex("5C7FA8"), "throneIn");
            WorldFx.Block(_world, Art.RoundedSmall, new Vector2(0, py - 0.38f), new Vector2(1.64f, 0.88f), 63, Pal.Hex("7FA6CE"), "throneInLit");
            Book = WorldFx.Spr(_world, Art.Img("prop_book"), new Vector2(-2.82f, py - 0.06f), 1.56f, 300, null, "book").transform;
            Leader = WorldFx.Spr(_world, Art.Img("prop_songjiang"), new Vector2(0, py + 0.1f), 1.85f, 301, null, "songjiang").transform;
            Drum = WorldFx.Spr(_world, Art.Img("prop_drum"), new Vector2(2.9f, py + 0.05f), 1.85f, 300, null, "drum").transform;
        }

        SpriteRenderer _range;
        LineRenderer _rangeRim, _rangeGlow, _rangeHi;
        float _rangeT, _rangeShown;
        const int RangeSegs = 120;

        LineRenderer RangeLine(string name, float width, Color c, int order, bool add)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_range.transform.parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = add ? WorldFx.AddMat : new Material(Shader.Find("Sprites/Default"));
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = RangeSegs;
            lr.widthMultiplier = width;
            lr.startColor = lr.endColor = c;
            lr.sortingOrder = order;
            lr.numCornerVertices = 2;
            return lr;
        }

        static void SetCircle(LineRenderer lr, Vector2 c, float r)
        {
            for (int i = 0; i < RangeSegs; i++)
            {
                float a = i * Mathf.PI * 2f / RangeSegs;
                lr.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0));
            }
        }

        /// <summary>选中好汉时在地面画攻击范围圈。远程好汉打到 EngageY，近战按 Range 从停止线算。</summary>
        public void ShowRange(Slot s, float secs)
        {
            if (s.Unit == null) return;
            var d = s.Unit.Def;
            var c = s.Pos.ToV();
            float reach = d.Kind == Role.Fighter ? Layout.StopY + d.Range : Layout.EngageY;
            float r = new V2(0, reach).ToV().y - c.y;
            if (_range == null)
            {
                // 参考：圈内一层暖黄，圈边一条清晰的亮橙描边 + 外侧柔光。
                _range = WorldFx.Spr(_world, Art.RangeDisc, c, 1, 96, Color.white, "range");
                _rangeGlow = RangeLine("rangeGlow", 0.32f, new Color(1f, 0.6f, 0.12f, 0.4f), 97, true);
                _rangeRim = RangeLine("rangeRim", 0.075f, Pal.Hex("FF9A1A"), 98, false);
                _rangeHi = RangeLine("rangeHi", 0.03f, Pal.Hex("FFE7A8"), 99, false);
            }
            _range.transform.localPosition = c;
            _rangeC = c;
            _rangeR = r;
            _rangeT = secs;
            _rangeShown = 0;
        }

        public void HideRange() => _rangeT = Mathf.Min(_rangeT, 0.2f);

        /// <summary>信息面板选中的格子刷成蓝色；-1 取消。</summary>
        public void Select(int slot)
        {
            for (int i = 0; i < _slots.Count; i++) _slots[i].Selected = i == slot;
        }

        float _rangeR;
        Vector2 _rangeC;

        void TickRange()
        {
            if (_range == null) return;
            float dt = Time.unscaledDeltaTime;
            _rangeT -= dt;
            _rangeShown += dt;
            float a = _rangeT > 0.2f ? Mathf.Clamp01(_rangeShown / 0.2f) : Mathf.Clamp01(_rangeT / 0.2f);
            bool on = a > 0.001f;
            if (_rangeRim.enabled != on) _rangeRim.enabled = _rangeGlow.enabled = _rangeHi.enabled = _range.enabled = on;
            if (!on) return;
            float pop = 0.9f + 0.1f * Tween.OutBack(Mathf.Clamp01(_rangeShown / 0.25f));
            float r = _rangeR * pop;
            _range.color = Color.white.A(a);
            _range.transform.localScale = Vector3.one * r * 2f;
            float breathe = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
            _rangeGlow.startColor = _rangeGlow.endColor = new Color(1f, 0.6f, 0.12f, 0.4f * a * breathe);
            _rangeRim.startColor = _rangeRim.endColor = Pal.Hex("FF9A1A").A(a);
            _rangeHi.startColor = _rangeHi.endColor = Pal.Hex("FFE7A8").A(0.9f * a);
            SetCircle(_rangeGlow, _rangeC, r);
            SetCircle(_rangeRim, _rangeC, r);
            SetCircle(_rangeHi, _rangeC, r - 0.035f);
        }

        void Pedestal(Vector2 p, float w)
        {
            WorldFx.Block(_world, Art.SoftDot, p + new Vector2(0, -0.1f), new Vector2(w * 1.1f, 0.7f), 58, new Color(0, 0, 0, 0.45f), "pedShadow");
        }

        SlotView AddSlotView(Slot s, bool animate)
        {
            var v = new SlotView(this, s, _world);
            _slots.Add(v);
            if (animate)
            {
                Tween.Pop(v.Root, 0.4f);
                WorldFx.I.Sparks(Art.SoftDot, s.Pos.ToV(), 10, 0.35f, 2.2f, Pal.Hex("D9C9A8").A(0.8f), 0.5f, 9000, 3f);
            }
            return v;
        }

        // ---------------- 主循环 ----------------

        void Update()
        {
            if (B == null) return;
            if (!Mathf.Approximately(_fitOrtho, Game.Cam.orthographicSize) || !Mathf.Approximately(_fitAspect, Game.Cam.aspect)) FitField();
            TickRange();
            foreach (var d in _doors) d?.Tick();
            bool frozen = Paused || Hud.Blocking;
            bool running = B.Phase == Phase.Running && !frozen;
            Time.timeScale = frozen ? 0f : Speed;
            if (running)
            {
                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                if (Auto != null) Auto.Step(B, dt);
                B.Update(dt);
            }

            PlayEvents();
            SyncSlots();
            SyncEnemies();
            SyncShots();
            HandleInput();
            if (_hitSfxGap > 0) _hitSfxGap -= Time.unscaledDeltaTime;
            if (_wallFlash > 0)
            {
                _wallFlash -= Time.deltaTime;
                _wall.color = Color.Lerp(_wallColor, new Color(1f, 0.55f, 0.5f), Mathf.Clamp01(_wallFlash / 0.12f));
            }
            else _wall.color = _wallColor;
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (Hud) Destroy(Hud.gameObject);
        }

        // ---------------- 事件 ----------------

        void PlayEvents()
        {
            var fx = WorldFx.I;
            foreach (var e in B.Events)
            {
                var p = e.Pos.ToV();
                switch (e.Type)
                {
                    case Ev.Summon:
                    {
                        var sv = _slots[e.Slot];
                        sv.Sync(true);
                        var c = e.Value >= 2 ? Pal.Star[e.Value - 1] : Pal.Hex("B98CFF");
                        // 参考：召唤时格子上冲起一道紫色光柱，落地一圈光环。
                        fx.Rise(Art.Img("fx_pillar"), p + new Vector2(0, -0.45f), 1.9f, 0.6f, c, 9050, true, 0.35f);
                        fx.LightPool(p + new Vector2(0, -0.3f), 2.0f, c.A(0.8f), 0.5f, 159);
                        fx.GroundWave(p + new Vector2(0, -0.3f), 0.9f, c.A(0.9f), 0.4f, 159, 0.42f);
                        fx.Flash(p + new Vector2(0, 0.3f), 1.8f, c.A(0.45f), 0.25f);
                        fx.Sparks(Art.Img("fx_star"), p + new Vector2(0, 0.2f), 6, 0.28f, 2.6f, Color.white, 0.5f, 9100, 2f, true);
                        Tween.Punch(Drum, 0.2f, 0.12f);
                        Sfx.Play(Snd.Summon, 0.55f);
                        Hud.OnSummon();
                        break;
                    }
                    case Ev.Merge:
                    {
                        var sv = _slots[e.Slot];
                        sv.Sync(true);
                        var c = Pal.Star[e.Value - 1];
                        fx.Rise(Art.Img("fx_pillar"), p + new Vector2(0, -0.45f), 2.4f, 0.7f, c, 9050, true, 0.35f);
                        fx.GroundWave(p + new Vector2(0, -0.3f), 1.3f, c, 0.5f, 159, 0.42f);
                        fx.LightPool(p + new Vector2(0, -0.3f), 2.6f, c.A(0.9f), 0.6f, 159);
                        fx.Flash(p + new Vector2(0, 0.3f), 2.6f, c.A(0.6f), 0.3f);
                        fx.Sparks(Art.Img("fx_star"), p + new Vector2(0, 0.3f), 10, 0.35f, 3.4f, Color.white, 0.6f, 9100, 2f, true);
                        fx.Number(p + new Vector2(0, 0.95f), StarText(e.Value), c, 46, 0.7f, 0.9f, true);
                        Sfx.Play(Snd.Merge, 0.7f, 0.9f + e.Value * 0.06f);
                        LastMergeTime = Time.unscaledTime;
                        MergesDone++;
                        break;
                    }
                    case Ev.Move:
                        foreach (var s in _slots) s.Sync(false);
                        Sfx.Play(Snd.Card, 0.6f);
                        break;
                    case Ev.SlotAdded:
                    {
                        var sv = AddSlotView(B.Slots[e.Slot], true);
                        sv.Sync(false);
                        Sfx.Play(Snd.SlotAdd, 0.8f);
                        Hud.OnSlotAdded();
                        break;
                    }
                    case Ev.Fire:
                        _slots[e.Slot].Kick(p);
                        Muzzle(e);
                        if (e.Hero == HeroId.HuaRong || e.Hero == HeroId.ZhangQing) Sfx.Play(Snd.Bow, e.Hero == HeroId.HuaRong ? 0.35f : 0.22f, e.Hero == HeroId.HuaRong ? 1f : 1.3f);
                        break;
                    case Ev.Hit:
                        PlayHit(e, p);
                        break;
                    case Ev.Damage:
                        ShowDamage(e, p);
                        break;
                    case Ev.Kill:
                        OnKill(e, p);
                        break;
                    case Ev.WallHit:
                        _wallFlash = 0.12f;
                        if (e.Value >= 60) fx.Shake(0.06f, 0.15f);
                        if (Random.value < 0.4f)
                        {
                            var wp = new Vector2(p.x + Random.Range(-0.2f, 0.2f), -4.45f);
                            fx.Burst(Art.Img("fx_hit"), wp + new Vector2(0, 0.25f), 0.55f, 0.14f, 9000, Pal.Hex("FFE0B0"), 0, 0.4f, true);
                            fx.Dust(wp, 0.8f, DustCol.A(0.6f), 0.5f, 8900);
                            if (e.Value >= 60) fx.Debris(wp, 2, 0.16f, 1.6f);
                        }
                        if (Random.value < 0.35f) Sfx.Play(Snd.WallHit, 0.35f);
                        Hud.OnWallHit();
                        break;
                    case Ev.Ult:
                        OnUlt(e, p);
                        break;
                    case Ev.WaveStart:
                        Hud.OnWave(e.Value);
                        foreach (var d in _doors) d?.Open(1.6f);
                        break;
                    case Ev.BossComing:
                    {
                        var bd = Defs.Enemy((EnemyKind)e.Slot);
                        Hud.Banner("强敌来袭", $"首领 {bd.Name} 率官军压境", Pal.Red, bd.Art);
                        Sfx.Play(Snd.Warn);
                        Sfx.PlayMusic(Bgm.Boss);
                        break;
                    }
                    case Ev.EliteComing:
                    {
                        var ed = Defs.Enemy((EnemyKind)e.Slot);
                        Hud.Banner(ed.Name + "出阵", ed.Desc, Pal.Purple, ed.Art, true);
                        Sfx.Play(Snd.Warn, 0.6f);
                        break;
                    }
                    case Ev.BossSkill:
                        OnBossSkill(e, p);
                        break;
                    case Ev.Explode:
                        Explosion(p, e.Radius, 1.3f);
                        fx.Shake(0.12f, 0.2f);
                        Sfx.Play(Snd.Bomb, 0.55f);
                        break;
                    case Ev.UnitStun:
                        fx.Number(p + new Vector2(0, 1.0f), "晕眩", Pal.Hex("C8D0DA"), 36, 0.5f, 0.8f, true);
                        fx.Sparks(Art.Img("fx_star"), p + new Vector2(0, 0.5f), 5, 0.22f, 2f, Color.white, 0.5f, 9450, 3f, true);
                        break;
                    case Ev.Coin:
                        fx.Number(p + new Vector2(0, 1.0f), "+" + e.Value, Pal.Gold, 40, 0.7f, 0.9f, true);
                        fx.Fly(Art.Coin, p + new Vector2(0, 0.4f), CoinTarget, 0.35f, 0.6f, 1.2f, () => { if (Hud) Hud.PunchCoins(); Sfx.Play(Snd.Coin, 0.3f); });
                        break;
                    case Ev.Rage:
                        Hud.Banner("官军狂暴", "移速、攻击大幅提升", Pal.RedDeep, null);
                        Hud.Rage();
                        Sfx.Play(Snd.Warn);
                        break;
                    case Ev.PorterKilled:
                        Hud.ChestDrop(p);
                        break;
                    case Ev.PorterEscaped:
                        Ui.Toast("生辰纲被运走了", 300);
                        break;
                    case Ev.Enhance:
                        Sfx.Play(Snd.Merge, 0.5f, 1.2f);
                        break;
                    case Ev.Revive:
                        fx.Ring(new Vector2(0, -4.5f), 6f, Pal.Gold, 0.7f);
                        fx.Shake(0.15f, 0.3f);
                        _wallColor = Color.white;
                        Sfx.Play(Snd.UltBomb, 0.8f);
                        break;
                    case Ev.Defeat:
                        _wallColor = new Color(0.55f, 0.5f, 0.5f);
                        fx.Shake(0.3f, 0.6f);
                        for (int i = 0; i < 6; i++)
                            fx.Burst(Art.Img("fx_explode"), new Vector2(Random.Range(-4.5f, 4.5f), Random.Range(-5.8f, -4.6f)), Random.Range(1.4f, 2.2f), 0.6f, 9500);
                        Sfx.Play(Snd.UltBomb);
                        break;
                    case Ev.Victory:
                        foreach (var s in _slots) s.Cheer();
                        break;
                }
            }
            B.Events.Clear();
        }

        /// <summary>出手瞬间在英雄身上的小闪光，让攻击有“发力点”。</summary>
        void Muzzle(BattleEvent e)
        {
            var fx = WorldFx.I;
            var sp = B.Slots[e.Slot].Pos.ToV();
            var dir = (e.Pos.ToV() - sp).normalized;
            var hand = sp + new Vector2(0, 0.25f) + dir * 0.35f;
            switch (e.Hero)
            {
                case HeroId.HuaRong:
                    fx.Burst(Art.Sparkle, hand + new Vector2(0, 0.1f), 0.8f, 0.16f, 9450, Color.white, 60f, 0.3f, true);
                    break;
                case HeroId.LingZhen:
                    fx.Flash(hand, 0.8f, Fire.A(0.5f), 0.1f);
                    break;
                case HeroId.GongSun:
                    fx.Flash(sp + new Vector2(0, 0.6f), 1.0f, Pal.Hex("7FC4FF").A(0.5f), 0.2f);
                    fx.Burst(Art.Sparkle, sp + new Vector2(0, 0.75f), 1.1f, 0.3f, 9450, Pal.Hex("8FD4FF"), 90f, 0.3f, true);
                    break;
                case HeroId.ZhangQing:
                    fx.Burst(Art.Sparkle, hand, 0.6f, 0.14f, 9450, Pal.Hex("E8E0D0"), 60f, 0.3f, true);
                    break;
                case HeroId.LinChong:
                    fx.Flash(hand + dir * 0.3f, 0.7f, Pal.Hex("CFE8FF").A(0.5f), 0.1f);
                    break;
                case HeroId.WuYong:
                    fx.Flash(sp + new Vector2(0, 0.6f), 0.9f, Pal.Hex("8FF0D0").A(0.45f), 0.2f);
                    break;
            }
        }

        static string StarText(int star) => star >= 4 ? "满星!" : new string('★', star);

        void PlayHit(BattleEvent e, Vector2 p)
        {
            var fx = WorldFx.I;
            switch (e.Shot)
            {
                case ShotKind.Arrow:
                {
                    // 参考：箭命中只有一颗干净的白色星闪 + 敌人整身泛红；偶尔有箭落地插在土里。
                    var hp = p + new Vector2(Random.Range(-0.1f, 0.1f), 0.45f);
                    fx.Burst(Art.Sparkle, hp, e.Crit ? 1.2f : 0.75f, 0.18f, 9500, e.Crit ? Pal.Hex("FFC54A") : Pal.Hex("FFF6DA"), 45f, 0.3f, true);
                    if (e.Crit)
                    {
                        fx.Burst(Art.Img("fx_hit"), hp, 1.0f, 0.18f, 9490, Pal.Hex("FF9A4A"), 0, 0.4f, true);
                        fx.Flash(hp, 1.2f, Pal.Hex("FFB050").A(0.4f));
                    }
                    if (Random.value < 0.3f)
                        fx.Linger(Art.Img("fx_arrow_stuck"), p + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(-0.25f, 0.05f)), 0.42f, 1.1f, Random.value < 0.5f);
                    if (_hitSfxGap <= 0) { Sfx.Play(Snd.ArrowHit, 0.25f); _hitSfxGap = 0.06f; }
                    break;
                }
                case ShotKind.FireArrow:
                    fx.Burst(Art.Img("fx_fireball"), p + new Vector2(0, 0.35f), 1.2f, 0.32f, 9510, null, 0, 0.3f, true);
                    fx.Burst(Art.Img("fx_explode"), p + new Vector2(0, 0.3f), 0.9f, 0.35f, 9500);
                    fx.LightPool(p, 2.0f, Fire.A(0.75f), 0.4f);
                    fx.Decal(Art.Img("fx_scorch"), p, 0.8f, ScorchCol.A(0.4f), 1.1f);
                    fx.Sparks(Art.SoftDot, p + new Vector2(0, 0.3f), 3, 0.14f, 3f, Fire, 0.4f, 9520, 6f, true);
                    break;
                case ShotKind.Bomb:
                    Explosion(p, e.Radius, 1f);
                    fx.Shake(0.06f, 0.12f);
                    Sfx.Play(Snd.Bomb, 0.45f);
                    break;
                case ShotKind.MiniBomb:
                    Explosion(p, 0.55f, 0.55f);
                    break;
                case ShotKind.BigBomb:
                    Explosion(p, e.Radius, 2.2f);
                    fx.Decal(Art.Img("fx_crack"), p, e.Radius * 1.9f, new Color(0.1f, 0.07f, 0.05f, 0.75f), 4f, 0.5f, 91);
                    fx.GroundWave(p, e.Radius * 1.6f, Pal.Hex("FFE2A0").A(0.9f), 0.7f);
                    fx.ScreenFlash(Pal.Hex("FFB050").A(0.35f), 0.35f);
                    fx.Shake(0.3f, 0.45f);
                    Sfx.Play(Snd.UltBomb);
                    break;
                case ShotKind.Lightning:
                    fx.Strike(p, e.Crit ? 3.4f : 1.5f, e.Crit ? 9650 : 9600);
                    fx.Sparks(Art.SoftDot, p + new Vector2(0, 0.25f), e.Crit ? 8 : 4, 0.13f, 3.2f, Pal.Hex("A8DCFF"), 0.35f, 9610, 6f, true);
                    if (B.Level("gs_ice") > 0)
                    {
                        fx.Rise(Art.Img("fx_icecrystal"), p + new Vector2(Random.Range(-0.1f, 0.1f), -0.15f), e.Crit ? 1.6f : 1.0f, 0.8f, Color.white, WorldFx.Order(p.y - 0.2f), true, 0.45f);
                        fx.Decal(Art.Img("fx_scorch"), p, e.Crit ? 1.8f : 1.2f, Pal.Hex("BFE6FF").A(0.55f), 1.8f, 0.5f, 92);
                        fx.LightPool(p, e.Crit ? 2.6f : 1.8f, Pal.Hex("6FC8FF").A(0.7f), 0.6f);
                    }
                    if (e.Crit) { fx.Shake(0.12f, 0.2f); fx.GroundWave(p, 1.2f, Pal.Hex("9AD4FF").A(0.8f), 0.4f); }
                    if (_hitSfxGap <= 0) { Sfx.Play(Snd.Thunder, e.Crit ? 0.7f : 0.35f); _hitSfxGap = 0.05f; }
                    break;
                case ShotKind.Punch:
                    fx.Burst(Art.Img("fx_shock"), p + new Vector2(0, 0.3f), e.Radius * 1.7f, 0.32f, 9500, null, 220f);
                    fx.Burst(Art.Img("fx_hit"), p + new Vector2(0, 0.45f), e.Radius * 1.3f, 0.18f, 9510, Pal.Hex("FFC08A"), 0, 0.4f, true);
                    fx.GroundWave(p, e.Radius * 1.1f, Pal.Hex("FFE6B0").A(0.75f), 0.35f);
                    fx.Dust(p, e.Radius * 2f, DustCol.A(0.45f), 0.5f);
                    if (e.Crit)
                    {
                        fx.Decal(Art.Img("fx_crack"), p, e.Radius * 1.6f, new Color(0.12f, 0.09f, 0.07f, 0.6f), 2.2f, 0.5f, 91);
                        fx.Debris(p, 4, 0.22f, 2.2f);
                        fx.Shake(0.08f, 0.12f);
                    }
                    Sfx.Play(Snd.Fist, 0.45f);
                    break;
                case ShotKind.Slam:
                    fx.Burst(Art.Img("fx_shock"), p + new Vector2(0, 0.25f), e.Radius * 1.8f, 0.36f, 9500, Pal.Hex("FFE2B0"), 160f);
                    fx.Decal(Art.Img("fx_crack"), p, e.Radius * (e.Crit ? 2.2f : 1.4f), new Color(0.12f, 0.09f, 0.07f, 0.55f), e.Crit ? 2.5f : 1.2f, 0.5f, 91);
                    fx.GroundWave(p, e.Radius * 1.2f, Pal.Hex("FFE6B0").A(0.8f), 0.4f);
                    fx.Dust(p, e.Radius * 2.2f, DustCol.A(0.55f), 0.6f);
                    fx.Debris(p, e.Crit ? 5 : 2, 0.2f, 2.2f);
                    if (e.Crit) fx.Shake(0.12f, 0.2f);
                    Sfx.Play(Snd.Fist, 0.5f, 0.75f);
                    break;
                case ShotKind.Spear:
                    fx.Burst(Art.Img("fx_hit"), p + new Vector2(0, 0.45f), e.Crit ? 1.2f : 0.8f, 0.18f, 9510, e.Crit ? Pal.Hex("FFC54A") : Pal.Hex("D8ECFF"), 0, 0.4f, true);
                    fx.Burst(Art.Sparkle, p + new Vector2(0, 0.5f), 0.7f, 0.16f, 9520, Color.white, 90f, 0.3f, true);
                    if (B.Level("lc_frost") > 0) fx.Decal(Art.Img("fx_scorch"), p, 0.9f, Pal.Hex("BFE6FF").A(0.45f), 1.2f, 0.5f, 92);
                    if (_hitSfxGap <= 0) { Sfx.Play(Snd.ArrowHit, 0.3f, 0.8f); _hitSfxGap = 0.06f; }
                    break;
                case ShotKind.Stone:
                    fx.Burst(Art.Img("fx_hit"), p + new Vector2(0, 0.45f), e.Crit ? 1.1f : 0.65f, 0.16f, 9510, Pal.Hex("EDE4D2"), 0, 0.4f, true);
                    fx.Sparks(Art.Img("fx_rock2"), p + new Vector2(0, 0.4f), 2, 0.12f, 2.2f, Color.white, 0.4f, 9520, 7f);
                    if (_hitSfxGap <= 0) { Sfx.Play(Snd.ArrowHit, 0.25f, 1.25f); _hitSfxGap = 0.05f; }
                    break;
                case ShotKind.Wind:
                    fx.Burst(Art.Img("fx_wave"), p + new Vector2(0, 0.3f), e.Radius * 1.8f, 0.4f, 9480, Pal.Hex("9FF5D8").A(0.85f), 300f, 0.4f, true);
                    fx.GroundWave(p, e.Radius, Pal.Hex("B8FFE6").A(0.6f), 0.35f);
                    if (e.Crit) fx.Smoke(p + new Vector2(0, 0.3f), 3, 1.0f, Pal.Hex("CFFFF0").A(0.4f), 0.8f, 1.2f, 0.5f);
                    break;
                case ShotKind.Frost:
                    for (int i = 0; i < 9; i++)
                    {
                        var ip = p + Random.insideUnitCircle * e.Radius * 0.9f;
                        fx.Rise(Art.Img("fx_icecrystal"), ip, Random.Range(0.9f, 1.6f), 1.4f, Color.white, WorldFx.Order(ip.y), true, 0.6f);
                    }
                    fx.Decal(Art.Img("fx_scorch"), p, e.Radius * 2.2f, Pal.Hex("CFEFFF").A(0.7f), 3f, 0.5f, 92);
                    fx.LightPool(p, e.Radius * 3f, Pal.Hex("8FD8FF").A(0.8f), 0.8f);
                    fx.ScreenFlash(Pal.Hex("D8F0FF").A(0.3f), 0.3f);
                    fx.Shake(0.15f, 0.3f);
                    Sfx.Play(Snd.UltThunder, 0.6f, 1.3f);
                    break;
                case ShotKind.Hammer:
                    fx.Burst(Art.Img("fx_hit"), p + new Vector2(0, 0.4f), 1.4f, 0.25f, 9510, Pal.Hex("FFD0A0"), 0, 0.4f, true);
                    fx.Debris(p, 4, 0.2f, 2.4f);
                    fx.Shake(0.12f, 0.2f);
                    Sfx.Play(Snd.WallHit, 0.7f, 0.8f);
                    break;
            }
        }

        void OnBossSkill(BattleEvent e, Vector2 p)
        {
            var fx = WorldFx.I;
            var top = p + new Vector2(0, 3.2f);
            switch ((BossSkill)e.Value)
            {
                case BossSkill.Hammer:
                    fx.Number(top, "流星飞锤", Pal.Hex("FFB070"), 48, 0.7f, 1.0f, true);
                    Sfx.Play(Snd.Warn, 0.5f);
                    break;
                case BossSkill.Charge:
                    fx.Number(top, "冲锋！", Pal.Hex("FF8A6A"), 52, 0.7f, 0.9f, true);
                    fx.Dust(p, 3f, DustCol.A(0.7f), 0.8f, 95);
                    fx.GroundWave(p, 2.2f, Pal.Hex("FFC8A0").A(0.7f), 0.4f);
                    Sfx.Play(Snd.Warn, 0.5f);
                    break;
                case BossSkill.Summon:
                    fx.Number(top, "连环马来援", Pal.Hex("FFB070"), 46, 0.7f, 1.0f, true);
                    fx.Dust(p + new Vector2(0, 0.6f), 4f, DustCol.A(0.8f), 0.8f, 95);
                    Sfx.Play(Snd.Warn, 0.6f);
                    break;
                case BossSkill.Ball:
                    fx.Number(top, "蹴鞠火球", Pal.Hex("FFB070"), 48, 0.7f, 1.0f, true);
                    Sfx.Play(Snd.Bomb, 0.4f, 1.3f);
                    break;
                case BossSkill.Shield:
                    fx.Number(top, "护体金光", Pal.Gold, 50, 0.7f, 1.0f, true);
                    fx.Ring(p + new Vector2(0, 1.2f), 2.4f, Pal.Gold, 0.6f);
                    fx.Flash(p + new Vector2(0, 1.2f), 4f, Pal.Gold.A(0.5f), 0.3f);
                    Sfx.Play(Snd.Jackpot, 0.4f);
                    break;
            }
        }

        static readonly Color Fire = new Color(1f, 0.55f, 0.18f);
        static readonly Color SmokeCol = new Color(0.3f, 0.27f, 0.25f, 0.8f);
        static readonly Color ScorchCol = new Color(0.07f, 0.05f, 0.04f, 0.6f);
        static readonly Color DustCol = new Color(0.86f, 0.78f, 0.64f, 1f);

        /// <summary>炸弹爆炸：火球（叠加）+ 火焰 + 地面受光 + 冲击波 + 焦痕 + 烟 + 碎石。scale 1 = 普通炸弹。</summary>
        void Explosion(Vector2 p, float radius, float scale)
        {
            var fx = WorldFx.I;
            float r = radius;
            fx.Flash(p + new Vector2(0, 0.4f * scale), r * 3.2f, Pal.Hex("FFC070").A(0.55f), 0.2f);
            fx.Burst(Art.Img("fx_fireball"), p + new Vector2(0, 0.45f * scale), r * 2.3f, 0.36f + 0.1f * scale, 9520, null, 0, 0.25f, true);
            fx.Burst(Art.Img("fx_explode"), p + new Vector2(0, 0.35f * scale), r * 2.0f, 0.42f + 0.1f * scale, 9500);
            fx.LightPool(p, r * 3.6f, Fire.A(0.85f), 0.5f + 0.15f * scale);
            fx.GroundWave(p, r * 1.15f, Pal.Hex("FFD9A0").A(0.75f), 0.38f);
            fx.Decal(Art.Img("fx_scorch"), p, r * 1.9f, ScorchCol.A(0.42f), 1.2f + 0.8f * scale);
            if (scale >= 0.9f) fx.Dust(p, r * 2.4f, DustCol.A(0.35f), 0.6f);
            fx.Smoke(p + new Vector2(0, 0.35f * scale), scale >= 2f ? 3 : 1, 0.7f * r + 0.25f, SmokeCol.A(0.6f), 0.9f + 0.3f * scale, r * 0.4f, 0.7f * scale + 0.3f);
            fx.Sparks(Art.SoftDot, p + new Vector2(0, 0.3f), Mathf.RoundToInt(2 + 3 * scale), 0.14f, 3.2f * Mathf.Sqrt(scale), Fire, 0.45f, 9530, 7f, true);
            if (scale >= 2f) fx.Debris(p, 5, 0.26f, 3f);
        }

        void ShowDamage(BattleEvent e, Vector2 p)
        {
            var fx = WorldFx.I;
            if (!e.Crit && fx.LiveTexts > 36) return;
            var at = p + new Vector2(Random.Range(-0.3f, 0.3f), 0.75f + Random.Range(0f, 0.25f));
            if (e.Crit) fx.Number(at, Ui.Num(e.Value), Pal.Hex("FFB12E"), 54, 0.8f, 0.8f, true);
            else fx.Number(at, Ui.Num(e.Value), Color.white, 36, 0.55f, 0.6f);
        }

        void OnKill(BattleEvent e, Vector2 p)
        {
            var fx = WorldFx.I;
            if (_coinsFlying < 18)
            {
                _coinsFlying++;
                fx.Fly(Art.Coin, p + new Vector2(0, 0.4f), CoinTarget, 0.3f, 0.65f, 1.2f, () =>
                {
                    _coinsFlying--;
                    if (Hud) Hud.PunchCoins();
                    Sfx.Play(Snd.Coin, 0.22f);
                });
            }
            else Hud.PunchCoins();
            Sfx.Play(Snd.Kill, 0.4f);
        }

        void OnUlt(BattleEvent e, Vector2 p)
        {
            var fx = WorldFx.I;
            var sv = _slots[e.Slot];
            var sp = sv.Slot.Pos.ToV();
            var def = Defs.Hero(e.Hero);
            fx.Rise(Art.Img("fx_pillar"), sp + new Vector2(0, -0.45f), 2.8f, 0.8f, Pal.Hex("FFD27A"), 9050, true, 0.4f);
            fx.LightPool(sp + new Vector2(0, -0.3f), 3.2f, Pal.Gold.A(0.9f), 0.7f, 159);
            fx.GroundWave(sp + new Vector2(0, -0.3f), 1.8f, Pal.Hex("FFE7A0"), 0.55f, 159, 0.42f);
            fx.Flash(sp + new Vector2(0, 0.4f), 3.4f, Pal.Gold.A(0.55f), 0.35f);
            fx.ScreenFlash(Pal.Hex("FFE2A0").A(0.18f), 0.3f);
            fx.Number(sp + new Vector2(0, 1.25f), def.UltName, Pal.Gold, 50, 0.9f, 1.1f, true);
            sv.Kick(p, 1.6f);
            switch (e.Hero)
            {
                case HeroId.HuaRong: Sfx.Play(Snd.UltArrow, 0.8f); break;
                case HeroId.LingZhen: Sfx.Play(Snd.Wave, 0.7f); break;
                case HeroId.GongSun: Sfx.Play(Snd.UltThunder, 0.8f); fx.Shake(0.1f, 0.3f); break;
                case HeroId.WuSong: Sfx.Play(Snd.UltFist, 0.9f); break;
                case HeroId.ZhangQing: Sfx.Play(Snd.UltArrow, 0.7f, 1.2f); break;
                case HeroId.LinChong: Sfx.Play(Snd.Wave, 0.7f, 1.2f); break;
                case HeroId.LuZhiShen: Sfx.Play(Snd.UltFist, 0.9f, 0.8f); fx.Shake(0.2f, 0.4f); break;
                case HeroId.WuYong:
                    Sfx.Play(Snd.Jackpot, 0.6f);
                    fx.ScreenFlash(Pal.Hex("B8FFE6").A(0.2f), 0.4f);
                    foreach (var s in _slots) if (s.Slot.Unit != null) fx.Rise(Art.Img("fx_pillar"), s.Slot.Pos.ToV() + new Vector2(0, -0.45f), 1.6f, 0.6f, Pal.Hex("8FF0D0"), 9050, true, 0.3f);
                    Hud.Banner("全军急攻", "六秒内全军攻速 +60%", Pal.TealDeep, null, true);
                    break;
            }
        }

        // ---------------- 同步 ----------------

        void SyncSlots()
        {
            for (int i = 0; i < _slots.Count; i++) _slots[i].Tick(i == _dragFrom);
        }

        void SyncEnemies()
        {
            foreach (var e in B.Enemies)
            {
                if (!_enemies.TryGetValue(e, out var v))
                {
                    v = new EnemyView(e, _world);
                    _enemies[e] = v;
                    if (e.Pos.Y > Layout.SpawnY - 0.6f)
                    {
                        if (e.Pos.X <= 0.5f) _doors[0]?.Open(1.4f);
                        if (e.Pos.X >= -0.5f) _doors[1]?.Open(1.4f);
                    }
                }
                v.Tick(B.Rage);
            }
            _gone.Clear();
            foreach (var kv in _enemies)
                if (kv.Key.Dead || kv.Key.Escaped || !B.Enemies.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var e in _gone)
            {
                var v = _enemies[e];
                _enemies.Remove(e);
                if (e.Escaped) v.Escape();
                else if (e.Dead) v.Die();
                else v.Destroy();
            }
            Hud.SyncBoss(BossEnemy());
        }

        Enemy BossEnemy()
        {
            foreach (var e in B.Enemies) if (e.Def.IsBoss && !e.Dead) return e;
            return null;
        }

        void SyncShots()
        {
            _liveShots.Clear();
            foreach (var s in B.Shots)
            {
                _liveShots.Add(s);
                if (!_shots.TryGetValue(s, out var v))
                {
                    v = new ShotView(s, _world);
                    _shots[s] = v;
                }
                v.Tick();
            }
            _goneShots.Clear();
            foreach (var kv in _shots) if (!_liveShots.Contains(kv.Key)) _goneShots.Add(kv.Key);
            foreach (var s in _goneShots)
            {
                _shots[s].Destroy();
                _shots.Remove(s);
            }
        }

        // ---------------- 拖拽合成 ----------------

        /// <summary>自检用的假指针：按下/按住/抬起 + 世界坐标。为空时读真实鼠标、触摸。</summary>
        public struct Pointer
        {
            public bool Down, Held, Up;
            public Vector2 World;
        }

        public System.Func<Pointer> FakePointer;

        Pointer ReadPointer()
        {
            if (FakePointer != null) return FakePointer();
            var w = Game.Cam.ScreenToWorldPoint(Input.mousePosition);
            return new Pointer
            {
                Down = Input.GetMouseButtonDown(0) && !Ui.PointerOverUi(),
                Held = Input.GetMouseButton(0),
                Up = Input.GetMouseButtonUp(0),
                World = new Vector2(w.x, w.y),
            };
        }

        public Vector2 SlotWorld(int i) => B.Slots[i].Pos.ToV();

        int SlotAt(Vector2 p)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < B.Slots.Count; i++)
            {
                var c = B.Slots[i].Pos.ToV() + new Vector2(0, 0.1f);
                var d = p - c;
                if (Mathf.Abs(d.x) > 0.7f || Mathf.Abs(d.y) > 0.72f) continue;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = i; }
            }
            return best;
        }

        void HandleInput()
        {
            if (Auto != null) return;
            bool can = B.Phase == Phase.Running && !Paused && !Hud.Blocking;
            if (!can)
            {
                if (_dragFrom >= 0) CancelDrag();
                _pressing = false;
                return;
            }
            var ptr = ReadPointer();
            var p = ptr.World;
            if (ptr.Down)
            {
                int i = SlotAt(p);
                if (i >= 0 && B.Slots[i].Unit != null)
                {
                    _pressing = true;
                    _pressPos = p;
                    _pressTime = Time.unscaledTime;
                    _dragFrom = i;
                    Sfx.Play(Snd.Tap, 0.5f);
                }
                else Hud.CloseInfo();
            }
            if (_pressing && ptr.Held && _dragFrom >= 0)
            {
                var sv = _slots[_dragFrom];
                bool moved = (p - _pressPos).magnitude > 0.18f;
                if (moved || sv.Dragging) sv.DragTo(p + new Vector2(0, 0.25f));
                int over = SlotAt(p);
                for (int i = 0; i < _slots.Count; i++)
                    _slots[i].Highlight(i == _dragFrom ? 0 : B.CanMerge(_dragFrom, i) ? (i == over ? 3 : 2) : (i == over && sv.Dragging ? 1 : 0));
            }
            if (_pressing && ptr.Up)
            {
                _pressing = false;
                int from = _dragFrom;
                int to = SlotAt(p);
                bool wasDrag = _slots[from].Dragging;
                CancelDrag();
                if (!wasDrag && Time.unscaledTime - _pressTime < 0.4f)
                {
                    Hud.SlotInfo(B.Slots[from]);
                    return;
                }
                if (to >= 0 && to != from)
                {
                    bool merge = B.CanMerge(from, to);
                    B.Drop(from, to);
                    if (!merge) foreach (var s in _slots) s.Sync(false);
                }
            }
        }

        void CancelDrag()
        {
            if (_dragFrom >= 0 && _dragFrom < _slots.Count) _slots[_dragFrom].EndDrag();
            _dragFrom = -1;
            foreach (var s in _slots) s.Highlight(0);
        }

        /// <summary>引导用：找一对能合成的格子。</summary>
        public bool FindMergePair(out Vector2 a, out Vector2 b)
        {
            for (int i = 0; i < B.Slots.Count; i++)
                for (int j = i + 1; j < B.Slots.Count; j++)
                    if (B.CanMerge(i, j))
                    {
                        a = B.Slots[i].Pos.ToV();
                        b = B.Slots[j].Pos.ToV();
                        return true;
                    }
            a = b = default;
            return false;
        }

        public bool Dragging => _dragFrom >= 0;
        public float TopUnitScore()
        {
            float s = 0;
            foreach (var x in B.Slots) if (x.Unit != null) s += x.Unit.Star;
            return s;
        }

        // ================= 子视图 =================

        sealed class SlotView
        {
            public readonly Slot Slot;
            public readonly Transform Root;
            readonly BattleView _v;
            readonly SpriteRenderer _frame, _inner, _ring, _glow, _selGlow;
            readonly Transform _figs;
            readonly List<SpriteRenderer> _bodies = new List<SpriteRenderer>();
            readonly List<Vector2> _home = new List<Vector2>();
            readonly List<float> _sizes = new List<float>();
            SpriteRenderer _crown, _stunStar;
            int _shownId = -1;
            float _kick, _kickPow = 1f, _kickDir, _cheer, _popUntil, _release, _lastFire = -99f;
            Sprite[] _anim;
            Bend.Info _bend = new Bend.Info(0f, 1f, 0.5f, 0.22f);
            int _hl;
            readonly float _phase;
            public bool Dragging { get; private set; }
            public bool Selected;

            static readonly Color FrameColor = Pal.Hex("B9C0CB");
            static readonly Color InnerColor = new Color(0.2f, 0.23f, 0.28f, 0.42f);

            public SlotView(BattleView v, Slot s, Transform parent)
            {
                _v = v;
                Slot = s;
                _phase = Random.Range(0f, 6f);
                Root = new GameObject("slot" + s.Index).transform;
                Root.SetParent(parent, false);
                Root.localPosition = s.Pos.ToV();
                bool front = s.Pos.Y > Layout.WallRowY + 0.5f;
                if (front) WorldFx.Block(Root, Art.SoftDot, new Vector2(0, -0.55f), new Vector2(1.6f, 0.6f), 140, new Color(0, 0, 0, 0.4f), "shadow");
                // 参考：浅石框 + 下沿加厚出立体 + 半透明内格透出地面 + 蓝色内描线 + 底部金色小铭牌。
                _selGlow = WorldFx.Additive(WorldFx.Block(Root, Art.RoundedSmall, new Vector2(0, -0.02f), TileSize + new Vector2(0.24f, 0.24f), 149, new Color(0.3f, 0.85f, 1f, 0f), "selGlow"));
                WorldFx.Block(Root, Art.RoundedSmall, new Vector2(0, -0.03f), TileSize + new Vector2(0.07f, 0.08f), 150, Pal.Hex("2E333C"), "edge");
                _frame = WorldFx.Block(Root, Art.RoundedSmall, Vector2.zero, TileSize, 151, FrameColor, "frame");
                WorldFx.Block(Root, Art.RoundedSmall, new Vector2(0, -TileSize.y * 0.5f + 0.09f), new Vector2(TileSize.x, 0.18f), 152, Pal.Hex("8C94A2"), "frameBottom");
                WorldFx.Block(Root, Art.RoundedSmall, new Vector2(0, 0.03f), TileSize - new Vector2(0.15f, 0.2f), 153, Pal.Hex("8DB4D8").A(0.75f), "innerLine");
                _inner = WorldFx.Block(Root, Art.RoundedSmall, new Vector2(0, 0.03f), TileSize - new Vector2(0.21f, 0.26f), 154, InnerColor, "inner");
                WorldFx.Block(Root, Art.RoundedSmall, new Vector2(0, 0.42f), new Vector2(TileSize.x - 0.26f, 0.08f), 155, new Color(0, 0, 0, 0.16f), "innerShade");
                WorldFx.Block(Root, Art.RoundedSmall, new Vector2(0, -TileSize.y * 0.5f + 0.07f), new Vector2(0.34f, 0.07f), 156, Pal.Hex("D9A84A"), "plate");
                _glow = WorldFx.Spr(Root, Art.SoftDot, new Vector2(0, -0.22f), 1.3f, 157, new Color(1, 1, 1, 0), "glow");
                _glow.transform.localScale = new Vector3(1.3f, 0.6f, 1);
                _ring = WorldFx.Spr(Root, Art.SoftDot, new Vector2(0, -0.3f), 1f, 158, new Color(1, 1, 1, 0), "ring");
                _ring.transform.localScale = new Vector3(1.0f, 0.42f, 1);
                _figs = new GameObject("figs").transform;
                _figs.SetParent(Root, false);
            }

            public void Sync(bool pop)
            {
                var u = Slot.Unit;
                int id = u?.Id ?? -1;
                if (id == _shownId) return;
                _shownId = id;
                for (int i = _figs.childCount - 1; i >= 0; i--)
                {
                    var old = _figs.GetChild(i);
                    old.SetParent(null, false);
                    Object.Destroy(old.gameObject);
                }
                _bodies.Clear();
                _home.Clear();
                _sizes.Clear();
                _crown = null;
                if (u == null)
                {
                    _ring.color = new Color(1, 1, 1, 0);
                    return;
                }
                _anim = Art.Frames(u.Def.Art);
                var sprite = _anim[0];
                Vector2[] pos;
                float size;
                switch (u.Star)
                {
                    case 1: pos = new[] { new Vector2(0, 0) }; size = 0.98f; break;
                    case 2: pos = new[] { new Vector2(-0.27f, 0.05f), new Vector2(0.27f, -0.03f) }; size = 0.86f; break;
                    case 3: pos = new[] { new Vector2(0, 0.2f), new Vector2(-0.3f, -0.05f), new Vector2(0.3f, -0.07f) }; size = 0.78f; break;
                    default: pos = new[] { new Vector2(0, 0) }; size = 1.3f; break;
                }
                float feet = -0.46f;
                for (int i = 0; i < pos.Length; i++)
                {
                    var box = Art.FrameBox(u.Def.Art, size);
                    var home = new Vector2(pos[i].x, feet + pos[i].y + box.CenterY);
                    var sh = WorldFx.Spr(_figs, Art.SoftDot, new Vector2(pos[i].x, feet + pos[i].y + 0.02f), size * 0.7f, 0, new Color(0, 0, 0, 0.45f), "sh");
                    sh.transform.localScale = new Vector3(size * 0.62f, size * 0.18f, 1);
                    var body = Bend.Use(WorldFx.Spr(_figs, sprite, home, box.Size, 0, null, "fig"));
                    _bend = Bend.Meta(u.Def.Art);
                    body.flipX = pos[i].x > 0.01f;
                    _bodies.Add(body);
                    _home.Add(home);
                    _sizes.Add(box.Size);
                    if (pop)
                    {
                        body.transform.localScale = Vector3.zero;
                        float sz = box.Size;
                        _popUntil = Time.unscaledTime + 0.36f + i * 0.06f;
                        Tween.Run(body, 0.35f, t => body.transform.localScale = Vector3.one * sz * Tween.OutBack(t), null, i * 0.06f);
                    }
                }
                if (u.Star >= 4)
                {
                    _crown = WorldFx.Spr(_figs, Art.Img("ic_crown"), new Vector2(0, feet + size + 0.05f), 0.42f, 0, null, "crown");
                }
                var c = Pal.Star[u.Star - 1];
                _ring.color = c.A(u.Star == 1 ? 0.0f : 0.75f);
                ApplyOrder(false);
            }

            void ApplyOrder(bool lifted)
            {
                int baseOrder = lifted ? 9200 : WorldFx.Order(Slot.Pos.Y) + 10;
                int n = 0;
                foreach (Transform c in _figs)
                {
                    var sr = c.GetComponent<SpriteRenderer>();
                    float y = c.localPosition.y;
                    sr.sortingOrder = baseOrder + (c.name == "sh" ? 0 : 5) + Mathf.RoundToInt(-y * 20f) + (c.name == "crown" ? 40 : 0);
                    n++;
                }
            }

            public void Tick(bool source)
            {
                Sync(false);
                float t = Time.time + _phase;
                float dt = Time.deltaTime;
                if (_kick > 0) _kick -= dt * 6f;
                if (_cheer > 0) _cheer -= Time.unscaledDeltaTime;
                if (_release > 0) _release -= dt;
                // 帧：待机 0/1 呼吸交替；临出手（冷却快到且正在交战）蓄力帧 2；出手后 0.22 秒释放帧 3。
                var unit = Slot.Unit;
                bool fighting = Time.time - _lastFire < 2.5f;
                bool windup = unit != null && fighting && unit.Cd > 0 && unit.Cd < Mathf.Min(0.22f, unit.Def.Interval * 0.4f);
                for (int i = 0; i < _bodies.Count; i++)
                {
                    var b = _bodies[i];
                    if (b == null) continue;
                    float size = _sizes[i];
                    int frame = _release > 0 ? 3 : windup ? 2 : (int)(t * 1.7f + i * 0.5f) % 2;
                    if (_cheer > 0) frame = 3;
                    var spr = _anim[frame % _anim.Length];
                    if (b.sprite != spr) b.sprite = spr;
                    float k = Mathf.Max(0, _kick);
                    float breathe = Mathf.Sin(t * 3.4f + i * 1.3f) * 0.022f;
                    float hop = _cheer > 0 ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f + i)) * 0.25f : 0f;
                    if (Time.unscaledTime > _popUntil) b.transform.localScale = Vector3.one * size;
                    b.transform.localPosition = _home[i] + new Vector2(0, hop);
                    b.transform.localRotation = Quaternion.identity;
                    float lean = _kickDir * 0.05f * k * (b.flipX ? -1f : 1f);
                    Bend.Set(b, _bend, breathe * 0.4f - k * 0.02f * _kickPow, lean,
                        1f + k * 0.05f * _kickPow - breathe * 0.4f, 1f - k * 0.05f * _kickPow + breathe);
                }
                if (_crown) _crown.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 3f) * 6f);
                var u = Slot.Unit;
                bool stunned = u != null && u.Stun > 0;
                foreach (var b in _bodies) if (b) b.color = stunned ? new Color(0.55f, 0.58f, 0.66f) : Color.white;
                if (stunned)
                {
                    if (_stunStar == null) _stunStar = WorldFx.Spr(Root, Art.Img("fx_star"), new Vector2(0, 0.75f), 0.34f, 9300, null, "stunStar");
                    _stunStar.transform.localPosition = new Vector2(Mathf.Cos(Time.time * 7f) * 0.25f, 0.78f + Mathf.Sin(Time.time * 7f) * 0.06f);
                    _stunStar.transform.localRotation = Quaternion.Euler(0, 0, Time.time * 300f);
                }
                else if (_stunStar != null)
                {
                    Object.Destroy(_stunStar.gameObject);
                    _stunStar = null;
                }
                float glowA = 0;
                Color glowC = Color.white;
                if (u != null && u.Burst) { glowA = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f); glowC = Pal.Gold; }
                else if (u != null && u.Star >= 4) { glowA = 0.45f + 0.15f * Mathf.Sin(t * 3f); glowC = Pal.Gold; }
                if (_hl >= 2) { glowA = _hl == 3 ? 0.95f : 0.55f + 0.3f * Mathf.Sin(Time.unscaledTime * 8f); glowC = Pal.Hex("7CFF6A"); }
                _glow.color = glowC.A(glowA);
                float selPulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
                _frame.color = _hl >= 2 ? Pal.Hex("FFE16A") : _hl == 1 ? Pal.Hex("D6DCE6") : Selected ? Pal.Hex("7FE4FF") : FrameColor;
                _selGlow.color = new Color(0.3f, 0.85f, 1f, Selected ? 0.75f * selPulse : 0f);
                _inner.color = source && Dragging ? InnerColor.Mul(0.75f) : Selected ? new Color(0.25f, 0.6f, 1f, 0.5f * selPulse) : InnerColor;
            }

            public void Kick(Vector2 target, float pow = 1f)
            {
                _kick = 1f;
                _kickPow = pow;
                _release = 0.22f;
                _lastFire = Time.time;
                _kickDir = Mathf.Clamp(target.x - Slot.Pos.X, -1f, 1f);
            }

            public void Cheer() => _cheer = 3f;

            public void Highlight(int level) => _hl = level;

            public void DragTo(Vector2 world)
            {
                if (!Dragging)
                {
                    Dragging = true;
                    ApplyOrder(true);
                    _figs.localScale = Vector3.one * 1.15f;
                }
                _figs.position = new Vector3(world.x, world.y, 0);
            }

            public void EndDrag()
            {
                if (!Dragging) return;
                Dragging = false;
                var from = (Vector2)_figs.localPosition;
                var figs = _figs;
                Tween.Run(figs, 0.12f, k =>
                {
                    figs.localPosition = Vector2.Lerp(from, Vector2.zero, Tween.OutCubic(k));
                    figs.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, k);
                });
                ApplyOrder(false);
            }
        }

        sealed class EnemyView
        {
            readonly Enemy _e;
            readonly Transform _root;
            readonly SpriteRenderer _body, _shadow, _aura, _flash;
            readonly SpriteRenderer _barBg, _bar;
            readonly Transform _barRoot;
            SpriteRenderer _stun, _tag, _shield;
            readonly float _h, _phase, _jit, _size, _cy;
            readonly Sprite[] _frames;
            readonly SpriteRenderer _barGhost, _barHi;
            readonly float _bw, _bh;
            readonly Bend.Info _bend;
            float _redT, _step, _lastX, _lastY, _walkW, _ghostFrac = 1f;

            const float BarK = 0.22f;
            /// <summary>飞行单位离地高度：影子留在地上，身体悬空。</summary>
            const float FlyH = 0.75f;

            /// <summary>一只脚相对身体的高度（正 = 往上收）。phase 每走一步加 1，[0,1) 摆动、[1,2) 支撑。</summary>
            static float Foot(float phase, float reach, float raise)
            {
                float k = phase - 2f * Mathf.Floor(phase * 0.5f);
                if (k < 1f) return Mathf.Lerp(reach, -reach, k * k * (3f - 2f * k)) + raise * Mathf.Sin(k * Mathf.PI);
                return Mathf.Lerp(-reach, reach, k - 1f);
            }

            /// <summary>圆角九宫格条，按 BarK 缩小后圆角半径刚好配细血条。</summary>
            SpriteRenderer BarPiece(string name, float w, float h, Color c)
            {
                var sr = WorldFx.Spr(_barRoot, Art.RoundedSmall, Vector2.zero, 1, 0, c, name);
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.transform.localScale = Vector3.one * BarK;
                sr.size = new Vector2(Mathf.Max(w, 0.01f) / BarK, h / BarK);
                return sr;
            }

            void SetBarWidth(SpriteRenderer sr, float frac, float w, float h)
            {
                float fw = Mathf.Max(w * frac, h);
                sr.size = new Vector2(fw / BarK, h / BarK);
                sr.transform.localPosition = new Vector2(-(w - fw) * 0.5f, sr.transform.localPosition.y);
            }

            public EnemyView(Enemy e, Transform parent)
            {
                _e = e;
                _phase = Random.Range(0f, 10f);
                _jit = e.Big ? 0f : Random.Range(0f, 0.75f);
                _h = e.Def.Size * (e.Def.IsBoss ? 1.05f : 1.02f);
                _root = new GameObject(e.Def.Name).transform;
                _root.SetParent(parent, false);
                _shadow = WorldFx.Spr(_root, Art.SoftDot, Vector2.zero, 1, 0, new Color(0, 0, 0, 0.42f), "shadow");
                _shadow.transform.localScale = new Vector3(_h * 0.75f, _h * 0.24f, 1);
                if (e.Def.Kind == EnemyKind.Porter || e.Big)
                {
                    var c = e.Def.Kind == EnemyKind.Porter ? Pal.Gold : e.Def.IsBoss ? Pal.Red : Pal.Purple;
                    _aura = WorldFx.Spr(_root, Art.SoftDot, Vector2.zero, 1, 0, c.A(0.5f), "aura");
                    _aura.transform.localScale = new Vector3(_h * 1.2f, _h * 0.45f, 1);
                }
                _frames = Art.Frames(e.Def.Art);
                (_size, _cy) = Art.FrameBox(e.Def.Art, _h);
                _body = WorldFx.Spr(_root, _frames[0], new Vector2(0, _cy), _size, 0, null, "body");
                _body.flipX = Random.value < 0.5f && !e.Def.IsBoss;
                Bend.Use(_body);
                _bend = Bend.Meta(e.Def.Art);
                _flash = Bend.Use(WorldFx.Spr(_body.transform, _body.sprite, Vector2.zero, 1f, 0, new Color(1, 1, 1, 0), "flash"), true);
                _flash.flipX = _body.flipX;
                if (e.Def.Kind == EnemyKind.Porter)
                    _tag = WorldFx.Spr(_root, Art.Img("ic_chest"), new Vector2(0, _h + 0.3f), 0.45f, 0, null, "tag");

                _barRoot = new GameObject("bar").transform;
                _barRoot.SetParent(_root, false);
                _bw = e.Big ? 1.2f : e.Def.Kind == EnemyKind.Porter ? 0.86f : 0.6f;
                _bh = e.Big ? 0.12f : 0.075f;
                _barRoot.localPosition = new Vector2(0, _h + (e.Def.Kind == EnemyKind.Porter ? 0.62f : 0.12f) + (e.Def.Flying ? FlyH : 0f));
                // 参考：深色细描边 + 暗红槽 + 亮红填充（上半一道高光），掉血时后面拖一截白色残影。
                _barBg = BarPiece("bg", _bw + 0.05f, _bh + 0.05f, new Color(0.08f, 0.05f, 0.04f, 0.92f));
                BarPiece("track", _bw, _bh, new Color(0.25f, 0.12f, 0.1f, 1f));
                _barGhost = BarPiece("ghost", _bw, _bh, new Color(1f, 0.93f, 0.8f, 0.95f));
                _bar = BarPiece("fill", _bw, _bh, e.Def.Kind == EnemyKind.Porter ? Pal.Hex("F2B531") : Pal.Hex("E8412F"));
                _barHi = BarPiece("hi", _bw - 0.02f, _bh * 0.38f, new Color(1f, 1f, 1f, 0.32f));
                _barHi.transform.localPosition = new Vector2(0, _bh * 0.2f);
                _barRoot.gameObject.SetActive(false);
                if (e.Def.IsBoss) _barRoot.localScale = Vector3.zero;
                _lastX = e.Pos.X;
                _lastY = e.Pos.Y;
                _step = Random.Range(0f, 2f);
                Tick(false);
                // 出场从门里淡入
                var body = _body;
                body.color = new Color(1, 1, 1, 0);
                Tween.Run(body, 0.4f, k => { if (body) body.color = body.color.A(k); });
            }

            public void Tick(bool rage)
            {
                var e = _e;
                var p = e.Pos.ToV();
                // 都停在同一条线上会排成一字，表现层给每个兵一点纵深。
                float near = Mathf.Clamp01((e.Def.StopY + 1.5f - p.y) / 1.5f);
                p.y += _jit * near;
                _root.localPosition = p;
                int order = WorldFx.Order(p.y);
                _shadow.sortingOrder = 120;
                if (_aura) { _aura.sortingOrder = 121; _aura.color = _aura.color.A(0.35f + 0.2f * Mathf.Sin(Time.time * 5f)); }
                _body.sortingOrder = order;
                if (_tag) { _tag.sortingOrder = order + 2; _tag.transform.localPosition = new Vector2(0, _h + 0.3f + Mathf.Sin(Time.time * 5f) * 0.06f); }
                int bo = order + 3;
                foreach (Transform piece in _barRoot) piece.GetComponent<SpriteRenderer>().sortingOrder = bo++;

                float t = Time.time + _phase;
                bool walking = !e.Attacking && e.Stun <= 0;
                bool cav = e.Def.Kind == EnemyKind.Cavalry;
                // 步伐按真实移动距离推进（脚不打滑）。一步 = _step 走 1，左右腿轮流迈。
                float moved = walking ? Mathf.Sqrt((e.Pos.X - _lastX) * (e.Pos.X - _lastX) + (e.Pos.Y - _lastY) * (e.Pos.Y - _lastY)) : 0f;
                _lastX = e.Pos.X;
                _lastY = e.Pos.Y;
                if (moved > 0.5f) moved = 0f;
                float stride = _h * (cav ? 0.36f : e.Big ? 0.24f : 0.19f);
                int stepBefore = (int)_step;
                _step += moved / stride;
                if (e.Big && (int)_step != stepBefore && walking)
                    WorldFx.I.Dust(p + new Vector2(0, -0.02f), _h * 0.5f, DustCol.A(0.28f), 0.4f, 95);
                _walkW = Mathf.MoveTowards(_walkW, walking && moved > 0f ? 1f : 0f, Time.deltaTime * 6f);

                // 走路只用站立帧，靠形变迈腿（AI 画的走路帧两腿几乎一样，来回切只会一跳一跳）。攻击才切蓄力/出手帧。
                int frame = 0;
                float lunge = 0f;
                bool fly = e.Def.Flying;
                if (fly) frame = (int)(t * 7f) % 2;
                if (e.Attacking && e.Stun <= 0)
                {
                    float period = e.Def.Kind == EnemyKind.Crossbow ? 1.6f : 1f;
                    float k = Mathf.Clamp01(e.AtkCd / period);
                    frame = k > 0.72f ? 3 : k < 0.32f ? 2 : 0;
                    lunge = k > 0.8f ? (k - 0.8f) / 0.2f : 0f;
                }
                frame %= _frames.Length;
                if (_body.sprite != _frames[frame]) { _body.sprite = _frames[frame]; _flash.sprite = _body.sprite; }

                // 迈步（正面走路的经典脚轨迹）：每条腿一个周期两步——
                // 摆动步：脚从后方（画面偏上）抬起、往前（画面偏下）落；支撑步：脚踩死在地上，身体往前走，
                // 所以相对身体它从前慢慢退到后。左右腿差半个周期。身体重心压到支撑腿那边。
                float s = Mathf.Sin(_step * Mathf.PI) * _walkW;
                float reach = (cav ? 0.05f : e.Big ? 0.045f : 0.055f) * _walkW;
                float raise = (cav ? 0.06f : e.Big ? 0.075f : 0.1f) * _walkW;
                float legL = Foot(_step, reach, raise);
                float legR = Foot(_step + 1f, reach, raise);
                float pass = Mathf.Abs(s);
                float bob = (pass - 0.5f) * (cav ? 0.03f : 0.018f) * _walkW;
                float shear = s * (e.Big ? 0.018f : 0.028f);
                float landing = (1f - pass) * _walkW;
                float breathe = _walkW < 0.5f ? Mathf.Sin(t * 3.2f) * 0.02f * (1f - _walkW) : 0f;
                // 受击：整身泛红（叠加）+ 被打得往后仰、压扁一下；出手那一下往前扑。
                if (e.HitT > 0.09f) _redT = 0.22f;
                else if (_redT > 0) _redT -= Time.deltaTime;
                float hit = Mathf.Clamp01(_redT / 0.22f);
                shear += hit * hit * (_body.flipX ? 0.07f : -0.07f) * (e.Big ? 0.5f : 1f);
                float sx = 1f + hit * 0.07f + landing * 0.012f + lunge * 0.05f - breathe * 0.4f;
                float sy = 1f - hit * 0.06f - landing * 0.015f - lunge * 0.07f + breathe;
                if (e.Attacking) bob = -lunge * 0.04f;
                float lift = fly ? FlyH + Mathf.Sin(t * 4f) * 0.08f : 0f;
                if (fly) { legL = legR = 0f; bob = 0f; }
                _body.transform.localPosition = new Vector2(0, _cy + lift);
                _body.transform.localRotation = Quaternion.identity;
                _body.transform.localScale = Vector3.one * _size;
                if (fly) _shadow.transform.localScale = new Vector3(_h * 0.55f, _h * 0.16f, 1);
                Bend.Set(_body, _bend, bob, shear, sx, sy, legL, legR);
                Bend.Set(_flash, _bend, bob, shear, sx, sy, legL, legR);
                if (e.ChargeT > 0 && Random.value < 0.5f) WorldFx.I.Dust(p, _h * 0.8f, DustCol.A(0.5f), 0.5f, 95);
                if (e.Shield > 0)
                {
                    if (_shield == null) _shield = WorldFx.Additive(WorldFx.Spr(_root, Art.Ring, new Vector2(0, _h * 0.5f), _h * 1.25f, 0, Pal.Gold.A(0.6f), "shield"));
                    _shield.sortingOrder = order + 4;
                    _shield.color = Pal.Gold.A(0.35f + 0.2f * Mathf.Sin(t * 6f));
                    _shield.transform.localScale = Vector3.one * _h * (1.25f + 0.04f * Mathf.Sin(t * 4f));
                }
                else if (_shield != null)
                {
                    WorldFx.I.Ring(p + new Vector2(0, _h * 0.5f), _h * 0.8f, Pal.Gold, 0.4f);
                    Object.Destroy(_shield.gameObject);
                    _shield = null;
                }
                Color c = Color.white;
                if (e.SlowT > 0) c = Pal.Hex("A8D8FF");
                if (rage) c = Color.Lerp(c, Pal.Hex("FF9C8C"), 0.35f);
                if (hit > 0) c = Color.Lerp(c, Pal.Hex("FF7A6A"), hit);
                _body.color = c.A(_body.color.a);
                _flash.sortingOrder = order + 1;
                _flash.color = new Color(1f, 0.32f, 0.26f, 0.85f * hit * _body.color.a);

                bool showBar = !e.Def.IsBoss && (e.Big || e.Def.Kind == EnemyKind.Porter || e.Hp < e.MaxHp);
                if (_barRoot.gameObject.activeSelf != showBar) _barRoot.gameObject.SetActive(showBar);
                if (showBar)
                {
                    float frac = Mathf.Clamp01(e.Hp / e.MaxHp);
                    _ghostFrac = _ghostFrac < frac ? frac : Mathf.MoveTowards(_ghostFrac, frac, Time.deltaTime * 0.9f);
                    SetBarWidth(_bar, frac, _bw, _bh);
                    SetBarWidth(_barGhost, _ghostFrac, _bw, _bh);
                    SetBarWidth(_barHi, frac, _bw - 0.02f, _bh * 0.38f);
                    _barHi.color = _barHi.color.A(frac > 0.02f ? 0.32f : 0f);
                }

                if (e.Stun > 0)
                {
                    if (_stun == null) _stun = WorldFx.Spr(_root, Art.Img("fx_star"), new Vector2(0, _h + 0.1f), 0.32f, 0, null, "stun");
                    _stun.sortingOrder = order + 5;
                    _stun.transform.localPosition = new Vector2(Mathf.Cos(t * 8f) * 0.2f, _h + 0.08f + Mathf.Sin(t * 8f) * 0.05f);
                    _stun.transform.localRotation = Quaternion.Euler(0, 0, t * 300f);
                }
                else if (_stun != null)
                {
                    Object.Destroy(_stun.gameObject);
                    _stun = null;
                }
            }

            public void Die()
            {
                var fx = WorldFx.I;
                var p = _e.Pos.ToV();
                bool big = _e.Big;
                _barRoot.gameObject.SetActive(false);
                if (_stun) _stun.gameObject.SetActive(false);
                if (_tag) _tag.gameObject.SetActive(false);
                _flash.gameObject.SetActive(false);
                fx.Flash(p + new Vector2(0, _h * 0.45f), _h * 1.2f, new Color(1f, 0.95f, 0.85f, 0.5f), 0.15f);
                fx.Dust(p, _h * 1.3f, DustCol.A(0.7f), 0.6f, WorldFx.Order(p.y) + 20);
                fx.Sparks(Art.SoftDot, p + new Vector2(0, _h * 0.3f), big ? 10 : 4, big ? 0.4f : 0.22f, big ? 3.5f : 2.2f, Pal.Hex("D6CBB5").A(0.85f), 0.45f, 9300, 5f);
                if (big) fx.Debris(p, 4, 0.22f, 2.4f);
                if (_e.Def.IsBoss)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        var bp = p + new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(0.2f, 2.2f));
                        fx.Burst(Art.Img("fx_fireball"), bp, Random.Range(1.6f, 2.4f), 0.5f, 9610, null, 0, 0.3f, true);
                        fx.Burst(Art.Img("fx_explode"), bp, Random.Range(1.6f, 2.6f), 0.6f, 9600);
                    }
                    fx.LightPool(p, 6f, Fire.A(0.9f), 0.9f);
                    fx.Decal(Art.Img("fx_crack"), p, 3.4f, new Color(0.1f, 0.07f, 0.05f, 0.7f), 4f, 0.5f, 91);
                    fx.Decal(Art.Img("fx_scorch"), p, 3.6f, ScorchCol, 4f);
                    fx.Smoke(p + new Vector2(0, 0.8f), 7, 1.6f, SmokeCol, 1.6f, 1.4f, 1.4f);
                    fx.Debris(p, 10, 0.3f, 3.5f, 1.3f);
                    fx.GroundWave(p, 3.5f, Pal.Hex("FFD9A0"), 0.8f);
                    fx.ScreenFlash(Color.white.A(0.5f), 0.4f);
                    fx.Shake(0.35f, 0.6f);
                    Sfx.Play(Snd.UltBomb);
                }
                var body = _body;
                var root = _root;
                float side = body.flipX ? 1 : -1;
                var sh = _shadow;
                Tween.Run(root, 0.4f, k =>
                {
                    if (!body) return;
                    body.transform.localRotation = Quaternion.Euler(0, 0, side * 80f * Tween.OutCubic(k));
                    body.transform.localPosition = new Vector2(side * -0.15f * k, _cy * (1 - 0.6f * k));
                    body.color = body.color.A(1 - k);
                    sh.color = sh.color.A(0.42f * (1 - k));
                }, () => { if (root) Object.Destroy(root.gameObject); });
            }

            public void Escape()
            {
                var body = _body;
                var root = _root;
                Tween.Run(root, 0.5f, k =>
                {
                    if (!body) return;
                    body.color = body.color.A(1 - k);
                    root.localScale = Vector3.one * (1 - 0.3f * k);
                }, () => { if (root) Object.Destroy(root.gameObject); });
            }

            public void Destroy() => Object.Destroy(_root.gameObject);
        }

        sealed class ShotView
        {
            readonly Shot _s;
            readonly Transform _root;
            readonly SpriteRenderer _sr, _shadow, _fuse;
            readonly TrailRenderer _trail;
            float _puffAt;
            bool _started;

            static Material _trailMat;

            public ShotView(Shot s, Transform parent)
            {
                _s = s;
                _root = new GameObject("shot").transform;
                _root.SetParent(parent, false);
                switch (s.Kind)
                {
                    case ShotKind.Arrow:
                    {
                        // 参考：实体木箭，抛物线飞行、箭身随速度转向，地上有影子，尾部一缕淡白风痕；暴击箭头带金光。
                        _shadow = WorldFx.Spr(_root, Art.SoftDot, Vector2.zero, 1, 125, new Color(0, 0, 0, 0.22f), "shadow");
                        _sr = WorldFx.Spr(_root, Art.Img("fx_arrow"), Vector2.zero, s.Crit ? 0.98f : 0.86f, 9200);
                        if (s.Crit) _fuse = WorldFx.Additive(WorldFx.Spr(_sr.transform, Art.Sparkle, new Vector2(0, 0.42f), 0.6f, 9201, Pal.Hex("FFC54A"), "glint"));
                        _trail = AddTrail(0.09f, 0.07f, s.Crit ? Pal.Hex("FFD27A") : Color.white, false);
                        break;
                    }
                    case ShotKind.FireArrow:
                        _sr = WorldFx.Spr(_root, Art.Img("fx_arrow"), Vector2.zero, 0.85f, 9250);
                        _fuse = WorldFx.Additive(WorldFx.Spr(_sr.transform, Art.Img("fx_fireball"), new Vector2(0, 0.36f), 0.42f, 9251, Color.white, "flame"));
                        _trail = AddTrail(0.16f, 0.16f, Fire);
                        break;
                    case ShotKind.Bomb:
                    case ShotKind.MiniBomb:
                    case ShotKind.BigBomb:
                    {
                        float size = s.Kind == ShotKind.BigBomb ? 1.4f : s.Kind == ShotKind.MiniBomb ? 0.3f : 0.48f;
                        _shadow = WorldFx.Spr(_root, Art.SoftDot, Vector2.zero, size, 130, new Color(0, 0, 0, 0.35f));
                        _sr = WorldFx.Spr(_root, Art.Img("ic_bomb"), Vector2.zero, size, 9200);
                        _fuse = WorldFx.Additive(WorldFx.Spr(_sr.transform, Art.Sparkle, new Vector2(0.2f, 0.45f), 0.8f, 9201, Fire.A(0.9f), "fuse"));
                        if (s.Kind == ShotKind.BigBomb) _trail = AddTrail(0.5f, 0.35f, Pal.Orange);
                        break;
                    }
                    case ShotKind.Bolt:
                        _sr = WorldFx.Spr(_root, Art.Img("ic_arrow"), Vector2.zero, 0.32f, 9200, Pal.Hex("5A5048"));
                        break;
                    case ShotKind.Punch:
                        _sr = WorldFx.Spr(_root, Art.Img("ic_fist"), Vector2.zero, s.Ult ? 0.75f : 0.45f, 9200);
                        if (!s.Ult) _trail = AddTrail(0.14f, 0.3f, Pal.Hex("FF8060"));
                        break;
                    case ShotKind.Stone:
                        _shadow = WorldFx.Spr(_root, Art.SoftDot, Vector2.zero, 1, 125, new Color(0, 0, 0, 0.22f), "shadow");
                        _sr = WorldFx.Spr(_root, Art.Img("fx_rock" + Random.Range(0, 3)), Vector2.zero, s.Ult ? 0.45f : 0.3f, 9200);
                        _trail = AddTrail(0.08f, s.Ult ? 0.16f : 0.1f, Pal.Hex("E8E0D0"), false);
                        break;
                    case ShotKind.Spear:
                        _sr = WorldFx.Spr(_root, Art.Img("ic_sword"), Vector2.zero, 0.8f, 9200, Pal.Hex("E8F2FF"));
                        _trail = AddTrail(0.1f, 0.18f, Pal.Hex("CFE8FF"));
                        break;
                    case ShotKind.Wind:
                        _sr = WorldFx.Additive(WorldFx.Spr(_root, Art.Img("fx_wave"), Vector2.zero, s.Ult ? 1.4f : 0.6f, 9200, Pal.Hex("9FF5D8").A(0.85f)));
                        _trail = AddTrail(0.15f, s.Ult ? 0.5f : 0.22f, Pal.Hex("B8FFE6"));
                        break;
                    case ShotKind.Hammer:
                        _shadow = WorldFx.Spr(_root, Art.SoftDot, Vector2.zero, 1, 125, new Color(0, 0, 0, 0.3f), "shadow");
                        _sr = WorldFx.Spr(_root, Art.Img("fx_rock1"), Vector2.zero, 0.6f, 9200, Pal.Hex("8C8C96"));
                        _trail = AddTrail(0.2f, 0.25f, Pal.Hex("FFB070"));
                        break;
                    case ShotKind.Ball:
                        _shadow = WorldFx.Spr(_root, Art.SoftDot, Vector2.zero, 1, 125, new Color(0, 0, 0, 0.3f), "shadow");
                        _sr = WorldFx.Additive(WorldFx.Spr(_root, Art.Img("fx_fireball"), Vector2.zero, 0.9f, 9200));
                        _trail = AddTrail(0.3f, 0.5f, Fire);
                        break;
                    case ShotKind.Lightning:
                    case ShotKind.Slam:
                    case ShotKind.Frost:
                        break;
                }
                Tick();
            }

            static Material _plainMat;

            TrailRenderer AddTrail(float time, float width, Color c, bool add = true)
            {
                if (_trailMat == null) _trailMat = WorldFx.AddMat;
                if (_plainMat == null) _plainMat = new Material(Shader.Find("Sprites/Default"));
                var tr = (_sr != null ? _sr.gameObject : _root.gameObject).AddComponent<TrailRenderer>();
                tr.sharedMaterial = add ? _trailMat : _plainMat;
                tr.time = time;
                tr.startWidth = width;
                tr.endWidth = 0f;
                tr.startColor = c.A(0.75f);
                tr.endColor = c.A(0);
                tr.sortingOrder = 9150;
                tr.minVertexDistance = 0.05f;
                tr.emitting = false;
                return tr;
            }

            public void Tick()
            {
                var s = _s;
                bool visible = s.T >= 0;
                if (!visible)
                {
                    _root.gameObject.SetActive(false);
                    return;
                }
                if (!_started)
                {
                    _started = true;
                    _root.gameObject.SetActive(true);
                    if (_sr) _sr.enabled = true;
                    if (_shadow) _shadow.enabled = true;
                    if (s.Kind == ShotKind.Lightning)
                    {
                        var from = s.From.ToV() + (s.Ult ? Vector2.zero : new Vector2(0, 0.4f));
                        if (!s.Ult) WorldFx.I.Arc(from, s.To.ToV() + new Vector2(0, 0.35f), Pal.Hex("8FD0FF"), 0.14f, 0.18f);
                    }
                }
                float k = Mathf.Clamp01(s.T / Mathf.Max(0.0001f, s.Dur));
                var a = s.From.ToV();
                var b = s.To.ToV();
                switch (s.Kind)
                {
                    case ShotKind.Arrow:
                    {
                        _root.localPosition = Vector2.zero;
                        var p = ArrowAt(a, b, k);
                        var ahead = ArrowAt(a, b, Mathf.Min(1f, k + 0.03f)) - ArrowAt(a, b, Mathf.Max(0f, k - 0.03f));
                        _sr.transform.localPosition = p;
                        _sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg - 90f);
                        var g = Vector2.Lerp(a, b, k);
                        _shadow.transform.localPosition = g;
                        _shadow.transform.localScale = new Vector3(0.42f, 0.09f, 1f);
                        if (_fuse) _fuse.transform.localRotation = Quaternion.Euler(0, 0, Time.time * 360f);
                        if (_trail && !_trail.emitting && k > 0.04f) _trail.emitting = true;
                        break;
                    }
                    case ShotKind.Bolt:
                    case ShotKind.Punch:
                    {
                        a += new Vector2(0, s.Kind == ShotKind.Bolt ? 0.5f : 0.35f);
                        b += new Vector2(0, s.Kind == ShotKind.Bolt ? 0.2f : 0.4f);
                        if (s.Kind == ShotKind.Punch && s.Ult) a = b + new Vector2(0, 1.2f);
                        var p = Vector2.Lerp(a, b, k);
                        _root.localPosition = p;
                        var d = b - a;
                        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                        if (s.Kind == ShotKind.Bolt) _sr.transform.localRotation = Quaternion.Euler(0, 0, ang - 45f);
                        else _sr.transform.localRotation = Quaternion.Euler(0, 0, ang - 90f);
                        if (_trail && !_trail.emitting && k > 0.05f) _trail.emitting = true;
                        break;
                    }
                    case ShotKind.FireArrow:
                    {
                        var start = b + new Vector2(-1.4f, 5.5f);
                        var p = Vector2.Lerp(start, b, Tween.InCubic(k) * 0.4f + k * 0.6f);
                        _root.localPosition = p + new Vector2(0, 0.3f);
                        var d = b - start;
                        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                        _sr.transform.localRotation = Quaternion.Euler(0, 0, ang - 90f);
                        if (_fuse) _fuse.transform.localScale = Vector3.one * (0.4f + 0.08f * Mathf.Sin(Time.time * 30f));
                        if (_trail && !_trail.emitting) _trail.emitting = true;
                        break;
                    }
                    case ShotKind.Bomb:
                    case ShotKind.MiniBomb:
                    case ShotKind.BigBomb:
                    {
                        float h = s.Kind == ShotKind.BigBomb ? 5f : s.Kind == ShotKind.MiniBomb ? 0.8f : 1.8f;
                        if (s.Kind != ShotKind.MiniBomb) a += new Vector2(0, 0.5f);
                        var ground = Vector2.Lerp(a, b, k);
                        _root.localPosition = ground;
                        float z = h * 4f * k * (1 - k);
                        _sr.transform.localPosition = new Vector2(0, z + 0.15f);
                        _sr.transform.localRotation = Quaternion.Euler(0, 0, -k * 400f);
                        // 参考：越高越大（离镜头近），引信火星闪烁，身后拖一串灰烟。
                        float baseSize = s.Kind == ShotKind.BigBomb ? 1.4f * (0.7f + 0.5f * k) : s.Kind == ShotKind.MiniBomb ? 0.3f : 0.5f;
                        float sz = baseSize * (1f + z * 0.12f);
                        _sr.transform.localScale = Vector3.one * sz;
                        if (_fuse)
                        {
                            _fuse.color = Pal.Hex("FFD27A").A(0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 40f)));
                            _fuse.transform.localScale = Vector3.one * (0.8f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 33f)));
                        }
                        _shadow.transform.localScale = new Vector3(1f, 0.4f, 1f) * baseSize * (0.5f + 0.5f / (1f + z * 0.4f));
                        _shadow.color = new Color(0, 0, 0, 0.4f / (1f + z * 0.3f));
                        if (_trail && !_trail.emitting) _trail.emitting = true;
                        if (s.Kind != ShotKind.MiniBomb && Time.time >= _puffAt)
                        {
                            _puffAt = Time.time + (s.Kind == ShotKind.BigBomb ? 0.05f : 0.09f);
                            var at = (Vector2)_root.localPosition + new Vector2(0, z + 0.15f);
                            WorldFx.I.Smoke(at, 1, sz * 0.6f, new Color(0.62f, 0.6f, 0.58f, 0.4f), 0.45f, 0.04f, 0.15f, 9150);
                        }
                        break;
                    }
                    case ShotKind.Stone:
                    case ShotKind.Hammer:
                    case ShotKind.Ball:
                    {
                        float h = s.Kind == ShotKind.Stone ? 0.5f + (b - a).magnitude * 0.06f : 2.4f;
                        a += new Vector2(0, s.Kind == ShotKind.Stone ? 0.5f : 1.6f);
                        b += new Vector2(0, s.Kind == ShotKind.Stone ? 0.4f : 0.2f);
                        _root.localPosition = Vector2.zero;
                        var g = Vector2.Lerp(a, b, k);
                        float z = h * 4f * k * (1 - k);
                        _sr.transform.localPosition = g + new Vector2(0, z);
                        _sr.transform.localRotation = Quaternion.Euler(0, 0, -Time.time * 720f);
                        if (_shadow)
                        {
                            _shadow.transform.localPosition = g - new Vector2(0, 0.3f);
                            _shadow.transform.localScale = new Vector3(0.4f, 0.1f, 1f) * (s.Kind == ShotKind.Stone ? 0.8f : 2f);
                        }
                        if (_trail && !_trail.emitting && k > 0.05f) _trail.emitting = true;
                        break;
                    }
                    case ShotKind.Spear:
                    {
                        a += new Vector2(0, 0.45f);
                        b += new Vector2(0, 0.45f);
                        var d = b - a;
                        _root.localPosition = Vector2.Lerp(a, b, Tween.OutCubic(k));
                        _sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 45f);
                        if (_trail && !_trail.emitting) _trail.emitting = true;
                        break;
                    }
                    case ShotKind.Wind:
                    {
                        a += new Vector2(0, 0.5f);
                        b += new Vector2(0, 0.35f);
                        _root.localPosition = Vector2.Lerp(a, b, k) + new Vector2(Mathf.Sin(k * 9f) * 0.15f, 0);
                        _sr.transform.localRotation = Quaternion.Euler(0, 0, Time.time * 900f);
                        if (_trail && !_trail.emitting) _trail.emitting = true;
                        break;
                    }
                    case ShotKind.Lightning:
                    case ShotKind.Slam:
                    case ShotKind.Frost:
                        _root.localPosition = b;
                        break;
                }
            }

            /// <summary>箭的屏幕位置：地面直线 + 从弓高到胸口的高度，中段抛起一点弧。</summary>
            static Vector2 ArrowAt(Vector2 a, Vector2 b, float k)
            {
                float dist = (b - a).magnitude;
                float z = Mathf.Lerp(0.5f, 0.45f, k) + Mathf.Min(1.4f, dist * 0.09f) * 4f * k * (1 - k);
                return Vector2.Lerp(a, b, k) + new Vector2(0, z);
            }

            public void Destroy() => Object.Destroy(_root.gameObject);
        }
    }

    static class V2Ext
    {
        public static float Pivot = -2.6f;
        public static float Stretch = 1f;
        public static float XStretch = 1f;

        /// <summary>
        /// 规则坐标转世界坐标；Pivot 以上纵向按 Stretch 拉长（高屏战场更长），
        /// 横向越靠近出生线越按 XStretch 张开，让出生点落在背景门洞上。
        /// </summary>
        public static Vector2 ToV(this V2 v)
        {
            if (v.Y <= Pivot) return new Vector2(v.X, v.Y);
            float f = Mathf.Clamp01((v.Y - Pivot) / (Layout.SpawnY - Pivot));
            return new Vector2(v.X * (1f + (XStretch - 1f) * f * f), Pivot + (v.Y - Pivot) * Stretch);
        }
    }
}
