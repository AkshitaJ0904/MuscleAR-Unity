using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

namespace MuscleAR
{
    /// <summary>
    /// Complete Real AR Foundation + ARCore manager.
    /// Manages ARSession, XROrigin, ARCamera, ARCameraBackground, ARPlaneManager, and ARRaycastManager.
    /// Provides real-world plane visualization, touch-to-place anchoring, and fallback to Virtual Lab.
    /// </summary>
    public class ARManager : MonoBehaviour
    {
        public static ARManager I { get; private set; }

        public ARSession session;
        public ARInputManager inputManager;
        public XROrigin origin;
        public Camera arCamera;
        public ARCameraManager cameraManager;
        public ARCameraBackground cameraBackground;
        public TrackedPoseDriver poseDriver;
        public ARPlaneManager planeManager;
        public ARRaycastManager raycastManager;
        public Light keyLight, fillLight, rimLight;

        public bool IsSupported { get; private set; } = true;
        public bool IsARActive { get; private set; } = false;
        public string Status { get; private set; } = "Initializing Real AR...";
        public bool HasReceivedFirstFrame { get; private set; } = false;

        public event Action<ARPlane> OnPlaneFound;
        static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        public int DetectedPlaneCount => planeManager != null ? planeManager.trackables.count : 0;
        public bool HasDetectedPlanes => DetectedPlaneCount > 0;

        public static ARManager Create(Transform parent)
        {
            var go = new GameObject("ARManager");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<ARManager>();
            m.Build();
            return m;
        }

        void Build()
        {
            I = this;

            Debug.developerConsoleVisible = false;

            // 1. Ensure XR Loader is started
            EnsureXRLoaderStarted();

            // 2. ARSession
            var sessionGo = new GameObject("ARSession");
            sessionGo.transform.SetParent(transform, false);
            session = sessionGo.AddComponent<ARSession>();
            session.attemptUpdate = true;
            session.matchFrameRateRequested = true;
            inputManager = sessionGo.AddComponent<ARInputManager>();

            ARSession.stateChanged += OnSessionStateChanged;

            // 3. XR Origin hierarchy: create GameObjects first
            var originGo = new GameObject("XROrigin");
            originGo.transform.SetParent(transform, false);

            var offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);

            // 4. AR Camera
            var camGo = new GameObject("ARCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offsetGo.transform, false);
            arCamera = camGo.AddComponent<Camera>();
            arCamera.clearFlags = CameraClearFlags.SolidColor;
            arCamera.backgroundColor = Color.black;
            arCamera.nearClipPlane = 0.05f;
            arCamera.farClipPlane = 50f;
            arCamera.allowMSAA = false; // Disable MSAA for smooth mobile AR performance
            arCamera.depth = 10; // Top-most 3D camera

            cameraManager = camGo.AddComponent<ARCameraManager>();
            cameraManager.frameReceived += OnCameraFrameReceived;

            cameraBackground = camGo.AddComponent<ARCameraBackground>();

            // 5. Tracked Pose Driver (Input System 6DoF)
            poseDriver = camGo.AddComponent<TrackedPoseDriver>();
            var posAction = new InputAction("Position", binding: "<HandheldARInputDevice>/devicePosition", expectedControlType: "Vector3");
            posAction.AddBinding("<XRHMD>/centerEyePosition");
            posAction.AddBinding("<XRHMD>/devicePosition");
            posAction.Enable();

            var rotAction = new InputAction("Rotation", binding: "<HandheldARInputDevice>/deviceRotation", expectedControlType: "Quaternion");
            rotAction.AddBinding("<XRHMD>/centerEyeRotation");
            rotAction.AddBinding("<XRHMD>/deviceRotation");
            rotAction.Enable();

            poseDriver.positionInput = new InputActionProperty(posAction);
            poseDriver.rotationInput = new InputActionProperty(rotAction);

            // Add XROrigin after Camera and PoseDriver exist
            origin = originGo.AddComponent<XROrigin>();
            origin.CameraFloorOffsetObject = offsetGo;
            origin.Camera = arCamera;

            // 6. AR Plane Manager & Raycast Manager
            planeManager = originGo.AddComponent<ARPlaneManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planeManager.planePrefab = CreatePlanePrefab();
            planeManager.trackablesChanged.AddListener(OnTrackablesChanged);

            raycastManager = originGo.AddComponent<ARRaycastManager>();

            // 7. Single-pass AR Directional Key Light (prevents expensive multi-pass rendering on 60+ meshes)
            var lightRig = new GameObject("ARLightRig");
            lightRig.transform.SetParent(transform, false);

            var kGo = new GameObject("ARKeyLight");
            kGo.transform.SetParent(lightRig.transform, false);
            keyLight = kGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1.0f, 0.98f, 0.95f);
            keyLight.intensity = 1.25f;
            keyLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Request camera permissions & initialize
            StartCoroutine(InitSequence());
        }

