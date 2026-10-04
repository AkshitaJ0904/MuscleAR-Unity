using UnityEngine;
using UnityEngine.UI;

namespace MuscleAR
{
    /// <summary>
    /// Live device-camera background (WebCamTexture). On a phone this is the rear camera; in the Editor it is the
    /// computer's webcam. Falls back silently to the virtual lab if no camera is available / permitted.
    /// </summary>
    public class PassthroughFeed : MonoBehaviour
    {
        public bool Active { get; private set; }
        public string Status = "";
        WebCamTexture tex;
        RawImage img;
        RectTransform imgRt;
        Canvas canvas;

        public static PassthroughFeed Create(Transform parent, Camera bgCam)
        {
            var go = new GameObject("PassthroughFeed");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<PassthroughFeed>();
            var cgo = new GameObject("FeedCanvas");
            cgo.transform.SetParent(go.transform, false);
            p.canvas = cgo.AddComponent<Canvas>();
            p.canvas.renderMode = RenderMode.ScreenSpaceCamera;
            p.canvas.worldCamera = bgCam;
            p.canvas.planeDistance = 1f;
            bgCam.cullingMask = 1 << LayerMask.NameToLayer("UI");
            var igo = new GameObject("Feed");
            igo.transform.SetParent(cgo.transform, false);
            p.img = igo.AddComponent<RawImage>();
            p.imgRt = igo.GetComponent<RectTransform>();
            p.img.raycastTarget = false;
            igo.SetActive(false);
            return p;
        }

        public bool TryStart()
        {
            if (Active) return true;
            var devs = WebCamTexture.devices;
            if (devs.Length == 0) { Status = "No camera found – using virtual lab"; return false; }
            string name = devs[0].name;
            foreach (var d in devs) if (!d.isFrontFacing) { name = d.name; break; }
            tex = new WebCamTexture(name, 1280, 720, 30);
            tex.Play();
            img.texture = tex;
            img.gameObject.SetActive(true);
            Active = true; Status = "Live camera: " + name;
            return true;
        }

        public void Stop()
        {
            if (tex != null) { tex.Stop(); Destroy(tex); tex = null; }
            img.gameObject.SetActive(false);
            Active = false;
        }

        /// <summary>Fit the feed into the phone viewport (fractions of the screen), cover-cropped.</summary>
        public void Layout(Rect phone)
        {
            if (!Active || tex == null) return;
            // The bg camera canvas is full screen; size the image to the phone rect in canvas pixels
            var rt = canvas.GetComponent<RectTransform>();
            Vector2 s = rt.rect.size;
            imgRt.anchorMin = imgRt.anchorMax = new Vector2(0.5f, 0.5f);
            imgRt.sizeDelta = new Vector2(phone.width * s.x, phone.height * s.y);
            imgRt.anchoredPosition = new Vector2((phone.center.x - 0.5f) * s.x, (phone.center.y - 0.5f) * s.y);
            float ta = (float)tex.width / Mathf.Max(1, tex.height);
            if (tex.videoRotationAngle == 90 || tex.videoRotationAngle == 270) ta = 1f / ta;
            float pa = (phone.width * Screen.width) / Mathf.Max(1f, phone.height * Screen.height);
            Rect uv = new Rect(0, 0, 1, 1);
            if (ta > pa) { uv.width = pa / ta; uv.x = (1f - uv.width) * 0.5f; }
            else { uv.height = ta / pa; uv.y = (1f - uv.height) * 0.5f; }
            img.uvRect = uv;
            imgRt.localEulerAngles = new Vector3(0, 0, -tex.videoRotationAngle);
            imgRt.localScale = new Vector3(1, tex.videoVerticallyMirrored ? -1 : 1, 1);
        }
    }
}
