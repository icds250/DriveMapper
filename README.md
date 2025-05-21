# Drivemapper and Installer

Deploy these applications together as an intunewin win32 app to map on-prem file shares on an Entra ID joined device (cloud only).

## About install.exe

Installer is a standalone .NET 8 installer utility that copies files to a target directory, registers scheduled tasks, and optionally creates a Start Menu shortcut. It's designed to be deployed via Intune or manually, with parameters provided via a JSON configuration file.

## Features

- Copies all files from the installer's directory to a specified target directory.
- Registers one or more scheduled tasks using configurable triggers:
  - On user logon
  - On network profile change
  - On system boot
- Optionally creates a Start Menu shortcut.
- Fully uninstallable (removes files, scheduled tasks, and shortcut).
- Supports multiple task definitions through a JSON configuration.
- Uses only one command-line parameter (`install` or `uninstall`) plus a config path.

## Requirements

- .NET 8
- Admin privileges (required to register tasks and copy to `Program Files`)
- [Microsoft.Win32.TaskScheduler](https://www.nuget.org/packages/TaskScheduler/) NuGet package
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/) NuGet package

## Configuration File (`config.json`)

The installer reads all parameters from a JSON file. Example:

```json
{
  "TargetDirectory": "C:\\Program Files\\DriveMapper",
  "ExeName": "DriveMapper.exe",
  "ShortcutName": "Drive Mapper",
  "Version": "1",
  "ScheduledTasks": [
    {
      "TaskName": "DriveMapper-Logon",
      "Arguments": "/map",
      "CreateLogonTask": true,
      "CreateNetworkTask": false,
      "CreateBootTask": false
    },
    {
      "TaskName": "DriveMapper-NetworkChange",
      "Arguments": "/sync",
      "CreateLogonTask": false,
      "CreateNetworkTask": true,
      "CreateBootTask": false
    }
  ]
}
```

### Field Descriptions

| Field | Description |
|-------|-------------|
| `TargetDirectory` | The directory where files will be copied. |
| `ExeName` | The main executable name to launch from tasks and shortcut. |
| `ShortcutName` | (Optional) Name for Start Menu shortcut. If omitted, no shortcut is created. |
| `Version` | Set the version number in the registry to target Intune detection rules. |
| `ScheduledTasks` | Array of scheduled task definitions. |
| `TaskName` | (Optional) Name of the scheduled task. If omitted, uses `ExeName`. |
| `Arguments` | Arguments to pass when the task is triggered. |
| `CreateLogonTask` | Create task triggered on user logon. |
| `CreateNetworkTask` | Create task triggered on network profile change. |
| `CreateBootTask` | Create task triggered on system boot. |

## Usage

Run the executable with administrative privileges:

```bash
Install.exe <path-to-config.json> install
```

To uninstall (remove tasks, files, and shortcuts):

```bash
Install.exe <path-to-config.json> uninstall
```

### Example

```bash
Install.exe install.config.json install
```

## Start Menu Shortcut

If `ShortcutName` is defined, a shortcut will be placed in:

```
C:\ProgramData\Microsoft\Windows\Start Menu\Programs
```

This ensures compatibility with both Windows 10 and Windows 11.

## About DriveMapper.exe

`DriveMapper.exe` is a lightweight, command-line utility that automates the mapping of network drives based on business or environmental needs. It is typically deployed in enterprise environments where persistent drive mappings are required without relying on traditional logon scripts or Group Policy.

### Key Features

- Maps one or more network drives.
- Supports conditional logic (e.g., based on group membership).
- Designed to be executed silently as a scheduled task.
- Does not require user interaction once deployed.

### Integration with Installer

This utility is bundled alongside `Install.exe` and referenced within `config.json` as the `ExeName`. The installer schedules it to run under specific conditions (logon, boot, or network change) as configured, ensuring persistent and reliable drive availability for users.

### Example DriveMapper JSON

```json
{
  "Domain": "corp.example.com",
  "DriveMappings": [
    {
      "Name": "Files",
      "Group": "intune_map_n_drive",
      "Path": "\\\\fs01\\data",
      "DriveLetter": "N"
    },
    {
      "Name": "Photos",
      "Group": "intune_map_o_drive",
      "Path": "\\\\fs01\\photos",
      "DriveLetter": "O"
    }
  ]
}

```

## Intune Deployment

To deploy this installer via Intune:

1. Package the following files using the [Microsoft Win32 Content Prep Tool](https://learn.microsoft.com/en-us/mem/intune/apps/apps-win32-app-management) Self-Contained exe and config files can be found in the build directory:
   - `Install.exe`
   - `install.config.json`
   - `DriveMapper.exe`
   - `config.json`

2. Create an `.intunewin` file from the folder.

3. In the Intune portal:
   - Create a new Win32 app.
   - Set the install command to: `Install.exe install.config.json install`
   - Set the uninstall command to: `Install.exe install.config.json uninstall`
   - Configure detection rules to check for the existence of the target HKLM/exeName/Version set by the installer.
   - Assign to user or device groups as needed.


## License

GPL. Use freely with credit www.cloudcondensate.ca <info@cloudcondensate.ca>
