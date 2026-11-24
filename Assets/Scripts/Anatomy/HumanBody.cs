using System.Collections.Generic;
using UnityEngine;

namespace MuscleAR
{
    public enum BodyLayer { Skin, Muscle, Tendon, Bone }

    /// <summary>Attached to every tappable muscle mesh.</summary>
    public class MuscleHandle : MonoBehaviour
    {
        public MuscleId id;
        public int side;
        [System.NonSerialized] public Vector3 initialLocalScale = Vector3.one;

        void Awake()
        {
            initialLocalScale = transform.localScale;
        }
    }

    public class FlexPivot : MonoBehaviour
    {
        public Vector3 axis = Vector3.right;
    }

    /// <summary>Runtime control of the procedural anatomy model: layers, selection, highlight, flexion.</summary>
    public class HumanBody : MonoBehaviour
    {
        public Transform skinRoot, muscleRoot, tendonRoot, boneRoot, organRoot, faceRoot;
        public Dictionary<MuscleId, Material> muscleMats = new Dictionary<MuscleId, Material>();
        public Dictionary<MuscleId, List<MuscleHandle>> handles = new Dictionary<MuscleId, List<MuscleHandle>>();
        public Dictionary<string, Transform> anchors = new Dictionary<string, Transform>();
        public List<FlexPivot> pivots = new List<FlexPivot>();
        public List<Renderer> skinRenderers = new List<Renderer>();
        public Material tendonMat, skinMat, skinGhostMat, baseMat;

        BodyLayer layer = BodyLayer.Muscle;
        MuscleId? selected, hint;
        float flexion;
        readonly Dictionary<Material, Color> baseColors = new Dictionary<Material, Color>();
        class ActionMesh
        {
            public Mesh mesh; public Transform transform; public Vector3[] rest, working;
            public MeshCollider collider;
            public bool deformed;
        }
        readonly List<ActionMesh> actionMeshes = new List<ActionMesh>();
        readonly Dictionary<string, Transform> actionJoints = new Dictionary<string, Transform>();
        bool actionDeformed;
        class ShoulderAssembly
        {
            public Transform pivot; public int side; public Quaternion neutral;
        }
        readonly List<ShoulderAssembly> shoulderAssemblies = new List<ShoulderAssembly>();

        public void RegisterShoulderAssembly(Transform pivot, int side)
        {
            shoulderAssemblies.Add(new ShoulderAssembly { pivot = pivot, side = side, neutral = pivot.localRotation });
        }

        void ResetShoulders()
        {
            foreach (var assembly in shoulderAssemblies)
                assembly.pivot.localRotation = assembly.neutral;
        }

        public void InitializeActionRig()
        {
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                // Each procedural mesh is owned by this body; preserve an immutable neutral copy.
                var mesh = Instantiate(filter.sharedMesh);
                filter.sharedMesh = mesh;
                var vertices = mesh.vertices;
                actionMeshes.Add(new ActionMesh { mesh = mesh, transform = filter.transform,
                    rest = vertices, working = new Vector3[vertices.Length], collider = filter.GetComponent<MeshCollider>() });
            }
            AddActionJoint("ShoulderL", new Vector3(0.19f, 1.43f, 0));
            AddActionJoint("ShoulderR", new Vector3(-0.19f, 1.43f, 0));
            AddActionJoint("Lumbar", new Vector3(0, 1.0f, -0.04f));
            for (int side = -1; side <= 1; side += 2)
            {
                AddActionJoint("Hip" + side, new Vector3(side * 0.09f, 0.90f, 0));
                AddActionJoint("Knee" + side, new Vector3(side * 0.085f, 0.50f, 0.015f));
                AddActionJoint("Ankle" + side, new Vector3(side * 0.085f, 0.085f, -0.015f));
            }
        }

        void AddActionJoint(string name, Vector3 position)
        {
            var joint = new GameObject("ActionJoint_" + name).transform;
            joint.SetParent(transform, false); joint.localPosition = position;
            actionJoints.Add(name, joint);
        }

