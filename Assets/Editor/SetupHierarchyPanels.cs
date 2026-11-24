using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;

namespace MuscleAR.EditorScripts
{
    public static class SetupHierarchyPanels
    {
        [MenuItem("MuscleAR/Setup Hierarchy Panels")]
        public static void ExecuteSetup()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MuscleAR.unity");

            // Load Sprites
            var sHomeScreen = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/HomeScreen.png");
            var sOnboarding = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Onboarding.png");
            var sChooseMode = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/ChooseMode.png");
            var sButton = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Rounded blue asset.png");
            var sSplash = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/SplashScreen-Logo.png");

            Font standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (standardFont == null) standardFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (standardFont == null) standardFont = Font.CreateDynamicFontFromOSFont(new[] { "Inter", "Arial", "Roboto" }, 32);

            // Find MainUICanvas
            var canvasGO = GameObject.Find("MainUICanvas");
            if (canvasGO == null)
            {
                Debug.LogError("MainUICanvas not found in scene!");
                return;
            }

            var canvas = canvasGO.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var raycaster = canvasGO.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = canvasGO.AddComponent<GraphicRaycaster>();

            var ctrl = canvasGO.GetComponent<MuscleAR.MainUIController>();
            if (ctrl == null) ctrl = canvasGO.AddComponent<MuscleAR.MainUIController>();
            ctrl.mainCanvas = canvas;
            ctrl.scaler = scaler;
            ctrl.raycaster = raycaster;

            // Find panels
            var tSplash = canvasGO.transform.Find("splashscreenpannel");
            var tHome = canvasGO.transform.Find("homescreenpannel");
            var tOnboarding = canvasGO.transform.Find("onboardingpannel");
            var tChooseMode = canvasGO.transform.Find("choosemodepannel");

            if (tSplash == null || tHome == null || tOnboarding == null || tChooseMode == null)
            {
                Debug.LogError("Could not find all 4 panels under MainUICanvas!");
                return;
            }

            ctrl.splashscreenpannel = tSplash.gameObject;
            ctrl.homescreenpannel = tHome.gameObject;
            ctrl.onboardingpannel = tOnboarding.gameObject;
            ctrl.choosemodepannel = tChooseMode.gameObject;

            void CleanChildren(Transform t)
            {
                while (t.childCount > 0)
                {
                    Object.DestroyImmediate(t.GetChild(0).gameObject);
                }
            }

            // ================= 1. SPLASH SCREEN PANEL =================
            CleanChildren(tSplash);
            if (sSplash != null)
            {
                var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bgGO.transform.SetParent(tSplash, false);
                var rt = bgGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var img = bgGO.GetComponent<Image>();
                img.sprite = sSplash;
                img.preserveAspect = true;
            }

            // ================= 2. HOMESCREEN PANEL =================
            CleanChildren(tHome);
            var homeBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            homeBg.transform.SetParent(tHome, false);
            var hrt = homeBg.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
            var hImg = homeBg.GetComponent<Image>();
            hImg.sprite = sHomeScreen;
            hImg.preserveAspect = true;
            hImg.raycastTarget = false;

            var getStartedGO = new GameObject("GetStartedButton", typeof(RectTransform), typeof(Image), typeof(Button));
            getStartedGO.transform.SetParent(tHome, false);
            var gsrt = getStartedGO.GetComponent<RectTransform>();
            gsrt.anchorMin = new Vector2(0.5f, 0); gsrt.anchorMax = new Vector2(0.5f, 0); gsrt.pivot = new Vector2(0.5f, 0.5f);
            gsrt.anchoredPosition = new Vector2(0, 160);
            gsrt.sizeDelta = new Vector2(880, 140);
            var gsImg = getStartedGO.GetComponent<Image>();
            gsImg.sprite = sButton;
            gsImg.type = Image.Type.Sliced;
            var gsBtn = getStartedGO.GetComponent<Button>();
            gsBtn.targetGraphic = gsImg;
            UnityEventTools.AddPersistentListener(gsBtn.onClick, ctrl.OnGetStartedClicked);

