# multifariousICU Support for Trados Studio

## Introduction
multifariousICU Support adds ICU MessageFormat to what Trados Studio can translate well. ICU messages are how modern applications write strings that depend on a count or a choice: `{count, plural, one {# unread message} other {# unread messages}}`. Each language needs its own set of plural forms, from one in Japanese to six in Arabic, and the forms are selected at run time by rules published in the Unicode CLDR data.

The plugin gives the translator each form as a complete sentence in its own segment, with the ICU syntax locked and the arguments protected, exactly the forms the target language needs. When the translation is done, a second task assembles the message back into valid ICU. A window under the editor shows every form of the message the translator is working on, with sample counts and the reassembled result, and a verifier lists what is still wrong.

It is built for translators and language service providers doing software localisation: resource files for web, mobile and desktop applications written against the ICU libraries that most frameworks use. No knowledge of ICU syntax is needed to translate, and no plural rule has to be looked up: the forms, their names and the counts that select them come from the same CLDR data the application uses.

Full documentation in detail is maintained at <https://multifarious.filkin.com/product/icu-support/>.

![The Studio editor with an expanded plural message: four Russian forms as segments, and the ICU Forms window below showing the forms with sample counts](multifarious_images/01.png)

---

## Key Features
- **One segment per form the language needs**: Russian four, Arabic six, Japanese one, each a whole sentence with a comment naming the form and the counts that select it, and a grammar hint where one is known.
- **Syntax protected**: the selector and branch syntax sits between the segments as locked content; arguments such as `{name}` and the count marker `#` are locked spans inside the sentence. A translator cannot break the message.
- **Finalise before delivery**: the forms a language does not use are removed, apostrophes and braces typed as text are escaped, placeholders are checked and untranslated forms are filled from the source with a warning. Running it twice changes nothing.
- **ICU Forms window**: every form of the active message with sample counts, the source and target rendered as sentences, a count box that says which form a number selects, the reassembled message checked as you type, and a zoom with Ctrl and the mouse wheel.
- **ICU Verifier**: untranslated forms, placeholder mismatches, a count marker typed as text and a message that will not parse, on F8 and as segments are confirmed, with a severity per check.
- **Reports** per language pair listing every message and what happened to it.
- **Localised**: settings pages with help, the window, the reports and the verifier messages in English, German, French, Spanish, Italian, Japanese, Korean, Russian and Simplified Chinese.

---

## Installation Guide
The app is a standard Trados Studio plugin installed from the AppStore.

### Technical Requirements
- **Supported OS**: Windows 10 or later (as required by Trados Studio)
- **Supported Studio versions**: Trados Studio 2026
- **Dependencies**: none beyond Trados Studio itself; the ICU parser and the CLDR plural data ship inside the plugin
- **Permissions**: none beyond a normal plugin installation

### Step-by-Step Installation
1. **Step 1**: Download/Install from the Trados Studio integrated AppStore (or download the `.sdlplugin` from the AppStore website and double-click it).
2. **Step 2**: Restart Trados Studio. The two batch tasks **ICU Expand Plural Forms** and **ICU Finalise Messages** appear in the Batch Tasks list, **ICU Forms** appears on the editor's Add-ins tab in the **multifarious ICU** group, and **ICU Verifier** appears under Project Settings, Verification.

---

## Configuration and Setup
The defaults are chosen so that a first project works without touching anything: plural and ordinal messages are expanded, each source segment gets a comment, the branch budget is 24, and Finalise fills an untranslated form from the source with a warning. The settings pages are where you adapt this.

### Configuration Steps
1. **Step 1**: Run the task through **Batch Tasks** and open its page on the Settings step, or open **Project Settings** and find the two tasks and the verifier there. A project template carries the settings into new projects.
2. **Step 2**: Adjust what you need. Expand: which message kinds to expand, which source form seeds a form the source language does not have, whether to write the segment comments and grammar hints, the branch budget, and what to do with a value that does not parse. Finalise: what to do with an untranslated form and with a placeholder mismatch. Verifier: whether it runs and the severity of each check.
3. **Step 3**: Save. Expand settings take effect on the next run of the task; a file already expanded is left alone.

