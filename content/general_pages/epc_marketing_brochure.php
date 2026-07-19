<?php
/**
 * Graphical product brochures — ECOM AE (platform) and eParts Cart (tenant + CP).
 * Printable via browser → Save as PDF. Self-contained HTML (no CMS shell required).
 */
defined('_ASTEXE_') or die('No access');

function epc_brochure_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

/**
 * @return array<string, mixed>
 */
function epc_brochure_profile(string $brand): array
{
	$brand = preg_replace('/[^a-z0-9_]/', '', strtolower($brand));
	if ($brand === 'epartscart' || $brand === 'auto_parts') {
		return array(
			'id' => 'epartscart',
			'name' => 'eParts Cart',
			'legal' => 'Electronic World Group',
			'tagline' => 'Spare parts commerce — storefront, OMS, and Control Panel',
			'domain' => 'www.epartscart.com',
			'url' => 'https://www.epartscart.com',
			'cp_url' => 'https://www.epartscart.com/cp',
			'cover' => '/content/general_pages/marketing_screens/epartscart-brochure-cover.jpg',
			'contact_email' => 'hello@epartscart.com',
			'contact_phone' => '+971-567607011',
			'accent' => '#dc2626',
			'accent2' => '#f97316',
			'ink' => '#0f172a',
			'ink2' => '#1e293b',
			'paper' => '#f8fafc',
			'muted' => '#64748b',
			'hero_sub' => 'One system for parts search, pricing, warehouses, orders, WhatsApp, AI chat, and ERP — so buyers and your team always see the same truth.',
			'cta_primary' => array('label' => 'Open storefront', 'href' => 'https://www.epartscart.com'),
			'cta_secondary' => array('label' => 'Open Control Panel', 'href' => 'https://www.epartscart.com/cp'),
		);
	}
	return array(
		'id' => 'ecomae',
		'name' => 'ECOM AE',
		'legal' => 'Electronic World Group',
		'tagline' => 'Blockchain BOS Enterprise System — commerce, ERP, CRM, compliance',
		'domain' => 'www.ecomae.com',
		'url' => 'https://www.ecomae.com',
		'cp_url' => 'https://www.ecomae.com/cp',
		'cover' => '/content/general_pages/marketing_screens/ecomae-brochure-cover.jpg',
		'contact_email' => 'hello@ecomae.com',
		'contact_phone' => '+971-567607011',
		'accent' => '#0ea5e9',
		'accent2' => '#0284c7',
		'ink' => '#0a0a0a',
		'ink2' => '#141414',
		'paper' => '#f4f6f8',
		'muted' => '#64748b',
		'hero_sub' => 'Hosted multi-tenant platform for UAE & GCC businesses: storefronts, Super CP, Client CP, ERP finance, Peppol e-invoice, and industry packs — one database, one operator story.',
		'cta_primary' => array('label' => 'Book a demo', 'href' => 'https://www.ecomae.com/platform/demo'),
		'cta_secondary' => array('label' => 'Platform capabilities', 'href' => 'https://www.ecomae.com/platform/capabilities'),
	);
}

/**
 * @return array<int, array{title:string,body:string,points:array<int,string>}>
 */
