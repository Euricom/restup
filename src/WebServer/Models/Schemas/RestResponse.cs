using System.Collections.Generic;
using System.Collections.Immutable;
using Restup.HttpMessage.Models.Schemas;
using Restup.Webserver.Models.Contracts;

namespace Restup.Webserver.Models.Schemas
{
    public class RestResponse : IRestResponse
    {
        public int StatusCode { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }

        public RestResponse(int statusCode, IReadOnlyDictionary<string, string> headers)
        {
            StatusCode = statusCode;
            Headers = headers;
        }

        public RestResponse(HttpResponseStatus statusCode): this((int) statusCode, ImmutableDictionary<string, string>.Empty)
        { }

        public RestResponse(HttpResponseStatus statusCode, IReadOnlyDictionary<string, string> headers) : this((int)statusCode, headers)
        { }
    }
}