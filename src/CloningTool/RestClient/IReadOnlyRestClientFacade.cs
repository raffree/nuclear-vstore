using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using CloningTool.Json;

using NuClear.VStore.DataContract;

namespace CloningTool.RestClient
{
    public interface IReadOnlyRestClientFacade
    {
        Task<IReadOnlyCollection<PositionDescriptor>> GetContentPositionsAsync();
        Task<ApiTemplateDescriptor> GetTemplateAsync(string templateId, string versionId = null);
        Task<IReadOnlyCollection<TemplateVersionRecord>> GetTemplateVersionsAsync(string templateId);
        Task<IReadOnlyCollection<ApiListTemplate>> GetTemplatesAsync();
        Task<IReadOnlyCollection<RemarkCategory>> GetRemarkCategoriesAsync();
        Task<IReadOnlyCollection<Remark>> GetRemarksAsync();
        Task<IReadOnlyCollection<ApiListAdvertisement>> GetAdvertisementsByTemplateAsync(long templateId, int? fetchSize);
        Task<IReadOnlyCollection<ApiListAdvertisement>> GetAdvertisementsByIdsAsync(IEnumerable<long> ids);
        Task<ApiObjectDescriptor> GetAdvertisementAsync(long advertisementId);
        Task<(byte[] data, MediaTypeHeaderValue contentType)> DownloadFileAsync(long advertisementId, Uri downloadUrl);
        Task EnsureApiAvailableAsync(int initialPingInterval, int initialPingTries);
    }
}
