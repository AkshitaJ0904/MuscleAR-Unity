using UnityEngine;

namespace MuscleAR
{
    /// <summary>
    /// Owns the placed anatomy model: its table position, yaw, scale and the move / rotate / zoom gestures.
    /// The body is built standing; it is laid supine on the table (head toward -Z, feet toward the camera).
    /// </summary>
    public class ModelController : MonoBehaviour
    {
        public enum Mode { None, Move, Explore }

        public Transform root, lying;
        public HumanBody body;
        public Mode mode = Mode.None;

        public float yaw, scale = 0.40f;
        public Vector2 tablePos;               // x,z offset on the table
        public float movedDistance, rotatedDegrees, zoomedRatio = 1f;
        public Camera cam;

        public Vector3 worldAnchor = new Vector3(0, LabEnvironment.TableTop, 0);
        public Quaternion worldAnchorRot = Quaternion.identity;
        public bool useWorldAnchor = false;

        public ModelDragTarget dragTarget;
        public bool IsRepositioning { get; private set; }

        public bool IsCameraARMode
        {
            get
            {
                if (AppFlow.I != null)
                {
                    return AppFlow.I.viewingMode == AppFlow.ViewingMode.CameraAR;
                }
                var ar = ARManager.I;
                return ar != null ? ar.IsARActive : true;
            }
        }

        public bool CanDragReposition()
        {
            if (!IsCameraARMode) return false;
            if (root == null || !root.gameObject.activeInHierarchy) return false;
            if (!useWorldAnchor) return false;

            if (AppFlow.I != null)
            {
                if (!AppFlow.I.placed) return false;
                var ar = AppFlow.I.ar;
                if (ar == null || !ar.IsARActive || ar.arCamera == null) return false;
            }

            return true;
        }

        public void UpdateDragTargetState()
        {
            if (dragTarget != null)
            {
                bool canDrag = CanDragReposition();
                dragTarget.SetVisible(canDrag);
                if (dragTarget.dragCollider != null)
                {
                    dragTarget.dragCollider.enabled = canDrag;
                }
                if (!canDrag && dragTarget.IsDragging)
                {
                    dragTarget.SetDraggingVisual(false);
                }
            }
        }

        float popT = 1f; bool popping;
        HoloDisc disc;
        Vector3 targetWorldAnchor;
        Vector3 dragOffset;
        bool hasValidDragTarget;
        InputRouter boundInput;
        Vector3 viewOffset;
        Vector3 lyingRestPosition;
        float restingBottom;
        Vector3 restingCentreXZ;

        bool IsEnvironmentMode => AppFlow.I != null &&
            AppFlow.I.viewingMode == AppFlow.ViewingMode.Environment;

        [Header("Beginner Interaction")]
        [Range(0.05f, 0.5f)] public float rotateSensitivity = 0.22f;
        [Range(0.25f, 2f)] public float panSensitivity = 0.85f;
        [Range(0.1f, 1f)] public float minimumScale = 0.35f;
        [Range(1f, 3f)] public float maximumScale = 2.25f;

        public void SetAnchor(Vector3 pos, Quaternion rot, Vector3? camPos = null)
        {
            if (useWorldAnchor) return; // Placement initialization must never replace user manipulation.
            viewOffset = Vector3.zero;
            worldAnchor = pos;
            if (camPos.HasValue)
            {
                Vector3 toCam = camPos.Value - pos;
                toCam.y = 0;
                if (toCam.sqrMagnitude > 0.01f)
                    worldAnchorRot = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                else
                    worldAnchorRot = Quaternion.Euler(0, rot.eulerAngles.y, 0);
            }
            else
            {
                worldAnchorRot = Quaternion.Euler(0, rot.eulerAngles.y, 0);
            }
            useWorldAnchor = true;
            tablePos = Vector2.zero;
            scale = 0.40f; // Tabletop scale (fits ~70cm anatomy model comfortably in view)
            // Put the anatomy's visual (bounds) centre, not just the root pivot, on the hit point.
            // Applied once at placement; later pan/zoom/rotation never recentre.
            worldAnchor = pos - worldAnchorRot * (restingCentreXZ * scale);
            Apply();
            UpdateDragTargetState();
        }

        public void ClearAnchor()
        {
            viewOffset = Vector3.zero;
            CancelReposition();
            useWorldAnchor = false;
            tablePos = Vector2.zero;
            worldAnchor = Vector3.zero;
            yaw = 0f;
            scale = 0.40f;
            movedDistance = 0f;
            rotatedDegrees = 0f;
            zoomedRatio = 1f;
            if (body != null) body.ResetAction();
            Show(false, false);
            ShowDisc(false);
            Apply();
            UpdateDragTargetState();
        }

