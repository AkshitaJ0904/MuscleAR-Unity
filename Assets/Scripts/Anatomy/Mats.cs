using UnityEngine;

namespace MuscleAR
{
    /// <summary>Material + procedural texture factory for the anatomy model and environment.</summary>
    public static class Mats
    {
        static Texture2D fiber, woodTex, noiseTex;

        static Shader Std { get { return Shader.Find("Standard"); } }

        public static Material Make(Color c, float smooth = 0.4f, float metal = 0f, Texture tex = null)
        {
            var m = new Material(Std);
            m.color = c;
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            if (tex != null) m.mainTexture = tex;
            return m;
        }

        public static Material Emissive(Color c, float intensity = 1f, Texture tex = null)
        {
            var m = Make(Color.black, 0.2f, 0f, tex);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c * intensity);
            if (tex != null) { m.SetTexture("_EmissionMap", tex); m.SetColor("_EmissionColor", Color.white * intensity); }
            return m;
        }

        public static Material Unlit(Color c, Texture tex = null, bool additive = false)
        {
            var m = new Material(Shader.Find("Sprites/Default"));
            m.color = c;
            if (tex != null) m.mainTexture = tex;
            m.renderQueue = 3000;
            if (additive)
            {
                m.SetInt("_BlendMode", 1);
            }
            return m;
        }

        public static void SetFade(Material m)
        {
            m.SetFloat("_Mode", 2);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        public static Texture2D FiberTexture()
        {
            if (fiber != null) return fiber;
            int w = 128, h = 256;
            fiber = new Texture2D(w, h, TextureFormat.RGBA32, true);
            fiber.wrapMode = TextureWrapMode.Repeat;
            fiber.filterMode = FilterMode.Bilinear;
            fiber.anisoLevel = 4;
            var rnd = new System.Random(7);
            var col = new float[64];
            for (int i = 0; i < col.Length; i++) col[i] = (float)rnd.NextDouble();
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x / 2f; int i0 = (int)fx % 64, i1 = (i0 + 1) % 64; float f = fx - Mathf.Floor(fx);
                    float line = Mathf.Lerp(col[i0], col[i1], f * f * (3 - 2 * f));
                    float wave = Mathf.Sin(y / (float)h * Mathf.PI * 4f + col[i0] * 6f) * 0.5f + 0.5f;
                    float g = 0.74f + 0.2f * line + 0.06f * wave;
                    if (col[i0] > 0.86f) g *= 0.78f; // dark cleft between fibre bundles
                    px[y * w + x] = new Color(g, g * 0.97f, g * 0.97f, 1f);
                }
            fiber.SetPixels(px); fiber.Apply(true);
            return fiber;
        }

        public static Texture2D NoiseTexture()
        {
            if (noiseTex != null) return noiseTex;
            int s = 64;
            noiseTex = new Texture2D(s, s, TextureFormat.RGBA32, true);
            noiseTex.wrapMode = TextureWrapMode.Repeat;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float n = 0.88f + 0.12f * Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
                    px[y * s + x] = new Color(n, n, n, 1);
                }
            noiseTex.SetPixels(px); noiseTex.Apply(true);
            return noiseTex;
        }

        public static Texture2D WoodTexture()
        {
            if (woodTex != null) return woodTex;
            int w = 256, h = 512;
            woodTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            woodTex.wrapMode = TextureWrapMode.Repeat;
            woodTex.anisoLevel = 4;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float grain = Mathf.PerlinNoise(x * 0.035f, y * 0.004f) * 0.6f + Mathf.PerlinNoise(x * 0.2f, y * 0.01f) * 0.4f;
                    float plank = ((x / 64) % 2 == 0) ? 1f : 0.93f;
                    float seam = (x % 64 < 1) ? 0.55f : 1f;
                    float g = (0.55f + 0.5f * grain) * plank * seam;
                    px[y * w + x] = new Color(g * 0.62f, g * 0.36f, g * 0.22f, 1);
                }
            woodTex.SetPixels(px); woodTex.Apply(true);
            return woodTex;
        }
    }
}
