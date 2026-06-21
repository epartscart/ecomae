<?php
/**
 * UAE FTA tax knowledge base — VAT, Excise, Corporate Tax, registration & filing.
 * Source reference: https://tax.gov.ae (Federal Tax Authority). ERP operational notes included.
 */
defined('_ASTEXE_') or die('No access');

/** FTA portal entry points for fetch + staff links. */
function epc_uae_fta_portal_urls(): array
{
	return array(
		'home' => 'https://tax.gov.ae/en/',
		'emaratax' => 'https://eservices.tax.gov.ae/',
		'legislation' => 'https://tax.gov.ae/en/legislation.aspx',
		'legislation_archive' => 'https://tax.gov.ae/en/Legislation.archive.aspx',
		'vat' => 'https://tax.gov.ae/en/taxes/vat.aspx',
		'excise' => 'https://tax.gov.ae/en/taxes/excise.tax.aspx',
		'corporate_tax' => 'https://tax.gov.ae/en/taxes/corporate.tax.aspx',
		'vat_guides' => 'https://tax.gov.ae/en/taxes/vat/vat.topics.aspx',
		'ct_guides' => 'https://tax.gov.ae/en/taxes/corporate.tax/corporate.tax.topics.aspx',
		'excise_guides' => 'https://tax.gov.ae/en/taxes/excise.tax/excise.tax.topics.aspx',
		'public_clarifications' => 'https://tax.gov.ae/en/taxes/vat/public.clarifications.aspx',
		'elearning' => 'https://tax.gov.ae/en/elearning.aspx',
	);
}

/**
 * Structured knowledge articles (registration, filing, formats, ERP mapping).
 *
 * @return array<string, array>
 */
