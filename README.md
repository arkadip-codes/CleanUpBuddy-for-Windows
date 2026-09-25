# CleanUp Buddy for Windows

A lightweight Windows utility for **cleaning your screen without accidentally touching your computer**.

CleanUp Buddy temporarily locks the keyboard and/or mouse while you clean your display. It also provides a dual-Alt unlock mechanism, brightness boost, Windows theme integration, system-tray controls, and persistent settings.

This Windows version is inspired by the original **CleanUp Buddy macOS app by Gui Rambo**. The original macOS app is available at https://cleanupbuddy.app/.

> **Note:** It is a vibe coded app.

## ✨ Features

* **⌨️ Keyboard Lock** — Blocks keyboard input during a cleaning session.
* **🖱️ Mouse Lock** — Blocks mouse input during a cleaning session.
* **🔓 Dual-Alt Unlock** — Hold **Left Alt + Right Alt** together for 3 seconds to end a session.
* **☀️ Brightness Boost** — Temporarily increases display brightness and restores the previous setting afterward.
* **💤 Stay Awake** — Prevents the system and display from sleeping during cleaning.
* **⚡ Power & Session Safety** — Stops cleaning if Windows suspends or the session is locked.
* **🎨 Theme Support** — Light, Dark, or System theme.
* **🪟 Windows Accent Integration** — Uses the Windows accent color throughout the UI.
* **🔔 System Tray Support** — Open the app or start cleaning from the tray.
* **💾 Persistent Settings** — Stores preferences locally and restores them on startup.
* **🧪 Alt-Key Test** — Test the unlock gesture before starting a cleaning session.
* **1️⃣ Single Instance** — Prevents multiple instances from running simultaneously.

## 🧹 How It Works

1. Choose whether to lock the **keyboard**, **mouse**, or both.
2. Configure the optional **brightness boost**.
3. Start cleaning mode.
4. Clean your screen while the selected input devices remain locked.
5. Hold **Left Alt + Right Alt** together for **3 seconds**.
6. The session ends and the previous display and power state is restored.

The app uses low-level Windows keyboard and mouse hooks during an active cleaning session so normal input cannot accidentally interfere with screen cleaning.

## 🖥️ Requirements

* **Windows 10 / Windows 11**
* **.NET 8**
* A Windows-compatible display for brightness control

## 🛠️ Tech Stack

| Technology                   | Purpose                                   |
| ---------------------------- | ----------------------------------------- |
| **C# / .NET 8**              | Core application                          |
| **WPF**                      | Windows desktop UI                        |
| **Windows API / P/Invoke**   | Keyboard/mouse hooks and power management |
| **System.Management**        | Windows system management                 |
| **Hardcodet.NotifyIcon.Wpf** | System-tray integration                   |
| **Windows Registry**         | Theme, accent color, and preferences      |
| **System.Text.Json**         | Local settings persistence                |

## 📁 Project Structure

```text
CleanUpBuddy-for-Windows/
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── CleaningOverlay.xaml
├── CleaningOverlay.xaml.cs
├── Services/
│   ├── BrightnessService.cs
│   ├── HookService.cs
│   ├── PowerService.cs
│   └── ThemeService.cs
├── Helpers/
│   ├── AltCheckRegistry.cs
│   └── SettingsStore.cs
├── Models/
│   └── AppSettings.cs
├── Assets/
└── CleanupBuddy.csproj
```

## 🚀 Build & Run

```bash
git clone https://github.com/arkadip-codes/CleanUpBuddy-for-Windows.git
cd CleanUpBuddy-for-Windows
dotnet restore
dotnet build
dotnet run
```

### Release Build

```bash
dotnet build -c Release
```

### Self-contained x64 Publish

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

## ⚙️ Settings

Application preferences are stored locally as JSON:

```text
%APPDATA%\CleanupBuddy\settings.json
```

The Windows Registry is also used for the optional **"Skip Alt-key check on startup"** preference.

## 🔐 Safety

Before using cleaning mode, it is recommended to use the built-in **Alt-Key Test** to verify the unlock gesture.

### Emergency / Unlock Gesture

**Hold Left Alt + Right Alt for 3 seconds.**

This completes the unlock sequence and terminates the active cleaning session.

## 🎨 Design

The interface follows a simple Windows-native approach with:

* Minimal UI
* Rounded WPF components
* Windows accent-color integration
* Light and Dark modes
* Smooth animations
* System-tray integration
* Dedicated cleaning-session overlay

## 🙏 Inspiration

The Windows version is inspired by **CleanUp Buddy for macOS by Gui Rambo**.

Original macOS app: https://cleanupbuddy.app/

This project brings the same simple screen-cleaning concept to Windows using native Windows functionality and UI patterns.

## 📄 License

No license has currently been specified for this repository.

If you plan to distribute CleanUp Buddy publicly, consider adding an appropriate open-source or proprietary license.

## 👨‍💻 Author

**Arkadip Ghosh**

GitHub: https://github.com/arkadip-codes

---

### CleanUp Buddy

**Clean your screen without accidentally touching it.**
