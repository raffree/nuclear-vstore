using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using CloningTool.Json;
using CloningTool.RestClient;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Http;
using NuClear.VStore.Objects;
using NuClear.VStore.Sessions;

namespace CloningTool.CloneStrategies
{
    public abstract class CloneAdvertisementsBase : ICloneStrategy
    {
        private static readonly IReadOnlyCollection<ModerationStatus> ModerationResolutions =
            new HashSet<ModerationStatus> { ModerationStatus.Approved, ModerationStatus.Rejected };

        private const int MaxIdsCountToFetch = 30;
        private readonly CloningToolOptions _options;
        private readonly ILogger<CloneAdvertisementsBase> _logger;
        private readonly bool _isTruncatedCloning;

        private long _uploadedBinariesCount;
        private long _createdAdsCount;
        private long _selectedToWhitelistCount;
        private long _rejectedCount;
        private long _approvedCount;
        private long _draftedCount;
        private long _nominallyApprovedCount;
        private IDictionary<long, ApiListTemplate> _sourceTemplates;
        private IDictionary<long, ApiListTemplate> _destTemplates;

        protected CloneAdvertisementsBase(
            CloningToolOptions options,
            IReadOnlyRestClientFacade sourceRestClient,
            IRestClientFacade destRestClient,
            ILogger<CloneAdvertisementsBase> logger,
            bool isTruncatedCloning)
        {
            _options = options;
            _logger = logger;
            _isTruncatedCloning = isTruncatedCloning;
            SourceRestClient = sourceRestClient;
            DestRestClient = destRestClient;
        }

        public IReadOnlyRestClientFacade SourceRestClient { get; }

        public IRestClientFacade DestRestClient { get; }

        public async Task<bool> ExecuteAsync()
        {
            ResetCounters();
            await EnsureTemplatesAreLoaded();
            List<ApiListAdvertisement> advertisements;
            if (string.IsNullOrEmpty(_options.AdvertisementIdsFilename))
            {
                advertisements = new List<ApiListAdvertisement>(_destTemplates.Count * _options.TruncatedCloneSize);
                foreach (var templateId in _destTemplates.Keys)
                {
                    if (_options.AdvertisementsTemplateId.HasValue && templateId != _options.AdvertisementsTemplateId)
                    {
                        _logger.LogInformation("Skip fetching ads for template {templateId}", templateId);
                        continue;
                    }

                    if (_sourceTemplates.ContainsKey(templateId))
                    {
                        var templateAds = await SourceRestClient.GetAdvertisementsByTemplateAsync(templateId, _isTruncatedCloning ? _options.TruncatedCloneSize : (int?)null);
                        _logger.LogInformation("Found {count} ads for template {templateId}", templateAds.Count, templateId);
                        advertisements.AddRange(_options.AdvertisementsCreatedAtBeginDate.HasValue
                                                ? templateAds.Where(a => a.CreatedAt >= _options.AdvertisementsCreatedAtBeginDate.Value)
                                                : templateAds);
                    }
                    else
                    {
                        _logger.LogWarning("Template {template} does not exist in source", _destTemplates[templateId]);
                    }
                }
            }
            else
            {
                var ids = LoadAdvertisementIdsFromFile(_options.AdvertisementIdsFilename);
                advertisements = new List<ApiListAdvertisement>(ids.Count);
                for (var i = 0; i <= ids.Count / MaxIdsCountToFetch; ++i)
                {
                    var portionIds = ids.Skip(MaxIdsCountToFetch * i).Take(MaxIdsCountToFetch);
                    var portionAds = await SourceRestClient.GetAdvertisementsByIdsAsync(portionIds);
                    advertisements.AddRange(portionAds);
                    _logger.LogInformation("Found {count} advertisements for {num} batch", portionAds.Count, i + 1);
                }
            }

            var clonedCount = 0L;
            var failedAds = new ConcurrentBag<ApiListAdvertisement>();
            _logger.LogInformation("Total advertisements to clone: {total}", advertisements.Count);
            await CloneHelpers.ParallelRunAsync(
                advertisements,
                _options.MaxDegreeOfParallelism,
                async advertisement =>
                    {
                        try
                        {
                            _logger.LogInformation(
                                "Start to clone advertisement {id} with created date {createdAt:o}",
                                advertisement.Id,
                                advertisement.CreatedAt);

                            await CloneAdvertisementAsync(advertisement, _options.FetchAdvertisementBeforeClone);
                            Interlocked.Increment(ref clonedCount);
                        }
                        catch (Exception ex)
                        {
                            failedAds.Add(advertisement);
                            _logger.LogError(default, ex, "Advertisement {id} cloning error", advertisement.Id);
                        }
                    });

            _logger.LogInformation("Total cloned advertisements: {cloned} of {total}", clonedCount, advertisements.Count);
            _logger.LogInformation("Total created advertisements: {created}", _createdAdsCount);
            _logger.LogInformation("Total uploaded binaries: {totalBinaries}", _uploadedBinariesCount);
            _logger.LogInformation("Total advertisements selected to whitelist: {selectedToWhitelistCount}", _selectedToWhitelistCount);
            _logger.LogInformation(
                "Total moderated advertisements: {totalModerated} (approved: {approvedCount}; rejected: {rejectedCount}). Total drafted: {draftedCount}; nominally approved: {nominallyCount}",
                _approvedCount + _rejectedCount,
                _approvedCount,
                _rejectedCount,
                _draftedCount,
                _nominallyApprovedCount);

            // All advertisements have been cloned, check the failed ones:
            if (failedAds.Count > 0)
            {
                return !_isTruncatedCloning && await CloneFailedAdvertisements(failedAds);
            }

            return true;
        }

