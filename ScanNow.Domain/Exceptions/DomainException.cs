using System.Net;

namespace ScanNow.Domain.Exceptions
{
    public class DomainException : BaseException
    {
        public DomainException(string message)
            : base(message, HttpStatusCode.BadRequest)
        {
        }

        public DomainException(string message, HttpStatusCode statusCode)
            : base(message, statusCode)
        {
        }

        public DomainException(string message, Exception innerException)
            : base(message, innerException, HttpStatusCode.BadRequest)
        {
        }
    }
}
