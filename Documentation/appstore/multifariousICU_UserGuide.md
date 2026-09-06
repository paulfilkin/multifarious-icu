# Translating ICU MessageFormat in Trados Studio: the multifariousICU Support user guide

Software that counts things has to say "1 file", "2 files" and, in Russian, "5 файлов" with a different ending from "2 файла". Modern applications write such strings as ICU MessageFormat messages:

```
{count, plural, one {# unread message} other {# unread messages}}
```

The developer writes the forms English needs. The application selects the right one at run time using the Unicode CLDR plural rules for the user's language. And the translator receives the whole expression as a single segment, with a job that is really three jobs: know the ICU syntax, know which forms the target language needs and which counts select them, and type the missing forms by hand without breaking a brace.

**multifariousICU Support for Trados Studio** takes those three jobs away. This guide walks through the whole workflow, from installation to the generated file, with a close look at every setting, the ICU Forms window and the verifier.

![The Studio editor with an expanded plural message: four Russian forms as segments, and the ICU Forms window below showing the forms with sample counts](multifarious_images/01.png)
*Screenshot 1: the main shot. The editor with `inbox.unreadCount` expanded for Russian, four segments with their locked spans, and the ICU Forms window docked below showing the four rows with counts, the count box and the reassembled message.*

---

## What an ICU message is, in five minutes

A message is text with arguments in braces. A plain argument, `{name}`, is replaced by a value. A **plural** argument chooses one of several branches by a count:

```
Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!
```

Inside a branch, `#` stands for the count. The branch names are CLDR's plural categories: `zero`, `one`, `two`, `few`, `many`, `other`. Which of them a language uses, and which counts select each, is fixed by CLDR:

| Language | Cardinal forms | Example: which form 5 selects |
|---|---|---|
| English | one, other | other |
| Russian | one, few, many, other | many |
| Arabic | zero, one, two, few, many, other | few |
| Japanese | other | other |

Three more constructs turn up:

- **Explicit values**: `=0 {No messages}` matches exactly that count, before the categories are considered.
- **selectordinal**: the same, for ordinals: "1st", "2nd", "3rd".
- **select**: a choice by a value rather than a count, typically gender: `{gender, select, female {...} male {...} other {...}}`. Its branches are the developer's, not the language's.

And two rules of ICU syntax that matter to a translator: a literal apostrophe is written as two, `It''s`, and a literal brace is wrapped in apostrophes, `'{'`. The plugin handles both for you.

---

## What the plugin does

**ICU Expand Plural Forms** is a batch task. For every plural and ordinal message in a file it works out which forms the file's target language needs, and lays the message out as one segment per form. Each segment is the whole sentence with that form selected, so "Hello {name}, you have # unread messages!" is one segment for Russian "few", not a fragment "# unread messages". The syntax between the segments is locked content; `{name}` and `#` are locked spans inside the sentence. A comment on each source segment names the form, lists the counts that select it, says which source form it started from, and adds a grammar hint where one is known.

**ICU Finalise Messages** is the batch task for the other end. It prunes each message to exactly the forms the language uses, escapes apostrophes and braces typed as text, checks that every protected span in the source is in the target, fills an untranslated form from the source with a warning, and leaves the file ready for Generate Target Translations.

**ICU Forms** is a window under the editor that shows every form of the message the active segment belongs to.

**ICU Verifier** is a verifier like Studio's tag verifier: it lists what is still wrong with an expanded message.

---

## Installation

1. In Trados Studio, open the integrated AppStore (or download the `.sdlplugin` from the RWS AppStore website and double-click it).
2. Search for **multifariousICU Support for Trados Studio** and install.
3. Restart Studio.

Afterwards you will find:

- **ICU Expand Plural Forms** and **ICU Finalise Messages** in the Batch Tasks list, and available for task sequences.
- **ICU Forms** on the editor's **Add-ins** tab, in the **multifarious ICU** group.
- **ICU Verifier** under **Project Settings, Verification**.

![The Batch Tasks menu with the two ICU tasks listed](multifarious_images/02.png)
*Screenshot 2: the Batch Tasks drop-down in the Projects or Files view with "ICU Expand Plural Forms" and "ICU Finalise Messages" visible among Studio's tasks.*

Requirements: Trados Studio 2026 on Windows. The ICU parser and the CLDR plural data ship inside the plugin; nothing else is needed.

