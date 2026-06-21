<?php
/**
 * Prime Invest landing data — nav, hero slides, icon boxes, counters, team, testimonials.
 */
defined('_ASTEXE_') or die('No access');

function epc_cpi_nav_links(): array
{
	return array(
		array('label' => 'Home', 'href' => '/'),
		array('label' => 'Services', 'href' => '/#epc-cpi-services'),
		array('label' => 'About', 'href' => '/#epc-cpi-about'),
		array('label' => 'Team', 'href' => '/#epc-cpi-team'),
		array('label' => 'Contact', 'href' => '/kontakty'),
	);
}

function epc_cpi_header_contact(): array
{
	return array(
		'phone' => '+971 4 355 2800',
		'email' => 'info@taxofinca.com',
		'hours' => 'Sun–Thu 9:00–18:00 GST',
	);
}

function epc_cpi_hero_slides(): array
{
	return array(
		array(
			'eyebrow' => 'Tax & advisory',
			'title' => 'Tax clarity and compliance in one place',
			'text' => 'Corporate tax, VAT and management reporting for UAE entities — with a secure client ERP portal.',
			'cta' => 'Discover our services',
			'href' => '/#epc-cpi-services',
			'tone' => 'dark',
		),
		array(
			'eyebrow' => 'Corporate tax & VAT',
			'title' => 'Beyond complexity — structured advisory',
			'text' => 'Registration, filing, reviews and cross-border structures handled by advisors who know UAE rules.',
			'cta' => 'Client ERP portal',
			'href' => '/erp',
			'tone' => 'green',
		),
		array(
			'eyebrow' => 'Business growth',
			'title' => 'Advisory that scales with your ambitions',
			'text' => 'Entity setup, bookkeeping oversight, payroll coordination and board-ready reporting.',
			'cta' => 'Book a consultation',
			'href' => '/kontakty',
			'tone' => 'dark',
		),
	);
}

function epc_cpi_icon_boxes(): array
{
	return array(
		array('icon' => 'fa-shield', 'title' => 'We handle the compliance', 'text' => 'Filings, deadlines and authority correspondence managed with clear accountability.'),
		array('icon' => 'fa-line-chart', 'title' => 'ERP & e-invoicing', 'text' => 'Invoices, customers and finance workflows on one portal your team can trust.'),
		array('icon' => 'fa-globe', 'title' => 'UAE corporate tax', 'text' => 'Registration, returns and advisory aligned with FTA requirements and timelines.'),
		array('icon' => 'fa-users', 'title' => 'Dedicated advisors', 'text' => 'Named contacts for tax, accounting and business questions — not a call centre.'),
	);
}

function epc_cpi_credentials(): array
{
	return array(
		array('icon' => 'fa-certificate', 'title' => 'FTA-aligned advisory', 'text' => 'Corporate tax & VAT compliance'),
		array('icon' => 'fa-lock', 'title' => 'Secure client ERP', 'text' => 'Encrypted documents & workflows'),
		array('icon' => 'fa-map-marker', 'title' => 'UAE entity focus', 'text' => 'Mainland & free-zone support'),
		array('icon' => 'fa-handshake-o', 'title' => 'Named advisors', 'text' => 'Direct partner access'),
	);
}

function epc_cpi_about_image_url(): string
{
	return '/content/files/images/storefronts/consulting/photo-1454165804606-220107c9a589.jpg';
}

function epc_cpi_services(): array
{
	return array(
		array(
			'icon' => 'fa-balance-scale',
			'title' => 'Corporate tax & VAT',
			'text' => 'Registration, filing, compliance reviews and advisory for UAE and international structures.',
			'num' => '01',
		),
		array(
			'icon' => 'fa-calculator',
			'title' => 'Accounting & bookkeeping',
			'text' => 'Monthly accounts, management reporting, payroll coordination and audit-ready records.',
			'num' => '02',
		),
		array(
			'icon' => 'fa-briefcase',
			'title' => 'Business advisory',
			'text' => 'Entity setup, restructuring, cash-flow planning and growth strategy for SMEs.',
			'num' => '03',
		),
		array(
			'icon' => 'fa-file-text-o',
			'title' => 'E-invoicing & ERP',
			'text' => 'Integrated invoicing, customer records and finance workflows on one secure portal.',
			'num' => '04',
		),
		array(
			'icon' => 'fa-building-o',
			'title' => 'Entity & licensing',
			'text' => 'Mainland and free-zone setup support with tax registration from day one.',
			'num' => '05',
		),
		array(
			'icon' => 'fa-pie-chart',
			'title' => 'Management reporting',
			'text' => 'Dashboards and packs tailored for founders, finance teams and investors.',
			'num' => '06',
		),
	);
}

