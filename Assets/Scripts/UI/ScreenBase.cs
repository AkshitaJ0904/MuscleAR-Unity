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
        public virtual void Enter()
        {
            T = 0f;
            if (MuscleAudioController.Instance != null)
            {
                MuscleAudioController.Instance.ScanAndBindButtons();
            }
        }
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
        protected float Bottom { get { return f.bottomInset; } }

        // ---------------- chrome ----------------
        protected Image TopShade(float h = 260f, float a = 0.50f)
        {
            var im = UIKit.Img(root, UIKit.GradUp, new Color(0.02f, 0.03f, 0.05f, a), "TopShade");
            UIKit.At(im.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, h));
            im.rectTransform.anchorMin = new Vector2(0, 1); im.rectTransform.anchorMax = new Vector2(1, 1);
            im.rectTransform.sizeDelta = new Vector2(0, h);
            return im;
        }

        protected Image BottomShade(float h = 320f, float a = 0.60f)
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

        // Stored layout references for responsive safe area updates
        protected RectTransform backBtnRt;
        protected RectTransform logoRt;
        protected RectTransform pillRt;
        protected RectTransform headlineRt;
        protected float headlineBaseY;
        protected float headlineYOffset;
        protected float headlineHeight;
        protected RectTransform subheadRt;
        protected float subheadBaseY;
        protected float subheadYOffset;

        /// <summary>Logo (top-left) + mode bar / pill (top-right). Back button on left when enabled.</summary>
        protected Text TopRow(string pill, bool compass = false, bool back = false)
        {
            float x = 16f;
            if (back)
            {
                var bb = UIKit.Img(root, UIKit.Round, new Color(0.10f, 0.16f, 0.28f, 0.95f), "BackButton", true, true);
                var ring = UIKit.Img(bb.transform, UIKit.Ring, UIKit.BorderSubtle, "Border", true);
                UIKit.Fill(ring.rectTransform);
                TL(bb.rectTransform, 16, Top + 6, 36, 32);
                var btn = bb.gameObject.AddComponent<Button>();
                btn.targetGraphic = bb;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => f.Back());
                MuscleAudioController.AttachClickSound(btn);
                var arr = UIKit.Label(bb.transform, "←", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Arr");
                UIKit.Fill(arr.rectTransform);
                backBtnRt = bb.rectTransform;
                x = 60f;
            }
            else if (compass) { Compass(root, 16, Top + 6); x = 68f; }

            var logo = UIKit.Logo(root, 18);
            logo.anchorMin = logo.anchorMax = new Vector2(0, 1); logo.pivot = new Vector2(0, 0.5f);
            logo.anchoredPosition = new Vector2(x, -(Top + 22)); logo.sizeDelta = new Vector2(110, 28);
            logoRt = logo;

            // On entry screens (< 4), display mode tag pill in top-right
            if (number < 4)
            {
                var p = UIKit.Pill(root, pill, 11);
                p.anchorMin = p.anchorMax = new Vector2(1, 1); p.pivot = new Vector2(1, 0.5f);
                p.anchoredPosition = new Vector2(-16, -(Top + 22));
                pillRt = p;
                return p.GetComponentInChildren<Text>();
            }
            return null;
        }

        protected void Compass(Transform parent, float x, float y)
        {
            var c = UIKit.Img(parent, UIKit.CircleRing, new Color(1, 1, 1, 0.35f), "Compass");
            TL(c.rectTransform, x, y, 36, 36);
            var n = UIKit.Label(c.transform, "N", 8, FontStyle.Bold, new Color(1, 1, 1, 0.75f), TextAnchor.UpperCenter, "N");
            UIKit.Fill(n.rectTransform); n.rectTransform.offsetMax = new Vector2(0, -2);
            var s = UIKit.Label(c.transform, "S", 8, FontStyle.Bold, new Color(1, 1, 1, 0.75f), TextAnchor.LowerCenter, "S");
            UIKit.Fill(s.rectTransform); s.rectTransform.offsetMin = new Vector2(0, 2);
            var needle = UIKit.Img(c.transform, UIKit.INeedle, new Color(1, 1, 1, 0.9f), "Needle");
            Mid(needle.rectTransform, 0, 0, 6, 22);
        }

        protected Text Headline(string text, int size = 26, float y = 0f)
        {
            var t = UIKit.Label(root, text, size, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, "Headline");
            headlineRt = t.rectTransform;
            headlineBaseY = number >= 4 ? 90f : 58f;
            headlineYOffset = y;
            headlineHeight = Mathf.Max(34f, size * 1.35f);
            TL(t.rectTransform, 10, Top + headlineBaseY + y, 340, headlineHeight);
            UIKit.Shadowed(t);
            return t;
        }

        protected Text Subhead(string text, float y = 0f, int size = 13)
        {
            var t = UIKit.Label(root, text, size, FontStyle.Normal, UIKit.Dim, TextAnchor.UpperLeft, "Subhead");
            t.lineSpacing = 1.25f;
            subheadRt = t.rectTransform;
            subheadBaseY = number >= 4 ? 128f : 98f;
            subheadYOffset = y;
            TL(t.rectTransform, 10, Top + subheadBaseY + y, 340, 42);
            UIKit.Shadowed(t);
            return t;
        }

        /// <summary>Updates anchored positions of header and title elements when safe area insets change.</summary>
        public virtual void UpdateSafeAreaLayout()
        {
            if (backBtnRt != null) TL(backBtnRt, 16, Top + 6, 36, 32);
            if (logoRt != null) logoRt.anchoredPosition = new Vector2(backBtnRt != null ? 60f : 16f, -(Top + 22));
            if (pillRt != null) pillRt.anchoredPosition = new Vector2(-16, -(Top + 22));
            if (headlineRt != null) TL(headlineRt, 10, Top + headlineBaseY + headlineYOffset, 340, headlineHeight);
            if (subheadRt != null) TL(subheadRt, 10, Top + subheadBaseY + subheadYOffset, 340, 42);
        }

        /// <summary>Top-centre caption (omitted on interactive screens to keep header uncluttered).</summary>
        protected void ViewerCaption()
        {
        }

        protected Button BottomButton(string text, Sprite icon, Action onClick, float y = 24f)
        {
            var b = UIKit.PrimaryButton(root, text, icon, onClick, 342, 52);
            BC(b.GetComponent<RectTransform>(), y + Bottom, 342, 52);
            return b;
        }

        protected Button BottomButton(string text, bool arrow, Action onClick, float y = 24f)
        {
            return BottomButton(text, arrow ? UIKit.IArrow : null, onClick, y);
        }

        protected Button Fab(float y = 110f)
        {
            // Redundant floating arrow removed to avoid duplicate arrow icons next to action buttons
            return null;
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
            if (f.model != null)
            {
                f.model.Show(show, pop);
                f.model.mode = mode;
            }
        }

        protected static Vector2 ToPhone(Vector3 viewport, AppFlow f)
        {
            Rect p = f.rig.phone;
            return new Vector2((viewport.x - p.x) / p.width * AppFlow.W, (viewport.y - p.y) / p.height * AppFlow.H);
        }

        /// <summary>
        /// Handled centrally by AppFlow.ModeSwitcherBar in the persistent AR/Environment header.
        /// </summary>
        protected Button AddResetRescanButton(float yOffset = 0f)
        {
            return null;
        }
    }
}
