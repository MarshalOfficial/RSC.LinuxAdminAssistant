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
        private static readonly HttpClient httpClient = new HttpClient();

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
                        await SendFileInChunksAsync(filePath, txtMessage);
                    }
                    return;
                }

                if (txtMessage.ToLower().StartsWith("fetch*"))
                {
                    var url = txtMessage.Substring(txtMessage.IndexOf('*') + 1).Trim();
                    try
                    {
                        var uri = new Uri(url);
                        var fileName = Path.GetFileName(uri.LocalPath);
                        if (string.IsNullOrWhiteSpace(fileName)) fileName = "downloaded_file";
                        var localTempFilePath = Path.Combine(baseFolderPath, fileName);

                        var msg = await botClient.SendMessage(adminGroupId, InfoPerfix + $"Starting fetch from {url}...");

                        using (var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            var contentType = response.Content.Headers.ContentType?.MediaType;

                            if (contentType != null && contentType.Contains("text/html") && Environment.OSVersion.Platform == PlatformID.Unix)
                            {
                                await botClient.EditMessageText(chatId: adminGroupId, messageId: msg.MessageId, text: InfoPerfix + $"HTML Webpage detected. Cloning site assets via wget...");
                                
                                var tempWebDir = Path.Combine(baseFolderPath, "webdl_" + Guid.NewGuid().ToString("N"));
                                Directory.CreateDirectory(tempWebDir);

                                var wgetProcess = new Process();
                                wgetProcess.StartInfo.FileName = "wget";
                                wgetProcess.StartInfo.Arguments = $"--execute=\"robots=off\" --page-requisites --convert-links --adjust-extension --no-parent -P \"{tempWebDir}\" \"{url}\"";
                                wgetProcess.StartInfo.UseShellExecute = false;
                                wgetProcess.StartInfo.CreateNoWindow = true;
                                wgetProcess.Start();
                                await wgetProcess.WaitForExitAsync();

                                var zipPath = localTempFilePath + ".zip";
                                System.IO.Compression.ZipFile.CreateFromDirectory(tempWebDir, zipPath);
                                
                                await botClient.EditMessageText(chatId: adminGroupId, messageId: msg.MessageId, text: InfoPerfix + $"Webpage cloned and zipped. Now sending to Telegram group...");
                                await SendFileInChunksAsync(zipPath, txtMessage);
                                
                                try { System.IO.File.Delete(zipPath); } catch {}
                                try { Directory.Delete(tempWebDir, true); } catch {}
                            }
                            else
                            {
                                using (var stream = await response.Content.ReadAsStreamAsync())
                                using (var fileStream = new FileStream(localTempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                                {
                                    await stream.CopyToAsync(fileStream);
                                }
                                await botClient.EditMessageText(chatId: adminGroupId, messageId: msg.MessageId, text: InfoPerfix + $"Saved locally as {localTempFilePath}. Now sending to Telegram group...");
                                await SendFileInChunksAsync(localTempFilePath, txtMessage);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await botClient.SendMessage(adminGroupId, ErrorPerfix + "Fetch Failed:\n" + ex.Message);
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

        private async Task SendFileInChunksAsync(string filePath, string commandText)
        {
            long maxFileSize = 49L * 1024 * 1024; // 49 MB limit for bots
            var fi = new FileInfo(filePath);

            if (fi.Length <= maxFileSize)
            {
                using FileStream stream = System.IO.File.OpenRead(filePath);
                InputFile inputOnlineFile = InputFile.FromStream(stream, Path.GetFileName(filePath));
                await botClient.SendDocument(adminGroupId, inputOnlineFile);
                return;
            }

            await botClient.SendMessage(adminGroupId, InfoPerfix + $"File ({(fi.Length / 1024 / 1024)}MB) exceeds Telegram's 50MB limit. Splitting into 49MB chunks...");

            int totalParts = (int)Math.Ceiling((double)fi.Length / maxFileSize);
            using FileStream fs = System.IO.File.OpenRead(filePath);
            byte[] buffer = new byte[81920];
            
            for (int i = 1; i <= totalParts; i++)
            {
                string partFileName = Path.GetFileName(filePath) + $".part{i}";
                string partPath = filePath + $".part{i}";
                
                using (FileStream partStream = new FileStream(partPath, FileMode.Create))
                {
                    long currentPartSize = 0;
                    int bytesRead;
                    while (currentPartSize < maxFileSize && (bytesRead = await fs.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, maxFileSize - currentPartSize))) > 0)
                    {
                        await partStream.WriteAsync(buffer, 0, bytesRead);
                        currentPartSize += bytesRead;
                    }
                }

                using FileStream readPartStream = System.IO.File.OpenRead(partPath);
                InputFile partFile = InputFile.FromStream(readPartStream, partFileName);
                await botClient.SendDocument(adminGroupId, partFile, caption: $"Part {i} of {totalParts}");
                
                readPartStream.Close();
                try { System.IO.File.Delete(partPath); } catch { }
            }
        }

    }
}
