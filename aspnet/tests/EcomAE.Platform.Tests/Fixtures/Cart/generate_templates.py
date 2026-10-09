#!/usr/bin/env python3
"""Generate renderer templates and compact SHA-256 goldens from PHP harness output."""
import hashlib
import json
import pathlib
import re
import sys

golden_path = pathlib.Path(sys.argv[1])
repo = pathlib.Path(sys.argv[2])
payload = json.loads(golden_path.read_text())
rows = {row["name"]: row for row in payload["results"]}
target = repo / "aspnet/src/EcomAE.Platform/Storefront/Templates"
target.mkdir(parents=True, exist_ok=True)


def common(html: str, csrf: str) -> str:
    html = (html
            .replace("[[PRICE_STYLES]]", "{{PRICE_STYLES}}")
            .replace("[[WA_STYLES]]", "{{WA_STYLES}}")
            .replace("[[WA_SCRIPT]]", "{{WA_SCRIPT}}"))
    return html.replace(csrf, "{{CSRF}}") if csrf else html


def garage(html: str) -> str:
    pattern = r'(<select id="garage_auto" class="form-control"><option value="0">\{2100\}</option>).*?(</select>)'
    return re.sub(pattern, r"\1{{GARAGE_OPTIONS}}\2", html, count=1, flags=re.S)


def nonempty(name: str, csrf: str) -> str:
    html = garage(common(rows[name]["html"], csrf))
    html = re.sub(r"(\n\s*)cart_records = \[.*?\];\n", r"\1cart_records = {{CART_JSON}};\n", html, count=1, flags=re.S)
    html = re.sub(
        r"(\t\tif\( sum_total_num > 0 \)\n).*?(\t\t\n\t\t\n        document\.getElementById)",
        r"\1{{CHECKOUT_JS}}\n\2",
        html,
        count=1,
        flags=re.S)
    return html


templates = {
    "CartBlocked.html": common(rows["blocked_visitor_without_session"]["html"], ""),
    "CartGuestEmpty.html": garage(common(rows["guest_empty_with_session"]["html"], "empty-key")),
    "CartSignedEmpty.html": garage(common(rows["signed_in_empty"]["html"], "signed-key")),
    "CartGuest.html": nonempty("guest_cart", "guest-key"),
    "CartSigned.html": nonempty("signed_cart_all_line_types_and_access_write", "signed-key"),
}
templates["CartBlocked.html"] = (templates["CartBlocked.html"]
                                 .replace("/en/login", "{{LOGIN_HREF}}")
                                 .replace("/en/reg", "{{SIGNUP_HREF}}"))
for name, text in templates.items():
    (target / name).write_text(text)

compact = {
    "php": payload["php"],
    "results": [
        {
            "name": row["name"],
            "html_sha256": hashlib.sha256(row["html"].encode()).hexdigest(),
            "html_length": len(row["html"].encode()),
            "cart_after": row["cart_after"],
        }
        for row in payload["results"]
    ],
}
(repo / "aspnet/tests/EcomAE.Platform.Tests/Fixtures/Cart/golden.json").write_text(
    json.dumps(compact, indent=2, ensure_ascii=False) + "\n")
