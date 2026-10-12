# multifariousICU Support for Trados Studio

## Introduction
Text that includes a number is written differently from one language to the next. Applications handle this with ICU MessageFormat, the notation of the International Components for Unicode library, in which one string carries every form the source language needs: `{count, plural, one {# unread message} other {# unread messages}}`. Each language needs its own set of forms, from one in Japanese to six in Arabic. The application selects the form at run time using the plural rules in CLDR, the Common Locale Data Repository of the Unicode Consortium.

multifariousICU Support gives the translator each form as a complete sentence in its own segment, with the syntax locked and the arguments protected, exactly the forms the target language needs. A second task puts the message back together as valid ICU, and the first lays it out again, so a file can be switched between the two shapes at any point. A window under the editor shows every form of the message the translator is working on, and a verifier lists what is still wrong.

It is built for translators and language service providers doing software localisation. No knowledge of the ICU notation is needed to translate, and no plural rule has to be looked up: the forms and the numbers that select them come from the same CLDR data the application uses.

Full documentation in detail is maintained at <https://multifarious.filkin.com/product/icu-support/>.

---

## Key Features
- **One segment per form the language needs**: Russian four, Arabic six, Japanese one, each a whole sentence.
- **Syntax protected**: the selector and branch syntax sits between the segments as locked text. Arguments such as `{name}` and the count marker `#` are placeholder tags inside the sentence, placed like any tag, with an option to lock them.
- **Restore to whole messages**: the forms a language does not use are removed, apostrophes and braces typed as text are escaped, placeholders are checked, untranslated forms are filled from the source with a warning, and each message becomes one plain segment again, the shape Studio's file writers need. Expand lays a restored message out again with its translations and confirmation states.
- **Keep what is already translated**: a message pre-translated as a whole from a memory of restored messages can be left as it is, and locked, when every segment of it is matched at a chosen percentage or better. Off by default.
- **ICU Forms window**: every form of the active message with the numbers that select it, written out in full, the source and target as sentences, a count box, the reassembled message checked as you type, and a zoom.
- **ICU Verifier**: untranslated forms, placeholder mismatches, a count marker typed as text and a message that will not parse, on F8 and as segments are confirmed, with a severity per check.
- **Reports** per language pair listing every message and what happened to it.
- **Localised**: settings pages with help, the window, the reports and the verifier messages in English, German, French, Spanish, Italian, Japanese, Korean, Russian and Simplified Chinese.

---

## Installation Guide
The app is a standard Trados Studio plugin installed from the AppStore.

### Technical Requirements
- **Supported OS**: Windows 10 or later (as required by Trados Studio)
- **Supported Studio versions**: Trados Studio 2026
- **Dependencies**: none beyond Trados Studio itself; the ICU parser and the CLDR plural data are inside the plugin
- **Permissions**: none beyond a normal plugin installation

### Step-by-Step Installation
1. **Step 1**: Download/Install from the Trados Studio integrated AppStore, or download the `.sdlplugin` from the plugin's AppStore record at <https://appstore.rws.com/plugin/490> and double-click it.
2. **Step 2**: Restart Trados Studio. The batch tasks **Expand ICU** and **Restore ICU** appear in the Batch Tasks list, **ICU Forms** on the editor's Add-ins tab in the **multifarious ICU** group, and **ICU Verifier** under Project Settings, Verification.

---

## Configuration and Setup
The defaults are chosen so that a first project works without changing anything: plural and ordinal messages are expanded, the placeholders are tags a translator can place, the branch budget is 24, every message is expanded whatever its match, and Restore fills an untranslated form from the source with a warning.

### Configuration Steps
1. **Step 1**: Run the task through **Batch Tasks** and open its page on the Settings step, or open **Project Settings** and find the two tasks and the verifier there. A project template carries the settings into new projects.
2. **Step 2**: Adjust what you need. Expand: which message kinds to expand, which source form a new form starts from, whether to lock the placeholders, the branch budget, whether to keep messages already translated at a given match and lock them, and what to do with a value that does not parse. Restore: what to do with an untranslated form and with a placeholder mismatch. Verifier: whether it runs and the severity of each check.
3. **Step 3**: Save. Expand settings take effect on the next run of the task; a file already expanded is left alone until it is restored.

> **Tip**: Every group on the pages has a help panel that explains the choice: why expand at all, where the text for a new form comes from, why the placeholders are tags, what the branch budget is for.

---

## Usage Instructions
The plugin fits Studio's normal workflow: create the project, expand, translate, restore, generate the target files.

### Basic Usage
1. **Create the project** with your JSON or Java properties files as usual.
2. **Expand**: run **Expand ICU** on the target files, after Copy to Target Languages and before Analyse and Pre-translate, so word counts and matches see whole sentences. In a custom task sequence it goes after Copy to Target Languages; otherwise run it as a batch task straight after the project is created.
3. **Translate**: each form is a segment. Open **ICU Forms** from the Add-ins tab to see every form of the message with the numbers that select each, try a count, and watch the reassembled message stay valid as you type. Keep the placeholder tags; QuickPlace inserts them.
4. **Verify**: press F8. The ICU Verifier lists untranslated forms, placeholder problems and anything that will not parse.
5. **Restore**: run **Restore ICU** on the target files before Generate Target Translations. Each message becomes one plain segment on each side: the target in the shape the translator worked in, with the forms the language does not use removed, and the source as the developer wrote it. A message with a placeholder problem stays expanded with its tags, gets a comment on the target segment, and has to be restored again once corrected.
6. **Generate the target files**. Every message is valid ICU with exactly the forms the language needs.

