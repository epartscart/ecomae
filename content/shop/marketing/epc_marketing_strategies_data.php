<?php
/**
 * Marketing & growth — 10 strategy playbooks (guidelines, tasks, reviews, KPIs).
 */
defined('_ASTEXE_') or die('No access');

function epc_marketing_strategies(): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_site_context.php';
	$domain = epc_site_domain();
	if ($domain === '' && isset($GLOBALS['DP_Config'])) {
		$domain = rtrim((string) $GLOBALS['DP_Config']->domain_path, '/');
	}
	$host = epc_site_host();
	return array(
		'measurement' => array(
			'key' => 'measurement',
			'title' => '1. Measurement',
			'icon' => 'fa-line-chart',
			'color' => '#2563eb',
			'summary' => 'Track visitors, searches, cart, WhatsApp clicks, and orders so you know what works.',
			'timeline' => '0–7 days',
			'guidelines' => array(
				array(
					'title' => 'Why measure first',
					'body' => 'You already have Google Analytics (G-J19D1KHXCG) on storefront templates. Without Search Console and conversion goals you cannot see which channels drive orders.',
				),
				array(
					'title' => 'Set up now',
					'body' => '<ul>
<li><strong>Google Search Console</strong> — verify <strong>' . htmlspecialchars($host !== '' ? $host : 'your domain', ENT_QUOTES, 'UTF-8') . '</strong>, submit sitemap: <code>' . $domain . '/sitemap-products.php</code></li>
<li><strong>Google Analytics 4</strong> — mark conversions: purchase, add_to_cart, WhatsApp click, part search submit</li>
<li><strong>Google Business Profile</strong> — UAE address, hours, phone, WhatsApp link</li>
<li><strong>Weekly dashboard</strong> — record KPIs in the Results tab below (or export GA weekly)</li>
</ul>',
				),
				array(
					'title' => 'What to monitor weekly',
					'body' => 'Sessions, organic clicks (Search Console), orders, revenue, cart abandonment, top search queries, mobile vs desktop, GCC vs international traffic.',
				),
			),
			'follow_tasks' => array(
				'gsc_verify' => 'Verify site in Google Search Console',
				'gsc_sitemap' => 'Submit sitemap-products.php in Search Console',
				'ga_conversions' => 'Configure GA4 conversion events (order, cart, WhatsApp)',
				'gbp_profile' => 'Create/claim Google Business Profile (UAE)',
				'baseline_kpi' => 'Record baseline KPIs in Results tab (week 1)',
				'weekly_routine' => 'Schedule weekly 30-min metrics review (calendar)',
			),
			'review_checklist' => array(
				'Are organic clicks trending up vs last month?',
				'Do conversion rates match industry (auto parts 1–3%)?',
				'Which landing pages have high bounce rate?',
				'Are GA and CP order counts aligned?',
				'Any tracking broken after deploy?',
			),
			'kpis' => array(
				'monthly_sessions' => array('label' => 'Monthly sessions', 'unit' => 'visits', 'target' => 'Grow 10% MoM', 'source' => 'GA4'),
				'organic_clicks' => array('label' => 'Organic search clicks', 'unit' => 'clicks', 'target' => 'Grow 15% MoM', 'source' => 'Search Console'),
				'conversion_rate' => array('label' => 'Order conversion rate', 'unit' => '%', 'target' => '1–3%', 'source' => 'GA4 / orders'),
				'whatsapp_clicks' => array('label' => 'WhatsApp button clicks', 'unit' => 'clicks', 'target' => 'Track trend', 'source' => 'GA4 event'),
			),
			'links' => array(
				array('label' => 'Sitemap', 'url' => $domain . '/sitemap-products.php', 'external' => true),
				array('label' => 'Google Analytics', 'url' => 'https://analytics.google.com/', 'external' => true),
				array('label' => 'Search Console', 'url' => 'https://search.google.com/search-console', 'external' => true),
			),
		),
		'seo' => array(
			'key' => 'seo',
			'title' => '2. SEO',
			'icon' => 'fa-search',
			'color' => '#059669',
			'summary' => 'Rank for part numbers, brand + model, and vehicle-specific queries — free long-term traffic.',
			'timeline' => '1–6 months',
			'guidelines' => array(
				array(
					'title' => 'High-intent keywords',
					'body' => 'Target: <code>[brand] [part number]</code>, <code>[part] for [Toyota Camry 2018]</code>, VIN lookup, cross-reference / interchange pages.',
				),
				array(
					'title' => 'On-site SEO',
					'body' => '<ul>
<li>Unique title + meta description per brand/category/top parts</li>
<li>Mobile speed — most GCC buyers are on phone</li>
<li>Arabic + English landing pages where possible</li>
<li>Product schema (JSON-LD) on popular part pages</li>
<li>Expand sitemap: vehicles, categories, guides</li>
<li>Internal links: vehicle catalog → part search → crosses</li>
</ul>',
				),
				array(
					'title' => 'Content that ranks',
					'body' => 'Publish guides: “Find parts by VIN”, “OEM vs aftermarket UAE”, brand-specific pages (Toyota, Nissan, BMW), shipping FAQ for GCC.',
				),
			),
			'follow_tasks' => array(
				'audit_titles' => 'Audit top 50 pages — unique titles & meta descriptions',
				'top500_landing' => 'Plan SEO landing pages for top 500 searched parts',
				'vehicle_pages' => 'Index vehicle catalog URLs in sitemap / internal links',
				'content_plan' => 'Create 4-week content calendar (2 guides/week)',
				'schema_jsonld' => 'Add Product schema on bestseller part pages',
				'ar_pages' => 'Add Arabic titles/descriptions on key category pages',
				'core_web_vitals' => 'Run PageSpeed / Core Web Vitals on mobile homepage + search',
			),
			'review_checklist' => array(
				'New keywords appearing in Search Console?',
				'Top 10 pages gaining impressions?',
				'Duplicate/thin content issues?',
				'Sitemap indexed URL count growing?',
				'Part-number URLs getting clicks?',
			),
			'kpis' => array(
				'indexed_pages' => array('label' => 'Indexed pages', 'unit' => 'pages', 'target' => 'Increase monthly', 'source' => 'Search Console'),
				'avg_position' => array('label' => 'Avg position (top queries)', 'unit' => 'rank', 'target' => '< 20', 'source' => 'Search Console'),
				'organic_orders' => array('label' => 'Orders from organic', 'unit' => 'orders', 'target' => 'Grow 10% MoM', 'source' => 'GA4 UTM/natural'),
				'backlinks' => array('label' => 'Referring domains', 'unit' => 'domains', 'target' => 'Grow steadily', 'source' => 'Ahrefs/GSC'),
			),
			'links' => array(
				array('label' => 'Vehicle catalog', 'url' => $domain . '/en/parts', 'external' => true),
				array('label' => 'Product sitemap', 'url' => $domain . '/sitemap-products.php', 'external' => true),
				array('label' => 'robots.txt', 'url' => $domain . '/robots.txt', 'external' => true),
			),
		),
		'paid_ads' => array(
			'key' => 'paid_ads',
			'title' => '3. Paid ads',
			'icon' => 'fa-bullhorn',
			'color' => '#dc2626',
			'summary' => 'Google Search + Shopping + Meta retargeting for fastest traffic — start with exact part numbers.',
			'timeline' => '0–30 days',
			'guidelines' => array(
				array(
					'title' => 'Channel priority',
					'body' => '<table class="table table-condensed"><tr><th>Channel</th><th>Best for</th></tr>
<tr><td>Google Search</td><td>Part numbers, car models</td></tr>
<tr><td>Google Shopping</td><td>Product feed with price/stock</td></tr>
<tr><td>Meta (FB/IG)</td><td>Retargeting, brand awareness</td></tr>
<tr><td>WhatsApp Click-to-Chat</td><td>GCC quote requests</td></tr></table>',
				),
				array(
					'title' => 'Budget guide',
					'body' => 'Low: pause broad keywords. Medium: AED/USD 500–2000/mo on exact match part numbers. Aggressive: + Shopping + Meta retargeting.',
				),
				array(
					'title' => 'Rules',
					'body' => 'Never bid on generic “auto parts” first — too expensive. Use negative keywords. Landing page = part search or exact part. Track ROAS weekly.',
				),
			),
			'follow_tasks' => array(
				'google_ads_account' => 'Create Google Ads account + link Analytics',
				'search_campaign' => 'Launch Search campaign — top 100 part numbers',
				'negative_keywords' => 'Add negative keyword list (free, jobs, repair manual)',
				'meta_pixel' => 'Install Meta Pixel + retarget site visitors',
				'whatsapp_ads' => 'Test Click-to-WhatsApp ad (GCC geo)',
				'landing_audit' => 'Ensure ads land on part search / cart — not homepage only',
				'roas_sheet' => 'Track spend vs revenue weekly in KPI log',
			),
			'review_checklist' => array(
				'Cost per acquisition below margin?',
				'Search terms report — any junk clicks?',
				'Ad copy A/B test results?',
				'Retargeting audience size sufficient?',
				'WhatsApp leads converting to orders?',
			),
			'kpis' => array(
				'ad_spend' => array('label' => 'Monthly ad spend', 'unit' => 'AED', 'target' => 'Within budget', 'source' => 'Ads dashboard'),
				'cpc' => array('label' => 'Avg CPC (Search)', 'unit' => 'AED', 'target' => 'Optimize down', 'source' => 'Google Ads'),
				'roas' => array('label' => 'ROAS', 'unit' => 'x', 'target' => '> 3x', 'source' => 'Ads + orders'),
				'paid_orders' => array('label' => 'Paid channel orders', 'unit' => 'orders', 'target' => 'Grow', 'source' => 'UTM tracking'),
			),
			'links' => array(
				array('label' => 'Google Ads', 'url' => 'https://ads.google.com/', 'external' => true),
				array('label' => 'Meta Business', 'url' => 'https://business.facebook.com/', 'external' => true),
			),
		),
		'marketplaces' => array(
			'key' => 'marketplaces',
			'title' => '4. Marketplaces',
			'icon' => 'fa-amazon',
			'color' => '#f59e0b',
			'summary' => 'List inventory on Amazon, Noon, eBay, Dubizzle — buyers already shop there.',
			'timeline' => '1–3 months',
			'guidelines' => array(
				array(
					'title' => 'Where to list',
					'body' => '<ul>
<li><strong>Amazon.ae / global</strong> — high trust, strict requirements</li>
<li><strong>Noon, Dubizzle</strong> — UAE local</li>
<li><strong>eBay</strong> — international OEM/rare parts</li>
<li><strong>CP Channels module</strong> — Amazon/eBay sync hub on this site</li>
</ul>',
				),
				array(
					'title' => 'Workflow',
					'body' => 'Map SKUs in Channels → sync stock → import marketplace orders → fulfil via Logistics (carriers). Each listing should link back to your storefront (' . $domain . ') for brand recall.',
				),
			),
			'follow_tasks' => array(
				'amazon_seller' => 'Register Amazon Seller / Noon partner account',
				'feed_top_sku' => 'Export top 200 SKUs with price/stock/images',
				'channels_sku_map' => 'Complete SKU map in CP Channels',
				'ebay_listings' => 'List rare/OEM parts on eBay (international)',
				'dubizzle_batch' => 'Batch-list fast movers on Dubizzle/Marketplace',
				'order_import_test' => 'Test marketplace order import → shop order flow',
				'pricing_rules' => 'Define marketplace markup vs web price',
			),
			'review_checklist' => array(
				'Stock sync accurate across channels?',
				'Marketplace order SLA met?',
				'Return rate acceptable?',
				'Which channel has best margin?',
				'Duplicate listing conflicts resolved?',
			),
			'kpis' => array(
				'marketplace_orders' => array('label' => 'Marketplace orders', 'unit' => 'orders', 'target' => 'Grow', 'source' => 'CP Channels / live'),
				'sku_mapped' => array('label' => 'SKUs mapped', 'unit' => 'SKUs', 'target' => '200+', 'source' => 'CP Channels'),
				'channel_revenue' => array('label' => 'Marketplace revenue', 'unit' => 'AED', 'target' => '10% of total', 'source' => 'Finance'),
				'stock_sync_errors' => array('label' => 'Sync errors', 'unit' => 'count', 'target' => '0', 'source' => 'Channel sync log'),
			),
			'links' => array(
				array('label' => 'CP Channels hub', 'url' => '/cp/shop/channels/channels', 'external' => false),
				array('label' => 'Channels guide', 'url' => '/cp/shop/channels/guide', 'external' => false),
			),
		),
		'whatsapp_social' => array(
			'key' => 'whatsapp_social',
			'title' => '5. WhatsApp & social',
			'icon' => 'fa-whatsapp',
			'color' => '#25d366',
			'summary' => 'Use wa.me sharing, bilingual quotes, short video, and WhatsApp ads — huge in GCC.',
			'timeline' => '0–30 days',
			'guidelines' => array(
				array(
					'title' => 'Already live on site',
					'body' => 'Part search, cart, header, and CP order card have WhatsApp share (EN+AR). Phase 2 API sends order notifications when Meta credentials are set.',
				),
				array(
					'title' => 'Growth tactics',
					'body' => '<ul>
<li>WhatsApp Status / Instagram Reels — part lookup demos, same-day dispatch</li>
<li>TikTok / YouTube Shorts — “Find any part in 30 seconds”</li>
<li>Encourage customers to share part links via WhatsApp</li>
<li>Click-to-WhatsApp ads targeting UAE/GCC</li>
</ul>',
				),
			),
			'follow_tasks' => array(
				'wa_business_profile' => 'Complete WhatsApp Business profile (catalog, hours)',
				'social_calendar' => 'Post 3×/week: part tip, brand spotlight, customer win',
				'short_video' => 'Record 3 screen-capture demos (search, VIN, cart share)',
				'wa_phase2_creds' => 'Add Meta API token for automated order WhatsApp (Phase 2)',
				'track_wa_clicks' => 'Track WhatsApp clicks as GA4 event',
				'influencer_garages' => 'Partner with 5 local garages for shoutouts',
			),
			'review_checklist' => array(
				'WhatsApp response time under 15 min?',
				'Social posts driving site clicks?',
				'Quote-to-order rate from WhatsApp?',
				'Phase 2 notification delivery success rate?',
				'Bilingual messages working for customers?',
			),
			'kpis' => array(
				'wa_inbound_chats' => array('label' => 'WhatsApp inbound chats', 'unit' => 'chats', 'target' => 'Grow weekly', 'source' => 'WA Business'),
				'wa_orders' => array('label' => 'Orders via WhatsApp', 'unit' => 'orders', 'target' => 'Track %', 'source' => 'CP orders tag'),
				'social_referrals' => array('label' => 'Social referrals', 'unit' => 'sessions', 'target' => 'Grow', 'source' => 'GA4'),
				'wa_api_sent' => array('label' => 'API notifications sent', 'unit' => 'msgs', 'target' => 'High success', 'source' => 'live'),
			),
			'links' => array(
				array('label' => 'WhatsApp guide (CP)', 'url' => '/cp/shop/orders/whatsapp-guide', 'external' => false),
				array('label' => 'Configuration', 'url' => '/cp/control/config', 'external' => false),
			),
		),
		'trust' => array(
			'key' => 'trust',
			'title' => '6. Trust signals',
			'icon' => 'fa-shield',
			'color' => '#6366f1',
			'summary' => 'Worldwide buyers need shipping clarity, policies, reviews, and visible company details.',
			'timeline' => '0–14 days',
			'guidelines' => array(
				array(
					'title' => 'Must be visible',
					'body' => '<ul>
<li>Shipping countries, times, costs</li>
<li>Return & warranty policy</li>
<li>Company registration, UAE address, phone</li>
<li>Google reviews, secure checkout badges</li>
<li>Live stock / warehouse labels (S-UAE, R-UAE)</li>
</ul>',
				),
			),
			'follow_tasks' => array(
				'shipping_page' => 'Publish clear shipping & delivery page (GCC + international)',
				'returns_policy' => 'Publish returns/warranty policy page',
				'about_page' => 'Update About Us with registration, address, team photo',
				'google_reviews' => 'Ask 10 happy customers for Google reviews',
				'trust_badges' => 'Add SSL, payment icons, UAE license on footer/checkout',
				'stock_transparency' => 'Show warehouse/stock labels on part search (already multi-warehouse)',
			),
			'review_checklist' => array(
				'Checkout abandonment dropping?',
				'Support tickets about trust/shipping down?',
				'Review score improving?',
				'Policies match actual operations?',
				'International buyers completing checkout?',
			),
			'kpis' => array(
				'google_rating' => array('label' => 'Google rating', 'unit' => 'stars', 'target' => '4.5+', 'source' => 'Google Business'),
				'review_count' => array('label' => 'Google review count', 'unit' => 'reviews', 'target' => '50+', 'source' => 'Google Business'),
				'checkout_completion' => array('label' => 'Checkout completion', 'unit' => '%', 'target' => 'Improve', 'source' => 'GA4 funnel'),
				'trust_bounce' => array('label' => 'Policy page bounce', 'unit' => '%', 'target' => '< 60%', 'source' => 'GA4'),
			),
			'links' => array(
				array('label' => 'Logistics guide', 'url' => '/cp/shop/logistics/guide', 'external' => false),
				array('label' => 'Payment gateways', 'url' => '/cp/shop/payments/payments', 'external' => false),
			),
		),
		'international' => array(
			'key' => 'international',
			'title' => '7. International',
			'icon' => 'fa-globe',
			'color' => '#0891b2',
			'summary' => 'Expand in phases: GCC → South Asia/Africa → Europe/US for rare/OEM parts.',
			'timeline' => '3–12 months',
			'guidelines' => array(
				array(
					'title' => 'Phased rollout',
					'body' => '<ol>
<li><strong>UAE + GCC</strong> — Saudi, Oman, Qatar, Kuwait, Bahrain (Arabic content, regional carriers)</li>
<li><strong>South Asia / Africa</strong> — price-sensitive, WhatsApp-friendly</li>
<li><strong>Europe / US</strong> — rare/OEM/hard-to-find; harder logistics</li>
</ol>',
				),
				array(
					'title' => 'Per region',
					'body' => 'Local currency display, shipping calculator, local payments (Tabby, Tamara, PayPal), hreflang for /en/ and /ar/.',
				),
			),
			'follow_tasks' => array(
				'gcc_shipping' => 'Enable GCC shipping rates in Logistics carriers',
				'currency_display' => 'Review multi-currency display on storefront',
				'hreflang_audit' => 'Audit EN/AR URLs and hreflang tags',
				'export_docs' => 'Document customs/commercial invoice process',
				'region_landing' => 'Create “Ship to Saudi” / “Ship to Africa” landing pages',
				'payment_international' => 'Enable PayPal/international cards if not live',
			),
			'review_checklist' => array(
				'International order volume growing?',
				'Carrier delivery times met per region?',
				'Customs delays or returns?',
				'Which countries have highest AOV?',
				'Arabic content helping GCC conversion?',
			),
			'kpis' => array(
				'intl_orders_pct' => array('label' => 'International orders', 'unit' => '%', 'target' => 'Grow', 'source' => 'CP orders'),
				'gcc_orders' => array('label' => 'GCC orders (non-UAE)', 'unit' => 'orders', 'target' => 'Grow', 'source' => 'CP orders'),
				'intl_shipping_time' => array('label' => 'Avg intl delivery days', 'unit' => 'days', 'target' => '< 10 GCC', 'source' => 'Logistics'),
				'intl_revenue' => array('label' => 'International revenue', 'unit' => 'AED', 'target' => '15% of total', 'source' => 'Finance'),
			),
			'links' => array(
				array('label' => 'Carriers & shipments', 'url' => '/cp/shop/logistics/carriers', 'external' => false),
				array('label' => 'Demand by country', 'url' => $domain . '/demand_intelligence', 'external' => true),
			),
		),
		'email_retention' => array(
			'key' => 'email_retention',
			'title' => '8. Email & retention',
			'icon' => 'fa-envelope',
			'color' => '#9333ea',
			'summary' => 'Capture emails, recover abandoned carts, back-in-stock alerts — cheap repeat traffic.',
			'timeline' => '14–60 days',
			'guidelines' => array(
				array(
					'title' => 'Tactics',
					'body' => '<ul>
<li>Email capture on quote/cart</li>
<li>Abandoned cart sequence (1h, 24h, 72h)</li>
<li>Back-in-stock for searched parts</li>
<li>Monthly newsletter: new brands, deals, vehicle offers</li>
<li>Re-engage users who searched but did not buy</li>
</ul>',
				),
			),
			'follow_tasks' => array(
				'esp_setup' => 'Choose ESP (Mailchimp, Brevo, Klaviyo) or use existing SMTP',
				'cart_abandon_flow' => 'Build 3-step abandoned cart email flow',
				'newsletter_signup' => 'Add footer newsletter signup + incentive',
				'back_in_stock' => 'Plan back-in-stock notification for top SKUs',
				'segment_b2b' => 'Segment garage/fleet customers for trade offers',
				'unsubscribe_compliance' => 'Ensure unsubscribe link and GDPR-style consent',
			),
			'review_checklist' => array(
				'Open rate above 20%?',
				'Cart recovery revenue tracked?',
				'Unsubscribe rate below 0.5%?',
				'List growth vs spam complaints?',
				'Repeat purchase rate improving?',
			),
			'kpis' => array(
				'email_list_size' => array('label' => 'Email list size', 'unit' => 'subscribers', 'target' => 'Grow 5% MoM', 'source' => 'ESP'),
				'open_rate' => array('label' => 'Campaign open rate', 'unit' => '%', 'target' => '> 20%', 'source' => 'ESP'),
				'cart_recovery' => array('label' => 'Cart recovery revenue', 'unit' => 'AED', 'target' => 'Track', 'source' => 'ESP + orders'),
				'repeat_customers' => array('label' => 'Repeat customers', 'unit' => '%', 'target' => '25%+', 'source' => 'CP users'),
			),
			'links' => array(
				array('label' => 'CP Configuration (SMTP)', 'url' => '/cp/control/config', 'external' => false),
				array('label' => 'Orders', 'url' => '/cp/shop/orders/orders', 'external' => false),
			),
		),
		'partnerships' => array(
			'key' => 'partnerships',
			'title' => '9. Partnerships & B2B',
			'icon' => 'fa-handshake-o',
			'color' => '#0d9488',
			'summary' => 'Garages, fleets, insurance shops, export agents — trade accounts and bulk quotes.',
			'timeline' => '1–6 months',
			'guidelines' => array(
				array(
					'title' => 'Target partners',
					'body' => 'Independent garages, fleet operators, insurance repair networks, Africa/Asia export agents. Offer: trade pricing, dedicated WhatsApp line, bulk quotes, net-30 for approved accounts.',
				),
			),
			'follow_tasks' => array(
				'b2b_price_list' => 'Create trade price tier / customer group in CP',
				'outreach_20' => 'Outreach to 20 garages (email + WhatsApp intro)',
				'fleet_pitch' => 'Prepare fleet company one-pager PDF',
				'approval_workflow' => 'Use customer approval module for B2B onboarding',
				'partner_landing' => 'Create “Trade & garage accounts” landing page',
				'monthly_review' => 'Monthly partner pipeline review meeting',
			),
			'review_checklist' => array(
				'B2B revenue share growing?',
				'Partner satisfaction / reorder rate?',
				'Credit terms under control?',
				'Which partner type converts best?',
				'LPO / WhatsApp workflow smooth for B2B?',
			),
			'kpis' => array(
				'b2b_accounts' => array('label' => 'Active B2B accounts', 'unit' => 'accounts', 'target' => '20+', 'source' => 'CP users'),
				'b2b_revenue' => array('label' => 'B2B revenue', 'unit' => 'AED', 'target' => '30% of total', 'source' => 'Finance'),
				'b2b_orders' => array('label' => 'B2B orders', 'unit' => 'orders', 'target' => 'Grow', 'source' => 'CP orders'),
				'partner_pipeline' => array('label' => 'Partners in pipeline', 'unit' => 'leads', 'target' => '10+ active', 'source' => 'CRM/sheet'),
			),
			'links' => array(
				array('label' => 'Customer approvals', 'url' => '/cp/users/epc_customer_approvals', 'external' => false),
				array('label' => 'ERP Finance', 'url' => '/cp/shop/finance/erp', 'external' => false),
			),
		),
		'quick_wins' => array(
			'key' => 'quick_wins',
			'title' => '10. Site quick wins',
			'icon' => 'fa-bolt',
			'color' => '#ea580c',
			'summary' => 'EpartsCart-specific improvements — sitemap, top parts, vehicle pages, WhatsApp tracking.',
			'timeline' => '0–30 days',
			'guidelines' => array(
				array(
					'title' => 'Already on your site',
					'body' => '<ul>
<li>Google Analytics G-J19D1KHXCG</li>
<li>sitemap-products.php in robots.txt</li>
<li>Part search, crosses, vehicle catalog, demand intelligence</li>
<li>WhatsApp share EN+AR on search, cart, orders</li>
<li>Multi-warehouse (S-UAE / R-UAE) labels</li>
<li>Channels + Logistics + Payments CP modules</li>
</ul>',
				),
				array(
					'title' => 'Do next',
					'body' => 'Link Search Console, create landing pages for top 500 parts, track WhatsApp in GA, list on marketplaces via Channels, enable Phase 2 WhatsApp notifications.',
				),
			),
			'follow_tasks' => array(
				'gsc_link' => 'Link Search Console + submit sitemap (if not done)',
				'top_parts_seo' => 'Export top searches from demand intelligence → SEO pages',
				'wa_ga_event' => 'Confirm WhatsApp click events in GA4',
				'channels_sample' => 'Load Channels sample data + map 10 real SKUs',
				'mobile_speed' => 'Fix top 3 mobile PageSpeed issues on homepage',
				'cp_marketing_weekly' => 'Use this Marketing hub weekly — update all 10 strategy tabs',
			),
			'review_checklist' => array(
				'All 10 strategies have progress updated this week?',
				'Live metrics (orders, users) trending up?',
				'Any CP module unused that could help growth?',
				'Security/backup current before campaigns?',
				'90-day plan on track?',
			),
			'kpis' => array(
				'strategy_completion' => array('label' => 'Strategy task completion', 'unit' => '%', 'target' => '80%+', 'source' => 'This hub'),
				'site_orders_30d' => array('label' => 'Orders (30 days)', 'unit' => 'orders', 'target' => 'Grow', 'source' => 'live'),
				'registered_users' => array('label' => 'Registered users', 'unit' => 'users', 'target' => 'Grow', 'source' => 'live'),
				'price_rows' => array('label' => 'Catalog price rows', 'unit' => 'rows', 'target' => 'Grow', 'source' => 'live'),
			),
			'links' => array(
				array('label' => 'Demand intelligence', 'url' => $domain . '/demand_intelligence', 'external' => true),
				array('label' => 'Part search', 'url' => $domain . '/shop/part_search', 'external' => true),
				array('label' => 'CP guideline', 'url' => '/cp/control/cp_guideline', 'external' => false),
			),
		),
	);
}
