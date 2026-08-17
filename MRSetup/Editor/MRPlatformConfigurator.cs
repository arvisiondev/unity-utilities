using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;

/// <summary>
/// Editor tool that switches the project's Android OpenXR feature set, Minimum API Level, and
/// scripting define symbols between a Meta Quest configuration and a PICO configuration.
///
/// OpenXR features are stored per <see cref="BuildTargetGroup"/>, not per Build Profile, so
/// creating separate Android Build Profiles for Meta Quest and PICO does not isolate their
/// feature sets - both profiles read/write the same underlying OpenXR settings. Also, Meta
/// Quest's "Camera (Passthrough)" feature enforces a hard Minimum API Level 32 build
/// validation rule, which is incompatible with PICO 4-series devices (API 29). This tool
/// applies the correct combination for the target vendor in a single step, replacing the
/// manual per-project setup previously required to build
/// <see cref="PicoPassthroughProvider"/> or <see cref="MetaPassthroughProvider"/> passthrough.
///
/// Before applying any change, it verifies that the vendor packages owning each OpenXR feature
/// are actually installed (com.unity.xr.meta-openxr for Meta, com.unity.xr.openxr.picoxr for
/// PICO). If a required package is missing, it aborts without changing any setting and shows
/// exactly which package to install via the Package Manager.
/// </summary>
public static class MRPlatformConfigurator
{
    private const string MetaQuestFeatureId = "com.unity.openxr.feature.metaquest";
    private const string MetaSessionFeatureId = "com.unity.openxr.feature.arfoundation-meta-session";
    private const string MetaCameraFeatureId = "com.unity.openxr.feature.arfoundation-meta-camera";
    private const string CompositionLayersFeatureId = "com.unity.openxr.feature.compositionlayers";
    private const string PicoSupportFeatureId = "com.unity.openxr.feature.pico";
    private const string PicoPassthroughFeatureId = "com.pico.openxr.feature.passthrough";
    private const string PicoScriptingDefine = "PICO_XR_INSTALLED";

    private struct FeatureRequirement
    {
        public string FeatureId;
        public string Label;
        public string RequiredPackage;
    }

    private static readonly FeatureRequirement[] MetaFeatures =
    {
        new FeatureRequirement { FeatureId = MetaQuestFeatureId, Label = "Meta Quest", RequiredPackage = "com.unity.xr.openxr" },
        new FeatureRequirement { FeatureId = MetaSessionFeatureId, Label = "Meta Quest: Session", RequiredPackage = "com.unity.xr.meta-openxr" },
        new FeatureRequirement { FeatureId = MetaCameraFeatureId, Label = "Meta Quest: Camera (Passthrough)", RequiredPackage = "com.unity.xr.meta-openxr" },
    };

    private static readonly FeatureRequirement[] PicoFeatures =
    {
        new FeatureRequirement { FeatureId = PicoSupportFeatureId, Label = "PICO Support", RequiredPackage = "com.unity.xr.openxr.picoxr" },
        new FeatureRequirement { FeatureId = PicoPassthroughFeatureId, Label = "PICO OpenXR Passthrough", RequiredPackage = "com.unity.xr.openxr.picoxr" },
    };

    /// <summary>
    /// Configures Android OpenXR features, Minimum API Level, and scripting defines for a
    /// Meta Quest passthrough build. Aborts with a dialog if the Meta OpenXR package is missing.
    /// </summary>
    [MenuItem("INVELON/MR Setup/Configure Android For Meta Quest")]
    public static void ConfigureForMetaQuest()
    {
        var missing = FindMissingFeatures(MetaFeatures);
        if (missing.Count > 0)
        {
            ShowMissingPackagesDialog("Meta Quest", missing);
            return;
        }

        SetFeatureEnabled(MetaQuestFeatureId, true);
        SetFeatureEnabled(MetaSessionFeatureId, true);
        SetFeatureEnabled(MetaCameraFeatureId, true);
        SetFeatureEnabled(CompositionLayersFeatureId, true);
        SetFeatureEnabled(PicoSupportFeatureId, false);
        SetFeatureEnabled(PicoPassthroughFeatureId, false);

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        RemoveScriptingDefine(PicoScriptingDefine);

        AssetDatabase.SaveAssets();

        ShowSuccessDialog("Meta Quest", new[]
        {
            "Enabled: Meta Quest, Meta Quest: Session, Meta Quest: Camera (Passthrough), Composition Layers Support",
            "Disabled: PICO Support, PICO OpenXR Passthrough",
            "Minimum API Level set to 32 (Android 12L)",
            $"Removed scripting define: {PicoScriptingDefine}",
        });
    }

