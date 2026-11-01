using System;
using System.Collections;
using System.IO;
using Liangshan.Sim;
using UnityEngine;

namespace Liangshan
{
    /// <summary>
    /// 入口：空场景启动后自动挂上。负责镜头、主界面 ↔ 战斗切换。
    /// 命令行带 -shots &lt;目录&gt; 时进入自检模式：机器人自动打一局，在关键节点截图后退出。
    /// </summary>
    public sealed class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        /// <summary>游戏自己的镜头。编辑器里打开的场景可能自带 Main Camera，不能用 Camera.main。</summary>
        public static Camera Cam { get; private set; }

        Camera _cam;
        RectTransform _floatLayer;
        BattleView _battle;
        HomeScreen _home;
        Vector2Int _screen;
        string _shots;
        /// <summary>只截局外界面（调界面布局时用，几十秒跑完）。</summary>
        bool _metaOnly;
        int _autoSkill = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Game");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Game>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Input.multiTouchEnabled = false;
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Pal.Hex("1C2128");
            _cam.nearClipPlane = -50;
            _cam.farClipPlane = 50;
            _cam.depth = 100;
            camGo.AddComponent<AudioListener>();
            Cam = _cam;
            MuteForeignCameras();
            FitCamera();

            Ui.Init(transform);
            Tween.Init(gameObject);
            Sfx.Init(gameObject);
            _floatLayer = Ui.Rect(Ui.Root, "FloatLayer");
            _floatLayer.anchorMin = Vector2.zero;
            _floatLayer.anchorMax = Vector2.one;
            _floatLayer.offsetMin = _floatLayer.offsetMax = Vector2.zero;
            WorldFx.Create(transform, _floatLayer, _cam.transform);

