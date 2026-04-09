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
        private readonly ConfigurationHelper.ConfigurationHelper config;
        private long adminGroupId;
        private string baseFolderPath;
        private string backupFolderPath;
        private Process? activeProcess;

        public MainService(TelegramBotClient BotClient, ConfigurationHelper.ConfigurationHelper Config)
        {                        
            botClient = BotClient;
            config = Config;
            adminGroupId = long.Parse(config.GetValue("AdminGroupId"));
            baseFolderPath = config.GetValue("ServerBaseFolder");
            backupFolderPath = config.GetValue("ServerBackupFolder");
            
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

                            await botClient.SendMessage(adminGroupId, InfoPerfix + $"File: '{fileName}' received.");

                            var filePath = update.Message.Caption.Substring(update.Message.Caption.IndexOf('*') + 1);

                            var file = await botClient.GetFile(update.Message.Document.FileId);

                            if (System.IO.File.Exists(filePath))
                            {
                                var backupPath = Path.Combine(filePath + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
                                System.IO.File.Copy(filePath, backupPath, true);
                            }
                            
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await botClient.DownloadFile(file.FilePath, stream);
                            }
                            
                            await botClient.SendMessage(adminGroupId, SuccessPerfix + $"File: '{fileName}' successfully updated.");
                            
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
                await botClient.SendMessage(adminGroupId, msg);
            }
        }

        private async Task ProcessCommand(Update update)
        {
            try
            {
                var txtMessage = update.Message.Text ?? "";
                
                if (txtMessage.ToLower().Trim() == "bash*kill")
                {
                    if (activeProcess != null && !activeProcess.HasExited)
                    {
                        activeProcess.Kill();
                        await botClient.SendMessage(adminGroupId, SuccessPerfix + "Running process killed.");
                    }
                    else
                    {
                        await botClient.SendMessage(adminGroupId, InfoPerfix + "No running process to kill.");
                    }
                    return;
                }

                if (txtMessage.ToLower().StartsWith("bash*"))
                {
                    if (activeProcess != null && !activeProcess.HasExited)
                    {
                        await botClient.SendMessage(adminGroupId, ErrorPerfix + "A process is already running. Wait for it to finish or send 'bash*kill' to terminate it.");
                        return;
                    }

                    var bashCommand = txtMessage.Substring(txtMessage.IndexOf('*') + 1);

                    activeProcess = new Process();

                    if (Environment.OSVersion.Platform == PlatformID.Unix)
                    {
                        activeProcess.StartInfo.FileName = "/bin/bash";
                        activeProcess.StartInfo.ArgumentList.Add("-c");
                        activeProcess.StartInfo.ArgumentList.Add(bashCommand);
                    }
                    else
                    {
                        activeProcess.StartInfo.FileName = "cmd.exe";
                        activeProcess.StartInfo.Arguments = "/c " + bashCommand;
                    }

                    activeProcess.StartInfo.UseShellExecute = false;
                    activeProcess.StartInfo.RedirectStandardOutput = true;
                    activeProcess.StartInfo.RedirectStandardError = true;
                    activeProcess.StartInfo.CreateNoWindow = true;

                    // Send initial message
                    var message = await botClient.SendMessage(adminGroupId, InfoPerfix + "Running:\n" + bashCommand + "\n\nResult:\n⏳...");

                    activeProcess.Start();

                    string outputBuffer = "";
                    object lockObj = new object();

                    activeProcess.OutputDataReceived += (s, e) => {
                        if (e.Data != null) lock (lockObj) outputBuffer += e.Data + "\n";
                    };
                    activeProcess.ErrorDataReceived += (s, e) => {
                        if (e.Data != null) lock (lockObj) outputBuffer += "ERROR: " + e.Data + "\n";
                    };

                    activeProcess.BeginOutputReadLine();
                    activeProcess.BeginErrorReadLine();

                    string lastSentText = "";
                    while (!activeProcess.HasExited)
                    {
                        await Task.Delay(1500); // update every 1.5s
                        string currentText;
                        lock (lockObj) { currentText = outputBuffer; }
                        
                        if (currentText != lastSentText)
                        {
                            var textToSend = currentText;
                            if (textToSend.Length > 4000)
                                textToSend = "..." + textToSend.Substring(textToSend.Length - 3990); // keep it within telegram limits
                                
                            try
                            {
                                await botClient.EditMessageText(
                                    chatId: adminGroupId,
                                    messageId: message.MessageId,
                                    text: InfoPerfix + "Running:\n" + bashCommand + "\n\nResult:\n" + textToSend + "\n⏳...");
                                lastSentText = currentText;
                            }
                            catch { /* Ignore edit limits if happens */ }
                        }
                    }

                    activeProcess.WaitForExit();
                    
                    // Final update
                    string finalText;
                    lock (lockObj) { finalText = outputBuffer; }
                    
                    if (string.IsNullOrWhiteSpace(finalText)) finalText = "[No output]";
                    
                    if (finalText.Length > 4000)
                        finalText = "..." + finalText.Substring(finalText.Length - 3990);

                    try
                    {
                        await botClient.EditMessageText(
                            chatId: adminGroupId,
                            messageId: message.MessageId,
                            text: InfoPerfix + "Bash command:\n" + bashCommand + "\n\nResult:\n" + finalText);
                    }
                    catch { } // Ignore if it's the exact same text
                    
                    activeProcess = null;
                    return;
                }
                if (txtMessage.ToLower().StartsWith("download*"))
                {
                    var filePath = txtMessage.Substring(txtMessage.IndexOf('*') + 1);
                    if (!System.IO.File.Exists(filePath))
                    {
                        await botClient.SendMessage(adminGroupId,
                        InfoPerfix +
                        "download command:\n" +
                        txtMessage +
                        "\nResult:\n" +
                        ErrorPerfix + "File not exists!!!");
                    }
                    else
                    {
                        using FileStream stream = System.IO.File.OpenRead(filePath);
                        InputFile inputOnlineFile = InputFile.FromStream(stream, Path.GetFileName(filePath));
                        await botClient.SendDocument(adminGroupId, inputOnlineFile);
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                var msg = ErrorPerfix +
                    "ErrorOccurred: \n" +
                    e.ToString();

                Console.WriteLine(msg);
                await botClient.SendMessage(adminGroupId, msg);
            }
        }

        private async Task ProcessFile(Update update)
        {
            if (!update.Message.Document.MimeType.ToLower().Contains("zip"))
            {
                await botClient.SendMessage(adminGroupId, ErrorPerfix + "File type error! zip files are only acceptable.");
                return;
            }

            var fileName = update.Message.Document.FileName;

            await botClient.SendMessage(adminGroupId, InfoPerfix + $"File: '{fileName}' received.");

            var dir = Path.Combine(baseFolderPath, fileName.Split('.')[0]);
            
            if (!Directory.Exists(dir))
            {
                await botClient.SendMessage(adminGroupId, ErrorPerfix + "File name is invalid! zip file name must be equivalent to target folder name on the server.");
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


            var file = await botClient.GetFile(update.Message.Document.FileId);
            
            var zipPath = Path.Combine(dir, fileName);

            using (var stream = new FileStream(zipPath, FileMode.Create))
            {
                await botClient.DownloadFile(file.FilePath, stream);                
            }

            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, dir, true);
                await botClient.SendMessage(adminGroupId, SuccessPerfix + $"File: '{fileName}' successfully download, unzip, replaced to the target folder.");
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(adminGroupId, ErrorPerfix + $"Failed to extract '{fileName}': {ex.Message}");
            }
            finally
            {
                if (System.IO.File.Exists(zipPath))
                {
                    System.IO.File.Delete(zipPath);
                }
            }

        }

    }
}
