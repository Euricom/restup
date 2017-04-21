﻿using Restup.Webserver.Models.Contracts;
using Restup.Webserver.Models.Schemas;
using Restup.WebServer.Models.Schemas;
using System;

namespace Restup.Webserver.Rest
{
    internal class RestResponseFactory
    {
        private readonly BadRequestResponse _badRequestResponse;

        internal RestResponseFactory()
        {
            _badRequestResponse = new BadRequestResponse();
        }

        internal IRestResponse CreateBadRequest()
        {
            return _badRequestResponse;
        }

		internal IRestResponse CreateWwwAuthenticate(string Realm)
		{
			return new WwwAuthenticateResponse(Realm);
		}

		internal IRestResponse CreateInternalServerError(Exception ex)
		{
			return new InternalServerErrorResponse(ex);
		}
    }
}
