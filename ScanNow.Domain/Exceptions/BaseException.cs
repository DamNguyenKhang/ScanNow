using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ScanNow.Domain.Exceptions
{
    public class BaseException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        protected BaseException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest) : base(message)
        {
            StatusCode = statusCode;
        }

        protected BaseException(string message, Exception innerException, HttpStatusCode statusCode = HttpStatusCode.BadRequest) : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}
