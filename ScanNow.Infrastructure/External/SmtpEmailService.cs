using MailKit.Security;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using ScanNow.Domain.Abstractions.External;

namespace ScanNow.Infrastructure.External
{
    public class SmtpEmailService : IEmailService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _senderName;
        private readonly string _senderEmail;
        private readonly string _username;
        private readonly string _password;

        public SmtpEmailService(IConfiguration configuration)
        {
            var emailSection = configuration.GetSection("Email");

            _host = emailSection["Host"]!;
            _port = int.Parse(emailSection["Port"]!);
            _senderName = emailSection["SenderName"]!;
            _senderEmail = emailSection["SenderEmail"]!;
            _username = emailSection["Username"]!;
            _password = emailSection["Password"]!;
        }
        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_senderName, _senderEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody
            }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_username, _password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

    }
}
