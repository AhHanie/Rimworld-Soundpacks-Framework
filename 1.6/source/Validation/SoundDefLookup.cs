using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;

namespace Soundpacks_Framework.Validation
{
    public sealed class SubSoundInfo
    {
        public string Name;
        public int Index;
        public int GrainCount;
        public SubSoundDef Def;
    }

    public sealed class SoundDefTargetInfo
    {
        public string DefName;
        public string Label;
        public string SourcePackageId;
        public string SourceModName;
        public bool Sustain;
        public string Context;
        public SoundDef Def;
        public List<SubSoundInfo> SubSounds = new List<SubSoundInfo>();
    }

    public interface ISoundDefLookup
    {
        IReadOnlyList<SoundDefTargetInfo> AllTargets();

        bool TryResolve(string soundDefName, string subSoundName, int subSoundIndex,
            out SoundDefTargetInfo defInfo, out SubSoundInfo subSoundInfo, out string error);
    }

    public sealed class RimWorldSoundDefLookup : ISoundDefLookup
    {
        private List<SoundDefTargetInfo> _cache;

        public void InvalidateCache() => _cache = null;

        public IReadOnlyList<SoundDefTargetInfo> AllTargets()
        {
            if (_cache != null) return _cache;

            var list = new List<SoundDefTargetInfo>();
            foreach (var def in DefDatabase<SoundDef>.AllDefsListForReading)
            {
                var info = new SoundDefTargetInfo
                {
                    DefName = def.defName,
                    Label = string.IsNullOrEmpty(def.label) ? def.defName : def.label,
                    SourcePackageId = def.modContentPack?.PackageId,
                    SourceModName = def.modContentPack?.Name,
                    Sustain = def.sustain,
                    Context = def.context.ToString(),
                    Def = def
                };
                for (int i = 0; i < def.subSounds.Count; i++)
                {
                    var sub = def.subSounds[i];
                    info.SubSounds.Add(new SubSoundInfo
                    {
                        Name = sub.name,
                        Index = i,
                        GrainCount = sub.grains?.Count ?? 0,
                        Def = sub
                    });
                }
                list.Add(info);
            }

            list.Sort((a, b) =>
            {
                int byMod = string.CompareOrdinal(a.SourceModName ?? "", b.SourceModName ?? "");
                return byMod != 0 ? byMod : string.CompareOrdinal(a.DefName, b.DefName);
            });

            _cache = list;
            return _cache;
        }

        public bool TryResolve(string soundDefName, string subSoundName, int subSoundIndex,
            out SoundDefTargetInfo defInfo, out SubSoundInfo subSoundInfo, out string error)
        {
            defInfo = null;
            subSoundInfo = null;
            error = null;

            defInfo = AllTargets().FirstOrDefault(d => d.DefName == soundDefName);
            if (defInfo == null)
            {
                error = "SoundDef '" + soundDefName + "' was not found in the final def database.";
                return false;
            }

            if (subSoundIndex >= 0 && subSoundIndex < defInfo.SubSounds.Count)
            {
                var byIndex = defInfo.SubSounds[subSoundIndex];
                if (subSoundName == null || byIndex.Name == subSoundName)
                {
                    subSoundInfo = byIndex;
                    return true;
                }
            }

            subSoundInfo = defInfo.SubSounds.FirstOrDefault(s => s.Name == subSoundName);
            if (subSoundInfo == null)
            {
                error = "SoundDef '" + soundDefName + "' has no sub-sound named '" + subSoundName + "' at index " + subSoundIndex + ".";
                return false;
            }

            return true;
        }
    }
}
