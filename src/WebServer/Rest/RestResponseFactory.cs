using Restup.Webserver.Models.Contracts;
using Restup.Webserver.Models.Schemas;
using Restup.WebServer.Models.Schemas;
using System;
using Restup.HttpMessage;

namespace Restup.Webserver.Rest
{
    public interface IExceptionHandler
    {
        IRestResponse Handle(IHttpServerRequest request, Exception exception);
    }

    internal class RestResponseFactory
    {
        private readonly IExceptionHandler _handler;

        internal RestResponseFactory()
        { }

        internal RestResponseFactory(IExceptionHandler handler)
        {
            _handler = handler;
        }

        internal IRestResponse CreateBadRequest()
        {
            return new BadRequestResponse();
        }

		internal IRestResponse CreateWwwAuthenticate(string realm)
		{
			return new WwwAuthenticateResponse(realm);
		}

		internal IRestResponse CreateExceptionResponse(IHttpServerRequest request, Exception ex)
		{
		    IRestResponse response = null;
		    if (_handler != null)
		    {
		        response = _handler.Handle(request, ex);
		    }

			return response ?? new InternalServerErrorResponse(ex);
		}
    }
}
