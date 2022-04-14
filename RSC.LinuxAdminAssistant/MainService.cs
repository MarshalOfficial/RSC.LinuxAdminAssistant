using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RSC.LinuxAdminAssistant
{
    public class MainService
    {
        private const string ErrorPerfix = "🛑 ";
        private const string InfoPerfix = "ℹ️ ";
        private const string SuccessPerfix = "✅ ";

        private readonly TelegramBotClient botClient;
        private readonly IConfiguration config;
        private long adminGroupId;
        private string baseFolderPath;
        private string backupFolderPath;

        public MainService(TelegramBotClient BotClient, IConfiguration Config)
        {                        
            botClient = BotClient;
            config = Config;
            adminGroupId = long.Parse(config.GetValue<string>("AdminGroupId"));
            baseFolderPath = config.GetValue<string>("ServerBaseFolder");
            backupFolderPath = config.GetValue<string>("ServerBackupFolder");
            
            if(!Directory.Exists(backupFolderPath))
            {
                Directory.CreateDirectory(backupFolderPath);
            }
        }    

        public async Task ProcessMessage(Update update)
        {
            if (update.Message.Type == MessageType.Text)
            {
                Console.WriteLine($"Received a '{update.Message.Text}' message in chat {update.Message.From.Id}.");
                await ProcessCommand(update);
            }                
            else if (update.Message!.Type == MessageType.Document)
            {
                Console.WriteLine($"Received a file '{update.Message.Document.FileName}' in chat {update.Message.From.Id}.");
                await ProcessFile(update);
            }
        }

        private async Task ProcessCommand(Update update)
        {
            var txtMessage = update.Message.Text;
            if (txtMessage.StartsWith("bash*"))
            {
                var bashCommand = txtMessage.Split('*')[1];
                Process proc = new();

                if (Environment.OSVersion.Platform == PlatformID.Unix)
                    proc.StartInfo.FileName = "/bin/bash";
                else
                    proc.StartInfo.FileName = "cmd.exe";

                proc.StartInfo.Arguments = "-c \" " + bashCommand + " \"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.Start();
                proc.WaitForExit();

                var result = proc.StandardOutput.ReadToEnd();
                await botClient.SendTextMessageAsync(adminGroupId,
                    InfoPerfix +
                    "Bash command:" +
                    Environment.NewLine +
                    bashCommand +
                    Environment.NewLine +
                    "Result:" +
                    Environment.NewLine +
                    result);
            }
        }

        private async Task ProcessFile(Update update)
        {
            if (!update.Message.Document.MimeType.ToLower().Contains("zip"))
            {
                await botClient.SendTextMessageAsync(adminGroupId, ErrorPerfix + "File type error! zip files are only acceptable.");
                return;
            }

            var fileName = update.Message.Document.FileName;

            await botClient.SendTextMessageAsync(adminGroupId, InfoPerfix + $"File: '{fileName}' received.");

            var dir = Path.Combine(baseFolderPath, fileName.Split('.')[0]);
            
            if (!Directory.Exists(dir))
            {
                await botClient.SendTextMessageAsync(adminGroupId, ErrorPerfix + "File name is invalid! zip file name must be equivalent to target folder name on the server.");
                return;
            }

            //backup process
            var backupPath = Path.Combine(backupFolderPath, fileName.Split('.')[0] + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
            foreach (string dirPath in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(dir, backupPath));
            }            
            foreach (string newPath in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                System.IO.File.Copy(newPath, newPath.Replace(dir, backupPath), true);
            }


            var file = await botClient.GetFileAsync(update.Message.Document.FileId);
            
            var zipPath = Path.Combine(dir, fileName);

            using (var stream = new FileStream(zipPath, FileMode.Create))
            {
                await botClient.DownloadFileAsync(file.FilePath, stream);                
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, dir, true);
            
            System.IO.File.Delete(zipPath);

            await botClient.SendTextMessageAsync(adminGroupId, SuccessPerfix + $"File: '{fileName}' successfully download, unzip, replaced to the target folder.");

        }

    }
}
