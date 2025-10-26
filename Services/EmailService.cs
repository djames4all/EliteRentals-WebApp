using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace EliteRentals.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var host = emailSettings["Host"];
            var port = int.Parse(emailSettings["Port"]);
            var enableSsl = bool.Parse(emailSettings["EnableSSL"]);
            var user = emailSettings["User"];
            var password = emailSettings["Password"];
            var from = emailSettings["From"];

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, password),
                EnableSsl = enableSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(from),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
        }
    }
}
