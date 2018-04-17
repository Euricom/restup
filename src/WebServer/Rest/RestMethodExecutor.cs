using Restup.Webserver.Models.Contracts;
using Restup.Webserver.Models.Schemas;
using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Restup.WebServer.Rest;

namespace Restup.Webserver.Rest
{
    internal abstract class RestMethodExecutor : IRestMethodExecutor
    {
        public async Task<IRestResponse> ExecuteMethodAsync(RestControllerMethodInfo info, object controller, RestServerRequest request, ParsedUri requestUri)
        {
            if (controller is RestControllerBase)
            {
                (controller as RestControllerBase).Request = request.HttpServerRequest;
            }

            var methodInvokeResult = ExecuteAnonymousMethod(info, controller, request, requestUri);
            switch (info.ReturnTypeWrapper)
            {
                case RestControllerMethodInfo.TypeWrapper.None:
                    return await Task.FromResult((IRestResponse)methodInvokeResult);
                case RestControllerMethodInfo.TypeWrapper.AsyncOperation:
                    return await ConvertToTask((dynamic)methodInvokeResult);
                case RestControllerMethodInfo.TypeWrapper.Task:
                    return await (dynamic)methodInvokeResult;
            }

            throw new Exception($"ReturnTypeWrapper of type {info.ReturnTypeWrapper} not known.");
        }

        private static Task<T> ConvertToTask<T>(IAsyncOperation<T> methodInvokeResult)
        {
            return methodInvokeResult.AsTask();
        }

        protected abstract object ExecuteAnonymousMethod(RestControllerMethodInfo info, object controller, RestServerRequest request, ParsedUri requestUri);
    }
}