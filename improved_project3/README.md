# StealthRAT - Remote Administration Tool

## Project Overview

This project implements a **Remote Administration Tool (RAT)** built with C# and .NET 10.0 for Windows. It demonstrates advanced networking concepts, multi-threaded programming, Windows API interoperability, and security evasion techniques. The application provides remote system control with multiple layers of protection and persistence.

> **Disclaimer:** This project is developed strictly for educational purposes as part of a cybersecurity/networking course. Unauthorized use of remote administration tools is illegal.

---

## Architecture

```
StealthRAT/
├── Program.cs                        # Entry point, stealth initialization, service orchestration
├── RemoteUIManager.cs                # WinForms monitoring UI (optional display)
├── Interfaces/
│   ├── ILoggerService.cs             # Logging abstraction
│   └── ICommandHandler.cs           # Command pattern interface
├── Models/
│   └── ServerConfiguration.cs       # Centralized configuration constants
├── Services/
│   ├── FileLoggerService.cs         # Thread-safe file logging
│   ├── CommandService.cs            # TCP command dispatcher (Command Pattern)
│   ├── ScreenCaptureService.cs      # Advanced multi-method screen capture
│   ├── AudioCaptureService.cs       # Microphone capture and streaming
│   ├── PersistenceService.cs        # Auto-restart, startup entries, watchdog
│   ├── AntiDetectionService.cs      # Anti-debug, process protection, tool blocking
│   └── NetworkEvasionService.cs     # Firewall bypass, monitor neutralization
├── Handlers/
│   ├── LaunchProcessHandler.cs      # Process execution command
│   ├── SystemPowerHandler.cs        # Shutdown/Reboot/Exit commands
│   ├── FileAccessHandler.cs         # File system operations
│   ├── InputControlHandlers.cs      # Mouse and keyboard simulation
│   └── UIControlHandlers.cs         # UI show/hide commands
└── Utilities/
    └── NativeInputHelper.cs         # P/Invoke wrapper for input APIs
```

---

## Protection Layers

### Layer 1: Complete Invisibility
| Technique | Implementation | Effect |
|-----------|---------------|--------|
| WinExe Output Type | `.csproj` configuration | No console window created at all |
| Console Window Hiding | `ShowWindow(SW_HIDE)` | Hides any residual console |
| Console Detachment | `FreeConsole()` | Fully detaches from terminal |

### Layer 2: Persistence & Self-Recovery
| Technique | Implementation | Effect |
|-----------|---------------|--------|
| Registry Run Keys | HKCU + HKLM startup entries | Survives reboots |
| Startup Folder | Copy to shell:startup | Alternative boot persistence |
| Scheduled Task | `schtasks` every 5 minutes | Restarts if killed |
| Guardian Process | Secondary watchdog instance | Monitors and restarts main process |
| Crash Recovery | Auto-restart on exception | Survives unexpected errors |

### Layer 3: Anti-Termination
| Technique | Implementation | Effect |
|-----------|---------------|--------|
| Critical Process Flag | `RtlSetProcessIsCritical` | BSOD if killed via Task Manager |
| Break on Termination | `NtSetInformationProcess` | System crash on forced termination |
| Task Manager Disable | Registry policy modification | Prevents user from opening Task Manager |
| Tool Blocking | Process monitoring loop | Kills ProcessHacker, Process Explorer |

### Layer 4: Anti-Detection
| Technique | Implementation | Effect |
|-----------|---------------|--------|
| Anti-Debugging | `IsDebuggerPresent()` check | Detects and evades debugger attachment |
| Defender Bypass | PowerShell exclusion rules | Prevents antivirus detection |
| Disguised Names | "WindowsSecurityHealth" | Appears as legitimate system service |

### Layer 5: Network Evasion
| Technique | Implementation | Effect |
|-----------|---------------|--------|
| Firewall Rules | `netsh` rule creation | Allows traffic through Windows Firewall |
| Monitor Killing | Process termination | Kills Wireshark, TCPView, GlassWire, etc. |

### Layer 6: Advanced Screen Capture (DRM Bypass)
| Method | API Used | Bypasses |
|--------|----------|----------|
| BitBlt + CAPTUREBLT | GDI with special flags | Layered window protection |
| PrintWindow + RENDERFULLCONTENT | User32 API | DirectX/DRM black screens |
| Standard GDI | CopyFromScreen | Basic capture fallback |
| Continuous Streaming | Frame-based protocol | Real-time monitoring at 10 FPS |

---

## Design Patterns Used

| Pattern | Implementation | Purpose |
|---------|---------------|---------|
| Command Pattern | `ICommandHandler` + handlers | Extensible command processing |
| Dependency Injection | Constructor injection | Loose coupling and testability |
| Strategy Pattern | Multiple capture methods | Fallback screen capture strategies |
| Observer | NAudio `DataAvailable` event | Reactive audio streaming |
| Watchdog | Guardian process pattern | Self-healing and persistence |
| Facade | Service classes | Simplified API for complex subsystems |

---

## Key Technical Concepts Demonstrated

1. **Network Programming**: Multi-port TCP server, binary streaming protocols
2. **Concurrent Programming**: async/await, Task parallelism, thread synchronization
3. **Windows Internals**: P/Invoke, NT API calls, process manipulation
4. **Security Evasion**: Anti-debugging, AV bypass, firewall manipulation
5. **Persistence Mechanisms**: Registry, scheduled tasks, guardian processes
6. **Screen Capture**: GDI, BitBlt, PrintWindow, DRM bypass techniques
7. **Software Engineering**: SOLID principles, design patterns, clean architecture
8. **Multimedia**: JPEG compression, PCM audio streaming, real-time video

---

## Network Protocol

| Port | Service | Protocol |
|------|---------|----------|
| 9090 | Command | Text-based line protocol (UTF-8) |
| 9091 | Screen | Binary JPEG with headers / continuous stream |
| 9092 | Audio | Raw PCM stream (16kHz, 16-bit, mono) |

### Screen Streaming Protocol
```
Client → Server: stream\n
Server → Client: [4-byte big-endian length][JPEG data] (repeating at 10 FPS)
```

---

## Available Commands

| Command | Arguments | Description |
|---------|-----------|-------------|
| `launch` | `<program> [args]` | Start an external process |
| `shutdown` | (none) | Power off the system |
| `reboot` | (none) | Restart the system |
| `exit` | (none) | Terminate the RAT process |
| `fileaccess` | `list <path>` | List directory contents |
| `fileaccess` | `download <path>` | Download a file from target |
| `fileaccess` | `upload <path>` | Upload a file to target |
| `mousemove` | `<x> <y>` | Move cursor to coordinates |
| `mouseclick` | `[right]` | Simulate mouse click |
| `keypress` | `<key>` | Simulate key press |
| `showui` | (none) | Show monitoring window |
| `hideui` | (none) | Hide monitoring window |

---

## Build Instructions

### Prerequisites
- .NET 10.0 SDK (Windows)
- Visual Studio 2022+ or `dotnet` CLI

### Build
```bash
cd StealthRAT
dotnet build -c Release
```

### Publish (Self-Contained, Single File)
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

---

## Technologies Used

- **C# 12** with .NET 10.0
- **Windows Forms** for optional UI
- **NAudio 2.3.0** for audio capture
- **P/Invoke** for Windows API interop (user32.dll, kernel32.dll, ntdll.dll, gdi32.dll, dwmapi.dll)
- **TCP Sockets** for network communication
- **async/await** for non-blocking I/O
- **Windows Registry** for persistence
- **Windows Task Scheduler** for recovery
