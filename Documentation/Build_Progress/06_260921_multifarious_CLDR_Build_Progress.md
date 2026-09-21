# 06 - multifarious_CLDR Build Progress - 2026-09-21

Sixth session.  A review of the plugin against the compact decimal notation defect found in
the cloud app on 21 September (its commit `d7f138b`), then the port of the cloud fix.  The
changes are built, tested and deployed to Studio's Packages folder, not yet seen in Studio and
not committed.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\06_260921_multifarious_CLDR_Build_Progress.md`.
>
> The compact notation fix is in the working tree, built, 1,433 tests passing, and the Debug
> package is deployed but not yet started in Studio.  Start with the Studio check written up
> under open items: a Spanish target, the ICU Forms window's Counts column on the `many` row,
> and the count box with `1c6`, `1.2c6`, `1c` and `c6`.  Then the commit, if Paul has not made
> it.  After that the open items from handover 05 stand: the Counts column of a walked message,
> the Studio look at count-based parity, the store housekeeping.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

The compact notation fix is written and tested; the Studio check and the commit remain.

## The defect

Since CLDR 42 the plural samples and rules use compact decimal notation: `1c6` is 1000000
written compactly, `1.2c6` is 1200000, and `e` is a synonym for `c`.  The exponent is a plural
operand.  Spanish, French, Italian and Portuguese cardinal `many` is
`e = 0 and i != 0 and i % 1000000 = 0 and v = 0 or e != 0..5`, so `1.1c6` selects `many` while
its numeric value 1100000 selects `other`.  Nine cardinal locales in CLDR 48 carry compact
samples: ca, es, fr, it, lld, pt, pt-PT, scn, vec.  No ordinal does, and no category lists a
compact sample first.

## What the review found

- **Covered already**: `PluralOperands` parses the written form with the exponent, the rule
  parser maps `c` and `e` to the C operand, the data is CLDR 48 with `many` for the four
  languages, and the conformance test asserts every sample selects its own category.
- **`MessageRenderer`** was the pre-fix renderer: a plain `decimal.TryParse` of the count,
  which throws on `1c6`, and the count reformatted as `number - offset` before the category
  decision, which zeroes the exponent.
- **`PreviewForms`** parsed sample text and typed counts with `decimal.Parse`/`TryParse`: the
  explicit-value collision loop, the offset helpers and the count resolver.
- **`IcuFormsReader`** took the raw samples for the Counts column and the tooltip, so a
  Spanish `many` row read `1000000, 1c6, 2c6, 3c6, 4c6, 5c6`.  The count box goes through the
  preview's resolver, so `1c6` matched no row instead of `many`.
- **Why nothing crashed here**: the plugin has no self-check that renders every message at
  every sample count (the cloud's I12), the window renders segments by its own text
  substitution, and the `#` value is the first sample of the category, never compact.

## What was done

- **`Icu.Core\MessageRenderer.cs`** matches the reference: `TryParseNumber` reads the compact
  form (mantissa, marker, exponent, bound 28), the numeric value has the consumed fraction
  digits gone (`1.2c6` is 1200000, `1.0000001c6` is 1000000.1), a compact count with no offset
  goes to the category decision as written, and an offset forces numeric subtraction as ICU
  does.  One net48 substitution: `decimal.Scale` is .NET 7, so `ScaleOf` reads it from
  `decimal.GetBits`.  Recorded under Porting in CLAUDE.md.
- **`Icu.Cldr\Rules\SampleDisplay.cs`** copied from the reference: examples render at their
  numeric value, duplicates collapse (`1000000` and `1c6`), and a converted value that no
  longer selects its own category is dropped.  Spanish `many` therefore lists
  `1000000, 2000000, ...` and no decimal examples at all.
- **`PreviewForms`**: the count resolver accepts what `PluralOperands.TryParse` accepts, the
  offset subtraction takes the numeric value from the operand parser with the sign read off the
  text (`StartsWith(string)`, the char overload is not on net48), and the rows use the
  converted examples, so the `decimal.Parse` calls left only ever see plain numbers.
- **`ExpansionPlanner`** uses `SampleDisplay` for the example metadata, in step with the
  reference; the writer does not emit it.
- **`IcuFormsReader`**: the Counts column, the tooltip and the bound counts of an outer
  selector come from `SampleDisplay`, with fractional-only derived from the converted lists.
- **Tests** (28 new, 1,433 in all): the reference's renderer tests (written-form selection,
  `#` at the numeric value, explicit value matching numerically, offset forcing numeric,
  hoisting property for a compact count, malformed `1c`, `c6`, `1c2c3`, `1c-2` rejected),
  `SampleDisplayTests`, the preview's resolver and row tests for es-ES, `1c` and `c6` in the
  operand tests, and a forms reader test that a Spanish `many` row shows the plain counts and
  that `1c6` and `1.2c6` typed in the count box land on it.

## Open items

- **Studio check of the fix**.  Studio was not running this session; the Debug package is in
  `Packages` (21 Sep, 15:48) and installs on the next start.  Make a project with
  `test-corpus\messages.json` and a Spanish target, run Expand, open the file, and look at the
  `many` row of a plural in the ICU Forms window: the Counts column should read
  `1000000, 2000000, 3000000, 4000000, 5000000, 6000000` and the tooltip the same.  In the
  count box `1c6` and `1.2c6` should mark the `many` row, `999999` the `other` row, and `1c`
  and `c6` nothing.
- **Commit** the change once seen.  Proposed message in the session's closing report.
- The Counts column of a walked message, the Studio look at count-based parity, the store
  housekeeping, the refiner tool's id reuse and the verifier's diagnostics lines: all as in
  handover 05.

## Traps

- **`decimal.Scale` and `string.StartsWith(char)` are not on net48**; a file copied from the
  reference that uses either will not build.  The substitutions are in CLAUDE.md.
- All earlier traps stand (handovers 01 to 05).