function epc_cpi_stats(): array
{
	return array(
		array('value' => '15+', 'label' => 'Years experience', 'sub' => 'tax & advisory'),
		array('value' => '500+', 'label' => 'Clients supported', 'sub' => 'UAE & GCC'),
		array('value' => '24h', 'label' => 'Response target', 'sub' => 'business days'),
		array('value' => '100%', 'label' => 'Compliance focus', 'sub' => 'no shortcuts'),
	);
}

function epc_cpi_team(): array
{
	return array(
		array('name' => 'Senior tax partner', 'role' => 'Corporate tax & VAT', 'initials' => 'ST'),
		array('name' => 'Lead accountant', 'role' => 'Bookkeeping & reporting', 'initials' => 'LA'),
		array('name' => 'Advisory director', 'role' => 'Business & entity setup', 'initials' => 'AD'),
	);
}

function epc_cpi_testimonials(): array
{
	return array(
		array(
			'quote' => 'Taxofinca gave us a clear UAE tax roadmap and a portal our finance team actually uses every day.',
			'author' => 'CFO, trading group',
			'company' => 'Dubai',
		),
		array(
			'quote' => 'VAT and corporate tax filings are on time, with proactive advice before deadlines — not after.',
			'author' => 'Founder, professional services',
			'company' => 'Abu Dhabi',
		),
		array(
			'quote' => 'We moved from spreadsheets to structured reporting and e-invoicing without disrupting operations.',
			'author' => 'Finance manager, SME',
			'company' => 'Sharjah',
		),
	);
}

function epc_cpi_partners(): array
{
	return array('FTA ready', 'UAE entities', 'ERP integrated', 'Multi-currency', 'Bilingual support');
}

function epc_cpi_process_steps(): array
{
	return array(
		array('num' => '01', 'title' => 'Discovery call', 'text' => 'We review your entity structure, obligations and reporting needs.'),
		array('num' => '02', 'title' => 'Tailored plan', 'text' => 'Clear scope for tax, accounting and advisory deliverables.'),
		array('num' => '03', 'title' => 'Ongoing support', 'text' => 'Filing, reporting and ERP access through your client portal.'),
	);
}

function epc_cpi_footer_columns(): array
{
	return array(
		array(
			'title' => 'Services',
			'links' => array(
				array('label' => 'Corporate tax', 'href' => '/#epc-cpi-services'),
				array('label' => 'VAT compliance', 'href' => '/#epc-cpi-services'),
				array('label' => 'Accounting', 'href' => '/#epc-cpi-services'),
				array('label' => 'Business advisory', 'href' => '/#epc-cpi-services'),
			),
		),
		array(
			'title' => 'Portal',
			'links' => array(
				array('label' => 'Client ERP login', 'href' => '/erp'),
				array('label' => 'Staff control panel', 'href' => '/cp/'),
				array('label' => 'Client login', 'href' => '/users/login'),
			),
		),
		array(
			'title' => 'Company',
			'links' => array(
				array('label' => 'About us', 'href' => '/#epc-cpi-about'),
				array('label' => 'Our team', 'href' => '/#epc-cpi-team'),
				array('label' => 'Contact', 'href' => '/kontakty'),
			),
		),
	);
}

function epc_cpi_theme_palette(): array
{
	return array(
		'primary' => '#1e40af',
		'primary_dark' => '#1e3a8a',
		'accent' => '#d4af37',
		'sidebar_from' => '#0f172a',
		'sidebar_to' => '#1e3a5f',
		'hero_from' => '#0f172a',
		'hero_to' => '#1e40af',
	);
}

function epc_cpi_pro_hero_eyebrow(): string
{
	return 'Tax & advisory services';
}

function epc_cpi_pro_hero_title(): string
{
	return 'Corporate tax clarity, VAT compliance, and growth you can measure.';
}

function epc_cpi_pro_hero_copy(): string
{
	return 'Registration, filing, management reporting and a secure client ERP portal — structured advisory for UAE entities.';
}

/** @return array<int, array{label: string, href: string, icon: string, primary?: bool}> */
function epc_cpi_pro_hero_actions(string $lang): array
{
	return array(
		array('label' => 'Our services', 'href' => '/#epc-cpi-services', 'icon' => 'fa-briefcase', 'primary' => true),
		array('label' => 'Client ERP', 'href' => '/shop/erp', 'icon' => 'fa-line-chart'),
		array('label' => 'Contact us', 'href' => '/kontakty', 'icon' => 'fa-envelope'),
	);
}

