using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MuscleAR.EditorTools
{
    // Retains the existing verifier asset; exercises the tooltip replacement.
    public static class VerifyLeaderRouting
    {
        const string Flag = "Temp/verify_fixed_labels.flag";
        [InitializeOnLoadMethod]
        static void Hook() { EditorApplication.update -= CheckFlag; EditorApplication.update += CheckFlag; }
        static void CheckFlag()
        {
            if (!File.Exists(Flag) || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
            File.Delete(Flag); Run();
        }
        [MenuItem("MuscleAR/Verify Muscle Tooltips")]
        public static void Run()
        {
            var log = new StringBuilder(); int passed = 0, total = 0;
            GameObject host = new GameObject("FixedLabelVerification");
            void Check(bool ok, string name) { total++; if (ok) passed++; log.AppendLine((ok ? "PASS " : "FAIL ") + name); }
            try
            {
                UIKit.Init();
                var cameraObject = new GameObject("TooltipTestCamera"); cameraObject.transform.SetParent(host.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.position = new Vector3(0, 1, 3); camera.transform.LookAt(Vector3.up);
                var body = HumanBodyBuilder.Build(); body.transform.SetParent(host.transform, false);
                var labels = HoloLabels.Create(host.transform, camera);
                labels.ShowTargetMuscles(body);
                var late = typeof(HoloLabels).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                late.Invoke(labels, null);
                Canvas.ForceUpdateCanvases();
                var buttons = labels.GetComponentsInChildren<Button>();
                Check(buttons.Length == 6, "six visible clickable labels");
                int connectorCount = 0; bool connectorsPassive = true;
                foreach (var link in labels.GetComponentsInChildren<MuscleConnector>(true)) { connectorCount++; connectorsPassive &= !link.raycastTarget; }
                Check(connectorCount == 6 && connectorsPassive, "six connectors exist and none intercept touches");

                // State machine: gesture hides, settling holds hidden, action shows only the selection, idle shows all.
                {
                    var v = new ConnectorVisibility(); float t = 10f; const float settle = 0.2f;
                    v.Step(t, false, false, settle);
                    Check(v.Current == ConnectorVisibility.State.Idle && v.Shows(MuscleId.Deltoid, null) && v.Shows(MuscleId.Gastrocnemius, null), "idle shows all six connectors");
                    v.SetGesture(true, t, settle); v.Step(t, false, false, settle);
                    Check(v.Current == ConnectorVisibility.State.Manipulating && !v.Shows(MuscleId.Deltoid, null), "gesture hides every connector");
                    t += 5f; v.Step(t, false, false, settle);
                    Check(v.Current == ConnectorVisibility.State.Manipulating, "held gesture never restores connectors");
                    v.SetGesture(false, t, settle); v.Step(t + 0.1f, false, false, settle);
                    Check(v.Current == ConnectorVisibility.State.Settling && !v.Shows(MuscleId.Deltoid, null), "settling keeps connectors hidden");
                    v.SetGesture(true, t + 0.15f, settle); v.Step(t + 0.3f, false, false, settle);
                    Check(v.Current == ConnectorVisibility.State.Manipulating, "new gesture during settling cancels restoration");
                    v.SetGesture(false, t + 0.3f, settle); v.Step(t + 0.4f, true, false, settle);
                    Check(v.Current == ConnectorVisibility.State.Manipulating, "inertial model motion keeps connectors hidden");
                    v.Step(t + 0.5f, false, false, settle); v.Step(t + 0.75f, false, false, settle);
                    Check(v.Current == ConnectorVisibility.State.Idle, "connectors restore after settle delay");
                    v.Step(t + 1f, false, true, settle);
                    Check(v.Current == ConnectorVisibility.State.Action && v.Shows(MuscleId.BicepsBrachii, MuscleId.BicepsBrachii) &&
                        !v.Shows(MuscleId.Deltoid, MuscleId.BicepsBrachii), "action shows only the selected connector");
                    v.SetGesture(true, t + 1.1f, settle); v.Step(t + 1.1f, false, true, settle);
                    Check(!v.Shows(MuscleId.BicepsBrachii, MuscleId.BicepsBrachii), "gesture during action hides the selected connector");
                    v.SetGesture(false, t + 1.2f, settle); v.Step(t + 1.5f, false, true, settle);
                    Check(v.Current == ConnectorVisibility.State.Action && v.Shows(MuscleId.BicepsBrachii, MuscleId.BicepsBrachii) &&
                        !v.Shows(MuscleId.Deltoid, MuscleId.BicepsBrachii), "after gesture during action only the selected connector returns");
                    float a = 0f; for (int i = 0; i < 20; i++) a = ConnectorVisibility.Fade(a, true, 0.01f, 0.12f, 0.2f);
                    Check(Mathf.Approximately(a, 1f) && ConnectorVisibility.Fade(0.5f, false, 0.03f, 0.12f, 0.2f) < 0.5f, "alpha fades smoothly in both directions");
                }
                var positions = new Vector3[buttons.Length];
                for (int i = 0; i < buttons.Length; i++) positions[i] = buttons[i].transform.position;
                MuscleId? clicked = null; labels.OnMuscleClicked = id => clicked = id;
                var ids = new[] { MuscleId.Deltoid, MuscleId.PectoralisMajor, MuscleId.BicepsBrachii,
                    MuscleId.RectusAbdominis, MuscleId.QuadricepsFemoris, MuscleId.Gastrocnemius };
                for (int i = 0; i < buttons.Length; i++)
                {
                    clicked = null; buttons[i].onClick.Invoke();
                    Check(clicked == ids[i], "button ID " + ids[i]);
                    Check(buttons[i].GetComponent<Image>().raycastTarget, "visible hitbox " + ids[i]);
                    labels.SetSelectedMuscle(ids[i]);
                    int accents = 0;
                    foreach (var graphic in labels.GetComponentsInChildren<Image>())
                        if (graphic.name == "SelectedAccent" && graphic.enabled) accents++;
                    Check(accents == 1 && buttons[i].transform.Find("SelectedAccent").GetComponent<Image>().enabled,
                        "exactly the selected tooltip is highlighted: " + ids[i]);
                }
                foreach (var graphic in labels.GetComponentsInChildren<Graphic>())
                    Check(!graphic.raycastTarget || graphic.GetComponent<Button>() != null, "only actual label boxes raycast: " + graphic.name);
                foreach (MuscleId id in ids) foreach (float scale in new[] {0.35f,1f,2.25f}) foreach (float yaw in new[] {0f,45f,90f,180f,270f,360f})
                {
                    body.transform.localScale = Vector3.one * scale;
                    body.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    body.DemonstrateAction(id, 0.65f); labels.ShowTargetMuscles(body); late.Invoke(labels, null);
                    bool stable = true;
                    for (int i = 0; i < buttons.Length; i++) stable &= buttons[i] != null && buttons[i].gameObject.activeInHierarchy && buttons[i].transform.position == positions[i];
                    Check(stable, "fixed labels during " + id + " yaw=" + yaw + " scale=" + scale);
                    body.ResetAction();
                }
                var events = EventSystem.current;
                // Edit-mode raycasts require a rendered Canvas (otherwise graphic.depth is -1).
                var hitTarget = new RenderTexture(720,1600,24);
                camera.targetTexture = hitTarget;
                var hitCanvas = labels.GetComponentInChildren<Canvas>();
                hitCanvas.renderMode = RenderMode.ScreenSpaceCamera; hitCanvas.worldCamera = camera; hitCanvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases(); camera.Render();
                GameObject temporaryEvents = null;
                if (events == null) { temporaryEvents = new GameObject("TestEvents", typeof(EventSystem)); events = temporaryEvents.GetComponent<EventSystem>(); }
                try
                {
                    var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left, position = new Vector2(50,50) };
                    var button = (MuscleLabelButton)buttons[0]; clicked = null;
                    button.OnPointerDown(pointer); pointer.position += new Vector2(30,0); button.OnDrag(pointer);
                    button.OnPointerUp(pointer); button.OnPointerClick(pointer);
                    Check(!clicked.HasValue, "label drag does not select");
                    pointer.position = new Vector2(50,50); clicked = null;
                    button.OnPointerDown(pointer); button.OnPointerUp(pointer); button.OnPointerClick(pointer);
                    Check(clicked == ids[0], "pointer tap selects label");
                    var raycaster = labels.GetComponentInChildren<GraphicRaycaster>();
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        pointer.position = RectTransformUtility.WorldToScreenPoint(camera, buttons[i].transform.position);
                        var hits = new System.Collections.Generic.List<RaycastResult>(); raycaster.Raycast(pointer, hits);
                        Check(hits.Exists(hit => hit.gameObject == buttons[i].gameObject), "label text area raycasts to button " + ids[i]);
                        clicked = null; var target = (MuscleLabelButton)buttons[i];
                        target.OnPointerDown(pointer); target.OnPointerUp(pointer); target.OnPointerClick(pointer);
                        Check(clicked == ids[i], "pointer tap ID " + ids[i]);
                    }
                }
                finally
                {
                    if (temporaryEvents != null) UnityEngine.Object.DestroyImmediate(temporaryEvents);
                    camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(hitTarget);
                }
                // Render real Unity UI to representative portrait targets, independent of the
                // batch-mode desktop dimensions. No scene or production Canvas is changed.
                body.gameObject.SetActive(false);
                var canvas = labels.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                canvas.planeDistance = 1f; camera.clearFlags = CameraClearFlags.SolidColor;
                var layout = typeof(HoloLabels).GetMethod("Layout", BindingFlags.Instance | BindingFlags.NonPublic);
                Directory.CreateDirectory("Shots/Tooltips");
                foreach (var size in new[] { new Vector2Int(720,1600), new Vector2Int(1080,1920), new Vector2Int(1080,2340), new Vector2Int(1080,2400) })
                {
                    var target = new RenderTexture(size.x, size.y, 24);
                    var previous = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = target;
                        Canvas.ForceUpdateCanvases();
                        layout.Invoke(labels, new object[] { new Rect(0,0,size.x,size.y) });
                        Canvas.ForceUpdateCanvases();
                        foreach (var text in labels.GetComponentsInChildren<Text>())
                        {
                            Check(text.preferredHeight <= text.rectTransform.rect.height + 0.5f,
                                "text fits " + text.text + " at " + size);
                            if (!text.text.Contains(" ")) Check(text.preferredWidth <= text.rectTransform.rect.width + 0.5f,
                                "single-word name remains intact: " + text.text + " at " + size);
                        }
                        foreach (bool light in new[] {false,true})
                        {
                            camera.backgroundColor = light ? Color.white : new Color(0.04f,0.06f,0.09f);
                            camera.Render(); RenderTexture.active = target;
                            var image = new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                            image.ReadPixels(new Rect(0,0,size.x,size.y),0,0); image.Apply();
                            File.WriteAllBytes($"Shots/Tooltips/{size.x}x{size.y}_{(light ? "Light" : "Dark")}.png", image.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(image);
                        }
                    }
                    finally
                    {
                        camera.targetTexture = null; RenderTexture.active = previous;
                        UnityEngine.Object.DestroyImmediate(target);
                    }
                }
            }
            catch (Exception ex) { log.AppendLine("EXCEPTION " + ex); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                log.AppendLine($"RESULT: {passed}/{total}"); Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/TooltipEditorVerification.txt", log.ToString()); Debug.Log(log.ToString());
            }
        }

        public static void RunAndBuild()
        {
            Run();
            string results = File.ReadAllText("Logs/TooltipEditorVerification.txt");
            if (results.Contains("FAIL ") || results.Contains("EXCEPTION "))
                throw new Exception("Tooltip verification failed; see Logs/TooltipEditorVerification.txt");
            BuildScript.BuildAndroid();
        }
    }
}
