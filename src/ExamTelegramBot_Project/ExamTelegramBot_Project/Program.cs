using ExamTelegramBot_Project;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ExamCode
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Bot token: tavsiya qilinadi—environment o'zgaruvchisidan o'qish
            string botToken = Environment.GetEnvironmentVariable("8492786743:AAF_lYp3bL2TxwEtXP2zMug2chhWFIwowbI") ?? "8492786743:AAF_lYp3bL2TxwEtXP2zMug2chhWFIwowbI";

            string usersFile = Path.Combine(AppContext.BaseDirectory, "users.json");
            string photosDirectory = Path.Combine(AppContext.BaseDirectory, "downloaded_photos");

            if (!Directory.Exists(photosDirectory)) Directory.CreateDirectory(photosDirectory);
            if (!File.Exists(usersFile)) File.WriteAllText(usersFile, "{}");

            var botClient = new TelegramBotClient(botToken);

            Dictionary<string, string> LoadUsers()
            {
                try
                {
                    var json = File.ReadAllText(usersFile);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
                catch
                {
                    return new Dictionary<string, string>();
                }
            }

            void SaveUsers(Dictionary<string, string> users)
            {
                var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(usersFile, json);
            }

            // SearchPhotos: Unsplash Source (no API key). Hamma rasmga alohida so'rov yuborib, redirect qilingan final URL olamiz.
            async Task<List<string>> SearchPhotos(string query)
            {
                var urls = new List<string>();

                for (int i = 0; i < 3; i++)
                {
                    var randomSeed = new Random().Next(1000, 9999);
                    urls.Add($"https://picsum.photos/600/400?random={randomSeed}");
                }

                return urls;
            }

            async Task<string> DownloadPhoto(string photoUrl, string query)
            {
                using var httpClient = new HttpClient();
                try
                {
                    var response = await httpClient.GetAsync(photoUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var safeQuery = string.Concat(query.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))).Replace(' ', '_');
                        var fileName = $"{safeQuery}_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.jpg";
                        var filePath = Path.Combine(photosDirectory, fileName);

                        using var stream = await response.Content.ReadAsStreamAsync();
                        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await stream.CopyToAsync(fileStream);

                        return filePath;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DownloadPhoto xato: {ex.Message}");
                }
                return string.Empty;
            }

            async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
            {
                try
                {
                    if (update.Type != UpdateType.Message) return;
                    var message = update.Message;
                    if (message?.Type != MessageType.Text) return;

                    var userId = message.From?.Id.ToString() ?? "";
                    var userName = message.From?.Username ?? message.From?.FirstName ?? "Unknown";
                    var text = message.Text?.Trim() ?? "";

                    if (text == "/start")
                    {
                        var users = LoadUsers();
                        if (!users.ContainsKey(userId))
                        {
                            users[userId] = userName;
                            SaveUsers(users);
                        }

                        await client.SendMessage(message.Chat.Id, $"Assalomu alaykum, {userName}! Siz ro'yxatga olindingiz.", cancellationToken: ct);
                        return;
                    }

                    if (text == "/help")
                    {
                        await client.SendMessage(message.Chat.Id, "Istalgan so'zni yuboring — men unga oid 3 ta rasm topib yuboraman va lokalga saqlayman.", cancellationToken: ct);
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

                        await client.SendMessage(message.Chat.Id, $"Qidirilmoqda: \"{text}\" — iltimos kuting...", cancellationToken: ct);

                        var photoUrls = await SearchPhotos(text);
                        if (photoUrls.Count == 0)
                        {
                            await client.SendMessage(message.Chat.Id, $"\"{text}\" uchun rasm topilmadi.", cancellationToken: ct);
                            return;
                        }

                        var downloadedPhotos = new List<string>();
                        foreach (var url in photoUrls.Take(3))
                        {
                            try
                            {
                                var photoPath = await DownloadPhoto(url, text);
                                if (!string.IsNullOrEmpty(photoPath))
                                {
                                    downloadedPhotos.Add(photoPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Saqlashda xato ({url}): {ex.Message}");
                            }
                        }

                        if (downloadedPhotos.Count > 0)
                        {
                            foreach (var photoPath in downloadedPhotos)
                            {
                                try
                                {
                                    using var fs = File.OpenRead(photoPath);
                                    var input = new InputOnlineFile(fs, Path.GetFileName(photoPath));
                                    await client.SendPhoto(message.Chat.Id, input, cancellationToken: ct);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Rasm yuborishda xato: {ex.Message}");
                                }
                            }

                            await client.SendMessage(message.Chat.Id, $"Topilgan va saqlangan rasmlar: {downloadedPhotos.Count}\nPapka: {photosDirectory}", cancellationToken: ct);
                        }
                        else
                        {
                            await client.SendMessage(message.Chat.Id, "Rasmlar saqlanmadi.", cancellationToken: ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"HandleUpdateAsync umumiy xato: {ex.Message}");
                }
            }

            async Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken ct)
            {
                Console.WriteLine($"Bot xatosi: {exception.Message}");
            }

            using (var cts = new CancellationTokenSource())
            {
                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = new[] { UpdateType.Message }
                };

                botClient.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    errorHandler: HandleErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: cts.Token);

                Console.WriteLine("Bot ishga tushdi. To'xtatish uchun Enter bosing...");
                Console.ReadLine();
                cts.Cancel();
            }
        }
    }
}