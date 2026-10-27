# Verification checklist

Automated checks I could perform in the authoring environment:

- Confirmed the project contains only built-in .NET / WinForms APIs; no NuGet dependencies are declared.
- Added defensive bounds checks for binary-manifest chunk sizes and string indexes.
- Added bounded reads for manifest entries and text files, guarded file I/O, and backup-before-save handling for text files.
- Reviewed the source for matching declarations and balanced delimiters.

A real `dotnet build` / Windows Forms launch could **not** be run in the authoring container because the .NET SDK is not installed there and network access is unavailable. Run the commands in `README.md` on Windows to perform the actual compiler verification before relying on the EXE. The publish script checks the exit code and confirms that the expected EXE exists.
