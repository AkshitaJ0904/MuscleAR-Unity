using System.Collections.Generic;
using UnityEngine;

namespace MuscleAR
{
    /// <summary>
    /// Builds a full anatomical figure (skin, ~60 named muscle bellies with tendons, skeleton, organs)
    /// entirely from procedural meshes. Built standing, facing +Z, anatomical left = +X, height 1.75 m.
    /// </summary>
    public class HumanBodyBuilder
    {
        // ---------- limb helper: elliptical cross-sections along a centre line ----------
        class Limb
        {
            public Vector3[] c; public float[] rl, ra; public Vector3 ant, lat; int n;
            public Limb(Vector3[] c, float[] rl, float[] ra, Vector3 ant, Vector3 lat)
            { this.c = c; this.rl = rl; this.ra = ra; this.ant = ant; this.lat = lat; n = c.Length; }
            public Limb(Vector3 a, Vector3 b, float[] rl, float[] ra, Vector3 ant, Vector3 lat)
                : this(Lin(a, b, rl.Length), rl, ra, ant, lat) { }
            static Vector3[] Lin(Vector3 a, Vector3 b, int n)
            { var r = new Vector3[n]; for (int i = 0; i < n; i++) r[i] = Vector3.Lerp(a, b, (float)i / (n - 1)); return r; }

            public void Eval(float t, out Vector3 cc, out float l, out float a)
            {
                t = Mathf.Clamp01(t);
                float f = t * (n - 1); int i = Mathf.Min((int)f, n - 2); float u = f - i;
                int i0 = Mathf.Max(i - 1, 0), i2 = Mathf.Min(i + 2, n - 1);
                cc = MeshFactory.CR(c[i0], c[i], c[i + 1], c[i2], u);
                l = MeshFactory.CR(rl[i0], rl[i], rl[i + 1], rl[i2], u);
                a = MeshFactory.CR(ra[i0], ra[i], ra[i + 1], ra[i2], u);
            }
            public Vector3 Center(float t) { Vector3 cc; float l, a; Eval(t, out cc, out l, out a); return cc; }
            public Vector3 Pt(float t, float thDeg, float k = 1f)
            {
                Vector3 cc; float l, a; Eval(t, out cc, out l, out a);
                float th = thDeg * Mathf.Deg2Rad;
                return cc + (ant * (Mathf.Cos(th) * a) + lat * (Mathf.Sin(th) * l)) * k;
            }
            public Vector3 Out(float t, float thDeg)
            {
                Vector3 cc; float l, a; Eval(t, out cc, out l, out a);
                float th = thDeg * Mathf.Deg2Rad;
                return (ant * (Mathf.Cos(th) / Mathf.Max(a, 1e-3f)) + lat * (Mathf.Sin(th) / Mathf.Max(l, 1e-3f))).normalized;
            }
        }

        class PB
        {
            public List<Vector3> P = new List<Vector3>(), O = new List<Vector3>();
            public PB L(Limb l, float t, float th, float k = 1f) { P.Add(l.Pt(t, th, k)); O.Add(l.Out(t, th)); return this; }
            public PB R(Vector3 p, Vector3 o) { P.Add(p); O.Add(o.normalized); return this; }
        }

        class Anchor { public Transform muscle, tendon, skin, bone; }

        // ---------- state ----------
        HumanBody body;
        Transform skinRoot, muscleRoot, tendonRoot, boneRoot, organRoot, faceRoot;
        Material baseMat, tendonMat, boneMat, skinMat, skinGhost, darkMat, whiteMat, heartMat, lungMat, handMat;
        Anchor stat;
        Limb torso, neck;
        float wMul = 1.4f, tMul = 1.15f;

        static readonly Color MuscleRed = new Color(0.68f, 0.16f, 0.14f);

        public static HumanBody Build() { return new HumanBodyBuilder().Run(); }

        // ---------- construction helpers ----------
        Transform NewRoot(Transform p, string n) { var g = new GameObject(n); g.transform.SetParent(p, false); return g.transform; }

        GameObject Part(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }

        GameObject Ball(Transform parent, string name, Vector3 c, Vector3 radii, Material mat, Quaternion? rot = null)
        {
            var go = Part(parent, name, MeshFactory.UnitSphere(), mat);
            go.transform.position = c;
            go.transform.rotation = rot ?? Quaternion.identity;
            go.transform.localScale = radii;
            return go;
        }

        Material MuscleMat(MuscleId id)
        {
            Material m;
            if (body.muscleMats.TryGetValue(id, out m)) return m;
            float v = Mathf.Repeat((int)id * 0.37f, 1f);
            Color c = MuscleRed * (0.88f + 0.22f * v);
            c.r = Mathf.Min(1f, c.r * (1.0f + 0.1f * (1f - v)));
            c.a = 1f;
            m = Mats.Make(c, 0.42f, 0f, Mats.FiberTexture());
            m.name = "M_" + id;
            body.RegisterMaterial(id, m);
            return m;
        }

        Mesh LoftMesh(Limb L, float scale, float add, int rings = 28, int radial = 28)
        {
            var pts = new Vector3[rings]; var ups = new Vector3[rings]; var rx = new float[rings]; var ry = new float[rings];
            for (int i = 0; i < rings; i++)
            {
                float t = (float)i / (rings - 1);
                Vector3 cc; float l, a; L.Eval(t, out cc, out l, out a);
                pts[i] = cc; ups[i] = L.ant;
                float cap = 1f;
                rx[i] = (l * scale + add) * cap; ry[i] = (a * scale + add) * cap;
            }
            // lat axis of the sweep is T x up, make sure rx maps to L.lat
            return MeshFactory.Sweep(pts, ups, rx, ry, radial, 3f, 1f);
        }

        void Loft(Limb L, Anchor an, float baseScale, float skinScale, string name)
        {
            Part(an.muscle, name + "_base", LoftMesh(L, baseScale, 0f), baseMat);
            var sk = Part(an.skin, name + "_skin", LoftMesh(L, skinScale, 0.004f), skinMat);
            body.skinRenderers.Add(sk.GetComponent<Renderer>());
        }

        void SkinBall(Anchor an, string name, Vector3 c, Vector3 r, Quaternion? rot = null)
        {
            var g = Ball(an.skin, name, c, r, skinMat, rot);
            body.skinRenderers.Add(g.GetComponent<Renderer>());
        }