function epc_brochure_sections(string $brand): array
{
	if ($brand === 'epartscart') {
		return array(
			array(
				'title' => 'Storefront buyers understand',
				'body' => 'Customers search by part number, brand, VIN, and crosses — then buy with clear stock, price, and delivery options.',
				'points' => array(
					'Live part search across warehouses & price lists',
					'OEM / aftermarket crosses',
					'Multi-currency display (AED, USD, …)',
					'WhatsApp quote & share from product/cart',
					'AI Parts Expert chat on the website',
					'Accessories & marketplace listings',
				),
			),
			array(
				'title' => 'Control Panel (Client CP) — daily work',
				'body' => 'Your operations team runs the business from /cp — not five disconnected apps. Same data as the storefront.',
				'points' => array(
					'OMS · Orders — tabs, filters, messages, print, fulfilment',
					'Warehouses & price lists — upload, history, storefront toggles',
					'Multivendor supplier pipelines',
					'Customers / CRM & documents',
					'AI agent chats — review what buyers asked',
					'ERP & finance shell — ledger, VAT, reports',
					'Marketing broadcast, social hub, POS',
				),
			),
			array(
				'title' => 'Orders & fulfilment (OMS)',
				'body' => 'From cart to courier without spreadsheet chaos. Statuses, supplier lines, VAT, and customer messaging stay on the order.',
				'points' => array(
					'Today / pending-ship filters & keyboard shortcuts',
					'Line items, margin, brand/part edit',
					'Supplier fulfilment pipeline',
					'Courier VAT mapping',
					'WhatsApp share templates (EN + AR)',
					'Daily OMS guide for new staff',
				),
			),
			array(
				'title' => 'Pricing & warehouses',
				'body' => 'Price lists attach to storages. Temporary storefront disable hides a supplier without deleting data. Uploads keep history.',
				'points' => array(
					'CSV / file price upload with history download',
					'Warehouse list search & currency per storage',
					'Storefront ON/OFF toggles for WH & price lists',
					'Crosses for OEM ↔ aftermarket',
					'Auto Price AI (platform module)',
				),
			),
			array(
				'title' => 'AI, marketing & growth',
				'body' => 'Help buyers find parts; help your team publish and broadcast without leaving CP.',
				'points' => array(
					'AI Parts Expert — VIN, article, country-aware replies',
					'Chat review + CSV export in CP',
					'Email + WhatsApp marketing broadcast',
					'Social hub — IG / FB / TikTok publish from drafts',
					'First-party web tracker + UTM',
				),
			),
			array(
				'title' => 'ERP, tax & compliance',
				'body' => 'Trading data flows into finance modules — VAT-aware for UAE operations, with document control for PDFs and e-invoice paths.',
				'points' => array(
					'ERP Suite (GL, AR, inventory views)',
					'Tax toolkit / VAT workflows',
					'Document control & print packs',
					'Customer TRN / Peppol buyer profiles',
					'POS terminal for counter sales',
				),
			),
		);
	}

	return array(
		array(
			'title' => 'One Blockchain BOS for the enterprise',
			'body' => 'ECOM AE is a hosted Business Operating System: commerce + ERP + CRM + compliance on one tenant model — with cryptographic proof hooks for critical documents.',
			'points' => array(
				'Multi-tenant Super CP for operators',
				'Client CP for each storefront business',
				'Industry packs (auto parts, jewellery, fashion, …)',
				'Shared database — no CSV sync between modules',
				'UAE / GCC ready: VAT, Peppol e-invoice paths',
			),
		),
		array(
			'title' => 'Commerce that sells',
			'body' => 'Storefronts, catalogues, pricing, carts, payments, and omnichannel listings — built for distribution and retail traders.',
			'points' => array(
				'Catalogue + CSV import',
				'Supplier price lists & live search',
				'Order desk / OMS & fulfilment',
				'Payments, multi-currency, credit limits',
				'Warehouses, carriers, RMA',
				'Marketplace / accessories channels',
			),
		),
		array(
			'title' => 'ERP & finance',
			'body' => 'Dynamics-style module coverage without bolting a second ERP beside the shop.',
			'points' => array(
				'General ledger & chart of accounts',
				'AR / treasury / inventory views',
				'Procurement & supplier payables',
				'Tax toolkit & e-invoice readiness',
				'Document control & print designer',
				'ERP-only mode for finance tenants',
			),
		),
		array(
			'title' => 'AI & automation',
			'body' => 'Assist buyers and operators with domain-aware agents — not generic chat widgets.',
			'points' => array(
				'AI parts / product agents',
				'VIN & demand intelligence',
				'Auto Price AI',
				'Marketing broadcast & social publish',
				'Web tracker + GA4 / Clarity hooks',
			),
		),
		array(
			'title' => 'Operator Control Panels',
			'body' => 'Super CP runs the platform. Client CP runs each tenant’s daily trading — orders, prices, warehouses, CRM, ERP shell.',
			'points' => array(
				'Tenant hub, DNS, health, onboard',
				'Client CP guideline & daily dashboards',
				'Roles, groups, backend access',
				'Integrations vault (SMTP, WhatsApp Cloud API, social)',
				'Business continuity & failover tooling',
			),
		),
		array(
			'title' => 'Who it is for',
			'body' => 'Distributors, retailers, and multi-brand groups across GCC industries — with templates that adapt language, tax, and catalogue shape.',
			'points' => array(
				'Automotive & spare parts',
				'Electronics & fashion',
				'Jewellery & medical supply',
				'Hospitality, beauty, construction packs',
				'Platform host for SaaS operators',
			),
		),
	);
}

