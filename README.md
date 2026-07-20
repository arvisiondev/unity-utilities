# unity-utilities

Unity Editor tools and utilities. Author: Daniel Guerra Gallardo

Each tool works standalone — copy the folder (or the single script) you need into your project's `Assets/`.

## Tools

### MissingScriptsFinder

`MissingScriptsFinder.cs` — Editor window to find and delete missing (`Missing`) script references across the
current scene and across all prefabs in the project.

**Menu:** `RimuruDev Tools > Find Missing Scripts`

### SceneSwitcher

`SceneSwitcher/` — Editor window to quickly switch between scenes without hunting through the Project view.
Lists either the scenes in Build Settings or every scene in the project, with optional auto-save before switching.

**Menu:** `RimuruDev Tools > Scene Switcher` (Ctrl+Shift+F2)

### BuildTools / VRGameBuilder

`BuildTools/Editor/VRGameBuilder.cs` — Editor window for building Android APKs targeting META Quest or PICO
devices. Switches OpenXR interaction profiles and patches `AndroidManifest.xml` per target platform, manages
bundle version/version code, and names the output APK as `[ProductName]_[PLATFORM]_[VERSION].apk`.

**Menu:** `INVELON > Build > VR Game Builder`

### PackageManifestInstaller

`PackageManifestInstaller/` — Editor window that auto-discovers every `*.dependencies.json` template manifest in
the project and installs their UPM packages (registry, git, OpenUPM, local tarball) with one click. Ships as its
own self-contained UPM package (`com.invelon.package-manifest-installer`) with zero external dependencies, tests,
and a `dependencies-json-generator` Claude skill for authoring manifests. See
`PackageManifestInstaller/README.md` for full documentation.

**Menu:** `INVELON > Package Manager > Dependency Installer`