        /// <summary>One muscle belly with optional tendons at both ends, mesh collider + selection handle.</summary>
        void M(MuscleId id, int side, Anchor an, PB pb, float w, float t, float belly = 0.5f,
               float tendA = 0.10f, float tendB = 0.10f, float full = 0.7f)
        {
            int N = 30;
            w *= wMul; t *= tMul;
            float uA = tendA, uB = 1f - tendB;
            float rt = Mathf.Min(w, t) * 0.24f;
            var pts = new Vector3[N]; var ups = new Vector3[N]; var rx = new float[N]; var ry = new float[N];
            float floorA = tendA > 0.001f ? rt : Mathf.Min(w, t) * 0.05f;
            float floorB = tendB > 0.001f ? rt : Mathf.Min(w, t) * 0.05f;
            for (int i = 0; i < N; i++)
            {
                float s = (float)i / (N - 1);
                float u = Mathf.Lerp(uA, uB, s);
                pts[i] = MeshFactory.EvalPath(pb.P, u);
                ups[i] = MeshFactory.EvalPath(pb.O, u).normalized;
                float f = MeshFactory.Spindle(s, belly, full);
                float flo = s < belly ? floorA : floorB;
                rx[i] = Mathf.Lerp(flo, w, f); ry[i] = Mathf.Lerp(flo, t, f);
            }
            Mesh mesh = MeshFactory.Sweep(pts, ups, rx, ry, 18, 7f, 1f);
            mesh.name = id + (side > 0 ? "_L" : "_R");
            var go = Part(an.muscle, mesh.name, mesh, MuscleMat(id));
            var col = go.AddComponent<MeshCollider>(); col.sharedMesh = mesh;
            var h = go.AddComponent<MuscleHandle>(); h.id = id; h.side = side;
            body.RegisterHandle(h);

            if (tendA > 0.001f)
            {
                var c = new List<Vector3>();
                for (int i = 0; i <= 5; i++) c.Add(MeshFactory.EvalPath(pb.P, uA * (1f - (float)i / 5f) * 0f + Mathf.Lerp(0f, uA, i / 5f)));
                Part(an.tendon, mesh.name + "_tendA", MeshFactory.Tube(c, rt * 0.6f, rt, 8, 8, null, true, false), tendonMat);
            }
            if (tendB > 0.001f)
            {
                var c = new List<Vector3>();
                for (int i = 0; i <= 5; i++) c.Add(MeshFactory.EvalPath(pb.P, Mathf.Lerp(uB, 1f, i / 5f)));
                Part(an.tendon, mesh.name + "_tendB", MeshFactory.Tube(c, rt, rt * 0.6f, 8, 8, null, false, true), tendonMat);
            }
        }

        void TendonTube(Anchor an, string name, List<Vector3> path, float r0, float r1)
        {
            Part(an.tendon, name, MeshFactory.Tube(path, r0, r1, 12, 8), tendonMat);
        }

        void BoneTube(Anchor an, string name, List<Vector3> path, float r0, float r1, Material m = null)
        {
            Part(an.bone, name, MeshFactory.Tube(path, r0, r1, Mathf.Max(10, path.Count * 5), 10), m ?? boneMat);
        }

        static List<Vector3> V(params Vector3[] v) { return new List<Vector3>(v); }
        static Vector3 P3(double x, double y, double z) { return new Vector3((float)x, (float)y, (float)z); }

        Anchor MakeFore(Vector3 elbow, Vector3 axis, string tag)
        {
            var a = new Anchor();
            a.muscle = Pivot(muscleRoot, elbow, axis, tag, true);
            a.tendon = Pivot(tendonRoot, elbow, axis, tag, false);
            a.skin = Pivot(skinRoot, elbow, axis, tag, false);
            a.bone = Pivot(boneRoot, elbow, axis, tag, false);
            return a;
        }

        Transform Pivot(Transform root, Vector3 pos, Vector3 axis, string tag, bool _)
        {
            var g = new GameObject("Forearm_" + tag);
            g.transform.SetParent(root, false);
            g.transform.position = pos;
            var fp = g.AddComponent<FlexPivot>(); fp.axis = axis.normalized;
            body.pivots.Add(fp);
            return g.transform;
        }

        void AnchorAt(string key, Vector3 pos)
        {
            var g = new GameObject("Anchor_" + key);
            g.transform.SetParent(body.transform, false);
            g.transform.position = pos;
            body.anchors[key] = g.transform;
        }

        // ---------- main ----------
        HumanBody Run()
        {
            var rootGO = new GameObject("HumanBody");
            body = rootGO.AddComponent<HumanBody>();
            skinRoot = body.skinRoot = NewRoot(rootGO.transform, "Layer_Skin");
            muscleRoot = body.muscleRoot = NewRoot(rootGO.transform, "Layer_Muscle");
            tendonRoot = body.tendonRoot = NewRoot(rootGO.transform, "Layer_Tendon");
            boneRoot = body.boneRoot = NewRoot(rootGO.transform, "Layer_Bone");
            organRoot = body.organRoot = NewRoot(rootGO.transform, "Layer_Organs");
            faceRoot = body.faceRoot = NewRoot(rootGO.transform, "Face");

            baseMat = Mats.Make(new Color(0.34f, 0.075f, 0.075f), 0.28f, 0f, Mats.FiberTexture());
            tendonMat = Mats.Make(new Color(0.93f, 0.89f, 0.80f), 0.5f);
            tendonMat.EnableKeyword("_EMISSION"); tendonMat.SetColor("_EmissionColor", Color.black);
            boneMat = Mats.Make(new Color(0.90f, 0.86f, 0.76f), 0.32f, 0f, Mats.NoiseTexture());
            skinMat = Mats.Make(new Color(0.80f, 0.60f, 0.50f), 0.35f, 0f, Mats.NoiseTexture());
            skinGhost = Mats.Make(new Color(0.45f, 0.65f, 1f, 0.10f), 0.8f);
            Mats.SetFade(skinGhost);
            darkMat = Mats.Make(new Color(0.06f, 0.03f, 0.03f), 0.1f);
            whiteMat = Mats.Make(new Color(0.96f, 0.95f, 0.92f), 0.7f);
            heartMat = Mats.Make(new Color(0.55f, 0.07f, 0.10f), 0.55f);
            lungMat = Mats.Make(new Color(0.80f, 0.55f, 0.56f), 0.3f);
            handMat = Mats.Make(new Color(0.58f, 0.14f, 0.12f), 0.35f, 0f, Mats.FiberTexture());
            body.tendonMat = tendonMat; body.skinMat = skinMat; body.skinGhostMat = skinGhost; body.baseMat = baseMat;

            stat = new Anchor { muscle = muscleRoot, tendon = tendonRoot, skin = skinRoot, bone = boneRoot };

            BuildTorsoAndHead();
            for (int s = 1; s >= -1; s -= 2)
            {
                BuildTorsoMuscles(s);
                BuildArm(s);
                BuildLeg(s);
                wMul = 1f; tMul = 1f; BuildHeadMuscles(s); wMul = 1.4f; tMul = 1.15f;
                BuildSkeletonSide(s);
            }
            BuildSkeletonCentral();
            BuildOrgans();
            AnchorsMisc();
            body.SetLayer(BodyLayer.Muscle);
            return body;
        }

        // ---------- torso, neck, head ----------
        float Ty(float y) { return (y - 0.74f) / 0.78f; }

        static readonly float[,] TorsoKeys = {
            // y,    hx,     hz
            { 0.74f, 0.030f, 0.030f }, { 0.77f, 0.100f, 0.075f }, { 0.80f, 0.135f, 0.092f }, { 0.88f, 0.165f, 0.105f },
            { 0.95f, 0.170f, 0.106f }, { 1.03f, 0.147f, 0.093f }, { 1.12f, 0.150f, 0.098f }, { 1.22f, 0.162f, 0.108f },
            { 1.32f, 0.176f, 0.120f }, { 1.40f, 0.184f, 0.116f }, { 1.46f, 0.168f, 0.100f }, { 1.49f, 0.112f, 0.078f },
            { 1.52f, 0.057f, 0.055f }
        };

        void TorsoAt(float y, out float hx, out float hz)
        {
            int n = TorsoKeys.GetLength(0);
            for (int i = 0; i < n - 1; i++)
                if (y <= TorsoKeys[i + 1, 0])
                {
                    float f = Mathf.InverseLerp(TorsoKeys[i, 0], TorsoKeys[i + 1, 0], y);
                    f = f * f * (3f - 2f * f);
                    hx = Mathf.Lerp(TorsoKeys[i, 1], TorsoKeys[i + 1, 1], f);
                    hz = Mathf.Lerp(TorsoKeys[i, 2], TorsoKeys[i + 1, 2], f);
                    return;
                }
            hx = TorsoKeys[n - 1, 1]; hz = TorsoKeys[n - 1, 2];
        }

