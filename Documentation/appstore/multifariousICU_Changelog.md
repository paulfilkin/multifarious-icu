# multifariousICU Support for Trados Studio - Changelog

## 1.0.2.0 (September 2026)

### Fixed

- In a message kept with the source's own forms, over the branch budget, the ICU Forms
  window showed the `other` row with the numbers of the `other` form only. The application
  uses that form for every number whose form the message does not have, so an Arabic `other`
  row is used for 0, 2, 5 and 11 as well. The Counts column now lists those numbers and the
  tooltip names the forms covered. A form the language does not have shows no numbers.
- The count box under the ICU Forms window marks the `other` row for such a number, as the
  application would. Before, it marked nothing.
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
