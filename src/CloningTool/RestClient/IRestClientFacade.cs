using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using CloningTool.Json;

namespace CloningTool.RestClient
{
    public interface IRestClientFacade : IReadOnlyRestClientFacade
    {
        Task CreatePositionTemplateLinkAsync(string positionId, string templateId);
        Task<string> CreateTemplateAsync(string templateId, ApiTemplateDescriptor template);
        Task<string> UpdateTemplateAsync(ApiTemplateDescriptor template, string versionId);
        Task<ApiObjectDescriptor> CreateAdvertisementPrototypeAsync(long templateId, string langCode, long firmId);
        Task<ApiObjectElementRawValue> UploadFileAsync(long advertisementId, Uri uploadUri, string fileName, byte[] fileData, MediaTypeHeaderValue contentType, params NameValueHeaderValue[] headers);
        Task UpdateAdvertisementModerationStatusAsync(long advertisementId, string versionId, ModerationResult moderationResult);
        Task SelectAdvertisementToWhitelistAsync(string advertisementId);
        Task<string> CreateAdvertisementAsync(long id, long firmId, ApiObjectDescriptor advertisement);
        Task<ApiObjectDescriptor> UpdateAdvertisementAsync(ApiObjectDescriptor advertisement);
        Task CreateRemarkCategoryAsync(string remarkCategoryId, RemarkCategory remarkCategory);
        Task UpdateRemarkCategoryAsync(string remarkCategoryId, RemarkCategory remarkCategory);
        Task CreateRemarkAsync(string remarkId, Remark remark);
        Task UpdateRemarkAsync(string remarkId, Remark remark);
    }
}