        void BuildTorsoAndHead()
        {
            int ns = 27;
            var hx = new float[ns]; var hz = new float[ns]; var c = new Vector3[ns];
            for (int i = 0; i < ns; i++)
            {
                float y = 0.74f + 0.03f * i;
                c[i] = new Vector3(0, y, 0);
                TorsoAt(y, out hx[i], out hz[i]);
            }
            torso = new Limb(c, hx, hz, Vector3.forward, Vector3.right);
            Loft(torso, stat, 0.93f, 1.045f, "Torso");

            neck = new Limb(P3(0, 1.47f, 0.0f), P3(0, 1.63f, 0.012f), new[] { 0.056f, 0.050f, 0.046f }, new[] { 0.054f, 0.048f, 0.046f }, Vector3.forward, Vector3.right);
            Loft(neck, stat, 0.93f, 1.05f, "Neck");

            // head shell
            Vector3 hc = P3(0, 1.655f, 0.008);
            Ball(muscleRoot, "Head", hc, P3(0.078f, 0.098f, 0.095f), baseMat);
            SkinBall(stat, "HeadSkin", hc, P3(0.081f, 0.101f, 0.098f));
            Ball(muscleRoot, "Chin", P3(0, 1.578f, 0.046f), P3(0.034f, 0.024f, 0.034f), baseMat);
            SkinBall(stat, "ChinSkin", P3(0, 1.578f, 0.048f), P3(0.037f, 0.027f, 0.037f));
            Ball(muscleRoot, "Nose", P3(0, 1.632f, 0.098f), P3(0.012f, 0.022f, 0.014f), handMat);
            SkinBall(stat, "NoseSkin", P3(0, 1.632f, 0.1f), P3(0.013f, 0.024f, 0.016f));
            for (int s = -1; s <= 1; s += 2)
            {
                SkinBall(stat, "EarSkin", P3(s * 0.08f, 1.65f, 0.0f), P3(0.008f, 0.025f, 0.016f));
                Ball(muscleRoot, "Ear", P3(s * 0.079f, 1.65f, 0.0f), P3(0.008f, 0.024f, 0.015f), handMat);
                // eyes
                Ball(faceRoot, "Eye", P3(s * 0.031f, 1.657f, 0.088f), P3(0.0115f, 0.0115f, 0.0115f), whiteMat);
                Ball(faceRoot, "Iris", P3(s * 0.031f, 1.657f, 0.0985f), P3(0.0058f, 0.0058f, 0.002f), Mats.Make(new Color(0.18f, 0.12f, 0.08f), 0.8f));
            }
        }

        void BuildHeadMuscles(int s)
        {
            var mm = stat;
            Vector3 hc = P3(0, 1.655f, 0.008);
            // orbicularis oculi ring
            var ring = new List<Vector3>();
            for (int i = 0; i <= 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f * 0.97f;
                ring.Add(P3(s * 0.031f + Mathf.Cos(a) * 0.021f, 1.657f + Mathf.Sin(a) * 0.015f, 0.0915f));
            }
            var ringMesh = MeshFactory.Tube(ring, 0.0055f, 0.0055f, 30, 8, Vector3.forward);
            var rg = Part(muscleRoot, "OrbicularisOculi" + s, ringMesh, MuscleMat(MuscleId.OrbicularisOculi));
            var rcol = rg.AddComponent<MeshCollider>(); rcol.sharedMesh = ringMesh;
            var rh = rg.AddComponent<MuscleHandle>(); rh.id = MuscleId.OrbicularisOculi; rh.side = s; body.RegisterHandle(rh);

            if (s > 0)
            {
                var mouth = new List<Vector3>();
                for (int i = 0; i <= 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2f * 0.97f;
                    mouth.Add(P3(Mathf.Cos(a) * 0.022f, 1.602f + Mathf.Sin(a) * 0.007f, 0.0905f));
                }
                var mmesh = MeshFactory.Tube(mouth, 0.0045f, 0.0045f, 34, 8, Vector3.forward);
                var mg = Part(muscleRoot, "OrbicularisOris", mmesh, MuscleMat(MuscleId.OrbicularisOris));
                var mc = mg.AddComponent<MeshCollider>(); mc.sharedMesh = mmesh;
                var mh = mg.AddComponent<MuscleHandle>(); mh.id = MuscleId.OrbicularisOris; mh.side = 0; body.RegisterHandle(mh);

                var f = new PB().R(P3(-0.05f, 1.718f, 0.062f), P3(0, 0.4f, 1)).R(P3(0f, 1.728f, 0.07f), P3(0, 0.4f, 1)).R(P3(0.05f, 1.718f, 0.062f), P3(0, 0.4f, 1));
                M(MuscleId.Frontalis, 0, mm, f, 0.022f, 0.007f, 0.5f, 0f, 0f, 0.5f);
            }
            var mass = new PB().R(P3(s * 0.058f, 1.626f, 0.040f), P3(s, 0, 0.4f)).R(P3(s * 0.066f, 1.60f, 0.032f), P3(s, 0, 0.3f)).R(P3(s * 0.060f, 1.578f, 0.022f), P3(s, 0, 0.2f));
            M(MuscleId.Masseter, s, mm, mass, 0.016f, 0.010f, 0.5f, 0f, 0f, 0.6f);
            var temp = new PB().R(P3(s * 0.062f, 1.71f, 0.025f), P3(s, 0.4f, 0.2f)).R(P3(s * 0.076f, 1.672f, 0.03f), P3(s, 0.1f, 0.1f)).R(P3(s * 0.064f, 1.632f, 0.042f), P3(s, 0, 0.3f));
            M(MuscleId.Temporalis, s, mm, temp, 0.028f, 0.007f, 0.5f, 0f, 0f, 0.5f);
            // sternocleidomastoid
            var scm = new PB().R(P3(s * 0.055f, 1.60f, -0.018f), P3(s, 0.1f, 0.4f)).R(P3(s * 0.045f, 1.54f, 0.040f), P3(s, 0.1f, 0.8f)).R(P3(s * 0.020f, 1.472f, 0.080f), P3(s * 0.3f, 0.3f, 1f));
            M(MuscleId.Sternocleidomastoid, s, mm, scm, 0.011f, 0.010f, 0.45f, 0.06f, 0.10f, 0.7f);
            // trapezius (upper fibres)
            var trap = new PB().R(P3(s * 0.035f, 1.585f, -0.045f), P3(0, 0.6f, -0.8f)).R(P3(s * 0.100f, 1.522f, -0.030f), P3(s * 0.2f, 0.9f, -0.2f)).R(P3(s * 0.175f, 1.472f, -0.012f), P3(s * 0.3f, 0.9f, 0));
            M(MuscleId.Trapezius, s, mm, trap, 0.034f, 0.010f, 0.5f, 0.02f, 0.05f, 0.5f);
        }

