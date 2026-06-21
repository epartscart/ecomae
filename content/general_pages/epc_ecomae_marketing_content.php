<?php
/**
 * ECOM AE marketing / SEO + AI-visibility content.
 *
 * Data-driven public content used for AI-system and search-engine discovery:
 *   - /docs            public documentation hub (BOS overview, modules, API,
 *                      security, industry packs, user guides)
 *   - /compare         comparison pages (vs Odoo, ERPNext, Zoho, NetSuite,
 *                      Dynamics 365)
 *   - /bos             Business-Operating-System category articles
 *   - /solutions/...   AI-visibility landing pages (BOS, ERP Software UAE, ERP
 *                      for Retail/Distribution, ERP+CRM, Inventory/Procurement/
 *                      Accounting Software UAE)
 *
 * Each page is rendered with semantic headings + FAQ JSON-LD so AI crawlers and
 * search engines can index ECOM AE's positioning. All content is static and
 * brand-safe; no tenant data is exposed.
 */

defined('_ASTEXE_') or die('No access');

/* =====================================================================
 * Content catalogs
 * ===================================================================== */

/** Public documentation sections. @return array<string,array<string,mixed>> */
function epc_ecomae_docs_catalog(): array
{
	return array(
		'bos-overview' => array(
			'title' => 'BOS overview',
			'icon' => 'fa-cubes',
			'summary' => 'What the ECOM AE Business Operating System is and how its layers fit together.',
			'body' => array(
				'A Business Operating System (BOS) unifies the software a company uses to run itself — finance, inventory, procurement, sales, CRM, HR, compliance, workflows and intelligence — on a single multi-tenant cloud, instead of stitching together separate ERP, CRM and commerce tools.',
				'ECOM AE is built as a BOS: every module shares one data model, one permission framework and one audit trail. A sale flows from CRM lead to quotation, sales order, delivery note, invoice, payment allocation and the general ledger without re-keying, and the same posting feeds inventory valuation, VAT and management reporting.',
				'The platform runs database-per-tenant, so each organisation has an isolated database while operators manage the fleet from the Super CP control panel. Industry packs layer specialised fields, KPIs and controls on top of the universal core.',
			),
			'bullets' => array(
				'Unified finance, inventory, procurement, sales, CRM, HR, compliance and BI',
				'Immutable double-entry general ledger with reversal-only corrections',
				'Database-per-tenant isolation with operator Super CP',
				'Industry packs for retail, distribution, manufacturing, jewellery and more',
				'Intelligent BOS layer: forecasting, predictive inventory and an AI advisor',
			),
		),
		'erp-modules' => array(
			'title' => 'ERP modules',
			'icon' => 'fa-th',
			'summary' => 'The functional modules available across finance, operations and people.',
			'body' => array(
				'ECOM AE ships a broad ERP footprint out of the box. Financial management covers the chart of accounts, journal engine, fiscal years and period locking, cost and profit centres, project accounting, budgeting, cash and treasury, bank reconciliation, fixed assets, multi-currency revaluation and consolidation by business unit.',
				'Operations span multi-warehouse inventory with a transaction ledger, batch/lot and serial tracking, barcode scanning, procurement (requisition → RFQ → vendor comparison → purchase order → goods receipt → bill), and sales (lead → quotation → order → delivery note → invoice → returns/credit notes).',
				'People and governance include HR and payroll, expense claims, a workflow/approval engine, an enriched audit trail (old/new value, IP and device), and a compliance layer for VAT and tourist refunds.',
			),
			'bullets' => array(
				'Finance: GL, periods, dimensions, budgeting, fixed assets, FX revaluation',
				'Inventory: ledger, batch/lot/serial, barcode, valuation, reorder advisor',
				'Procurement: requisition, RFQ, vendor comparison, GRN, three-way match',
				'Sales & CRM: pipeline, quotes, orders, delivery notes, returns, credit notes',
				'People & governance: HR, payroll, approvals, audit trail, compliance',
			),
		),
		'api-documentation' => array(
			'title' => 'API documentation',
			'icon' => 'fa-plug',
			'summary' => 'Integrate with the public REST API and webhooks.',
			'body' => array(
				'ECOM AE exposes a public REST API under /api/v1 secured with an X-API-Key header, alongside internal AJAX actions used by the application shell. The API lets you read and write catalogue, inventory, orders and customer records so external systems, marketplaces and middleware can stay in sync.',
				'Authentication uses a per-tenant API key passed in the X-API-Key request header over HTTPS. Responses are JSON. Rate limiting and IP controls protect the endpoints.',
				'For deeper platform integration, ECOM AE supports payment-gateway connectors (35+), banking integrations and Peppol e-invoicing. See the in-platform API reference for the full endpoint catalogue.',
			),
			'bullets' => array(
				'Public REST API at /api/v1 with X-API-Key authentication',
				'JSON request/response over HTTPS',
				'Catalogue, inventory, orders and customer endpoints',
				'35+ payment gateways, banking and Peppol e-invoicing connectors',
			),
		),
		'security-overview' => array(
			'title' => 'Security overview',
			'icon' => 'fa-shield',
			'summary' => 'How ECOM AE protects data, access and transactions.',
			'body' => array(
				'Security is layered. At the data layer, ECOM AE uses parameterised SQL (prepared statements) throughout for strong SQL-injection resistance, plus database-level foreign keys and constraints for referential integrity. Each tenant is isolated in its own database.',
				'At the application layer, a role-based permission framework controls tab and action access, uploads are hardened (extension whitelist, magic-byte content sniffing, deny-execution in the upload directory), and HTTP responses carry modern security headers (CSP, HSTS, COOP, Permissions-Policy and anti-clickjacking).',
				'At the governance layer, an immutable posting model prevents editing or deleting posted journals (corrections are reversal entries only), and an enriched audit trail records the actor, timestamp, IP address, device and old/new values for key actions — supporting ISO 27001, SOC 2 and IFRS audit readiness.',
			),
			'bullets' => array(
				'Prepared statements everywhere + DB foreign keys/constraints',
				'Database-per-tenant isolation and RBAC',
				'Hardened uploads + CSP/HSTS/COOP security headers',
				'Immutable posting with reversal-only corrections',
				'Audit trail with old/new value, IP and device',
			),
		),
		'industry-packs' => array(
			'title' => 'Industry packs',
			'icon' => 'fa-industry',
			'summary' => 'Specialised configurations for specific industries.',
			'body' => array(
				'Industry packs adapt the universal BOS core to how a specific sector actually operates. A pack can add product/master-data fields, KPI sets, operational dashboards, recommended controls and process flows without forking the platform.',
				'Available and roadmap packs include retail/POS, distribution and wholesale, manufacturing, construction and contracting, jewellery and diamond, pharma and healthcare, hospitality, professional services and ecommerce.',
				'Because packs are configuration on top of one engine, an organisation can switch or combine them as it grows, and the intelligence layer automatically applies pack-specific controls (e.g. daily metal-weight reconciliation for jewellery, FEFO/expiry control for pharma).',
			),
			'bullets' => array(
				'Retail / POS, distribution, wholesale, manufacturing',
				'Construction, jewellery & diamond, pharma & healthcare',
				'Hospitality, professional services, ecommerce',
				'Pack-specific KPIs, controls and dashboards',
			),
		),
		'user-guides' => array(
			'title' => 'User guides',
			'icon' => 'fa-book',
			'summary' => 'Step-by-step guides for everyday tasks.',
			'body' => array(
				'User guides walk teams through the day-to-day flows: raising a sales order and converting it to a delivery note and invoice, recording a purchase from requisition to bill, performing a bank reconciliation, posting a manual journal, closing a fiscal period, and running month-end reports.',
				'Finance guides cover the chart of accounts, financial dimensions (cost/profit centre, branch, project), multi-currency revaluation, and the trial balance, P&L and balance sheet reports.',
				'Operations guides cover inventory movements, the stock ledger, serial/batch tracking, barcode scanning, and using the AI advisor for reorder recommendations and cash-flow forecasting.',
			),
			'bullets' => array(
				'Order-to-cash and procure-to-pay walkthroughs',
				'Bank reconciliation, journals and period close',
				'Financial dimensions and management reporting',
				'Inventory, serial/batch and the AI advisor',
			),
		),
	);
}