function epc_uae_tax_knowledge_catalog(): array
{
	$fta = epc_uae_fta_portal_urls();
	return array(
		'overview' => array(
			'title' => 'UAE tax system overview',
			'tax_type' => 'general',
			'topic' => 'overview',
			'summary' => 'Federal taxes administered by FTA: VAT (5%), Excise (select goods), Corporate Tax (9% on taxable income). Services via EmaraTax / UAE Pass.',
			'steps' => array(
				'Register on EmaraTax (UAE Pass) for VAT, Excise, and/or Corporate Tax as applicable.',
				'Maintain TRN, tax invoices, and records per FTA record-keeping rules.',
				'File periodic returns and pay tax due by published deadlines.',
				'Use ERP Tax compliance tab for operational VAT/CT; official filing on FTA portal.',
			),
			'fta_links' => array(
				array('label' => 'FTA home', 'url' => $fta['home']),
				array('label' => 'EmaraTax e-Services', 'url' => $fta['emaratax']),
			),
			'erp_notes' => array(
				'Finance → Tax compliance: guide, KPIs, FTA update fetch.',
				'Finance → UAE VAT return: output/input/net for the period.',
				'Insights → P&L: Corporate Tax provision estimate.',
			),
		),
		'vat_registration' => array(
			'title' => 'VAT registration process',
			'tax_type' => 'vat',
			'topic' => 'registration',
			'summary' => 'Mandatory registration when taxable supplies exceed AED 375,000 (mandatory threshold) or voluntary from AED 187,500. Apply via EmaraTax with trade licence, bank details, and turnover evidence.',
			'steps' => array(
				'Create UAE Pass account (required for FTA services).',
				'Log in to EmaraTax → Register for VAT.',
				'Submit legal entity details, trade licence, authorised signatory, bank IBAN, contact details.',
				'Declare expected turnover and supply categories; attach supporting documents if requested.',
				'Receive TRN (15-digit Tax Registration Number) upon approval.',
				'Configure seller TRN in ERP E-Invoicing settings before issuing tax invoices.',
			),
			'fta_links' => array(
				array('label' => 'VAT hub', 'url' => $fta['vat']),
				array('label' => 'VAT topics & guides', 'url' => $fta['vat_guides']),
			),
			'erp_notes' => array(
				'E-Invoicing → Seller profile: seller TRN, legal name, address, emirate, Peppol endpoint.',
				'Buyer profiles: B2B UAE customers need TRN on tax invoices.',
			),
		),
		'vat_return_filing' => array(
			'title' => 'VAT return filing process',
			'tax_type' => 'vat',
			'topic' => 'filing',
			'summary' => 'Standard VAT return periods (monthly/quarterly per FTA assignment). Report output tax, input tax, adjustments, and net payable/recoverable on EmaraTax.',
			'steps' => array(
				'Reconcile ERP UAE VAT return (sales output, purchase input, advance VAT) with GL accounts 2100/1150.',
				'Include output VAT on taxable supplies, zero-rated exports (Box adjustments), and exempt supplies as required.',
				'Claim recoverable input VAT only on valid tax invoices from UAE VAT-registered suppliers.',
				'Account for VAT on advance payments in the period received; credit on final tax invoice period.',
				'Log in to EmaraTax → VAT returns → select period → complete boxes → submit and pay if net payable.',
				'Keep tax invoices, credit notes, customs docs (exports) for audit (minimum record period per FTA).',
			),
			'fta_links' => array(
				array('label' => 'VAT refunds guidance', 'url' => $fta['vat']),
				array('label' => 'Public clarifications', 'url' => $fta['public_clarifications']),
			),
			'erp_notes' => array(
				'Finance → UAE VAT return tab: operational calculation for the selected date range.',
				'Advance payment VAT section: output on receipts vs credited on invoices.',
				'Input VAT by expense type: recoverable vs blocked (entertainment, partial motor).',
			),
		),
		'vat_tax_invoice' => array(
			'title' => 'VAT tax invoice format (PINT-AE / FTA)',
			'tax_type' => 'vat',
			'topic' => 'compliance',
			'summary' => 'Tax invoice must contain mandatory fields per UAE e-invoicing (PINT-AE) and VAT Executive Regulations — header, seller/buyer TRN, line VAT, totals, currency AED.',
			'steps' => array(
				'Issue tax invoice within 14 days of supply (or on advance receipt per FTA rules).',
				'Include unique serial number, date, seller & buyer TRN (B2B), full addresses, line description, qty, ex-VAT amount, VAT rate, VAT amount, total.',
				'For exports: transaction flag Exports, tax category Z (zero-rated), retain export evidence.',
				'Credit note (381) must reference original invoice number and adjust VAT accordingly.',
				'Show advance VAT credit on final invoice when customer prepaid (ERP: vat_net_after_advance).',
				'Validate in ERP E-Invoicing before ASP/Peppol submission.',
			),
			'fta_links' => array(
				array('label' => 'Legislation', 'url' => $fta['legislation']),
			),
			'erp_notes' => array(
				'E-Invoicing tab: auto-validation against mandatory field map.',
				'Tax compliance tab: full invoice format checklist.',
			),
		),
		'vat_advance_payments' => array(
			'title' => 'VAT on advance payments',
			'tax_type' => 'vat',
			'topic' => 'compliance',
			'summary' => 'Output VAT is due when an advance payment is received; adjusted when the final tax invoice is issued (Federal Decree-Law No. 8 of 2017, supply/payment rules).',
			'steps' => array(
				'Customer pays deposit → ERP records output VAT on VAT-inclusive amount (epc_uae_vat_advance).',
				'Include advance VAT in the VAT return for the period payment was received.',
				'On final supply, issue tax invoice for full value; credit advance VAT against invoice VAT.',
				'Net VAT on invoice = total invoice VAT − advance VAT already accounted.',
				'Do not double-count the same economic amount in output tax.',
			),
			'fta_links' => array(
				array('label' => 'VAT legislation', 'url' => $fta['legislation']),
			),
			'erp_notes' => array(
				'Automatic on order payments (income=0 ledger rows) and on e-invoice creation.',
				'Fields: advance_vat_credit, vat_net_after_advance on epc_einvoice_documents.',
			),
		),
		'vat_input_expenses' => array(
			'title' => 'Input VAT on expense types',
			'tax_type' => 'vat',
			'topic' => 'compliance',
			'summary' => 'Recover input VAT only where FTA allows — valid tax invoice, business purpose, not blocked (e.g. entertainment). Partial recovery for certain motor expenses.',
			'steps' => array(
				'Supplier purchases: record ex-VAT on Purchases; VAT auto for UAE VAT-registered suppliers.',
				'Staff expenses (CRM): categorize (travel vs entertainment) for recoverability.',
				'Blocked categories: entertainment/hospitality → 0% recoverable; add back for CT if non-deductible.',
				'Motor vehicle/fuel: often 50% recoverable — verify with advisor.',
				'Reverse charge (imported services): self-account output and input if applicable (category AE).',
				'Capital assets: follow capital goods scheme; capitalise in Fixed assets + GL.',
			),
			'fta_links' => array(
				array('label' => 'VAT guides', 'url' => $fta['vat_guides']),
			),
			'erp_notes' => array(
				'UAE VAT return → Input VAT by expense type table.',
				'CRM expense category maps to vat_expense_type where configured.',
			),
		),
		'excise_overview' => array(
			'title' => 'Excise tax overview',
			'tax_type' => 'excise',
			'topic' => 'overview',
			'summary' => 'Excise tax on specific goods (tobacco, carbonated/sweetened drinks, energy drinks, etc.) at rates set by FTA. Applies to producers, importers, and stockpilers in UAE.',
			'steps' => array(
				'Determine if your products are excise goods (product classification on FTA list).',
				'Register for Excise Tax on EmaraTax if producing, importing, or holding stock in designated zones.',
				'Register excise products on FTA platform (OCR-assisted product registration).',
				'File periodic excise returns and pay excise due; claim refunds when deductible tax exceeds payable.',
				'Report lost/damaged excise goods in designated zones via EmaraTax where applicable.',
			),
			'fta_links' => array(
				array('label' => 'Excise tax hub', 'url' => $fta['excise']),
				array('label' => 'Excise topics', 'url' => $fta['excise_guides']),
			),
			'erp_notes' => array(
				'Excise is not auto-calculated in storefront VAT module — track excise SKUs separately.',
				'Use inventory + purchase records for excise reporting; consult FTA excise scenarios guide.',
				'KKT/fiscal modules may tag excise goods for retail receipt compliance.',
			),
		),
		'excise_registration_filing' => array(
			'title' => 'Excise registration & return filing',
			'tax_type' => 'excise',
			'topic' => 'filing',
			'summary' => 'Register as excise business on EmaraTax; file excise tax returns per assigned period; product registration required before commercial movement.',
			'steps' => array(
				'EmaraTax → Excise Tax registration (entity + warehouse/designated zone details).',
				'Register each excise product (ingredients, HS code, brand) on FTA excise portal.',
				'Maintain stock records: imports, production, releases, losses in designated zones.',
				'File excise return: excise due vs deductible excise; pay net or apply for refund if excess input.',
				'Tiered/volumetric model updates (sweetened drinks) — monitor FTA announcements.',
			),
			'fta_links' => array(
				array('label' => 'Excise refunds', 'url' => $fta['excise']),
				array('label' => 'Excise scenarios', 'url' => $fta['excise_guides']),
			),
			'erp_notes' => array(
				'Document excise SKUs in product master; flag in CP catalog if needed.',
				'Custom & Shipping: import declarations support excise import evidence.',
			),
		),
		'ct_overview' => array(
			'title' => 'Corporate Tax overview (9%)',
			'tax_type' => 'corporate_tax',
			'topic' => 'overview',
			'summary' => 'UAE CT under Federal Decree-Law No. 47 of 2022 — 0% on taxable income up to AED 375,000 (small business relief) and 9% above threshold for most mainland entities (subject to free zone rules).',
			'steps' => array(
				'Assess CT registration obligation (generally all UAE persons with business activity).',
				'Register for Corporate Tax on EmaraTax; obtain CT registration number.',
				'Maintain financial statements, transfer pricing docs if related-party transactions.',
				'Calculate taxable income: accounting profit ± CT adjustments (exempt income, non-deductible expenses).',
				'File annual CT return and pay tax within FTA deadlines.',
			),
			'fta_links' => array(
				array('label' => 'Corporate Tax hub', 'url' => $fta['corporate_tax']),
				array('label' => 'CT topics & guides', 'url' => $fta['ct_guides']),
			),
			'erp_notes' => array(
				'P&L tab: accounting profit → CT add-backs/deductions → 9% provision estimate.',
				'Tax compliance tab: enter CT adjustment fields per period.',
			),
		),
		'ct_registration_filing' => array(
			'title' => 'Corporate Tax registration & filing',
			'tax_type' => 'corporate_tax',
			'topic' => 'filing',
			'summary' => 'CT registration via EmaraTax; annual return reporting accounting income with tax adjustments. Free zone qualifying income may be 0% if conditions met.',
			'steps' => array(
				'Register on EmaraTax → Corporate Tax (entity details, financial year end, activities).',
				'Prepare financial statements (IFRS or acceptable standards per MOF).',
				'Apply adjustments: non-deductible entertainment, fines, related-party, exempt income, loss utilisation.',
				'Complete CT return on EmaraTax; attach financials if required.',
				'Pay CT due or claim refund; keep documentation 7+ years.',
				'Monitor FTA decisions on small business relief, foreign PE, and QFZP rules.',
			),
			'fta_links' => array(
				array('label' => 'CT workshops & webinars', 'url' => $fta['corporate_tax']),
				array('label' => 'CT e-Learning', 'url' => $fta['elearning']),
			),
			'erp_notes' => array(
				'ERP CT provision is indicative — not a substitute for CT return.',
				'Use adjustment fields: entertainment, fines, exempt income, loss carryforward, etc.',
			),
		),
		'applications_clarifications' => array(
			'title' => 'Applications, clarifications & certificates',
			'tax_type' => 'general',
			'topic' => 'process',
			'summary' => 'FTA EmaraTax services: tax residency certificates, reconsideration, voluntary disclosures, tax clarifications, refund schemes.',
			'steps' => array(
				'Tax residency certificate: apply via EmaraTax for treaty / bank requirements.',
				'Tax clarification request: formal FTA binding guidance on specific transactions.',
				'Reconsideration: dispute FTA assessment decisions within time limits.',
				'Voluntary disclosure: correct errors before audit (penalty reduction may apply).',
				'Tourist VAT refund / foreign business refund: separate FTA schemes with evidence rules.',
			),
			'fta_links' => array(
				array('label' => 'FTA services (home)', 'url' => $fta['home']),
				array('label' => 'EmaraTax', 'url' => $fta['emaratax']),
			),
			'erp_notes' => array(
				'Export VAT zero-rating: link Custom & Shipping docs to e-invoice export flag.',
				'Keep ERP audit trail (GL, e-invoice event log) for reconsideration support.',
			),
		),
		'einvoicing_peppol' => array(
			'title' => 'E-Invoicing & Peppol (UAE)',
			'tax_type' => 'vat',
			'topic' => 'compliance',
			'summary' => 'UAE moving to structured e-invoicing (PINT-AE, Peppol 0235 endpoints). ASP submission for B2B/B2G per rollout timeline.',
			'steps' => array(
				'Configure seller Peppol endpoint (0235:TIN) in ERP E-Invoicing settings.',
				'Generate PINT-AE XML/JSON from validated tax invoices.',
				'Submit via Accredited Service Provider (ASP) when mandated for your sector.',
				'Buyer TRN and endpoint required for B2B UAE recipients.',
				'Monitor FTA e-invoicing mandatory dates for your entity size/sector.',
			),
			'fta_links' => array(
				array('label' => 'FTA legislation (e-invoicing)', 'url' => $fta['legislation']),
			),
			'erp_notes' => array(
				'E-Invoicing tab: build from order, validate, export XML, ASP API mode in settings.',
			),
		),
	);
}

