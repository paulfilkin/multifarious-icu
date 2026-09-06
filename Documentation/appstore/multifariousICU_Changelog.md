# multifariousICU Support for Trados Studio - Changelog

## 1.0.0.0 (September 2026)

Initial release.

### Batch tasks

- **ICU Expand Plural Forms** restructures every ICU `plural` and `selectordinal` message in a
  JSON or Java properties target file into one segment per CLDR form the target language needs,
  each a whole sentence with the syntax locked and the arguments protected. A `select` is walked
  rather than expanded, since its branches are the developer's. A message with arguments and no
  selector is laid out as one segment with the arguments protected. A message over the branch
  budget is laid out with its syntax protected and its own branches, and reported.
- **ICU Finalise Messages** prunes each message to the target language's form set, escapes ICU
  special characters typed as text, checks placeholder parity, fills untranslated forms from the
  source with a warning, and is safe to run twice.
- Settings pages for both tasks with plain-language help under every group.
- A report per language pair in Studio's Reports view listing every message and what happened
  to it.

### Editor

- **ICU Forms** window under the editor: every form of the active segment's message with
  sample counts, source and target rendered as sentences, the active row marked, a count box,
  and the reassembled message checked as you type. Zoom with Ctrl and the mouse wheel or the
  buttons in the corner, remembered between sessions. Explains itself when a file is of another
  type, has not been expanded, or the segment is ordinary text.
- **ICU Verifier** under Verification with a settings page: untranslated forms, placeholder
  mismatches, a count marker typed as text, and a message that will not parse, each with a
  chosen severity.

### Notes

- Studio's JSON and Java Resources file types only. Other file types show an explanation in
  the ICU Forms window and are left untouched by the tasks.
- Studio's editor AutoCorrect replaces a typed straight apostrophe with a typographic one,
  which ICU treats as plain text. Only the straight apostrophe is ICU syntax; Finalise escapes
  it correctly, and the typographic one needs nothing.
- User interface, settings help, reports and verifier messages in English, German, French,
  Spanish, Italian, Japanese, Korean, Russian and Simplified Chinese. The comments written
  into the source segments are English.
