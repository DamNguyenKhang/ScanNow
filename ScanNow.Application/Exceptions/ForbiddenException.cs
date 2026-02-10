using ScanNow.Domain.Exceptions;
using System.Net;

namespace ScanNow.Application.Exceptions
{
    public class ForbiddenException : BaseException
    {
        public ForbiddenException(string message = "You don't have permission to perform this action.")
            : base(message, HttpStatusCode.Forbidden)
        {
        }

        public ForbiddenException(string message, Exception innerException)
            : base(message, innerException, HttpStatusCode.Forbidden)
        {
        }
    }
}
