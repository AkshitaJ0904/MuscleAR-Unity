using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuscleAR
{
    /// <summary>World-space holographic callouts (leader line + small-caps text) that always face the camera.</summary>
    public class HoloLabels : MonoBehaviour
    {
        class Item
        {
            public Func<Vector3> anchor; public Vector2 off; public GameObject go;
            public LineRenderer line; public TextMesh title, sub; public Transform dot; public bool right;
        }

        readonly List<Item> items = new List<Item>();
        Camera cam; Material lineMat, dotMat;
        public float textScale = 1f;

        public static HoloLabels Create(Transform parent, Camera cam)
        {
            var go = new GameObject("HoloLabels");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<HoloLabels>();
            h.cam = cam;
            h.lineMat = Mats.Unlit(new Color(0.75f, 0.88f, 1f, 0.85f));
            h.dotMat = Mats.Unlit(new Color(0.55f, 0.85f, 1f, 1f));
            return h;
        }

        public void Clear()
        {
            foreach (var i in items) if (i.go != null) Destroy(i.go);
            items.Clear();
        }

        /// <param name="off">Offset of the label in camera-plane metres (x right, y up) at 1 m distance (scaled by distance).</param>
        public void Add(Func<Vector3> anchor, Vector2 off, string title, string sub = null)
        {
            var it = new Item { anchor = anchor, off = off, right = off.x >= 0 };
            var g = new GameObject("Label_" + title);
            g.transform.SetParent(transform, false);
            it.go = g;
            it.line = g.AddComponent<LineRenderer>();
            it.line.material = lineMat; it.line.positionCount = 3; it.line.useWorldSpace = true;
            it.line.startWidth = it.line.endWidth = 0.003f;
            it.line.numCapVertices = 2;

            var dot = new GameObject("Dot"); dot.transform.SetParent(g.transform, false);
            dot.AddComponent<MeshFilter>().sharedMesh = MeshFactory.UnitSphere();
            dot.AddComponent<MeshRenderer>().sharedMaterial = dotMat;
            it.dot = dot.transform;

            it.title = MakeText(g.transform, title, 1f, it.right);
            if (!string.IsNullOrEmpty(sub)) it.sub = MakeText(g.transform, sub, 0.78f, it.right);
            items.Add(it);
        }

        TextMesh MakeText(Transform parent, string text, float size, bool right)
        {
            var g = new GameObject("Text");
            g.transform.SetParent(parent, false);
            var tm = g.AddComponent<TextMesh>();
            tm.font = UIKit.Font;
            tm.text = text; tm.fontSize = 64; tm.characterSize = 0.0042f * size;
            tm.anchor = right ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            tm.alignment = right ? TextAlignment.Left : TextAlignment.Right;
            tm.color = new Color(0.82f, 0.92f, 1f, 0.95f);
            var mr = g.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(UIKit.Font.material);
            mr.sharedMaterial.renderQueue = 3500;
            return tm;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            foreach (var it in items)
            {
                Vector3 a = it.anchor();
                float dist = Vector3.Distance(cam.transform.position, a);
                float unit = dist * 0.45f * textScale;
                Vector3 r = cam.transform.right, u = cam.transform.up;
                Vector3 p1 = a + (r * it.off.x + u * it.off.y) * dist * 0.5f;
                Vector3 p2 = p1 + r * (it.right ? 1f : -1f) * unit * 0.12f;
                it.line.SetPosition(0, a); it.line.SetPosition(1, p1); it.line.SetPosition(2, p2);
                it.line.startWidth = it.line.endWidth = 0.0028f * dist * 0.6f;
                it.dot.position = a; it.dot.localScale = Vector3.one * 0.006f * dist * 0.6f;

                Vector3 tp = p2 + r * (it.right ? 1f : -1f) * unit * 0.02f;
                var rot = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                it.title.transform.SetPositionAndRotation(tp + (it.sub != null ? u * unit * 0.035f : Vector3.zero), rot);
                it.title.transform.localScale = Vector3.one * unit;
                if (it.sub != null)
                {
                    it.sub.transform.SetPositionAndRotation(tp - u * unit * 0.045f, rot);
                    it.sub.transform.localScale = Vector3.one * unit;
                }
            }
        }
    }
}