        public static ModelController Create(Transform parent, HumanBody body, Camera cam)
        {
            var go = new GameObject("AnatomyRoot");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<ModelController>();
            m.root = go.transform; m.body = body; m.cam = cam;
            var lying = new GameObject("Lying").transform;
            lying.SetParent(go.transform, false);
            // standing local (x,y,z) -> world (x, z, -y): face up, head toward -Z. Centre the 1.75 m figure and rest its back on the table.
            lying.localRotation = Quaternion.Euler(-90, 0, 0);
            lying.localPosition = new Vector3(0, 0.1f, 0.875f);
            m.lying = lying;
            m.lyingRestPosition = lying.localPosition;
            body.transform.SetParent(lying, false);
            body.transform.localPosition = Vector3.zero; body.transform.localRotation = Quaternion.identity;
            // Cache the neutral anatomy's lower surface in root space, before action animations.
            // World-up bounds are valid here: the newly created root has no tilt or scale.
            float bottom = float.PositiveInfinity;
            Bounds all = default; bool haveBounds = false;
            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                bottom = Mathf.Min(bottom, go.transform.InverseTransformPoint(renderer.bounds.min).y);
                if (haveBounds) all.Encapsulate(renderer.bounds); else { all = renderer.bounds; haveBounds = true; }
            }
            m.restingBottom = float.IsPositiveInfinity(bottom) ? 0f : bottom;
            // Horizontal visual centre of the whole neutral anatomy, relative to the root pivot.
            if (haveBounds)
            {
                Vector3 centre = go.transform.InverseTransformPoint(all.center);
                m.restingCentreXZ = new Vector3(centre.x, 0f, centre.z);
            }
            m.disc = HoloDisc.Create(go.transform);
            m.dragTarget = ModelDragTarget.Create(go.transform, m);
            m.Apply();
            return m;
        }

        public void ResetTransform()
        {
            CancelReposition();
            viewOffset = Vector3.zero;
            yaw = 0; scale = useWorldAnchor ? 0.40f : 1f; tablePos = Vector2.zero; movedDistance = 0; rotatedDegrees = 0; zoomedRatio = 1f;
            Apply();
        }

        public void Show(bool v, bool pop = true)
        {
            root.gameObject.SetActive(v);
            // Visibility and screen transitions do not animate the user's scale.
            popping = false; popT = 1f;
            Apply();
        }

        public void ShowDisc(bool v)
        {
            if (disc != null) disc.gameObject.SetActive(v);
            if (dragTarget != null)
            {
                bool canDrag = v && CanDragReposition();
                dragTarget.SetVisible(canDrag);
                if (dragTarget.dragCollider != null) dragTarget.dragCollider.enabled = canDrag;
                if (!canDrag && dragTarget.IsDragging) dragTarget.SetDraggingVisual(false);
            }
        }

        public void Bind(InputRouter input)
        {
            boundInput = input;
            input.CanManipulate = HitModel;
            input.OnDrag += OnDrag;
            input.OnPan += OnPan;
            input.OnPinch += OnPinch;
            input.OnTwist += OnTwist;
            // Direct gestures own manipulation; the surface ring no longer captures pointer drags.
            input.OnTwoFingerBegan += CancelReposition;
        }

        public Camera GetActiveCamera()
        {
            var ar = ARManager.I;
            if (ar != null && ar.IsARActive && ar.arCamera != null && ar.arCamera.isActiveAndEnabled)
                return ar.arCamera;
            if (cam != null && cam.isActiveAndEnabled)
                return cam;
            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return Camera.main;
            return cam != null ? cam : ar?.arCamera;
        }

        void OnPointerDown(Vector2 screenPos)
        {
            if (!root.gameObject.activeInHierarchy) return;
            if (!CanDragReposition()) return;

            Camera activeCam = GetActiveCamera();
            if (activeCam == null) return;

            Ray r = activeCam.ScreenPointToRay(screenPos);
            RaycastHit[] hits = Physics.RaycastAll(r, 40f);
            if (hits == null || hits.Length == 0) return;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // 1. If any muscle collider was hit, prioritize muscle exploration/tap and do NOT drag
            foreach (var hit in hits)
            {
                var h = hit.collider.GetComponentInParent<MuscleHandle>();
                if (h != null && MuscleDB.IsTarget(h.id))
                {
                    return; // User touched a target muscle
                }
            }

            // 2. Check if the designated drag area (reposition base / ring) was touched
            foreach (var hit in hits)
            {
                var dt = hit.collider.GetComponentInParent<ModelDragTarget>();
                if (dt != null)
                {
                    StartReposition(screenPos);
                    return;
                }
            }
        }

