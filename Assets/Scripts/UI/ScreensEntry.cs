using System;
using UnityEngine;
using UnityEngine.UI;

namespace MuscleAR
{
    // ===================================================================================================
    // 01 HOME SCREEN  -  ENTRY
    // ===================================================================================================
    public class HomeScreen : ScreenBase
    {
        RectTransform ring;
        public HomeScreen(AppFlow f) : base(f, 1, "HOME SCREEN", "ENTRY") { }

        public override void Build()
        {
            Veil(0.80f);
            BottomShade(420, 0.9f);

            var c = UIKit.Img(root, UIKit.CircleRing, new Color(0.26f, 0.53f, 0.96f, 0.9f), "LogoRing");
            Mid(c.rectTransform, 0, 110, 112, 112);
            ring = c.rectTransform;
            var inner = UIKit.Img(root, UIKit.Circle, new Color(0.1f, 0.2f, 0.4f, 0.55f), "LogoFill");
            Mid(inner.rectTransform, 0, 110, 96, 96);
            var ic = UIKit.Icon(root, UIKit.IARBox, UIKit.BlueText, 52, "LogoIcon");
            Mid(ic.rectTransform, 0, 110, 52, 52);

            var t = UIKit.Label(root, "Muscle<color=#60A5FA>AR</color>", 46, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Title");
            Mid(t.rectTransform, 0, 20, 340, 60);
            var s = UIKit.Label(root, "Explore. Learn. Understand.", 16, FontStyle.Normal, UIKit.Dim, TextAnchor.MiddleCenter, "Tagline");
            Mid(s.rectTransform, 0, -22, 340, 24);

            var cap = UIKit.Label(root, "Open the anatomy lab and begin an immersive learning journey.", 14, FontStyle.Normal, UIKit.Dim, TextAnchor.MiddleCenter, "Caption");
            BC(cap.rectTransform, 108, 300, 44);
            BottomButton("Enter Anatomy Lab", true, () => f.Next());
            var v = UIKit.Label(root, "ANATOMY VIEWER v2.3", 9, FontStyle.Bold, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleCenter, "Version");
            TC(v.rectTransform, Top + 40, 200, 12);
        }

        public override void Enter()
        {
            base.Enter();
            f.lab.SetLook(LabEnvironment.Look.Lab);
            f.scanner.Stop();
            SetupModel(ModelController.Mode.None, true, false); f.model.ShowDisc(true);
            f.body.SetLayer(BodyLayer.Muscle);
            f.rig.Orbit(new Vector3(0, 0.9f, 0), 2.7f, 1.0f, 3.5f, 20f, 42f);
        }

        public override void Tick()
        {
            base.Tick();
            float s = 1f + 0.05f * Mathf.Sin(T * 2.2f);
            ring.localScale = new Vector3(s, s, 1);
        }
    }

    // ===================================================================================================
    // 02 ONBOARDING  -  ORIENT
    // ===================================================================================================
    public class OnboardingScreen : ScreenBase
    {
        public OnboardingScreen(AppFlow f) : base(f, 2, "ONBOARDING", "ORIENT") { }

        public override void Build()
        {
            TopShade(360, 0.88f);
            BottomShade(300, 0.92f);
            var h = Headline("Your Anatomy Lab.\nAnywhere.", 36, -8);
            Subhead("Explore a realistic human body in augmented reality.", 196, 17);
            BottomButton("Continue", true, () => f.Next());
        }

        public override void Enter()
        {
            base.Enter();
            f.lab.SetLook(LabEnvironment.Look.Lab);
            SetupModel(ModelController.Mode.None, true, false); f.model.ShowDisc(false);
            f.rig.Orbit(new Vector3(0, 0.88f, 0.1f), 2.3f, 0.95f, -2.5f, 8f, 40f);
        }
    }

    // ===================================================================================================
    // 03 CHOOSE MODE  -  SETUP
    // ===================================================================================================
    public class ChooseModeScreen : ScreenBase
    {
        class Opt { public Image bg, ring, check; public Text sub, title; }
        readonly Opt[] opts = new Opt[2];
        Button startBtn;

