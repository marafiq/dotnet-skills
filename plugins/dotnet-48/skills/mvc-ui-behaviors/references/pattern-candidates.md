# Pattern candidates

A registry of patterns, behaviors, or schema fields that have been **proposed** for promotion into the canonical taxonomy or artifact schema. The skill is meant to learn — but only after **facts establish a pattern** *and* a **reviewer (Codex) agrees**. This file is the bookkeeping that prevents one-off observations from quietly mutating the schema.

## How a pattern enters the skill

1. **Observation.** A pattern surfaces in 1+ artifacts via:
   - A repeating `extensions[]` value across artifacts (custom `event` / `action` / `relation` / `control_type`).
   - A repeating `extensions`-block proposal flagged at Codex review.
   - A reviewer raising a gap that several artifacts share.

2. **Candidacy.** The pattern is logged in this file with:
   - `slug` — kebab-case identifier.
   - `kind` — `event` | `action` | `relation` | `control_type` | `field` | `gate_rule`.
   - `evidence_count` — how many distinct artifacts reference the pattern.
   - `evidence_refs` — list of `{artifact_id, location}` pairs.
   - `proposed_by` — Codex round number, or human reviewer.
   - `status` — `candidate` (1 observation), `accepted_for_review` (≥2 observations), `promoted` (sanctioned), `rejected` (with reason).

3. **Promotion.** A candidate is promoted to the canonical schema only after:
   - `evidence_count` ≥ 2 *across distinct slices* (not 2 references in the same artifact).
   - Codex review of the proposed schema change passes.
   - `schema_version` is incremented in `assets/artifact-template.md`.
   - Every existing artifact is re-validated against the new schema.

4. **Rejection.** A candidate may be rejected if Codex review identifies a generalization gap or a clean way to express the behavior under the existing schema.

## Active candidates

(Empty — Round 5 promoted concurrency_conflict + audit_emission cells, regulated_data_handling block, mutates_state field, signal_sources.endpoint_id, cross_slice_refs_pending, schema_version, gate rules 6–12 directly into the schema as part of the round itself. Round 6 was a consolidation pass — no new candidates.)

## Promoted history

Each row is one breaking schema bump. Non-breaking gate-tightening rounds keep the same `schema_version`.

| schema_version | round | additions |
|----------------|-------|-----------|
| 1 | initial | base artifact frontmatter (id, control_type, data_source, business_logic, configuration, validation, reactivity, endpoints, authorization). |
| 2 | round-1 | per-aspect endpoint verification (replaced single `unverified: bool`); structured `extensions[]`; `tenant_boundary` block introduced. |
| 3 | round-2 | structured `failure_matrix` cells `{status, behavior, evidence}`; `tamper_matrix` per endpoint; `contract_status` gate (5 rules). |
| 4 | round-3 | stable `endpoints[].id`; tamper_matrix references via `endpoint_id`; `contract_status_exceptions[]`. |
| 5 | round-4 | gate rule 6 (evidence coherence); `mutates_state` decouples from HTTP method; broadened `tenant_boundary` triggers; `signal_sources[].endpoint_id`; `cross_slice_refs_pending`; gate rule 8 (Mode B unknowns); `concurrency_conflict` + `audit_emission` cells; privacy lint extended to verification log. |
| 6 | round-5 | typed evidence shape (`source_refs` as `{path,symbol}` / `{path,line}` / `{test_id}` / `{artifact,section}`); gate rule 10 (`n/a` coherence); structured `business_logic.selection.{predicates, projection, ordering, paging}` REPLACES `selection.rules`; `regulated_data_handling` block; gate rule 11 (PHI coverage); `schema_version` field + gate rule 12; `pattern-candidates.md` registry; `verification_evidence` block parallel to `verification`. |
| 6 | round-6 | consolidation pass: aligned SKILL.md gate references with template field names (predicates not rules); `contract_status_exceptions` block now explicitly covers gate-critical `n/a` (not only `observed_partial`); template gate-comment block trimmed to a pointer at SKILL.md to prevent rule-count drift; updated reference docs to v6 schema. No new gate rules. |

`schema_version` increments only when the round produces a change that requires existing artifacts to be re-shaped — e.g. round-2 introducing structured failure cells, or round-5 replacing `selection.rules` with structured predicates. Gate-tightening rounds that add rules without breaking existing artifacts keep the version stable (round-6 is one such consolidation under v6).

## How to use this file

- Round-N reviewer (human or Codex): when proposing a new schema field or vocabulary term, add an entry under "Active candidates" with evidence and rationale.
- Promoting reviewer: when promoting a candidate, move its entry to the "Promoted history" table with its `schema_version`.
- Rejecting reviewer: leave the candidate in this file under a `## Rejected` section with the reason; future authors can search before re-proposing.

This file is referenced by `SKILL.md` "Skill evolution discipline" — it is the structural mechanism behind the prose discipline.