    /// <summary>
    /// Configures Android OpenXR features, Minimum API Level, and scripting defines for a
    /// PICO passthrough build. Aborts with a dialog if the PICO OpenXR package is missing.
    /// </summary>
    [MenuItem("INVELON/MR Setup/Configure Android For PICO")]
    public static void ConfigureForPico()
    {
        var missing = FindMissingFeatures(PicoFeatures);
        if (missing.Count > 0)
        {
            ShowMissingPackagesDialog("PICO", missing);
            return;
        }

        SetFeatureEnabled(PicoSupportFeatureId, true);
        SetFeatureEnabled(PicoPassthroughFeatureId, true);
        SetFeatureEnabled(CompositionLayersFeatureId, true);
        SetFeatureEnabled(MetaCameraFeatureId, false);
        SetFeatureEnabled(MetaSessionFeatureId, false);
        SetFeatureEnabled(MetaQuestFeatureId, false);

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        AddScriptingDefine(PicoScriptingDefine);

        AssetDatabase.SaveAssets();

        ShowSuccessDialog("PICO", new[]
        {
            "Enabled: PICO Support, PICO OpenXR Passthrough, Composition Layers Support",
            "Disabled: Meta Quest, Meta Quest: Session, Meta Quest: Camera (Passthrough)",
            "Minimum API Level set to 29 (Android 10.0)",
            $"Added scripting define: {PicoScriptingDefine}",
        });
    }

    private static List<FeatureRequirement> FindMissingFeatures(FeatureRequirement[] requirements)
    {
        var missing = new List<FeatureRequirement>();
        foreach (var requirement in requirements)
        {
            var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, requirement.FeatureId);
            if (feature == null)
                missing.Add(requirement);
        }

        return missing;
    }

    private static void SetFeatureEnabled(string featureId, bool isEnabled)
    {
        var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, featureId);
        if (feature == null)
        {
            Debug.LogWarning($"MRPlatformConfigurator: OpenXR feature '{featureId}' was not " +
                "found for Android. Is the corresponding package installed?");
            return;
        }

        feature.enabled = isEnabled;
        EditorUtility.SetDirty(feature);
    }

    private static void AddScriptingDefine(string define)
    {
        var target = NamedBuildTarget.Android;
        PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
        if (Array.IndexOf(defines, define) >= 0)
            return;

        var updated = new string[defines.Length + 1];
        defines.CopyTo(updated, 0);
        updated[defines.Length] = define;
        PlayerSettings.SetScriptingDefineSymbols(target, updated);
    }

    private static void RemoveScriptingDefine(string define)
    {
        var target = NamedBuildTarget.Android;
        PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
        var updated = Array.FindAll(defines, d => d != define);
        if (updated.Length != defines.Length)
            PlayerSettings.SetScriptingDefineSymbols(target, updated);
    }

    private static void ShowMissingPackagesDialog(string vendorLabel, List<FeatureRequirement> missing)
    {
        var requiredPackages = missing.Select(m => m.RequiredPackage).Distinct().ToList();
        var builder = new StringBuilder();
        builder.AppendLine($"Cannot configure Android for {vendorLabel}: required OpenXR features are missing.");
        builder.AppendLine();
        builder.AppendLine("Missing features:");
        foreach (var requirement in missing)
            builder.AppendLine($"  • {requirement.Label} (needs package: {requirement.RequiredPackage})");
        builder.AppendLine();
        builder.AppendLine("Install the missing package(s) via Window > Package Manager, then run this command again. No settings were changed.");

        Debug.LogWarning($"MRPlatformConfigurator: {builder}");
        EditorUtility.DisplayDialog(
            $"MR Setup — {vendorLabel} packages missing",
            builder.ToString(),
            "OK");
    }

    private static void ShowSuccessDialog(string vendorLabel, string[] summaryLines)
    {
        var message = string.Join("\n", summaryLines);
        Debug.Log($"MRPlatformConfigurator: Android configured for {vendorLabel}.\n{message}");
        EditorUtility.DisplayDialog(
            $"MR Setup — Configured for {vendorLabel}",
            message,
            "OK");
    }
}
