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

        public float yaw, scale = 1f;
        public Vector2 tablePos;               // x,z offset on the table
        public float movedDistance, rotatedDegrees, zoomedRatio = 1f;
        public Camera cam;

        float popT = 1f; bool popping;
        HoloDisc disc;

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
            body.transform.SetParent(lying, false);
            body.transform.localPosition = Vector3.zero; body.transform.localRotation = Quaternion.identity;
            m.disc = HoloDisc.Create(go.transform);
            m.Apply();
            return m;
        }

        public void ResetTransform()
        {
            yaw = 0; scale = 1f; tablePos = Vector2.zero; movedDistance = 0; rotatedDegrees = 0; zoomedRatio = 1f;
            Apply();
        }

        public void Show(bool v, bool pop = true)
        {
            root.gameObject.SetActive(v);
            if (v && pop) { popT = 0f; popping = true; }
            else { popping = false; popT = 1f; }
            Apply();
        }

        public void ShowDisc(bool v) { if (disc != null) disc.gameObject.SetActive(v); }

        public void Bind(InputRouter input)
        {
            input.OnDrag += OnDrag;
            input.OnPinch += OnPinch;
            input.OnTwist += OnTwist;
        }

        void OnDrag(Vector2 d)
        {
            if (!root.gameObject.activeInHierarchy || mode == Mode.None) return;
            if (mode == Mode.Move)
            {
                Vector3 fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
                Vector3 right = cam.transform.right; right.y = 0; right.Normalize();
                float dist = Vector3.Distance(cam.transform.position, root.position);
                float k = dist * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, Screen.height * cam.rect.height);
                Vector3 w = (right * d.x + fwd * d.y) * k;
                Vector2 before = tablePos;
                tablePos += new Vector2(w.x, w.z);
                tablePos.x = Mathf.Clamp(tablePos.x, -0.22f, 0.22f);
                tablePos.y = Mathf.Clamp(tablePos.y, -0.5f, 0.5f);
                movedDistance += (tablePos - before).magnitude;
            }
            else
            {
                float dy = -d.x * 0.35f;
                yaw += dy; rotatedDegrees += Mathf.Abs(dy);
            }
            Apply();
        }

        void OnPinch(float f)
        {
            if (!root.gameObject.activeInHierarchy || mode == Mode.None) return;
            scale = Mathf.Clamp(scale * f, 0.45f, 1.9f);
            zoomedRatio = scale;
            Apply();
        }

        void OnTwist(float deg)
        {
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
            root.position = new Vector3(tablePos.x, LabEnvironment.TableTop, tablePos.y);
            root.rotation = Quaternion.Euler(0, yaw, 0);
            root.localScale = Vector3.one * scale * pop;
        }

        /// <summary>Raycast a tap against muscle colliders. Returns the muscle id if one was hit.</summary>
        public bool Pick(Vector2 screenPos, out MuscleId id)
        {
            id = MuscleId.BicepsBrachii;
            Ray r = cam.ScreenPointToRay(screenPos);
            RaycastHit hit;
            if (Physics.Raycast(r, out hit, 20f))
            {
                var h = hit.collider.GetComponentInParent<MuscleHandle>();
                if (h != null) { id = h.id; return true; }
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
