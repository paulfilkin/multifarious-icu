# 10 - multifarious_CLDR Build Progress - 2026-10-13

Tenth session.  The keep settings run in Studio (stage 1, run 2) found two design faults and
one bug, all fixed, verified in Projects 60 to 62 and committed.  One commit on `release/2.0`,
pushed.  Stage 1 and stage 2 of the 11 October plan are now complete.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\10_261013_multifarious_CLDR_Build_Progress.md`.
>
> Release 2.0 is on branch `release/2.0`, committed and pushed, deployed to Studio's Packages
> folder: Restore ICU in place of Finalise, target-aware Expand, keep settings, comments
> surviving both tasks, filter tags kept through both tasks, and since 13 October the source
> restored as the developer wrote it so a memory of restored messages pre-translates the next
> project (1,489 tests).  Every Studio run planned for 2.0 has passed.  Still open on the
> release: the user guide screenshots 4, 11 and 12, a native read of the eight translations,
> and the release itself (merge to `main`, Release build, store package).
>
> Today's work is the grammar dictionary design for select messages.  Read
> `Documentation\Design\Grammar-Dictionary-Design-v1.md` (local, not published) and the
> "Grammar dictionaries" section of handover 09 for the decisions already taken: upper-case
> English keys in the shipped default, the dictionary in the project settings imported from a
> developer's file and exported back, a dependent selector inserted only where the developer's
> file declares the argument supplied.  The source material under
> `Documentation\CustomDictionaries` is local and must not be named in anything published.
> Start with Phase 0 on a neutral corpus file and the list of languages the shipped default
> covers.  Design before code: agree the note's next version before anything is built.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

Expand ICU and Restore ICU on `release/2.0` round-trip JSON and Java properties with translations, states, comments and filter tags, and a memory updated from a restored file pre-translates the next project as Context Matches, kept or split by the keep setting.

## Commits this session

- `7e627d9` Restore the source as written and judge keep after classification.

On `release/2.0`, pushed to origin.  `main` is still at 1.0.2.0.

## The keep settings run, and what it found

Paul asked why the run was needed before the release.  The answer: the keep path had only run
against a fixture, and three things in it were assumptions about Studio.  The run proved the
first (the origin values Studio writes) and broke on the third.

### Project 59: a memory of restored messages matched nothing

Project 57's restored file updated a memory; a fresh Project 59 from the same native files
pre-translated nothing but the protected message.  The memory held the restored source as
2.0.0 wrote it: hoisted and pruned to Russian, four whole-sentence branches with `few` and
`many` the English never had.  The fresh project's source was the developer's original, a
different string below the fuzzy minimum.  So the 11 October decision to restore the source
hoisted made the keep setting unreachable for any message with a selector.

Decision (Paul): Restore writes the source back as the developer wrote it; only the target is
hoisted.  The memory then stores original source against hoisted target, which is the pairing
a new project needs.

Implementation, `IcuRestoreProcessor.OriginalSource`: the pattern from `icu:pattern` (decoded,
each filter tag as its native text) with the filter's tags found by that text, longest first,
an end closing the innermost open pair, the clones taken from the expanded source one per tag
id in document order so a second pair with the same text keeps its own id.  A unit without the
pattern collapses as the target does.  The target side, the parity check and the pruning are
unchanged; the source pruning open item from handover 08 is gone with the hoisted source.
A comment on a source segment does not survive Restore; the guide says so.

The pre-translated path needed no code: `TargetBranches` hoists the target alone and splits by
path, so a hoisted target against the developer's source already lines up.

### Project 61: two comment lines kept and locked, the messages not

Paul ran Expand with keep on against Project 61 after a Restore.  The log said two units kept
and locked in the properties file and eight expanded.  The two were the comment lines at the
top of `tagged.properties`, no ICU in them, matched at 100: the keep check sat above the
classifier.  The eight messages were restored units, and the restored branch never consulted
the keep rule.

Decisions (Paul): keep is decided after classification, for protected and plural messages
only, so a value with no ICU is never touched, locked or counted; and a restored unit is kept
too, judged on the form states Restore recorded in `icu:segmentStates`, staying restored with
its context and its one segment locked, so a later Expand with keep off still lays it out.

### Projects 60 to 62: the run

- Project 60, new build: Expand, pseudo-translate, Restore, Update Main Translation Memories
  into `en-ru_(ICU TM Update)`.  Eleven units added; read directly from the `.sdltm`, each is
  the original source against the hoisted target.
- Project 61: pre-translate, every unit a Context Match at 100 (the first attempt searched a
  disabled memory, Paul's slip).  Expand with keep off: four forms per plural, each Translated
  from the memory at 100.  The Translation Results window shows 41% for a form, since the
  memory holds the whole message; Paul: looks odd, is fine.
- Project 62, the final shape: pre-translate; Expand keep on, locked: 8 of 8 kept and locked,
  the comment lines untouched; Restore: nothing to do; Expand keep off: 8 expanded, every form
  filled from the target; Restore: 8 restored, one plain segment per side, source as written.

### Tests

Five added, three adjusted, 1,489 in all: the restored source with a nested pair, a placeholder
and two same-text pairs keeping their ids; a hoisted Russian target against the original source
split by path and kept at the threshold; a value with no ICU never kept or locked; a restored
unit kept at 100 and laid out at 99 (theory).

### Documentation

Changelog, documentation, user guide (restore shape, memory, keep section, limitations), README
and CLAUDE.md (two decision bullets rewritten with the dates and the project numbers).

## Open items

- Grammar dictionaries, next session (Paul, 13 October): the design note's next version,
  Phase 0 on a neutral corpus file, and the list of languages the shipped default covers
  (handover 09).  Design work can run on `release/2.0` or a branch off it; nothing of it goes
  into the 2.0 package.
- The release: merge `release/2.0` to `main`, the version is already 2.0.0.0, build the store
  package in Release configuration, retake user guide screenshots 4, 11 and 12 (they show
  Finalise), and have a native reader check the eight translations of the new strings.
- The ICU Forms window shows a form's filter tags as their text, folded; acceptable, not
  reviewed by Paul.
- The classifier's parse-error offsets are into the encoded value, so a message with tags
  reports an offset short by the tags' text lengths.  Cosmetic.
- `test-corpus\probes` holds the two probe SDLXLIFFs and their saved targets, untracked.
- A source-side comment is lost by Restore (the source is rebuilt from the pattern).  Documented
  as a limitation; nobody has asked for it.
- The restored source is rebuilt by matching tag text in the readable pattern.  A tag whose
  native text also occurs untagged in the value would be mis-placed; the filters tag every
  occurrence their rules match, so it has not arisen.  An exact position record at Expand time
  is the fix if it ever does.
- The open items from handovers 05 to 08 stand, except the source pruning, which is gone.

## Traps

- All earlier traps stand (handovers 01 to 09).
- A memory updated from a restored file written by 2.0.0 (before 13 October) holds hoisted
  sources and matches nothing; re-expand and re-restore with the current build before updating.
- Pre-translate in a project whose memory is disabled in the project settings reports every
  segment unchanged and the analysis shows every segment new with no fuzzy band at all; check
  `Enabled` on the `MainTranslationProviderItem` in the `.sdlproj` before suspecting the plugin.
- `git commit -F -` in PowerShell with a here-string does not read stdin; the message becomes
  a pathspec and nothing is committed.  Use `git commit -m @'...'@`.
- A test that replaces a form's content with plain text drops the `{name}` and `#` placeholder
  tags, so Restore records a parity mismatch and leaves the unit expanded; use
  `ParagraphUnits.CopySourceToTargets` and then set the origins.
- Reading a `.sdltm` directly: it is SQLite, `translation_units` with `source_segment` and
  `target_segment` as XML, text in `<Value>` elements.  `python -X utf8 -I` from a script file
  in the scratchpad.
