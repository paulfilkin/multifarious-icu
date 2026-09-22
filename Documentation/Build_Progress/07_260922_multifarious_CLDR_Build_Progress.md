# 07 - multifarious_CLDR Build Progress - 2026-09-22

Seventh session, short.  A review of the plugin against the cloud app's count-or-all defect
of 22 September (its commit `cedb3b4`, note `Documentation\Cloud_Notes\Count_Or_All_Select_Rows.md`),
and the port of its preview fix for library parity.  No release: nothing a user sees changes.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\07_260922_multifarious_CLDR_Build_Progress.md`.
>
> 1.0.2.0 is on the store.  The count-or-all port is in the tree (1,441 tests), for parity
> with the cloud app's `PreviewForms`; the window was never exposed.  Nothing is open in the
> code.  The open items from handovers 05 and 06 stand: the Studio look at count-based
> parity, the store housekeeping, and two offered-not-taken items, the count box for a plural
> inside a select and explicit values in the Counts column.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

1.0.2.0 is on the store; the shared preview helper matches the cloud's again.

## The defect, as found in the cloud

The count-or-all pattern is a select and a plural sharing one argument:
`{n, select, all {All datasets} other {{n, plural, one {# dataset} other {# datasets}}}}`.
The cloud's all-forms preview builds one row per branch of the outermost selector and, for a
select, substituted the branch key as the argument's value.  The `other` row set `n` to
"other", rendering descended into the nested plural on the same argument, and the renderer
threw.  The cloud's data endpoint built rows for every unit with no guard, so one such message
failed the whole page.  Expansion, Finalise and the self-check were unaffected.

## What the review found

- **The window is not exposed.**  `IcuFormsReader` renders a row by substituting its tags:
  `#` from the row's category sample, other arguments from fixed samples.  A select's argument
  never appears inside a segment, since the select's syntax is locked text between the
  segments, so nothing puts "other" into a plural.  The bound counts skip select frames, and
  the layout record keys the two selectors on different paths, `n` and `n:other/n`.
- **One bad message degrades alone.**  The view part controller wraps every refresh; a reader
  exception is logged to diagnostics and that message shows the "Not an ICU message" panel.
  The window shows one message at a time in any case.
- **No count box**, since the outermost selector is a select, so a typed count cannot reach a
  branch key.
- **The shared `PreviewForms.Rows` had the crash**: the plugin's copy was the pre-fix one.
  It is called only from tests here, so no user could reach it, but it is the library the
  two projects share.

## What was done

- **`PreviewForms`** ported from the cloud: `ContainsPluralOn` walks the source and target
  trees for a plural-kind selector on the select's argument, and the `other` row then
  substitutes `FirstCountAvoiding`, the smallest count from 2 that matches no named branch
  key, so the select still falls through and the plural renders it.  A named branch keeps its
  key.  No net48 substitution needed.
- **Tests** (4 new, 1,441): the cloud's three preview tests (the `other` row renders
  "2 datasets"; a numeric key `2` pushes the substitute to 3; a gender select keeps its key),
  and one reader test running the message through Expand for Russian and reading it back:
  paths `n:all` and `n:other/n:one` to `n:other/n:other`, expanded set `n:other/n`, the `all`
  row rendering "All datasets" with no counts, the `other / one` row rendering "1 dataset",
  no count argument, and a typed count matching nothing.
- The Debug package was deployed (Studio closed) but there is nothing to see in Studio.

## Decisions taken this session

- **No release for this change** (Paul, 22 Sep 2026): the only production code touched is a
  method the plugin never calls at run time.  It rides along with the next release, with no
  changelog entry.

## Open items

- The Studio look at count-based parity and the store housekeeping, as in handover 05.
- The count box for a plural inside a select, and explicit values in the Counts column, as in
  handover 06: offered, not taken up.

## Traps

- All earlier traps stand (handovers 01 to 06).
