using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MuscleAR
{
    public class MainUIController : MonoBehaviour
    {
        public Canvas mainCanvas;
        public CanvasScaler scaler;
        public GraphicRaycaster raycaster;
        public GameObject splashscreenpannel;
        public GameObject homescreenpannel;
        public GameObject onboardingpannel;
        public GameObject choosemodepannel;
        public int selectedMode = 0; // 0 = AR Explorer, 1 = Quiz

        [Header("Mode Option Highlight Elements")]
        public Image arExplorerRing;
        public Image quizRing;

        public static MainUIController Instance { get; private set; }
        RectTransform homeMatte;
        Vector2Int homeResolution;
        Rect homeSafeArea, homeCanvasRect;
        bool homeLayoutValid;

        /// <summary>Uniformly fit the existing 1080x1920 composition; never scale its axes independently.</summary>
        public static Rect CalculateHomeFrame(Rect available, float margin = 24f)
        {
            float width = Mathf.Max(1f, available.width - margin * 2f);
            float height = Mathf.Max(1f, available.height - margin * 2f);
            float fit = Mathf.Min(1f, width / 1080f, height / 1920f);
            Vector2 size = new Vector2(1080f, 1920f) * fit;
            return new Rect(available.center - size * 0.5f, size);
        }

        void LayoutHome(bool force = false)
        {
            if (homescreenpannel == null || !homescreenpannel.activeInHierarchy) return;
            var panel = homescreenpannel.GetComponent<RectTransform>();
            var parent = panel.parent as RectTransform;
            if (parent == null || mainCanvas == null) return;
            var resolution = new Vector2Int(Screen.width, Screen.height);
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f || resolution.x <= 0 || resolution.y <= 0) return;
            if (!force && homeLayoutValid && resolution == homeResolution && safe == homeSafeArea && parent.rect == homeCanvasRect) return;
            Canvas.ForceUpdateCanvases();
            Camera camera = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, safe.min, camera, out Vector2 min) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, safe.max, camera, out Vector2 max)) return;
            Rect frame = CalculateHomeFrame(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1080, 1920);
            panel.anchoredPosition = frame.center - parent.rect.center;
            float fit = frame.width / 1080f;
            panel.localScale = new Vector3(fit, fit, 1f);

            // Preserve the existing direct-child paths used by navigation and button bindings.
            var background = panel.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.preserveAspect = true; background.raycastTarget = false;
                var rect = background.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            var button = panel.Find("GetStartedButton") as RectTransform;
            if (button != null)
            {
                button.anchorMin = button.anchorMax = new Vector2(0.5f, 0f);
                button.pivot = new Vector2(0.5f, 0.5f);
                button.sizeDelta = new Vector2(880f, 140f);
                button.anchoredPosition = new Vector2(0f, 160f);
                button.localScale = Vector3.one;
            }
            if (background != null && button != null)
            {
                // Reserve the button's existing bounds plus a proportional gap; only the
                // image's available rectangle changes. preserveAspect fits the full artwork.
                var corners = new Vector3[4];
                button.GetWorldCorners(corners);
                float buttonTop = float.NegativeInfinity;
                foreach (Vector3 corner in corners)
                    buttonTop = Mathf.Max(buttonTop, panel.InverseTransformPoint(corner).y);
                float gap = Mathf.Max(24f, panel.rect.height * 0.015f);
                background.rectTransform.offsetMin = new Vector2(0f,
                    Mathf.Clamp(buttonTop + gap - panel.rect.yMin, 0f, panel.rect.height - 1f));
            }
            if (homeMatte == null)
            {
                var matte = new GameObject("HomeLetterbox", typeof(RectTransform), typeof(Image));
                homeMatte = matte.GetComponent<RectTransform>();
                homeMatte.SetParent(panel, false); homeMatte.SetAsFirstSibling();
                var image = matte.GetComponent<Image>(); image.color = Color.black; image.raycastTarget = false;
            }
            homeMatte.anchorMin = homeMatte.anchorMax = homeMatte.pivot = new Vector2(0.5f, 0.5f);
            homeMatte.anchoredPosition = (parent.rect.center - frame.center) / fit;
            homeMatte.sizeDelta = parent.rect.size / fit;
            homeMatte.localScale = Vector3.one;
            homeResolution = resolution; homeSafeArea = safe; homeCanvasRect = parent.rect; homeLayoutValid = true;
        }


        void LateUpdate() { LayoutHome(); }

        void Awake()
        {
            Instance = this;
            if (mainCanvas == null) mainCanvas = GetComponent<Canvas>();
            if (scaler == null) scaler = GetComponent<CanvasScaler>();
            if (raycaster == null) raycaster = GetComponent<GraphicRaycaster>();

            if (splashscreenpannel == null) splashscreenpannel = transform.Find("splashscreenpannel")?.gameObject;
            if (homescreenpannel == null) homescreenpannel = transform.Find("homescreenpannel")?.gameObject;
            if (onboardingpannel == null) onboardingpannel = transform.Find("onboardingpannel")?.gameObject;
            if (choosemodepannel == null) choosemodepannel = transform.Find("choosemodepannel")?.gameObject;

            if (choosemodepannel != null)
            {
                EnsureChooseModeBackground();
                if (arExplorerRing == null)
                    arExplorerRing = choosemodepannel.transform.Find("Option_ARExplorer/Ring")?.GetComponent<Image>();
                if (quizRing == null)
                    quizRing = choosemodepannel.transform.Find("Option_Quiz/Ring")?.GetComponent<Image>();
            }

            // Bind button click listeners
            if (homescreenpannel != null)
            {
                var btn = homescreenpannel.transform.Find("GetStartedButton")?.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveListener(OnGetStartedClicked);
                    btn.onClick.AddListener(OnGetStartedClicked);
                    MuscleAudioController.AttachClickSound(btn);
                }
            }

            if (onboardingpannel != null)
            {
                var btn = onboardingpannel.transform.Find("ContinueButton")?.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveListener(OnContinueClicked);
                    btn.onClick.AddListener(OnContinueClicked);
                    MuscleAudioController.AttachClickSound(btn);
                }
            }

            if (choosemodepannel != null)
            {
                var btnExp = choosemodepannel.transform.Find("Option_ARExplorer")?.GetComponent<Button>();
                if (btnExp != null)
                {
                    btnExp.onClick.RemoveListener(SelectARExplorer);
                    btnExp.onClick.AddListener(SelectARExplorer);
                    MuscleAudioController.AttachClickSound(btnExp);
                }

                var btnQuiz = choosemodepannel.transform.Find("Option_Quiz")?.GetComponent<Button>();
                if (btnQuiz != null)
                {
                    btnQuiz.onClick.RemoveListener(SelectQuiz);
                    btnQuiz.onClick.AddListener(SelectQuiz);
                    MuscleAudioController.AttachClickSound(btnQuiz);
                }

                var btnStart = choosemodepannel.transform.Find("StartExploringButton")?.GetComponent<Button>();
                if (btnStart != null)
                {
                    btnStart.onClick.RemoveListener(OnStartExploringClicked);
                    btnStart.onClick.AddListener(OnStartExploringClicked);
                    MuscleAudioController.AttachClickSound(btnStart);
                }
            }
        }

        void Start()
        {
            // Initial panel: if splashscreen is assigned and enabled, show splash briefly then homescreen
            if (splashscreenpannel != null && splashscreenpannel.activeSelf)
            {
                StartCoroutine(SplashRoutine());
            }
            else
            {
                ShowPanel(homescreenpannel);
            }
            UpdateModeVisuals();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleAndroidBack();
            }
        }

        public void HandleAndroidBack()
        {
            if (choosemodepannel != null && choosemodepannel.activeSelf)
            {
                ShowPanel(onboardingpannel);
            }
            else if (onboardingpannel != null && onboardingpannel.activeSelf)
            {
                ShowPanel(homescreenpannel);
            }
            else if (homescreenpannel != null && homescreenpannel.activeSelf)
            {
                Application.Quit();
            }
        }

        IEnumerator SplashRoutine()
        {
            ShowPanel(splashscreenpannel);
            yield return new WaitForSeconds(1.8f);
            ShowPanel(homescreenpannel);
        }

        public void ShowPanel(GameObject target)
        {
            if (splashscreenpannel) splashscreenpannel.SetActive(splashscreenpannel == target);
            if (homescreenpannel) homescreenpannel.SetActive(homescreenpannel == target);
            if (onboardingpannel) onboardingpannel.SetActive(onboardingpannel == target);
            if (choosemodepannel) choosemodepannel.SetActive(choosemodepannel == target);
            if (target == homescreenpannel) LayoutHome(true);
        }

        public void ShowChooseMode()
        {
            EnsureChooseModeBackground();
            gameObject.SetActive(true);
            if (mainCanvas != null)
            {
                mainCanvas.gameObject.SetActive(true);
                mainCanvas.enabled = true;
            }
            ShowPanel(choosemodepannel);
            UpdateModeVisuals();
        }

        void EnsureChooseModeBackground()
        {
            if (choosemodepannel != null)
            {
                var bgImg = choosemodepannel.transform.Find("Background")?.GetComponent<Image>();
                if (bgImg != null && bgImg.sprite == null)
                {
                    var sp = Resources.Load<Sprite>("UI/ChooseMode");
                    if (sp != null)
                    {
                        bgImg.sprite = sp;
                        bgImg.color = Color.white;
                    }
                }
            }
        }

        public void OnGetStartedClicked()
        {
            ShowPanel(onboardingpannel);
        }

        public void OnContinueClicked()
        {
            ShowPanel(choosemodepannel);
        }

        public void SelectARExplorer()
        {
            selectedMode = 0;
            UpdateModeVisuals();
        }

        public void SelectQuiz()
        {
            selectedMode = 1;
            UpdateModeVisuals();
        }

        public void UpdateModeVisuals()
        {
            if (arExplorerRing != null)
            {
                arExplorerRing.color = selectedMode == 0
                    ? new Color(0.35f, 0.65f, 1f, 1f)
                    : new Color(0.2f, 0.35f, 0.55f, 0.45f);
            }
            if (quizRing != null)
            {
                quizRing.color = selectedMode == 1
                    ? new Color(0.35f, 0.65f, 1f, 1f)
                    : new Color(0.2f, 0.35f, 0.55f, 0.45f);
            }

            var opt1Img = choosemodepannel != null ? choosemodepannel.transform.Find("Option_ARExplorer")?.GetComponent<Image>() : null;
            if (opt1Img != null)
                opt1Img.color = selectedMode == 0 ? new Color(0.05f, 0.42f, 1f, 1f) : new Color(0.06f, 0.10f, 0.18f, 0.92f);

            var opt2Img = choosemodepannel != null ? choosemodepannel.transform.Find("Option_Quiz")?.GetComponent<Image>() : null;
            if (opt2Img != null)
                opt2Img.color = selectedMode == 1 ? new Color(0.05f, 0.42f, 1f, 1f) : new Color(0.06f, 0.10f, 0.18f, 0.92f);
        }

        public void OnStartExploringClicked()
        {
            // Hide hierarchy entry canvas
            if (mainCanvas != null)
            {
                mainCanvas.enabled = false;
                mainCanvas.gameObject.SetActive(false);
            }
            gameObject.SetActive(false);

            // Hand over control to AppFlow
            if (AppFlow.I != null)
            {
                if (selectedMode == 1)
                {
                    AppFlow.I.BeginDirectQuiz();
                }
                else
                {
                    AppFlow.I.quizFromMenu = false;
                    AppFlow.I.mode = LearnMode.ARExplorer;
                    AppFlow.I.SwitchViewingMode(AppFlow.ViewingMode.CameraAR);
                    AppFlow.I.GoNumber(4); // Direct to Detect Surface / AR
                }
            }
        }
    }
}
