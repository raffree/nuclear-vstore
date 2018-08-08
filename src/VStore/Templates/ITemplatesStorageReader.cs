﻿using System.Collections.Generic;
using System.Threading.Tasks;

using NuClear.VStore.DataContract;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Templates
{
    public interface ITemplatesStorageReader
    {
        Task<ContinuationContainer<IdentifyableObjectRecord<long>>> List(string continuationToken);
        Task<IReadOnlyCollection<ObjectMetadataRecord>> GetTemplateMetadatas(IReadOnlyCollection<long> ids);
        Task<TemplateDescriptor> GetTemplateDescriptor(long id, string versionId);
        Task<string> GetTemplateLatestVersion(long id);
        Task<bool> IsTemplateExists(long id);
        Task<IReadOnlyCollection<TemplateVersionRecord>> GetTemplateVersions(long id);
    }
}