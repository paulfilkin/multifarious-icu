# 02 - multifarious_CLDR Build Progress - 2026-09-05

Second session, same day.  Phase 0 closed on evidence, Phase 1's expansion is built, tested and
proven end to end in Studio (Project 40: 31 messages across six files expanded, analysed,
pre-translated, pseudo-translated in the editor and generated back to JSON and Java properties
with their complete ICU syntax).  Phase 2, the finalise task and the protection of argument-only
messages, is built and unit-tested but has not yet run in Studio.  Two Studio runs failed on the
way and both changed the design: one confirmed the cloud project's D11 for Studio, the other found
a sharing trap in the SDLXLIFF reader.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then the reference design record
> `c:\Users\paul\Documents\GIT\.NET\CLDR Support\Docs\Design\ICU-Message-Expansion-Design-v4.md`
> (section 8.6 is the finalise contract; 5.8 the escaping), then
> `Documentation\Build_Progress\02_260905_multifarious_CLDR_Build_Progress.md`.
>
> The expand task is proven in Studio.  The finalise task and argument-only protection are
> built, unit-tested (1,321 tests) and deployed, and need their Studio run: a fresh project
> en-GB to ru-RU, ja-JP and ar-SA from `test-corpus`, Expand, pseudo-translate, then in the
> editor type an apostrophe into one Russian target form and a literal brace into another,
> save, run "ICU Finalise Messages" on all target files, Generate Target Translations, then
> parse every generated value (a throwaway xunit probe did this last time; see "Findings").
> Check the Arabic file has six forms and the Japanese one, that the finalised Russian file
> carries `It''s` and `'{...}'`, and that the escaping is not doubled by a second Finalise run.
>
> After that: slice 3, the settings classes and WPF pages for both tasks (options, allowlist),
> the task reports (currently empty, file name has a trailing space), localisation.  Then
> Phase 3, the ICU Forms editor view part and a verifier, following Localyzer Connect's view
> part pattern (see "Findings").  Agree each plan before coding.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.

## Where we are in one line

Expand is proven in Studio on both filters; finalise is built and awaits its Studio run.

## What was done

- **Phase 0 evidence** (Project "en-ru only", probe processor): both tasks list and run; file
  type ids are exactly `JSON v 1.0.0.0` and `Java Resources v 2.0.0.0`; both filters deliver an
  ICU value as plain text with no tags; Studio's segmentation cuts a message at sentence ends
  inside branches, so reconstruction reads the whole paragraph; the resource key sits in
  context metadata (`JsonPath`, `SDL:SpiceId`).
- **Slice 1 built**: `RawValueReconstruction`, `ResourceKey`, `ExpansionWriter`,
  `IcuExpandProcessor`, `SdlxliffChecks`, `BilingualFileUpdater` (own converter, self-check,
  swap).  Tests over real framework paragraph units through `DefaultDocumentItemFactory`, with
  `StudioAssemblyResolver` as a module initialiser.
- **Studio run 1 (Project 38) failed** in Analyse and Pre-translate with a null context.  Cause:
  the SDLXLIFF reader gives every unit in a `<group>` the same `IContextProperties` object and
  the Java Resources filter groups a value with its surrounding whitespace units; adding the ICU
  context to the shared object produced references without definitions.  Fix: clone the
  context properties before adding.  A self-check now refuses to swap a file whose context
  references lack definitions.
- **Studio run 2 (Project 39)** clean through Analyse and Pre-translate, but the generated JSON
  had lost every placeholder tag while the target paragraph still carried them.  The cloud's
  D11 holds in Studio: locked content is the default, placeholder tags the variant.
- **Studio run 3 (Project 40)**: expand, Analyse, Pre-translate, pseudo-translate in the editor,
  Generate Target Translations for all six files.  A parser probe over every generated value
  found 31 messages with selectors, all parsing, every plural selector carrying exactly the
  Russian cardinal set plus its explicit values, every selectordinal exactly the Russian ordinal
  set, and every argument set identical to the source.  Comment metadata, tag metadata (under
  the variant) and unit contexts survived every stage.