        static void EnsureXRLoaderStarted()
        {
            try
            {
                var generalSettings = XRGeneralSettings.Instance;
                if (generalSettings != null && generalSettings.Manager != null)
                {
                    var mgr = generalSettings.Manager;
                    if (mgr.activeLoader == null)
                    {
                        Debug.Log("[MuscleAR] Initializing XR Loader via InitializeLoaderSync()...");
                        mgr.InitializeLoaderSync();
                    }
                    if (mgr.activeLoader != null)
                    {
                        Debug.Log("[MuscleAR] Starting XR Subsystems with loader: " + mgr.activeLoader.name);
                        mgr.StartSubsystems();
                        return;
                    }
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                Debug.Log("[MuscleAR] Standalone ARCoreLoader fallback...");
                var arcoreLoader = ScriptableObject.CreateInstance<UnityEngine.XR.ARCore.ARCoreLoader>();
                if (arcoreLoader != null && arcoreLoader.Initialize())
                {
                    arcoreLoader.Start();
                    Debug.Log("[MuscleAR] Standalone ARCoreLoader initialized and started successfully!");
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError("[MuscleAR] Exception in EnsureXRLoaderStarted: " + ex.Message);
            }
        }

        IEnumerator InitSequence()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                Debug.Log("[MuscleAR] Requesting camera permission for Real AR...");
                bool granted = false;
                bool completed = false;
                var cb = new UnityEngine.Android.PermissionCallbacks();
                cb.PermissionGranted += s => { granted = true; completed = true; };
                cb.PermissionDenied += s => { completed = true; };
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, cb);
                float timeout = 5f;
                while (!completed && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
            }
#endif

            if (Application.isEditor)
            {
                IsSupported = false;
                Status = "Editor mode: Virtual Lab fallback available";
                yield break;
            }

            Status = "Checking ARCore availability...";
            yield return ARSession.CheckAvailability();

            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                Status = "Installing ARCore services...";
                yield return ARSession.Install();
            }