/** @return array<int, array{value: string, label: string}> */
function epc_cpi_pro_hero_stats(): array
{
	return array(
		array('value' => 'FTA', 'label' => 'aligned advisory'),
		array('value' => 'ERP', 'label' => 'client portal'),
		array('value' => 'UAE', 'label' => 'entity focus'),
	);
}

/** Stamp demo SKUs onto product/service arrays when missing. */
function epc_cpi_stamp_skus(array $items, string $prefix = 'TF'): array
{
	$n = 1001;
	foreach ($items as &$item) {
		if (empty($item['sku'])) {
			$item['sku'] = $prefix . '-' . $n++;
		}
	}
	unset($item);
	return $items;
}

function epc_cpi_format_aed($amount): string
{
	$amount = (float) $amount;
	if ($amount <= 0) {
		return '';
	}
	return 'AED ' . number_format($amount, 0, '.', ',');
}

/** Advisory service packages — demo catalogue (prices in AED). */
function epc_cpi_service_packages(): array
{
	$packages = array(
		array('sku' => 'TF-VAT-001', 'category' => 'VAT Compliance', 'name' => 'VAT Registration & FTA Setup', 'price' => 2500, 'was' => 0, 'image' => '/content/files/images/storefronts/consulting/photo-1454165804606-220107c9a589.jpg'),
		array('sku' => 'TF-CT-002', 'category' => 'Corporate Tax', 'name' => 'Corporate Tax Registration Package', 'price' => 3500, 'was' => 0, 'image' => '/content/files/images/storefronts/consulting/photo-1554224155-6726b3ff858f.jpg', 'is_new' => true),
		array('sku' => 'TF-VAT-003', 'category' => 'VAT Compliance', 'name' => 'Monthly VAT Return Filing', 'price' => 850, 'was' => 0, 'image' => '/content/files/images/storefronts/consulting/photo-1556761175-5973dc0f32e7.jpg'),
		array('sku' => 'TF-CT-004', 'category' => 'Corporate Tax', 'name' => 'Annual Corporate Tax Return', 'price' => 4500, 'was' => 5200, 'image' => '/content/files/images/storefronts/consulting/photo-1460925895917-afdab827c52f.jpg'),
		array('sku' => 'TF-ACC-005', 'category' => 'Accounting', 'name' => 'Bookkeeping Starter — 50 transactions/mo', 'price' => 1200, 'was' => 0, 'image' => '/content/files/images/storefronts/consulting/photo-1507679799987-c73779587eea.jpg'),
		array('sku' => 'TF-ACC-006', 'category' => 'Accounting', 'name' => 'Management Accounts Pack — Quarterly', 'price' => 2800, 'was' => 3200, 'image' => '/content/files/images/storefronts/consulting/photo-1551836022-d5d88e9218df.jpg'),
		array('sku' => 'TF-ADV-007', 'category' => 'Advisory', 'name' => 'Business Advisory Strategy Session', 'price' => 950, 'was' => 0, 'image' => '/content/files/images/storefronts/consulting/photo-1521737711867-e3b97375f902.jpg', 'is_new' => true),
		array('sku' => 'TF-ENT-008', 'category' => 'Entity Setup', 'name' => 'Mainland LLC Entity Formation', 'price' => 8500, 'was' => 0, 'image' => '/content/files/images/storefronts/consulting/photo-1486406146926-c627a92ad1ab.jpg'),
		array('sku' => 'TF-ERP-009', 'category' => 'ERP Portal', 'name' => 'Client ERP Portal Onboarding', 'price' => 1500, 'was' => 0, 'image' => '/content/files/images/storefronts/consulting/photo-1551288049-bebda4e38f71.jpg'),
		array('sku' => 'TF-PAY-010', 'category' => 'Payroll', 'name' => 'Payroll Processing — up to 10 staff', 'price' => 650, 'was' => 0, 'image' => '/content/files/images/storefronts/consulting/photo-1573164713714-d95e436ab8d6.jpg'),
		array('sku' => 'TF-AUD-011', 'category' => 'Audit Ready', 'name' => 'Pre-Audit Readiness Review', 'price' => 3200, 'was' => 3600, 'image' => '/content/files/images/storefronts/consulting/photo-1450101499163-c8848c66ca85.jpg'),
		array('sku' => 'TF-PLN-012', 'category' => 'Advisory', 'name' => 'Cash-Flow Forecast Workshop', 'price' => 1800, 'was' => 0, 'image' => '/content/files/images/storefronts/consulting/photo-1554224155-8d04cb21cd6c.jpg', 'is_new' => true),
	);
	return epc_cpi_stamp_skus($packages, 'TF');
}
