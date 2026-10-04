using System;
using UnityEngine;
using UnityEngine.UI;

namespace MuscleAR
{
    /// <summary>Base for the 16 screens. Provides the shared Figma chrome: header, pill, headline, shades, buttons.</summary>
    public abstract class ScreenBase
    {
        public int number; public string title, tag;
        protected AppFlow f;
        public RectTransform root;
        CanvasGroup cg; float alpha; bool shown;
        protected float T;

        protected ScreenBase(AppFlow f, int number, string title, string tag)
        {
            this.f = f; this.number = number; this.title = title; this.tag = tag;
            root = UIKit.NewRect("Screen_" + number.ToString("00") + "_" + title.Replace(' ', '_'), f.phone);
            UIKit.Fill(root);
            cg = root.gameObject.AddComponent<CanvasGroup>();
        }

        public abstract void Build();
        public virtual void Enter() { T = 0f; }
        public virtual void Exit() { }
        public virtual void Tick() { T += Time.deltaTime; }
        public virtual void OnTap(Vector2 screenPos) { }

        public void SetShown(bool s, bool instant = false)
        {
            shown = s;
            if (s) root.gameObject.SetActive(true);
            if (instant) { alpha = s ? 1f : 0f; cg.alpha = alpha; root.gameObject.SetActive(s); }
        }

        public void Frame()
        {
            if (!root.gameObject.activeSelf) return;
            alpha = Mathf.MoveTowards(alpha, shown ? 1f : 0f, Time.deltaTime * 4.5f);
            cg.alpha = alpha;
            cg.blocksRaycasts = shown && alpha > 0.6f;
            cg.interactable = shown;
            if (!shown && alpha <= 0.001f) root.gameObject.SetActive(false);
        }