---

## Which files

The tasks work on Studio's own **JSON** and **Java Resources** file types, the two proven end to end. A project can contain anything else; those files are simply left alone, and the ICU Forms window says why when you open one.

Within a file, a value is expanded when it contains a plural or ordinal message. A value with arguments but no selector, such as "Hello {name}, welcome back!", is laid out as one segment with the arguments protected. A value with no braces is left as Studio's filter delivered it. A value that has braces but is not valid ICU is left as it is, with a warning on the paragraph and in the report.

---

## Your first project

Take a JSON resource file with a message like this one in it:

```
"inbox.unreadCount": "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!"
```

1. Create a project from it into, say, Russian, Arabic and Japanese, with the default task sequence.
2. Run **ICU Expand Plural Forms** on all target files: select the files, Batch Tasks, ICU Expand Plural Forms, Next, Next, Finish. Leave the settings at their defaults for now.
3. Open the Russian file in the editor.

The message is now four segments, one per Russian form, each a complete sentence:

| Form | Source segment | Comment says |
|---|---|---|
| one | Hello {name}, you have # unread message! | Used when the count is: 1, 21, 31, 41, 51, 61 |
| few | Hello {name}, you have # unread messages! | Used when the count is: 2, 3, 4, 22, 23, 24. Seeded from "other" |
| many | Hello {name}, you have # unread messages! | Used when the count is: 0, 5, 6, 7, 8, 9. Seeded from "other". Grammar: genitive plural agreement |
| other | Hello {name}, you have # unread messages! | Used when the count is: fractional counts only, e.g. 0.0, 0.1, 0.2 |

Hover the comment on a source segment to read it in full.

![Four segments of one message in the editor, with the comment of one source segment open](multifarious_images/04.png)
*Screenshot 3: the four Russian segments of the message with the comment tooltip or the Comments window showing the "few" segment's comment.*

Open Arabic and the same message is six segments; Japanese, one. A multilingual project gets exactly the right forms for each language because the task runs on each language's own file.

### Where in the workflow

Expand runs on the bilingual target files, so it must come after Copy to Target Languages. Put it before Analyse Files and Pre-translate, so word counts and translation memory matches see whole sentences rather than brace syntax. Two ways to arrange that:

- Run it as a batch task straight after the project is created and before you analyse.
- Add it to a **custom task sequence** after Convert to Translatable Format and Copy to Target Languages, before Analyse Files. The project is then expanded as it is created.

Finalise belongs at the other end: after Update Main Translation Memories, before Generate Target Translations. The memory then stores the translator's clean text, and only the generated file carries the ICU escaping.

![A custom task sequence with the ICU tasks placed](multifarious_images/12.png)
*Screenshot 4: the task sequence editor with ICU Expand Plural Forms after Copy to Target Languages and before Analyse Files, and ICU Finalise Messages after Update Main Translation Memories.*

---

## Translating

Each form is an ordinary segment. Translate the sentence as your language needs it for the counts the comment lists; word order and agreement can change freely per form, which is the whole point of having whole sentences.

- **Locked spans** are the arguments and the count marker: `{name}`, `{amount, number, ::currency/EUR}`, `#`. Place them where your language wants them. They cannot be edited or deleted; if a target ends up without one, because it was cleared or came from a memory match, the verifier says so.
- **Locked content between segments** is the message's syntax. You never touch it.
- **Apostrophes and braces**: type them as you would in any text. Finalise turns `It's` into `It''s` and a literal `{` into `'{'` when it generates the file. Studio's AutoCorrect may replace a straight apostrophe with a typographic one; ICU treats that as ordinary text, so it needs nothing.
- **A `#` typed as text** is the one thing to avoid: it is shown literally in the application. Use the locked `#` from the source, by copying source to target or placing the span.

![A target segment with its locked spans placed in a different order from the source](multifarious_images/05.png)
*Screenshot 5: a Russian or Arabic target where `{name}` and `#` sit in a different position from the English, showing that the spans move freely.*

---

## The ICU Forms window

Click **ICU Forms** on the Add-ins tab. The window docks under the editor and follows the active segment:

- The header names the message key, the target language and the number of forms, and says on the right whether the reassembled message is valid ICU.
- One row per form: the form's name, the counts that select it, the source sentence and your translation, both with sample values in place of the arguments and a sample count for `#`. The active segment's row is shaded with a blue accent. An untranslated form shows "(not translated)" in grey; a target whose placeholders differ from the source shows in red with the detail in a tooltip.
- **Try a count**: type a number and the row it selects turns amber, with "selects few" beside the box. Explicit values match first, then the language's rule, exactly as the application will do it.
- **Reassembled message**: the whole target message as Finalise will write it, escaping included, updated as you type, with "Valid ICU" or the parser's complaint beside it.
- **Zoom**: Ctrl and the mouse wheel over the window, or the minus and plus buttons in its top right corner, scale everything in it between 60% and 250%. The button between them shows the current zoom and resets it to 100%. The zoom is remembered between sessions.

For a select over a plural, such as a gendered message, the Form column shows the full path: "female / one", "female / few", and so on.

![The ICU Forms window with a count typed and its row highlighted](multifarious_images/06.png)
*Screenshot 6: the window with "22" in the count box, the "few" row amber, the active row blue, one target still "(not translated)", and the reassembled message below.*

When there is nothing to show, the window says why:

- **Not an ICU message**: the active segment is ordinary text. Move to a plural message.
- **No ICU messages expanded in this file**: a JSON or properties file the expand task has not run on yet.
- **Not available for this file**: a file of another type.
- **No document open**.