        void OnPointerMove(Vector2 screenPos)
        {
            if (IsRepositioning)
            {
                UpdateReposition(screenPos);
            }
        }

        void OnPointerUp(Vector2 screenPos)
        {
            if (IsRepositioning)
            {
                EndReposition(screenPos);
            }
        }

        /// <summary>
        /// Begins dragging the model to reposition it on detected surfaces.
        /// Preserves existing rotation and scale while calculating initial grab offset.
        /// </summary>
        public void StartReposition(Vector2 screenPos)
        {
            if (!CanDragReposition()) return;
            if (root == null || !root.gameObject.activeInHierarchy) return;

            Camera activeCam = GetActiveCamera();
            if (activeCam == null) return;

            IsRepositioning = true;
            if (boundInput != null) boundInput.SuppressTap = true;

            var ar = ARManager.I;
            if (ar != null && ar.IsARActive)
            {
                // AR Foundation surface raycast
                Pose hitPose;
                if (ar.RaycastSurface(screenPos, out hitPose))
                {
                    dragOffset = root.position - hitPose.position;
                    dragOffset.y = 0; // Surface-relative offset
                    targetWorldAnchor = root.position;
                    hasValidDragTarget = true;
                }
                else
                {
                    dragOffset = Vector3.zero;
                    targetWorldAnchor = root.position;
                    hasValidDragTarget = true;
                }
                // Temporarily illuminate AR planes so user sees all valid surfaces
                ar.SetPlaneVisualsVisible(true);
            }
            else
            {
                // Virtual Lab / Editor mode: raycast against tabletop plane
                Plane tablePlane = new Plane(Vector3.up, new Vector3(0, LabEnvironment.TableTop, 0));
                Ray ray = activeCam.ScreenPointToRay(screenPos);
                if (tablePlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    dragOffset = root.position - hitPoint;
                    dragOffset.y = 0;
                    targetWorldAnchor = root.position;
                    hasValidDragTarget = true;
                }
                else
                {
                    dragOffset = Vector3.zero;
                    targetWorldAnchor = root.position;
                    hasValidDragTarget = true;
                }
            }

            if (dragTarget != null) dragTarget.SetDraggingVisual(true);
            if (disc != null) disc.gameObject.SetActive(true);
        }

        /// <summary>
        /// Updates the model's target position while dragging.
        /// Restricts movement strictly to valid detected AR surfaces, keeping last valid position when off-plane.
        /// </summary>
        public void UpdateReposition(Vector2 screenPos)
        {
            if (!CanDragReposition())
            {
                CancelReposition();
                return;
            }
            if (!IsRepositioning || root == null) return;

            Camera activeCam = GetActiveCamera();
            if (activeCam == null) return;

            var ar = ARManager.I;
            if (ar != null && ar.IsARActive)
            {
                Pose hitPose;
                // Strict surface raycasting against detected planes
                if (ar.RaycastSurface(screenPos, out hitPose))
                {
                    targetWorldAnchor = hitPose.position + dragOffset;
                    hasValidDragTarget = true;
                }
                // When finger is off valid surfaces, targetWorldAnchor is preserved (no jumping or disappearance)
            }
            else
            {
                // Virtual Lab / Editor fallback
                Plane tablePlane = new Plane(Vector3.up, new Vector3(0, LabEnvironment.TableTop, 0));
                Ray ray = activeCam.ScreenPointToRay(screenPos);
                if (tablePlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    Vector3 newPos = hitPoint + dragOffset;
                    newPos.x = Mathf.Clamp(newPos.x, -LabEnvironment.TableW * 0.48f, LabEnvironment.TableW * 0.48f);
                    newPos.z = Mathf.Clamp(newPos.z, -LabEnvironment.TableL * 0.48f, LabEnvironment.TableL * 0.48f);
                    newPos.y = LabEnvironment.TableTop;
                    targetWorldAnchor = newPos;
                    hasValidDragTarget = true;
                }
            }
        }

        /// <summary>
        /// Completes the drag repositioning, leaving the model anchored at its final valid position.
        /// </summary>
        public void EndReposition(Vector2 screenPos)
        {
            if (!IsRepositioning) return;
            IsRepositioning = false;

            if (hasValidDragTarget)
            {
                if (useWorldAnchor)
                {
                    worldAnchor = targetWorldAnchor;
                    tablePos = Vector2.zero;
                }
                else
                {
                    tablePos = new Vector2(targetWorldAnchor.x, targetWorldAnchor.z);
                }
                Apply();
            }

            if (dragTarget != null) dragTarget.SetDraggingVisual(false);

            var ar = ARManager.I;
            if (ar != null && ar.IsARActive)
            {
                ar.SetPlaneVisualsVisible(false);
            }

            if (boundInput != null) boundInput.SuppressTap = true;

            if (MuscleAudioController.Instance != null)
                MuscleAudioController.Instance.PlayClick();
        }

