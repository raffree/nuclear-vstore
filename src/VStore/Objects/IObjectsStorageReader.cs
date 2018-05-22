using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using NuClear.VStore.DataContract;
using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects
{
    public interface IObjectsStorageReader
    {
        Task<ContinuationContainer<IdentifyableObjectRecord<long>>> List(string continuationToken);
        Task<IReadOnlyCollection<ObjectMetadataRecord>> GetObjectMetadatas(IReadOnlyCollection<long> ids);
        Task<IVersionedTemplateDescriptor> GetTemplateDescriptor(long id, string versionId);
        Task<IReadOnlyCollection<ObjectVersionRecord>> GetObjectVersions(long id, string initialVersionId);
        Task<IReadOnlyCollection<VersionedObjectDescriptor<string>>> GetObjectLatestVersions(long id);
        Task<ObjectDescriptor> GetObjectDescriptor(long id, string versionId, CancellationToken cancellationToken);
        Task<bool> IsObjectExists(long id);
        Task<(bool isVersionExists, DateTime lastModified)> IsObjectVersionExists(long id, string versionId);
        Task<IImageElementValue> GetImageElementValue(long id, string versionId, int templateCode);
    }
}