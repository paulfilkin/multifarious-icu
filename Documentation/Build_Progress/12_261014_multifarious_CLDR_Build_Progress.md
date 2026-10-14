# 12 - multifarious_CLDR Build Progress - 2026-10-14

Twelfth session, and the release.  The settings pages reworked before the grammar
dictionary's Studio run, the run itself and its fixes, the documents for 2.0, the Release
build, the branch and tag work, and **2.0.0.0 published on the AppStore and live** (Paul,
late on 14 October).  1,662 tests.  The dictionary is a tab of its own with two editable
tables, the Expand options are in four groups, the Projects view button has its own icon.
The template stays as it is (option 1, Paul): its `keys` section is empty, so an import fills
the arguments table and leaves every language row Default.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\12_261014_multifarious_CLDR_Build_Progress.md`.
>
> 2.0.0.0 is released and live on the AppStore (14 October 2026): Restore ICU in place of
> Finalise, target-aware Expand, keep settings, comments and filter tags through both tasks,
> the source restored as written, and the grammar dictionary for gender selects with its
> own tab, the ICU Settings dialog on the Projects view, and the editable tables.  `main`
> and `release/2.0` are level at the release plus the handover, `v2.0.0.0` is on `1e141db`,
> `release/1.0` and `v1.0.2.0` hold the previous release; new work starts from `main` and a
> 2.0.x fix goes on `release/2.0`, tagged and merged back.  1,662 tests.
>
> Open after the release: the ten screenshots for the website guide and its HTML at
> `Documentation\website\multifariousICU_UserGuide_2.0.html` (local), the native read of the
> fifty-three strings added on 14 October, and the uncommitted `tools\plan-file.ps1`, a
> harness that plans any JSON file for any languages outside Studio, which Paul has not yet
> decided to keep.  The design note `Documentation\Design\Grammar-Dictionary-Design-v2.md`
> and the test plan `Documentation\Testing\Grammar-Dictionary-Studio-Test-Plan.md` are local.
> A test file built from the source material is under `Documentation\CustomDictionaries\Test`
> (local): German source, article selects written with gender keys, `other` carrying the
> plural, and what the planner makes of it is in this handover.  The source material under
> `Documentation\CustomDictionaries` is local and must not be named in anything published.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

2.0.0.0 is live with the grammar dictionary, the settings tabs and the ICU Settings dialog, tested through the whole plan in Studio and documented; the next session starts from `main`.

## What was asked (Paul, 14 October 2026)

Three things, agreed as proposed: the grammar settings on their own tab with an editable
table over the shipped default, updated by the existing import and written by export; the
other Expand options in a logical sequence, grouped; a new icon for the settings button, the
SVG supplied as `Images\icu-expansion-settings.svg`.  Decided on the way: both tables
(languages and arguments), three top-level tabs in the dialog, export writes the edited file.

## What was built

- `Settings\ViewModels\GrammarDictionaryViewModel.cs`, new.  Owns the file's text, name and
  import date, parses the text to a `JObject` and writes every edit back into it: a key set
  to `keys.<kind>.languages.<locale>` (removed again when it equals the default), an argument
  row to `arguments.<base>` with its role names and `supplies`, the `other` wording to
  `other`.  Rows: `GrammarLanguageRow` (kind, locale, display name from `CultureInfo`, keys,
  Default or Project, a tooltip of hints, samples and needs, Reset), `GrammarArgumentRow`
  with a `GrammarRoleCell` per role (the kinds, then count).  Validates through the existing
  merge after every edit; `Error` and `IsValid` feed the Expand view model's `IsValid`.
  `ParseKeys` and `ParseSupplies` are lenient and public.
- `IcuExpandSettingsViewModel` keeps the task options and owns `Grammar`; Apply writes the
  child's text, name and date; Reset reloads it.
- Views: `IcuExpandOptionsView` (the four groups), `IcuGrammarSettingsView` (status, buttons,
  the two `ItemsControl` tables with shared column sizes, the add-language row, the other
  box), `IcuExpandSettingsView` now a `TabControl` over the two, `IcuSettingsWindow` three
  tabs.  Default WPF tabs, not Studio's underlined style, which is what passed S2.
- Forty-four new strings in nine resource files (eight of them the three help panels on the Grammar dictionary tab, added at Paul's request after the first render), two changed (`Expand_GrammarRemove` is
  "Remove file"; the last sentence of `Expand_GrammarHelp2` now says export writes the edited
  file), `UIStrings.cs` regenerated.  Translated by me, not yet read natively.
- `IcuExpandTask` names a file made by edits alone as "made on the settings page" in the
  report.
- `Images\icu-expansion-settings.ico` from the SVG (Inkscape to PNG at 16, 20, 24, 32, 40,
  48, 64 and 256, assembled by hand, 32-bit), `IcuSettings_Icon` in `PluginResources.resx`,
  the action switched to it.
- Tests: `GrammarPageTests.cs`, thirteen tests over the rows, the edits, the refusals, the
  file around the edits and the Apply round trip; `GrammarStudioTests` and `SettingsTests`
  follow the child view model.  1,652 in all.
- `tools\render-settings.ps1`: renders the dialog's three tabs and the wizard page's two
  offscreen to `Documentation\Testing\renders` (gitignored) against Studio's styles assembly.
  Used to check every tab before the deploy; the renders are what the test plan's C0 to C2b
  describe.

## Decisions recorded

In `CLAUDE.md` under "Decisions taken (14 October 2026, the settings pages)", and a paragraph
in the design note's Settings section.  The one reversal: export writes the project's file as
it stands, edits included, where 13 October said unchanged.

## Proposed commit

Stage: the `Settings\` and `Projects\` files, `BatchTasks\IcuExpandTask.cs`,
`PluginResources.resx`, the nine resx and `UIStrings.cs`, the three test files,
`tools\render-settings.ps1`, `Images\icu-expansion-settings.svg` and `.ico`, this handover.
`CLAUDE.md` is local (gitignored), as are the design note and the test plan.  Not
`Images\icu-expansion-tile-blog.png`, `Documentation\appstore\multifarious_images\` or
`test-corpus\dictionaries\`, which are Paul's and were untracked before the session.

```
Put the grammar dictionary on its own tab with editable tables

