using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MuscleAR
{
    /// <summary>One fixed-elbow connector, clipped rather than rerouted around UI.</summary>
    public sealed class MuscleConnector : MaskableGraphic
    {
        Vector2 start, elbow, end;
        Rect area;
        IReadOnlyList<Rect> obstacles;
        float thickness;
        readonly List<Vector2> intervals = new List<Vector2>(32);
        readonly List<Vector2> segments = new List<Vector2>(32);
        public int SegmentCount => segments.Count / 2;
        public void GetSegment(int index, out Vector2 a, out Vector2 b)
        { a = segments[index * 2]; b = segments[index * 2 + 1]; }
        public void SetGeometry(Vector2 a, Vector2 bend, Vector2 b, Rect permitted, IReadOnlyList<Rect> blocked, float width, bool force = false)
        {
            if (!force && start == a && elbow == bend && (end - b).sqrMagnitude < 0.0625f && area == permitted) return;
            start = a; elbow = bend; end = b; area = permitted; obstacles = blocked; thickness = width;
            SetVerticesDirty();
        }
        static bool Slab(float p, float d, float min, float max, ref float lo, ref float hi)
        {
            if (Mathf.Abs(d) < 0.000001f) return p >= min && p <= max;
            float a = (min - p) / d, b = (max - p) / d;
            lo = Mathf.Max(lo, Mathf.Min(a,b)); hi = Mathf.Min(hi, Mathf.Max(a,b)); return hi > lo;
        }
        public static bool Clip(Vector2 a, Vector2 b, Rect r, out float lo, out float hi)
        {
            lo = 0f; hi = 1f; Vector2 d = b - a;
            return Slab(a.x,d.x,r.xMin,r.xMax,ref lo,ref hi) && Slab(a.y,d.y,r.yMin,r.yMax,ref lo,ref hi);
        }
        void AddSegments(Vector2 a, Vector2 b)
        {
            if ((a-b).sqrMagnitude < 0.01f) return;
            float pad = thickness * 1.25f;
            Rect inner = Rect.MinMaxRect(area.xMin+pad,area.yMin+pad,area.xMax-pad,area.yMax-pad);
            if (!Clip(a,b,inner,out float lo,out float hi)) return;
            intervals.Clear(); intervals.Add(new Vector2(lo,hi));
            if (obstacles != null) foreach (Rect obstacle in obstacles)
            {
                Rect grown = Rect.MinMaxRect(obstacle.xMin-pad,obstacle.yMin-pad,obstacle.xMax+pad,obstacle.yMax+pad);
                if (!Clip(a,b,grown,out lo,out hi)) continue;
                for (int i=intervals.Count-1;i>=0;i--)
                {
                    Vector2 old = intervals[i]; if (hi <= old.x || lo >= old.y) continue;
                    intervals.RemoveAt(i);
                    if (lo > old.x) intervals.Add(new Vector2(old.x,lo));
                    if (hi < old.y) intervals.Add(new Vector2(hi,old.y));
                }
            }
            foreach (Vector2 interval in intervals)
            {
                Vector2 p = Vector2.Lerp(a,b,interval.x), q = Vector2.Lerp(a,b,interval.y);
                if ((p-q).sqrMagnitude < 0.1f) continue;
                segments.Add(p); segments.Add(q);
            }
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); segments.Clear(); AddSegments(start,elbow); AddSegments(elbow,end);
            for (int i=0;i<segments.Count;i+=2)
            {
                Quad(mesh,segments[i],segments[i+1],thickness*2.2f,new Color(0.01f,0.025f,0.045f,0.85f));
                Quad(mesh,segments[i],segments[i+1],thickness,color);
            }
        }
        static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 normal = new Vector2(-(b-a).y,(b-a).x).normalized * width * 0.5f;
            int n = mesh.currentVertCount;
            mesh.AddVert(a-normal,tint,Vector2.zero); mesh.AddVert(a+normal,tint,Vector2.zero);
            mesh.AddVert(b+normal,tint,Vector2.zero); mesh.AddVert(b-normal,tint,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
        }
    }
}