        private IList<long> LoadAdvertisementIdsFromFile(string fileName)
        {
            var lineNumber = 0L;
            var ids = new List<long>();
            foreach (var line in File.ReadLines(fileName, Encoding.UTF8))
            {
                ++lineNumber;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!long.TryParse(line, out var id))
                {
                    throw new InvalidCastException("Cannot parse id from '" + line + "' on line " + lineNumber.ToString());
                }

                ids.Add(id);
            }

            _logger.LogInformation("Total {count} advertisement identifiers has been fetched from file {filename}", ids.Count, fileName);
            return ids;
        }

        private async Task EnsureTemplatesAreLoaded()
        {
            _sourceTemplates = _sourceTemplates ?? (await SourceRestClient.GetTemplatesAsync()).ToDictionary(p => p.Id);
            _destTemplates = _destTemplates ?? (await DestRestClient.GetTemplatesAsync()).ToDictionary(p => p.Id);
        }

        private async Task<bool> CloneFailedAdvertisements(IReadOnlyCollection<ApiListAdvertisement> failedAds)
        {
            ResetCounters();
            var clonedCount = 0;
            var totallyFailedAds = new ConcurrentBag<long>();
            _logger.LogInformation("Start to clone failed advertisements, total {count}", failedAds.Count.ToString());
            await CloneHelpers.ParallelRunAsync(
                failedAds,
                _options.MaxDegreeOfParallelism,
                async advertisement =>
                    {
                        bool hasFailed;
                        var tries = 0;
                        do
                        {
                            try
                            {
                                ++tries;
                                await CloneAdvertisementAsync(advertisement, true);
                                Interlocked.Increment(ref clonedCount);
                                hasFailed = false;
                            }
                            catch (Exception ex)
                            {
                                hasFailed = true;
                                _logger.LogError(default, ex, "Advertisement {id} repeated cloning error", advertisement.Id.ToString());
                                await Task.Delay(200);
                            }
                        }
                        while (hasFailed && tries < _options.MaxCloneTries);

                        if (hasFailed)
                        {
                            totallyFailedAds.Add(advertisement.Id);
                        }
                    });

            _logger.LogInformation("Failed advertisements repeated cloning done, cloned: {cloned} of {total}", clonedCount, failedAds.Count);
            _logger.LogInformation("Total created advertisements during repeated cloning: {created}", _createdAdsCount);
            _logger.LogInformation("Total uploaded binaries during repeated cloning: {totalBinaries}", _uploadedBinariesCount);
            _logger.LogInformation("Total advertisements selected to whitelist during repeated cloning: {selectedToWhitelistCount}", _selectedToWhitelistCount);
            _logger.LogInformation(
                "Total moderated advertisements during repeated cloning: {totalModerated} (approved: {approvedCount}; rejected: {rejectedCount}). Total drafted: {draftedCount}; nominally approved: {nominallyCount}",
                _approvedCount + _rejectedCount,
                _approvedCount,
                _rejectedCount,
                _draftedCount,
                _nominallyApprovedCount);

            if (totallyFailedAds.Count < 1)
            {
                return true;
            }

            _logger.LogError("Failed advertisements after repeated cloning: {ids}", totallyFailedAds);
            return false;
        }

