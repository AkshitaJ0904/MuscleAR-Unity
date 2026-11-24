using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MuscleAR.EditorTools
{
    public static class BuildScript
    {
        const string TriggerFlag = "Temp/trigger_build.flag";

        [InitializeOnLoadMethod]
        static void AutoTriggerIfRequested()
        {
            EditorApplication.delayCall += () =>
            {
                CheckTrigger();
            };
            EditorApplication.update -= CheckTrigger;
            EditorApplication.update += CheckTrigger;
        }

        static void CheckTrigger()
        {
            if (File.Exists(TriggerFlag))
            {
                try
                {
                    File.Delete(TriggerFlag);
                }
                catch { }
                BuildAndroid();
            }
        }

        [MenuItem("MuscleAR/Build Android APK")]
        public static void BuildAndroid()
        {
            try
            {
                Debug.Log("[MuscleAR] Starting Android APK Build...");
                Directory.CreateDirectory("Builds");

                // Execute automated Drag-to-Reposition and Mode tests
                DragRepositionTests.RunTests();

                // Execute automated UI and Dismissible Panel tests
                UITests.RunTests();

                // Ensure ARCore & Player Settings are configured
                MuscleAR.Editor.ARSetup.Configure();

                var buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/MuscleAR.unity" },
                    locationPathName = "Builds/MuscleAR.apk",
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                var summary = report.summary;

                var resultText = $"BUILD RESULT: {summary.result}\n" +
                                 $"Total Errors: {summary.totalErrors}\n" +
                                 $"Total Warnings: {summary.totalWarnings}\n" +
                                 $"Total Time: {summary.totalTime.TotalSeconds:F2}s\n" +
                                 $"Output Size: {summary.totalSize} bytes\n" +
                                 $"Output Path: {summary.outputPath}\n";

                File.WriteAllText("Builds/build_summary.txt", resultText);
                var details = new System.Text.StringBuilder();
                foreach (var step in report.steps)
                    foreach (var message in step.messages)
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                            details.AppendLine(message.content);
                File.WriteAllText("Logs/BuildErrors.txt", details.ToString());
                Debug.Log("[MuscleAR] " + resultText);

                if (summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[MuscleAR] Build Succeeded! APK located at: {summary.outputPath}");
                    AutoInstallToDevice(summary.outputPath);
                }
                else
                {
                    Debug.LogError($"[MuscleAR] Build failed with result: {summary.result}");
                }

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[MuscleAR] Build exception: " + ex);
                File.WriteAllText("Builds/build_summary.txt", "BUILD EXCEPTION: " + ex);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        public static void AutoInstallToDevice(string apkPath)
        {
            try
            {
                string adbPath = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe");
                if (!File.Exists(adbPath)) adbPath = @"E:\Unity\Editors\6000.5.5f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe";
                if (!File.Exists(adbPath)) adbPath = @"C:\Users\Kanishk\AppData\Local\Android\Sdk\platform-tools\adb.exe";
                if (!File.Exists(adbPath)) adbPath = "adb";

                var p = new System.Diagnostics.Process();
                p.StartInfo.FileName = adbPath;
                p.StartInfo.Arguments = $"install -r -d -g \"{Path.GetFullPath(apkPath)}\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                Debug.Log("[MuscleAR ADB Install] " + output);

                // Launch app
                var launchP = new System.Diagnostics.Process();
                launchP.StartInfo.FileName = adbPath;
                launchP.StartInfo.Arguments = "shell monkey -p com.musclear.app -c android.intent.category.LAUNCHER 1";
                launchP.StartInfo.UseShellExecute = false;
                launchP.StartInfo.CreateNoWindow = true;
                launchP.Start();
                launchP.WaitForExit();
                Debug.Log("[MuscleAR ADB Launch] Launched on device.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MuscleAR] AutoInstallToDevice notice: " + ex.Message);
            }
        }
    }
}