        static float Blend(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        static Vector3 Around(Vector3 point, Vector3 pivot, Quaternion rotation, float weight) =>
            Vector3.Lerp(point, pivot + rotation * (point - pivot), weight);

        /// <summary>Neutral-pose weighted child geometry deformation; never writes the model/placement transform.</summary>
        public void DemonstrateAction(MuscleId id, float amount)
        {
            amount = Mathf.Clamp01(amount);
            ResetShoulders();
            Flexion = 0f;
            ActionAmount = amount;
            if (id == MuscleId.BicepsBrachii) { RestoreActionMeshes(); Flexion = amount * 145f; return; }
            if (id == MuscleId.Deltoid || id == MuscleId.PectoralisMajor)
            {
                RestoreActionMeshes();
                foreach (var assembly in shoulderAssemblies)
                {
                    // Mirror across anatomical X. At zero both assemblies are exactly neutral.
                    Quaternion rotation = id == MuscleId.Deltoid
                        ? Quaternion.AngleAxis(assembly.side * amount * 60f, Vector3.forward)
                        : Quaternion.AngleAxis(-assembly.side * amount * 30f, Vector3.up) *
                            Quaternion.AngleAxis(-amount * 65f, Vector3.right);
                    assembly.pivot.localRotation = assembly.neutral * rotation;
                }
                return;
            }
            Quaternion trunkRotation = Quaternion.AngleAxis(amount * 22f, Vector3.right);
            Quaternion kneeRotation = Quaternion.AngleAxis(75f * (1f - amount), Vector3.right);
            Quaternion hipRotation = Quaternion.AngleAxis(-55f, Vector3.right);
            Quaternion ankleRotation = Quaternion.AngleAxis(amount * 35f, Vector3.right);
            foreach (var data in actionMeshes)
            {
                Matrix4x4 toBody = transform.worldToLocalMatrix * data.transform.localToWorldMatrix;
                Matrix4x4 toMesh = toBody.inverse;
                bool changed = false;
                for (int i = 0; i < data.rest.Length; i++)
                {
                    Vector3 p = toBody.MultiplyPoint3x4(data.rest[i]);
                    int side = p.x >= 0f ? 1 : -1;
                    if (id == MuscleId.RectusAbdominis)
                        p = Around(p, actionJoints["Lumbar"].localPosition,
                            trunkRotation, Blend(0.98f, 1.22f, p.y));
                    else if (id == MuscleId.QuadricepsFemoris)
                    {
                        float leg = 1f - Blend(0.86f, 0.99f, p.y);
                        float lowerLeg = 1f - Blend(0.46f, 0.55f, p.y);
                        p = Around(p, actionJoints[side > 0 ? "Knee1" : "Knee-1"].localPosition,
                            kneeRotation, lowerLeg);
                        // Hip support lifts the knee so flexion/extension clears the examination table.
                        p = Around(p, actionJoints[side > 0 ? "Hip1" : "Hip-1"].localPosition,
                            hipRotation, leg);
                    }
                    else if (id == MuscleId.Gastrocnemius)
                        p = Around(p, actionJoints[side > 0 ? "Ankle1" : "Ankle-1"].localPosition,
                            ankleRotation, 1f - Blend(0.065f, 0.14f, p.y));
                    data.working[i] = toMesh.MultiplyPoint3x4(p);
                    changed |= (data.working[i] - data.rest[i]).sqrMagnitude > 0.00000001f;
                }
                if (!changed && !data.deformed) continue;
                data.mesh.vertices = data.working;
                data.mesh.RecalculateNormals(); data.mesh.RecalculateBounds();
                if (data.collider != null) data.collider.sharedMesh = data.mesh;
                data.deformed = changed;
            }
            actionDeformed = true;
        }

        void RestoreActionMeshes()
        {
            if (!actionDeformed) return;
            foreach (var data in actionMeshes)
            {
                data.mesh.vertices = data.rest; data.mesh.RecalculateNormals(); data.mesh.RecalculateBounds();
                if (data.collider != null) data.collider.sharedMesh = data.mesh;
                data.deformed = false;
            }
            actionDeformed = false;
        }

        public BodyLayer Layer { get { return layer; } }
        public MuscleId? Selected { get { return selected; } }

        public void RegisterMaterial(MuscleId id, Material m)
        {
            muscleMats[id] = m;
            baseColors[m] = m.color;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", Color.black);
        }

        public void RegisterHandle(MuscleHandle h)
        {
            h.initialLocalScale = h.transform.localScale;
            List<MuscleHandle> l;
            if (!handles.TryGetValue(h.id, out l)) handles[h.id] = l = new List<MuscleHandle>();
            l.Add(h);
        }

        public void SetLayer(BodyLayer l)
        {
            layer = l;
            skinRoot.gameObject.SetActive(l == BodyLayer.Skin || l == BodyLayer.Bone);
            foreach (var r in skinRenderers) r.sharedMaterial = (l == BodyLayer.Skin) ? skinMat : skinGhostMat;
            muscleRoot.gameObject.SetActive(l == BodyLayer.Muscle || l == BodyLayer.Tendon);
            tendonRoot.gameObject.SetActive(l == BodyLayer.Muscle || l == BodyLayer.Tendon);
            boneRoot.gameObject.SetActive(l == BodyLayer.Bone);
            organRoot.gameObject.SetActive(l == BodyLayer.Bone);
            faceRoot.gameObject.SetActive(true);
            // tendons glow in the tendon layer; muscles desaturate
            tendonMat.SetColor("_EmissionColor", l == BodyLayer.Tendon ? new Color(0.55f, 0.6f, 0.7f) : Color.black);
            if (l == BodyLayer.Tendon) tendonMat.EnableKeyword("_EMISSION");
        }

        public void Select(MuscleId? id) { selected = id; }
        public void SetHint(MuscleId? id) { hint = id; }

        public float Flexion
        {
            get { return flexion; }
            set
            {
                flexion = Mathf.Clamp(value, 0f, 145f);
                foreach (var p in pivots) p.transform.localRotation = Quaternion.AngleAxis(flexion, p.axis);
            }
        }

        MuscleId? lastSelected, lastHint;
        BodyLayer lastLayer = (BodyLayer)(-1);

        float actionAmount;
        public float ActionAmount
        {
            get { return actionAmount; }
            set
            {
                actionAmount = Mathf.Clamp01(value);
            }
        }

        public void ResetAction()
        {
            ResetShoulders();
            RestoreActionMeshes();
            actionAmount = 0f;
            Flexion = 0f;
            foreach (var kv in handles)
            {
                foreach (var h in kv.Value)
                    if (h != null) h.transform.localScale = h.initialLocalScale;
            }
        }

        [Header("Highlight Settings")]
        [Tooltip("Blink frequency in cycles per second (Hz)")]
        [Range(0.5f, 5f)]
        public float blinkSpeed = 2.0f;

        [Tooltip("Peak emission intensity of the highlight")]
        [Range(0.2f, 4f)]
        public float highlightIntensity = 2.2f;

        [Tooltip("Highlight color for the selected muscle")]
        public Color highlightColor = new Color(0.05f, 0.95f, 1f);

        void Update()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.2f);
            bool tendonView = layer == BodyLayer.Tendon;

