using System.Collections.Generic;
using UnityEngine;

namespace MuscleAR
{
    /// <summary>
    /// The virtual "camera feed": an anatomy lab / study room with a walnut table.
    /// Stands in for the real camera image in the Editor; hidden when the live webcam feed is used.
    /// </summary>
    public class LabEnvironment : MonoBehaviour
    {
        public const float TableTop = 0.78f, TableW = 1.0f, TableL = 2.4f;
        public enum Look { Lab, Day }

        Light sun, lamp, overhead, glow;
        Material windowMat;
        readonly List<Renderer> renderers = new List<Renderer>();
        float blend; float target;
        Color ambLab = new Color(0.075f, 0.085f, 0.11f), ambDay = new Color(0.42f, 0.43f, 0.47f);

        public static LabEnvironment Create(Transform parent)
        {
            var go = new GameObject("LabEnvironment");
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<LabEnvironment>();
            e.Build();
            e.SetLook(Look.Lab, true);
            return e;
        }

        Transform Cube(string name, Vector3 pos, Vector3 size, Material m, Transform parent = null, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            g.transform.SetParent(parent ?? transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = size;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; renderers.Add(r);
            return g.transform;
        }

        Transform Cyl(string name, Vector3 pos, float radius, float height, Material m, Transform parent = null, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            g.transform.SetParent(parent ?? transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; renderers.Add(r);
            return g.transform;
        }

        Transform Sphere(string name, Vector3 pos, Vector3 radii, Material m, Transform parent = null)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent ?? transform, false);
            g.transform.localPosition = pos; g.transform.localScale = radii;
            g.AddComponent<MeshFilter>().sharedMesh = MeshFactory.UnitSphere();
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; renderers.Add(r);
            return g.transform;
        }