        private async Task CloneAdvertisementAsync(ApiListAdvertisement advertisement, bool fetchAdvertisementBeforeCloning)
        {
            ApiObjectDescriptor destDescriptor = null;
            var objectId = advertisement.Id.ToString();
            if (fetchAdvertisementBeforeCloning)
            {
                destDescriptor = await DestRestClient.GetAdvertisementAsync(advertisement.Id);
                _logger.LogInformation("Object {id} has been fetched from destination preliminarily with version {versionId}", objectId, destDescriptor.VersionId);
            }

            var versionId = destDescriptor?.VersionId;
            var sourceDescriptor = await SourceRestClient.GetAdvertisementAsync(advertisement.Id);
            if (destDescriptor == null)
            {
                if (sourceDescriptor.Elements.Any(e => IsBinaryAdvertisementElementType(e.Type)))
                {
                    var newAm = await DestRestClient.CreateAdvertisementPrototypeAsync(advertisement.Template.Id, advertisement.Language.ToString(), advertisement.Firm.Id);
                    await SendBinaryContent(sourceDescriptor, newAm);
                }

                try
                {
                    sourceDescriptor.TemplateVersionId = _destTemplates[sourceDescriptor.TemplateId].VersionId;
                    versionId = await DestRestClient.CreateAdvertisementAsync(advertisement.Id, advertisement.Firm.Id, sourceDescriptor);
                    Interlocked.Increment(ref _createdAdsCount);
                }
                catch (ObjectAlreadyExistsException ex)
                {
                    _logger.LogWarning(default, ex, "Object {id} already exists in destination, try to continue execution", objectId);
                }
            }

            if (advertisement.IsWhiteListed)
            {
                await DestRestClient.SelectAdvertisementToWhitelistAsync(objectId);
                Interlocked.Increment(ref _selectedToWhitelistCount);
            }

            await ModerateAdvertisementAsync(advertisement.Id, versionId, sourceDescriptor);
        }