            // When the structural layer changes, update materials once
            if (layer != lastLayer)
            {
                lastLayer = layer;
                foreach (var kv in muscleMats)
                {
                    Color bc = baseColors[kv.Value];
                    kv.Value.color = tendonView ? Color.Lerp(bc, new Color(0.22f, 0.12f, 0.12f), 0.65f) : bc;
                    kv.Value.SetColor("_EmissionColor", Color.black);
                }
            }

            // Restore previously selected muscle if selection changed
            if (lastSelected.HasValue && lastSelected != selected && muscleMats.TryGetValue(lastSelected.Value, out var prevMat))
            {
                Color bc = baseColors[prevMat];
                prevMat.color = tendonView ? Color.Lerp(bc, new Color(0.22f, 0.12f, 0.12f), 0.65f) : bc;
                prevMat.SetColor("_EmissionColor", Color.black);
            }
            if (lastHint.HasValue && lastHint != hint && lastHint != selected && muscleMats.TryGetValue(lastHint.Value, out var prevHintMat))
            {
                prevHintMat.SetColor("_EmissionColor", Color.black);
            }
            lastSelected = selected;
            lastHint = hint;

            // Update only the active selected muscle material: always pulses smoothly
            if (selected.HasValue && muscleMats.TryGetValue(selected.Value, out var activeMat))
            {
                Color bc = baseColors[activeMat];
                // Smooth, repeating pulse at ~2 cycles per second
                float rawT = 0.5f + 0.5f * Mathf.Sin(Time.time * blinkSpeed * Mathf.PI * 2f);
                float smoothT = Mathf.SmoothStep(0f, 1f, rawT);

                // Keep a persistent high-contrast selection and add a restrained pulse.
                // The old zero-to-peak pulse periodically returned to the red anatomy colour,
                // which made the selected muscle disappear against real-world backgrounds.
                float visibleT = Mathf.Lerp(0.55f, 0.9f, smoothT);
                float emissionT = Mathf.Lerp(0.35f, 1f, smoothT);
                Color c = Color.Lerp(bc, highlightColor, visibleT);
                Color e = highlightColor * (emissionT * highlightIntensity);

                if (actionAmount > 0.01f)
                {
                    c = Color.Lerp(c, new Color(1f, 0.45f, 0.35f), actionAmount * 0.6f);
                    e = Color.Lerp(e, new Color(1f, 0.3f, 0.2f), actionAmount * 0.7f);
                }
                activeMat.color = c;
                activeMat.SetColor("_EmissionColor", e);
            }

