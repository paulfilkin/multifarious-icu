# multifariousICU Support for Trados Studio

## Introduction
Text that includes a number is written differently from one language to the next. Applications handle this with ICU MessageFormat, the notation of the International Components for Unicode library, in which one string carries every form the source language needs: `{count, plural, one {# unread message} other {# unread messages}}`. Each language needs its own set of forms, from one in Japanese to six in Arabic. The application selects the form at run time using the plural rules in CLDR, the Common Locale Data Repository of the Unicode Consortium.

multifariousICU Support gives the translator each form as a complete sentence in its own segment, with the syntax locked and the arguments protected, exactly the forms the target language needs. A second task assembles the message back into valid ICU when the translation is done. A window under the editor shows every form of the message the translator is working on, and a verifier lists what is still wrong.

It is built for translators and language service providers doing software localisation. No knowledge of the ICU notation is needed to translate, and no plural rule has to be looked up: the forms and the numbers that select them come from the same CLDR data the application uses.

Full documentation in detail is maintained at <https://multifarious.filkin.com/product/icu-support/>.

---

## Key Features
- **One segment per form the language needs**: Russian four, Arabic six, Japanese one, each a whole sentence.
- **Syntax protected**: the selector and branch syntax sits between the segments as locked text. Arguments such as `{name}` and the count marker `#` are placeholder tags inside the sentence, placed like any tag, with an option to lock them.
- **Finalise before delivery**: the forms a language does not use are removed, apostrophes and braces typed as text are escaped, placeholders are checked, untranslated forms are filled from the source with a warning, and the tags become locked text for Studio's file writers.
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
2. **Step 2**: Restart Trados Studio. The batch tasks **ICU Expand Plural Forms** and **ICU Finalise Messages** appear in the Batch Tasks list, **ICU Forms** on the editor's Add-ins tab in the **multifarious ICU** group, and **ICU Verifier** under Project Settings, Verification.

---

## Configuration and Setup
The defaults are chosen so that a first project works without changing anything: plural and ordinal messages are expanded, the placeholders are tags a translator can place, the branch budget is 24, and Finalise fills an untranslated form from the source with a warning.

### Configuration Steps
1. **Step 1**: Run the task through **Batch Tasks** and open its page on the Settings step, or open **Project Settings** and find the two tasks and the verifier there. A project template carries the settings into new projects.
2. **Step 2**: Adjust what you need. Expand: which message kinds to expand, which source form a new form starts from, whether to lock the placeholders, the branch budget, and what to do with a value that does not parse. Finalise: what to do with an untranslated form and with a placeholder mismatch. Verifier: whether it runs and the severity of each check.
3. **Step 3**: Save. Expand settings take effect on the next run of the task; a file already expanded is left alone.

> **Tip**: Every group on the pages has a help panel that explains the choice: why expand at all, where the text for a new form comes from, why the placeholders are tags, what the branch budget is for.

---

## Usage Instructions
The plugin fits Studio's normal workflow: create the project, expand, translate, finalise, generate the target files.

### Basic Usage
1. **Create the project** with your JSON or Java properties files as usual.
2. **Expand**: run **ICU Expand Plural Forms** on the target files, after Copy to Target Languages and before Analyse and Pre-translate, so word counts and matches see whole sentences. In a custom task sequence it goes after Copy to Target Languages; otherwise run it as a batch task straight after the project is created.
3. **Translate**: each form is a segment. Open **ICU Forms** from the Add-ins tab to see every form of the message with the numbers that select each, try a count, and watch the reassembled message stay valid as you type. Keep the placeholder tags; QuickPlace inserts them.
4. **Verify**: press F8. The ICU Verifier lists untranslated forms, placeholder problems and anything that will not parse.
5. **Finalise**: run **ICU Finalise Messages** on the target files after Update Main Translation Memories and before Generate Target Translations. The tags become locked text at this step. A message with a placeholder problem keeps its tags, gets a comment on the target segment, and has to be finalised again.
6. **Generate the target files**. Every message is valid ICU with exactly the forms the language needs.

### Advanced Tips
- **A form the source language does not have**, such as Russian "few", starts from the source's "one" form where there is one, otherwise from "other". The ICU Forms window's tooltip says which. Switch to "always other" if your translators prefer the general form as the starting point.
- **Nested messages**: a select, which chooses a branch by a value such as gender, keeps its branches, and a plural inside each branch is expanded. Offsets, exact values such as `=0` and ordinals are handled.
- **Two independent plurals in one message multiply**: six forms times six is 36 segments for Arabic. Over the branch budget the message keeps the source's own branches with the syntax protected, and the report says so. The ICU Forms window shows which numbers each kept branch is used for; `other` takes every number the message has no form for.
- **Apostrophes**: type them as usual. Finalise doubles the straight apostrophe as ICU requires. Studio's AutoCorrect may replace it with a typographic one, which ICU treats as plain text.
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
A4: Every form the language needs must be present. Translate it, or let Finalise fill it from the source, which it does with a warning so the gap is recorded in the report.

**Q5: I typed a `#` and the verifier warns about it.**
A5: The count marker in the source is a tag; use that one, with QuickPlace or by copying the source. A `#` typed as text is shown literally in the application.

**Q6: Can I run Finalise more than once?**
A6: Yes. A second run on a clean file changes nothing.

**Q7: After Finalise the placeholders are locked and I need to correct a segment.**
A7: Run Finalise again. A message whose placeholders differ from the source is turned back into tags, with a comment on the target segment, so the missing one can be placed. Once corrected, the next run removes the comment and locks the message, and Generate Target Translations needs that locked shape.

**Q8: A Spanish, French, Italian or Portuguese file has a "many" form I have never seen.**
A8: CLDR gives these languages, and Catalan, a `many` form for a round million and above. The application uses it, so the form is needed. The ICU Forms window shows the numbers: 1000000, 2000000, 3000000.

> **Tip**: Full documentation in detail is maintained at <https://multifarious.filkin.com/product/icu-support/>.

---

## License
Copyright 2026 multifarious. Released under the Apache License, Version 2.0. The source code is at <https://github.com/paulfilkin/multifarious-icu>.

---

## Acknowledgements
The plural rules and their example numbers are the Unicode **CLDR** plural rules, used under the Unicode licence.

---
