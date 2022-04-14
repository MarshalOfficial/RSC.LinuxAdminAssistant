# RSC.LinuxAdminAssistant

A Telegram bot that will act as an remote assistant on your server, you can run bash commands remotely on your server via this bot, also send your applications file updates to the bot and it will get a backup from target folder then unzip and replace new update for you.

requirements:  
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
