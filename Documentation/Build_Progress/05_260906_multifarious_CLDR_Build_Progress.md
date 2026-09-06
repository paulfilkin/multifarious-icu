# 05 - multifarious_CLDR Build Progress - 2026-09-06

Fifth session, the second on 6 September.  The plugin is complete for 1.0 and the Release
package is built.  The session changed how the arguments are carried (placeholder tags instead
of locked spans), removed every comment the expansion used to write, gave Finalise a shape
rule and target-segment findings, surfaced task warnings in the Task Results window, fixed the
verifier's placeholder comparison, and rewrote the AppStore documentation.  Six commits on
`main` since handover 04, pushed, tree clean.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\05_260906_multifarious_CLDR_Build_Progress.md`.
>
> The plugin is complete for 1.0 and the Release package (1.0.0.0) is at
> `Documentation\appstore\IcuSupport.sdlplugin`.  One fix is outstanding and written up as the
> first open item: the ICU Forms window's Counts column is wrong for a message the task did not
> category-expand.  Start there, with the plan, then the Studio check of the count-based
> placeholder comparison, which is deployed but not yet seen.  After that only store
> housekeeping remains, which is mine: the screenshots, the product page, the upload.  A fixed
> Counts column needs a fresh Release package before the upload.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

Complete for 1.0 but for the Counts column of a walked message; the Release package is built;
the store submission is Paul's.

## What was done

- **Placeholder tags** (commit faf6fcc).  Inside a segment every argument and `#` is a
  placeholder tag, which QuickPlace, Ctrl+Alt+Down, tag verification and TM placeables work
  with; a locked span could not be placed by the translator at all.  The syntax between
  segments stays locked text.  `LockPlaceholders` wraps each tag in locked content.  Studio's
  JSON writer, decompiled, emits text for two special-character tags only and nothing for any
  other placeholder tag, locked or not, so Finalise turns the tags into locked text on both
  sides before generation, and parity is keyed on the syntax whichever construct carries it.
- **No comments from the expansion** (same commit).  A marker inside the source segment is
  copied into the target by pseudo-translation and Copy Source to Target (Project 46); a marker
  around the segment fails SDLXLIFF validation, whose key selects the direct children of
  `seg-source` (Project 47, 537 Analyse errors); a comment on the unit only repeats the ICU
  Forms window.  The comments and hints settings are gone.  The seeded-from facts travel on
  the unit context (`icu:seededFrom`, `icu:syntheticSource`) for the window's tooltip.
- **Lock setting** (commit 623dd07): a Placeholders group on the expand page, off by default,
  in nine languages, in the report and the diagnostics.
- **Finalise shape rule** (commit 02b1e82, Project 52): a clean message is locked; a message
  with a placeholder mismatch, with the task set to warn, keeps its tags, and locked spans
  from an earlier run turn back into tags, so the missing one can be placed.
- **Task Results and target findings** (commit aeb8be3, Project 56).  `TaskMessageRelay`, a
  processor added to Studio's own converter after the write-back, reports each warning so the
  task completes with warnings in the dialog.  Finalise writes each finding as a comment on
  the target segment concerned, severity High for a mismatch, and strips the plugin's own
  target comments at the start of every run.  Expand warnings stay on the unit.
- **Verifier parity by count** (commit 7514663).  A duplicated `#` in the target left the set
  of names equal to the source, so the verifier and the window saw nothing while Finalise,
  which counts, warned.  The verifier is instrumented (shared objects, reporter, language,
  units, reports) and reports a failure inside itself.
- **AppStore documentation** (commit 724fa06): all four documents rewritten to Paul's voice
  rules, ICU and CLDR explained, no internal terms, no comments; the AppStore record
  `https://appstore.rws.com/plugin/490` is the download link; the "full documentation" link
  stays `https://multifarious.filkin.com/product/icu-support/`.  The two Holy Grail samples
  are gone; `harder.json` holds the guide's examples.
