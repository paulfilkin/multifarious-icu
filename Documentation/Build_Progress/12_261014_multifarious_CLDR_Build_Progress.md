# 12 - multifarious_CLDR Build Progress - 2026-10-14

Twelfth session.  The settings pages reworked before the grammar dictionary's Studio run, then the run itself and its fixes: the
dictionary on a tab of its own with two editable tables, the Expand options reordered into
four groups, a settings icon for the Projects view button.  Built, 1,652 tests, rendered
offscreen against Studio's styles, the morning package deployed at 09:37 and committed as
`e777049` on `release/2.0`.  The template stays as it is (option 1, Paul): its `keys` section is
empty, so an import fills the arguments table and leaves every language row Default.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\12_261014_multifarious_CLDR_Build_Progress.md`.
>
> Release 2.0 is on branch `release/2.0`: Restore ICU in place of Finalise, target-aware
> Expand, keep settings, comments and filter tags through both tasks, the source restored as
> written.  Still open on the release: the user guide screenshots 4, 11 and 12, a native read
> of the translations, and the release itself (merge to `main`, Release build, store package).
>
> On top of it, the grammar dictionary for select messages
> (`Documentation\Design\Grammar-Dictionary-Design-v2.md`, local): Phases 0 to 3 built and
> committed, the ICU Settings dialog on the Projects view, and on 14 October the settings
> pages reworked: the Expand ICU page has two tabs, Options and Grammar dictionary; the
> dialog has three, Expand ICU, Grammar dictionary, Restore ICU; the Grammar dictionary tab
> shows the effective dictionary as two editable tables whose edits are written into the
> developer's file; the Projects view button has its own icon.  1,652 tests, forty-four new UI strings.  The Studio test
> plan `Documentation\Testing\Grammar-Dictionary-Studio-Test-Plan.md` (local) has sections S
> and C rewritten for the tabs; A and S1 to S2 passed on the 13 October build, so S2 onwards
> is where to resume on the 14 October afternoon package (deployed 16:08; Studio installs it on its
> next start).  Its Project 63 run is from `test-corpus\grammar.json`, targets German, French,
> Italian, Japanese and American English.  Then Phase 4, the user guide section (the tabs,
> the tables, the developer's file key by key, the shipped default), and the forty-four new
> UI strings need a native read with the rest.  The source material under
> `Documentation\CustomDictionaries` is local and must not be named in anything published.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

The grammar dictionary is a tab the translator can read and the developer can edit in place, the Expand options read in the order the task decides them, and nothing of either has yet been opened in Studio.

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
- Phase 4: the user guide section on grammar selects, the tabs and the tables, the
  developer's file key by key, the shipped default; the changelog.
- The forty-four new strings in eight languages need a native read with the rest.
- The Options tab's sub-headings reuse `Expand_SeedHeader` and `Expand_PlaceholdersHeader`
  as they stand; if a native reader finds them long as sub-headings, shorten there.
- Hints, samples, needs, narrowing and renames are read-only on the page.  Making them
  editable means extending the developer's file format: a separate decision.
- The release of 2.0 as it stood, or 2.1 with the dictionary: Paul's call.
- The open items from handovers 05 to 11 stand.

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
