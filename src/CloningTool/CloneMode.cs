using System;

namespace CloningTool
{
    [Flags]
    public enum CloneMode
    {
        CloneTemplates = 1,
        CloneAdvertisements = 2,
        CloneContentPositionsLinks = 4,
        TruncatedCloneAdvertisements = 8,
        CloneRemarkCategories = 16,
        CloneRemarks = 32,
        ReloadFiles = 64,
        CloneTemplatesWithLinks = CloneTemplates | CloneContentPositionsLinks,
        CloneRemarksWithCategories = CloneRemarkCategories | CloneRemarks,
        CloneAll = CloneTemplatesWithLinks | CloneRemarksWithCategories | CloneAdvertisements,
        TruncatedCloneAll = CloneTemplatesWithLinks | CloneRemarksWithCategories | TruncatedCloneAdvertisements
    }
}
