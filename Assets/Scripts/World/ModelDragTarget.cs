using System;
using UnityEngine;

namespace MuscleAR
{
    /// <summary>
    /// Designated interaction target and visual anchor pedestal for Drag-to-Reposition.
    /// Provides a dedicated 3D collider on the surface plane beneath the anatomy figure,
    /// along with holographic reposition graphics (boundary ring, directional arrows, and status badge).
    /// Prevents conflicts with muscle selection by separating the surface drag area from muscle meshes.
    /// </summary>
    public class ModelDragTarget : MonoBehaviour
    {
        public ModelController controller;
        public BoxCollider dragCollider;
        public MeshRenderer ringRenderer;

        Material ringMat;
        bool isDragging;
        float pulseTime;

        // Visual styling colors
        static readonly Color AmbientColor = new Color(0.25f, 0.65f, 1f, 0.45f);
        static readonly Color DraggingColor = new Color(0.35f, 0.95f, 1f, 0.92f);

        public bool IsDragging => isDragging;

        /// <summary>
        /// Creates the designated reposition target as a child of the AnatomyRoot transform.
        /// </summary>
        public static ModelDragTarget Create(Transform parent, ModelController controller)
        {
            var go = new GameObject("ModelDragTarget");
            go.transform.SetParent(parent, false);

            var target = go.AddComponent<ModelDragTarget>();
            target.controller = controller;

            // 1. BoxCollider covering the anchor base on the floor/table beneath the figure
            // Local dimensions: width 1.7m, length 2.3m, height 0.06m (scaled by root.scale ~0.40 = ~0.68m x 0.92m)
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.05f, 0.45f);
            col.size = new Vector3(2.4f, 0.30f, 2.8f);
            col.isTrigger = false;
            target.dragCollider = col;

            // 2. Holographic Reposition Ring Visual Quad
            var visualGo = new GameObject("RepositionVisual");
            visualGo.transform.SetParent(go.transform, false);
            visualGo.transform.localPosition = new Vector3(0, 0.006f, 0.45f);
            visualGo.transform.localScale = new Vector3(2.4f, 1f, 2.8f);

            var mf = visualGo.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateQuadMesh();

            var mr = visualGo.AddComponent<MeshRenderer>();
            target.ringMat = Mats.Unlit(AmbientColor, CreateRepositionTexture());
            mr.sharedMaterial = target.ringMat;
            target.ringRenderer = mr;

            return target;
        }

        static Mesh CreateQuadMesh()
        {
            var m = new Mesh();
            m.name = "RepositionRingMesh";
            m.vertices = new[]
            {
                new Vector3(-0.5f, 0, -0.5f),
                new Vector3( 0.5f, 0, -0.5f),
                new Vector3( 0.5f, 0,  0.5f),
                new Vector3(-0.5f, 0,  0.5f)
            };
            m.uv = new[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            return m;
        }

        static Texture2D CreateRepositionTexture()
        {
            int s = 512;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[s * s];

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f; // -1 to +1
                    float v = (y + 0.5f) / s * 2f - 1f; // -1 to +1

                    float au = Mathf.Abs(u);
                    float av = Mathf.Abs(v);
                    float r = Mathf.Sqrt(u * u + v * v);

                    float alpha = 0f;

                    // 1. Outer rounded boundary ring
                    float cornerRadius = Mathf.Max(au - 0.55f, 0f) * Mathf.Max(au - 0.55f, 0f) +
                                         Mathf.Max(av - 0.65f, 0f) * Mathf.Max(av - 0.65f, 0f);
                    float dRounded = Mathf.Max(au / 0.88f, av / 0.94f);
                    if (dRounded > 0.92f && dRounded < 0.99f)
                    {
                        alpha = Mathf.Max(alpha, 0.75f);
                    }

                    // 2. Corner brackets (L-shapes at corners for tech HUD feel)
                    bool inCornerX = au > 0.78f && au < 0.95f;
                    bool inCornerY = av > 0.84f && av < 0.97f;
                    if (inCornerX && (av > 0.93f && av < 0.97f)) alpha = Mathf.Max(alpha, 0.95f);
                    if (inCornerY && (au > 0.90f && au < 0.95f)) alpha = Mathf.Max(alpha, 0.95f);

                    // 3. Four directional arrows (North, South, East, West)
                    // North arrow (top center)
                    if (av > 0.82f && av < 0.91f && au < (0.91f - av) * 0.9f) alpha = Mathf.Max(alpha, 0.85f);
                    // South arrow (bottom center)
                    if (av > 0.82f && av < 0.91f && v < 0 && au < (0.91f - av) * 0.9f) alpha = Mathf.Max(alpha, 0.85f);
                    // East arrow (right center)
                    if (au > 0.77f && au < 0.86f && u > 0 && av < (0.86f - au) * 0.8f) alpha = Mathf.Max(alpha, 0.85f);
                    // West arrow (left center)
                    if (au > 0.77f && au < 0.86f && u < 0 && av < (0.86f - au) * 0.8f) alpha = Mathf.Max(alpha, 0.85f);

                    // 4. Subtle inner dashed guide ring
                    if (r > 0.68f && r < 0.71f)
                    {
                        float ang = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                        if (Mathf.Repeat(ang, 12f) < 7f) alpha = Mathf.Max(alpha, 0.45f);
                    }

                    // 5. Subtle pedestal floor wash
                    if (dRounded < 0.92f)
                    {
                        float innerWash = Mathf.Clamp01((0.92f - dRounded) / 0.4f);
                        alpha = Mathf.Max(alpha, innerWash * 0.08f);
                    }

                    px[y * s + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        void Update()
        {
            if (ringMat == null) return;

            pulseTime += Time.deltaTime;

            if (isDragging)
            {
                // Rapid vibrant pulsing while actively dragging
                float pulse = 0.75f + 0.25f * Mathf.Sin(pulseTime * 8f);
                ringMat.color = Color.Lerp(DraggingColor * 0.8f, DraggingColor * 1.2f, pulse);
            }
            else
            {
                // Calm, breathing ambient glow when idle
                float breathe = 0.5f + 0.5f * Mathf.Sin(pulseTime * 2.2f);
                ringMat.color = Color.Lerp(AmbientColor * 0.85f, AmbientColor * 1.15f, breathe);
            }
        }

        /// <summary>
        /// Updates the visual feedback state when dragging starts or ends.
        /// </summary>
        public void SetDraggingVisual(bool dragging)
        {
            isDragging = dragging;
            if (ringMat != null)
            {
                ringMat.color = dragging ? DraggingColor : AmbientColor;
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (dragCollider != null)
            {
                dragCollider.enabled = visible;
            }
            if (!visible && isDragging)
            {
                SetDraggingVisual(false);
            }
        }
    }
}
