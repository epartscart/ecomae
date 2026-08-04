#!/usr/bin/env python3
"""Fail if product Blazor chrome still advertises PHP/ASP.NET/cutover look gaps."""
from __future__ import annotations
import json, re, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
PAGES=ROOT/"aspnet/src/EcomAE.Platform/Components"
SKIP_FILES={"MigrationCompareConsole.razor","ZeroPhpConsole.razor","ErpOnPremisesApp.razor"}
FORBIDDEN=re.compile(
    r"Open PHP|Hybrid workspace · PHP|JSON digest|Not a broad|/cp cutover|/erp cutover|/bos cutover|"
    r"same-to-same|ASP\.NET preview|Target: 100% ASP\.NET|title=\"Open PHP|title=\"Open live PHP|>PHP</a>|"
    r"PHP authoritative|Hybrid CP chrome|Hybrid ERP chrome|Hybrid chrome|PHP \$_SESSION|"
    r"Live storefront remains on PHP|modex chrome hybrid|"
    r">PHP CP<|>PHP ERP<|>PHP BOS<|Prefer PHP login|PHP-matching|from PHP |"
    r"remain on PHP|stay on PHP|Tab bodies stay PHP|Live PHP |Open full PHP |"
    r"Provisioning stays on PHP|intentional cutover|"
    r"Cookie bridge|hybrid digests only|"
    r"PHP parity preview|>PHP [A-Za-z]|Edit in PHP|"
    r"more reliable than PHP|PHP hub orbit|Append-only PHP",
    re.I,
)
hits=[]
for path in sorted(PAGES.rglob("*.razor")):
    if path.name in SKIP_FILES or path.name.startswith("Migration") or path.name.startswith("ZeroPhp"):
        continue
    text=path.read_text(encoding="utf-8")
    markup=text.split("@code",1)[0]
    clean=re.sub(r"@\*.*?\*@", "", markup, flags=re.S)
    for i,line in enumerate(clean.splitlines(),1):
        if FORBIDDEN.search(line):
            # map approx line in clean; store path + content
            hits.append(f"{path.relative_to(ROOT)}:{line.strip()[:160]}")
out={
    "role":"same-to-same-look-gap-floor",
    "cutoverAllowed":False,
    "readyForPhpRemoval":False,
    "aspNetInteractiveComplete":0,
    "forbiddenHits":len(hits),
    "hits":hits[:80],
    "note":"Product CP/ERP/BOS/storefront chrome must not advertise stack/cutover. Operator consoles excluded.",
}
out_path=ROOT/"docs/migration/evidence/presentation/same-to-same-look-gap-floor.json"
out_path.parent.mkdir(parents=True, exist_ok=True)
out_path.write_text(json.dumps(out, indent=2)+"\n", encoding="utf-8")
print(json.dumps({"ok": len(hits)==0, "hits": len(hits), "out": str(out_path)}, indent=2))
if hits:
    for h in hits[:60]:
        print(h)
    sys.exit(1)
