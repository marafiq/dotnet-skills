---
# Identity
id: <kebab-case slug, e.g. checkout-country-dropdown>
title: <human-readable name, e.g. "Checkout — Country Dropdown">
view: <path/to/View.cshtml>
view_lines: <e.g. 42-78, optional>
control_type: <e.g. dropdown | grid | form | modal | datepicker | autocomplete | toast | tab_set | accordion | side_menu | drawer | etc. — pick whichever fits; add new types when needed>

# What model property the control binds to (if any)
binding:
  view_model: <view-model class>
  property: <property name>

# How the control's data is populated
data_source:
  kind: <model_property | viewbag | viewdata | helper | hardcoded | api | dynamic_via_parent>
  reference: <e.g. "ViewBag.Countries", "Model.OrderItems", "Url.Action('CountryData')">
  populated_by: <controller action or helper that produces the data>
  fields:                          # for list-like data
    value: <field name used as option value>
    text: <field name shown to the user>
  group_by: <field name if grouped, else null>
  example_payload: <optional, kept short>

# What the user sees. Keys are control-type-specific — populate what fits, drop the rest.
configuration:
  # Common across many control types:
  placeholder: <e.g. "-- Select --" | null>
  default_value: <typed value or null>
  read_only: <true | false>
  disabled_when: <prose condition or null>

  # Examples of type-specific keys (use whichever apply, add others as needed):
  # For dropdowns:
  #   multi_select: false
  #   searchable: false
  #   grouped: false
  # For grids:
  #   paging: { server_side: true, page_size: 25 }
  #   sorting: { enabled: true, sortable_columns: [...] }
  #   filtering: { enabled: true, global_search: false }
  #   selection: { mode: single | multi | none }
  #   row_actions: [edit, delete, ...]
  # For date pickers:
  #   format: "yyyy-MM-dd"
  #   min: <date or "today">
  #   max: <date or null>
  # For modals / drawers:
  #   size: <small | medium | large>
  #   closable: { close_icon: true, escape: true, backdrop_click: false }
  # For wizards / tabbed forms:
  #   layout: <tabs | wizard>
  #   steps_or_tabs: [...]
  # …and so on. The schema is illustrative; populate what describes the slice.

# Validation rules that fire on this slice
validation:
  - rule: <required | string_length | range | regex | compare | email | remote | custom>
    parameters: <e.g. { min: 1, max: 100 } | null>
    trigger: <client | server | client+server>
    message: <error message text the user sees, verbatim>
    conditional_on: <prose condition or null>

# What happens when the user interacts with this slice
reactivity:
  - event: <change | click | focus | blur | submit | load | row_click | row_expand | server_push>
    targets: [<list of related slice ids>]
    action: <reload | hide | show | enable | disable | submit | navigate | replace_partial | open | dismiss>
    endpoint:
      method: <GET | POST | PUT | DELETE | null>
      url: <e.g. "/Address/StatesForCountry" | null>
      request_payload: <prose summary | null>
    response_handling: <prose: what changes in the page after the response>

# Other slices linked to this one
related_controls:
  - id: <slice-id>
    relation: <parent | child | sibling | trigger | target>

# Server endpoints this slice talks to
endpoints:
  - method: <GET | POST | PUT | DELETE>
    url: <route path>
    purpose: <prose>
    requires_anti_forgery: <true | false>
---

# <Title>

## Behavior summary

<2–4 sentences. Lead with the user-visible purpose. Mention key interactions and dependencies. The downstream LLM reads this first.>

## Code references

(For human reviewers cross-checking the artifact. The downstream LLM does not need these.)

- View: `<path>:<line>`
- Model: `<class>.<property>`
- Helper / extension: `<call site>`
- Client script: `<path/to/script.js>:<function>`
- Controller action: `<Controller>.<Action>`

## Edge cases

- <Empty / null data — what does the user see?>
- <Max length, invalid format, out-of-range — what message, what state?>
- <Network failure during AJAX — does the user see an error, a stale state, a retry?>
- <Permission-denied / unauthorized — does the slice hide, disable, show a message?>

## Verification claims

Each claim is a testable assertion. Step 2 of the workflow exercises these against the running app.

1. <e.g. "Initial render: the country dropdown contains the entries returned by `AddressController.GetCountries()`, ordered alphabetically by Name.">
2. <e.g. "Submitting the form with no country selected shows the inline message 'Country is required.' next to the dropdown.">
3. <e.g. "Selecting a country fires a GET to `/Address/StatesForCountry?countryId=<selected>` and replaces the State dropdown's options with the response payload.">
4. <…>

## Verification log

(Updated by step 3 as claims are confirmed or corrected. Format: `<date> — <change>`.)

- <e.g. "2026-05-02 — claim 3 corrected: response also includes culture code; static analysis missed it.">
