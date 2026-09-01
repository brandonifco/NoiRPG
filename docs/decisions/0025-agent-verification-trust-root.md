# 0025 — The `agent-verification` trust root, and the codex-conformance / semantic-gate triage policy

Status: Accepted

This is a process record, not a BRP rules record — the sourced/house-rule convention
in `docs/decisions/README.md` does not apply; there is no rules-source claim here to
mark.

## The question

`tools/agent-verify.sh` posts the `agent-verification` commit status that
GitHub's `main` ruleset requires. GitHub cannot independently confirm that a
semantic reviewer actually ran the gates it names — the required-status rule binds
only the **context name** `agent-verification`, not any integration identity. Who
is authorized to mint that status, and how is that trust root documented?
Separately, the #174 burn-in (the first live `formulas`-route PR to run the full
gate set end to end, `agent-verify` posting on head `c4cf378`, PR #179) surfaced a
second, related process gap: an adversarial "falsify" semantic gate
(`codex-conformance`) produces *some* finding on every pass, decaying in severity
round over round, so a strict "the gate must say CONFIRMED" rule is unsatisfiable.
Both are recorded here because the second was identified as direct input to this
same Issue (#171).

## Part 1 — Trust root

**Decision:** the local orchestrator's GitHub credential is the trust root that
mints the `agent-verification` commit status. Agents never receive status-write
scope; only the orchestrator runs `tools/agent-verify.sh --post`.

This is deliberately **accidental-error protection, not adversarial-agent
protection**. Because the required-status rule binds the context name
`agent-verification` rather than an integration identity, any actor holding the
orchestrator's credential could post that status directly, bypassing the gates it
is supposed to attest to. Under the project's current solo threat model
(orchestrator trusted, agents fallible workers, no semi-trusted third party holding
write credentials) this is an acceptable gap: it stops the same classes of error
the rest of the pipeline stops (a gate not run, a stale result, a gate that failed
being posted as passing), and it does so without building new machinery.

It stops being acceptable the moment the credential is shared with a
less-trusted actor. At that point, revisit binding the required status to a
dedicated integration/bot identity rather than a context name — the direction
previously proposed and rejected in #90/#91 as a gate-poster GitHub App. That
direction was rejected then because it reintroduced App/fan-out machinery the
project had deliberately removed; the rejection was about the credential-sharing
condition not holding yet, not about the mechanism being wrong in principle. Do
not build it preemptively.

**Explicitly out of scope:** rebuilding the gate-poster App (already rejected in
#65/#90/#91); moving semantic gates into a trusted CI runner (contradicts the
deliberate design that semantic models do not run in GitHub Actions — see
`docs/orchestration/agent-verification.md`).

## Part 2 — codex-conformance / semantic-gate triage policy

**Evidence base:** the #174 burn-in, PR #179 (Issue #112, hit locations). Codex's
adversarial "falsify" conformance pass ran four rounds. Severity decayed across
them: round 1 surfaced a real crash (`ArmorCoverage` on printed `"All"` / `"All but
head"` labels) and a real layering-rule bug (max instead of printed total); round 3
surfaced a legitimate but already-decidable design-contract question (per-blow vs.
cumulative damage banding); round 4 surfaced an unreachable `Int32` overflow and a
data-boundary point that `scope-warden` had already ruled structural. A gate
capable of catching round-1-grade defects will, by construction, also keep firing
on round-4-grade nitpicks — "must say CONFIRMED" is not a policy a gate built this
way can ever satisfy.

**Decision — the triage policy:**

- **Value/behavior conformance defects block merge.** A finding that the shipped
  behavior diverges from the source rule (or from a recorded, cited deviation) is a
  real defect and gates the PR until fixed.
- **Unreachable-robustness findings, style findings, and findings the source
  packet already adjudicates are logged, not blocking.** These go into the PR body
  or a follow-up Issue rather than forcing another rework cycle.
- **The orchestrator adjudicates gate-vs-gate disagreement.** When two semantic
  gates disagree (as `rules-conformance` and `codex-conformance` did in #179 —
  each caught something the other missed), the orchestrator makes the call and
  records the adjudication in the PR's `agent-verification` evidence block or a PR
  comment. This is a human/orchestrator judgment call, not something either gate
  resolves unilaterally. This adjudication authority rests on the same trusted
  orchestrator named as the minting authority in Part 1 — it is one point of trust,
  not two independent gaps — and it shares Part 1's revisit condition: reconsider
  it only if that credential/role is ever shared with a less-trusted actor.
- **Documented deviations get errata-authority packet blocks.** When a decision
  record already corrects a printed misprint or documents a deliberate departure
  (for example, `docs/decisions/0024-hit-locations.md`'s D20 `8–11` -> `9–11`
  table-misprint correction), quote that authority directly into the independent
  gate's source packet so the gate verifies against the *recorded* rule rather than
  flagging every departure from raw printed text as unsupported.
- **Deferred/contracted scope gets design-contract packet blocks.** When a
  component's contract is deliberately narrower than a naive reading would assume
  (for example, `HitLocationDamageResolver` in #179 being a stateless single-blow
  classifier, with accumulation left to the caller), state that contract explicitly
  in the packet so the gate verifies conformance-to-contract instead of flagging the
  narrower scope as a defect.

Both packet-block patterns are legitimate recorded authority, analogous in kind to
`orc-scope-filter.md`, and reusing them does **not** blunt the gate's independence
— the gate still checks the implementation against a citable, committed authority;
it is simply the *correct* authority (the recorded erratum or the recorded
contract) rather than the raw, uncorrected printed text or an assumed contract the
component never had. The design-contract block has a latent abuse vector — a
contract narrowed *post-hoc*, after a gate flags a behavior, specifically to write
that behavior out of scope — and the guardrail against it is that the contract must
already be committed, citable, prior authority, not language drafted in reaction to
dodge a specific finding.

## Consequences

- No new machinery: this section documents an existing credential boundary and a
  triage discipline for existing gates; it does not add a status-poster identity,
  App, or aggregator.
- `tools/agent-verify.sh` and its required-status wiring are unchanged by this
  record.
- Future packets for `rules-conformance` / `codex-conformance` should include
  errata-authority and design-contract blocks where applicable, per Part 2, rather
  than each implementer rediscovering the pattern.
- Revisit Part 1 (the trust root) only if the orchestrator credential is ever
  shared with a less-trusted actor.

## References

- `docs/orchestration/agent-verification.md` — the mechanism this record's trust
  root applies to.
- `docs/orchestration/agent-verification-burn-in.md` — the #174 burn-in evidence,
  including finding F3 (triage policy) and F4 (packet-authority patterns).
- Issue #171 (this decision); Issue #174 and PR #179 (the evidence base); #90/#91
  (the rejected gate-poster App direction); #65 (earlier related rejection).