        // ---------- torso muscles ----------
        void BuildTorsoMuscles(int s)
        {
            var an = stat;
            Limb T = torso;
            Limb UA = ArmLimb(s);
            float sd = s;

            // pectoralis major (4 fan bundles from clavicle/sternum to humerus)
            float[] py = { 1.425f, 1.372f, 1.315f };
            float[] wd = { 0.030f, 0.036f, 0.034f };
            float[] ins = { 0.15f, 0.12f, 0.09f };
            for (int i = 0; i < 3; i++)
            {
                var pb = new PB()
                    .L(T, Ty(py[i]), 8 * sd, 1.0f)
                    .L(T, Ty(py[i] + 0.004f), 36 * sd, 1.0f)
                    .L(T, Ty(py[i] + 0.012f + 0.004f * i), 70 * sd, 1.0f)
                    .L(UA, ins[i], 12, 1.0f);
                M(MuscleId.PectoralisMajor, s, an, pb, wd[i], 0.011f, 0.5f, 0f, 0.12f, 0.35f);
            }

            // rectus abdominis: 4 rows x 1 column per side + inscriptions + linea alba
            float[] rowTop = { 1.238f, 1.172f, 1.104f, 1.036f };
            float[] rowBot = { 1.178f, 1.110f, 1.042f, 0.965f };
            for (int i = 0; i < 4; i++)
            {
                var pb = new PB().L(T, Ty(rowTop[i]), 15 * sd, 1.0f).L(T, Ty(0.5f * (rowTop[i] + rowBot[i])), 15 * sd, 1.04f).L(T, Ty(rowBot[i]), 15 * sd, 1.0f);
                M(MuscleId.RectusAbdominis, s, an, pb, 0.031f, 0.017f, 0.5f, 0f, 0f, 0.38f);
            }
            for (int i = 0; i < 5; i++)
            {
                float y = i == 0 ? 1.243f : (rowBot[i - 1] + (i < 4 ? rowTop[i] : 0.96f)) * 0.5f;
                var ins2 = V(T.Pt(Ty(y), 1 * sd, 1.04f), T.Pt(Ty(y), 15 * sd, 1.05f), T.Pt(Ty(y), 30 * sd, 1.0f));
                Part(tendonRoot, "Inscription", MeshFactory.Tube(ins2, 0.0035f, 0.0035f, 10, 6), tendonMat);
            }
            if (s > 0)
            {
                var la = V(T.Pt(Ty(1.265f), 0, 1.03f), T.Pt(Ty(1.1f), 0, 1.05f), T.Pt(Ty(0.93f), 0, 1.03f));
                Part(tendonRoot, "LineaAlba", MeshFactory.Tube(la, 0.0035f, 0.0035f, 18, 6), tendonMat);
            }

            // external oblique
            for (int i = 0; i < 2; i++)
            {
                float o = i * 0.045f;
                var pb = new PB().L(T, Ty(1.285f - o * 0.4f), 88 * sd, 1.0f).L(T, Ty(1.17f - o * 0.5f), 68 * sd, 1.0f).L(T, Ty(1.03f - o * 0.2f), 45 * sd, 1.0f);
                M(MuscleId.ExternalOblique, s, an, pb, 0.036f, 0.014f, 0.5f, 0f, 0.15f, 0.4f);
            }
            // serratus anterior digitations
            for (int i = 0; i < 4; i++)
            {
                float y0 = 1.36f - i * 0.042f;
                var pb = new PB().L(T, Ty(y0), 98 * sd, 1.0f).L(T, Ty(y0 - 0.02f), 80 * sd, 1.02f).L(T, Ty(y0 - 0.04f), 64 * sd, 1.0f);
                M(MuscleId.SerratusAnterior, s, an, pb, 0.012f, 0.009f, 0.5f, 0f, 0f, 0.6f);
            }
        }

        // ---------- arms ----------
        Limb ArmLimb(int s)
        {
            Vector3 SH = P3(s * 0.19f, 1.43f, 0f);
            Vector3 dU = P3(s * Mathf.Sin(32 * Mathf.Deg2Rad), -Mathf.Cos(32 * Mathf.Deg2Rad), 0f);
            Vector3 EL = SH + dU * 0.30f;
            Vector3 lat = (s * Vector3.Cross(Vector3.forward, dU)).normalized;
            return new Limb(SH, EL, new[] { 0.044f, 0.043f, 0.040f, 0.037f, 0.034f }, new[] { 0.044f, 0.044f, 0.042f, 0.040f, 0.036f }, Vector3.forward, lat);
        }

