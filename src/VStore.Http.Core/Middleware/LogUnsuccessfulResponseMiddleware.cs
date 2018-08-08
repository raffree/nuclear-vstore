using System.IO;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Core.Enrichers;

namespace NuClear.VStore.Http.Core.Middleware
{
    public sealed class LogUnsuccessfulResponseMiddleware
    {
        private static readonly ILogger Logger = Log.ForContext<LogUnsuccessfulResponseMiddleware>();

        private readonly RequestDelegate _next;

        public LogUnsuccessfulResponseMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var bodyStream = httpContext.Response.Body;

            var responseBodyStream = new MemoryStream();
            try
            {
                httpContext.Response.Body = responseBodyStream;

                await _next(httpContext);

                if (httpContext.Response?.StatusCode > 399)
                {
                    responseBodyStream.Seek(0, SeekOrigin.Begin);
                    var reader = new StreamReader(responseBodyStream);
                    var responseContent = await reader.ReadToEndAsync();

                    var enrichers = new ILogEventEnricher[]
                        {
                            new PropertyEnricher(nameof(httpContext.Response.ContentType), httpContext.Response.ContentType),
                            new PropertyEnricher(nameof(httpContext.Response.StatusCode), httpContext.Response.StatusCode),
                            new PropertyEnricher("ResponseContent", responseContent)
                        };
                    using (LogContext.Push(enrichers))
                    {
                        Logger.Error($"Request processed with {httpContext.Response.StatusCode}.");
                    }
                }
            }
            finally
            {
                responseBodyStream.Seek(0, SeekOrigin.Begin);
                await responseBodyStream.CopyToAsync(bodyStream);
                httpContext.Response.Body = bodyStream;

                responseBodyStream.Dispose();
            }
        }
    }
}