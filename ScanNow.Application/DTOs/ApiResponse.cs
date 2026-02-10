using Microsoft.AspNetCore.Http;

namespace ScanNow.Application.DTOs
{
    public class ApiResponse
    {
        public int Code { get; set; } = 200;
        public string Message { get; set; } = string.Empty;

        /* ---------- SUCCESS ---------- */
        public static ApiResponse Success(
            string message = "Success",
            int code = StatusCodes.Status200OK)
        {
            return new ApiResponse
            {
                Code = code,
                Message = message
            };
        }

        /* ---------- FAILURE ---------- */
        public static ApiResponse Failure(
            string message,
            int code = StatusCodes.Status400BadRequest)
        {
            return new ApiResponse
            {
                Code = code,
                Message = message
            };
        }
    }

    public class ApiResponse<T> : ApiResponse
    {
        public T? Result { get; set; }
    }

}
