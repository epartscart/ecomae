<?php
/**
 * Social media content pack — extracted from ecomae_social_media_pack.html
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

$epcPackDocRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
if ($epcPackDocRoot === '') {
	$epcPackDocRoot = dirname(__DIR__, 2);
}
require_once $epcPackDocRoot . '/content/social_media/epc_social_media_helpers.php';

/** @return array<string, array<string, mixed>> */
function epc_social_pack_platforms(): array
{
	return array(
		'linkedin' => array(
			'label' => 'LinkedIn',
			'intro' => 'Long-form thought leadership. Ideal for decision-makers in UAE and GCC. Focus on business value, ROI, and operational pain points.',
			'hashtags' => array('#ECOMAE', '#UAEBusiness', '#Dubai', '#ERP', '#Ecommerce', '#DigitalTransformation', '#UAECompliance', '#UAEVAT', '#Einvoicing', '#FTA', '#SaaS', '#MultiTenant', '#B2B', '#AutoParts', '#DubaiTech', '#CloudERP', '#SME', '#Wholesale', '#UAEStartup', '#GCC'),
		),
		'instagram' => array(
			'label' => 'Instagram',
			'intro' => 'Short, punchy captions. Hook in the first line. Pair with bold branded visuals. Use carousel posts for feature lists and Reels for demos.',
			'hashtags' => array('#ECOMAE', '#DubaiTech', '#UAEStartup', '#Ecommerce', '#DubaiBusiness', '#UAEBusiness', '#MadeForUAE', '#DubaiEntrepreneur', '#ERP', '#CloudPlatform', '#B2BUAE', '#UAERetail', '#AutoPartsUAE', '#DubaiShopping', '#OnlineBusiness', '#AI', '#GCCBusiness', '#SmallBizUAE', '#WholesaleUAE', '#UAEVat', '#DubaiDigital'),
		),
		'facebook' => array(
			'label' => 'Facebook',
			'intro' => 'Conversational tone, slightly longer. Great for UAE traders\' communities. Use link previews. Questions and CTAs get strong engagement.',
			'hashtags' => array('#UAEBusiness', '#DubaiSME', '#UAEEntrepreneur', '#DubaiTrading', '#B2BUAE', '#AutoPartsUAE', '#UAERetail', '#EcommerceUAE', '#ECOMAE', '#DubaiStartup', '#WholesaleUAE', '#GCCBusiness', '#UAECompliance'),
		),
		'x' => array(
			'label' => 'X / Twitter',
			'intro' => 'Punchy, direct, max 280 characters. Bold statements work best. Use threads for deeper feature dives.',
			'hashtags' => array('#UAECompliance', '#Ecommerce', '#ERP', '#AutoParts', '#UAE', '#B2B', '#FreeTrial', '#ECOMAE'),
		),
		'tiktok' => array(
			'label' => 'TikTok',
			'intro' => 'Vertical video 9:16, 15–60 seconds. Hook in first 2 seconds. Native captions + trending audio. GCC/Pakistan: Arabic/Urdu subtitles boost reach.',
			'hashtags' => array('#B2B', '#EcommerceTips', '#SmallBusiness', '#UAEBusiness', '#ERP', '#AutoParts', '#DubaiBusiness', '#TechTok', '#LearnOnTikTok'),
		),
	);
}

