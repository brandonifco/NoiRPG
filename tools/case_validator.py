#!/usr/bin/env python3
"""Case data validator for NoiRPG (cases/SCHEMA.md).

Structural checks (FAIL = exit 1):
  - known schema_version; required top-level keys present
  - Three Doors rule: every core clue has >=2 skill doors with distinct
    skills plus >=1 fallback door
  - door skills come from the canonical skill list; door locations exist
  - every evidence_out references declared evidence; no orphaned evidence
    (unreachable and untagged); interrogation doors name real suspects
  - accusation solution/required clues reference real suspects/clues;
    wrong_paths reference real suspects
  - junction-point rule: at most 3 junctions

Build audit (WARN only): walks every core clue for each background build
and reports which door opens it (skill door at min_rating, else
interrogation, else fallback). Fallback-heavy builds are flagged — that is
the case-board test's Three Doors audit, run by machine.

Usage: case_validator.py cases/overpass.yaml
"""

import sys

import yaml

SCHEMA_VERSIONS = {"0.1"}

SKILLS = {
    "Streetwise", "Shadow", "Insight", "Fast Talk", "Intimidate", "Persuade",
    "Law", "Accounting", "Photography", "Locksmith", "Research", "First Aid",
    "Firearms", "Brawl", "Dodge", "Drive", "Stealth", "Spot",
}

REQUIRED_KEYS = ("schema_version", "case", "locations", "suspects",
                 "evidence", "core_clues", "accusation")

# Audit builds: background packages from the framework (top skills only).
BUILDS = {
    "ex-cop":        {"Streetwise": 65, "Intimidate": 65, "Firearms": 60,
                      "Spot": 60, "Insight": 55, "First Aid": 45,
                      "Fast Talk": 40, "Law": 35, "Photography": 30,
                      "Accounting": 15},
    "ex-accountant": {"Accounting": 70, "Research": 65, "Law": 55,
                      "Insight": 50, "Persuade": 50, "Fast Talk": 45,
                      "Photography": 25, "First Aid": 25, "Streetwise": 20},
    "ex-soldier":    {"Firearms": 70, "Dodge": 60, "Brawl": 55, "Spot": 55,
                      "First Aid": 50, "Intimidate": 50, "Drive": 45,
                      "Stealth": 45, "Streetwise": 35, "Persuade": 25},
}

failures = []
warnings = []


def fail(msg):
    failures.append(msg)


def warn(msg):
    warnings.append(msg)


def check_structure(data):
    for key in REQUIRED_KEYS:
        if key not in data:
            fail(f"missing top-level key: {key}")
    if failures:
        return
    if str(data["schema_version"]) not in SCHEMA_VERSIONS:
        fail(f"unknown schema_version {data['schema_version']!r}")

    locations = {l["id"] for l in data["locations"]}
    suspects = {s["id"] for s in data["suspects"]}
    evidence = {e["id"] for e in data["evidence"]}
    clue_ids = {c["id"] for c in data["core_clues"]}
    reachable = set()

    for clue in data["core_clues"]:
        cid = clue["id"]
        doors = clue.get("doors", [])
        skill_doors = [d for d in doors if d["type"] == "skill"]
        fallbacks = [d for d in doors if d["type"] == "fallback"]
        distinct = {d["skill"] for d in skill_doors}
        if len(distinct) < 2:
            fail(f"{cid}: Three Doors violation — needs >=2 distinct skill "
                 f"doors, has {sorted(distinct)}")
        if not fallbacks:
            fail(f"{cid}: Three Doors violation — no fallback door")
        for d in doors:
            out = d.get("evidence_out")
            if out not in evidence:
                fail(f"{cid}: door yields undeclared evidence {out!r}")
            reachable.add(out)
            if d["type"] == "skill":
                if d["skill"] not in SKILLS:
                    fail(f"{cid}: unknown skill {d['skill']!r}")
                if d.get("location") not in locations:
                    fail(f"{cid}: unknown location {d.get('location')!r}")
            if d["type"] == "interrogation" and d.get("suspect") not in suspects:
                fail(f"{cid}: interrogation door names unknown suspect "
                     f"{d.get('suspect')!r}")
            if d["type"] == "fallback" and d.get("skill"):
                fail(f"{cid}: fallback door must be skill-free")

    for e in data["evidence"]:
        if e["id"] not in reachable and "tag" not in e:
            fail(f"orphaned evidence: {e['id']} (unreachable and untagged)")

    acc = data["accusation"]
    for c in acc.get("required_clues", []) + acc.get("full_clear_clues", []):
        if c not in clue_ids:
            fail(f"accusation references unknown clue {c!r}")
    for role, val in acc.get("solution", {}).items():
        if val not in suspects | clue_ids:
            fail(f"accusation solution {role}={val!r} matches no suspect/clue")
    for wp in acc.get("wrong_paths", []):
        if wp["accused"] not in suspects:
            fail(f"wrong_path accuses unknown suspect {wp['accused']!r}")
        for item in wp.get("lure", []):
            if item not in evidence:
                fail(f"wrong_path lure references unknown evidence {item!r}")

    junctions = data.get("junctions", [])
    if len(junctions) > 3:
        fail(f"junction-point rule violation: {len(junctions)} junctions (max 3)")


def audit_builds(data):
    print("\n=== Three Doors build audit ===")
    for name, ratings in BUILDS.items():
        fallback_count = 0
        print(f"\n  {name}:")
        for clue in data["core_clues"]:
            opened = None
            for d in clue["doors"]:
                if (d["type"] == "skill"
                        and ratings.get(d["skill"], 0) >= d.get("min_rating", 40)):
                    opened = f"{d['skill']} {ratings[d['skill']]} @ {d['location']}"
                    break
            if opened is None:
                for d in clue["doors"]:
                    if d["type"] == "interrogation":
                        opened = f"interrogation: {d['suspect']} (record guaranteed)"
                        break
            if opened is None:
                fb = next(d for d in clue["doors"] if d["type"] == "fallback")
                opened = f"FALLBACK — {fb['cost']}"
                fallback_count += 1
            print(f"    {clue['id']:<24} {opened}")
        if fallback_count >= 2:
            warn(f"{name} needs fallbacks on {fallback_count}/"
                 f"{len(data['core_clues'])} core clues — fallback-heavy "
                 f"build; add doors for its top skills or accept the pacing")


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else "cases/overpass.yaml"
    with open(path) as f:
        data = yaml.safe_load(f)

    check_structure(data)
    if not failures:
        audit_builds(data)

    print(f"\n=== {path} ===")
    for msg in failures:
        print(f"  FAIL  {msg}")
    for msg in warnings:
        print(f"  WARN  {msg}")
    if not failures:
        n_doors = sum(len(c["doors"]) for c in data["core_clues"])
        print(f"  PASS  {len(data['core_clues'])} core clues, {n_doors} doors, "
              f"{len(data['evidence'])} evidence items, "
              f"{len(data.get('junctions', []))}/3 junctions")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