            ParseArgs();
            Profile.Load();
            Sfx.On = Profile.S.Sound == 1;
            Sfx.Music = Profile.S.Music == 1;
            if (_shots != null) StartCoroutine(SelfCheck());
            else if (Application.isBatchMode) ShowHome();
            else
            {
                CloudSync.Imported += OnCloudImported;
                var boot = BootCover();
                CloudSync.Startup(() =>
                {
                    Destroy(boot.gameObject);
                    ShowHome();
                });
            }
        }

        /// <summary>等云存档（最多几秒）时盖住画面：先拉云再进首页，免得本地改动和云端打架。</summary>
        RectTransform BootCover()
        {
            var layer = Ui.Rect(Ui.Root, "boot");
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            var bg = Ui.Box(layer, new Vector2(1080, 1920), Vector2.zero, Color.white, Art.Img("home_bg"), "bg");
            bg.type = UnityEngine.UI.Image.Type.Simple;
            float scale = Mathf.Max(Ui.W / 1080f, Ui.Height / 1920f);
            bg.rectTransform.sizeDelta = new Vector2(1080, 1920) * scale;
            Ui.Fill(layer, new Color(0, 0, 0, 0.35f), true);
            Ui.Label(layer, "读取存档…", 46, Color.white, new Vector2(0, -Ui.Height * 0.5f + 260), new Vector2(900, 90), TextAnchor.MiddleCenter, Fonts.Heavy);
            return layer;
        }

        void OnDestroy() => CloudSync.Imported -= OnCloudImported;

        /// <summary>局中被云端覆盖不打断，回首页时自然用新档。</summary>
        void OnCloudImported()
        {
            Sfx.On = Profile.S.Sound == 1;
            Sfx.Music = Profile.S.Music == 1;
            if (_home) ShowHome(_home.Tab);
        }

        void ParseArgs()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-shots") _shots = args[i + 1];
            }
            _metaOnly = Array.IndexOf(args, "-quickmeta") >= 0;
            if (_shots != null)
            {
                Directory.CreateDirectory(_shots);
                PlayerPrefs.DeleteAll();
                Profile.Volatile = true;
                Ads.Fast = true;
            }
        }

        void MuteForeignCameras()
        {
            foreach (var c in FindObjectsOfType<Camera>())
                if (c != _cam) c.enabled = false;
            foreach (var l in FindObjectsOfType<AudioListener>())
                if (l.gameObject != _cam.gameObject) l.enabled = false;
        }

        /// <summary>界面根节点让出底部横条后，战场跟着抬：世界 y = -9.6 对齐到横条上沿，横条里照常画战场。</summary>
        void FitCamera()
        {
            _screen = new Vector2Int(Screen.width, Screen.height);
            float lift = Ui.SafeBottomPixels;
            float area = Mathf.Max(1f, Screen.height - lift);
            float aspect = area / Mathf.Max(1f, Screen.width);
            float half = aspect > 16f / 9f ? 5.4f * aspect : 9.6f;
            float unitsPerPixel = 2f * half / area;
            _cam.orthographicSize = Screen.height * unitsPerPixel * 0.5f;
            var p = new Vector3(0, -9.6f - lift * unitsPerPixel + _cam.orthographicSize, -10);
            _cam.transform.localPosition = p;
            if (WorldFx.I) WorldFx.I.SetCamBase(p);
        }

        void Update()
        {
            if (_screen.x != Screen.width || _screen.y != Screen.height)
            {
                FitCamera();
                Ui.Fit();
                // 首页按 SafeTop 排版，屏幕一变（微信启动头几帧常见）就整页重搭。
                if (_home) ShowHome(_home.Tab);
            }
            if ((_tick -= Time.unscaledDeltaTime) <= 0)
            {
                _tick = 1f;
                int st = Profile.S.Stamina, day = Profile.S.Day;
                Profile.Tick();
                if (st != Profile.S.Stamina || day != Profile.S.Day) Profile.Save();
            }
        }

        float _tick;

        // ---------------- 流程 ----------------

        void Clear()
        {
            MuteForeignCameras();
            if (_battle) Destroy(_battle.gameObject);
            _battle = null;
            if (_home) Destroy(_home.gameObject);
            _home = null;
            Time.timeScale = 1f;
        }

        public HomeScreen Home => _home;

        public void ShowHome(int tab = 2)
        {
            Clear();
            _home = HomeScreen.Create(tab);
            Sfx.PlayMusic(Bgm.Menu);
        }

        public void StartLevel(int no) => Play(Defs.Level(Mathf.Min(no, Profile.S.Cleared + 1)));

        public void StartDaily() => Play(Defs.DailyStage(Profile.S.Cleared, Profile.Today));

        public void StartEscort() => Play(Defs.EscortStage(Profile.S.Cleared));

        /// <summary>出征检查体力和次数；不够时弹补给窗或提示，返回 false。</summary>
        public bool Play(LevelDef st)
        {
            var s = Profile.S;
            bool auto = _autoSkill >= 0;
            if (st.Mode == StageMode.Daily && s.DailyTries >= Meta.DailyTriesMax && !auto) { Ui.Toast("今日挑战次数已用完", 200); return false; }
            if (st.Mode == StageMode.Escort && s.EscortTries >= Meta.EscortTriesMax && !auto) { Ui.Toast("今日截纲次数已用完", 200); return false; }
            if (!auto && !Meta.Spend(s, Cur.Stamina, Meta.BattleStamina))
            {
                if (_home) MetaUi.StaminaShop(_home.Layer);
                else
                {
                    Ui.Toast("体力不足", 200);
                    ShowHome();
                }
                return false;
            }
            if (st.Mode == StageMode.Daily) s.DailyTries++;
            if (st.Mode == StageMode.Escort) s.EscortTries++;
            if (st.Mode == StageMode.Main) s.Selected = st.No;
            Profile.Save();
            Launch(st);
            return true;
        }

        /// <summary>重开本关：已经扣过体力，不再扣。</summary>
        public void Restart() => Launch(_stage);

        LevelDef _stage;
        int _seed;
        bool _settled;
        Rewards _last;

        void Launch(LevelDef st)
        {
            Clear();
            _stage = st;
            _settled = false;
            var go = new GameObject("Battle");
            go.transform.SetParent(transform, false);
            _battle = go.AddComponent<BattleView>();
            bool tutorial = _autoSkill < 0 && Profile.S.TutorialDone == 0 && st.Mode == StageMode.Main && st.No == 1;
            _seed = _autoSkill >= 0 ? 20261031 + _autoSkill : Environment.TickCount;
            _battle.Begin(_seed, st, Meta.BuildLoadout(Profile.S), tutorial);
            if (_autoSkill >= 0)
            {
                _battle.Auto = new Bot(_autoSkill);
                _battle.Speed = 2f;
            }
            else _battle.Speed = Profile.S.Speed15 == 1 && PlayerPrefs.GetInt("speed15", 0) == 1 ? 1.5f : 1f;
            _floatLayer.SetAsFirstSibling();
            Sfx.PlayMusic(st.Mode == StageMode.Main && st.IsChapterBoss ? Bgm.Boss : Bgm.Battle);
        }

        Rewards Compute(Battle b, bool win) =>
            Meta.BattleRewards(Profile.S, b.Stage, win, b.Wave, b.WallMax > 0 ? b.Wall / b.WallMax : 0, b.PortersKilled, new Rng(_seed ^ 0x5A5A));

        public Rewards Preview(Battle b, bool win) => Compute(b, win);

        /// <summary>结算发奖：每局只发一次（失败后复活再赢，按赢算）。</summary>
        public Rewards Settle(Battle b, bool win)
        {
            if (_settled) return _last;
            _settled = true;
            var s = Profile.S;
            var r = Compute(b, win);
            Meta.Give(s, r);
            Meta.FinishMain(s, b.Stage, win, r);
            Meta.MarkSeen(s, b.Seen);
            Profile.Save();
            _last = r;
            return r;
        }

        public void GiveAgain(Rewards r)
        {
            foreach (var it in r.Items) Meta.Give(Profile.S, it);
            Profile.Save();
        }

        // ---------------- 自检截图 ----------------

        bool _autoDone, _autoWon;

        public void OnAutoDone(bool won)
        {
            _autoDone = true;
            _autoWon = won;
        }

        void Shot(string name)
        {
            var path = Path.Combine(_shots, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[自检] 截图 " + path);
        }

        /// <summary>
        /// 走路自检：1 倍速，用一台特写相机跟住一个正在走的怪，每 3 帧拍一张共 16 张，
        /// 拼起来能直接看出两腿是否交替迈步、身体是不是在弹。
        /// </summary>
        IEnumerator WalkBurst(EnemyKind kind, string prefix)
        {
            Enemy target = null;
            foreach (var e in _battle.B.Enemies)
                if (e.Def.Kind == kind && !e.Attacking && e.Stun <= 0 && (target == null || e.Pos.Y > target.Pos.Y)) target = e;
            if (target == null) yield break;
            float keep = _battle.Speed;
            _battle.Speed = 1f;
            var cam = new GameObject("walkCam").AddComponent<Camera>();
            cam.CopyFrom(_cam);
            cam.depth = _cam.depth + 1;
            cam.orthographicSize = target.Def.Size * 1.6f;
            for (int i = 0; i < 16; i++)
            {
                for (int f = 0; f < 3; f++)
                {
                    var p = target.Pos.ToV();
                    cam.transform.position = new Vector3(p.x, p.y + target.Def.Size * 0.5f, -10);
                    yield return null;
                }
                Shot(prefix + i.ToString("00"));
            }
            yield return null;
            Destroy(cam.gameObject);
            _battle.Speed = keep;
        }

        IEnumerator WaitReal(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>按真人路径操作一遍：点召唤、按引导拖拽合成、点强化选卡。指针走 BattleView 的输入代码。</summary>
        IEnumerator ManualPlay()
        {
            var v = _battle;
            v.B.Coins += 600;
            v.Hud.PressSummon();
            yield return WaitReal(0.5f);
            for (int i = 0; i < 8 && !v.FindMergePair(out _, out _); i++)
            {
                v.Hud.PressSummon();
                yield return WaitReal(0.25f);
            }
            yield return WaitReal(1.0f);
            Shot("02b_merge_guide");
            yield return WaitReal(0.3f);

            int a = -1, b = -1;
            for (int i = 0; i < v.B.Slots.Count && a < 0; i++)
                for (int j = 0; j < v.B.Slots.Count; j++)
                    if (v.B.CanMerge(i, j)) { a = i; b = j; break; }
            if (a < 0)
            {
                Debug.LogError("[自检] 没凑出可合成的一对");
                yield break;
            }
            var from = v.SlotWorld(a);
            var to = v.SlotWorld(b);
            var ptr = new BattleView.Pointer { Down = true, Held = true, World = from };
            v.FakePointer = () => ptr;
            yield return null;
            ptr.Down = false;
            float t0 = Time.realtimeSinceStartup;
            bool shot = false;
            while (Time.realtimeSinceStartup - t0 < 0.7f)
            {
                float k = (Time.realtimeSinceStartup - t0) / 0.7f;
                ptr.World = Vector2.Lerp(from, to, k) + new Vector2(0, Mathf.Sin(k * Mathf.PI) * 0.6f);
                if (!shot && k > 0.85f) { shot = true; Shot("02c_dragging"); }
                yield return null;
            }
            ptr.World = to;
            yield return null;
            int mergesBefore = v.MergesDone;
            ptr.Held = false;
            ptr.Up = true;
            yield return null;
            v.FakePointer = null;
            yield return WaitReal(0.35f);
            Shot("02d_merged");
            Debug.Log($"[自检] 拖拽合成 {(v.MergesDone > mergesBefore || v.B.Slots[b].Unit?.Star > 1 ? "成功" : "失败")}");
            yield return WaitReal(0.6f);
            Shot("02d2_book_guide");
            yield return WaitReal(0.3f);

            v.Hud.PressEnhance();
            yield return WaitReal(1.0f);
            Shot("02e_enhance_cards");
            v.Hud.ChooseCard(0);
            yield return WaitReal(1.2f);
            Shot("02f_after_pick");
            yield return WaitReal(0.3f);
            Slot info = null;
            foreach (var sl in v.B.Slots)
                if (sl.Unit != null && (info == null || sl.Unit.Star > info.Unit.Star)) info = sl;
            if (info != null)
            {
                v.Hud.SlotInfo(info);
                yield return WaitReal(0.5f);
                Shot("02i_unit_info");
                yield return WaitReal(0.2f);
                v.Hud.SlotInfo(info, 1);
                yield return WaitReal(0.3f);
                Shot("02j_unit_ult");
                yield return WaitReal(0.2f);
                v.Hud.CloseInfo();
            }
            yield return WaitReal(0.3f);
            v.Hud.OpenPanel("stats");
            yield return WaitReal(1.0f);
            Shot("02g_stats");
            v.Hud.ClosePanel();
            yield return WaitReal(0.3f);
            v.Hud.OpenPanel("pause");
            yield return WaitReal(0.6f);
            Shot("02h_pause");
            v.Hud.ClosePanel();
            yield return WaitReal(0.3f);
        }

        void CloseModals()
        {
            if (_home == null) return;
            for (int i = _home.Layer.childCount - 1; i >= 0; i--) DestroyImmediate(_home.Layer.GetChild(i).gameObject);
        }

        /// <summary>截图在帧末才落盘：截完等一下再动界面，免得拍到下一步的半成品。</summary>
        IEnumerator Snap(string name)
        {
            Shot(name);
            yield return WaitReal(0.15f);
        }

        IEnumerator ModalShot(Action open, string name)
        {
            CloseModals();
            open();
            yield return WaitReal(0.9f);
            yield return Snap(name);
        }

        /// <summary>局外各界面截图：先把存档推到“玩了一阵子”的样子。</summary>
        IEnumerator MetaShots()
        {
            _autoSkill = -1;
            var s = Meta.ExpectedSave(12);
            s.Cleared = 11;
            s.Selected = 12;
            for (int i = 0; i < 11; i++) s.Stars[i] = i % 4 == 0 ? 2 : 3;
            s.HeroLv[(int)HeroId.ZhangQing] = 3;
            s.HeroLv[(int)HeroId.WuYong] = 2;
            s.Shards[0] = 30;
            s.Shards[2] = 5;
            s.Tokens = 14;
            s.Ingots = 860;
            s.Copper = 26000;
            s.Iron = 120;
            s.Stamina = 37;
            s.StaminaAt = Profile.Now - 100;
            s.IdleAt = Profile.Now - 3 * 3600 - 1200;
            s.Seen[(int)EnemyKind.Soldier] = 2;
            s.Seen[(int)EnemyKind.Shield] = 1;
            s.Seen[(int)EnemyKind.Cavalry] = 1;
            s.Seen[(int)EnemyKind.Porter] = 2;
            s.Seen[(int)EnemyKind.Hawk] = 1;
            s.Task[0] = 3;
            s.Task[1] = 2;
            s.Task[4] = 1;
            s.SignDays = 2;
            s.SignDay = Profile.Today - 1;
            s.Day = Profile.Today;
            Profile.Use(s);
            ShowHome(HomeScreen.TabBattle);
            yield return WaitReal(1.5f);
            yield return Snap("13_home_ch2");
            yield return ModalShot(() => MetaUi.SignIn(_home.Layer), "14_sign");
            yield return ModalShot(() => MetaUi.Tasks(_home.Layer), "15_tasks");
            yield return ModalShot(() => MetaUi.Idle(_home.Layer), "16_idle");
            yield return ModalShot(() => MetaUi.Codex(_home.Layer), "17_codex");
            yield return ModalShot(() => MetaUi.Daily(_home.Layer), "18_daily");
            yield return ModalShot(() => MetaUi.Escort(_home.Layer), "18b_escort");
            yield return ModalShot(() => MetaUi.StaminaShop(_home.Layer), "18c_stamina");
            yield return ModalShot(() => MetaUi.Chest(_home.Layer, 1, 2), "18d_chest");
            CloseModals();
            _home.SetTab(HomeScreen.TabHero);
            yield return WaitReal(1.0f);
            yield return Snap("19_heroes");
            yield return ModalShot(() => MetaUi.HeroDetail(_home.Layer, HeroId.HuaRong), "20_hero_detail");
            yield return ModalShot(() => MetaUi.HeroDetail(_home.Layer, HeroId.LinChong), "20b_hero_locked");
            CloseModals();
            _home.SetTab(HomeScreen.TabForge);
            yield return WaitReal(1.0f);
            yield return Snap("21_forge");
            yield return ModalShot(() => MetaUi.ForgeSlot(_home.Layer, 0), "22_forge_slot");
            CloseModals();
            _home.SetTab(HomeScreen.TabFort);
            yield return WaitReal(1.0f);
            yield return Snap("23_fort");
            _home.SetTab(HomeScreen.TabShop);
            yield return WaitReal(1.0f);
            yield return Snap("24_shop");
            _home.DoRecruit(10, false);
            yield return WaitReal(2.2f);
            yield return Snap("24b_recruit10");
            CloseModals();
        }

        /// <summary>后期关卡：按标准养成让机器人打一局，看新敌人、新首领和章节战场。</summary>
        IEnumerator AutoBattle(int level, string prefix)
        {
            var s = Meta.ExpectedSave(level);
            s.HeroLv[(int)HeroId.ZhangQing] = Mathf.Max(1, s.HeroLv[(int)HeroId.ZhangQing]);
            s.Lineup = level >= 40 ? new[] { 2, 6, 5, 7 } : new[] { 0, 2, 4, 1 };
            for (int h = 0; h < Defs.HeroCount; h++) if (Array.IndexOf(s.Lineup, h) >= 0) s.HeroLv[h] = Mathf.Max(s.HeroLv[h], s.HeroLv[0]);
            Profile.Use(s);
            _autoSkill = 2;
            Play(Defs.Level(level));
            _battle.Speed = 3f;
            bool boss = false, elite = false;
            float last = 0;
            int n = 0;
            while (true)
            {
                var b = _battle.B;
                var hud = _battle.Hud;
                if (!elite && hud.ModalKind == null)
                    foreach (var e in b.Enemies)
                        if (e.Def.Rank == EnemyRank.Elite && e.Pos.Y < 4f) { elite = true; Shot(prefix + "_elite"); break; }
                if (!boss && b.Wave == b.Stage.BossWave && b.Time > (b.Stage.BossWave - 1) * Defs.WaveTime + 10f && hud.ModalKind == null) { boss = true; Shot(prefix + "_boss"); }
                if (b.Time - last > 40f && hud.ModalKind == null)
                {
                    last = b.Time;
                    Shot($"{prefix}_p{n++}");
                }
                if (b.Phase == Phase.Victory || b.Phase == Phase.Defeat)
                {
                    while (hud.ModalKind != "victory" && hud.ModalKind != "defeat") yield return null;
                    yield return WaitReal(1.4f);
                    Shot(prefix + "_" + (b.Phase == Phase.Victory ? "win" : "lose"));
                    Debug.Log($"[自检] 第{level}关 {b.Phase}，墙 {b.Wall:0}/{b.WallMax:0}，用时 {b.Time:0}s");
                    break;
                }
                yield return null;
            }
            _autoDone = false;
            yield return WaitReal(0.3f);
        }

        IEnumerator SelfCheck()
        {
            if (_metaOnly)
            {
                Sfx.On = false;
                Sfx.Music = false;
                _autoSkill = 0;
                Play(Defs.Level(1));
                yield return WaitReal(2f);
                yield return MetaShots();
                Application.Quit(0);
                yield break;
            }
            Sfx.On = false;
            Sfx.Music = false;
            // 模拟编辑器场景里自带的 Main Camera，确认不会抢走画面。
            var decoy = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            decoy.orthographic = true;
            decoy.orthographicSize = 5;
            decoy.gameObject.AddComponent<AudioListener>();
            ShowHome();
            yield return WaitReal(1.5f);
            Shot("01_home");
            yield return WaitReal(0.5f);

            // 新手引导截一张：真人模式开局。
            StartLevel(1);
            yield return WaitReal(2.5f);
            Shot("02_tutorial");
            yield return WaitReal(0.4f);
            yield return ManualPlay();

            _autoSkill = 2;
            Play(Defs.Level(1));
            bool cards = false, reels = false, boss = false, rage = false, mid = false, early = false, late = false, gate = false, walkBig = false;
            float lastPeriodic = 0;
            int periodic = 0;
            _autoDone = false;
            while (!_autoDone)
            {
                var b = _battle.B;
                var hud = _battle.Hud;
                if (!early && b.Time > 6f)
                {
                    early = true;
                    Shot("03_early");
                    yield return WalkBurst(EnemyKind.Soldier, "03w");
                }
                if (!walkBig && b.Wave >= 3 && hud.ModalKind == null)
                {
                    foreach (var en in b.Enemies)
                        if (en.Def.Kind == EnemyKind.Shield && !en.Attacking && en.Pos.Y < 6f) { walkBig = true; break; }
                    if (walkBig) yield return WalkBurst(EnemyKind.Shield, "03s");
                }
                if (!gate && b.Wave == 2 && _battle.GateOpen > 0.95f && hud.ModalKind == null) { gate = true; Shot("03b_gate_open"); }
                if (!cards && hud.ModalKind == "cards")
                {
                    cards = true;
                    yield return WaitReal(0.9f);
                    Shot("04_cards");
                }
                if (!reels && hud.ModalKind == "reels")
                {
                    reels = true;
                    yield return WaitReal(0.5f);
                    Shot("05_reels_open");
                    yield return WaitReal(2.8f);
                    Shot("06_reels_result");
                }
                if (!mid && b.Time > 45f && hud.ModalKind == null)
                {
                    mid = true;
                    Shot("07_mid");
                    for (int i = 0; i < 3; i++)
                    {
                        yield return WaitReal(0.12f);
                        Shot("07_fx" + i);
                    }
                }
                if (!boss && b.Wave == b.Stage.BossWave && b.Time > (b.Stage.BossWave - 1) * Defs.WaveTime + 9f && hud.ModalKind == null) { boss = true; Shot("08_boss"); }
                if (!rage && b.Wave == b.Stage.RageWave && b.Time > (b.Stage.RageWave - 1) * Defs.WaveTime + 4f && hud.ModalKind == null) { rage = true; Shot("09_rage"); }
                if (!late && b.Wave >= 10 && b.Time > 9 * Defs.WaveTime + 8f && hud.ModalKind == null) { late = true; Shot("10_late"); }
                if (b.Time - lastPeriodic > 30f && hud.ModalKind == null)
                {
                    lastPeriodic = b.Time;
                    Shot($"p{periodic++:00}_t{Mathf.RoundToInt(b.Time)}");
                }
                if (b.Phase == Phase.Victory || b.Phase == Phase.Defeat)
                {
                    while (hud.ModalKind != "victory" && hud.ModalKind != "defeat") yield return null;
                    yield return WaitReal(1.4f);
                    Shot(b.Phase == Phase.Victory ? "11_victory" : "11_defeat_skill2");
                    Debug.Log($"[自检] 第一局结果 {b.Phase}，墙 {b.Wall:0}/{b.WallMax:0}，用时 {b.Time:0}s");
                    yield return WaitReal(0.4f);
                    break;
                }
                yield return null;
            }

            // 失败结算：只会召唤的机器人。
            _autoSkill = 0;
            Play(Defs.Level(1));
            _battle.Speed = 3f;
            while (true)
            {
                var b = _battle.B;
                if (b.Phase == Phase.Defeat || b.Phase == Phase.Victory)
                {
                    while (_battle.Hud.ModalKind != "victory" && _battle.Hud.ModalKind != "defeat") yield return null;
                    yield return WaitReal(1.2f);
                    Shot("12_" + (b.Phase == Phase.Defeat ? "defeat" : "victory_skill0"));
                    break;
                }
                yield return null;
            }
            yield return WaitReal(0.5f);
            yield return MetaShots();
            yield return AutoBattle(35, "25_L35");
            yield return AutoBattle(50, "26_L50");
            Debug.Log("[自检] 完成");
            Application.Quit(0);
        }
    }
}