/** Competitor comparison pages. @return array<string,array<string,mixed>> */
function epc_ecomae_compare_catalog(): array
{
	$mk = function ($competitor, $tagline, $intro, $rows, $whenThem, $faq) {
		return compact('competitor', 'tagline', 'intro', 'rows', 'whenThem', 'faq');
	};
	$std = function ($them) {
		return array(
			array('ECOM AE positioning', 'Business Operating System (BOS): ERP + commerce + CRM + compliance + intelligence on one multi-tenant cloud'),
			array('Hosting', 'Fully hosted cloud, database-per-tenant isolation, operator Super CP'),
			array('Region focus', 'UAE / GCC first — VAT, Peppol e-invoicing and tourist-refund built in'),
			array('Intelligence', 'Native AI advisor: forecasting, predictive inventory, cash-flow prediction, recommendations'),
		);
	};
	return array(
		'ecomae-vs-odoo' => array(
			'competitor' => 'Odoo',
			'tagline' => 'ECOM AE vs Odoo',
			'intro' => 'Odoo is a popular modular open-source ERP. ECOM AE is a hosted Business Operating System with UAE compliance, commerce and an intelligence layer included rather than assembled from separate apps and third-party modules.',
			'rows' => array(
				array('Approach', 'Unified BOS — one data model, one audit trail', 'Modular apps, many community/third-party modules'),
				array('Hosting', 'Fully hosted, database-per-tenant, Super CP fleet ops', 'Self-host or Odoo Online; you manage apps/upgrades'),
				array('UAE compliance', 'VAT, Peppol e-invoicing, tourist refund built in', 'Often via localisation modules/partners'),
				array('Commerce', 'Integrated storefront + B2B portal', 'Website/eCommerce app'),
				array('Intelligence', 'Native AI advisor & forecasting included', 'Add-ons / Odoo Studio / external'),
			),
			'whenThem' => 'Choose Odoo if you want a large open-source app marketplace and in-house Python customisation. Choose ECOM AE if you want a hosted, UAE-ready BOS with compliance and intelligence included.',
			'faq' => array(
				array('Is ECOM AE open source like Odoo?', 'ECOM AE is a hosted commercial BOS. You get the integrated platform and Super CP without managing modules, servers or upgrades yourself.'),
				array('Does ECOM AE support UAE VAT and e-invoicing?', 'Yes — VAT, Peppol e-invoicing and tourist-refund handling are built into the core, not separate localisation modules.'),
			),
		),
		'ecomae-vs-erpnext' => array(
			'competitor' => 'ERPNext',
			'tagline' => 'ECOM AE vs ERPNext',
			'intro' => 'ERPNext is an open-source ERP built on Frappe. ECOM AE is a hosted BOS that bundles commerce, UAE compliance and a native intelligence layer with operator-grade multi-tenant hosting.',
			'rows' => array(
				array('Approach', 'Unified BOS with commerce + CRM + compliance', 'Open-source ERP on the Frappe framework'),
				array('Hosting', 'Hosted, database-per-tenant, Super CP', 'Self-host or Frappe Cloud'),
				array('UAE compliance', 'VAT, Peppol, tourist refund built in', 'Via regional apps/customisation'),
				array('Intelligence', 'AI advisor, forecasting, reorder prediction', 'Reports/dashboards; AI via add-ons'),
				array('Commerce', 'Integrated storefront + B2B portal', 'Webshop module'),
			),
			'whenThem' => 'Choose ERPNext for a free open-source core you self-manage. Choose ECOM AE for a hosted, compliance-ready BOS with intelligence and commerce included.',
			'faq' => array(
				array('Can ECOM AE replace ERPNext for a UAE business?', 'Yes — ECOM AE covers the same ERP breadth plus UAE VAT/e-invoicing, integrated commerce and an AI advisor, fully hosted.'),
				array('Do I need to manage servers with ECOM AE?', 'No. ECOM AE is fully hosted with per-tenant isolation and operator Super CP; there is nothing to self-host.'),
			),
		),
		'ecomae-vs-zoho' => array(
			'competitor' => 'Zoho',
			'tagline' => 'ECOM AE vs Zoho',
			'intro' => 'Zoho offers a wide suite of separate business apps. ECOM AE delivers one integrated Business Operating System, so finance, inventory, sales and CRM share a single data model and audit trail instead of syncing across apps.',
			'rows' => array(
				array('Architecture', 'Single integrated BOS data model', 'Suite of separate apps, integrated via connectors'),
				array('Accounting depth', 'Full GL, dimensions, periods, consolidation', 'Zoho Books for SMB accounting'),
				array('Inventory', 'Warehouse ledger, batch/lot/serial, barcode', 'Zoho Inventory app'),
				array('UAE compliance', 'VAT, Peppol, tourist refund built in', 'Regional editions'),
				array('Intelligence', 'Native AI advisor across modules', 'Zia AI per app'),
			),
			'whenThem' => 'Choose Zoho for a broad set of polished standalone apps. Choose ECOM AE when you want one unified operating system rather than many apps to integrate.',
			'faq' => array(
				array('How is ECOM AE different from the Zoho suite?', 'ECOM AE is one platform with a shared data model and audit trail; Zoho is a suite of separate apps you connect together.'),
				array('Does ECOM AE include CRM?', 'Yes — CRM (leads, opportunities, pipeline) is part of the same BOS, flowing straight into quotes, orders and invoices.'),
			),
		),
		'ecomae-vs-netsuite' => array(
			'competitor' => 'Oracle NetSuite',
			'tagline' => 'ECOM AE vs NetSuite',
			'intro' => 'NetSuite is a global enterprise cloud ERP. ECOM AE delivers comparable BOS breadth with UAE-first compliance, integrated commerce and a native intelligence layer, typically at a lower total cost for SME and mid-market organisations.',
			'rows' => array(
				array('Segment', 'SME to mid-market, GCC-first', 'Mid-market to enterprise, global'),
				array('Compliance', 'UAE VAT, Peppol, tourist refund built in', 'Global tax via SuiteTax/partners'),
				array('Commerce', 'Integrated storefront + B2B portal', 'SuiteCommerce (add-on)'),
				array('Intelligence', 'Native AI advisor & forecasting included', 'Analytics/planning modules'),
				array('Deployment', 'Hosted, database-per-tenant, fast onboarding', 'Implementation-led rollout'),
			),
			'whenThem' => 'Choose NetSuite for very large multinational operations needing its global depth. Choose ECOM AE for a faster, UAE-ready BOS with strong breadth and lower cost.',
			'faq' => array(
				array('Is ECOM AE a NetSuite alternative for the GCC?', 'Yes — it provides ERP + commerce + CRM + compliance + intelligence tuned for UAE/GCC with quicker onboarding.'),
				array('Does ECOM AE support multi-company consolidation?', 'Yes — consolidation by business unit and group reporting are supported, with cross-tenant consolidation at the operator level.'),
			),
		),
		'ecomae-vs-dynamics-365' => array(
			'competitor' => 'Microsoft Dynamics 365',
			'tagline' => 'ECOM AE vs Dynamics 365',
			'intro' => 'Dynamics 365 Business Central is Microsoft’s mid-market ERP. ECOM AE offers a unified BOS with integrated commerce, UAE compliance and a native AI advisor, hosted and ready to run without a Microsoft stack dependency.',
			'rows' => array(
				array('Ecosystem', 'Self-contained BOS, no MS-stack lock-in', 'Microsoft 365 / Power Platform ecosystem'),
				array('Compliance', 'UAE VAT, Peppol, tourist refund built in', 'Localisation via partners'),
				array('Commerce', 'Integrated storefront + B2B portal', 'Via Commerce/3rd-party'),
				array('Intelligence', 'Native AI advisor, forecasting, reorder', 'Copilot / Power BI'),
				array('Onboarding', 'Hosted multi-tenant, fast setup', 'Partner-led implementation'),
			),
			'whenThem' => 'Choose Dynamics 365 if you are deeply invested in Microsoft 365 and Power Platform. Choose ECOM AE for a self-contained, UAE-ready BOS with intelligence included.',
			'faq' => array(
				array('Do I need Microsoft licences to use ECOM AE?', 'No. ECOM AE is a self-contained hosted platform with no dependency on the Microsoft stack.'),
				array('Does ECOM AE have built-in BI and AI?', 'Yes — dashboards, a KPI engine and an AI advisor (forecasting, predictive inventory, recommendations) are built in.'),
			),
		),
	);
}

