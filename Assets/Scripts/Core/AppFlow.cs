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
        public static AppFlow I;
        public const float W = 390f, H = 844f;

        public CameraRig rig; public LabEnvironment lab; public HumanBody body; public ModelController model;
        public HoloLabels labels; public SurfaceScanner scanner; public PassthroughFeed feed; public InputRouter input;
        public Canvas canvas; public CanvasScaler scaler; public RectTransform phone, dev;
        public List<ScreenBase> screens = new List<ScreenBase>();
        public int current = -1;
        public LearnMode mode = LearnMode.ARExplorer;
        public MuscleId? selected;
        public readonly HashSet<MuscleId> explored = new HashSet<MuscleId>();
        public bool placed;
        public float topInset = 0f;

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
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 12f;
            QualitySettings.vSyncCount = 1;
            UIKit.Init();

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
            scanner = SurfaceScanner.Create(worldRoot);
            feed = PassthroughFeed.Create(transform, rig.bg);
            input = gameObject.AddComponent<InputRouter>();
            model.Bind(input);
            input.OnTap += p => { if (current >= 0) screens[current].OnTap(p); };
            model.Show(false, false);

            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

            BuildCanvas();
            BuildScreens();
            BuildDevPanel();
            UpdateViewport(true);
            rig.Snap(new Vector3(1.5f, 1.8f, 2.4f), new Vector3(0, 0.9f, 0), 42f);
            Go(0);

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
            }
            else
            {
                float pw, ph;
                if (sw / sh > target) { ph = sh; pw = sh * target; } else { pw = sw; ph = sw / target; }
                r = new Rect((sw - pw) * 0.5f / sw, (sh - ph) * 0.5f / sh, pw / sw, ph / sh);
                scale = ph / H;
                phone.sizeDelta = new Vector2(W, H);
                topInset = 0f;
            }
            rig.phone = r;
            scaler.scaleFactor = Mathf.Max(0.1f, scale);
            phone.anchoredPosition = Vector2.zero;
            if (phone.sizeDelta.x > 0)
            {
                // centre inside the canvas
                phone.anchoredPosition = Vector2.zero;
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
            screens.Add(new ExploreLayersScreen(this));
            screens.Add(new MuscleDetailsScreen(this));
            screens.Add(new MuscleActionScreen(this));
            screens.Add(new ExploreAnotherScreen(this));
            screens.Add(new LearningCompleteScreen(this));
            screens.Add(new QuizScreen(this));
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
            if (current >= 0) { screens[current].Exit(); screens[current].SetShown(false); }
            current = index;
            labels.Clear();
            body.Select(null); body.SetHint(null);
            screens[current].Enter();
            screens[current].SetShown(true);
            RefreshDev();
        }

        public void GoNumber(int number) { Go(IndexOf(number)); }

        public void Next()
        {
            int n = screens[current].number;
            if (n == 6 && mode == LearnMode.Quiz) { GoNumber(16); return; }
            if (n == 15) { GoNumber(14); return; }
            if (n == 16) { GoNumber(15); return; }
            GoNumber(Mathf.Min(n + 1, 15));
        }

        public void Back()
        {
            int n = screens[current].number;
            if (n == 16) { GoNumber(15); return; }
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
            if (Input.GetKeyDown(KeyCode.F1)) { devVisible = !devVisible; dev.gameObject.SetActive(devVisible); }
            if (Input.GetKeyDown(KeyCode.C)) ToggleFeed();
        }

        public void ToggleFeed()
        {
            if (feed.Active)
            {
                feed.Stop(); rig.SetPassthrough(false); lab.SetVisible(true);
            }
            else if (feed.TryStart())
            {
                rig.SetPassthrough(true); lab.SetVisible(false);
            }
            Debug.Log("[MuscleAR] " + feed.Status);
        }
    }
}