        // ---------------- layout helpers ----------------
        protected static void TL(RectTransform rt, float x, float y, float w, float h)
        { UIKit.At(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h)); }
        protected static void TR(RectTransform rt, float x, float y, float w, float h)
        { UIKit.At(rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-x, -y), new Vector2(w, h)); }
        protected static void TC(RectTransform rt, float y, float w, float h)
        { UIKit.At(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(w, h)); }
        protected static void BC(RectTransform rt, float y, float w, float h, float x = 0)
        { UIKit.At(rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(x, y), new Vector2(w, h)); }
        protected static void BR(RectTransform rt, float x, float y, float w, float h)
        { UIKit.At(rt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-x, y), new Vector2(w, h)); }
        protected static void Mid(RectTransform rt, float x, float y, float w, float h)
        { UIKit.At(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h)); }

        protected float Top { get { return f.topInset; } }

        // ---------------- chrome ----------------
        protected Image TopShade(float h = 320f, float a = 0.78f)
        {
            var im = UIKit.Img(root, UIKit.GradUp, new Color(0.02f, 0.03f, 0.05f, a), "TopShade");
            UIKit.At(im.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, h));
            im.rectTransform.anchorMin = new Vector2(0, 1); im.rectTransform.anchorMax = new Vector2(1, 1);
            im.rectTransform.sizeDelta = new Vector2(0, h);
            return im;
        }

        protected Image BottomShade(float h = 380f, float a = 0.88f)
        {
            var im = UIKit.Img(root, UIKit.GradDown, new Color(0.02f, 0.03f, 0.05f, a), "BottomShade");
            im.rectTransform.anchorMin = new Vector2(0, 0); im.rectTransform.anchorMax = new Vector2(1, 0);
            im.rectTransform.pivot = new Vector2(0.5f, 0); im.rectTransform.anchoredPosition = Vector2.zero;
            im.rectTransform.sizeDelta = new Vector2(0, h);
            return im;
        }

        protected Image Veil(float a)
        {
            var im = UIKit.Img(root, UIKit.White, new Color(0.02f, 0.03f, 0.06f, a), "Veil");
            UIKit.Fill(im.rectTransform);
            return im;
        }

        /// <summary>Logo (top-left) + outlined pill (top-right). Optionally a compass left of the logo.</summary>
        protected Text TopRow(string pill, bool compass = false)
        {
            float x = 20f;
            if (compass) { Compass(root, 20, Top + 38); x = 72f; }
            var logo = UIKit.Logo(root, 19);
            logo.anchorMin = logo.anchorMax = new Vector2(0, 1); logo.pivot = new Vector2(0, 0.5f);
            logo.anchoredPosition = new Vector2(x, -(Top + 58)); logo.sizeDelta = new Vector2(140, 28);
            var p = UIKit.Pill(root, pill, 12);
            p.anchorMin = p.anchorMax = new Vector2(1, 1); p.pivot = new Vector2(1, 0.5f);
            p.anchoredPosition = new Vector2(-20, -(Top + 58));
            return p.GetComponentInChildren<Text>();
        }

        protected void Compass(Transform parent, float x, float y)
        {
            var c = UIKit.Img(parent, UIKit.CircleRing, new Color(1, 1, 1, 0.35f), "Compass");
            TL(c.rectTransform, x, y, 44, 44);
            var n = UIKit.Label(c.transform, "N", 8, FontStyle.Bold, new Color(1, 1, 1, 0.75f), TextAnchor.UpperCenter, "N");
            UIKit.Fill(n.rectTransform); n.rectTransform.offsetMax = new Vector2(0, -3);
            var s = UIKit.Label(c.transform, "S", 8, FontStyle.Bold, new Color(1, 1, 1, 0.75f), TextAnchor.LowerCenter, "S");
            UIKit.Fill(s.rectTransform); s.rectTransform.offsetMin = new Vector2(0, 3);
            var needle = UIKit.Img(c.transform, UIKit.INeedle, new Color(1, 1, 1, 0.9f), "Needle");
            Mid(needle.rectTransform, 0, 0, 7, 26);
        }

        protected Text Headline(string text, int size = 34, float y = 0f)
        {
            var t = UIKit.Label(root, text, size, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, "Headline");
            TL(t.rectTransform, 20, Top + 92 + y, 350, size * 2.6f);
            UIKit.Shadowed(t);
            return t;
        }

        protected Text Subhead(string text, float y, int size = 16)
        {
            var t = UIKit.Label(root, text, size, FontStyle.Normal, new Color(1, 1, 1, 0.9f), TextAnchor.UpperLeft, "Subhead");
            TL(t.rectTransform, 20, Top + y, 320, 60);
            UIKit.Shadowed(t);
            return t;
        }

        /// <summary>"ANATOMY VIEWER v2.3 / FULL MUSCULOSKELETAL SYSTEM" top-centre caption.</summary>
        protected void ViewerCaption()
        {
            var a = UIKit.Label(root, "ANATOMY VIEWER v2.3", 12, FontStyle.Bold, new Color(1, 1, 1, 0.78f), TextAnchor.MiddleCenter, "ViewerTitle");
            TC(a.rectTransform, Top + 44, 200, 16);
            var b = UIKit.Label(root, "FULL MUSCULOSKELETAL SYSTEM", 7, FontStyle.Normal, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, "ViewerSub");
            TC(b.rectTransform, Top + 60, 200, 10);
        }

        protected Button BottomButton(string text, Sprite icon, Action onClick, float y = 34f)
        {
            var b = UIKit.PrimaryButton(root, text, icon, onClick);
            BC(b.GetComponent<RectTransform>(), y, 342, 56);
            return b;
        }

        protected Button BottomButton(string text, bool arrow, Action onClick, float y = 34f)
        {
            return BottomButton(text, arrow ? UIKit.IArrow : null, onClick, y);
        }

        protected Button Fab(float y = 110f)
        {
            var b = UIKit.NextArrow(root, () => f.Next(), 50);
            BR(b.GetComponent<RectTransform>(), 24, y, 50, 50);
            return b;
        }

        /// <summary>Rounded translucent pill-shaped bottom label (e.g. "Drag to move").</summary>
        protected RectTransform BottomPill(string text, float y)
        {
            var p = UIKit.Pill(root, text, 14);
            p.anchorMin = p.anchorMax = new Vector2(0.5f, 0); p.pivot = new Vector2(0.5f, 0);
            p.anchoredPosition = new Vector2(0, y);
            var h = p.GetComponent<HorizontalLayoutGroup>(); h.padding = new RectOffset(22, 22, 11, 11);
            return p;
        }

        protected static string Short(string name)
        {
            if (name == "Biceps Brachii") return "Biceps";
            if (name == "Triceps Brachii") return "Triceps";
            return name;
        }

        protected void SetupModel(ModelController.Mode mode, bool show = true, bool pop = false)
        {
            f.model.Show(show, pop);
            f.model.mode = mode;
        }

        protected static Vector2 ToPhone(Vector3 viewport, AppFlow f)
        {
            Rect p = f.rig.phone;
            return new Vector2((viewport.x - p.x) / p.width * AppFlow.W, (viewport.y - p.y) / p.height * AppFlow.H);
        }
    }
}
