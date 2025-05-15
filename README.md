# DriveMapper (.NET Framework 4.8)

## Overview

DriveMapper is a utility that maps network drives based on a user's Active Directory group membership. It is designed to work with Intune-managed Windows 11 devices that are Azure AD-joined but operating on-premises.

## Features

- Reads mapping config from `config.json`
- Uses Active Directory to determine user group membership
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

- Adding a NuGet package source in Visual Studio
   - Open Visual Studio
    - Go to Tools > Options
    - In the Options window, navigate to NuGet Package Manager > Package Sources
    - Click the Add button (the plus icon) to add a new source
    - Enter a Name (e.g., nuget.org)
    - Enter the Source URL: https://api.nuget.org/v3/index.json
- Install NuGet packages Newtonsoft.Json and Newtonsoft.Json
- Open in Visual Studio or VS Code with .NET Framework 4.8 installed.
- Build both `DriveMapper` and `Installer` projects.
- Run `Install.exe` to create scheduled tasks.

## Intune Deployment

1. Bundle `DriveMapper.exe`, `config.json`, and `Install.exe` into a `.intunewin` package.
2. Deploy as a Win32 app using Microsoft Intune.
3. Ensure `Install.exe` is the install command.

## Requirements

- Windows 10/11
- .NET Framework 4.8
- On-premises Active Directory