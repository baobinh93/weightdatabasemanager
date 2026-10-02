# Weight Database Manager

Windows desktop WinForms app for managing SQL Server Express tables `dbo.Weightman` and `dbo.Weightsave`.

## Target
- .NET 7 WinForms
- Windows x64
- Self-contained publish: target PC does not need .NET installed
- Windows Authentication only
- Default SQL Server: `\\.\\SQLEXPRESS`

## Build without Visual Studio
This repository includes GitHub Actions. Push it to GitHub, then open **Actions → Build Windows EXE → Run workflow**.

Download the generated artifact `WeightDatabaseManager-win-x64` and extract it on the Windows PC.

The target PC still needs SQL Server Express and the required database attached/available to that SQL Server instance.
