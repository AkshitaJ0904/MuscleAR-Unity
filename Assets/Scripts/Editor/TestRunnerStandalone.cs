using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MuscleAR.EditorTools
{
    public static class TestRunnerStandalone
    {
        const string TriggerFlag = "Temp/trigger_test.flag";

        [InitializeOnLoadMethod]
        static void AutoHook()
        {
            EditorApplication.delayCall += () =>
            {
                CheckTestTrigger();
            };
            EditorApplication.update -= CheckTestTrigger;
            EditorApplication.update += CheckTestTrigger;
        }

        static void CheckTestTrigger()
        {
            if (File.Exists(TriggerFlag))
            {
                try { File.Delete(TriggerFlag); } catch { }
                RunAll();
            }
        }

        [MenuItem("MuscleAR/Run Automated Test Suite")]
        public static void RunAll()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== Running MuscleAR Automated Test Suite ===");
                
                // Run Drag-to-Reposition Tests
                DragRepositionTests.RunTests();
                
                // Run UI Tests
                UITests.RunTests();

                File.WriteAllText("Builds/test_results.txt", "TESTS COMPLETED SUCCESSFULLY AT " + DateTime.Now);
                Debug.Log("[TestRunnerStandalone] All tests executed successfully!");
            }
            catch (Exception ex)
            {
                File.WriteAllText("Builds/test_results.txt", "TEST RUN EXCEPTION: " + ex);
                Debug.LogError("[TestRunnerStandalone] Test execution exception: " + ex);
            }
        }
    }
}