        void BuildArm(int s)
        {
            Vector3 SH = P3(s * 0.19f, 1.43f, 0f);
            Limb U = ArmLimb(s);
            Vector3 EL = U.c[U.c.Length - 1];
            Vector3 dU = (EL - SH).normalized;
            Vector3 dF = P3(s * Mathf.Sin(40 * Mathf.Deg2Rad), -Mathf.Cos(40 * Mathf.Deg2Rad), 0.02f).normalized;
            Vector3 WR = EL + dF * 0.26f;
            Vector3 latF = (s * Vector3.Cross(Vector3.forward, dF)).normalized;
            Limb F = new Limb(EL, WR, new[] { 0.036f, 0.040f, 0.034f, 0.026f, 0.021f }, new[] { 0.038f, 0.042f, 0.034f, 0.024f, 0.017f }, Vector3.forward, latF);
            Vector3 axis = Vector3.Cross(dF, Vector3.forward);
            string tag = s > 0 ? "L" : "R";
            Anchor fa = MakeFore(EL, axis, tag);
            Anchor ua = stat;
            float sd = s;

            // ----- base tissue + skin -----
            Loft(U, ua, 0.88f, 1.06f, "UpperArm" + tag);
            Loft(F, fa, 0.88f, 1.06f, "Forearm" + tag);
            Ball(muscleRoot, "Elbow" + tag, EL, P3(0.036f, 0.036f, 0.036f), baseMat);
            SkinBall(ua, "ElbowSkin" + tag, EL, P3(0.038f, 0.038f, 0.038f));
            Ball(muscleRoot, "ShoulderBase" + tag, SH + P3(s * 0.01f, -0.005f, 0), P3(0.052f, 0.056f, 0.052f), baseMat);
            SkinBall(ua, "ShoulderSkin" + tag, SH + P3(s * 0.01f, -0.005f, 0), P3(0.055f, 0.06f, 0.055f));

            // ----- deltoid: cap + three heads -----
            {
                var capPos = SH + P3(s * 0.016f, -0.004f, 0);
                var cap = Ball(muscleRoot, "DeltoidCap" + tag, capPos, P3(0.047f, 0.052f, 0.048f), MuscleMat(MuscleId.Deltoid), Quaternion.Euler(0, 0, -s * 20f));
                var mc = cap.AddComponent<SphereCollider>(); mc.radius = 1f;
                var hh = cap.AddComponent<MuscleHandle>(); hh.id = MuscleId.Deltoid; hh.side = s; body.RegisterHandle(hh);
                Vector3[] org = { SH + P3(s * -0.045f, 0.035f, 0.045f), SH + P3(s * 0.015f, 0.05f, 0.0f), SH + P3(s * -0.03f, 0.04f, -0.05f) };
                float[] ang = { 0, 90, 180 };
                for (int i = 0; i < 3; i++)
                {
                    Vector3 ins3 = U.Pt(0.40f, ang[i], 1.0f);
                    Vector3 o0 = (org[i] - SH).normalized; if (o0.sqrMagnitude < 0.01f) o0 = Vector3.up;
                    Vector3 o1 = U.Out(0.4f, ang[i]);
                    Vector3 midP = Vector3.Lerp(org[i], ins3, 0.45f) + U.Out(0.2f, ang[i]) * 0.022f;
                    var pb = new PB().R(org[i], o1 + Vector3.up * 0.3f).R(midP, o1).R(ins3, o1);
                    M(MuscleId.Deltoid, s, ua, pb, 0.034f, 0.016f, 0.4f, 0f, 0.1f, 0.5f);
                }
            }

            // ----- biceps (long + short head), brachialis, triceps -----
            {
                var lh = new PB().L(U, 0.02f, 14, 0.92f).L(U, 0.25f, 10, 1.06f).L(U, 0.60f, 6, 1.14f).L(U, 0.97f, 0, 0.92f);
                M(MuscleId.BicepsBrachii, s, ua, lh, 0.020f, 0.018f, 0.52f, 0.14f, 0.17f, 0.65f);
                var sh2 = new PB().L(U, 0.02f, -16, 0.92f).L(U, 0.25f, -12, 1.06f).L(U, 0.60f, -8, 1.12f).L(U, 0.97f, -2, 0.92f);
                M(MuscleId.BicepsBrachii, s, ua, sh2, 0.020f, 0.017f, 0.50f, 0.14f, 0.17f, 0.65f);
                var bt = V(U.Pt(0.98f, 0, 0.9f), F.Pt(0.06f, -6, 0.8f), F.Pt(0.14f, -14, 0.55f));
                TendonTube(fa, "BicepsTendon" + tag, bt, 0.0048f, 0.0035f);

                var br = new PB().L(U, 0.38f, 30, 0.95f).L(U, 0.66f, 24, 1.02f).L(U, 0.97f, 8, 0.9f);
                M(MuscleId.Brachialis, s, ua, br, 0.019f, 0.014f, 0.55f, 0.15f, 0.12f, 0.7f);

                var t1 = new PB().L(U, 0.10f, 150, 0.95f).L(U, 0.45f, 138, 1.04f).L(U, 0.88f, 168, 0.98f).L(U, 1.0f, 180, 0.9f);
                M(MuscleId.TricepsBrachii, s, ua, t1, 0.022f, 0.020f, 0.45f, 0.06f, 0.2f, 0.6f);
                var t2 = new PB().L(U, 0.04f, -150, 0.92f).L(U, 0.42f, -166, 1.05f).L(U, 0.90f, 180, 0.96f);
                M(MuscleId.TricepsBrachii, s, ua, t2, 0.024f, 0.022f, 0.45f, 0.06f, 0.2f, 0.6f);
            }

            // ----- forearm muscles (move with the elbow pivot) -----
            {
                var bra = new PB().L(U, 0.80f, 52, 1.02f).L(F, 0.02f, 56, 1.02f).L(F, 0.22f, 48, 1.02f).L(F, 0.55f, 28, 0.86f).L(F, 0.98f, 10, 0.7f);
                M(MuscleId.Brachioradialis, s, fa, bra, 0.017f, 0.015f, 0.2f, 0.0f, 0.4f, 0.7f);
                var pt = new PB().L(F, 0.0f, -34, 0.95f).L(F, 0.14f, -8, 1.02f).L(F, 0.45f, 24, 0.92f);
                M(MuscleId.PronatorTeres, s, fa, pt, 0.016f, 0.014f, 0.35f, 0.04f, 0.15f, 0.7f);
                var fcr = new PB().L(F, 0.03f, -22, 0.98f).L(F, 0.26f, -6, 1.06f).L(F, 0.65f, -2, 0.82f).L(F, 1.0f, 0, 0.7f);
                M(MuscleId.ForearmFlexors, s, fa, fcr, 0.016f, 0.013f, 0.25f, 0.04f, 0.45f, 0.7f);
                var fcu = new PB().L(F, 0.03f, -62, 0.98f).L(F, 0.28f, -58, 1.02f).L(F, 0.7f, -48, 0.82f).L(F, 1.0f, -40, 0.72f);
                M(MuscleId.ForearmFlexors, s, fa, fcu, 0.016f, 0.012f, 0.25f, 0.04f, 0.4f, 0.7f);
                var fds = new PB().L(F, 0.04f, -36, 0.95f).L(F, 0.30f, -28, 1.0f).L(F, 0.75f, -20, 0.76f).L(F, 1.0f, -14, 0.66f);
                M(MuscleId.ForearmFlexors, s, fa, fds, 0.017f, 0.012f, 0.28f, 0.04f, 0.4f, 0.7f);
                var ext = new PB().L(F, 0.04f, 148, 0.95f).L(F, 0.30f, 158, 1.02f).L(F, 0.72f, 170, 0.8f).L(F, 1.0f, 180, 0.7f);
                M(MuscleId.ForearmExtensors, s, fa, ext, 0.022f, 0.014f, 0.28f, 0.04f, 0.4f, 0.7f);
            }

            // ----- hand: palm, thenar, fingers -----
            {
                Vector3 dH = P3(s * Mathf.Sin(42 * Mathf.Deg2Rad), -Mathf.Cos(42 * Mathf.Deg2Rad), 0.04f).normalized;
                Vector3 latH = (s * Vector3.Cross(Vector3.forward, dH)).normalized;
                Vector3 palm = WR + dH * 0.048f;
                Quaternion rot = Quaternion.LookRotation(Vector3.forward, dH);
                Ball(fa.muscle, "Palm" + tag, palm, P3(0.034f, 0.052f, 0.014f), handMat, rot);
                Ball(fa.muscle, "Thenar" + tag, palm + latH * 0.02f + Vector3.forward * 0.011f, P3(0.016f, 0.032f, 0.011f), handMat, rot);
                Ball(fa.muscle, "Hypothenar" + tag, palm - latH * 0.025f + Vector3.forward * 0.008f, P3(0.011f, 0.034f, 0.010f), handMat, rot);
                var skinPalm = Ball(fa.skin, "PalmSkin" + tag, palm, P3(0.038f, 0.056f, 0.017f), skinMat, rot);
                body.skinRenderers.Add(skinPalm.GetComponent<Renderer>());
                Ball(fa.bone, "Carpals" + tag, WR + dH * 0.012f, P3(0.024f, 0.016f, 0.012f), boneMat, rot);

                float[] off = { 0.0f, 0.027f, 0.010f, -0.008f, -0.024f };
                float[] len = { 0.062f, 0.082f, 0.090f, 0.082f, 0.066f };
                for (int f = 0; f < 5; f++)
                {
                    Vector3 st, dir;
                    if (f == 0) { st = WR + dH * 0.045f + latH * 0.030f; dir = (dH * 0.75f + latH * 0.55f + Vector3.forward * 0.25f).normalized; }
                    else { st = WR + dH * 0.092f + latH * off[f]; dir = (dH + latH * (off[f] * 1.6f)).normalized; }
                    float L = len[f];
                    var path = V(st, st + dir * L * 0.40f + Vector3.forward * 0.004f, st + dir * L * 0.72f + Vector3.forward * 0.016f, st + dir * L * 0.92f + Vector3.forward * 0.030f);
                    Part(fa.muscle, "Finger" + f + tag, MeshFactory.Tube(path, 0.0092f, 0.0058f, 14, 8), tendonMat);
                    var sp = Part(fa.skin, "FingerSkin" + f + tag, MeshFactory.Tube(path, 0.0112f, 0.0074f, 14, 8), skinMat);
                    body.skinRenderers.Add(sp.GetComponent<Renderer>());
                    Part(fa.bone, "FingerBone" + f + tag, MeshFactory.Tube(path, 0.0048f, 0.0034f, 14, 8), boneMat);
                    // knuckles
                    Ball(fa.bone, "Knuckle", path[1], P3(0.0062f, 0.0062f, 0.0062f), boneMat);
                    Ball(fa.bone, "Knuckle", path[2], P3(0.0056f, 0.0056f, 0.0056f), boneMat);
                }
                // metacarpals
                for (int f = 1; f < 5; f++)
                {
                    var mc = V(WR + dH * 0.02f + latH * (off[f] * 0.5f), WR + dH * 0.092f + latH * off[f]);
                    Part(fa.bone, "Metacarpal", MeshFactory.Tube(mc, 0.0058f, 0.0058f, 8, 8), boneMat);
                }
                var th = V(WR + dH * 0.02f + latH * 0.012f, WR + dH * 0.045f + latH * 0.030f);
                Part(fa.bone, "Metacarpal", MeshFactory.Tube(th, 0.0065f, 0.0058f, 8, 8), boneMat);
            }

            // ----- bones of the arm -----
            {
                var hum = V(SH + P3(-s * 0.012f, 0.004f, 0), SH + dU * 0.1f, SH + dU * 0.2f, EL + dU * -0.01f);
                BoneTube(ua, "Humerus" + tag, hum, 0.0125f, 0.0105f);
                Ball(boneRoot, "HumerusHead" + tag, SH + P3(-s * 0.014f, 0.006f, 0f), P3(0.0225f, 0.0225f, 0.0225f), boneMat);
                Ball(boneRoot, "HumerusCondyle" + tag, EL, P3(0.027f, 0.016f, 0.016f), boneMat, Quaternion.LookRotation(Vector3.forward, Vector3.Cross(Vector3.forward, dU)));
                var rad = V(EL + latF * 0.010f, EL + dF * 0.13f + latF * 0.012f, WR + latF * 0.016f);
                var uln = V(EL - latF * 0.006f - Vector3.forward * 0.004f, EL + dF * 0.13f - latF * 0.010f, WR - latF * 0.014f);
                Part(fa.bone, "Radius" + tag, MeshFactory.Tube(rad, 0.0085f, 0.0095f, 14, 8), boneMat);
                Part(fa.bone, "Ulna" + tag, MeshFactory.Tube(uln, 0.0105f, 0.0072f, 14, 8), boneMat);
                Ball(fa.bone, "Olecranon" + tag, EL - Vector3.forward * 0.018f, P3(0.014f, 0.016f, 0.014f), boneMat);
            }

            // anchors for labels
            AnchorAt("humerus_" + tag, SH + dU * 0.16f);
            AnchorAt("scapula_" + tag, P3(s * 0.12f, 1.38f, -0.085f));
            AnchorAt("clavicle_" + tag, P3(s * 0.10f, 1.47f, 0.06f));
        }

