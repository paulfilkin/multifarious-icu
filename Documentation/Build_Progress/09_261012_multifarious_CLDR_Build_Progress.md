# 09 - multifarious_CLDR Build Progress - 2026-10-12

Ninth session.  Stage 1 of the 11 October plan verified in Studio and extended: comments now
survive both tasks.  Stage 2, filter tags, taken from Phase 0 evidence through implementation
and a Studio run in one session.  Two commits on `release/2.0`, both pushed.  The keep settings
run (stage 1, run 2) is still open.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\09_261012_multifarious_CLDR_Build_Progress.md`.
>
> Release 2.0 is on branch `release/2.0`, committed and pushed, deployed to Studio's Packages
> folder: Restore ICU in place of Finalise, target-aware Expand, keep settings, comments
> surviving both tasks, and filter tags kept through both tasks (1,484 tests).  Open: the keep
> settings run in Studio (pre-translate from a memory of restored messages, Expand with keep off
> and on), the user guide screenshots, a native read of the eight translations, and the release
> itself (merge to `main`, version, store package).
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

Expand ICU and Restore ICU on `release/2.0` keep translations, states, comments and the filter's own tags through both directions; proven in Studio for JSON and Java properties.

## Commits this session

- `a37a8a1` Carry comments through Expand and nested markers through Restore.
- `a9515c8` Keep filter tags through Expand and Restore as sentinels.

Both on `release/2.0`, pushed to origin.  `main` is still at 1.0.2.0.

## Stage 1 verification (done)

- **Java properties round trip** (Project 53, Paul): Expand, pseudo-translate, Restore, Expand,
  Restore on messages.properties, 7 messages each pass, 0 pruned, 0 filled, 0 warnings, every
  pass "keep output".  The restored units show one segment per side in the editor.
- **Comment on a restored message** (Project 53): Paul added a comment to a form, restored,
  and saw it rebuilt in the restored segment; expanding again lost it.  That became the first
  piece of work below.
- **Generate Target** for the properties file was covered by Projects 56 and 57 (below).
- **Still open**: run 2, the keep settings.  Update a memory from a restored file, create a
  fresh project, pre-translate, Expand with keep off (forms filled with the match on each) and
  keep on with lock (message left as one segment, locked, counted as kept in the report).

## Comments survive both ways (commit a37a8a1)

Paul: "Comment in Project 53 - impressive.  Although it gets lost if you expand again."  Then
"Add to stage 1 now."

- `RawValueReconstruction.Reconstruct` records every comment marker's half-open range in the
  raw string (`ReconstructedValue.Comments`, `CommentRange`), an outer marker before the ones
  inside it.
- `TargetBranches.Read` hoists at node level (`Hoister.Hoist(message.Nodes)`), which reuses
  the parsed nodes so their spans still point into the raw target.  The message-level hoist
  re-parses its own serialisation, which gave spans into the hoisted text; the first version
  passed only because a restored message is already canonical.
- `ExpansionWriter` lays a form out as pieces with raw offsets (a text node's characters one to
  one, quoted text and an argument tag atomic), cuts the text at every comment boundary and
  walks the pieces with the open markers as a stack, comments ordered by start then longest
  first.  A comment touching nothing in a form is not written in it; one touching an atomic
  piece wraps the whole piece; shared text hoisted into every form carries its comment into
  every form.  `WriteResult.CommentsCarried`; the diagnostics line says "n comment markers
  carried".
- `IcuRestoreProcessor.Collapsed` rebuilds a marker inside a marker instead of flattening its
  content to text, so nested comments survive the next Restore.
- The ICU Forms panel's two empty-state notes said "ICU Expand Plural Forms", the 1.0 task
  name, in all nine resource files; now "Expand ICU".  `UIStrings.cs` regenerated.
- Five tests; the limitation removed from the changelog, documentation Q6, the user guide and
  CLAUDE.md.  Paul confirmed in Project 53 that the comment is back on its form after Expand.

## Stage 2, filter tags (commit a9515c8)

### Phase 0 evidence, Project 56

Paul created Project 56 from `tagged.json` and the new `tagged.properties` (the same four values
in properties form, now in `test-corpus`), with the HTML embedded content processor on the JSON
file type and tag definition rules on Java Resources, Expand in the sequence, pseudo-translate,
Restore, Generate.

- **Java Resources with regex rules** puts tag pairs inline in the one file, one definition per
  instance (ids 3, 4, 7, 10, 13), `bpt`/`ept` with the literal markup as
  `OriginalEmbeddedContent`.
- **JSON with the HTML processor** turns each value into its own sub-content `<file>` in the
  SDLXLIFF (nine file elements for four values), the outer JSON file holding each whole value
  as a placeholder `<st>`, the sub-file carrying the HTML filter's tag definitions with
  formatting.  The bilingual API still handed the task the four units.
- **What 2.0.0 did**: folded the tag content into text (the 1.0 `UsedFilterTags` path), so the
  forms carried `<b>` as text, the pseudo-translation chopped it into fragments, the restored
  files lost the tag and formatting definitions, and the generated JSON carried `&quot;` for
  every quotation mark the targets held: the HTML sub-content writer escapes text, so markup
  as text would come out as entities.  The Java writer wrote the text verbatim.
- **Writer probes**: two SDLXLIFFs built by hand from Project 56's restored files with the
  definitions put back (`test-corpus\probes`, untracked), three units each: a pair cloned into
  two segments, a pair split across two segments as `bx`/`ex`, a pair once.  Opened in Studio
  as single documents and saved as target.  Both writers accept the cloned pair and write each
  copy as markup, and both drop the split pair silently.  The first properties probe failed to
  open with "Missing formatting definition with ID '1'" until `<fmt-defs>` was put back too.

### Design and implementation

- **Encoder.**  `RawValueReconstruction.Encode` returns `EncodedValue`: the value with one
  private-use character per tag (a start and an end for a pair, one for a placeholder or
  structure tag), the comment ranges on that string, and a table from character to
  `TagSentinel` (kind, the tag item, pair index, native text).  Characters the text already
  uses are skipped.  `Decode` gives the readable text.  `Reconstruct` is unchanged for the
  projection, `icu:pattern`, `icu:hoisted`, the ICU Forms window and the verifier.  The walk is
  shared through an `ITagTexts` strategy.
- **Expand.**  The classifier, planner and hoister run on the encoded value.  `ExpansionSeed`
  carries `SourceTags` and `TargetTags`.  The writer's `Content` lays a form out as pieces
  (`Pieces`, `AddTextPieces`), cuts text at comment boundaries (`Split`), and `Assemble`
  builds each level: a cloned pair per pair start, filled by building up to its end at the
  next level; a comment covering a whole pair wraps the pair at the outer level and is not
  applied inside it; other comments wrap the leaves they touch (`Wrap`).  `ClonePair` copies
  the start and end properties, tag id included.  A form holding one half of a pair throws
  `TagPairSplitException`; the processor's `TryWrite` warns and records a pass-through, and the
  unit is untouched because the writer builds before it refills.  The diagnostics line says
  "n filter tags kept".  The metadata is decoded, so `icu:pattern` and `icu:hoisted` are
  readable.
- **Restore.**  `Collapsed` clones a pair around its collapsed content and clones a filter
  placeholder or structure tag; the expansion's argument tags and locked spans become text.
  `IsArgumentTag`: the `Constants.ArgumentTagKey` metadata the writer and the restore's
  `Tag()` now set, with the tag content (ICU syntax) as the fallback for files expanded before
  the key existed.  `FillFromSource` copies tags recursively (`CopiesOf`).  `IsEmpty` counts
  only argument tags as content.  Parity is unchanged: `SyntaxOf` keys every placeholder tag,
  filter placeholders included, which both sides hold.
- **Tests**: `FilterTagTests.cs`, twelve tests (the encoder, a pair around the plural cloned
  into every form with readable metadata, a pair inside a branch and a protected message, a
  filter placeholder, the split pair pass-through, a pair holding the segments themselves, the
  collapse on both sides, the round trip, comments inside and around a pair, the fill).
  Test fixture: `ParagraphUnits.Pair`, `Placeholder`, `JsonMarkup`, `CopySourceToTargets`,
  `WrapPhrase`, `TranslatorComment`, `CommentText`, `MarkersOf`.  1,484 in all.
- **Documentation**: user guide section "Markup inside the values" and a limitation line, FAQ
  Q10, changelog, README scope and design bullet, CLAUDE.md decisions of 12 October.

### Studio run, Project 57 (Paul)

Project 56's shape again with the new build: Expand kept 2 tags on each protected message and 4
on each plural in both files, no warnings; the restored SDLXLIFFs hold the pairs as `<g>`
elements with the filter's ids cloned per form, no `bx`/`ex`, no sentinel characters, readable
patterns; the generated JSON and properties files carry the markup in every form of every
message with the arguments intact and no entities.

## Open items

- Stage 1 run 2, the keep settings, as above.
- The release: merge `release/2.0` to `main`, the version is already 2.0.0.0, build the store
  package in Release configuration, retake user guide screenshots 4, 11 and 12 (they show
  Finalise), and have a native reader check the eight translations of the new strings.
- The ICU Forms window shows a form's filter tags as their text, folded; acceptable, not
  reviewed by Paul.
- The classifier's parse-error offsets are into the encoded value, so a message with tags
  reports an offset short by the tags' text lengths.  Cosmetic.
- `test-corpus\probes` holds the two probe SDLXLIFFs and their saved targets, untracked.  Keep
  or delete; the handover above records how they were built and what they showed.
- Comments: a comment whose selection ran across the syntax between two forms comes back on
  both; a comment covering a whole pair comes back around the pair.  Documented.
- Earlier open items stand: Restore prunes the source as well as the target; the open items
  from handovers 05 to 08.

## Traps

- All earlier traps stand (handovers 01 to 08).
- `Hoister.Hoist(IcuMessage)` re-parses its own serialisation, so node spans on its result
  point into the hoisted text, not the raw.  Use `Hoister.Hoist(message.Nodes)` wherever spans
  must map back to the raw value.
- The HTML embedded content writer escapes text: anything that must come out as markup must be
  a tag in the SDLXLIFF.  Studio's writers accept a tag cloned into several segments and drop a
  pair split across segments without a word.
- A restored file written by the plugin has no `<tag-defs>` or `<fmt-defs>` for tags that no
  longer appear; a hand-built probe has to put both back or Studio refuses it with "Missing
  formatting definition".
- A form filled from the source by Restore is wrapped in the plugin's own comment marker (the
  finding), so a test that looks for the form's items must look inside the marker or translate
  the targets first (`ParagraphUnits.CopySourceToTargets`).
- `python -I` ignores `PYTHONIOENCODING`; printing Cyrillic from a script under this harness
  needs `python -X utf8 -I`.
- Writing a project file from a Python heredoc failed once with "OSError: Invalid argument"
  for no visible reason; the same edit from a script file in the scratchpad went through.
  Check the build output after every scripted edit.
