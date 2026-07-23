#nullable enable

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Icebreaker.Integration.Editor
{
    public static class BuildCommands
    {
        public static void BuildMacOS()
        {
            Build(BuildTarget.StandaloneOSX, "Builds/StandaloneOSX/ICEBREAKER.app");
        }

        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/StandaloneWindows64/ICEBREAKER.exe");
        }

        public static void BuildWindowsDemo()
        {
            Build(
                BuildTarget.StandaloneWindows64,
                "Builds/StandaloneWindows64Demo/ICEBREAKER_DEMO.exe",
                new[] { "ICEBREAKER_DEMO" });
        }

        private static void Build(
            BuildTarget target,
            string locationPathName,
            string[]? extraScriptingDefines = null)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[BUILD] no enabled scenes");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = locationPathName,
                target = target,
                options = BuildOptions.None,
                extraScriptingDefines = extraScriptingDefines ?? Array.Empty<string>()
            };

            var report = BuildPipeline.BuildPlayer(options);
            var success = report.summary.result == BuildResult.Succeeded;

            if (success)
            {
                Debug.Log($"[BUILD] {target} build succeeded: {locationPathName} ({report.summary.totalSize} bytes)");
            }
            else
            {
                Debug.LogError($"[BUILD] {target} build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(success ? 0 : 1);
            }
        }
    }
}
