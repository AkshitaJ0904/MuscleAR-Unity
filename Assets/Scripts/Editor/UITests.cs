using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MuscleAR.EditorTools
{
    public static class SixActionVerification
    {
        [MenuItem("MuscleAR/Verify Six Muscle Actions")]
        public static void RunTests()
        {
            var log = new StringBuilder(); int passed = 0, total = 0;
            GameObject host = null;
            void Check(bool ok, string name) { total++; if (ok) passed++; log.AppendLine((ok ? "PASS " : "FAIL ") + name); }
            try
            {
                host = new GameObject("ActionRegressionRoot");
                host.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(-90, 37, 0));
                host.transform.localScale = Vector3.one * 0.7f;
                var body = HumanBodyBuilder.Build(); body.transform.SetParent(host.transform, false);
                var cameraObject = new GameObject("ActionVerificationCamera");
                cameraObject.transform.SetParent(host.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                camera.fieldOfView = 48f; camera.nearClipPlane = 0.03f;
                camera.cullingMask = 1 << 30;
                foreach (var child in body.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                var light = cameraObject.AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = 1.4f; light.cullingMask = 1 << 30;
                Vector3 center = host.transform.TransformPoint(new Vector3(0, 0.875f, 0));
                camera.transform.position = center + new Vector3(0.9f, 1.5f, 1.5f);
                camera.transform.LookAt(center);
                void Capture(string name, Action prepare = null)
                {
                    var target = new RenderTexture(720, 960, 24);
                    var previous = RenderTexture.active;
                    camera.targetTexture = target; prepare?.Invoke();
                    camera.Render(); RenderTexture.active = target;
                    var image = new Texture2D(720, 960, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 720, 960), 0, 0); image.Apply();
                    System.IO.Directory.CreateDirectory("Shots");
                    System.IO.File.WriteAllBytes("Shots/" + name + ".png", image.EncodeToPNG());
                    camera.targetTexture = null; RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
                }
                var filters = body.GetComponentsInChildren<MeshFilter>(true);
                Vector3[] Snapshot()
                {
                    var points = new System.Collections.Generic.List<Vector3>();
                    foreach (var filter in filters)
                    {
                        var vertices = filter.sharedMesh.vertices;
                        for (int i = 0; i < vertices.Length; i += 17) points.Add(filter.transform.TransformPoint(vertices[i]));
                    }
                    return points.ToArray();
                }
                float Difference(Vector3[] a, Vector3[] b)
                { float max = 0f; for (int i = 0; i < a.Length; i++) max = Mathf.Max(max, Vector3.Distance(a[i], b[i])); return max; }
                var neutral = Snapshot();
                var neutralMeshes = new System.Collections.Generic.Dictionary<MeshFilter, Vector3[]>();
                foreach (var filter in filters) neutralMeshes[filter] = filter.sharedMesh.vertices;
                Vector3 position = host.transform.position, scale = host.transform.localScale;
                Quaternion rotation = host.transform.rotation;
                foreach (MuscleId id in new[] { MuscleId.BicepsBrachii, MuscleId.Deltoid, MuscleId.PectoralisMajor,
                    MuscleId.RectusAbdominis, MuscleId.QuadricepsFemoris, MuscleId.Gastrocnemius })
                {
                    body.ResetAction(); body.Select(id);
                    if (id == MuscleId.Deltoid || id == MuscleId.PectoralisMajor)
                    {
                        foreach (float progress in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                        {
                            body.GetType().GetMethod("DemonstrateAction").Invoke(body, new object[] { id, progress });
                            bool rigid = true, unrelatedStable = true;
                            foreach (var filter in filters)
                            {
                                var vertices = filter.sharedMesh.vertices;
                                var rest = neutralMeshes[filter];
                                for (int vertex = 0; vertex < rest.Length; vertex += 17)
                                    rigid &= Vector3.Distance(vertices[vertex], rest[vertex]) < 0.00001f;
                            }
                            var frame = Snapshot(); int sampleIndex = 0;
                            foreach (var filter in filters)
                            {
                                bool leftArm = false;
                                for (Transform parent = filter.transform; parent != null && parent != body.transform; parent = parent.parent)
                                    if (parent.name == "ShoulderAssembly_L" || parent.name == "ShoulderAssembly_R") leftArm = true;
                                for (int vertex = 0; vertex < neutralMeshes[filter].Length; vertex += 17)
                                {
                                    if (!leftArm) unrelatedStable &= Vector3.Distance(frame[sampleIndex], neutral[sampleIndex]) < 0.00001f;
                                    sampleIndex++;
                                }
                            }
                            Check(rigid, id + " " + progress + " preserves all mesh proportions without vertex stretching");
                            Check(unrelatedStable, id + " " + progress + " leaves torso and legs stationary");
                            int leftCount = 0, rightCount = 0; bool mirrored = true;
                            Quaternion leftPose = Quaternion.identity, rightPose = Quaternion.identity;
                            foreach (Transform joint in body.GetComponentsInChildren<Transform>(true))
                            {
                                if (joint.name == "ShoulderAssembly_L") { leftCount++; leftPose = joint.localRotation; }
                                if (joint.name == "ShoulderAssembly_R") { rightCount++; rightPose = joint.localRotation; }
                            }
                            foreach (Vector3 axis in new[] { Vector3.up, Vector3.forward })
                            {
                                Vector3 reflected = Vector3.Scale(leftPose * axis, new Vector3(-1, 1, 1));
                                mirrored &= Vector3.Distance(reflected, rightPose * axis) < 0.00001f;
                            }
                            Check(leftCount == 4 && rightCount == 4 && mirrored, id + " " + progress + " has four synchronized mirrored layer pivots per arm");
                            Check(progress == 0f ? Quaternion.Angle(leftPose, Quaternion.identity) < 0.001f && Quaternion.Angle(rightPose, Quaternion.identity) < 0.001f :
                                Quaternion.Angle(leftPose, Quaternion.identity) > 0.1f && Quaternion.Angle(rightPose, Quaternion.identity) > 0.1f,
                                id + " " + progress + " both arms are neutral at zero and both move above zero");
                            Capture("Shoulder_" + id + "_" + Mathf.RoundToInt(progress * 100));
                        }
                    }
                    body.GetType().GetMethod("DemonstrateAction").Invoke(body, new object[] { id, 0f }); var start = Snapshot();
                    body.GetType().GetMethod("DemonstrateAction").Invoke(body, new object[] { id, 0.5f }); var middle = Snapshot();
                    body.GetType().GetMethod("DemonstrateAction").Invoke(body, new object[] { id, 1f }); var end = Snapshot();
                    Capture("Action_" + id);
                    Check(Difference(start, end) > 0.005f && Difference(start, middle) > 0.001f, id + " has real 3D movement at slider midpoint/end");
                    body.GetType().GetMethod("DemonstrateAction").Invoke(body, new object[] { id, 0f }); body.GetType().GetMethod("DemonstrateAction").Invoke(body, new object[] { id, 1f });
                    Check(Difference(end, Snapshot()) < 0.00001f, id + " repeat playback has no geometry drift");
                    Check(host.transform.position == position && host.transform.localScale == scale &&
                        Quaternion.Angle(host.transform.rotation, rotation) < 0.001f, id + " preserves root pose/scale");
                    body.ResetAction();
                    Check(Difference(neutral, Snapshot()) < 0.00001f, id + " child animation resets exactly");
                }
                UIKit.Init();
                if (UIKit.Font == null) UIKit.Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var labels = HoloLabels.Create(host.transform, camera);
                labels.GetType().GetMethod("ShowTargetMuscles").Invoke(labels, new object[] { body });
                var labelItems = (System.Collections.ICollection)labels.GetType().GetField("items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(labels);
                Check(labelItems.Count == 6, "Exactly six muscle tooltips created together");
                Check(labels.GetComponentsInChildren<GraphicRaycaster>(true).Length == 1, "Callout overlay has one UI raycaster");
                Check(labels.GetComponentsInChildren<Button>(true).Length == 6, "Exactly six interactive label boxes");
                bool decorative = true;
                foreach (var graphic in labels.GetComponentsInChildren<Graphic>(true))
                    decorative &= !graphic.raycastTarget || graphic.GetComponent<Button>() != null;
                Check(decorative, "Only visible label backgrounds intercept input");
                Check(labels.GetComponentsInChildren<MuscleConnector>(true).Length == 6, "Tooltip overlay contains six connectors");
                foreach (Vector2 resolution in new[] { new Vector2(1080,1920), new Vector2(1080,2340), new Vector2(1080,2400), new Vector2(720,1600) })
                {
                    float density = resolution.x / 390f;
                    Rect available = Rect.MinMaxRect(10f * density, resolution.y * 0.36f, resolution.x - 10f * density, resolution.y * 0.78f);
                    Rect[] slots = (Rect[])labels.GetType().GetMethod("CalculateSlots").Invoke(null, new object[] { available, density });
                    bool valid = slots.Length == 6;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        // Rect arithmetic can cancel by a few float ULPs at an exact boundary.
                        // A twentieth of one physical pixel is below raster precision.
                        valid &= slots[i].xMin >= available.xMin - 0.05f && slots[i].yMin >= available.yMin - 0.05f &&
                            slots[i].xMax <= available.xMax + 0.05f && slots[i].yMax <= available.yMax + 0.05f;
                        for (int j = i + 1; j < slots.Length; j++) valid &= !slots[i].Overlaps(slots[j]);
                    }
                    Check(valid, "Six non-overlapping safe callout slots at " + resolution);
                }
                foreach (var child in labels.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                Action updateLabels = () => labels.SendMessage("LateUpdate");
                camera.backgroundColor = Color.white; Capture("Labels_White", updateLabels);
                camera.backgroundColor = Color.black; Capture("Labels_Black", updateLabels);
            }
            catch (Exception ex) { log.AppendLine("EXCEPTION " + ex); }
            finally
            {
                log.AppendLine($"Result: {passed}/{total} passed");
                System.IO.File.WriteAllText("Logs/ActionRegressionResults.txt", log.ToString());
                Debug.Log(log.ToString());
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}



namespace MuscleAR.EditorTools
{
    public static class UITests
    {
        const string TriggerFlag = "Temp/trigger_ui_tests.flag";

        // Runs only when a trigger file exists (same pattern as BuildScript / TestRunnerStandalone).
        // It used to run on every script reload and on entering Play mode; the throwaway AppFlow it
        // creates disables every Camera and EventSystem in the scene, which broke the UI in Play mode.
        [InitializeOnLoadMethod]
        static void HookTrigger()
        {
            EditorApplication.update -= CheckTrigger;
            EditorApplication.update += CheckTrigger;
        }

        static void CheckTrigger()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!System.IO.File.Exists(TriggerFlag)) return;
            try { System.IO.File.Delete(TriggerFlag); } catch { }
            RunTests();
        }

        [MenuItem("MuscleAR/Run UI Polish & Layout Tests")]
        public static void RunTests()
        {
            SixActionVerification.RunTests();
            var sb = new StringBuilder();
            sb.AppendLine("=== MuscleAR UI Polish and Bug-Free Layout Tests ===");
            int passed = 0;
            int total = 0;

            void Assert(bool condition, string testName, string details = "")
            {
                total++;
                if (condition)
                {
                    passed++;
                    sb.AppendLine($"[PASS] {testName}");
                }
                else
                {
                    sb.AppendLine($"[FAIL] {testName} - {details}");
                }
            }

            GameObject host = null;
            try
            {
                UIKit.Init();
                host = new GameObject("TestHost");
                var app = host.AddComponent<AppFlow>();

                // Build Canvas & Screens
                var canvasGO = new GameObject("TestCanvas");
                canvasGO.transform.SetParent(host.transform, false);
                app.canvas = canvasGO.AddComponent<Canvas>();
                app.scaler = canvasGO.AddComponent<CanvasScaler>();
                app.phone = UIKit.NewRect("Phone", canvasGO.transform);
                app.phone.sizeDelta = new Vector2(AppFlow.W, AppFlow.H);
                app.topInset = 34f;
                app.bottomInset = 24f;

                // Test 1: Mode Switcher Bar Structure
                app.BuildModeSwitcherBar();
                var modeBar = app.phone.Find("ModeSwitcherBar") as RectTransform;
                Assert(modeBar != null, "ModeSwitcherBar Created");
                Assert(modeBar.Find("ToggleCard") != null, "ToggleCard Exists (Segmented Control)");
                Assert(modeBar.Find("ToggleCard/BtnCameraAR") != null, "BtnCameraAR Exists");
                Assert(modeBar.Find("ToggleCard/BtnEnvironment") != null, "BtnEnvironment Exists");
                Assert(modeBar.Find("MainMenuBtn") != null, "MainMenuBtn Exists in Action Row");
                Assert(modeBar.Find("ARContextGroup/ResetRescanBtn") != null, "ResetRescanBtn Exists in Action Row");

                // Test 2: DetectSurfaceScreen (Screen 4) Layout & Overlap Check
                var scanScreen = new DetectSurfaceScreen(app);
                scanScreen.Build();
                var scanHeadline = scanScreen.root.Find("Headline")?.GetComponent<RectTransform>();
                var scanSubhead = scanScreen.root.Find("Subhead")?.GetComponent<RectTransform>();
                var scanFrame = scanScreen.root.Find("ScanFrame")?.GetComponent<RectTransform>();
                var scanStatus = scanScreen.root.Find("Status")?.GetComponent<RectTransform>();
                var scanPlaceBtn = scanScreen.root.Find("Button")?.GetComponent<RectTransform>();

                Assert(scanHeadline != null, "Scan Headline Exists");
                Assert(scanSubhead != null, "Scan Subhead Exists");
                Assert(scanFrame != null, "Scan Frame Exists");
                Assert(scanPlaceBtn != null, "Place Anatomy Button Exists");

                // Verify Headline and Subhead do not overlap in Y
                float headlineBottomY = Mathf.Abs(scanHeadline.anchoredPosition.y) + scanHeadline.sizeDelta.y;
                float subheadTopY = Mathf.Abs(scanSubhead.anchoredPosition.y);
                Assert(subheadTopY >= Mathf.Abs(scanHeadline.anchoredPosition.y) + 30f, "Headline and Subhead Separated Vertically", $"Subhead Y: {subheadTopY}, Headline Y: {scanHeadline.anchoredPosition.y}");
                Assert(scanHeadline.sizeDelta.x >= 340f, "Headline Uses Full Readable Width", $"Width: {scanHeadline.sizeDelta.x}");
                Assert(scanSubhead.sizeDelta.x >= 340f, "Subhead Uses Full Readable Width", $"Width: {scanSubhead.sizeDelta.x}");

                // Test 3: ModelPlacedScreen (Screen 6) Toolbar & Arrows
                var placedScreen = new ModelPlacedScreen(app);
                placedScreen.Build();
                var ctrlBar = placedScreen.root.Find("ARControlsBar") as RectTransform;
                Assert(ctrlBar != null, "ARControlsBar Exists");
                // Check toolbar has exactly 5 buttons (Reset View present, redundant re-scan absent)
                int ctrlBtnCount = 0;
                for (int i = 0; i < 10; i++)
                {
                    if (ctrlBar.Find("Btn" + i) != null) ctrlBtnCount++;
                }
                Assert(ctrlBtnCount == 5, "ARControlsBar Has Exactly 5 Buttons (No Redundant Re-scan)", $"Count: {ctrlBtnCount}");
                var resetViewBtn = ctrlBar.Find("Btn4/Txt")?.GetComponent<Text>();
                Assert(resetViewBtn != null && resetViewBtn.text.Contains("Reset View"), "Reset View Button Clearly Labeled", $"Text: {resetViewBtn?.text}");

                var exploreMusclesBtn = placedScreen.root.Find("Button")?.GetComponent<Button>();
                Assert(exploreMusclesBtn != null, "Explore Muscles Button Exists");
                Assert(exploreMusclesBtn.transform.Find("Icon") == null, "Explore Muscles Button Has No Extra Arrow Icon");

                // Test 4: EducationalToastScreen (Screen 10) Layout & Scrolling
                var infoScreen = new EducationalToastScreen(app);
                infoScreen.Build();
                var chips = infoScreen.root.Find("TargetChips") as RectTransform;
                Assert(chips != null, "Target Muscle Chips Container Exists");
                int chipCount = 0;
                foreach (var m in MuscleDB.TargetMuscles)
                {
                    if (chips.Find("Chip_" + m) != null) chipCount++;
                }
                Assert(chipCount == 6, "All 6 Target Muscles Have Selection Chips", $"Count: {chipCount}");

                var infoCard = infoScreen.root.Find("InfoCard") as RectTransform;
                Assert(infoCard != null, "InfoCard Exists");
                var listenAgainBtn = infoCard.Find("ListenAgainBtn")?.GetComponent<Button>();
                Assert(listenAgainBtn != null, "Listen Again Button Exists Inside InfoCard");
                var listenAgainTxt = infoCard.Find("ListenAgainBtn/Txt")?.GetComponent<Text>();
                Assert(listenAgainTxt != null && listenAgainTxt.text.Contains("Listen Again"), "Listen Again Button Correctly Labeled", $"Text: {listenAgainTxt?.text}");

                var scroll = infoCard.Find("DetailsScroll")?.GetComponent<ScrollRect>();
                Assert(scroll != null, "ScrollRect Exists for Info Panel");
                Assert(scroll != null && scroll.vertical && !scroll.horizontal, "ScrollRect Configured for Vertical Scrolling Only");

                var content = infoCard.Find("DetailsScroll/Content");
                Assert(content != null, "Scroll Content Exists");
                Assert(content.Find("Location") != null, "Location Row Exists in Scroll Content");
                Assert(content.Find("Origin") != null, "Origin Row Exists in Scroll Content");
                Assert(content.Find("Insertion") != null, "Insertion Row Exists in Scroll Content");
                Assert(content.Find("Function") != null, "Function Row Exists in Scroll Content");
                Assert(content.Find("Description") != null, "Description Exists in Scroll Content");

                var actionBtn = infoCard.Find("Button")?.GetComponent<Button>();
                Assert(actionBtn != null, "See It in Action Button Docked at Card Bottom");
                Assert(actionBtn.transform.Find("Icon") == null, "See It in Action Has No Redundant Arrow Icon");

                // Test 4b: Single Toggle Arrow Button (No redundant X button)
                Assert(infoCard.Find("CloseBtn") == null, "Close Button (X) Completely Removed From Header");
                
                var collapseBtn = infoCard.Find("CollapseBtn")?.GetComponent<Button>();
                Assert(collapseBtn != null, "Single Toggle Button (CollapseBtn) Exists Beside Listen Again");
                var collapseTxt = infoCard.Find("CollapseBtn/Txt")?.GetComponent<Text>();
                Assert(collapseTxt != null && collapseTxt.text == "▼", "Toggle Button Initially Shows Down Arrow (▼)");
                Assert(infoScreen.IsExpanded, "Panel IsExpanded Flag Initially True");

                // Test Collapse Action (Tapping down arrow ▼ collapses panel to header)
                infoScreen.CollapsePanel();
                Assert(!infoScreen.IsExpanded, "Panel IsExpanded is False After CollapsePanel()");
                Assert(collapseTxt.text == "▲", "Toggle Button Changes To Up Arrow (▲) When Collapsed");
                Assert(infoCard.sizeDelta.y <= 50f, "InfoCard Collapses to Minimal Header Height");
                Assert(chips.gameObject.activeSelf, "Muscle Selection Buttons (TargetChips) Remain Visible When Collapsed");

                // Test Expand Action (Tapping up arrow ▲ expands panel)
                infoScreen.ExpandPanel();
                Assert(infoScreen.IsExpanded, "Panel IsExpanded is True After ExpandPanel()");
                Assert(collapseTxt.text == "▼", "Toggle Button Changes To Down Arrow (▼) When Expanded");
                Assert(infoCard.sizeDelta.y >= 300f, "InfoCard Restores Full Expanded Height");
                Assert(chips.gameObject.activeSelf, "Muscle Selection Buttons Remain Visible When Expanded");

                // Test Toggle Method
                infoScreen.TogglePanel();
                Assert(!infoScreen.IsExpanded, "TogglePanel() Collapses Panel");
                Assert(collapseTxt.text == "▲", "TogglePanel() Updates Arrow To ▲");
                infoScreen.TogglePanel();
                Assert(infoScreen.IsExpanded, "TogglePanel() Expands Panel");
                Assert(collapseTxt.text == "▼", "TogglePanel() Updates Arrow To ▼");

                // Test Muscle Selection Reopens Collapsed Panel
                infoScreen.CollapsePanel();
                infoScreen.ShowMuscle(MuscleId.Deltoid, false);
                Assert(infoScreen.IsExpanded, "Selecting Muscle Automatically Reopens Collapsed Panel");
                Assert(collapseTxt.text == "▼", "Arrow Switches to ▼ After Selecting Muscle");
                var nameTxt = infoCard.Find("Name")?.GetComponent<Text>();
                Assert(nameTxt != null && nameTxt.text.Contains("DELTOID"), "InfoCard Correctly Updated with Selected Muscle Name");

                // Test 5: Muscle Audio Controller Narration & Replay
                var audioMgr = MuscleAudioController.Ensure(host);
                app.audioCtrl = audioMgr;
                Assert(audioMgr != null, "MuscleAudioController Created");
                foreach (var m in MuscleDB.TargetMuscles)
                {
                    var clip = audioMgr.GetClip(m);
                    Assert(clip != null, $"AudioClip Loaded For {m}", $"Name: {clip?.name}");
                }

                // ShowMuscle triggers auto-playback
                infoScreen.ShowMuscle(MuscleId.BicepsBrachii, true);
                Assert(audioMgr.CurrentMuscle == MuscleId.BicepsBrachii, "Selecting Muscle Auto-Plays Narration", $"Playing: {audioMgr.CurrentMuscle}");

                // Switch to Deltoid -> stops previous and plays Deltoid
                infoScreen.ShowMuscle(MuscleId.Deltoid, true);
                Assert(audioMgr.CurrentMuscle == MuscleId.Deltoid, "Switching Muscle Replaces Narration With New Muscle", $"Playing: {audioMgr.CurrentMuscle}");

                // Test 6: ModeSwitcherBar Non-Overlap with Headings Across Screens (Android Safe Area)
                float modeBarBottomY = Mathf.Abs(modeBar.anchoredPosition.y) + modeBar.sizeDelta.y;

                var testScreens = new ScreenBase[] {
                    scanScreen,
                    new PlaceModelScreen(app),
                    placedScreen,
                    new SelectPartScreen(app),
                    infoScreen,
                    new MuscleActionScreen(app),
                    new ExploreAnotherScreen(app),
                    new QuizScreen(app)
                };

                foreach (var sc in testScreens)
                {
                    if (sc.root.Find("Headline") == null) sc.Build();
                    var hl = sc.root.Find("Headline")?.GetComponent<RectTransform>();
                    Assert(hl != null, $"{sc.title} (Screen {sc.number}) Has Headline");
                    if (hl != null)
                    {
                        float hlTopY = Mathf.Abs(hl.anchoredPosition.y);
                        float gap = hlTopY - modeBarBottomY;
                        Assert(gap >= 10f, $"{sc.title} (Screen {sc.number}) Headline Clears ModeSwitcherBar (Gap: {gap:F1}px >= 10px)");
                    }
                }

                // Verify dynamic safe area update propagates without overlap
                app.topInset = 48f; // Large notch device
                modeBar.anchoredPosition = new Vector2(0, -(app.topInset + 44));
                foreach (var sc in testScreens) sc.UpdateSafeAreaLayout();
                float updatedModeBarBottomY = Mathf.Abs(modeBar.anchoredPosition.y) + modeBar.sizeDelta.y;
                foreach (var sc in testScreens)
                {
                    var hl = sc.root.Find("Headline")?.GetComponent<RectTransform>();
                    if (hl != null)
                    {
                        float hlTopY = Mathf.Abs(hl.anchoredPosition.y);
                        float gap = hlTopY - updatedModeBarBottomY;
                        Assert(gap >= 10f, $"{sc.title} Dynamic Safe Area Update Maintains Headline Clearance (Gap: {gap:F1}px)");
                    }
                }

                // Reset topInset for subsequent tests
                app.topInset = 34f;
                modeBar.anchoredPosition = new Vector2(0, -(app.topInset + 44));
                foreach (var sc in testScreens) sc.UpdateSafeAreaLayout();

                // Test 7: Redesigned Quiz Results Screen Structure and Logic
                var quizScreen = new QuizScreen(app);
                quizScreen.Build();

                var qResultsPanel = quizScreen.root.Find("ResultsPanel")?.GetComponent<RectTransform>();
                Assert(qResultsPanel != null, "Quiz ResultsPanel Exists");
                Assert(qResultsPanel.anchorMin == new Vector2(0.5f, 0.5f) && qResultsPanel.anchorMax == new Vector2(0.5f, 0.5f), "Quiz ResultsPanel is Centered");

                var rCard = qResultsPanel.Find("ResultsCard");
                Assert(rCard != null, "ResultsCard Exists Inside ResultsPanel");
                Assert(rCard.Find("RMuscle") != null, "Quiz Results Title Exists (RMuscle)");
                Assert(rCard.Find("Score") != null, "Quiz Score Text Exists");
                Assert(rCard.Find("ScorePct") != null, "Quiz Score Percentage Exists");
                Assert(rCard.Find("Button") != null, "Retake Quiz Button Exists");
                Assert(rCard.Find("BackToMuscleBtn") != null, "Back to Muscle Button Exists");

                // Verify unwanted question review elements are removed
                Assert(rCard.Find("ReviewScroll") == null, "Question Review Scroll Removed From Results");
                Assert(rCard.Find("Content") == null, "Question Review Content Removed From Results");
                Assert(rCard.Find("Msg") == null, "Generic Long Score Msg Paragraph Removed");

                var retakeBtnText = rCard.Find("Button/Label")?.GetComponent<Text>();
                Assert(retakeBtnText != null && retakeBtnText.text == "Retake Quiz", "Retake Quiz Button Has Correct Text ('Retake Quiz')", $"Text: {retakeBtnText?.text}");

                var backMuscleBtnText = rCard.Find("BackToMuscleBtn/Txt")?.GetComponent<Text>();
                Assert(backMuscleBtnText != null && backMuscleBtnText.text.Contains("Back to Muscle"), "Back to Muscle Button Has Correct Text ('← Back to Muscle')", $"Text: {backMuscleBtnText?.text}");

                // Test Quiz Scoring & Results Data Formatting via Reflection
                var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var activeMuscleField = typeof(QuizScreen).GetField("activeMuscle", bindingFlags);
                var userChoicesField = typeof(QuizScreen).GetField("userChoices", bindingFlags);
                var currentQuestionsField = typeof(QuizScreen).GetField("currentQuestions", bindingFlags);
                var restartQuizMethod = typeof(QuizScreen).GetMethod("RestartQuiz", bindingFlags);
                var showResultsMethod = typeof(QuizScreen).GetMethod("ShowResults", bindingFlags);
                var userScoreField = typeof(QuizScreen).GetField("userScore", bindingFlags);
                var currentQIndexField = typeof(QuizScreen).GetField("currentQIndex", bindingFlags);

                activeMuscleField.SetValue(quizScreen, MuscleId.Gastrocnemius);
                restartQuizMethod.Invoke(quizScreen, null);

                var questions = currentQuestionsField.GetValue(quizScreen) as System.Collections.Generic.List<QuizScreen.QuizQuestion>;
                var choices = userChoicesField.GetValue(quizScreen) as System.Collections.Generic.List<int>;
                Assert(questions != null && questions.Count > 0, "Gastrocnemius Quiz Questions Loaded");

                // Test Case A: 1/5 correct (20%)
                choices[0] = questions[0].correctIndex;
                for (int i = 1; i < questions.Count; i++) choices[i] = (questions[i].correctIndex + 1) % 4;
                showResultsMethod.Invoke(quizScreen, null);

                var rTitleTxt = rCard.Find("RMuscle")?.GetComponent<Text>()?.text;
                var scoreTxt = rCard.Find("Score")?.GetComponent<Text>()?.text;
                var pctTxt = rCard.Find("ScorePct")?.GetComponent<Text>()?.text;

                Assert(rTitleTxt != null && rTitleTxt.Contains("GASTROCNEMIUS QUIZ RESULTS"), "Results Title Displays Muscle Name (GASTROCNEMIUS QUIZ RESULTS)", $"Title: {rTitleTxt}");
                Assert(scoreTxt == "1/5 Correct", "Score Formatted Accurately (1/5 Correct)", $"Score: {scoreTxt}");
                Assert(pctTxt == "20%", "Percentage Formatted Accurately (20%)", $"Pct: {pctTxt}");

                // Test Case B: 5/5 correct (100%)
                for (int i = 0; i < questions.Count; i++) choices[i] = questions[i].correctIndex;
                showResultsMethod.Invoke(quizScreen, null);
                scoreTxt = rCard.Find("Score")?.GetComponent<Text>()?.text;
                pctTxt = rCard.Find("ScorePct")?.GetComponent<Text>()?.text;
                Assert(scoreTxt == "5/5 Correct", "Perfect Score Formatted Accurately (5/5 Correct)", $"Score: {scoreTxt}");
                Assert(pctTxt == "100%", "Perfect Percentage Formatted Accurately (100%)", $"Pct: {pctTxt}");

                // Test Case C: 0/5 correct (0%)
                for (int i = 0; i < questions.Count; i++) choices[i] = (questions[i].correctIndex + 1) % 4;
                showResultsMethod.Invoke(quizScreen, null);
                scoreTxt = rCard.Find("Score")?.GetComponent<Text>()?.text;
                pctTxt = rCard.Find("ScorePct")?.GetComponent<Text>()?.text;
                Assert(scoreTxt == "0/5 Correct", "Zero Score Formatted Accurately (0/5 Correct)", $"Score: {scoreTxt}");
                Assert(pctTxt == "0%", "Zero Percentage Formatted Accurately (0%)", $"Pct: {pctTxt}");

                // Test Retake Quiz Action
                var retakeBtn = rCard.Find("Button")?.GetComponent<Button>();
                retakeBtn.onClick.Invoke();
                int resetIndex = (int)currentQIndexField.GetValue(quizScreen);
                int resetScore = (int)userScoreField.GetValue(quizScreen);
                choices = userChoicesField.GetValue(quizScreen) as System.Collections.Generic.List<int>;
                bool allChoicesReset = true;
                foreach (int c in choices) if (c != -1) allChoicesReset = false;

                Assert(resetIndex == 0, "Retake Quiz Restarts at Question 1 (Index 0)");
                Assert(resetScore == 0, "Retake Quiz Resets Score to 0");
                Assert(allChoicesReset, "Retake Quiz Resets All User Choices to -1");
                Assert(qResultsPanel.gameObject.activeSelf == false, "ResultsPanel Hidden On Retake Quiz");

                // Test Back to Muscle Action
                activeMuscleField.SetValue(quizScreen, MuscleId.Gastrocnemius);
                var backMuscleBtn = rCard.Find("BackToMuscleBtn")?.GetComponent<Button>();
                backMuscleBtn.onClick.Invoke();
                Assert(app.selected == MuscleId.Gastrocnemius, "Back to Muscle Preserves Gastrocnemius Selection", $"Selected: {app.selected}");

                // Exercise the real direct-quiz routes and shared question sets for every muscle.
                var pickerType = typeof(QuizScreen).Assembly.GetType("MuscleAR.QuizMuscleSelectionScreen", true);
                var picker = (ScreenBase)Activator.CreateInstance(pickerType, app);
                picker.Build();
                app.screens.Clear(); app.screens.Add(picker); app.screens.Add(quizScreen); app.current = -1;
                typeof(AppFlow).GetMethod("BeginDirectQuiz").Invoke(app, null);
                Assert(app.screens[app.current].number == 17, "Direct Quiz opens muscle picker instead of Biceps quiz");
                Assert(picker.root.GetComponentsInChildren<Button>(true).Length == 7, "Picker has six muscle buttons and Back");
                var database = (System.Collections.Generic.Dictionary<MuscleId, System.Collections.Generic.List<QuizScreen.QuizQuestion>>)
                    typeof(QuizScreen).GetField("QuizDB", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
                foreach (var muscle in new[] { MuscleId.BicepsBrachii, MuscleId.Deltoid, MuscleId.PectoralisMajor,
                    MuscleId.RectusAbdominis, MuscleId.QuadricepsFemoris, MuscleId.Gastrocnemius })
                {
                    pickerType.GetMethod("ChooseMuscle").Invoke(picker, new object[] { muscle });
                    questions = (System.Collections.Generic.List<QuizScreen.QuizQuestion>)currentQuestionsField.GetValue(quizScreen);
                    Assert(app.selected == muscle && (MuscleId)activeMuscleField.GetValue(quizScreen) == muscle &&
                        object.ReferenceEquals(questions, database[muscle]) && questions.Count == 5, muscle + " direct quiz uses exactly its own five questions");
                    choices = (System.Collections.Generic.List<int>)userChoicesField.GetValue(quizScreen);
                    for (int i = 0; i < 5; i++) choices[i] = questions[i].correctIndex;
                    showResultsMethod.Invoke(quizScreen, null);
                    Assert(rCard.Find("RMuscle").GetComponent<Text>().text.Contains(MuscleDB.Get(muscle).name.ToUpper()) &&
                        rCard.Find("Score").GetComponent<Text>().text == "5/5 Correct", muscle + " results match the selected muscle");
                    retakeBtn.onClick.Invoke();
                    Assert((int)currentQIndexField.GetValue(quizScreen) == 0 && (int)userScoreField.GetValue(quizScreen) == 0 &&
                        (MuscleId)activeMuscleField.GetValue(quizScreen) == muscle && choices.TrueForAll(choice => choice == -1), muscle + " retry starts fresh on the same muscle");
                    choices[0] = 0;
                    typeof(QuizScreen).GetMethod("RequestBack").Invoke(quizScreen, null);
                    Assert(quizScreen.root.Find("LeaveAttemptConfirmation").gameObject.activeSelf, muscle + " unfinished Back asks before discarding answers");
                    // Continue preserves the attempt, then a completed attempt can return to the picker.
                    var confirmationButtons = quizScreen.root.Find("LeaveAttemptConfirmation").GetComponentsInChildren<Button>(true);
                    confirmationButtons[0].onClick.Invoke();
                    Assert(choices[0] == 0, muscle + " Continue preserves current answer");
                    showResultsMethod.Invoke(quizScreen, null);
                    typeof(QuizScreen).GetMethod("RequestBack").Invoke(quizScreen, null);
                    Assert(app.screens[app.current].number == 17, muscle + " results Back returns to picker");
                }
                app.screens.Clear(); app.current = -1;

                sb.AppendLine($"\nSUMMARY: {passed}/{total} Tests Passed!");
                System.IO.File.WriteAllText("Logs/UITestResults.txt", sb.ToString());
                Debug.Log(sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError("[UITests] Exception during test execution: " + ex);
            }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
// Trigger reload

