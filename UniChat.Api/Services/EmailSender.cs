using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace UniChat.Api.Services;

public sealed class SmtpOptions
{
    public string Host { get; init; } = default!;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;

    public string UserName { get; init; } = default!;
    public string Password { get; init; } = default!;

    public string FromEmail { get; init; } = default!;
    public string FromName { get; init; } = "UniChat";
}

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _opt;
    public SmtpEmailSender(IOptions<SmtpOptions> opt) => _opt = opt.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(_opt.FromEmail, _opt.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        msg.To.Add(toEmail);

        using var client = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl = _opt.EnableSsl,
            Credentials = new NetworkCredential(_opt.UserName, _opt.Password)
        };

        
        await client.SendMailAsync(msg);
    }
}
