# Browser verification — observe behaviors, not DOM

Step 3 of the workflow. The artifact draft is what we want; the running app is what we get. Verify each claim, correct mismatches, log changes.

## Three principles

### 1. Locate by behavior, not structure

Find controls the way a user would. In order of preference:

1. **Visible label text** — the text adjacent to or inside the control (`Country`, `Save`, `Activities Assistance`).
2. **Surrounding context** — *"the dropdown directly below the City label"*, *"the button on the bottom-right of the modal"*.
3. **Action description** — *"the link that opens the help drawer"*.

ARIA role + accessible name is *fine when present*. Legacy MVC widgets routinely skip semantics: Syncfusion configurations, jQuery widgets, and hand-rolled wrappers often produce DOM that fails accessibility audits. Don't depend on `role="combobox"` existing because the visible widget looks like a combobox. If your locator fails because the widget lacks semantics, fall back to visible text and surrounding context, AND **flag the missing semantics as a requirement on the modern rewrite** in the artifact's `## Edge cases`.

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

A claim verified on only one half is half-verified. Capture both — even when one is null. *"No network call fires"* is a valid assertion for pure show/hide reactivity, and the absence is part of the contract.

### 3. Allow settle windows

State mutations driven by SignalR / SSE / polling don't arrive synchronously with the triggering action. After an action that triggers a push update:

1. Confirm the AJAX response (immediate).
2. Wait the documented settle window (commonly 1–3 s for SignalR-driven counters).
3. Re-check the affected region.

A claim that fails immediate verification but passes after settle is **a settle-window-dependent claim** — record the window in the artifact: `settle_ms: 2000` on the reactivity entry. The downstream rewrite must reproduce the timing semantics, not just the eventual state.

## Network capture timing

The Claude-in-Chrome `read_network_requests` tool starts capturing only when first called. Pages loaded *before* the call have their requests hidden from the tool.

Always:

1. **Arm capture early** — call `read_network_requests` once (even with empty result) at the start of a verification session.
2. **For initial-load behavior** — refresh the page after arming.
3. **Before every triggering action** — re-call to clear noise from prior actions.
4. **Filter by URL pattern** when the page is busy — scope by the documented endpoint URL substring (`urlPattern: "/Address/States"`).
5. **For SignalR/WebSocket** — they appear as a single OPTIONS+CONNECT pair, then frames don't show in REST-style capture. The presence of a `signalr.net` or `/signalr/` URL in network confirms a Hub connection; the actual frames need a different observation strategy (often: rely on the visible mutation as your evidence, with the network confirming connection state).

## Tools

Either pattern works; both are semantic.

### Claude-in-Chrome MCP (`mcp__Claude_in_Chrome__*`)

Native fit when this skill runs from a Claude Code session.

| Tool | Use |
|---|---|
| `tabs_context_mcp` / `tabs_create_mcp` / `navigate` | Open the running app. |
| `find` | Natural-language locator: *"the Reason for Leave dropdown"*. Returns a `ref_<n>` for use with `computer`. Behavior-friendly — matches what the user calls things. |
| `read_page` (filter: `interactive`) | Accessibility tree (filterable). Useful for enumerating dropdown options and reading visible labels — works even when roles are partial. |
| `computer` | Drive interactions: `left_click`, `type`, `key`, `scroll`, `screenshot`. |
| `read_network_requests` | The "network half" of every claim. |
| `read_console_messages` | JS errors that mask real failures. |
| `browser_batch` | Combine multiple actions in one round-trip — significantly faster for predictable sequences (click → type → submit → screenshot → read_network_requests). |

**Use `browser_batch` aggressively.** Any predictable multi-step sequence should be batched: it's one model↔runtime round trip, preserves causality, and reduces flakiness.

### Playwright

Same principles, scriptable for CI:

```javascript
const country = page.getByLabel(/country/i);                                   // visible label
const responsePromise = page.waitForResponse(r => r.url().includes('/Address/States'));
await country.selectOption({ label: 'Germany' });
const response = await responsePromise;                                        // network half
const json = await response.json();
const stateOptions = await page.getByLabel(/state/i).locator('option').allTextContents();
```

`getByLabel` / `getByText` / `getByPlaceholder` are the semantic anchors. `getByRole` is fine when present — but have a non-role fallback ready when the legacy widget skips ARIA.

## Per-claim flow

For each entry in the artifact's `## Verification claims`:

1. Load the page where the slice lives. The user supplies the URL.
2. Locate the slice by visible text or surrounding context (no DOM ids).
3. Arm `read_network_requests`.
4. If the claim involves a network call, set up the network promise/listener **before** triggering.
5. Trigger the user-facing event (click, type, blur, submit).
6. Observe the **immediate** half (visible + network).
7. If the artifact declares a settle window for this claim, wait that window, then re-observe.
8. Compare to the claim text.

Outcomes:

