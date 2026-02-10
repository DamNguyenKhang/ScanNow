using System.Net;

namespace ScanNow.Domain.Exceptions
{
    public class BusinessRuleException : BaseException
    {
        public BusinessRuleException(string message)
            : base(message, HttpStatusCode.UnprocessableEntity)
        {
        }

        public BusinessRuleException(string message, Exception innerException)
            : base(message, innerException, HttpStatusCode.UnprocessableEntity)
        {
        }
    }
}
