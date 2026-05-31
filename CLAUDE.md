# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Role

`midiLib` is the C# MIDI parser and data model for **Visual Music**. It is a git submodule (separate repo:
`yousernaym/midiLib`) consumed by the app as a `ProjectReference`. It is the canonical note model for the
whole app — MOD/SID files are first converted to MIDI by Remuxer, then parsed here.

- Output: class library `midilib.dll`, root namespace `Midi`.
- Target framework: **.NET Framework 4.8** (`v4.8`) — note the host app (VisualMusic) targets net8.0-windows.
- Built as part of the repo-root `VisualMusic.sln`.

## Structure

- [Midi.cs](Midi.cs) — the data model and parser. Key public types:
  - `Song` (`partial class`) — the top-level parsed song; aggregates tracks and tempo events.
  - `Track` — a sequence of `Event`s; `Note` — a parsed note.
  - `Event` (base) → `ChannelEvent` / `MetaEvent`; `TempoEvent` — tempo-map entries.
  - `NoteBsp` — note partitioning helper.
  - enums `FileType { Midi, Mod, Sid }`, `MixdownType { None, Tparty, Internal }`.
- [SongImport.cs](SongImport.cs) — import/parse entry points that produce a `Song`.
- `Third party/BEBinaryReader.cs` / `BEBinaryWriter.cs` — big-endian readers/writers for the MIDI byte format.

## Consumed by

VisualMusic's `Project` holds a `Midi.Song` as its core note model and drives all rendering and playback
timing from it. See [../../VisualMusic/CLAUDE.md](../../VisualMusic/CLAUDE.md) for how notes become geometry,
and [../../CLAUDE.md](../../CLAUDE.md) for the repo-wide picture.
