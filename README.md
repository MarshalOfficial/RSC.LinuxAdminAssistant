# RSC.LinuxAdminAssistant

> A powerful, interactive remote server assistant via a Telegram bot. Run secure bash/cmd commands, deploy application updates, and manage your server's filesystem effortlessly—all from Telegram.

`RSC.LinuxAdminAssistant` is a lightweight .NET application designed to give you continuous control over your VPS or local server (supports both Linux and Windows). It enables you to execute shell commands interactively, upload new application builds, automatically back up current deployments before overwriting, and securely download log files or configuration documents directly to your Telegram chat. 

## Features

- **Interactive Command Execution**: Run shell commands remotely. Unlike simple bots, this app streams standard output (`stdout`) and standard error (`stderr`) back to Telegram continuously, keeping you updated in real-time for long-running processes (e.g., `ping`, `top`).
- **Cross-Platform Support**: Effortlessly processes system commands using `/bin/bash` on Unix environments and `cmd.exe` on Windows.
- **Task Cancellation (`bash*kill`)**: Easily terminate long-running or hanging scripts securely without crashing the bot natively.
- **Smart Deployments**: Upload a ZIP file to your bot's group chat along with the target destination, and the bot will cleanly back up the old configuration, decompress the ZIP, and inject the new build automatically.
- **File Management**: `download*` and `upload*` files remotely from absolute server filepaths with automated `.bak` rotations to ensure nothing is lost during an overwrite.
- **Container Ready**: A fully dockerized setup allows dropping this bot into any architecture within seconds.

---

## Getting Started

### 1. Telegram Bot Setup
1. Message [@BotFather](https://t.me/BotFather) on Telegram and create a new bot to get your **Bot API Key**.
2. Create a private Telegram group and add your bot.
3. Find your **Admin Group ID** (Chat ID) so the bot knows to only accept commands from that specific secured group.

### 2. Standard Installation (Bare-Metal strictly recommended)

> [!WARNING]
> Do **NOT** use Docker to deploy this bot. Docker inherently isolates applications into virtual sandbox containers, meaning `bash*` commands and filesystem actions will only execute inside a temporary void, completely failing to administrate your actual server host. This bot must be installed natively Bare-Metal to manage your actual infrastructure!

**Prerequisites**: [install .NET 10.0 Runtime](https://dotnet.microsoft.com/download) on your server.

1. Clone and publish the repository for your environment:
   ```bash
   dotnet publish -c Release -o ./publish
   ```
2. Copy the `publish` directory natively onto your server.
3. Configure your keys inside `appsettings.json`.
4. Create a system persistence daemon to run it eternally in the background natively.

**Example `systemd` setup (Ubuntu/Debian):**
```bash
# 1. Create a service file
sudo nano /etc/systemd/system/linux-admin-bot.service

# 2. Paste the configuration (adjust paths!):
[Unit]
Description=RSC Linux Admin Assistant Bot
After=network.target

[Service]
WorkingDirectory=/path/to/your/publish/folder
ExecStart=/usr/bin/dotnet /path/to/your/publish/folder/RSC.LinuxAdminAssistant.dll
Restart=always
RestartSec=10
SyslogIdentifier=linux-admin-bot
User=root # (Warning: Root gives bot full admin power over OS)

[Install]
WantedBy=multi-user.target

# 3. Save, activate, and run forever:
sudo systemctl daemon-reload
sudo systemctl enable linux-admin-bot.service
sudo systemctl start linux-admin-bot.service
```

---

## Usage Guide

Send messages directly inside your secure Telegram group to control your server:

**Run a Shell Command:**
```text
bash* YOUR_COMMAND
```
*Examples:*  
`bash* date`  
`bash* ls -la`  
`bash* supervisorctl status all`  

**Stop a Hanging Command:**
```text
bash*kill
```
*(Stops the currently running process if it gets trapped in an infinite loop)*  

**Fetch a File or Webpage from the Internet:**
Downloads a remote file directly to the server, and then automatically uploads it to the Telegram chat. 
*(If you are running the bot on **Linux** and provide the link to an HTML webpage instead of a direct file, the bot will intelligently use `wget` to clone the entire website (HTML, CSS, and Images), compress it into a `.zip` archive, and send it to your chat!)*
```text
fetch* https://example.com/database.sql
fetch* https://example.com/
```

**Download a Target File:**
```text
download* /absolute/path/to/file.conf
```
*(Note: For both `fetch*` and `download*`, if the file is over Telegram's 50MB bot upload limit, the bot will automatically split it into 49MB chunks (e.g. `.part1`, `.part2`) and send each chunk sequentially to bypass the limit).*

**Upload a Target File:**
Attach a document in Telegram (e.g. `nginx.conf`) and put the path as the file **caption**:
```text
upload* /etc/nginx/nginx.conf
```
*(If the destination file already exists, a timestamped copy will automatically be recorded.)*

**Automated App Updating via ZIP:**
Just send a `.zip` document to the group! Ensure the ZIP file name matches exactly to a sub-folder within your `ServerBaseFolder` set in your `appsettings.json`. The bot will back up the current deployment and extract the new content over it. 

---

> **⚠️ Security Warning:** This bot acts natively as the user executing the host application. If run under `root` or `Administrator`, the bot holds full control of your infrastructure. Use responsibly.
