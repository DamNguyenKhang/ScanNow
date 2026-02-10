using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ScanNow.Domain.Exceptions
{
    public class ValidationException : BaseException
    {
        public Dictionary<string, string[]>? Errors { get; }

        /// <summary>
        /// Single validation error
        /// </summary>
        public ValidationException(string propertyName, string errorMessage)
            : base($"Validation failed for '{propertyName}': {errorMessage}", HttpStatusCode.BadRequest)
        {
            Errors = new Dictionary<string, string[]>
            {
                [propertyName] = new[] { errorMessage }
            };
        }

        /// <summary>
        /// Multiple validation errors
        /// </summary>
        public ValidationException(Dictionary<string, string[]> errors)
            : base("One or more validation errors occurred.", HttpStatusCode.BadRequest)
        {
            Errors = errors;
        }

        /// <summary>
        /// Simple message validation error
        /// </summary>
        public ValidationException(string message)
            : base(message, HttpStatusCode.BadRequest)
        {
            Errors = null;
        }
    }
}
