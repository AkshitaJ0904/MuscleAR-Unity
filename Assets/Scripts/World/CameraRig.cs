using UnityEngine;

namespace MuscleAR
{
    /// <summary>
    /// The AR camera. Smoothly flies between per-screen poses, adds handheld sway, can orbit,
    /// and can split the phone viewport into two stacked views (muscle-details screen).
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera main, close, bg;
        public Rect phone = new Rect(0, 0, 1, 1);

        Vector3 pos, look, cp, cl; float fov = 50f, cf = 50f, speed = 2.2f;
        bool orbit; Vector3 oCenter; float oRadius, oHeight, oSpeed, oAngle;
        bool split; Vector3 pos2, look2, cp2, cl2; float fov2 = 40f, cf2 = 40f;
        public bool sway = true;

        public static CameraRig Create(Transform parent)
        {
            var go = new GameObject("CameraRig");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<CameraRig>();

            var bgGo = new GameObject("BackgroundCam");
            bgGo.transform.SetParent(go.transform, false);
            r.bg = bgGo.AddComponent<Camera>();
            r.bg.clearFlags = CameraClearFlags.SolidColor; r.bg.backgroundColor = Color.black;
            r.bg.cullingMask = 0; r.bg.depth = -100;

            var mg = new GameObject("ARCamera");
            mg.tag = "MainCamera";
            mg.transform.SetParent(go.transform, false);
            r.main = mg.AddComponent<Camera>();
            r.main.clearFlags = CameraClearFlags.SolidColor; r.main.backgroundColor = new Color(0.03f, 0.04f, 0.05f);
            r.main.nearClipPlane = 0.03f; r.main.farClipPlane = 40f; r.main.depth = 0; r.main.allowMSAA = true;

            var cg = new GameObject("CloseUpCamera");
            cg.transform.SetParent(go.transform, false);
            r.close = cg.AddComponent<Camera>();
            r.close.clearFlags = CameraClearFlags.SolidColor; r.close.backgroundColor = new Color(0.03f, 0.04f, 0.05f);
            r.close.nearClipPlane = 0.03f; r.close.farClipPlane = 40f; r.close.depth = 1; r.close.allowMSAA = true;
            r.close.enabled = false;
            return r;
        }

        public void Snap(Vector3 p, Vector3 l, float f)
        {
            pos = cp = p; look = cl = l; fov = cf = f; orbit = false;
        }

        public void Frame(Vector3 p, Vector3 l, float f = 45f, float spd = 2.2f)
        {
            pos = p; look = l; fov = f; speed = spd; orbit = false;
        }

        public void Orbit(Vector3 center, float radius, float height, float degPerSec, float startDeg = 20f, float f = 42f)
        {
            orbit = true; oCenter = center; oRadius = radius; oHeight = height; oSpeed = degPerSec; oAngle = startDeg; fov = f; speed = 1.8f;
        }

        public void SetSplit(bool on, Vector3 p2 = default(Vector3), Vector3 l2 = default(Vector3), float f2 = 40f)
        {
            split = on;
            close.enabled = on;
            if (on) { pos2 = p2; look2 = l2; fov2 = f2; if (cp2 == Vector3.zero) { cp2 = p2; cl2 = l2; cf2 = f2; } }
        }

        public void SetPassthrough(bool on)
        {
            // In passthrough mode the real camera image is the background, so the 3D camera must not clear.
            main.clearFlags = on ? CameraClearFlags.Depth : CameraClearFlags.SolidColor;
            close.clearFlags = on ? CameraClearFlags.Depth : CameraClearFlags.SolidColor;
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (orbit)
            {
                oAngle += oSpeed * dt;
                float a = oAngle * Mathf.Deg2Rad;
                pos = oCenter + new Vector3(Mathf.Sin(a) * oRadius, oHeight, Mathf.Cos(a) * oRadius);
                look = oCenter;
            }
            float k = 1f - Mathf.Exp(-speed * dt);
            cp = Vector3.Lerp(cp, pos, k); cl = Vector3.Lerp(cl, look, k); cf = Mathf.Lerp(cf, fov, k);
            Vector3 jitter = Vector3.zero; Quaternion jr = Quaternion.identity;
            if (sway)
            {
                float t = Time.time * 0.55f;
                jitter = new Vector3(Mathf.PerlinNoise(t, 1.3f) - 0.5f, Mathf.PerlinNoise(t, 7.1f) - 0.5f, Mathf.PerlinNoise(t, 3.7f) - 0.5f) * 0.012f;
                jr = Quaternion.Euler((Mathf.PerlinNoise(t, 9.2f) - 0.5f) * 0.35f, (Mathf.PerlinNoise(t, 5.5f) - 0.5f) * 0.35f, 0);
            }
            Place(main, cp + jitter, cl, cf, jr);

            Rect full = phone;
            if (split)
            {
                float k2 = 1f - Mathf.Exp(-2.2f * dt);
                cp2 = Vector3.Lerp(cp2, pos2, k2); cl2 = Vector3.Lerp(cl2, look2, k2); cf2 = Mathf.Lerp(cf2, fov2, k2);
                Place(close, cp2 + jitter, cl2, cf2, jr);
                main.rect = new Rect(full.x, full.y + full.height * 0.5f, full.width, full.height * 0.5f);
                close.rect = new Rect(full.x, full.y, full.width, full.height * 0.5f);
            }
            else main.rect = full;
        }

        static void Place(Camera c, Vector3 p, Vector3 l, float f, Quaternion extra)
        {
            c.transform.position = p;
            Vector3 d = l - p;
            if (d.sqrMagnitude > 1e-6f) c.transform.rotation = Quaternion.LookRotation(d, Vector3.up) * extra;
            c.fieldOfView = f;
        }
    }
}