The Expand ICU page is now two tabs, Options and Grammar dictionary, and
the ICU Settings dialog three. The Grammar dictionary tab shows the
effective dictionary as two tables, languages with their keys and the
arguments the application passes, and writes every edit into the
developer's file by round-tripping its JSON, so the shipped default is
never changed and the merge validates each edit. Export writes the file
as it stands. The Options tab is four groups in the order the task
decides them. The settings action has its own icon.

Forty-four new strings in nine languages, thirteen new tests (1,652),
and tools\render-settings.ps1 to render the pages against Studio's
styles without opening Studio.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
```

## The Studio run (afternoon) and what it found

Paul ran the plan through to J on the morning package (Projects 63 to 69).  S, C, D, F, G, H,
I and J passed; the Expand and Restore round trips and the checker's structures are right for
all five languages.  Five findings, all fixed in the afternoon build (1,659 tests):

- **The hints were two rows out** (E2, E7 to E10, I5).  The per-segment records on the unit
  context (`icu:seededFrom`, `icu:syntheticSource`, `icu:grammarForms`) were keyed by the
  segment id the writer gave, and Studio renumbers the ids after the task: in the French file
  of Project 64 the forms of `readyMinimal` carry ids 3 to 9 (the file's first unit is the
  two-segment `_comment`) while the records ran from 1.  The retest on Project 72 (R1) showed
  the task numbering that unit from 3 and keying from 3, so the task's own output agrees with
  itself.  Project 73 then cleared the two suspects: the ids stayed at 3 to 9 after the
  editor's save (17:02) and after a Restore and second Expand (17:08); Project 74, the
  standard filter without the embedded content processor as Project 64 was, agreed too
  ("numbered from 3", ids 3 to 6).  How Project 64's ids moved is unexplained; it cannot
  matter again.  Fix: the records name a segment by its position in
  the unit, with `icu:numbering=position` on the context; the reader keys by position where
  the marker is and by id where it is not, so the morning's files still read.
- **A refusal went unseen** (S4a, C17 to C20).  The message now sits in a red box under the
  table it came from, the offending row's cell has a red border, the add-language box too,
  and the status line is semibold.  The mark stays on the row that caused the refusal when
  another row is edited correctly.
- **"Your Anna has been delayed"** (E6).  The base argument of a grammar select previews as
  the dictionary's sample noun on a form that has one ("Your Zug"), and as its own name
  otherwise ("Your vehicle").  The writer records the base argument as a sixth field.
- **The window did not navigate** (Paul).  A click on a row calls
  `SetActiveSegmentPair(paragraphId, segmentId, true)` through the controller.
- **No Reset in the dialog** (C13).  A Reset to defaults button, bottom left, resets both
  groups as the wizard page does.

The retest (R1) passed on Project 72, created with the HTML embedded content processor left
on by accident, which found one more: **the window's heading had no key**.  Under the
processor each value is its own sub-content `<file>` whose unit carries no JsonPath; the key
sits on a structure unit of the enclosing JSON file, a `StructureParagraphUnit` context with
`StructureParagraphPath`, ahead of the sub-content.  Both processors now keep the last
structure path and use it where the unit has no key of its own
(`ResourceKey.StructurePathOf`); the expansion records it as `icu:key`, so the window and
the reports name the message.  Two tests, 1,661.  R12 in the plan.

The retest went on through R1 to R6 and R12 on Projects 72 to 74, all passing, with one
more change from R7: the refusal message was still out of sight under a sixty-row table, so
it now sits directly under the row it belongs to (`ce0cb4f`); the box under the table stays
only for a refused language tag.  The package of 18:48 carries everything in this handover.

Seen once in the log and not reproduced: Studio's message reporter threw a null reference on
a located placeholder error (Project 64).  The verifier now logs the full exception and
reports the message on the unit when the located report throws.  Section R of the test plan
is the retest.  `tools\render-settings.ps1` gained a refused-edit snapshot.

## Open items

- The Studio test plan is complete: A to J on the morning package, R1 to R10 and R12 on the
  afternoon packages, all passing (Paul, 14 October, evening).  R11 waits on the verifier
  fault recurring.
- Phase 4 is written (evening of 14 October): the user guide has a section "Messages that
  choose by gender" with the coverage and article tables, the developer's file key by key and
  a worked example; "The settings" describes the two tabs and the ICU Settings dialog and no
  longer says the pages are under Project Settings; the changelog, Documentation.md and
  Description.md carry the dictionary; `grammar.json` and the template are in the samples.
  The images follow the website's numbering (001 to 015): 001, 003, 005, 006 and 008 stand;
  002 and 004 need retaking (they show the old task names and Finalise); 007 (Messages
  window), 009 (Options tab), 010 (Grammar dictionary tab), 011 (ICU Settings dialog), 012
  (Restore page), 013 (Restore report), 014 (the window on a French article form) and 015
  (a target with its tags reordered) are new.  Paul takes them in Studio.
- Released as 2.0.0.0 with the dictionary (Paul, 14 October, 21:45), then rebuilt at 22:44
  after a last fix from the French screenshots: a plural row under a gender select previewed
  its argument as "Anna" rather than the gender's sample noun, because only a gender leaf
  carried the base argument.  A plural leaf under a gender now carries the gender's base
  argument and sample (planner), the writer records it, and the window reads "Your Zug is
  waiting at the platform."  One test, 1,662.  The Release build passed them all; the
  package is `Documentation\appstore\IcuSupport.sdlplugin` (1,105,021 bytes, gitignored)
  for the store upload, and `v2.0.0.0` was moved to the rebuilt commit before the upload.  The manifest's description, which the
  AppStore shows, still said "finalise" and was corrected first (`9e87e9a`).  Git: `v1.0.2.0`
  and `release/1.0` on the old `main`, `main` fast-forwarded to `release/2.0`, `v2.0.0.0` on
  `9e87e9a`, all pushed; `v2.0.0.0` then moved to `1e141db` for the sample-noun fix.
  `CLAUDE.md` records the branch scheme.  **Paul uploaded the package and 2.0.0.0 went live
  on the AppStore late on 14 October.**  Still to do for the website: the ten screenshots and
  the HTML guide at `Documentation\website\multifariousICU_UserGuide_2.0.html` with its
  placeholders replaced; and the native read of the strings.
- The fifty-three new strings in eight languages need a native read with the rest.
- The Options tab's sub-headings reuse `Expand_SeedHeader` and `Expand_PlaceholdersHeader`
  as they stand; if a native reader finds them long as sub-headings, shorten there.
- Hints, samples, needs, narrowing and renames are read-only on the page.  Making them
  editable means extending the developer's file format: a separate decision.
- **The ICU Forms window on a passed-through unit** (2.0.1 candidate, from the test file,
  14 October).  A message over the budget both expanded and walked is passed through with
  a warning comment on the unit, Studio then sentence-segments the raw value, and the
  window shows "Not an ICU message" on those segments.  It should read the unit's own
  warning instead: "This message was passed through: expansion needs 3,110 segments and
  the source's own branches 84, against a budget of 24", and point at the Expand setting.
  The warning is on the unit already (`Warn`, a comment by the plugin's author), so the
  window only has to find it; the Counts column and the rows stay empty.  In the test file
  this is `RESULT_ON_DEVICETYPE` for French and Italian and `DEVICE_NEEDS_TIME` for all
  but English.
- **The ICU Forms window should scroll to the active row** (2.0.1 candidate, Paul,
  14 October).  A message with more forms than the window shows, a walked list at 100 or
  an Italian gender over a plural, leaves the active row out of view as the translator
  moves through the segments, so the hints cannot be followed.  Once the active row would
  fall outside the visible rows, keep it vertically centred: `ScrollViewer.ScrollToVerticalOffset`
  from the row's position when `IsActive` changes, in the view's code-behind, with no
  scrolling while the row is already in view so the translator's own scroll position is
  not fought.  The click navigation added today goes the other way and is unaffected.
- The open items from handovers 05 to 11 stand.

## A test file from the source material (after the release)

Paul asked for a test file in the shape of the files the dictionary was designed for, from
the source material under `Documentation\CustomDictionaries` (local, never named in anything
published).  Written to `Documentation\CustomDictionaries\Test` (local): a JSON resource file
of 31 values with a German source, the application's macro fragments inlined as the
application resolves them, and a developer's file for it.  Planned for English, French,
Italian and Spanish with `tools\plan-file.ps1` (uncommitted, Paul's call), which plans any
JSON file for any languages with or without a developer's file outside Studio.  What the
material taught, for the design:

- **`other` carries the plural.**  In this content a gender select's `other` branch is the
  sentence for several devices, not a neutral singular.  An English target, with `other`
  alone, is seeded from that plural sentence.  The developer's file's `other` wording is
  what tells the translator; nothing else can.
- **Article selects written with gender keys.**  The German macros use the article argument
  with MASCULINE, FEMININE and NEUTER, where the French and Italian macros use LE, L, LA and
  IL, LO, L, I, GLI, LA under a gender.  Without help the dictionary treats the German keys
  as the developer's own and every target keeps them.  The fix is in the file: an article
  entry for `de` with the gender keys makes them keys the kind knows, so French prunes them
  and lays out LE, LA, L, other; `en` and `es` get `other` alone, one form.  A real project
  will need exactly this entry, and the user guide does not yet say so.
- **Keys respelt per language.**  The Spanish macros use MASKULINUM in a few cells and
  MASCULINE elsewhere; the file can respell per argument, not per language, so a project
  with mixed spellings has to be cleaned at the source.
- **Italian indefinite articles** (UN, UNO, UNA) exist in the macros; the file adds them to
  Italian's set, but the `within` narrowing knows nothing of them, so they are offered on
  every article row rather than under the right gender and number.  A limit of the format.
- **Over the budget as a matter of course.**  A list macro with an article select in each
  of its `=1`, `=2` and `other` slots plans to 212 French and 3,110 Italian segments, and a
  gender select over a nested duration plural to 225 and 525; both are laid out walked.  Two
  article selects in one sentence reach 16 for French and 100 for Italian.
- **Shapes that parse but mean something else**, both from the sheet: a stray `mit {…}`
  between branches is read as a branch named "mit", and `other{x,plural}` is literal text.
  Neither is reported; both pass through the plugin as the developer wrote them.
- A plural written as a select, `=0 =1 =2` with number arguments, application-state selects
  (OVERZERO, CST_PROCESSACTIONS_SC, TEXT, TODAY), two plurals on one argument in a sentence,
  and SSML (`<speak>`, `<phoneme>`) inside a value all behave as the design says.

## Traps

- All earlier traps stand (handovers 01 to 11).
- The Bash tool's heredoc halves backslashes and chokes on long scripts with non-ASCII text
  even with a quoted delimiter; write any script with the Write tool and run it from the
  scratchpad.  Bit this twice today.
- A WPF harness for the plugin must be Windows PowerShell (`powershell.exe -STA`), not pwsh:
  the plugin is net48 and pwsh is .NET Core.  `$host` is a reserved variable there.
- Studio's control styles look their brushes up in the application's resources; outside
  Studio, merge `theming/default` and `commonresources` from `Sdl.Desktop.Platform.Styles`
  into `Application.Current.Resources` first, or `Show` fails on an unset BorderBrush.
- A `Grid` in an `ItemsControl` row shares column widths with the header only inside a
  `Grid.IsSharedSizeScope="True"` ancestor, and a star column is never shared; a read-only
  column beside the star one squeezed it, so the key spelling went under the row.
- xunit's analyser refuses `Assert.False(collection.Any(...))` (xUnit2012); use
  `Assert.DoesNotContain`.
