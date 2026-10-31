using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Liangshan
{
    /// <summary>战场上的一次性特效：爆炸、光圈、落雷、飞金币、漂字、震屏。全部走对象池，随 timeScale 暂停。</summary>
    public sealed class WorldFx : MonoBehaviour
    {
        public static WorldFx I { get; private set; }

        sealed class Job
        {
            public GameObject Go;
            public float T, Dur, Delay;
            public Action<float> Step;
            public bool Release;
        }

        readonly List<Job> _jobs = new List<Job>();
        readonly List<Job> _add = new List<Job>();
        readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        readonly Stack<LineRenderer> _lines = new Stack<LineRenderer>();
        readonly Stack<Text> _texts = new Stack<Text>();
        int _liveTexts;
        Material _lineMat;
        RectTransform _textLayer;
        Transform _cam;
        Vector3 _camBase;
        float _shake, _shakeT;

        public int LiveTexts => _liveTexts;

        public static WorldFx Create(Transform parent, RectTransform textLayer, Transform cam)
        {
            var go = new GameObject("WorldFx");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<WorldFx>();
            I._textLayer = textLayer;
            I._cam = cam;
            I._camBase = cam.localPosition;
            I._lineMat = AddMat;
            return I;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (_cam) _cam.localPosition = _camBase;
        }

        // ---------- 基础件 ----------

        /// <summary>sprite 长边 = 1 单位，所以 size 就是长边的世界长度。</summary>
        public static SpriteRenderer Spr(Transform parent, Sprite s, Vector2 pos, float size, int order, Color? c = null, string name = "spr")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            sr.color = c ?? Color.white;
            return sr;
        }

        /// <summary>九宫格块，size 为世界尺寸。</summary>
        public static SpriteRenderer Block(Transform parent, Sprite s, Vector2 pos, Vector2 size, int order, Color c, string name = "block")
        {
            var sr = Spr(parent, s, pos, 1f, order, c, name);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            return sr;
        }

        public static int Order(float y) => 5000 - Mathf.RoundToInt(y * 100f);

        static Material _addMat, _defMat;

        /// <summary>叠加发光材质：亮的地方把底下的地面、人物一起照亮，不再像贴纸浮在上面。</summary>
        public static Material AddMat
        {
            get
            {
                if (_addMat == null)
                {
                    var sh = Resources.Load<Shader>("Shaders/FxAdd");
                    _addMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
                }
                return _addMat;
            }
        }

        public static SpriteRenderer Additive(SpriteRenderer sr)
        {
            sr.sharedMaterial = AddMat;
            return sr;
        }

        SpriteRenderer Take(Sprite s, Vector2 pos, float size, int order, Color c, bool add = false)
        {
            SpriteRenderer sr;
            if (_pool.Count > 0)
            {
                sr = _pool.Pop();
                sr.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("fx");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                if (_defMat == null) _defMat = sr.sharedMaterial;
            }
            sr.sharedMaterial = add ? AddMat : _defMat;
            sr.sprite = s;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingOrder = order;
            sr.color = c;
            sr.flipX = false;
            var t = sr.transform;
            t.localPosition = pos;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one * size;
            return sr;
        }

        public void Anim(GameObject go, float dur, Action<float> step, bool release = true, float delay = 0)
        {
            if (delay > 0) go.SetActive(false);
            _add.Add(new Job { Go = go, Dur = Mathf.Max(0.0001f, dur), Step = step, Release = release, Delay = delay });
        }

        void Update()
        {
            if (_add.Count > 0) { _jobs.AddRange(_add); _add.Clear(); }
            float dt = Time.deltaTime;
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                var j = _jobs[i];
                if (j.Go == null) { _jobs.RemoveAt(i); continue; }
                if (j.Delay > 0)
                {
                    j.Delay -= dt;
                    if (j.Delay > 0) continue;
                    j.Go.SetActive(true);
                }
                j.T += dt;
                float k = Mathf.Clamp01(j.T / j.Dur);
                j.Step(k);
                if (k < 1f) continue;
                _jobs.RemoveAt(i);
                if (j.Release) Release(j.Go);
            }
            UpdateShake(dt);
        }

        void Release(GameObject go)
        {
            if (go.TryGetComponent<SpriteRenderer>(out var sr) && go.transform.parent == transform)
            {
                go.SetActive(false);
                _pool.Push(sr);
            }
            else if (go.TryGetComponent<LineRenderer>(out var lr))
            {
                go.SetActive(false);
                _lines.Push(lr);
            }
            else if (go.TryGetComponent<Text>(out var tx))
            {
                go.SetActive(false);
                _texts.Push(tx);
                _liveTexts--;
            }
            else Destroy(go);
        }

        // ---------- 预设 ----------

        /// <summary>放大再淡出的一次性图。</summary>
        public void Burst(Sprite s, Vector2 pos, float size, float dur = 0.35f, int order = 9500, Color? c = null, float spin = 0, float from = 0.55f, bool add = false)
        {
            var col = c ?? Color.white;
            var sr = Take(s, pos, size * from, order, col, add);
            float rot0 = UnityEngine.Random.Range(0f, 360f) * (spin != 0 ? 1 : 0);
            Anim(sr.gameObject, dur, k =>
            {
                float sc = Mathf.Lerp(from, 1.08f, Tween.OutCubic(Mathf.Min(1, k * 1.8f)));
                sr.transform.localScale = Vector3.one * size * sc;
                if (spin != 0) sr.transform.localRotation = Quaternion.Euler(0, 0, rot0 + spin * k);
                sr.color = col.A(col.a * (k < 0.45f ? 1f : 1f - (k - 0.45f) / 0.55f));
            });
        }

        public void Ring(Vector2 pos, float radius, Color c, float dur = 0.4f, int order = 9400)
        {
            var sr = Take(Art.Ring, pos, 0.1f, order, c, true);
            Anim(sr.gameObject, dur, k =>
            {
                sr.transform.localScale = new Vector3(1f, 0.62f, 1f) * radius * 2f * Tween.OutCubic(k);
                sr.color = c.A(c.a * (1 - k));
            });
        }

        /// <summary>地面光晕：柔光点压扁成椭圆。</summary>
        public void Glow(Vector2 pos, float size, Color c, float dur = 0.4f, int order = 140)
        {
            var sr = Take(Art.SoftDot, pos, size, order, c);
            Anim(sr.gameObject, dur, k =>
            {
                sr.transform.localScale = new Vector3(1f, 0.6f, 1f) * size * (0.7f + 0.5f * k);
                sr.color = c.A(c.a * (1 - k));
            });
        }

        /// <summary>天雷：底端落在 pos。</summary>
        public void Strike(Vector2 pos, float height, int order = 9600)
        {
            var sr = Take(Art.Img("fx_lightning"), pos + new Vector2(0, height * 0.5f), height, order, Color.white, true);
            sr.flipX = UnityEngine.Random.value < 0.5f;
            Anim(sr.gameObject, 0.28f, k =>
            {
                float a = k < 0.15f ? 1f : Mathf.Abs(Mathf.Sin(k * 26f)) * (1 - k);
                sr.color = new Color(1, 1, 1, a);
                sr.transform.localScale = new Vector3(0.9f + 0.2f * k, 1f, 1f) * height;
            });
            var zap = Take(Art.Img("fx_zap"), pos + new Vector2(0, height * 0.12f), height * 0.5f, order + 1, Color.white, true);
            zap.flipX = UnityEngine.Random.value < 0.5f;
            Anim(zap.gameObject, 0.3f, k =>
            {
                zap.transform.localScale = new Vector3(1f, 0.75f, 1f) * height * Mathf.Lerp(0.35f, 0.6f, Tween.OutCubic(k));
                zap.color = Color.white.A(k < 0.2f ? 1f : 1f - (k - 0.2f) / 0.8f);
            });
            LightPool(pos, height * 1.1f, new Color(0.45f, 0.7f, 1f, 0.9f), 0.35f);
            Flash(pos + new Vector2(0, 0.4f), height * 0.9f, new Color(0.5f, 0.75f, 1f, 0.5f), 0.18f);
            Decal(Art.Img("fx_scorch"), pos, height * 0.32f, new Color(0.12f, 0.12f, 0.2f, 0.55f), 1.6f);
        }

        // ---------- 贴地：让特效“落”在地面上 ----------

        int _decals;

        /// <summary>地面印记（焦痕、裂缝、冰霜），压扁贴地，缓慢淡出。</summary>
        public void Decal(Sprite s, Vector2 pos, float size, Color c, float life = 2.5f, float flat = 0.5f, int order = 90, bool add = false)
        {
            if (_decals >= 40) return;
            _decals++;
            var sr = Take(s, pos, size, order, c.A(0), add);
            sr.flipX = UnityEngine.Random.value < 0.5f;
            float sx = size * UnityEngine.Random.Range(0.9f, 1.1f);
            sr.transform.localScale = new Vector3(sx, size * flat, 1f);
            Anim(sr.gameObject, life, k =>
            {
                float a = k < 0.05f ? k / 0.05f : k > 0.55f ? 1f - (k - 0.55f) / 0.45f : 1f;
                sr.color = c.A(c.a * a);
                if (k >= 1f) _decals--;
            });
        }

        /// <summary>贴地冲击波：压扁的不规则光环向外扩。</summary>
        public void GroundWave(Vector2 pos, float radius, Color c, float dur = 0.4f, int order = 94, float flat = 0.5f)
        {
            var sr = Take(Art.Img("fx_wave"), pos, 0.1f, order, c, true);
            Anim(sr.gameObject, dur, k =>
            {
                float r = radius * 2f * Mathf.Lerp(0.2f, 1f, Tween.OutCubic(k));
                sr.transform.localScale = new Vector3(r, r * flat, 1f);
                sr.color = c.A(c.a * (1 - k) * (1 - k));
            });
        }

        /// <summary>地面受光：叠加的扁圆光斑，爆炸、落雷照亮地面。</summary>
        public void LightPool(Vector2 pos, float size, Color c, float dur = 0.4f, int order = 93)
        {
            var sr = Take(Art.SoftDot, pos, size, order, c, true);
            Anim(sr.gameObject, dur, k =>
            {
                sr.transform.localScale = new Vector3(1f, 0.55f, 1f) * size * (0.8f + 0.35f * Tween.OutCubic(k));
                sr.color = c.A(c.a * (k < 0.1f ? k / 0.1f : 1f - (k - 0.1f) / 0.9f));
            });
        }

        /// <summary>局部闪光：压在人物之上，连周围的兵一起照亮一下。</summary>
        public void Flash(Vector2 pos, float size, Color c, float dur = 0.16f, int order = 9450)
        {
            var sr = Take(Art.SoftDot, pos, size, order, c, true);
            Anim(sr.gameObject, dur, k =>
            {
                sr.transform.localScale = Vector3.one * size * (0.85f + 0.3f * k);
                sr.color = c.A(c.a * (1 - k) * (1 - k));
            });
        }

        /// <summary>烟团：普通混合，边升边胀边淡，给爆炸“分量”。</summary>
        public void Smoke(Vector2 pos, int n, float size, Color c, float dur = 1.1f, float spread = 0.6f, float rise = 0.9f, int order = 9420)
        {
            var spr = Art.Img("fx_smoke");
            for (int i = 0; i < n; i++)
            {
                var off = new Vector2(UnityEngine.Random.Range(-spread, spread), UnityEngine.Random.Range(-spread, spread) * 0.4f);
                var p0 = pos + off;
                float sz = size * UnityEngine.Random.Range(0.7f, 1.15f);
                float rot = UnityEngine.Random.Range(0f, 360f);
                float spin = UnityEngine.Random.Range(-40f, 40f);
                float d = dur * UnityEngine.Random.Range(0.8f, 1.15f);
                var drift = new Vector2(off.x * 0.6f, rise * UnityEngine.Random.Range(0.7f, 1.2f));
                var sr = Take(spr, p0, sz * 0.4f, order + i, c.A(0));
                Anim(sr.gameObject, d, k =>
                {
                    float e = Tween.OutCubic(k);
                    sr.transform.localPosition = p0 + drift * e;
                    sr.transform.localScale = Vector3.one * sz * Mathf.Lerp(0.45f, 1.25f, e);
                    sr.transform.localRotation = Quaternion.Euler(0, 0, rot + spin * k);
                    float a = k < 0.12f ? k / 0.12f : 1f - (k - 0.12f) / 0.88f;
                    sr.color = c.A(c.a * a * a);
                }, true, i * 0.04f);
            }
        }

        /// <summary>贴地扬尘：横向铺开的低矮尘土。</summary>
        public void Dust(Vector2 pos, float width, Color c, float dur = 0.6f, int order = 9300)
        {
            var sr = Take(Art.Img("fx_dust"), pos, width * 0.5f, order, c);
            sr.flipX = UnityEngine.Random.value < 0.5f;
            Anim(sr.gameObject, dur, k =>
            {
                float e = Tween.OutCubic(k);
                float w = width * Mathf.Lerp(0.5f, 1.15f, e);
                sr.transform.localScale = new Vector3(w, w * 0.8f, 1f);
                sr.transform.localPosition = pos + new Vector2(0, w * 0.18f + 0.1f * e);
                sr.color = c.A(c.a * (1 - k));
            });
        }

        /// <summary>碎石：抛起、落地弹一下、停住后淡出。地面高度就是 pos.y 附近。</summary>
        public void Debris(Vector2 pos, int n, float size, float speed, float dur = 0.9f)
        {
            for (int i = 0; i < n; i++)
            {
                var spr = Art.Img("fx_rock" + UnityEngine.Random.Range(0, 6));
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
                var vx = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.45f) * speed * UnityEngine.Random.Range(0.4f, 1f);
                float vz = speed * UnityEngine.Random.Range(0.9f, 1.6f);
                float g = 14f;
                float t1 = 2f * vz / g;
                float sz = size * UnityEngine.Random.Range(0.55f, 1f);
                var ground = pos + new Vector2(0, UnityEngine.Random.Range(-0.25f, 0.15f));
                float spin = UnityEngine.Random.Range(-720f, 720f);
                var sr = Take(spr, ground, sz, 9350, Pal.Hex("8A7A68"));
                var sh = Take(Art.SoftDot, ground, sz, 125, new Color(0, 0, 0, 0.3f));
                float d = Mathf.Max(dur, t1 * 1.5f + 0.3f);
                Anim(sr.gameObject, d, k =>
                {
                    float t = k * d;
                    float z;
                    Vector2 xy;
                    if (t < t1) { z = vz * t - g * t * t * 0.5f; xy = vx * t; }
                    else
                    {
                        float t2 = t - t1, v2 = vz * 0.3f, tb = 2f * v2 / g;
                        z = t2 < tb ? v2 * t2 - g * t2 * t2 * 0.5f : 0f;
                        xy = vx * t1 + vx * 0.3f * Mathf.Min(t2, tb);
                    }
                    var gp = ground + xy;
                    sr.transform.localPosition = gp + new Vector2(0, z + sz * 0.3f);
                    sr.transform.localRotation = Quaternion.Euler(0, 0, spin * Mathf.Min(t, t1 * 1.4f));
                    sr.sortingOrder = Order(gp.y) + 30;
                    sh.transform.localPosition = gp;
                    sh.transform.localScale = new Vector3(sz * 0.9f, sz * 0.3f, 1f) / (1f + z * 0.5f);
                    float fade = k > 0.75f ? 1f - (k - 0.75f) / 0.25f : 1f;
                    sr.color = Pal.Hex("8A7A68").A(fade);
                    sh.color = new Color(0, 0, 0, 0.3f * fade);
                    if (k >= 1f) Release(sh.gameObject);
                });
            }
        }

        /// <summary>从地面长出来的特效（冰晶、光柱）：底边钉在 pos，纵向拉起再收回。</summary>
        public void Rise(Sprite s, Vector2 pos, float size, float dur, Color c, int order, bool add = true, float hold = 0.5f)
        {
            var sr = Take(s, pos, size, order, c, add);
            sr.flipX = UnityEngine.Random.value < 0.5f;
            float aspect = s.bounds.size.y / Mathf.Max(s.bounds.size.x, s.bounds.size.y);
            Anim(sr.gameObject, dur, k =>
            {
                float up = k < 0.18f ? Tween.OutBack(k / 0.18f) : 1f;
                float fade = k > hold ? 1f - (k - hold) / (1f - hold) : 1f;
                float sy = size * up;
                sr.transform.localScale = new Vector3(size * (0.85f + 0.15f * up), sy, 1f);
                sr.transform.localPosition = pos + new Vector2(0, sy * aspect * 0.5f);
                sr.color = c.A(c.a * fade);
            });
        }

        /// <summary>落地残留物（插地的箭）：立在地面，停留后淡出。底边钉在 pos。</summary>
        public void Linger(Sprite s, Vector2 pos, float size, float life, bool flip = false)
        {
            if (_decals >= 40) return;
            _decals++;
            float aspect = s.bounds.size.y / Mathf.Max(s.bounds.size.x, s.bounds.size.y);
            var sr = Take(s, pos + new Vector2(0, size * aspect * 0.5f), size, Order(pos.y), Color.white);
            sr.flipX = flip;
            Anim(sr.gameObject, life, k =>
            {
                float sq = k < 0.08f ? 1f + 0.15f * Mathf.Sin(k / 0.08f * Mathf.PI) : 1f;
                sr.transform.localScale = new Vector3(size / sq, size * sq, 1f);
                sr.color = Color.white.A(k > 0.6f ? 1f - (k - 0.6f) / 0.4f : 1f);
                if (k >= 1f) _decals--;
            });
        }

        /// <summary>全屏闪白/闪色，大招、首领落地用。</summary>
        public void ScreenFlash(Color c, float dur = 0.25f)
        {
            if (_cam == null) return;
            var sr = Take(Art.White, Vector2.zero, 1f, 9900, c, true);
            sr.transform.position = new Vector3(_cam.position.x, _cam.position.y, 0f);
            sr.transform.localScale = new Vector3(40f, 60f, 1f);
            Anim(sr.gameObject, dur, k => sr.color = c.A(c.a * (1 - k) * (1 - k)));
        }

        /// <summary>折线电弧。</summary>
        public void Arc(Vector2 a, Vector2 b, Color c, float width = 0.12f, float dur = 0.2f, int order = 9550)
        {
            LineRenderer lr;
            if (_lines.Count > 0)
            {
                lr = _lines.Pop();
                lr.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("arc");
                go.transform.SetParent(transform, false);
                lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = _lineMat;
                lr.useWorldSpace = true;
                lr.numCapVertices = 2;
                lr.positionCount = 8;
            }
            lr.sortingOrder = order;
            var d = b - a;
            var n = new Vector2(-d.y, d.x).normalized;
            float jag = Mathf.Min(0.35f, d.magnitude * 0.12f);
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                float off = i == 0 || i == 7 ? 0 : UnityEngine.Random.Range(-jag, jag);
                lr.SetPosition(i, (Vector3)(a + d * t + n * off) + new Vector3(0, 0, -0.1f));
            }
            Anim(lr.gameObject, dur, k =>
            {
                float w = width * (1 - k * 0.6f);
                lr.startWidth = w;
                lr.endWidth = w * 0.7f;
                var cc = c.A(c.a * (1 - k));
                lr.startColor = cc;
                lr.endColor = Color.white.A(1 - k);
            });
        }

        /// <summary>从 a 飞到 b 的小图（金币、宝箱），带弧线。</summary>
        public void Fly(Sprite s, Vector2 a, Vector2 b, float size, float dur, float arc, Action done = null, int order = 9800)
        {
            var sr = Take(s, a, size, order, Color.white);
            var side = new Vector2(UnityEngine.Random.Range(-0.8f, 0.8f), arc);
            Anim(sr.gameObject, dur, k =>
            {
                float e = Tween.InCubic(k) * 0.65f + k * 0.35f;
                var p = Vector2.Lerp(a, b, e) + side * Mathf.Sin(k * Mathf.PI) * (1 - k * 0.3f);
                sr.transform.localPosition = p;
                sr.transform.localScale = Vector3.one * size * (k < 0.15f ? Tween.OutBack(k / 0.15f) : 1f - 0.3f * k);
                if (k >= 1f) done?.Invoke();
            });
        }

        /// <summary>小碎片四散：命中火花、合成星星。</summary>
        public void Sparks(Sprite s, Vector2 pos, int n, float size, float speed, Color c, float dur = 0.45f, int order = 9700, float gravity = 4f, bool add = false)
        {
            for (int i = 0; i < n; i++)
            {
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
                var v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.7f + 0.6f) * speed * UnityEngine.Random.Range(0.55f, 1f);
                float sz = size * UnityEngine.Random.Range(0.6f, 1f);
                var sr = Take(s, pos, sz, order, c, add);
                Anim(sr.gameObject, dur, k =>
                {
                    float t = k * dur;
                    sr.transform.localPosition = pos + v * t + new Vector2(0, -gravity * t * t * 0.5f);
                    sr.transform.localScale = Vector3.one * sz * (1 - k * 0.7f);
                    sr.transform.localRotation = Quaternion.Euler(0, 0, k * 240f);
                    sr.color = c.A(c.a * (1 - k * k));
                });
            }
        }

        // ---------- 漂字 ----------

        public void Number(Vector2 world, string s, Color c, int size, float rise = 0.6f, float dur = 0.65f, bool pop = false)
        {
            Text t;
            if (_texts.Count > 0)
            {
                t = _texts.Pop();
                t.gameObject.SetActive(true);
                t.transform.SetAsLastSibling();
            }
            else
            {
                t = Ui.Label(_textLayer, "", 40, Color.white, Vector2.zero, new Vector2(500, 120), TextAnchor.MiddleCenter, Fonts.Heavy, "num");
                Ui.Stroke(t, Pal.Ink, 3f);
                var rt0 = t.rectTransform;
                rt0.anchorMin = rt0.anchorMax = new Vector2(0.5f, 1f);
            }
            _liveTexts++;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            var rt = t.rectTransform;
            var p0 = new Vector2(world.x * 100f, -Ui.FromTop(world.y));
            rt.anchoredPosition = p0;
            rt.localScale = Vector3.one;
            Anim(t.gameObject, dur, k =>
            {
                rt.anchoredPosition = p0 + new Vector2(0, rise * 100f * Tween.OutCubic(k));
                float sc = pop ? (k < 0.25f ? Mathf.Lerp(1.8f, 1f, k / 0.25f) : 1f) : (k < 0.15f ? Mathf.Lerp(0.6f, 1.1f, k / 0.15f) : 1f);
                rt.localScale = Vector3.one * sc;
                t.color = c.A(k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
            });
        }

        // ---------- 震屏 ----------

        public void Shake(float amp, float dur = 0.25f)
        {
            if (amp < _shake * (_shakeT > 0 ? 1 : 0)) return;
            _shake = amp;
            _shakeT = dur;
        }

        void UpdateShake(float dt)
        {
            if (_cam == null) return;
            if (_shakeT <= 0)
            {
                _cam.localPosition = _camBase;
                return;
            }
            _shakeT -= dt;
            float a = _shake * Mathf.Clamp01(_shakeT / 0.25f);
            _cam.localPosition = _camBase + new Vector3(UnityEngine.Random.Range(-a, a), UnityEngine.Random.Range(-a, a), 0);
        }

        public void SetCamBase(Vector3 p) => _camBase = p;
    }
}