function epc_uae_tax_knowledge_by_type(string $tax_type = ''): array
{
	$all = epc_uae_tax_knowledge_catalog();
	if ($tax_type === '') {
		return $all;
	}
	return array_filter($all, function ($a) use ($tax_type) {
		return ($a['tax_type'] ?? '') === $tax_type || ($tax_type === 'all');
	});
}

function epc_uae_tax_knowledge_type_labels(): array
{
	return array(
		'general' => 'General & processes',
		'vat' => 'VAT',
		'excise' => 'Excise tax',
		'corporate_tax' => 'Corporate Tax',
	);
}

/** Render one article as HTML for ERP panels. */
function epc_uae_tax_knowledge_render_article(array $article): string
{
	$html = '<div class="epc-uae-kb-article">';
	$html .= '<p class="text-muted">' . htmlspecialchars($article['summary'] ?? '', ENT_QUOTES, 'UTF-8') . '</p>';
	if (!empty($article['steps'])) {
		$html .= '<h5>Process steps</h5><ol>';
		foreach ($article['steps'] as $s) {
			$html .= '<li>' . htmlspecialchars($s, ENT_QUOTES, 'UTF-8') . '</li>';
		}
		$html .= '</ol>';
	}
	if (!empty($article['fta_links'])) {
		$html .= '<h5>FTA references</h5><ul>';
		foreach ($article['fta_links'] as $lnk) {
			$url = htmlspecialchars($lnk['url'] ?? '', ENT_QUOTES, 'UTF-8');
			$lbl = htmlspecialchars($lnk['label'] ?? $url, ENT_QUOTES, 'UTF-8');
			$html .= '<li><a href="' . $url . '" target="_blank" rel="noopener">' . $lbl . '</a></li>';
		}
		$html .= '</ul>';
	}
	if (!empty($article['erp_notes'])) {
		$html .= '<h5>How this ERP helps</h5><ul>';
		foreach ($article['erp_notes'] as $n) {
			$html .= '<li>' . htmlspecialchars($n, ENT_QUOTES, 'UTF-8') . '</li>';
		}
		$html .= '</ul>';
	}
	$html .= '</div>';
	return $html;
}

