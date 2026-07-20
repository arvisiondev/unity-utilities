using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace INVELON.Editor
{
    /// <summary>
    /// Editor window for building Android APKs targeting META Quest or PICO devices.
    /// Automatically switches OpenXR interaction profiles per platform and names the APK
    /// using the convention: [ProductName]_[PLATFORM]_[VERSION].apk
    /// </summary>
    public class VRGameBuilder : EditorWindow
    {
        // ──────────────────────────────────────────────────────────────────────────
        //  Platform definitions
        // ──────────────────────────────────────────────────────────────────────────

        private enum VRPlatform { META, PICO }

        /// <summary>
        /// OpenXR featureIdInternal values that should be ENABLED when building for META.
        /// These are read from the serialised OpenXR Package Settings asset.
        /// </summary>
        /// <summary>OpenXR feature IDs that must be ENABLED when building for META.</summary>
        private static readonly string[] MetaFeatureIds =
        {
            "com.unity.openxr.feature.input.oculustouch",    // Oculus Touch Controller Profile
            "com.unity.openxr.feature.input.metaquestplus",  // Meta Quest Touch Plus Controller Profile
        };

        /// <summary>OpenXR feature IDs that must be ENABLED when building for PICO.</summary>
        private static readonly string[] PicoFeatureIds =
        {
            "com.unity.openxr.feature.input.PICO4touch",      // PICO4 Touch Controller Profile
            "com.unity.openxr.feature.input.PICO4Ultratouch", // PICO4 Ultra Touch Controller Profile
            "com.unity.openxr.feature.input.PICOG3touch",     // PICOG3 Touch Controller Profile
            // PICO runtime feature — required for PICO builds, must be OFF for META to pass validation
            "com.unity.openxr.pico.features",
        };

        // OpenXR Feature Group (FeatureSet) IDs — used to persist the selection in OpenXREditorSettings
        private const string PicoFeatureSetId     = "com.picoxr.openxr.features";
        private const string MetaFeatureSetUiName = "Meta Quest";

        private static readonly Dictionary<string, string> FeatureDisplayNames = new()
        {
            { "com.unity.openxr.feature.input.oculustouch",     "Oculus Touch Controller Profile" },
            { "com.unity.openxr.feature.input.metaquestplus",   "Meta Quest Touch Plus Controller Profile" },
            { "com.unity.openxr.feature.input.PICO4touch",      "PICO4 Touch Controller Profile" },
            { "com.unity.openxr.feature.input.PICO4Ultratouch", "PICO4 Ultra Touch Controller Profile" },
            { "com.unity.openxr.feature.input.PICOG3touch",     "PICOG3 Touch Controller Profile" },
            { "com.unity.openxr.pico.features",                 "PICO OpenXR Features (runtime)" },
        };

        // ──────────────────────────────────────────────────────────────────────────
        //  Window state
        // ──────────────────────────────────────────────────────────────────────────

        private VRPlatform _platform = VRPlatform.META;

        // Version
        private string _editableVersion = "";
        private bool   _versionPendingApply = false;

        // Scenes
        private List<(string path, bool enabled)> _scenes = new();
        private bool _showScenes   = true;
        private bool _showOptions  = false;

        // Output
        private string _outputFolder = "Builds/Android";

        // Build options
        private bool _developmentBuild      = false;
        private bool _connectProfiler       = false;
        private bool _scriptOnlyBuild       = false;
        private bool _autoIncrementBundleCode = true;
        private bool _applyProfilesOnly     = false;

        // Scroll
        private Vector2 _scroll;

        // ──────────────────────────────────────────────────────────────────────────
        //  Colors
        // ──────────────────────────────────────────────────────────────────────────

        private static readonly Color MetaColor  = new(0.18f, 0.47f, 0.90f);
        private static readonly Color PicoColor  = new(0.07f, 0.60f, 0.50f);
        private static readonly Color DimColor   = new(0.25f, 0.25f, 0.25f);
        private static readonly Color SepColor   = new(0.30f, 0.30f, 0.30f, 0.50f);

        // ──────────────────────────────────────────────────────────────────────────
        //  Menu item
        // ──────────────────────────────────────────────────────────────────────────

        [MenuItem("INVELON/Build/VR Game Builder")]
        public static void OpenWindow()
        {
            var w = GetWindow<VRGameBuilder>("VR Game Builder");
            w.minSize = new Vector2(420, 600);
            w.Show();
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ──────────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            RefreshSceneList();
            _editableVersion = PlayerSettings.bundleVersion;
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  GUI
        // ──────────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            DrawPlatformSelector();
            DrawApkPreview();
            DrawSeparator();
            DrawVersionSection();
            DrawSeparator();
            DrawScenesSection();
            DrawSeparator();
            DrawOutputSection();
            DrawSeparator();
            DrawBuildOptionsSection();
            DrawSeparator();
            DrawActionButtons();

            EditorGUILayout.EndScrollView();
        }

        // ── Header ───────────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("VR Game Builder", new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 });
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Unity {Application.unityVersion}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);
        }

        // ── Platform selector ─────────────────────────────────────────────────────

        private void DrawPlatformSelector()
        {
            EditorGUILayout.LabelField("Target Platform", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPlatformBtn("  META Quest", VRPlatform.META, MetaColor);
                DrawPlatformBtn("  PICO",       VRPlatform.PICO, PicoColor);
            }

            EditorGUILayout.Space(4);

            // Active profiles preview
            string[] activeIds = _platform == VRPlatform.META ? MetaFeatureIds : PicoFeatureIds;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Interaction profiles that will be enabled:", EditorStyles.miniLabel);
                foreach (string id in activeIds)
                {
                    string label = FeatureDisplayNames.TryGetValue(id, out string name) ? name : id;
                    EditorGUILayout.LabelField($"  ✓  {label}", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(4);
        }

        private void DrawPlatformBtn(string label, VRPlatform target, Color activeColor)
        {
            bool isActive = _platform == target;
            var old = GUI.backgroundColor;
            GUI.backgroundColor = isActive ? activeColor : DimColor;

            var style = new GUIStyle(GUI.skin.button) { fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal };
            if (GUILayout.Button(label, style, GUILayout.Height(38)))
                _platform = target;

            GUI.backgroundColor = old;
        }

        // ── APK name preview ──────────────────────────────────────────────────────

        private void DrawApkPreview()
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("APK Name:", EditorStyles.miniLabel, GUILayout.Width(62));
                EditorGUILayout.SelectableLabel(BuildApkName(), EditorStyles.boldLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        // ── Version ───────────────────────────────────────────────────────────────

        private void DrawVersionSection()
        {
            EditorGUILayout.LabelField("Version", EditorStyles.boldLabel);

            // Bundle version string
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Bundle Version:", GUILayout.Width(110));
                string newVer = EditorGUILayout.TextField(_editableVersion);
                if (newVer != _editableVersion)
                {
                    _editableVersion = newVer;
                    _versionPendingApply = true;
                }
                using (new EditorGUI.DisabledGroupScope(!_versionPendingApply))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(50)))
                        ApplyVersion();
                }
            }

            // Bump buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Quick Bump:", GUILayout.Width(110));
                if (GUILayout.Button("Patch")) BumpVersion(2);
                if (GUILayout.Button("Minor")) BumpVersion(1);
                if (GUILayout.Button("Major")) BumpVersion(0);
            }

            EditorGUILayout.Space(2);

            // Bundle version code
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Bundle Code:", GUILayout.Width(110));
                int code = EditorGUILayout.IntField(PlayerSettings.Android.bundleVersionCode);
                if (code != PlayerSettings.Android.bundleVersionCode)
                    PlayerSettings.Android.bundleVersionCode = code;
            }

            _autoIncrementBundleCode = EditorGUILayout.Toggle("Auto-increment Code", _autoIncrementBundleCode);
        }

        // ── Scenes ────────────────────────────────────────────────────────────────

        private void DrawScenesSection()
        {
            int enabledCount = _scenes.Count(s => s.enabled);
            _showScenes = EditorGUILayout.Foldout(_showScenes, $"Scenes  ({enabledCount} / {_scenes.Count})", true, EditorStyles.foldoutHeader);

            if (!_showScenes) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All",     EditorStyles.miniButtonLeft))  SetAllScenes(true);
                if (GUILayout.Button("None",    EditorStyles.miniButtonMid))   SetAllScenes(false);
                if (GUILayout.Button("Refresh", EditorStyles.miniButtonRight)) RefreshSceneList();
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_scenes.Count == 0)
                {
                    EditorGUILayout.HelpBox("No scenes configured in Build Settings (File → Build Settings).", MessageType.Warning);
                }
                else
                {
                    for (int i = 0; i < _scenes.Count; i++)
                    {
                        var (path, enabled) = _scenes[i];
                        bool newEnabled = EditorGUILayout.ToggleLeft(Path.GetFileNameWithoutExtension(path), enabled);
                        if (newEnabled != enabled)
                            _scenes[i] = (path, newEnabled);
                    }
                }
            }
        }

        // ── Output folder ─────────────────────────────────────────────────────────

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("Output Folder", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField(_outputFolder);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Select Output Folder", _outputFolder, "");
                    if (!string.IsNullOrEmpty(picked))
                        _outputFolder = picked;
                }
            }
        }

        // ── Build options ─────────────────────────────────────────────────────────

        private void DrawBuildOptionsSection()
        {
            _showOptions = EditorGUILayout.Foldout(_showOptions, "Build Options", true, EditorStyles.foldoutHeader);
            if (!_showOptions) return;

            _developmentBuild = EditorGUILayout.Toggle("Development Build", _developmentBuild);

            using (new EditorGUI.DisabledGroupScope(!_developmentBuild))
            {
                EditorGUI.indentLevel++;
                _connectProfiler  = EditorGUILayout.Toggle("Auto-connect Profiler",   _connectProfiler);
                _scriptOnlyBuild  = EditorGUILayout.Toggle("Script Only Rebuild",     _scriptOnlyBuild);
                EditorGUI.indentLevel--;
            }
        }

        // ── Action buttons ────────────────────────────────────────────────────────

        private void DrawActionButtons()
        {
            EditorGUILayout.Space(6);

            bool hasScenes   = _scenes.Any(s => s.enabled);
            bool hasOutput   = !string.IsNullOrWhiteSpace(_outputFolder);
            bool isCompiling = EditorApplication.isCompiling;

            // Apply profiles only
            if (GUILayout.Button("Apply Interaction Profiles Only", GUILayout.Height(28)))
            {
                EditorPrefs.SetString(VRGameBuilderPrefs.LastPlatformKey, _platform.ToString());
                ApplyInteractionProfiles(_platform);
                EditorUtility.DisplayDialog("Profiles Applied",
                    $"OpenXR interaction profiles have been configured for {_platform}.", "OK");
            }

            EditorGUILayout.Space(4);

            // Main build button
            Color platformColor = _platform == VRPlatform.META ? MetaColor : PicoColor;
            bool canBuild = hasScenes && hasOutput && !isCompiling;

            using (new EditorGUI.DisabledGroupScope(!canBuild))
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = canBuild ? platformColor : DimColor;

                string btnLabel = isCompiling ? "Compiling…" : $"Build  {_platform}  ▶";
                if (GUILayout.Button(btnLabel, GUILayout.Height(46)))
                    PerformBuild();

                GUI.backgroundColor = old;
            }

            if (!hasScenes)
                EditorGUILayout.HelpBox("Enable at least one scene to build.", MessageType.Warning);
            else if (!hasOutput)
                EditorGUILayout.HelpBox("Set an output folder.", MessageType.Warning);

            EditorGUILayout.Space(10);
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  Build logic
        // ──────────────────────────────────────────────────────────────────────────

        private void PerformBuild()
        {
            // 1. Persist target platform for the manifest patcher
            EditorPrefs.SetString(VRGameBuilderPrefs.LastPlatformKey, _platform.ToString());

            // 2. Switch OpenXR interaction profiles
            ApplyInteractionProfiles(_platform);

            // 3. Apply any pending version string
            if (_versionPendingApply) ApplyVersion();

            // 4. Bump bundle version code
            if (_autoIncrementBundleCode)
                PlayerSettings.Android.bundleVersionCode++;

            // 4. Resolve APK path
            string apkName    = BuildApkName();
            string outputPath = Path.Combine(_outputFolder, apkName);

            if (!Directory.Exists(_outputFolder))
                Directory.CreateDirectory(_outputFolder);

            // 5. Compose build options
            BuildOptions options = BuildOptions.None;
            if (_developmentBuild) options |= BuildOptions.Development;
            if (_connectProfiler && _developmentBuild)  options |= BuildOptions.ConnectWithProfiler;
            if (_scriptOnlyBuild && _developmentBuild)  options |= BuildOptions.BuildScriptsOnly;

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes           = _scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = outputPath,
                target           = BuildTarget.Android,
                options          = options,
            };

            // 6. Save before build
            AssetDatabase.SaveAssets();

            // 7. Build
            Debug.Log($"[VRGameBuilder] Starting {_platform} build → {outputPath}");
            BuildReport  report  = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            // 8. Result
            if (summary.result == BuildResult.Succeeded)
            {
                long   sizeMB    = (long)(summary.totalSize / 1024 / 1024);
                string duration  = summary.totalTime.ToString(@"mm\:ss");

                bool openFolder = EditorUtility.DisplayDialog(
                    "Build Succeeded ✓",
                    $"Platform : {_platform}\n" +
                    $"APK      : {apkName}\n" +
                    $"Size     : {sizeMB} MB\n" +
                    $"Duration : {duration}",
                    "Open Folder", "Close");

                if (openFolder)
                    EditorUtility.RevealInFinder(outputPath);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Build Failed",
                    $"Build failed for {_platform}.\nCheck the Console for details.",
                    "OK");
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  OpenXR profile switching
        // ──────────────────────────────────────────────────────────────────────────

        private static void ApplyInteractionProfiles(VRPlatform platform)
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogError("[VRGameBuilder] OpenXR settings not found for Android. " +
                               "Make sure OpenXR is configured in XR Plug-in Management.");
                return;
            }

            string[] enableIds  = platform == VRPlatform.META ? MetaFeatureIds : PicoFeatureIds;
            string[] disableIds = platform == VRPlatform.META ? PicoFeatureIds : MetaFeatureIds;

            // ── 1. Toggle individual interaction profile features ─────────────────
            OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();

            foreach (OpenXRFeature feature in features)
            {
                // Use SerializedObject to both read featureIdInternal and write m_enabled,
                // so Unity's serialization pipeline persists the change to disk correctly.
                var    so      = new SerializedObject(feature);
                string fid     = so.FindProperty("featureIdInternal")?.stringValue ?? string.Empty;
                string display = FeatureDisplayNames.TryGetValue(fid, out string name)
                                 ? name
                                 : (so.FindProperty("nameUi")?.stringValue ?? fid);

                SerializedProperty enabledProp = so.FindProperty("m_enabled");
                if (enabledProp == null) continue;

                bool currentlyEnabled = enabledProp.boolValue;

                if (enableIds.Contains(fid) && !currentlyEnabled)
                {
                    so.Update();
                    enabledProp.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[VRGameBuilder] Enabled  : {display}");
                }
                else if (disableIds.Contains(fid) && currentlyEnabled)
                {
                    so.Update();
                    enabledProp.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[VRGameBuilder] Disabled : {display}");
                }
            }

            // ── 2. Persist Feature Group selection in OpenXREditorSettings ────────
            ApplyFeatureGroups(platform);

            // ── 3. Flush everything to disk ───────────────────────────────────────
            AssetDatabase.SaveAssets();
            Debug.Log($"[VRGameBuilder] Interaction profiles applied for {platform}.");
        }

        /// <summary>
        /// Persists the Feature Group (FeatureSet) enabled state to OpenXREditorSettings.
        /// Does NOT call SetFeaturesFromEnabledFeatureSets — that method conditionally overrides
        /// individual feature enabled states based on a wasEnabled/isEnabled delta that is
        /// unreliable outside the Project Settings UI, and would silently undo the SerializedObject
        /// changes already applied above.
        /// </summary>
        private static void ApplyFeatureGroups(VRPlatform platform)
        {
            const BuildTargetGroup target = BuildTargetGroup.Android;
            bool wantMeta = platform == VRPlatform.META;
            bool wantPico = platform == VRPlatform.PICO;

            var featureSets = OpenXRFeatureSetManager.FeatureSetsForBuildTarget(target);
            if (featureSets == null) return;

            foreach (var fs in featureSets)
            {
                bool isPico = string.Equals(fs.featureSetId, PicoFeatureSetId,
                                            System.StringComparison.OrdinalIgnoreCase);
                bool isMeta = fs.name != null &&
                              fs.name.IndexOf(MetaFeatureSetUiName,
                                              System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (isPico)
                {
                    fs.isEnabled = wantPico;
                    Debug.Log($"[VRGameBuilder] Feature Group '{fs.name}' → {(wantPico ? "ON" : "OFF")}");
                }
                else if (isMeta)
                {
                    fs.isEnabled = wantMeta;
                    Debug.Log($"[VRGameBuilder] Feature Group '{fs.name}' → {(wantMeta ? "ON" : "OFF")}");
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Generates the APK file name following [ProductName]_[PLATFORM]_[VERSION].apk</summary>
        private string BuildApkName()
        {
            string product  = SanitizeName(PlayerSettings.productName);
            string version  = _versionPendingApply ? _editableVersion : PlayerSettings.bundleVersion;
            string platform = _platform.ToString().ToUpper();
            return $"{product}_{platform}_{version}.apk";
        }

        private static string SanitizeName(string input) =>
            input.Replace(" ", "").Replace("&", "And").Replace("/", "-").Replace("\\", "-");

        private void BumpVersion(int segment)
        {
            string[] parts = _editableVersion.Split('.');
            var list = new List<string>(parts);
            while (list.Count < 3) list.Add("0");

            if (int.TryParse(list[segment], out int val))
            {
                list[segment] = (val + 1).ToString();
                for (int i = segment + 1; i < list.Count; i++)
                    list[i] = "0";
            }

            _editableVersion         = string.Join(".", list);
            PlayerSettings.bundleVersion = _editableVersion;
            _versionPendingApply     = false;
        }

        private void ApplyVersion()
        {
            PlayerSettings.bundleVersion = _editableVersion;
            _versionPendingApply         = false;
        }

        private void RefreshSceneList()
        {
            _scenes = EditorBuildSettings.scenes
                .Select(s => (s.path, s.enabled))
                .ToList();
        }

        private void SetAllScenes(bool enabled)
        {
            for (int i = 0; i < _scenes.Count; i++)
                _scenes[i] = (_scenes[i].path, enabled);
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space(4);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, SepColor);
            EditorGUILayout.Space(4);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  AndroidManifest patcher — runs automatically before every Android build
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Patches AndroidManifest.xml before each build so the correct VR-platform
    /// intent-filter category and meta-data entries are present.
    ///
    /// META  → category: com.oculus.intent.category.VR
    ///         meta-data: com.oculus.supportedDevices = quest|quest2|questpro|quest3
    ///
    /// PICO  → category: com.picovr.intent.category.VR
    ///         meta-data: (Oculus entries removed)
    /// </summary>
    public class VRManifestPatcher : IPreprocessBuildWithReport
    {
        private const string ManifestRelativePath = "Assets/Plugins/Android/AndroidManifest.xml";

        // Intent-filter categories
        private const string OculusVrCategory = "com.oculus.intent.category.VR";
        private const string PicoVrCategory   = "com.picovr.intent.category.VR";

        // Meta-data keys
        private const string OculusSupportedDevicesKey   = "com.oculus.supportedDevices";
        private const string OculusSupportedDevicesValue = "quest|quest2|questpro|quest3";

        public int callbackOrder => 0;

        /// <summary>Patches the manifest based on the platform stored in EditorPrefs.</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            string platform = EditorPrefs.GetString(VRGameBuilderPrefs.LastPlatformKey, "META");
            bool isMetaBuild = platform.Equals("META", System.StringComparison.OrdinalIgnoreCase);

            string manifestPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath)!, ManifestRelativePath);

            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[VRManifestPatcher] AndroidManifest.xml not found at: " + manifestPath);
                return;
            }

            XDocument doc = XDocument.Load(manifestPath);
            XNamespace android = "http://schemas.android.com/apk/res/android";

            // ── Locate the UnityPlayerGameActivity <activity> element ──────────────
            XElement activity = doc.Descendants("activity")
                .FirstOrDefault(a => (string)a.Attribute(android + "name") ==
                                     "com.unity3d.player.UnityPlayerGameActivity");

            if (activity == null)
            {
                Debug.LogError("[VRManifestPatcher] UnityPlayerGameActivity not found in manifest.");
                return;
            }

            // ── 1. Intent-filter categories ───────────────────────────────────────
            XElement intentFilter = activity.Element("intent-filter");
            if (intentFilter != null)
            {
                // Remove both VR categories first, then add the correct one
                intentFilter.Elements("category")
                    .Where(c => (string)c.Attribute(android + "name") == OculusVrCategory ||
                                (string)c.Attribute(android + "name") == PicoVrCategory)
                    .ToList()
                    .ForEach(c => c.Remove());

                string targetCategory = isMetaBuild ? OculusVrCategory : PicoVrCategory;
                intentFilter.Add(new XElement("category",
                    new XAttribute(android + "name", targetCategory)));
            }

            // ── 2. Oculus supportedDevices meta-data ──────────────────────────────
            // Present only on META; removed for PICO (it causes PICO to not launch the app).
            activity.Elements("meta-data")
                .Where(m => (string)m.Attribute(android + "name") == OculusSupportedDevicesKey)
                .ToList()
                .ForEach(m => m.Remove());

            if (isMetaBuild)
            {
                activity.Add(new XElement("meta-data",
                    new XAttribute(android + "name",  OculusSupportedDevicesKey),
                    new XAttribute(android + "value", OculusSupportedDevicesValue)));
            }

            doc.Save(manifestPath);
            Debug.Log($"[VRManifestPatcher] Manifest patched for {platform}. " +
                      $"VR category: {(isMetaBuild ? OculusVrCategory : PicoVrCategory)}");
        }
    }

    /// <summary>EditorPrefs keys shared between VRGameBuilder and VRManifestPatcher.</summary>
    internal static class VRGameBuilderPrefs
    {
        internal const string LastPlatformKey = "VRGameBuilder.LastPlatform";
    }
}
