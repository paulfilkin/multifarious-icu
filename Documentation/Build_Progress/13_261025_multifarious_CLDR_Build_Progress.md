# 13 - multifarious_CLDR Build Progress - 2026-10-25

Thirteenth session, the first after the release, on a new branch `release/2.1` (Paul's
call).  Two pieces.  First, the three ICU Forms window items that handover 12 listed as
2.0.1 candidates: a passed-through message shows the unit's own warning and the setting
that answers it, an ordinary segment's note names the unit's key, and the active row is
kept in view (committed, `0454680`).  Second, from Paul's Studio run of that build: two
article selects on one argument in one sentence came out as a hundred Italian forms, ninety
of them unreachable, so the hoister now collapses a selector on an argument already bound
above, and the window's summary line says where nested forms come from (`98f22df`).  Third,
from the run of that build: the header grew to ten lines under a long summary, so it is a
Grid with the summary wrapping, and the summary says when a message was laid out as written
(`36a447b`).  Fourth, from Paul's embedded content run (Project 83): the window has a
restored state, and the SSML round trip through the HTML processor is recorded with what the
HTML filter does to it.  1,675 tests.

## Kickoff prompt for the next session (paste this)

> Read `CLAUDE.md`, then
> `Documentation\Build_Progress\13_261025_multifarious_CLDR_Build_Progress.md`.
>
> 2.0.0.0 is live on the AppStore (14 October 2026).  Work since then is on `release/2.1`,
> branched from `main` at `733539c` on 25 October: the ICU Forms window reads a passed-through
> unit's warning, names the key on an ordinary segment and scrolls to the active row
> (`0454680`); the hoister collapses a selector on an argument already bound above, so two
> article selects in one sentence are ten Italian forms and not a hundred, and the window's
> summary says where nested forms come from (`98f22df`); the header is a Grid that wraps the
> summary, and the summary says when a message was laid out as written (`36a447b`); the
> window has a restored state (fourth commit).  Twelve new strings in nine resource files,
> thirteen new tests (1,675), `tools\check-forms-scroll.ps1`.  The version numbers are still
> 2.0.0.0.  Project 83 proved SSML through the HTML embedded content processor once `speak`
> and `phoneme` are inline elements; the HTML writer's entities are the open question there.  The hoister change is in `Icu.Core`, a file copy
> from the reference project at `c:\Users\paul\Documents\GIT\.NET\CLDR Support\` (read
> only), and has not been copied back there or into its design's section 5.9.
>
> Open: Paul's Studio run of the collapse and the summary line (the window items passed
> their first run); the website screenshots (002, 004, 007 and 009 to 015) and the HTML
> guide at `Documentation\website\multifariousICU_UserGuide_2.0.html` (local); the native
> read of the sixty strings added since 14 October; the uncommitted `tools\plan-file.ps1`,
> which Paul has not yet decided to keep; the version and changelog for 2.1 when Paul sets
> them.  The source material under `Documentation\CustomDictionaries` is local and must not
> be named in anything published.
>
> Follow the working conventions in `CLAUDE.md`: agree a plan before coding, British English,
> no em-dashes, never commit or push without being asked, propose the commit message first.
> Documentation prose follows the voice rules in the memory file `feedback-documentation-voice`.

## Where we are in one line

Four commits on `release/2.1`: the window improvements, the same-argument collapse with the summary line, the header fix with the walked wording, and the restored state; the SSML route is proven through the plugin and waits on the HTML writer's entity setting.

## What was asked (Paul, 25 October 2026)

The open items from handover 12.  Of those, three are code: the window on a passed-through
unit, the window naming the key on an ordinary segment, and the window scrolling to the
active row.  The rest need Paul (screenshots, the native read, `plan-file.ps1`) or are
separate decisions (editable hints and samples mean extending the developer's file format).
Agreed as proposed, on `release/2.1` rather than `release/2.0`.

## What was built

- **A passed-through unit shows its warning** (`IcuFormsViewModel.Show`).  The reader returns
  null for a unit without the plugin's context, which a passed-through unit is; before falling
  to "Not an ICU message" the view model looks for a unit comment by `Constants.CommentAuthor`
  opening with `Constants.PassedThroughCommentPrefix` (new, the literal the processor already
  wrote) and shows a new empty state: title "ICU message passed through unexpanded", then
  the first line naming the key where the unit has one, the task's reason as written, and a
  pointer at the Expand ICU page, one wording for the branch budget and one for a value that
  does not parse; a hoist failure gets no pointer.  Rows, the Counts column and the count box
  stay empty, as the handover asked.  Only the task's own comment counts; a translator's
  comment opening with the same words does not.
- **An ordinary segment names its key** (`ShowNotIcu(string key = null)`).  "The active
  segment is ordinary text with nothing to expand: ['_comment']." where `ResourceKey.Of`
  finds a key on the unit, the old wording otherwise.  Limit, found on the way: the editor's
  document model (`IStudioDocument`, decompiled) hands out segment pairs and their paragraph
  units only, never structure units, so under the HTML embedded content processor neither
  this nor the passed-through state can reach the key on the structure unit ahead of the
  sub-content.  The window then names no key.  Excluding comment-like values from
  translation stays the JSON file type's job, through its parser rules.
- **The active row is kept in view** (`IcuFormsView.xaml.cs`).  The view watches the view
  model's `Rows` for additions and each row's `IsActive`; one scroll per refresh is deferred
  to `DispatcherPriority.Loaded`, finds the active row's container, and if it lies outside the
  scroller's viewport sets the offset so the row is centred.  A row already in view is left
  alone, so the translator's own scrolling is not fought.  The ScrollViewer and ItemsControl
  are named `RowsScroller` and `RowsControl`; a test asserts the names are in the markup.
  The click navigation goes the other way and is unchanged.
- Six new strings in nine resource files (`Forms_PassedThroughTitle`, `Forms_PassedThrough`,
  `Forms_PassedThroughKeyed`, `Forms_PassedThroughBudget`, `Forms_PassedThroughParse`,
  `Forms_NotIcuKeyed`), translated by me, not read natively; `UIStrings.cs` regenerated.
- Tests: three new in `IcuFormsViewTests` for the states (an over-budget unit through the
  processor with `MaxUnitsPerMessage = 1`, a broken value, the translator's look-alike
  comment, the three key shapes) and one for the named parts.  1,666 in all.
- `tools\check-forms-scroll.ps1`, new: hosts the view offscreen in Windows PowerShell over an
  Arabic plural (six rows, three visible), moves the active segment and reads the offset back
  after each move, PNGs to `Documentation\Testing\renders` (gitignored).  Passed: first row 0,
  last row 37 (the scroller's maximum), a row above the viewport back to 0, a row already in
  view unmoved from either end.

## Decisions recorded

- `release/2.1` for this work, from `main` at `733539c` (Paul).  Noted in `CLAUDE.md`'s
  branch paragraph.  The version numbers (`AssemblyInfo.cs`, `pluginpackage.manifest.xml`)
  stay 2.0.0.0 until Paul sets them.

## Proposed commit

Stage: `Constants.cs`, `Services\IcuExpandProcessor.cs`,
`Editor\ViewModels\IcuFormsViewModel.cs`, `Editor\Views\IcuFormsView.xaml` and `.xaml.cs`,
the nine resx and `UIStrings.cs`, `multifarious.Icu.Tests\BatchTasks\IcuFormsViewTests.cs`,
`tools\check-forms-scroll.ps1`, handover 12 (a line trimmed) and this handover.  Not
`tools\plan-file.ps1` or the other untracked files, which are Paul's.

```
Show a passed-through unit's warning in the ICU Forms window

