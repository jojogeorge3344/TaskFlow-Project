#!/usr/bin/env python3
"""Faithful raw extraction of a DOCX/PDF into Markdown. Stdlib only (+ pdftotext/pypdf for PDF).

usage: extract.py <input.docx|input.pdf> <output_raw.md>
Prints a JSON summary (stats + flags). The raw file is a DRAFT: Claude then restructures it
following SKILL.md. Flags are also written inline as '> REVIEW:' lines.
"""
import json, re, subprocess, sys, zipfile, shutil
import xml.etree.ElementTree as ET
from pathlib import Path

W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
flags = []


def flag(msg):
    flags.append(msg)
    return f"> ⚠ REVIEW: {msg}"


def esc(s):
    return s.replace("|", "\\|").replace("\n", "<br>")


# ---------------- DOCX ----------------
def ptext(p):
    out = []
    for el in p.iter():
        t = el.tag
        if t == W + "t":
            out.append(el.text or "")
        elif t == W + "tab":
            out.append(" ")
        elif t in (W + "br", W + "cr"):
            out.append("\n")
        # w:delText (tracked deletion) is ignored on purpose; flagged in docx()
    return "".join(out).strip()


class Numbering:
    def __init__(self, root):
        self.lvls, self.num2abs, self.counters = {}, {}, {}
        if root is None:
            return
        for a in root.findall(W + "abstractNum"):
            aid = a.get(W + "abstractNumId")
            for l in a.findall(W + "lvl"):
                fmt = l.find(W + "numFmt")
                txt = l.find(W + "lvlText")
                self.lvls[(aid, int(l.get(W + "ilvl")))] = (
                    fmt.get(W + "val") if fmt is not None else "decimal",
                    txt.get(W + "val") if txt is not None else "%1.")
        for n in root.findall(W + "num"):
            a = n.find(W + "abstractNumId")
            if a is not None:
                self.num2abs[n.get(W + "numId")] = a.get(W + "val")

    def label(self, numid, ilvl):
        aid = self.num2abs.get(numid)
        fmt, txt = self.lvls.get((aid, ilvl), ("decimal", "%1."))
        c = self.counters.setdefault(numid, {})
        c[ilvl] = c.get(ilvl, 0) + 1
        for k in [k for k in c if k > ilvl]:
            del c[k]
        if fmt == "bullet":
            return "-"
        lab = txt
        for i in range(ilvl + 1):
            n = c.get(i, 1)
            v = {"lowerLetter": chr(96 + n) if n <= 26 else str(n),
                 "upperLetter": chr(64 + n) if n <= 26 else str(n),
                 "lowerRoman": roman(n).lower(), "upperRoman": roman(n)}.get(fmt, str(n))
            lab = lab.replace(f"%{i+1}", v)
        return lab.strip()


