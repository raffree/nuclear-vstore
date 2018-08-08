using System;
using System.Threading;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NuClear.VStore.Http.Core.Extensions
{
    public static class WebHostExtensions
    {
        public static void ConfigureThreadPool(this IWebHost webHost)
        {
            var loggerFactory = webHost.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(webHost.GetType());

            logger.LogInformation("Processor count = {processorCount}", Environment.ProcessorCount);

            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);
            ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
            ThreadPool.SetMinThreads(minWorkerThreads, maxCompletionPortThreads);

            ThreadPool.GetMinThreads(out minWorkerThreads, out minCompletionPortThreads);

            logger.LogInformation(
                "Threadpool info: MinWorkerThreads = {minWorkerThreads}, MaxWorkerThreads = {maxWorkerThreads}, " +
                "MinCompletionPortThreads = {minCompletionPortThreads}, MaxCompletionPortThreads = {maxCompletionPortThreads}",
                minWorkerThreads,
                maxWorkerThreads,
                minCompletionPortThreads,
                maxCompletionPortThreads);
        }
    }
}