        // ---------- legs ----------
        void BuildLeg(int s)
        {
            string tag = s > 0 ? "L" : "R";
            Vector3 HP = P3(s * 0.09f, 0.90f, 0f), KN = P3(s * 0.085f, 0.50f, 0.015f), AN = P3(s * 0.085f, 0.085f, -0.015f);
            Vector3 dT = (KN - HP).normalized, dS = (AN - KN).normalized;
            Limb T = new Limb(HP, KN, new[] { 0.098f, 0.092f, 0.078f, 0.066f, 0.056f }, new[] { 0.095f, 0.090f, 0.080f, 0.068f, 0.064f },
                              Vector3.forward, (s * Vector3.Cross(Vector3.forward, dT)).normalized);
            Limb S = new Limb(KN, AN, new[] { 0.056f, 0.055f, 0.050f, 0.036f, 0.030f }, new[] { 0.058f, 0.062f, 0.060f, 0.040f, 0.032f },
                              Vector3.forward, (s * Vector3.Cross(Vector3.forward, dS)).normalized);
            Anchor an = stat;
            Loft(T, an, 0.88f, 1.06f, "Thigh" + tag);
            Loft(S, an, 0.88f, 1.06f, "Shank" + tag);
            Ball(muscleRoot, "Knee" + tag, KN, P3(0.054f, 0.05f, 0.054f), baseMat);
            SkinBall(an, "KneeSkin" + tag, KN, P3(0.058f, 0.054f, 0.058f));
            Ball(muscleRoot, "HipBase" + tag, HP, P3(0.104f, 0.10f, 0.100f), baseMat);
            SkinBall(an, "HipSkin" + tag, HP, P3(0.11f, 0.105f, 0.106f));
            float sd = s;

            // thigh
            Vector3 aiis = P3(s * 0.095f, 0.985f, 0.095f), asis = P3(s * 0.135f, 1.0f, 0.085f);
            var rf = new PB().R(aiis, P3(0, 0.3f, 1)).L(T, 0.10f, 0, 0.98f).L(T, 0.45f, 0, 1.06f).L(T, 0.82f, 0, 0.96f).L(T, 1.0f, 0, 0.90f);
            M(MuscleId.RectusFemoris, s, an, rf, 0.030f, 0.024f, 0.45f, 0.10f, 0.22f, 0.65f);
            var vl = new PB().L(T, 0.12f, 62, 1.0f).L(T, 0.40f, 56, 1.06f).L(T, 0.72f, 48, 1.0f).L(T, 0.98f, 30, 0.92f);
            M(MuscleId.VastusLateralis, s, an, vl, 0.034f, 0.024f, 0.42f, 0.08f, 0.15f, 0.6f);
            var vm = new PB().L(T, 0.35f, -52, 1.0f).L(T, 0.68f, -44, 1.08f).L(T, 0.95f, -22, 0.98f);
            M(MuscleId.VastusMedialis, s, an, vm, 0.030f, 0.022f, 0.68f, 0.05f, 0.12f, 0.6f);
            var sar = new PB().R(asis, P3(s * 0.4f, 0.2f, 1)).L(T, 0.18f, -10, 1.0f).L(T, 0.48f, -42, 1.0f).L(T, 0.8f, -80, 0.97f).L(S, 0.12f, -88, 0.97f);
            M(MuscleId.Sartorius, s, an, sar, 0.012f, 0.008f, 0.5f, 0.05f, 0.1f, 0.7f);
            var add = new PB().R(P3(s * 0.030f, 0.86f, 0.04f), P3(-s * 0.5f, 0, 1)).L(T, 0.35f, -86, 0.96f).L(T, 0.75f, -94, 0.92f);
            M(MuscleId.Adductors, s, an, add, 0.024f, 0.018f, 0.35f, 0.03f, 0.1f, 0.6f);
            var gra = new PB().R(P3(s * 0.02f, 0.86f, 0.03f), P3(-s * 0.6f, 0, 0.8f)).L(T, 0.5f, -102, 0.9f).L(T, 0.95f, -110, 0.85f);
            M(MuscleId.Adductors, s, an, gra, 0.010f, 0.009f, 0.4f, 0.03f, 0.2f, 0.7f);
            var tfl = new PB().R(asis + P3(s * 0.01f, 0, -0.005f), P3(s, 0, 0.5f)).L(T, 0.12f, 72, 1.0f).L(T, 0.45f, 80, 0.97f).L(T, 0.95f, 85, 0.9f);
            M(MuscleId.TensorFasciaeLatae, s, an, tfl, 0.020f, 0.012f, 0.18f, 0.0f, 0.6f, 0.7f);
            var gmed = new PB().R(P3(s * 0.155f, 1.0f, -0.02f), P3(s, 0.4f, 0.3f)).R(P3(s * 0.145f, 0.96f, 0.0f), P3(s, 0.1f, 0.3f)).L(T, 0.04f, 92, 1.0f);
            M(MuscleId.GluteusMedius, s, an, gmed, 0.045f, 0.016f, 0.5f, 0f, 0.15f, 0.45f);
            var gmax = new PB().R(P3(s * 0.04f, 0.95f, -0.085f), P3(0, 0.1f, -1)).R(P3(s * 0.10f, 0.9f, -0.098f), P3(s * 0.3f, 0, -1)).L(T, 0.12f, 150, 1.0f);
            M(MuscleId.GluteusMaximus, s, an, gmax, 0.055f, 0.034f, 0.5f, 0f, 0.15f, 0.5f);

            // shank
            var ta = new PB().L(S, 0.04f, 34, 0.96f).L(S, 0.30f, 26, 1.02f).L(S, 0.82f, 6, 0.84f).L(S, 1.0f, -6, 0.72f);
            M(MuscleId.TibialisAnterior, s, an, ta, 0.020f, 0.017f, 0.3f, 0.04f, 0.45f, 0.65f);
            var gl = new PB().L(S, 0.0f, 150, 0.8f).L(S, 0.22f, 148, 1.02f).L(S, 0.58f, 166, 0.9f).L(S, 0.98f, 180, 0.7f);
            M(MuscleId.Gastrocnemius, s, an, gl, 0.026f, 0.026f, 0.3f, 0.02f, 0.4f, 0.65f);
            var gm = new PB().L(S, 0.0f, -150, 0.8f).L(S, 0.22f, -148, 1.02f).L(S, 0.58f, -166, 0.9f).L(S, 0.98f, 180, 0.7f);
            M(MuscleId.Gastrocnemius, s, an, gm, 0.026f, 0.026f, 0.3f, 0.02f, 0.4f, 0.65f);
            var so = new PB().L(S, 0.12f, 100, 0.96f).L(S, 0.42f, 108, 1.0f).L(S, 0.8f, 150, 0.8f);
            M(MuscleId.Soleus, s, an, so, 0.020f, 0.016f, 0.4f, 0.05f, 0.3f, 0.6f);
            var pe = new PB().L(S, 0.08f, 96, 0.97f).L(S, 0.34f, 100, 1.0f).L(S, 0.84f, 112, 0.84f).L(S, 1.0f, 130, 0.7f);
            M(MuscleId.PeroneusLongus, s, an, pe, 0.014f, 0.012f, 0.3f, 0.04f, 0.4f, 0.65f);

            // foot
            Vector3 heel = P3(s * 0.088f, 0.04f, -0.06f), toe = P3(s * 0.105f, 0.026f, 0.17f);
            Limb foot = new Limb(heel, toe, new[] { 0.034f, 0.040f, 0.044f, 0.040f }, new[] { 0.034f, 0.030f, 0.020f, 0.012f }, Vector3.up, P3(s, 0, 0));
            Part(muscleRoot, "Foot" + tag, LoftMesh(foot, 0.95f, 0f, 20, 20), handMat);
            var fs = Part(skinRoot, "FootSkin" + tag, LoftMesh(foot, 1.1f, 0.004f, 20, 20), skinMat);
            body.skinRenderers.Add(fs.GetComponent<Renderer>());
            Ball(muscleRoot, "Ankle" + tag, AN, P3(0.034f, 0.034f, 0.036f), baseMat);
            SkinBall(an, "AnkleSkin" + tag, AN, P3(0.038f, 0.038f, 0.04f));
            for (int i = 0; i < 5; i++)
            {
                float x = s * (0.072f + i * 0.0175f);
                float L = i == 0 ? 0.05f : 0.036f - i * 0.004f;
                var tp = V(P3(x, 0.022f, 0.15f), P3(x, 0.02f, 0.15f + L));
                Part(muscleRoot, "Toe", MeshFactory.Tube(tp, 0.0085f, 0.0065f, 8, 8), tendonMat);
                var ts = Part(skinRoot, "ToeSkin", MeshFactory.Tube(tp, 0.0105f, 0.0085f, 8, 8), skinMat);
                body.skinRenderers.Add(ts.GetComponent<Renderer>());
                Part(boneRoot, "ToeBone", MeshFactory.Tube(tp, 0.0045f, 0.0035f, 8, 8), boneMat);
            }
            // foot bones
            Part(boneRoot, "Tarsus" + tag, MeshFactory.Tube(V(heel + P3(0, 0.0f, 0.0f), P3(s * 0.09f, 0.04f, 0.04f)), 0.022f, 0.02f, 8, 10), boneMat);
            for (int i = 0; i < 5; i++)
            {
                float x = s * (0.072f + i * 0.0175f);
                Part(boneRoot, "Metatarsal", MeshFactory.Tube(V(P3(s * 0.09f, 0.04f, 0.05f), P3(x, 0.025f, 0.15f)), 0.0055f, 0.005f, 8, 8), boneMat);
            }

            // leg bones
            {
                var fem = V(P3(s * 0.062f, 0.925f, 0f), P3(s * 0.100f, 0.895f, 0.0f), P3(s * 0.094f, 0.70f, 0.01f), P3(s * 0.086f, 0.53f, 0.015f));
                BoneTube(an, "Femur" + tag, fem, 0.0165f, 0.0165f);
                Ball(boneRoot, "FemoralHead" + tag, P3(s * 0.065f, 0.925f, 0f), P3(0.024f, 0.024f, 0.024f), boneMat);
                Ball(boneRoot, "Trochanter" + tag, P3(s * 0.108f, 0.895f, -0.005f), P3(0.017f, 0.02f, 0.017f), boneMat);
                Ball(boneRoot, "FemurCondyle" + tag, KN + P3(0, 0.02f, -0.003f), P3(0.042f, 0.024f, 0.032f), boneMat);
                var tib = V(P3(s * 0.082f, 0.495f, 0.018f), P3(s * 0.080f, 0.30f, 0.02f), P3(s * 0.082f, 0.10f, 0.0f));
                BoneTube(an, "Tibia" + tag, tib, 0.0165f, 0.0125f);
                Ball(boneRoot, "TibialPlateau" + tag, KN + P3(0, -0.01f, 0.003f), P3(0.043f, 0.012f, 0.032f), boneMat);
                var fib = V(P3(s * 0.108f, 0.46f, 0.0f), P3(s * 0.104f, 0.28f, -0.004f), P3(s * 0.098f, 0.09f, -0.012f));
                BoneTube(an, "Fibula" + tag, fib, 0.0075f, 0.0065f);
                Ball(boneRoot, "Patella" + tag, P3(s * 0.085f, 0.51f, 0.058f), P3(0.019f, 0.022f, 0.011f), boneMat);
                Ball(boneRoot, "Malleolus" + tag, AN, P3(0.03f, 0.02f, 0.025f), boneMat);
            }
        }