        static Texture2D MonitorTexture()
        {
            int w = 256, h = 160;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float g = ((x % 16 == 0) || (y % 16 == 0)) ? 0.12f : 0.0f;
                    Color c = new Color(0.02f + g * 0.3f, 0.07f + g, 0.15f + g * 1.4f, 1);
                    // anatomical silhouette
                    float fx = (x - 70f) / 14f, fy = (y - 80f) / 60f;
                    bool body = (Mathf.Abs(fx) < 0.9f && Mathf.Abs(fy) < 1f) || (Mathf.Abs(fx) < 2.4f && fy > 0.35f && fy < 0.55f)
                                || (Mathf.Abs(fx - 0.6f) < 0.35f && fy < -0.4f && Mathf.Abs(fy) < 1.1f) || (Mathf.Abs(fx + 0.6f) < 0.35f && fy < -0.4f && Mathf.Abs(fy) < 1.1f)
                                || (fx * fx + (fy - 1.02f) * (fy - 1.02f) * 2.2f < 0.7f);
                    if (body) c = new Color(0.65f, 0.16f, 0.15f, 1) * (0.8f + 0.2f * Mathf.Sin(x * 0.9f));
                    if (x > 140 && x < 240 && ((y / 14) % 2 == 0) && y > 20 && y < 140)
                        c = Color.Lerp(c, new Color(0.25f, 0.7f, 1f), (x - 140) < (40 + 55 * Mathf.PerlinNoise(y * 0.1f, 3f)) ? 0.85f : 0.1f);
                    px[y * w + x] = c;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        static Texture2D WindowTexture()
        {
            int w = 128, h = 128;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = y / (float)h;
                    Color sky = Color.Lerp(new Color(0.75f, 0.85f, 0.95f), new Color(0.95f, 0.97f, 1f), v);
                    float tree = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                    Color green = Color.Lerp(new Color(0.25f, 0.45f, 0.2f), new Color(0.5f, 0.7f, 0.35f), tree);
                    px[y * w + x] = (v < 0.55f + 0.2f * tree) ? green : sky;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        void Build()
        {
            var floorM = Mats.Make(new Color(0.16f, 0.13f, 0.11f), 0.45f);
            var wallM = Mats.Make(new Color(0.20f, 0.21f, 0.23f), 0.15f);
            var darkM = Mats.Make(new Color(0.08f, 0.09f, 0.1f), 0.5f, 0.4f);
            var metalM = Mats.Make(new Color(0.55f, 0.57f, 0.6f), 0.7f, 0.8f);
            var woodTex = Mats.WoodTexture();
            var woodM = Mats.Make(Color.white, 0.55f, 0f, woodTex);
            woodM.mainTextureScale = new Vector2(1f, 2.4f);
            var shelfM = Mats.Make(new Color(0.12f, 0.1f, 0.09f), 0.3f);

            // room shell
            Cube("Floor", new Vector3(0, -0.05f, 0.4f), new Vector3(7f, 0.1f, 8f), floorM);
            Cube("Ceiling", new Vector3(0, 3.05f, 0.4f), new Vector3(7f, 0.1f, 8f), wallM);
            Cube("BackWall", new Vector3(0, 1.5f, -3.4f), new Vector3(7f, 3f, 0.1f), wallM);
            Cube("LeftWall", new Vector3(-3.5f, 1.5f, 0.4f), new Vector3(0.1f, 3f, 8f), wallM);
            Cube("RightWall", new Vector3(3.5f, 1.5f, 0.4f), new Vector3(0.1f, 3f, 8f), wallM);
            Cube("FrontWall", new Vector3(0, 1.5f, 4.4f), new Vector3(7f, 3f, 0.1f), wallM);

            // the table
            Cube("TableTop", new Vector3(0, TableTop - 0.03f, 0), new Vector3(TableW, 0.06f, TableL), woodM);
            Cube("TableApron", new Vector3(0, TableTop - 0.11f, 0), new Vector3(TableW - 0.1f, 0.1f, TableL - 0.1f), shelfM);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Cube("TableLeg", new Vector3(sx * (TableW * 0.5f - 0.06f), (TableTop - 0.06f) * 0.5f, sz * (TableL * 0.5f - 0.08f)), new Vector3(0.07f, TableTop - 0.06f, 0.07f), darkM);

            // back desk + monitors
            Cube("BackDesk", new Vector3(0, 0.76f, -3.0f), new Vector3(4.2f, 0.06f, 0.7f), woodM);
            Cube("BackDeskBase", new Vector3(0, 0.37f, -3.05f), new Vector3(4.1f, 0.72f, 0.55f), shelfM);
            var screenTex = MonitorTexture();
            var mon = Mats.Emissive(Color.white, 1.0f, screenTex);
            var mon2 = Mats.Emissive(new Color(0.2f, 0.55f, 1f), 0.9f);
            windowMat = null;
            for (int i = 0; i < 3; i++)
            {
                float x = -1.2f + i * 1.1f;
                Cube("MonitorFrame", new Vector3(x, 1.2f, -3.2f), new Vector3(0.82f, 0.52f, 0.04f), darkM);
                Cube("Monitor", new Vector3(x, 1.2f, -3.175f), new Vector3(0.76f, 0.46f, 0.01f), i == 1 ? mon2 : mon);
                Cube("MonitorStand", new Vector3(x, 0.92f, -3.2f), new Vector3(0.05f, 0.3f, 0.05f), metalM);
            }
            // wall shelves with bottles & jars
            for (int row = 0; row < 3; row++)
            {
                float y = 1.75f + row * 0.5f;
                Cube("Shelf", new Vector3(-0.3f, y, -3.25f), new Vector3(5.2f, 0.04f, 0.3f), shelfM);
                var rnd = new System.Random(row * 31 + 5);
                for (int k = 0; k < 14; k++)
                {
                    float x = -2.7f + k * 0.38f + (float)rnd.NextDouble() * 0.1f;
                    float h = 0.12f + (float)rnd.NextDouble() * 0.16f;
                    var gm = Mats.Make(Color.HSVToRGB((float)rnd.NextDouble() * 0.15f + (rnd.Next(3) == 0 ? 0.5f : 0.07f), 0.35f, 0.5f), 0.9f);
                    Cyl("Jar", new Vector3(x, y + 0.02f + h * 0.5f, -3.25f), 0.045f, h, gm);
                }
            }
            // microscope
            Cube("MicroBase", new Vector3(2.4f, 0.82f, -2.6f), new Vector3(0.22f, 0.04f, 0.28f), darkM);
            Cyl("MicroTube", new Vector3(2.4f, 0.98f, -2.62f), 0.028f, 0.34f, metalM, null, new Vector3(-18f, 0, 0));
            Cyl("MicroArm", new Vector3(2.4f, 0.93f, -2.7f), 0.02f, 0.25f, darkM);
            // monitor stands near table (left & right, the "lab computers" seen in the figma)
            var sideScreen = Mats.Emissive(Color.white, 0.8f, screenTex);
            Cube("SideDesk", new Vector3(-2.3f, 0.45f, -1.2f), new Vector3(0.9f, 0.9f, 1.8f), shelfM);
            Cube("SideMon", new Vector3(-2.7f, 1.2f, -1.2f), new Vector3(0.04f, 0.5f, 0.8f), darkM);
            Cube("SideMonScreen", new Vector3(-2.675f, 1.2f, -1.2f), new Vector3(0.01f, 0.44f, 0.74f), sideScreen);

            // window (left wall) – bright in the "Day" look
            windowMat = Mats.Emissive(Color.white, 1f, WindowTexture());
            Cube("Window", new Vector3(-3.44f, 1.7f, 0.4f), new Vector3(0.02f, 1.7f, 2.4f), windowMat);
            for (int i = 0; i < 4; i++) Cube("Mullion", new Vector3(-3.42f, 1.7f, -0.8f + i * 0.8f), new Vector3(0.06f, 1.74f, 0.07f), darkM);
            Cube("MullionH", new Vector3(-3.42f, 1.7f, 0.4f), new Vector3(0.06f, 0.07f, 2.44f), darkM);
            // bookshelf + plant (right wall, "Day" scan scene)
            Cube("Bookcase", new Vector3(3.3f, 1.1f, 0.6f), new Vector3(0.4f, 2.2f, 2.0f), shelfM);
            for (int r = 0; r < 5; r++)
            {
                var rnd = new System.Random(r + 11);
                float z = -0.3f;
                for (int b = 0; b < 12; b++)
                {
                    float bw = 0.04f + (float)rnd.NextDouble() * 0.05f, bh = 0.22f + (float)rnd.NextDouble() * 0.12f;
                    var bm = Mats.Make(Color.HSVToRGB((float)rnd.NextDouble(), 0.25f, 0.45f + 0.3f * (float)rnd.NextDouble()), 0.2f);
                    Cube("Book", new Vector3(3.17f, 0.3f + r * 0.42f + bh * 0.5f, z + 0.1f + b * 0.15f), new Vector3(0.24f, bh, bw), bm);
                }
            }
            var potM = Mats.Make(new Color(0.75f, 0.72f, 0.66f), 0.3f);
            var leafM = Mats.Make(new Color(0.18f, 0.4f, 0.16f), 0.4f);
            Cyl("PlantPot", new Vector3(3.0f, 0.18f, 2.6f), 0.18f, 0.36f, potM);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 0.7f;
                Sphere("Leaf", new Vector3(3.0f + Mathf.Cos(a) * 0.18f, 0.6f + (i % 3) * 0.16f, 2.6f + Mathf.Sin(a) * 0.18f), new Vector3(0.12f, 0.2f, 0.12f), leafM);
            }
            Cyl("DeskLampPole", new Vector3(-2.2f, 1.1f, -1.9f), 0.015f, 0.4f, metalM);

            // lights
            sun = NewLight("Sun", LightType.Directional, new Color(1f, 0.96f, 0.88f), 0f);
            sun.transform.rotation = Quaternion.Euler(38, 100, 0);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.75f;
            lamp = NewLight("WarmLamp", LightType.Point, new Color(1f, 0.62f, 0.34f), 0f);
            lamp.transform.position = new Vector3(1.8f, 2.3f, -1.4f); lamp.range = 9f;
            overhead = NewLight("Overhead", LightType.Spot, new Color(0.85f, 0.93f, 1f), 0f);
            overhead.transform.position = new Vector3(0.1f, 2.9f, 0.4f); overhead.transform.rotation = Quaternion.Euler(90, 0, 0);
            overhead.range = 6f; overhead.spotAngle = 80f; overhead.shadows = LightShadows.Soft; overhead.shadowStrength = 0.85f;
            glow = NewLight("MonitorGlow", LightType.Point, new Color(0.3f, 0.65f, 1f), 0f);
            glow.transform.position = new Vector3(0, 1.4f, -2.6f); glow.range = 6f;
        }

        Light NewLight(string name, LightType t, Color c, float intensity)
        {
            var g = new GameObject(name);
            g.transform.SetParent(transform, false);
            var l = g.AddComponent<Light>();
            l.type = t; l.color = c; l.intensity = intensity;
            return l;
        }

        public void SetLook(Look l, bool instant = false)
        {
            target = l == Look.Day ? 1f : 0f;
            if (instant) { blend = target; Apply(); }
        }

        public void SetVisible(bool v)
        {
            foreach (var r in renderers) if (r != null) r.enabled = v;
        }

        void Update()
        {
            if (Mathf.Abs(blend - target) > 0.001f)
            {
                blend = Mathf.MoveTowards(blend, target, Time.deltaTime * 1.4f);
                Apply();
            }
        }

        void Apply()
        {
            float b = blend;
            sun.intensity = Mathf.Lerp(0.05f, 1.15f, b);
            lamp.intensity = Mathf.Lerp(1.7f, 0.15f, b);
            overhead.intensity = Mathf.Lerp(4.0f, 0.5f, b);
            glow.intensity = Mathf.Lerp(1.6f, 0.0f, b);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(ambLab, ambDay, b);
            if (windowMat != null) windowMat.SetColor("_EmissionColor", Color.white * Mathf.Lerp(0.12f, 1.5f, b));
        }
    }
}