/** Seed KB articles from fetched FTA legislation summaries. */
function epc_uae_tax_legislation_seed_kb(PDO $db, array $legislation): int
{
	require_once __DIR__ . '/epc_erp_extended.php';
	epc_erp_extended_ensure_schema($db);
	$n = 0;
	foreach ($legislation as $item) {
		$slug = 'uae-leg-' . preg_replace('/[^a-z0-9-]+/', '-', strtolower((string)($item['slug'] ?? 'item')));
		$slug = trim($slug, '-');
		if (strlen($slug) > 80) {
			$slug = substr($slug, 0, 80);
		}
		$title = mb_substr((string)($item['title'] ?? 'UAE legislation'), 0, 250);
		$summary = mb_substr((string)($item['summary'] ?? ''), 0, 500);
		$body = '<div class="epc-uae-leg-item">';
		$body .= '<p><strong>Issue date:</strong> ' . htmlspecialchars((string)($item['issue_date'] ?? ''), ENT_QUOTES, 'UTF-8') . '</p>';
		if (!empty($item['publish_date'])) {
			$body .= '<p><strong>Publish date:</strong> ' . htmlspecialchars((string)$item['publish_date'], ENT_QUOTES, 'UTF-8') . '</p>';
		}
		$body .= '<p>' . htmlspecialchars($summary, ENT_QUOTES, 'UTF-8') . '</p>';
		if (!empty($item['erp_apply'])) {
			$body .= '<p><strong>ERP:</strong> ' . htmlspecialchars((string)$item['erp_apply'], ENT_QUOTES, 'UTF-8') . '</p>';
		}
		if (!empty($item['pdf_url'])) {
			$body .= '<p><a href="' . htmlspecialchars((string)$item['pdf_url'], ENT_QUOTES, 'UTF-8') . '" target="_blank" rel="noopener">PDF on tax.gov.ae</a></p>';
		}
		$body .= '</div>';
		$chk = $db->prepare('SELECT `id` FROM `epc_erp_kb_articles` WHERE `slug` = ? LIMIT 1');
		$chk->execute(array($slug));
		$now = time();
		if ($chk->fetchColumn()) {
			$db->prepare(
				'UPDATE `epc_erp_kb_articles` SET `title`=?, `category`=?, `summary`=?, `body_html`=?, `time_updated`=? WHERE `slug`=?'
			)->execute(array($title, 'uae_tax', $summary, $body, $now, $slug));
		} else {
			$db->prepare(
				'INSERT INTO `epc_erp_kb_articles` (`slug`, `title`, `category`, `summary`, `body_html`, `published`, `admin_id`, `time_created`, `time_updated`)
				VALUES (?,?,?,?,?,1,0,?,?)'
			)->execute(array($slug, $title, 'uae_tax', $summary, $body, $now, $now));
		}
		$n++;
	}
	return $n;
}

