using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramNotesCollector.Database;
using TelegramNotesCollector.Models;
using TelegramNotesCollector.Services;

namespace TelegramNotesCollector
{
    public class BotService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly AppDbContext _dbContext;
        private readonly TelegramService _telegramService;
        private readonly NoteProcessorService _noteProcessor;
        private readonly ILogger<BotService> _logger;
        private readonly IConfiguration _configuration;
        
        private Dictionary<long, UserState> _userStates = new();
        
        private enum UserState
        {
            None,
            AwaitingTwitterUsername,
            AwaitingRedditSubreddit,
            AwaitingYouTubeChannel,
            AwaitingVKSource,
            AwaitingRSSUrl,
            AwaitingKeyword,
            AwaitingCategory,
            AwaitingNoteContent
        }
        
        public BotService(
            ITelegramBotClient botClient,
            AppDbContext dbContext,
            TelegramService telegramService,
            NoteProcessorService noteProcessor,
            ILogger<BotService> logger,
            IConfiguration configuration)
        {
            _botClient = botClient;
            _dbContext = dbContext;
            _telegramService = telegramService;
            _noteProcessor = noteProcessor;
            _logger = logger;
            _configuration = configuration;
        }
        
        public async Task StartAsync()
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                ThrowPendingUpdates = true
            };
            
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions
            );
            
            await _botClient.SetMyCommandsAsync(new[]
            {
                new BotCommand { Command = "start", Description = "Запустить бота" },
                new BotCommand { Command = "help", Description = "Помощь" },
                new BotCommand { Command = "add", Description = "Добавить источник" },
                new BotCommand { Command = "list", Description = "Мои заметки" },
                new BotCommand { Command = "sources", Description = "Мои источники" },
                new BotCommand { Command = "search", Description = "Поиск заметок" },
                new BotCommand { Command = "stats", Description = "Статистика" },
                new BotCommand { Command = "settings", Description = "Настройки" }
            });
            
            _logger.LogInformation("Бот успешно запущен");
        }
        
        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Message is { } message)
                {
                    await HandleMessageAsync(message, cancellationToken);
                }
                else if (update.CallbackQuery is { } callbackQuery)
                {
                    await HandleCallbackQueryAsync(callbackQuery, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке обновления");
            }
        }
        
        private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
        {
            var userId = message.Chat.Id;
            var text = message.Text ?? string.Empty;
            
            // Получаем или создаем настройки пользователя
            var userSettings = await _dbContext.UserSettings
                .FirstOrDefaultAsync(u => u.UserId == userId);
                
            if (userSettings == null)
            {
                userSettings = new UserSettings { UserId = userId };
                _dbContext.UserSettings.Add(userSettings);
                await _dbContext.SaveChangesAsync();
            }
            
            // Проверяем состояние пользователя
            if (_userStates.TryGetValue(userId, out var state) && state != UserState.None)
            {
                await HandleUserStateAsync(userId, text, state, cancellationToken);
                return;
            }
            
            // Обработка команд
            if (text.StartsWith('/'))
            {
                await HandleCommandAsync(userId, text, cancellationToken);
            }
            else if (message.ForwardFromChat != null || message.ForwardFrom != null)
            {
                // Обработка пересланных сообщений
                await HandleForwardedMessageAsync(message, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(message.Text) && message.Text.Length > 10)
            {
                // Создание новой заметки вручную
                await CreateManualNoteAsync(userId, message.Text, cancellationToken);
            }
        }
        
        private async Task HandleCommandAsync(long userId, string command, CancellationToken cancellationToken)
        {
            switch (command.ToLower())
            {
                case "/start":
                    await SendWelcomeMessageAsync(userId, cancellationToken);
                    break;
                    
                case "/help":
                    await SendHelpMessageAsync(userId, cancellationToken);
                    break;
                    
                case "/add":
                    await ShowAddSourceMenuAsync(userId, cancellationToken);
                    break;
                    
                case "/list":
                    await ShowNotesListAsync(userId, 0, cancellationToken);
                    break;
                    
                case "/sources":
                    await ShowUserSourcesAsync(userId, cancellationToken);
                    break;
                    
                case "/search":
                    _userStates[userId] = UserState.AwaitingKeyword;
                    await _botClient.SendTextMessageAsync(
                        userId,
                        "Введите ключевое слово для поиска:",
                        cancellationToken: cancellationToken);
                    break;
                    
                case "/stats":
                    await ShowStatisticsAsync(userId, cancellationToken);
                    break;
                    
                case "/settings":
                    await ShowSettingsMenuAsync(userId, cancellationToken);
                    break;
                    
                default:
                    await _botClient.SendTextMessageAsync(
                        userId,
                        "Неизвестная команда. Используйте /help для списка команд.",
                        cancellationToken: cancellationToken);
                    break;
            }
        }
        
        private async Task SendWelcomeMessageAsync(long userId, CancellationToken cancellationToken)
        {
            var welcomeText = @"🎯 *Notes Collector Bot*

*Возможности:*
• 📱 Сбор заметок из Telegram, Twitter, Reddit, YouTube, VK
• 🔍 Автоматическая категоризация и тегирование
• 📚 Организация по категориям и тегам
• 🔔 Уведомления о новых заметках
• 📊 Статистика и аналитика
• 🔎 Поиск по всем заметкам

*Основные команды:*
/start - Запустить бота
/add - Добавить источник
/list - Показать заметки
/search - Поиск по заметкам
/settings - Настройки
/help - Помощь

*Как добавить источник:*
1. Используйте команду /add
2. Выберите платформу
3. Введите username/URL
4. Настройте фильтры

*Примеры:*
• Twitter: @username
• Reddit: r/subreddit
• YouTube: URL канала
• VK: URL группы или пользователя";

            await _botClient.SendTextMessageAsync(
                userId,
                welcomeText,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
        
        private async Task ShowAddSourceMenuAsync(long userId, CancellationToken cancellationToken)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🐦 Twitter", "add_twitter"),
                    InlineKeyboardButton.WithCallbackData("👾 Reddit", "add_reddit")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📺 YouTube", "add_youtube"),
                    InlineKeyboardButton.WithCallbackData("👥 VK", "add_vk")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🌐 RSS/Сайт", "add_rss"),
                    InlineKeyboardButton.WithCallbackData("📱 Telegram", "add_telegram")
                }
            });
            
            await _botClient.SendTextMessageAsync(
                userId,
                "Выберите платформу для добавления:",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        
        private async Task ShowNotesListAsync(long userId, int page, CancellationToken cancellationToken)
        {
            var pageSize = 5;
            var notes = await _dbContext.Notes
                .Where(n => n.UserId == userId && n.Status == NoteStatus.New)
                .OrderByDescending(n => n.CreatedAt)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();
                
            var totalNotes = await _dbContext.Notes
                .CountAsync(n => n.UserId == userId && n.Status == NoteStatus.New);
                
            if (notes.Count == 0)
            {
                await _botClient.SendTextMessageAsync(
                    userId,
                    "У вас пока нет заметок. Добавьте источники с помощью команды /add",
                    cancellationToken: cancellationToken);
                return;
            }
            
            var sb = new StringBuilder();
            sb.AppendLine($"📚 *Ваши заметки ({page * pageSize + 1}-{page * pageSize + notes.Count} из {totalNotes})*\n");
            
            foreach (var note in notes)
            {
                var platformIcon = GetPlatformIcon(note.Platform);
                var preview = note.Content.Length > 100 
                    ? note.Content.Substring(0, 100) + "..." 
                    : note.Content;
                    
                sb.AppendLine($"{platformIcon} *{note.Title}*");
                sb.AppendLine($"📅 {note.CreatedAt:dd.MM.yyyy HH:mm}");
                sb.AppendLine($"🔗 [Открыть]({note.Url})");
                sb.AppendLine($"📝 {preview}");
                sb.AppendLine();
            }
            
            var keyboard = new List<InlineKeyboardButton[]>();
            if (page > 0)
            {
                keyboard.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"list_page_{page - 1}")
                });
            }
            
            if ((page + 1) * pageSize < totalNotes)
            {
                var buttons = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("Вперед ➡️", $"list_page_{page + 1}")
                };
                
                if (page > 0)
                {
                    keyboard[keyboard.Count - 1] = new[]
                    {
                        InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"list_page_{page - 1}"),
                        InlineKeyboardButton.WithCallbackData("Вперед ➡️", $"list_page_{page + 1}")
                    };
                }
                else
                {
                    keyboard.Add(buttons.ToArray());
                }
            }
            
            var replyMarkup = keyboard.Count > 0 ? new InlineKeyboardMarkup(keyboard) : null;
            
            await _botClient.SendTextMessageAsync(
                userId,
                sb.ToString(),
                parseMode: ParseMode.Markdown,
                disableWebPagePreview: true,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        
        private string GetPlatformIcon(SocialPlatform platform)
        {
            return platform switch
            {
                SocialPlatform.Twitter => "🐦",
                SocialPlatform.Reddit => "👾",
                SocialPlatform.YouTube => "📺",
                SocialPlatform.VK => "👥",
                SocialPlatform.Telegram => "📱",
                SocialPlatform.Web => "🌐",
                SocialPlatform.RSS => "📡",
                _ => "📄"
            };
        }
        
        private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var userId = callbackQuery.Message.Chat.Id;
            var data = callbackQuery.Data;
            
            if (data.StartsWith("add_"))
            {
                var platform = data.Replace("add_", "");
                await HandleAddSourceCallback(userId, platform, cancellationToken);
            }
            else if (data.StartsWith("list_page_"))
            {
                var pageStr = data.Replace("list_page_", "");
                if (int.TryParse(pageStr, out int page))
                {
                    await ShowNotesListAsync(userId, page, cancellationToken);
                }
            }
            
            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        }
        
        private async Task HandleAddSourceCallback(long userId, string platform, CancellationToken cancellationToken)
        {
            var state = platform switch
            {
                "twitter" => UserState.AwaitingTwitterUsername,
                "reddit" => UserState.AwaitingRedditSubreddit,
                "youtube" => UserState.AwaitingYouTubeChannel,
                "vk" => UserState.AwaitingVKSource,
                "rss" => UserState.AwaitingRSSUrl,
                "telegram" => UserState.AwaitingTelegramSource,
                _ => UserState.None
            };
            
            if (state != UserState.None)
            {
                _userStates[userId] = state;
                
                var message = state switch
                {
                    UserState.AwaitingTwitterUsername => "Введите Twitter username (например, @elonmusk):",
                    UserState.AwaitingRedditSubreddit => "Введите название subreddit (например, programming):",
                    UserState.AwaitingYouTubeChannel => "Введите URL YouTube канала:",
                    UserState.AwaitingVKSource => "Введите URL VK группы или пользователя:",
                    UserState.AwaitingRSSUrl => "Введите RSS feed URL:",
                    UserState.AwaitingTelegramSource => "Перешлите сообщение из канала/чата или введите @username:",
                    _ => "Введите источник:"
                };
                
                await _botClient.SendTextMessageAsync(
                    userId,
                    message,
                    cancellationToken: cancellationToken);
            }
        }
        
        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException 
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            
            _logger.LogError(exception, "Ошибка polling");
            return Task.CompletedTask;
        }
        
        // ... остальные методы обработки
    }
}
