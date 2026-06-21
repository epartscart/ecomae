# ecomae ERP — Roadmap, Capability Matrix & Deployment Checklist

Industry-agnostic ERP + CRM + e-commerce, multi-tenant, worldwide tax, with
SAP / Oracle / Dynamics 365-class functionality — additive on top of the
existing ecomae platform, tenant-isolated and entitlement-gated.

**Test status:** 840 automated tests passing, 0 failing (run
`bash tests/erp_advanced/run_all.sh`).

---

## 1. Enterprise capability matrix (our module → big-ERP equivalent)

| Capability (ecomae) | SAP | Oracle | Dynamics 365 | Status |
|---|---|---|---|---|
| General Ledger, multi-currency, year-end | FI | GL / Fusion | Finance | ✓ |
| Consolidation + intercompany elimination | FI-CO / EC-CS | FCCS | Finance | ✓ |
| Cost accounting, rebates, budgeting | CO | Costing | Finance | ✓ |
| Fixed assets + depreciation + disposal | FI-AA | Assets | Finance (FA) | ✓ |
| Enterprise asset maintenance | PM / EAM | EAM | Asset Mgmt | ✓ |
| Procurement / purchasing | MM | Procurement | SCM (Procure) | ✓ |
| Supply chain, landed cost, customs | MM / GTS | SCM / GTM | SCM | ✓ |
| MRP / demand planning + ATP | PP-MRP / GATP | Planning | SCM (Planning) | ✓ |
| Sales & distribution (CRM→SO→DO→Invoice) | SD | Order Mgmt | Sales | ✓ |
| Pricing, promotions, loyalty | SD pricing | Pricing | Commerce | ✓ |
| Manufacturing (BOM, WO, WIP, FG) + QC | PP / QM | Manufacturing | SCM | ✓ |
| Projects, contracts, progress claims | PS | Projects | Project Ops | ✓ |
| Treasury, daily cash, bank reconciliation | TR / FSCM | Cash Mgmt | Finance | ✓ |
| Credit control & collections | FSCM Credit | Credit | Finance | ✓ |
| HR, payroll, leave, expense | HCM | HCM | Human Resources | ✓ |
| Tax/VAT, e-invoice, corporate tax | DRC | Tax | Finance (Tax) | ✓ |
| Live country compliance engine (FTA) | DRC feeds | — | RCS | ✓ (engine; live feed needs authority keys) |
| Governance, roles, approvals, audit | GRC | RMC | Security/Workflow | ✓ |
| Integration / API / event bus | PI-PO / BTP | OIC | Dataverse | ✓ (framework; live certs per provider) |
| Analytics / dashboards | SAC | OAC | Power BI | ✓ |
| Data migration | Migration Cockpit | FBDI | Data Mgmt | ✓ |
| Company profile, letterhead, SOA | — | — | — | ✓ |
| Provider / multi-tenant fleet control | — | — | — | ✓ |
| World-language + RTL/LTR | multi-lang | NLS | multi-lang | ✓ (~120 languages + Google fallback) |
| Mobile (Android/iOS) | Fiori mobile | mobile | mobile | ✓ (PWA + Capacitor wrapper) |

**Honest caveats (need third-party credentials to go fully live):**
- Regulator endpoints (FTA, ZATCA, customs): need the authority's API URL + certs.
- Payment/bank/shipping connectors: need each provider's sandbox keys.
- App-store publishing: needs Apple Developer + Google Play accounts.

The code/engine is built and tested; it activates when those keys are provided.

---

## 2. Module / feature matrix

Every module is **multi-tenant** (separate DB per tenant), **entitlement-gated**
(a payroll-only client sees only payroll), audited, and dashboard-enabled.