        // ---------- skeleton ----------
        void BuildSkeletonSide(int s)
        {
            string tag = s > 0 ? "L" : "R";
            // ribs: elliptical arcs from spine to costal cartilage
            for (int i = 0; i < 12; i++)
            {
                float ys = 1.445f - i * 0.026f;
                float A = Mathf.Lerp(0.085f, 0.158f, Mathf.Clamp01(i / 7f)) - (i > 8 ? (i - 8) * 0.008f : 0f);
                float B = 0.058f + 0.04f * Mathf.Min(i, 5) / 5f;
                float zc = 0.012f + (i < 4 ? 0.0f : 0.0f);
                float drop = 0.034f + i * 0.0075f;
                float aEnd = i < 7 ? 0.93f : (i < 10 ? 0.80f : 0.62f);
                var path = new List<Vector3>();
                int K = 9;
                for (int k = 0; k < K; k++)
                {
                    float f = (float)k / (K - 1);
                    float a = Mathf.Lerp(0.10f, aEnd, f) * Mathf.PI;
                    path.Add(P3(s * (0.018f + (A - 0.018f) * Mathf.Sin(a)), ys - drop * (a / Mathf.PI), zc - B * Mathf.Cos(a) * 0.85f - 0.003f - (i < 7 ? 0 : 0)));
                }
                var m = i < 7 ? boneMat : boneMat;
                BoneTube(stat, "Rib" + i + tag, path, 0.0068f, 0.006f, m);
                if (i < 7)
                {
                    // costal cartilage
                    var cart = V(path[K - 2], path[K - 1], P3(s * 0.016f, path[K - 1].y - 0.004f, path[K - 1].z + 0.003f));
                    Part(boneRoot, "Cartilage", MeshFactory.Tube(cart, 0.0065f, 0.006f, 8, 8), tendonMat);
                }
            }
            // clavicle, scapula
            var clav = V(P3(s * 0.012f, 1.445f, 0.078f), P3(s * 0.06f, 1.462f, 0.086f), P3(s * 0.12f, 1.476f, 0.046f), P3(s * 0.178f, 1.472f, 0.002f));
            BoneTube(stat, "Clavicle" + tag, clav, 0.0085f, 0.0075f);
            Vector3 n = P3(s * 0.3f, 0, -0.95f).normalized;
            Ball(boneRoot, "Scapula" + tag, P3(s * 0.115f, 1.375f, -0.092f), P3(0.052f, 0.075f, 0.007f), boneMat, Quaternion.LookRotation(n, Vector3.up));
            BoneTube(stat, "Acromion" + tag, V(P3(s * 0.09f, 1.42f, -0.098f), P3(s * 0.15f, 1.468f, -0.04f), P3(s * 0.178f, 1.474f, 0f)), 0.0075f, 0.0075f);
            Ball(boneRoot, "Coracoid" + tag, P3(s * 0.15f, 1.425f, 0.04f), P3(0.008f, 0.008f, 0.012f), boneMat);
            // pelvis
            Vector3 wn = P3(s * 0.65f, 0, 0.76f).normalized;
            Ball(boneRoot, "Ilium" + tag, P3(s * 0.112f, 0.975f, 0.0f), P3(0.078f, 0.068f, 0.012f), boneMat, Quaternion.LookRotation(wn, Vector3.up));
            var pub = V(P3(s * 0.006f, 0.838f, 0.082f), P3(s * 0.045f, 0.805f, 0.074f), P3(s * 0.075f, 0.805f, 0.03f), P3(s * 0.078f, 0.86f, -0.004f));
            BoneTube(stat, "Pubis" + tag, pub, 0.0085f, 0.0095f);
            Ball(boneRoot, "Acetabulum" + tag, P3(s * 0.075f, 0.905f, 0.0f), P3(0.02f, 0.02f, 0.02f), boneMat);
        }

