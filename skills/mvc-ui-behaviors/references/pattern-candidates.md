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

(Empty — Rounds 5–7 promoted concurrency_conflict + audit_emission cells, mutates_state field, signal_sources.endpoint_id, cross_slice_refs_pending, schema_version, gate rules 6–11 directly into the schema as part of the rounds themselves. Round 7 also REMOVED a previously-promoted block — see history below.)

## Promoted history

Each row is one breaking schema bump. Non-breaking gate-tightening rounds keep the same `schema_version`. Removals are also breaking — they bump the version.

| schema_version | round | additions / removals |
|----------------|-------|----------------------|
| 1 | initial | base artifact frontmatter (id, control_type, data_source, business_logic, configuration, validation, reactivity, endpoints, authorization). |
| 2 | round-1 | per-aspect endpoint verification (replaced single `unverified: bool`); structured `extensions[]`; `tenant_boundary` block introduced. |
| 3 | round-2 | structured `failure_matrix` cells `{status, behavior, evidence}`; `tamper_matrix` per endpoint; `contract_status` gate (5 rules). |
| 4 | round-3 | stable `endpoints[].id`; tamper_matrix references via `endpoint_id`; `contract_status_exceptions[]`. |
| 5 | round-4 | gate rule 6 (evidence coherence); `mutates_state` decouples from HTTP method; broadened `tenant_boundary` triggers; `signal_sources[].endpoint_id`; `cross_slice_refs_pending`; gate rule 8 (Mode B unknowns); `concurrency_conflict` + `audit_emission` cells; privacy lint extended to verification log. |
| 6 | round-5 | **add**: typed evidence shape (`source_refs` as `{path,symbol}` / `{path,line}` / `{test_id}` / `{artifact,section}`); gate rule 10 (`n/a` coherence); structured `business_logic.selection.{predicates, projection, ordering, paging}` REPLACES `selection.rules`; `schema_version` field + gate rule (originally numbered 12); `pattern-candidates.md` registry; `verification_evidence` block parallel to `verification`. |
| 6 | round-6 | consolidation pass: aligned SKILL.md gate references with template field names (predicates not rules); `contract_status_exceptions` block now explicitly covers gate-critical `n/a` (not only `observed_partial`); template gate-comment block trimmed to a pointer at SKILL.md to prevent rule-count drift; updated reference docs to v6 schema. No new gate rules. |
| 7 | round-7 | **remove**: `regulated_data_handling` block (read_audit, export_audit, retention, minimum_necessary) and the prior gate rule 11 (PHI coverage). Compliance / HIPAA-style metadata is project-context per goal.md, not artifact scope. User-visible audit emission stays in scope via `failure_matrix.audit_emission`. **Rationale**: rounds 5–6 let adversarial review drive scope expansion past the goal; round 7 is the scope correction. Current gate is 11 rules (was 12). |

`schema_version` increments when the round produces a change that requires existing artifacts to be re-shaped — including REMOVALS. Round 7 bumped v6 → v7 because all v6 artifacts must drop the `regulated_data_handling` block to validate.

## How to use this file

- Round-N reviewer (human or Codex): when proposing a new schema field or vocabulary term, add an entry under "Active candidates" with evidence and rationale.
- Promoting reviewer: when promoting a candidate, move its entry to the "Promoted history" table with its `schema_version`.
- Rejecting reviewer: leave the candidate in this file under a `## Rejected` section with the reason; future authors can search before re-proposing.

This file is referenced by `SKILL.md` "Skill evolution discipline" — it is the structural mechanism behind the prose discipline.
