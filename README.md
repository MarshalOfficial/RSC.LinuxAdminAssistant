# RSC.LinuxAdminAssistant

A Telegram bot that will act as an remote assistant on your server, you can run bash commands remotely on your server via this bot, also send your applications file updates to the bot and it will get a backup from target folder then unzip and replace new update for you.

Step by step guide:  
* create a new bot via TelegramBotFather and get your bot API key  
* create a Telegram group and add your bot as admin to the group  
* get a publish from the project and install it on your server(Linux/Windows)  
https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish  
* make sure that you installed dotnet 6 runtime on your server  
* copy publish folder on your server  
* run the project   
Linux: dotnet RSC.RSC.LinuxAdminAssistant.dll  
Windows: double click on RSC.RSC.LinuxAdminAssistant.exe  
* in my case to make the bot run in 7*24 I used supervisor application in linux:  
https://www.digitalocean.com/community/tutorials/how-to-install-and-manage-supervisor-on-ubuntu-and-debian-vps  
* now you can talk to bot in telegram group, to execute bash command, you must use this format:  
bash + * + YOURCOMMAND  
bash* date  
bash* ls  
bash* supervisorctl status all  
* if you send a zip file to the group, bot will search in a target folder that you set before in appsettings.json, and if find a folder with that name, will make a backup from that folder then unzip and replace zip file to the folder for you.  
* download*{path} for example: download*/etc/nginx/nginx.conf will download the target file for you into current chat
* sending a file with this caption: upload*{path} 
for example: upload*/etc/nginx/nginx.conf will upload the file to the target path for you. (If the file doesn't exist it will create it there, otherwise, the bot will create a backup from the file by appending DateTime to the end of the file name and then replacing it for you)

* Docker   
** to run the project via docker, you need to build the image then run it, so inside the project folder run these below commands:   
1- docker build -t rsc-linux-assistant .   
2- docker run -d -e BotApiKey='YourBotApiKey' -e ProxyEnable='false' -e ProxyIP='127.0.0.1' -e ProxyPort='9050' -e AdminGroupId='YourTelegramGroupChatId' -e ServerBaseFolder='/home/X' -e ServerBackupFolder='/home/X/backups'  --name test-assistant rsc-linux-assistant   
(note that you must pass all the configuration values that are inside appsettings.json as docker run environment variables)   

* WARNING: if your are running the bot as administrator or root, be careful what you send to call on your server 😉.
