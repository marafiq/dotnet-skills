---
name: mvc-ui-behaviors
description: Use to extract the user-visible *behavior* of a legacy ASP.NET MVC 5.3 view (.cshtml + jQuery Unobtrusive AJAX + Html.* helpers, often with Syncfusion or custom helpers) into a structured Markdown-per-slice artifact a separate LLM session can consume to re-implement the same behavior in modern ASP.NET Core MVC 10. The skill READS code and OBSERVES the running app — it never writes production code. Two-step: code reading produces ~90% of the artifact; the running app is the ultimate truth and corrects the rest. Trigger on phrases like "document this view", "capture the behavior of", "spec out this drawer / modal / form", "modernize this page", "extract MVC 5 UI behavior", "describe the cascading dropdown", or any request to translate a legacy MVC view into a behavioral contract for re-implementation. Applies to the dotnet-48 plugin (.NET Framework 4.8 / MVC 5.3).
---

# mvc-ui-behaviors

Read a legacy ASP.NET MVC 5.3 view, draft a behavior artifact, then exercise the running app to confirm or correct each claim. The artifact is the contract — a separate LLM session uses it to write the modern .NET 10 MVC implementation, without ever reading the legacy code.

## Two steps. Code first, browser second.

1. **Read the code (~90% of the artifact).** Open the `.cshtml`, the view model, the controller action, and any associated `.js`. Claude already knows MVC 5.3 and jQuery Unobtrusive AJAX — they're open-source and well-represented in training. **This skill does not re-teach the framework.** It teaches what to *extract* and what to *exclude*.
2. **Verify in the browser (the remaining 10% — and the corrections).** The running app is the ultimate truth. Static analysis misses behavior that lives in scripts you didn't find, in helpers that override defaults, or in stale code paths the app no longer hits. Browser observation finds these and corrects the artifact.

If you are unsure whether something is a behavior worth capturing, **ask the user**. The user knows the modernization scope; you don't. Don't guess at the boundary of "behavior."

## What's a behavior (and what isn't)

A **behavior** is what the user observes — and the contract the modern app must preserve:

- *"When the user selects a country, the state dropdown reloads with that country's states."*
- *"If the user submits with name empty, the message 'Name is required.' appears next to the field."*
- *"The confirmation dialog appears before delete; if the user cancels, nothing changes; if they confirm, the row vanishes and a 'Customer deleted' toast appears."*
- *"The grid pages 25 rows at a time; sorting by Name posts back; filters on Email and CreatedAt persist across page changes."*

**Not behaviors** (do not include in the artifact):

- DOM ids, CSS class names, jQuery selectors used in legacy code.
- Specific ARIA roles or accessibility attributes — legacy widgets often violate semantics; the modern app may use different markup. (See `references/browser-verification.md` for how to locate without depending on roles.)
- Server-side implementation (which controller method, which EF query, which interceptor).
- The widget library used (Syncfusion vs Kendo vs Bootstrap) — the modern session picks its own.

When in doubt: would the user *notice* if the modern app did this differently? If yes, it's a behavior. If no, it's implementation.

## Artifact: one Markdown file per UI slice

A *slice* is a coherent user-visible unit — a form, a dropdown that drives others, a grid, a modal, a navigation panel. Smaller than a full page, bigger than a single `<input>`. Each slice is one artifact file.

Each artifact has:
1. **YAML frontmatter** — structured fields a downstream LLM can parse predictably.
2. **Markdown body** — prose for what YAML can't capture cleanly (cascading flows, edge cases, observed quirks).

The downstream session is told: *"Implement the behavior in `<artifact.md>` in ASP.NET Core MVC 10. Do not read the legacy code."* Every artifact must stand alone.

See [`assets/artifact-template.md`](assets/artifact-template.md) for the blank template. The schema is **illustrative, not prescriptive** — populate the keys that make sense for the slice's control type, leave the rest off. Add new keys when the slice has behavior the template doesn't anticipate.

## Step 1 — read the code, draft the artifact

For each slice, capture the dimensions that apply:

- **Identity** — what the user calls it, what view file it's in, what model property it binds to.
- **What the user sees** — control type, placeholder, default value, read-only/disabled conditions, conditional visibility.
- **Where its data comes from** — model property, ViewBag/ViewData, an action endpoint, a static list. For lists: value field and text field. For grids: full data round-trip (server-side paging? client-side?).
- **Validation rules that fire** — required, format, range, conditional. Where (client + server, server only). The user-visible message text.
- **Reactivity** — what happens on user actions. Targets (which other slices). Side effects (network call, region replaced, toast, modal opens, control disabled).
- **Cross-slice dependencies** — parent / child / sibling links by slice id.
- **Server endpoints touched** — method, URL, purpose.

You don't need a per-widget pattern catalog. Read MVC 5 code directly, apply the dimensions above. The shape of an extracted behavior is concrete in the worked example below.

