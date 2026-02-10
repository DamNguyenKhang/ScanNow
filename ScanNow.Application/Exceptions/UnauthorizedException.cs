using ScanNow.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ScanNow.Application.Exceptions
{
    public class UnauthorizedException : BaseException
    {
        public UnauthorizedException(string message = "Unauthenticated.")
            : base(message, HttpStatusCode.Unauthorized)
        {
        }

        public UnauthorizedException(string message, Exception innerException)
            : base(message, innerException, HttpStatusCode.Unauthorized)
        {
        }
    }
}
