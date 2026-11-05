using System.Collections.Generic;
using Liangshan.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Liangshan
{
    /// <summary>首页出征：阵上最强的输出用自己的攻击动作、特效和声音打台阶上的怪物，换阵容就换人。</summary>
    public sealed class HomeVolley : MonoBehaviour
    {
        sealed class Bolt
        {
            public RectTransform Rt;
            public Image Img, Shade;
            public Vector2 A, B;
            public float T, Dur, Arc;
            public ShotKind Kind;
            public Color Tint;
            public bool Boom, Upright;
        }

        sealed class Puff
        {
            public Image Img;
            public float T, Dur, Size;
        }

        RectTransform _layer;
        Image _boss;
        Image[] _heroes;
        HeroId[] _ids;
        readonly List<Bolt> _bolts = new List<Bolt>();
        readonly List<Puff> _puffs = new List<Puff>();
        float _wait = 0.6f, _flinch, _castAt = -1f;
        int _who;
        HeroDef _def;
        float[] _flash, _atk, _hold;
        Sprite[][] _clip;

        /// <summary>best：阵上出手的那一位（战力最高的输出）。其余人只站着呼吸。</summary>
        public void Begin(Image boss, Image[] heroes, HeroId[] ids, int best)
        {
            _layer = (RectTransform)transform;
            _boss = boss;
            _heroes = heroes;
            _ids = ids;
            _who = Mathf.Clamp(best, 0, Mathf.Max(0, ids.Length - 1));
            _def = ids.Length > 0 ? Defs.Hero(ids[_who]) : null;
            _flash = new float[heroes.Length];
            _atk = new float[heroes.Length];
            _hold = new float[heroes.Length];
            _clip = new Sprite[heroes.Length][];
            for (int i = 0; i < heroes.Length; i++)
            {
                _atk[i] = -1f;
                int k = i;
                string art = Defs.Hero(ids[i]).Art;
                // 0/1 待机，2 蓄力，3 出手。和局里同一套。
                Art.Late(this, art, () =>
                {
                    var fr = Art.Frames(art);
                    _clip[k] = fr.Length >= 4 ? fr : null;
                });
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_ids == null || _ids.Length == 0 || _boss == null) return;
            Recover(dt);
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                if (Tick(_bolts[i], dt)) continue;
                if (_bolts[i].Shade) Destroy(_bolts[i].Shade.gameObject);
                Destroy(_bolts[i].Rt.gameObject);
                _bolts.RemoveAt(i);
            }
            for (int i = _puffs.Count - 1; i >= 0; i--)
                if (!TickPuff(_puffs[i], dt)) { Destroy(_puffs[i].Img.gameObject); _puffs.RemoveAt(i); }
            Pose(dt);
            if (_castAt >= 0f)
            {
                _castAt -= dt;
                if (_castAt < 0f)
                {
                    if (_def.Coef > 0f) HeroSfx.Cast(_def, Volume);
                    else HeroSfx.Buff(_def, Volume);
                }
            }
            _wait -= dt;
            if (_wait > 0) return;
            int n = Launch(_who);
            _castAt = 0.14f;
            _wait = 1.3f + n * 0.12f;
        }

        /// <summary>首页是循环演示，比局里轻一点，不盖过配乐和按钮声。</summary>
        const float Volume = 0.7f;

        int Launch(int i)
        {
            var d = Defs.Hero(_ids[i]);
            int n = Mathf.Clamp(Mathf.Max(1, d.Count), 1, 5);
            if (d.Chan > 0f && d.Tick > 0f) n = Mathf.Clamp(Mathf.RoundToInt(d.Chan / d.Tick), 1, 5);
            if (d.Coef <= 0f) n = 1;
            var from = Point(_heroes[i].rectTransform, new Vector2(0, 36));
            var to = Point(_boss.rectTransform, new Vector2(0, 8));
            var dir = to - from;
            var perp = dir.sqrMagnitude > 1f ? new Vector2(-dir.y, dir.x).normalized : Vector2.right;
            for (int k = 0; k < n; k++)
            {
                float spread = (k - (n - 1) * 0.5f) * 26f;
                Spawn(d, from + perp * spread * 0.25f, to + perp * spread, 0.14f + k * 0.08f);
            }
            if (_clip[i] != null)
            {
                _atk[i] = 0f;
                _hold[i] = 0.14f + 0.32f + Mathf.Max(0, n - 1) * 0.08f;
            }
            else
            {
                _flash[i] = 0.12f;
                _heroes[i].color = new Color(1f, 0.94f, 0.75f);
            }
            return n;
        }

        /// <summary>出手时切到蓄力帧再切到攻击帧，打完回到待机呼吸。</summary>
        void Pose(float dt)
        {
            for (int i = 0; i < _heroes.Length; i++)
            {
                var img = _heroes[i];
                var clip = _clip[i];
                if (img == null || clip == null) continue;
                int frame;
                if (_atk[i] >= 0f)
                {
                    _atk[i] += dt;
                    frame = _atk[i] < 0.14f ? 2 : 3;
                    if (_atk[i] >= _hold[i]) _atk[i] = -1f;
                }
                else frame = ((int)(Time.unscaledTime * 1.6f) + i) % 2;
                var spr = clip[frame];
                if (img.sprite != spr) img.sprite = spr;
            }
        }

        void Spawn(HeroDef d, Vector2 a, Vector2 b, float delay)
        {
            var kind = d.Shot;
            var tint = string.IsNullOrEmpty(d.Tint) ? Color.white : Pal.Hex(d.Tint);
            float dur = 0.42f, arc = 70f, size = 78f;
            string art = "fx_arrow";
            bool shade = false, along = true;
            switch (kind)
            {
                case ShotKind.Arrow:
                    art = "fx_arrow"; dur = 0.4f; arc = 64f; size = 86f; break;
                case ShotKind.Bomb:
                case ShotKind.MiniBomb:
                    art = "ic_bomb"; dur = 0.55f; arc = kind == ShotKind.MiniBomb ? 120f : 190f; size = kind == ShotKind.MiniBomb ? 52f : 72f; shade = true; along = false; break;
                case ShotKind.Fireball:
                    art = "fx_fireball"; dur = 0.5f; arc = 160f; size = 96f; shade = true; along = false; break;
                case ShotKind.Stone:
                    art = "fx_rock" + Random.Range(0, 3); dur = 0.42f; arc = 100f; size = 58f; shade = true; along = false;
                    if (!string.IsNullOrEmpty(d.Tint)) tint = Pal.Hex(d.Tint); break;
                case ShotKind.Spear:
                    art = "ic_sword"; dur = 0.28f; arc = 8f; size = 92f;
                    tint = string.IsNullOrEmpty(d.Tint) ? Pal.Hex("E8F2FF") : Pal.Hex(d.Tint); break;
                case ShotKind.Punch:
                    art = "ic_fist"; dur = 0.26f; arc = 16f; size = 70f; break;
                case ShotKind.Wind:
                    art = "fx_wave"; dur = 0.48f; arc = 24f; size = 110f; along = false;
                    tint = string.IsNullOrEmpty(d.Tint) ? Pal.Hex("9FF5D8") : Pal.Hex(d.Tint); break;
                case ShotKind.Lasso:
                    art = "fx_wave"; dur = 0.4f; arc = 36f; size = 84f; tint = Pal.Hex("E04848"); break;
                case ShotKind.Lightning:
                    BoltLine(Art.Img("fx_lightning"), a, b, 84f, 0.22f, Pal.Hex("8FD4FF"), ShotKind.Lightning, true, delay);
                    return;
                case ShotKind.Beam:
                    tint = string.IsNullOrEmpty(d.Tint) ? Pal.Hex("FF8A2A") : Pal.Hex(d.Tint);
                    BoltLine(Art.Beam, a + new Vector2(0, 20), b, 36f, 0.62f, tint, ShotKind.Beam, false, delay);
                    return;
                case ShotKind.Slam:
                    var pop = d.Proj != null ? Make(Art.Img(d.Proj), b, 64f, tint) : Make(Art.Img("fx_shock"), b, 40f, Color.white);
                    pop.enabled = false;
                    _bolts.Add(new Bolt { Rt = pop.rectTransform, Img = pop, A = b, B = b, T = -delay, Dur = 0.34f, Kind = ShotKind.Slam, Tint = d.Proj != null ? tint : Color.white });
                    return;
            }
            bool upright = d.Proj != null;
            if (upright)
            {
                art = d.Proj;
                tint = d.Proj == "wp_slash" && !string.IsNullOrEmpty(d.Tint) ? Pal.Hex(d.Tint) : Color.white;
                along = !d.ProjSpin;
                shade = true;
                size = (kind == ShotKind.Spear ? 124f : kind == ShotKind.Arrow ? 90f : kind == ShotKind.Punch ? 84f : kind == ShotKind.Stone ? 56f : kind == ShotKind.Wind ? 96f : 76f) * d.ProjSize;
            }
            var img = Make(Art.Img(art), a, size, tint.a < 0.01f ? Color.white : tint);
            Image sh = null;
            if (shade)
            {
                sh = Make(Art.SoftDot, a, 36f, new Color(0, 0, 0, 0.28f));
                sh.rectTransform.sizeDelta = new Vector2(48, 16);
                sh.transform.SetAsFirstSibling();
            }
            img.enabled = false;
            if (sh) sh.enabled = false;
            _bolts.Add(new Bolt
            {
                Rt = img.rectTransform, Img = img, Shade = sh, A = a, B = b,
                T = -delay, Dur = dur, Arc = arc, Kind = kind, Tint = tint, Boom = !along, Upright = upright,
            });
        }

        void BoltLine(Sprite sprite, Vector2 a, Vector2 b, float thick, float dur, Color c, ShotKind kind, bool vertical, float delay)
        {
            var img = Make(sprite, (a + b) * 0.5f, thick, c, false);
            img.enabled = false;
            var d = b - a;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            img.rectTransform.sizeDelta = vertical ? new Vector2(thick, d.magnitude) : new Vector2(d.magnitude, thick);
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, vertical ? ang - 90f : ang);
            _bolts.Add(new Bolt { Rt = img.rectTransform, Img = img, A = a, B = b, T = -delay, Dur = dur, Kind = kind, Tint = c });
        }

        bool Tick(Bolt o, float dt)
        {
            o.T += dt;
            if (o.T < 0) return true;
            if (o.Img && !o.Img.enabled) o.Img.enabled = true;
            if (o.Shade && !o.Shade.enabled) o.Shade.enabled = true;
            float k = Mathf.Clamp01(o.T / Mathf.Max(0.05f, o.Dur));
            if (o.Kind == ShotKind.Lightning || o.Kind == ShotKind.Beam)
            {
                float pulse = o.Kind == ShotKind.Beam ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 40f) : 1f - k;
                var c = o.Tint; c.a = pulse;
                o.Img.color = c;
                if (o.Kind == ShotKind.Beam)
                    o.Rt.sizeDelta = new Vector2((o.B - o.A).magnitude, 28f + 10f * Mathf.Sin(Time.unscaledTime * 47f));
            }
            else if (o.Kind == ShotKind.Slam)
            {
                o.Rt.anchoredPosition = o.B;
                float s = Mathf.Lerp(0.35f, 1.7f, Tween.OutCubic(k));
                o.Rt.localScale = Vector3.one * s;
                o.Img.color = Color.white.A(1f - k);
            }
            else
            {
                var ahead = Spot(o, Mathf.Min(1f, k + 0.04f));
                var now = Spot(o, k);
                o.Rt.anchoredPosition = now;
                if (o.Shade)
                {
                    var g = Vector2.Lerp(o.A, o.B, k);
                    o.Shade.rectTransform.anchoredPosition = g;
                    float lift = o.Arc * 4f * k * (1f - k);
                    o.Shade.color = new Color(0, 0, 0, 0.3f / (1f + lift * 0.004f));
                }
                if (o.Boom)
                    o.Rt.localRotation = Quaternion.Euler(0, 0, -o.T * (o.Kind == ShotKind.Wind ? 180f : o.Upright ? 900f : 520f));
                else
                {
                    var vel = ahead - now;
                    float ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
                    o.Rt.localRotation = Quaternion.Euler(0, 0, ang - (o.Kind == ShotKind.Spear && !o.Upright ? 45f : 90f));
                }
                if (o.Kind == ShotKind.Wind)
                    o.Rt.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.35f, k);
            }
            if (o.T < o.Dur) return true;
            Impact(o.Kind, o.Kind == ShotKind.Slam || o.Kind == ShotKind.Lightning || o.Kind == ShotKind.Beam ? o.B : Spot(o, 1f), o.Tint);
            return false;
        }

        static Vector2 Spot(Bolt o, float k)
        {
            float z = o.Arc * 4f * k * (1f - k);
            return Vector2.Lerp(o.A, o.B, o.Kind == ShotKind.Spear || o.Kind == ShotKind.Punch ? Tween.OutCubic(k) : k) + new Vector2(0, z);
        }

        void Impact(ShotKind kind, Vector2 at, Color tint)
        {
            if (_def != null && _def.Coef > 0f) HeroSfx.Hit(_def, false, Volume);
            switch (kind)
            {
                case ShotKind.Bomb:
                case ShotKind.MiniBomb:
                case ShotKind.Fireball:
                    Burst(Art.Img("fx_explode"), at, 160, 0.32f, Color.white);
                    Burst(Art.Img("fx_hit"), at, 90, 0.2f, Pal.Hex("FFB060"));
                    break;
                case ShotKind.Lightning:
                    Burst(Art.Img("fx_zap"), at, 130, 0.26f, Color.white);
                    Burst(Art.Sparkle, at, 80, 0.18f, Pal.Hex("BFE6FF"));
                    break;
                case ShotKind.Punch when _def != null && _def.Proj != null:
                    Burst(Art.Img("wp_slash"), at + new Vector2(0, 10), 120, 0.2f, Color.white);
                    Burst(Art.Sparkle, at + new Vector2(0, 16), 80, 0.16f, Pal.Hex("FFE6B0"));
                    break;
                case ShotKind.Punch:
                case ShotKind.Slam:
                    Burst(Art.Img("fx_shock"), at, kind == ShotKind.Slam ? 180 : 130, 0.3f, Color.white);
                    Burst(Art.Img("fx_hit"), at + new Vector2(0, 16), 80, 0.16f, Pal.Hex("FFC08A"));
                    break;
                case ShotKind.Wind:
                    Burst(Art.Img("fx_wave"), at, 150, 0.32f, tint.a < 0.01f ? Pal.Hex("9FF5D8") : tint);
                    break;
                case ShotKind.Lasso:
                    Burst(Art.Img("fx_wave"), at, 110, 0.28f, Pal.Hex("E04848"));
                    break;
                case ShotKind.Beam:
                    Burst(Art.Sparkle, at, 70, 0.16f, tint);
                    break;
                default:
                    Burst(Art.Sparkle, at, 72, 0.16f, Color.white);
                    Burst(Art.Img("fx_hit"), at, 64, 0.14f, Color.white);
                    break;
            }
            _flinch = 0.12f;
            if (_boss)
            {
                _boss.color = new Color(1f, 0.7f, 0.66f);
                _boss.rectTransform.localScale = Vector3.one * 0.9f;
            }
        }

        void Burst(Sprite s, Vector2 at, float size, float dur, Color c)
        {
            var img = Make(s, at, size * 0.45f, c);
            _puffs.Add(new Puff { Img = img, T = 0, Dur = dur, Size = size });
        }

        bool TickPuff(Puff p, float dt)
        {
            p.T += dt;
            float k = Mathf.Clamp01(p.T / p.Dur);
            p.Img.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.35f, Tween.OutCubic(k));
            var c = p.Img.color;
            c.a = 1f - k;
            p.Img.color = c;
            return p.T < p.Dur;
        }

        void Recover(float dt)
        {
            if (_flinch > 0 && _boss)
            {
                _flinch -= dt;
                float k = Mathf.Clamp01(_flinch / 0.12f);
                _boss.color = Color.Lerp(Color.white, new Color(1f, 0.7f, 0.66f), k);
                _boss.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, k);
            }
            for (int i = 0; i < _flash.Length; i++)
            {
                if (_flash[i] <= 0 || _heroes[i] == null) continue;
                _flash[i] -= dt;
                _heroes[i].color = Color.Lerp(Color.white, new Color(1f, 0.94f, 0.75f), Mathf.Clamp01(_flash[i] / 0.12f));
            }
        }

        Vector2 Point(RectTransform rt, Vector2 nudge)
        {
            return (Vector2)_layer.InverseTransformPoint(rt.TransformPoint(rt.rect.center)) + nudge;
        }

        Image Make(Sprite sprite, Vector2 at, float size, Color c, bool aspect = true)
        {
            var rt = Ui.Rect(_layer, "fx");
            rt.sizeDelta = Vector2.one * size;
            rt.anchoredPosition = at;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = c;
            img.raycastTarget = false;
            img.preserveAspect = aspect;
            return img;
        }
    }
}
