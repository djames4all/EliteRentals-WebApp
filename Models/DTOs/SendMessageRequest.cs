namespace EliteRentals.Models.DTOs
{
    public class SendMessageRequest
    {
        public int ReceiverId { get; set; }
        public string MessageText { get; set; } = string.Empty;
    }
}