If you encounter a widget pattern that confuses you (custom helper layered over a Syncfusion widget, unusual jQuery soup, an inline script you can't trace), **ask the user before guessing**. Getting the artifact wrong wastes both this session and the downstream one.

### Worked example — cascading dropdown + a conditional validator

**Legacy code** (combined view + script + view model, abbreviated):

```cshtml
@Html.DropDownListFor(m => m.CountryId, (SelectList)ViewBag.Countries, "-- Country --",
    new { data_states_url = Url.Action("StatesForCountry") })
@Html.DropDownListFor(m => m.StateId, Enumerable.Empty<SelectListItem>(), "-- State --")

<script>
$('[name=CountryId]').on('change', function () {
    $.getJSON($(this).data('states-url'), { countryId: $(this).val() }, function (data) {
        var $s = $('[name=StateId]').empty().append('<option>-- State --</option>');
        $.each(data, (_, x) => $s.append(`<option value="${x.Id}">${x.Name}</option>`));
    });
});
</script>
```

```csharp
public class CheckoutViewModel : IValidatableObject {
    [Required(ErrorMessage = "Country is required.")] public int? CountryId { get; set; }
    [Required(ErrorMessage = "State is required.")]   public int? StateId   { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx) {
        // States with mandatory ZIP rule
        if ((CountryId == 1 || CountryId == 124) && string.IsNullOrEmpty(Zip))
            yield return new ValidationResult("ZIP is required for this country.", new[] { nameof(Zip) });
    }
    public string Zip { get; set; }
}
```

**Three artifacts** (one per slice):

```yaml
# country-dropdown.md
control_type: dropdown
title: "Country"
data_source:
  kind: viewbag
  reference: "ViewBag.Countries (SelectList)"
  populated_by: "<the action returning this view>"
  fields: { value: Id, text: Name }
configuration:
  placeholder: "-- Country --"
validation:
  - { rule: required, trigger: client+server, message: "Country is required." }
reactivity:
  - event: change
    targets: [state-dropdown]
    action: reload
    endpoint:
      method: GET
      url: "/Address/StatesForCountry"
      request_payload: "{ countryId: <selected> }"
    response_handling: "State dropdown's options are replaced with the response payload; placeholder option is preserved."
related_controls:
  - { id: state-dropdown, relation: child }
```

```yaml
# state-dropdown.md
control_type: dropdown
title: "State"
data_source:
  kind: dynamic_via_parent
  populated_by: "AddressController.StatesForCountry(int countryId)"
  fields: { value: Id, text: Name }
configuration:
  placeholder: "-- State --"
  initial_state: "Empty until a country is selected. Effectively unusable until then."
validation:
  - { rule: required, trigger: client+server, message: "State is required." }
related_controls:
  - { id: country-dropdown, relation: parent }
```

```yaml
# checkout-zip.md
control_type: textbox
title: "ZIP / postal code"
validation:
  - rule: required
    trigger: server
    message: "ZIP is required for this country."
    conditional_on: "Country is one of: United States, Canada"
```

What got captured: data flow, triggers, results, message text, conditions. What got *not* captured: the jQuery selector `[name=CountryId]`, the data-* attribute name, the fact that `IValidatableObject` runs in `Validate()`, the country IDs `1` and `124`. Those are MVC 5 mechanics the downstream session doesn't need.

## Step 2 — verify in the browser

The artifact from step 1 is a draft. Exercise the running app to confirm each claim.

The user supplies the running URL when invoking the skill (e.g. `https://supersecret.alisonline.com/Dashboard`). Don't hard-code app URLs in the skill or in artifacts.

Two non-negotiable principles:

- **Locate by behavior, not by structure.** Find controls the way a user would: by visible label text, by surrounding context, by what the action does. Roles and ARIA names *can* help when the widget exposes them — but legacy MVC widgets often don't, so don't depend on them.
- **Observe both halves.** What the user sees AND what the server is asked. A claim verified on only one half is half-verified.

See [`references/browser-verification.md`](references/browser-verification.md) for the playbook (which MCP tools, what to assert per behavior kind, fallback when widgets break HTML semantics).

## Step 3 — iterate

For each verification mismatch:
1. Update the artifact: correct the claim, add missing edge cases, remove stale claims.
2. Add a line to `## Verification log` at the bottom of the artifact: `<date> — <change>`. The log is for the human reviewer; the downstream LLM ignores it.
3. Re-run verification on changed claims only.

Stop when every claim is **verified** or has been explicitly reframed as *a requirement on the modern rewrite* (e.g. *"the country dropdown's accessible name must be 'Country'"* — the legacy app fails this; the rewrite must succeed).

## Skill non-goals

- **No production code.** Read and observe; do not produce C#, Razor, or JavaScript. The downstream session does that.
- **No tutorial on MVC 5 / Unobtrusive AJAX.** Claude already knows them. Use that knowledge to read the code, then move to the artifact.
- **No DOM ids, no CSS classes, no widget-library names in the artifact's behavioral claims.** Implementation details that the modern rewrite will replace.
- **No comprehensive widget catalog.** The shape of an extraction is the same regardless of widget. When unsure, ask.

## When this skill is the wrong tool

- The view is plain HTML with no MVC helpers — there's nothing legacy-specific to extract.
- The team is staying on MVC 5.3 — no modernization, no contract.
- The slice is trivial (one static text label) — prose in a PR description is enough.

## References

- [`references/browser-verification.md`](references/browser-verification.md) — locating without DOM, observing both UI and network, per-behavior playbook.
- [`assets/artifact-template.md`](assets/artifact-template.md) — blank artifact template (illustrative schema, populate what applies).
