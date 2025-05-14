# DriveMapper (.NET Framework 4.8)

## Overview

DriveMapper is a utility that maps network drives based on a user's Active Directory group membership. It is designed to work with Intune-managed Windows 11 devices that are Azure AD-joined but operating on-premises.

## Features

- Reads mapping config from `config.json`
- Uses Active Directory to determine user group membership
- Maps drives using native Windows API (no PowerShell or external dependencies)
- Installer creates two scheduled tasks:
  - At user logon
  - On network change

## Usage

Place your `config.json` next to `DriveMapper.exe` and define drive mappings as:

```json
[
  {
    "Name": "SharedDocs",
    "Group": "ITUsers",
    "Path": "\\fileserver\shared",
    "DriveLetter": "Z"
  }
]
```

## Compile Instructions

- Install dependencies Install-Package System.Text.Json
- Open in Visual Studio or VS Code with .NET Framework 4.8 installed.
- Build both `DriveMapper` and `Installer` projects.
- Run `Installe.exe` to create scheduled tasks.

## Intune Deployment

1. Bundle `DriveMapper.exe`, `config.json`, and `Install.exe` into a `.intunewin` package.
2. Deploy as a Win32 app using Microsoft Intune.
3. Ensure `Installe.exe` is the install command.

## Requirements

- Windows 10/11
- .NET Framework 4.8
- On-premises Active Directory