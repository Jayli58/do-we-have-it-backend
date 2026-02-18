using System.Net;

namespace DoWeHaveItApp.Exceptions
{
    public class BaseException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public BaseException(string message, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
