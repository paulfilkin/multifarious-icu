# 06 - multifarious_CLDR Build Progress - 2026-09-21

Sixth session.  A review of the plugin against the compact decimal notation defect found in
the cloud app that morning (its commit `d7f138b`), the port of that fix as 1.0.1.0, then the
walked Counts column from handover 05 and two count box defects found in Studio while checking
it, as 1.0.2.0.  Both releases are on the store with their changelog entries, the user guide
and documentation match, and the tree is clean.  Eight commits on `main`, pushed.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\06_260921_multifarious_CLDR_Build_Progress.md`.
>
> 1.0.1.0 (compact notation) and 1.0.2.0 (walked Counts column, count box fallback and
> label) are on the store, with the changelog, user guide and documentation matching.  1,437
> tests.  Nothing is open in the code from this session.  The open items from handover 05
> stand: the Studio look at count-based parity, the store housekeeping.  A technical note for
> the cloud app is at `Documentation\Cloud_Notes\Compact_Notation_and_Walked_Counts.md`
> (gitignored, local).
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

1.0.2.0 is on the store; nothing is open in the code.

## The compact notation defect (1.0.1.0)

Since CLDR 42 the plural samples and rules use compact decimal notation: `1c6` is 1000000
written compactly, `1.2c6` is 1200000, and `e` is a synonym for `c`.  The exponent is a plural
operand.  Spanish, French, Italian and Portuguese cardinal `many` is
`e = 0 and i != 0 and i % 1000000 = 0 and v = 0 or e != 0..5`, so `1.1c6` selects `many` while
its numeric value 1100000 selects `other`.  Nine cardinal locales in CLDR 48 carry compact
samples: ca, es, fr, it, lld, pt, pt-PT, scn, vec.  No ordinal does, and no category lists a
compact sample first.

What the review found:

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

- **Compact notation** (commit 2529ea9).  `Icu.Core\MessageRenderer.cs` matches the
  reference: `TryParseNumber` reads the compact form (mantissa, marker, exponent, bound 28),
  the numeric value has the consumed fraction digits gone (`1.2c6` is 1200000, `1.0000001c6`
  is 1000000.1), a compact count with no offset goes to the category decision as written, and
  an offset forces numeric subtraction as ICU does.  `Icu.Cldr\Rules\SampleDisplay.cs` copied
  from the reference: examples render at their numeric value, duplicates collapse, and a
  converted value that no longer selects its own category is dropped, so Spanish `many` lists
  `1000000, 2000000, ...` and no decimal examples.  `PreviewForms`: the count resolver accepts
  what `PluralOperands.TryParse` accepts, the offset subtraction takes the numeric value from
  the operand parser, the rows use the converted examples.  `ExpansionPlanner` uses
  `SampleDisplay` for its example metadata, which the writer does not emit.  `IcuFormsReader`:
  the Counts column, the tooltip and the bound counts of an outer selector come from
  `SampleDisplay`.  Seen in Studio on a Spanish target: the Counts column and `1.2c6` in the
  count box selecting `many`.  Released as 1.0.1.0 (commit fd32176).
- **Walked Counts column** (commit b2f243e, handover 05's first open item).
  `IcuFormsReader` reads the layout for each row's innermost selector: its path in
  `icu:expandedSelectors`, and the branch keys the layout has for it.  In a selector the task
  did not category expand, the `other` row's counts are the merge of its own and of every
  category the language has and the message has no branch for, whole numbers first then
  fractions, and the tooltip says "Also used for: zero, two, few, many"; a branch for a
  category the language does not have shows no counts and "Not used".  The `#` sample stays
  the branch's own first count, so the rendered sentence is unchanged.  The count box falls
  back to the outermost selector's `other` row when the resolved category has no row and the
  selector was not expanded, as ICU does.  Seen in Studio: Arabic, `nested.json`,
  `sync.status`, the `other` rows read `0, 2, 3, 4, 5, 6` and 5 marks them.
- **Count box label** (commit 7accd4d), found in that screenshot: 5 said "selects one" while
  the `other` rows were marked.  The view model took the first matched row's own category, an
  inner branch in a nested message, walked or not.  `RowsFor` now also returns the outermost
  branch the rows were matched on, and the label uses it.  Covered by a view model test, not
  yet seen in Studio.  Released with the above as 1.0.2.0 (commit fa3333e).
- **Documentation**: changelog entries for both releases; user guide and documentation
  updated for each (commits ea3e265 and 748daab): a Spanish row in the plural forms table and
  the `many` form for millions, counts written out in full, the count box accepting `1c6`,
  what the window shows for a message kept with the source's forms, and a troubleshooting entry
  in each for a `many` form a translator has not seen.
- **Tests**: 32 new, 1,437 in all.  The reference's renderer tests, `SampleDisplayTests`,
  the preview's resolver and row tests for es-ES, `1c` and `c6` in the operand tests, a
  Spanish forms reader test, three walked reader tests (the over-budget Arabic sync message,
  Russian walked by a budget of 2, a Russian ordinal walked by switching ordinal expansion
  off) and a view model test for the label on a nested message.
- Porting notes in CLAUDE.md (gitignored) record the two net48 substitutions.

## Decisions taken this session

- **The count box drives the outermost plural only, and that stays** (Paul, 21 Sep 2026).
  In a nested message a count of 1 on the Arabic sync message marks both rows under
  `files: one`, while the Counts column of each row belongs to the inner `devices` selector.
  Naming the argument on the label, or a box per argument, was offered and declined as
  over-complication.  The user guide says what the box does in a nested message.
- **No count box for a plural inside a select** (`profile.itemsByGender`, 18 Arabic rows):
  the box appears only when the outermost selector is a plural.  The matching by path
  component would work for a plural under a select.  Offered, not taken up.

## Findings worth keeping

- **A budget of 1 passes a message through rather than walking it**: the walked layout of a
  two-branch plural needs two segments, and the processor passes the message through when the
  walked plan is over budget too.  A budget of 2 walks a single English plural.  A single
  plural for a one-category language such as Japanese can never be over budget, so the "never
  used" branch case is tested by switching ordinal expansion off instead.
- **Whole numbers before fractions when merging categories' samples**: a numeric sort would
  list Russian walked `other` as 0, 0.1, 0.2, 0.3, 0.4, 0.5 and bury 2 and 5.
- **`decimal.Scale` and `string.StartsWith(char)` are not on net48.**

## Open items

- **Explicit values are not subtracted from a category's counts**: with `=0` present, a
  Russian `many` row still lists 0.  The reader does not know the selector's offset, on which
  the comparison depends, so it is not the one-line filter it looks like.  Left as is.
- The count box label on a nested message has not been seen in Studio; the tests cover it.
- The Studio look at count-based parity, the store housekeeping, the refiner tool's id reuse
  and the verifier's diagnostics lines: all as in handover 05.

## Traps

- **A file copied from the reference may use `decimal.Scale` or `StartsWith(char)`**; neither
  builds on net48.  The substitutions are in CLAUDE.md.
- **Build with `-p:DeployPluginPackage=false` while Studio is open**, and remember the Debug
  package in `Packages` is then behind the code.
- All earlier traps stand (handovers 01 to 05).
