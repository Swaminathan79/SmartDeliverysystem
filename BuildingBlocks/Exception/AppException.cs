using System;

namespace BuildingBlocks.Exception
{
    public class AppException : System.Exception
    {
        public int StatusCode { get; }

        public AppException(string message, int statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}