/** @return array<int, array<string, string>> */
function epc_social_pack_posts(string $platform): array
{
	$posts = array(
		'linkedin' => array(
			array('title' => 'Post 1 — Platform Introduction', 'caption' => "Running a UAE business means juggling Shopify, QuickBooks, a CRM, and spreadsheets — simultaneously.\n\nWe built ECOM AE to end that.\n\nOne cloud platform. One database. Everything connected:\n✅ Online storefront\n✅ Full ERP + accounting\n✅ Built-in CRM & customer management\n✅ UAE VAT & Peppol e-invoicing\n✅ 100+ features — live in 24 hours\n\nAuto spare parts. Fashion. Electronics. Jewellery. Trading.\nWe have an industry template ready for you.\n\n👉 3-day free sandbox demo at ecomae.com\n\n#ECOMAE #UAEBusiness #ERP #Ecommerce #Dubai #DigitalTransformation"),
			array('title' => 'Post 2 — UAE E-Invoicing', 'caption' => "UAE businesses: e-invoicing requirements are expanding. Is your stack ready?\n\nECOM AE ships with full compliance built in — not bolted on:\n\n📋 Peppol / PINT-AE e-invoice generation\n📋 FTA XML & JSON export\n📋 VAT return preparation tools\n📋 TRN-aware tax invoices\n📋 One-click FTA legislation updates\n\nStop retrofitting compliance onto legacy software. Build on a platform designed for UAE from day one.\n\nThe businesses that prepare now will avoid costly disruption later.\n\n🔗 Explore the platform at ecomae.com\n\n#UAECompliance #UAEVAT #Einvoicing #FTA #Peppol #DubaiFintech"),
			array('title' => 'Post 3 — Super CP Multi-Tenant', 'caption' => "If you're an agency, ERP reseller, or franchise operator — ECOM AE's Super CP was built for your business model.\n\nOne operator console. Every client. Zero data bleed.\n\n✦ Onboard new tenants in under 24 hours\n✦ Push industry templates across all clients\n✦ Monitor health, SSL, and backups centrally\n✦ Deploy module packs per client industry\n✦ Isolated MySQL databases per tenant\n\nScale your client base without scaling your ops team.\n\nThis is how modern SaaS operators should run.\n\n📩 Book a consultation: ecomae.com\n\n#SaaS #MultiTenant #ERP #Agencies #FranchiseOps #UAE #CloudERP"),
			array('title' => 'Post 4 — Auto Parts', 'caption' => "The UAE auto spare parts market is massive — and most of it still runs on phone calls and Excel.\n\nECOM AE's auto parts module solves every pain point:\n\n🔧 VIN search & OEM number lookup\n🔧 Cross-reference catalog management\n🔧 Vehicle fitment database\n🔧 Supplier price comparison & import\n🔧 B2B trade accounts with credit limits\n🔧 AI parts agent on your storefront\n\nYour competitors are quoting via WhatsApp.\nYou can be running a fully integrated B2B e-commerce + ERP stack.\n\nSee it live at epartscart.com | Learn more: ecomae.com\n\n#AutoParts #UAE #SparePartsUAE #B2B #Ecommerce #AutoIndustry"),
		),
		'instagram' => array(
			array('title' => 'Post 1 — Zero to Live Store', 'caption' => "From zero to live e-commerce store in 24 hours. 🚀\n\nNo developers needed. No long setup. Just your business — online and running with full ERP + CRM built right in.\n\nOne platform. One database. Everything connected. 🔗\n\n👉 3-day free demo — link in bio\n\n#ECOMAE #DubaiTech #UAEStartup #Ecommerce #GoLive #OneBusiness"),
			array('title' => 'Post 2 — 100+ Features', 'caption' => "100+ features. One platform. 💪\n\nE-commerce ✅ ERP ✅ CRM ✅\nVAT Ready ✅ AI-Powered ✅\nMulti-currency ✅ B2B Portal ✅\n\nUAE businesses deserve better than 5 disconnected tools and hours of manual reconciliation.\n\nStart your free demo → ecomae.com (link in bio)\n\n#AllInOne #ECOMAE #UAEBusiness #TechUAE #Dubai #ERP #SME"),
			array('title' => 'Post 3 — Meet Layla AI', 'caption' => "Meet Layla 🤖✨ — your AI-powered ECOM AE guide.\n\nShe'll walk you through your industry solution, ERP setup, and UAE compliance features.\n\nNo sales calls. No waiting. Just click, explore, and go. 💡\n\n👉 Try the AI Demo free — link in bio\n\n#AI #ECOMAE #DubaiTech #FutureOfBusiness #AIDemo #MeetLayla"),
			array('title' => 'Post 4 — Industry Templates', 'caption' => "Auto parts 🔧 Fashion 👗 Electronics 📱 Jewellery 💎 Medical 🏥\n\nWhatever you sell, ECOM AE has an industry template for it — pre-built, ready to launch.\n\nBuilt for UAE traders, retailers, and wholesalers who want ONE system, not five. 🇦🇪\n\n🔗 Start free → ecomae.com (link in bio)\n\n#UAETrade #Retail #Wholesale #Ecommerce #MadeForUAE #Dubai #ECOMAE"),
		),
		'facebook' => array(
			array('title' => 'Post 1 — One Platform', 'caption' => "Still juggling Shopify + Excel + accounting software + WhatsApp for your UAE business? 😓\n\nThere's a better way — and it's already built.\n\nECOM AE brings everything under one roof:\n🛒 Online storefront with checkout\n📦 Inventory & multi-warehouse management\n💰 Accounts, finance & bank reconciliation\n📋 UAE VAT & Peppol e-invoicing\n🤝 CRM, leads & customer management\n\nOne platform. One database. Zero data chaos.\n\n👇 Start with a free 3-day sandbox demo at ecomae.com"),
			array('title' => 'Post 2 — Free Demo', 'caption' => "🎉 FREE 3-Day Demo — No commitment. No credit card required.\n\nSee ECOM AE live with a full sandbox tailored to your business:\n\n🔧 Auto spare parts & VIN catalogue\n👗 Fashion with variant SKUs & lookbooks\n📱 Electronics with specs & RMA handling\n💎 Jewellery with gallery product cards\n📦 Trading & distribution with B2B portal\n\nOur AI guide Layla will walk you through every feature — at your pace.\n\n👉 Start your free demo now: ecomae.com\n\nWhat industry are you in? Drop it in the comments 👇"),
			array('title' => 'Post 3 — FTA Compliance', 'caption' => "📢 Important for UAE businesses: FTA e-invoicing requirements are expanding.\n\nMany businesses are still not prepared.\n\nECOM AE has compliance built in from day one:\n✅ Peppol / PINT-AE e-invoice generation\n✅ UAE VAT return preparation tools\n✅ FTA XML & JSON export formats\n✅ TRN-aware tax invoices\n✅ One-click FTA legislation updates\n\nDon't get caught unprepared when enforcement kicks in.\n\n🔗 Learn more and book a demo: ecomae.com"),
			array('title' => 'Post 4 — B2B Portal', 'caption' => "B2B businesses in UAE — this one's for you. 📣\n\nYour trade customers expect a professional experience. Not WhatsApp quotes and manual invoices.\n\nECOM AE's B2B portal gives your buyers:\n💳 Contract pricing & credit limits\n📋 Order history & one-click re-ordering\n🏦 Account balance view at any time\n🌍 Multi-currency checkout\n✅ Approval workflows for new accounts\n\nTurn your wholesale operation into a modern, self-service B2B portal.\n\n👉 Book a demo: ecomae.com"),
		),
		'x' => array(
			array('title' => 'Post 1 — One Cloud', 'caption' => "UAE businesses are still using 5 different tools for e-commerce, ERP, CRM, VAT, and inventory.\n\nECOM AE replaces all of them.\n\nOne cloud. Go live in 24 hours.\n\necomae.com"),
			array('title' => 'Post 2 — UAE Compliance', 'caption' => "Built for UAE from day one.\n\nPeppol e-invoicing ✓\nFTA XML export ✓\nUAE VAT returns ✓\nTRN invoicing ✓\n\nNot an afterthought. It's the core of how ECOM AE was designed.\n\necomae.com\n\n#UAECompliance #Ecommerce #ERP"),
			array('title' => 'Post 3 — Auto Parts', 'caption' => "Auto parts businesses:\n\nVIN search ✓\nOEM lookup ✓\nSupplier pricing ✓\nCross-reference catalog ✓\nB2B portal ✓\nERP built-in ✓\n\nAll in one stack.\n\nNo more WhatsApp quotes. No more Excel inventory.\n\necomae.com | epartscart.com\n\n#AutoParts #UAE #B2B"),
			array('title' => 'Post 4 — Free Demo', 'caption' => "3-day free demo.\nNo credit card.\nMeet Layla — the AI guide.\n\nSee your exact industry solution live.\n\nAuto parts → Electronics → Fashion → Jewellery → Trading\n\nThen decide.\n\necomae.com\n\n#FreeTrial #ECOMAE #UAE 🇦🇪"),
		),
		'tiktok' => array(
			array('title' => 'Reel 1 — 24h Go Live', 'caption' => "POV: Your boss says \"go online this week\" and you don't have a dev team. 😅\n\nWe went from zero → live store + ERP in 24 hours on ECOM AE.\n\nScreen record: CP → add product → live checkout.\n\nLink in bio for free demo.\n\n#EcommerceTips #SmallBusiness #UAEBusiness #B2B #GoLive"),
			array('title' => 'Reel 2 — VIN Search Demo', 'caption' => "Stop typing part numbers wrong. 🔧\n\nVIN search → OEM → add to cart → ERP invoice.\n\nOne flow. No WhatsApp back-and-forth.\n\nAuto parts shops in UAE/Pakistan — this is for you.\n\n#AutoParts #VIN #SpareParts #B2B #ERP"),
			array('title' => 'Reel 3 — 5 Tools vs 1', 'caption' => "Things I stopped paying for after switching to one platform:\n\n❌ Separate shop\n❌ Accounting export hell\n❌ CRM spreadsheet\n❌ Manual VAT\n❌ Inventory sync nightmares\n\n✅ One login\n\n#TechTok #BusinessTips #DubaiBusiness #ERP"),
			array('title' => 'Reel 4 — AI Demo Layla', 'caption' => "No sales call. No waiting.\n\nMeet Layla — AI walks you through YOUR industry demo.\n\nTap link in bio. 3 days free.\n\n#AI #SaaS #Ecommerce #LearnOnTikTok #UAEBusiness"),
		),
	);
	return $posts[$platform] ?? array();
}

