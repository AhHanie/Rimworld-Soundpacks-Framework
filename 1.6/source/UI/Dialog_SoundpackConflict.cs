using System;
using Soundpacks_Framework.Archive;
using UnityEngine;
using Verse;

namespace Soundpacks_Framework.UI
{
    public sealed class Dialog_SoundpackConflict : Window
    {
        private readonly string _packId;
        private readonly Action<ImportConflictResolution> _onResolved;
        private bool _resolved;

        public Dialog_SoundpackConflict(string packId, Action<ImportConflictResolution> onResolved)
        {
            _packId = packId;
            _onResolved = onResolved;
            forcePause = true;
            doCloseX = false;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = false;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(460f, 220f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("SPF.ConflictTitle".Translate(_packId));
            listing.Gap(12f);

            if (listing.ButtonText("SPF.ConflictReplace".Translate()))
            {
                Resolve(ImportConflictResolution.Replace);
            }
            listing.Gap(4f);
            if (listing.ButtonText("SPF.ConflictDuplicate".Translate()))
            {
                Resolve(ImportConflictResolution.Duplicate);
            }
            listing.Gap(4f);
            if (listing.ButtonText("SPF.ConflictCancel".Translate()))
            {
                Resolve(ImportConflictResolution.Cancel);
            }

            listing.End();
        }

        public override void PreClose()
        {
            base.PreClose();
            if (!_resolved)
            {
                Resolve(ImportConflictResolution.Cancel);
            }
        }

        private void Resolve(ImportConflictResolution resolution)
        {
            if (_resolved) return;
            _resolved = true;
            _onResolved?.Invoke(resolution);
            Close();
        }
    }
}
