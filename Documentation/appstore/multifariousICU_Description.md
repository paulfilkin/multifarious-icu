**Translate ICU MessageFormat plurals in Trados Studio as whole sentences, one per form the target language needs, with the syntax protected and the message reassembled for you.**

Text that includes a number is written differently from one language to the next: English has two forms, Russian four, Arabic six, Japanese one. Applications handle this with ICU MessageFormat, the notation of the International Components for Unicode library, in which one string carries every form: `{count, plural, one {# unread message} other {# unread messages}}`. A translator normally receives that whole expression as one segment and has to know the syntax, know which forms the language needs, and type the missing ones by hand. **multifariousICU Support for Trados Studio** turns it into ordinary translation.

### What it does

- **ICU Expand Plural Forms**, a batch task, restructures every plural and ordinal message in a JSON or Java properties file into one segment per form the target language needs, each a complete sentence. The syntax between the forms is locked. Arguments such as `{name}` and the count marker `#` are placeholder tags, placed like any tag in Studio.
- **ICU Finalise Messages**, a batch task for the end of the workflow, removes the forms the language does not use, escapes apostrophes and braces typed as text, checks the placeholders, fills an untranslated form from the source with a warning, and turns the tags into locked text for Studio's file writers, so the generated file is valid ICU.
- **ICU Forms**, a window under the editor, shows every form of the message the active segment belongs to, with the numbers that select each, the source and target as sentences, a count box, and the whole message reassembled and checked as you type.
- **ICU Verifier** joins Studio's verifiers: an untranslated form, placeholders that differ from the source, a count marker typed as text, or a message that will not parse.
- **Reports** per language pair after each task, and settings pages with help, in nine languages.

### Who it is for

Translators and language service providers working on software localisation: application resource files, mobile and web strings, anything written for the ICU libraries. The plural rules come from CLDR, the Common Locale Data Repository of the Unicode Consortium, which is the same data the applications use, so the forms Studio offers are the forms the application will select.

### What it handles

Studio's own JSON and Java Resources file types. The tasks run on the bilingual target files, so a multilingual project gets the right forms for each language.

### Maturity

The tasks, the window and the verifier have been tested end to end on a set of ICU messages covering nested selects, exact values, offsets, ordinals and argument-only messages, generating files that parse and render as the source does for every number. Real projects will contain messages that set never saw. Feedback from anyone translating ICU messages is welcome: a message that lays out oddly, a form a language needs that the window describes badly, a file type you would like added.

Full documentation: <https://multifarious.filkin.com/product/icu-support/>
