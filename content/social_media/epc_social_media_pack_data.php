<?php
/**
 * Social media content pack — modern ready-to-post captions + video library.
 * Source copy uses ECOM AE / ecomae.com so epc_social_adapt_text() can retarget tenants.
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
			'intro' => 'Decision-maker feed. Lead with outcomes — stock accuracy, faster quotes, UAE compliance — then proof and a clear CTA.',
			'hashtags' => array('#ECOMAE', '#UAEBusiness', '#Dubai', '#ERP', '#Ecommerce', '#DigitalTransformation', '#UAECompliance', '#UAEVAT', '#Einvoicing', '#FTA', '#SaaS', '#B2B', '#AutoParts', '#DubaiTech', '#CloudERP', '#SME', '#Wholesale', '#GCC', '#SpareParts', '#TradeTech'),
		),
		'instagram' => array(
			'label' => 'Instagram',
			'intro' => 'Hook in line one. Pair with bold visuals or Reels. Carousels for feature lists; Reels for 10–20s demos.',
			'hashtags' => array('#ECOMAE', '#DubaiTech', '#UAEStartup', '#Ecommerce', '#DubaiBusiness', '#UAEBusiness', '#MadeForUAE', '#ERP', '#CloudPlatform', '#B2BUAE', '#UAERetail', '#AutoPartsUAE', '#SpareParts', '#OnlineBusiness', '#AI', '#GCCBusiness', '#WholesaleUAE', '#UAEVat', '#VINSearch', '#PartsLookup'),
		),
		'facebook' => array(
			'label' => 'Facebook',
			'intro' => 'Conversational, slightly longer. Strong for UAE trading communities. Ask a question + CTA; use link previews.',
			'hashtags' => array('#UAEBusiness', '#DubaiSME', '#UAEEntrepreneur', '#DubaiTrading', '#B2BUAE', '#AutoPartsUAE', '#UAERetail', '#EcommerceUAE', '#ECOMAE', '#WholesaleUAE', '#GCCBusiness', '#UAECompliance', '#SparePartsUAE'),
		),
		'x' => array(
			'label' => 'X / Twitter',
			'intro' => 'Punchy and direct (≤280 chars). Bold claims + proof. Threads for deeper feature walks.',
			'hashtags' => array('#UAECompliance', '#Ecommerce', '#ERP', '#AutoParts', '#UAE', '#B2B', '#FreeTrial', '#ECOMAE', '#SpareParts'),
		),
		'tiktok' => array(
			'label' => 'TikTok',
			'intro' => 'Vertical 9:16, 15–60s. Hook in 2 seconds. Burn-in captions; Arabic/Urdu subtitles lift GCC & Pakistan reach.',
			'hashtags' => array('#B2B', '#EcommerceTips', '#SmallBusiness', '#UAEBusiness', '#ERP', '#AutoParts', '#DubaiBusiness', '#TechTok', '#LearnOnTikTok', '#SpareParts', '#VINSearch'),
		),
	);
}

/** @return array<int, array<string, string>> */
function epc_social_pack_posts(string $platform): array
{
	$posts = array(
		'linkedin' => array(
			array(
				'title' => 'Post 1 — One stack for spare parts',
				'caption' => "Most UAE spare-parts teams still quote on WhatsApp and reconcile stock in Excel.\n\nECOM AE puts storefront, warehouse stock, crosses, ERP, and B2B accounts on one database:\n✅ Part search + cross references\n✅ Multi-warehouse availability\n✅ Retail & wholesale pricing profiles\n✅ UAE VAT & e-invoice ready\n✅ Orders that post straight into finance\n\nStop stitching five tools. Run one stack.\n\n👉 Live demo: ecomae.com · See it in market: epartscart.com\n\n#ECOMAE #AutoParts #UAEBusiness #ERP #B2B #SpareParts",
			),
			array(
				'title' => 'Post 2 — Prices only for approved buyers',
				'caption' => "Guest browsers should see catalogues — not your margin.\n\nOn ECOM AE / eParts Cart:\n🔒 Guests see availability & price as ***\n✅ Retail customers unlock prices instantly\n⏳ Wholesale stays pending until CP approval\n\nProtect your trade pricing without hiding your catalogue.\n\nBuilt for UAE B2B auto parts — not a bolted-on plugin.\n\n🔗 ecomae.com | epartscart.com\n\n#B2B #Wholesale #UAEBusiness #AutoParts #TradePricing",
			),
			array(
				'title' => 'Post 3 — UAE compliance without the scramble',
				'caption' => "E-invoicing and VAT workflows are expanding across the UAE.\n\nECOM AE ships compliance as core — not a retrofit:\n📋 Peppol / PINT-AE ready invoicing\n📋 FTA export paths\n📋 TRN-aware tax documents\n📋 VAT-aware pricing for retail & wholesale\n\nPrepare once. Operate every day.\n\nExplore: ecomae.com\n\n#UAECompliance #UAEVAT #Einvoicing #FTA #DubaiFintech",
			),
			array(
				'title' => 'Post 4 — Crosses, VIN, warehouse truth',
				'caption' => "Buyers ask: “Do you have it — and which number fits?”\n\nECOM AE’s auto-parts layer answers in one flow:\n🔧 Article + brand search\n🔧 Cross-reference catalogue\n🔧 UAE warehouse stock signals\n🔧 Supplier / storage captions for ops\n🔧 B2B credit & approval workflows\n\nYour competitors are still typing into chat apps.\nYou can be publishing live offers from Control Panel.\n\nepartscart.com · ecomae.com\n\n#AutoParts #SparePartsUAE #VIN #B2B #Ecommerce",
			),
		),
		'instagram' => array(
			array(
				'title' => 'Post 1 — Search → stock → order',
				'caption' => "Part number in. Offer out. 🔧\n\nCrosses · UAE warehouses · login for live prices.\n\nOne platform for spare parts trading — not five tabs.\n\n👉 Link in bio — epartscart.com\n\n#AutoPartsUAE #SpareParts #DubaiBusiness #ECOMAE #B2B",
			),
			array(
				'title' => 'Post 2 — *** until you log in',
				'caption' => "Catalogue open. Margin closed. 🔒\n\nGuests see *** on qty, term, warehouse & price.\nRetail → instant. Wholesale → manager approve.\n\nThat’s how modern B2B parts shops protect pricing.\n\nLink in bio ↗\n\n#Wholesale #TradePricing #UAEBusiness #AutoParts",
			),
			array(
				'title' => 'Post 3 — Meet Layla',
				'caption' => "Meet Layla 🤖 — your AI walkthrough for ECOM AE.\n\nIndustry template → ERP → UAE compliance. No waiting on a sales call.\n\nFree sandbox → link in bio\n\n#AI #DubaiTech #ECOMAE #Ecommerce",
			),
			array(
				'title' => 'Post 4 — Built for UAE traders',
				'caption' => "Auto parts 🔧 Electronics 📱 Fashion 👗 Jewellery 💎\n\nIndustry templates. One cloud. Go live fast.\n\nMade for UAE traders & wholesalers. 🇦🇪\n\nStart free → ecomae.com\n\n#MadeForUAE #WholesaleUAE #ERP #CloudPlatform",
			),
		),
		'facebook' => array(
			array(
				'title' => 'Post 1 — Still quoting on WhatsApp?',
				'caption' => "Still juggling Excel stock + WhatsApp quotes + a separate shop?\n\nThere’s a cleaner way:\n🛒 Storefront with part search & crosses\n📦 Multi-warehouse availability\n💰 Retail / wholesale pricing rules\n📋 UAE VAT & invoice workflows\n🤝 Trade accounts with approval\n\nOne login. One database.\n\n👇 Try the demo at ecomae.com — or browse epartscart.com",
			),
			array(
				'title' => 'Post 2 — Free sandbox, real workflow',
				'caption' => "🎉 Free sandbox — no card required.\n\nWalk the full flow with Layla (AI guide):\n🔧 Spare parts catalogue & VIN-style lookup\n📦 Stock + crosses\n🧾 Orders into ERP\n🇦🇪 VAT-aware documents\n\n👉 Start at ecomae.com\n\nWhat do you sell? Drop it in the comments 👇",
			),
			array(
				'title' => 'Post 3 — Protect trade prices',
				'caption' => "📢 For UAE wholesalers: don’t publish your margin to every visitor.\n\nECOM AE / eParts Cart protocol:\n✅ Guests → *** on qty, term, info, price\n✅ Retail → auto-approve, prices unlock\n✅ Wholesale → CP manager approval first\n\nCatalogue stays findable. Pricing stays controlled.\n\n🔗 ecomae.com",
			),
			array(
				'title' => 'Post 4 — B2B portal buyers expect',
				'caption' => "B2B buyers expect more than a chat thread. 📣\n\nGive them:\n💳 Contract / profile pricing\n📋 Order history & re-order\n🏦 Clear account status\n✅ Approval workflows for wholesale\n\nTurn spare-parts wholesale into a modern self-service portal.\n\n👉 Book a look: ecomae.com",
			),
		),
		'x' => array(
			array(
				'title' => 'Post 1 — One cloud',
				'caption' => "UAE spare-parts ops still run on WhatsApp + Excel + a separate shop.\n\nECOM AE replaces the stack.\n\nOne cloud. Search → stock → invoice.\n\necomae.com | epartscart.com",
			),
			array(
				'title' => 'Post 2 — Price protocol',
				'caption' => "Guests: *** on qty, term, warehouse, price.\nRetail: unlock instantly.\nWholesale: CP approve first.\n\nThat’s B2B pricing done right.\n\nepartscart.com\n\n#AutoParts #B2B #UAE",
			),
			array(
				'title' => 'Post 3 — Crosses + stock',
				'caption' => "Article in → crosses out → UAE warehouse signal.\n\nOEM / aftermarket in one search.\n\n#SpareParts #AutoParts #UAE #B2B\n\nepartscart.com",
			),
			array(
				'title' => 'Post 4 — Free demo',
				'caption' => "Free sandbox. No card.\nMeet Layla — AI guide.\n\nSee your industry live, then decide.\n\necomae.com\n\n#FreeTrial #ECOMAE #UAE",
			),
		),
		'tiktok' => array(
			array(
				'title' => 'Reel 1 — WhatsApp quotes → one search',
				'caption' => "POV: Customer sends 12 part numbers on WhatsApp 😅\n\nYou: open search → crosses → stock → quote.\n\nScreen-record the CP / storefront flow.\n\n#AutoParts #UAEBusiness #B2B #SpareParts",
			),
			array(
				'title' => 'Reel 2 — VIN / OEM → cart',
				'caption' => "Stop mistyping part numbers. 🔧\n\nLookup → OEM/aftermarket → add to cart → ERP invoice.\n\nBuilt for UAE / GCC / Pakistan parts desks.\n\n#VIN #OEM #SpareParts #ERP",
			),
			array(
				'title' => 'Reel 3 — *** price lock',
				'caption' => "Guest view: *** *** *** ***\n\nLogin retail → prices unlock.\nWholesale → wait for manager.\n\nProtect your margin on camera.\n\n#TradePricing #B2B #TechTok",
			),
			array(
				'title' => 'Reel 4 — 5 tools vs 1',
				'caption' => "Things I cancelled after one platform:\n❌ Separate shop\n❌ Stock spreadsheet\n❌ Manual VAT scramble\n❌ CRM notepad\n\n✅ One login\n\n#DubaiBusiness #ERP #LearnOnTikTok",
			),
		),
	);
	return $posts[$platform] ?? array();
}

