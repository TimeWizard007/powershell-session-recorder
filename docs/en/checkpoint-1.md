# Checkpoint 1

[Polski](../pl/etap-1.md)

Checkpoint 1 is the foundation of PowerShell Session Recorder. The solution builds, the Avalonia window opens, and an administrator can prepare a session description. Checkpoint 1 itself does not record PowerShell. Recording a PowerShell 7 session is described in [Checkpoint 2](checkpoint-2.md).

## In scope

- Solution layout: Core, Avalonia application, and tests
- English and Polish interface
- Session form, validation, and filename preview
- Local folder selection and per-user JSON settings
- Filename rules that stay compatible with Windows and do not overwrite an existing file when a unique name is requested

## Out of scope

- Launching `pwsh` or `powershell.exe`
- Writing a transcript
- Elevation and UAC
- A real session history

The **Start Session** button stays disabled on purpose. History stays empty on purpose.

## Verify

```bash
dotnet test PowerShellSessionRecorder.sln
dotnet run --project src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj
```

The run command needs a graphical session. Build and test instructions are in the [English README](../../README.md).