A message the expand task passed through carries no context of the
plugin's, and Studio segments its raw value, so the window called it
ordinary text. It now reads the task's own comment off the unit and
shows the reason with the setting that answers it, the branch budget
or the parse behaviour. An ordinary segment's note names the unit's
key where the filter put one on the unit. The active row is kept in
view: when the editor moves to a row outside the visible rows it is
centred, and a row already in view is left where it is.

Six new strings in nine languages, four new tests (1,666), and
tools\check-forms-scroll.ps1, which proves the scrolling offscreen.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
```

## The second piece: the same-argument collapse (evening)

Paul's Studio run of the window build showed `Default['LOCAL_CONTROL_CONFIRMATION.1']` at
a hundred Italian forms, the Form column reading `L / I`, every Source cell the same German
sentence.  The value has two `{deviceTypeArticle, select, ...}` in one sentence; hoisting
nested the second under every branch of the first and the planner expanded both to Italian's
ten article keys.  The argument has one value at run time, so only the ten rows whose keys
agree can ever be rendered; the other ninety were segments to fill for nothing, and a third
such select would have been a thousand.  Agreed as proposed, four parts:

- **Hoister** (`Icu.Core\Hoister.cs`).  `HoistSequence` carries a list of `Binding` records
  (type, argument, offset, the branch key, the outer's sibling keys) down the recursion
  beside the enclosing plural.  Before a selector is hoisted, `Collapse` looks for a binding
  of the same type and argument (and offset, for a plural kind).  Under a named key the
  selector becomes its branch of that key, or `other`; under the outer's `other` the value
  matches none of the outer's named keys, so those branches of the inner are dropped and the
  rest stands, whole or as a smaller selector.  A plural kind collapses only when the bound
  key is a keyword and the inner has no explicit `=n` key; anything else nests as before,
  since a number resolves to a category only with CLDR.  A `#` the collapse moves into
  another plural's scope (a plural on another argument between the two) is rewritten to its
  own argument through the existing `RewritePounds`; with a non-zero offset the selector is
  left nested rather than refused.  Every reduction is exact, so the three T1 properties
  (idempotent, argument set, render equality over five locales) pass unchanged.
