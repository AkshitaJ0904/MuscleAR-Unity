using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.ARCore;
using UnityEngine.XR.Management;

namespace MuscleAR.Editor
{
    public static class ARSetup
    {
        [MenuItem("MuscleAR/Configure ARCore")]
        public static void Configure()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Configuring ARCore...");

                var getOrCreateMethod = typeof(XRGeneralSettingsPerBuildTarget).GetMethod("GetOrCreate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var buildTargetSettings = getOrCreateMethod != null ? (XRGeneralSettingsPerBuildTarget)getOrCreateMethod.Invoke(null, null) : null;
                if (buildTargetSettings == null)
                {
                    if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out buildTargetSettings))
                    {
                        buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                        EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, buildTargetSettings, true);
                    }
                }
                if (buildTargetSettings == null)
                {
                    Debug.LogError("[MuscleAR] Could not get or create XRGeneralSettingsPerBuildTarget.");
                    return;
                }

                var settings = buildTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Android);
                if (settings != null && settings.Manager != null)
                {
                    bool hasARCore = false;
                    foreach (var loader in settings.Manager.activeLoaders)
                    {
                        if (loader != null && loader.GetType().Name.Contains("ARCore"))
                        {
                            hasARCore = true;
                            break;
                        }
                    }
                    if (hasARCore)
                    {
                        Debug.Log("[MuscleAR] ARCore already configured for Android.");
                        return;
                    }
                }
                bool assigned = XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android);
                sb.AppendLine("Assigned ARCoreLoader: " + assigned);

                settings.Manager.automaticLoading = true;
                settings.Manager.automaticRunning = true;
                settings.InitManagerOnStart = true;

                EditorUtility.SetDirty(settings.Manager);
                EditorUtility.SetDirty(settings);
                EditorUtility.SetDirty(buildTargetSettings);

                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

                AssetDatabase.SaveAssets();

                if (!Directory.Exists("Builds"))
                {
                    Directory.CreateDirectory("Builds");
                }
                File.WriteAllText("Builds/ar_setup_log.txt", sb.ToString());
                Debug.Log("[MuscleAR] " + sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError("[MuscleAR] Error in ARSetup.Configure: " + ex);
            }
        }
    }
}
