# PACS-Soft

Windows Forms access-control management system for Dahua NetSDK controllers and MariaDB.

## Project structure

- `src/PacsSoft.WinForms/UI` — WinForms UI layer. Button handlers delegate to services.
- `src/PacsSoft.WinForms/Services` — business services, event batching, emergency/fire workflows, statistics.
- `src/PacsSoft.WinForms/Data` — MariaDB connection factory and repositories.
- `src/PacsSoft.WinForms/Device` — Dahua NetSDK integration wrapper preserving real-time alarm callback, door open, and mode changes.
- `src/PacsSoft.WinForms/Models` — domain DTOs/enums.
- `database/schema.sql` — MariaDB schema.

## Configuration

Copy `src/PacsSoft.WinForms/appsettings.example.json` to `appsettings.json` beside the executable and set:

- `PACS_DB_PASSWORD` for MariaDB.
- `PACS_DAHUA_PASSWORD` for the Dahua controller.

The default host is `vweb-01.local`, user is `root`, and the schema database is `pacs_soft`.

## Build notes

Place Dahua `NetSDKCS.dll` at `src/PacsSoft.WinForms/libs/NetSDKCS.dll` or adjust the project reference. Build with Visual Studio 2022 on Windows with .NET Framework 4.8 targeting pack installed.
