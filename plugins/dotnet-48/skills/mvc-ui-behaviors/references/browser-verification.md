# Browser verification — observable behavior, not DOM

Step 2 of the workflow. The artifact from step 1 is a draft; the running app is the ultimate truth. This file is the playbook for confirming each claim, correcting mismatches, and logging what changed.

## Three principles

### 1. Locate by behavior, not structure

Find controls the way a user would. In order of preference:

1. **Visible label text** — the text adjacent to or inside the control (`Country`, `Email`, `Save`).
2. **Surrounding context** — *"the dropdown immediately below the City field"*, *"the button at the bottom-right of the form"*.
3. **Action description** — *"the link that opens the help drawer"*, *"the button that submits the form"*.

ARIA `role` + accessible name is fine *when present*, but **legacy MVC widgets routinely don't follow HTML semantic conventions** — Syncfusion configurations, jQuery widgets, and hand-rolled wrappers often produce DOM that fails accessibility audits. Don't assume `role="combobox"` exists just because something looks like a dropdown; if your locator depends on roles and the locator fails, that means the legacy widget lacks semantics. Fall back to visible-text or context, and **flag the missing semantics as a requirement on the modern rewrite** in the artifact's `## Edge cases`.

Do **not** use:
- DOM ids
- CSS class chains (`.form-control.input-sm.has-error`)
- Tag-name selectors (`select`, `input[type=text]`)
- nth-child positions

These will all change in the modern rewrite. They are not part of the contract.

### 2. Observe both halves

Every behavior claim has two halves:
- **Visible** — what the user sees change (a message appears, options re-populate, a region's content swaps, a panel opens, a control becomes enabled).
- **Network** — what the server is asked (URL, method, request payload shape, response shape).

A claim verified on only one half is half-verified. Capture both — even when one side is null. *"No network call fires"* is a valid assertion for pure show/hide reactivity, and the absence is part of the contract.

### 3. The browser is the ultimate truth

When the artifact and the browser disagree, the browser wins — because the running app is what the user actually experiences. Update the artifact, not your interpretation of the code. If the browser shows behavior the code obviously didn't predict, suspect:
- A script you didn't find (often inline in a partial, or in a bundle config you didn't follow).
- A custom helper that adds defaults invisibly — trace into the helper.
- A stale code path the app no longer hits — the artifact reflects *running* behavior, not dead code.

## Tools

Either pattern works; both are semantic. Pick whichever is at hand.

### Claude-in-Chrome MCP (`mcp__Claude_in_Chrome__*`)

Native fit when this skill runs from a Claude Code session.

| Tool | Use |
|---|---|
| `tabs_context_mcp` / `tabs_create_mcp` / `navigate` | Open and load the running app. |
| `find` | Natural-language locator: *"the country dropdown"*, *"the Save button"*. Returns a `ref_<n>` you can pass to `computer` for clicks. Behavior-friendly — matches what the user calls things. |
| `read_page` | Accessibility tree (filterable to interactive elements). Useful for enumerating dropdown options and reading visible labels — works even when roles are partial. |
| `computer` | Drive interactions: `left_click`, `type`, `key`, `scroll`. |
| `read_network_requests` | Capture XHR/fetch traffic. Filter by URL pattern; assert method, request body, response shape. The "network half" of every claim. |
| `read_console_messages` | Catch JS errors that would otherwise mask real failures. |
| `javascript_tool` | Last resort for reading state when the accessibility tree doesn't expose it. Used to inspect, not to drive. |

### Playwright

Same principle, scriptable for CI.

```javascript
// Visible-label locator (does not depend on role).
const country = page.getByLabel(/country/i);

// Network promise BEFORE the trigger.
const responsePromise = page.waitForResponse(r => r.url().includes('/Address/StatesForCountry'));
await country.selectOption({ label: 'Germany' });
const response = await responsePromise;
const json = await response.json();

// Visible half: re-read the dependent control.
const stateOptions = await page.getByLabel(/state/i).locator('option').allTextContents();
```

`getByLabel` / `getByText` / `getByPlaceholder` are the semantic anchors. `getByRole` is fine when it works, but have a non-role locator ready when the legacy widget skips ARIA.

## Per-claim flow

For each entry in the artifact's `## Verification claims`:

1. Load the page where the slice lives. The user supplies the URL when invoking the skill.
2. Locate the slice by visible text or surrounding context (no DOM ids).
3. If the claim involves a network call, set up the network promise/listener **before** triggering.
4. Trigger the user-facing event (click, type, blur, submit).
5. Observe both halves: the visible change, and the network call (or its absence).
6. Compare to the claim text.

