**Translate ICU MessageFormat plurals in Trados Studio as whole sentences, one per form the target language needs, with the syntax protected and the message reassembled for you.**

Software strings that count things are written as ICU MessageFormat messages: `{count, plural, one {# unread message} other {# unread messages}}`. English needs two forms. Russian needs four, Arabic six, Japanese one. Until now a translator got the whole brace expression as one segment and had to know the ICU syntax, know which forms their language needs, and type the missing ones by hand without breaking a bracket. **multifariousICU Support for Trados Studio** turns that into ordinary translation.

### What it does

- **ICU Expand Plural Forms**, a batch task, restructures every plural and ordinal message in a JSON or Java properties file into one segment per form the target language needs, each a complete sentence. The syntax between the forms is locked; arguments such as `{name}` and the count marker `#` are locked spans inside the sentence. Every source segment can carry a comment naming its form and the counts that select it, with a grammar hint where one is known.
- **ICU Finalise Messages**, a batch task for the end of the workflow, prunes each message to the forms the language uses, escapes the apostrophes and braces a translator typed as text, checks the placeholders and fills any untranslated form from the source with a warning, so the generated file is valid ICU.
- **ICU Forms**, a window under the editor, shows every form of the message the active segment belongs to, with sample counts, the source and the translation rendered as sentences, a count box that tells you which form a number selects, and the whole message reassembled and checked as you type.
- **ICU Verifier** joins Studio's verifiers: an untranslated form, placeholders that differ from the source, a count marker typed as text, or a message that will not parse, all listed in the Messages window on F8.
- **Reports** per language pair after each task, and settings pages with plain-language help, all in nine languages.

### Who it's for

Translators and LSPs working on software localisation: application resource files, mobile and web app strings, anything written for the ICU MessageFormat libraries that most modern frameworks use. The plural rules come from the Unicode CLDR data, the same data the applications themselves use at run time, so the forms Studio offers are exactly the forms the application will select.

### What it handles

Studio's own JSON and Java Resources file types. The tasks run on the bilingual target files, so a multilingual project gets exactly the right forms for each language.

### A word on maturity, and an invitation

The tasks, the window and the verifier have been proven end to end on a corpus of ICU messages covering nested selects, explicit values, offsets, ordinals and argument-only messages, generating files that parse and render identically to the source for every count. Real projects will have messages that corpus never saw. **Feedback from anyone translating ICU messages is very welcome**: a message that lays out oddly, a form a language needs that the comment describes badly, a file type you would like added. Every report makes the plugin more useful for everyone.

Full documentation: <https://multifarious.filkin.com/product/icu-support/>