/** Seed / update internal KB articles (category uae_tax). */
function epc_uae_tax_knowledge_seed_kb(PDO $db): int
{
	require_once __DIR__ . '/epc_erp_extended.php';
	epc_erp_extended_ensure_schema($db);
	$n = 0;
	foreach (epc_uae_tax_knowledge_catalog() as $key => $article) {
		$slug = 'uae-tax-' . preg_replace('/[^a-z0-9-]+/', '-', strtolower($key));
		$chk = $db->prepare('SELECT `id` FROM `epc_erp_kb_articles` WHERE `slug` = ? LIMIT 1');
		$chk->execute(array($slug));
		$body = epc_uae_tax_knowledge_render_article($article);
		$summary = (string)($article['summary'] ?? '');
		$title = (string)($article['title'] ?? $key);
		$now = time();
		if ($chk->fetchColumn()) {
			$db->prepare(
				'UPDATE `epc_erp_kb_articles` SET `title`=?, `category`=?, `summary`=?, `body_html`=?, `time_updated`=? WHERE `slug`=?'
			)->execute(array($title, 'uae_tax', $summary, $body, $now, $slug));
		} else {
			$db->prepare(
				'INSERT INTO `epc_erp_kb_articles` (`slug`, `title`, `category`, `summary`, `body_html`, `published`, `admin_id`, `time_created`, `time_updated`)
				VALUES (?,?,?,?,?,1,0,?,?)'
			)->execute(array($slug, $title, 'uae_tax', $summary, $body, $now, $now));
		}
		$n++;
	}
	return $n;
}
