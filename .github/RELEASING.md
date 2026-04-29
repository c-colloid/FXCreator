# Release Process

This repository ships three independent VPM packages. Releases are
triggered by tags; everything else is automated.

## Packages

| Tag prefix | Package name | Path |
|---|---|---|
| `clipgen` | `jp.colloid.clipgen` | `Packages/jp.colloid.clipgen` |
| `eyejiggling` | `jp.colloid.eye-jiggling-generator` | `Packages/jp.colloid.eye-jiggling-generator` |
| `vpe` | `jp.colloid.vrc-expression-params-extension` | `Packages/jp.colloid.vrc-expression-params-extension` |

The mapping lives in `.github/scripts/packages.json` (single source of truth).

## Release a new version

1. Bump `version` in the package's `package.json` and update its `CHANGELOG.md`.
2. Commit on `main`.
3. Tag with the format `<prefix>/v<version>`, e.g. `clipgen/v0.1.4`. Push the tag.

```bash
git tag clipgen/v0.1.4
git push origin clipgen/v0.1.4
```

The `Release Package` workflow then:

1. Verifies the tag version matches `package.json`.
2. Builds:
   - `<package>-<version>.zip` (VPM)
   - `<package>-<version>.unitypackage`
   - `package.json` (for the listing builder)
3. Creates a GitHub Release with all three assets.
4. Calls `Build VPM Listing` which regenerates `index.json`
   from every published release and deploys it to GitHub Pages.

## Listing URL

Once GitHub Pages is enabled (Settings → Pages → Source: GitHub Actions),
the listing is published at:

```
https://<owner>.github.io/<repo>/index.json
```

Add this URL once in VCC; subsequent package versions appear automatically.

## Manual rebuild

If a release is edited or assets are re-uploaded, run the
`Build VPM Listing` workflow manually (Actions tab → workflow_dispatch).

## First-time setup

1. Settings → Pages → Source: **GitHub Actions**
2. Settings → Actions → General → Workflow permissions:
   **Read and write**
3. Push a release tag to verify the pipeline.
