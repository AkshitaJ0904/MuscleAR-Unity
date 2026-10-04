using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuscleAR
{
    /// <summary>
    /// Simulated AR plane detection: a sweeping scan band, feature points and a grid that lock onto the table.
    /// (On a device with AR Foundation this is where ARPlaneManager results would drive the same visuals.)
    /// </summary>
    public class SurfaceScanner : MonoBehaviour
    {
        public bool Scanning { get; private set; }
        public bool Detected { get; private set; }
        public float Progress { get; private set; }
        public Action OnDetected;

        Transform gridQuad, sweep, root;
        Material gridMat, sweepMat, pointMat, lineMat;
        LineRenderer outline;
        readonly List<Transform> points = new List<Transform>();
        float duration = 3.0f;

        static Texture2D GridTexture()
        {
            int s = 128;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Repeat;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool line = x < 2 || y < 2;
                    px[y * s + x] = line ? new Color(0.35f, 0.7f, 1f, 0.9f) : new Color(0.25f, 0.55f, 1f, 0.06f);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        static Mesh QuadMesh()
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            return m;
        }

        public static SurfaceScanner Create(Transform parent)
        {
            var go = new GameObject("SurfaceScanner");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<SurfaceScanner>();
            s.Build();
            s.root.gameObject.SetActive(false);
            return s;
        }

        void Build()
        {
            root = new GameObject("ScanVisuals").transform;
            root.SetParent(transform, false);
            float y = LabEnvironment.TableTop + 0.004f;
            float w = LabEnvironment.TableW, l = LabEnvironment.TableL;

            gridMat = Mats.Unlit(new Color(1, 1, 1, 0), GridTexture());
            var g = new GameObject("Grid"); g.transform.SetParent(root, false); g.transform.localPosition = new Vector3(0, y, 0);
            g.transform.localScale = new Vector3(w, 1, l);
            g.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            g.AddComponent<MeshRenderer>().sharedMaterial = gridMat;
            gridMat.mainTextureScale = new Vector2(w / 0.1f, l / 0.1f);
            gridQuad = g.transform;

            sweepMat = Mats.Unlit(new Color(0.3f, 0.7f, 1f, 0.0f));
            var sw = new GameObject("Sweep"); sw.transform.SetParent(root, false);
            sw.transform.localScale = new Vector3(w, 1, 0.09f);
            sw.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            sw.AddComponent<MeshRenderer>().sharedMaterial = sweepMat;
            sweep = sw.transform;

            pointMat = Mats.Unlit(new Color(0.8f, 0.95f, 1f, 1f));
            var rnd = new System.Random(3);
            for (int i = 0; i < 90; i++)
            {
                var p = new GameObject("Pt" + i); p.transform.SetParent(root, false);
                p.transform.localPosition = new Vector3(((float)rnd.NextDouble() - 0.5f) * w * 0.96f, y + 0.002f, ((float)rnd.NextDouble() - 0.5f) * l * 0.96f);
                p.transform.localScale = new Vector3(0.012f, 1, 0.012f);
                p.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
                p.AddComponent<MeshRenderer>().sharedMaterial = pointMat;
                p.SetActive(false);
                points.Add(p.transform);
            }

            lineMat = Mats.Unlit(new Color(0.35f, 0.65f, 1f, 1f));
            var lo = new GameObject("Outline"); lo.transform.SetParent(root, false);
            outline = lo.AddComponent<LineRenderer>();
            outline.material = lineMat; outline.loop = true; outline.positionCount = 4;
            outline.startWidth = outline.endWidth = 0.012f; outline.useWorldSpace = true;
            outline.alignment = LineAlignment.TransformZ; outline.transform.rotation = Quaternion.Euler(90, 0, 0);
            float hw = w * 0.5f, hl = l * 0.5f;
            outline.SetPositions(new[] { new Vector3(-hw, y + 0.003f, -hl), new Vector3(hw, y + 0.003f, -hl), new Vector3(hw, y + 0.003f, hl), new Vector3(-hw, y + 0.003f, hl) });
            outline.enabled = false;
        }

        public void Begin()
        {
            root.gameObject.SetActive(true);
            Scanning = true; Detected = false; Progress = 0f;
            outline.enabled = false;
            foreach (var p in points) p.gameObject.SetActive(false);
        }

        public void Stop()
        {
            Scanning = false;
            if (root != null) root.gameObject.SetActive(false);
        }

        /// <summary>Keep the detected-plane overlay visible (used when returning to the scan screen).</summary>
        public void Reset() { Detected = false; Progress = 0f; Scanning = false; }

        void Update()
        {
            if (!root.gameObject.activeSelf) return;
            float dt = Time.deltaTime;
            float y = LabEnvironment.TableTop + 0.004f;
            if (Scanning)
            {
                Progress = Mathf.Min(1f, Progress + dt / duration);
                float z = Mathf.PingPong(Time.time * 1.1f, 1f) * LabEnvironment.TableL - LabEnvironment.TableL * 0.5f;
                sweep.localPosition = new Vector3(0, y + 0.006f, z);
                sweepMat.color = new Color(0.3f, 0.7f, 1f, 0.35f);
                int visible = Mathf.RoundToInt(Progress * points.Count);
                for (int i = 0; i < points.Count; i++) points[i].gameObject.SetActive(i < visible);
                gridMat.color = new Color(1, 1, 1, Mathf.Clamp01(Progress * 1.4f) * 0.6f);
                if (Progress >= 1f)
                {
                    Scanning = false; Detected = true;
                    outline.enabled = true;
                    sweepMat.color = new Color(0, 0, 0, 0);
                    if (OnDetected != null) OnDetected();
                }
            }
            else if (Detected)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f);
                gridMat.color = new Color(1, 1, 1, 0.55f + 0.2f * p);
                lineMat.color = new Color(0.35f, 0.65f, 1f, 0.7f + 0.3f * p);
            }
        }
    }
}
