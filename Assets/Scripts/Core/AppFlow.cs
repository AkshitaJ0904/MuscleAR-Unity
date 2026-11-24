using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MuscleAR
{
    public enum LearnMode { ARExplorer, Learn, Quiz }

    /// <summary>
    /// Builds the whole experience at runtime (lab, anatomy, AR camera, UI) and drives the 15-screen flow.
    /// Starts automatically when you press Play in any scene.
    /// </summary>
    public class AppFlow : MonoBehaviour
    {
        public enum ViewingMode { CameraAR, Environment }
        public static AppFlow I;
        public const float W = 390f, H = 844f;

        public CameraRig rig; public LabEnvironment lab; public HumanBody body; public ModelController model;
        public HoloLabels labels; public SurfaceScanner scanner; public PassthroughFeed feed; public InputRouter input;
        public ARManager ar;
        public MuscleAudioController audioCtrl;
        public Canvas canvas; public CanvasScaler scaler; public RectTransform phone, dev;
        public List<ScreenBase> screens = new List<ScreenBase>();
        public int current = -1;
        public LearnMode mode = LearnMode.ARExplorer;
        [System.NonSerialized] public bool quizFromMenu;

        public void BeginDirectQuiz()
        {
            mode = LearnMode.Quiz; quizFromMenu = true;
            GoNumber(17);
        }

        public void BeginExplorerQuiz()
        {
            quizFromMenu = false;
            GoNumber(16);
        }
        public ViewingMode viewingMode = ViewingMode.CameraAR;
        public LabEnvironment.Look currentEnvironmentLook = LabEnvironment.Look.Lab;
        public MuscleId? selected;
        public readonly HashSet<MuscleId> explored = new HashSet<MuscleId>();
        public bool placed;
        public float topInset = 0f;
        public float bottomInset = 0f;

        // Persistent Mode Switcher & Context Action UI
        RectTransform modeBarRoot;
        Image btnCameraARBg, btnCameraARRing;
        Text btnCameraARTxt;
        Image btnEnvBg, btnEnvRing;
        Text btnEnvTxt;
        Button btnMainMenu;

        RectTransform arContextGroup;
        Button btnResetRescan;

        RectTransform envContextGroup;
        Image btnLookLabBg, btnLookLabRing;
        Text btnLookLabTxt;
        Image btnLookDayBg, btnLookDayRing;
        Text btnLookDayTxt;

        Text devHint; bool devVisible; bool devAuto = true;
        Vector2Int lastRes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<AppFlow>() == null) new GameObject("MuscleAR").AddComponent<AppFlow>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.antiAliasing = Application.isMobilePlatform ? 0 : 2;
            QualitySettings.shadowDistance = 12f;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            UIKit.Init();

            Debug.developerConsoleVisible = false;

            // take over any default camera / light / event system in the open scene
            foreach (var c in FindObjectsOfType<Camera>()) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsOfType<Light>()) l.gameObject.SetActive(false);
            foreach (var e in FindObjectsOfType<EventSystem>()) e.gameObject.SetActive(false);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            var worldRoot = new GameObject("World").transform;
            worldRoot.SetParent(transform, false);
            rig = CameraRig.Create(worldRoot);
            lab = LabEnvironment.Create(worldRoot);
            body = HumanBodyBuilder.Build();
            body.name = "HumanBody";
            model = ModelController.Create(worldRoot, body, rig.main);
            labels = HoloLabels.Create(worldRoot, rig.main);
            labels.OnMuscleClicked = OpenMuscleInformation;
            scanner = SurfaceScanner.Create(worldRoot);
            ar = ARManager.Create(worldRoot);
            feed = PassthroughFeed.Create(transform, rig.bg);
            input = gameObject.AddComponent<InputRouter>();
            model.Bind(input);
            input.OnManipulationChanged += labels.SetGestureActive;
            labels.TrackModel(model.root);
            input.OnTap += p =>
            {
                if (current < 0) return;
                if (UsesAnatomyLabels(screens[current].number) && model.Pick(p, out var id) && MuscleDB.IsTarget(id))
                    OpenMuscleInformation(id);
                else screens[current].OnTap(p);
            };
            model.Show(false, false);
            audioCtrl = MuscleAudioController.Ensure(gameObject);

            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

            BuildCanvas();
            UpdateViewport(true);
            BuildScreens();
            BuildModeSwitcherBar();
            BuildDevPanel();
            if (!Application.isEditor)
            {
                var mainUI = Object.FindAnyObjectByType<MainUIController>();
                if (mainUI != null && mainUI.gameObject.activeInHierarchy)
                {
                    mainUI.ShowPanel(mainUI.homescreenpannel);
                }
                else
                {
                    // Default startup mode: REAL AR on Android device
                    SwitchViewingMode(ViewingMode.CameraAR);
                    GoNumber(4); // Start directly on Screen 4: Detect Surface
                }
            }
            else
            {
                rig.Snap(new Vector3(1.5f, 1.8f, 2.4f), new Vector3(0, 0.9f, 0), 42f);
                SwitchViewingMode(ViewingMode.Environment);
                Go(0);
            }

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-qatour") >= 0)
                gameObject.AddComponent<QATour>();
        }

        // ---------------- canvas / viewport ----------------
        void BuildCanvas()
        {
            var cgo = new GameObject("UI");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            cgo.AddComponent<GraphicRaycaster>();
            phone = UIKit.NewRect("Phone", cgo.transform);
            phone.anchorMin = phone.anchorMax = new Vector2(0.5f, 0.5f); phone.pivot = new Vector2(0.5f, 0.5f);
            phone.sizeDelta = new Vector2(W, H);
            dev = UIKit.NewRect("DevPanel", cgo.transform);
            UIKit.Fill(dev);
        }

        void UpdateViewport(bool force = false)
        {
            var res = new Vector2Int(Screen.width, Screen.height);
            if (!force && res == lastRes) return;
            lastRes = res;
            float sw = res.x, sh = res.y;
            float target = W / H;
            Rect r;
            float scale;
            if (Application.isMobilePlatform)
            {
                scale = Mathf.Min(sh / H, sw / W);
                r = new Rect(0, 0, 1, 1);
                phone.sizeDelta = new Vector2(sw / scale, sh / scale);
                var sa = Screen.safeArea;
                topInset = (sh - sa.yMax) / scale;
                bottomInset = sa.yMin / scale;
            }
            else
            {
                float pw, ph;
                if (sw / sh > target) { ph = sh; pw = sh * target; } else { pw = sw; ph = sw / target; }
                r = new Rect((sw - pw) * 0.5f / sw, (sh - ph) * 0.5f / sh, pw / sw, ph / sh);
                scale = ph / H;
                phone.sizeDelta = new Vector2(W, H);
                topInset = 0f;
                bottomInset = 0f;
            }
            rig.phone = r;
            scaler.scaleFactor = Mathf.Max(0.1f, scale);
            phone.anchoredPosition = Vector2.zero;
            if (phone.sizeDelta.x > 0)
            {
                // centre inside the canvas
                phone.anchoredPosition = Vector2.zero;
            }
            if (modeBarRoot != null)
            {
                modeBarRoot.anchorMin = modeBarRoot.anchorMax = new Vector2(0.5f, 1f);
                modeBarRoot.pivot = new Vector2(0.5f, 1f);
                modeBarRoot.anchoredPosition = new Vector2(0, -(topInset + 44));
            }
            if (screens != null)
            {
                for (int i = 0; i < screens.Count; i++)
                {
                    screens[i]?.UpdateSafeAreaLayout();
                }
            }
            // wide windows have black margins: show the storyboard sidebar there
            devAuto = r.x * sw > 230f;
            if (devAuto) devVisible = true;
            if (dev != null) dev.gameObject.SetActive(devVisible);
        }

        // ---------------- screens ----------------
        void BuildScreens()
        {
            screens.Add(new HomeScreen(this));
            screens.Add(new OnboardingScreen(this));
            screens.Add(new ChooseModeScreen(this));
            screens.Add(new DetectSurfaceScreen(this));
            screens.Add(new PlaceModelScreen(this));
            screens.Add(new ModelPlacedScreen(this));
            screens.Add(new MoveModelScreen(this));
            screens.Add(new RotateZoomScreen(this));
            screens.Add(new SelectPartScreen(this));
            screens.Add(new EducationalToastScreen(this));
            screens.Add(new MuscleActionScreen(this));
            screens.Add(new ExploreAnotherScreen(this));
            screens.Add(new LearningCompleteScreen(this));
            screens.Add(new QuizScreen(this));
            screens.Add(new QuizMuscleSelectionScreen(this));
            foreach (var s in screens) { s.Build(); s.SetShown(false, true); }
        }

        public int IndexOf(int number)
        {
            for (int i = 0; i < screens.Count; i++) if (screens[i].number == number) return i;
            return 0;
        }

        public void Go(int index)
        {
            index = Mathf.Clamp(index, 0, screens.Count - 1);
            for (int i = 0; i < screens.Count; i++)
            {
                if (i == index) continue;
                if (i == current) screens[i].Exit();
                screens[i].SetShown(false, true);
            }
            current = index;
            // Keep the same six panels/anchors across selection, information and action screens.
            if (labels != null && !UsesAnatomyLabels(screens[current].number)) labels.Clear();
            if (body != null)
            {
                body.SetHint(null);
                if (selected.HasValue) body.Select(selected.Value);
            }
            if (screens != null && current >= 0 && current < screens.Count)
            {
                screens[current].Enter();
                screens[current].SetShown(true, true);
            }
            RefreshDev();
            UpdateModeSwitcherUI();
        }


        public void GoNumber(int number) { Go(IndexOf(number)); }

        public static bool UsesAnatomyLabels(int number)
        { return number == 9 || number == 10 || number == 13 || number == 14 || number == 15; }

        /// <summary>Shared by label buttons and direct 3D taps; reuses the information screen.</summary>
        public void OpenMuscleInformation(MuscleId id)
        {
            if (!MuscleDB.IsTarget(id)) return;
            if (audioCtrl != null) audioCtrl.PlayClick();
            selected = id; MarkExplored(id);
            if (body != null) { body.SetHint(null); body.Select(id); }
            if (current >= 0 && screens[current] is EducationalToastScreen info)
                info.ShowMuscle(id, playNarration: true);
            else GoNumber(10);
        }

        public void Next()
        {
            int n = screens[current].number;
            if (n == 16 || n == 17) return; // Quiz buttons own attempt/selection progression.
            if (n == 3)
            {
                if (mode == LearnMode.Quiz) { BeginDirectQuiz(); return; }
                if (mode == LearnMode.Learn) { GoNumber(9); return; }
                GoNumber(4); return; // ARExplorer -> Screen 4 (Scan)
            }
            if (n == 6 && mode == LearnMode.Quiz) { BeginDirectQuiz(); return; }
            if (n == 6) { GoNumber(9); return; } // Model placed -> go to Select Muscle!
            if (n == 9) { GoNumber(10); return; } // Select Muscle -> Muscle Information Screen!
            if (n == 10) { GoNumber(13); return; } // Information -> See It in Action!
            if (n == 13) { BeginExplorerQuiz(); return; } // Action -> Take Quiz!
            if (n == 14) { GoNumber(15); return; }
            if (n == 15) { GoNumber(9); return; } // After summary, explore more muscles
            if (n == 16) { GoNumber(15); return; }
            GoNumber(Mathf.Min(n + 1, 15));
        }

        public void Back()
        {
            int n = screens[current].number;
            if (n == 17) { quizFromMenu = false; ReturnToMainMenu("Back from quiz muscle selection"); return; }
            if (n == 16 && screens[current] is QuizScreen quiz) { quiz.RequestBack(); return; }
            if (n == 15) { ReturnToMainMenu("Back from Summary"); return; }
            if (n == 14) { GoNumber(10); return; }
            if (n == 13) { GoNumber(10); return; } // From Action back to Overview
            if (n == 10) { GoNumber(9); return; }  // From Overview back to Select Part
            if (n == 9)  { ReturnToMainMenu("Back from Select Part"); return; }
            if (n == 8)  { GoNumber(9); return; }
            if (n == 7)  { GoNumber(9); return; }
            if (n == 6)  { ReturnToMainMenu("Back from Model Placed"); return; }
            if (n == 5)  { GoNumber(4); return; }
            if (n == 4)  { ReturnToMainMenu("Back from Scan"); return; }
            if (n == 3)  { GoNumber(1); return; }
            GoNumber(Mathf.Max(1, n - 1));
        }

        // ---------------- helpers shared by screens ----------------
        public Vector3 MuscleCenter(MuscleId id, int side = 1) { return body.MuscleCenter(id, side); }

        public void FocusMuscle(MuscleId id, Vector3 dir, float dist, float fov = 40f, float yOff = 0f)
        {
            Vector3 c = MuscleCenter(id, 1);
            rig.Frame(c + dir.normalized * dist + Vector3.up * yOff, c, fov);
        }

        public void MarkExplored(MuscleId id) { explored.Add(id); }

        public string SideName(int s) { return s > 0 ? "LEFT" : "RIGHT"; }

        // ---------------- dev sidebar ("storyboard") ----------------
        void BuildDevPanel()
        {
            var bg = UIKit.Img(dev, UIKit.White, new Color(0.02f, 0.03f, 0.05f, 0.82f), "Bg");
            UIKit.At(bg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(224, 700));
            devHint = UIKit.Label(bg.transform, "", 12, FontStyle.Normal, new Color(1, 1, 1, 0.6f), TextAnchor.UpperLeft, "Hint");
            UIKit.At(devHint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 10), new Vector2(200, 92));
            devHint.text = "← →  prev / next\nF1  hide this panel\nC  live camera on/off\nDrag / scroll / Q E  gestures";
            var title = UIKit.Label(bg.transform, "MUSCLE<color=#60A5FA>AR</color> STORYBOARD", 12, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, "Title");
            UIKit.At(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(200, 20));
            for (int i = 0; i < screens.Count; i++)
            {
                int idx = i;
                var row = UIKit.Img(bg.transform, UIKit.Round, new Color(1, 1, 1, 0.0f), "Row" + i, true, true);
                UIKit.At(row.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -34 - i * 35.5f), new Vector2(208, 32));
                var b = row.gameObject.AddComponent<Button>(); b.targetGraphic = row; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => Go(idx));
                var t = UIKit.Label(row.transform, "", 12, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, "T");
                UIKit.Fill(t.rectTransform); t.rectTransform.offsetMin = new Vector2(10, 0);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                string num = screens[i].number.ToString("00");
                t.text = "<color=#60A5FA>" + num + "</color>  " + screens[i].title;
                row.name = "Row" + i;
            }
        }

        void RefreshDev()
        {
            if (dev == null) return;
            var bg = dev.Find("Bg");
            for (int i = 0; i < screens.Count; i++)
            {
                var row = bg.Find("Row" + i).GetComponent<Image>();
                row.color = i == current ? new Color(0.26f, 0.53f, 0.96f, 0.35f) : new Color(1, 1, 1, 0f);
            }
        }

        void Update()
        {
            UpdateViewport();
            feed.Layout(rig.phone);
            for (int i = 0; i < screens.Count; i++) screens[i].Frame();
            if (current >= 0) screens[current].Tick();

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Space)) Next();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Back();
            if (Input.GetKeyDown(KeyCode.Escape)) HandleAndroidBack();
            if (Input.GetKeyDown(KeyCode.F1)) { devVisible = !devVisible; dev.gameObject.SetActive(devVisible); }
            if (Input.GetKeyDown(KeyCode.C)) ToggleFeed();
            if (Input.GetKeyDown(KeyCode.M)) SwitchViewingMode(viewingMode == ViewingMode.CameraAR ? ViewingMode.Environment : ViewingMode.CameraAR);
            if (Input.GetKeyDown(KeyCode.L)) SetEnvironmentLook(currentEnvironmentLook == LabEnvironment.Look.Lab ? LabEnvironment.Look.Day : LabEnvironment.Look.Lab);
            if (Input.GetKeyDown(KeyCode.R)) ResetAndRescan();
        }

        public void BuildModeSwitcherBar()
        {
            modeBarRoot = UIKit.NewRect("ModeSwitcherBar", phone);
            modeBarRoot.anchorMin = modeBarRoot.anchorMax = new Vector2(0.5f, 1f);
            modeBarRoot.pivot = new Vector2(0.5f, 1f);
            modeBarRoot.sizeDelta = new Vector2(360, 32);
            modeBarRoot.anchoredPosition = new Vector2(0, -(topInset + 44));

            // Item 1 & 2: Segmented Mode Toggle Card [Camera AR | Environment] (176px wide, left-aligned in row)
            var toggleCard = UIKit.Card(modeBarRoot, new Color(0.06f, 0.10f, 0.18f, 0.95f), UIKit.BorderSubtle, "ToggleCard");
            UIKit.At(toggleCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-91, 0), new Vector2(176, 30));

            // Button 1: Camera AR (Left half)
            var btnArGo = UIKit.Img(toggleCard, UIKit.Round, new Color(0.12f, 0.35f, 0.85f, 0.95f), "BtnCameraAR", true, true);
            UIKit.At(btnArGo.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(85, 24));
            btnCameraARRing = UIKit.Img(btnArGo.transform, UIKit.Ring, UIKit.BlueBtn, "Border", true);
            UIKit.Fill(btnCameraARRing.rectTransform);
            btnCameraARBg = btnArGo;
            var btnAr = btnArGo.gameObject.AddComponent<Button>();
            btnAr.targetGraphic = btnArGo;
            btnAr.transition = Selectable.Transition.None;
            btnArGo.gameObject.AddComponent<PressScale>();
            btnAr.onClick.AddListener(() => SwitchViewingMode(ViewingMode.CameraAR, "ModeSwitcherBar: BtnCameraAR"));
            MuscleAudioController.AttachClickSound(btnAr);

            var arIcon = UIKit.Icon(btnArGo.transform, UIKit.ICamera, Color.white, 12, "Icon");
            UIKit.At(arIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(12, 12));
            btnCameraARTxt = UIKit.Label(btnArGo.transform, "Camera AR", 10, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, "Lbl");
            UIKit.Fill(btnCameraARTxt.rectTransform);
            btnCameraARTxt.rectTransform.offsetMin = new Vector2(21, 0);

            // Button 2: Environment (Right half)
            var btnEnvGo = UIKit.Img(toggleCard, UIKit.Round, new Color(0, 0, 0, 0), "BtnEnvironment", true, true);
            UIKit.At(btnEnvGo.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-2, 0), new Vector2(85, 24));
            btnEnvRing = UIKit.Img(btnEnvGo.transform, UIKit.Ring, new Color(0, 0, 0, 0), "Border", true);
            UIKit.Fill(btnEnvRing.rectTransform);
            btnEnvBg = btnEnvGo;
            var btnEnv = btnEnvGo.gameObject.AddComponent<Button>();
            btnEnv.targetGraphic = btnEnvGo;
            btnEnv.transition = Selectable.Transition.None;
            btnEnvGo.gameObject.AddComponent<PressScale>();
            btnEnv.onClick.AddListener(() => SwitchViewingMode(ViewingMode.Environment, "ModeSwitcherBar: BtnEnvironment"));
            MuscleAudioController.AttachClickSound(btnEnv);

            var envIcon = UIKit.Icon(btnEnvGo.transform, UIKit.IARBox, Color.white, 12, "Icon");
            UIKit.At(envIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(12, 12));
            btnEnvTxt = UIKit.Label(btnEnvGo.transform, "Environment", 10, FontStyle.Bold, new Color(0.65f, 0.75f, 0.88f, 0.75f), TextAnchor.MiddleLeft, "Lbl");
            UIKit.Fill(btnEnvTxt.rectTransform);
            btnEnvTxt.rectTransform.offsetMin = new Vector2(20, 0);

            // Item 3: Main Menu Button (Compact 68px card in the middle)
            var menuCard = UIKit.Card(modeBarRoot, new Color(0.08f, 0.14f, 0.25f, 0.95f), UIKit.BorderSubtle, "MainMenuBtn");
            UIKit.At(menuCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(36, 0), new Vector2(68, 30));
            var menuImg = menuCard.GetComponent<Image>();
            if (menuImg != null) menuImg.raycastTarget = true;
            btnMainMenu = menuCard.gameObject.AddComponent<Button>();
            btnMainMenu.targetGraphic = menuImg;
            btnMainMenu.transition = Selectable.Transition.None;
            menuCard.gameObject.AddComponent<PressScale>();
            btnMainMenu.onClick.AddListener(() => ReturnToMainMenu("ModeSwitcherBar: Main Menu"));
            MuscleAudioController.AttachClickSound(btnMainMenu);

            var menuIcon = UIKit.Icon(menuCard.transform, UIKit.IList, UIKit.BlueText, 12, "Icon");
            UIKit.At(menuIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(7, 0), new Vector2(12, 12));
            var menuLbl = UIKit.Label(menuCard.transform, "Menu", 10, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, "Lbl");
            UIKit.Fill(menuLbl.rectTransform);
            menuLbl.rectTransform.offsetMin = new Vector2(22, 0);

            // Item 4: Context Action (Reset & Rescan in AR, Look Toggle in Env - 104px card on the right)
            arContextGroup = UIKit.NewRect("ARContextGroup", modeBarRoot);
            UIKit.At(arContextGroup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(126, 0), new Vector2(104, 30));
            var rescanCard = UIKit.Card(arContextGroup, new Color(0.08f, 0.14f, 0.25f, 0.95f), UIKit.BorderSubtle, "ResetRescanBtn");
            UIKit.Fill(rescanCard);
            var rescanImg = rescanCard.GetComponent<Image>();
            if (rescanImg != null) rescanImg.raycastTarget = true;
            btnResetRescan = rescanCard.gameObject.AddComponent<Button>();
            btnResetRescan.targetGraphic = rescanImg;
            btnResetRescan.transition = Selectable.Transition.None;
            rescanCard.gameObject.AddComponent<PressScale>();
            btnResetRescan.onClick.AddListener(() => ResetAndRescan("ModeSwitcherBar: Reset & Rescan"));
            MuscleAudioController.AttachClickSound(btnResetRescan);

            var rescanIcon = UIKit.Icon(rescanCard.transform, UIKit.IRotate, UIKit.BlueText, 12, "Icon");
            UIKit.At(rescanIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(12, 12));
            var rescanLbl = UIKit.Label(rescanCard.transform, "Reset & Rescan", 9, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, "Lbl");
            UIKit.Fill(rescanLbl.rectTransform);
            rescanLbl.rectTransform.offsetMin = new Vector2(20, 0);

            // Item 4 Alternative: Environment Context Action (Look: Lab vs Day)
            envContextGroup = UIKit.NewRect("EnvContextGroup", modeBarRoot);
            UIKit.At(envContextGroup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(126, 0), new Vector2(104, 30));
            var lookCard = UIKit.Card(envContextGroup, new Color(0.06f, 0.10f, 0.18f, 0.95f), UIKit.BorderSubtle, "LookCard");
            UIKit.Fill(lookCard);

            // Look: Lab
            var labGo = UIKit.Img(lookCard, UIKit.Round, new Color(0.12f, 0.35f, 0.85f, 0.95f), "LookLab", true, true);
            UIKit.At(labGo.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(49, 24));
            btnLookLabRing = UIKit.Img(labGo.transform, UIKit.Ring, UIKit.BlueBtn, "Border", true);
            UIKit.Fill(btnLookLabRing.rectTransform);
            btnLookLabBg = labGo;
            var bLab = labGo.gameObject.AddComponent<Button>();
            bLab.targetGraphic = labGo;
            bLab.transition = Selectable.Transition.None;
            labGo.gameObject.AddComponent<PressScale>();
            bLab.onClick.AddListener(() => SetEnvironmentLook(LabEnvironment.Look.Lab));
            MuscleAudioController.AttachClickSound(bLab);
            btnLookLabTxt = UIKit.Label(labGo.transform, "🌙 Lab", 9, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Lbl");
            UIKit.Fill(btnLookLabTxt.rectTransform);

            // Look: Day
            var dayGo = UIKit.Img(lookCard, UIKit.Round, new Color(0, 0, 0, 0), "LookDay", true, true);
            UIKit.At(dayGo.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-2, 0), new Vector2(49, 24));
            btnLookDayRing = UIKit.Img(dayGo.transform, UIKit.Ring, new Color(0, 0, 0, 0), "Border", true);
            UIKit.Fill(btnLookDayRing.rectTransform);
            btnLookDayBg = dayGo;
            var bDay = dayGo.gameObject.AddComponent<Button>();
            bDay.targetGraphic = dayGo;
            bDay.transition = Selectable.Transition.None;
            dayGo.gameObject.AddComponent<PressScale>();
            bDay.onClick.AddListener(() => SetEnvironmentLook(LabEnvironment.Look.Day));
            MuscleAudioController.AttachClickSound(bDay);
            btnLookDayTxt = UIKit.Label(dayGo.transform, "☀ Day", 9, FontStyle.Bold, new Color(0.65f, 0.75f, 0.88f, 0.75f), TextAnchor.MiddleCenter, "Lbl");
            UIKit.Fill(btnLookDayTxt.rectTransform);

            UpdateModeSwitcherUI();
        }

        public void UpdateModeSwitcherUI()
        {
            if (labels != null && current >= 0 && current < screens.Count)
                labels.SetContext(screens[current].root,modeBarRoot,screens[current].number == 13);
            if (modeBarRoot == null) return;

            bool showBar = current >= 0 && screens[current].number >= 4 &&
                !(quizFromMenu && (screens[current].number == 16 || screens[current].number == 17));
            modeBarRoot.gameObject.SetActive(showBar);
            if (!showBar) return;

            modeBarRoot.transform.SetAsLastSibling();

            bool isAr = viewingMode == ViewingMode.CameraAR;

            btnCameraARBg.color = isAr ? new Color(0.12f, 0.35f, 0.85f, 0.95f) : new Color(0, 0, 0, 0);
            btnCameraARRing.color = isAr ? UIKit.BlueBtn : new Color(0, 0, 0, 0);
            btnCameraARTxt.color = isAr ? Color.white : new Color(0.65f, 0.75f, 0.88f, 0.75f);

            btnEnvBg.color = !isAr ? new Color(0.12f, 0.35f, 0.85f, 0.95f) : new Color(0, 0, 0, 0);
            btnEnvRing.color = !isAr ? UIKit.BlueBtn : new Color(0, 0, 0, 0);
            btnEnvTxt.color = !isAr ? Color.white : new Color(0.65f, 0.75f, 0.88f, 0.75f);

            if (arContextGroup != null) arContextGroup.gameObject.SetActive(isAr);
            if (envContextGroup != null) envContextGroup.gameObject.SetActive(!isAr);

            if (!isAr)
            {
                bool isLab = currentEnvironmentLook == LabEnvironment.Look.Lab;
                btnLookLabBg.color = isLab ? new Color(0.12f, 0.35f, 0.85f, 0.95f) : new Color(0, 0, 0, 0);
                btnLookLabRing.color = isLab ? UIKit.BlueBtn : new Color(0, 0, 0, 0);
                btnLookLabTxt.color = isLab ? Color.white : new Color(0.65f, 0.75f, 0.88f, 0.75f);

                btnLookDayBg.color = !isLab ? new Color(0.12f, 0.35f, 0.85f, 0.95f) : new Color(0, 0, 0, 0);
                btnLookDayRing.color = !isLab ? UIKit.BlueBtn : new Color(0, 0, 0, 0);
                btnLookDayTxt.color = !isLab ? Color.white : new Color(0.65f, 0.75f, 0.88f, 0.75f);
            }
        }

        public void SwitchViewingMode(ViewingMode newMode, string caller = "User Action")
        {
            ViewingMode oldMode = viewingMode;
            Debug.Log($"[MuscleAR-Mode] Transition requested: {oldMode} -> {newMode}. Caller/Reason: '{caller}'. Current screen: {(current >= 0 ? screens[current].number : -1)}");
            viewingMode = newMode;
            if (audioCtrl != null) audioCtrl.PlayClick();

            if (newMode == ViewingMode.Environment)
            {
                // 0. Cancel active repositioning and stop dragging immediately
                if (model != null)
                {
                    model.CancelReposition();
                }

                // 1. Safely deactivate AR Foundation tracking & plane visuals
                if (ar != null)
                {
                    ar.SetPlanesActive(false);
                    ar.SetARActive(false);
                }

                // 2. Activate Virtual Lab environment and apply preserved look
                if (lab != null)
                {
                    lab.AlignToPlacement(model);
                    lab.gameObject.SetActive(true);
                    lab.SetVisible(true);
                    lab.SetLook(currentEnvironmentLook, instant: true);
                }

                // 3. Ensure virtual camera rig is active & framed
                if (rig != null && rig.main != null)
                {
                    rig.main.enabled = true;
                    rig.main.tag = "MainCamera";
                    rig.SetPassthrough(false);
                    if (lab != null) lab.FrameEnvironment(rig);
                }

                // 4. Position anatomy model stably on virtual table
                if (model != null)
                {
                    model.cam = rig != null ? rig.main : Camera.main;
                    model.Show(true, false);
                    model.ShowDisc(true);
                    model.UpdateDragTargetState();
                }
                if (labels != null && rig != null)
                {
                    labels.cam = rig.main;
                }

                // 5. In Environment mode, there are no surfaces to scan; advance Screen 4 to Screen 6
                if (current >= 0 && screens[current].number == 4)
                {
                    GoNumber(6);
                }
            }
            else // CameraAR Mode
            {
                // 1. Strictly hide virtual lab
                if (lab != null)
                {
                    lab.SetVisible(false);
                    lab.gameObject.SetActive(false);
                }

                // 2. Disable virtual rig main camera
                if (rig != null && rig.main != null)
                {
                    rig.main.enabled = false;
                    rig.main.tag = "Untagged";
                }

                // 3. Activate AR Foundation & plane detection
                if (ar != null)
                {
                    ar.SetARActive(true);
                    ar.SetPlanesActive(true);
                }

                // 4. Update cameras to AR camera
                if (model != null && ar != null)
                {
                    model.cam = ar.arCamera;
                }
                if (labels != null && ar != null)
                {
                    labels.cam = ar.arCamera;
                }

                // 5. Reset model state so the user scans real surfaces and places the model
                if (model != null && !placed)
                {
                    model.CancelReposition();
                    model.ClearAnchor();
                    model.ResetTransform();
                    model.Show(false, false);
                    model.ShowDisc(false);
                    model.UpdateDragTargetState();
                }
                // 6. Navigate to Screen 4 (Detect Surface)
                GoNumber(placed ? 6 : 4);
            }

            UpdateModeSwitcherUI();
            Debug.Log($"[MuscleAR-Mode] Transition completed: {oldMode} -> {viewingMode}. Active screen: {(current >= 0 ? screens[current].number : -1)}");
        }

        public void SetEnvironmentLook(LabEnvironment.Look look)
        {
            currentEnvironmentLook = look;
            if (audioCtrl != null) audioCtrl.PlayClick();
            if (lab != null)
            {
                lab.SetLook(look, instant: false);
            }
            UpdateModeSwitcherUI();
            Debug.Log("[MuscleAR] Environment look set to: " + look);
        }

        public void ToggleFeed()
        {
            SwitchViewingMode(viewingMode == ViewingMode.CameraAR ? ViewingMode.Environment : ViewingMode.CameraAR);
        }

        /// <summary>
        /// Anchors the model if it is not anchored yet. In AR the model is ALWAYS placed from the
        /// viewport centre (tap position is only a trigger). Returns false, and spawns nothing,
        /// while there is no valid tracked horizontal surface at the centre.
        /// </summary>
        public bool EnsureModelAnchoredInAR()
        {
            // Screen entry, muscle selection and action playback reuse the existing placement.
            if (model != null && model.useWorldAnchor)
            {
                model.Show(true, false);
                return true;
            }
            if (ar != null && ar.IsARActive)
            {
                if (!ar.TryRaycastViewportCentre(out Pose hitPose)) return false;
                Vector3 camPos = ar.arCamera != null ? ar.arCamera.transform.position : Vector3.zero;
                if (model != null) model.SetAnchor(hitPose.position, hitPose.rotation, camPos);
                placed = true;
                if (model != null)
                {
                    model.Show(true, false);
                    model.ShowDisc(true);
                    model.UpdateDragTargetState();
                }
            }
            else
            {
                placed = true;
                if (model != null)
                {
                    model.Show(true, false);
                    model.ShowDisc(true);
                    model.UpdateDragTargetState();
                }
            }
            return true;
        }

        /// <summary>
        /// Clears the currently placed anatomy model and anchor, stops active drag sessions,
        /// clears muscle selection and highlights, restores plane detection/visualization,
        /// and returns to Screen 04 (Detect Surface) so the user can scan and place a new model.
        /// </summary>
        public void ResetAndRescan(string caller = "User Action")
        {
            ViewingMode modeBefore = viewingMode;
            Debug.Log($"[MuscleAR-Reset] Reset method entered from caller='{caller}'. Current mode before reset: {modeBefore}, active screen: {(current >= 0 ? screens[current].number : -1)}");

            viewingMode = ViewingMode.CameraAR;

            // 1. Cancel any active repositioning and remove the placed model safely
            if (model != null)
            {
                model.CancelReposition();
                model.ClearAnchor();
                model.ResetTransform();
                model.Show(false, false);
                model.ShowDisc(false);
                model.UpdateDragTargetState();
            }
            Debug.Log("[MuscleAR-Reset] Model and anchor cleanup completed.");

            // 2. Clear muscle selection, highlights, callouts, and audio playback
            selected = null;
            if (body != null)
            {
                body.RestoreAllMaterials();
                body.Select(null);
                body.SetHint(null);
                body.ResetAction();
            }
            if (labels != null) labels.Clear();
            if (audioCtrl != null) audioCtrl.Stop();

            // 3. Reset placement state flag
            placed = false;

            // 4. Strictly hide virtual lab and disable virtual camera
            if (lab != null)
            {
                lab.SetVisible(false);
                lab.gameObject.SetActive(false);
            }
            if (rig != null && rig.main != null)
            {
                rig.main.enabled = false;
                rig.main.tag = "Untagged";
            }

            // 5. Ensure AR is active and resume physical surface plane detection and visualization
            if (ar != null)
            {
                ar.SetARActive(true);
                ar.SetPlanesActive(true);
                if (model != null) model.cam = ar.arCamera;
                if (labels != null) labels.cam = ar.arCamera;
            }

            // 6. Stop simulated scanner to ensure only real AR plane scanning runs
            if (scanner != null) scanner.Stop();
            Debug.Log("[MuscleAR-Reset] Scanning restarted in Camera AR mode.");

            // 7. Navigate cleanly back to Detect Surface Screen (Screen 4)
            GoNumber(4);

            // 8. Audio feedback confirmation
            if (audioCtrl != null) audioCtrl.PlayClick();

            UpdateModeSwitcherUI();
            Debug.Log($"[MuscleAR-Reset] Reset & Rescan completed. Active mode: {viewingMode}, Screen: {(current >= 0 ? screens[current].number : -1)}");
        }

        /// <summary>
        /// Professional Back to Main Menu: Safely cleans up active AR / Environment / Quiz / Model states
        /// and navigates directly to the Main Menu (Screen 03: ChooseModeScreen) without restarting the app.
        /// </summary>
        public void ReturnToMainMenu(string caller = "User Action")
        {
            Debug.Log($"[MuscleAR-Nav] ReturnToMainMenu called from '{caller}'. Current screen: {(current >= 0 ? screens[current].number : -1)}, viewingMode: {viewingMode}");

            // 1. Cancel repositioning, clear anchors, and hide the 3D model
            if (model != null)
            {
                model.CancelReposition();
                model.Show(false, false);
                model.ShowDisc(false);
                model.UpdateDragTargetState();
            }

            // 2. Clear muscle selection, restore materials (stops blinking highlight), clear labels & audio
            selected = null;
            if (body != null)
            {
                body.RestoreAllMaterials();
                body.Select(null);
                body.SetHint(null);
                body.ResetAction();
            }
            if (labels != null) labels.Clear();
            if (audioCtrl != null) audioCtrl.Stop();

            // Preserve placement and user transform while the menu hides the model.

            // 4. Safely pause AR plane detection without destroying the ARSession or creating duplicate cameras
            if (ar != null)
            {
                ar.SetPlanesActive(false);
            }
            if (scanner != null) scanner.Stop();

            // 5. Hide virtual lab environment safely
            if (lab != null)
            {
                lab.SetVisible(false);
                lab.gameObject.SetActive(false);
            }

            // 6. Ensure virtual camera Orbit is ready for Main Menu background
            if (rig != null && rig.main != null)
            {
                rig.main.enabled = true;
                rig.main.tag = "MainCamera";
                rig.Orbit(new Vector3(0, 0.9f, 0), 2.7f, 1f, 3f, 40f, 42f);
            }

            // 7. Ensure Camera AR mode is set as default; do NOT open Environment mode by default
            viewingMode = ViewingMode.CameraAR;

            // 8. Audio feedback confirmation
            if (audioCtrl != null) audioCtrl.PlayClick();

            // 9. Navigate directly to Main Menu (MainUIController choosemodepannel)
            var mainUI = MainUIController.Instance;
            if (mainUI == null)
            {
                mainUI = UnityEngine.Object.FindAnyObjectByType<MainUIController>(FindObjectsInactive.Include);
            }

            if (mainUI != null)
            {
                if (current >= 0 && current < screens.Count && screens[current] != null && screens[current].root != null)
                {
                    screens[current].root.gameObject.SetActive(false);
                }
                current = -1;
                mainUI.ShowChooseMode();
                Debug.Log("[MuscleAR-Nav] Successfully returned to Main Menu (MainUIController choosemodepannel).");
            }
            else
            {
                GoNumber(3);
                Debug.Log("[MuscleAR-Nav] Successfully returned to Main Menu (Screen 03).");
            }

            // 10. Hide ModeSwitcherBar on Main Menu
            if (modeBarRoot != null)
            {
                modeBarRoot.gameObject.SetActive(false);
            }
        }

        float lastBackPressTime = -10f;
        public void HandleAndroidBack()
        {
            if (current < 0 || screens.Count == 0) return;
            int n = screens[current].number;
            Debug.Log($"[MuscleAR-Nav] Android Back button pressed on Screen {n}");
            if (n == 16 || n == 17) { Back(); return; }

            // If on Screen 10 (EducationalToastScreen) and the info card is currently open, collapse it first!
            if (screens[current] is EducationalToastScreen toast && toast.IsExpanded)
            {
                toast.CollapsePanel();
                return;
            }

            // From any secondary screen (>= 4), return directly to Main Menu
            if (n >= 4)
            {
                ReturnToMainMenu("Android Back Button");
                return;
            }

            // On Main Menu (Screen 03):
            if (n == 3)
            {
                if (Time.unscaledTime - lastBackPressTime < 2.0f)
                {
                    Application.Quit();
                }
                else
                {
                    lastBackPressTime = Time.unscaledTime;
                    GoNumber(1);
                }
                return;
            }

            // On Onboarding (Screen 02):
            if (n == 2)
            {
                GoNumber(1);
                return;
            }

            // On Home Screen (Screen 01): exit application
            Application.Quit();
        }
    }
}
