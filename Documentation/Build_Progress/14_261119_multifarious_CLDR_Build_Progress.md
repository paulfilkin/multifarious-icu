# 14 - multifarious_CLDR Build Progress - 2026-11-19

Fourteenth session, the first after 2.1.0.0 went live.  Started from an RWS support case: a
Trados Cloud project's SDLXLIFF opened offline in Studio shows the plural forms with no
context.  The ICU Forms window now reads such a file, both tasks leave cloud work alone and
say so, and the day's evidence on what the cloud export keeps is recorded.  1,679 tests.
Nothing committed yet; the commit is proposed below.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\14_261119_multifarious_CLDR_Build_Progress.md`.
>
> 2.1.0.0 is live on the AppStore (25 October 2026); `main` and `release/2.1` are level at
> `4a69421`.  Work on 19 November, uncommitted unless this handover says otherwise: the ICU
> Forms window reads a unit laid out by the Trados Cloud app and downloaded for offline work
> (`ResourceKey.IsCloudExpanded`, a reader fallback that takes the per-segment facts from the
> translation origins), the survey counts such units as expanded, Expand ICU skips such a
> unit, and both tasks process nothing on a cloud project and say so in Task Results.  One
> new string in nine resource files, four new tests (1,679).  A copy of the cloud file is at
> `test-corpus\probes\cloud-less_simple-ru-RU.sdlxliff` (local).
>
> Open: Paul's Studio run on the downloaded project `6a995f7f` (the window on `knightCount`,
> the tasks' message, and which `ProjectType` the diagnostics log reports for a downloaded
> project); the reply to the customer's case, and whether the cloud app gets an option to write
> the note as a comment for offline work; the version for a 2.1.1 or 2.2; the website
> guide's SSML section and screenshots; the native read of the thirteen strings added
> since 14 October that are still unread; the reference project's `Hoister.cs` and design
> 5.9; the uncommitted `tools\plan-file.ps1`.  The source material under
> `Documentation\CustomDictionaries` is local and must not be named in anything published.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

A cloud project's file opened offline now shows its forms in the ICU Forms window, and the batch tasks refuse a cloud project with a message; built, tested and deployed, waiting for Paul's run and the commit.

## What was asked

Paul forwarded a customer's email of 18 November (an RWS support case, to the account
team and RWS Support): after "the customization and the workflow" were updated,
the plural forms are right for each language but a fr-CA SDLXLIFF opened offline in Studio
gives no context as to which form a segment is for, "neither comments nor DSI", where the
Online Editor's native preview shows the active segment inside its `other {…}`.  Paul asked
why that question arose at all given the ICU Forms window, then what the file holds that a
"clever preview" could use.  On the way he ran both batch tasks on the downloaded project,
found they do not work there, and asked that they be unavailable on projects not created in
Studio.

## What the question was

The customer's file is a Trados Cloud project's export, and the customisation is the cloud app, the
reference project.  Its D6, revised and built on 26 October, dropped the per-segment comment
("CLDR category: many, used when the count is…") in favour of `icu:note` segment metadata,
which only the app's own preview page renders.  Her previous build gave her the comments; the
update took them away; offline in Studio nothing shows metadata, document structure
information is per paragraph unit and names only the key, and the app's preview page is not
what she compared against.  The Studio plugin could not read the file either: no
`multifarious:icu` context.

## What the cloud file holds (project 6a995f7f, downloaded into Studio)

- Each `sdl:seg` carries nine `icu:` values as `sdl:value` entries: `path`, `selector`,
  `category`, `exampleIntegers`, `exampleDecimals`, `seededFrom`, `syntheticSource`,
  `locale`, `offset`; a build from 26 October on adds `icu:note`.  The decompiled SDLXLIFF
  filter writes them from `TranslationOrigin.MetaData`, so the bilingual API gives them as
  `pair.Properties.TranslationOrigin.GetMetaData(...)`.
- Paul edited segment 7 and saved: the origin became "interactive" with every `icu:` value
  copied onto it, and the previous origin kept its own copy.  Stable through editing.
- The unit-level metadata (`icu:pattern`, `icu:hoisted`, `icu:expandedSelectors`,
  `icu:key`) is not in the file; the paragraph contexts hold `JsonPath` only.
- The layout is the three locked shapes the reader knows, `{count, plural,` then ` =0 {`
  then `}`, as `<x>` references to `lockTU` units, and a `#` inside a segment is a locked
  span too (the fixture predates D11's placeholder route, which was not adopted anyway).
  The fixture is a 3 September expansion and still has the old source comments.
- Alongside the SDLXLIFF the folder holds the BCM (`less_simple.json.bcm`) with the full
  metadata, including the unit-level keys; not used.

## What was built

- `ResourceKey.IsCloudExpanded`: no context of ours, the source paragraph's first
  non-blank item a locked span opening a selector, and `icu:path` on some segment pair's
  origin.  The shapes alone are not enough; a manual lock is not the cloud.
- `IcuFormsReader.Read` fallback: the pattern and the hoisted text are the reconstructed
  source paragraph, every plural kind on the paths counts as expanded
  (`CloudExpandedSelectors`), numbering is by position, and `ReadCloudOrigins` fills the
  seeding, the synthetic flag and the note from the origins; a note stands in for the
  composed tooltip.  `Layout.SelectorPath` is public for it.  Everything else, the counts
  from CLDR, the count box, the projection and its parse, runs as for the plugin's own files.
- `IcuFormsViewPartController.Survey` counts a cloud-expanded unit as expanded.
- `IcuExpandProcessor.Expand` skips a cloud-expanded unit as already expanded with the
  detail "Laid out by the Trados Cloud app; left as it is."  Restore skipped it already,
  since the unit carries no context.
- `Services\CloudProjects.cs`: `IsCloud(ProjectInfo)` on the four cloud `ProjectType` values,
  `Message` and `Describe`.  Both tasks: on a cloud project `ShouldProcessFile` accepts the
  first wanted file only, `ConfigureConverter` adds a `TaskMessageRelay` with
  `Task_CloudProject` at Warning level and no processor, and `OnFileComplete` returns false
  so Studio discards its parse.  The diagnostics line prints the project type and origin.
- `Task_CloudProject` in nine resource files, `UIStrings.cs` regenerated.
- Tests: `ParagraphUnits.CloudExpanded` builds the cloud shape with origins; four tests
  (recognition, the reader, the window on the customer's fr-CA shape, the processor leaving it
  alone).  1,679.  Deployed at 19:39 with Studio closed.

## Proposed commit

Stage: `Services\ResourceKey.cs`, `Services\IcuFormsReader.cs`, `Services\IcuExpandProcessor.cs`,
`Services\CloudProjects.cs`, `BatchTasks\IcuExpandTask.cs`, `BatchTasks\IcuRestoreTask.cs`,
`Editor\IcuFormsViewPartController.cs`, the nine resx and `UIStrings.cs`, the five test
files, this handover.  Not the probe copy (gitignored) or Paul's untracked files.

```
Read a cloud-expanded file in the ICU Forms window; refuse cloud projects

A Trados Cloud project's SDLXLIFF opened offline carries no context of
this plugin's, but the same locked shapes and the cloud app's facts for
every segment on its translation origin, which Studio keeps through an
edit. The window now reads such a unit: the pattern from the paragraph,
the paths from the layout, the counts from CLDR, the seeding and the
note from the origins. Expand ICU leaves such a unit alone instead of
expanding it again, and on a project downloaded from the cloud both
tasks process nothing and say so in Task Results.

One new string in nine languages, four new tests (1,679).

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
```

## Open items

- **Paul's Studio run on `6a995f7f`**: open `less_simple.json.sdlxliff` for ru-RU and move
  through `knightCount` (six rows, the active one marked, the count box live); run Expand ICU
  and Restore ICU on the project and read the Task Results message; read
  `%TEMP%\multifarious-icu-diagnostics.log` for `type=` on the project line, which says
  which `ProjectType` a downloaded project reports.  If it is `Standard`, the project guard
  never fires and the unit guard is all there is.
- **The reply to the customer**: the Studio plugin's window answers the offline case once this is
  released; the cloud app's own preview page answers the online case; and a cloud option to
  write the note as a comment for offline teams is a switch, since the writer existed until
  26 October.  Paul's call on which to offer.
- **A release for this**: 2.1.1 as a fix on `release/2.1`, or 2.2; the version numbers are
  still 2.1.0.0.
- The website guide's SSML section and screenshots, the native read of the thirteen strings
  added since 14 October that are still unread, the reference project's `Hoister.cs` and
  design section 5.9, and `tools\plan-file.ps1` (handover 13).

## Traps

- All earlier traps stand (handovers 01 to 13).
- `CreateTranslationOrigin` is on `IDocumentItemFactory`, not `IPropertiesFactory`.
- `ProjectFile.ReportMessage` is not public; a task that processes no file has no channel
  to Task Results.  Accept one file, relay the message through its converter, return false
  from `OnFileComplete`.
- A private static method of a nested class is not reachable from the enclosing class;
  `Layout.SelectorPath` had to become public.
- `powershell -File script -Targets a,b` is one string; see handover 13.
