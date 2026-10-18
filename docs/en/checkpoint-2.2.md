# Checkpoint 2.2

[Polski](../pl/etap-2.2.md)

Checkpoint 2.2 adds a notify-only check for a newer stable release on GitHub. Recording, navigation, themes, and the About page stay as they were in checkpoint 2.1.

## What the check does

The application reads `https://api.github.com/repos/TimeWizard007/powershell-session-recorder/releases/latest` over HTTPS. The request identifies the application in the User-Agent. It does not include customer TAGs, session names, transcripts, log paths, or other settings.

The release tag is compared with the assembly informational version as a semantic version. A leading `v` is accepted. Build metadata after `+` is ignored. A label such as `0.1.0-checkpoint.2.1` is a prerelease of `0.1.0`, so the stable release `0.1.0` is newer than that checkpoint build, and `0.2.0` is newer as well. A remote prerelease is never treated as a newer stable release.

If GitHub returns HTTP 404, the application says that no release has been published. It does not describe that result as "up to date".

A newer stable release is shown in a notice at the top of the main content, with the installed version, the available version, **View release**, and **Later**. **View release** opens only an `https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/...` page. **Later** hides that version until the application is started again. There is no modal dialog.

The application does not download release assets, install packages, execute downloaded files, or restart itself.

## When it runs

Automatic checking is on by default and can be turned off in Settings. When it is on, a check starts after the window is shown and does not delay opening. A successful check is not repeated for 24 hours. A manual **Check for updates now** ignores that interval. Only one check runs at a time.

The settings file stores the preference, the time of the last successful check, the latest stable tag, and a trusted release page URL. It does not store the API response body. A failed check does not change that timestamp and does not block Start Session.

Offline, timeout, HTTP 403, HTTP 404, malformed JSON, and an unusable version tag each have their own message.
