# 03 - multifarious_CLDR Build Progress - 2026-09-06

Third session.  Phase 2 is proven in Studio and slice 3 is built, proven and committed: settings
pages with help panels for both tasks, task reports rendered in Studio's Reports view, and the
strings localised into the eight languages the sibling plugins ship.  Six commits on `main`,
pushed.  Two decisions changed the design on the way: over-budget messages are laid out walked
rather than passed through, and target segments never carry a comment.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then the reference design record
> `c:\Users\paul\Documents\GIT\.NET\CLDR Support\Docs\Design\ICU-Message-Expansion-Design-v4.md`
> (section 12 is the preview design), then
> `Documentation\Build_Progress\03_260906_multifarious_CLDR_Build_Progress.md`.
>
> Both tasks, their settings pages, their reports and the localisation are proven in Studio and
> committed.  Next is Phase 3: the ICU Forms editor view part and a verifier, on Localyzer
> Connect's view part pattern (`c:\Users\paul\Documents\GIT\.NET\LocalyzerConnect\`, see
> "Findings" in handover 02 and the notes below).  Propose the plan for the view part first:
> what it shows for the active segment's paragraph unit (every form of the message, source and
> target, the counts that select each, the rendered message for sample counts), how it follows
> the active segment and typing, and what the verifier checks.  Agree the plan before coding.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.

## Where we are in one line

Everything below the editor is done and proven; Phase 3 (the view part and verifier) is next.

## What was done

- **Phase 2 Studio run (Projects 41 and 42)**: three-language project en-GB to ru-RU, ja-JP and
  ar-SA.  A throwaway probe (`multifarious.Icu.Tests\BatchTasks\GeneratedFileProbe.cs`,
  untracked, driven by `MULTIFARIOUS_ICU_PROBE_PROJECT`) flattens every generated file, parses
  every value and compares argument sets and category sets with the source.  141 values, all
  parsing: Arabic six forms, Japanese one, Russian four, explicit values kept, ordinals
  resolving to `other`, argument-only messages intact through pseudo-translation, every
  apostrophe doubled exactly once, the typed brace as `'{literal}'`, and a second Finalise
  leaving all 18 generated files byte-identical.
- **Over-budget messages laid out walked** (commit 539e7a3).  Project 41 showed the Arabic
  sync.status value (36 segments against the budget of 24) passed through as plain text and
  garbled by pseudo-translation.  `ExpansionPlanner.PlanWalked` plans the hoisted message with
  every selector walked and a note in every segment comment; the processor writes that layout
  with a Medium comment on the unit and the reason in the report.  The diagnostics log now
  appends a dated header per run instead of truncating.
