using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Soundpacks_Framework.Validation;

namespace Soundpacks_Framework.UI
{
    public sealed class SoundDefSearchIndex
    {
        private readonly RimWorldSoundDefLookup _lookup = new RimWorldSoundDefLookup();
        private IReadOnlyList<SoundDefTargetInfo> _all;

        public IReadOnlyList<SoundDefTargetInfo> All => _all ?? (_all = _lookup.AllTargets());

        public void Rebuild()
        {
            _lookup.InvalidateCache();
            _all = _lookup.AllTargets();
        }

        public IEnumerable<SoundDefTargetInfo> Filter(QuickSearchFilter filter)
        {
            if (filter == null || !filter.Active) return All;
            return All.Where(d => Matches(d, filter));
        }

        private static bool Matches(SoundDefTargetInfo d, QuickSearchFilter filter)
        {
            return filter.Matches(d.DefName) ||
                   filter.Matches(d.Label) ||
                   filter.Matches(d.SourcePackageId ?? "") ||
                   filter.Matches(d.SourceModName ?? "") ||
                   filter.Matches(d.Context) ||
                   (d.Sustain && filter.Matches("sustain"));
        }
    }
}