/**
 * @return array<int, array{label:string,value:string}>
 */
function epc_brochure_stats(string $brand): array
{
	if ($brand === 'epartscart') {
		return array(
			array('label' => 'Focus', 'value' => 'Auto parts B2B/B2C'),
			array('label' => 'Workspace', 'value' => 'Storefront + /cp'),
			array('label' => 'Core', 'value' => 'OMS · Prices · WH'),
			array('label' => 'AI', 'value' => 'Parts Expert chat'),
			array('label' => 'Region', 'value' => 'UAE · GCC'),
		);
	}
	return array(
		array('label' => 'Model', 'value' => 'Multi-tenant BOS'),
		array('label' => 'Panels', 'value' => 'Super CP + Client CP'),
		array('label' => 'Modules', 'value' => '100+ capabilities'),
		array('label' => 'Compliance', 'value' => 'VAT · Peppol path'),
		array('label' => 'HQ', 'value' => 'Dubai, UAE'),
	);
}

/**
 * @return array<int, array{step:string,title:string,body:string}>
 */
function epc_brochure_journey(string $brand): array
{
	if ($brand === 'epartscart') {
		return array(
			array('step' => '01', 'title' => 'Customer finds a part', 'body' => 'Search, VIN, AI chat, or WhatsApp — prices from live warehouses.'),
			array('step' => '02', 'title' => 'Order lands in OMS', 'body' => 'Staff confirm lines, margin, supplier, and ship status in one desk.'),
			array('step' => '03', 'title' => 'Fulfil & message', 'body' => 'Print, courier VAT, WhatsApp updates — customer stays informed.'),
			array('step' => '04', 'title' => 'Finance closes the loop', 'body' => 'Documents and ERP views reflect the same order truth.'),
		);
	}
	return array(
		array('step' => '01', 'title' => 'Onboard a tenant', 'body' => 'Super CP provisions industry pack, branding, and modules.'),
		array('step' => '02', 'title' => 'Trade on storefront', 'body' => 'Catalogue, prices, cart, and payments go live.'),
		array('step' => '03', 'title' => 'Operate in Client CP', 'body' => 'Orders, warehouses, CRM, marketing — daily workspace.'),
		array('step' => '04', 'title' => 'Govern in ERP', 'body' => 'Ledger, tax, documents, and proof-ready workflows.'),
	);
}

