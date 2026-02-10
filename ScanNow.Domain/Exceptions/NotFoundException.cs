using System.Net;

namespace ScanNow.Domain.Exceptions
{
    public class NotFoundException : BaseException
    {
        public NotFoundException(string entityName, object entityId)
            : base($"{entityName} with ID '{entityId}' was not found.", HttpStatusCode.NotFound)
        {
        }

        public NotFoundException(string message)
            : base(message, HttpStatusCode.NotFound)
        {
        }
    }
}
