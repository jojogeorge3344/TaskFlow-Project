#!/usr/bin/env python3
"""Check a TDD against the generated code fact sheet.

Usage:  python verify_tdd.py <tdd-facts.md> <TDD-TaskFlow.md>
Reports code items missing from the TDD and TDD routes that do not exist in code. Exit code 1 if anything is missing.
"""
import re, sys

facts = open(sys.argv[1], encoding="utf-8").read()
tdd = open(sys.argv[2], encoding="utf-8").read()

def norm(route):
    r = re.sub(r"\{(\w+)(:[^}]*)?\}", r"{\1}", route)          # {id:int} -> {id}
    return r.rstrip("/").lower()

code_routes = {}
for verb, route in re.findall(r"\| \w+ \| (GET|POST|PUT|PATCH|DELETE) \| `([^`]+)` \|", facts):
    code_routes[(verb, norm(route))] = route
tdd_api = {norm(m) for m in re.findall(r"`(/api/v1/[^`\s]+)`", tdd)}          # literal API routes
tdd_short = {norm("/api/v1" + m) for m in re.findall(r"`(/(?!api/)[a-z][^`\s]*)`", tdd)
             if ":" not in m and "?" not in m}                                   # short form /tasks/{id} (SPA routes use :id)
tdd_norm = tdd_api | tdd_short

missing_routes = sorted({r for (_, r) in code_routes if r not in tdd_norm})
code_norm = {r for (_, r) in code_routes}
def is_prefix(r):            # base paths like /api/v1/auth are fine
    return any(c.startswith(r + "/") for c in code_norm)
DEV_ONLY = {"/api/v1/diagnostics/throw"}   # mapped in Program.cs in Development only, not a controller action
stale_routes = sorted(r for r in tdd_api
                      if r not in code_norm and not is_prefix(r) and "*" not in r and r not in DEV_ONLY)

keys = re.findall(r"^- `([a-z]+(?:\.[a-z]+)+)` \(", facts, re.M)
missing_keys = [k for k in keys if f"`{k}`" not in tdd and k not in tdd]

entities = re.findall(r"^\*\*(\w+)\*\*:", facts, re.M)
missing_entities = [e for e in entities if not re.search(rf"\b{e}\b", tdd)]

enum_lines = re.findall(r"^- `(\w+)`: (.+)$", facts, re.M)
missing_enum_values = []
for name, vals in enum_lines:
    for v in [x.strip() for x in vals.split(",")]:
        if v and not re.search(rf"\b{re.escape(v)}\b", tdd):
            missing_enum_values.append(f"{name}.{v}")

def show(title, items):
    print(f"\n{title}: {len(items)}")
    for i in items[:200]:
        print("  -", i)

print(f"Code endpoints: {len(code_routes)} (distinct routes {len(code_norm)}) | permission keys: {len(keys)} | entities: {len(entities)}")
show("Code routes not mentioned in TDD", missing_routes)
show("TDD routes that do not exist in code (stale)", stale_routes)
show("Permission keys not in TDD", missing_keys)
show("Entities not in TDD", missing_entities)
show("Enum values not in TDD", missing_enum_values)
bad = missing_routes or stale_routes or missing_keys or missing_entities or missing_enum_values
print("\nRESULT:", "FIX NEEDED" if bad else "CLEAN")
sys.exit(1 if bad else 0)
