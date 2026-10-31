#!/usr/bin/env python3
"""Extract code facts for the TaskFlow TDD.

Usage:  python extract_facts.py <TaskFlow Proj folder> <output.md>
Reads only; writes one Markdown fact sheet: packages, endpoints (verb, route, policy/permission),
permission keys, DbSets/entities with properties, enums, migrations, frontend routes and nav, test files.
"""
import re, sys, json, pathlib

root = pathlib.Path(sys.argv[1]); out = pathlib.Path(sys.argv[2])
api = root / "backend" / "src" / "TaskFlow.Api"
fe = root / "frontend"
L = []
def h(t): L.append(f"\n## {t}\n")
def rd(p):
    try: return p.read_text(encoding="utf-8-sig", errors="replace")
    except Exception: return ""

# 1. Packages / framework
h("1. Backend packages and target framework")
for cs in api.glob("*.csproj"):
    t = rd(cs)
    for m in re.findall(r"<TargetFramework>(.*?)</TargetFramework>", t): L.append(f"- TargetFramework: `{m}`")
    for n, v in re.findall(r'PackageReference Include="([^"]+)" Version="([^"]+)"', t): L.append(f"- `{n}` {v}")
h("2. Frontend packages")
pj = fe / "package.json"
if pj.exists():
    d = json.loads(rd(pj))
    for sec in ("dependencies", "devDependencies"):
        for k, v in d.get(sec, {}).items(): L.append(f"- ({sec[:3]}) `{k}` {v}")
    for k, v in d.get("scripts", {}).items(): L.append(f"- script `{k}`: `{v}`")

# 3. Endpoints
h("3. API endpoints (from controllers)")
L.append("| Controller | Verb | Route | Authorization attributes on action/class |")
L.append("|---|---|---|---|")
verb_re = re.compile(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]')
count = 0
for f in sorted((api / "Controllers").rglob("*Controller.cs")):
    t = rd(f)
    base = (re.search(r'\[Route\("([^"]+)"\)\]\s*(?:\[[^\]]*\]\s*)*public\s+(?:sealed\s+)?class', t) or re.search(r'\[Route\("([^"]+)"\)\]', t))
    base = base.group(1) if base else ""
    cls_head = t.split("class", 1)[0]
    cls_auth = re.findall(r'\[(Authorize[^\]]*|AllowAnonymous|RequirePermission[^\]]*)\]', cls_head)
    lines = t.split("\n")
    for i, ln in enumerate(lines):
        m = verb_re.search(ln)
        if not m: continue
        # attributes in the block above the method (until blank line or closing brace)
        attrs = []
        j = i
        while j >= 0 and lines[j].strip().startswith("[") or j == i:
            attrs += re.findall(r'\[(Authorize[^\]]*|AllowAnonymous|RequirePermission[^\]]*|EnableRateLimiting[^\]]*)\]', lines[j]); j -= 1
        k = i + 1
        while k < len(lines) and lines[k].strip().startswith("["):
            attrs += re.findall(r'\[(Authorize[^\]]*|AllowAnonymous|RequirePermission[^\]]*|EnableRateLimiting[^\]]*)\]', lines[k]); k += 1
        sub = m.group(2) or ""
        route = "/" + "/".join(x for x in [base.strip("/"), sub.strip("/")] if x)
        auth = "; ".join(cls_auth + attrs) or "-"
        L.append(f"| {f.stem} | {m.group(1).upper()} | `{route}` | `{auth}` |"); count += 1
L.append(f"\nTotal HTTP actions: **{count}**")

# 4. Permission keys
h("4. Permission keys (Common/Security/PermissionKeys.cs) and catalog")
pk = rd(api / "Common" / "Security" / "PermissionKeys.cs")
for name, key in re.findall(r'(\w+)\s*=\s*"([a-z]+(?:\.[a-z]+)+)"', pk): L.append(f"- `{key}` ({name})")
cat = api / "Common" / "Security" / "PermissionCatalog.cs"
if cat.exists(): L.append(f"\nCatalog file (role defaults, assignability): `{cat.relative_to(root)}` – read it for Granted/Denied/Always-On defaults.")

