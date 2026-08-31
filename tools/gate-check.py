#!/usr/bin/env python3
"""gate-check — post a verification gate result to a PR head commit as a check-run.

This is the identity through which the local/self-hosted orchestrator publishes
agent gate results (scope-warden, rules-conformance, codex-conformance,
architecture-review) back to GitHub, using a GitHub App rather than a personal
token. See docs/orchestration/github-app.md for registering the App.

Dependency-light: Python stdlib + the `openssl` binary for RS256 signing. No pip.

Usage:
    tools/gate-check.py \
        --gate scope-warden --sha <head-sha> \
        --conclusion success --title "scope-warden" \
        --summary "No out-of-scope content; source ok."

Env:
    GH_APP_ID                 GitHub App id (integer).
    GH_APP_PRIVATE_KEY        path to the App private-key .pem (or the PEM itself).
    GH_APP_INSTALLATION_ID    optional; discovered from the repo if unset.
    GH_REPO                   owner/name; defaults to --repo or the git remote.

--dry-run mints the JWT and prints the check-run payload without calling GitHub.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request

API = "https://api.github.com"
VALID_CONCLUSIONS = {"success", "failure", "neutral", "cancelled", "timed_out", "action_required"}


def _b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def _load_key(val: str) -> bytes:
    if "-----BEGIN" in val:
        return val.encode()
    return open(os.path.expanduser(val), "rb").read()


def _sign_rs256(private_key: bytes, signing_input: bytes) -> bytes:
    import tempfile

    with tempfile.NamedTemporaryFile(suffix=".pem") as kf:
        kf.write(private_key)
        kf.flush()
        proc = subprocess.run(
            ["openssl", "dgst", "-sha256", "-sign", kf.name],
            input=signing_input, capture_output=True,
        )
    if proc.returncode != 0:
        raise SystemExit(f"openssl signing failed: {proc.stderr.decode().strip()}")
    return proc.stdout


def jwt_for_app(app_id: str, private_key: bytes) -> str:
    now = int(time.time())
    header = _b64url(json.dumps({"alg": "RS256", "typ": "JWT"}).encode())
    payload = _b64url(json.dumps({"iat": now - 30, "exp": now + 540, "iss": str(app_id)}).encode())
    signing_input = f"{header}.{payload}".encode()
    sig = _b64url(_sign_rs256(private_key, signing_input))
    return f"{header}.{payload}.{sig}"


def api(method: str, path: str, token: str, token_type: str = "Bearer", body: dict | None = None) -> dict:
    url = path if path.startswith("http") else API + path
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Authorization", f"{token_type} {token}")
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("X-GitHub-Api-Version", "2022-11-28")
    req.add_header("User-Agent", "noirpg-gate-check")
    try:
        with urllib.request.urlopen(req) as resp:
            raw = resp.read()
            return json.loads(raw) if raw else {}
    except urllib.error.HTTPError as e:
        raise SystemExit(f"{method} {url} -> {e.code}: {e.read().decode()[:300]}")


def default_repo() -> str | None:
    for env in ("GH_REPO", "GITHUB_REPOSITORY"):
        if os.environ.get(env):
            return os.environ[env]
    try:
        url = subprocess.check_output(["git", "remote", "get-url", "origin"], text=True).strip()
        tail = url.split("github.com")[-1].lstrip(":/").removesuffix(".git")
        return tail or None
    except Exception:  # noqa: BLE001
        return None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--gate", required=True, help="gate name, e.g. scope-warden")
    ap.add_argument("--sha", required=True, help="PR head commit sha")
    ap.add_argument("--conclusion", required=True, choices=sorted(VALID_CONCLUSIONS))
    ap.add_argument("--title", default=None)
    ap.add_argument("--summary", default="")
    ap.add_argument("--details-url", default=None)
    ap.add_argument("--repo", default=None)
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    repo = args.repo or default_repo()
    if not repo:
        return _fail("cannot determine repo (set GH_REPO or --repo)")

    payload = {
        "name": args.gate,
        "head_sha": args.sha,
        "status": "completed",
        "conclusion": args.conclusion,
        "output": {"title": args.title or args.gate, "summary": args.summary or f"{args.gate}: {args.conclusion}"},
    }
    if args.details_url:
        payload["details_url"] = args.details_url

    app_id = os.environ.get("GH_APP_ID")
    key_val = os.environ.get("GH_APP_PRIVATE_KEY")
    if not (app_id and key_val):
        if args.dry_run:
            print("[dry-run] no App creds; would POST check-run:")
            print(json.dumps(payload, indent=2))
            return 0
        return _fail("GH_APP_ID and GH_APP_PRIVATE_KEY must be set")

    jwt = jwt_for_app(app_id, _load_key(key_val))
    if args.dry_run:
        print(f"[dry-run] minted JWT (len={len(jwt)}); would POST check-run to {repo}:")
        print(json.dumps(payload, indent=2))
        return 0

    inst = os.environ.get("GH_APP_INSTALLATION_ID")
    if not inst:
        inst = str(api("GET", f"/repos/{repo}/installation", jwt)["id"])
    tok = api("POST", f"/app/installations/{inst}/access_tokens", jwt)["token"]
    res = api("POST", f"/repos/{repo}/check-runs", tok, token_type="token", body=payload)
    print(f"posted check-run '{args.gate}' = {args.conclusion} on {args.sha[:10]} (id {res.get('id')})")
    return 0


def _fail(msg: str) -> int:
    print(f"gate-check: {msg}", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