        public void CancelReposition()
        {
            if (!IsRepositioning && (dragTarget == null || !dragTarget.IsDragging)) return;
            IsRepositioning = false;
            hasValidDragTarget = false;
            if (boundInput != null) boundInput.SuppressTap = false;
            if (dragTarget != null) dragTarget.SetDraggingVisual(false);
            var ar = ARManager.I;
            if (ar != null && ar.IsARActive) ar.SetPlaneVisualsVisible(false);
        }

        void OnDrag(Vector2 d)
        {
            if (!root.gameObject.activeInHierarchy || mode == Mode.None) return;
            if (IsRepositioning) return; // Suppress gesture drag when drag-repositioning
            float degrees = Mathf.Clamp(-d.x * rotateSensitivity, -8f, 8f);
            yaw += degrees; rotatedDegrees += Mathf.Abs(degrees);
            Apply();
        }

        void OnPan(Vector2 d)
        {
            // Both two-finger drag and right-mouse drag arrive here. Keep AR offsets intact.
            if (IsEnvironmentMode) return;
            if (!root.gameObject.activeInHierarchy || mode == Mode.None) return;
            if (IsRepositioning) CancelReposition();

            Camera activeCam = GetActiveCamera();
            Vector3 right = activeCam != null ? activeCam.transform.right : Vector3.right;
            Vector3 up = activeCam != null ? activeCam.transform.up : Vector3.forward;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            if (up.sqrMagnitude < 0.001f) up = Vector3.forward;
            right.Normalize(); up.Normalize();

            float dist = activeCam != null ? Vector3.Distance(activeCam.transform.position, root.position) : 1.5f;
            float fov = activeCam != null ? activeCam.fieldOfView : 50f;
            float screenHeight = activeCam != null ? Mathf.Max(1f, Screen.height * activeCam.rect.height) : Mathf.Max(1f, Screen.height);
            float metresPerPixel = dist * 2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / screenHeight;
            Vector3 worldDelta = (right * d.x + up * d.y) * metresPerPixel * panSensitivity;
            // Store a world-space offset from the tracked placement. Never reparent or move the AR camera.
            Vector3 before = viewOffset;
            viewOffset = Vector3.ClampMagnitude(viewOffset + worldDelta, Mathf.Max(1f, dist));
            movedDistance += Vector3.Distance(viewOffset, before);
            Apply();
        }

        void OnPinch(float f)
        {
            if (IsRepositioning) CancelReposition();
            if (!root.gameObject.activeInHierarchy || mode == Mode.None) return;
            float safeFactor = Mathf.Clamp(f, 0.94f, 1.06f);
            scale = Mathf.Clamp(scale * safeFactor, minimumScale, maximumScale);
            zoomedRatio = scale;
            Apply();
        }

        void OnTwist(float deg)
        {
            if (IsRepositioning) CancelReposition();
            if (!root.gameObject.activeInHierarchy || mode == Mode.None) return;
            yaw += deg; rotatedDegrees += Mathf.Abs(deg);
            Apply();
        }

        void Update()
        {
            if (popping)
            {
                popT += Time.deltaTime / 0.65f;
                if (popT >= 1f) { popT = 1f; popping = false; }
                Apply();
            }

            if (IsRepositioning)
            {
                if (!CanDragReposition())
                {
                    CancelReposition();
                    return;
                }

                if (hasValidDragTarget)
                {
                    // Smoothly follow the detected surface plane with frame-rate independent exponential smoothing
                    float smoothFactor = 1f - Mathf.Exp(-18f * Time.deltaTime);
                    Vector3 prevPos = root.position;
                    Vector3 smoothedPos = Vector3.Lerp(root.position, targetWorldAnchor, smoothFactor);

                    if (useWorldAnchor)
                    {
                        worldAnchor = smoothedPos;
                        tablePos = Vector2.zero;
                    }
                    else
                    {
                        tablePos = new Vector2(smoothedPos.x, smoothedPos.z);
                    }
                    Apply();
                    movedDistance += Vector3.Distance(root.position, prevPos);
                }
            }
        }

