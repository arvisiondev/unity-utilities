# unity-utilities

Unity Editor tools and utilities. Author: Daniel Guerra Gallardo

Each tool works standalone. Every tool folder (except `MissingScriptsFinder.cs`, a single loose script) is also
its own self-contained UPM package, so it can be installed directly via Package Manager without pulling the whole
repo:

```
https://github.com/arvisiondev/unity-utilities.git?path=<ToolFolder>
```

e.g. `https://github.com/arvisiondev/unity-utilities.git?path=MRSetup`. Alternatively, copy the folder (or the
single script) you need into your project's `Assets/`.

> **AI agents / contributors:** see [AGENTS.md](AGENTS.md) for the required folder structure before adding or
> updating a tool. Every tool must follow that convention so it stays independently installable.

## Tools

### MissingScriptsFinder

`MissingScriptsFinder.cs` — Editor window to find and delete missing (`Missing`) script references across the
current scene and across all prefabs in the project.

**Menu:** `RimuruDev Tools > Find Missing Scripts`

### SceneSwitcher

`SceneSwitcher/Editor/SceneSwitcher.cs` — Editor window to quickly switch between scenes without hunting through
the Project view. Lists either the scenes in Build Settings or every scene in the project, with optional auto-save
before switching. Ships as its own self-contained UPM package (`com.invelon.scene-switcher`).

**Menu:** `RimuruDev Tools > Scene Switcher` (Ctrl+Shift+F2)

### BuildTools / VRGameBuilder

`BuildTools/Editor/VRGameBuilder.cs` — Editor window for building Android APKs targeting META Quest or PICO
devices. Switches OpenXR interaction profiles and patches `AndroidManifest.xml` per target platform, manages
bundle version/version code, and names the output APK as `[ProductName]_[PLATFORM]_[VERSION].apk`. Ships as its
own self-contained UPM package (`com.invelon.build-tools`).

**Menu:** `INVELON > Build > VR Game Builder`

### PackageManifestInstaller

`PackageManifestInstaller/` — Editor window that auto-discovers every `*.dependencies.json` template manifest in
the project and installs their UPM packages (registry, git, OpenUPM, local tarball) with one click. Ships as its
own self-contained UPM package (`com.invelon.package-manifest-installer`) with zero external dependencies, tests,
and a `dependencies-json-generator` Claude skill for authoring manifests. See
`PackageManifestInstaller/README.md` for full documentation.

**Menu:** `INVELON > Package Manager > Dependency Installer`

### MRSetup / MRPlatformConfigurator

`MRSetup/Editor/MRPlatformConfigurator.cs` — Editor tool that switches the project's Android OpenXR feature set,
Minimum API Level, and scripting define symbols between a Meta Quest passthrough configuration and a PICO
passthrough configuration in a single step. Verifies the vendor OpenXR package (`com.unity.xr.meta-openxr` or
`com.unity.xr.openxr.picoxr`) is installed before changing any setting, and aborts with a dialog listing exactly
what's missing otherwise. Ships as its own self-contained UPM package (`com.invelon.mr-setup`).

**Menu:** `INVELON > VRCulture > MR Setup > Configure Android For Meta Quest` /
`INVELON > VRCulture > MR Setup > Configure Android For PICO`