            var gsTextGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            gsTextGO.transform.SetParent(getStartedGO.transform, false);
            var gstrt = gsTextGO.GetComponent<RectTransform>();
            gstrt.anchorMin = Vector2.zero; gstrt.anchorMax = Vector2.one;
            gstrt.offsetMin = Vector2.zero; gstrt.offsetMax = Vector2.zero;
            var gsText = gsTextGO.GetComponent<Text>();
            gsText.font = standardFont;
            gsText.text = "Get Started";
            gsText.fontSize = 44;
            gsText.fontStyle = FontStyle.Bold;
            gsText.color = Color.white;
            gsText.alignment = TextAnchor.MiddleCenter;

            // ================= 3. ONBOARDING PANEL =================
            CleanChildren(tOnboarding);
            var onbBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            onbBg.transform.SetParent(tOnboarding, false);
            var onbrt = onbBg.GetComponent<RectTransform>();
            onbrt.anchorMin = Vector2.zero; onbrt.anchorMax = Vector2.one;
            onbrt.offsetMin = Vector2.zero; onbrt.offsetMax = Vector2.zero;
            var onbImg = onbBg.GetComponent<Image>();
            onbImg.sprite = sOnboarding;
            onbImg.preserveAspect = false;
            onbImg.raycastTarget = false;

