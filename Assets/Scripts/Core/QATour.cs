using System.Collections;
using System.IO;
using UnityEngine;

namespace MuscleAR
{
    /// <summary>
    /// Developer QA: when the app is launched with "-qatour" it walks every screen and saves a screenshot to /Shots.
    /// </summary>
    public class QATour : MonoBehaviour
    {
        IEnumerator Start()
        {
            var f = AppFlow.I;
            string dir = Path.Combine(Application.dataPath, "../Shots");
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(1.5f);
            // number -> seconds to settle before the shot
            float[] wait = { 2.5f, 2.5f, 1.5f, 4.5f, 2.5f, 2.0f, 2.0f, 2.0f, 2.5f, 2.5f, 2.5f, 3.0f, 3.0f, 3.0f, 2.5f, 2.5f };
            for (int i = 0; i < f.screens.Count; i++)
            {
                f.Go(i);
                int n = f.screens[i].number;
                if (n == 9) { yield return new WaitForSeconds(2.2f); yield return Snap(dir, "screen_09a"); ((SelectPartScreen)f.screens[i]).Select(MuscleId.BicepsBrachii); yield return new WaitForSeconds(0.5f); yield return Snap(dir, "screen_09b"); continue; }
                if (n == 11)
                {
                    yield return new WaitForSeconds(2.2f); yield return Snap(dir, "screen_11a");
                    foreach (var l in new[] { BodyLayer.Skin, BodyLayer.Tendon, BodyLayer.Bone }) { f.body.SetLayer(l); yield return new WaitForSeconds(0.6f); yield return Snap(dir, "screen_11_" + l); }
                    continue;
                }
                yield return new WaitForSeconds(wait[Mathf.Min(i, wait.Length - 1)]);
                yield return Snap(dir, "screen_" + n.ToString("00"));
            }
            Debug.Log("[MuscleAR] QA tour finished: " + dir);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        IEnumerator Snap(string dir, string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
