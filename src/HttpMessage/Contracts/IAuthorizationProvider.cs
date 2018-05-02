using Restup.HttpMessage;
using Restup.HttpMessage.Models.Schemas;
using Restup.WebServer.Attributes;

namespace Restup.WebServer.Models.Contracts
{
	public interface IAuthorizationProvider
	{
		string Realm { get; }
	    HttpResponseStatus Authorize(IHttpServerRequest request, AuthorizeAttribute attribute = null);
	}
}
