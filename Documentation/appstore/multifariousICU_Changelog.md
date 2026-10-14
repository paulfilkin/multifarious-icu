# multifariousICU Support for Trados Studio - Changelog

## 2.0.0.0 (October 2026)

Two batch tasks, **Expand ICU** and **Restore ICU**, and a grammar dictionary that lays out a
select by gender as the target language needs it. ICU Finalise Messages is replaced by
Restore, and a project template or task sequence that named it has to be re-saved with the new
task.

### Changed

- **Restore ICU** replaces ICU Finalise Messages. It does everything Finalise did, removing the
  forms the language does not use, escaping apostrophes and braces typed as text, checking the
  placeholders and filling an untranslated form from the source with a warning, and then puts
  each message back together as one plain segment on each side, with no tags and no locked
  spans. The target comes back in the shape the translator worked in, with the shared text
  inside every form; the source comes back as the developer wrote it, so a memory updated from
  the restored file matches the same message in the next project. Run it before Generate
  Target Translations, or whenever the whole messages are wanted back.
- **Expand ICU** lays a restored message out again, each form with its translation, its
  confirmation level, its lock and its match, so expanded and restored can be switched between
  at will. The pattern as the file carried it stays in the file's metadata.
- A message that arrives pre-translated as a whole, from a memory of restored messages, is
  split into its forms with the match on every form.
- The task names are Expand ICU and Restore ICU; the reports are named the same.
- The Expand ICU settings page has two tabs, **Options** and **Grammar dictionary**. The
  options are in the order the task decides them: which messages to expand and the branch
  budget, messages already translated, the segment text and the placeholders, and what to do
  with a value that does not parse.
- A click on a row in the ICU Forms window moves the editor to that segment.

### Added

- **Gender selects.** A select whose argument name ends in Gender or Article, or that the
  project's developer's file declares, is laid out like a plural: one segment per form the
  target language needs, from a grammar dictionary built on CLDR's grammatical features for
  61 languages. German gets masculine, feminine and neuter; French masculine and feminine,
  each with its article forms where the application supplies the article argument; Japanese
  one form. Restore removes the forms the language does not use. The ICU Forms window shows
  the grammatical term where a plural shows its numbers, with a sample noun for German,
  French, Italian and Spanish, and the verifier names a form with its term.
- **The Grammar dictionary tab** shows the dictionary in force as two tables, the languages
  with their keys and the arguments the application passes, both editable, with Import,
  Export, Remove file and Save template for the developer's file. The file names the
  application's arguments, says for which languages it supplies a dependent argument such as
  the article, respells keys and adds languages. A project template carries it.
- **ICU Settings** on the Projects view's Add-ins tab opens the Expand ICU, Grammar dictionary
  and Restore ICU pages for the selected project, with Reset to defaults. Studio shows a
  plugin task's page only inside its wizards, so this is how a project's settings are changed
  between runs.

- Expand setting **Keep messages already translated**, off by default: a message whose every
  segment is matched at the chosen percentage or better is left as it is, and locked if
  **Lock kept segments** is on. Perfect Match and Context Match count as 100%. A restored
  message is kept the same way when every form it had was matched. Restore ignores a kept
  message and Generate Target Translations writes it as it stands.
- The Expand report counts kept messages and shows the keep setting.
- Markup opened as tags through the file type's Embedded Content settings survives both tasks.
  Expand puts each tag into every form it belongs to, and Restore puts the tags back where
  each form held them, so the generated file carries the markup as the source did. A tag pair
  that opens in one branch and closes in another is not laid out; the message is left as it is
  with a warning.
- Comments survive both tasks. A comment on a form comes back after Restore around the text
  of the form it was on; a comment on a restored message comes back after Expand on every
  form that holds the text it was on.

### Fixed

- Expand and Restore now run inside Studio's own task pass. Before, a task placed directly
  after Expand in a task sequence, such as Pseudo-translate in a project template, saw the
  file as it was before the expansion and wrote it back unexpanded.
