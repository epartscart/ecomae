namespace EcomAE.Platform.Presentation;

internal static class PhpPlanQ1HivePages
{
    internal const string AboutTpl = @"<div class=""epm-wrap"">
	<div class=""epm-hero"" style=""min-height:220px"">
		<div class=""epm-hero__shade"" style=""opacity:1;background:linear-gradient(135deg,#1a0407,#5a0f16 50%,#0a0a0a)""></div>
		<div class=""epm-hero__content"">
			<h1>About ECOM AE</h1>
			<p class=""lead"">Built by Electronic World Group in Dubai — one cloud for commerce, ERP, CRM, and UAE e-invoicing.</p>
		</div>
	</div>
	<div class=""epm-split"">
		<div>
			<h2 class=""epm-section-title"" style=""margin-top:0"">Who we are</h2>
			<p class=""epm-section-lead"">ECOM AE is the hosted operator platform behind live tenants including eParts Cart, Taxofinca, Electronicae, Stylenlook, and The Jewellery Trend. We provision isolated MySQL databases, industry themes, and Super CP onboarding so agencies go live in as little as 24 hours after DNS.</p>
			<ul class=""epm-feature-list"">
				<li>Multi-tenant Model C — one nginx docroot, hostname routing</li>
				<li>Super CP (operator control panel) at ecomae.com/cp</li>
				<li>Client CP and storefront on each client domain</li>
				<li>Peppol / PINT-AE e-invoice in ERP Finance</li>
			</ul>
		</div>
		<div class=""epm-card epm-card--accent"">
			<h4>Electronic World Group</h4>
			<p>Dubai, UAE · B2B SaaS and commerce infrastructure for the GCC.</p>
			<div class=""epm-cta"" style=""margin-top:12px"">
				<a class=""epm-btn epm-btn--primary epm-btn--sm"" href=""{BASE}platform/contact"">Contact us</a>
				<a class=""epm-btn epm-btn--ghost epm-btn--sm"" href=""{BASE}platform/customer-results"">Customer results</a>
			</div>
		</div>
	</div>
</div>
	";

    internal const string ApiDocumentationTpl = @"<div class=""epm-wrap"">
	<div class=""epm-hero"">
		<div class=""epm-hero__shade"" style=""opacity:1;background:linear-gradient(135deg,#1e1b4b 0%,#7c3aed 45%,#0a0a0a 100%)""></div>
		<div class=""epm-hero__content"">
			<div class=""epm-badge""><i class=""fa fa-code""></i> Phase 1 — read-only</div>
			<h1>API documentation</h1>
			<p class=""lead"">Integrate your ERP, BI, or middleware with ECOM AE tenant data via JSON REST. Phase 1 covers health checks, catalogue search, orders, and ERP KPIs — authenticated per tenant with API keys issued from Super CP.</p>
			<div class=""epm-cta"">
				<a class=""epm-btn epm-btn--primary"" href=""{BASE}epc-api/v1/openapi.json"" target=""_blank"" rel=""noopener""><i class=""fa fa-file-code-o""></i> OpenAPI JSON</a>
				<a class=""epm-btn epm-btn--ghost"" href=""{BASE}epc-api/v1/health"" target=""_blank"" rel=""noopener""><i class=""fa fa-heartbeat""></i> API health</a>
				<a class=""epm-btn epm-btn--outline"" href=""{BASE}platform/demo""><i class=""fa fa-play-circle""></i> {DAYS}-day demo</a>
			</div>
		</div>
	</div>

	<h2 class=""epm-section-title"">Why integrate?</h2>
	<p class=""epm-section-lead"">Connect warehouse systems, accounting exports, partner portals, or custom dashboards without scraping the storefront. Each tenant receives isolated API keys scoped to their MySQL database — orders and ERP figures never cross tenants.</p>

	<h2 class=""epm-section-title"">Phase 1 endpoints</h2>
	<div class=""epm-card"" style=""overflow-x:auto;padding:0"">
		<table style=""width:100%;border-collapse:collapse;font-size:14px"">
			<thead>
				<tr style=""background:#171717;color:#e2e8f0"">
					<th style=""padding:12px;text-align:left"">Method</th>
					<th style=""padding:12px;text-align:left"">Path</th>
					<th style=""padding:12px;text-align:left"">Auth</th>
					<th style=""padding:12px;text-align:left"">Description</th>
				</tr>
			</thead>
			<tbody>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>GET</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>/epc-api/v1/health</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">None</td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Platform API status and endpoint list</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>GET</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>/epc-api/v1/capabilities</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">None</td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Capability area summary from marketing catalog</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>GET</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>/epc-api/v1/openapi.json</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">None</td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Machine-readable OpenAPI 3 spec</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>GET</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>/epc-api/v1/tenant/info</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">X-API-Key</td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Tenant name, industry, access mode</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>GET</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>/epc-api/v1/orders</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">X-API-Key</td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Recent shop orders (limit 20)</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>GET</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>/epc-api/v1/products/search?q=</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">X-API-Key</td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Search published catalogue products</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>GET</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>/epc-api/v1/erp/dashboard-summary</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">X-API-Key</td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">ERP KPI summary (read-only)</td>
				</tr>
							</tbody>
		</table>
	</div>

	<h2 class=""epm-section-title"">Authentication</h2>
	<div class=""epm-split"">
		<div class=""epm-card epm-card--accent"">
			<h4><i class=""fa fa-key text-primary""></i> API key header</h4>
			<ol class=""epm-steps"">
				<li>Request enterprise API access via <a href=""{BASE}platform/contact"" style=""color:var(--epm-cyan)"">contact</a> or your {DAYS}-day demo onboarding.</li>
				<li>Platform operator issues a tenant-scoped key from Super CP (stored as SHA-256 hash only).</li>
				<li>Send the key on every authenticated request: <code>X-API-Key: your_key_here</code></li>
				<li>Keys include read scopes: tenant info, orders, products, ERP dashboard.</li>
			</ol>
		</div>
		<div class=""epm-card"">
			<h4><i class=""fa fa-terminal text-primary""></i> Example curl (safe placeholders)</h4>
			<pre style=""background:#171717;color:#e2e8f0;padding:14px;border-radius:8px;font-size:12px;overflow-x:auto;margin:0""># Public health — no key
curl -s {BASE}epc-api/v1/health

# Tenant info — replace placeholder
curl -s -H ""X-API-Key: epc_YOUR_TENANT_read_XXXXXXXXXXXXXXXX"" \
  {BASE}epc-api/v1/tenant/info

# Product search
curl -s -H ""X-API-Key: epc_YOUR_TENANT_read_XXXXXXXXXXXXXXXX"" \
  ""{BASE}epc-api/v1/products/search?q=brake""</pre>
			<p style=""font-size:13px;color:var(--epm-muted);margin-top:10px"">Never commit real keys to source control or public pages.</p>
		</div>
	</div>

	<h2 class=""epm-section-title"">Existing integrations scope</h2>
	<div class=""epm-grid"">
		<div class=""epm-card"">
			<h4><i class=""fa fa-globe text-primary""></i> Public REST API (Phase 1)</h4>
			<p>JSON at <code>/epc-api/v1/</code> — designed for external systems, BI, and partner integrations with API keys.</p>
		</div>
		<div class=""epm-card"">
			<h4><i class=""fa fa-lock text-primary""></i> ERP ajax (internal)</h4>
			<p>CP session endpoints under <code>/cp/shop/finance/erp/ajax_*</code> require staff login — not exposed as public API.</p>
		</div>
	</div>

	<h2 class=""epm-section-title"">Future scope</h2>
	<ul class=""epm-feature-list"">
		<li>Webhooks — order placed, stock low, lead captured</li>
		<li>E-invoice submit — Peppol / PINT-AE outbound</li>
		<li>Microsoft Dynamics 365 sync</li>
		<li>Marketplace channel write-back</li>
		<li>Write APIs v2 — create orders, update stock, post invoices</li>
	</ul>

	<div class=""epm-highlight"">
		<h3>Need API keys for production?</h3>
		<p style=""color:var(--epm-muted);margin-bottom:14px"">Enterprise tenants receive scoped keys after onboarding. Start with a {DAYS}-day demo or contact sales for API access on your live tenant.</p>
		<div class=""epm-cta"">
			<a class=""epm-btn epm-btn--primary"" href=""{BASE}platform/contact"">Contact for API keys</a>
			<a class=""epm-btn epm-btn--ghost"" href=""{BASE}platform/demo"">Book demo</a>
			<a class=""epm-btn epm-btn--outline"" href=""{BASE}epc-api/v1/openapi.json"" target=""_blank"" rel=""noopener"">Download OpenAPI</a>
		</div>
	</div>
</div>
	";

    internal const string ApiServicesTpl = @"<div class=""epm-wrap"">
	<div class=""epm-hero"">
		<div class=""epm-hero__shade"" style=""opacity:1;background:linear-gradient(135deg,#5a0f16 0%,#8a131c 40%,#0a0a0a 100%)""></div>
		<div class=""epm-hero__content"">
			<div class=""epm-badge""><i class=""fa fa-plug""></i> For your own website — not a tenant</div>
			<h1>Autoparts Catalog &amp; Price PRO API</h1>
			<p class=""lead"">Offer vehicle spare-parts lookup on <strong>your</strong> site without renting a full ECOM AE storefront or ERP. Choose <strong>Catalog API</strong> (vehicle &amp; parts catalog data), <strong>Price PRO API</strong> (live supplier price &amp; availability), or both — we issue client keys and document integration for your developers.</p>
			<div class=""epm-cta"">
				<a class=""epm-btn epm-btn--primary"" href=""{BASE}platform/contact""><i class=""fa fa-envelope""></i> Request API access</a>
				<a class=""epm-btn epm-btn--ghost"" href=""#catalog-api""><i class=""fa fa-car""></i> Catalog API</a>
				<a class=""epm-btn epm-btn--outline"" href=""#price-pro-api""><i class=""fa fa-tags""></i> Price PRO</a>
			</div>
		</div>
	</div>

	<h2 class=""epm-section-title"">Who this is for</h2>
	<p class=""epm-section-lead"">These products are for <strong>integration partners</strong> — independent websites, marketplaces, garage portals, and apps that need catalog or price data only. They are <em>not</em> the same as a monthly tenant (storefront + CP + ERP) or ERP-only client access.</p>
	<div class=""epm-grid"">
		<div class=""epm-card epm-card--accent"">
			<h4><i class=""fa fa-book text-primary""></i> Catalog customer</h4>
			<p>JSON REST for manufacturers → models → categories → articles, VIN decode, engine search, analogs. Powered by the Epart vehicle catalog service. Embed in your UX with your branding.</p>
		</div>
		<div class=""epm-card epm-card--accent"">
			<h4><i class=""fa fa-line-chart text-primary""></i> Price PRO customer</h4>
			<p>Article + brand → supplier offers, stock hints, and list prices from connected price feeds. For B2B portals and comparison widgets on third-party sites.</p>
		</div>
		<div class=""epm-card"">
			<h4><i class=""fa fa-building text-primary""></i> Not a tenant</h4>
			<p>No CP login, no shop checkout, no full ERP. Need orders, inventory, and UAE e-invoice? See <a href=""{BASE}platform/pricing"">tenant plans</a> or <a href=""{BASE}platform/api-documentation"">Tenant ERP API</a>.</p>
		</div>
	</div>

	<h2 class=""epm-section-title"" id=""catalog-api"">Autoparts Catalog API</h2>
	<p class=""epm-section-lead"">Vehicle-aware spare parts catalog (Epart catalog API). Your server or front-end calls our gateway; we handle upstream catalog keys, caching, and daily quotas.</p>
	<div class=""epm-card"" style=""overflow-x:auto;padding:0;margin-bottom:20px"">
		<table style=""width:100%;border-collapse:collapse;font-size:14px"">
			<thead>
				<tr style=""background:#171717;color:#e2e8f0"">
					<th style=""padding:12px;text-align:left"">Query param <code>action</code></th>
					<th style=""padding:12px;text-align:left"">Purpose</th>
				</tr>
			</thead>
			<tbody>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>manufacturers</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">List vehicle manufacturers (passenger, commercial, motorbike)</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>models</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Models for a manufacturer</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>modifications</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Engine / trim variants for a model</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>categories</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Part categories for a vehicle modification</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>products</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Products in a category</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>articles</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Articles in a category</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>article</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Single article detail by brand + number</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>analogs</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Cross-reference / analog parts</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>brands</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Aftermarket brands list</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>vin</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">VIN decode → vehicle + applicable catalog tree</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>engines</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Engine code lookup</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>engine_search</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Search engines by code fragment</td>
				</tr>
								<tr>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0""><code>status</code></td>
					<td style=""padding:10px 12px;border-bottom:1px solid #e2e8f0"">Connection status and daily quota usage</td>
				</tr>
							</tbody>
		</table>
	</div>
	<div class=""epm-split"">
		<div class=""epm-card"">
			<h4><i class=""fa fa-link text-primary""></i> Base URL (keys issued on onboarding)</h4>
			<p>After onboarding you receive an <code>X-API-Key</code> (prefix <code>epc_catalog_</code>…) and daily quota. Public entry point:</p>
			<pre style=""background:#171717;color:#e2e8f0;padding:14px;border-radius:8px;font-size:12px;overflow-x:auto;margin:12px 0 0"">GET {BASE}api/v1/catalog.php?action=manufacturers&amp;section=passenger
Header: X-API-Key: epc_catalog_YOUR_CLIENT_KEY</pre>
			<p style=""font-size:13px;color:var(--epm-muted);margin-top:10px"">Hosted ECOM AE storefronts use the built-in catalog widget on your domain. Third-party sites and apps should call <code>/api/v1/catalog.php</code> only.</p>
		</div>
		<div class=""epm-card"">
			<h4><i class=""fa fa-terminal text-primary""></i> Example integration flow</h4>
			<ol class=""epm-steps"">
				<li>User picks manufacturer → your site calls <code>action=models&amp;manufacturer_id=…</code></li>
				<li>User picks model → <code>action=modifications</code></li>
				<li>User browses category tree → <code>categories</code>, then <code>articles</code> or <code>products</code></li>
				<li>Optional VIN tab → <code>action=vin&amp;vin=…</code> or engine code → <code>engine_search</code></li>
			</ol>
		</div>
	</div>
	<pre style=""background:#171717;color:#e2e8f0;padding:14px;border-radius:8px;font-size:12px;overflow-x:auto;margin:16px 0""># Manufacturers (passenger cars) — replace YOUR_CLIENT_KEY after onboarding
curl -s -H ""X-API-Key: epc_catalog_YOUR_CLIENT_KEY"" \
  ""{BASE}api/v1/catalog.php?action=manufacturers&amp;section=passenger""

# VIN decode
curl -s -H ""X-API-Key: epc_catalog_YOUR_CLIENT_KEY"" \
  ""{BASE}api/v1/catalog.php?action=vin&amp;vin=WBAXG1103CDW29096""

# Quota / status (includes api_client.remaining when authenticated)
curl -s -H ""X-API-Key: epc_catalog_YOUR_CLIENT_KEY"" \
  ""{BASE}api/v1/catalog.php?action=status""</pre>

	<h2 class=""epm-section-title"" id=""price-pro-api"">Spare Parts Price PRO API</h2>
	<p class=""epm-section-lead"">Look up supplier list prices and availability for an article on a brand — designed for B2B widgets, garage systems, and marketplaces. Requires a <strong>Price PRO</strong> client key (separate from Catalog-only).</p>
	<div class=""epm-split"">
		<div class=""epm-card epm-card--accent"">
			<h4><i class=""fa fa-cogs text-primary""></i> Price PRO endpoint (beta)</h4>
			<pre style=""background:#171717;color:#e2e8f0;padding:14px;border-radius:8px;font-size:12px;overflow-x:auto;margin:0"">GET {BASE}api/v1/price/lookup.php?brand=BOSCH&amp;article=0986424590
Header: X-API-Key: epc_pricepro_YOUR_CLIENT_KEY

Response: { ""ok"": true, ""beta"": true, ""offers"": [ { ""supplier"", ""price"", ""currency"", ""stock_hint"" } ] }</pre>
			<p style=""font-size:13px;color:var(--epm-muted);margin-top:10px"">Price PRO keys are issued on onboarding. Empty <code>offers</code> with a beta message means supplier feeds are not enabled yet — contact sales for enablement.</p>
		</div>
		<div class=""epm-card"">
			<h4><i class=""fa fa-info-circle text-primary""></i> vs Catalog API</h4>
			<ul class=""epm-feature-list"">
				<li><strong>Catalog</strong> — vehicle tree, OEM categories, article metadata, VIN, analogs</li>
				<li><strong>Price PRO</strong> — commercial offers from connected supplier price files</li>
				<li>Many clients start with Catalog only; add Price PRO when ready to show live buy prices</li>
			</ul>
		</div>
	</div>
	<pre style=""background:#171717;color:#e2e8f0;padding:14px;border-radius:8px;font-size:12px;overflow-x:auto;margin:16px 0""># Price PRO — supplier offers for brand + article
curl -s -H ""X-API-Key: epc_pricepro_YOUR_CLIENT_KEY"" \
  ""{BASE}api/v1/price/lookup.php?brand=BOSCH&amp;article=0986424590""</pre>

	<h2 class=""epm-section-title"">How integration works</h2>
	<div class=""epm-grid"">
		<div class=""epm-card"">
			<h4>1. Choose product</h4>
			<p>Catalog customer, Price PRO customer, or bundled. No tenant storefront required.</p>
		</div>
		<div class=""epm-card"">
			<h4>2. Receive credentials</h4>
			<p>We issue <code>X-API-Key</code>, daily quota, and allowed actions. Keys are hashed server-side; rotate on request.</p>
		</div>
		<div class=""epm-card"">
			<h4>3. Build on your site</h4>
			<p>Call JSON REST from your backend (recommended) or SPA with a thin proxy — never expose upstream catalog API keys in browser code.</p>
		</div>
		<div class=""epm-card"">
			<h4>4. Go live</h4>
			<p>Usage metering and support via ECOM AE. Upgrade to full tenant later if you need shop, CP, and ERP.</p>
		</div>
	</div>

	<h2 class=""epm-section-title"">Related products</h2>
	<div class=""epm-split"">
		<div class=""epm-card"">
			<h4><i class=""fa fa-code text-primary""></i> Tenant ERP API</h4>
			<p>For existing ECOM AE tenants: orders, shop products, ERP KPIs at <code>/epc-api/v1/</code>.</p>
			<p><a href=""{BASE}platform/api-documentation"">Tenant API documentation →</a></p>
		</div>
		<div class=""epm-card"">
			<h4><i class=""fa fa-shopping-cart text-primary""></i> Full storefront tenant</h4>
			<p>Hosted shop + CP + ERP on your domain — see <a href=""{BASE}platform/pricing"">monthly plans</a>.</p>
		</div>
	</div>

	<div class=""epm-highlight"">
		<h3>Start as a Catalog or Price PRO client</h3>
		<p style=""color:var(--epm-muted);margin-bottom:14px"">Tell us your use case (website URL, expected daily calls, catalog vs price). We enable sandbox access and integration support.</p>
		<div class=""epm-cta"">
			<a class=""epm-btn epm-btn--primary"" href=""{BASE}platform/contact"">Contact for API keys</a>
			<a class=""epm-btn epm-btn--ghost"" href=""{BASE}platform/demo"">Book platform demo</a>
		</div>
	</div>
</div>
		";

    internal const string EcosystemModelSectionTpl = @"<section class=""epm-eco-model"" id=""ecosystem-model"" aria-labelledby=""epm-eco-model-title"">
	<div class=""epm-eco-model__head"">
		<div class=""epm-badge""><i class=""fa fa-sitemap""></i> Ecosystem structure</div>
		<h2 class=""epm-section-title"" id=""epm-eco-model-title"" style=""margin-top:8px"">5 connected ecosystems for structured expansion</h2>
		<p class=""epm-section-lead"" style=""max-width:860px"">Designed as a disciplined operating model: each ecosystem has active industries today and expansion-ready industries on roadmap.</p>
	</div>
	<div class=""epm-eco-model__grid"">
				<article class=""epm-card epm-card--accent epm-eco-model__card"">
			<h3>Commerce Ecosystem</h3>
			<p class=""epm-eco-model__label""><strong>Active industries</strong></p>
			<ul class=""epm-eco-model__list"">
								<li>Auto parts commerce</li>
								<li>Fashion e-commerce</li>
								<li>Electronics retail</li>
								<li>Medical supplies ordering</li>
							</ul>
			<p class=""epm-eco-model__label""><strong>Expansion-ready roadmap</strong></p>
			<ul class=""epm-eco-model__list epm-eco-model__list--roadmap"">
								<li>Hyperlocal grocery &amp; FMCG</li>
								<li>B2B wholesale marketplaces</li>
								<li>Cross-border niche commerce</li>
							</ul>
		</article>
				<article class=""epm-card epm-card--accent epm-eco-model__card"">
			<h3>Business Services Ecosystem</h3>
			<p class=""epm-eco-model__label""><strong>Active industries</strong></p>
			<ul class=""epm-eco-model__list"">
								<li>Tax advisory portals</li>
								<li>Consultancy CRM + billing</li>
								<li>ERP finance workflows</li>
							</ul>
			<p class=""epm-eco-model__label""><strong>Expansion-ready roadmap</strong></p>
			<ul class=""epm-eco-model__list epm-eco-model__list--roadmap"">
								<li>Legal/pro services operations</li>
								<li>Corporate services onboarding</li>
								<li>Managed back-office services</li>
							</ul>
		</article>
				<article class=""epm-card epm-card--accent epm-eco-model__card"">
			<h3>Lifestyle &amp; Consumer Ecosystem</h3>
			<p class=""epm-eco-model__label""><strong>Active industries</strong></p>
			<ul class=""epm-eco-model__list"">
								<li>Health &amp; wellness commerce</li>
								<li>Jewellery premium storefronts</li>
								<li>Consumer engagement campaigns</li>
							</ul>
			<p class=""epm-eco-model__label""><strong>Expansion-ready roadmap</strong></p>
			<ul class=""epm-eco-model__list epm-eco-model__list--roadmap"">
								<li>Beauty &amp; personal care subscriptions</li>
								<li>Home &amp; living curation</li>
								<li>Experience-led consumer services</li>
							</ul>
		</article>
				<article class=""epm-card epm-card--accent epm-eco-model__card"">
			<h3>Asset &amp; Sharing Economy</h3>
			<p class=""epm-eco-model__label""><strong>Active industries</strong></p>
			<ul class=""epm-eco-model__list"">
								<li>Rental operations workflows</li>
								<li>Multi-asset inventory controls</li>
								<li>Deposit and contract tracking</li>
							</ul>
			<p class=""epm-eco-model__label""><strong>Expansion-ready roadmap</strong></p>
			<ul class=""epm-eco-model__list epm-eco-model__list--roadmap"">
								<li>Fleet sharing platforms</li>
								<li>Equipment sharing networks</li>
								<li>Property/service asset scheduling</li>
							</ul>
		</article>
				<article class=""epm-card epm-card--accent epm-eco-model__card"">
			<h3>Digital &amp; Technology Ecosystem</h3>
			<p class=""epm-eco-model__label""><strong>Active industries</strong></p>
			<ul class=""epm-eco-model__list"">
								<li>Super CP tenant orchestration</li>
								<li>Client CP operations layer</li>
								<li>Integrated ERP + CRM stack</li>
							</ul>
			<p class=""epm-eco-model__label""><strong>Expansion-ready roadmap</strong></p>
			<ul class=""epm-eco-model__list epm-eco-model__list--roadmap"">
								<li>API-led partner integrations</li>
								<li>Intelligence automation services</li>
								<li>Data products for operator decisions</li>
							</ul>
		</article>
			</div>
	<div class=""epm-highlight epm-eco-model__meta"">
		<p><strong>Governance rule:</strong> Industries are added only when they increase transaction volume, platform dependency, or repeat usage.</p>
		<p><strong>Scale horizon:</strong> Now: active operations running across commerce, advisory, and tenantized CP/ERP delivery.<br />In 6 months: expand depth in each ecosystem with adjacent high-frequency workflows.<br />In 1-2 years: a connected ecosystem portfolio with shared data rails and repeat B2B/B2C cycles.</p>
		<p><strong>Positioning:</strong> ECOM AE is a structured multi-ecosystem platform, not a random listing site.</p>
	</div>
</section>
	";

    internal const string IndustryMissingTpl = @"<div class=""epm-wrap"">
	<div class=""epm-alert err"">Industry not found. <a href=""{BASE}platform/industries"" style=""color:inherit"">Browse all industries</a></div>
</div>
		";

    internal const string ContactTpl = @"<div class=""epm-wrap"">
	<div class=""epm-hero"" style=""min-height:200px"">
		<div class=""epm-hero__shade"" style=""opacity:1;background:linear-gradient(135deg,#1a0407,#5a0f16)""></div>
		<div class=""epm-hero__content"">
			<h1>Contact ECOM AE</h1>
			<p class=""lead"">Demo requests, rental quotes, and operator support. Response within one business day.</p>
		</div>
	</div>
{FLASH}		<div class=""epm-grid"">
		<div class=""epm-card"">
			<h4><i class=""fa fa-phone text-primary""></i> Phone</h4>
			<p><a href=""tel:+971567607011"" style=""color:var(--epm-cyan)"">+971 56 760 7011</a></p>
		</div>
		<div class=""epm-card"">
			<h4><i class=""fa fa-whatsapp text-primary""></i> WhatsApp</h4>
			<p><a href=""https://wa.me/971567607011?text=Hello%20ECOM%20AE%20%E2%80%94%20I%20would%20like%20to%20discuss%20platform%20rental%20or%20a%20demo."" style=""color:var(--epm-cyan)"" target=""_blank"" rel=""noopener"">Chat on WhatsApp</a></p>
		</div>
		<div class=""epm-card"">
			<h4><i class=""fa fa-envelope text-primary""></i> Email</h4>
			<p><a href=""mailto:hello@ecomae.com"" style=""color:var(--epm-cyan)"">hello@ecomae.com</a></p>
		</div>
		<div class=""epm-card"">
			<h4><i class=""fa fa-th-large text-primary""></i> Super CP</h4>
			<p><a href=""{SUPER}"" style=""color:var(--epm-cyan)"">Operator control panel</a></p>
			<p class=""text-muted"" style=""font-size:13px"">Tenant hub, onboard client, DNS checklist.</p>
		</div>
	</div>
	<div class=""epm-split"">
		<div class=""epm-card epm-form"">
			<h4>Send a message</h4>
			<form method=""post"" action="""">
				<input type=""hidden"" name=""epc_contact_request"" value=""1"" />
				<div class=""form-group"">
					<label>Name</label>
					<input class=""form-control"" name=""contact_name"" required />
				</div>
				<div class=""form-group"">
					<label>Email</label>
					<input class=""form-control"" type=""email"" name=""contact_email"" required />
				</div>
				<div class=""form-group"">
					<label>Company</label>
					<input class=""form-control"" name=""company"" />
				</div>
				<div class=""form-group"">
					<label>Message</label>
					<textarea class=""form-control"" name=""message"" rows=""4"" required></textarea>
				</div>
				<button type=""submit"" class=""epm-btn epm-btn--primary"" style=""width:100%;justify-content:center;border:0;cursor:pointer"">Send enquiry</button>
			</form>
		</div>
		<div class=""epm-card"">
			<h4><i class=""fa fa-map-marker text-primary""></i> Electronic World Group</h4>
			<p>Dubai, United Arab Emirates</p>
			<p style=""color:var(--epm-muted);font-size:14px"">Hosted multi-tenant cloud · Go live in 24 hours · UAE e-invoice ready.</p>
		</div>
	</div>
	<div class=""epm-cta"">
		<a class=""epm-btn epm-btn--primary"" href=""{BASE}platform/demo"">Request demo</a>
		<a class=""epm-btn epm-btn--outline"" href=""{BASE}platform/industries"">Browse industries</a>
	</div>
</div>
	";

    internal static string Fill(string tpl, string baseUrl, string days = "3")
        => tpl.Replace("{BASE}", baseUrl, StringComparison.Ordinal)
            .Replace("{DAYS}", days, StringComparison.Ordinal);
}
