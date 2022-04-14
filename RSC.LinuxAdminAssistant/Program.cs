using Microsoft.Extensions.Configuration;
using MihaZupan;
using RSC.LinuxAdminAssistant;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var proxy = new HttpToSocks5Proxy(config.GetValue<string>("ProxyIP"), config.GetValue<int>("ProxyPort"));

proxy.ResolveHostnamesLocally = true;

var httpClient = new HttpClient(
    new HttpClientHandler { Proxy = proxy, UseProxy = true }
);

var isProxyEnable = config.GetValue<bool>("ProxyEnable");
var botClient = new TelegramBotClient(config.GetValue<string>("BotApiKey"), isProxyEnable ? httpClient : null);

var mainService = new MainService(botClient, config);

using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = { }
};

botClient.StartReceiving(
    HandleUpdateAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token);


var me = await botClient.GetMeAsync();
Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    try
    {
        Thread thread = new(async () => await Handle(update));
        thread.Start();
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
}

async Task Handle(Update update)
{
    try
    {
        if (update.Message == null || 
            update.Message.From == null || 
            update.Message.From.IsBot || 
            update.Message.Chat.Id.ToString() != config.GetValue<string>("AdminGroupId") || 
            update.Type != UpdateType.Message)
            return;

        if (update.Message!.Type != MessageType.Text && 
            update.Message!.Type != MessageType.Document)
            return;
                             
        await mainService.ProcessMessage(update);                
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        await botClient.SendTextMessageAsync(config.GetValue<string>("AdminGroupId"), "🛑 " + ex.ToString());
    }
}

Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);    

    return Task.CompletedTask;
}



static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
{
    var txt = e.ExceptionObject.ToString();
    Console.WriteLine(txt);    
    Environment.Exit(1);
}