| Module | CRUD | Reporting | Dashboard | Audit | Approval | Tests |
|---|---|---|---|---|---|---|
| Finance / GL | ✓ | ✓ | ✓ | ✓ | ✓ | run_finance_tests |
| Inventory + ageing/turnover | ✓ | ✓ | ✓ | ✓ | ✓ | run_ops_tests |
| CRM + pipeline | ✓ | ✓ | ✓ | ✓ | ✓ | run_tests |
| Procurement / SCM / customs | ✓ | ✓ | ✓ | ✓ | ✓ | run_scm_tests, run_customs_integration_tests |
| MRP / ATP / intercompany | ✓ | ✓ | ✓ | ✓ | — | run_enterprise_tests |
| Manufacturing / projects / pricing | ✓ | ✓ | ✓ | ✓ | ✓ | run_projects_pricing_tests |
| Treasury / cash / bank rec | ✓ | ✓ | ✓ | ✓ | ✓ | run_treasury_tests |
| Cost accounting / rebates / EAM | ✓ | ✓ | ✓ | ✓ | ✓ | run_costing_tests |
| HR / payroll / leave / expense | ✓ | ✓ | ✓ | ✓ | ✓ | run_hr_tests |
| Tax / e-invoice / compliance | ✓ | ✓ | ✓ | ✓ | ✓ | run_compliance_tests |
| Org / closing / consolidation | ✓ | ✓ | ✓ | ✓ | ✓ | run_org_closing_tests |
| Industry packs + currency | ✓ | ✓ | ✓ | ✓ | — | run_industry_currency_tests |
| E-commerce ↔ ERP bridge | ✓ | ✓ | ✓ | ✓ | — | run_ecommerce_tests |
| Live data-link (storefront→ERP) | ✓ | ✓ | ✓ | ✓ | — | run_datalink_tests |
| Company profile / letterhead / SOA | ✓ | ✓ | — | ✓ | — | run_company_tests |
| Control tiers / governance / roles | ✓ | ✓ | ✓ | ✓ | ✓ | run_control_tests, run_governance_tests |
| Migration toolkit | ✓ | ✓ | — | ✓ | ✓ | run_migration_tests |
| Integration / API / events | ✓ | ✓ | — | ✓ | — | run_platform_tests |
| Dashboards (KPIs + charts) | — | ✓ | ✓ | — | — | run_dashboard_tests |
| Document flows (per industry) | ✓ | ✓ | — | ✓ | ✓ | run_flow_tests |
| Step-by-step guide content | — | — | — | — | — | run_guide_tests |
| World-language + RTL | — | — | — | — | — | run_i18n_tests |
| Demo / sample data | — | ✓ | ✓ | — | — | run_demo_tests |
| Mobile / PWA | — | — | — | — | — | run_pwa_tests |
| CP render-safety lint | — | — | — | — | — | run_cp_lint |

---

## 3. Per-industry document workflows (which document at which level)

Driven by the in-app guide (`shop/finance/erp/guide`) + `epc_flow_registry()`.

- **Jewellery:** PR → LPO/PO (gold rate, weight, purity) → GRN (weigh+assay) →
  Bill → Payment → Job/Making Order (karigar, WIP) → Hallmark/QC →
  Quotation → SO → DO → Tax Invoice → Receipt → (buy-back / exchange).
- **Trading / import:** PR → RFQ → PO → LC → Bill of Entry/Customs → GRN →
  Landed Cost → Purchase Invoice → SO → DO → Tax Invoice → Receipt.
- **Construction:** BOQ → Contract → Subcontract/PO → Material Requisition →
  GRN → Progress Claim (IPC) → Retention → Certification → Tax Invoice → Receipt.
- **Retail / POS:** Shift Open → POS Sale → Tender → Receipt → Z-Report/Day-Close
  → Stock Replenishment.
- **Manufacturing:** Forecast → BOM → Work Order → Material Issue → WIP →
  FG Receipt → QC → SO → DO → Tax Invoice → Receipt.

Each step lists who prepares it and its accounting impact.

---

## 4. Surfaces & UI

- **Marketing site (ecomae.com):** value props, industry packs, live-demo data.
- **Super CP (operator):** themed provider console — fleet overview, provision/
  suspend/re-plan, compliance push (per-tenant DB; no cross-tenant data).
- **Tenant CP:** entitlement-aware menu surfacing ERP modules + guide; same theme.
- **ERP:** animated crypto/fintech login + dashboard (count-up KPIs, Chart.js).
- **All four** share the i18n + RTL/LTR layer and are PWA-installable.

---

## 5. Deployment checklist (safe, additive)

1. **Review the PR** (`#2`) — all changes are additive (new files + `CREATE TABLE
   IF NOT EXISTS`); no existing tables altered, no live CP core rewritten.
2. **Run tests:** `bash tests/erp_advanced/run_all.sh` → expect 840 passing.
3. **Per-tenant rollout** — enable modules via entitlements per tenant; start with
   a non-critical tenant (never epartscart.com, which is off-limits).
4. **CP render-safety** — `php tests/erp_advanced/run_cp_lint.php` must pass
   (guards the dp_core eval HTTP 500 class of bug).
5. **Storefront theme** — opt-in per tenant flag; preview before enabling (shared
   storefront code is never auto-modified).
6. **Mobile** — enable PWA per tenant; native app store submission needs your
   Apple/Google accounts.
7. **Compliance/integration live feeds** — provide authority/provider keys to
   activate FTA/ZATCA/customs/payment connectors.
8. **Isolation invariant** — every tenant uses its own DB; the provider console
   connects one tenant at a time. No shared cross-tenant database.

---

## 6. Next / optional

- Connect live regulator + payment/bank/shipping endpoints (needs credentials).
- Publish native apps to Play Store / App Store (needs developer accounts).
- Expand curated translations beyond the current built-in set (Google Translate
  already covers the rest).
- Per-tenant storefront theme rollout after preview sign-off.