function epc_brochure_css(array $p): string
{
	$a = epc_brochure_h($p['accent']);
	$a2 = epc_brochure_h($p['accent2']);
	$ink = epc_brochure_h($p['ink']);
	$ink2 = epc_brochure_h($p['ink2']);
	$paper = epc_brochure_h($p['paper']);
	$muted = epc_brochure_h($p['muted']);
	return <<<CSS
@import url('https://fonts.googleapis.com/css2?family=Syne:wght@600;700;800&family=Source+Sans+3:wght@400;500;600;700&display=swap');
:root{
  --br-accent:{$a};--br-accent2:{$a2};--br-ink:{$ink};--br-ink2:{$ink2};
  --br-paper:{$paper};--br-muted:{$muted};--br-white:#fff;
}
*{box-sizing:border-box}
html,body{margin:0;padding:0;background:var(--br-paper);color:var(--br-ink);
  font-family:'Source Sans 3',system-ui,sans-serif;line-height:1.5;-webkit-font-smoothing:antialiased}
.epc-br{max-width:1100px;margin:0 auto;padding:0 20px 64px}
.epc-br__bar{display:flex;justify-content:space-between;align-items:center;gap:12px;padding:14px 0;position:sticky;top:0;z-index:5;
  background:rgba(248,250,252,.92);backdrop-filter:blur(10px);border-bottom:1px solid rgba(15,23,42,.08)}
.epc-br__brand{font-family:Syne,sans-serif;font-weight:800;font-size:1.15rem;letter-spacing:-.02em;text-decoration:none;color:var(--br-ink)}
.epc-br__actions{display:flex;gap:8px;flex-wrap:wrap}
.epc-br__btn{display:inline-flex;align-items:center;gap:6px;padding:8px 14px;border-radius:8px;font-weight:600;font-size:.9rem;text-decoration:none;border:0;cursor:pointer}
.epc-br__btn--pri{background:var(--br-accent);color:#fff}
.epc-br__btn--ghost{background:transparent;color:var(--br-ink);border:1px solid rgba(15,23,42,.15)}
.epc-br__hero{position:relative;margin:18px 0 28px;border-radius:0;overflow:hidden;min-height:min(72vh,560px);
  background:linear-gradient(135deg,var(--br-ink) 0%,var(--br-ink2) 55%,#000 100%);color:var(--br-white)}
.epc-br__hero-media{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:.55}
.epc-br__hero-veil{position:absolute;inset:0;background:linear-gradient(105deg,rgba(0,0,0,.82) 0%,rgba(0,0,0,.35) 55%,rgba(0,0,0,.55) 100%)}
.epc-br__hero-inner{position:relative;z-index:1;padding:clamp(36px,6vw,72px);max-width:640px}
.epc-br__eyebrow{display:inline-block;font-size:.75rem;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:var(--br-accent);margin:0 0 12px}
.epc-br__hero h1{font-family:Syne,sans-serif;font-weight:800;font-size:clamp(2.4rem,5.5vw,3.8rem);line-height:1.05;letter-spacing:-.03em;margin:0 0 14px}
.epc-br__hero p{font-size:1.05rem;color:rgba(255,255,255,.88);margin:0 0 22px;max-width:34em}
.epc-br__hero-ctas{display:flex;gap:10px;flex-wrap:wrap}
.epc-br__hero-ctas .epc-br__btn--ghost{color:#fff;border-color:rgba(255,255,255,.35)}
.epc-br__strip{display:grid;grid-template-columns:repeat(5,1fr);gap:1px;background:rgba(15,23,42,.08);border-radius:12px;overflow:hidden;margin:0 0 40px}
.epc-br__strip div{background:var(--br-white);padding:16px 14px}
.epc-br__strip strong{display:block;font-family:Syne,sans-serif;font-size:1rem;margin-bottom:2px}
.epc-br__strip span{font-size:.8rem;color:var(--br-muted)}
.epc-br__sec{margin:0 0 36px;break-inside:avoid}
.epc-br__sec h2{font-family:Syne,sans-serif;font-size:1.55rem;letter-spacing:-.02em;margin:0 0 8px}
.epc-br__sec > p{color:var(--br-muted);margin:0 0 14px;max-width:48em}
.epc-br__points{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px 18px;margin:0;padding:0;list-style:none}
.epc-br__points li{position:relative;padding:8px 0 8px 22px;font-size:.95rem}
.epc-br__points li::before{content:'';position:absolute;left:0;top:14px;width:10px;height:10px;border-radius:2px;background:linear-gradient(135deg,var(--br-accent),var(--br-accent2))}
.epc-br__journey{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin:8px 0 40px}
.epc-br__j{padding:18px 16px;background:var(--br-white);border-left:3px solid var(--br-accent);box-shadow:0 1px 0 rgba(15,23,42,.04)}
.epc-br__j em{font-style:normal;font-family:Syne,sans-serif;font-weight:800;color:var(--br-accent);font-size:1.1rem}
.epc-br__j strong{display:block;margin:6px 0 4px;font-size:1rem}
.epc-br__j span{font-size:.88rem;color:var(--br-muted)}
.epc-br__cp{display:grid;grid-template-columns:1.1fr .9fr;gap:20px;align-items:stretch;margin:0 0 40px}
.epc-br__cp-copy h2{margin-top:0}
.epc-br__panel{background:linear-gradient(160deg,var(--br-ink),var(--br-ink2));color:#fff;padding:22px;border-radius:14px;min-height:260px;
  background-image:radial-gradient(ellipse at 20% 0%,rgba(255,255,255,.08),transparent 50%),linear-gradient(160deg,var(--br-ink),var(--br-ink2))}
.epc-br__panel-top{display:flex;gap:6px;margin-bottom:16px}
.epc-br__dot{width:9px;height:9px;border-radius:50%;background:rgba(255,255,255,.25)}
.epc-br__dot:first-child{background:var(--br-accent)}
.epc-br__menu{display:grid;gap:6px}
.epc-br__menu span{display:block;padding:8px 10px;border-radius:6px;font-size:.82rem;background:rgba(255,255,255,.06)}
.epc-br__menu span.is-on{background:var(--br-accent);color:#fff;font-weight:700}
.epc-br__foot{margin-top:48px;padding:28px 0 8px;border-top:1px solid rgba(15,23,42,.1);display:flex;justify-content:space-between;gap:16px;flex-wrap:wrap}
.epc-br__foot strong{font-family:Syne,sans-serif}
.epc-br__foot a{color:var(--br-accent);font-weight:600;text-decoration:none}
@media (max-width:860px){
  .epc-br__strip{grid-template-columns:repeat(2,1fr)}
  .epc-br__journey{grid-template-columns:1fr 1fr}
  .epc-br__cp{grid-template-columns:1fr}
  .epc-br__points{grid-template-columns:1fr}
}
@media print{
  .epc-br__bar,.epc-br__actions{display:none!important}
  body{background:#fff}
  .epc-br{max-width:100%;padding:0}
  .epc-br__hero{min-height:320px;break-after:avoid}
  .epc-br__sec,.epc-br__j,.epc-br__cp{break-inside:avoid}
  a{color:inherit;text-decoration:none}
}
CSS;
}

/**
 * @param array{print?:bool} $opts
 */
function epc_brochure_render_html(string $brand, array $opts = array()): string
{
	$p = epc_brochure_profile($brand);
	$id = (string) $p['id'];
	$sections = epc_brochure_sections($id);
	$stats = epc_brochure_stats($id);
	$journey = epc_brochure_journey($id);
	$css = epc_brochure_css($p);
	$autoPrint = !empty($opts['print']) ? '<script>window.addEventListener("load",function(){setTimeout(function(){window.print()},400)});</script>' : '';

	$cpItems = $id === 'epartscart'
		? array('OMS · Orders', 'Warehouses', 'Prices & toggles', 'AI chats', 'Customers', 'ERP & VAT', 'Marketing', 'Settings')
		: array('Super CP tenants', 'Client CP home', 'Capabilities', 'Industries', 'ERP modules', 'Integrations', 'Tax toolkit', 'Health');

	$statsHtml = '';
	foreach ($stats as $s) {
		$statsHtml .= '<div><strong>' . epc_brochure_h($s['value']) . '</strong><span>' . epc_brochure_h($s['label']) . '</span></div>';
	}

	$journeyHtml = '';
	foreach ($journey as $j) {
		$journeyHtml .= '<div class="epc-br__j"><em>' . epc_brochure_h($j['step']) . '</em><strong>' . epc_brochure_h($j['title']) . '</strong><span>' . epc_brochure_h($j['body']) . '</span></div>';
	}

	$secHtml = '';
	foreach ($sections as $sec) {
		$secHtml .= '<section class="epc-br__sec"><h2>' . epc_brochure_h($sec['title']) . '</h2><p>' . epc_brochure_h($sec['body']) . '</p><ul class="epc-br__points">';
		foreach ($sec['points'] as $pt) {
			$secHtml .= '<li>' . epc_brochure_h($pt) . '</li>';
		}
		$secHtml .= '</ul></section>';
	}

	$menuHtml = '';
	foreach ($cpItems as $i => $item) {
		$menuHtml .= '<span' . ($i === 0 ? ' class="is-on"' : '') . '>' . epc_brochure_h($item) . '</span>';
	}

	$title = epc_brochure_h($p['name'] . ' — Product brochure');
	$desc = epc_brochure_h($p['tagline']);
	$cover = epc_brochure_h($p['cover']);

	return '<!DOCTYPE html><html lang="en"><head><meta charset="utf-8">'
		. '<meta name="viewport" content="width=device-width,initial-scale=1">'
		. '<title>' . $title . '</title>'
		. '<meta name="description" content="' . $desc . '">'
		. '<meta name="robots" content="index,follow">'
		. '<meta property="og:title" content="' . $title . '">'
		. '<meta property="og:description" content="' . $desc . '">'
		. '<meta property="og:image" content="' . epc_brochure_h(rtrim((string) ($p['url'] ?? ''), '/') . $p['cover']) . '">'
		. '<style>' . $css . '</style></head><body>'
		. '<div class="epc-br">'
		. '<div class="epc-br__bar"><a class="epc-br__brand" href="' . epc_brochure_h($p['url']) . '">' . epc_brochure_h($p['name']) . '</a>'
		. '<div class="epc-br__actions">'
		. '<button type="button" class="epc-br__btn epc-br__btn--ghost" onclick="window.print()">Print / PDF</button>'
		. '<a class="epc-br__btn epc-br__btn--pri" href="' . epc_brochure_h($p['cta_primary']['href']) . '">' . epc_brochure_h($p['cta_primary']['label']) . '</a>'
		. '</div></div>'
		. '<header class="epc-br__hero">'
		. '<img class="epc-br__hero-media" src="' . $cover . '" alt="' . epc_brochure_h($p['name'] . ' brochure cover') . '" width="1600" height="900">'
		. '<div class="epc-br__hero-veil" aria-hidden="true"></div>'
		. '<div class="epc-br__hero-inner">'
		. '<div class="epc-br__eyebrow">Product brochure</div>'
		. '<h1>' . epc_brochure_h($p['name']) . '</h1>'
		. '<p>' . epc_brochure_h($p['hero_sub']) . '</p>'
		. '<div class="epc-br__hero-ctas">'
		. '<a class="epc-br__btn epc-br__btn--pri" href="' . epc_brochure_h($p['cta_primary']['href']) . '">' . epc_brochure_h($p['cta_primary']['label']) . '</a>'
		. '<a class="epc-br__btn epc-br__btn--ghost" href="' . epc_brochure_h($p['cta_secondary']['href']) . '">' . epc_brochure_h($p['cta_secondary']['label']) . '</a>'
		. '</div></div></header>'
		. '<div class="epc-br__strip">' . $statsHtml . '</div>'
		. '<section class="epc-br__sec"><h2>How work flows</h2><p>A simple journey customers and staff can follow.</p></section>'
		. '<div class="epc-br__journey">' . $journeyHtml . '</div>'
		. '<div class="epc-br__cp"><div class="epc-br__cp-copy">'
		. '<h2>' . ($id === 'epartscart' ? 'Inside the Control Panel' : 'Inside the Control Panels') . '</h2>'
		. '<p>' . ($id === 'epartscart'
			? 'Client CP at /cp is the daily workspace: OMS first, then warehouses, prices, AI chats, customers, and ERP. Left-menu search jumps to any module.'
			: 'Super CP governs tenants and platform health. Each customer gets a Client CP tailored by industry pack — commerce and finance without a second login maze.') . '</p>'
		. '<ul class="epc-br__points">'
		. ($id === 'epartscart'
			? '<li>Dashboard: OMS, Warehouses, Prices, Multivendor, Crosses, AI chats</li><li>Secure vaults for WhatsApp & social tokens</li><li>Printable OMS daily guide for training</li>'
			: '<li>Tenant hub & industry templates</li><li>Capabilities catalog (100+ modules)</li><li>Demo & pricing paths for sales</li>')
		. '</ul></div>'
		. '<div class="epc-br__panel" aria-hidden="true"><div class="epc-br__panel-top"><i class="epc-br__dot"></i><i class="epc-br__dot"></i><i class="epc-br__dot"></i></div>'
		. '<div class="epc-br__menu">' . $menuHtml . '</div></div></div>'
		. $secHtml
		. '<footer class="epc-br__foot">'
		. '<div><strong>' . epc_brochure_h($p['name']) . '</strong><br><span style="color:var(--br-muted)">' . epc_brochure_h($p['legal']) . ' · Dubai, UAE</span></div>'
		. '<div style="text-align:right">'
		. '<a href="mailto:' . epc_brochure_h($p['contact_email']) . '">' . epc_brochure_h($p['contact_email']) . '</a><br>'
		. '<a href="tel:' . epc_brochure_h(preg_replace('/\s+/', '', $p['contact_phone'])) . '">' . epc_brochure_h($p['contact_phone']) . '</a><br>'
		. '<a href="' . epc_brochure_h($p['url']) . '">' . epc_brochure_h($p['domain']) . '</a>'
		. ($id === 'epartscart' ? '<br><a href="' . epc_brochure_h($p['cp_url']) . '">Control Panel →</a>' : '')
		. '</div></footer></div>'
		. $autoPrint
		. '</body></html>';
}

/** Exit with full brochure document (marketing hosts / direct includes). */
function epc_brochure_render_and_exit(string $brand, array $opts = array()): void
{
	if (!headers_sent()) {
		header('Content-Type: text/html; charset=utf-8');
		header('X-Robots-Tag: index, follow');
	}
	echo epc_brochure_render_html($brand, $opts);
	exit;
}
