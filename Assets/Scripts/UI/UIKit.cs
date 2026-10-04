using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MuscleAR
{
    /// <summary>Procedural sprites/icons + factory helpers for the MuscleAR UI (matches the Figma look).</summary>
    public static class UIKit
    {
        public static Font Font;
        public static readonly Color BlueBtn = new Color32(66, 135, 245, 255);
        public static readonly Color BlueText = new Color32(96, 165, 250, 255);
        public static readonly Color BlueLine = new Color32(59, 130, 246, 255);
        public static readonly Color Navy = new Color32(10, 15, 26, 235);
        public static readonly Color PillFill = new Color32(24, 58, 112, 190);
        public static readonly Color Dim = new Color(1, 1, 1, 0.62f);
        public static readonly Color CardFill = new Color32(13, 20, 34, 235);

        public static Sprite Round, Ring, Circle, CircleRing, GradDown, GradUp, White;
        public static Sprite IArrow, ICheck, ICamera, IARBox, IList, IDiamond, IMove, IZoom, IRotate, ITap, IFinger, IProfile, INeedle;
        static bool inited;

        public static void Init()
        {
            if (inited) return;
            inited = true;
            Font = Font.CreateDynamicFontFromOSFont(new[] { "Inter", "SF Pro Display", "Helvetica Neue", "Arial" }, 32);
            if (Font == null) Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Round = RoundedSprite(false); Ring = RoundedSprite(true);
            Circle = CircleSprite(false); CircleRing = CircleSprite(true);
            GradDown = Gradient(false); GradUp = Gradient(true);
            White = MakeSprite(4, 4, (x, y) => 1f);

            IArrow = Icon(p => Min(Seg(p, V(.16, .5), V(.84, .5), .055f), Seg(p, V(.56, .25), V(.84, .5), .055f), Seg(p, V(.56, .75), V(.84, .5), .055f)));
            ICheck = Icon(p => Min(Seg(p, V(.18, .5), V(.41, .27), .07f), Seg(p, V(.41, .27), V(.82, .74), .07f)));
            ICamera = Icon(p => Min(BoxRing(p, V(.5, .46), V(.38, .24), .06f, .04f), Math.Abs((p - V(.5, .46)).magnitude - .13f) - .035f, Box(p, V(.5, .74), V(.1, .04), .02f)));
            IARBox = Icon(p => Min(BoxRing(p, V(.5, .5), V(.34, .34), .05f, .045f), Box(p, V(.5, .5), V(.1, .1), .02f)));
            IList = Icon(p => Min(BoxRing(p, V(.5, .5), V(.34, .3), .05f, .045f), Seg(p, V(.3, .36), V(.7, .36), .03f), Seg(p, V(.3, .5), V(.7, .5), .03f), Seg(p, V(.3, .64), V(.7, .64), .03f)));
            IDiamond = Icon(p => Math.Abs(Math.Abs(p.x - .5f) + Math.Abs(p.y - .5f) - .34f) * .7071f - .03f);
            IMove = Icon(p => Min(Seg(p, V(.14, .5), V(.86, .5), .04f), Seg(p, V(.5, .14), V(.5, .86), .04f),
                Seg(p, V(.14, .5), V(.26, .6), .04f), Seg(p, V(.14, .5), V(.26, .4), .04f), Seg(p, V(.86, .5), V(.74, .6), .04f), Seg(p, V(.86, .5), V(.74, .4), .04f),
                Seg(p, V(.5, .86), V(.4, .74), .04f), Seg(p, V(.5, .86), V(.6, .74), .04f), Seg(p, V(.5, .14), V(.4, .26), .04f), Seg(p, V(.5, .14), V(.6, .26), .04f)));
            IZoom = Icon(p => Min(Seg(p, V(.2, .2), V(.8, .8), .04f), Seg(p, V(.2, .2), V(.2, .4), .04f), Seg(p, V(.2, .2), V(.4, .2), .04f), Seg(p, V(.8, .8), V(.8, .6), .04f), Seg(p, V(.8, .8), V(.6, .8), .04f)));
            IRotate = Icon(p =>
            {
                Vector2 c = p - V(.5, .5); float ang = Mathf.Atan2(c.y, c.x) * Mathf.Rad2Deg;
                float d = Math.Abs(c.magnitude - .3f) - .04f;
                if (ang > 20f && ang < 75f) d = 1f;
                return Min(d, Seg(p, V(.5 + .3 * Math.Cos(75 * Mathf.Deg2Rad), .5 + .3 * Math.Sin(75 * Mathf.Deg2Rad)), V(.5 + .3 * Math.Cos(75 * Mathf.Deg2Rad) + .13, .5 + .3 * Math.Sin(75 * Mathf.Deg2Rad) + .01), .04f),
                    Seg(p, V(.5 + .3 * Math.Cos(75 * Mathf.Deg2Rad), .5 + .3 * Math.Sin(75 * Mathf.Deg2Rad)), V(.5 + .3 * Math.Cos(75 * Mathf.Deg2Rad) + .01, .5 + .3 * Math.Sin(75 * Mathf.Deg2Rad) - .13), .04f));
            });
            ITap = Icon(p => Min(Math.Abs((p - V(.5, .5)).magnitude - .36f) - .035f, (p - V(.5, .5)).magnitude - .13f));
            IFinger = Icon(p => Min(Seg(p, V(.42, .5), V(.42, .9), .075f), Box(p, V(.52, .36), V(.26, .2), .1f), Seg(p, V(.68, .45), V(.78, .55), .05f), Seg(p, V(.58, .5), V(.58, .56), .06f)));
            INeedle = Icon(p => (Math.Abs(p.x - .5f) / .16f + Math.Abs(p.y - .5f) / .46f - 1f) * .07f);
            IProfile = Icon(p => Min((p - V(.5, .66)).magnitude - .16f, Math.Abs((p - V(.5, .2)).magnitude - .36f) - .04f));
        }

        // ---------------- sprite generation ----------------
        static Vector2 V(double x, double y) { return new Vector2((float)x, (float)y); }
        static float Min(params float[] v) { float m = v[0]; for (int i = 1; i < v.Length; i++) if (v[i] < m) m = v[i]; return m; }
        static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }
        static float Box(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
        }
        static float BoxRing(Vector2 p, Vector2 c, Vector2 half, float r, float thick)
        {
            return Mathf.Abs(Box(p, c, half, r) + thick * 0.5f) - thick * 0.5f;
        }

        static Sprite MakeSprite(int w, int h, Func<int, int, float> alpha, float ppu = 100f, Vector4? border = null)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(1, 1, 1, Mathf.Clamp01(alpha(x, y)));
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border ?? Vector4.zero);
        }

        static Sprite Icon(Func<Vector2, float> sdf)
        {
            int s = 128;
            return MakeSprite(s, s, (x, y) =>
            {
                float a = 0f;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        Vector2 p = new Vector2((x + 0.25f + sx * 0.5f) / s, (y + 0.25f + sy * 0.5f) / s);
                        a += Mathf.Clamp01(0.5f - sdf(p) * s);
                    }
                return a * 0.25f;
            });
        }

        static Sprite RoundedSprite(bool ring)
        {
            int s = 128; float r = 56f, thick = 5f;
            return MakeSprite(s, s, (x, y) =>
            {
                float px = x + 0.5f - s * 0.5f, py = y + 0.5f - s * 0.5f;
                Vector2 q = new Vector2(Mathf.Abs(px), Mathf.Abs(py)) - new Vector2(s * 0.5f - r, s * 0.5f - r);
                float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
                float fill = Mathf.Clamp01(0.5f - d);
                if (!ring) return fill;
                return fill * Mathf.Clamp01(0.5f + (d + thick));
            }, 200f, new Vector4(r, r, r, r));
        }

        static Sprite CircleSprite(bool ring)
        {
            int s = 128;
            return MakeSprite(s, s, (x, y) =>
            {
                float d = new Vector2(x + 0.5f - s * 0.5f, y + 0.5f - s * 0.5f).magnitude - s * 0.5f + 1f;
                float fill = Mathf.Clamp01(0.5f - d);
                if (!ring) return fill;
                return fill * Mathf.Clamp01(0.5f + (d + 4.5f));
            });
        }

        /// <summary>up=false: opaque at the bottom, fading to clear at the top. up=true: the reverse.</summary>
        static Sprite Gradient(bool up)
        {
            return MakeSprite(4, 64, (x, y) =>
            {
                float t = y / 63f; // 0 bottom .. 1 top
                float a = up ? t : 1f - t;
                return Mathf.Pow(a, 1.5f);
            });
        }

        // ---------------- element factories ----------------
        public static RectTransform NewRect(string name, Transform parent)
        {
            var g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            return g.GetComponent<RectTransform>();
        }

        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>Anchor to a point, set pivot, position and size.</summary>
        public static void At(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        public static Image Img(Transform parent, Sprite s, Color c, string name = "Image", bool sliced = false, bool raycast = false)
        {
            var rt = NewRect(name, parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = s; im.color = c; im.raycastTarget = raycast;
            if (sliced) im.type = Image.Type.Sliced;
            return im;
        }

        public static Text Label(Transform parent, string text, int size, FontStyle style, Color color, TextAnchor anchor = TextAnchor.UpperLeft, string name = "Text")
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = anchor; t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.05f;
            return t;
        }

        public static Text Shadowed(Text t) { var s = t.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, 0.55f); s.effectDistance = new Vector2(0, -1.5f); return t; }

        public static Image Icon(Transform parent, Sprite s, Color c, float size, string name = "Icon")
        {
            var im = Img(parent, s, c, name);
            im.rectTransform.sizeDelta = new Vector2(size, size);
            return im;
        }

        /// <summary>Rounded panel with fill and a 1.5px outline.</summary>
        public static RectTransform Card(Transform parent, Color fill, Color border, string name = "Card")
        {
            var f = Img(parent, Round, fill, name, true);
            f.pixelsPerUnitMultiplier = 1f;
            var b = Img(f.transform, Ring, border, "Border", true);
            Fill(b.rectTransform);
            return f.rectTransform;
        }

        public static RectTransform Logo(Transform parent, int size = 19)
        {
            var t = Label(parent, "Muscle<color=#60A5FA>AR</color>", size, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, "Logo");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            Shadowed(t);
            return t.rectTransform;
        }

        /// <summary>Rounded outlined pill sized to its text (uses layout components).</summary>
        public static RectTransform Pill(Transform parent, string text, int size = 12, string name = "Pill")
        {
            var bg = Img(parent, Round, PillFill, name, true);
            var ring = Img(bg.transform, Ring, BlueBtn, "Border", true); Fill(ring.rectTransform);
            var h = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 7, 7); h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
            var fit = bg.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var t = Label(bg.transform, text, size, FontStyle.Bold, BlueText, TextAnchor.MiddleCenter, "Label");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var le = ring.gameObject.AddComponent<LayoutElement>(); le.ignoreLayout = true;
            return bg.rectTransform;
        }

        public static Button PrimaryButton(Transform parent, string text, bool arrow, Action onClick, float width = 342, float height = 56)
        {
            return PrimaryButton(parent, text, arrow ? IArrow : null, onClick, width, height);
        }

        public static Button PrimaryButton(Transform parent, string text, Sprite icon, Action onClick, float width = 342, float height = 56)
        {
            var bg = Img(parent, Round, BlueBtn, "Button", true, true);
            bg.rectTransform.sizeDelta = new Vector2(width, height);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg; btn.transition = Selectable.Transition.None;
            bg.gameObject.AddComponent<PressScale>();
            var t = Label(bg.transform, text, 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Label");
            Fill(t.rectTransform);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (icon != null)
            {
                float tw = text.Length * 10.2f;
                var a = Icon(bg.transform, icon, Color.white, 22, "Icon");
                At(a.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(tw * 0.5f + 18f, 0), new Vector2(20, 20));
                t.rectTransform.offsetMin = new Vector2(-18, 0); t.rectTransform.offsetMax = new Vector2(-18, 0);
            }
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Circular blue "next" arrow button used as the storyboard's step arrow.</summary>
        public static Button NextArrow(Transform parent, Action onClick, float size = 50)
        {
            var bg = Img(parent, Circle, BlueBtn, "NextArrow", false, true);
            bg.rectTransform.sizeDelta = new Vector2(size, size);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg; btn.transition = Selectable.Transition.None;
            bg.gameObject.AddComponent<PressScale>();
            var a = Icon(bg.transform, IArrow, Color.white, size * 0.56f, "Arrow");
            At(a.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.56f, size * 0.56f));
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    }

    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float target = 1f;
        public void OnPointerDown(PointerEventData e) { target = 0.96f; }
        public void OnPointerUp(PointerEventData e) { target = 1f; }
        public void OnPointerExit(PointerEventData e) { target = 1f; }
        void Update() { transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime)); }
    }

    /// <summary>Minimal horizontal slider (0..1) drawn with sprites.</summary>
    public class UISlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Action<float> OnChanged;
        public float Value { get; private set; }
        RectTransform track; Image fill; RectTransform knob;

        public static UISlider Create(Transform parent, float width)
        {
            var rt = UIKit.NewRect("Slider", parent);
            rt.sizeDelta = new Vector2(width, 36);
            var s = rt.gameObject.AddComponent<UISlider>();
            var hit = rt.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); hit.raycastTarget = true;
            var tr = UIKit.Img(rt, UIKit.Round, new Color(1, 1, 1, 0.18f), "Track", true);
            UIKit.At(tr.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(width, 6));
            s.track = tr.rectTransform;
            s.fill = UIKit.Img(tr.transform, UIKit.Round, UIKit.BlueBtn, "Fill", true);
            var k = UIKit.Img(rt, UIKit.Circle, Color.white, "Knob");
            k.rectTransform.sizeDelta = new Vector2(22, 22);
            s.knob = k.rectTransform;
            s.Set(0, false);
            return s;
        }

        public void Set(float v, bool notify = true)
        {
            Value = Mathf.Clamp01(v);
            float w = track.sizeDelta.x;
            fill.rectTransform.anchorMin = new Vector2(0, 0); fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.pivot = new Vector2(0, 0.5f); fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = new Vector2(Value * w, 0);
            fill.rectTransform.sizeDelta = new Vector2(Value * w, 0);
            knob.anchorMin = knob.anchorMax = new Vector2(0, 0.5f);
            knob.anchoredPosition = new Vector2(Value * w, 0);
            if (notify && OnChanged != null) OnChanged(Value);
        }

        void Pick(PointerEventData e)
        {
            Vector2 lp;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out lp))
                Set((lp.x) / track.sizeDelta.x);
        }
        public void OnPointerDown(PointerEventData e) { Pick(e); }
        public void OnDrag(PointerEventData e) { Pick(e); }
    }
}