- **Settings pages** (commit 56b93f3): `IcuExpandSettings` and `IcuFinaliseSettings` derive
  from Studio's `SettingsGroup`, bound to the tasks with `RequiresSettings`, pages on
  `DefaultSettingsPage<View, Settings>` over WPF user controls implementing
  `IUISettingsControl` and `ISettingsAware<T>` (the Multilingual XML reference's pattern, in
  `multifarious_YAML\references\`).  Strings in `Resources\UIStrings.resx` with a generated
  accessor (`tools\generate-uistrings.ps1`, the YAML plugin's script).  Proven in Project 44: a
  budget of 36 set on the page was stored in the project and read by the task.
- **The allowlist was built as a setting and dropped** (Paul): the two proven file types stay
  hard-coded in `Constants.DefaultFileTypeIds`, and new ones join once run end to end.
- **Help panels** (commit 87cb58a): a collapsible "Why…?" panel under every group, two short
  paragraphs each, and an introduction on each page, as the YAML plugin's pages have.
- **Reports** (commits f2f0d5e, 6e42583): the processors record an outcome per unit; the tasks
  write one report per language direction through `CreateReport(..., LanguageDirection)`,
  which names the file with the language pair.  `Resources\TaskReport.xsl` is the one embedded
  stylesheet, branching on the task name and taking every label from the XML.  Tests render
  both reports through `XslCompiledTransform` with a stand-in for Studio's `XmlReporting`
  extension object.  Proven in Project 45, rendered in the Reports view.
- **Localisation** (commit de0c41c): eight satellites (de, es, fr, it-IT, ja, ko-KR, ru-RU,
  zh-CN) for all 78 strings; a satellite test suite on the YAML pattern.  The package carries
  the culture folders without manifest entries.
- **Target comments removed** (Paul): finalise warnings join the source segment's comment
  marker; no code path writes a comment into a target segment, and a test asserts it.
- 1,360 tests.

## Decisions taken this session

- **Over-budget messages are laid out walked, not passed through.**  Design 5.7 is not
  followed here; recorded in CLAUDE.md.
- **No comments in target segments, ever.**  Target comments are the translator's.  Expand
  warnings go on the paragraph unit, finalise warnings on the source segment.
- **File types hard-coded**, not a setting.
- **Settings groups are keyed on their class names** (`IcuExpandSettings`,
  `IcuFinaliseSettings`): Studio's bundle uses `typeof(T).Name` and ignores an `Id` override.
  Renaming a class orphans every project's settings.
- **The tag construct is not a setting**: the placeholder variant produces JSON without its
  placeholders and stays a test-only option.
- **Report names stay English in every language**, matching the task names in Studio's list,
  which are neutral in `PluginResources.resx`.
- **Translator-facing comments in the SDLXLIFF stay English.**  Not raised again; raise it if a
  customer asks.

## Findings worth keeping

- **Studio's editor AutoCorrect turns a typed straight apostrophe into U+2019**, which ICU treats
  as plain text.  Only the ASCII apostrophe is ICU syntax; the escaping is right, and the guide
  should say so.
- **Studio renders a custom task's report through the first `.xsl` embedded in the task
  assembly** (`TaskReportTypeManager`), and the `AutomaticTaskAttribute` plugins use cannot name
  one per task, so there is exactly one stylesheet.  The viewer supplies an `XmlReporting`
  extension object with `GetDefaultCssLinkTag()`, `GetImagesUrl()` and `GetResourceString()`;
  the default CSS has the classes `InfoList`, `InfoItem`, `InfoData`, `ReportTable`, `TypeHead`,
  `Unit`, `File`, `Total`.
- **A report string written through an `XmlWriter` over a `StringBuilder` declares utf-16.**
  Omit the declaration, as Studio's own reports do.
- **The batch task thread's culture is not the user's** (en-US date on a British machine), so
  nothing in a report or a comment should use a culture's short date pattern.
- **`GetSettingsGroup<T>()` keys on the class name.**  An empty metadata value on a context
  (`icu:expandedSelectors=""`) survives the SDLXLIFF round trip.
- **A settings page control must not carry a fixed minimum width**: the batch task wizard's
  panel is narrower than the file type settings dialog and the right-hand column was cut off.
- **`ProjectFile.GetLanguageDirection()`** (an extension in `Sdl.ProjectAutomation.AutomaticTasks`)
  is how a task gets the language direction for the per-language `CreateReport` overload.
- **Localyzer Connect is the Phase 3 pattern** (handover 02): `[ViewPart]` with
  `ViewPartLayout(Dock = Bottom, LocationByType = typeof(EditorController))`, a WPF `WebView2`
  control (package with `ExcludeAssets=runtime`; Studio ships 1.0.3856.49), a `[RibbonGroup]`
  with an `[Action]` showing the view part, `EditorController.ActiveDocumentChanged` and
  `IStudioDocument.ActiveSegmentChanged` driving the content.  Do differently: read the active
  segment's paragraph unit through `IStudioDocument.GetParentParagraphUnit(segmentPair)` rather
  than the SDLXLIFF on disk, and react to `ContentChanged` and `SegmentTranslated` so the target
  column follows typing.  The layout can be read back the way the finalise processor reads it
  (selector open, branch open, close brace; `icu:expandedSelectors` on the unit context; the
  per-segment metadata on the source comment).  The cloud's `PreviewForms` (rows per form, count
  resolver) is free of BCM and copies into the expansion library.

## Open items

- Phase 3: the ICU Forms view part and a verifier.  Plan first.
- The help-panels commit (87cb58a) holds only the two XAML files; the strings it references
  landed in the reports commit after it, so that one commit does not build on its own.  History
  is right from f2f0d5e onward.  Leave it.
- The AppStore material and the plain guide come at the end.  The guide must mention the
  AutoCorrect apostrophe.
- The probe stays untracked; delete or keep at the end.

## Traps

- **Studio locks the deployed DLLs**: build with `-p:DeployPluginPackage=false` while it runs.
  The build commands in this session checked for `SDLTradosStudio` and chose the flag.
- **Regenerate `UIStrings.cs` after every resx change** (`tools\generate-uistrings.ps1`); the
  parity test fails otherwise.
- **Every language must carry every key**; the satellite tests fail on a missing or extra one.
- **Do not add a second `.xsl` resource** to the plugin assembly.
- All earlier traps stand (handovers 01 and 02).
