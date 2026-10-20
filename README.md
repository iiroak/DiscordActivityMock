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

> **Windows compatibility:** Each release has one x86 executable for Windows 10/11 x64. Windows runs x86 programs through WoW64, and the app bundles its matching x86 WordPad backup. WordPad's removal from Windows 11 version 24H2 does not affect the bundled backup.

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

- Windows 10/11 x64 (Windows 10 build 17763 or newer).

### Installation

Download the single x86 `.exe` from [Releases](https://github.com/iiroak/DiscordActivityMock/releases) and run it directly. It is self-contained, includes the x86 WordPad backup, and does not require .NET installation or an accompanying ZIP. Keep its versioned filename unchanged; WinUI single-file apps require the name they were built with. Release availability depends on a maintainer publishing a tagged build. The app checks for releases but does not install updates automatically.

### Usage

1. **Get WordPad**: Click "Copy from System" if WordPad is installed, or click "Use bundled x86" for the included backup.
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

# Publish the self-contained x86 executable
dotnet publish --configuration Release -r win-x86 -p:Platform=x86 -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o publish/win-x86
```

The output directory contains the single self-contained x86 executable. GitHub Actions publishes that executable as the only release asset.

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