            if (ARSession.state >= ARSessionState.Ready)
            {
                IsSupported = true;
                Status = "ARCore Ready";
                Debug.Log("[MuscleAR] ARCore is supported and ready! State: " + ARSession.state);
                SetARActive(true);
            }
            else
            {
                IsSupported = false;
                Status = "AR unsupported (state: " + ARSession.state + ")";
                Debug.LogWarning("[MuscleAR] ARCore state: " + ARSession.state);
            }
        }

        void OnSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            Debug.Log("[MuscleAR] ARSession state changed: " + args.state);
            if (args.state == ARSessionState.SessionTracking)
            {
                Status = "ARCore Tracking Active";
            }
            else if (args.state == ARSessionState.SessionInitializing)
            {
                Status = "ARCore Initializing...";
            }
        }

        void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            if (!HasReceivedFirstFrame)
            {
                HasReceivedFirstFrame = true;
                Debug.Log("[MuscleAR] First AR camera frame received! Live camera feed is active. (Timestamp: " + args.timestampNs + ")");
            }
        }

        GameObject CreatePlanePrefab()
        {
            var pgo = new GameObject("ARPlane_Template");
            pgo.transform.SetParent(transform, false);

            pgo.AddComponent<ARPlane>();
            pgo.AddComponent<MeshFilter>();
            var mr = pgo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Unlit(new Color(0.18f, 0.65f, 1f, 0.35f), SurfaceScanner.GridTexture());

            var lr = pgo.AddComponent<LineRenderer>();
            lr.sharedMaterial = Mats.Unlit(new Color(0.35f, 0.85f, 1f, 0.95f));
            lr.startWidth = lr.endWidth = 0.015f;
            lr.loop = true;
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;

            pgo.AddComponent<ARPlaneMeshVisualizer>();
            pgo.SetActive(false);
            return pgo;
        }

        void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            if (args.added != null && args.added.Count > 0)
            {
                Debug.Log("[MuscleAR] Real physical planes detected: " + args.added.Count + " new (Total: " + DetectedPlaneCount + ")");
                foreach (var p in args.added)
                {
                    if (!PlanesVisualsVisible && p != null)
                    {
                        var mr = p.GetComponent<MeshRenderer>();
                        if (mr != null) mr.enabled = false;
                        var lr = p.GetComponent<LineRenderer>();
                        if (lr != null) lr.enabled = false;
                    }
                    OnPlaneFound?.Invoke(p);
                }
            }
        }

        /// <summary>
        /// Strictly raycasts against detected AR plane polygons/bounds at the screen point.
        /// Returns true ONLY if a valid detected AR plane surface was hit directly beneath screenPoint.
        /// Does not fall back to arbitrary plane centers, preventing jumpy movement during reposition dragging.
        /// Reuses static hit buffer to guarantee zero per-frame garbage collection allocations.
        /// </summary>
        public bool RaycastSurface(Vector2 screenPoint, out Pose hitPose)
        {
            s_Hits.Clear();
            if (raycastManager != null && raycastManager.Raycast(screenPoint, s_Hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.PlaneEstimated))
            {
                hitPose = s_Hits[0].pose;
                return true;
            }

            // Fallback: If we have tracked planes, project ray from AR camera onto the closest tracked plane
            if (planeManager != null && planeManager.trackables.count > 0 && arCamera != null)
            {
                Ray ray = arCamera.ScreenPointToRay(screenPoint);
                float bestDist = float.MaxValue;
                Pose bestPose = default;
                bool found = false;

                foreach (var plane in planeManager.trackables)
                {
                    if (plane.trackingState == TrackingState.Tracking || plane.trackingState == TrackingState.Limited)
                    {
                        Plane mathPlane = new Plane(plane.normal, plane.center);
                        if (mathPlane.Raycast(ray, out float enter))
                        {
                            if (enter > 0 && enter < bestDist)
                            {
                                bestDist = enter;
                                Vector3 pt = ray.GetPoint(enter);
                                bestPose = new Pose(pt, plane.transform.rotation);
                                found = true;
                            }
                        }
                    }
                }

                if (found)
                {
                    hitPose = bestPose;
                    return true;
                }
            }

            hitPose = default;
            return false;
        }

        /// <summary>Centre of the active AR camera's viewport in screen pixels (not the full screen).</summary>
        public Vector2 ViewportCentre
        {
            get { return arCamera != null ? arCamera.pixelRect.center : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f); }
        }

        /// <summary>
        /// Initial-placement raycast: strictly from the viewport centre against a tracked horizontal plane.
        /// No tap coordinates, no plane-centre fallback and no in-front-of-camera fallback.
        /// </summary>
        public bool TryRaycastViewportCentre(out Pose hitPose)
        {
            s_Hits.Clear();
            if (raycastManager != null && IsARActive &&
                raycastManager.Raycast(ViewportCentre, s_Hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds))
            {
                foreach (var hit in s_Hits)
                {
                    if (Vector3.Dot(hit.pose.up, Vector3.up) < 0.7f) continue; // horizontal surfaces only
                    hitPose = hit.pose;
                    return true;
                }
            }
            hitPose = default;
            return false;
        }

        public bool TryRaycastPlane(Vector2 screenPoint, out Pose hitPose)
        {
            s_Hits.Clear();
            if (raycastManager != null && raycastManager.Raycast(screenPoint, s_Hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds))
            {
                hitPose = s_Hits[0].pose;
                return true;
            }

            if (planeManager != null && planeManager.trackables.count > 0)
            {
                foreach (var plane in planeManager.trackables)
                {
                    if (plane.trackingState == TrackingState.Tracking || plane.trackingState == TrackingState.Limited)
                    {
                        hitPose = new Pose(plane.center, plane.transform.rotation);
                        return true;
                    }
                }
            }

            hitPose = default;
            return false;
        }

        public bool TryGetFirstPlanePose(out Pose pose)
        {
            if (planeManager != null && planeManager.trackables.count > 0)
            {
                foreach (var p in planeManager.trackables)
                {
                    if (p.trackingState == TrackingState.Tracking || p.trackingState == TrackingState.Limited)
                    {
                        pose = new Pose(p.center, p.transform.rotation);
                        return true;
                    }
                }
            }
            pose = default;
            return false;
        }

        public bool PlanesVisualsVisible { get; private set; } = true;

        /// <summary>
        /// Toggles visual plane mesh & boundary rendering without stopping underlying plane detection in ARCore.
        /// Keeps planeManager.enabled alive so drag repositioning can continuously find nearby surfaces.
        /// </summary>
        public void SetPlaneVisualsVisible(bool visible)
        {
            PlanesVisualsVisible = visible;
            if (planeManager != null)
            {
                foreach (var p in planeManager.trackables)
                {
                    if (p != null)
                    {
                        var mr = p.GetComponent<MeshRenderer>();
                        if (mr != null) mr.enabled = visible;
                        var lr = p.GetComponent<LineRenderer>();
                        if (lr != null) lr.enabled = visible;
                    }
                }
            }
        }

        public void SetPlanesActive(bool active)
        {
            if (planeManager != null && IsARActive)
            {
                // Keep planeManager alive in background so ARRaycastManager continues finding planes!
                planeManager.enabled = true;
            }
            SetPlaneVisualsVisible(active);
        }

        public void SetARActive(bool active)
        {
            IsARActive = active;

            // Keep the underlying ARSession running in background so the native camera feed
            // and device tracking context are preserved seamlessly during mode switches.
            if (session != null && !session.gameObject.activeSelf) session.gameObject.SetActive(true);
            if (origin != null && !origin.gameObject.activeSelf) origin.gameObject.SetActive(true);

            var flow = AppFlow.I;
            if (flow == null) return;

            if (active)
            {
                if (keyLight != null) keyLight.enabled = true;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.52f, 0.54f, 0.58f);

                arCamera.enabled = true;
                arCamera.tag = "MainCamera";

                if (flow.rig != null)
                {
                    if (flow.rig.main != null) { flow.rig.main.enabled = false; flow.rig.main.tag = "Untagged"; }
                    if (flow.rig.bg != null) flow.rig.bg.enabled = false;
                    if (flow.rig.close != null) flow.rig.close.enabled = false;
                }

                if (flow.lab != null) flow.lab.SetVisible(false);
                if (flow.feed != null) flow.feed.Stop();
                if (flow.model != null) flow.model.cam = arCamera;
                if (flow.labels != null) flow.labels.cam = arCamera;

                Status = "Real AR Active";
            }
            else
            {
                if (keyLight != null) keyLight.enabled = false;

                arCamera.enabled = false;
                arCamera.tag = "Untagged";

                if (flow.rig != null && flow.rig.main != null)
                {
                    flow.rig.main.enabled = true;
                    flow.rig.main.tag = "MainCamera";
                }

                if (flow.lab != null) flow.lab.SetVisible(true);
                if (flow.model != null)
                {
                    flow.model.cam = flow.rig != null ? flow.rig.main : null;
                    // A viewing-mode switch must preserve placement and user manipulation.
                }
                if (flow.labels != null && flow.rig != null) flow.labels.cam = flow.rig.main;

                Status = "Virtual Lab Active";
            }
            Debug.Log("[MuscleAR] " + Status);
        }
    }
}