To go back to the forms, run **Expand ICU** on the restored file. Each form gets its translation and its confirmation state back, so expanded and restored can be switched between as often as needed.

### Advanced Tips
- **A form the source language does not have**, such as Russian "few", starts from the source's "one" form where there is one, otherwise from "other". The ICU Forms window's tooltip says which. Switch to "always other" if your translators prefer the general form as the starting point.
- **Nested messages**: a select, which chooses a branch by a value such as gender, keeps its branches, and a plural inside each branch is expanded. Offsets, exact values such as `=0` and ordinals are handled.
- **Two independent plurals in one message multiply**: six forms times six is 36 segments for Arabic. Over the branch budget the message keeps the source's own branches with the syntax protected, and the report says so. The ICU Forms window shows which numbers each kept branch is used for; `other` takes every number the message has no form for.
- **Apostrophes**: type them as usual. Restore doubles the straight apostrophe as ICU requires. Studio's AutoCorrect may replace it with a typographic one, which ICU treats as plain text.
- **Translation memory**: Update Main Translation Memories stores the file as it is at the time: one unit per form while expanded, one unit per whole message once restored, the source as the developer wrote it against the whole target message. Store whichever suits the memory's use, or both.
- **Pre-translated whole messages**: a project pre-translated from a memory of restored messages arrives with whole messages matched. Expand splits such a message into its forms, each carrying the message's match, unless the Expand setting to keep messages already translated is on, in which case a message matched at the threshold or better is left as it is and, if chosen, locked.
- **Reports**: after each task, one report per language pair in the Reports view lists every message with its outcome and any warning.

---

## Troubleshooting and FAQs

**Q1: The task ran but nothing changed in my file.**
A1: The task processes Studio's JSON and Java Resources file types only, and only values that contain an ICU message. A file already expanded is left alone. The report lists what was seen.

**Q2: The ICU Forms window says "Not available for this file".**
A2: The file is of a type the plugin does not handle. The window and the tasks work with JSON and Java properties files.

**Q3: A message shows the source's own forms only, and a warning about the branch budget.**
A3: The message has several independent plurals and would need more segments than the budget allows. Raise the budget on the expand page if you want every form, or ask the developer to split the message. Meanwhile the ICU Forms window shows which numbers each kept branch is used for.

**Q4: The Messages window says a form has no translation.**
A4: Every form the language needs must be present. Translate it, or let Restore fill it from the source, which it does with a warning so the gap is recorded in the report.

**Q5: I typed a `#` and the verifier warns about it.**
A5: The count marker in the source is a tag; use that one, with QuickPlace or by copying the source. A `#` typed as text is shown literally in the application.

**Q6: Can I go back to the forms after Restore?**
A6: Yes. Run Expand ICU on the restored file. Each form comes back with its translation, its confirmation state and its match, and a Restore after that gives the same message again. Comments come back too, around the text they were on: a comment on a restored message appears on every form that holds that text.

**Q7: Restore left a message expanded with a comment about its placeholders.**
A7: A target form has lost a placeholder the source has, or gained one. The message stays expanded with its placeholders as tags so the missing one can be placed with QuickPlace. Once corrected, run Restore ICU again: the comment goes and the message is put back together.

**Q8: My project was pre-translated and Expand split the matched messages into forms.**
A8: That is the default: every message is expanded so a translator can check a match form by form. To leave matched messages alone, turn on **Keep messages already translated** on the Expand page and set the minimum match; Perfect Match and Context Match count as 100%. Kept messages are locked unless you switch that off.

**Q9: A Spanish, French, Italian or Portuguese file has a "many" form I have never seen.**
A9: CLDR gives these languages, and Catalan, a `many` form for a round million and above. The application uses it, so the form is needed. The ICU Forms window shows the numbers: 1000000, 2000000, 3000000.

**Q10: My values contain HTML. Can the markup be tags?**
A10: Yes. Turn on the file type's Embedded Content settings before creating the project: the HTML embedded content processor on JSON, or tag definition rules on Java Resources. Expand puts each tag into every form it belongs to and Restore puts them back, so the generated file carries the markup as the source did. A tag pair that opens in one branch and closes in another cannot be laid out, and that message is left as it is with a warning.

> **Tip**: Full documentation in detail is maintained at <https://multifarious.filkin.com/product/icu-support/>.

---

## License
Copyright 2026 multifarious. Released under the Apache License, Version 2.0. The source code is at <https://github.com/paulfilkin/multifarious-icu>.

---

## Acknowledgements
The plural rules and their example numbers are the Unicode **CLDR** plural rules, used under the Unicode licence.

---
