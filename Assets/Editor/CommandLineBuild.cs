using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CommandLineBuild
{
    public static void BuildAndroid()
    {
        Debug.Log("[CommandLineBuild] Starting Android build...");
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = new[] { "Assets/Scenes/MuscleAR.unity" };
        buildPlayerOptions.locationPathName = "Builds/MuscleAR.apk";
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.targetGroup = BuildTargetGroup.Android;
        buildPlayerOptions.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        Debug.Log($"[CommandLineBuild] Result: {summary.result}, TotalTime: {summary.totalTime.TotalSeconds}s, Size: {summary.totalSize} bytes");

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[CommandLineBuild] Build failed with {summary.totalErrors} errors.");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("[CommandLineBuild] Build succeeded!");
            EditorApplication.Exit(0);
        }
    }
}