- **Verified** — note in `## Verification log`.
- **Mismatch** — update the claim or relevant frontmatter; log the change; re-verify.
- **Untestable** — legacy app doesn't expose what the artifact claimed (e.g. accessible name missing). Don't drop. Reframe as *a requirement on the rewrite* and note in `## Edge cases`.

## What to observe per behavior category

These are illustrations tied to [`behavior-taxonomy.md`](behavior-taxonomy.md). Adapt to actual claims.

### Population

- Open the dropdown / view the region; read visible options.
- Hit the documented endpoint directly to learn expected count and shape.
- Compare counts and labels.
- For pre-filled defaults: reload the page; verify the field's initial value (today's date, current user, last selection).

### State change — visual-only / local mutation

- Trigger the action.
- Confirm the visible change (button highlight, link toggled, count incremented locally).
- Confirm `read_network_requests` shows **no new request** for the trigger.
- Note any local indicator that something is "queued" or "dirty" — that's part of the contract.

### State change — immediate AJAX

- Set up network promise filtering by documented URL.
- Trigger.
- Confirm method + payload of the request.
- Confirm response status and shape.
- Re-read the affected region; compare to claim.

### State change — deferred batch

- Trigger the queueing action multiple times; confirm `no network` per click and a queue indicator (commit count, "1 unsaved change") advances.
- Trigger the commit action; confirm a single network request with the batched payload.
- Re-read the region after the commit response.

### State change — server push (SignalR)

- Confirm the immediate half (POST returns 200, often with no UI mutation in the response body).
- Wait the documented settle window.
- Re-read the affected region.
- If the change appears within window: pass. If not: extend window, re-test; if still no change: fail; either the settle window in the artifact is too short or the push isn't firing (worth investigating).

### Validation — required