            var continueGO = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
            continueGO.transform.SetParent(tOnboarding, false);
            var crt = continueGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0); crt.anchorMax = new Vector2(0.5f, 0); crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0, 160);
            crt.sizeDelta = new Vector2(880, 140);
            var cImg = continueGO.GetComponent<Image>();
            cImg.sprite = sButton;
            cImg.type = Image.Type.Sliced;
            var cBtn = continueGO.GetComponent<Button>();
            cBtn.targetGraphic = cImg;
            UnityEventTools.AddPersistentListener(cBtn.onClick, ctrl.OnContinueClicked);

            var cTextGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            cTextGO.transform.SetParent(continueGO.transform, false);
            var ctrt = cTextGO.GetComponent<RectTransform>();
            ctrt.anchorMin = Vector2.zero; ctrt.anchorMax = Vector2.one;
            ctrt.offsetMin = Vector2.zero; ctrt.offsetMax = Vector2.zero;
            var cText = cTextGO.GetComponent<Text>();
            cText.font = standardFont;
            cText.text = "Continue";
            cText.fontSize = 44;
            cText.fontStyle = FontStyle.Bold;
            cText.color = Color.white;
            cText.alignment = TextAnchor.MiddleCenter;

            // ================= 4. CHOOSE MODE PANEL =================
            CleanChildren(tChooseMode);
            var cmBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            cmBg.transform.SetParent(tChooseMode, false);
            var cmrt = cmBg.GetComponent<RectTransform>();
            cmrt.anchorMin = Vector2.zero; cmrt.anchorMax = Vector2.one;
            cmrt.offsetMin = Vector2.zero; cmrt.offsetMax = Vector2.zero;
            var cmImg = cmBg.GetComponent<Image>();
            cmImg.sprite = sChooseMode;

            // Option 1: AR Explorer
            var opt1GO = new GameObject("Option_ARExplorer", typeof(RectTransform), typeof(Image), typeof(Button));
            opt1GO.transform.SetParent(tChooseMode, false);
            var o1rt = opt1GO.GetComponent<RectTransform>();
            o1rt.anchorMin = new Vector2(0.5f, 0.5f); o1rt.anchorMax = new Vector2(0.5f, 0.5f); o1rt.pivot = new Vector2(0.5f, 0.5f);
            o1rt.anchoredPosition = new Vector2(0, -90);
            o1rt.sizeDelta = new Vector2(880, 140);
            var o1Img = opt1GO.GetComponent<Image>();
            o1Img.color = new Color(0.06f, 0.10f, 0.18f, 0.92f);
            if (sButton != null) { o1Img.sprite = sButton; o1Img.type = Image.Type.Sliced; }
            var o1Btn = opt1GO.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(o1Btn.onClick, ctrl.SelectARExplorer);

            var o1RingGO = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            o1RingGO.transform.SetParent(opt1GO.transform, false);
            var o1rrt = o1RingGO.GetComponent<RectTransform>();
            o1rrt.anchorMin = Vector2.zero; o1rrt.anchorMax = Vector2.one;
            o1rrt.offsetMin = new Vector2(-3, -3); o1rrt.offsetMax = new Vector2(3, 3);
            var o1Ring = o1RingGO.GetComponent<Image>();
            if (sButton != null) { o1Ring.sprite = sButton; o1Ring.type = Image.Type.Sliced; }
            o1Ring.color = new Color(0.35f, 0.65f, 1f, 1f);
            ctrl.arExplorerRing = o1Ring;

            var o1TitleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            o1TitleGO.transform.SetParent(opt1GO.transform, false);
            var o1trt = o1TitleGO.GetComponent<RectTransform>();
            o1trt.anchorMin = new Vector2(0, 0.5f); o1trt.anchorMax = new Vector2(1, 1);
            o1trt.offsetMin = new Vector2(35, 0); o1trt.offsetMax = new Vector2(-20, -12);
            var o1Title = o1TitleGO.GetComponent<Text>();
            o1Title.font = standardFont;
            o1Title.text = "▣  AR Explorer";
            o1Title.fontSize = 40;
            o1Title.fontStyle = FontStyle.Bold;
            o1Title.color = Color.white;
            o1Title.alignment = TextAnchor.MiddleLeft;

            var o1SubGO = new GameObject("Sub", typeof(RectTransform), typeof(Text));
            o1SubGO.transform.SetParent(opt1GO.transform, false);
            var o1srt = o1SubGO.GetComponent<RectTransform>();
            o1srt.anchorMin = new Vector2(0, 0); o1srt.anchorMax = new Vector2(1, 0.5f);
            o1srt.offsetMin = new Vector2(35, 10); o1srt.offsetMax = new Vector2(-20, 0);
            var o1Sub = o1SubGO.GetComponent<Text>();
            o1Sub.font = standardFont;
            o1Sub.text = "Place full-body anatomy in your real space";
            o1Sub.fontSize = 24;
            o1Sub.fontStyle = FontStyle.Normal;
            o1Sub.color = new Color(0.85f, 0.90f, 0.98f, 0.85f);
            o1Sub.alignment = TextAnchor.MiddleLeft;

            // Option 2: Quiz
            var opt2GO = new GameObject("Option_Quiz", typeof(RectTransform), typeof(Image), typeof(Button));
            opt2GO.transform.SetParent(tChooseMode, false);
            var o2rt = opt2GO.GetComponent<RectTransform>();
            o2rt.anchorMin = new Vector2(0.5f, 0.5f); o2rt.anchorMax = new Vector2(0.5f, 0.5f); o2rt.pivot = new Vector2(0.5f, 0.5f);
            o2rt.anchoredPosition = new Vector2(0, -260);
            o2rt.sizeDelta = new Vector2(880, 140);
            var o2Img = opt2GO.GetComponent<Image>();
            o2Img.color = new Color(0.06f, 0.10f, 0.18f, 0.92f);
            if (sButton != null) { o2Img.sprite = sButton; o2Img.type = Image.Type.Sliced; }
            var o2Btn = opt2GO.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(o2Btn.onClick, ctrl.SelectQuiz);

            var o2RingGO = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            o2RingGO.transform.SetParent(opt2GO.transform, false);
            var o2rrt = o2RingGO.GetComponent<RectTransform>();
            o2rrt.anchorMin = Vector2.zero; o2rrt.anchorMax = Vector2.one;
            o2rrt.offsetMin = new Vector2(-3, -3); o2rrt.offsetMax = new Vector2(3, 3);
            var o2Ring = o2RingGO.GetComponent<Image>();
            if (sButton != null) { o2Ring.sprite = sButton; o2Ring.type = Image.Type.Sliced; }
            o2Ring.color = new Color(0.2f, 0.35f, 0.55f, 0.45f);
            ctrl.quizRing = o2Ring;

            var o2TitleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            o2TitleGO.transform.SetParent(opt2GO.transform, false);
            var o2trt = o2TitleGO.GetComponent<RectTransform>();
            o2trt.anchorMin = new Vector2(0, 0.5f); o2trt.anchorMax = new Vector2(1, 1);
            o2trt.offsetMin = new Vector2(35, 0); o2trt.offsetMax = new Vector2(-20, -12);
            var o2Title = o2TitleGO.GetComponent<Text>();
            o2Title.font = standardFont;
            o2Title.text = "◇  Quiz";
            o2Title.fontSize = 40;
            o2Title.fontStyle = FontStyle.Bold;
            o2Title.color = Color.white;
            o2Title.alignment = TextAnchor.MiddleLeft;

            var o2SubGO = new GameObject("Sub", typeof(RectTransform), typeof(Text));
            o2SubGO.transform.SetParent(opt2GO.transform, false);
            var o2srt = o2SubGO.GetComponent<RectTransform>();
            o2srt.anchorMin = new Vector2(0, 0); o2srt.anchorMax = new Vector2(1, 0.5f);
            o2srt.offsetMin = new Vector2(35, 10); o2srt.offsetMax = new Vector2(-20, 0);
            var o2Sub = o2SubGO.GetComponent<Text>();
            o2Sub.font = standardFont;
            o2Sub.text = "Test your knowledge on 6 target muscles";
            o2Sub.fontSize = 24;
            o2Sub.fontStyle = FontStyle.Normal;
            o2Sub.color = new Color(0.85f, 0.90f, 0.98f, 0.85f);
            o2Sub.alignment = TextAnchor.MiddleLeft;

            // Start Exploring Button
            var startExpGO = new GameObject("StartExploringButton", typeof(RectTransform), typeof(Image), typeof(Button));
            startExpGO.transform.SetParent(tChooseMode, false);
            var srt = startExpGO.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0); srt.anchorMax = new Vector2(0.5f, 0); srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0, 160);
            srt.sizeDelta = new Vector2(880, 140);
            var sImg = startExpGO.GetComponent<Image>();
            sImg.sprite = sButton;
            sImg.type = Image.Type.Sliced;
            var sBtn = startExpGO.GetComponent<Button>();
            sBtn.targetGraphic = sImg;
            UnityEventTools.AddPersistentListener(sBtn.onClick, ctrl.OnStartExploringClicked);

            var sTextGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            sTextGO.transform.SetParent(startExpGO.transform, false);
            var strt = sTextGO.GetComponent<RectTransform>();
            strt.anchorMin = Vector2.zero; strt.anchorMax = Vector2.one;
            strt.offsetMin = Vector2.zero; strt.offsetMax = Vector2.zero;
            var sText = sTextGO.GetComponent<Text>();
            sText.font = standardFont;
            sText.text = "Start Exploring";
            sText.fontSize = 44;
            sText.fontStyle = FontStyle.Bold;
            sText.color = Color.white;
            sText.alignment = TextAnchor.MiddleCenter;

            // Initial Active States:
            // homescreenpannel is active, others inactive
            tHome.gameObject.SetActive(true);
            tOnboarding.gameObject.SetActive(false);
            tChooseMode.gameObject.SetActive(false);
            tSplash.gameObject.SetActive(false);

            ctrl.selectedMode = 0;
            ctrl.UpdateModeVisuals();

            EditorUtility.SetDirty(canvasGO);
            EditorUtility.SetDirty(ctrl);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[SetupHierarchyPanels] Hierarchy panels successfully created in MainUICanvas and scene saved!");
        }
    }
}