            if (hint.HasValue && hint != selected && muscleMats.TryGetValue(hint.Value, out var hMat))
            {
                hMat.SetColor("_EmissionColor", new Color(0.1f, 0.3f, 0.9f) * (0.15f + 0.35f * pulse));
            }
        }

        public void RestoreSelectedNormalMaterial()
        {
            if (selected.HasValue && muscleMats.TryGetValue(selected.Value, out var mat))
            {
                if (baseColors.TryGetValue(mat, out var bc))
                {
                    mat.color = (layer == BodyLayer.Tendon) ? Color.Lerp(bc, new Color(0.22f, 0.12f, 0.12f), 0.65f) : bc;
                }
                mat.SetColor("_EmissionColor", Color.black);
            }
        }

        public void RestoreAllMaterials()
        {
            foreach (var kv in muscleMats)
            {
                if (baseColors.TryGetValue(kv.Value, out var bc))
                    kv.Value.color = (layer == BodyLayer.Tendon) ? Color.Lerp(bc, new Color(0.22f, 0.12f, 0.12f), 0.65f) : bc;
                kv.Value.SetColor("_EmissionColor", Color.black);
            }
        }

        void OnDisable()
        {
            RestoreAllMaterials();
        }

        void OnDestroy()
        {
            RestoreAllMaterials();
        }

        public Bounds BoundsOf(MuscleId id)
        {
            var b = new Bounds(transform.position, Vector3.zero);
            bool init = false;
            List<MuscleHandle> l;
            if (!handles.TryGetValue(id, out l)) return b;
            foreach (var h in l)
            {
                var r = h.GetComponent<Renderer>();
                if (r == null) continue;
                if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Bind once to a fixed vertex on one muscle mesh. Follow its transform and
        /// existing action deformation data without recomputing world renderer bounds.</summary>
        public System.Func<Vector3> CreateLabelAnchor(MuscleId id, int side)
        {
            if (!handles.TryGetValue(id, out var list)) return () => transform.position;
            MuscleHandle chosen = null; Mesh mesh = null;
            foreach (var handle in list)
            {
                if (handle == null || handle.side != side) continue;
                var filter = handle.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                if (mesh == null || filter.sharedMesh.vertexCount > mesh.vertexCount)
                { chosen = handle; mesh = filter.sharedMesh; }
            }
            if (chosen == null) return () => transform.position;
            var target = chosen.transform;
            var data = actionMeshes.Find(entry => entry.transform == target);
            Vector3[] neutral = data != null ? data.rest : mesh.vertices;
            Vector3 centre = mesh.bounds.center;
            int index = 0; float nearest = float.PositiveInfinity;
            for (int i = 0; i < neutral.Length; i++)
            {
                float distance = (neutral[i] - centre).sqrMagnitude;
                if (distance < nearest) { nearest = distance; index = i; }
            }
            Vector3 fixedPoint = neutral.Length > 0 ? neutral[index] : centre;
            return () => target == null ? transform.position : target.TransformPoint(
                data != null && data.deformed ? data.working[index] : fixedPoint);
        }

        /// <summary>World-space centre of one side's muscle (side: +1 anatomical left (+X), -1 right).</summary>
        public Vector3 MuscleCenter(MuscleId id, int side)
        {
            List<MuscleHandle> l;
            if (!handles.TryGetValue(id, out l)) return transform.position;
            Bounds b = new Bounds(); bool init = false;
            foreach (var h in l)
            {
                if (side != 0 && h.side != side) continue;
                var r = h.GetComponent<Renderer>();
                if (r == null) continue;
                if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
            }
            return init ? b.center : transform.position;
        }

        public Vector3 AnchorPos(string key)
        {
            Transform t;
            return anchors.TryGetValue(key, out t) ? t.position : transform.position;
        }
    }
}
