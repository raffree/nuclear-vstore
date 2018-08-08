using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CloningTool.Json;
using CloningTool.RestClient;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

namespace CloningTool.CloneStrategies
{
    public class CloneRemarkCategories : ICloneStrategy
    {
        private readonly CloningToolOptions _options;
        private readonly ILogger<CloneRemarks> _logger;

        public CloneRemarkCategories(
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
            var sourceCategories = (await SourceRestClient.GetRemarkCategoriesAsync()).ToDictionary(p => p.Id);
            var destCategories = (await DestRestClient.GetRemarkCategoriesAsync()).ToDictionary(p => p.Id);

            var diff = new HashSet<long>(sourceCategories.Keys);
            diff.SymmetricExceptWith(destCategories.Keys);
            if (diff.Count > 0)
            {
                var missedInSource = diff.Where(d => !sourceCategories.ContainsKey(d)).ToList();
                if (missedInSource.Count > 0)
                {
                    _logger.LogWarning(
                        "Next {count} remark categories are not present in source: {list}",
                        missedInSource.Count,
                        missedInSource.Select(p => new { Id = p, destCategories[p].Name }));
                }

                var missedInDest = diff.Where(d => !destCategories.ContainsKey(d)).ToList();
                if (missedInDest.Count > 0)
                {
                    _logger.LogWarning("Next {count} remark categories are not present in destination: {list}", missedInDest.Count, missedInDest);
                }
            }
            else
            {
                _logger.LogInformation("All {count} remark categories are present both in source and destination", sourceCategories.Count);
            }

            var clonedCount = 0L;
            var failedIds = new ConcurrentBag<long>();
            await CloneHelpers.ParallelRunAsync(
                sourceCategories.Values,
                _options.MaxDegreeOfParallelism,
                async sourceCategory =>
                    {
                        try
                        {
                            var destCategory = destCategories.ContainsKey(sourceCategory.Id) ? destCategories[sourceCategory.Id] : null;
                            await CloneRemarkCategoryAsync(sourceCategory, destCategory);
                            Interlocked.Increment(ref clonedCount);
                            _logger.LogInformation("Remark category cloning succeeded: {category}", sourceCategory);
                        }
                        catch (Exception ex)
                        {
                            failedIds.Add(sourceCategory.Id);
                            _logger.LogError(default, ex, "Remark category cloning error: {category}", sourceCategory);
                        }
                    });

            _logger.LogInformation("Cloned remark categories: {cloned} of {total}", clonedCount, sourceCategories.Count);
            if (failedIds.Count > 0)
            {
                _logger.LogWarning("Id's of failed remark categories: {list}", failedIds);
                return false;
            }

            return true;
        }

        private async Task CloneRemarkCategoryAsync(RemarkCategory sourceCategory, RemarkCategory destCategory)
        {
            if (destCategory == null)
            {
                _logger.LogInformation("Creating remark category {id}...", sourceCategory.Id);
                await DestRestClient.CreateRemarkCategoryAsync(sourceCategory.Id.ToString(), sourceCategory);
                return;
            }

            if (!JToken.DeepEquals(sourceCategory.Name, destCategory.Name))
            {
                _logger.LogInformation(
                    "Remark category {id} has unequal names in source and destination: {source} and {dest}",
                    sourceCategory.Id,
                    sourceCategory.Name.ToString(),
                    destCategory.Name.ToString());

                if (!_options.OverwriteUnequalRemarks)
                {
                    _logger.LogWarning("Skip cloning remark category {id} because {param} parameter is not set", sourceCategory.Id, nameof(_options.OverwriteUnequalRemarks));
                    return;
                }

                _logger.LogWarning("Overwriting remark category {id} because {param} parameter is set", sourceCategory.Id, nameof(_options.OverwriteUnequalRemarks));
                await DestRestClient.UpdateRemarkCategoryAsync(sourceCategory.Id.ToString(), sourceCategory);
            }
            else
            {
                _logger.LogInformation("Remark category {id} is equal in source and destination", sourceCategory.Id);
            }
        }
    }
}