# 0005. Target net10.0, pinned via global.json

## Status

Accepted — 2026-08-29

## Context

`engine-implementation-plan.md` and `AGENTS.md` originally specified `net8.0`. That
was written before the toolchain was checked, and it does not survive contact with
the support timeline.

As of August 2026:

| Release | Track | Support ends |
|---|---|---|
| .NET 8 | LTS | November 2026 — roughly three months away |
| .NET 9 | STS | May 2026 — **already ended** |
| .NET 10 | LTS | November 2028 |

The machine has SDKs 9.0.120 and 10.0.111 installed, plus runtimes for 8, 9, and 10.
There is no .NET 8 SDK, and the 9.0 SDK's project templates do not offer `net8.0` as
a target at all.

This is a multi-year project. Starting it on a runtime three months from end of
support means a migration before the first vertical slice ships.

## Decision

Target **`net10.0`**. Pin the SDK in `global.json`, and have CI read that same file
through `actions/setup-dotnet`'s `global-json-file` input so there is one source of
truth rather than two that drift.

## Alternatives considered

**Target `net8.0` as originally specified.** Rejected. It would require installing an
SDK that is not present, to target a runtime that leaves support before the project
reaches a playable slice.

**Target `netstandard2.1` for maximum host compatibility.** Rejected for now, but
worth revisiting. It is the most portable target for a library intended to be
consumed by a game engine, and Unity in particular has historically constrained which
target frameworks it accepts. Deferred because the engine and platform choice is a
Phase 2 decision (`development-plan.md`), and `Brp.Core` is deliberately free of
engine dependencies, so retargeting later is cheap. If Unity is chosen and rejects
`net10.0`, revisit this — that is the scenario that would supersede this record.

## Consequences

- `global.json` pins `10.0.111` with `rollForward: latestFeature`, so patch and
  feature-band updates are picked up without editing the file, while a major-version
  jump stays deliberate.
- CI installs the SDK from `global.json`. Changing the SDK is a one-line change in one
  place.
- The plan's D6 note about keeping IL2CPP and AOT viable still stands and still
  matters — no reflection-heavy DI in the core — because it is what keeps the
  retargeting escape hatch open.
