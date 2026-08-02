# Shipping a soundpack from your mod

Soundpacks Framework lets a mod ship a read-only, discoverable soundpack without writing any
C# code, by placing a soundpack folder (in the same layout as an exported `.zip`, just
unzipped) under your mod's `Soundpacks/` folder.

```text
YourMod/
├── About/
│   └── About.xml
└── Soundpacks/
    └── your-pack-id/
        ├── soundpack.json
        └── Audio/
            └── ...
```

`Soundpacks/` follows the same content folder RimWorld selects for the rest of your mod: it can
sit at your mod's root (the same level as `Defs/`, `Textures/`, etc.), inside a versioned folder
such as `1.6/`, inside `Common/`, or inside any folder your mod's own `LoadFolders.xml` selects.
RimWorld resolves that folder list for you; Soundpacks Framework looks for a `Soundpacks/`
directory in each of those selected content roots, highest-priority first. A soundpack-only mod
needs nothing else: `About/` and `Soundpacks/` (wherever it lives) are enough.

With Soundpacks Framework loaded, a mod counts as having loaded content if any of its selected
content folders has a `Soundpacks/` directory with at least one direct pack directory containing
`soundpack.json`, so RimWorld's "did not load any content" warning is suppressed for it. Don't add
a dummy `Defs/` entry, texture, audio clip, or translation file merely to silence that warning: it
isn't needed. An empty `Soundpacks/` directory, or one whose pack folder has no `soundpack.json`,
is not recognized as content and will still produce RimWorld's normal warning.

### Layout options

If you don't ship a `LoadFolders.xml`, RimWorld's automatic fallback applies: the exact current
version folder if present (e.g. `1.6/`), otherwise the nearest supported lower version folder,
then `Common/`, then your mod root. A root-level `Soundpacks/` folder is valid under this
fallback and covers every version that has no more specific folder:

```text
YourMod/
├── About/
├── 1.6/
│   └── Soundpacks/
│       └── your-pack-id/
│           ├── soundpack.json
│           └── Audio/
└── Soundpacks/
    └── shared-pack-id/
        ├── soundpack.json
        └── Audio/
```

Here the `1.6/Soundpacks/your-pack-id` pack is used on RimWorld 1.6; the root-level
`Soundpacks/shared-pack-id` pack is a lower-priority fallback for other versions (or for
`your-pack-id` itself, if the same id also existed at the root: see "Same id in more than one
folder" below).

If you ship a `LoadFolders.xml`, only the folders it selects for the running game version are
considered, in the resolved order (later `<li>` entries are higher priority). Include a `/`
(or `\`) entry if you want your root-level `Soundpacks/` folder to still be included:

```xml
<loadFolders>
  <v1.6>
    <li>/</li>
    <li>Common</li>
    <li>1.6</li>
  </v1.6>
</loadFolders>
```

This resolves to `1.6/`, then `Common/`, then the mod root, in that priority order. A soundpack
placed under any of those folders is discoverable. If the `/` entry were omitted, a root-level
`Soundpacks/` folder would not be discovered for that version, and would not suppress the
no-content warning either.

### Same id in more than one folder

If your mod supplies the same pack id from more than one of its own selected content folders
(for example, a version-specific override and a root-level fallback), only the copy from the
highest-priority folder is used: its manifest and `Audio/` files are read as a self-contained
unit. There is no merging of files or manifest fields across folders, so a version-specific
pack should be complete on its own rather than relying on the root-level copy to fill in the
rest.

## Requirements

1. **Depend on Soundpacks Framework.** Add it to your `About/About.xml` `<modDependencies>` the
   same way you'd depend on Harmony, and make sure it loads before your mod (a `<loadAfter>`
   entry for `sk.soundpacks` is recommended if load order matters for your pack's targets).
2. **Write a valid `soundpack.json`.** See `Docs/SoundpackSchema.md` for the full schema. Your
   pack's `id` becomes its folder name under `Soundpacks/`: they must match.
3. **Validate before you ship.** Open the Soundpack Manager in Mod Settings with your mod
   active; your pack should appear as a read-only, mod-provided pack with no error-level
   diagnostics. Warnings (e.g. a source-package mismatch) are fine if you understand why.

## What players can do with your pack

- Select it like any other installed pack (applies after a restart).
- **Duplicate** it into an editable user copy, then edit or export that copy. Your original
  mod-provided pack is never modified, duplication always writes into the player's own
  save-data folder, never back into your mod's install directory.
- They cannot rename, edit, or delete your pack in place: those actions are disabled for
  read-only/mod-provided packs.

## Targeting mods (yours or others')

Mapping targets are resolved against the **final** `DefDatabase<SoundDef>`, after every active
mod's XML and patches have loaded, the same load order the game itself uses. This means:

- You can target `SoundDef`s added or patched by other mods, not just vanilla ones.
- If two mods define a `SoundDef` with the same `defName`, whichever one wins RimWorld's normal
  load-order resolution is the one your mapping affects. Set `expectedSourcePackageId` to the
  package ID you expect to win; if load order changes and a different mod's def wins instead,
  activation emits a warning (not an error) so the mismatch is visible without silently
  breaking.
- If your mod is itself the source of the target `SoundDef`, that's fine: mod-defined defs
  work exactly like vanilla ones as mapping targets.

## Startup and quit cues

Soundpacks Framework ships two of its own framework-owned `SoundDef`s, `SoundpacksFramework_Startup`
and `SoundpacksFramework_Quit`, which mod-provided and user packs can target the same way as any
other `SoundDef`. See `Docs/SoundpackSchema.md` for the exact sub-sound names/indexes and timing.

```json
{
  "soundDef": "SoundpacksFramework_Startup",
  "subSoundName": "SoundpacksFramework_Startup_0",
  "subSoundIndex": 0,
  "files": [
    { "path": "Audio/startup_jingle.mp3" }
  ]
}
```

A pack that doesn't map one of these cues leaves it silent, there is no default sound and no
config error either way.

## Conflicts between soundpacks

If two active mods ship packs with the same `id`, RimWorld's load order decides which one wins
for that ID: whichever mod is later in the active mod list. The losing pack still shows up in
the manager, marked as suppressed with a diagnostic explaining the conflict, and its files are
never touched. If a player's own installed pack shares an ID with a mod-provided pack, the
player's pack always takes precedence for that ID.

## Common mistakes to avoid

- **A mapping with zero files.** This is rejected outright, not treated as "silence": always
  ship at least one file per mapped sub-sound.
- **Targeting the wrong sub-sound.** A `SoundDef` with multiple sub-sounds (e.g. separate
  impact and tail layers) needs one mapping per sub-sound you actually want to replace. Mapping
  the whole def as if it had one sub-sound will silently only affect index 0.
- **Shipping unsupported audio.** Only `.mp3`, `.wav`, and `.ogg` are accepted; mono or stereo,
  8,000–48,000 Hz, ≤120 seconds, ≤64 MiB decoded PCM per clip.
- **Expecting immediate switching.** Activation only happens once, during startup, before the
  main menu; there is no in-session pack switching in v1.
