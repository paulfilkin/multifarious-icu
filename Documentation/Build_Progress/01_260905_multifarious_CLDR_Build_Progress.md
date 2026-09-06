# 01 - multifarious_CLDR Build Progress - 2026-09-05

First session.  The plan was agreed, the three libraries were ported from the Trados Cloud
project to net48 with their tests green, and the plugin builds, packages and deploys with both
batch tasks declared.  The Studio evidence run has not happened yet: Studio was not restarted
after the deploy, so the package was never installed.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then the reference design record
> `c:\Users\paul\Documents\GIT\.NET\CLDR Support\Docs\Design\ICU-Message-Expansion-Design-v4.md`
> (sections 5, 6, 8 and 8.6 are the ones the SDLXLIFF layout has to honour), then
> `Documentation\Build_Progress\01_260905_multifarious_CLDR_Build_Progress.md`.
>
> Phase 0 is the Studio spike.  The plugin is deployed with read-only probe processors; the
> next step is to read `%TEMP%\multifarious-icu-diagnostics.log` after Paul has launched
> Studio with ICU logging ticked, created an en-GB to ru-RU project from `test-corpus\`, and run
> "ICU Expand Plural Forms".  From the log: confirm the tasks list, record the exact
> `FileTypeId` strings for the allowlist, and record whether the JSON and Java Resources filters
> leave an ICU value as one plain segment or tag its braces.  Then agree the SDLXLIFF layout
> (placeholder tags versus locked content, context info for unit metadata, comment marker with
> metadata for segments) and write the Phase 1 expansion processor.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.

## Where we are in one line

Everything below the Studio boundary is ported and green; nothing above it has run in Studio.

## What was done

- **Plan agreed.**  Two batch tasks on bilingual target files (Expand after Copy to Target
  Languages, Finalise between Update Main TM and Generate Target Translations), the planner
  layer reused, a new writer against the bilingual API.  Decisions: target side only,
  allowlist defaulting to JSON and Java Resources, AppStore name
  "multifariousICU Support for Trados Studio", Studio spike before any layout code.
- **Code root scaffolded** at `multifarious_CLDR\multifarious_CLDR\`: `Directory.Build.props`
  (net48 x64, C# 12, nullable, warnings as errors, PolySharp, shared signing key),
  `multifarious.Icu.sln`, five projects.
- **Libraries ported.**  `Icu.Core` (1,566 lines) and `Icu.Cldr` (1,842 lines) copied verbatim;
  the four planner files became `multifarious.Icu.Expansion`.  net48 substitutions are listed
  in `CLAUDE.md` under Porting notes.  The JSON loaders moved to Newtonsoft.
- **Tests ported.**  1,282 passing: 1,099 CLDR conformance and data, 148 Core, 28 planner and
  classifier, 7 batch task contract tests (ported from the YAML plugin to xunit).
- **Plugin scaffolded.**  `IcuExpandTask` and `IcuFinaliseTask` with the `AutomaticTask` and
  `AutomaticTaskSupportedFileType(BilingualTarget)` attributes; `PluginResources.resx` with the
  names; `Constants` with task ids and the default allowlist; `Diagnostics` on
  `MULTIFARIOUS_ICU_DIAGNOSTICS`; `ProbeProcessor` logging every paragraph unit's contexts,
  segment count, text runs, tag counts and reconstructed source text.  `ShouldProcessFile`
  logs every file's `FileTypeId`, language and path, so the allowlist strings can be confirmed
  from the log.
- **Package verified.**  `IcuSupport.plugin.xml` declares both extensions with the supported
  file type; `IcuSupport.sdlplugin` carries `Icu.Core.dll`, `Icu.Cldr.dll` and
  `multifarious.Icu.Expansion.dll`; deployed to `Packages\` at 20:54.
- **Repository.**  `git init` on `main`, `.gitignore` publishing only the inner code root,
  remote `origin` added through the `github-paulfilkin` alias and reachable.  Nothing
  committed yet; the first commit message is proposed in the session transcript.
- **Test corpus.**  The six fixture files from the reference project's capture recipe copied
  to `test-corpus\`: `messages.json`, `messages.properties`, `nested.json`, `tagged.json`,
  `simple.json`, `less_simple.json`.
- **Launcher.**  `PF_Automate.ahk` gained an "ICU" entry mapping to
  `MULTIFARIOUS_ICU_DIAGNOSTICS`.  A user-level copy of the variable that had been set by
  mistake was removed again; the launcher is the only place it should be set.

## Findings worth keeping

- **Studio installs a package at start, not at deploy.**  A deploy while Studio is closed puts
  the `.sdlplugin` in `Packages\`; the `Unpacked\` folder gains an entry only on the next
  start.  Its absence is proof that Studio has not run since the deploy.
- **Studio 2026 ships the BCM stack** (`Sdl.Core.Bcm.BcmModel` 1.7.0.0, the same build the
  reference tracks, plus `BcmConverters` with `FileToBcmConverter` and `BcmToFileConverter`).
  Recorded and rejected as the basis for this plugin; see `CLAUDE.md`.
- **Metadata carriers in the bilingual API** that implement `IMetaDataContainer`: tag
  properties (`IAbstractBasicTagProperties` and below), `IComment`, `IContextInfo`,
  `ITranslationOrigin`.  `ISegmentPairProperties`, `IParagraphUnitProperties` and
  `ILockedContentProperties` carry none.  Context info metadata is confirmed to persist in
  SDLXLIFF (`cxt-def` props in the DXF corpus); comment and tag metadata persistence is what
  the spike has to show.
- **net48 nullable analysis cannot see through BCL guards.**  `string.IsNullOrWhiteSpace(x)`
  leaves `x` maybe-null afterwards; write the check inline.

## Open items

- The Studio evidence run (kickoff prompt above).  Until it has run, the layout decision
  (placeholder tags versus locked content, where segment metadata lives) stays open.
- First commit and push, once the proposed message is approved.
- Localisation of `PluginResources` into the nine languages, and the task settings pages, are
  Phase 1 and 3 work.

## Traps

- Do not read the cloud project's D11 (locked content) as settled for Studio.  It was decided on
  the cloud writers' behaviour; Studio's filters are different implementations and the spike
  decides.
- `Documentation\` is gitignored on purpose.  Back it up locally.
