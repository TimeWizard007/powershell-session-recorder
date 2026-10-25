# CI and release builds

[Polski](../pl/potok-wydania.md)

This source tree is version `1.0.0`. GitHub Actions can build tag `v1.0.0` after that tag exists. Pushing a commit or a tag does not publish a GitHub Release. Previously published beta releases stay in place.

## Continuous integration

`.github/workflows/ci.yml` runs on pull requests and on pushes to `main`.

- Ubuntu 24.04 builds Debug and Release and runs the automated tests.
- Windows Server 2022 does the same. The Windows PowerShell 5.1 parser test is compiled only on Windows, so this is the job that runs it. The Ubuntu job reports that test as skipped.
- macOS 15 builds Debug and Release, runs the tests, and packages `osx-x64` and `osx-arm64`. It ad-hoc signs the bundles, checks `codesign --verify --deep --strict`, and smoke-tests the Apple Silicon apphost. Those packages are CI artifacts named `powershell-session-recorder-macos-ci`. They are not the release assets.
- The jobs use the .NET 10 SDK.
- The workflow has read-only repository permission. It does not create tags or GitHub Releases.

## Release build

`.github/workflows/release.yml` starts only from the Actions tab, with **Run workflow**. It does not run on a push.

Enter an existing tag, such as `v1.0.0`. Leave the mode at `review-only`.

The workflow checks out that tag, refuses a branch or a dirty tree, and requires the tag commit to match:

- application version `1.0.0`
- Debian version `1.0.0-1`
- Inno Setup `MyAppVersion`

Linux, Windows, and macOS jobs then check out that same commit by its SHA. They do not reuse a previously uploaded CI artifact or a file copied from a developer machine. `SOURCE_REVISION_ID` is that SHA, so the informational version embedded in each build is `1.0.0+` followed by the 40-character commit. About shows `1.0.0` without that suffix.

The Ubuntu 24.04 job builds the `.deb` and the per-user `.tar.gz`, writes SHA-256 files, and rejects settings, customers, history, debug symbols, and the old SVG icon. It also requires the approved hicolor PNGs and the informational version plus the full commit SHA.

The Windows x64 job runs the Release tests, including the Windows PowerShell 5.1 parser test. It downloads Inno Setup 6.5.4 from the official `jrsoftware/issrc` release and checks SHA-256 `fa73bf47a4da250d185d07561c2bfda387e5e20db77e4570004cf6a133cc10b1` before installing it. It publishes the application, compiles `packaging/windows/PowerShellSessionRecorder.iss`, and checks the installer checksum, the stable AppId, and that the executable and installer contain the approved icon images. There is no separate Windows zip.

The macOS 15 job runs the Release tests and `sh packaging/macos/build-dmg.sh all`. That produces the Intel and Apple Silicon disk images and zip archives, ad-hoc signs them, verifies the signatures, and smoke-tests Apple Silicon. It does not notarize the apps.

A final Ubuntu job downloads those assets, checks all seven checksums again, checks the installer icon, and uploads one review bundle. This job does not create a release. Publishing stops if any required asset is missing, so a Linux-only or Windows-only set is not published.

## Approval before publishing

Publishing is a second run, not part of an ordinary build.

1. Create a GitHub environment named `release`.
2. Add required reviewers to that environment. A publish job waits until one of them approves it.
3. Add an environment variable named `RELEASE_APPROVED` with the value `yes` only when publishing is allowed. The workflow cannot set this value.
4. Run the workflow again with the same tag and mode `publish`.

The publish job checks the reviewed checksums, checks that the tag still points at the built commit, and stops if a release with that tag already exists. It does not replace or delete an existing release. A tag whose version contains a hyphen is marked prerelease. `v1.0.0` has no hyphen, so the Stable update channel can treat it as a stable release. Existing beta releases remain prereleases.

Until the environment and the variable exist, mode `publish` cannot finish, even if someone selects it.

## Permissions

| Workflow | Permission |
|---|---|
| CI | Contents: read. The macOS job can upload its own CI artifacts. |
| Release build and review | Contents: read. The macOS release job can upload its release artifacts. |
| Publish job | Contents: write, and only after the `release` environment approves it |

The default `GITHUB_TOKEN` is enough. No extra secret is required for a public repository. The token is not granted write access on pull-request builds.

## Artifacts and checksums

Open the workflow run, then open **Artifacts**. `review-only` produces `release-linux`, `release-windows`, `release-macos`, and `release-review-<tag>`. Each package has a `.sha256` file beside it. The review bundle keeps them for 14 days.

Download the bundle and, from the directory that contains a package and its `.sha256` file, run:

```bash
sha256sum -c powershell-session-recorder_1.0.0-1_amd64.deb.sha256
sha256sum -c powershell-session-recorder-1.0.0-linux-x64.tar.gz.sha256
sha256sum -c PowerShellSessionRecorder-1.0.0-win-x64-setup.exe.sha256
sha256sum -c PowerShellSessionRecorder-1.0.0-osx-x64.dmg.sha256
sha256sum -c PowerShellSessionRecorder-1.0.0-osx-x64.zip.sha256
sha256sum -c PowerShellSessionRecorder-1.0.0-osx-arm64.dmg.sha256
sha256sum -c PowerShellSessionRecorder-1.0.0-osx-arm64.zip.sha256
```

On Windows PowerShell, `Get-FileHash -Algorithm SHA256` should show the same digest as the first field in the `.sha256` file. On macOS, `shasum -a 256 -c` checks the same files.

## Publish an approved release

After the `review-only` artifacts have been inspected:

```text
Actions → Release → Run workflow
Tag: v1.0.0
Mode: publish
```

Approve the waiting `release` environment. The job creates the GitHub Release from the tag and attaches the seven packages and their checksums. The release notes file is `docs/en/release-notes-1.0.0.md` from that tag.

Create the tag from the reviewed commit before the first run. Do not point it at a branch name or at uncommitted files:

```bash
git tag v1.0.0 <reviewed-commit>
git push origin v1.0.0
```

Pushing the tag does not start the release workflow. Only **Run workflow** does.

## Failed builds

A failed platform job uploads nothing for that platform, and the publish job does not run. Use **Re-run failed jobs** after a transient failure. For a source or packaging failure, fix it in a new commit, move or replace the tag only when you intend to, and start a new `review-only` run. Do not publish a partial set. If a publish attempt stops before GitHub creates the release, there is nothing to delete. If a release already exists, the workflow leaves it in place.
