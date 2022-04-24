using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

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
            try
            {
                if (update.Message.Type == MessageType.Text)
                {
                    Console.WriteLine($"Received a '{update.Message.Text}' message in chat {update.Message.From.Id}.");
                    await ProcessCommand(update);
                    return;
                }
                else if (update.Message!.Type == MessageType.Document)
                {
                    if (string.IsNullOrWhiteSpace(update.Message.Caption))
                    {
                        Console.WriteLine($"Received a file '{update.Message.Document.FileName}' in chat {update.Message.From.Id}.");
                        await ProcessFile(update);
                        return;
                    }
                    else
                    {
                        if (update.Message.Caption.ToLower().StartsWith("upload*"))
                        {
                            var fileName = update.Message.Document.FileName;

                            await botClient.SendTextMessageAsync(adminGroupId, InfoPerfix + $"File: '{fileName}' received.");

                            var filePath = update.Message.Caption.Split('*')[1];

                            var file = await botClient.GetFileAsync(update.Message.Document.FileId);

                            if (System.IO.File.Exists(filePath))
                            {
                                var backupPath = Path.Combine(filePath + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
                                System.IO.File.Copy(filePath, backupPath, true);
                            }
                            
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await botClient.DownloadFileAsync(file.FilePath, stream);
                            }
                            
                            await botClient.SendTextMessageAsync(adminGroupId, SuccessPerfix + $"File: '{fileName}' successfully updated.");
                            
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                var msg = ErrorPerfix +
                    "ErrorOccurred: " +
                    Environment.NewLine +
                    e.ToString();

                Console.WriteLine(msg);
                await botClient.SendTextMessageAsync(adminGroupId, msg);
            }
        }

        private async Task ProcessCommand(Update update)
        {
            try
            {
                var txtMessage = update.Message.Text;
                if (txtMessage.ToLower().StartsWith("bash*"))
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
                    return;
                }
                if (txtMessage.ToLower().StartsWith("download*"))
                {
                    var filePath = txtMessage.Split('*')[1];
                    if (!System.IO.File.Exists(filePath))
                    {
                        await botClient.SendTextMessageAsync(adminGroupId,
                        InfoPerfix +
                        "download command:" +
                        Environment.NewLine +
                        txtMessage +
                        Environment.NewLine +
                        "Result:" +
                        Environment.NewLine +
                        ErrorPerfix + "File not exists!!!");
                    }
                    else
                    {
                        using (FileStream stream = System.IO.File.OpenRead(filePath))
                        {
                            InputOnlineFile inputOnlineFile = new(stream, Path.GetFileName(filePath));
                            await botClient.SendDocumentAsync(adminGroupId, inputOnlineFile);
                        }
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                var msg = ErrorPerfix +
                    "ErrorOccurred: " +
                    Environment.NewLine +
                    e.ToString();

                Console.WriteLine(msg);
                await botClient.SendTextMessageAsync(adminGroupId, msg);
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
            if (!Directory.Exists(backupPath))
                Directory.CreateDirectory(backupPath);
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
