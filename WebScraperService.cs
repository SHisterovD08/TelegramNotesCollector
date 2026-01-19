using HtmlAgilityPack;
using TelegramNotesCollector.Models;

namespace TelegramNotesCollector.Services
{
    public class WebScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WebScraperService> _logger;
        
        public WebScraperService(
            HttpClient httpClient,
            ILogger<WebScraperService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }
        
        public async Task<Note> ScrapeArticleAsync(string url)
        {
            try
            {
                var html = await _httpClient.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                
                // Пытаемся извлечь заголовок
                var title = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", null)
                          ?? doc.DocumentNode.SelectSingleNode("//title")?.InnerText
                          ?? "Без названия";
                
                // Пытаемся извлечь описание
                var description = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']")?.GetAttributeValue("content", null)
                                ?? doc.DocumentNode.SelectSingleNode("//meta[@name='description']")?.GetAttributeValue("content", null);
                
                // Извлекаем основной контент (упрощенная версия)
                var content = ExtractMainContent(doc);
                
                return new Note
                {
                    Platform = SocialPlatform.Web,
                    Title = CleanText(title),
                    Content = CleanText(content ?? description ?? "Контент недоступен"),
                    Url = url,
                    SourceId = GenerateSourceId(url),
                    PublishedAt = ExtractDate(doc),
                    Author = ExtractAuthor(doc),
                    HasMedia = doc.DocumentNode.SelectNodes("//img")?.Any() == true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при скрейпинге {Url}", url);
                return null;
            }
        }
        
        private string ExtractMainContent(HtmlDocument doc)
        {
            // Эвристики для поиска основного контента
            var selectors = new[]
            {
                "//article",
                "//div[contains(@class, 'content')]",
                "//div[contains(@class, 'article')]",
                "//div[contains(@class, 'post')]",
                "//main",
                "//div[@role='main']"
            };
            
            foreach (var selector in selectors)
            {
                var node = doc.DocumentNode.SelectSingleNode(selector);
                if (node != null && node.InnerText.Length > 200)
                {
                    return node.InnerText;
                }
            }
            
            return doc.DocumentNode.InnerText;
        }
        
        private DateTime? ExtractDate(HtmlDocument doc)
        {
            var dateSelectors = new[]
            {
                "//meta[@property='article:published_time']",
                "//meta[@name='publish_date']",
                "//time",
                "//span[contains(@class, 'date')]"
            };
            
            foreach (var selector in dateSelectors)
            {
                var node = doc.DocumentNode.SelectSingleNode(selector);
                if (node != null)
                {
                    var dateStr = node.GetAttributeValue("content", node.GetAttributeValue("datetime", node.InnerText));
                    if (DateTime.TryParse(dateStr, out var date))
                        return date;
                }
            }
            
            return null;
        }
        
        private string ExtractAuthor(HtmlDocument doc)
        {
            var authorSelectors = new[]
            {
                "//meta[@name='author']",
                "//a[contains(@rel, 'author')]",
                "//span[contains(@class, 'author')]"
            };
            
            foreach (var selector in authorSelectors)
            {
                var node = doc.DocumentNode.SelectSingleNode(selector);
                if (node != null)
                {
                    return CleanText(node.GetAttributeValue("content", node.InnerText));
                }
            }
            
            return null;
        }
        
        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            return System.Net.WebUtility.HtmlDecode(text)
                .Replace("\n", " ")
                .Replace("\t", " ")
                .Trim();
        }
        
        private string GenerateSourceId(string url)
        {
            var uri = new Uri(url);
            return $"{uri.Host}_{uri.AbsolutePath.GetHashCode()}";
        }
    }
}