/** @return array<int, array<string, string>> */
function epc_social_instagram_reels_ideas(): array
{
	return array(
		array('title' => 'Carousel — 5 features', 'caption' => "Slide 1: Problem (5 tools)\nSlide 2: One platform\nSlide 3: ERP + VAT\nSlide 4: B2B portal\nSlide 5: CTA + link in bio"),
		array('title' => 'Reel — Order to invoice', 'caption' => "15 sec screen record: customer order → stock deducts → invoice PDF → VAT entry. Text overlay each step."),
		array('title' => 'Reel — Industry template', 'caption' => "Split screen: generic Shopify vs your industry CP (auto/electronics/fashion). End with your domain."),
		array('title' => 'Story series — Day in CP', 'caption' => "Morning orders → pick list → dispatch → bank rec. Stickers: polls \"Still on Excel?\""),
	);
}

/** @return array<string, string> */
function epc_social_tiktok_specs(): array
{
	return array(
		'Aspect ratio' => '9:16 vertical (1080×1920 recommended)',
		'Duration' => '15–60 sec (hooks in first 2 sec)',
		'Captions' => 'Burn-in text + platform auto-captions (Arabic/Urdu for GCC/PK)',
		'Safe zone' => 'Keep logos/text away from bottom 250px (UI overlap)',
		'Posting' => '3–5× per week; repurpose Instagram Reels',
		'CTA' => 'Link in bio → storefront or demo URL',
	);
}

