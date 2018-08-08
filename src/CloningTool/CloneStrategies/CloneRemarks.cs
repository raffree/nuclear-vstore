using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CloningTool.Json;
using CloningTool.RestClient;

using Microsoft.Extensions.Logging;

namespace CloningTool.CloneStrategies
{
    public class CloneRemarks : ICloneStrategy
    {
        private readonly CloningToolOptions _options;
        private readonly ILogger<CloneRemarks> _logger;

        public CloneRemarks(
            CloningToolOptions options,
            IReadOnlyRestClientFacade sourceRestClient,
            IRestClientFacade destRestClient,
            ILogger<CloneRemarks> logger)
        {
            _options = options;
            _logger = logger;
            SourceRestClient = sourceRestClient;
            DestRestClient = destRestClient;
        }

        public IRestClientFacade DestRestClient { get; }

        public IReadOnlyRestClientFacade SourceRestClient { get; }

        public async Task<bool> ExecuteAsync()
        {
            var sourceRemarks = (await SourceRestClient.GetRemarksAsync()).ToDictionary(p => p.Id);
            var destRemarks = (await DestRestClient.GetRemarksAsync()).ToDictionary(p => p.Id);

            var diff = new HashSet<long>(sourceRemarks.Keys);
            diff.SymmetricExceptWith(destRemarks.Keys);
            if (diff.Count > 0)
            {
                var missedInSource = diff.Where(d => !sourceRemarks.ContainsKey(d)).ToList();
                if (missedInSource.Count > 0)
                {
                    _logger.LogWarning(
                        "Next {count} remarks are not present in source: {list}",
                        missedInSource.Count,
                        missedInSource.Select(p => new { Id = p, destRemarks[p].Name }));
                }

                var missedInDest = diff.Where(d => !destRemarks.ContainsKey(d)).ToList();
                if (missedInDest.Count > 0)
                {
                    _logger.LogWarning("Next {count} remarks are not present in destination: {list}", missedInDest.Count, missedInDest);
                }
            }
            else
            {
                _logger.LogInformation("All {count} remarks are present both in source and destination", sourceRemarks.Count);
            }

            var clonedCount = 0L;
            var failedIds = new ConcurrentBag<long>();
            await CloneHelpers.ParallelRunAsync(
                sourceRemarks.Values,
                _options.MaxDegreeOfParallelism,
                async sourceRemark =>
                    {
                        try
                        {
                            var destRemark = destRemarks.ContainsKey(sourceRemark.Id) ? destRemarks[sourceRemark.Id] : null;
                            await CloneRemarkAsync(sourceRemark, destRemark);
                            Interlocked.Increment(ref clonedCount);
                            _logger.LogInformation("Remark cloning succeeded: {remark}", sourceRemark);
                        }
                        catch (Exception ex)
                        {
                            failedIds.Add(sourceRemark.Id);
                            _logger.LogError(default, ex, "Remark cloning error: {remark}", sourceRemark);
                        }
                    });

            _logger.LogInformation("Cloned remarks: {cloned} of {total}", clonedCount, sourceRemarks.Count);
            if (failedIds.Count > 0)
            {
                _logger.LogWarning("Id's of failed remarks: {list}", failedIds);
                return false;
            }

            return true;
        }

        private async Task CloneRemarkAsync(Remark sourceRemark, Remark destRemark)
        {
            if (destRemark == null)
            {
                _logger.LogInformation("Creating remark {id}...", sourceRemark.Id);
                await DestRestClient.CreateRemarkAsync(sourceRemark.Id.ToString(), sourceRemark);
                return;
            }

            if (sourceRemark.Equals(destRemark))
            {
                _logger.LogInformation("Remark {id} is equal in source and destination", sourceRemark.Id);
                return;
            }

            _logger.LogInformation(
                "Remark {id} is not equal in source and destination: {source} and {dest}",
                sourceRemark.Id,
                sourceRemark.Name.ToString(),
                destRemark.Name.ToString());

            if (!_options.OverwriteUnequalRemarks)
            {
                _logger.LogWarning("Skip cloning remark {id} because {param} parameter is not set", sourceRemark.Id, nameof(_options.OverwriteUnequalRemarks));
                return;
            }

            _logger.LogWarning("Overwriting remark {id} because {param} parameter is set", sourceRemark.Id, nameof(_options.OverwriteUnequalRemarks));
            await DestRestClient.UpdateRemarkAsync(sourceRemark.Id.ToString(), sourceRemark);
        }
    }
}