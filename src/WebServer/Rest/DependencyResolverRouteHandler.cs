using System;
using System.Threading.Tasks;
using Restup.HttpMessage;
using Restup.Webserver.Models.Contracts;
using Restup.WebServer.Models.Contracts;

namespace Restup.Webserver.Rest
{
    public class DependencyResolverRouteHandler : IRouteHandler
    {
        private readonly DependencyResolverRestControllerRequestHandler _requestHandler;
        private readonly RestToHttpResponseConverter _restToHttpConverter;
        private readonly RestServerRequestFactory _restServerRequestFactory;
        private readonly IAuthorizationProvider _authenticationProvider;

        public DependencyResolverRouteHandler(Func<Type, object> resolve)
        {
            _restServerRequestFactory = new RestServerRequestFactory();
            _requestHandler = new DependencyResolverRestControllerRequestHandler(resolve);
            _restToHttpConverter = new RestToHttpResponseConverter();
        }

        public DependencyResolverRouteHandler(Func<Type, object> resolve, IAuthorizationProvider authenticationProvider) : this(resolve)
        {
            _authenticationProvider = authenticationProvider;
        }

        public async Task<HttpServerResponse> HandleRequest(IHttpServerRequest request)
        {
            var restServerRequest = _restServerRequestFactory.Create(request);

            var restResponse = await _requestHandler.HandleRequestAsync(restServerRequest, _authenticationProvider);

            var httpResponse = _restToHttpConverter.ConvertToHttpResponse(restResponse, restServerRequest);

            return httpResponse;
        }

        public void RegisterController(Type type)
        {
            _requestHandler.RegisterController(type);
        }
    }
}