> **Tip**: Every group on the pages has a "Why...?" panel explaining the choice in plain terms: why expand at all, where the text for an extra form comes from, what the branch budget is for. With the ICU Forms window open some translators prefer no segment comments; that is one tick box.
>
> ![The ICU Expand Plural Forms settings page with its groups and one help panel open](multifarious_images/03.png)

---

## Usage Instructions
The plugin fits Studio's normal workflow: create the project, expand, translate, finalise, generate the target files.

### Basic Usage
1. **Create the project** with your JSON or Java properties files as usual. Studio's own JSON and Java Resources file types read them.
2. **Expand**: run **ICU Expand Plural Forms** on the target files, after Copy to Target Languages and before Analyse and Pre-translate, so word counts and matches see whole sentences. In a custom task sequence it slots in after Convert to Translatable Format; otherwise run it as a batch task straight after the project is created.
3. **Translate**: each form is a segment; the comment on the source segment says which form it is and which counts select it. Open **ICU Forms** from the Add-ins tab to see every form of the message at once, try a count, and watch the reassembled message stay valid as you type. Keep the locked spans; they are the arguments and the count marker.
4. **Verify**: press F8. The ICU Verifier lists untranslated forms, placeholder problems and anything that will not parse, beside Studio's other verifiers.
5. **Finalise**: run **ICU Finalise Messages** on the target files after Update Main Translation Memories and before Generate Target Translations, so the memory holds the translator's clean text and only the generated file carries the ICU escaping.
6. **Generate the target files**. Every message is valid ICU with exactly the forms the language needs.

### Advanced Tips
- **A form the source language does not have**, such as Russian "few", starts from the source's "one" form where there is one or from "other"; the comment says which. Switch to "always other" if your translators prefer the general form as the starting point.
- **Nested messages**: a select over a plural is walked into and the plural inside expanded, so a gendered message gets "female / one", "female / few" and so on. An offset, an exact value such as `=0` and an ordinal are all handled; the comment on each segment says which counts the form is for.
- **Two independent plurals in one message multiply**: six forms times six is 36 segments for Arabic. Over the branch budget the message is laid out with its syntax protected and its own branches, nothing added, and the report says so. Raise the budget for a project that wants them all.
- **Apostrophes**: type them as you normally would. Finalise doubles the straight apostrophe as ICU requires. Studio's AutoCorrect may replace it with a typographic one, which ICU treats as plain text and which needs nothing.
- **Reports**: after each task, one report per language pair in the Reports view lists every message with its outcome and any warning.

---

## Troubleshooting and FAQs

**Q1: The task ran but nothing changed in my file.**
A1: The task processes Studio's JSON and Java Resources file types only, and only values that contain an ICU message. A value with no braces is left alone, as is a file already expanded. The report lists what was seen and what was done.

**Q2: The ICU Forms window says "Not available for this file".**
A2: The file is of a type the plugin does not handle. The window and the tasks work with JSON and Java properties files, the two proven end to end.

**Q3: A message shows the source's own forms only, and a warning about the branch budget.**
A3: The message has several independent plurals and would need more segments than the budget allows. It is laid out with its syntax protected so it cannot be damaged; raise the budget on the expand page if you want every form, or ask the developer to split the message.

**Q4: The Messages window says a form has no translation.**
A4: Every form the language needs must be present. Translate it, or let Finalise fill it from the source, which it does with a warning so the gap is recorded in the report.

**Q5: I typed a `#` and the verifier warns about it.**
A5: The count marker in the source is a locked span; use that one, by copying the source or placing the span. A `#` typed as text is shown literally in the application.

**Q6: Can I run Finalise more than once?**
A6: Yes. It removes the same forms and escapes the same text each time; a second run changes nothing.

> **Tip**: Full documentation in detail is maintained at <https://multifarious.filkin.com/product/icu-support/>.

---

## License
Copyright 2026 multifarious. All rights reserved.

---

## Acknowledgements
The plural rules and their example counts are the Unicode **CLDR** plural rules, used under the Unicode licence. The ICU MessageFormat parser, the CLDR rule evaluator and the expansion model were designed for the multifarious ICU app for Trados Cloud and ported to Studio for this plugin.

---
