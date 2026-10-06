# multifariousICU Support for Trados Studio

A Trados Studio 2026 plugin that turns ICU MessageFormat plurals into ordinary translation: one segment per form the target language needs, each a whole sentence, with the syntax protected and the message reassembled for you.

- AppStore listing: <https://appstore.rws.com/plugin/490>
- User documentation: <https://multifarious.filkin.com/product/icu-support/>
- Changelog: [Documentation/appstore/multifariousICU_Changelog.md](Documentation/appstore/multifariousICU_Changelog.md)

## The problem

Text that includes a number is written differently from one language to the next.  English has two forms, Russian four, Arabic six, Japanese one.  Applications handle this with ICU MessageFormat, the notation of the International Components for Unicode library, in which one string carries every form:

```
Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!
```

A translator normally receives that whole expression as one segment.  They have to know the syntax, know which forms their language needs, and type the missing ones by hand without breaking a brace.  The application then picks a form at run time using the plural rules in CLDR, the Common Locale Data Repository of the Unicode Consortium, so a form the translator did not add is a form the user never sees.

## What the plugin does

**ICU Expand Plural Forms**, a batch task, restructures every `plural` and `selectordinal` message in a JSON or Java properties target file into one segment per CLDR form the target language needs.  For the message above a Russian file gets four segments, `one`, `few`, `many` and `other`, each a complete sentence.  The selector syntax between the forms is locked text.  Arguments such as `{name}` and the count marker `#` are placeholder tags inside the sentence, placed like any tag in Studio.  A `select` keeps its branches and a plural inside one is expanded.  A message with arguments and no plural becomes one segment with the arguments protected.

**ICU Finalise Messages**, a batch task for the end of the workflow, removes the forms the language does not use, escapes apostrophes and braces typed as text, checks the placeholders against the source, fills an untranslated form from the source with a warning, and turns the placeholder tags back into locked text so Studio's file writers produce valid ICU.  A second run on a clean file changes nothing.

**ICU Forms**, a window under the editor, shows every form of the message the active segment belongs to: the numbers that select each form, source and target as sentences, a count box, and the whole message reassembled and checked as you type.

**ICU Verifier** joins Studio's verifiers on F8: an untranslated form, placeholders that differ from the source, a count marker typed as text, or a message that will not parse.

Each task writes a report per language pair.  The settings pages, the window, the reports and the verifier messages are in English, German, French, Spanish, Italian, Japanese, Korean, Russian and Simplified Chinese.

The plural rules and the numbers that select each form come from the CLDR data embedded in the plugin, the same data the applications use, so the forms Studio offers are the forms the application will select.

## Workflow

1. Create the project with your JSON or Java properties files as usual.
2. Run **ICU Expand Plural Forms** on the target files, after Copy to Target Languages and before Analyse and Pre-translate, so word counts and matches see whole sentences.
3. Translate.  Open **ICU Forms** from the Add-ins tab to see every form of the current message.
4. Press F8.  The verifier lists what is still wrong.
5. Run **ICU Finalise Messages** after Update Main Translation Memories and before Generate Target Translations.
6. Generate the target files.

Finalise must run before Generate Target Translations: Studio's JSON writer does not write placeholder tags, and Finalise is what turns them into locked text.

The full user guide is at <https://multifarious.filkin.com/product/icu-support/>.

## Scope

The tasks process Studio's own JSON and Java Resources file types only.  Other file types are left alone and the ICU Forms window says so.  The list is hard-coded in `Constants.DefaultFileTypeIds`: a file type joins it once it has been run end to end in Studio, because the classifier alone is not a safe gate (a brace in a Word document is not ICU).

Both tasks run on bilingual target files, so each language in a multilingual project gets exactly its own set of forms.

## Installing

