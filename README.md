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

### 2. Standard Installation (Bare Metal)

**Prerequisites**: [install .NET Runtime](https://dotnet.microsoft.com/download) on your server.

1. Clone and publish the repository for your environment:
   ```bash
   dotnet publish -c Release -o ./publish
   ```
2. Copy the `publish` directory onto your server.
3. Configure your properties in `appsettings.json`.
4. Run the application:
   - **Linux**: `dotnet RSC.LinuxAdminAssistant.dll`
   - **Windows**: `RSC.LinuxAdminAssistant.exe`

*(Tip: On Linux, we recommend using [Supervisor](https://www.digitalocean.com/community/tutorials/how-to-install-and-manage-supervisor-on-ubuntu-and-debian-vps) or a systemd service to keep the bot running 24/7).*

### 3. Docker Installation

For an isolated launch or simple deployment, you can use Docker. Follow these steps to build the image, push it to your Docker Hub for public use, and use Docker Compose to run it anywhere.

#### Step 1: Build and Push to Docker Hub
To publish the docker image so anyone can pull it, run:
```bash
# 1. Navigate to the project directory
cd RSC.LinuxAdminAssistant

# 2. Build the image (replace <your-dockerhub-username> with yours)
docker build -t <your-dockerhub-username>/rsc-linux-assistant:latest .

# 3. Log into Docker Desktop or CLI
docker login

# 4. Push the image to Docker Hub
docker push <your-dockerhub-username>/rsc-linux-assistant:latest
```

#### Step 2: Run via Docker Compose
Create a `docker-compose.yml` file anywhere on your target server and paste the following configuration:

```yaml
version: '3.8'

services:
  linux-admin-assistant:
    image: <your-dockerhub-username>/rsc-linux-assistant:latest
    container_name: rsc-linux-assistant
    restart: unless-stopped
    environment:
      - BotApiKey=YOUR-BOT-API-KEY
      - ProxyEnable=false
      - ProxyIP=127.0.0.1
      - ProxyPort=9050
      - AdminGroupId=YOUR-ADMIN-GROUP-ID
      - ServerBaseFolder=/server/base/
      - ServerBackupFolder=/server/base/backups
    # volumes:
    #  - /:/host_root:rw # Optional: Mount host root to allow the bot to manage the host system files.
```

To start the bot, run:
```bash
docker-compose up -d
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

**Fetch a File from the Internet:**
Downloads a remote file directly to the server, and then automatically uploads it to the Telegram chat.
```text
fetch* https://example.com/database.sql
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
