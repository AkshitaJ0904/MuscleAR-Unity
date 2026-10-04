using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MuscleAR.EditorTools
{
    /// <summary>Creates the MuscleAR scene, applies mobile player settings and opens the scene on first launch.</summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/MuscleAR.unity";
        static string OpenedKey { get { return "MuscleAR.opened." + Application.dataPath; } }

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(ScenePath)) return;
                if (EditorPrefs.GetBool(OpenedKey, false)) return;
                EditorPrefs.SetBool(OpenedKey, true);
                if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path) && !EditorSceneManager.GetActiveScene().isDirty)
                    EditorSceneManager.OpenScene(ScenePath);
                SetGameViewPortrait();
            };
        }

        [MenuItem("MuscleAR/Setup Project (scene + settings)")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("MuscleAR");
            go.AddComponent<AppFlow>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "MuscleAR";
            PlayerSettings.companyName = "MuscleAR";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.iOS.cameraUsageDescription = "MuscleAR uses the camera to place the anatomy model in your space.";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.musclear.app");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.musclear.app");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.musclear.app");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[MuscleAR] Project set up. Open " + ScenePath + " and press Play.");
        }

        [MenuItem("MuscleAR/Game View -> Phone Portrait (390x844)")]
        public static void SetGameViewPortrait()
        {
            try
            {
                var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
                var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleType.GetProperty("instance").GetValue(null, null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { 0 }); // Standalone
                var groupType = group.GetType();
                var getTotal = groupType.GetMethod("GetTotalCount");
                var getSize = groupType.GetMethod("GetGameViewSize");
                int total = (int)getTotal.Invoke(group, null);
                int index = -1;
                for (int i = 0; i < total; i++)
                {
                    var s = getSize.Invoke(group, new object[] { i });
                    var t = s.GetType();
                    int w = (int)t.GetProperty("width").GetValue(s, null), h = (int)t.GetProperty("height").GetValue(s, null);
                    if (w == 390 && h == 844) { index = i; break; }
                }
                if (index < 0)
                {
                    var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
                    var enumType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var ctor = sizeType.GetConstructor(new[] { enumType, typeof(int), typeof(int), typeof(string) });
                    var size = ctor.Invoke(new object[] { Enum.ToObject(enumType, 1), 390, 844, "MuscleAR Phone Portrait 390x844" });
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = total;
                }
                var gvType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                var win = EditorWindow.GetWindow(gvType);
                gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(win, index, null);
                win.Repaint();
            }
            catch (Exception e)
            {
                Debug.Log("[MuscleAR] Could not set Game view size automatically (" + e.Message + "). Pick any portrait size manually; the app letterboxes itself.");
            }
        }

        [MenuItem("MuscleAR/Open MuscleAR Scene")]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Setup();
        }

        // ---------- headless helpers (used for verification) ----------
        public static void SetupBatch() { Setup(); }

        public static void RunQA()
        {
            if (!File.Exists(ScenePath)) Setup();
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }
    }
}
