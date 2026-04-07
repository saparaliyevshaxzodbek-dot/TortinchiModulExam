using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ExamTelegramBot_Project;

internal class Program
{
    private static readonly string BOT_TOKEN = "8492786743:AAF_lYp3bL2TxwEtXP2zMug2chhWFIwowbI";
    private static readonly string USERS_FILE = Path.Combine(AppContext.BaseDirectory, "users.json");
    private static readonly string PHOTOS_DIR = Path.Combine(AppContext.BaseDirectory, "downloaded_photos");
    private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    static async Task Main()
    {
        if (!Directory.Exists(PHOTOS_DIR)) Directory.CreateDirectory(PHOTOS_DIR);
        if (!File.Exists(USERS_FILE)) File.WriteAllText(USERS_FILE, "{}");

        var botClient = new TelegramBotClient(BOT_TOKEN);
        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message]
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cts.Token
        );

        Console.WriteLine("Bot ishga tushdi!");
        Console.ReadLine();
        await cts.CancelAsync();
    }

    static Dictionary<string, string> LoadUsers()
    {
        try
        {
            var json = File.ReadAllText(USERS_FILE);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    static void SaveUsers(Dictionary<string, string> users)
    {
        var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(USERS_FILE, json);
    }

    static List<string> SearchPhotos(string query)
    {
        var urls = new List<string>();
        var random = new Random();
        var encodedQuery = Uri.EscapeDataString(query);

        for (int i = 0; i < 3; i++)
        {
            var seed = random.Next(1, 100000);
            urls.Add($"https://image.pollinations.ai/prompt/{encodedQuery}?width=600&height=400&nologo=true&seed={seed}");
        }

        return urls;
    }

    static async Task<string> DownloadPhoto(string photoUrl, string query, string userId)
    {
        try
        {
            var response = await httpClient.GetAsync(photoUrl);
            if (response.IsSuccessStatusCode)
            {
                var safeQuery = string.Concat(query.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))).Replace(' ', '_');
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                var fileName = $"{userId}_{safeQuery}_{timestamp}.jpg";
                var filePath = Path.Combine(PHOTOS_DIR, fileName);

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream);

                return filePath;
            }
        }
        catch
        {
        }
        return string.Empty;
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Type != UpdateType.Message || update.Message?.Type != MessageType.Text) return;

            var message = update.Message;
            var userId = message.From!.Id.ToString();
            var userName = message.From.Username ?? message.From.FirstName ?? "Unknown";
            var chatId = message.Chat.Id;
            var text = message.Text?.Trim() ?? "";

            if (text == "/start")
            {
                var users = LoadUsers();
                if (!users.ContainsKey(userId))
                {
                    users[userId] = userName;
                    SaveUsers(users);
                    await botClient.SendMessage(chatId, $"Assalomu alaykum, {userName}! Siz ro'yxatga olindingiz.\n\nMenga istalgan ob'ekt nomini yozing, men sizga 3 ta rasm topib beraman.", cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessage(chatId, $"Assalomu alaykum, {userName}! Siz allaqachon ro'yxatdan o'tgansiz.\n\nMenga istalgan ob'ekt nomini yozing, men sizga 3 ta rasm topib beraman.", cancellationToken: cancellationToken);
                }
                return;
            }

            if (text == "/help")
            {
                await botClient.SendMessage(chatId, "/start - Ro'yxatdan o'tish\n/help - Yordam\nIstalgan ob'ekt nomini yozing - 3 ta rasm topib beraman", cancellationToken: cancellationToken);
                return;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                var users = LoadUsers();
                if (!users.ContainsKey(userId))
                {
                    users[userId] = userName;
                    SaveUsers(users);
                }

                await botClient.SendMessage(chatId, $"🔍 \"{text}\" uchun qidirilmoqda...\n⏳ Iltimos, kuting", cancellationToken: cancellationToken);

                var photoUrls = SearchPhotos(text);
                var downloadedPhotos = new List<string>();

                foreach (var url in photoUrls)
                {
                    var photoPath = await DownloadPhoto(url, text, userId);
                    if (!string.IsNullOrEmpty(photoPath))
                    {
                        downloadedPhotos.Add(photoPath);
                    }
                }

                if (downloadedPhotos.Count > 0)
                {
                    int photoNumber = 1;
                    foreach (var photoPath in downloadedPhotos)
                    {
                        using var fs = File.OpenRead(photoPath);
                        var inputFile = InputFile.FromStream(fs, Path.GetFileName(photoPath));

                        await botClient.SendPhoto(chatId, inputFile, caption: $"🖼️ {photoNumber}/{downloadedPhotos.Count}", cancellationToken: cancellationToken);
                        photoNumber++;
                        await Task.Delay(500, cancellationToken);
                    }

                    await botClient.SendMessage(chatId, $"✅ Hammasi tayyor! {downloadedPhotos.Count} ta rasm topildi va saqlandi.", cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessage(chatId, "❌ Kechirasiz, rasmlarni yuklab olishda xatolik yuz berdi.\n🔄 Qaytadan urinib ko'ring.", cancellationToken: cancellationToken);
                }
            }
        }
        catch
        {
            try
            {
                if (update.Message != null)
                {
                    await botClient.SendMessage(update.Message.Chat.Id, "Botda xatolik yuz berdi. Qaytadan urinib ko'ring.", cancellationToken: cancellationToken);
                }
            }
            catch { }
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}