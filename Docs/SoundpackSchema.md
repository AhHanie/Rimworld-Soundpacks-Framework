# Soundpack archive format (schemaVersion 1)

A soundpack is a `.zip` archive containing exactly one root `soundpack.json` and zero or more
audio files under `Audio/`.

```text
MySoundpack.zip
├── soundpack.json
└── Audio/
    ├── combat_rifle_01.mp3
    ├── combat_rifle_02.mp3
    └── research_complete.mp3
```

## Archive rules

- Entry names use `/` as the separator, are UTF-8, relative, and case-sensitive as written.
- Only two kinds of entries are accepted: the root `soundpack.json`, and files under `Audio/`
  with extension `.mp3`, `.wav`, or `.ogg`. Anything else (other folders, other extensions,
  executables, symlinks) is rejected and the whole archive fails to import.
- Two entries whose canonical paths differ only by case (e.g. `Audio/Foo.mp3` vs `Audio/foo.mp3`)
  are rejected, so packs behave the same on case-sensitive and case-insensitive filesystems.
- Path traversal (`..`), absolute paths, and drive prefixes are rejected.

## v1 limits

| Limit | Value |
|---|---|
| Archive entries | 2,000 |
| Total uncompressed size | 512 MiB |
| Total compressed size | 128 MiB |
| Per audio file | 64 MiB |
| Manifest (`soundpack.json`) | 100 MiB |
| Compression ratio | 100:1 |
| Clip duration | 120 seconds |
| Decoded PCM per clip | 64 MiB |
| Sample rate | 8,000–48,000 Hz |
| Channels | mono or stereo |

These are fixed for v1; raising them is a post-release decision, not a per-pack setting.

## `soundpack.json`

```json
{
  "schemaVersion": 1,
  "id": "com.example.rifle-overhaul",
  "name": "Rifle Overhaul",
  "author": "Example Author",
  "description": "Sharper rifle reports.",
  "version": "1.0.0",
  "supportedRimWorldVersions": ["1.6"],
  "attribution": "Audio licensed CC BY 4.0",
  "license": "CC-BY-4.0",
  "mappings": [
    {
      "soundDef": "Gun_ShootRifle",
      "expectedSourcePackageId": "ludeon.rimworld",
      "subSoundName": "Gun_ShootRifle_0",
      "subSoundIndex": 0,
      "volumeRange": { "min": 40, "max": 70 },
      "files": [
        { "path": "Audio/combat_rifle_01.mp3", "volume": 0.5 },
        { "path": "Audio/combat_rifle_02.mp3", "volume": 2.0 }
      ]
    }
  ]
}
```

### Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `schemaVersion` | int | yes | Must be `1`. A version newer than what your installed framework supports is read-only and cannot activate. |
| `id` | string | yes | Lower-case reverse-DNS or GUID-like identifier (`^[a-z0-9]+(?:[._-][a-z0-9]+)*$`). This is also the pack's folder name once installed. |
| `name` | string | yes | Display name. |
| `author`, `description`, `version`, `attribution`, `license` | string | no | Display-only metadata. |
| `supportedRimWorldVersions` | string[] | no | Informational; mismatches warn but do not block activation. |
| `mappings` | array | yes (may be empty) | See below. |

### Mapping object

| Field | Type | Required | Notes |
|---|---|---|---|
| `soundDef` | string | yes | Target `SoundDef.defName`, resolved against the final `DefDatabase<SoundDef>` after all mods and patches load. |
| `expectedSourcePackageId` | string | no | Compatibility assertion / display aid. If the def's winning source mod differs at activation time, activation warns but still proceeds. |
| `subSoundName` | string | yes | The target sub-sound's name (auto-named `<defName>_<index>` by RimWorld if the XML didn't name it). |
| `subSoundIndex` | int | yes | The sub-sound's index within its parent `SoundDef.subSounds`. Used as the primary match; `subSoundName` is cross-checked. |
| `files` | array of file objects | yes, at least one | The random pool for this sub-sound. A mapping with zero files is invalid and is rejected outright: it can never fall back to silence. |
| `volumeRange` | object (`{ "min": number, "max": number }`) | no | Overrides the target `SubSoundDef`'s native `volumeRange`, RimWorld's own "play at a random volume inside this range" field. Expressed on RimWorld's own `0`-`100` percent scale, not the `0.0`-`2.0` linear scale used by the per-file `volume` field below: `volumeRange` is set directly onto RimWorld's own field, so it reuses RimWorld's own authoring convention instead of inventing a new one. `min` must be `<=` `max`, and both must be within `0` through `100` inclusive. Omitted means "do not touch this target's `volumeRange` at all", not "reset it to some default"; there is no single meaningful default value to fall back to, since almost every real `SoundDef` overrides RimWorld's own structural default of `(50, 50)`. |

### File object

