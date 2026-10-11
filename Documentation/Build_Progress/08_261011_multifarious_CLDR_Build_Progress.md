# 08 - multifarious_CLDR Build Progress - 2026-10-11

Eighth session.  Release 2.0 committed on `release/2.0` and pushed, first Studio runs passed: ICU Finalise
Messages replaced by Restore ICU, Expand made target-aware so the two shapes alternate, and a
keep setting for pre-translated messages.  Stage 1 of the plan agreed on 11 October; stage 2,
filter tags, has not started.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\08_261011_multifarious_CLDR_Build_Progress.md`.
>
> Release 2.0 is on branch `release/2.0`, committed and pushed, deployed to Studio's Packages
> folder: Restore ICU in
> place of Finalise, target-aware Expand, keep settings (1,467 tests).  Stage 1 of the plan
> needs its Studio verification before anything else: the three runs listed under "Studio
> verification" below, with diagnostics on.  Then the commit, then stage 2, filter tags,
> starting with its Phase 0 on `tagged.json` and a properties file with embedded content rules.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

Two tasks, Expand ICU and Restore ICU, on branch `release/2.0`; Expand, Restore and Generate Target proven in Studio for JSON.

## Branch

The work is on `release/2.0`, created from `main` at `a009b20` on 11 October (Paul: the project is
open source now, and anyone on the current build must not be disturbed).  `main` stays at 1.0.2.0
until 2.0 has been through Studio and is released.  Pushed to origin at the end of the session.

## The plan agreed (11 October 2026)

Paul's brief: two batch tasks only, Expand and Restore.  Restore undoes the expansion on both
sides and reassembles the target translations too, so Studio's own Finalize sequence can follow
it and the file can go back and forth between the two shapes.  A second topic, filter tags that
must survive expansion, became stage 2.

Decisions taken in the discussion, in order:

- The restored source is the hoisted shape, like the target, so the two line up.  The original
  pattern stays in the context.
- The restored segment is plain text, no locked spans, no tags.  Paul: "I don't like these
  locked spans at all.  It's enough for them to be tagged when hoisted."
- The tasks say nothing about the translation memory.  The translator decides when to update,
  and may update in both shapes.
- Expand gets settings to keep already translated messages, because a pre-translated project
  arrives with whole messages matched.  Off by default, since a translator may want to check a
  100% or context match.  Minimum match 100, Perfect Match and Context Match count as 100, lock
  kept segments on.
- Stage 1 first, stage 2 (filter tags) after its own Phase 0 in Studio.

## What was done

- **`IcuRestoreProcessor`** (the Finalise processor renamed and extended).  The checks are as
  before: prune, strip own comments, fill, escape, parity.  Then, clean, `Collapse`: one segment
  per side with id 1, the source as the text of its paragraph, the target as the text of its
  paragraph with each comment marker rebuilt around the text it wrapped (`CollapsedTarget`).
  Before collapsing, each form's confirmation level, lock and translation origin is captured by
  branch path and written to the context as `icu:segmentStates`, and `icu:state` becomes
  `restored` (`MarkRestored`, on a cloned context, never in place).  The pair's confirmation is
  the forms' common level or Draft; locked only if every form was.  The prune walk notes each
  segment's path (`NotePath`, a reference-keyed dictionary); the empty path is a protected
  message.  With a mismatch under the warn setting the unit stays expanded with tags, as before,
  and the comment says to run Restore again.  `FlattenPlaceholders` and `LockedText` are gone.
- **`SegmentStates.cs`**: `SegmentState` (path, confirmation, lock, origin type and system,
  match percent, text context level; `EffectiveMatch` counts document-match and SourceAndTarget
  as 100, no origin as 0; `Capture`, `Apply`, `At`) and `SegmentStates` (serialise as
  `path|level|locked|type|system|percent|context` entries joined by `;`, fields escaped; parse
  skips a bad entry; `Combined`).
- **`TargetBranches.cs`**: parses a target message, hoists it, and maps each leaf's nodes by the
  planner's path (`count:few`, `gender:female/count:many`, empty for no selector).  Null with a
  reason when it does not parse or hoist.
- **`ExpansionWriter`**: `Write(unit, plan, key, ExpansionSeed)` returns a `WriteResult`.  The
  seed carries `TargetForms`, `States` by path, `StateForAll`, `OriginalPattern`,
  `TargetFailure`.  Each target segment is filled from the seed's form where there is one and
  the pair gets its state; `icu:pattern` is the seed's original where present; `icu:state` is
  written `expanded`; an existing plugin context is removed before the new one is added.
- **`IcuExpandProcessor`**: a unit with the context in the restored state is expanded again
  (`Reexpanded` counter), seeded from `icu:pattern` and `icu:segmentStates`; a never-expanded
  unit with a non-empty target is either kept (`ShouldKeep`: option on, every segment has an
  origin at or above the minimum; `Keep` locks the segments when asked; outcome `Kept`) or
  seeded with the whole message's state (`StateForAll`: the segments' state where they agree,
  else Draft with no origin, never locked).  `ReadTargetForms` reads the target into forms for
  both Protect and Expand kinds; a target that does not read, or lacks a path, puts a line in
  the warnings and the outcome detail, no unit comment.  The walked layout takes the seed too.
- **`ResourceKey`**: `IcuContextOf`, `IsExpanded` (context present and not restored),
  `IsRestored`.  The verifier, the ICU Forms reader and the view part controller use
  `IsExpanded` unchanged, so a restored unit is invisible to them.
- **Options and settings**: `RestoreOptions` (was `FinaliseOptions`); `ExpansionOptions` gains
  `KeepTranslated`, `KeepMinimumMatch`, `LockKept`; `IcuExpandSettings`, its view model (match
  box validated only while keeping is on) and its view (a group between the budget and the parse
  groups); `IcuRestoreSettings`, page, view model and view renamed from Finalise.
- **Task**: `IcuRestoreTask`, id `multifarious_Icu_RestoreTask`.  `Constants.RestoreTaskId`,
  `StateKey`, `StateExpanded`, `StateRestored`, `SegmentStatesKey`.
- **Resources**: `Finalise_*` keys are `Restore_*` in all nine resx files; `Report_RestoreName`,
  `Report_RestoreDescription`; twelve new keys for the keep group and report (`Expand_Keep*`,
  `Expand_LockKept`, `Report_Kept`, `Report_KeepAt`, `Report_KeepLocked`), translated into the
  eight languages by me, not reviewed by a translator; the intro and two help paragraphs of the
  restore page and the verifier's empty-form message rewritten in every language; task names
  `Expand ICU` and `Restore ICU` in `PluginResources.resx` and as report names.
  `UIStrings.cs` regenerated.  `TaskReport.xsl` templates renamed; the expand report gains a
  `kept` count and the keep setting line.
- **Version** 2.0.0.0 in `AssemblyInfo.cs` and the manifest.
- **Tests** (1,467, all passing): `IcuRestoreProcessorTests` rewritten around the collapse and
  the round trip (states back, forms the restored target lacks, comments through the collapse,
  mismatch leaves the unit expanded, restored unit ignored by Restore); expand tests for the
  pre-translated seed, the unparseable target, and the keep rule (table of origins and
  thresholds); settings, contract, report, relay and satellite tests updated.
- **Documentation**: README, app store description, documentation, user guide and changelog
  rewritten for the two tasks, the switching workflow, the memory note and the keep setting;
  `CLAUDE.md` gains the 11 October decisions.
- **Deployed**: a Debug build with the package deployed to `%AppData%\Trados\Trados Studio\19\
  Plugins\Packages\IcuSupport.sdlplugin` (Studio closed), redeployed at 23:26 with the
  write-back fix and at 23:40 with the numbering fix.  Studio installed the first at 23:10 and removed the store package from the
  folder.

## Project 51: the write-back was lost, and the fix

Paul created a project from a template whose creation sequence runs Expand ICU and then
Pseudo-translate.  Expand's diagnostics showed every unit expanded and no warnings, and the
resulting files held the original segmentation, pseudo-translated, with no plugin context.  The
project file showed the two tasks run as one automatic task with two template ids.

Decompiling Studio's `ContentProcessingTaskImplementation` (Sdl.ProjectApi.Implementation):
for each file, one converter is created from the project file to a temporary file, every task
of the pass gets `ConfigureConverter`, the converter parses once, every task gets
`OnFileComplete`, and the temporary file replaces the project file if any task returned true.
`AbstractFileContentProcessingAutomaticTask.OnFileComplete` is virtual and returns false.  So
the YAML plugin's "the converter does not write back" was the default verdict, not a
limitation.  Our file swap held when the task ran alone because Studio discarded its own
output; paired with Pseudo-translate, which returns true, Studio's output, parsed from its own
read of the file, replaced ours.

Fix (Paul: "go"): both tasks add their processor to Studio's converter and override
`OnFileComplete` to return true.  `BilingualFileUpdater` is deleted; the file-level self-check
for undefined context references went with it, the defect it guarded being fixed and
unit-tested.  `TaskMessageRelay` now takes a delegate and reports at `Complete()`, since the
processor's warnings exist only after the parse.  A contract test asserts both tasks return
true.  `CLAUDE.md`'s gotcha rewritten.  The YAML, LaTeX and DXF plugins still swap files and
have the same exposure; Paul's call.

Seen in Studio the same evening.  Project 51 recreated from the same template: the forms are
expanded and pseudo-translated, every file's diagnostics line ends in "keep output".  Project
52: Expand and Pseudo-translate in the creation sequence, Restore ICU on the Russian
less_simple.json (13 units restored, 0 pruned, 0 filled, 0 warnings), Generate Target
Translations.  The restored SDLXLIFF has one segment per unit, no locked spans, no tags, the
plugin's context with state restored and a segment-state record on every unit.  All 13
generated values parse with the plugin's ICU parser, the keys match the source, every argument
is present, the selects keep their branches and every plural has the Russian set plus the
source's explicit values.

## Segment numbering runs through the file

Paul's screenshot of the re-expanded Russian file showed the left-hand column reading 1, 1, 1,
2, 3, 4, 5, 6, 1, 2.  Studio's editor shows the segment id as the segment number, and the
writer numbered each unit from 1 (the id is per unit in SDLXLIFF), while Studio's own filters
number across the file.  Fix: `SegmentNumbering.Renumber` keeps a running count in both
processors and renumbers every translatable unit they pass, touched or not; the expansion
writer starts a unit from the count (`ExpansionSeed.FirstSegmentNumber`) so the seeded-from
record on the context, which the ICU Forms reader matches rows on, carries the final numbers.
Two tests, 1,467 in all; redeployed at 23:40.  A probe of whether the framework's writer
renumbers by itself could not run: the file type manager needs Studio's plug-in directory.

## Studio verification

With `MULTIFARIOUS_ICU_DIAGNOSTICS=1`, on Project 4 or a fresh project from `test-corpus`:

0. Done (Project 51 recreated): Expand then Pseudo-translate in the creation sequence leaves
   expanded, pseudo-translated forms with the plugin context in place.
1. Done for JSON (Project 52, Russian less_simple.json): Expand, pseudo-translate, Restore,
   Generate Target Translations; the generated values parse as ICU with the Russian set and
   the arguments intact, and the restored units are one plain segment per side.  Still to do
   for the Java properties file, and a look at the restored units in the editor.
2. Done (Paul, end of session): the round trip works and the numbering is right after the
   second Expand.
3. Pre-translate from a memory holding restored messages, then Expand with keep off (forms with
   the match on each) and keep on (message left, locked).

Things to look at while there: whether Studio accepts the collapsed paragraph's single segment
id 1 after it had 1 to n (the writer does the same on first expansion, so it should); whether
a comment marker rebuilt in the restored segment shows in the Comments window; the ICU Forms
window on a restored unit (should show the "not an ICU message" panel); and the two packages
in the Packages folder, in case Studio installs the store one over the Debug one.

## Open items

- The Java properties run, the keep settings run, and a look at the restored units in the
  editor, as under Studio verification.
- Screenshots 4, 11 and 12 in the user guide show Finalise and need retaking.
- The eight translations of the new and changed strings want a native reader.
- Stage 2: filter tags.  Phase 0 first: embedded content rules on the JSON and Java filters over
  `tagged.json` and a properties file; record the tag shapes in the SDLXLIFF, whether a cloned
  tag in several segments survives the writer, id uniqueness, and the fate of an orphaned half
  of a pair.  Then the sentinel mechanism (each filter tag replaced by a private-use character
  before parsing, re-emitted as a clone of the tag at write time) and Restore assembling from
  items so the tags carry through.
- Comments on a restored message do not come back through Expand (the target is rebuilt from
  parsed text).  Documented as a limitation; a position-mapped carry-over is possible later.
- Restore prunes the source as well as the target, as Finalise did, so a unit restored for a
  language with fewer forms has lost source branches for good.  Harmless in Studio, where a
  target file has one language; noted in case the source side is ever reused.
- The open items from handovers 05 to 07 stand.

## Traps

- All earlier traps stand (handovers 01 to 07).
- Bash heredocs over a few dozen lines fail in this harness with "unexpected EOF while looking
  for matching quote".  Write the script to the scratchpad with the Write tool and run it.
- Adjacent content-processing tasks in a sequence share one converter pass and Studio writes
  the pass's output over the file if any task returns true from `OnFileComplete`.  Never
  rewrite the file on disk from `ConfigureConverter`; add the processor to the converter and
  return true.
- The framework's markup types are not safe dictionary keys by content; `IcuRestoreProcessor`
  keys segments by reference (`ReferenceComparer`).
- `ISegmentPairProperties` is shared by the source and target segments of a pair; setting the
  state on one sets it on both, which is what the round trip relies on.
- The xunit analyzers reject `Assert.Equal(n, collection.Count)` as a build error
  (`TreatWarningsAsErrors`); use `Assert.Single` or `Assert.Empty` over `ParagraphUnits.ItemsOf`.
