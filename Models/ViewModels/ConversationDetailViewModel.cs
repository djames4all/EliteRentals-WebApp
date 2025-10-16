using EliteRentals.Models.DTOs;

namespace EliteRentals.Models.ViewModels
{
    public class ConversationDetailViewModel
    {
        public int OtherUserId { get; set; }
        public string OtherUserName { get; set; } = string.Empty;
        public List<MessageDto> Messages { get; set; } = new List<MessageDto>();
        public int CurrentUserId { get; set; }
    }
}