- **Tests**: five directed tests in `HoisterTests` (the collapse, the `other` rule keeping
  the keys the outer cannot have ruled out, a plural by category, explicit keys left nested,
  the `#` rewrite through another plural).
- **Window summary** (`IcuFormsViewModel.SelectorsOf`, `Forms_SummaryBy` in nine files):
  "Arabic (Saudi Arabia), 12 form(s): g 2 × count 6", read from the rows' paths and shown
  only when every row goes the same depth through the same arguments and the product is
  the row count.  One test in `IcuFormsViewTests`.
- **Records**: `CLAUDE.md` under "Decisions taken (25 October 2026, release/2.1)"; the
  reference project's `Icu.Core\Hoister.cs`, `HoisterTests.cs` and design section 5.9 are
  Paul's to update from here.

Checked with `tools\plan-file.ps1` on the test file (`-Targets @('en-GB','fr-FR','it-IT')`
as an array, see Traps): `LOCAL_CONTROL_CONFIRMATION.1` 10 Italian and 4 French forms (were
100 and 16); `RESULT_ON_DEVICETYPE.1` 50 Italian and 20 French (were 3,110 and 212), still
over budget for Italian; `DEVICE_NEEDS_TIME.1` unchanged at 525 and 225, its selectors being
on different arguments.  1,672 tests, deployed at 22:13.

### Proposed second commit

Stage: `Icu.Core\Hoister.cs`, `Editor\ViewModels\IcuFormsViewModel.cs`, the nine resx and
`UIStrings.cs`, `multifarious.Icu.Tests\Core\HoisterTests.cs`,
`multifarious.Icu.Tests\BatchTasks\IcuFormsViewTests.cs`, this handover.

```
Collapse a selector on an argument already bound above it

Two selects on the same argument in one sentence were hoisted as every
combination, a hundred Italian forms of which ninety could never be
rendered, since the argument has one value. Hoisting now carries the
keys bound on the path: a select on a bound argument resolves to that
key's branch or other, and under the outer's other keeps only the keys
the outer has not got. A plural kind collapses the same way by category
when both sides are keywords; an explicit key leaves it nested. Every
reduction is exact and the hoisting properties pass unchanged.

The ICU Forms summary line says where nested forms come from, such as
"g 2 x count 6", when the product is the row count. One new string in
nine languages, six new tests (1,672).

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
```

## The third piece: the header and the walked wording (late evening)

