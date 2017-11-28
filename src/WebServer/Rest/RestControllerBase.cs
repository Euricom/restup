using Restup.HttpMessage;

namespace Restup.WebServer.Rest
{
    public class RestControllerBase
    {
        public IHttpServerRequest Request { get; internal set; }
    }
}
