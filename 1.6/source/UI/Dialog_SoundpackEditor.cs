using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Soundpacks_Framework.Audio;
using Soundpacks_Framework.Models;
using Soundpacks_Framework.Settings;
using Soundpacks_Framework.Storage;
using Soundpacks_Framework.Validation;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Soundpacks_Framework.UI
{
    public enum EditorTab
    {
        Metadata,
        Mappings,
        Files
    }

    public sealed class Dialog_SoundpackEditor : Window
    {
        private const float MaxPreviewVolume = 2f;

        private readonly SoundpackDescriptor _source;
        private readonly bool _assignNewId;
        private readonly Action _onSavedOrClosed;

        private string _draftDir;
        private string _draftAudioDir;
        private SoundpackManifest _manifest;
        private bool _saved;

        private EditorTab _tab = EditorTab.Metadata;
        private readonly SoundDefSearchIndex _searchIndex = new SoundDefSearchIndex();
        private readonly QuickSearchWidget _mappingSearch = new QuickSearchWidget();
        private SoundDefTargetInfo _selectedDef;
        private SubSoundInfo _selectedSubSound;
        private Vector2 _defScroll, _fileListScroll, _mappingFilesScroll;
        private string _error;
        private string _supportedVersionsText;

        private GameObject _previewObject;
        private AudioSource _previewSource;
        private AudioClip _previewClip;

        private Sustainer _previewSustainer;
        private SoundDef _previewSustainerDef;

        public override Vector2 InitialSize => new Vector2(940f, 660f);

        public Dialog_SoundpackEditor(SoundpackDescriptor source, bool assignNewId, Action onSavedOrClosed)
        {
            _source = source;
            _assignNewId = assignNewId || source == null;
            _onSavedOrClosed = onSavedOrClosed;
            resizeable = true;
            draggable = true;
            forcePause = true;
            doCloseX = true;
            InitializeDraft();
        }

        private void InitializeDraft()
        {
            _draftDir = SoundpackRepository.NewDraftDir();
            _draftAudioDir = Path.Combine(_draftDir, "Audio");

            if (_source == null)
            {
                Directory.CreateDirectory(_draftAudioDir);
                _manifest = new SoundpackManifest
                {
                    schemaVersion = SoundpackManifest.CurrentSchemaVersion,
                    id = SoundpackPathPolicy.NewGuidPackId(),
                    name = "New Soundpack",
                    author = "",
                    version = "1.0.0",
                    supportedRimWorldVersions = new List<string> { "1.6" }
                };
            }
            else
            {
                CopyDirectory(_source.DirectoryPath, _draftDir);
                Directory.CreateDirectory(_draftAudioDir);
                var loaded = SoundpackRepository.LoadPack(_source.Id);
                _manifest = loaded.Success ? loaded.Manifest : _source.Manifest?.Clone();
                if (_manifest == null)
                {
                    string manifestPath = Path.Combine(_draftDir, SoundpackPathPolicy.ManifestFileName);
                    var text = File.ReadAllText(manifestPath, System.Text.Encoding.UTF8);
                    _manifest = Serialization.SoundpackManifestSerializer.Deserialize(text).Manifest;
                }
                if (_assignNewId)
                {
                    _manifest.id = SoundpackPathPolicy.NewGuidPackId();
                    _manifest.name = (_manifest.name ?? "Soundpack") + " (Copy)";
                }
            }

            _supportedVersionsText = string.Join(", ", _manifest.supportedRimWorldVersions);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect tabsRect = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            DrawTabs(tabsRect);

            Rect bodyRect = new Rect(inRect.x, tabsRect.yMax + 8f, inRect.width, inRect.height - tabsRect.height - 56f);
            switch (_tab)
            {
                case EditorTab.Metadata: DrawMetadataTab(bodyRect); break;
                case EditorTab.Mappings: DrawMappingsTab(bodyRect); break;
                case EditorTab.Files: DrawFilesTab(bodyRect); break;
            }

            Rect bottom = new Rect(inRect.x, inRect.yMax - 36f, inRect.width, 36f);
            DrawBottomBar(bottom);
        }

        private void DrawTabs(Rect rect)
        {
            const float circleWidth = 24f;
            const float gapAfterLabel = 8f;
            const float gapBetweenTabs = 20f;

            float x = rect.x;
            x = DrawTab(x, rect, EditorTab.Metadata, "SPF.TabMetadata".Translate(), circleWidth, gapAfterLabel, gapBetweenTabs);
            x = DrawTab(x, rect, EditorTab.Mappings, "SPF.TabMappings".Translate(), circleWidth, gapAfterLabel, gapBetweenTabs);
            DrawTab(x, rect, EditorTab.Files, "SPF.TabFiles".Translate(), circleWidth, gapAfterLabel, gapBetweenTabs);
        }

        private float DrawTab(float x, Rect rowRect, EditorTab tab, string label, float circleWidth, float gapAfterLabel, float gapBetweenTabs)
        {
            float buttonWidth = Text.CalcSize(label).x + gapAfterLabel + circleWidth;
            Rect tabRect = new Rect(x, rowRect.y, buttonWidth, rowRect.height);
            if (Widgets.RadioButtonLabeled(tabRect, label, _tab == tab))
            {
                _tab = tab;
            }
            return x + buttonWidth + gapBetweenTabs;
        }

        private void DrawMetadataTab(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("SPF.FieldId".Translate() + ": " + _manifest.id);
            listing.Gap(6f);
            listing.Label("SPF.FieldName".Translate());
            _manifest.name = listing.TextEntry(_manifest.name ?? "");
            listing.Label("SPF.FieldAuthor".Translate());
            _manifest.author = listing.TextEntry(_manifest.author ?? "");
            listing.Label("SPF.FieldVersion".Translate());
            _manifest.version = listing.TextEntry(_manifest.version ?? "");
            listing.Label("SPF.FieldDescription".Translate());
            _manifest.description = listing.TextEntry(_manifest.description ?? "", 3);
            listing.Label("SPF.FieldAttribution".Translate());
            _manifest.attribution = listing.TextEntry(_manifest.attribution ?? "");
            listing.Label("SPF.FieldLicense".Translate());
            _manifest.license = listing.TextEntry(_manifest.license ?? "");
            listing.Label("SPF.FieldSupportedVersions".Translate());
            _supportedVersionsText = listing.TextEntry(_supportedVersionsText ?? "");
            _manifest.supportedRimWorldVersions = _supportedVersionsText
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            listing.End();
        }

        private void DrawMappingsTab(Rect rect)
        {
            Rect leftRect = new Rect(rect.x, rect.y, rect.width * 0.45f, rect.height);
            Rect rightRect = new Rect(leftRect.xMax + 8f, rect.y, rect.width - leftRect.width - 8f, rect.height);

            Rect searchRect = new Rect(leftRect.x, leftRect.y, leftRect.width, 28f);
            _mappingSearch.OnGUI(searchRect);

            Rect listRect = new Rect(leftRect.x, searchRect.yMax + 4f, leftRect.width, leftRect.height - searchRect.height - 4f);
            DrawDefList(listRect);

            DrawSelectedTargetPane(rightRect);
        }

        private void DrawDefList(Rect outRect)
        {
            var defs = _searchIndex.Filter(_mappingSearch.filter).ToList();
            float rowHeight = 24f;

            var rows = new List<(SoundDefTargetInfo def, SubSoundInfo sub)>();
            foreach (var def in defs)
            {
                if (def.SubSounds.Count <= 1)
                {
                    rows.Add((def, def.SubSounds.FirstOrDefault()));
                }
                else
                {
                    foreach (var sub in def.SubSounds)
                    {
                        rows.Add((def, sub));
                    }
                }
            }

            Rect viewRect = new Rect(0, 0, outRect.width - 20f, rows.Count * rowHeight + 4f);
            Widgets.BeginScrollView(outRect, ref _defScroll, viewRect);
            float y = 0f;
            foreach (var (def, sub) in rows)
            {
                string label = def.SubSounds.Count <= 1
                    ? def.DefName + "  [" + (def.SourceModName ?? "?") + "]"
                    : "  " + def.DefName + " / " + (sub?.Name ?? "?");
                bool isSelected = _selectedDef == def && _selectedSubSound == sub;
                Rect row = new Rect(0, y, viewRect.width, rowHeight);
                if (Widgets.RadioButtonLabeled(row, label, isSelected))
                {
                    _selectedDef = def;
                    _selectedSubSound = sub;
                }
                y += rowHeight;
            }
            Widgets.EndScrollView();
        }

        private void DrawSelectedTargetPane(Rect rect)
        {
            if (_selectedDef == null || _selectedSubSound == null)
            {
                StopPreviewSustainer();
                Widgets.Label(rect, "SPF.NoTargetSelected".Translate());
                return;
            }

            if (_previewSustainer != null && _previewSustainerDef != _selectedDef.Def)
            {
                StopPreviewSustainer();
            }

            bool isFrameworkCue = IsFrameworkCueDef(_selectedDef.DefName);
            var mapping = FindOrNull(_selectedDef.DefName, _selectedSubSound.Name, _selectedSubSound.Index);
            float volumeRangeExtra = mapping == null ? 0f : (mapping.volumeRange != null ? 64f : 32f);
            float paneHeight = (isFrameworkCue ? 160f : 140f) + volumeRangeExtra;

            var listing = new Listing_Standard();
            listing.Begin(new Rect(rect.x, rect.y, rect.width, paneHeight));
            listing.Label(_selectedDef.DefName + " / " + _selectedSubSound.Name);
            listing.Label("SPF.FieldSourceMod".Translate(_selectedDef.SourceModName ?? "?", _selectedDef.SourcePackageId ?? "?"));
            DrawPreviewVolumeSlider(listing);
            listing.Gap(4f);
            DrawVanillaPreviewControl(listing);
            if (isFrameworkCue)
            {
                listing.Gap(4f);
                listing.Label("SPF.FrameworkCueNote".Translate());
            }
            if (mapping != null)
            {
                listing.Gap(4f);
                DrawVolumeRangeOverride(listing, mapping);
            }
            listing.End();

            Rect filesRect = new Rect(rect.x, rect.y + paneHeight + 6f, rect.width, rect.height - paneHeight - 40f);
            DrawMappingFiles(filesRect, mapping);

            Rect addRect = new Rect(rect.x, rect.yMax - 30f, 160f, 28f);
            if (Widgets.ButtonText(addRect, "SPF.AddFileToMapping".Translate()))
            {
                OpenAudioFilePicker(paths =>
                {
                    var m = FindOrCreate(_selectedDef.DefName, _selectedSubSound.Name, _selectedSubSound.Index);
                    foreach (var path in paths)
                    {
                        AddFileToDraftPool(path, out string canonical, out string err);
                        if (canonical != null)
                        {
                            m.files.Add(new SoundpackAudioFile { path = canonical });
                        }
                        else
                        {
                            _error = err;
                        }
                    }
                });
            }
        }

        private static (float min, float max)? _volumeRangeClipboard;

        private void DrawVolumeRangeOverride(Listing_Standard listing, SoundpackMapping mapping)
        {
            bool hadOverride = mapping.volumeRange != null;

            const float buttonWidth = 55f;
            Rect checkboxRowRect = listing.GetRect(24f);
            Rect pasteRect = new Rect(checkboxRowRect.xMax - buttonWidth, checkboxRowRect.y, buttonWidth, checkboxRowRect.height);
            Rect copyRect = new Rect(pasteRect.x - 4f - buttonWidth, checkboxRowRect.y, buttonWidth, checkboxRowRect.height);
            Rect checkboxRect = new Rect(checkboxRowRect.x, checkboxRowRect.y, copyRect.x - 8f - checkboxRowRect.x, checkboxRowRect.height);

            bool wantsOverride = hadOverride;
            Widgets.CheckboxLabeled(checkboxRect, "SPF.OverrideVolumeRange".Translate(), ref wantsOverride);

            if (wantsOverride != hadOverride)
            {
                if (wantsOverride)
                {
                    FloatRange live = _selectedSubSound.Def.volumeRange;
                    mapping.volumeRange = new SoundpackVolumeRange
                    {
                        min = Mathf.Clamp(live.min, SoundpackLimits.MinSubSoundVolumeRangePercent, SoundpackLimits.MaxSubSoundVolumeRangePercent),
                        max = Mathf.Clamp(live.max, SoundpackLimits.MinSubSoundVolumeRangePercent, SoundpackLimits.MaxSubSoundVolumeRangePercent)
                    };
                }
                else
                {
                    mapping.volumeRange = null;
                }
            }

            if (Widgets.ButtonText(copyRect, "SPF.Copy".Translate(), active: mapping.volumeRange != null))
            {
                _volumeRangeClipboard = (mapping.volumeRange.min, mapping.volumeRange.max);
            }
            if (Widgets.ButtonText(pasteRect, "SPF.Paste".Translate(), active: _volumeRangeClipboard.HasValue))
            {
                mapping.volumeRange = new SoundpackVolumeRange { min = _volumeRangeClipboard.Value.min, max = _volumeRangeClipboard.Value.max };
            }

            if (mapping.volumeRange != null)
            {
                FloatRange range = new FloatRange(mapping.volumeRange.min, mapping.volumeRange.max);
                Rect sliderRect = listing.GetRect(28f);
                Widgets.FloatRange(sliderRect, GetHashCode(), ref range,
                    SoundpackLimits.MinSubSoundVolumeRangePercent, SoundpackLimits.MaxSubSoundVolumeRangePercent,
                    null, ToStringStyle.Integer);
                mapping.volumeRange.min = range.min;
                mapping.volumeRange.max = range.max;
            }
        }

        private static bool IsFrameworkCueDef(string defName)
        {
            return defName == Soundpacks_Framework.SoundpacksFrameworkSoundDefOf.SoundpacksFramework_Startup?.defName
                || defName == Soundpacks_Framework.SoundpacksFrameworkSoundDefOf.SoundpacksFramework_Quit?.defName;
        }

        private void DrawPreviewVolumeSlider(Listing_Standard listing)
        {
            listing.Label("SPF.PreviewVolume".Translate(Mathf.RoundToInt(SoundpackSettings.editorPreviewVolume * 100f)));
            Rect row = listing.GetRect(20f);
            float newVolume = Widgets.HorizontalSlider(row, SoundpackSettings.editorPreviewVolume, 0f, MaxPreviewVolume);
            if (!Mathf.Approximately(newVolume, SoundpackSettings.editorPreviewVolume))
            {
                SoundpackSettings.editorPreviewVolume = newVolume;
                SoundpackSettingsController.Save();

                if (_previewSource != null)
                {
                    _previewSource.volume = SoundpackSettings.editorPreviewVolume;
                }
            }
        }

        private void DrawVanillaPreviewControl(Listing_Standard listing)
        {
            bool needsMap = _selectedDef.Def.HasSubSoundsInWorld;
            if (needsMap && Find.CurrentMap == null)
            {
                listing.Label("SPF.PreviewUnavailableNoMap".Translate());
                return;
            }

            if (_selectedDef.Sustain)
            {
                if (_previewSustainer == null || _previewSustainer.Ended)
                {
                    if (listing.ButtonText("SPF.PreviewPlay".Translate()))
                    {
                        StartPreviewSustainer();
                    }
                }
                else
                {
                    _previewSustainer.Maintain();
                    if (listing.ButtonText("SPF.PreviewStop".Translate()))
                    {
                        StopPreviewSustainer();
                    }
                }
            }
            else if (listing.ButtonText("SPF.PreviewVanilla".Translate()))
            {
                try
                {
                    _selectedDef.Def.PlayOneShot(BuildPreviewSoundInfo(_selectedDef.Def));
                }
                catch (Exception ex)
                {
                    _error = ex.Message;
                }
            }
        }

        private void StartPreviewSustainer()
        {
            try
            {
                _previewSustainer = _selectedDef.Def.TrySpawnSustainer(BuildPreviewSoundInfo(_selectedDef.Def));
                _previewSustainerDef = _selectedDef.Def;
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                _previewSustainer = null;
                _previewSustainerDef = null;
            }
        }

        private void StopPreviewSustainer()
        {
            if (_previewSustainer != null && !_previewSustainer.Ended)
            {
                _previewSustainer.End();
            }
            _previewSustainer = null;
            _previewSustainerDef = null;
        }

        private static SoundInfo BuildPreviewSoundInfo(SoundDef def)
        {
            SoundInfo info = def.HasSubSoundsInWorld
                ? SoundInfo.InMap(new TargetInfo(Find.CameraDriver.MapPosition, Find.CurrentMap), MaintenanceType.PerFrame)
                : SoundInfo.OnCamera(MaintenanceType.PerFrame);
            info.testPlay = true;
            info.volumeFactor = SoundpackSettings.editorPreviewVolume;
            return info;
        }

        private void DrawMappingFiles(Rect rect, SoundpackMapping mapping)
        {
            if (mapping == null || mapping.files.Count == 0)
            {
                Widgets.Label(rect, "SPF.MappingHasNoFiles".Translate());
                return;
            }

            float rowHeight = 46f;
            Rect viewRect = new Rect(0, 0, rect.width - 20f, mapping.files.Count * rowHeight + 4f);
            Widgets.BeginScrollView(rect, ref _mappingFilesScroll, viewRect);
            float y = 0f;
            SoundpackAudioFile toRemove = null;
            foreach (var file in mapping.files)
            {
                Rect row = new Rect(0, y, viewRect.width, rowHeight);
                Rect pathRect = new Rect(row.x, row.y, row.width - 80f, 22f);
                Rect previewRect = new Rect(row.xMax - 52f, row.y, 24f, 22f);
                Rect removeRect = new Rect(row.xMax - 24f, row.y, 24f, 22f);
                Widgets.Label(pathRect, file.path);
                if (Widgets.ButtonText(previewRect, ">"))
                {
                    PreviewDraftFile(file);
                }
                if (Widgets.ButtonText(removeRect, "x"))
                {
                    toRemove = file;
                }

                Rect percentRect = new Rect(row.x, row.y + 22f, 100f, 22f);
                Rect resetRect = new Rect(row.xMax - 24f, row.y + 22f, 24f, 22f);
                Rect sliderRect = new Rect(percentRect.xMax + 4f, row.y + 22f, resetRect.x - percentRect.xMax - 8f, 22f);
                float newVolume = Widgets.HorizontalSlider(sliderRect, file.volume, 0f, 2f);
                if (!Mathf.Approximately(newVolume, file.volume))
                {
                    file.volume = newVolume;
                }
                Widgets.Label(percentRect, "SPF.FileVolume".Translate(Mathf.RoundToInt(file.volume * 100f)));
                if (Widgets.ButtonText(resetRect, "SPF.Reset".Translate()))
                {
                    file.volume = 1f;
                }
                TooltipHandler.TipRegion(resetRect, "SPF.ResetVolumeTooltip".Translate());

                y += rowHeight;
            }
            Widgets.EndScrollView();

            if (toRemove != null)
            {
                mapping.files.Remove(toRemove);
                if (mapping.files.Count == 0)
                {
                    _manifest.mappings.Remove(mapping);
                }
            }
        }

        private void DrawFilesTab(Rect rect)
        {
            Rect topBar = new Rect(rect.x, rect.y, rect.width, 28f);
            if (Widgets.ButtonText(new Rect(topBar.x, topBar.y, 160f, topBar.height), "SPF.AddFilesToPool".Translate()))
            {
                OpenAudioFilePicker(paths =>
                {
                    foreach (var path in paths)
                    {
                        AddFileToDraftPool(path, out _, out string err);
                        if (err != null) _error = err;
                    }
                });
            }

            List<string> files;
            try
            {
                files = Directory.GetFiles(_draftAudioDir).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch
            {
                files = new List<string>();
            }

            Rect listRect = new Rect(rect.x, topBar.yMax + 6f, rect.width, rect.height - topBar.height - 6f);
            float rowHeight = 24f;
            Rect viewRect = new Rect(0, 0, listRect.width - 20f, files.Count * rowHeight + 4f);
            Widgets.BeginScrollView(listRect, ref _fileListScroll, viewRect);
            float y = 0f;
            string toDelete = null;
            foreach (var file in files)
            {
                string canonical = "Audio/" + Path.GetFileName(file);
                int usageCount = _manifest.mappings.SelectMany(m => m.files).Count(f => f.path == canonical);
                Rect row = new Rect(0, y, viewRect.width, rowHeight);
                Rect removeRect = new Rect(row.xMax - 24f, row.y, 24f, row.height);
                Widgets.Label(new Rect(row.x, row.y, row.width - 220f, row.height), Path.GetFileName(file));
                Widgets.Label(new Rect(row.xMax - 200f, row.y, 170f, row.height), "SPF.UsedByMappings".Translate(usageCount));
                if (Widgets.ButtonText(removeRect, "x"))
                {
                    toDelete = file;
                }
                y += rowHeight;
            }
            Widgets.EndScrollView();

            if (toDelete != null)
            {
                string canonical = "Audio/" + Path.GetFileName(toDelete);
                foreach (var mapping in _manifest.mappings)
                {
                    mapping.files.RemoveAll(f => f.path == canonical);
                }
                _manifest.mappings.RemoveAll(m => m.files.Count == 0);
                try { File.Delete(toDelete); } catch (Exception ex) { _error = ex.Message; }
            }
        }

        private void DrawBottomBar(Rect rect)
        {
            if (_error != null)
            {
                Widgets.Label(new Rect(rect.x, rect.y, rect.width - 220f, rect.height), _error);
            }

            Rect saveRect = new Rect(rect.xMax - 200f, rect.y, 95f, rect.height);
            Rect cancelRect = new Rect(rect.xMax - 100f, rect.y, 95f, rect.height);

            if (Widgets.ButtonText(saveRect, "SPF.Save".Translate()))
            {
                TrySave();
            }
            if (Widgets.ButtonText(cancelRect, "SPF.Cancel".Translate()))
            {
                Close();
            }
        }

        private void TrySave()
        {
            var structureResult = SoundpackValidator.ValidateStructure(_manifest);
            if (!structureResult.StructurallyValid)
            {
                _error = string.Join("; ", structureResult.Diagnostics.Where(d => d.IsError).Select(d => d.Message));
                return;
            }

            try
            {
                string manifestPath = Path.Combine(_draftDir, SoundpackPathPolicy.ManifestFileName);
                File.WriteAllText(manifestPath, Serialization.SoundpackManifestSerializer.SerializeCanonical(_manifest), new System.Text.UTF8Encoding(false));
                SoundpackRepository.PromoteDirectoryToPack(_draftDir, _manifest.id);
                _saved = true;
                Close();
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            DestroyPreview();
            StopPreviewSustainer();
            if (!_saved)
            {
                try { SoundpackRepository.DeleteScratchDirectory(_draftDir); } catch {  }
            }
            _onSavedOrClosed?.Invoke();
        }

        private void OpenAudioFilePicker(Action<List<string>> onSelected)
        {
            Find.WindowStack.Add(new Dialog_SoundpackFilePicker(FilePickerMode.SelectMultipleFiles,
                new[] { ".mp3", ".wav", ".ogg" }, onSelected));
        }

        private void AddFileToDraftPool(string sourcePath, out string canonicalPath, out string error)
        {
            canonicalPath = null;
            error = null;

            var info = new FileInfo(sourcePath);
            if (!info.Exists)
            {
                error = "File not found: " + sourcePath;
                return;
            }
            if (info.Length > SoundpackLimits.MaxPerAudioFileBytes)
            {
                error = "File exceeds the per-file size limit: " + info.Name;
                return;
            }

            string destName = MakeUniqueFileName(info.Name);
            string destPath = Path.Combine(_draftAudioDir, destName);
            File.Copy(sourcePath, destPath, overwrite: false);
            canonicalPath = "Audio/" + destName;
        }

        private string MakeUniqueFileName(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            string candidate = fileName;
            int suffix = 1;
            while (File.Exists(Path.Combine(_draftAudioDir, candidate)))
            {
                candidate = name + "_" + (++suffix) + ext;
            }
            return candidate;
        }

        private SoundpackMapping FindOrNull(string soundDef, string subSoundName, int subSoundIndex)
        {
            return _manifest.mappings.FirstOrDefault(m =>
                m.soundDef == soundDef && m.subSoundName == subSoundName && m.subSoundIndex == subSoundIndex);
        }

        private SoundpackMapping FindOrCreate(string soundDef, string subSoundName, int subSoundIndex)
        {
            var existing = FindOrNull(soundDef, subSoundName, subSoundIndex);
            if (existing != null) return existing;

            var mapping = new SoundpackMapping
            {
                soundDef = soundDef,
                subSoundName = subSoundName,
                subSoundIndex = subSoundIndex,
                expectedSourcePackageId = _selectedDef?.SourcePackageId
            };
            _manifest.mappings.Add(mapping);
            return mapping;
        }

        private void PreviewDraftFile(SoundpackAudioFile file)
        {
            _error = null;
            if (!SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath(file.path, out string canonical, out _))
            {
                _error = "Invalid file path: " + file.path;
                return;
            }
            string absolute = Path.Combine(_draftDir, canonical.Replace('/', Path.DirectorySeparatorChar));
            var decodeResult = AudioDecodeService.Decode(absolute);
            if (!decodeResult.Success)
            {
                _error = decodeResult.Error;
                return;
            }

            DestroyPreview();
            _previewClip = AudioClip.Create("SPF_Preview", decodeResult.Audio.FrameCount, decodeResult.Audio.Channels, decodeResult.Audio.SampleRate, false);
            _previewClip.SetData(RuntimeClipFactory.ApplyGain(decodeResult.Audio.Samples, file.volume), 0);

            _previewObject = new GameObject("SPF_EditorPreview");
            _previewSource = _previewObject.AddComponent<AudioSource>();
            _previewSource.clip = _previewClip;
            _previewSource.spatialBlend = 0f;
            _previewSource.volume = SoundpackSettings.editorPreviewVolume;
            _previewSource.Play();
        }

        private void DestroyPreview()
        {
            if (_previewObject != null)
            {
                UnityEngine.Object.Destroy(_previewObject);
                _previewObject = null;
                _previewSource = null;
            }
            if (_previewClip != null)
            {
                UnityEngine.Object.Destroy(_previewClip);
                _previewClip = null;
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }
    }
}
