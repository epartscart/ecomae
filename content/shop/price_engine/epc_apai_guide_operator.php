<?php
/**
 * Auto Price AI — operator-only guide sections (included from epc_apai_guide_shared.php).
 */
defined('_ASTEXE_') or die('No access');
?>

	<details class="epc-ape-guide__section epc-ape-guide__start" open>
		<summary><i class="fa fa-flag-checkered"></i> Quick start — your workflow in 7 steps</summary>
		<div class="epc-ape-guide__section-body">
			<ol class="epc-ape-guide__steps">
				<li>
					<strong>Open Market sources</strong>
					<p><?php if ($industryKey === 'auto_parts') { ?>Go to <a href="<?php echo epc_ape_h($sourcesUrl); ?>">Market sources</a>. Find spare247.com → tick <strong>Requires login</strong> → enter username and password → click <strong>Test login</strong>. Without login, spare247 prices are skipped.<?php } else { ?>Go to <a href="<?php echo epc_ape_h($sourcesUrl); ?>">Market sources</a>. Seeded retailers (Sharaf DG, Jumbo, Noon, Amazon) are ready. Add a custom vendor domain only if you need it.<?php } ?></p>
				</li>
				<li>
					<strong>Set sell marketplaces on Rules</strong>
					<p>Open <a href="<?php echo epc_ape_h($rulesUrl); ?>">Rules</a> → tick <strong>Noon</strong>, <strong>Amazon.ae</strong>, <strong>eBay</strong> (your sell channels only) → set min margin to <?php echo epc_ape_h($marginPct); ?>% → save. Unchecked marketplaces are ignored for gap detection.</p>
				</li>
				<li>
					<strong>Discover → Quick crawl</strong>
					<p><a href="<?php echo epc_ape_h($discoverUrl); ?>">Discover</a> → click <strong>Quick crawl</strong> (~30 sec). <strong>You do not need to pick a product line first</strong> — Quick crawl searches all top lines for your industry automatically. Optional: use the product-line dropdown to narrow results. First-time setup? Run <strong>Full crawl</strong> for a deeper background pass.</p>
				</li>
				<li>
					<strong>Review the right sub-tab</strong>
					<p><?php if ($isArbitrage) { ?>Open <strong>Marketplace opportunities</strong> — products on buy sources but not on your sell marketplaces yet.<?php } elseif ($industryKey === 'auto_parts') { ?>Open <strong>Market confirmed</strong> (same part on 2+ sources) or <strong>Marketplace opportunities</strong> for gaps on Noon/Amazon.<?php } else { ?>Open <strong>Market confirmed</strong> or <strong>All suggestions</strong> after your first crawl.<?php } ?> Sort by margin or newest.</p>
				</li>
				<li>
					<strong>Read each card — four key numbers</strong>
					<p><strong>Buy from (lowest)</strong> = what you pay · <strong>Sell on marketplace</strong> = where you list · <strong>Your price</strong> = your current cost · <strong>Margin</strong> = sell − buy. Green margin = above your <?php echo epc_ape_h($marginPct); ?>% rule.</p>
				</li>
				<li>
					<strong>Add to catalogue</strong>
					<p>Click <strong>Add to my catalogue</strong> on a good card. Import sets cost = buy lowest and sell = marketplace target. Nothing publishes automatically — you approve every import.</p>
				</li>
				<li>
					<strong>Monitor Price changes</strong>
					<p>After import, switch Discover to <strong>Price changes</strong> (or check <a href="<?php echo epc_ape_h($importsUrl); ?>">My imports</a>). When a source price moves (↑/↓), re-price or re-source before margin shrinks.</p>
				</li>
			</ol>
		</div>
	</details>

	<div class="epc-ape-guide__legend" aria-label="Badge legend">
		<div class="epc-ape-guide__legend-card epc-ape-guide__legend-card--green">
			<h5><span class="epc-ape-guide__legend-badge epc-ape-guide__legend-badge--green">Market confirmed</span></h5>
			<p>Same product found on <strong>2+ different websites</strong>. High confidence — safe to import. <?php echo $industryKey === 'auto_parts' ? 'Needs brand + article match.' : 'Model/spec chips must align.'; ?></p>
		</div>
		<div class="epc-ape-guide__legend-card epc-ape-guide__legend-card--blue">
			<h5><span class="epc-ape-guide__legend-badge epc-ape-guide__legend-badge--blue">Arbitrage opportunity</span></h5>
			<p>Product is on a buy source but <strong>not listed</strong> on your sell marketplaces (Noon/Amazon/eBay). You can list first and capture margin.</p>
		</div>
		<div class="epc-ape-guide__legend-card epc-ape-guide__legend-card--amber">
			<h5><span class="epc-ape-guide__legend-badge epc-ape-guide__legend-badge--amber">Buy cheaper</span></h5>
			<p>Your warehouse cost is <strong>higher</strong> than the lowest buy source. Consider switching supplier — e.g. buy from spare247 instead of your current vendor.</p>
		</div>
		<?php if ($industryKey === 'auto_parts') { ?>
		<div class="epc-ape-guide__legend-card epc-ape-guide__legend-card--red">
			<h5><span class="epc-ape-guide__legend-badge epc-ape-guide__legend-badge--red">Part number required</span></h5>
			<p>Auto parts need <strong>Brand + Article</strong> (e.g. Toyota 1310154101). A title alone cannot match reliably — add the part number and re-crawl.</p>
		</div>
		<?php } ?>
	</div>

	<div class="epc-ape-guide__ref">
		<h4 style="margin:0 0 10px;font-size:14px;color:#0f766e"><i class="fa fa-th-list"></i> Tab quick reference</h4>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Tab</th><th>What it does</th><th>When to use</th></tr></thead>
			<tbody>
				<tr><td><strong><i class="fa fa-compass"></i> Discover</strong></td><td>Find products, compare buy vs sell, import winners.</td><td>Daily — crawl, review cards, bulk import.</td></tr>
				<tr><td><strong><i class="fa fa-table"></i> Compare</strong></td><td>Matrix of buy range vs marketplace sell for imported SKUs.</td><td><?php echo $isWarehouse ? 'Check warehouse price list vs market.' : 'Audit margins on imported products.'; ?></td></tr>
				<tr><td><strong><i class="fa fa-globe"></i> Market sources</strong></td><td>Enable domains, B2B login, custom vendors.</td><td><?php echo $industryKey === 'auto_parts' ? 'First setup — spare247 credentials.' : 'Add vendors or toggle sources.'; ?></td></tr>
				<tr><td><strong><i class="fa fa-sliders"></i> Rules</strong></td><td>Min margin %, sell marketplace checkboxes, profile.</td><td>Once at setup; revisit when adding sell channels.</td></tr>
				<tr><td><strong><i class="fa fa-sitemap"></i> Product lines</strong></td><td>Industry taxonomy tree — filter crawls by category.</td><td>Focus crawl on phones, filters, services, etc.</td></tr>
				<tr><td><strong><i class="fa fa-download"></i> My imports</strong></td><td>Already-imported products, duplicates, photo refresh.</td><td>After bulk import — resolve duplicates before publish.</td></tr>
				<tr><td><strong><i class="fa fa-book"></i> Guide</strong></td><td>This page — workflow, badges, FAQ.</td><td>New team members or when margin looks wrong.</td></tr>
			</tbody>
		</table>
	</div>

	<div class="epc-ape-guide__mock-wrap">
		<h4><i class="fa fa-clone"></i> What a Discover card looks like</h4>
		<p class="epc-ape-guide__mock-label">Sample layout — your real cards show live crawl data. Labels below match what you see in Discover.</p>
		<div class="epc-ape-guide__mock">
			<div class="epc-disc-card">
				<div class="epc-disc-card__img epc-disc-card__img--empty"><i class="fa fa-image"></i></div>
				<div class="epc-disc-card__body">
					<h5><?php echo $industryKey === 'auto_parts' ? 'Toyota Oil Filter 1310154101' : 'Samsung Galaxy Buds FE'; ?></h5>
					<?php if ($industryKey === 'auto_parts') { ?>
					<div class="epc-disc-part-identity">
						<span class="label label-default epc-disc-part-identity__brand">Toyota</span>
						<span class="epc-disc-part-identity__article">1310154101</span>
					</div>
					<?php } else { ?>
					<div class="epc-disc-card__specs">
						<span class="epc-disc-spec-chip">Model: SM-R400</span>
						<span class="epc-disc-spec-chip">White</span>
					</div>
					<?php } ?>
					<div class="epc-disc-card__meta">
						<span class="label label-success">Market confirmed · 3 sources</span>
						<?php if ($isArbitrage) { ?><span class="label label-info">Arbitrage opportunity</span><?php } ?>
					</div>
					<div class="epc-disc-price-range epc-disc-price-range--good">
						<div class="epc-disc-price-range__row"><small>Buy from (lowest)</small> <strong><?php echo $industryKey === 'auto_parts' ? '120 AED' : '149 AED'; ?></strong> <span class="epc-disc-price-range__src">(<?php echo $industryKey === 'auto_parts' ? 'spare247.com' : 'sharafdg.com'; ?>)</span></div>
						<div class="epc-disc-price-range__row"><small>Sell on marketplace</small> <strong><?php echo $industryKey === 'auto_parts' ? '189 AED' : '219 AED'; ?></strong> <span class="epc-disc-price-range__src">(noon.com)</span></div>
						<div class="epc-disc-price-range__row"><small>Your price</small> <strong>—</strong></div>
						<div class="epc-disc-price-range__row"><small>Margin</small> <strong style="color:#15803d"><?php echo $industryKey === 'auto_parts' ? '69 AED (57%)' : '70 AED (47%)'; ?></strong></div>
					</div>
					<div class="epc-disc-advice epc-disc-advice--success"><i class="fa fa-check"></i> Good margin — above <?php echo epc_ape_h($marginPct); ?>% rule</div>
					<div class="epc-disc-card__actions">
						<span class="btn btn-success btn-sm btn-block"><i class="fa fa-plus"></i> Add to my catalogue</span>
					</div>
				</div>
			</div>
		</div>
		<div class="epc-ape-guide__mock-callout">
			<span><strong>① Buy from (lowest)</strong>What you pay to source</span>
			<span><strong>② Sell on marketplace</strong>Where you list for sale</span>
			<span><strong>③ Your price</strong>Your warehouse/catalogue cost</span>
			<span><strong>④ Margin</strong>Sell − buy = profit</span>
		</div>
	</div>

	<details class="epc-ape-guide__section" open>
		<summary><i class="fa fa-info-circle"></i> 1. What is Auto Price AI?</summary>
		<div class="epc-ape-guide__section-body">
			<p>Auto Price AI is <strong>multi-source price intelligence</strong> for your business. It crawls market websites, finds the same product on multiple sites, compares buy cost vs sell price, and lets you import winners into your catalogue with one click.</p>
			<p>In plain terms:</p>
			<ul>
				<li><strong>Discover</strong> — find products and prices across many market sources at once.</li>
				<li><strong>Compare</strong> — see buy-source range vs marketplace sell price side by side.</li>
				<li><strong>Import</strong> — add to catalogue with cost = lowest buy price, sell = marketplace target.</li>
				<li><strong>Sell</strong> — list on Noon, Amazon, eBay (your configured sell marketplaces).</li>
			</ul>
			<div style="font-size:12px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:12px 14px;margin:10px 0">
				<strong>Buy → Sell flow</strong>
				<pre style="margin:8px 0 0;background:transparent;border:none;padding:0;font-size:11px">[Buy sources: Sharaf DG · Jumbo · spare247]  ──import──▶  [Your catalogue]  ──list──▶  [Sell: Noon · Amazon · eBay]</pre>
				<p style="margin:6px 0 0;font-size:11px;color:#64748b">Buy-source prices = what you PAY. Marketplace prices = where you LIST and what buyers see. Margin = marketplace sell − buy lowest.</p>
			</div>
			<p class="epc-ape-guide__contrast" style="margin-top:12px;padding:10px 12px;background:#fef3c7;border-color:#fcd34d">
				<strong>Important:</strong> We do <em>not</em> sell on non-marketplace retailer sites (Sharaf DG, Jumbo, spare247, autoparts.ae). Those are <strong>buy sources only</strong> — places to source stock. You sell only on marketplaces you tick on the Rules tab.
			</p>
			<p>Products never auto-publish. Every import requires your approval. Optional hourly crawl keeps prices fresh after import.</p>
		</div>
	</details>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-globe"></i> 2b. Your market is your country</summary>
		<div class="epc-ape-guide__section-body">
			<p>Auto Price AI resolves your tenant country from Tax Toolkit, ERP settings, or portal registry. Current country: <strong><?php echo epc_ape_h($tenantCountryLabel); ?></strong> (<code><?php echo epc_ape_h($tenantCountryCode); ?></code>).</p>
			<pre style="font-size:11px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:12px;margin:10px 0">Tenant country (<?php echo epc_ape_h($tenantCountryCode); ?>) → Buy sources (<?php echo epc_ape_h($tenantCountryCode); ?> retailers + industry pack) → Sell on global (eBay, Amazon.com) + local (Noon, Amazon.<?php echo epc_ape_h(strtolower($tenantCountryCode)); ?>…)</pre>
			<ul>
				<li><strong>Buy sources</strong> — only your country pack + industry overlay (Oman tenant → OM auto_parts/electronics, not UAE-only spare247 unless also in OM pack).</li>
				<li><strong>Sell marketplaces</strong> — global (eBay.com, Amazon.com) for all tenants; local channels (Noon, Dubizzle, Amazon.<?php echo epc_ape_h(strtolower($tenantCountryCode)); ?>…) auto-install from country on setup, country change, daily sync, and weekly platform sync.</li>
				<li><strong>Sources grow automatically</strong> — platform taxonomy updates → daily source expand cron → hourly crawl (when auto-crawl enabled) → Quick crawl adds new product suggestions. Custom domains you add on Market sources are preserved.</li>
				<li><strong>New tenant onboarding</strong> — first APAI open installs country pack + sell marketplaces; demo Oman tenant gets OM sources immediately.</li>
			</ul>
		</div>
	</details>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-briefcase"></i> 2. Your business model (tenant profile)</summary>
		<div class="epc-ape-guide__section-body">
			<p>Your tenant profile controls how Discover cards, margin, and Compare tables interpret prices. Current profile: <strong><?php echo epc_ape_h($profileLabel); ?></strong> (<code><?php echo epc_ape_h($profile); ?></code>).</p>
			<table class="table table-condensed table-bordered" style="font-size:12px;margin:10px 0">
				<thead><tr><th>Profile</th><th>Typical tenant</th><th>Buy from</th><th>Sell on</th></tr></thead>
				<tbody>
					<tr<?php echo $isWarehouse ? ' style="background:#ecfdf5"' : ''; ?>>
						<td><strong>warehouse_supplier</strong></td>
						<td>eParts Cart (epartscart)</td>
						<td>Wholesale / B2B (spare247, autoparts.ae) + market reference</td>
						<td>Your catalogue &amp; storefront; also Noon/Amazon gaps</td>
					</tr>
					<tr<?php echo $isArbitrage ? ' style="background:#ecfdf5"' : ''; ?>>
						<td><strong>marketplace_arbitrage</strong></td>
						<td>Electronicae (electronicae)</td>
						<td>Retailers (Sharaf DG, Jumbo, Virgin, Microless)</td>
						<td><strong>Only</strong> Noon, Amazon.ae, eBay — nowhere else</td>
					</tr>
					<tr<?php echo $isProfessional ? ' style="background:#ecfdf5"' : ''; ?>>
						<td><strong>professional_services</strong></td>
						<td>Taxofinca (tax advisory)</td>
						<td>Competitor consultancies, FTA / Big4 benchmarks</td>
						<td>Your service catalogue &amp; client quotes (not Noon/Amazon)</td>
					</tr>
				</tbody>
			</table>
			<?php if ($isWarehouse) { ?>
			<p><strong>Your workflow (warehouse_supplier):</strong> Buy wholesale, hold stock, sell retail via your catalogue. Compare tab matches your warehouse price list to live market min/max. Discover prioritises parts confirmed on 2+ sources.</p>
			<?php } elseif ($isArbitrage) { ?>
			<p><strong>Your workflow (marketplace_arbitrage):</strong> Buy from retailers when price is low, list on Noon/Amazon/eBay where the product is missing or undercut. Discover default view = <em>Marketplace opportunities</em>.</p>
			<?php } elseif ($isProfessional) { ?>
			<p><strong>Your workflow (professional_services):</strong> Benchmark service packages (accounting, audit, VAT, corporate tax) against competitor published rates and FTA fee guidance. Import winning packages to your catalogue; use Tax Toolkit for compliance workflows.</p>
			<?php } else { ?>
			<p>Ask your platform operator which profile fits your business. Most tenants use warehouse_supplier (own catalogue) or marketplace_arbitrage (cross-list only).</p>
			<?php } ?>
			<p><strong>Critical rule (all tenants):</strong> Sharaf DG, Jumbo, spare247, autoparts.ae, and similar discovery sites are <em>buy sources</em>. We never list products for sale on those domains. Sell targets always come from Noon, Amazon, or eBay listings configured under Rules.</p>
		</div>
	</details>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-compass"></i> 3. Discover tab — sub-tabs explained</summary>
		<div class="epc-ape-guide__section-body">
			<p>Discover is your main workspace. Use the pill buttons at the top to switch views. Count badges show how many products are in each view.</p>
			<table class="table table-condensed table-striped" style="font-size:12px">
				<thead><tr><th>View</th><th>What it means</th><th>When to use</th></tr></thead>
				<tbody>
					<tr>
						<td><strong>Market confirmed</strong></td>
						<td>Same product verified on <strong>2+ different sources</strong> (brand+article for parts, model match for electronics).</td>
						<td><?php echo $industryKey === 'auto_parts' ? 'Default for auto parts — your target catalogue filter.' : 'Available when multi-source match exists.'; ?></td>
					</tr>
					<tr>
						<td><strong>Marketplace opportunities</strong></td>
						<td>Product found on a <em>buy source</em> but <strong>not listed</strong> on your sell marketplaces (Noon/Amazon/eBay).</td>
						<td><?php echo $isArbitrage ? 'Default for arbitrage — gaps to fill by listing.' : 'Find parts to list on marketplaces.'; ?></td>
					</tr>
					<tr>
						<td><strong>All suggestions</strong> <?php if ($industryKey !== 'auto_parts') { ?><small>(label: New)</small><?php } ?></td>
						<td>Every crawled item — including single-source hits not yet market-confirmed.</td>
						<td>Broad review after a full crawl; expect more noise.</td>
					</tr>
					<tr>
						<td><strong>Price changes</strong></td>
						<td>Imported or queued products where a source price moved since last crawl (↑/↓ badges).</td>
						<td>Re-price catalogue or re-source when buy cost drops.</td>
					</tr>
					<tr>
						<td><strong>My products vs market</strong></td>
						<td>Your existing catalogue / warehouse prices compared to live market buy range and marketplace sell target.</td>
						<td><?php echo $isWarehouse ? 'Daily check — are you overpaying or undercutting?' : 'See if your listed price matches market.'; ?></td>
					</tr>
				</tbody>
			</table>
			<p>Toolbar actions on Discover: keyword <strong>Search</strong>, <strong>Crawl sources now</strong> (refresh prices), filter by product line, sort by newest / price change / last updated. Select multiple cards for bulk import.</p>
			<p><a href="<?php echo epc_ape_h($discoverUrl); ?>">Open Discover</a></p>
		</div>
	</details>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-tags"></i> 4. What each result card field means</summary>
		<div class="epc-ape-guide__section-body">
			<p>Every Discover card shows price intelligence. Here is what each label means in plain language:</p>
			<table class="table table-condensed table-bordered" style="font-size:12px">
				<thead><tr><th>Label on card</th><th>Meaning</th></tr></thead>
				<tbody>
					<tr><td><strong>Buy from (lowest)</strong> / <strong>Lowest source</strong></td><td>Cheapest place to <em>source</em> the product right now. This becomes your import cost. Shown with source domain in parentheses (e.g. spare247.com).</td></tr>
					<tr><td><strong>Buy source range</strong></td><td>Min–max price among buy-only sources (e.g. spare247 120 AED – autoparts.ae 145 AED). Informational — shows market spread. <em>Not</em> a sell target.</td></tr>
					<tr><td><strong>Sell on marketplace</strong> / <strong>Target sell price</strong></td><td>Price on Noon, Amazon.ae, or eBay — where you <em>list</em> for sale. Comes from marketplace crawl or category benchmark. This is your sell target.</td></tr>
					<tr><td><strong>Your price</strong> / <strong>Your warehouse</strong> / <strong>Warehouse cost</strong></td><td>Your current catalogue or warehouse cost for this SKU. Compared against buy lowest to show if you should re-source.</td></tr>
					<tr><td><strong>Margin</strong></td><td>Marketplace sell − buy lowest = real profit opportunity. Green = above min margin rule (<?php echo epc_ape_h($marginPct); ?>%). Red = below threshold.</td></tr>
					<tr><td><strong>Market confirmed · N sources</strong></td><td>Same SKU verified on N different sites — high confidence match.</td></tr>
					<tr><td><strong>Arbitrage opportunity</strong></td><td>Product on a buy source but missing from your sell marketplaces — a gap you can fill by listing.</td></tr>
					<tr><td><strong>Buy cheaper</strong> advice</td><td>Your price is higher than the lowest buy source — consider buying from [source] instead of your current supplier.</td></tr>
					<tr><td><strong>Overpriced</strong> advice</td><td>Your sell price is above marketplace listing — lower to compete or you may not sell.</td></tr>
					<tr><td><strong>Part number required</strong></td><td>Auto parts need <strong>Brand + Article</strong> (e.g. Toyota 1310154101). Free-text title alone is not enough for matching.</td></tr>
					<tr><td><strong>Single source — run crawl</strong></td><td>Only one buy source found so far. Run Quick or Full crawl on more sources before treating as market confirmed.</td></tr>
					<tr><td><strong>Buy sources:</strong> (footer line)</td><td>List of sites checked for this product — your &ldquo;sources compared&rdquo; audit trail.</td></tr>
					<tr><td><strong>Not listed — opportunity</strong></td><td>No marketplace listing found yet — potential arbitrage if you list first.</td></tr>
					<tr><td><strong>Price ↓ / Price ↑</strong></td><td>Source or marketplace price changed since last crawl — review for re-pricing.</td></tr>
				</tbody>
			</table>
			<p><strong>Remember:</strong> Buy source range max is never your sell price. Only marketplace rows set sell targets.</p>
		</div>
	</details>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-play-circle"></i> 5. Actions explained</summary>
		<div class="epc-ape-guide__section-body">
			<h5 style="margin:0 0 8px;font-size:13px">Crawl &amp; search</h5>
			<ul>
				<li><strong>Quick crawl</strong> (default button) — queued background job with progress bar; searches top product lines + refreshes prices. Returns in &lt;1s; progress polls every 2s. No HTTP request blocks longer than ~20s.</li>
				<li><strong>Full crawl</strong> (background) — all enabled sources + deeper product-line discovery. Same job architecture as Quick crawl.</li>
				<li><strong>Fast search</strong> (bolt icon) — optional keyword lookup on 3 sources. Product-line filter is optional.</li>
				<li><strong>Full search</strong> (Search button) — all merged sources (country pack + industry + your custom sites).</li>
				<li><strong>Fetch latest prices</strong> — re-fetch prices for items already in the queue without full re-discovery.</li>
				<li><strong>Sources grow automatically</strong> — platform pack updates + daily sync + your hourly crawl (when enabled) + custom sources on Market sources. Not infinite live scraping 24/7 without cron.</li>
			</ul>
			<h5 style="margin:12px 0 8px;font-size:13px">Import &amp; catalogue</h5>
			<ul>
				<li><strong>Add to my catalogue</strong> — imports product with <strong>cost = buy lowest</strong>, <strong>sell = marketplace target</strong> (or cost + margin if no marketplace data). Copies photos, specs, and source price links.</li>
				<li><strong>Add to catalogue → List on Noon</strong> — same import, tagged for marketplace cross-list (arbitrage cards).</li>
				<li><strong>Add selected to catalogue (N)</strong> — bulk approve checked cards.</li>
				<li><strong>Reject</strong> — remove suggestion from queue (does not delete catalogue items).</li>
				<li><strong>Update price to marketplace target</strong> — on My products vs market view when your price is stale.</li>
			</ul>
			<h5 style="margin:12px 0 8px;font-size:13px">Market sources &amp; login</h5>
			<ul>
				<li><strong>Test login</strong> — on Market sources tab for spare247.com (and other login-gated B2B portals). Verifies username/password before crawl uses them.</li>
				<li><strong>Requires login</strong> checkbox — stores credentials per tenant; used only for price crawl, never exposed on storefront.</li>
				<li>If spare247 login fails, that source is skipped for 1 hour automatically. Use Retry login or Skip for 24h on Market sources.</li>
			</ul>
		</div>
	</details>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-table"></i> 6. Compare tab</summary>
		<div class="epc-ape-guide__section-body">
			<p>Compare shows matrix views for imported products and warehouse alignment. Tables use colour cues: <span style="color:#047857">green</span> = lowest buy (cost), <span style="color:#1d4ed8">blue</span> = marketplace sell reference.</p>
			<?php if ($isWarehouse || $industryKey === 'auto_parts') { ?>
			<h5 style="font-size:13px">Warehouse picker + compare (auto parts / warehouse_supplier)</h5>
			<ol style="font-size:12px;padding-left:18px">
				<li><strong>Your warehouse</strong> — full list from <code>shop_docpart_prices_data</code> (c110, dt068, all linked price lists). Filter by price list, search brand/article, paginate 50/page. SQL only — fast even at 100k rows.</li>
				<li><strong>Compare selected</strong> — tick up to <strong>10</strong> rows, click <em>Compare selected with market</em>. Background job fetches market min/max for those keys only.</li>
				<li><strong>Previously compared</strong> — last 10 results cached in <code>epc_warehouse_market_match</code>.</li>
			</ol>
			<p style="font-size:12px">Badges: <strong>Good margin</strong>, <strong>Below market</strong>, <strong>Over market</strong>, <strong>No market data</strong>.</p>
			<?php } ?>
			<h5 style="font-size:13px;margin-top:10px">Marketplace gaps table</h5>
			<table class="table table-condensed table-bordered" style="font-size:12px">
				<thead><tr><th>Column</th><th>Meaning</th></tr></thead>
				<tbody>
					<tr><td><strong>Product</strong> / <strong>Brand · Article</strong></td><td>Product title or part identity (brand + article for auto parts).</td></tr>
					<tr><td><strong>Buy range (sources)</strong></td><td>Min–max among buy-only sources (Sharaf DG, spare247…). Green cell = lowest cost.</td></tr>
					<tr><td><strong>Marketplace price</strong></td><td>Live listing or category benchmark on Noon/Amazon/eBay. &ldquo;Research needed&rdquo; = not listed yet.</td></tr>
					<tr><td><strong>Your price</strong></td><td>Your catalogue or warehouse cost if already imported; dash if not in catalogue.</td></tr>
					<tr><td><strong>Advice</strong></td><td>Buy cheaper / Overpriced / List on Noon badges — same logic as Discover cards.</td></tr>
					<tr><td><strong>Margin</strong></td><td>Marketplace sell − buy lowest (absolute + %).</td></tr>
				</tbody>
			</table>
			<p>For warehouse tenants, open Compare to browse the full warehouse list — do not use bulk “match all” (that causes timeouts). Select ≤10 SKUs per market compare run.</p>
			<p><a href="<?php echo epc_ape_h($compareUrl); ?>">Open Compare</a></p>
		</div>
	</details>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-sliders"></i> 7. Rules tab — pricing strategy</summary>
		<div class="epc-ape-guide__section-body">
			<p>Rules control how Auto Price AI calculates sell price and flags opportunities.</p>
			<ul>
				<li><strong>Tenant profile</strong> — warehouse_supplier vs marketplace_arbitrage (see section 2).</li>
				<li><strong>Min margin %</strong> — minimum acceptable margin (marketplace sell − buy lowest). Current: <strong><?php echo epc_ape_h($marginPct); ?>%</strong>. Cards below this show red margin.</li>
				<li><strong>Sell marketplaces</strong> — checkboxes for Noon, Amazon.ae, eBay, etc. These are the <em>only</em> domains where sell targets are taken from. Unchecked = ignored for gap detection.</li>
				<li><strong>Enable marketplace gap detection</strong> — turns on Marketplace opportunities view and Compare gaps table.</li>
				<li><strong>Currency</strong> — display currency for all price blocks (usually AED for UAE tenants).</li>
				<li><strong>Auto-update catalogue prices</strong> — when enabled, future crawls may push price changes to imported products (use with care).</li>
				<li><strong>Cross-list channels</strong> — future: auto-publish to marketplaces after import.</li>
			</ul>
			<p class="epc-ape-guide__contrast" style="padding:10px 12px">
				Help text on Rules tab: &ldquo;Buy sources (Sharaf DG, Jumbo, spare247…) are shown for sourcing only — we do not sell on those sites.&rdquo;
			</p>
			<p><a href="<?php echo epc_ape_h($rulesUrl); ?>">Open Rules</a></p>
		</div>
	</details>

	<?php if ($industryKey === 'auto_parts') { ?>
	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-car"></i> 8. Auto parts — industry specifics</summary>
		<div class="epc-ape-guide__section-body">
			<p>Profile: <strong>warehouse_supplier</strong> (eParts Cart). Target products = same brand + article on <strong>2+ sources</strong>.</p>
			<ul>
				<li><strong>Brand + Article Number</strong> — always search <code>Toyota 1310154101</code> format. Cards without both show <em>Part number required</em>.</li>
				<li><strong>spare247.com</strong> — B2B reference pricing. Add credentials on <a href="<?php echo epc_ape_h($sourcesUrl); ?>">Market sources</a> → Requires login → Test login.</li>
				<li><strong>Warehouse price list</strong> — Compare tab lists every warehouse row (c110, DT068, etc.). Select up to 10, then compare with market. Green warehouse cost below market min = good margin.</li>
				<li><strong>Discover default</strong> — Market confirmed (2+ sources). Also check Marketplace opportunities for parts to list on Noon/Amazon.</li>
				<li><strong>Seeded sources</strong> — spare247, autoparts.ae, autodoc.ae, partsouq, noon, amazon.ae (buy vs sell classified automatically).</li>
			</ul>
		</div>
	</details>
	<?php } elseif ($industryKey === 'electronics') { ?>
	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-mobile"></i> 8. Electronics — industry specifics</summary>
		<div class="epc-ape-guide__section-body">
			<p>Profile: <strong>marketplace_arbitrage</strong> (Electronicae). Buy from retailers, sell <em>only</em> on Noon, Amazon.ae, eBay.</p>
			<ul>
				<li><strong>Model matching</strong> — cards show spec chips (RAM, Storage, Model). Compare tab warns on spec mismatch (128GB vs 256GB).</li>
				<li><strong>Buy sources</strong> — Sharaf DG, Jumbo, Virgin, Microless, emax (sourcing only — never sell targets).</li>
				<li><strong>Arbitrage flow</strong> — crawl buy sources → scan sell marketplaces → Marketplace opportunities → import → list on Noon/Amazon.</li>
				<li><strong>Storefront</strong> — optional Market prices block on product pages when imported via Auto Price AI (toggle on Market sources).</li>
			</ul>
		</div>
	</details>
	<?php } elseif ($industryKey === 'tax_advisory') { ?>
	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-briefcase"></i> 8. Tax advisory &amp; professional services — Taxofinca</summary>
		<div class="epc-ape-guide__section-body">
			<p>Profile: <strong>professional_services</strong> (Taxofinca). This is <em>not</em> retail arbitrage — Auto Price AI helps benchmark service packages against competitor consultancies and FTA guidance.</p>
			<ul>
				<li><strong>Service product lines</strong> — Accounting packages, audit, VAT filing, corporate tax registration, business advisory (see Product lines tab).</li>
				<li><strong>Discovery sources</strong> — Big4 benchmarks (PwC, EY, KPMG, Deloitte), mid-tier firms (Grant Thornton, BDO, Mazars), plus <strong>tax.gov.ae</strong> FTA references — not ACE Hardware or shopping sites.</li>
				<li><strong>Tax Toolkit</strong> — enable via Integrations hub; use ERP → Tax for VAT/CT compliance alongside service pricing.</li>
				<li><strong>No marketplace arbitrage</strong> — Marketplace opportunities and Noon/Amazon sell targets are disabled. Compare tab shows service benchmark ranges only.</li>
				<li><strong>Import workflow</strong> — add service packages to catalogue (fixed-fee SKUs) with benchmark notes; clients book via storefront or CP quotes.</li>
				<li><strong>Margin rule</strong> — default <?php echo epc_ape_h($marginPct); ?>% on service packages vs competitor benchmark floor.</li>
			</ul>
			<p><a href="/<?php echo epc_ape_h($backend); ?>/control/portal/epc_integrations_hub">Integrations hub</a> · <a href="/<?php echo epc_ape_h($backend); ?>/shop/finance/erp?area=tax">Tax Toolkit (ERP)</a></p>
		</div>
	</details>
	<?php } else { ?>
	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-industry"></i> 8. <?php echo epc_ape_h($industryLabel); ?> — industry notes</summary>
		<div class="epc-ape-guide__section-body">
			<p>Country pack + industry taxonomy drive discovery. Use custom sources for niche vendors. Review spec chips and category mapping before bulk import. Check duplicates on My imports before publishing.</p>
		</div>
	</details>
	<?php } ?>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-list-ol"></i> 9. Extended workflow notes</summary>
		<div class="epc-ape-guide__section-body">
			<p>Follow the <strong>Quick start — 7 steps</strong> section above for daily work. These notes cover one-time setup and automation.</p>
			<ul>
				<li><strong>Enable module</strong> — Super CP operator enables Auto Price AI under <a href="<?php echo epc_ape_h($tenantFeaturesUrl); ?>">Tenant features</a>. Taxonomy and country sources seed on first engine load.</li>
				<li><strong>First Quick crawl</strong> — open Discover and click Quick crawl once (no category search needed). Auto-seed runs on first visit if the queue is empty. Daily Quick crawl + hourly cron keep prices fresh.</li>
				<li><strong>Bulk import</strong> — tick card checkboxes → <strong>Add selected to catalogue (N)</strong> on Discover toolbar.</li>
				<li><strong>Publish</strong> — imported products stay draft until you publish in Catalogue admin. Cross-list to Noon/Amazon from your catalogue workflow.</li>
				<li><strong>Hourly cron</strong> — live tenants auto-run Quick crawl each hour. See panel at bottom of this guide for manual trigger URL.</li>
			</ul>
		</div>
	</details>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-question-circle"></i> 10. FAQ</summary>
		<div class="epc-ape-guide__section-body">
			<p><strong>Why is margin 0% or missing?</strong></p>
			<ul>
				<li>Only one buy source found (<em>Single source</em>) — need marketplace listing data for margin.</li>
				<li>Product on buy sources only — no sell marketplace price yet (<em>Not listed — opportunity</em>).</li>
				<li>Buy source range alone does not create margin — margin needs marketplace sell − buy lowest.</li>
			</ul>
			<p><strong>Why is Discover empty?</strong></p>
			<ul>
				<li>No crawl run yet — click <strong>Crawl sources now</strong> or <strong>Search</strong>.</li>
				<li>Wrong sub-tab — try All suggestions instead of Market confirmed.</li>
				<li>spare247 login failed — fix credentials on Market sources and retry.</li>
				<li>Product line filter active — clear filter or pick another taxonomy node.</li>
			</ul>
			<p><strong>What&rsquo;s the difference between target price and buy range?</strong></p>
			<ul>
				<li><strong>Buy source range</strong> = what you PAY suppliers/retailers (Sharaf DG, spare247…). Min = your import cost.</li>
				<li><strong>Target price / Sell on marketplace</strong> = what you CHARGE on Noon/Amazon/eBay. Max of buy range is <em>never</em> your sell price.</li>
				<li><strong>Margin</strong> = target (marketplace) − buy lowest.</li>
			</ul>
			<p><strong>Can I sell on Sharaf DG or Jumbo?</strong></p>
			<p>No. Those are buy sources only. Auto Price AI uses them to find sourcing cost. You list for sale on marketplaces configured on Rules.</p>
			<p><strong>Do prices update automatically?</strong></p>
			<p>Hourly quick crawl refreshes enabled sources. Enable auto-update on Rules to push changes to catalogue (optional).</p>
		</div>
	</details>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-globe"></i> Market sources tab</summary>
		<div class="epc-ape-guide__section-body">
			<p>Manage discovery domains for your country pack plus custom websites.</p>
			<ul>
				<li><strong>Country pack</strong> — seeded automatically (e.g. noon, amazon.ae for UAE). Badge: Country pack.</li>
				<li><strong>Custom website</strong> — your vendor or competitor domain. Badge: Custom.</li>
				<li><strong>Requires login</strong> — B2B portals (spare247). Credentials per-tenant; Test login before crawl.</li>
				<li><strong>Enable/disable</strong> — toggle sources without deleting. Disabled sources skip crawl.</li>
			</ul>
			<p>Every discovery run merges: country pack + industry overlay + your custom sources. Your own storefront domain is never a discovery source.</p>
			<p><a href="<?php echo epc_ape_h($sourcesUrl); ?>">Open Market sources</a></p>
		</div>
	</details>

	<details class="epc-ape-guide__section">
		<summary><i class="fa fa-download"></i> My imports tab</summary>
		<div class="epc-ape-guide__section-body">
			<p>Products already linked to your catalogue from Auto Price AI.</p>
			<ul>
				<li><strong>New imports</strong> — recently approved.</li>
				<li><strong>Price changes</strong> — market source moved since import.</li>
				<li><strong>Duplicates</strong> — same SKU clusters — resolve before bulk publishing.</li>
			</ul>
			<p>Local photo thumbnails; <strong>Refresh photos</strong> re-downloads from source URLs.</p>
			<p><a href="<?php echo epc_ape_h($importsUrl); ?>">Open My imports</a></p>
		</div>
	</details>

	<div class="hpanel">
		<div class="panel-heading"><h4>Hourly auto-crawl</h4></div>
		<div class="panel-body" style="font-size:12px">
			<p>Live tenants run <strong>quick crawl</strong> hourly (staggered slots). Pending full-crawl jobs run first when queued.</p>
			<p class="text-muted" style="margin:0">Manual trigger: <a href="<?php echo epc_ape_h($runCrawlUrl); ?>" target="_blank" rel="noopener"><?php echo epc_ape_h($runCrawlUrl); ?></a></p>
		</div>
	</div>

	<?php if (!$isSuperCp) { ?>
	<div class="hpanel">
		<div class="panel-heading"><h4>Your tenant quick links</h4></div>
		<div class="panel-body" style="font-size:12px">
			<p><a href="<?php echo epc_ape_h($engineUrl); ?>">Auto Price AI engine</a> ·
				<a href="<?php echo epc_ape_h($discoverUrl); ?>">Discover</a> ·
				<a href="<?php echo epc_ape_h($compareUrl); ?>">Compare</a> ·
				<a href="<?php echo epc_ape_h($sourcesUrl); ?>">Market sources</a> ·
				<a href="<?php echo epc_ape_h($rulesUrl); ?>">Rules</a> ·
				<a href="<?php echo epc_ape_h($urls['catalogue_cp']); ?>">Catalogue admin</a>
				<?php if (!empty($demo['storefront_url'])) { ?> · <a href="<?php echo epc_ape_h($demo['storefront_url']); ?>" target="_blank" rel="noopener">Demo storefront product</a><?php } ?>
			</p>
		</div>
	</div>
	<?php } ?>