Paul's run of the second build showed `DeviceState['DEVICE_NEEDS_TIME.1']` for French, 36
forms, with the header ten lines tall and the summary running off the right edge.  Two
causes, one layout, one wording.  The header was a DockPanel: the summary does not wrap, so
a long one took the whole width, and the status docked right was left no width and wrapped
one character per line.  And the message was laid out walked (its categorical expansion is
225 forms, over the project's budget), so the rows were the source's `=0`, `=1` and `other`
under every gender including NEUTER, with nothing in the window to say why.

- **Header as a Grid** (`IcuFormsView.xaml`): key, summary in a star column with
  `TextWrapping="Wrap"`, status at its own width on the right.  `check-forms-scroll.ps1`
  still passes, so the XAML loads and the rows still scroll.
- **Walked wording** (`IcuFormsViewModel.Show`, `IcuFormsModel.HasPluralKind`): a plural kind
  on any row's path with `icu:expandedSelectors` empty reads "French (France), 36 form(s)
  laid out as written: deviceTypeGender 4 × durationHoursPlural 3 × durationMinutesPlural 3".
  Not `CountArgument`, which is null when the outermost selector is a select, exactly Paul's
  case; the first test run caught that.  Two strings, `Forms_SummaryWalked` and
  `Forms_SummaryWalkedBy`, where the plan said one: the product variant needs its own so the
  translations stay whole.  One test, two cases.  1,673.

### Proposed third commit

Stage: `Editor\Views\IcuFormsView.xaml`, `Editor\ViewModels\IcuFormsViewModel.cs`,
`Services\IcuFormsReader.cs`, the nine resx and `UIStrings.cs`,
`multifarious.Icu.Tests\BatchTasks\IcuFormsViewTests.cs`, this handover.

```
Wrap the ICU Forms header and say when a message was laid out as written

The header was a DockPanel: a long summary took the whole width and
left the status no room, so it wrapped one character per line and the
header grew to ten lines. It is now a Grid with the summary wrapping.

A message over the branch budget is laid out with the source's own
branches and nothing category expanded, and the rows showed =0, =1 and
other under every gender with no word on why. The summary now says
"laid out as written" when a plural kind is on the path and nothing
was expanded. Two new strings in nine languages, one test (1,673).

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
```

## The fourth piece: the embedded content run and the restored state (night)

Paul ran the test file through the JSON filter with the HTML embedded content processor
(Project 83, Italian).  Three screenshots and the saved files:

- **As shipped, the HTML filter splits SSML values.**  `<speak>` and `<phoneme>` are not HTML,
  so the filter took them as structure and `ACTIVATE_REMOTE_CONTROL_ON_DGBM.1` arrived as
  several units ("Mobile" and "Start" segments of their own); no piece parsed, nothing
  expanded, the window called every segment ordinary text.
- **Defined as inline elements in the HTML file type, the route works end to end.**  One unit,
  two forms, the pairs around "Mobile" and "Start" with the IPA inside the tags; Restore put
  the source back as written with the tags and the target hoisted with the pairs cloned into
  both branches; a second Expand brought the forms back with the tags and the filled targets.
  The saved target JSON has the SSML as written, attributes, quotes and order intact, and the
  article select collapsed to Italian's ten keys.  A new test,
  `Fill_comments_on_a_tagged_message_come_back_on_every_form_after_a_second_expand`,
  reproduces the shape and shows the fill comments wrapping each form's pair after the
  second Expand.  Segment 59's target showed no comment highlight in Studio, most likely
  because the marker wraps the pair and the text sits inside it, which Studio does not paint;
  the Comments window on the next Expand settles it.
- **Two things are the HTML filter's.**  It trimmed the spaces inside the element ends on
  reading (`<speak> Damit … Symbol. </speak>` to `<speak>Damit … Symbol.</speak>`; the
  sub-content's whitespace handling is `NormalizeWithXmlSpaceOverride` and `icu:pattern`
  already lacks them).  And its writer emitted named entities for every non-ASCII character
  in text, 335 in the file (`&auml;` 155, `&uuml;` 91, `&ouml;` 79, `&bdquo;`, `&ldquo;`),
  while attribute values kept their characters.  No JSON or SSML consumer decodes those, so
  the route needs the HTML file type's entity setting to write characters.  Not verified yet.
- **The restored state** (`IcuFormsViewModel.ShowRestored`, `IcuFormsViewPartController`).
  On the restored file the window said "No ICU messages expanded in this file" and told the
  translator to expand before translating.  A restored unit now reads "ICU message restored"
  with the key, `icu:key` where the unit has none of its own (the structure path under the
  processor), and says Expand lays the forms out again with the recorded translations and
  states; the survey notes restored units so a restored-only file is not gated.  Three
  strings (title, note, keyed note) where the plan said two.  One test.  1,675.

### Proposed fourth commit

Stage: `Editor\IcuFormsViewPartController.cs`, `Editor\ViewModels\IcuFormsViewModel.cs`, the
nine resx and `UIStrings.cs`, `multifarious.Icu.Tests\BatchTasks\IcuFormsViewTests.cs`,
`multifarious.Icu.Tests\BatchTasks\FilterTagTests.cs`, this handover.

```
Give the ICU Forms window a restored state

A restored unit is one segment per side with the plugin's context
saying so. The window called it ordinary text, and a file with only
restored units was told to run Expand ICU before translating. A
restored unit now reads "ICU message restored" with its key, the
recorded one where the unit carries none, and says Expand lays the
forms out again with the translations and states Restore recorded.

Also a test for the SSML shape from Project 83: a select inside a
speak pair with phoneme pairs after it, expanded, restored with every
form filled from the source, and expanded again, the fill comments
back on each form. Three new strings in nine languages, two tests
(1,675).

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
```

## Open items

- **The HTML writer's entities.**  Find the HTML file type's entity setting, set it to write
  characters, save the target again and check the JSON for `&auml;` and the rest.  Until
  then the inline-element route is not fit for SSML output.
- **Documentation of the SSML routes** (AppStore `Documentation.md`, the user guide): the
  standard filter keeps the message whole with the markup as text; the HTML processor
  needs `speak` and `phoneme` inline and characters written as characters, and trims the
  whitespace at the element ends.  After the entity check.
- **Studio run of the restored state and the collapse**: a restored file for the new state
  and its key under the processor; Italian `LOCAL_CONTROL_CONFIRMATION.1` (ten rows, no
  `L / I`); French `DEVICE_NEEDS_TIME.1` for the header and the "laid out as written"
  wording; the Comments window on segment 59 after an Expand.
- **Copy the hoister back to the reference project** (`Icu.Core\Hoister.cs`, the five
  tests) and add the collapse to design section 5.9.  Paul's, the project is read only here.
- The website screenshots and the HTML guide (handover 12).
- The sixty strings added since 14 October need a native read with the rest.
- The Options tab's sub-headings reuse `Expand_SeedHeader` and `Expand_PlaceholdersHeader`;
  shorten there if a native reader finds them long.
- Hints, samples, needs, narrowing and renames are read-only on the Grammar dictionary page;
  making them editable means extending the developer's file format, a separate decision.
- `tools\plan-file.ps1` is still uncommitted, Paul's call.
- The version and changelog for 2.1 when Paul decides what else goes in it.
- R11 of the Studio test plan waits on the verifier fault recurring.
- The open items from handovers 05 to 11 stand (Catalan's article set, `countAsSelect`).

## Traps

- All earlier traps stand (handovers 01 to 12).
- A PowerShell function that writes a line and then returns a value returns both: the caller
  gets an array and every comparison on it is silently true.  Write the line with
  `Write-Host` and return the value alone.
- `MethodInfo.Invoke($null, @($collection))` unrolls the collection into the argument list;
  build an `object[]` of one element by hand.
- An optional parameter is not optional through `Activator.CreateInstance`; pass `$null`.
- A view hosted in a window too short for its fixed parts gets a zero-height ScrollViewer,
  and every row is then "out of view"; print the viewport before trusting the offsets.
- The LF/CRLF warning on a file written by the Write tool is about the next checkout, not
  the diff; the diff is clean.
- `powershell -File tools\plan-file.ps1 -Targets en-GB,fr-FR` hands the script one string,
  "en-GB,fr-FR", which plans as an unknown language with `other` alone and looks like every
  target agreeing.  Call it through `-Command` with `-Targets @('en-GB','fr-FR')`.
