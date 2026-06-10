using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class GearScavengerDemoBuilder
{
    private static readonly string[] RequiredReleaseAssets =
    {
        "Assets/Scenes/SampleScene.unity",
        "Assets/Scripts/GearScavengerGame.cs",
        "Assets/Scripts/PlayerController.cs",
        "Assets/Resources/GearScavenger/player.png",
        "Assets/Resources/GearScavenger/enemy_chaser.png",
        "Assets/Resources/GearScavenger/enemy_drone.png",
        "Assets/Resources/GearScavenger/enemy_support.png",
        "Assets/Resources/GearScavenger/enemy_boss.png"
    };

    [MenuItem("Gear Scavenger/Run Release Readiness Check")]
    private static void RunReleaseReadinessCheck()
    {
        ValidateReleaseReadiness(true);
    }

    [MenuItem("Gear Scavenger/Build Windows Demo")]
    private static void BuildWindowsDemo()
    {
        if (!ValidateReleaseReadiness(true))
        {
            Debug.LogError("Gear Scavenger demo build cancelled until release readiness errors are fixed.");
            return;
        }

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

    private static bool ValidateReleaseReadiness(bool logSuccess)
    {
        List<string> errors = new List<string>();
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            errors.Add("No enabled scene is configured in Build Settings.");
        }

        foreach (string requiredAsset in RequiredReleaseAssets)
        {
            if (AssetDatabase.LoadMainAssetAtPath(requiredAsset) == null)
            {
                errors.Add($"Missing required release asset: {requiredAsset}");
            }
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError($"Release readiness: {error}");
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log($"Gear Scavenger release readiness passed: {scenes.Length} enabled scene(s), all required scripts and fallback runtime sprites found.");
        }

        return true;
    }
}