        public ChooseModeScreen(AppFlow f) : base(f, 3, "CHOOSE MODE", "SETUP") { }

        public override void Build()
        {
            var bg = UIKit.Img(root, UIKit.White, new Color(0.035f, 0.075f, 0.15f, 1f), "Bg"); UIKit.Fill(bg.rectTransform);
            var glow = UIKit.Img(root, UIKit.GradUp, new Color(0.12f, 0.3f, 0.65f, 0.35f), "Glow");
            glow.rectTransform.anchorMin = new Vector2(0, 0.45f); glow.rectTransform.anchorMax = new Vector2(1, 1); glow.rectTransform.offsetMin = glow.rectTransform.offsetMax = Vector2.zero;
            GhostHud();

            Headline("How do you want\nto learn?", 31, -2);

            string[] names = { "AR Explorer", "Quiz" };
            string[] subs = { "Place full-body anatomy in your real space", "Test your knowledge on 6 target muscles" };
            Sprite[] icons = { UIKit.IARBox, UIKit.IDiamond };
            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                float y = 130f + (1 - i) * 88f;
                var o = new Opt();
                o.bg = UIKit.Img(root, UIKit.Round, UIKit.CardFill, "Option" + i, true, true);
                BC(o.bg.rectTransform, y, 342, 74);
                var b = o.bg.gameObject.AddComponent<Button>(); b.targetGraphic = o.bg; b.transition = UnityEngine.UI.Selectable.Transition.None;
                o.bg.gameObject.AddComponent<PressScale>();
                b.onClick.AddListener(() => Select(idx));
                o.ring = UIKit.Img(o.bg.transform, UIKit.Ring, new Color(0.3f, 0.4f, 0.55f, 0.55f), "Border", true); UIKit.Fill(o.ring.rectTransform);
                var ic = UIKit.Icon(o.bg.transform, icons[i], Color.white, 20, "Icon");
                UIKit.At(ic.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(16, -24), new Vector2(20, 20));
                o.title = UIKit.Label(o.bg.transform, names[i], 19, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, "Title");
                UIKit.At(o.title.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(44, -24), new Vector2(200, 26));
                o.sub = UIKit.Label(o.bg.transform, subs[i], 13, FontStyle.Normal, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleLeft, "Sub");
                UIKit.At(o.sub.rectTransform, new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(16, 22), new Vector2(300, 22));
                o.check = UIKit.Icon(o.bg.transform, UIKit.ICheck, Color.white, 18, "Check");
                UIKit.At(o.check.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(44 + names[i].Length * 10.4f + 12, -24), new Vector2(18, 18));
                opts[i] = o;
            }
            startBtn = BottomButton("Start Exploring", true, StartPressed);
            Select(f.mode == LearnMode.Quiz ? 1 : 0);
        }

