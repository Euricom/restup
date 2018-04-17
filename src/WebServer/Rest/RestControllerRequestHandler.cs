﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Foundation;
using Restup.HttpMessage.Models.Schemas;
using Restup.Webserver.Attributes;
using Restup.Webserver.InstanceCreators;
using Restup.Webserver.Models.Contracts;
using Restup.Webserver.Models.Schemas;
using Restup.WebServer.Attributes;
using Restup.WebServer.Logging;
using Restup.WebServer.Models.Contracts;

namespace Restup.Webserver.Rest
{
    internal class RestControllerRequestHandler
    {
        private ImmutableArray<RestControllerMethodInfo> _restMethodCollection;
        private readonly RestResponseFactory _responseFactory;
        private readonly RestControllerMethodExecutorFactory _methodExecuteFactory;
        private readonly UriParser _uriParser;
		private readonly ILogger _log = LogManager.GetLogger<RestControllerRequestHandler>();
        private readonly RestControllerMethodInfoValidator _restControllerMethodInfoValidator;

        internal RestControllerRequestHandler()
        {
            _restMethodCollection = ImmutableArray<RestControllerMethodInfo>.Empty;
            _responseFactory = new RestResponseFactory();
            _methodExecuteFactory = new RestControllerMethodExecutorFactory();
            _uriParser = new UriParser();
            _restControllerMethodInfoValidator = new RestControllerMethodInfoValidator();
        }

        internal void RegisterController<T>(params object[] constructorArgs) where T : class
        {
            constructorArgs.GuardNull(nameof(constructorArgs));

            ConstructorInfo constructorInfo;
            if (!ReflectionHelper.TryFindMatchingConstructor<T>(constructorArgs, out constructorInfo))
            {
                throw new Exception($"No constructor found on {typeof(T)} that matches passed in constructor arguments.");
            }

            var restControllerMethodInfos = GetRestMethods<T>(() => constructorArgs, constructorInfo);
            AddRestMethods<T>(restControllerMethodInfos);
        }

        internal void RegisterController<T>(Func<object[]> constructorArgs) where T : class
        {
            constructorArgs.GuardNull(nameof(constructorArgs));

            var constructorInfos = typeof (T).GetConstructors();
            if (constructorInfos.Length > 1)
                throw new Exception("More than one constructor defined.");
            if (constructorInfos.Length == 0)
                throw new Exception("No public constructor defined.");

            var restControllerMethodInfos = GetRestMethods<T>(constructorArgs, constructorInfos.Single());
            AddRestMethods<T>(restControllerMethodInfos);
        }

        private void AddRestMethods<T>(IEnumerable<RestControllerMethodInfo> restControllerMethodInfos) where T : class
        {
            var newControllerMethodInfos = restControllerMethodInfos.ToArray();

            _restControllerMethodInfoValidator.Validate<T>(_restMethodCollection, newControllerMethodInfos);

            _restMethodCollection = _restMethodCollection.Concat(newControllerMethodInfos)
                .OrderByDescending(x => x.MethodInfo.GetParameters().Count())
                .ToImmutableArray();

            InstanceCreatorCache.Default.CacheCreator(typeof(T));
        }

        private IEnumerable<RestControllerMethodInfo> GetRestMethods<T>(Func<object[]> constructorArgs, ConstructorInfo constructor) where T : class
        {
            var possibleValidRestMethods = (from m in typeof(T).GetRuntimeMethods()
                                            where m.IsPublic &&
                                                  m.IsDefined(typeof(UriFormatAttribute))
                                            select m).ToList();

            foreach (var restMethod in possibleValidRestMethods)
            {
                if (HasRestResponse(restMethod))
                    yield return new RestControllerMethodInfo(restMethod, constructor, constructorArgs, RestControllerMethodInfo.TypeWrapper.None);
                else if (HasAsyncRestResponse(restMethod, typeof(Task<>)))
                    yield return new RestControllerMethodInfo(restMethod, constructor, constructorArgs, RestControllerMethodInfo.TypeWrapper.Task);
                else if (HasAsyncRestResponse(restMethod, typeof(IAsyncOperation<>)))
                    yield return new RestControllerMethodInfo(restMethod, constructor, constructorArgs, RestControllerMethodInfo.TypeWrapper.AsyncOperation);
            }
        }

        internal static bool HasRestResponse(MethodInfo m)
        {
            return m.ReturnType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(IRestResponse));
        }

        internal static bool HasAsyncRestResponse(MethodInfo m, Type type)
        {
            if (!m.ReturnType.IsConstructedGenericType)
                return false;

            var genericTypeDefinition = m.ReturnType.GetGenericTypeDefinition();
            var isAsync = genericTypeDefinition == type;
            if (!isAsync)
                return false;

            var genericArgs = m.ReturnType.GetGenericArguments();
            if (!genericArgs.Any())
            {
                return false;
            }

            return genericArgs[0].GetTypeInfo().ImplementedInterfaces.Contains(typeof(IRestResponse));
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

            ParsedUri parsedUri;
            var incomingUriAsString = req.HttpServerRequest.Uri.ToRelativeString();
            if (!_uriParser.TryParse(incomingUriAsString, out parsedUri))
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
			if(restMethod.MethodInfo.DeclaringType.GetTypeInfo().IsDefined(typeof(AuthorizeAttribute)))
			{
				authAttribute = restMethod.MethodInfo.DeclaringType.GetTypeInfo().GetCustomAttributes<AuthorizeAttribute>().Single();
			}
			// otherwise check on method level
			else if(restMethod.MethodInfo.IsDefined(typeof(AuthorizeAttribute)))
			{
				authAttribute = restMethod.MethodInfo.GetCustomAttributes<AuthorizeAttribute>().Single();
			}
			if(authAttribute != null) // need to check authentication
			{
				if (authorizationProvider == null)
				{
					_log.Error("HandleRequestAsync|AuthenticationProvider not configured");
					return _responseFactory.CreateInternalServerError(new Exception("HandleRequestAsync|AuthenticationProvider not configured"));
				}
				var authResult = authorizationProvider.Authorize(req.HttpServerRequest);
				if(authResult == HttpResponseStatus.Unauthorized)
				{
					return _responseFactory.CreateWwwAuthenticate(authorizationProvider.Realm);
				}
			}

            var restMethodExecutor = _methodExecuteFactory.Create(restMethod);

            try
            {
                var instantiator = InstanceCreatorCache.Default.GetCreator(restMethod.MethodInfo.DeclaringType);
                var obj = instantiator.Create(restMethod.ControllerConstructor, restMethod.ControllerConstructorArgs());

                return await restMethodExecutor.ExecuteMethodAsync(restMethod, obj, req, parsedUri);
            }
            catch(Exception ex)
            {
                if (ex.InnerException != null)
                    return _responseFactory.CreateInternalServerError(ex.InnerException);
                else
                    return _responseFactory.CreateInternalServerError(new Exception(ex.Message));
            }
        }
    }
}