def roman(n):
    r = ""
    for v, s in [(10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")]:
        while n >= v:
            r, n = r + s, n - v
    return r


def para_md(p, num, styles):
    txt = ptext(p)
    if not txt:
        return None
    ppr = p.find(W + "pPr")
    style = ""
    numid = ilvl = None
    if ppr is not None:
        ps = ppr.find(W + "pStyle")
        if ps is not None:
            style = styles.get(ps.get(W + "val"), ps.get(W + "val"))
        np_ = ppr.find(W + "numPr")
        if np_ is not None:
            ni, il = np_.find(W + "numId"), np_.find(W + "ilvl")
            if ni is not None and ni.get(W + "val") != "0":
                numid = ni.get(W + "val")
                ilvl = int(il.get(W + "val")) if il is not None else 0
    m = re.match(r"(?i)heading\s*(\d)", style)
    if m:
        return "#" * min(int(m.group(1)) + 1, 6) + " " + txt  # H1 reserved for doc title
    if style.lower() == "title":
        return "# " + txt
    if numid:
        return "  " * ilvl + f"{num.label(numid, ilvl)} {txt}"
    return txt


def table_cells(tbl):
    return [[ptext(tc) for tc in tr.findall(W + "tc")] for tr in tbl.findall(W + "tr")]


def table_md(tbl, num, styles):
    grid = []
    for tr in tbl.findall(W + "tr"):
        row = []
        for tc in tr.findall(W + "tc"):
            pr = tc.find(W + "tcPr")
            span, vm = 1, None
            if pr is not None:
                gs, v = pr.find(W + "gridSpan"), pr.find(W + "vMerge")
                span = int(gs.get(W + "val")) if gs is not None else 1
                if v is not None:
                    vm = v.get(W + "val", "continue")
            parts = []
            for ch in tc:
                if ch.tag == W + "p":
                    t = para_md(ch, num, styles)
                    if t:
                        parts.append(re.sub(r"^#+ ", "", t).strip())
                elif ch.tag == W + "tbl":
                    parts.append("[nested table: " + " ; ".join(
                        " | ".join(c) for c in table_cells(ch)) + "]")
                    flag("nested table flattened inside a cell; verify")
            row.append({"t": "<br>".join(parts), "span": span, "vm": vm})
        grid.append(row)
    # Repeat values across horizontal/vertical merges so every row stays self-contained.
    rows, merged, prev = [], False, []
    for r in grid:
        out = []
        for c in r:
            txt = c["t"]
            if c["vm"] == "continue":
                txt = prev[len(out)] if len(out) < len(prev) else ""
                merged = True
            if c["span"] > 1:
                merged = True
            out += [txt] * c["span"]
        rows.append(out)
        prev = out
    if not rows:
        return ""
    n = max(len(r) for r in rows)
    rows = [r + [""] * (n - len(r)) for r in rows]
    md = ["| " + " | ".join(esc(c) for c in rows[0]) + " |", "|" + "---|" * n]
    md += ["| " + " | ".join(esc(c) for c in r) + " |" for r in rows[1:]]
    note = ""
    if merged:
        note = "\n\n" + flag("table has merged cells; merged values were repeated in every cell they span. Verify.")
    return "\n".join(md) + note


def docx(path):
    z = zipfile.ZipFile(path)
    names = z.namelist()
    load = lambda n: ET.fromstring(z.read(n)) if n in names else None
    styles = {}
    sr = load("word/styles.xml")
    if sr is not None:
        for s in sr.findall(W + "style"):
            nm = s.find(W + "name")
            if nm is not None:
                styles[s.get(W + "styleId")] = nm.get(W + "val")
    num = Numbering(load("word/numbering.xml"))
    doc = load("word/document.xml")
    out = []

    def walk(container):
        for ch in container:
            if ch.tag == W + "p":
                t = para_md(ch, num, styles)
                if t:
                    out.append(t)
            elif ch.tag == W + "tbl":
                out.append(table_md(ch, num, styles))
            elif ch.tag == W + "sdt":
                c = ch.find(W + "sdtContent")
                if c is not None:
                    walk(c)
    walk(doc.find(W + "body"))
    md = "\n\n".join(out)

    extra, seen = [], set()
    for pat, title in [(r"word/header\d*\.xml", "Header"), (r"word/footer\d*\.xml", "Footer"),
                       (r"word/footnotes\.xml", "Footnotes"), (r"word/endnotes\.xml", "Endnotes"),
                       (r"word/comments\.xml", "Reviewer comments")]:
        for n in names:
            if re.fullmatch(pat, n):
                txt = "\n".join(t for t in (ptext(p) for p in load(n).iter(W + "p")) if t)
                if txt and txt not in seen:
                    seen.add(txt)
                    extra.append(f"### {title} ({n})\n\n{txt}")
                    if title == "Reviewer comments":
                        flag("document contains reviewer comments; may hold unresolved decisions")
    ins, dele = len(list(doc.iter(W + "ins"))), len(list(doc.iter(W + "del")))
    if ins or dele:
        flag(f"tracked changes present ({ins} insertions, {dele} deletions). Insertions kept, deletions dropped; confirm intended final text.")
    media = [n for n in names if n.startswith("word/media/")]
    if media:
        flag(f"{len(media)} embedded image(s) not extracted (may hold diagrams/screenshots/text): {', '.join(Path(m).name for m in media)}")
    if any(el.tag.endswith(("}object", "}chart")) for el in doc.iter()):
        flag("embedded objects/charts present and not extracted")
    if extra:
        md += "\n\n---\n\n## Document Furniture (headers, footers, notes, comments)\n\n" + "\n\n".join(extra)
    return md


# ---------------- PDF ----------------
def pdf(path):
    if shutil.which("pdftotext"):
        raw = subprocess.run(["pdftotext", "-layout", str(path), "-"], capture_output=True,
                             text=True, encoding="utf-8", errors="replace").stdout
        pages = raw.split("\f")
        if pages and not pages[-1].strip():
            pages.pop()
    else:
        from pypdf import PdfReader
        pages = [(p.extract_text() or "") for p in PdfReader(str(path)).pages]
        flag("pdftotext unavailable; used pypdf (table layout is likely lost)")
    out = []
    for i, p in enumerate(pages, 1):
        out.append(f"<!-- page {i} -->")
        if len(p.strip()) < 20:
            out.append(flag(f"page {i} has almost no extractable text (scanned/image page?). Needs OCR or manual reading."))
        else:
            out.append(p.rstrip())
            if re.search(r"\S {3,}\S", p):
                flags.append(f"page {i}: column-aligned text, probably a table; reconstruct it")
    flag("PDF extraction is layout-based: reconstruct tables, headings and lists from the raw text; confirm against the PDF where columns are ambiguous")
    return "\n\n".join(out)


if __name__ == "__main__":
    src, dst = Path(sys.argv[1]), Path(sys.argv[2])
    ext = src.suffix.lower()
    if ext == ".docx":
        md = docx(src)
    elif ext == ".pdf":
        md = pdf(src)
    else:
        sys.exit("only .docx and .pdf supported (convert .doc to .docx first)")
    dst.write_text(f"<!-- RAW EXTRACTION of {src.name}: draft, restructure per SKILL.md -->\n\n{md}\n", encoding="utf-8")
    print(json.dumps({"source": src.name, "raw": str(dst), "chars": len(md), "flags": flags}, indent=2, ensure_ascii=False))