- Segment numbers run on through the file after Expand and Restore, as Studio's own filters
  number them, instead of starting again at 1 for every message.

### Notes

- Studio's JSON writer does not write placeholder tags, so Restore must still run before
  Generate Target Translations; it is what turns the forms back into one plain message.
- Update Main Translation Memories stores whatever shape the file is in when it runs: one unit
  per form while expanded, one per whole message once restored. The tasks do not decide this.
- An article form is only inserted where the developer's file says the application supplies
  the article argument for that language, because an inserted selector changes what the
  application must pass at run time. Without it the message gets its genders and a note.

## 1.0.2.0 (September 2026)

### Fixed

- In a message kept with the source's own forms, over the branch budget, the ICU Forms
  window showed the `other` row with the numbers of the `other` form only. The application
  uses that form for every number whose form the message does not have, so an Arabic `other`
  row is used for 0, 2, 5 and 11 as well. The Counts column now lists those numbers and the
  tooltip names the forms covered. A form the language does not have shows no numbers.
- The count box under the ICU Forms window marks the `other` row for such a number, as the
  application would. Before, it marked nothing.
- For a message with one plural inside another, the count box named the inner form of the
  first marked row. It now names the form the number selects.
- The batch tasks are unchanged.

## 1.0.1.0 (September 2026)

### Fixed

- The ICU Forms window showed the numbers that select a form the way the Common Locale Data
  Repository (CLDR) writes them. For Spanish, French, Italian, Portuguese and Catalan that
  included a compact form such as `1c6`, which means 1000000. The Counts column now shows the
  plain numbers.
- The count box under the ICU Forms window accepts a compact number such as `1c6` or `1.2c6`
  and marks the form it selects. Before, it matched nothing.
- The batch tasks are unchanged. Files expanded with 1.0.0.0 need no rework.

## 1.0.0.0 (September 2026)

Initial release.

### Batch tasks

- **ICU Expand Plural Forms** restructures every ICU `plural` and `selectordinal` message in a
  JSON or Java properties target file into one segment per form the target language needs,
  each a whole sentence. The syntax between the segments is locked; the arguments are
  placeholder tags, with an option to lock them. A `select` keeps its branches, and a plural
  inside one is expanded. A message with arguments and no plural becomes one segment with the
  arguments protected. A message over the branch budget keeps the source's branches with the
  syntax protected, and is reported.
- **ICU Finalise Messages** removes the forms the language does not use, escapes ICU special
  characters typed as text, checks the placeholders, fills untranslated forms from the source
  with a warning, and turns the placeholder tags into locked text for Studio's file writers. A
  message with a placeholder mismatch keeps its tags so it can be corrected. Each finding is a
  comment on the target segment, removed by the next run. A second run on a clean file changes
  nothing.
- Settings pages for both tasks with a help panel under every group.
- A report per language pair in Studio's Reports view listing every message and what happened
  to it. Warnings also appear in the Task Results window.

### Editor

- **ICU Forms** window under the editor: every form of the active segment's message with the
  numbers that select it, source and target as sentences, the active row marked, a count box,
  and the reassembled message checked as you type. Zoom with Ctrl and the mouse wheel or the
  buttons in the corner. Explains itself when a file is of another type, has not been
  expanded, or the segment is ordinary text.
- **ICU Verifier** under Verification with a settings page: untranslated forms, placeholder
  mismatches, a count marker typed as text, and a message that will not parse, each with a
  chosen severity.

### Notes

- Studio's JSON and Java Resources file types only. Other file types are left alone by the
  tasks, and the ICU Forms window says so.
- Finalise must run before Generate Target Translations: Studio's JSON writer does not write
  placeholder tags, and Finalise is what turns them into locked text.
- Studio's editor AutoCorrect replaces a typed straight apostrophe with a typographic one,
  which ICU treats as plain text. Only the straight apostrophe is ICU syntax, and Finalise
  escapes it.
- User interface, settings help, reports and verifier messages in English, German, French,
  Spanish, Italian, Japanese, Korean, Russian and Simplified Chinese.
