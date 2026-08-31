# The gate-poster GitHub App

The routing state machine (#62) blocks a PR until each required verification gate
(`scope-warden`, `rules-conformance`, `codex-conformance`, `architecture-review`)
has reported. Those gates run in the local/self-hosted orchestrator, not in
Actions, so they need an identity to publish results back to GitHub as check-runs.
That identity is a **GitHub App**, chosen over a personal token for scoped,
revocable, clearly-attributed check-runs.

[`tools/gate-check.py`](../../tools/gate-check.py) is the poster. The one thing that
cannot be scripted from here is the App **registration** — that is a web-UI action
on your account. Steps below (~5 minutes, once).

## 1. Register the App

Settings → Developer settings → **GitHub Apps** → **New GitHub App**.

- **Name:** `NoiRPG Gate Poster` (any unique name)
- **Homepage URL:** the repo URL
- **Webhook:** uncheck **Active** (this App is polled/pushed to, it receives nothing)
- **Repository permissions:**
  - **Checks:** Read and write
  - **Contents:** Read-only
  - **Pull requests:** Read and write
- **Where can this App be installed:** Only on this account

Values mirror [`.github/github-app-manifest.json`](../../.github/github-app-manifest.json),
which you can also feed to the App-manifest creation flow if you prefer.

## 2. Generate a private key

On the App's page → **Private keys** → **Generate a private key**. A `.pem`
downloads. Store it where the orchestrator runs, outside the repo — e.g.
`~/.config/noirpg/gate-poster.pem`, `chmod 600`. **Never commit it.**

## 3. Install the App on the repo

App page → **Install App** → install on `brandonifco/NoiRPG`.

## 4. Wire the orchestrator's environment

```bash
export GH_APP_ID=<the App id shown on its page>
export GH_APP_PRIVATE_KEY=~/.config/noirpg/gate-poster.pem   # path or PEM contents
# GH_APP_INSTALLATION_ID is optional — discovered from the repo if unset.
```

## 5. Post a gate result

```bash
tools/gate-check.py \
  --gate scope-warden --sha "$(git rev-parse HEAD)" \
  --conclusion success --summary "No out-of-scope content; source ok."
```

`--dry-run` mints the JWT and prints the check-run payload without calling GitHub —
use it to confirm the key and App id are wired before going live. Valid
`--conclusion` values: `success`, `failure`, `neutral`, `cancelled`, `timed_out`,
`action_required`.

## How it works

1. Mint a short-lived RS256 JWT signed by the App private key (via `openssl`).
2. Exchange it for an installation access token scoped to the repo.
3. `POST /repos/{owner}/{repo}/check-runs` with `name = <gate>`, `head_sha`,
   `conclusion`, and an output summary.

The check-run name is the gate name, so #62's aggregator can find it on the PR
head commit. Dependency-light: Python stdlib + the `openssl` binary, no pip.

## Security posture

- Least privilege: three repository permissions, no webhook, no org scope.
- The private key stays on the orchestrator host; GitHub only ever sees short-lived
  installation tokens.
- The App cannot edit code (Contents is read-only) — it can only report checks and
  read PRs, so it can never "fix" what a gate is judging.