        void BuildSkeletonCentral()
        {
            // spine
            int nv = 24;
            for (int i = 0; i < nv; i++)
            {
                float y = Mathf.Lerp(0.955f, 1.585f, (float)i / (nv - 1));
                float z = -0.07f - 0.008f * Mathf.Sin((y - 0.955f) / 0.63f * Mathf.PI * 2f) + (i > 18 ? 0.025f : 0f);
                float w = i < 12 ? Mathf.Lerp(0.024f, 0.019f, i / 11f) : (i < 19 ? Mathf.Lerp(0.019f, 0.014f, (i - 12) / 6f) : 0.012f);
                Ball(boneRoot, "Vertebra" + i, P3(0, y, z), P3(w, 0.0105f, w * 0.85f), boneMat);
                BoneTube(stat, "Spinous" + i, V(P3(0, y, z - w * 0.5f), P3(0, y - 0.006f, z - w * 1.4f)), 0.004f, 0.003f);
            }
            Ball(boneRoot, "Sacrum", P3(0, 0.915f, -0.08f), P3(0.044f, 0.064f, 0.018f), boneMat, Quaternion.Euler(-12, 0, 0));
            // sternum
            Ball(boneRoot, "Manubrium", P3(0, 1.432f, 0.088f), P3(0.03f, 0.022f, 0.008f), boneMat);
            Ball(boneRoot, "SternumBody", P3(0, 1.345f, 0.099f), P3(0.02f, 0.068f, 0.008f), boneMat);
            Ball(boneRoot, "Xiphoid", P3(0, 1.262f, 0.1f), P3(0.008f, 0.018f, 0.005f), boneMat);
            // skull
            Ball(boneRoot, "Cranium", P3(0, 1.668f, -0.006f), P3(0.072f, 0.083f, 0.09f), boneMat);
            Ball(boneRoot, "Maxilla", P3(0, 1.607f, 0.058f), P3(0.038f, 0.034f, 0.04f), boneMat);
            Ball(boneRoot, "NasalCavity", P3(0, 1.632f, 0.093f), P3(0.0085f, 0.016f, 0.01f), darkMat);
            for (int s = -1; s <= 1; s += 2)
            {
                Ball(boneRoot, "Orbit", P3(s * 0.031f, 1.655f, 0.08f), P3(0.019f, 0.017f, 0.014f), darkMat);
                BoneTube(stat, "Zygomatic", V(P3(s * 0.042f, 1.64f, 0.075f), P3(s * 0.070f, 1.628f, 0.04f), P3(s * 0.074f, 1.64f, 0.0f)), 0.006f, 0.006f);
            }
            var mand = V(P3(-0.058f, 1.636f, 0.0f), P3(-0.054f, 1.598f, 0.004f), P3(-0.036f, 1.572f, 0.044f), P3(0, 1.565f, 0.062f), P3(0.036f, 1.572f, 0.044f), P3(0.054f, 1.598f, 0.004f), P3(0.058f, 1.636f, 0.0f));
            BoneTube(stat, "Mandible", mand, 0.0085f, 0.0085f);
            var teeth = V(P3(-0.032f, 1.588f, 0.046f), P3(0, 1.588f, 0.074f), P3(0.032f, 1.588f, 0.046f));
            Part(boneRoot, "Teeth", MeshFactory.Tube(teeth, 0.006f, 0.006f, 14, 8), whiteMat);
        }

        void BuildOrgans()
        {
            Ball(organRoot, "Heart", P3(0.032f, 1.31f, 0.045f), P3(0.04f, 0.047f, 0.034f), heartMat, Quaternion.Euler(0, 0, -25));
            for (int s = -1; s <= 1; s += 2)
                Ball(organRoot, "Lung", P3(s * 0.078f, 1.345f, 0.003f), P3(0.058f, 0.095f, 0.058f), lungMat, Quaternion.Euler(0, 0, -s * 6f));
            Ball(organRoot, "Brain", P3(0, 1.675f, -0.004f), P3(0.062f, 0.07f, 0.078f), Mats.Make(new Color(0.85f, 0.62f, 0.62f), 0.4f));
        }

        void AnchorsMisc()
        {
            AnchorAt("heart", P3(0.032f, 1.31f, 0.06f));
            AnchorAt("lungs", P3(-0.075f, 1.36f, 0.06f));
            AnchorAt("brain", P3(0, 1.69f, 0.0f));
            AnchorAt("pelvis", P3(0.13f, 0.93f, -0.02f));
            AnchorAt("torso", P3(0, 1.2f, 0.05f));
            AnchorAt("head", P3(0, 1.66f, 0.05f));
            AnchorAt("center", P3(0, 0.875f, 0));
        }
    }
}
