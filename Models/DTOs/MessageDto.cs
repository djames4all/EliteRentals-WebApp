using System;

namespace EliteRentals.Models.DTOs
{
    public class MessageDto
    {
        public int MessageId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = "";
        public int? ReceiverId { get; set; }
        public string ReceiverName { get; set; } = "";
        public string MessageText { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsChatbot { get; set; } = false;
        public bool IsBroadcast { get; set; }
        public string? TargetRole { get; set; }

        public bool IsRead { get; set; }

        public DateTime? ArchivedDate { get; set; }

    }
}
