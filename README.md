# Quest Metadata Editor

A lightweight C# WinForms desktop app with a Windows 7-inspired visual style. It does not require third-party NuGet packages.

## What it does

- Opens `.apk`, `.obb`, and `.zip` archives and lists entries with uncompressed / compressed sizes.
- Inspects common metadata from an APK's `AndroidManifest.xml`: package name, version name/code, app label, min/target SDK and declared permissions when present in the manifest.
- Reads common fields from Android's binary XML manifest for inspection.
- Opens and edits plain-text XML, JSON and text files.
- Edits the fields in a decoded, plain-text `AndroidManifest.xml` (package name, label, version, min SDK and target SDK).
- Extracts selected archive entries and exports a JSON metadata report.
- Makes a `.bak` backup when saving over an existing text file.

## Important limits

A normal APK usually stores `AndroidManifest.xml` in Android's compiled binary XML format. This app can inspect common fields from that format, but it intentionally does **not** patch binary APKs or rebuild / sign APKs. To edit manifest fields, open a decoded `AndroidManifest.xml` that is regular XML text. Keep the original APK unchanged, and use a legitimate decode/rebuild/sign workflow for apps you own or are authorized to modify. Rebuilding an APK changes its signature; a different signing key may prevent an update over an installed copy.

An `android:label` that refers to a compiled resource may be shown as a resource identifier rather than the resolved human-readable app name.

## Compile on Windows 10/11

The project targets `.NET 10` Windows Forms. The .NET SDK (not only the runtime) must be installed.

1. Extract this folder somewhere such as `C:\Users\<you>\Downloads\QuestMetadataEditor`.
2. Open that folder in VS Code.
3. Select **Terminal → New Terminal**.
4. Run:

   ```powershell
   dotnet --version
   dotnet restore .\QuestMetadataEditor.csproj
   dotnet build .\QuestMetadataEditor.csproj -c Release
   ```

5. To create a single-file, self-contained 64-bit Windows executable, run:

   ```powershell
   .\publish-exe.ps1
   ```

6. The executable will be at:

   `bin\Release\net10.0-windows\win-x64\publish\QuestMetadataEditor.exe`

The publish output is self-contained for Windows x64, so the target PC does not need a separately installed .NET runtime. The SDK is still needed on the build PC.

## Quick build command

If PowerShell script execution is restricted, use this directly:

```powershell
dotnet publish .\QuestMetadataEditor.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

## Source

- `Program.cs` — application entry point
- `MainForm.cs` — Windows 7-inspired UI and file workflows
- `AndroidBinaryManifestReader.cs` — defensive read-only binary Android XML metadata reader
- `MetadataInfo.cs` — metadata model
