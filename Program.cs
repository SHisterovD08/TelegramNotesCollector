using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using TelegramNotesCollector.Database;
using TelegramNotesCollector.Services;

namespace TelegramNotesCollector
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            
            using (var scope = host.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.MigrateAsync();
            }
            
            var botService = host.Services.GetRequiredService<BotService>();
            await botService.StartAsync();
            
            Console.WriteLine("Бот запущен! Нажмите Ctrl+C для остановки.");
            
            // Ожидаем остановки
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };
            
            await Task.Delay(-1, cancellationTokenSource.Token);
        }
        
        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    // Конфигурация
                    var configuration = context.Configuration;
                    services.AddSingleton(configuration);
                    
                    // База данных
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));
                    
                    // Telegram Bot Client
                    services.AddSingleton<ITelegramBotClient>(provider =>
                        new TelegramBotClient(configuration["TelegramBot:Token"]));
                    
                    // Сервисы
                    services.AddSingleton<BotService>();
                    services.AddSingleton<TelegramService>();
                    services.AddSingleton<TwitterService>();
                    services.AddSingleton<RedditService>();
                    services.AddSingleton<YouTubeService>();
                    services.AddSingleton<VKService>();
                    services.AddSingleton<WebScraperService>();
                    services.AddSingleton<NoteProcessorService>();
                    
                    // Hosted Service
                    services.AddHostedService<BackgroundFetchService>();
                });
    }
}