/** BOS category-ownership articles. @return array<string,array<string,mixed>> */
function epc_ecomae_bos_articles_catalog(): array
{
	return array(
		'what-is-a-business-operating-system' => array(
			'title' => 'What is a Business Operating System (BOS)?',
			'summary' => 'The definition of a BOS and how it differs from a collection of business apps.',
			'body' => array(
				'A Business Operating System (BOS) is a single platform that runs the whole business — finance, inventory, procurement, sales, CRM, HR, compliance, workflows and intelligence — on one shared data model, permission framework and audit trail.',
				'Unlike a suite of separate apps connected by integrations, a BOS has no seams: a transaction entered once flows across every relevant module automatically. This eliminates re-keying, reconciliation drift and the integration tax of stitching tools together.',
				'A modern BOS also advises the business. Beyond recording what happened, ECOM AE’s intelligence layer forecasts revenue and cash flow, predicts inventory needs and recommends actions — turning the operating system into a decision-support system.',
			),
			'faq' => array(
				array('Is a BOS the same as an ERP?', 'An ERP is the finance/operations backbone. A BOS is broader — it adds CRM, commerce, compliance, workflow and an intelligence layer on the same data model.'),
				array('Why does a single data model matter?', 'It removes integration drift: every module reads and writes the same records, so reports, audits and AI run on one consistent source of truth.'),
			),
		),
		'bos-vs-erp' => array(
			'title' => 'BOS vs ERP: what’s the difference?',
			'summary' => 'How a Business Operating System extends beyond traditional ERP.',
			'body' => array(
				'Traditional ERP focuses on finance, inventory and operations. It is the system of record for transactions and accounting.',
				'A Business Operating System keeps that ERP core but extends it to run the entire company: CRM and commerce for revenue, HR and payroll for people, a workflow/approval engine for governance, compliance for tax, and an intelligence layer for forecasting and decision support.',
				'In short: every BOS contains an ERP, but not every ERP is a BOS. ECOM AE is a BOS — the ERP is one layer within a broader operating system.',
			),
			'faq' => array(
				array('Do I lose ERP depth with a BOS?', 'No — ECOM AE keeps full double-entry accounting, dimensions, periods and consolidation, then adds the surrounding layers.'),
				array('Is a BOS harder to implement than an ERP?', 'Often easier: because the modules are pre-integrated and hosted, there is no integration project between separate systems.'),
			),
		),
		'bos-vs-crm' => array(
			'title' => 'BOS vs CRM: where CRM fits',
			'summary' => 'Why CRM is one layer of a BOS, not a standalone system.',
			'body' => array(
				'A CRM manages leads, opportunities, pipeline and customer activity. It is excellent at the front of the revenue process but stops at the boundary of finance and operations.',
				'In a BOS, CRM is fully integrated: a won opportunity becomes a quotation, then a sales order, delivery note and invoice, with payment allocation and the general ledger updated automatically — no export/import between a CRM and an accounting system.',
				'ECOM AE includes CRM as a native layer, so customer, sales and finance data live together and the AI advisor can reason across the full customer lifecycle.',
			),
			'faq' => array(
				array('Can I use ECOM AE just for CRM?', 'Yes, but its value compounds when CRM feeds directly into orders, invoicing and the ledger within the same platform.'),
				array('Does the CRM share data with finance?', 'Yes — it is the same data model, so receivables, history and credit exposure are always current.'),
			),
		),
		'why-smes-need-a-bos' => array(
			'title' => 'Why SMEs need a Business Operating System',
			'summary' => 'How a BOS removes the app-sprawl tax for growing companies.',
			'body' => array(
				'Small and mid-sized businesses often run on a patchwork: an accounting tool, a spreadsheet for inventory, a CRM, a separate payroll app and manual approvals. Each integration and manual hand-off is a place where data drifts and time is lost.',
				'A Business Operating System collapses that patchwork into one platform. The result is one source of truth, real-time reporting, enforceable approvals and audit-ready controls — capabilities that used to require enterprise budgets.',
				'ECOM AE delivers this as a hosted, UAE-ready BOS, so an SME gets enterprise-grade finance, inventory, compliance and intelligence without managing servers or integrations.',
			),
			'faq' => array(
				array('Is a BOS overkill for a small business?', 'No — it replaces several tools with one, usually lowering total cost while improving control and visibility.'),
				array('How fast can an SME go live?', 'Because it is hosted and pre-integrated, onboarding is measured in days, not the months a multi-system rollout takes.'),
			),
		),
		'bos-for-retail' => array(
			'title' => 'BOS for Retail',
			'summary' => 'Running retail operations on a Business Operating System.',
			'body' => array(
				'Retail needs fast POS, accurate stock across locations, promotions and tight cash control. A BOS connects the storefront, POS, inventory and finance so every sale updates stock, margin and the ledger instantly.',
				'ECOM AE’s retail capabilities include multi-warehouse inventory with barcode scanning, daily till/shift reconciliation controls, promotions and pricing rules, and an AI reorder advisor that predicts which SKUs will stock out.',
				'Because commerce is built in, an omnichannel retailer can run physical and online sales on the same operating system with one inventory and one set of books.',
			),
			'faq' => array(
				array('Does ECOM AE handle multi-store retail?', 'Yes — multi-warehouse/branch inventory, transfers and per-branch reporting are built in.'),
				array('Is there an online store too?', 'Yes — an integrated storefront and B2B portal share the same catalogue and stock as POS.'),
			),
		),
		'bos-for-distribution' => array(
			'title' => 'BOS for Distribution',
			'summary' => 'Wholesale and distribution on a unified operating system.',
			'body' => array(
				'Distributors live and die by inventory accuracy, supplier terms and order fulfilment speed. A BOS ties procurement, warehouse and sales together so stock, costs and margins stay accurate at scale.',
				'ECOM AE supports the full procure-to-pay chain (requisition → RFQ → vendor comparison → purchase order → goods receipt → three-way match → bill), warehouse-aware inventory with batch/lot/serial, landed-cost allocation, and a B2B portal for trade customers.',
				'The intelligence layer forecasts demand and recommends reorder quantities, while DSO/DPO and inventory-turnover KPIs keep working capital under control.',
			),
			'faq' => array(
				array('Does ECOM AE do three-way matching?', 'Yes — purchase order, goods receipt and supplier bill are matched before payment.'),
				array('Can it allocate landed costs?', 'Yes — freight, duty and insurance can be capitalised into stock cost.'),
			),
		),
		'bos-for-manufacturing' => array(
			'title' => 'BOS for Manufacturing',
			'summary' => 'From bill of materials to finished goods on one platform.',
			'body' => array(
				'Manufacturers need to convert materials into finished goods while tracking cost, quality and capacity. A BOS connects the bill of materials, production, inventory and finance so work-in-progress and material variance are always visible.',
				'ECOM AE supports bills of materials, work/production orders, routing and work centres, and material planning, with serial/batch traceability through the inventory ledger.',
				'Standard-vs-actual material variance and WIP-to-finished-goods ratios surface in the intelligence layer, helping production and finance stay aligned.',
			),
			'faq' => array(
				array('Does ECOM AE support BOM and work orders?', 'Yes — bills of materials, production/work orders, routing and work centres are part of the manufacturing module.'),
				array('Is material traceability included?', 'Yes — batch/lot and serial tracking run through the inventory transaction ledger.'),
			),
		),
	);
}