- Submit empty.
- Match the inline message text **exactly** (the message is part of the contract).
- Confirm no network request to the form action.
- Confirm visual error state (whatever the project's convention is — pink background, red border, asterisk turning red, etc.).

### Validation — format / range / regex / email

- Type invalid; tab out; assert message + state.
- Type valid; assert clear.

### Validation — async / remote

- Type input designed to fail the remote rule.
- Network: assert request fires to documented endpoint with documented payload.
- Visible: assert message appears after the response.
- Type input that passes; assert message clears.

### Validation — cross-field

- Set the trigger condition; submit.
- Assert the dependent rule fires with the documented message.
- Set the condition the other way; submit; assert the dependent rule does NOT fire.

### Navigation — page

- Trigger; confirm URL changes to documented target.

### Navigation — modal

- Trigger; confirm modal appears (a region of new content visible above the page).
- Confirm URL did NOT change.
- If `loaded_via_ajax: true`: arm network; trigger; confirm GET to documented URL with `X-Requested-With=XMLHttpRequest`.
- Test each dismiss path the artifact promises (close icon, escape, backdrop click, cancel button).

### Navigation — drawer

- Trigger; confirm drawer appears (typically off-canvas).
- Confirm URL change pattern if documented (`/Pane`-style suffix).
- Test focus management if claimed.

### Navigation — wizard step

- Confirm step indicator shows current step highlighted, visited steps marked done, future steps locked.
- Try advancing without filling required fields; confirm gating message and lack of advance.
- Fill required; advance; confirm step indicator updates.
- Try going back; confirm previous step's state is preserved.

### Reactivity — cascading dropdown

- Capture network promise filtered by documented URL.
- Trigger change on parent.
- Wait response; re-read child's options; compare to response payload.

### Reactivity — show / hide

- Capture network during the action; assert no relevant request.
- Confirm dependent area's visibility flipped.

### Reactivity — conditionally-present slice

- Set up state per the artifact's `presence_condition`.
- Confirm the slice **becomes** visible (locate it via `find` — it should now be present where it wasn't before).
- Reverse the state; confirm the slice disappears entirely (not just hidden).

### Reactivity — counter / live aggregate

- For each claimed source: trigger that source; confirm the counter responds (immediate or with settle window per the artifact).
- This often involves multiple test rounds — one per trigger source.

### Filter / sort / page

- For each dimension separately, capture a network promise (filtered by URL pattern), trigger, confirm request payload includes the dimension parameter, re-read visible rows.
- Combined: trigger filter; trigger sort; trigger paging; assert each request and the resulting visible rows match the cumulative state.
- **Test reset rules**: change a filter; confirm page resets to 1 (or doesn't, per the artifact).
- **Test URL persistence**: refresh the page mid-state; confirm the state survives the reload (or doesn't).

### Submission — multi-action

- For each action button: trigger; confirm the documented post-action flow (which page, which toast, which slice refreshes).
- The actions usually have the same endpoint but different `redirect_to` or post-success behavior.

### Toast / notification

- Trigger; confirm text appears in documented region (top-right, bottom-right, banner).
- Confirm verbatim message text.
- Wait timeout; confirm dismiss.
- Where dismissable: click dismiss; confirm immediate disappearance.

### Loading indicator

- Trigger an action that hits a slow endpoint.
- Within the load window, confirm indicator visible.
- After response, confirm indicator hidden.
- If `blocks_interaction: true`, attempt another action during load; confirm it's blocked.

## Verifying advanced behaviors

The advanced categories in the taxonomy (composite state, drag-drop, concurrent editing, long-running ops, export, multimodal input, audit timelines, workflows) are harder to verify automatically because they often involve multiple actors, time, or non-DOM input. Treat each as a multi-claim cluster.

### Composite / derived state

- Change one input the derivation depends on; confirm the derived display recalculates.
- Change the input again; confirm recalc.
- For server-side derivations: confirm the recalc happens after a re-render or AJAX, not instantly.

### Drag-drop / reordering

- Confirm draggable affordance (cursor change, drag handle visible).
- Drag to a valid target; confirm drop succeeds and visible state updates.
- Drag to an invalid target; confirm the documented invalid-drop behavior (snap back / error / no-op).
- Capture the network call on drop (the persistence half).

### Concurrent editing

- Two-session test: open the record in two browser sessions (or use private browsing for the second).
- Edit and save in session A.
- Try to save the same record from session B without reloading; assert the documented conflict prompt appears.
- Test each documented resolution path (overwrite, reload, merge).

If a two-session test isn't feasible, simulate a stale ETag / concurrency-token in the request and assert the conflict response.

### Long-running operations

- Trigger the job; confirm the documented submitted-state UI (toast, queue entry, etc.).
- If polling: capture the polling network calls and their interval; confirm UI updates each poll.
- If SignalR-driven: confirm connection, then verify the visible progress indicator advances over time.
- Simulate failure: hit an endpoint that returns failure (or trigger a known-failing condition); confirm the failure UI matches the documented state.

### Export / print

- Trigger export; confirm the format chosen (or the default).
- For synchronous downloads: assert `Content-Disposition: attachment` in the response and the documented filename pattern.
- For queued exports: verify the documented status indicator appears, then completion notification fires when the job finishes.

### Multimodal input

- For voice / signature / camera: these often require user-grant permissions (mic, camera). The skill cannot grant them automatically — document the requirement and verify when permissions are granted.
- For drag-drop file upload: simulate drop programmatically; confirm preview and submit flow.

### Activity timelines / comments

- Create a known event (edit a field, add a comment); reload the timeline; confirm the new entry appears.
- Test filters (by user, by date) with predictable test data; confirm filtered results match.
- For mentions: trigger a mention; confirm notification fires (separate verification).

### Workflow / state machines

- For each documented transition: set up the prerequisite state; trigger the transition; confirm the post-state UI (badge, status field, allowed actions).
- For role-gated transitions: switch to a user without the role; confirm the transition is hidden or disabled.
- For required-field transitions: try without the required field; confirm the documented blocking message.
- Verify side effects: notification sent, audit entry written, downstream record created — each is a separate sub-claim.

## Verification log entries

Format suggestion (each line = one change):

```
- 2026-05-02 — claim 3 verified.
- 2026-05-02 — claim 5 corrected: payload also includes `cultureCode` from a hidden input.
- 2026-05-02 — claim 7 reframed: legacy lacks accessible name on State dropdown; rewrite must add `aria-label="State"`.
- 2026-05-02 — claim 9 corrected: SignalR-driven counter settle window is ~2.5 s, not "immediate" — recorded `settle_ms: 2500`.
- 2026-05-02 — claim 12 added: filtering resets page to 1 (observed; not in original draft).
```

Keep entries terse. They are for the human reviewer auditing how the artifact got to its current state. The downstream LLM does not read the verification log.

## Privacy

The running app shows real data. **Generalize observed values everywhere in the artifact**, including the verification log. The Senior-Living domain is regulated; resident names, room numbers, dates of birth, medication entries, and tenant names are PHI / PII. The artifact is a behavioral contract, not a data dump.

- *"The dropdown contains the residents currently on premise"* — not *"contains Lily Avelar, Frankie Blake, James Bond, …"*.
- *"The toast confirms the action with text matching the artifact's claim"* — paraphrase if any verbatim leak would expose specifics.
- Verification log entries: *"2026-05-02 — claim 7 verified: only granted communities appear in the option list (tested with Community-A grant absent)"* — synthetic labels, not real names.

Reject everywhere in the artifact (frontmatter, claims, edge-case prose, AND verification log): tenant names, tenant ids, resident names, room numbers, emails, dates of birth, phone numbers, addresses, medical record numbers, hostnames, any identifier that ties an artifact to a single real tenant or person. Use synthetic labels (`Community-A`, `Resident-1`, `Date-X`) when a worked example is necessary.

If the running app is a production tenant, ask the user whether they prefer pointing the skill at a staging environment. Production observation works; staging is safer. If only production is available, every value typed into the artifact must be a synthetic label even if the data on screen is real.

## What this file is not

A complete UI testing methodology. The skill targets behavioral artifacts for modernization; rigorous regression testing is layered on top of the verified artifact, not embedded here.
