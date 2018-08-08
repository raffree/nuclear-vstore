using System.Threading.Tasks;

namespace CloningTool.CloneStrategies
{
    public class CloneRemarksWithCategories : ICloneStrategy
    {
        private readonly CompositeCloneStrategy _composite;

        public CloneRemarksWithCategories(ICloneStrategyProvider cloneStrategyProvider)
        {
            _composite = new CompositeCloneStrategy(
                cloneStrategyProvider.GetCloneStrategy(CloneMode.CloneRemarkCategories),
                cloneStrategyProvider.GetCloneStrategy(CloneMode.CloneRemarks));
        }

        public async Task<bool> ExecuteAsync() => await _composite.ExecuteAsync();
    }
}