/** @return array<int, array<string, string>> */
function epc_social_instagram_reels_ideas(): array
{
	return array(
		array(
			'title' => 'Carousel — 5 slides',
			'caption' => "1) WhatsApp quoting chaos\n2) One search box\n3) Crosses + UAE stock\n4) *** until approved login\n5) CTA → link in bio / epartscart.com",
		),
		array(
			'title' => 'Reel — Order to invoice',
			'caption' => "15s screen record: offer → cart → stock move → invoice PDF. Text overlay each beat.",
		),
		array(
			'title' => 'Reel — Guest vs login',
			'caption' => "Split: guest *** mask vs logged-in retail price. End with wholesale “awaiting approval” note.",
		),
		array(
			'title' => 'Story — Day in the parts desk',
			'caption' => "Morning orders → pick → dispatch → bank rec. Poll sticker: “Still on Excel?”",
		),
	);
}

/** @return array<string, string> */
function epc_social_tiktok_specs(): array
{
	return array(
		'Aspect ratio' => '9:16 vertical (1080×1920 recommended)',
		'Duration' => '15–60 sec (hook in first 2 sec)',
		'Captions' => 'Burn-in text + platform auto-captions (Arabic/Urdu for GCC/PK)',
		'Safe zone' => 'Keep logos/text away from bottom 250px (UI overlap)',
		'Posting' => '3–5× per week; repurpose Instagram Reels',
		'CTA' => 'Link in bio → storefront or demo URL',
		'Ready videos' => 'Use the sample reels below (host on your CDN / public HTTPS for Publish)',
	);
}