![The window's "Not an ICU message" panel](multifarious_images/07.png)
*Screenshot 7: the titled explanation panel shown for an ordinary segment such as `app.title`.*

---

## The verifier

Press **F8** or confirm a segment and the **ICU Verifier** reports, in the Messages window beside Studio's own verifiers:

| Finding | Default severity | What it means |
|---|---|---|
| The form "count:few" has no translation | Warning | Every form the language needs must be present. Finalise will use the source text if you leave it. |
| The protected placeholders differ from the source | Error | A locked span is missing from the target or one has been added. |
| A '#' typed as text will be shown literally | Warning | Use the locked count marker instead. |
| The reassembled message is not valid ICU | Error | Something in the layout is broken; the message names the position. |

![The Messages window with ICU Verifier findings beside QA Checker and Tag Verifier entries](multifarious_images/08.png)
*Screenshot 8: the Messages window after F8 on a partly translated file, with an ICU Verifier error and warning visible, their Origin column reading "ICU Verifier".*

Under **Project Settings, Verification, ICU Verifier** you can switch the verifier off and choose the severity of each check: error, warning, note, or do not report.

![The ICU Verifier settings page](multifarious_images/09.png)
*Screenshot 9: the verifier's page with the Enabled tick box and the four severity drop-downs.*

---

## The settings, in depth

The pages appear on the Settings step of the batch task wizard, under Project Settings, and in project templates. Every group has a "Why...?" panel with the explanation; this guide covers the practice.

![The ICU Expand Plural Forms settings page with one help panel open](multifarious_images/03.png)
*Screenshot 10: the expand page, the "Why expand at all?" panel expanded.*

### Messages to expand

Plural and ordinal messages, both on by default. A message whose only selectors are of an unticked kind is left as it is. There is no tick box for select: a select is always walked, never expanded, because its branches belong to the developer.

### Source text for forms the source language does not have

English has "one" and "other". A Russian "few" segment has to start from one of them, and that text is what the translator sees as the source. The default takes the source form with the same name where there is one, otherwise "other". The alternative takes "other" for every form, which some teams prefer because the English "one" form is often worded for exactly one item and reads oddly as the starting point for anything else. The comment on each segment says which form it started from.

### Segment comments

Whether each source segment gets a comment at all, on by default, and whether the comment carries a grammar hint. The comment is a convenience for translators; with the ICU Forms window open some find it noise, and it can go. The tasks do not depend on it. Grammar hints come from the plugin's own table, for example that the Russian "few" form takes the genitive singular; they are advice, not rules.

### Branch budget

A message with two independent plurals multiplies: six forms times six is 36 segments for Arabic from one message. The budget, 24 by default, caps that. Over the budget the message is laid out with its syntax protected and its original branches, nothing added, with a warning on the paragraph and in the report. Raise the budget for a project that wants every form regardless, or ask the developer to split the message, which is usually the better fix.

### When a value does not parse as an ICU message

A brace without its partner, an apostrophe where ICU reads it as a quote, a misspelt keyword. Leaving the value untouched with a warning keeps the rest of the file moving; stopping the task is for a pipeline where a broken message must never reach a translator unnoticed.

### Finalise: when a form has no translation

Use the source text and warn, the default, keeps the generated file valid and records the gap in the report. Stop the task is for a final delivery where an untranslated form must not slip through in the source language.

### Finalise: when the protected placeholders differ

Warn and continue, the default, or stop the task until the translation is put right.

![The ICU Finalise Messages settings page](multifarious_images/10.png)
*Screenshot 11: the finalise page with its two groups and a help panel open.*

---

## Finalising and generating the target files

1. Run **ICU Finalise Messages** on all target files, after the translation memories have been updated.
2. Check the report: one per language pair in the Reports view, listing every message, the forms removed, the segments filled from the source, and every warning by segment.
3. **Generate Target Translations**. The JSON or properties file comes back with each message reassembled: exactly the forms the language needs, in CLDR's order, explicit values kept, apostrophes and braces escaped as ICU requires.

![The Reports view with an ICU Finalise Messages report open](multifarious_images/11.png)
*Screenshot 12: a finalise report in Studio's Reports view: the summary, the per-file totals and the warnings table.*

Finalise is safe to run again after further editing: it removes the same forms and escapes the same text each time, so a second run changes nothing.

### What the generated file looks like

Source:

```
"inbox.unreadCount": "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!"
```

Russian target:

```
"inbox.unreadCount": "{count, plural, one {Привет, {name}, у вас # непрочитанное сообщение!} few {Привет, {name}, у вас # непрочитанных сообщения!} many {Привет, {name}, у вас # непрочитанных сообщений!} other {Привет, {name}, у вас # непрочитанного сообщения!}}"
```

The text that was outside the selector in the source is inside every branch in the target. That is by design and is the documented best practice for translatable ICU: each form is a whole sentence, so word order and agreement are free. The argument set is preserved exactly.

---

## Harder messages

The one-plural message above is the common case. The shapes below are the ones that make ICU hard to translate by hand, and they show what the plugin does with each. The segment counts are what the expand task produces from an English source with the default settings.

**An exact value with a plural.** `=0` matches exactly that count before the language's categories are considered, so it is always its own segment, in every language.

```
{minutes, plural, =0 {This parrot is resting.} one {This parrot has been resting for # minute.} other {This parrot has been resting for # minutes.}}
```

| Target | Segments | Forms |
|---|---|---|
| German | 3 | =0, one, other |
| Russian | 5 | =0, one, few, many, other |
| Arabic | 7 | =0, zero, one, two, few, many, other |
| Japanese | 2 | =0, other |

**An offset.** With `offset:1` the count marker shows the count minus one, and `=0` and `=1` still match the real count. The comment on each segment lists the numbers the form is used for, and the ICU Forms window renders each row with a real value in place of `#`.

```
{count, plural, offset:1 =0 {Nobody is coming} =1 {Only you are coming} one {You and # other guest are coming} other {You and # other guests are coming}}.
```

Russian gets six segments, `=0`, `=1`, one, few, many and other; Arabic eight; Japanese three.

**An ordinal.** `selectordinal` uses the language's ordinal categories, which are a different set from its cardinal ones. English needs four for "1st, 2nd, 3rd, 4th". Russian, German, Arabic and Japanese need one, so the message collapses to a single segment. Hungarian needs two, Welsh six.

```
You are {position, selectordinal, one {#st} two {#nd} few {#rd} other {#th}} in the queue.
```

**A select over a plural.** A `select` chooses by a value rather than a count, typically gender. Its branches are the developer's, so the plugin never adds or removes them; it walks into each and expands the plural inside. The Form column of the ICU Forms window shows the path: "female / one", "female / few".

```
{customer, select, female {{count, plural, =0 {She asked for cheese and found none.} one {She asked for # cheese and found none.} other {She asked for # cheeses and found none.}}} other {{count, plural, =0 {They asked for cheese and found none.} one {They asked for # cheese and found none.} other {They asked for # cheeses and found none.}}}}
```

Two select branches times five Russian forms is ten segments; Arabic fourteen; Japanese four.

**Two independent plurals.** Each form of the first has to be combined with each form of the second, because the sentence has to agree with both counts at once. This is where a translator working by hand gives up, and where the branch budget matters.

```
{spam, plural, =0 {Egg and bacon, no spam} one {Egg, bacon and spam} other {Egg, bacon and # helpings of spam}} for {diners, plural, one {# diner} other {# diners}}.
```

| Target | Segments | Why |
|---|---|---|
| German | 6 | 3 forms times 2 |
| Russian | 20 | 5 forms times 4 |
| Japanese | 2 | 2 forms times 1 |
| Arabic | the source's 6 | 7 times 6 is 42, over the budget of 24, so the message is laid out with its syntax protected and the source's own branches, and reported |

**An ordinal and two plurals.** The same rule, three deep. Russian needs 1 times 4 times 4, sixteen segments. Welsh would need 6 times 6 times 6, 216, and gets the budget warning instead.

```
{rank, selectordinal, one {#st} two {#nd} few {#rd} other {#th}} lumberjack of {total, plural, one {# lumberjack} other {# lumberjacks}} to fell {trees, plural, one {# tree} other {# trees}}.
```

**Apostrophes and braces as text.** This value has no argument at all, so it is left exactly as Studio's filter delivered it; a translator types the apostrophe and the brace as text, and Finalise writes them back as ICU requires.

```
Type '{' to insert a placeholder, and don''t forget to close it.
```

Whatever the shape, the rule for the translator is the same: each segment is one whole sentence for the counts its comment names, the syntax is locked, and the ICU Forms window shows the whole message reassembled.

---

## Workflow recipes

### Piloting a new resource file

Before quoting, run one file through with pseudo-translation:

1. Create a throwaway project with the file into a language with many forms, such as Arabic, and expand it.
2. Run Studio's pseudo-translation, then Finalise, then Generate Target Translations.
3. Read the report and open the generated file: every message parses, every plural carries six forms plus its explicit values, every argument survived. Anything that passed through unexpanded is listed with its reason.

### Translation memory strategy

Expand before Analyse and Pre-translate. Whole sentences per form leverage far better than brace expressions, and a form that repeats across messages, as "other" forms often do, is reused by the memory. Finalise after Update Main Translation Memories, so the memory never sees ICU escaping.

### A project with several languages

Nothing special: each language's bilingual file is expanded for that language and finalised for that language. Russian gets four forms, Arabic six, Japanese one, from the same source project.

---

## Troubleshooting

**Nothing changed after Expand.**
The file is not one of the two supported types, or its values hold no ICU messages, or it was already expanded. The report and the ICU Forms window both say which.

**A message has only the source's forms and a budget warning.**
It exceeded the branch budget. Raise the budget on the expand page and run again on a fresh project, or accept the source's branches for that message.

**The Messages window lists a form with no translation after I translated everything.**
A form was filled by a memory match that left it empty, or a segment was cleared. The ICU Forms window shows the empty row in grey.

**The reassembled message is red.**
The window names the position. Almost always the target contains an unmatched apostrophe or brace typed as text; Finalise escapes those, and the window shows the message as Finalise will write it, so a red reading means something Finalise cannot repair, such as a locked span pasted in twice.

**My typed apostrophe was not doubled in the file.**
Studio's AutoCorrect replaced it with a typographic apostrophe (’), which ICU treats as plain text. The file is valid; nothing needs doubling.

---

## Limitations and an invitation

The plugin has been proven end to end on a corpus of ICU messages covering nested selects, explicit values, offsets, ordinals, argument-only messages and apostrophes, generating files that parse and render identically to the source for every count in three languages. Current known limits:

- Studio's JSON and Java Resources file types only. Others follow once they have been run end to end.
- A message with `#` bound through a non-zero offset across a nested selector is left unexpanded with a warning; it cannot be rewritten faithfully.
- The comments written into the source segments are English in every user interface language.

If you translate ICU messages, **your feedback directly improves the plugin**: a message that lays out oddly, a language whose grammar hint is wrong or missing, a file type you need. Reports with a sample file are gold.

---

*multifariousICU Support for Trados Studio. The plural rules and their example counts are the Unicode CLDR plural rules. The ICU parser, the CLDR evaluator and the expansion model were designed for the multifarious ICU app for Trados Cloud and ported to Studio for this plugin.*