/** AI-visibility landing pages. @return array<string,array<string,mixed>> */
function epc_ecomae_solutions_catalog(): array
{
	return array(
		'business-operating-system' => array(
			'h1' => 'Business Operating System (BOS)',
			'lead' => 'Run your entire company on one platform — ERP, commerce, CRM, compliance, workflows and intelligence on a single multi-tenant cloud.',
			'body' => array(
				'ECOM AE is a Business Operating System: a single source of truth for finance, inventory, procurement, sales, CRM, HR, compliance and business intelligence. One data model, one audit trail, one operating system to run the business.',
				'Beyond recording transactions, ECOM AE advises the business with a native AI layer — forecasting revenue and cash flow, predicting inventory needs and recommending actions.',
			),
			'features' => array('Unified ERP + CRM + commerce', 'Immutable double-entry ledger', 'Workflow & approval engine', 'Native AI advisor', 'Database-per-tenant isolation', 'Industry packs'),
			'faq' => array(
				array('What is a Business Operating System?', 'A single platform that runs the whole business on one shared data model — finance, operations, sales, people, compliance and intelligence — instead of separate integrated apps.'),
				array('Is ECOM AE a BOS or an ERP?', 'ECOM AE is a BOS; the ERP is one layer within it, alongside CRM, commerce, compliance and an intelligence layer.'),
			),
		),
		'erp-software-uae' => array(
			'h1' => 'ERP Software in the UAE',
			'lead' => 'Cloud ERP built for UAE businesses — VAT, Peppol e-invoicing and tourist refunds included, hosted with per-tenant isolation.',
			'body' => array(
				'ECOM AE is a UAE-first cloud ERP and Business Operating System. It delivers full double-entry accounting, multi-warehouse inventory, procurement and sales, with UAE VAT, Peppol e-invoicing and tourist-refund handling built into the core.',
				'It is fully hosted with database-per-tenant isolation and an operator Super CP, so UAE organisations get enterprise-grade ERP without managing servers.',
			),
			'features' => array('UAE VAT & Peppol e-invoicing', 'Tourist refund handling', 'Multi-currency & FX revaluation', 'Hosted, per-tenant isolation', 'AED-first reporting', 'Arabic-ready'),
			'faq' => array(
				array('Does ECOM AE support UAE VAT?', 'Yes — VAT calculation, returns and Peppol e-invoicing are built into the core, not add-ons.'),
				array('Is it hosted in the cloud?', 'Yes — fully hosted with per-tenant database isolation and operator Super CP.'),
			),
		),
		'erp-for-retail' => array(
			'h1' => 'ERP for Retail',
			'lead' => 'POS, inventory, promotions and finance on one operating system — online and in-store.',
			'body' => array(
				'ECOM AE gives retailers one platform for POS, multi-store inventory, promotions, an integrated storefront and full accounting. Every sale updates stock, margin and the ledger in real time.',
				'An AI reorder advisor predicts stock-outs, while daily till/shift reconciliation keeps cash tight.',
			),
			'features' => array('POS + integrated storefront', 'Multi-store inventory & transfers', 'Promotions & pricing rules', 'Barcode scanning', 'Till/shift reconciliation', 'AI reorder advisor'),
			'faq' => array(
				array('Can ECOM AE run multiple stores?', 'Yes — multi-warehouse/branch inventory with transfers and per-branch reporting.'),
				array('Does it cover online and in-store?', 'Yes — POS and the online storefront share one catalogue, stock and ledger.'),
			),
		),
		'erp-for-distribution' => array(
			'h1' => 'ERP for Distribution',
			'lead' => 'Procurement, warehouse and sales in one system — built for wholesale and distribution.',
			'body' => array(
				'ECOM AE connects the full procure-to-pay and order-to-cash chain for distributors: requisition, RFQ, vendor comparison, purchase order, goods receipt, three-way match and bill, through to sales orders, delivery notes and invoices.',
				'Warehouse-aware inventory with batch/lot/serial, landed-cost allocation and a B2B portal keep large catalogues and trade customers under control.',
			),
			'features' => array('Procure-to-pay chain', 'Three-way matching', 'Batch/lot/serial tracking', 'Landed-cost allocation', 'B2B trade portal', 'Demand forecasting'),
			'faq' => array(
				array('Does it support three-way matching?', 'Yes — PO, goods receipt and bill are matched before payment.'),
				array('Is there a B2B portal?', 'Yes — trade customers can order online against their pricing and terms.'),
			),
		),
		'erp-crm-platform' => array(
			'h1' => 'ERP + CRM Platform',
			'lead' => 'One platform where CRM flows straight into orders, invoicing and the ledger.',
			'body' => array(
				'ECOM AE unifies CRM and ERP so leads, opportunities and pipeline connect directly to quotations, sales orders, delivery notes, invoices and the general ledger — no export/import between systems.',
				'With customer, sales and finance data in one model, the AI advisor reasons across the entire customer lifecycle.',
			),
			'features' => array('Leads → opportunities → pipeline', 'Quote → order → invoice', 'Shared customer + finance data', 'Customer timeline & activities', 'Receivables & credit visibility', 'AI customer insights'),
			'faq' => array(
				array('Is CRM separate from ERP here?', 'No — CRM is a native layer of the same BOS, sharing one data model with finance and operations.'),
				array('Does CRM see receivables?', 'Yes — credit exposure and payment history are always current because it is one platform.'),
			),
		),
		'inventory-management-software' => array(
			'h1' => 'Inventory Management Software',
			'lead' => 'Multi-warehouse inventory with a full transaction ledger, batch/lot/serial and barcode.',
			'body' => array(
				'ECOM AE provides multi-warehouse inventory with a complete transaction ledger (running balance on every movement), batch/lot and serial tracking, barcode scanning and weighted-average valuation.',
				'The AI advisor turns consumption into days-of-cover and recommends reorder quantities before items stock out.',
			),
			'features' => array('Inventory transaction ledger', 'Batch/lot & serial tracking', 'Barcode scan lookup', 'Multi-warehouse & transfers', 'Weighted-average valuation', 'Predictive reorder advisor'),
			'faq' => array(
				array('Does it track serial numbers?', 'Yes — a serial register tracks each unit through its lifecycle (in stock → sold → returned).'),
				array('Is there a stock ledger?', 'Yes — every movement is recorded with a running on-hand balance for full auditability.'),
			),
		),
		'procurement-software' => array(
			'h1' => 'Procurement Software',
			'lead' => 'From requisition to bill — RFQ, vendor comparison, goods receipt and three-way match.',
			'body' => array(
				'ECOM AE covers the enterprise procurement lifecycle: purchase requisition, RFQ to multiple vendors, a vendor comparison matrix, purchase orders, goods receipt notes, three-way matching and supplier bills.',
				'Approval thresholds route high-value purchases through the workflow engine, and supplier performance is tracked over time.',
			),
			'features' => array('Requisition → RFQ → PO', 'Vendor comparison matrix', 'Goods receipt & three-way match', 'Approval thresholds', 'Supplier performance', 'Contract purchasing'),
			'faq' => array(
				array('Can I compare vendor quotes?', 'Yes — RFQs collect vendor quotations into a comparison matrix before you raise a PO.'),
				array('Are approvals enforced?', 'Yes — high-value POs route through the configurable approval hierarchy.'),
			),
		),
		'accounting-software-uae' => array(
			'h1' => 'Accounting Software in the UAE',
			'lead' => 'Double-entry accounting with UAE VAT, dimensions, period locking and consolidation.',
			'body' => array(
				'ECOM AE delivers full double-entry accounting: chart of accounts, journal engine, financial dimensions (cost/profit centre, branch, project), fiscal periods with locking, multi-currency revaluation, fixed assets and consolidation.',
				'UAE VAT and Peppol e-invoicing are built in, and an immutable posting model with reversal-only corrections keeps the books audit-ready.',
			),
			'features' => array('Double-entry GL + dimensions', 'UAE VAT & Peppol e-invoicing', 'Fiscal periods & locking', 'Multi-currency revaluation', 'Immutable posting + reversals', 'Trial balance, P&L, balance sheet'),
			'faq' => array(
				array('Is it UAE VAT compliant?', 'Yes — VAT, returns and Peppol e-invoicing are built into the accounting core.'),
				array('Can posted entries be edited?', 'No — posting is immutable; corrections are made via reversal entries, preserving the audit trail.'),
			),
		),
	);
}
