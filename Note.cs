using System;
using System.ComponentModel.DataAnnotations;

namespace TelegramNotesCollector.Models
{
    public enum SocialPlatform
    {
        Telegram,
        Twitter,
        Reddit,
        YouTube,
        VK,
        Web,
        RSS
    }
    
    public enum NoteStatus
    {
        New,
        Archived,
        Deleted,
        Processed
    }
    
    public class Note
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public long UserId { get; set; }
        
        [Required]
        public SocialPlatform Platform { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string Title { get; set; }
        
        [MaxLength(5000)]
        public string Content { get; set; }
        
        [MaxLength(500)]
        public string Url { get; set; }
        
        [MaxLength(500)]
        public string SourceId { get; set; } // ID поста/твита/видео
        
        [MaxLength(200)]
        public string Author { get; set; }
        
        public DateTime? PublishedAt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public NoteStatus Status { get; set; } = NoteStatus.New;
        
        [MaxLength(50)]
        public string Category { get; set; }
        
        public List<string> Tags { get; set; } = new List<string>();
        
        public bool HasMedia { get; set; }
        
        [MaxLength(500)]
        public string MediaUrl { get; set; }
        
        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }
        public int ViewsCount { get; set; }
        
        [MaxLength(1000)]
        public string RawData { get; set; } // JSON с оригинальными данными
    }
    
    public class UserSettings
    {
        [Key]
        public long UserId { get; set; }
        
        [MaxLength(500)]
        public string TimeZone { get; set; } = "UTC";
        
        public int ItemsPerPage { get; set; } = 10;
        
        public bool AutoCategorize { get; set; } = true;
        
        public bool SendNotifications { get; set; } = true;
        
        public Dictionary<SocialPlatform, bool> EnabledPlatforms { get; set; } = new()
        {
            { SocialPlatform.Telegram, true },
            { SocialPlatform.Twitter, false },
            { SocialPlatform.Reddit, false },
            { SocialPlatform.YouTube, false },
            { SocialPlatform.VK, false },
            { SocialPlatform.Web, false },
            { SocialPlatform.RSS, false }
        };
        
        public List<string> Keywords { get; set; } = new List<string>();
        public List<string> BlockedSources { get; set; } = new List<string>();
        
        [MaxLength(100)]
        public string Language { get; set; } = "ru";
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    
    public class Subscription
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public long UserId { get; set; }
        
        [Required]
        public SocialPlatform Platform { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string SourceIdentifier { get; set; } // @username, subreddit, channel ID
        
        [MaxLength(100)]
        public string DisplayName { get; set; }
        
        public DateTime LastFetched { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public int FetchIntervalMinutes { get; set; } = 60;
        
        public List<string> Filters { get; set; } = new List<string>();
    }
}
