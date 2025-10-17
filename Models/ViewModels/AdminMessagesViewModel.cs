using EliteRentals.Models.DTOs;

namespace EliteRentals.Models.ViewModels
{

    public class AdminMessagesViewModel
    {
        public List<ConversationDto> Conversations { get; set; } = new List<ConversationDto>();
        public List<UserDto> Users { get; set; } = new List<UserDto>();

        public List<MessageDto> InboxMessages { get; set; } = new(); // Received messages
        public List<MessageDto> SentMessages { get; set; } = new();
        public int CurrentUserId { get; set; }
        public string CurrentUserName { get; set; } = string.Empty;

        public bool HasUnreadMessages { get; set; }
    }

    public class ConversationDto
    {
        public int OtherUserId { get; set; }
        public string OtherUserName { get; set; } = string.Empty;
        public string LastMessage { get; set; } = string.Empty;
        public DateTime LastMessageTimestamp { get; set; }
        public bool IsChatbot { get; set; }
        public int UnreadCount { get; set; }
    }

    public class SendMessageRequest
    {
        public int ReceiverId { get; set; }
        public string MessageText { get; set; } = string.Empty;
    }
}