/**
 * Built-in guide + sample reel videos shipped with the hub.
 *
 * @return array<int, array{id:string,title:string,kind:string,url:string,poster:string,aspect:string,blurb:string}>
 */
function epc_social_video_library(): array
{
	$base = '/content/social_media/videos';
	return array(
		array(
			'id' => 'guide-connect',
			'title' => 'Guide — Connect accounts',
			'kind' => 'guide',
			'url' => $base . '/guide-connect-accounts.mp4',
			'poster' => '',
			'aspect' => '16:9',
			'blurb' => 'Paste Meta / TikTok tokens in Connected accounts, then Test connection.',
		),
		array(
			'id' => 'guide-pack',
			'title' => 'Guide — Marketing pack',
			'kind' => 'guide',
			'url' => $base . '/guide-marketing-pack.mp4',
			'poster' => '',
			'aspect' => '16:9',
			'blurb' => 'Copy ready captions, save drafts, or publish live where enabled.',
		),
		array(
			'id' => 'guide-publish',
			'title' => 'Guide — Publish from Drafts',
			'kind' => 'guide',
			'url' => $base . '/guide-publish-drafts.mp4',
			'poster' => '',
			'aspect' => '16:9',
			'blurb' => 'Public HTTPS media URL required for Facebook, Instagram, and TikTok.',
		),
		array(
			'id' => 'reel-vin',
			'title' => 'Sample Reel — VIN → Cart',
			'kind' => 'reel',
			'url' => $base . '/reel-vin-search.mp4',
			'poster' => '',
			'aspect' => '9:16',
			'blurb' => 'Vertical template: lookup → OEM → cart. Swap in your screen recording.',
		),
		array(
			'id' => 'reel-stock',
			'title' => 'Sample Reel — UAE stock',
			'kind' => 'reel',
			'url' => $base . '/reel-stock-parts.mp4',
			'poster' => '',
			'aspect' => '9:16',
			'blurb' => 'Warehouse + crosses narrative; pair with login-to-see-prices CTA.',
		),
		array(
			'id' => 'reel-b2b',
			'title' => 'Sample Reel — B2B portal',
			'kind' => 'reel',
			'url' => $base . '/reel-b2b-portal.mp4',
			'poster' => '',
			'aspect' => '9:16',
			'blurb' => 'Retail auto-approve vs wholesale CP approval — modern trade story.',
		),
		array(
			'id' => 'reel-five',
			'title' => 'Sample Reel — 5 tools vs 1',
			'kind' => 'reel',
			'url' => $base . '/reel-five-tools.mp4',
			'poster' => '',
			'aspect' => '9:16',
			'blurb' => 'Classic before/after: cancel the tool stack, keep one login.',
		),
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
	return "🧵 Why UAE spare-parts teams move to one stack (a thread):\n\n1/ Most desks still run WhatsApp quotes + Excel stock + a separate shop. Every week = reconciliation debt.\n\n2/ ECOM AE connects search → crosses → warehouse → cart → ERP. One database, not five exports.\n\n3/ Pricing protocol: guests see ***; retail unlocks instantly; wholesale needs CP approval. Protect margin without hiding the catalogue.\n\n4/ UAE compliance is core: VAT-aware docs and e-invoice paths — not a plugin afterthought.\n\n5/ Publish social from Control Panel (Facebook / Instagram / TikTok) with encrypted account vault.\n\n6/ Free sandbox + Layla AI guide. No card. See your industry live.\n\n→ ecomae.com · live example epartscart.com";
}