        public void Apply()
        {
            float pop = 1f;
            if (popping || popT < 1f)
            {
                float t = Mathf.Clamp01(popT);
                pop = t < 1f ? 1f + Mathf.Sin(t * Mathf.PI) * 0.06f - (1f - t) * (1f - t) * (1f - t) : 1f; // soft overshoot
                pop = Mathf.Max(0.001f, pop);
            }
            if (useWorldAnchor)
            {
                root.position = worldAnchor + worldAnchorRot * new Vector3(tablePos.x, 0, tablePos.y);
                root.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * worldAnchorRot;
            }
            else
            {
                root.position = new Vector3(tablePos.x, LabEnvironment.TableTop, tablePos.y);
                root.rotation = Quaternion.AngleAxis(yaw, Vector3.up);
            }
            root.position += viewOffset;
            root.localScale = Vector3.one * scale * pop;
            if (lying != null)
            {
                Vector3 childPosition = lyingRestPosition;
                if (IsEnvironmentMode && AppFlow.I.lab != null)
                {
                    // A separate, fixed table pivot for the Environment presentation. Neither
                    // the table nor stored AR placement follows rotation, pinch, or pan input.
                    root.position = AppFlow.I.lab.TableSurfaceCenter;
                    childPosition.y -= restingBottom;
                }
                lying.localPosition = childPosition;
            }
        }

        public bool HitModel(Vector2 screenPos)
        {
            if (root == null || !root.gameObject.activeInHierarchy || mode == Mode.None) return false;
            Camera camera = GetActiveCamera();
            if (camera == null) return false;
            foreach (var hit in Physics.RaycastAll(camera.ScreenPointToRay(screenPos), 40f))
                if (hit.collider.GetComponentInParent<MuscleHandle>() != null ||
                    (body != null && hit.collider.transform.IsChildOf(body.transform))) return true;
            return false;
        }

        /// <summary>Raycast a tap against muscle colliders. Returns the muscle id if one was hit.</summary>
        public bool Pick(Vector2 screenPos, out MuscleId id)
        {
            id = MuscleId.BicepsBrachii;
            Camera activeCam = GetActiveCamera();
            if (activeCam == null) return false;
            Ray r = activeCam.ScreenPointToRay(screenPos);
            RaycastHit[] hits = Physics.RaycastAll(r, 40f);
            if (hits == null || hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var h = hit.collider.GetComponentInParent<MuscleHandle>();
                if (h != null && MuscleDB.IsTarget(h.id))
                {
                    id = h.id;
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Flat holographic floor disc with concentric rings and tick marks around the model.</summary>
    public class HoloDisc : MonoBehaviour
    {
        Material mat;

        public static HoloDisc Create(Transform parent)
        {
            var go = new GameObject("HoloDisc");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<HoloDisc>();
            int s = 512;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = 0f;
                    a = Mathf.Max(a, Ring(r, 0.96f, 0.006f) * 0.9f);
                    a = Mathf.Max(a, Ring(r, 0.80f, 0.004f) * 0.6f);
                    a = Mathf.Max(a, Ring(r, 0.58f, 0.003f) * 0.35f);
                    float ang = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                    if (r > 0.9f && r < 0.96f && Mathf.Abs(Mathf.Repeat(ang, 5f) - 2.5f) > 2.2f) a = Mathf.Max(a, 0.8f);
                    if (r > 0.80f && r < 0.84f && Mathf.Abs(Mathf.Repeat(ang, 30f) - 15f) > 14.2f) a = Mathf.Max(a, 0.9f);
                    // crosshair
                    if ((Mathf.Abs(u) < 0.003f || Mathf.Abs(v) < 0.003f) && r < 0.96f && r > 0.5f) a = Mathf.Max(a, 0.35f);
                    px[y * s + x] = new Color(0.35f, 0.7f, 1f, a);
                }
            t.SetPixels(px); t.Apply(true);
            d.mat = Mats.Unlit(new Color(1, 1, 1, 0.55f), t);
            go.AddComponent<MeshFilter>().sharedMesh = DiscQuad();
            go.AddComponent<MeshRenderer>().sharedMaterial = d.mat;
            go.transform.localPosition = new Vector3(0, 0.004f, 0);
            go.transform.localScale = new Vector3(2.3f, 1, 2.3f);
            return d;
        }

        static float Ring(float r, float at, float w) { return Mathf.Clamp01(1f - Mathf.Abs(r - at) / w); }

        static Mesh DiscQuad()
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            return m;
        }

        void Update()
        {
            transform.Rotate(0, Time.deltaTime * 6f, 0, Space.Self);
            float p = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.6f);
            mat.color = new Color(1, 1, 1, 0.35f + 0.25f * p);
        }
    }
}
