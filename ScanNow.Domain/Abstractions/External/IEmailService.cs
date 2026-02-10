using System;
using System.Collections.Generic;
using System.Text;

namespace ScanNow.Domain.Abstractions.External
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string htmlBody);
    }
}
