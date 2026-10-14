# multifariousICU Support for Trados Studio: user guide

Text that includes a number is written differently from one language to the next. English has "1 file" and "2 files". Russian needs a different ending for 2, for 5 and for 21. Arabic has six forms. Japanese has one. Application developers handle this with ICU MessageFormat, a notation in which one string carries every form the source language needs:

```
{count, plural, one {# unread message} other {# unread messages}}
```

ICU is the International Components for Unicode, the library most applications use to format text for a language. MessageFormat is its notation for text that changes with a number or a choice. The application picks the right form at run time using the plural rules for the user's language from CLDR, the Common Locale Data Repository, which is the Unicode Consortium's database of language rules.

For a translator this notation is hard work. The whole expression arrives as one segment. The translator has to know the syntax, know which forms the target language needs and which numbers select them, and type the missing forms by hand without breaking a brace.

**multifariousICU Support for Trados Studio** does that work for them. Each form becomes its own segment, a complete sentence, with the syntax protected. The plugin uses the same CLDR data as the application, so the forms it offers are the forms the application will use. The same applies to text that changes with the gender of a thing named in it: a message that chooses by gender is laid out with the genders the target language has. Note: if the translation is wrong please let me know and I will correct it, but for now try to focus on the message we're trying to explain:

![The Studio editor with an expanded plural message: four Russian forms as segments, and the ICU Forms window beside it](multifarious_images/001.png)
*The editor with a message expanded for Russian, four segments with their placeholder tags, and the ICU Forms window. Also note the red text indicating I still haven't used a proper tag in the target.*

---

## What an ICU message contains

A message is text with arguments in braces. A plain argument such as `{name}` is replaced by a value. A **plural** argument chooses one of several branches by a number:

```
Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!
```

Inside a branch, `#` stands for the number. The branch names are the plural categories defined by CLDR: `zero`, `one`, `two`, `few`, `many` and `other`. Each language uses some of them, and CLDR says which numbers select each:

| Language | Plural forms | The form for 5 |
|---|---|---|
| English | one, other | other |
| Russian | one, few, many, other | many |
| Arabic | zero, one, two, few, many, other | few |
| Spanish | one, many, other | other |
| Japanese | other | other |

Spanish, French, Italian, Portuguese and Catalan have a `many` form that is used for a round million and above: 1000000, 2000000, 3000000. Applications use it, so a translation needs it, and the plugin lays it out like any other form.

Three other constructs appear in real files:

- **Exact values** such as `=0 {No messages}` match one number only and take precedence over the categories.
- **Ordinals** (`selectordinal`) work like plurals but for "1st", "2nd", "3rd".
- **Selects** choose a branch by a value rather than a number: `{status, select, DELAYED {...} CANCELLED {...} other {...}}`. The branches are usually decided by the developer and are the same in every language. When the value is grammatical, the gender of a thing named in the sentence, the branches a language needs differ, and the plugin treats such a select like a plural; see [Messages that choose by gender](#messages-that-choose-by-gender).

Two rules of the notation matter to a translator. A literal apostrophe is written as two, `It''s`, and a literal brace is wrapped in apostrophes, `'{'`. The plugin takes care of both.

---

## What the plugin does

**Expand ICU** is a batch task. For every plural and ordinal message in a file it works out which forms the target language needs and lays the message out as one segment per form. Each segment is the whole sentence with that form in place, so "Hello {name}, you have # unread messages!" is one segment for Russian "few", not a fragment. The syntax between the segments is locked. Inside a segment, `{name}` and `#` are placeholder tags, which you place like any other tag in Studio. A select whose branches are genders is laid out the same way, with the genders the target language has.

**Restore ICU** is the batch task that puts the messages back together. It removes the forms the language does not use, escapes apostrophes and braces typed as text, checks that every placeholder in the source is in the target, fills an untranslated form from the source with a warning, and assembles the forms into one plain segment on each side, which is the shape Studio's file writers need. Run it before Generate Target Translations, or whenever you want the whole messages back. Expand ICU on a restored file lays the forms out again, each with its translation and its confirmation state, so you can move between the two shapes as often as you like.

**ICU Forms** is a window under the editor that shows every form of the message the active segment belongs to, with the numbers or the grammatical form that select each, the source and target as sentences, and the message reassembled as you type.

**ICU Verifier** works like Studio's tag verifier and reports what is still wrong with a message.

**ICU Settings** is a button on the Projects view that opens the two tasks' settings for the selected project.

---

## Installation

1. In Trados Studio, open the integrated AppStore, or download the `.sdlplugin` from the plugin's AppStore record at <https://appstore.rws.com/plugin/490> and double-click it.
2. Search for **multifariousICU Support for Trados Studio** and install.
3. Restart Studio.

You will then find:

- **Expand ICU** and **Restore ICU** in the Batch Tasks list and in task sequences.
- **ICU Forms** on the editor's **Add-ins** tab, in the **multifarious ICU** group.
- **ICU Settings** on the Projects view's **Add-ins** tab, in the same group.
- **ICU Verifier** under **Project Settings, Verification**.

![The Batch Tasks menu with the two ICU tasks listed](multifarious_images/002.png)
*The Batch Tasks drop-down with the two ICU tasks.*

Requirements: Trados Studio 2026 on Windows. The ICU parser, the CLDR data and the grammar dictionary are inside the plugin.

---

## Which files

The tasks work on Studio's **JSON** and **Java Resources** file types. Other files in a project are left alone, and the ICU Forms window says so when you open one.

A value is expanded when it contains a plural, an ordinal or a gender select. A value with arguments but no such message, such as "Hello {name}, welcome back!", becomes one segment with the arguments protected. A value with no braces is not touched. A value with braces that is not valid ICU is left as it is, with a warning in the report.

### Markup inside the values

Values with HTML or other markup in them can be opened with the file type's **Embedded Content** settings, so that the markup becomes tags rather than text to translate. The JSON file type offers the HTML embedded content processor; the Java Resources file type takes tag definition rules, such as `<(b|i|a|span)[^>]*>` and `</(b|i|a|span)>` as a tag pair. Set them up before creating the project.

Both tasks keep those tags. Expand puts each tag into every form it belongs to, so a `<b>` around the whole plural appears in each form around that form's text, and a `<b>` inside one branch appears in that form only. Restore puts the tags back into the restored message where each form held them. The generated file has the markup as the source had it.

One shape cannot be laid out: a tag pair that opens in one branch and closes in another, such as `{count, plural, one {<b># file} other {# files</b>}}`. Studio's writers drop a tag pair split across segments, so the message is left as it is, with a warning in the report and on the message.

---

## Your first project

Take a JSON file with a message like this:

```
"inbox.unreadCount": "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!"
```

1. Create a project from it into Russian, Arabic and Japanese, with the default task sequence.
2. Run **Expand ICU** on all target files: select the files, Batch Tasks, Expand ICU, Next, Next, Finish. Leave the settings at their defaults.
3. Open the Russian file in the editor.

The message is now four segments, one per Russian form:

| Form | Source segment | Selected by |
|---|---|---|
| one | Hello {name}, you have # unread message! | 1, 21, 31, 41, 51, 61 |
| few | Hello {name}, you have # unread messages! | 2, 3, 4, 22, 23, 24 |
| many | Hello {name}, you have # unread messages! | 0, 5, 6, 7, 8, 9 |
| other | Hello {name}, you have # unread messages! | fractions such as 0.5 |

Click **ICU Forms** on the Add-ins tab. The window lists the four forms with the numbers that select each. The tooltip on a row adds where its source text came from, "other" for few and many because English has no such forms, and a grammar hint where one is known, such as the genitive plural for many.

![Four segments of one message in the editor, with the ICU Forms window below](multifarious_images/003.png)
*The four Russian segments and the ICU Forms window.*

Open the Arabic file and the same message is six segments. Japanese has one. Each language gets its own forms because the task runs on each language's own file.

### Where in the workflow

Expand runs on the target files, so it must come after Copy to Target Languages. Put it before Analyse Files and Pre-translate, so word counts and translation memory matches see whole sentences. Either run it as a batch task straight after the project is created, or add it to a custom task sequence after Copy to Target Languages (if you are running the Professional version of Trados Studio, otherwise you need to run these tasks after the project has been created, or simply as separate actions and not part of a custom task).

Restore belongs before Generate Target Translations. Where it sits relative to Update Main Translation Memories is your choice: the memory stores the file as it is at the time, one unit per form while expanded, one unit per whole message once restored, the source as the developer wrote it. Form units leverage across messages that share a sentence; whole-message units pre-translate a repeated message in one go, in the next project that carries it. Some teams update twice and keep both.

![A custom task sequence with the ICU tasks placed](multifarious_images/004.png)
*A task sequence with Expand after Copy to Target Languages and Restore before Generate Target Translations.*

---

## Translating

Each form is an ordinary segment. Translate the sentence as your language needs it for the numbers, or the gender, shown in the ICU Forms window. Word order and agreement can differ from one form to the next.

- **Placeholders** are the arguments and the count marker: `{name}`, `{amount, number, ::currency/EUR}`, `#`. They are tags. Place them where your language needs them with QuickPlace (Ctrl+comma), Ctrl+Alt+Down, or by copying the source. If one is missing from a target, the ICU Verifier and Studio's tag verifier report it. The expand settings can lock them so they cannot be moved or deleted; locked tags are not offered by QuickPlace.
- **Locked text between segments** is the message's syntax. Leave it alone.
- **Apostrophes and braces** are typed as in any text. Restore turns `It's` into `It''s` and a literal `{` into `'{'` in the restored message. Studio's AutoCorrect may replace a straight apostrophe with a typographic one, which ICU treats as ordinary text.
- **A `#` typed as text** is shown literally in the application. Use the `#` tag from the source instead.

![A target segment with its placeholder tags in a different order from the source](multifarious_images/015.png)
*A Russian target with `{name}` and `#` in different positions from the English.*

---

## The ICU Forms window

Click **ICU Forms** on the Add-ins tab. The window docks under the editor and follows the active segment.

- The header names the message key, the target language and the number of forms, and shows whether the reassembled message is valid ICU.
- One row per form: the form's name, the numbers that select it, and the source and target as sentences with sample values in place of the arguments. The numbers are written out in full, so a Spanish `many` row reads 1000000, 2000000, 3000000. For a gender form the Counts column shows the grammatical term instead, with a sample noun where one is known: "masculine, e.g. der Zug". The active segment's row is shaded. An untranslated form shows "(not translated)". A target whose placeholders differ from the source shows in red, with the detail in a tooltip.
- **Click a row** and the editor moves to that segment, so the window can be used to navigate a long message as well as to read it.
- **Try a count**: type a number and the row it selects is highlighted. Exact values match first, then the language's rules, as in the application. A large number can also be typed the way CLDR writes it, `1c6` for 1000000 or `1.2c6` for 1200000. In a message with one plural inside another, the count applies to the outer plural and marks every row under the form it selects. In a message kept with the source's own forms, a number whose form the message does not have marks the `other` row, as in the application.
- **Reassembled message**: the whole target message as Restore will assemble it, updated as you type, with "Valid ICU" or the parser's message beside it.
- **Zoom**: Ctrl and the mouse wheel, or the buttons in the top right corner, from 60% to 250%. The zoom is remembered.

For a select containing a plural, the Form column shows the full path: "MASCULINE / one", "MASCULINE / few".

In a message kept with the source's own forms, over the branch budget, the `other` row is used for every number whose form the message does not have. Its Counts column lists those numbers too, and the tooltip names the forms covered. A form the language does not have shows no numbers.

![The ICU Forms window with a count typed and its row highlighted](multifarious_images/005.png)
*The window with 22 typed in the count box and the few row highlighted.*

When there is nothing to show, the window explains why: the segment is ordinary text, the file has not been expanded, the file type is not supported, or no document is open.

![The window's "Not an ICU message" panel](multifarious_images/006.png)
*The panel shown for an ordinary segment.*

---

## The verifier

Press **F8** or confirm a segment and the **ICU Verifier** reports in the Messages window beside Studio's own verifiers:

| Finding | Default severity | Meaning |
|---|---|---|
| The form "count:few" has no translation | Warning | Every form the language needs must be translated. Restore uses the source text if you leave it. |
| The protected placeholders differ from the source | Error | A placeholder tag is missing from the target or one has been added. Studio's tag verifier reports this too. |
| A '#' typed as text will be shown literally | Warning | Use the count marker tag. |
| The reassembled message is not valid ICU | Error | The message names the position of the problem. |

A gender form is named with its hint, "vehicleGender:MASCULINE (masculine)", so the message says which form is meant.

![The Messages window with ICU Verifier findings](multifarious_images/007.png)
*The Messages window after F8, with ICU Verifier entries.*

Under **Project Settings (or Options), Verification, ICU Verifier** you can switch the verifier off and set the severity of each check.

![The ICU Verifier settings page](multifarious_images/008.png)
*The verifier settings page.*

---

## The settings

The two tasks' pages appear on the Settings step of the batch task wizard and in the New Project wizard, and a project template carries them into new projects. Studio does not show a plugin task's page under Project Settings, so the plugin adds a button for it: **ICU Settings** on the Projects view's **Add-ins** tab opens the Expand ICU, Grammar dictionary and Restore ICU pages for the selected project in one dialog, with **OK**, **Cancel** and **Reset to defaults**. Use it to set a project up before the first run, or to change a setting between runs. Each group on the pages has a help panel.

![The ICU Settings dialog opened from the Projects view, three tabs](multifarious_images/011.png)
*The ICU Settings dialog for a project.*

The Expand ICU page has two tabs. **Options** holds the task's settings in the order the task decides them; **Grammar dictionary** holds the dictionary for gender selects, described in [Messages that choose by gender](#messages-that-choose-by-gender).

![The Expand ICU settings page, Options tab, with one help panel open](multifarious_images/009.png)
*The Options tab of the expand settings.*

### Messages to expand

Plural and ordinal messages, both on by default. A message that contains neither is left as it is. Beneath them, the **branch budget**: a message with two independent plurals multiplies, six forms times six is 36 segments for Arabic, and the budget, 24 by default, caps that. Over the budget, the message keeps the source's own branches with the syntax protected, and the report says so. Raise the budget if you want every form, or ask the developer to split the message, which may be the better fix.

### Messages already translated

Off by default. A project pre-translated before Expand runs, from a memory that holds whole messages Restore put back together, arrives with those messages matched. Expanded, such a message is split into its forms, each carrying the message's match and confirmation level, so the translator can check it form by form. With **Keep messages already translated** on, a message whose every segment is matched at the **Minimum match** or better is left as it is instead; Perfect Match and Context Match count as 100%. **Lock kept segments**, on by default, locks it so nobody changes a message the tasks never restructured. Restore ignores a kept message and Generate Target Translations writes it as it stands. A restored message counts too: Expand with the setting on leaves it restored when every form it had was matched at the minimum or better, and locks its one segment. A value with no ICU message in it is never kept or locked.

### Segment text and placeholders

**Source text for forms the source language does not have.** English has "one" and "other". A Russian "few" segment has to start from one of them, and that text is what the translator sees as the source. The default takes the source form with the same name where there is one, otherwise "other". The alternative takes "other" for every form. Some teams prefer that because the English "one" form is often written for exactly one item.

**Placeholders.** Whether the placeholder tags are locked, off by default. Unlocked, a tag can be placed with QuickPlace or Ctrl+Alt+Down, and a missing one is reported by the verifiers. Locked, a tag cannot be moved or deleted, but it is not offered by QuickPlace, so a form typed from scratch has to start from a copy of the source.

### When a value does not parse as an ICU message

A brace without its partner, an apostrophe where ICU reads it as a quote, a misspelt keyword. Leaving the value as it is with a warning keeps the rest of the file moving. Stopping the task is for a pipeline where a broken message must not reach a translator.

### Restore: when a form has no translation

Use the source text and warn, the default, keeps the restored message valid and records the gap as a comment on the segment and in the report. Stop the task is for a delivery where an untranslated form must not slip through.

### Restore: when the placeholders differ

Warn and continue, the default, leaves that message expanded with its placeholders as tags so it can be corrected, and puts the finding as a comment on the target segment, where the Comments window jumps to it. The report and the Task Results window list it too. Run Restore again once the segment is right. Stop the task stops at the first mismatch and changes nothing.

![The Restore ICU settings page](multifarious_images/012.png)
*The restore settings page.*

---

## Messages that choose by gender

A select whose value is the gender of something named in the sentence has the same problem as a plural: the branches the source has are the source language's, and the target language may need different ones. English has no grammatical gender, so an English source often has one branch:

```
"boarding.readyMinimal": "{vehicleGender, select, other {Your {vehicle} is ready to board.}}"
```

German needs masculine, feminine and neuter, because "your train", "your ferry" and "your taxi" each take a different word for "your". French needs masculine and feminine, and an article form as well. Japanese needs one form. The plugin carries a **grammar dictionary** that says, for each language, which forms a gender select needs, and Expand lays such a select out like a plural: one segment per form, the syntax locked, the forms the language does not need removed by Restore.

### How a select is recognised

Two ways. An argument whose name ends in **Gender** or **Article**, such as `vehicleGender`, is recognised by its name. Any other argument name, `vehicleGenus` or `sex`, is recognised when the project's developer's file declares it (below). A select whose argument is recognised by neither, such as `{status, select, DELAYED {...} ...}`, keeps its branches as the developer wrote them, each branch its own segment.

The keys inside a gender select are upper-case English words: MASCULINE, FEMININE, NEUTER, COMMON, ANIMATE, INANIMATE, PERSONAL, and `other`, which ICU requires and which takes any value the message has no branch for. A key in the source that the dictionary does not know, UNKNOWN say, is treated as the developer's own and kept in every language. A key the dictionary knows but the target language does not use, such as NEUTER for French, is removed on Restore.

### What each language gets

The shipped dictionary covers the 61 languages for which CLDR publishes grammatical gender. For a language not listed, a gender select keeps the source's branches.

| Forms | Languages |
|---|---|
| MASCULINE, FEMININE, NEUTER, other | German, Greek, Gujarati, Icelandic, Kannada, Marathi, Norwegian (Bokmål, Nynorsk and plain), Romanian, Russian, Sinhala, Slovenian, Ukrainian |
| MASCULINE, FEMININE, other | Arabic, Catalan, Spanish, French, Hebrew, Hindi, Italian, Lithuanian, Latvian, Punjabi, Portuguese, Urdu |
| ANIMATE, INANIMATE, FEMININE, NEUTER, other | Czech, Croatian, Malayalam, Slovak, Serbian |
| ANIMATE, INANIMATE, PERSONAL, FEMININE, NEUTER, other | Polish |
| COMMON, NEUTER, other | Danish, Dutch, Swedish |
| other only | Afrikaans, Amharic, Azerbaijani, Bangla, English, Persian, Finnish, Filipino, Hungarian, Armenian, Indonesian, Japanese, Kazakh, Korean, Kyrgyz, Lao, Malay, Burmese, Nepali, Tamil, Telugu, Thai, Turkish, Uzbek, Vietnamese, Chinese |

ANIMATE and INANIMATE are the masculine animate and inanimate genders of the Slavic languages, and PERSONAL the masculine personal gender of Polish, as CLDR records them. A language with `other` only gets one form, as Japanese does for a plural.

In the ICU Forms window each gender row shows its grammatical term in the Counts column, with a sample noun for German, French, Italian and Spanish: "feminine, e.g. die Fähre". The sample noun also stands in for the argument in the Source column, "Your Fähre is ready to board.", so the row reads as the sentence the translator is writing.

![The ICU Forms window on a French message with article forms](multifarious_images/014.png)
*A French message laid out by gender and article, with the hint in the Counts column.*

### Article forms for French and Italian

French and Italian need more than the gender: the article before the noun changes with the gender and with the sound the noun starts with. "Le train", "la voiture", "l'avion". The dictionary has an **article** form set for both languages, with a hint on every key:

| Language | Keys | Meaning |
|---|---|---|
| French | LE, LA, L, other | masculine singular before a consonant; feminine singular before a consonant; singular before a vowel sound |
| Italian | IL, LO, L, I, GLI, LA, other | masculine singular before most consonants; masculine singular before s + consonant, z, gn, ps, x, y; singular before a vowel sound; masculine plural before most consonants; masculine plural before a vowel, s + consonant, z, gn, ps, x, y; feminine singular before a consonant |

Under a plural the set narrows: Italian offers IL, LO and L under `one` and I and GLI under `other`, and French offers the article forms under `one` only, because the plural article does not change with the sound.

An article form is a second select nested inside the gender select, so French `ready` becomes seven segments: LE, L and other under MASCULINE, LA, L and other under FEMININE, and other. That second select is only inserted when the project's developer's file says the application passes the article argument for that language, because an inserted selector changes what the application has to supply at run time. Without that declaration the French message gets its two genders and `other`, and the report, the Task Results window and the row's tooltip say that French needs an article form the application does not supply.

### The developer's file

The file is the application's side of the agreement: which arguments it passes, under what names, and for which languages. On the Grammar dictionary tab, **Save template…** writes a template with an explanation at the top; fill it in and **Import…** it. A project template carries the imported file into new projects. Its entries:

- **arguments**: one entry per thing the application describes, `vehicle`, naming the argument it passes for each role: `"gender": "vehicleGender"`, `"article": "vehicleArticle"`, `"count": "vehicleCount"`. An argument named here is recognised whatever its name. **supplies** lists, per language, the dependent arguments the application also passes, `"supplies": { "fr": ["article"], "it": ["article"] }`; only those are inserted into a message that does not have them. **keys** respells the keys for this one argument, where the application passes MASKULINUM rather than MASCULINE for it.
- **keys**: the respelling for the whole project. **rename** maps a default key to the project's spelling; **languages** replaces a language's key set, or adds a language the dictionary does not list. Every set ends in `other`.
- **other**: the project's words for what the `other` branch holds, "the plural form" or "any other value", shown as the hint on every `other` row.

The tab shows the result as two tables. **Languages and their keys** lists every language of every kind with the keys in force; a row reads Default where the shipped set applies and Project where the file changes it, and the keys can be edited in place, with **Reset** putting the default back. **Add a language** adds one the dictionary does not list. **Arguments the application passes** lists the file's arguments, editable, with **Add argument** and **Remove**. Every edit is checked at once, and a refusal is shown under the row it concerns; **Export…** writes the file as it stands, edits included. Hints, sample nouns and the article narrowing come from the shipped dictionary and cannot be edited on the page.

![The Grammar dictionary tab with the template imported](multifarious_images/010.png)
*The Grammar dictionary tab: the languages table, the arguments table and the file's buttons.*

### A worked example

The sample file `grammar.json` holds eight messages in a travel app's wording, and the template declares `vehicle` with a gender, a count and an article argument, the article supplied for French and Italian. Segments per message with the template imported:

| Message | German | French | Italian | Japanese |
|---|---|---|---|---|
| `ready`, a gender select with four branches | 4 | 7 | 8 | 1 |
| `waiting`, a gender select over a plural | 8 | 13 | 18 | 1 |
| `readyExtraKey`, the four branches and UNKNOWN | 5 | 8 | 9 | 2 |
| `status`, application states | 3 | 3 | 3 | 3 |

German has no article forms, so a source message that already contains an article select keeps it as written, and the Task Results window carries a note saying so. Without the template, French and Italian get their genders and `other` only, three segments for `ready`, with a note that the application does not supply the article argument.

After Restore the German target is `{vehicleGender, select, MASCULINE {...} FEMININE {...} NEUTER {...} other {...}}`, the French target has the article select inside each gender, and the source comes back as the developer wrote it.

---

## Restoring and generating the target files

1. Run **Restore ICU** on all target files.
2. Check the results. Each finding is a comment on the target segment it concerns, listed in the Comments window with its severity, and the report, one per language pair in the Reports view, lists every message, the forms removed, the segments filled from the source, and every warning.
3. Run **Generate Target Translations**. Each message comes back with exactly the forms the language needs, exact values kept, and apostrophes and braces escaped.

![The Reports view with a Restore ICU report open](multifarious_images/013.png)
*A restore report in the Reports view.*

After Restore each message is one segment on each side, plain text, no tags and no locked spans. The target is in the shape the translator worked in, with the shared text inside every form; the source is back as the developer wrote it, so a memory updated from the file matches the same message in the next project. A restored message's confirmation level is the one its forms shared, or Draft where they differed, and it is locked only if every form was.

### Switching back to the forms

Run **Expand ICU** on the restored file. Each form comes back with its translation, its confirmation level, its lock and its match, and a Restore after that gives the same message as before. Expand and Restore can alternate as often as the work needs: restore to see or deliver whole messages, expand to work on the forms. Comments survive both ways, around the text they were on. A comment on a form comes back in the restored message; a comment on a restored message comes back on every form that holds the text it was on, so a comment on text shared by all the forms appears on each of them.

### What the generated file looks like

Source:

```
"inbox.unreadCount": "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!"
```

Russian target:

```
"inbox.unreadCount": "{count, plural, one {Привет, {name}, у вас # непрочитанное сообщение!} few {Привет, {name}, у вас # непрочитанных сообщения!} many {Привет, {name}, у вас # непрочитанных сообщений!} other {Привет, {name}, у вас # непрочитанного сообщения!}}"
```

The text that stood outside the plural in the source is inside every branch in the target. That is the recommended way to write translatable ICU, because each form is a whole sentence and word order is free. The arguments are unchanged.

---

## Harder messages

The counts below are what the expand task produces from an English source with the default settings.

**An exact value with a plural.** `=0` matches that number only and is always its own segment.

```
{minutes, plural, =0 {This parrot is resting.} one {This parrot has been resting for # minute.} other {This parrot has been resting for # minutes.}}
```

| Target | Segments | Forms |
|---|---|---|
| German | 3 | =0, one, other |
| Russian | 5 | =0, one, few, many, other |
| Arabic | 7 | =0, zero, one, two, few, many, other |
| Japanese | 2 | =0, other |

**An offset.** With `offset:1` the count marker shows the number minus one, and `=0` and `=1` still match the real number. The ICU Forms window shows the numbers each form is used for.

```
{count, plural, offset:1 =0 {Nobody is coming} =1 {Only you are coming} one {You and # other guest are coming} other {You and # other guests are coming}}.
```

Russian gets six segments, Arabic eight, Japanese three.

**An ordinal.** Ordinal forms are a different set from plural forms. English needs four for "1st, 2nd, 3rd, 4th". Russian, German, Arabic and Japanese need one, so the message becomes a single segment. Hungarian needs two, Welsh six.

```
You are {position, selectordinal, one {#st} two {#nd} few {#rd} other {#th}} in the queue.
```

**A select containing a plural.** A select the dictionary does not recognise keeps its branches as they are, and the plural inside each is expanded. The Form column shows "female / one", "female / few" and so on.

```
{customer, select, female {{count, plural, =0 {She asked for cheese and found none.} one {She asked for # cheese and found none.} other {She asked for # cheeses and found none.}}} other {{count, plural, =0 {They asked for cheese and found none.} one {They asked for # cheese and found none.} other {They asked for # cheeses and found none.}}}}
```

Two branches times five Russian forms is ten segments; Arabic fourteen; Japanese four. Had the argument been called `customerGender`, the select would have been laid out by the dictionary instead: three branches for Russian, two for Arabic, one for Japanese, each over the plural.

**Two independent plurals.** Every form of one has to be combined with every form of the other, because the sentence has to agree with both numbers.

```
{spam, plural, =0 {Egg and bacon, no spam} one {Egg, bacon and spam} other {Egg, bacon and # helpings of spam}} for {diners, plural, one {# diner} other {# diners}}.
```

| Target | Segments |
|---|---|
| German | 6 |
| Russian | 20 |
| Japanese | 2 |
| Arabic | 42 would be needed, over the budget of 24, so the message keeps the source's six branches and is reported |

For the Arabic file the ICU Forms window shows which numbers each kept branch is used for: `one` for 1, and `other` for 0, 2, 3, 4, 5, 6 and everything else, because the application uses `other` for every number whose form the message does not have.

**An ordinal and two plurals.** Russian needs 1 times 4 times 4, sixteen segments. Welsh would need 216 and gets the budget warning.

```
{rank, selectordinal, one {#st} two {#nd} few {#rd} other {#th}} lumberjack of {total, plural, one {# lumberjack} other {# lumberjacks}} to fell {trees, plural, one {# tree} other {# trees}}.
```

**Apostrophes and braces as text.** This value has no argument, so it is left as the filter delivered it. The translator types the apostrophe and the brace as text.

```
Type '{' to insert a placeholder, and don''t forget to close it.
```

---

## Workflow notes

**Piloting a new resource file.** Create a project into a language with many forms, such as Arabic, expand it, run Studio's pseudo-translation, then Restore and Generate Target Translations. The report lists anything that was not expanded and why.

**Translation memory.** Expand before Analyse and Pre-translate, so the memory sees whole sentences. Update Main Translation Memories stores the file as it is at the time: forms while expanded, whole messages once restored, with the ICU escaping in them. Store whichever shape the memory is for, or update in both.

**Pre-translated projects.** A memory of whole messages pre-translates a repeated message in one go. Expand then splits each matched message into its forms with the match on every form, or leaves it alone when the keep setting is on and the match is at the threshold or better. A fuzzy match that put a fragment into a target, from a memory of forms, does not read as a whole message; Expand says so in the report and starts those forms empty.

**Several languages.** Each language's file is expanded and restored for that language. Russian gets four forms, Arabic six, Japanese one, from the same source, and German three genders where French gets two with their articles.

**Gender selects across a project.** Import the developer's file once, on the ICU Settings dialog or the wizard page, and save the project as a template. Every project from the template carries the file.

---

## Troubleshooting

**Nothing changed after Expand.**
The file is not JSON or Java properties, its values hold no ICU messages, or it was already expanded. The report lists what was seen.

**A message has only the source's forms and a budget warning.**
It exceeded the branch budget. Raise the budget on the expand page and run again on a fresh project, or accept the source's branches for that message. The ICU Forms window shows which numbers each of those forms is used for; the `other` form takes every number the message has no form for.

**A gender select kept the source's branches.**
Its argument name does not end in Gender or Article and is not declared in the developer's file, or the target language is not in the dictionary. Declare the argument in the file, or add the language on the Grammar dictionary tab.

**French or Italian got genders but no article forms.**
The developer's file does not say the application supplies the article argument for that language, so no selector was inserted. The Task Results window says so per file. Add the language under `supplies` for the argument, import the file again and expand a fresh copy of the file.

**The Messages window lists a form with no translation after I translated everything.**
A form was filled by a memory match that left it empty, or a segment was cleared. The ICU Forms window shows the empty row.

**The reassembled message is red.**
The window names the position. Usually the target contains an unmatched apostrophe or brace typed as text, which Restore escapes. A red reading after that means something Restore cannot repair, such as a placeholder pasted twice.

**Restore left a message expanded, with a comment about its placeholders.**
A target form has lost a placeholder the source has, or gained one. The message stays expanded with its placeholders as tags so the missing one can be placed with QuickPlace. Once it is right, run Restore again: the comment goes and the message is put back together.

**I restored the file and now want to work on the forms again.**
Run Expand ICU. The forms come back with their translations and states.

**Expand split my pre-translated messages into forms.**
That is the default, so a match can be checked form by form. Turn on Keep messages already translated on the Expand page to leave matched messages as they are.

**The settings pages are not under Project Settings.**
Studio shows a plugin task's page only inside the New Project and batch task wizards. Use **ICU Settings** on the Projects view's Add-ins tab, which opens the same pages for the selected project.

**My typed apostrophe was not doubled in the file.**
Studio's AutoCorrect replaced it with a typographic apostrophe (’), which ICU treats as plain text. The file is valid.

**A Spanish, French, Italian or Portuguese file has a "many" form I have never seen.**
CLDR gives these languages, and Catalan, a `many` form for a round million and above. The application uses it, so the form is needed. The Counts column shows the numbers: 1000000, 2000000, 3000000. Translate it as the sentence your language uses for those numbers.

---

## Limitations

- Studio's JSON and Java Resources file types only. Others follow once they have been tested end to end.
- A message with `#` inside a nested plural that has an offset is left unexpanded with a warning; the plugin cannot rewrite it without changing its meaning.
- Restore has to run before Generate Target Translations. Studio's JSON writer does not write placeholder tags, so a message that was never restored, or was left expanded after a placeholder warning, loses its arguments in the generated file.
- A comment on an argument comes back around its placeholder tag, and a comment whose selection ran across the syntax between two forms comes back on both.
- A markup tag pair that opens in one branch of a message and closes in another is not laid out; the message is left as it is with a warning.
- Restore puts the source back from the pattern the file's metadata kept at the first expansion. A comment on a source segment does not survive it.
- The grammar dictionary's article forms cover French and Italian, and its sample nouns German, French, Italian and Spanish. Case, definiteness and anything else the sentence decides rather than the application passes are left to the translator. A project can add languages and respell keys in its developer's file, but cannot add a kind beyond gender and article.

Feedback from anyone translating ICU messages is welcome: a message that lays out oddly, a language whose grammar hint is wrong or missing, a gender set a language needs, a file type you need. A sample file with the report helps.

---

*multifariousICU Support for Trados Studio. The plural rules and their example numbers are the Unicode CLDR plural rules; the gender sets are CLDR's grammatical features.*
