using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Soundpacks_Framework.Archive;
using Soundpacks_Framework.Discovery;
using Soundpacks_Framework.Models;
using Soundpacks_Framework.Settings;
using Soundpacks_Framework.Storage;
using UnityEngine;
using Verse;

namespace Soundpacks_Framework.UI
{
    public sealed class Dialog_SoundpackManager : Window
    {
        private List<SoundpackDescriptor> _descriptors = new List<SoundpackDescriptor>();
        private SoundpackDescriptor _selected;
        private readonly QuickSearchWidget _search = new QuickSearchWidget();
        private Vector2 _listScroll, _diagScroll;
        private string _statusMessage;

        public override Vector2 InitialSize => new Vector2(880f, 620f);

        public Dialog_SoundpackManager()
        {
            resizeable = true;
            draggable = true;
            doCloseX = true;
            onlyOneOfTypeAllowed = true;
            Refresh();
        }

        private void Refresh()
        {
            _descriptors = SoundpackDiscoveryService.DiscoverAll()
                .OrderBy(d => d.Source)
                .ThenBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (_selected != null)
            {
                _selected = _descriptors.FirstOrDefault(d => d.Id == _selected.Id && d.Source == _selected.Source);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect header = new Rect(inRect.x, inRect.y, inRect.width, 30f);
            DrawHeaderButtons(header);

            Rect body = new Rect(inRect.x, header.yMax + 8f, inRect.width, inRect.height - header.height - 60f);
            Rect leftRect = new Rect(body.x, body.y, body.width * 0.42f, body.height);
            Rect rightRect = new Rect(leftRect.xMax + 8f, body.y, body.width - leftRect.width - 8f, body.height);

            Rect searchRect = new Rect(leftRect.x, leftRect.y, leftRect.width, 28f);
            _search.OnGUI(searchRect);
            Rect listRect = new Rect(leftRect.x, searchRect.yMax + 4f, leftRect.width, leftRect.height - searchRect.height - 4f);
            DrawPackList(listRect);

            DrawDetailPane(rightRect);

            Rect footer = new Rect(inRect.x, inRect.yMax - 40f, inRect.width, 40f);
            DrawFooter(footer);
        }

        private void DrawHeaderButtons(Rect rect)
        {
            const float w = 110f;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, w, rect.height), "SPF.New".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SoundpackEditor(null, false, Refresh));
            }
            if (Widgets.ButtonText(new Rect(rect.x + w + 4f, rect.y, w, rect.height), "SPF.Import".Translate()))
            {
                ImportFlow();
            }
            if (Widgets.ButtonText(new Rect(rect.x + (w + 4f) * 2f, rect.y, w, rect.height), "SPF.Refresh".Translate()))
            {
                Refresh();
            }
        }

        private void DrawPackList(Rect outRect)
        {
            var filtered = _descriptors.Where(MatchesSearch).ToList();
            float rowHeight = 44f;
            Rect viewRect = new Rect(0, 0, outRect.width - 20f, filtered.Count * rowHeight + 4f);
            Widgets.BeginScrollView(outRect, ref _listScroll, viewRect);
            float y = 0f;
            foreach (var descriptor in filtered)
            {
                DrawPackRow(new Rect(0, y, viewRect.width, rowHeight - 2f), descriptor);
                y += rowHeight;
            }
            Widgets.EndScrollView();
        }

        private bool MatchesSearch(SoundpackDescriptor d)
        {
            if (!_search.filter.Active) return true;
            return _search.filter.Matches(d.DisplayName) || _search.filter.Matches(d.Id) ||
                   _search.filter.Matches(d.SourceModName ?? "");
        }

        private void DrawPackRow(Rect row, SoundpackDescriptor descriptor)
        {
            if (_selected == descriptor)
            {
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));
            }

            string badge = descriptor.Source == SoundpackSource.User ? "[User]" : (descriptor.ReadOnly ? "[Mod-RO]" : "[Mod]");
            string status = !descriptor.LoadSucceeded ? " - invalid" : descriptor.SuppressedByConflict ? " - conflict" : "";
            string active = SoundpackSettings.activePackId == descriptor.Id ? "  *ACTIVE*" : "";
            string label = badge + " " + descriptor.DisplayName + status + active;

            if (Widgets.ButtonText(row, label, drawBackground: false))
            {
                _selected = descriptor;
            }
        }

        private void DrawDetailPane(Rect rect)
        {
            if (_selected == null)
            {
                Widgets.Label(rect, "SPF.NoPackSelected".Translate());
                return;
            }

            var listing = new Listing_Standard();
            listing.Begin(new Rect(rect.x, rect.y, rect.width, 150f));
            listing.Label(_selected.DisplayName);
            listing.Label("SPF.FieldId".Translate() + ": " + _selected.Id);
            if (_selected.Manifest != null)
            {
                listing.Label("SPF.FieldAuthor".Translate() + ": " + _selected.Manifest.author);
                listing.Label("SPF.FieldVersion".Translate() + ": " + _selected.Manifest.version);
                listing.Label("SPF.MappingCount".Translate(_selected.Manifest.mappings.Count));
            }
            if (_selected.Source == SoundpackSource.Mod)
            {
                listing.Label("SPF.FieldSourceMod".Translate(_selected.SourceModName ?? "?", _selected.SourcePackageId ?? "?"));
            }
            listing.End();

            Rect diagRect = new Rect(rect.x, rect.y + 156f, rect.width, rect.height - 156f - 100f);
            DrawDiagnostics(diagRect);

            Rect actionsRect = new Rect(rect.x, rect.yMax - 96f, rect.width, 96f);
            DrawActions(actionsRect);
        }

        private void DrawDiagnostics(Rect rect)
        {
            var diagnostics = _selected.Diagnostics;
            if (diagnostics.Count == 0)
            {
                Widgets.Label(rect, "SPF.NoDiagnostics".Translate());
                return;
            }

            float rowHeight = 20f;
            Rect viewRect = new Rect(0, 0, rect.width - 20f, diagnostics.Count * rowHeight + 4f);
            Widgets.BeginScrollView(rect, ref _diagScroll, viewRect);
            float y = 0f;
            foreach (var diagnostic in diagnostics)
            {
                Widgets.Label(new Rect(0, y, viewRect.width, rowHeight), diagnostic.ToString());
                y += rowHeight;
            }
            Widgets.EndScrollView();
        }

        private void DrawActions(Rect rect)
        {
            const float w = 130f;
            float x = rect.x;
            bool canSelect = _selected.IsSelectable;

            if (Widgets.ButtonText(new Rect(x, rect.y, w, 28f), "SPF.SelectPack".Translate(), active: canSelect))
            {
                SoundpackSettings.activePackId = _selected.Id;
                SoundpackSettingsController.Save();
            }
            x += w + 4f;
            if (Widgets.ButtonText(new Rect(x, rect.y, w, 28f), "SPF.SelectNone".Translate()))
            {
                SoundpackSettings.activePackId = "";
                SoundpackSettingsController.Save();
            }

            x = rect.x;
            float y2 = rect.y + 32f;

            if (!_selected.ReadOnly)
            {
                if (Widgets.ButtonText(new Rect(x, y2, w, 28f), "SPF.Edit".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_SoundpackEditor(_selected, false, Refresh));
                }
                x += w + 4f;
            }
            if (Widgets.ButtonText(new Rect(x, y2, w, 28f), "SPF.Duplicate".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SoundpackEditor(_selected, true, Refresh));
            }
            x += w + 4f;
            if (Widgets.ButtonText(new Rect(x, y2, w, 28f), "SPF.Export".Translate()))
            {
                ExportFlow(_selected);
            }

            if (!_selected.ReadOnly)
            {
                Rect deleteRect = new Rect(rect.x, y2 + 32f, w, 28f);
                if (Widgets.ButtonText(deleteRect, "SPF.Delete".Translate()))
                {
                    string idToDelete = _selected.Id;
                    string displayName = _selected.DisplayName;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "SPF.ConfirmDelete".Translate(displayName),
                        delegate
                        {
                            SoundpackRepository.DeletePack(idToDelete);
                            if (SoundpackSettings.activePackId == idToDelete)
                            {
                                SoundpackSettings.activePackId = "";
                                SoundpackSettingsController.Save();
                            }
                            _selected = null;
                            Refresh();
                        },
                        destructive: true));
                }
            }
        }

        private void DrawFooter(Rect rect)
        {
            if (SoundpackSettings.RestartRequired)
            {
                Widgets.Label(rect, "SPF.RestartRequired".Translate());
            }
            else if (!string.IsNullOrEmpty(_statusMessage))
            {
                Widgets.Label(rect, _statusMessage);
            }
        }

        private void ImportFlow()
        {
            Find.WindowStack.Add(new Dialog_SoundpackFilePicker(FilePickerMode.SelectExistingFile, new[] { ".zip" }, paths =>
            {
                string zipPath = paths.FirstOrDefault();
                if (zipPath == null) return;

                var staged = SoundpackArchiveService.StageImport(zipPath);
                if (!staged.Success)
                {
                    _statusMessage = string.Join("; ", staged.Diagnostics.Select(d => d.Message));
                    return;
                }

                if (staged.IdAlreadyInstalled)
                {
                    Find.WindowStack.Add(new Dialog_SoundpackConflict(staged.Manifest.id, resolution => CommitStaged(staged, resolution)));
                }
                else
                {
                    CommitStaged(staged, ImportConflictResolution.Replace);
                }
            }));
        }

        private void CommitStaged(ImportStageResult staged, ImportConflictResolution resolution)
        {
            if (SoundpackArchiveService.CommitImport(staged, resolution, out string finalId, out string error))
            {
                _statusMessage = finalId != null ? "SPF.ImportSucceeded".Translate(finalId) : "SPF.ImportCancelled".Translate();
                Refresh();
            }
            else
            {
                _statusMessage = error;
            }
        }

        private void ExportFlow(SoundpackDescriptor descriptor)
        {
            if (descriptor.ReadOnly)
            {
                _statusMessage = "SPF.ExportReadOnlyBlocked".Translate();
                return;
            }

            string defaultName = (descriptor.Id ?? "soundpack") + ".zip";
            Find.WindowStack.Add(new Dialog_SoundpackFilePicker(FilePickerMode.SelectDestinationFilePath, Array.Empty<string>(), paths =>
            {
                string destination = paths.FirstOrDefault();
                if (destination == null) return;

                Action doExport = () =>
                {
                    bool ok = SoundpackArchiveService.Export(descriptor.Manifest, descriptor.DirectoryPath, destination, out var diagnostics);
                    if (ok)
                    {
                        _statusMessage = diagnostics.Count == 0
                            ? "SPF.ExportSucceeded".Translate(destination).ToString()
                            : "SPF.ExportSucceededWithWarnings".Translate(destination).ToString() + " " + string.Join("; ", diagnostics.Select(d => d.Message));
                    }
                    else
                    {
                        _statusMessage = string.Join("; ", diagnostics.Select(d => d.Message));
                    }
                };

                if (File.Exists(destination))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("SPF.ConfirmOverwrite".Translate(destination), doExport, destructive: true));
                }
                else
                {
                    doExport();
                }
            }, defaultName));
        }
    }
}
