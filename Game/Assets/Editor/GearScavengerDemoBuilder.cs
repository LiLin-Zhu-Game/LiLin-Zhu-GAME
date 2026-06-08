using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class GearScavengerDemoBuilder
{
    [MenuItem("Gear Scavenger/Build Windows Demo")]
    private static void BuildWindowsDemo()
    {
        string buildDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Windows"));
        Directory.CreateDirectory(buildDirectory);

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(buildDirectory, "Gear Scavenger.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Gear Scavenger demo build failed: {report.summary.result}");
            return;
        }

        Debug.Log($"Gear Scavenger demo built successfully: {options.locationPathName}");
        EditorUtility.RevealInFinder(options.locationPathName);
    }
}
