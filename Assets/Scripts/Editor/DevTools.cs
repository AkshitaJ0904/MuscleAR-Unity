using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MuscleAR.EditorTools
{
    /// <summary>Developer helpers: headless screenshots of the procedural body (used to verify the model).</summary>
    public static class DevTools
    {
        static string OutDir { get { var d = Path.Combine(Application.dataPath, "../Shots"); Directory.CreateDirectory(d); return d; } }

        static void Shot(Camera cam, string name, int w = 900, int h = 1200)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        static Camera MakeCam(Vector3 pos, Vector3 look, float fov)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.1f);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.allowMSAA = true;
            go.transform.position = pos;
            go.transform.LookAt(look);
            return cam;
        }

        static void Lights()
        {
            var l = new GameObject("Key").AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.1f; l.color = new Color(1f, 0.95f, 0.9f);
            l.transform.rotation = Quaternion.Euler(40, 150, 0);
            var f = new GameObject("Fill").AddComponent<Light>();
            f.type = LightType.Directional; f.intensity = 0.5f; f.color = new Color(0.6f, 0.75f, 1f);
            f.transform.rotation = Quaternion.Euler(20, -40, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.38f);
        }

        public static void CaptureBody()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Lights();
            var body = HumanBodyBuilder.Build();

            body.SetLayer(BodyLayer.Muscle);
            var cam = MakeCam(new Vector3(0, 1.0f, 2.6f), new Vector3(0, 0.9f, 0), 38);
            Shot(cam, "body_front_muscle");
            cam.transform.position = new Vector3(0.7f, 1.45f, 1.0f); cam.transform.LookAt(new Vector3(0.1f, 1.35f, 0)); cam.fieldOfView = 40;
            Shot(cam, "body_torso_muscle");
            cam.transform.position = new Vector3(-0.7f, 1.2f, 0.9f); cam.transform.LookAt(new Vector3(-0.27f, 1.2f, 0)); cam.fieldOfView = 30;
            Shot(cam, "body_rarm_muscle");
            cam.transform.position = new Vector3(0.0f, 0.55f, 1.6f); cam.transform.LookAt(new Vector3(0.0f, 0.5f, 0)); cam.fieldOfView = 40;
            Shot(cam, "body_legs_muscle");
            cam.transform.position = new Vector3(0.0f, 1.62f, 0.7f); cam.transform.LookAt(new Vector3(0.0f, 1.62f, 0)); cam.fieldOfView = 30;
            Shot(cam, "body_head_muscle");

            cam.transform.position = new Vector3(0, 1.0f, 2.6f); cam.transform.LookAt(new Vector3(0, 0.9f, 0)); cam.fieldOfView = 38;
            body.SetLayer(BodyLayer.Bone); Shot(cam, "body_front_bone");
            body.SetLayer(BodyLayer.Skin); Shot(cam, "body_front_skin");
            body.SetLayer(BodyLayer.Tendon); Shot(cam, "body_front_tendon");

            // lying pose, side view
            body.SetLayer(BodyLayer.Muscle);
            body.transform.rotation = Quaternion.Euler(-90, 0, 0);
            body.transform.position = new Vector3(0, 0.115f, 0.875f);
            var c2 = MakeCam(new Vector3(0, 1.55f, 2.3f), new Vector3(0, 0.3f, -0.1f), 42);
            Shot(c2, "body_lying_foot");
            c2.transform.position = new Vector3(-1.0f, 0.9f, -0.2f); c2.transform.LookAt(new Vector3(-0.2f, 0.3f, -0.4f));
            Shot(c2, "body_lying_side");
            Debug.Log("[MuscleAR] shots written to " + OutDir);
        }
    }
}