Install from the integrated AppStore inside Trados Studio, or download the `.sdlplugin` from the [AppStore record](https://appstore.rws.com/plugin/490) and double-click it.  Restart Studio.  The two tasks appear in the Batch Tasks list, ICU Forms on the editor's Add-ins tab in the multifarious ICU group, and ICU Verifier under Project Settings, Verification.

Requirements: Trados Studio 2026 on Windows.  There are no other dependencies.  The ICU parser and the CLDR data are inside the plugin.

## Building from source

Prerequisites:

- Trados Studio 2026 installed.  The Studio assemblies are referenced from the install, not from NuGet, at `%ProgramW6432%\Trados\Trados Studio\Studio19`.  Override with `-p:TradosFolder=<path>` if yours is elsewhere.  The tests need it too.
- Visual Studio 2026 with the .NET desktop development workload and the .NET Framework 4.8 targeting pack.  Any edition.

Build with full MSBuild, not `dotnet build`: the targets are net48 desktop plus the SDL packaging task.

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  multifarious_CLDR\multifarious.Icu.sln -restore -t:Build -p:Configuration=Debug
```

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" `
  multifarious_CLDR\multifarious.Icu.Tests\bin\Debug\net48\multifarious.Icu.Tests.dll /Platform:x64 "/logger:console;verbosity=minimal"
```

Notes:

- A build packages the `.sdlplugin` and deploys it to `%AppData%\Trados\Trados Studio\19\Plugins\Packages`.  Studio installs it on its next start.  Studio locks the deployed DLLs while it is running, so close it first or build with `-p:DeployPluginPackage=false`.
- `TreatWarningsAsErrors` is on.  The build is expected to be clean.
- The UI strings accessor `Resources\UIStrings.cs` is generated from `UIStrings.resx` by `multifarious_CLDR\tools\generate-uistrings.ps1`.  Run it after adding or renaming an entry.  A test fails if the two drift apart, and another compares every translated resx with the English one.
- Setting `MULTIFARIOUS_ICU_DIAGNOSTICS=1` in Studio's environment writes `%TEMP%\multifarious-icu-diagnostics.log`.

## Repository layout

```
multifarious_CLDR\                  solution root
    Icu.Core\                       ICU MessageFormat: parser, tree, serialiser, hoisting, escaping
    Icu.Cldr\                       CLDR plural data, rule evaluator, locale fallback, hints
    multifarious.Icu.Expansion\     classifier, planner, plan, options (no Studio dependency)
    multifarious.Icu.BatchTasks\    the plugin: tasks, processors, settings, editor window, verifier
    multifarious.Icu.Tests\         one xunit project, folders per library
    test-corpus\                    ICU fixture files (JSON, properties)
    tools\                          generate-uistrings.ps1
Documentation\
    appstore\                       AppStore description, documentation, user guide, changelog, samples
    Build_Progress\                 session handovers, one per working session
Images\                             tile and icon
```

The first three projects reference nothing from Studio and are tested without it.  `multifarious.Icu.BatchTasks` is the Studio-facing shell over them.  The libraries target .NET Framework 4.8 in C# 12, with PolySharp supplying the compiler-facing types the framework lacks.

`Icu.Core` and `Icu.Cldr` are shared file for file with the companion Trados Cloud app, which is why their namespaces do not carry the multifarious prefix.

## Design

The design is recorded as it was made, in the handovers under `Documentation\Build_Progress\`.  The decisions that shape the code:

- **Target side only.**  Both tasks run after Copy to Target Languages, so each language gets its own category set and no union of forms across languages is needed.
- **Locked syntax between segments, tags inside them.**  A locked span cannot be placed by the translator at all.  A tag works with QuickPlace, tag verification and translation memory placeables.  The selector and branch syntax is therefore locked text between the segments, and the arguments inside a segment are placeholder tags.
- **Finalise reads the layout from the content.**  Locked syntax has three shapes (selector open, branch open, close brace), the expanded selectors are recorded in the paragraph unit's context, and escaping is unescape-then-escape, so a second run is idempotent.
- **Over-budget messages are laid out walked, not passed through.**  Two independent plurals multiply: six forms times six is 36 segments for Arabic.  Above the branch budget the message keeps the source's own branches with the syntax protected, and the report says so.
- **No comments except warnings, and those on the paragraph unit.**  Studio copies a comment inside a source segment into the target.  Expand warnings go on the unit, which Studio never copies.  Finalise findings go on the target segment concerned, as a reviewer would record them, and are removed at the start of the next run.
- **Bilingual API, not BCM converters.**  Studio ships converters from SDLXLIFF to its Bilingual Content Model and back, which would have let the cloud app's code run unchanged.  They are undocumented and the fidelity of the regenerated SDLXLIFF is unproven, so the tasks operate on SDLXLIFF through the File Type Support Framework.

## Maturity and feedback

The tasks, the window and the verifier have been tested end to end on a set of ICU messages covering nested selects, exact values, offsets, ordinals and argument-only messages, generating files that parse and render as the source does for every number.  Real projects will contain messages that set never saw.  Please open an issue for a message that lays out oddly, a form a language needs that the window describes badly, or a file type you would like added.

## Licence

Apache License, Version 2.0.  See [LICENSE](LICENSE) and [NOTICE](NOTICE).  You may use, modify and redistribute the code, in open or closed products, provided the licence and notice travel with it.  Contributions are accepted under the same licence.

Third-party material:

- The plural rules and their example numbers in `Icu.Cldr\Data\` are from the Unicode CLDR project, release 48, used under the [Unicode Licence v3](https://www.unicode.org/license.txt).  A copy is beside the data files.
- Newtonsoft.Json (MIT) is referenced but not redistributed.  Studio ships it.
- PolySharp (MIT) is a build-time source generator.  Nothing of it reaches the package.
- xunit (Apache 2.0) and FsCheck (BSD 3-clause) are used by the tests only.
- The Trados Studio assemblies and the `Sdl.Core.PluginFramework` packages belong to RWS.  They are referenced from a Studio installation and are not part of this repository.

## Author

multifarious.  <https://multifarious.filkin.com>
