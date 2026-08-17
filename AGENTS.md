# AGENTS.md

Conventions for AI agents (and humans) adding or updating tools in this repo. Follow this file exactly — it
exists so every tool folder stays independently installable via Unity Package Manager (UPM) git-path installs:

```
https://github.com/arvisiondev/unity-utilities.git?path=<ToolFolder>
```

UPM resolves that URL by cloning the repo and looking for `<ToolFolder>/package.json`. If it's missing, the
install fails with:

```
Repository does not contain a package manifest:
  The file [...\clone\<ToolFolder>\package.json] cannot be found
```

So: **every top-level tool folder must be a self-contained UPM package.** `MissingScriptsFinder.cs` is the one
legacy exception (a single loose script at repo root, predates this convention) — do not use it as a template.

## Required structure for a new tool

```
<ToolName>/
  <ToolName>.meta                          # folder .meta, at repo root, sibling to <ToolName>/
  package.json                             # UPM manifest — REQUIRED
  package.json.meta
  Editor/
    INVELON.<ToolName>.Editor.asmdef       # assembly definition, Editor-only
    INVELON.<ToolName>.Editor.asmdef.meta
    <ToolName>.cs                          # implementation
    <ToolName>.cs.meta
  Editor.meta                              # folder .meta for Editor/
  README.md                                # optional, for larger tools (see PackageManifestInstaller/)
  README.md.meta
```

Runtime (non-Editor) code, if a tool ever needs it, goes in a sibling `Runtime/` folder with its own
`INVELON.<ToolName>.asmdef` (no `.Editor` suffix, no `includePlatforms: ["Editor"]` restriction) — none of the
current tools need this yet.

### `package.json` template

```json
{
  "name": "com.invelon.<kebab-case-tool-name>",
  "version": "1.0.0",
  "displayName": "INVELON <Tool Name>",
  "description": "<one paragraph: what it does, key behaviors, menu location if relevant>",
  "unity": "2021.3",
  "author": {
    "name": "INVELON",
    "url": "https://invelon.com/"
  },
  "keywords": ["editor", "..."]
}
```

- `name` must be globally unique within the repo and follow `com.invelon.<kebab-case>`.
- Add a `"dependencies"` object only if the tool's asmdef references another package's assembly (e.g. OpenXR).
  Match the version already pinned by sibling tools that use the same dependency (check their `package.json`
  first, e.g. MRSetup/BuildTools both pin `com.unity.xr.openxr`).
- Start new tools at `"version": "1.0.0"` and bump it (semver) whenever that tool's own files change —
  versions are per-tool, not per-repo.

### `.asmdef` template

```json
{
    "name": "INVELON.<ToolName>.Editor",
    "rootNamespace": "INVELON.Editor",
    "references": [],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Use 4-space indentation for `.asmdef` files specifically (matches existing files); everything else in the repo
uses 2-space JSON indentation.

## `.meta` files

Every asset (folder, script, json) needs a matching `<name>.meta`. **Never hand-roll a GUID** — collisions
silently corrupt references in any Unity project that consumes this package. Generate one fresh per file:

- PowerShell: `[guid]::NewGuid().ToString('N')` (32 lowercase hex chars, no dashes)

Meta file bodies by asset type (copy exactly, just swap the `guid`):

**Folder** (`<Folder>.meta`):
```yaml
fileFormatVersion: 2
guid: <new-guid>
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

**`package.json.meta`** (Unity imports `.json` as a text asset):
```yaml
fileFormatVersion: 2
guid: <new-guid>
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

**`.asmdef.meta`**:
```yaml
fileFormatVersion: 2
guid: <new-guid>
AssemblyDefinitionImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

**`.cs.meta`**:
```yaml
fileFormatVersion: 2
guid: <new-guid>
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
```

## Checklist when adding a new tool

1. Create `<ToolName>/Editor/<ToolName>.cs` with the implementation, namespaced under `INVELON.Editor` (or the
   project's existing namespace if migrating legacy code — check neighboring tools before inventing a new one).
2. Add the `.asmdef` in `Editor/`, named `INVELON.<ToolName>.Editor`.
3. Add `package.json` at the tool root with a unique `com.invelon.*` name.
4. Generate `.meta` files for every new file/folder (`<ToolName>.meta`, `Editor.meta`, `.asmdef.meta`,
   `package.json.meta`, `.cs.meta`) using fresh GUIDs — never reuse or guess one.
5. Add an entry to the root `README.md` under `## Tools`, following the existing per-tool format (one-line
   summary, menu path if it adds a menu item, and a note that it "Ships as its own self-contained UPM package
   (`com.invelon.*`)").
6. If the tool's `README.md` mentions the repo's root `package.json` description, update that too — it lists
   every tool in one paragraph.
7. Verify every new `.json`/`.asmdef` file is valid JSON before finishing.
8. Do not modify unrelated tools' files, versions, or GUIDs in the same change.

## Checklist when updating an existing tool

- Bump that tool's `package.json` `"version"` (semver) if the change is user-facing (new feature, bug fix,
  behavior change). Skip the bump for pure docs/comment changes.
- Keep the asmdef's `"references"` in sync with any new external assembly the code starts using, and mirror
  that as a `"dependencies"` entry in `package.json` if the reference comes from another UPM package.
- Do not change a tool's `package.json` `"name"` (its UPM identity) — that breaks existing installs that
  pin/lock it.
- Do not regenerate or touch `.meta` file GUIDs for files that already exist — Unity uses the GUID, not the
  path, to track asset references. Changing one silently breaks every project that already has this package
  installed.
