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
tdd_norm = {norm(m) for m in re.findall(r"`(/api/v1/[^`\s]+)`", tdd)}
tdd_norm |= {norm("/api/v1" + m) for m in re.findall(r"`(/(?!api/)[a-z][^`\s]*)`", tdd)}   # short form /tasks/{id}

missing_routes = sorted({r for (_, r) in code_routes if r not in tdd_norm})
code_norm = {r for (_, r) in code_routes}
stale_routes = sorted(r for r in tdd_norm if r.startswith("/api/v1/") and r not in code_norm)

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
