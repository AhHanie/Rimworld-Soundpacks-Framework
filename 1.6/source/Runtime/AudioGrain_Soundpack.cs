using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse.Sound;

namespace Soundpacks_Framework.Runtime
{
    public sealed class AudioGrain_Soundpack : AudioGrain
    {
        private readonly List<ResolvedGrain> _resolvedGrains;

        public AudioGrain_Soundpack(IEnumerable<AudioClip> clips)
        {
            _resolvedGrains = clips.Select(clip => (ResolvedGrain)new ResolvedGrain_Clip(clip)).ToList();
        }

        public override IEnumerable<ResolvedGrain> GetResolvedGrains()
        {
            return _resolvedGrains;
        }
    }
}
