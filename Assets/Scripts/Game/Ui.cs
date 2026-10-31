using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Liangshan
{
    /// <summary>
    /// 界面全部用代码搭：设计稿 1080×1920。比 9:16 更高的屏幕宽度锁 1080、高度拉长；更宽的窗口居中缩放、左右留边。
    /// 战场世界坐标 1 单位 = 100 设计像素，顶边对齐，所以贴着世界物件的界面用 FromTop 换算。
    /// </summary>
    public static class Ui
    {
        public const float W = 1080f;
        public const float H = 1920f;

        public static Canvas Canvas { get; private set; }
        public static RectTransform Root { get; private set; }
        static RectTransform _canvasRt;
        static CanvasScaler _scaler;

        public static bool Tall => Screen.height / Mathf.Max(1f, Screen.width) > H / W + 0.01f;

        public static void Init(Transform host)
        {
            var go = new GameObject("UiCanvas");
            go.transform.SetParent(host, false);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;
            _scaler = go.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(W, H);
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();
            _canvasRt = (RectTransform)go.transform;
            Root = Rect(_canvasRt, "Phone");
            Fit();
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(host, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        public static void Fit()
        {
            float match = Tall ? 0f : 1f;
            if (!Mathf.Approximately(_scaler.matchWidthOrHeight, match))
            {
                _scaler.matchWidthOrHeight = match;
                typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(_scaler, null);
                Canvas.ForceUpdateCanvases();
            }
            var size = _canvasRt.rect.size;
            if (Tall)
            {
                Root.anchorMin = Vector2.zero;
                Root.anchorMax = Vector2.one;
                Root.offsetMin = Root.offsetMax = Vector2.zero;
            }
            else
            {
                Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
                Root.sizeDelta = new Vector2(W, H);
                Root.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>界面高度（设计像素）。宽窗口恒为 1920。</summary>
        public static float Height => Root.rect.height;

        // ---------- 搭建 ----------

        public static RectTransform Rect(Transform parent, string name = "rect")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>锚在父节点底边中点，y 为离底距离。</summary>
        public static RectTransform Bottom(Transform parent, float x, float y, Vector2 size, string name = "b")
        {
            var rt = Rect(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        /// <summary>锚在父节点顶边中点，y 为离顶距离（向下为正）。</summary>
        public static RectTransform Top(Transform parent, float x, float y, Vector2 size, string name = "t")
        {
            var rt = Rect(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        /// <summary>世界坐标 y 换成离屏幕底边的设计像素。镜头底边固定在 y = -9.6，高屏多出来的都在上面（战场变长）。</summary>
        public static float FromBottom(float worldY) => (worldY + 9.6f) * 100f;

        /// <summary>世界坐标 y 换成离屏幕顶边的设计像素。随屏高变化，常驻界面要用 FromBottom + Bottom 锚点。</summary>
        public static float FromTop(float worldY) => Height - FromBottom(worldY);

        /// <summary>顶部刘海、状态栏占掉的设计像素。</summary>
        public static float SafeTop
        {
            get
            {
                var sa = Screen.safeArea;
                float px = Screen.height - sa.yMax;
                return Screen.height > 0 ? px * Height / Screen.height : 0;
            }
        }

        public static RectTransform Fill(Transform parent, Color c, bool block = true, string name = "fill")
        {
            var rt = Rect(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = block;
            return rt;
        }

        public static Image Box(Transform parent, Vector2 size, Vector2 pos, Color c, Sprite sprite = null, string name = "box")
        {
            var rt = Rect(parent, name);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite ?? Art.Rounded;
            img.type = img.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static Image Icon(Transform parent, string art, float size, Vector2 pos, string name = "icon")
        {
            var img = Box(parent, Vector2.one * size, pos, Color.white, Art.Img(art), name);
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            return img;
        }

        public static Text Label(Transform parent, string s, int size, Color c, Vector2 pos, Vector2 box,
            TextAnchor align = TextAnchor.MiddleCenter, Font font = null, string name = "label")
        {
            var rt = Rect(parent, name);
            rt.sizeDelta = box;
            rt.anchoredPosition = pos;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font ?? Fonts.Bold;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.text = s;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.05f;
            return t;
        }

        /// <summary>卡通描边字：四向描边 + 下沉投影。</summary>
        public static Text Outlined(Transform parent, string s, int size, Color c, Vector2 pos, Vector2 box, Color edge,
            float thick = 3f, TextAnchor align = TextAnchor.MiddleCenter, string name = "olabel")
        {
            var t = Label(parent, s, size, c, pos, box, align, Fonts.Heavy, name);
            Stroke(t, edge, thick);
            return t;
        }

        /// <summary>Outline 自带四向偏移；叠多个会让顶点数按 5 的幂增长，长文本直接超 65000。</summary>
        public static Text Stroke(Text t, Color c, float d)
        {
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor = c;
            ol.effectDistance = new Vector2(d, -d);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = c;
            sh.effectDistance = new Vector2(0, -d * 1.6f);
            return t;
        }

        public static void Tap(Graphic g, Action onClick, Transform punch = null, bool sound = true)
        {
            g.raycastTarget = true;
            var b = g.gameObject.GetComponent<Button>() ?? g.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var t = punch ? punch : g.transform;
            b.onClick.AddListener(() =>
            {
                if (sound) Sfx.Play(Snd.Tap);
                Tween.Punch(t, 0.16f, 0.12f);
                onClick?.Invoke();
            });
        }

        /// <summary>卡通按钮：深色描边、面色、下半压暗、顶部高光，可选左侧图标。</summary>
        public static RectTransform Btn(Transform parent, string text, Vector2 size, Vector2 pos, Color face, Action onClick,
            int fontSize = 52, string icon = null, Color? fg = null)
        {
            var holder = Rect(parent, "btn");
            holder.sizeDelta = size;
            holder.anchoredPosition = pos;
            Box(holder, size + new Vector2(10, 10), new Vector2(0, -9), Pal.Shadow, null, "shadow");
            Box(holder, size + new Vector2(10, 10), Vector2.zero, Pal.Edge, null, "edge");
            var f = Box(holder, size, Vector2.zero, face, null, "face");
            Box(holder, new Vector2(size.x - 8, size.y * 0.46f), new Vector2(0, -size.y * 0.27f + 2), face.Mul(0.84f), null, "under");
            Box(holder, new Vector2(size.x - 34, Mathf.Max(8, size.y * 0.14f)), new Vector2(0, size.y * 0.3f), Color.white.A(0.35f), null, "gloss");
            float iconW = icon != null ? size.y * 0.7f : 0f;
            var label = Outlined(holder, text, fontSize, fg ?? Color.white, new Vector2(iconW * 0.5f, 3), new Vector2(size.x - iconW, size.y),
                face.Mul(0.45f), 2.6f, TextAnchor.MiddleCenter, "text");
            if (icon != null)
            {
                float tw = Mathf.Min(size.x - iconW - 20, label.preferredWidth);
                Icon(holder, icon, iconW, new Vector2(-tw * 0.5f - iconW * 0.15f, 2), "btnIcon");
            }
            Tap(f, onClick, holder);
            return holder;
        }

        public static void SetBtnText(RectTransform btn, string s) => btn.Find("text").GetComponent<Text>().text = s;

        /// <summary>羊皮纸面板：深描边、木色框、纸面。返回纸面那层。</summary>
        public static RectTransform Panel(Transform parent, Vector2 size, Vector2 pos, string name = "panel")
        {
            var holder = Rect(parent, name);
            holder.sizeDelta = size;
            holder.anchoredPosition = pos;
            Box(holder, size + new Vector2(16, 16), new Vector2(0, -16), Pal.Shadow, null, "shadow");
            Box(holder, size + new Vector2(16, 16), Vector2.zero, Pal.Edge, null, "edge");
            Box(holder, size, Vector2.zero, Pal.Hex("A0683A"), null, "frame");
            Box(holder, size - new Vector2(28, 28), new Vector2(0, -2), Pal.Hex("6E4122"), null, "innerEdge");
            Box(holder, size - new Vector2(38, 38), new Vector2(0, -2), Pal.Parchment, null, "paper");
            return holder;
        }

        /// <summary>丝带标题：红绶带上压粗描边金字。</summary>
        public static RectTransform Ribbon(Transform parent, string s, Vector2 pos, float width = 560, Color? color = null)
        {
            var c = color ?? Pal.Hex("D8452F");
            var holder = Rect(parent, "ribbon");
            holder.anchoredPosition = pos;
            holder.sizeDelta = new Vector2(width, 110);
            foreach (float side in new[] { -1f, 1f })
            {
                var tail = Box(holder, new Vector2(110, 80), new Vector2(side * (width * 0.5f - 10), -22), c.Mul(0.62f), null, "tail");
                tail.transform.localRotation = Quaternion.Euler(0, 0, side * -8);
            }
            Box(holder, new Vector2(width + 10, 104), Vector2.zero, Pal.Edge, null, "edge");
            Box(holder, new Vector2(width, 94), Vector2.zero, c, null, "face");
            Box(holder, new Vector2(width - 30, 22), new Vector2(0, 24), Color.white.A(0.22f), null, "gloss");
            Outlined(holder, s, 64, Pal.Hex("FFE9A8"), new Vector2(0, 4), new Vector2(width, 100), Pal.Edge, 3.5f);
            return holder;
        }

        public static void Toast(string s, float y = 0)
        {
            int len = Mathf.Max(6, s.Length);
            var bg = Box(Root, new Vector2(Mathf.Min(980, len * 44 + 120), 100), new Vector2(0, y), Pal.SlateDeep.A(0.88f), Art.Pill, "toast");
            Label(bg.transform, s, 42, Pal.Cream, Vector2.zero, new Vector2(960, 96));
            bg.transform.SetAsLastSibling();
            Tween.Pop(bg.transform, 0.22f);
            Tween.Run(bg, 1.5f, k => { }, () => UnityEngine.Object.Destroy(bg.gameObject));
        }

        public static bool PointerOverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            return hits.Count > 0;
        }

        public static string Num(float v)
        {
            if (v >= 1000000) return (v / 1000000f).ToString(v >= 10000000 ? "0" : "0.#") + "M";
            if (v >= 1000) return (v / 1000f).ToString(v >= 10000 ? "0" : "0.#") + "K";
            return ((int)v).ToString();
        }
    }

    /// <summary>极简补间：不受 timeScale 影响，宿主销毁即停。</summary>
    public sealed class Tween : MonoBehaviour
    {
        sealed class Job
        {
            public Action<float> Step;
            public float T, Dur, Delay;
            public Action Done;
            public UnityEngine.Object Owner;
        }

        static Tween _i;
        readonly List<Job> _jobs = new List<Job>();
        readonly List<Job> _add = new List<Job>();

        public static void Init(GameObject host) { if (_i == null) _i = host.AddComponent<Tween>(); }

        public static void Run(UnityEngine.Object owner, float dur, Action<float> step, Action done = null, float delay = 0)
            => _i._add.Add(new Job { Owner = owner, Dur = Mathf.Max(0.0001f, dur), Delay = delay, Done = done, Step = step });

        void Update()
        {
            if (_add.Count > 0) { _jobs.AddRange(_add); _add.Clear(); }
            float dt = Time.unscaledDeltaTime;
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                var j = _jobs[i];
                if (j.Owner == null) { _jobs.RemoveAt(i); continue; }
                if (j.Delay > 0) { j.Delay -= dt; continue; }
                j.T += dt;
                float k = Mathf.Clamp01(j.T / j.Dur);
                j.Step(k);
                if (k < 1f) continue;
                _jobs.RemoveAt(i);
                j.Done?.Invoke();
            }
        }

        public static float OutBack(float k) { const float s = 1.9f; k -= 1; return k * k * ((s + 1) * k + s) + 1; }
        public static float OutCubic(float k) { k -= 1; return k * k * k + 1; }
        public static float InCubic(float k) => k * k * k;

        public static void Pop(Transform t, float dur = 0.3f, float delay = 0)
        {
            var target = t.localScale == Vector3.zero ? Vector3.one : t.localScale;
            t.localScale = Vector3.zero;
            Run(t, dur, k => t.localScale = target * OutBack(k), null, delay);
        }

        static readonly HashSet<Transform> Punching = new HashSet<Transform>();

        /// <summary>同一个节点正在弹时不叠加，免得基准缩放被带偏。</summary>
        public static void Punch(Transform t, float dur = 0.2f, float amount = 0.15f)
        {
            if (t == null || !Punching.Add(t)) return;
            var baseScale = t.localScale;
            Run(t, dur, k => t.localScale = baseScale * (1f + Mathf.Sin(k * Mathf.PI) * amount), () =>
            {
                Punching.Remove(t);
                if (t) t.localScale = baseScale;
            });
        }

        public static void Fade(CanvasGroup g, float from, float to, float dur, Action done = null, float delay = 0)
        {
            g.alpha = from;
            Run(g, dur, k => g.alpha = Mathf.Lerp(from, to, k), done, delay);
        }
    }
}