        private async Task ModerateAdvertisementAsync(long id, string versionId, ApiObjectDescriptor sourceDescriptor)
        {
            if (sourceDescriptor.Moderation != null && ModerationResolutions.Contains(sourceDescriptor.Moderation.Status))
            {
                if (string.IsNullOrEmpty(versionId))
                {
                    _logger.LogWarning("VersionId for advertisement {id} is unknown, need to get latest version", id);
                    versionId = (await DestRestClient.GetAdvertisementAsync(id)).VersionId;
                }

                await DestRestClient.UpdateAdvertisementModerationStatusAsync(id, versionId, sourceDescriptor.Moderation);
            }

            switch (sourceDescriptor.Moderation?.Status)
            {
                case null:
                    break;
                case ModerationStatus.Approved:
                    Interlocked.Increment(ref _approvedCount);
                    break;
                case ModerationStatus.Rejected:
                    Interlocked.Increment(ref _rejectedCount);
                    break;
                case ModerationStatus.OnApproval:
                    Interlocked.Increment(ref _draftedCount);
                    break;
                case ModerationStatus.NominallyApproved:
                    Interlocked.Increment(ref _nominallyApprovedCount);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceDescriptor.Moderation), sourceDescriptor.Moderation.Status, "Unsupported moderation status");
            }
        }

        private async Task SendBinaryContent(ApiObjectDescriptor sourceDescriptor, ApiObjectDescriptor newAdvertisement)
        {
            var binaryElements = sourceDescriptor.Elements.Where(e => IsBinaryAdvertisementElementType(e.Type));
            foreach (var element in binaryElements)
            {
                var value = element.Value as IBinaryElementValue
                                         ?? throw new InvalidOperationException($"Cannot cast advertisement {sourceDescriptor.Id} binary element {element.TemplateCode} value");
                if (string.IsNullOrEmpty(value.Raw))
                {
                    continue;
                }

                var (fileData, contentType) = await SourceRestClient.DownloadFileAsync(sourceDescriptor.Id, value.DownloadUri);
                var matchedElement = newAdvertisement.Elements.First(e => e.TemplateCode == element.TemplateCode);
                EnsureUploadUrlIsValid(sourceDescriptor.Id, matchedElement);
                var uploadResponse = await DestRestClient.UploadFileAsync(
                                         sourceDescriptor.Id,
                                         new Uri(matchedElement.UploadUrl),
                                         value.Filename,
                                         fileData,
                                         contentType);

                Interlocked.Increment(ref _uploadedBinariesCount);

                if (element.Type == ElementDescriptorType.CompositeBitmapImage)
                {
                    var compositeBitmapImageElementValue = element.Value as ICompositeBitmapImageElementValue
                                    ?? throw new InvalidOperationException($"Cannot cast advertisement {sourceDescriptor.Id} composite image element {element.TemplateCode} value");

                    var sizeSpecificImagesUploadTasks = compositeBitmapImageElementValue
                            .SizeSpecificImages
                            .Select(async image =>
                                        {
                                            var (imageFileData, imageContentType) = await SourceRestClient.DownloadFileAsync(sourceDescriptor.Id, image.DownloadUri);
                                            var headers = new[]
                                                {
                                                    new NameValueHeaderValue(HeaderNames.AmsFileType, FileType.SizeSpecificBitmapImage.ToString()),
                                                    new NameValueHeaderValue(HeaderNames.AmsImageSize, image.Size.ToString())
                                                };

                                            var imageUploadResponse = await DestRestClient.UploadFileAsync(
                                                                                sourceDescriptor.Id,
                                                                                new Uri(matchedElement.UploadUrl),
                                                                                image.Filename,
                                                                                imageFileData,
                                                                                imageContentType,
                                                                                headers);

                                            Interlocked.Increment(ref _uploadedBinariesCount);
                                            return new SizeSpecificImage
                                                {
                                                    Size = image.Size,
                                                    Raw = imageUploadResponse.Raw
                                                };
                                        })
                            .ToList();

                    element.Value = new CompositeBitmapImageElementValue
                        {
                            Raw = uploadResponse.Raw,
                            CropArea = compositeBitmapImageElementValue.CropArea,
                            SizeSpecificImages = await Task.WhenAll(sizeSpecificImagesUploadTasks)
                        };
                }
                else
                {
                    element.Value = uploadResponse;
                }
            }
        }

        private static void EnsureUploadUrlIsValid(long advertisementId, ApiObjectElementDescriptor advertisementElement)
        {
            if (string.IsNullOrEmpty(advertisementElement.UploadUrl))
            {
                throw new ArgumentException($"Upload URL is empty for {advertisementElement.TemplateCode} element (advertisement {advertisementId})");
            }

            if (!Uri.IsWellFormedUriString(advertisementElement.UploadUrl, UriKind.Absolute))
            {
                throw new ArgumentException($"Upload URL is not well formed for {advertisementElement.TemplateCode} element (advertisement {advertisementId})");
            }
        }

        private bool IsBinaryAdvertisementElementType(ElementDescriptorType elementDescriptorType)
        {
            switch (elementDescriptorType)
            {
                case ElementDescriptorType.PlainText:
                case ElementDescriptorType.FormattedText:
                case ElementDescriptorType.FasComment:
                case ElementDescriptorType.Link:
                case ElementDescriptorType.Phone:
                case ElementDescriptorType.VideoLink:
                case ElementDescriptorType.Color:
                    return false;
                case ElementDescriptorType.BitmapImage:
                case ElementDescriptorType.VectorImage:
                case ElementDescriptorType.Article:
                case ElementDescriptorType.CompositeBitmapImage:
                case ElementDescriptorType.ScalableBitmapImage:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementDescriptorType), elementDescriptorType, "Unknown advertisement's element type");
            }
        }

        private void ResetCounters()
        {
            _createdAdsCount = 0L;
            _uploadedBinariesCount = 0L;
            _approvedCount = 0L;
            _draftedCount = 0L;
            _rejectedCount = 0L;
            _selectedToWhitelistCount = 0L;
            _nominallyApprovedCount = 0L;
        }
    }
}
