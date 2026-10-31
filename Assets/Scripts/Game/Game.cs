using System;
using System.Collections;
using System.IO;
using Liangshan.Sim;
using UnityEngine;

namespace Liangshan
{
    /// <summary>玩家存档：垂直切片只存货币和引导进度。</summary>
    public static class Profile
    {
        public static int Ingots => PlayerPrefs.GetInt("ingots", 0);
        public static int Copper => PlayerPrefs.GetInt("copper", 0);
        public static int Clears => PlayerPrefs.GetInt("clears", 0);

        public static void AddRewards(int ingots, int copper)
        {
            PlayerPrefs.SetInt("ingots", Ingots + ingots);
            PlayerPrefs.SetInt("copper", Copper + copper);
            PlayerPrefs.SetInt("clears", Clears + 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// 入口：空场景启动后自动挂上。负责镜头、主界面 ↔ 战斗切换。
    /// 命令行带 -shots &lt;目录&gt; 时进入自检模式：机器人自动打一局，在关键节点截图后退出。
    /// </summary>
    public sealed class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public static readonly HeroId[] Lineup = { HeroId.HuaRong, HeroId.LingZhen, HeroId.GongSun, HeroId.WuSong };

        /// <summary>游戏自己的镜头。编辑器里打开的场景可能自带 Main Camera，不能用 Camera.main。</summary>
        public static Camera Cam { get; private set; }

        Camera _cam;
        RectTransform _floatLayer;
        BattleView _battle;
        HomeScreen _home;
        Vector2Int _screen;
        string _shots;
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
            if (_shots != null) StartCoroutine(SelfCheck());
            else ShowHome();
        }

        void ParseArgs()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-shots") _shots = args[i + 1];
            }
            if (_shots != null)
            {
                Directory.CreateDirectory(_shots);
                PlayerPrefs.DeleteAll();
            }
        }

        void MuteForeignCameras()
        {
            foreach (var c in FindObjectsOfType<Camera>())
                if (c != _cam) c.enabled = false;
            foreach (var l in FindObjectsOfType<AudioListener>())
                if (l.gameObject != _cam.gameObject) l.enabled = false;
        }

        void FitCamera()
        {
            _screen = new Vector2Int(Screen.width, Screen.height);
            float aspect = Screen.height / Mathf.Max(1f, Screen.width);
            _cam.orthographicSize = aspect > 16f / 9f ? 5.4f * aspect : 9.6f;
            var p = new Vector3(0, -9.6f + _cam.orthographicSize, -10);
            _cam.transform.localPosition = p;
            if (WorldFx.I) WorldFx.I.SetCamBase(p);
        }

        void Update()
        {
            if (_screen.x != Screen.width || _screen.y != Screen.height)
            {
                FitCamera();
                Ui.Fit();
            }
        }

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

        public void ShowHome()
        {
            Clear();
            _home = HomeScreen.Create();
            Sfx.PlayMusic(Bgm.Menu);
        }

        public void StartBattle()
        {
            Clear();
            var go = new GameObject("Battle");
            go.transform.SetParent(transform, false);
            _battle = go.AddComponent<BattleView>();
            bool tutorial = _autoSkill < 0 && PlayerPrefs.GetInt("tutorial_done", 0) == 0;
            int seed = _autoSkill >= 0 ? 20261031 + _autoSkill : Environment.TickCount;
            _battle.Begin(seed, Lineup, tutorial);
            if (_autoSkill >= 0)
            {
                _battle.Auto = new Bot(_autoSkill);
                _battle.Speed = 2f;
            }
            else _battle.Speed = PlayerPrefs.GetInt("speed15", 0) == 1 ? 1.5f : 1f;
            _floatLayer.SetAsFirstSibling();
            Sfx.PlayMusic(Bgm.Battle);
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

        IEnumerator SelfCheck()
        {
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
            StartBattle();
            yield return WaitReal(2.5f);
            Shot("02_tutorial");
            yield return WaitReal(0.4f);
            yield return ManualPlay();

            _autoSkill = 2;
            StartBattle();
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
            StartBattle();
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
            Debug.Log("[自检] 完成");
            Application.Quit(0);
        }
    }
}
