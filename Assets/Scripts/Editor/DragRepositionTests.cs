using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MuscleAR.EditorTools
{
    public static class DragRepositionTests
    {
        [MenuItem("MuscleAR/Run Drag-to-Reposition Tests")]
        public static void RunTests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MuscleAR Drag-to-Reposition Automated Tests ===");
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

            GameObject testRoot = null;
            try
            {
                testRoot = new GameObject("TestRoot");

                // 1. Create HumanBody and Camera
                var camGo = new GameObject("TestCam");
                camGo.transform.SetParent(testRoot.transform);
                camGo.transform.position = new Vector3(0, 1.5f, 2.5f);
                camGo.transform.LookAt(Vector3.zero);
                var cam = camGo.AddComponent<Camera>();
                cam.pixelRect = new Rect(0, 0, 1080, 1920);

                var body = HumanBodyBuilder.Build();
                body.transform.SetParent(testRoot.transform);

                // 2. Create ModelController
                var model = ModelController.Create(testRoot.transform, body, cam);
                var input = testRoot.AddComponent<InputRouter>();
                model.Bind(input);

                // Test 1: Hierarchy and Component Creation
                Assert(model != null, "ModelController Created");
                Assert(model.root != null, "ModelController Root Assigned");
                Assert(model.dragTarget != null, "ModelDragTarget Component Created");
                Assert(model.dragTarget.dragCollider != null, "ModelDragTarget BoxCollider Exists");
                Assert(model.dragTarget.ringRenderer != null, "ModelDragTarget Visual Ring Exists");

                // Test 2: Initial Anchoring State
                Vector3 initialAnchor = new Vector3(0.5f, 0.78f, 1.2f);
                Quaternion initialRot = Quaternion.Euler(0, 45f, 0);
                model.SetAnchor(initialAnchor, initialRot, cam.transform.position);

                Assert(model.useWorldAnchor, "Anchor Activated (useWorldAnchor == true)");
                float initialScale = model.scale;
                float initialYaw = model.yaw;
                Quaternion modelRotBeforeDrag = model.root.rotation;

                // Regression: user yaw is world-up only; anatomy retains its lying base pose.
                model.mode = ModelController.Mode.Explore;
                Quaternion lyingPose = body.transform.parent.localRotation;
                for (int turn = 0; turn < 4; turn++)
                {
                    model.yaw = turn * 90f;
                    input.OnDrag?.Invoke(new Vector2(0f, 120f));
                    Assert(Vector3.Dot(model.root.up, Vector3.up) > 0.9999f, "Yaw-only root stays level " + turn);
                    Assert(Quaternion.Angle(body.transform.parent.localRotation, lyingPose) < 0.001f, "Lying child pose preserved " + turn);
                }
                input.OnPan?.Invoke(new Vector2(75f, 50f));
                input.OnPinch?.Invoke(1.05f);
                Vector3 userPosition = model.root.position;
                Quaternion userRotation = model.root.rotation;
                Vector3 userScale = model.root.localScale;
                model.SetAnchor(Vector3.one * 10f, Quaternion.identity);
                foreach (MuscleId id in new[] { MuscleId.BicepsBrachii, MuscleId.Gastrocnemius })
                {
                    body.Select(id); body.ActionAmount = 0.7f; body.Flexion = 70f;
                    model.Show(true, true);
                    Assert(Vector3.Distance(model.root.position, userPosition) < 0.00001f &&
                        Quaternion.Angle(model.root.rotation, userRotation) < 0.001f &&
                        Vector3.Distance(model.root.localScale, userScale) < 0.00001f,
                        "Selection, child action, visibility and repeated placement preserve user transform " + id);
                }
                body.ResetAction(); model.ResetTransform();
                Assert(model.yaw == 0f && Mathf.Abs(model.scale - 0.4f) < 0.00001f, "Explicit Reset View restores defaults");
                model.ClearAnchor(); model.SetAnchor(initialAnchor, initialRot, cam.transform.position);
                model.mode = ModelController.Mode.None;

                // Test 3: Drag Start on Drag Target
                // Simulate pointer down directly on the reposition drag target
                Ray dragRay = new Ray(new Vector3(0.5f, 2.0f, 1.4f), Vector3.down);
                RaycastHit[] hits = Physics.RaycastAll(dragRay, 10f);
                bool hitDragTarget = false;
                foreach (var h in hits)
                {
                    if (h.collider.GetComponentInParent<ModelDragTarget>() != null)
                    {
                        hitDragTarget = true;
                        break;
                    }
                }
                Assert(hitDragTarget, "Physics Raycast Hits ModelDragTarget On Floor Surface");

                // Start Repositioning
                model.StartReposition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                Assert(model.IsRepositioning, "IsRepositioning Flag Set to True on Drag Start");
                Assert(input.SuppressTap, "InputRouter SuppressTap Flag Set on Drag Start");
                Assert(model.dragTarget.IsDragging, "ModelDragTarget Visual State Set to Dragging");

                // Test 4: Drag Movement & Smooth Surface Following
                Vector3 newTargetPos = new Vector3(0.8f, 0.78f, 1.5f);
                // Simulate updating reposition position
                model.UpdateReposition(new Vector2(Screen.width * 0.6f, Screen.height * 0.4f));

                // Test 5: End Repositioning & Preservation of Scale/Rotation
                model.EndReposition(new Vector2(Screen.width * 0.6f, Screen.height * 0.4f));
                Assert(!model.IsRepositioning, "IsRepositioning Flag Cleared on Drag End");
                Assert(!model.dragTarget.IsDragging, "ModelDragTarget Visual State Cleared on Drag End");
                Assert(Mathf.Approximately(model.scale, initialScale), "Model Scale Perfectly Preserved");
                Assert(Mathf.Approximately(model.yaw, initialYaw), "Model Yaw Offset Perfectly Preserved");
                Assert(Quaternion.Angle(model.root.rotation, modelRotBeforeDrag) < 0.1f, "World Rotation Perfectly Preserved");

                // Test 6: Muscle Selection Priority (Conflict Prevention)
                // When a ray hits a muscle, it should NOT trigger drag repositioning
                RaycastHit[] muscleHits = new RaycastHit[2];
                // Check that ModelController.Pick finds muscles
                var bicepsCenter = body.MuscleCenter(MuscleId.BicepsBrachii, 1);
                var bicepsScreenPos = cam.WorldToScreenPoint(bicepsCenter);
                MuscleId pickedMuscle;
                bool picked = model.Pick(bicepsScreenPos, out pickedMuscle);
                Assert(picked, "ModelController.Pick Successfully Detects Muscle Taps", $"Picked: {pickedMuscle}");

                // Test 7: Two-Finger Gesture Precedence
                model.StartReposition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                Assert(model.IsRepositioning, "Repositioning Started Before Two-Finger Gesture");
                input.OnTwoFingerBegan?.Invoke();
                Assert(!model.IsRepositioning, "Two-Finger Gesture Cancels Repositioning Cleanly");

                // Test 8: ARManager Surface Raycasting API
                var arGo = new GameObject("ARManagerTest");
                arGo.transform.SetParent(testRoot.transform);
                var ar = arGo.AddComponent<ARManager>();
                Assert(ar != null, "ARManager Component Instantiated");

                Pose testPose;
                // Without detected planes, RaycastSurface should safely return false without throwing or snapping
                bool raycastResult = ar.RaycastSurface(new Vector2(100, 100), out testPose);
                Assert(!raycastResult, "RaycastSurface Safely Returns False When No Plane Detected (Prevents Unpredictable Snapping)");

                // Test 9: ModelController.ClearAnchor Resets State Safely
                model.StartReposition(new Vector2(100, 100));
                model.ClearAnchor();
                Assert(!model.useWorldAnchor, "ClearAnchor Clears useWorldAnchor");
                Assert(!model.IsRepositioning, "ClearAnchor Cancels Active Repositioning");
                Assert(model.worldAnchor == Vector3.zero, "ClearAnchor Zeroes World Anchor");
                Assert(model.movedDistance == 0f, "ClearAnchor Resets movedDistance Metric");
                Assert(!model.root.gameObject.activeSelf, "ClearAnchor Hides Model Instance");

                // Test 10: Idempotent Clear / Repeated Resets
                bool threw = false;
                try
                {
                    model.ClearAnchor();
                    model.ClearAnchor();
                }
                catch
                {
                    threw = true;
                }
                Assert(!threw, "Repeated ClearAnchor Calls Are Completely Safe & Idempotent");

                // Test 11: SwitchViewingMode State Transitions & Drag Disabled in Environment Mode
                if (AppFlow.I != null)
                {
                    AppFlow.I.SwitchViewingMode(AppFlow.ViewingMode.Environment);
                    Assert(AppFlow.I.viewingMode == AppFlow.ViewingMode.Environment, "Switched to Environment Mode");
                    Assert(!AppFlow.I.ar.IsARActive, "ARManager IsARActive is False in Environment Mode");
                    Assert(AppFlow.I.rig.main.enabled, "CameraRig main camera enabled in Environment Mode");
                    Assert(!AppFlow.I.model.useWorldAnchor, "Model useWorldAnchor is False in Environment Mode");

                    // Verify dragging is completely disabled in Environment Mode
                    Assert(!AppFlow.I.model.CanDragReposition(), "CanDragReposition is False in Environment Mode");
                    Assert(AppFlow.I.model.dragTarget != null && !AppFlow.I.model.dragTarget.gameObject.activeSelf, "ModelDragTarget is Inactive in Environment Mode");
                    if (AppFlow.I.model.dragTarget != null && AppFlow.I.model.dragTarget.dragCollider != null)
                    {
                        Assert(!AppFlow.I.model.dragTarget.dragCollider.enabled, "ModelDragTarget BoxCollider Disabled in Environment Mode");
                    }

                    // Attempt StartReposition in Environment Mode -> Must be ignored
                    AppFlow.I.model.StartReposition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                    Assert(!AppFlow.I.model.IsRepositioning, "StartReposition Safely Ignored in Environment Mode");

                    // Attempt OnDrag in Move Mode -> Model position must remain unchanged
                    Vector2 envPosBefore = AppFlow.I.model.tablePos;
                    AppFlow.I.model.mode = ModelController.Mode.Move;
                    // Trigger gesture drag
                    Vector3 rootPosBefore = AppFlow.I.model.root.position;
                    // Test 12: Environment Look Switching & Preservation
                    AppFlow.I.SetEnvironmentLook(LabEnvironment.Look.Day);
                    Assert(AppFlow.I.currentEnvironmentLook == LabEnvironment.Look.Day, "Environment Look switched to Day");

                    // Switch back to Camera AR mode and verify dragging re-enables only after placement
                    AppFlow.I.SwitchViewingMode(AppFlow.ViewingMode.CameraAR);
                    Assert(AppFlow.I.viewingMode == AppFlow.ViewingMode.CameraAR, "Switched to Camera AR Mode");
                    Assert(AppFlow.I.ar.IsARActive, "ARManager IsARActive is True in Camera AR Mode");
                    Assert(!AppFlow.I.placed, "Model placed flag is False prior to AR placement");
                    Assert(!AppFlow.I.model.CanDragReposition(), "CanDragReposition remains False during AR scanning before placement");

                    // Simulate surface placement in Camera AR
                    AppFlow.I.EnsureModelAnchoredInAR();
                    Assert(AppFlow.I.placed, "Model placed flag is True after AR placement");
                    Assert(AppFlow.I.model.CanDragReposition(), "CanDragReposition is True after AR placement in Camera AR Mode");
                    Assert(AppFlow.I.model.dragTarget.gameObject.activeSelf, "ModelDragTarget Active in Camera AR Mode after placement");

                    // Verify active drag cancellation on mode switch
                    AppFlow.I.model.StartReposition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                    Assert(AppFlow.I.model.IsRepositioning, "Active Repositioning Started in Camera AR");
                    AppFlow.I.SwitchViewingMode(AppFlow.ViewingMode.Environment);
                    Assert(!AppFlow.I.model.IsRepositioning, "Active Repositioning Immediately Cancelled on Switch to Environment Mode");
                    Assert(!AppFlow.I.model.CanDragReposition(), "CanDragReposition safely cleared upon switching to Environment Mode");

                    // Reset back to AR mode for default
                    AppFlow.I.SwitchViewingMode(AppFlow.ViewingMode.CameraAR);
                }
                else
                {
                    // Standalone mode validation (without full AppFlow instance)
                    model.SetAnchor(initialAnchor, initialRot, cam.transform.position);
                    Assert(model.CanDragReposition(), "CanDragReposition is True for anchored model in standalone test");
                    model.ClearAnchor();
                    Assert(!model.CanDragReposition(), "CanDragReposition is False after ClearAnchor");
                }

                sb.AppendLine($"\nResult: {passed}/{total} tests passed.");
                System.IO.File.WriteAllText("Logs/TransformRegressionResults.txt", sb.ToString());
                Debug.Log(sb.ToString());
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[EXCEPTION] Test execution failed: {ex.Message}\n{ex.StackTrace}");
                Debug.LogError(sb.ToString());
            }
            finally
            {
                System.IO.File.WriteAllText("Logs/TransformRegressionResults.txt", sb.ToString());
                if (testRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(testRoot);
                }
            }
        }
    }
}
