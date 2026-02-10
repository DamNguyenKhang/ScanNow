using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ScanNow.Domain.Exceptions
{
    public class ConflictException : BaseException
    {
        public ConflictException(string message)
            : base(message, HttpStatusCode.Conflict)
        {
        }

        public ConflictException(string message, Exception innerException)
            : base(message, innerException, HttpStatusCode.Conflict)
        {
        }
    }
}
