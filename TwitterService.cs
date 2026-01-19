using System.Text.Json;
using TwitterNotesCollector.Models;

namespace TelegramNotesCollector.Services
{
    public class TwitterService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TwitterService> _logger;
        
        public TwitterService(
            HttpClient httpClient, 
            IConfiguration configuration,
            ILogger<TwitterService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            var bearerToken = _configuration["Twitter: BearerToken"];
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        }
        
        public async Task<List<Note>> FetchTweetsAsync(string username, DateTime since)
        {
            try
            {
                // Получаем ID пользователя по username
                var userId = await GetUserIdByUsernameAsync(username);
                if (string.IsNullOrEmpty(userId))
                    return new List<Note>();
                
                // Получаем твиты
                var url = $"https://api.twitter.com/2/users/{userId}/tweets" +
                         $"?max_results=50" +
                         $"&start_time={since:yyyy-MM-ddTHH:mm:ssZ}" +
                         $"&tweet.fields=created_at,public_metrics,entities" +
                         $"&expansions=author_id" +
                         $"&user.fields=username,name";
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var tweets = JsonSerializer.Deserialize<TwitterResponse>(json);
                
                return tweets?.Data?.Select(tweet => new Note
                {
                    Platform = SocialPlatform.Twitter,
                    Title = $"Твит от @{username}",
                    Content = tweet.Text,
                    Url = $"https://twitter.com/{username}/status/{tweet.Id}",
                    SourceId = tweet.Id,
                    Author = username,
                    PublishedAt = tweet.CreatedAt,
                    LikesCount = tweet.PublicMetrics?.LikeCount ?? 0,
                    CommentsCount = tweet.PublicMetrics?.ReplyCount ?? 0,
                    HasMedia = tweet.Entities?.Urls?.Any() == true || 
                              tweet.Attachments?.MediaKeys?.Any() == true,
                    Tags = ExtractHashtags(tweet.Text)
                }).ToList() ?? new List<Note>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении твитов для {Username}", username);
                return new List<Note>();
            }
        }
        
        private async Task<string> GetUserIdByUsernameAsync(string username)
        {
            var cleanUsername = username.Replace("@", "");
            var url = $"https://api.twitter.com/2/users/by/username/{cleanUsername}";
            
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;
                
            var json = await response.Content.ReadAsStringAsync();
            var userData = JsonSerializer.Deserialize<TwitterUserResponse>(json);
            
            return userData?.Data?.Id;
        }
        
        private List<string> ExtractHashtags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();
                
            var hashtags = new List<string>();
            var words = text.Split(' ');
            
            foreach (var word in words)
            {
                if (word.StartsWith("#") && word.Length > 1)
                {
                    hashtags.Add(word.Trim('#'));
                }
            }
            
            return hashtags;
        }
        
        private class TwitterResponse
        {
            public List<TweetData> Data { get; set; }
        }
        
        private class TweetData
        {
            public string Id { get; set; }
            public string Text { get; set; }
            public DateTime CreatedAt { get; set; }
            public PublicMetrics PublicMetrics { get; set; }
            public Entities Entities { get; set; }
            public Attachments Attachments { get; set; }
        }
        
        private class PublicMetrics
        {
            public int LikeCount { get; set; }
            public int ReplyCount { get; set; }
            public int RetweetCount { get; set; }
        }
        
        private class Entities
        {
            public List<UrlEntity> Urls { get; set; }
            public List<HashtagEntity> Hashtags { get; set; }
        }
        
        private class UrlEntity
        {
            public string Url { get; set; }
            public string ExpandedUrl { get; set; }
        }
        
        private class HashtagEntity
        {
            public string Tag { get; set; }
        }
        
        private class Attachments
        {
            public List<string> MediaKeys { get; set; }
        }
        
        private class TwitterUserResponse
        {
            public UserData Data { get; set; }
        }
        
        private class UserData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Username { get; set; }
        }
    }
}
