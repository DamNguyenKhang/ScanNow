using System.Net;

namespace ScanNow.Domain.Exceptions
{
    public class ExternalServiceException : BaseException
    {
        public string ServiceName { get; }

        public ExternalServiceException(string serviceName, string message)
            : base($"{serviceName} service error: {message}", HttpStatusCode.BadGateway)
        {
            ServiceName = serviceName;
        }

        public ExternalServiceException(string serviceName, string message, Exception innerException)
            : base($"{serviceName} service error: {message}", innerException, HttpStatusCode.BadGateway)
        {
            ServiceName = serviceName;
        }
    }
}
