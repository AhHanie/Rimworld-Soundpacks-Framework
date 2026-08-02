using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace Soundpacks_Framework.UI
{
    public enum FilePickerMode
    {
        SelectExistingFile,
        SelectMultipleFiles,
        SelectDestinationFilePath,
        SelectDirectory
    }

    public sealed class Dialog_SoundpackFilePicker : Window
    {
        private readonly FilePickerMode _mode;
        private readonly string[] _allowedExtensions;
        private readonly Action<List<string>> _onConfirmed;
        private readonly string _defaultFileName;

        private string _currentDir;
        private string _typedPath = "";
        private string _typedFileName;
        private Vector2 _scroll;
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _error;

        public Dialog_SoundpackFilePicker(FilePickerMode mode, string[] allowedExtensions, Action<List<string>> onConfirmed, string defaultFileName = null)
        {
            _mode = mode;
            _allowedExtensions = allowedExtensions ?? Array.Empty<string>();
            _onConfirmed = onConfirmed;
            _defaultFileName = defaultFileName;
            _typedFileName = defaultFileName ?? "";

            string startDir = SoundpackSettings.lastPickerDirectory;
            _currentDir = !string.IsNullOrEmpty(startDir) && Directory.Exists(startDir)
                ? startDir
                : GenFilePaths.SaveDataFolderPath;

            forcePause = true;
            doCloseX = true;
            resizeable = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(640f, 540f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("SPF.PickerCurrentDir".Translate(_currentDir));

            Rect pathRow = listing.GetRect(28f);
            Rect goButtonRect = new Rect(pathRow.xMax - 60f, pathRow.y, 60f, pathRow.height);
            Rect typedRect = new Rect(pathRow.x, pathRow.y, pathRow.width - 68f, pathRow.height);
            _typedPath = Widgets.TextField(typedRect, _typedPath);
            if (Widgets.ButtonText(goButtonRect, "SPF.PickerGo".Translate()))
            {
                TryNavigateTyped();
            }
            listing.Gap(4f);

            if (Widgets.ButtonText(listing.GetRect(24f), "..", drawBackground: true))
            {
                string parent = Directory.GetParent(_currentDir)?.FullName;
                if (parent != null) _currentDir = parent;
            }

            listing.End();

            Rect listOuter = new Rect(inRect.x, inRect.y + 120f, inRect.width, inRect.height - 200f);
            DrawEntries(listOuter);

            Rect bottom = new Rect(inRect.x, inRect.yMax - 70f, inRect.width, 70f);
            DrawBottomBar(bottom);
        }

        private void DrawEntries(Rect outRect)
        {
            List<string> directories;
            List<string> files;
            try
            {
                directories = Directory.GetDirectories(_currentDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
                files = Directory.GetFiles(_currentDir)
                    .Where(IsAllowedFile)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                _error = null;
            }
            catch (Exception ex)
            {
                directories = new List<string>();
                files = new List<string>();
                _error = ex.Message;
            }

            float rowHeight = 26f;
            float viewHeight = (directories.Count + files.Count) * rowHeight + 10f;
            Rect viewRect = new Rect(0, 0, outRect.width - 20f, Mathf.Max(viewHeight, outRect.height));

            bool confirmAfterScrollView = false;

            Widgets.BeginScrollView(outRect, ref _scroll, viewRect);
            float y = 0f;

            foreach (var dir in directories)
            {
                Rect row = new Rect(0, y, viewRect.width, rowHeight);
                if (Widgets.ButtonText(row, "[Dir] " + Path.GetFileName(dir), drawBackground: false))
                {
                    _currentDir = dir;
                }
                y += rowHeight;
            }

            foreach (var file in files)
            {
                Rect row = new Rect(0, y, viewRect.width, rowHeight);
                string name = Path.GetFileName(file);
                if (_mode == FilePickerMode.SelectMultipleFiles)
                {
                    bool isSelected = _selected.Contains(file);
                    Widgets.CheckboxLabeled(row, name, ref isSelected);
                    if (isSelected) _selected.Add(file); else _selected.Remove(file);
                }
                else if (Widgets.ButtonText(row, name, drawBackground: false))
                {
                    _selected.Clear();
                    _selected.Add(file);
                    if (_mode == FilePickerMode.SelectExistingFile)
                    {
                        confirmAfterScrollView = true;
                    }
                }
                y += rowHeight;
            }

            Widgets.EndScrollView();

            if (confirmAfterScrollView)
            {
                Confirm();
                return;
            }

            if (_error != null)
            {
                Widgets.Label(new Rect(outRect.x, outRect.yMax + 2f, outRect.width, 20f), "SPF.PickerError".Translate(_error));
            }
        }

        private void DrawBottomBar(Rect rect)
        {
            if (_mode == FilePickerMode.SelectDestinationFilePath || _mode == FilePickerMode.SelectDirectory)
            {
                Rect nameRow = new Rect(rect.x, rect.y, rect.width, 28f);
                if (_mode == FilePickerMode.SelectDestinationFilePath)
                {
                    _typedFileName = Widgets.TextField(nameRow, _typedFileName);
                }
            }

            Rect buttonRow = new Rect(rect.x, rect.yMax - 32f, rect.width, 32f);
            Rect confirmRect = new Rect(buttonRow.xMax - 200f, buttonRow.y, 95f, buttonRow.height);
            Rect cancelRect = new Rect(buttonRow.xMax - 100f, buttonRow.y, 95f, buttonRow.height);

            if (Widgets.ButtonText(confirmRect, "SPF.PickerConfirm".Translate()))
            {
                Confirm();
            }
            if (Widgets.ButtonText(cancelRect, "SPF.PickerCancel".Translate()))
            {
                Close();
            }
        }

        private bool IsAllowedFile(string path)
        {
            if (_allowedExtensions.Length == 0) return true;
            string ext = Path.GetExtension(path);
            return _allowedExtensions.Any(a => string.Equals(a, ext, StringComparison.OrdinalIgnoreCase));
        }

        private void TryNavigateTyped()
        {
            if (string.IsNullOrWhiteSpace(_typedPath)) return;
            try
            {
                string full = Path.GetFullPath(_typedPath);
                if (Directory.Exists(full))
                {
                    _currentDir = full;
                    _error = null;
                }
                else
                {
                    _error = "Path does not exist: " + full;
                }
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }

        private void Confirm()
        {
            SoundpackSettings.lastPickerDirectory = _currentDir;

            List<string> result;
            switch (_mode)
            {
                case FilePickerMode.SelectDestinationFilePath:
                    string fileName = string.IsNullOrWhiteSpace(_typedFileName) ? (_defaultFileName ?? "export.zip") : _typedFileName;
                    result = new List<string> { Path.Combine(_currentDir, fileName) };
                    break;
                case FilePickerMode.SelectDirectory:
                    result = new List<string> { _currentDir };
                    break;
                default:
                    result = _selected.ToList();
                    break;
            }

            if (result.Count == 0)
            {
                _error = "SPF.PickerNothingSelected".Translate();
                return;
            }

            _onConfirmed?.Invoke(result);
            Close();
        }
    }
}
