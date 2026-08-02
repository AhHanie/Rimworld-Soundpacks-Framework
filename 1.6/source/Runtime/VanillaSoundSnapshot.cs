using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace Soundpacks_Framework.Runtime
{
    public sealed class VanillaSoundSnapshot
    {
        private readonly Dictionary<SubSoundDef, List<AudioGrain>> _originalGrains = new Dictionary<SubSoundDef, List<AudioGrain>>();
        private readonly Dictionary<SubSoundDef, FloatRange> _originalVolumeRanges = new Dictionary<SubSoundDef, FloatRange>();

        public int Count => _originalGrains.Count;

        public void Capture(SubSoundDef subSound)
        {
            if (!_originalGrains.ContainsKey(subSound))
            {
                _originalGrains[subSound] = subSound.grains;
            }
        }

        public void CaptureVolumeRange(SubSoundDef subSound)
        {
            if (!_originalVolumeRanges.ContainsKey(subSound))
            {
                _originalVolumeRanges[subSound] = subSound.volumeRange;
            }
        }

        public void RestoreAll()
        {
            foreach (var pair in _originalGrains)
            {
                pair.Key.grains = pair.Value;
                pair.Key.ResolveReferences();
            }
            _originalGrains.Clear();

            foreach (var pair in _originalVolumeRanges)
            {
                pair.Key.volumeRange = pair.Value;
            }
            _originalVolumeRanges.Clear();
        }
    }
}