        void GhostHud()
        {
            // faint holographic "mode selection" HUD behind the cards (as in the Figma backdrop)
            var card = UIKit.Card(root, new Color(1, 1, 1, 0.03f), new Color(0.4f, 0.7f, 1f, 0.16f), "GhostCard");
            TC(card, Top + 150, 300, 160);
            string[] g = { "DIAGNOSTIC", "THERAPEUTIC", "MONITORING" };
            for (int i = 0; i < 3; i++)
            {
                var c = UIKit.Img(card, UIKit.CircleRing, new Color(0.5f, 0.8f, 1f, i == 0 ? 0.35f : 0.18f), "G" + i);
                UIKit.At(c.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-92 + i * 92, -40), new Vector2(54, 54));
                var l = UIKit.Label(card, g[i], 8, FontStyle.Normal, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleCenter, "L" + i);
                UIKit.At(l.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-92 + i * 92, -110), new Vector2(90, 14));
            }
            var cap = UIKit.Label(card, "MODE SELECTION", 8, FontStyle.Bold, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleCenter, "Cap");
            UIKit.At(cap.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(200, 12));
        }

        void Select(int i)
        {
            f.mode = i == 1 ? LearnMode.Quiz : LearnMode.ARExplorer;
            for (int k = 0; k < 2; k++)
            {
                bool on = k == i;
                opts[k].bg.color = on ? new Color(0.1f, 0.22f, 0.45f, 0.95f) : UIKit.CardFill;
                opts[k].ring.color = on ? UIKit.BlueBtn : new Color(0.3f, 0.4f, 0.55f, 0.55f);
                opts[k].sub.color = on ? new Color(1, 1, 1, 0.62f) : new Color(1, 1, 1, 0.5f);
                opts[k].check.enabled = on;
            }
        }

        void StartPressed() { f.Next(); }

        public override void Enter()
        {
            var mainUI = MainUIController.Instance;
            if (mainUI == null) mainUI = UnityEngine.Object.FindAnyObjectByType<MainUIController>(FindObjectsInactive.Include);
            if (mainUI != null)
            {
                if (root != null) root.gameObject.SetActive(false);
                mainUI.ShowChooseMode();
                return;
            }

            base.Enter();
            f.model.Show(false, false);
            if (f.ar != null)
            {
                f.ar.SetPlanesActive(false);
            }
            if (f.scanner != null) f.scanner.Stop();
            if (f.lab != null) { f.lab.SetVisible(false); f.lab.gameObject.SetActive(false); }
            if (f.rig != null && f.rig.main != null)
            {
                f.rig.main.enabled = true;
                f.rig.main.tag = "MainCamera";
                f.rig.Orbit(new Vector3(0, 0.9f, 0), 2.7f, 1f, 3f, 40f, 42f);
            }
            Select(f.mode == LearnMode.Quiz ? 1 : 0);
        }
    }

    // ===================================================================================================
    // 04 DETECT TABLE SURFACE  -  SCAN
    // ===================================================================================================
    public class DetectSurfaceScreen : ScreenBase
    {
        Text status; Image statusCheck; Button place; Image placeBg; Text placeLabel;
        RectTransform frame; Image camBtnIcon; Text camHint;
        bool wasDetected;

        public DetectSurfaceScreen(AppFlow f) : base(f, 4, "DETECT TABLE SURFACE", "SCAN") { }

        public override void Build()
        {
            TopShade(260, 0.45f);
            BottomShade(300, 0.55f);
            TopRow("SCAN", false, true);
            Headline("Scan your space", 26, 0);
            Subhead("Move your phone slowly to scan a surface (table or desk).", 0, 14);

            // scan frame with hatch marks (as in the Figma)
            frame = UIKit.Card(root, new Color(0.1f, 0.25f, 0.55f, 0.08f), UIKit.BorderAccent, "ScanFrame");
            BC(frame, Bottom + 154, 342, 136);
            var hatch = UIKit.Label(frame, "/ / / / /\n— — — — —\n/ / / / /", 16, FontStyle.Normal, UIKit.BlueText, TextAnchor.MiddleCenter, "Hatch");
            UIKit.Fill(hatch.rectTransform);
            hatch.lineSpacing = 0.95f;

            var sp = UIKit.Pill(root, "Move phone slowly to scan…", 13, "Status");
            sp.anchorMin = sp.anchorMax = new Vector2(0.5f, 0); sp.pivot = new Vector2(0.5f, 0);
            sp.anchoredPosition = new Vector2(0, Bottom + 90);
            status = sp.GetComponentInChildren<Text>();
            statusCheck = UIKit.Icon(sp, UIKit.ICheck, UIKit.BlueText, 14, "Check");
            var le = statusCheck.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 14; le.preferredHeight = 14;
            statusCheck.enabled = false;
            sp.GetComponent<HorizontalLayoutGroup>().spacing = 6;

            place = BottomButton("Place Anatomy", (Sprite)null, () => TryPlaceAtCentre(), 20f);
            placeBg = place.GetComponent<Image>(); placeLabel = place.GetComponentInChildren<Text>();
        }

        // The tap is only a trigger: placement always comes from the viewport centre.
        public override void OnTap(Vector2 p) { TryPlaceAtCentre(); }

        void TryPlaceAtCentre()
        {
            if (!f.EnsureModelAnchoredInAR()) return; // no valid centre surface: stay in placement state
            if (f.ar != null) f.ar.SetPlanesActive(false);
            f.GoNumber(6);
        }

        public override void Enter()
        {
            base.Enter();
            if (f.placed || f.model.useWorldAnchor || f.viewingMode == AppFlow.ViewingMode.Environment)
            {
                f.GoNumber(6);
                return;
            }

            f.model.Show(false, false);
            f.placed = false;
            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                f.scanner.Stop();
                if (f.lab != null) { f.lab.SetVisible(false); f.lab.gameObject.SetActive(false); }
                if (f.rig != null && f.rig.main != null)
                {
                    f.rig.main.enabled = false;
                    f.rig.main.tag = "Untagged";
                }
                if (f.ar != null)
                {
                    f.ar.SetARActive(true);
                    f.ar.SetPlanesActive(true);
                }
            }
            else
            {
                f.scanner.Begin();
            }
        }

        public override void Exit() { f.scanner.Stop(); }

        void SetPlace(bool on)
        {
            place.interactable = on;
            placeBg.color = on ? UIKit.BlueBtn : new Color(UIKit.BlueBtn.r, UIKit.BlueBtn.g, UIKit.BlueBtn.b, 0.38f);
            placeLabel.color = on ? Color.white : new Color(1, 1, 1, 0.6f);
        }

        public override void Tick()
        {
            base.Tick();

            if (f.ar != null && f.ar.IsARActive)
            {
                if (f.ar.HasDetectedPlanes)
                {
                    wasDetected = true;
                    f.scanner.MarkDetected();
                    bool centreValid = f.ar.TryRaycastViewportCentre(out _);
                    status.text = centreValid ? "Surface found! Tap anywhere to place at the centre"
                        : "Aim the centre of the screen at the detected surface";
                    statusCheck.enabled = centreValid;
                    SetPlace(centreValid);
                }
            }
            else
            {
                if (f.scanner.Detected && !wasDetected)
                {
                    wasDetected = true;
                    status.text = "Surface detected"; statusCheck.enabled = true;
                    SetPlace(true);
                }
            }

            float p = 0.5f + 0.5f * Mathf.Sin(T * 3f);
            frame.localScale = Vector3.one * (f.scanner.Detected ? 1f : 1f + 0.012f * p);
        }
    }

    // ===================================================================================================
    // 05 PLACE MODEL  -  PLACE
    // ===================================================================================================
    public class PlaceModelScreen : ScreenBase
    {
        RectTransform tip, ring;
        public PlaceModelScreen(AppFlow f) : base(f, 5, "PLACE MODEL", "PLACE") { }

        public override void Build()
        {
            TopShade(300, 0.55f);
            BottomShade(380, 0.9f);
            TopRow("PLACE", false, true);
            ViewerCaption();
            Headline("Position anatomy", 26, 0);

            var r = UIKit.Img(root, UIKit.CircleRing, new Color(0.38f, 0.66f, 1f, 0.95f), "PlaceRing");
            BC(r.rectTransform, 116, 124, 38);
            r.rectTransform.sizeDelta = new Vector2(124, 38);
            ring = r.rectTransform;
            var fillE = UIKit.Img(r.transform, UIKit.Circle, new Color(0.2f, 0.45f, 0.9f, 0.22f), "Fill"); UIKit.Fill(fillE.rectTransform);

            tip = UIKit.Card(root, new Color(0.05f, 0.09f, 0.16f, 0.9f), UIKit.BlueBtn, "Tooltip");
            BC(tip, 168, 160, 62);
            var tt = UIKit.Label(tip, "Anatomy Model\nTap to place", 15, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter, "T");
            UIKit.Fill(tt.rectTransform);

            BottomButton("Place Here", UIKit.ICheck, () => DoPlace(null));
        }

        void DoPlace(Vector2? tapPos)
        {
            if (f.ar != null && f.ar.IsARActive)
            {
                Vector2 screenPoint = tapPos.HasValue ? tapPos.Value : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Pose hitPose;
                if (f.ar.TryRaycastPlane(screenPoint, out hitPose) || f.ar.TryGetFirstPlanePose(out hitPose))
                {
                    Vector3 camPos = f.ar.arCamera != null ? f.ar.arCamera.transform.position : Vector3.zero;
                    f.model.SetAnchor(hitPose.position, hitPose.rotation, camPos);
                    f.ar.SetPlanesActive(false);
                }
            }
            f.placed = true;
            f.model.Show(true, false);   // confirm pop
            f.Next();
        }

        public override void OnTap(Vector2 p)
        {
            if (T > 0.6f) DoPlace(p);
        }

        public override void Enter()
        {
            base.Enter();
            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                if (f.lab != null) f.lab.SetVisible(false);
                if (f.rig != null && f.rig.main != null)
                {
                    f.rig.main.enabled = false;
                    f.rig.main.tag = "Untagged";
                }
                if (f.ar != null) f.ar.SetARActive(true);
                f.EnsureModelAnchoredInAR();
            }
            else
            {
                if (f.lab != null)
                {
                    f.lab.SetVisible(true);
                    f.lab.SetLook(f.currentEnvironmentLook);
                }
                if (f.rig != null && f.rig.main != null)
                {
                    f.rig.main.enabled = true;
                    f.rig.main.tag = "MainCamera";
                }
                f.rig.Frame(new Vector3(0, 1.62f, 2.45f), new Vector3(0, 0.88f, -0.1f), 46f, 1.8f);
            }
            f.scanner.Stop();
            SetupModel(ModelController.Mode.Move, true, false); f.model.ShowDisc(true);
            f.body.SetLayer(BodyLayer.Muscle);
            PlaceLabels(f);
        }

        public static void PlaceLabels(AppFlow f)
        {
            // Unwanted labels and callouts (Gluteus Maximus, Musculoskeletal System, Heart, Lungs, Brain) removed
            // Screen remains clean and uncluttered.
            f.labels.Clear();
        }

        public override void Tick()
        {
            base.Tick();
            float s = 1f + 0.06f * Mathf.Sin(T * 3f);
            ring.localScale = new Vector3(s, s, 1);
            tip.anchoredPosition = new Vector2(0, 168 + 3f * Mathf.Sin(T * 2.4f));
        }
    }

    // ===================================================================================================
    // 06 MODEL PLACED  -  AR ACTIVE
    // ===================================================================================================
    public class ModelPlacedScreen : ScreenBase
    {
        public ModelPlacedScreen(AppFlow f) : base(f, 6, "MODEL PLACED", "AR ACTIVE") { }

        public override void Build()
        {
            TopShade(260, 0.45f);
            BottomShade(280, 0.65f);
            TopRow("AR ACTIVE", false, true);
            ViewerCaption();
            Headline("Ready to explore", 26, 0);

            // Model Transform & Navigation Toolbar (5 buttons with consistent sizing)
            var bar = UIKit.Card(root, new Color(0.06f, 0.10f, 0.18f, 0.95f), UIKit.BorderSubtle, "ARControlsBar");
            BC(bar, Bottom + 18, 350, 44);

            // Rotate left, Rotate right, Zoom in, Zoom out, Reset View (re-scan is in the top bar)
            string[] btnLabels = { "⟲ -15°", "⟳ +15°", "Zoom −", "Zoom +", "Reset View" };
            Action[] actions = new Action[] {
                () => { f.model.yaw -= 15f; f.model.Apply(); },
                () => { f.model.yaw += 15f; f.model.Apply(); },
                () => { f.model.scale = Mathf.Max(0.15f, f.model.scale * 0.85f); f.model.Apply(); },
                () => { f.model.scale = Mathf.Min(2.0f, f.model.scale * 1.18f); f.model.Apply(); },
                () => { f.model.ResetTransform(); }
            };

            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                float x = -136f + i * 68f;
                var btnImg = UIKit.Img(bar, UIKit.Round, new Color(0.10f, 0.16f, 0.28f, 0.95f), "Btn" + i, true, true);
                UIKit.At(btnImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(62, 34));
                var bRing = UIKit.Img(btnImg.transform, UIKit.Ring, UIKit.BorderSubtle, "Border", true);
                UIKit.Fill(bRing.rectTransform);

                var btn = btnImg.gameObject.AddComponent<Button>();
                btn.targetGraphic = btnImg;
                btn.transition = Selectable.Transition.None;
                btnImg.gameObject.AddComponent<PressScale>();
                btn.onClick.AddListener(() => actions[idx]());
                MuscleAudioController.AttachClickSound(btn);

                var lbl = UIKit.Label(btnImg.transform, btnLabels[i], 10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Txt");
                UIKit.Fill(lbl.rectTransform);
            }

            // Button to jump straight to interactive muscle selection (no extra arrow)
            var expBtn = BottomButton("Explore Muscles", (Sprite)null, () => f.GoNumber(9), 72f);

            // Drag repositioning guidance
            var dragHint = UIKit.Label(root, "Drag to rotate  •  Pinch to zoom  •  Two fingers to move", 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "GestureHint");
            BC(dragHint.rectTransform, Bottom + 132, 390, 22);
            var mouseHint = UIKit.Label(root, "Editor: left-drag rotate  •  right-drag move  •  scroll zoom", 9, FontStyle.Normal, new Color(0.82f, 0.9f, 1f, 0.82f), TextAnchor.MiddleCenter, "MouseHint");
            BC(mouseHint.rectTransform, Bottom + 110, 390, 18);
        }

        public override void OnTap(Vector2 p)
        {
            MuscleId id;
            if (f.model.Pick(p, out id) && MuscleDB.IsTarget(id))
            {
                if (MuscleAudioController.Instance != null)
                    MuscleAudioController.Instance.PlayClick();
                f.selected = id;
                f.GoNumber(10);
            }
        }

        public override void Enter()
        {
            base.Enter();
            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                if (f.lab != null) f.lab.SetVisible(false);
                if (f.rig != null && f.rig.main != null)
                {
                    f.rig.main.enabled = false;
                    f.rig.main.tag = "Untagged";
                }
                if (f.ar != null) f.ar.SetARActive(true);
                f.EnsureModelAnchoredInAR();
            }
            else
            {
                if (f.lab != null)
                {
                    f.lab.SetVisible(true);
                    f.lab.SetLook(f.currentEnvironmentLook);
                }
                if (f.rig != null && f.rig.main != null)
                {
                    f.rig.main.enabled = true;
                    f.rig.main.tag = "MainCamera";
                }
                f.rig.Frame(new Vector3(0, 1.62f, 2.45f), new Vector3(0, 0.88f, -0.1f), 46f, 1.8f);
            }
            SetupModel(ModelController.Mode.Explore, true, false); f.model.ShowDisc(true);
            PlaceModelScreen.PlaceLabels(f);
        }
    }

    // ===================================================================================================
    // 07 MOVE MODEL  -  GESTURE
    // ===================================================================================================
    public class MoveModelScreen : ScreenBase
    {
        RectTransform finger, arrowLine, hand; Text pillText; bool done; float doneT;
        public MoveModelScreen(AppFlow f) : base(f, 7, "MOVE MODEL", "GESTURE") { }

        public override void Build()
        {
            TopShade(300, 0.5f);
            BottomShade(300, 0.9f);
            TopRow("GESTURE", false, true);
            ViewerCaption();
            Headline("Drag to move", 26, 0);
            AddResetRescanButton();

            hand = UIKit.NewRect("HandGroup", root);
            BC(hand, 120, 200, 110);
            var line = UIKit.Img(hand, UIKit.White, new Color(0.38f, 0.66f, 1f, 0.95f), "Line");
            UIKit.At(line.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(-12, 14), new Vector2(120, 2));
            var head = UIKit.Icon(hand, UIKit.IArrow, new Color(0.38f, 0.66f, 1f, 0.95f), 40, "Head");
            UIKit.At(head.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(60, 14), new Vector2(34, 34));
            var fg = UIKit.Icon(hand, UIKit.IFinger, new Color(1f, 0.8f, 0.3f, 1f), 70, "Finger");
            UIKit.At(fg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(60, 60));
            finger = fg.rectTransform;

            var p = BottomPill("Drag to move", 30);
            pillText = p.GetComponentInChildren<Text>();
            Fab(104);
        }

        public override void Enter()
        {
            base.Enter();
            done = false; doneT = 0;
            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                if (f.lab != null) { f.lab.SetVisible(false); f.lab.gameObject.SetActive(false); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = false; f.rig.main.tag = "Untagged"; }
                if (f.ar != null) f.ar.SetARActive(true);
            }
            else
            {
                if (f.lab != null) { f.lab.gameObject.SetActive(true); f.lab.SetVisible(true); f.lab.SetLook(f.currentEnvironmentLook); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = true; f.rig.main.tag = "MainCamera"; }
                f.rig.Frame(new Vector3(0, 1.62f, 2.45f), new Vector3(0, 0.88f, -0.1f), 46f, 1.8f);
            }
            PlaceModelScreen.PlaceLabels(f);
        }

        public override void Tick()
        {
            base.Tick();
            float k = Mathf.PingPong(T * 0.55f, 1f);
            k = k * k * (3 - 2 * k);
            finger.anchoredPosition = new Vector2(Mathf.Lerp(-60, 60, k) - 0f, 20);
            if (!done && f.model.movedDistance > 0.14f)
            {
                done = true; pillText.text = "Nice — model moved";
            }
            if (done) { doneT += Time.deltaTime; if (doneT > 1.4f) f.Next(); }
        }
    }

    // ===================================================================================================
    // 08 ROTATE & ZOOM  -  GESTURE   (not in the shared screenshots; inferred from the gesture bar of 06)
    // ===================================================================================================
    public class RotateZoomScreen : ScreenBase
    {
        RectTransform rot, zoom; Text pillText; bool done; float doneT;
        public RotateZoomScreen(AppFlow f) : base(f, 8, "ROTATE & ZOOM", "GESTURE") { }

        public override void Build()
        {
            TopShade(300, 0.5f);
            BottomShade(300, 0.9f);
            TopRow("GESTURE", false, true);
            ViewerCaption();
            Headline("Rotate & zoom", 26, 0);
            AddResetRescanButton();

            var g = UIKit.NewRect("Icons", root); BC(g, 118, 220, 90);
            var r = UIKit.Icon(g, UIKit.IRotate, new Color(0.45f, 0.72f, 1f, 1f), 62, "Rotate");
            UIKit.At(r.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-55, 0), new Vector2(62, 62));
            rot = r.rectTransform;
            var z = UIKit.Icon(g, UIKit.IZoom, new Color(0.45f, 0.72f, 1f, 1f), 62, "Zoom");
            UIKit.At(z.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(55, 0), new Vector2(62, 62));
            zoom = z.rectTransform;

            string msg = Application.isMobilePlatform ? "Twist to rotate · Pinch to zoom" : "Drag / Q E to rotate · Scroll to zoom";
            var p = BottomPill(msg, 30);
            pillText = p.GetComponentInChildren<Text>();
            Fab(104);
        }

        public override void Enter()
        {
            base.Enter();
            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                if (f.lab != null) { f.lab.SetVisible(false); f.lab.gameObject.SetActive(false); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = false; f.rig.main.tag = "Untagged"; }
                if (f.ar != null) f.ar.SetARActive(true);
            }
            else
            {
                if (f.lab != null) { f.lab.gameObject.SetActive(true); f.lab.SetVisible(true); f.lab.SetLook(f.currentEnvironmentLook); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = true; f.rig.main.tag = "MainCamera"; }
                f.rig.Frame(new Vector3(0, 1.62f, 2.45f), new Vector3(0, 0.88f, -0.1f), 46f, 1.8f);
            }
            PlaceModelScreen.PlaceLabels(f);
        }

        public override void Tick()
        {
            base.Tick();
            rot.localEulerAngles = new Vector3(0, 0, -T * 90f);
            float s = 1f + 0.18f * Mathf.Sin(T * 2.6f);
            zoom.localScale = new Vector3(s, s, 1);
            if (!done && f.model.rotatedDegrees > 30f && Mathf.Abs(f.model.zoomedRatio - 1f) > 0.08f)
            {
                done = true; pillText.text = "Great — you've got it";
            }
            if (done) { doneT += Time.deltaTime; if (doneT > 1.4f) f.Next(); }
        }
    }
}