| Field | Type | Required | Notes |
|---|---|---|---|
| `path` | string | yes | Archive-relative path under `Audio/`. |
| `volume` | number | no | Linear amplitude multiplier for this file, not a percentage: `0.5`, `1.0`, and `2.0` mean 50%, 100%, and 200%. Valid range is `0.0` through `2.0` inclusive. Omitted, it defaults to `1.0`, so packs written before this field existed are unaffected. Only the framework-owned runtime clip's sample amplitude changes; the target `SubSoundDef`'s volume range, filters, distance behavior, parameter mappings, and user volume preferences all continue to apply normally on top of it. Boosting past `1.0` can drive decoded peaks above what the format supports; the framework clamps the result, so a poorly mastered source may audibly clip at `2.0`, use a quieter source file if that happens. The same file referenced at different `volume` values (e.g. once at `0.5` and once at `2.0`) produces separate runtime clip variants rather than sharing one. |

`volumeRange` and a file's own `volume` compose: `volumeRange` scales/randomizes the whole pool
the way vanilla always has, and a file's own `volume` can still make that one file louder or
quieter than its pool-mates on top of that. They are entirely independent controls, one is
baked into the decoded PCM samples of the framework-owned clip, the other is set on RimWorld's
own `SubSoundDef` field and sampled once per played sample the same way vanilla's own random
volume already works.

A `SoundDef` with exactly one sub-sound may be mapped by targeting its single sub-sound
(`subSoundIndex: 0`), this is the "whole-def" case in practice. A `SoundDef` with multiple
sub-sounds must be mapped per sub-sound; mapping only one sub-sound leaves the others playing
their original audio.

Duplicate mappings that target the exact same `(soundDef, subSoundName, subSoundIndex)` are
rejected: they block both export and activation.

Unknown top-level or per-mapping/per-file fields are preserved verbatim on re-save (not
discarded), so forward-compatible or tool-authored extensions round-trip safely.

## What a mapping changes, and what it never changes

A mapping replaces **only** the resolved audio variants of the targeted `SubSoundDef`, and,
when `volumeRange` is explicitly present, that one `SubSoundDef` property. Every other property
of that sub-sound (pitch range, distance range, filters, parameter mappings, start delay, sustain
loop/attack/release behavior) and every property of its parent `SoundDef` (context, voice limits,
priority, sustain start/stop sounds) are completely untouched. A file's optional `volume` only
scales that framework-owned clip's sample amplitude before it is handed to RimWorld; it is not a
substitute for, and does not modify, any of the `SubSoundDef`/`SoundDef` properties above.

## Framework startup and quit cues

Soundpacks Framework ships two of its own `SoundDef`s that a mapping can target exactly like any
vanilla or mod-added `SoundDef`. No manifest field enables this feature: mapping the def is the
only opt-in mechanism, and an unmapped cue stays silent.

| `SoundDef.defName` | Sub-sound name | `subSoundIndex` | Fires |
|---|---|---|---|
| `SoundpacksFramework_Startup` | `SoundpacksFramework_Startup_0` | `0` | Once, after RimWorld has finished loading and the interface has become interactive. |
| `SoundpacksFramework_Quit` | `SoundpacksFramework_Quit_0` | `0` | Once, when the player uses RimWorld's normal Quit to OS flow (including Save and Quit to OS), immediately before RimWorld's original shutdown runs. Direct/forced process termination and RimWorld's Restart action do not trigger it. |

Both defs ship with a single silent, zero-duration grain, so they produce no audio and no
config error by default. Mapping `files` for the def's one sub-sound (normal `.mp3`/`.wav`/`.ogg`
rules apply, and a mapping with zero files is rejected the same as for any other target) is the
only way to make either cue audible:

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

```json
{
  "soundDef": "SoundpacksFramework_Quit",
  "subSoundName": "SoundpacksFramework_Quit_0",
  "subSoundIndex": 0,
  "files": [
    { "path": "Audio/quit_jingle.mp3" }
  ]
}
```

Only the selected pack's mapping can make a cue audible, since the runtime only installs
mapping grains for the pack the player has selected.

## Activation semantics

- Selecting a pack requires a RimWorld restart to take effect; it never interrupts audio that is
  already playing.
- If a mapping's target `SoundDef`/sub-sound no longer exists, or none of its files can be
  decoded, that one mapping is disabled and the target keeps its original (vanilla or
  mod-provided) audio, including its original `volumeRange` if the mapping requested an
  override: the override is never applied partially. It never breaks other mappings or
  unrelated sounds.
- A `volumeRange` override, like the swapped audio variants themselves, is snapshotted before
  being applied and restored to its original value at teardown.
- Files referenced by more than one mapping are decoded once and share a single `AudioClip`,
  deduplicated by SHA-256 content hash plus `volume`: the same file at the same `volume` shares
  one clip, while the same file at two different `volume` values gets a separate clip per value.