function epc_social_pack_posts_for_brand(string $platform, array $brand): array
{
	$posts = epc_social_pack_posts($platform);
	$out = array();
	foreach ($posts as $post) {
		$out[] = array(
			'title' => epc_social_adapt_text((string) $post['title'], $brand),
			'caption' => epc_social_adapt_text((string) $post['caption'], $brand),
		);
	}
	return $out;
}

function epc_social_x_thread_starter(): string
{
	return "🧵 Why UAE businesses are switching to ECOM AE (a thread):\n\n1/ Most UAE trading businesses run on 4-5 disconnected systems: Shopify + QuickBooks + a CRM + spreadsheets. Every month = hours of manual reconciliation.\n\n2/ ECOM AE built one stack where orders flow directly into ERP. Customer places order → inventory deducts → finance journals post → VAT entry created. Automatically.\n\n3/ UAE compliance is built in: Peppol / PINT-AE e-invoicing, FTA XML export, VAT returns. Not a plugin. Not an add-on. Core features.\n\n4/ Auto parts? VIN search, OEM lookup, cross-reference catalog, supplier pricing, B2B portal. All in one place.\n\n5/ Multi-tenant architecture means agencies and operators can manage 100+ client businesses from one Super CP console. One codebase. Isolated data. Central monitoring.\n\n6/ 3-day free sandbox demo with Layla — the AI guide. No card required. See your industry template live.\n\n→ ecomae.com";
}
