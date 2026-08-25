# Changelog

## [2.2.0] - 2026-08-25

### Fixed
- **Tarball installs no longer hardcode an absolute path in `manifest.json`.** The Install button still ended up writing an absolute path even after the identifier passed to `Client.Add()` was made relative: `Client.Add()` resolves any `file:` identifier to an absolute path and writes that back into `manifest.json`, which clobbered the relative `file:./x.tgz` entry that `EnsureTarballInManifest` had just written. `Client.Add()` is no longer called for tarball entries at all — registration happens entirely through the direct manifest edit, which is the only step that was ever needed.
- **Export now recognizes tarball packages.** *Export current packages* had no tarball detection: a `file:` package was exported as `source: "registry"` with no `tgzFileName`, producing a manifest that would try (and fail) to install it from the registry on re-import. Packages installed as a local tarball (`PackageSource.LocalTarball`) now export with `source: "tarball"` and the correct `tgzFileName`.
- **Asset Store detection no longer depends on an exact `assetFolderPath` match.** If the configured path was stale, mistyped, or the asset imported into a different parent folder, the row always showed "Missing" even when the asset was present, with no indication of what was checked. Detection now falls back to searching anywhere under `Assets/` for a folder matching the configured path's last segment, and each row's note shows the path that was found (or checked, if still missing) so a wrong `assetFolderPath` can be spotted and fixed.

## [2.1.0] - 2026-07-14

### Fixed
- **Install queue now survives domain reloads.** Installing a package with scripts triggers a recompile that used to wipe the queue and silently stall *Install pending* mid-batch. The queue is persisted in `SessionState` and resumes automatically.
- **No more duplicate scoped registries.** When an OpenUPM registry with the same URL already exists in `Packages/manifest.json`, the new scope is appended to it instead of inserting a second registry block.
- **Robust manifest.json / packages-lock.json edits.** Naive `Contains()`/`IndexOf()` checks replaced with a structure-aware scanner (`UpmManifestJson`): no false positives from nested keys or unrelated `file:` refs, correct handling of empty objects/arrays and trailing commas, and lock-file cleanup can no longer remove the wrong nested block.
- Export no longer writes `exported-*.dependencies.json` silently next to the manifest (which appeared as bogus new tabs) — a save dialog asks where to put it.
- Git packages now export their URL correctly (the `name@` prefix from `packageId` is stripped).

### Added
- `package.json` — the tool is now a proper UPM package (`com.invelon.package-manifest-installer`), installable from a git URL or tarball in addition to the XLINK workflow.
- Edit-mode test suite (`Tests/Editor/`) covering JSON surgery, version comparison, schema policy, and export generation.
- *Open Page* button for Asset Store entries with an `assetStoreId`.
- *Locate* button in the header to ping the active manifest asset (replaces the ping-on-every-tab-click behavior).
- Cancelable install progress bar.
- Schema version policy: any `2.x` manifest loads (newer minors show an info note); only major mismatches are rejected.
- `README.md` (supersedes the PDF) and this changelog.

### Changed
- UI language switched to English; all strings centralized in `InstallerStrings`.
- Colors are now dark/light editor skin aware (`InstallerColors`).
- Package name column is flexible-width with full package id tooltips; action buttons are in a fixed-width column so rows stay aligned.
- Code split into focused files: model/policy, JSON editing, export builder, support types, window. Pure logic classes are Unity-free and unit-testable.
- `menuGroup` is documented as informational only (no menu shortcut was ever registered).
- Discovery error message is now generic (no template-specific wording).

## [2.0.0]

- Multi-template tabs, schema v2.0, sources: registry, git, openupm, tarball, assetstore.