- **Phase 2 built** (not yet run in Studio): `MessageKind.Protect` for messages with arguments
  and no selector, laid out as one segment with the arguments locked and a comment saying so;
  `icu:expandedSelectors` on the unit context (paths of category-expanded selectors);
  `FinaliseOptions`; `IcuFinaliseProcessor` reading the layout back from the locked syntax
  (selector open, branch open, close brace: an unambiguous grammar for the writer's output),
  pruning both paragraphs, filling empty targets from the source, escaping by
  unescape-then-escape (idempotent), checking parity, warning as comments; `IcuFinaliseTask`
  over the shared updater.  13 new tests including render equality after pruning for Japanese.
- **Housekeeping**: git repository on `main`, remote `origin` through the `github-paulfilkin`
  alias, `.gitignore` publishing only the code root, `.gitattributes`; `PF_Automate.ahk` gained
  the ICU diagnostics entry.

## Decisions taken this session

- **Target side only; allowlist of JSON and Java Resources; AppStore name
  "multifariousICU Support for Trados Studio"; Studio spike first** (Paul, start of session).
- **Locked content carries the ICU syntax** (cloud D11 confirmed for Studio).  Per-segment
  metadata lives on the segment's comment; unit metadata on the `multifarious:icu` context.
- **Argument-only messages are protected too** (Paul, after pseudo-translation garbled them).
- **Context properties are cloned, never shared.**
- **A written bilingual file is self-checked before it replaces the project's copy.**
- **Multilingual projects need nothing more**: one bilingual file per language, each expanded
  and finalised for its own language.  To be shown in the next Studio run.
- **Preview becomes an editor view part** (Phase 3), not a file type preview: see Findings.

## Findings worth keeping

- **The SDLXLIFF reader shares one `IContextProperties` across a group.**  Any processor that
  edits contexts on a unit read from SDLXLIFF must clone first.
- **Studio's JSON writer drops placeholder tags, in and between segments alike.**  The Java
  Resources writer emits them.  Locked content is emitted verbatim by both.
- **The file type manager cannot convert SDLXLIFF outside Studio**: `CreateInstance(true)`
  needs the application plugin directory and `CreateInstance(false)` knows no file types, so the
  write-back path is exercised only in Studio.  The parser probe over generated files is the
  substitute: flatten the JSON and properties, parse every value, compare category sets and
  argument sets with the source.
- **Escaping shape**: `IcuEscaping.Escape` doubles apostrophes and quotes the stretch from the
  first syntax character to the last as one literal: `It's {x} ` becomes `It''s '{x}' `.
- **Localyzer Connect** (`c:\Users\paul\Documents\GIT\.NET\LocalyzerConnect\`) is the pattern
  for Phase 3: `[ViewPart]` with `ViewPartLayout(Dock = Bottom, LocationByType =
  typeof(EditorController))`, a WPF `WebView2` control, a `[RibbonGroup]` in the Add-ins tab
  with an `[Action]` that shows the view part, `EditorController.ActiveDocumentChanged` and
  `IStudioDocument.ActiveSegmentChanged` driving the content, and the WebView2 package with
  `ExcludeAssets=runtime` because Studio 2026 ships `Microsoft.Web.WebView2.Core/WPF`
  1.0.3856.49 and `WebView2Loader.dll`.  Do differently: use
  `IStudioDocument.GetParentParagraphUnit(segmentPair)` rather than re-reading the SDLXLIFF
  from disk, and also react to `ContentChanged`/`SegmentTranslated` so the target column
  follows typing.  The cloud's `PreviewForms` (rows per form, count resolver) is free of BCM
  and copies into the expansion library.
- **The task report is empty** and its file name carries a trailing space
  (`ICU Expand Plural Forms .xml`): `CreateReport` with empty data.  Slice 3.

## Open items

- The Phase 2 Studio run (kickoff prompt above), including the three-language project.
- Slice 3: settings classes and WPF pages for both tasks, allowlist editing, task reports,
  localisation into the nine languages.
- Phase 3: ICU Forms view part and verifier.
- The AppStore material and the plain guide come at the end.

## Traps

- **Studio locks the deployed DLLs**: build with `-p:DeployPluginPackage=false` while it runs,
  or the plugin project fails with PFE402 and the tests do not rebuild.
- **Do not add to `unit.Properties.Contexts` in place.**  Clone.
- **Do not trust placeholder tags to reach a generated JSON file.**  Locked content only.
- **A unit expanded before `icu:expandedSelectors` existed** is finalised with every plural-kind
  selector treated as expanded (Project 40's files are such units).
- All earlier traps stand (handover 01).
