# 11 - multifarious_CLDR Build Progress - 2026-10-13

Eleventh session.  The grammar dictionary for select messages: design note v2 agreed, Phases
0 to 3 built and tested, then the ICU Settings dialog (1,639 tests), all three committed on `release/2.0` and the package
deployed to Studio's Packages folder for the Phase 3 run.  Nothing of
it is in the 2.0 package or the published documentation yet.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\11_261013_multifarious_CLDR_Build_Progress.md`.
>
> Release 2.0 is on branch `release/2.0`: Restore ICU in place of Finalise, target-aware
> Expand, keep settings, comments and filter tags through both tasks, the source restored as
> written.  Still open on the release: the user guide screenshots 4, 11 and 12, a native read
> of the eight translations, and the release itself (merge to `main`, Release build, store
> package).
>
> On top of it, the grammar dictionary for select messages
> (`Documentation\Design\Grammar-Dictionary-Design-v2.md`, local): Phases 0 to 3 are built and
> committed, 1,639 tests, plus the ICU Settings dialog on the Projects view (Studio shows a
> plugin task's page only inside its wizards).  The Studio test plan
> `Documentation\Testing\Grammar-Dictionary-Studio-Test-Plan.md` (local) is the next step, Paul
> working through it: section A and S2 passed on 13 October (the package of 22:21 installed, the
> ICU Settings dialog opens over Project 63), S3 onwards is where to resume.  Its Project 63 run is from
> `test-corpus\grammar.json`, targets German, French, Italian, Japanese and American English,
> the template saved from the Expand page and imported back, Expand, pseudo-translate, Restore,
> Generate Target, the restored JSON checked per language, Expand again over the restored file,
> the ICU Forms window and F8 on a French article form.  Then Phase 4, the user guide section
> and the shipped default documented key by key, and the eighteen new UI strings need a native
> read with the rest.  The source material under `Documentation\CustomDictionaries` is local
> and must not be named in anything published.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

A select whose argument the grammar dictionary recognises is laid out with the forms the target language needs, a dependent article form inserted where the developer's file declares it supplied, round-tripped through Restore and shown with its hint in the ICU Forms window, all tested without Studio; the Studio run is next.

## Commits this session

- `5fb0b75` Add the grammar dictionary model and the Phase 0 corpus (Phase 1).
- `111964c` Expand grammar selects in the planner (Phase 2).
- `1b80d0f` Wire the grammar dictionary into Studio (Phase 3).
- `010a468` Add the ICU Settings dialog on the Projects view.

All on `release/2.0`; `main` is still at 1.0.2.0.

## Phase 0: the corpus and the current build

`test-corpus\grammar.json`, eight values in a travel app's wording: a gender select with
`other` only, the four keys, gender over a plural, gender over an article select, a fragment,
an extra application key, a plural written as a select, application states.  Run through the
13 October build from a scratch PowerShell harness: every select walked with the source's
keys for every language, nothing over budget.  CLDR 48's `grammaticalFeatures.json` lists 61
locales, 35 with a nominal gender; that is the shipped default's coverage.  The source
material showed six things the note records: a German source, fragments as often as
sentences, `other` carrying the plural, key spelling varying inside one project, article keys
in definite and indefinite sets, and selects that look grammatical but pass the same keys for
every language.

## Decisions (Paul, 13 October 2026)

Seven agreed as proposed in the note's v2: the 61-locale default, Slavic animacy as CLDR
gives it, the article kind for French and Italian only, `other` as the fallback the project
names, hints as the grammatical term with samples for the four reviewed languages only, three
respellings in the developer's file, and no Studio run for Phase 0.  Recorded in `CLAUDE.md`
with the details the build settled: known-but-unneeded keys pruned and unknown keys kept; the
within path per base argument; the planner's null dictionary meaning walk; the settings
holding the file's text; Restore needing no pruning; the window reading hints from the
context.

## Phase 1: the model

`GrammarDictionary`, `GrammarDictionaryReader`, `DeveloperFile` and
`GrammarDictionaryException` in `multifarious.Icu.Expansion`; `LocaleChain` and
`GrammarDictionaryResource` in `Icu.Cldr`, with `Data\grammar-dictionary.json` generated from
CLDR 48 and hand-edited for the article sets, hints and samples.  The legacy code table in
`CldrPlurals` became internal, the only change to a reference-copied file.  Three lookups:
recognise an argument name (declared, then suffix), resolve a kind for a language down the
chain, and whether the file declares a dependent kind supplied.  23 refusals with plain
messages.  102 tests.

## Phase 2: the planner

`ExpansionPlanner` takes an optional dictionary.  A recognised select is category expanded
with the language's keys, the developer's own keys first, each dictionary key seeded from its
own branch or `other`; a dependent kind is inserted at the leaf as a synthetic
`{vehicleArticle, select, other {leaf}}` and planned as recognised, so it lands under a gender
directly or under the plural beneath it; a need the file does not declare supplied becomes
`icu:needs` on the leaf and a line in `ExpansionPlan.Notes`.  `PlannedSelector` carries `Kind`
and `Keys`.  Two corrections to the design came out of the tests: pruning is by the kind's
known keys, not the language's set, and the corpus message with a plural under every gender
gives Italian 18 forms, not 16.  32 tests.

## Phase 3: Studio wiring, built, run pending

- `IcuExpandSettings`: the file's text, name and import date; `ToDictionary(out failure)`.
- The Expand page: a Grammar dictionary group with the default's line, the imported file's
  line, Import, Export, Remove and Save template, the refusal shown in place.  Eighteen UI
  strings in the nine resource files, translated by me, not yet read natively.
- `IcuExpandTask` builds the dictionary once and passes it to the processor; a stored file
  that no longer validates is said once at Warning level and the default runs.  The plan's
  notes go to the report (`notes` attribute, shown in the note column) and to the Task
  Results window at Note level.
- `ExpansionWriter` records `icu:grammar` (path, kind, keys per grammar select) and
  `icu:grammarForms` (segment, kind, hint, sample, needs), tab-separated, one per line.
- `IcuFormsReader` reads them: the row's hint sits where a plural's counts sit, the tooltip
  reads `Grammar form: MASCULINE (masculine, e.g. der Zug)` with the seeded and needs lines;
  the verifier's form name carries the hint.
- Restore unchanged: the round trip of a French unit with inserted articles is tested, the
  source coming back as written.
- `Resources\grammar-dictionary-template.json`, embedded, with a `_note` the parser ignores.
- 12 tests in `BatchTasks\GrammarStudioTests.cs`, 1,638 in all.

## The ICU Settings dialog (after the Phase 3 commit)

Paul's first look at Studio showed the Expand ICU page absent from Project Settings.
Decompiling `BatchProcessingSettingsPageCategory` (Sdl.TranslationStudio.ProjectManagement)
settled it: the category adds a plugin task's `RequiresSettings` page only when a wizard hands
it a start task template, so the page exists in the New Project and batch task wizards and
nowhere else; the template editor and File > Options never show it, and the API has no
extension point for a batch task page or a settings category (the categories come from
Studio's internal container; only Verification has a public page extension point, which is
why the verifier page shows there).  Paul wanted this fixed before testing.

Built: `Projects\IcuProjectsRibbonGroup.cs`, a "multifarious ICU" group on the Projects view's
Add-ins tab with an "ICU Settings" action, and `Settings\Views\IcuSettingsWindow`, a WPF
dialog hosting the existing Expand and Restore views and view models over the selected
project's settings bundle, saved through `FileBasedProject.UpdateSettings` and `Save`, then
`RefreshProjects`.  Cancel discards because the view models hold edits until Apply.  Seven UI
strings in nine languages, three plugin resource entries, one contract test, 1,639 tests.  The
test plan gained section S for it.  Studio was open throughout, so the package was not yet
deployed when this was written; it was deployed at 22:21 after Paul closed Studio, committed as
`010a468`, and the first two checks passed: the button is on the Projects view's Add-ins tab and
the dialog opens over Project 63 with both tabs (S2).  Testing resumes at S3 tomorrow.

## The Studio run to do (Project 63)

1. New project from `test-corpus\grammar.json`, targets de-DE, fr-FR, it-IT, ja-JP, en-US.
2. Expand ICU settings: Save template, then Import the saved file.  The template declares
   `vehicleArticle` supplied for French and Italian and a second argument `lineGenus`.
3. Expand: the segment counts the planner gives for the eight values in corpus order
   (`readyMinimal`, `ready`, `waiting`, `readyWithArticle`, `fragment`, `readyExtraKey`,
   `countAsSelect`, `status`), checked from the built libraries with the template loaded:

   | Target | Counts |
   |---|---|
   | German | 4, 4, 8, 4, 4, 5, 2, 3 |
   | French | 7, 7, 13, 7, 7, 8, 2, 3 |
   | Italian | 8, 8, 18, 8, 8, 9, 2, 3 |
   | Japanese | 1, 1, 1, 1, 1, 2, 2, 3 |
   | English | 1, 1, 2, 1, 1, 2, 2, 3 |

   The Task Results window should carry one note for German, on `readyWithArticle`: "German
   is not in the grammar dictionary for article; {vehicleArticle} is laid out as written".
   None for French or Italian, since the template supplies the article.  The report's settings
   table names the imported file.
4. Pseudo-translate, Restore, Generate Target; parse each language's JSON as ICU.
5. Expand again over the restored file: the forms filled from the target.
6. The ICU Forms window on a French article form: the counts column reads
   "masculine singular before a consonant"; F8 on an empty one names the form with its hint.
7. Remove the file on the page and Expand a fresh copy for French: three forms and a note in
   the Task Results window, "French needs an article form for {vehicle}; the application does
   not supply vehicleArticle".

## Open items

- The Studio test plan from S3: the dialog's Cancel, validation and save path (S3 to S7), then
  the settings page (C), the Expand run with the template (D), the editor and window (E), the
  verifier (F), the round trip (G, H), the run without a file (I), regression (J).

- The Studio run above, then Phase 4: a user guide section on grammar selects and the
  developer's file, the template, the shipped default documented key by key, the changelog.
- The eighteen new UI strings in eight languages need a native read with the rest.
- The release of 2.0 as it stood, or 2.1 with the dictionary: Paul's call.  Merge to `main`,
  Release build, store package, screenshots 4, 11 and 12.
- Catalan's article set (EL, LA, L, ELS, LES) waits for a native read before joining the
  default.
- A select whose keys are CLDR keywords (`countAsSelect`) stays walked; a kind for it is a
  separate decision.
- The open items from handovers 05 to 10 stand.

## Traps

- All earlier traps stand (handovers 01 to 10).
- net48 has no `string.Contains(string, StringComparison)` and no
  `Dictionary(IReadOnlyDictionary, comparer)` constructor; use `IndexOf` and `ToDictionary`.
  Both compiled in my head and not in the build.
- A new UI string must go into all nine resource files or `SatelliteLanguageTests` fails;
  the neutral entry needs a translator comment.
- The Bash tool's heredoc choked on a long script with non-ASCII text; write the script to the
  scratchpad with the Write tool and run it.
- `IcuExpandProcessor` without a dictionary walks every select: a test that expects grammar
  expansion must pass `GrammarDictionary.Default` or a merged dictionary explicitly.
