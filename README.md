# Discord Activity Mock

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows-blue?style=for-the-badge&logo=windows" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-8.0-purple?style=for-the-badge&logo=dotnet" alt=".NET">
  <img src="https://img.shields.io/badge/WinUI-3-blue?style=for-the-badge" alt="WinUI 3">
  <img src="https://img.shields.io/github/license/iiroak/DiscordActivityMock?style=for-the-badge" alt="License">
</p>

<p align="center">
<b>Experiment with Discord game detection using an isolated WordPad copy.</b>
</p>

---

## 📖 Description

Discord Activity Mock is a Windows utility that experiments with Discord's detected-game status by running an isolated copy of WordPad under a selected executable and folder name. Discord may not recognize every entry or configuration. The app never renames or replaces the system WordPad files.

> **Windows compatibility:** WordPad was removed from Windows 11 version 24H2 and later. The system-copy option is unavailable on those systems; the app's downloadable backup is x64 and requires 64-bit Windows (including supported ARM64 Windows installations).

## ⚠️ Disclaimer

**This application is made for joking around with friends about games. I am not responsible for any use outside of this purpose.**

## ✨ Features

- 🎮 **Activity Catalog** - Select from the maintained online catalog, with a bundled offline fallback
- 🎨 **Custom Activities** - Create your own custom game activities
- 🌙 **Dark/Light Theme** - Toggle between dark and light modes
- 👁️ **Auto-Hide WordPad** - Automatically hide the WordPad dialog
- ⏱️ **Auto-Stop Timer** - Set a timer to automatically stop the activity
- 🌍 **Multi-language Support** - English, Spanish, and Portuguese
- 🔄 **Update Check** - Detects a newer GitHub release and opens its download page

## 📸 Screenshots

<p align="center">
  <img src="docs/screenshot.png" alt="Discord Activity Mock main window" width="900">
</p>

## 🚀 Getting Started

### Prerequisites

- 64-bit Windows 10/11 (x64 or ARM64)

### Installation

1. Download the matching architecture's standalone `.exe` or `.zip` from [Releases](https://github.com/iiroak/DiscordActivityMock/releases)
2. Run the `.exe` directly, or extract the ZIP and run the included executable. Both are self-contained and do not require a separate .NET installation.

Each release includes x64, x86, and ARM64 builds; GitHub also provides the source-code ZIP and tarball. Release availability depends on a maintainer publishing a tagged build. The app checks for releases but does not install updates automatically.

### Usage

1. **Get WordPad**: Click "Copy from System" on Windows installations that still include WordPad, or click "Download Backup"
2. **Select Activity**: Choose a game from the list or create a custom activity
3. **Start**: Click the Start button to begin mocking the activity
4. Discord may detect the selected executable as a game; detection depends on Discord and the selected entry.

## 🛠️ Building from Source

### Prerequisites

- Visual Studio 2022 with .NET Desktop Development workload (or the .NET 8 SDK and Windows build tools)
- Windows App SDK (restored from NuGet)
- .NET 8.0 SDK

### Build Steps

```bash
# Clone the repository
git clone https://github.com/iiroak/DiscordActivityMock.git

# Navigate to the project directory
cd DiscordActivityMock

# Restore packages
dotnet restore

# Publish one self-contained x64 executable
dotnet publish --configuration Release -p:Platform=x64
```

The executable is written to `bin/Release/net8.0-windows10.0.19041.0/win-x64/single-file/`. Use the `win-x86` or `win-arm64` publish profile to target another architecture.

## 🌍 Supported Languages

| Language | Status |
|----------|--------|
| English | ✅ Complete |
| Español (Spanish) | ✅ Complete |
| Português (Portuguese) | ✅ Complete |

The application automatically detects your system language.

## Troubleshooting

If a setup, activity, or update operation fails, check `%LOCALAPPDATA%\DiscordActivityMock\app.log`. The log is stored locally and rotated after it grows beyond 1 MiB.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the project
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📞 Contact & Support

- **Discord**: [Join our server](https://discord.com)
- **GitHub**: [@iiroak](https://github.com/iiroak)

## ⭐ Show your support

Give a ⭐ if this project helped you!

---

<p align="center">Made with ❤️ by <a href="https://github.com/iiroak">iroaK</a></p>
