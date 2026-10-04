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

        void Update()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.2f);
            bool tendonView = layer == BodyLayer.Tendon;
            float contract = Mathf.InverseLerp(0f, 145f, flexion);
            foreach (var kv in muscleMats)
            {
                Color bc = baseColors[kv.Value];
                Color c = bc;
                Color e = Color.black;
                if (tendonView) c = Color.Lerp(bc, new Color(0.22f, 0.12f, 0.12f), 0.65f);
                if (kv.Key == MuscleId.BicepsBrachii) c = Color.Lerp(c, new Color(0.92f, 0.28f, 0.22f), contract * 0.6f);
                if (selected.HasValue && selected.Value == kv.Key)
                {
                    c = Color.Lerp(c, new Color(0.25f, 0.5f, 1f), 0.28f + 0.12f * pulse);
                    e = new Color(0.12f, 0.38f, 1f) * (0.55f + 0.7f * pulse);
                }
                else if (hint.HasValue && hint.Value == kv.Key)
                {
                    e = new Color(0.1f, 0.3f, 0.9f) * (0.15f + 0.35f * pulse);
                }
                kv.Value.color = c;
                kv.Value.SetColor("_EmissionColor", e);
            }
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