- **Documentation Project repair**: the refiner had reused the source's locked-content ids
  for the targets it wrote, which Studio's reader rejects ("Missing locked content", seen
  through Apply PerfectMatch on Project 54).  708 target spans were given their own
  definitions; the refiner tool itself still has the defect.
- **Release 1.0.0.0** built: manifest and assembly agree, no symbols in the package, copied to
  `Documentation\appstore\IcuSupport.sdlplugin`.  Project 56's twenty generated files all parse
  with the right arguments and forms.
- 1,405 tests.

## Decisions taken this session

- Arguments as tags, syntax as locked text, lock as an option; recorded in CLAUDE.md.
- No comment from the expansion at all; Finalise findings on the target segment, the way a
  reviewer records them; expand warnings on the unit.
- Finalise locks a clean message and leaves a broken one editable.
- Both verifiers report a placeholder problem, Studio's and ours.
- Documentation voice: plain, short, acronyms expanded, no "walked", "faithfully", "gives
  up", "where X matters", "both say which", "nothing special".

## Findings worth keeping

- **SDLXLIFF keys segments on `./xlf:seg-source/xlf:mrk`**; nothing may wrap a segment.
- **Studio's JSON writer**: `VisitPlaceholderTag` writes only backspace and form feed;
  `VisitLockedContent` writes its children.  A tag inside locked content is dropped too.
- **Batch verification** wraps each `[GlobalVerifier]` in `BilingualContentHandlerAdapter`,
  which forwards `MessageReporter` and shared objects; it publishes `SettingsBundle`,
  `TermVerifierContext` and `ResourceDataAccessor`, no `CurrentlyVerifyingSegmentId`.
  `SegmentId` is a struct, so the missing key reads as "all segments".
- **A processor added to Studio's converter in `ConfigureConverter` runs, and its
  `ReportMessage` reaches the Task Results window**; our own write-back converter's do not.
- **`git checkout --` a file that shows modified with an empty diff** after a line-ending fix.
- **Long heredocs and non-ASCII paths through the Bash tool fail**; write scripts with the
  Write tool and run them with Windows paths.

## Open items

- **The Counts column is wrong for a walked message** (Paul, 6 Sep 2026, Project 56, Arabic
  `sync.status` in nested.json).  `IcuFormsReader.Row` takes the counts from
  `CldrPlurals.Resolve(language, kind).RuleSet.GetRule(category).Samples`, the samples of the
  category whose name the branch carries.  In a message the task did not category-expand, the
  branches are the source's, and at run time ICU sends every category with no branch of its own
  to `other`.  So the Arabic `other` row showed 100, 101, 102, 200, 201, 202 when the branch is
  in fact used for 0, 2, 5, 11 and everything else except exactly 1, and a translator reading
  the column writes for the wrong range.  The fix: the reader already reads
  `icu:expandedSelectors` from the unit context for the layout walk, so where a selector's path
  is not in that set, the counts of a branch are the union of the samples of its own category
  and of every category the message has no branch for, and the row's tooltip says the branch is
  a catch-all.  Tests: an Arabic and a Russian walked message, and an expanded one to show the
  counts are unchanged there.
- Studio look at the count-based parity: a duplicated tag should now be reported by the ICU
  Verifier in the editor and in Verify Files.
- Twelve screenshots for `Documentation\appstore\multifarious_images\`, the product page, the
  store upload.  `messages.json` for the main shots, `harder.json` for the nested messages.
- The refiner tool's locked-content id reuse (multifariousMCP), outside this repository.
- The verifier's diagnostics lines could come out once the batch run has been seen clean.

## Traps

- **Studio locks the deployed DLLs**: build with `-p:DeployPluginPackage=false` while it runs.
- **Regenerate `UIStrings.cs` after every resx change** and add every key to all eight
  satellites.
- **Every binding in `IcuFormsView.xaml` except the count box must say `Mode=OneWay`.**
- **Finalise must run before Generate Target Translations**; the guide says so.
- All earlier traps stand (handovers 01 to 04).
