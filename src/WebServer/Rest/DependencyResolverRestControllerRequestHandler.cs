using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Foundation;
using Restup.HttpMessage.Models.Schemas;
using Restup.Webserver.Attributes;
using Restup.Webserver.Models.Contracts;
using Restup.Webserver.Models.Schemas;
using Restup.WebServer.Attributes;
using Restup.WebServer.Logging;
using Restup.WebServer.Models.Contracts;

namespace Restup.Webserver.Rest
{
    internal class DependencyResolverRestControllerRequestHandler
    {
        private readonly Func<Type, object> _resolve;
        private ImmutableArray<RestControllerMethodInfo> _restMethodCollection;
        private readonly RestResponseFactory _responseFactory;
        private readonly RestControllerMethodExecutorFactory _methodExecuteFactory;
        private readonly UriParser _uriParser;
        private readonly ILogger _log = LogManager.GetLogger<RestControllerRequestHandler>();
        private readonly RestControllerMethodInfoValidator _restControllerMethodInfoValidator;

        internal DependencyResolverRestControllerRequestHandler(Func<Type, object> resolve)
        {
            _resolve = resolve;
            _restMethodCollection = ImmutableArray<RestControllerMethodInfo>.Empty;
            _responseFactory = new RestResponseFactory();
            _methodExecuteFactory = new RestControllerMethodExecutorFactory();
            _uriParser = new UriParser();
            _restControllerMethodInfoValidator = new RestControllerMethodInfoValidator();
        }

        internal Task<IRestResponse> HandleRequestAsync(RestServerRequest req)
        {
            return HandleRequestAsync(req, null);
        }

        internal async Task<IRestResponse> HandleRequestAsync(RestServerRequest req, IAuthorizationProvider authorizationProvider)
        {
            if (!req.HttpServerRequest.IsComplete ||
                req.HttpServerRequest.Method == HttpMethod.Unsupported)
            {
                return _responseFactory.CreateBadRequest();
            }

            var incomingUriAsString = req.HttpServerRequest.Uri.ToRelativeString();
            if (!_uriParser.TryParse(incomingUriAsString, out var parsedUri))
            {
                throw new Exception($"Could not parse uri: {incomingUriAsString}");
            }

            var restMethods = _restMethodCollection.Where(r => r.Match(parsedUri)).ToList();
            if (!restMethods.Any())
            {
                return _responseFactory.CreateBadRequest();
            }

            var restMethod = restMethods.FirstOrDefault(r => r.Verb == req.HttpServerRequest.Method);
            if (restMethod == null)
            {
                return new MethodNotAllowedResponse(restMethods.Select(r => r.Verb));
            }

            // check if authentication is required
            AuthorizeAttribute authAttribute = null;
            // first check on controller level
            if (restMethod.MethodInfo.DeclaringType.GetTypeInfo().IsDefined(typeof(AuthorizeAttribute)))
            {
                authAttribute = restMethod.MethodInfo.DeclaringType.GetTypeInfo().GetCustomAttributes<AuthorizeAttribute>().Single();
            }
            // otherwise check on method level
            else if (restMethod.MethodInfo.IsDefined(typeof(AuthorizeAttribute)))
            {
                authAttribute = restMethod.MethodInfo.GetCustomAttributes<AuthorizeAttribute>().Single();
            }
            if (authAttribute != null) // need to check authentication
            {
                if (authorizationProvider == null)
                {
                    _log.Error("HandleRequestAsync|AuthenticationProvider not configured");
                    return _responseFactory.CreateInternalServerError(new Exception("HandleRequestAsync|AuthenticationProvider not configured"));
                }
                var authResult = authorizationProvider.Authorize(req.HttpServerRequest, authAttribute);
                if (authResult == HttpResponseStatus.Unauthorized)
                {
                    return _responseFactory.CreateWwwAuthenticate(authorizationProvider.Realm);
                }
            }

            var restMethodExecutor = _methodExecuteFactory.Create(restMethod);

            try
            {
                var controller = _resolve(restMethod.MethodInfo.DeclaringType);
                return await restMethodExecutor.ExecuteMethodAsync(restMethod, controller, req, parsedUri);
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                    return _responseFactory.CreateInternalServerError(ex.InnerException);
                else
                    return _responseFactory.CreateInternalServerError(new Exception(ex.Message));
            }
        }

        public void RegisterController(Type type)
        {
            ConstructorInfo constructorInfo = type.GetConstructors().FirstOrDefault();
            if (constructorInfo == null)
            {
                throw new Exception($"No constructor found on {type}.");
            }

            var restControllerMethodInfos = GetRestMethods(type, constructorInfo);
            AddRestMethods(type, restControllerMethodInfos);
        }

        private void AddRestMethods(Type t, IEnumerable<RestControllerMethodInfo> restControllerMethodInfos)
        {
            var newControllerMethodInfos = restControllerMethodInfos.ToArray();

            _restControllerMethodInfoValidator.Validate(t, _restMethodCollection, newControllerMethodInfos);

            _restMethodCollection = _restMethodCollection.Concat(newControllerMethodInfos)
                .OrderByDescending(x => x.MethodInfo.GetParameters().Count())
                .ToImmutableArray();
        }

        private IEnumerable<RestControllerMethodInfo> GetRestMethods(Type t, ConstructorInfo constructor)
        {
            var possibleValidRestMethods = (from m in t.GetRuntimeMethods()
                where m.IsPublic &&
                      m.IsDefined(typeof(UriFormatAttribute))
                select m).ToList();

            foreach (var restMethod in possibleValidRestMethods)
            {
                if (RestControllerRequestHandler.HasRestResponse(restMethod))
                    yield return new RestControllerMethodInfo(restMethod, constructor, null, RestControllerMethodInfo.TypeWrapper.None);
                else if (RestControllerRequestHandler.HasAsyncRestResponse(restMethod, typeof(Task<>)))
                    yield return new RestControllerMethodInfo(restMethod, constructor, null, RestControllerMethodInfo.TypeWrapper.Task);
                else if (RestControllerRequestHandler.HasAsyncRestResponse(restMethod, typeof(IAsyncOperation<>)))
                    yield return new RestControllerMethodInfo(restMethod, constructor, null, RestControllerMethodInfo.TypeWrapper.AsyncOperation);
            }
        }
    }
}