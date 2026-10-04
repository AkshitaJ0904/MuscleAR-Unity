using System.Collections.Generic;
using UnityEngine;

namespace MuscleAR
{
    /// <summary>Procedural mesh helpers: swept elliptical tubes (muscles, bones, limbs) and ellipsoids.</summary>
    public static class MeshFactory
    {
        public static float CR(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            return new Vector3(CR(p0.x, p1.x, p2.x, p3.x, t), CR(p0.y, p1.y, p2.y, p3.y, t), CR(p0.z, p1.z, p2.z, p3.z, t));
        }

        /// <summary>Evaluate a Catmull-Rom spline through control points for u in [0,1].</summary>
        public static Vector3 EvalPath(IList<Vector3> c, float u)
        {
            if (c.Count == 1) return c[0];
            if (c.Count == 2) return Vector3.Lerp(c[0], c[1], Mathf.Clamp01(u));
            float f = Mathf.Clamp01(u) * (c.Count - 1);
            int i = Mathf.Min((int)f, c.Count - 2);
            float t = f - i;
            return CR(c[Mathf.Max(i - 1, 0)], c[i], c[i + 1], c[Mathf.Min(i + 2, c.Count - 1)], t);
        }

        /// <summary>
        /// Sweeps an elliptical cross-section along a polyline. rx is along the side axis (T x up), ry along the up axis.
        /// </summary>
        public static Mesh Sweep(Vector3[] pts, Vector3[] ups, float[] rx, float[] ry, int radial,
                                 float uvAlong = 6f, float uvAround = 1f)
        {
            int n = pts.Length;
            var verts = new Vector3[n * (radial + 1)];
            var norms = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[(n - 1) * radial * 6];
            float along = 0f;
            Vector3 prevS = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = pts[Mathf.Max(i - 1, 0)], b = pts[Mathf.Min(i + 1, n - 1)];
                Vector3 T = (b - a);
                float seg = T.magnitude;
                T = seg > 1e-6f ? T / seg : Vector3.up;
                Vector3 up = ups[i];
                Vector3 S = Vector3.Cross(T, up);
                if (S.sqrMagnitude < 1e-6f) S = prevS.sqrMagnitude > 0 ? prevS : Vector3.Cross(T, Vector3.right);
                S.Normalize();
                Vector3 U = Vector3.Cross(S, T).normalized;
                prevS = S;
                if (i > 0) along += (pts[i] - pts[i - 1]).magnitude;
                float dr = 0f;
                if (n > 2)
                {
                    int i0 = Mathf.Max(i - 1, 0), i1 = Mathf.Min(i + 1, n - 1);
                    float d = (pts[i1] - pts[i0]).magnitude;
                    if (d > 1e-6f) dr = ((rx[i1] + ry[i1]) - (rx[i0] + ry[i0])) * 0.5f / d;
                }
                float rxx = Mathf.Max(rx[i], 1e-4f), ryy = Mathf.Max(ry[i], 1e-4f);
                for (int k = 0; k <= radial; k++)
                {
                    float phi = (float)k / radial * Mathf.PI * 2f;
                    float c = Mathf.Cos(phi), s = Mathf.Sin(phi);
                    int idx = i * (radial + 1) + k;
                    verts[idx] = pts[i] + S * (c * rx[i]) + U * (s * ry[i]);
                    Vector3 nrm = (S * (c / rxx) + U * (s / ryy)).normalized;
                    norms[idx] = (nrm - T * dr).normalized;
                    uvs[idx] = new Vector2((float)k / radial * uvAround, along * uvAlong);
                }
            }
            int t = 0;
            for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < radial; k++)
                {
                    int a0 = i * (radial + 1) + k, a1 = a0 + 1, b0 = a0 + radial + 1, b1 = b0 + 1;
                    tris[t++] = a0; tris[t++] = b0; tris[t++] = a1;
                    tris[t++] = b0; tris[t++] = b1; tris[t++] = a1;
                }
            var m = new Mesh();
            m.indexFormat = verts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.vertices = verts; m.normals = norms; m.uv = uvs; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        public static Vector3[] Fill(int n, Vector3 v)
        {
            var a = new Vector3[n];
            for (int i = 0; i < n; i++) a[i] = v;
            return a;
        }

        /// <summary>Round-ended tube along a spline with constant radius.</summary>
        public static Mesh Tube(IList<Vector3> ctrl, float r0, float r1, int rings = 14, int radial = 10, Vector3? up = null,
                                bool roundA = true, bool roundB = true)
        {
            var pts = new Vector3[rings]; var rx = new float[rings]; var ry = new float[rings]; var ups = new Vector3[rings];
            Vector3 u = up ?? Vector3.forward;
            for (int i = 0; i < rings; i++)
            {
                float t = (float)i / (rings - 1);
                pts[i] = EvalPath(ctrl, t);
                float cap = 1f;
                if (roundA && t < 0.5f) cap = RoundEnd(t, 0.06f);
                if (roundB && t >= 0.5f) cap = RoundEnd(t, 0.06f);
                float r = Mathf.Lerp(r0, r1, t) * cap;
                rx[i] = r; ry[i] = r; ups[i] = u;
            }
            return Sweep(pts, ups, rx, ry, radial, 12f);
        }

        /// <summary>1 in the middle, drops to ~0 over `edge` at both ends as a quarter circle.</summary>
        public static float RoundEnd(float t, float edge)
        {
            float d = Mathf.Min(t, 1f - t);
            if (d >= edge) return 1f;
            float x = 1f - d / edge;
            return Mathf.Sqrt(Mathf.Max(0.0004f, 1f - x * x));
        }

        static Mesh unitSphere;
        /// <summary>Unit-radius sphere (scale non-uniformly for an ellipsoid).</summary>
        public static Mesh UnitSphere()
        {
            if (unitSphere != null) return unitSphere;
            int rings = 20;
            var pts = new Vector3[rings]; var r = new float[rings]; var ups = new Vector3[rings];
            for (int i = 0; i < rings; i++)
            {
                float t = (float)i / (rings - 1);
                float y = -Mathf.Cos(t * Mathf.PI);
                pts[i] = new Vector3(0, y, 0);
                r[i] = Mathf.Max(0.0005f, Mathf.Sin(t * Mathf.PI));
                ups[i] = Vector3.forward;
            }
            unitSphere = Sweep(pts, ups, r, r, 28, 1f);
            unitSphere.name = "UnitSphere";
            return unitSphere;
        }

        /// <summary>Spindle (fusiform) muscle belly profile: 0..1 along, returns 0..1 radius factor.</summary>
        public static float Spindle(float s, float belly, float full)
        {
            float a = s < belly ? s / Mathf.Max(belly, 1e-4f) : (1f - s) / Mathf.Max(1f - belly, 1e-4f);
            a = Mathf.Clamp01(a);
            return Mathf.Pow(Mathf.Sin(a * Mathf.PI * 0.5f), full);
        }
    }
}