Outcomes:

- **Verified** — note the claim and the date in the artifact's `## Verification log`.
- **Mismatch** — update the claim text or the relevant frontmatter field; log the change; re-verify.
- **Untestable** — the legacy app doesn't expose what the artifact claimed (e.g. a control has no accessible name, or a fragment has no observable indicator). Don't drop the claim. Reframe it as *a requirement on the modern rewrite* (e.g. *"the country dropdown must expose its label as the accessible name"*) and note in `## Edge cases` that the legacy fails this.

## What to observe per behavior kind

These are illustrations, not a complete catalog. Adapt to the artifact's actual claims.

**Population** (*"dropdown contains the entries returned by `<endpoint>`"*):
- Open the dropdown (click or focus to expand it).
- Read the visible options and their text.
- Hit the documented endpoint directly to know the expected count.
- Compare counts and labels.

**Validation — required** (*"submit empty shows 'X is required.'"*):
- Submit the form without filling.
- Look for the message text near the affected field.
- Match the message text exactly — wording is part of the contract; paraphrasing breaks UX consistency for users on assistive tech and for translation/localization work.
- Confirm the network does *not* receive the submission request.

**Validation — format / range / regex**:
- Type invalid input. Trigger blur. Assert message text appears.
- Type valid input. Assert message clears.

**Validation — remote** (*"server-checked async rule"*):
- Type input designed to fail the remote check.
- Watch the network: assert request to the documented URL with the documented payload.
- Assert message text appears after the response.
- Type input that passes; confirm the message clears.

**Validation — conditional**:
- Set the trigger condition (e.g. choose `PaymentMethod = Credit card`).
- Re-run the validation step for the dependent rule.
- Set the condition the other way; confirm the dependent rule does *not* fire.

**Reactivity — cascading** (*"change parent, child reloads"*):
- Capture network promise filtered by the documented endpoint URL.
- Trigger change on the parent.
- Assert the request fires with documented method and payload.
- Wait for the response, then re-read the child control.
- Compare child's options to the response payload — count, labels, grouping if any.

**Reactivity — partial replacement** (*"action replaces a region's content"*):
- Identify the target region by *what's around it* (the labeled section, the heading above it, the form it sits inside) — not by DOM id.
- Trigger the action.
- Assert the network call.
- Confirm the region's visible content changed (new text, new control structure, fewer/more entries).

**Reactivity — show / hide** (no network):
- Capture network during the action; assert no relevant request fires.
- Confirm the dependent area's visible state flipped (visible/invisible, enabled/disabled).

**Modal / drawer** (*"button opens overlay"*):
- Trigger the opener.
- Confirm the overlay is visible above the rest of the page (an off-canvas region with content visible).
- Test each dismiss path the artifact promises (close button, escape key, backdrop click).
- Verify focus management if the artifact claims it (focus trapped inside, returned on dismiss).

**Toast / notification**:
- Trigger the action that emits the toast.
- Assert the toast text appears (in the position the artifact claims — corner, banner, etc.).
- Wait for the documented timeout; confirm it disappears.
- Where dismissable, click dismiss; confirm immediate disappearance.

**Grid — sorting / paging / filtering**:
- For server-side: capture network on each control. Trigger sort by a column; assert request includes the sort parameter; assert the visible row order changed.
- For paging: trigger next page; assert request includes page number; assert visible rows changed.
- For filtering: type into the column filter; assert request includes the filter; assert visible rows match.
- For client-side: same observable assertions, but without expecting network calls.

When a behavior doesn't fit any of the above, decompose it into "visible change" + "network call" + "trigger" — those three primitives describe nearly everything observable.

## Verification log entries

Format suggestion (each line one change):

```
- 2026-05-02 — claim 3 corrected: payload also includes `cultureCode` from a hidden input; static analysis missed it.
- 2026-05-02 — claim 5 reframed: legacy lacks accessible name on the State dropdown; rewrite must add `aria-label="State"`.
- 2026-05-02 — claim 7 verified.
```

Keep entries terse. They are for the human reviewer auditing how the artifact got to its current state.

## What this file is not

A complete UI testing methodology. The skill targets behavioral artifacts for modernization; it doesn't replace QA. If rigorous regression testing is needed, layer it on top of the verified artifact — the artifact's claims are good test seeds.
