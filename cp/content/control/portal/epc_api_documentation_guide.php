<?php
/**
 * Super CP — API documentation & key management guide.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';

$isSuper = function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator();
if (!$isSuper) {
	echo '<div class="alert alert-warning">This guide is available on <strong>Super CP</strong> (www.ecomae.com) only.</div>';
	return;
}

function epc_adg_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$backend = htmlspecialchars((string) $GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');
$token = 'epartscart-deploy-2026';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array('class' => 'epc-adg'));
?>
<div class="epc-portal-settings epc-adg">
	<div class="hero">
		<h2><i class="fa fa-code"></i> API documentation &amp; tenant keys</h2>
		<p style="margin:0;opacity:.92">Phase 1 public REST API at <code>/epc-api/v1/</code> — read-only, tenant-scoped via <code>X-API-Key</code>. Keys live in platform DB (<code>ecomae</code>), never in marketing HTML.</p>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Quick links</h4></div>
		<div class="panel-body">
			<ul>
				<li><a href="https://www.ecomae.com/platform/api-documentation" target="_blank" rel="noopener">Marketing — API documentation</a></li>
				<li><a href="https://www.ecomae.com/platform/api-services" target="_blank" rel="noopener">Marketing — Catalog &amp; Price PRO API</a></li>
				<li><a href="/<?php echo $backend; ?>/control/portal/epc_api_clients_manage">Catalog &amp; Price PRO — client keys</a></li>
				<li><a href="https://www.ecomae.com/epc-api/v1/openapi.json" target="_blank" rel="noopener">OpenAPI spec (JSON)</a></li>
				<li><a href="https://www.ecomae.com/epc-api/v1/health" target="_blank" rel="noopener">API health probe</a></li>
			</ul>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Issue API keys (operators)</h4></div>
		<div class="panel-body">
			<ol>
				<li>Run setup on platform stack (once per environment):<br>
					<code>https://www.ecomae.com/epc-api-keys-setup.php?token=<?php echo epc_adg_h($token); ?></code><br>
					<span class="text-muted">Creates <code>epc_api_keys</code> table and rotates demo keys for <code>epartscart</code> and <code>asap</code>. Plain keys print in setup output only — copy to password manager.</span>
				</li>
				<li>Register Super CP menu (if missing):<br>
					<code>https://www.ecomae.com/epc-api-documentation-cp-setup.php?token=<?php echo epc_adg_h($token); ?></code>
				</li>
				<li>For enterprise tenants, insert a row in <code>epc_api_keys</code> with SHA-256 hash of the key, tenant <code>site_key</code>, and scopes JSON.</li>
			</ol>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Scopes (Phase 1)</h4></div>
		<div class="panel-body">
			<table class="table">
				<thead><tr><th>Scope</th><th>Endpoint</th></tr></thead>
				<tbody>
					<tr><td><code>read:tenant</code></td><td>GET /epc-api/v1/tenant/info</td></tr>
					<tr><td><code>read:orders</code></td><td>GET /epc-api/v1/orders</td></tr>
					<tr><td><code>read:products</code></td><td>GET /epc-api/v1/products/search?q=</td></tr>
					<tr><td><code>read:erp</code></td><td>GET /epc-api/v1/erp/dashboard-summary</td></tr>
					<tr><td><code>read:bi</code></td><td>GET /epc-api/v1/powerbi/* (Power BI JSON/CSV datasets)</td></tr>
					<tr><td><code>read:*</code></td><td>All authenticated read endpoints</td></tr>
				</tbody>
			</table>
			<p class="text-muted">Power BI Desktop accepts <code>read:bi</code> or existing <code>read:erp</code> / <code>read:*</code> keys. Guide: <a href="/<?php echo $backend; ?>/control/portal/epc_power_bi">Portal → Power BI</a> · <code>docs/POWER_BI.md</code></p>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Test with curl</h4></div>
		<div class="panel-body">
			<pre># Public — no key
curl -s https://www.ecomae.com/epc-api/v1/health

# Authenticated — replace with key from setup output
curl -s -H "X-API-Key: epc_YOUR_TENANT_read_XXXXXXXXXXXXXXXX" \
  https://www.ecomae.com/epc-api/v1/tenant/info

curl -s -H "X-API-Key: epc_YOUR_TENANT_read_XXXXXXXXXXXXXXXX" \
  "https://www.ecomae.com/epc-api/v1/products/search?q=filter"

curl -s -H "X-API-Key: epc_YOUR_TENANT_read_XXXXXXXXXXXXXXXX" \
  https://www.ecomae.com/epc-api/v1/erp/dashboard-summary

# Power BI — KPI CSV (Web connector)
curl -s -H "X-API-Key: epc_YOUR_TENANT_read_XXXXXXXXXXXXXXXX" \
  "https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv"</pre>
			<p class="text-muted">Expect HTTP 401 without header. Never paste live keys into tickets, chat, or marketing pages.</p>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Tenant vs platform keys</h4></div>
		<div class="panel-body">
			<ul>
				<li><strong>Tenant keys</strong> — bound to one <code>site_key</code>; queries run against that tenant’s dedicated commerce database (e.g. automotive or industry-specific schemas).</li>
				<li><strong>Platform keys</strong> — not issued in Phase 1; Super CP deploy token is for operators only, not customer integrations.</li>
				<li><strong>ERP ajax</strong> — internal CP session endpoints under <code>/cp/shop/finance/erp/ajax_*</code> are not the public API.</li>
			</ul>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Security rules</h4></div>
		<div class="panel-body">
			<ul>
				<li>Store only <code>key_hash</code> (SHA-256) in DB — never the plain key after handoff.</li>
				<li>Revoke by setting <code>active = 0</code> on the key row.</li>
				<li>Phase 1 is read-only — no POST/PUT/DELETE on tenant data via public API.</li>
				<li>Rate-limit at nginx/Cloudflare for production tenants if needed.</li>
			</ul>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Future scope (not Phase 1)</h4></div>
		<div class="panel-body">
			<ul>
				<li>Webhooks (order placed, stock low)</li>
				<li>E-invoice submit (Peppol / PINT-AE)</li>
				<li>Microsoft Dynamics 365 sync</li>
				<li>Marketplace channel write APIs</li>
				<li>Write APIs v2 (create order, update stock)</li>
			</ul>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
