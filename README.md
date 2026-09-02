# 🏗️ RAT_Build: Build Suite & Relay Dashboard

**RAT_Build** provides an integrated compilation environment and central web relay management system for the **StealthRAT** platform. It contains the C# agent codebase, an organized deployment structure, and a **Node.js + WebSockets** relay server that enables remote administration through a web browser.

---

## 📁 Repository Structure

```text
RAT_Build/
├── dashboard/               # Central Relay Server & Web Dashboard
│   ├── server.js            # Express server & WebSocket router
│   ├── package.json         # Node.js dependencies (express, ws)
│   └── public/              # Web dashboard frontend (HTML5, CSS, JS)
│
├── StealthRAT/              # C# .NET 10.0 Remote Administration Agent
│   ├── Program.cs           # Stealth initialization and service orchestration
│   ├── StealthRAT.csproj    # .NET project configuration
│   ├── Handlers/            # Command handlers (process, power, input, files)
│   ├── Services/            # System services (audio, screen, persistence)
│   └── Models/              # Configuration models
│
├── improved_project3/       # Packaged distribution with specialized documentation
│   ├── README.md
│   └── StealthRAT/
│
├── .gitignore               # Excludes node_modules, build outputs, and large binaries
└── README.md                # Project documentation
```

---

## 🌟 Features

### Central Web Relay Server (`dashboard/`)
- **WebSocket Multiplexing**: Connects multiple agents and web dashboard operators concurrently.
- **Token-Based Authentication**: Secures websocket traffic and client registrations with a configurable pre-shared token (`secret123` by default).
- **Web-Based Management**: Monitor active agent connections, latency, and send commands directly from a browser canvas.

### Stealth Agent (`StealthRAT/`)
- Developed in C# targeting **.NET 10.0-windows**.
- Operates silently with hidden windows (`WinExe`), console detachment (`FreeConsole`), and registry persistence.
- Provides multi-technique screen capture and audio streaming via `NAudio`.

---

## 🚀 Getting Started

### Prerequisites
- [Node.js](https://nodejs.org/) (version 18 or higher)
- [.NET SDK 10.0](https://dotnet.microsoft.com/download)

### 1. Running the Relay Dashboard
1. Navigate to the `dashboard` directory:
   ```bash
   cd dashboard
   ```
2. Install npm dependencies:
   ```bash
   npm install
   ```
3. Start the relay server:
   ```bash
   npm start
   # Or directly: node server.js
   ```
4. Access the web interface at: **`http://localhost:3000`**
   - Default port: `3000` (configurable via the `PORT` environment variable).
   - Default authentication token: `secret123` (configured in `server.js`).

### 2. Compiling the StealthRAT Agent
1. Navigate to the agent directory:
   ```powershell
   cd StealthRAT
   ```
2. Build a self-contained, single-file executable:
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```
3. The compiled binary will be placed in:
   `bin/Release/net10.0-windows/win-x64/publish/StealthRAT.exe`

---

## ⚠️ Disclaimer
This software is provided strictly for educational purposes, defensive cyber analysis, and authorized penetration testing. Unauthorized deployment against any computing device is strictly prohibited.