# 5. Entities
h("5. DbSets and entities")
ctx = rd(api / "Persistence" / "AppDbContext.cs")
for typ, name in re.findall(r'DbSet<([\w\.]+)>\s+(\w+)', ctx): L.append(f"- DbSet `{name}` → `{typ.split('.')[-1]}`")
for f in sorted((api / "Models" / "Entities").glob("*.cs")):
    props = re.findall(r'public\s+([\w<>\?\[\], \.]+?)\s+(\w+)\s*\{\s*get;', rd(f))
    L.append(f"\n**{f.stem}**: " + ", ".join(f"{n} ({t.strip()})" for t, n in props))
h("6. Enums")
for f in sorted((api / "Models" / "Enums").glob("*.cs")):
    t = rd(f); m = re.search(r'enum\s+(\w+)\s*\{([^}]*)\}', t, re.S)
    if m:
        vals = [re.sub(r'\s*=.*', '', v.strip()) for v in re.sub(r'//.*', '', m.group(2)).split(",") if v.strip() and not v.strip().startswith("[")]
        L.append(f"- `{m.group(1)}`: {', '.join(vals)}")

# 7. Migrations
h("7. Migrations (chronological)")
for f in sorted((api / "Persistence" / "Migrations").glob("2*.cs")):
    if not f.name.endswith(".Designer.cs"): L.append(f"- `{f.stem}`")

# 8. Config keys
h("8. appsettings.json keys (values of secrets are not printed)")
def walk(o, p=""):
    if isinstance(o, dict):
        for k, v in o.items(): walk(v, f"{p}:{k}" if p else k)
    else:
        sec = re.search(r'(key|secret|password|token|connection)', p, re.I)
        L.append(f"- `{p}` = {'<secret/omitted>' if sec else json.dumps(o)[:80]}")
try:
    walk(json.loads(re.sub(r'//.*', '', rd(api / "appsettings.json"))))
except Exception as e: L.append(f"- (could not parse appsettings.json: {e})")

# 9. Services / repositories / background services
h("9. Services, repositories, hosted services, integrations")
for layer in ("Services", "Repositories", "Integrations"):
    names = sorted({f.stem for f in (api / layer).rglob("*.cs") if not f.stem.startswith("I") or not f.stem[1:2].isupper()})
    L.append(f"- **{layer}** ({len(names)}): " + ", ".join(names))
hosted = [f.stem for f in api.rglob("*.cs") if "BackgroundService" in rd(f) and ": BackgroundService" in rd(f)]
L.append(f"- **Hosted/background services**: {', '.join(hosted) or 'none found'}")

# 10. Frontend routes
h("10. Frontend routes (*.routes.ts) and navigation")
app = fe / "src" / "app"
for f in sorted(app.rglob("*.routes.ts")):
    t = rd(f)
    for p in re.findall(r"path:\s*'([^']*)'", t): L.append(f"- `{f.relative_to(app)}` → path `'{p}'`")
nav = app / "layouts" / "nav-items.ts"
if nav.exists():
    for lab, r in re.findall(r"label:\s*'([^']+)'[^}]*?route:\s*'([^']+)'", rd(nav)): L.append(f"- menu **{lab}** → `{r}`")

# 11. Tests
h("11. Test files")
tests = root / "backend" / "tests"
tf = sorted(str(p.relative_to(tests)) for p in tests.rglob("*Tests.cs") if "obj" not in p.parts and "bin" not in p.parts)
L.append(f"Total test classes: **{len(tf)}**")
for x in tf: L.append(f"- `{x}`")
fspec = sorted(str(p.relative_to(app)) for p in app.rglob("*.spec.ts"))
L.append(f"\nFrontend spec files: **{len(fspec)}**")

out.write_text("# TaskFlow code facts (generated)\n" + "\n".join(L) + "\n", encoding="utf-8")
print(f"wrote {out}  endpoints={count}")
