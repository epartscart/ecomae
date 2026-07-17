<?php
/**
 * CP page: Commerce data upload → warehouse price lists (*-S / *.P / *-L).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_commerce_price_ingest.php';

$user_session = DP_User::getAdminSession();
$csrf = htmlspecialchars((string) ($user_session['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8');
$backend = htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8');

$sources = array();
try {
	$sources = epc_commerce_list_sources($db_link, false);
} catch (Throwable $e) {
	$sources = array();
}

$epc_commerce_url_count = 0;
foreach ($sources as $epc_src_row) {
	if (!empty($epc_src_row['has_url'])) {
		$epc_commerce_url_count++;
	}
}

/**
 * @param array<string,mixed> $src
 */
function epc_commerce_render_source_row(array $src, string $backend): string
{
	$priceId = (int) ($src['price_id'] ?? 0);
	$name = htmlspecialchars((string) ($src['price_name'] ?? ''), ENT_QUOTES, 'UTF-8');
	$role = strtolower((string) ($src['role'] ?? ''));
	$roleClass = $role === 'sales' ? 'success' : ($role === 'purchase' ? 'warning' : 'info');
	$roleLabel = $role !== '' ? htmlspecialchars($role, ENT_QUOTES, 'UTF-8') : '—';
	$margin = htmlspecialchars((string) ($src['margin_percent'] ?? '0'), ENT_QUOTES, 'UTF-8');
	$link = (string) ($src['link'] ?? '');
	$hasUrl = !empty($src['has_url']);
	$lu = (int) ($src['last_updated'] ?? 0);
	$updated = $lu > 0 ? htmlspecialchars(date('Y-m-d H:i', $lu), ENT_QUOTES, 'UTF-8') : '—';
	$rows = isset($src['records_count']) ? number_format((int) $src['records_count']) : '—';
	$safeLink = htmlspecialchars($link, ENT_QUOTES, 'UTF-8');
	$urlCell = $hasUrl
		? '<a class="epc-commerce-url" href="' . $safeLink . '" target="_blank" rel="noopener" title="' . $safeLink . '">' . $safeLink . '</a>'
		: '<span class="epc-commerce-muted">File upload only</span>';
	$refreshBtn = $hasUrl
		? '<button type="button" class="btn btn-xs btn-primary epc-commerce-refresh-one" data-price-id="' . $priceId . '"><i class="fas fa-cloud-download-alt"></i> Refresh</button> '
		: '';

	return '<tr data-price-id="' . $priceId . '">'
		. '<td><strong>' . $name . '</strong><br><small class="epc-commerce-muted">#' . $priceId . '</small></td>'
		. '<td><span class="label label-' . $roleClass . '">' . $roleLabel . '</span></td>'
		. '<td class="epc-commerce-num">' . $margin . '%</td>'
		. '<td class="epc-commerce-num">' . htmlspecialchars($rows, ENT_QUOTES, 'UTF-8') . '</td>'
		. '<td class="epc-commerce-url-cell">' . $urlCell . '</td>'
		. '<td class="epc-commerce-num">' . $updated . '</td>'
		. '<td class="text-right epc-commerce-row-actions">'
		. $refreshBtn
		. '<a class="btn btn-xs btn-default" href="/' . $backend . '/shop/prices/price?price_id=' . $priceId . '">Open</a>'
		. '</td></tr>';
}
?>
<div class="epc-commerce-page">
	<div class="epc-commerce-hero">
		<div class="epc-commerce-hero__text">
			<p class="epc-commerce-kicker">Shop · Price lists</p>
			<h2>Commerce data</h2>
			<p>Turn sales, purchase, or inventory Excel/CSV into warehouse price lists for storefront search. Re-upload anytime, or connect a file URL and refresh on demand / via cron.</p>
		</div>
		<div class="epc-commerce-hero__actions">
			<a class="btn btn-default" href="/<?php echo $backend; ?>/shop/prices"><i class="fas fa-list"></i> Price lists</a>
			<a class="btn btn-default" href="/<?php echo $backend; ?>/shop/prices/guide"><i class="fas fa-book"></i> Upload guide</a>
		</div>
	</div>

	<div class="epc-commerce-stats">
		<div class="epc-commerce-stat">
			<span class="epc-commerce-stat__lbl">Linked lists</span>
			<span class="epc-commerce-stat__val"><?php echo count($sources); ?></span>
		</div>
		<div class="epc-commerce-stat">
			<span class="epc-commerce-stat__lbl">With URL refresh</span>
			<span class="epc-commerce-stat__val"><?php echo (int) $epc_commerce_url_count; ?></span>
		</div>
		<div class="epc-commerce-stat">
			<span class="epc-commerce-stat__lbl">Roles</span>
			<span class="epc-commerce-stat__val epc-commerce-stat__val--sm">*-S · *.P · *-L</span>
		</div>
	</div>

	<div class="epc-commerce-roles">
		<article class="epc-commerce-role epc-commerce-role--sales">
			<span class="epc-commerce-role__badge">*-S</span>
			<h3>Sales</h3>
			<p>Highest sales price per brand+article becomes shelf price. Quantities are summed.</p>
			<code>BASE-S</code>
		</article>
		<article class="epc-commerce-role epc-commerce-role--purchase">
			<span class="epc-commerce-role__badge">*.P</span>
			<h3>Purchase</h3>
			<p>One list per supplier. Shelf price = cost × (1 + margin%). Lowest cost wins on duplicates.</p>
			<code>SUPPLIER.P</code>
		</article>
		<article class="epc-commerce-role epc-commerce-role--inventory">
			<span class="epc-commerce-role__badge">*-L</span>
			<h3>Inventory</h3>
			<p>Stock quantities + cost/list price with margin. Creates or updates the local warehouse list.</p>
			<code>BASE-L</code>
		</article>
	</div>

	<div class="hpanel epc-commerce-panel">
		<div class="panel-heading hbuilt">
			<i class="fas fa-upload"></i> Upload file or connect URL
		</div>
		<div class="panel-body">
			<form id="epcCommerceIngestForm" enctype="multipart/form-data" onsubmit="return false;">
				<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
				<div class="epc-commerce-form-grid">
					<div class="epc-commerce-field">
						<label for="epcCommerceRole">Data type</label>
						<select class="form-control" name="role" id="epcCommerceRole">
							<option value="sales">Sales → *-S</option>
							<option value="purchase">Purchase → *.P</option>
							<option value="inventory">Inventory → *-L</option>
						</select>
					</div>
					<div class="epc-commerce-field">
						<label for="epcCommerceBase">Base name</label>
						<input class="form-control" type="text" name="base_name" id="epcCommerceBase" value="EPC" placeholder="MAIN or EPC" autocomplete="off" />
						<small class="help-block">Used for <code>BASE-S</code> / <code>BASE-L</code>. Purchase prefers the Supplier column.</small>
					</div>
					<div class="epc-commerce-field" id="epcCommerceMarginWrap">
						<label for="epcCommerceMargin">Margin %</label>
						<input class="form-control" type="number" step="0.01" min="0" name="margin_percent" id="epcCommerceMargin" value="0" />
						<small class="help-block">Purchase &amp; inventory (saved for URL refresh)</small>
					</div>
					<div class="epc-commerce-field epc-commerce-field--wide">
						<label for="epcCommerceFile">Excel / CSV file <span class="epc-commerce-muted">(optional if URL set)</span></label>
						<input class="form-control" type="file" name="price_file" id="epcCommerceFile" accept=".csv,.txt,.xls,.xlsx" />
					</div>
					<div class="epc-commerce-field epc-commerce-field--wide">
						<label for="epcCommerceUrl">Recurring file URL</label>
						<input class="form-control" type="url" name="source_url" id="epcCommerceUrl" placeholder="https://…/sales.xlsx" autocomplete="off" />
						<small class="help-block">Direct HTTPS link to Excel/CSV. Google Drive / Dropbox share links are converted when possible. Refresh below or use cron <code>action=refresh_all</code>.</small>
					</div>
				</div>
				<div class="epc-commerce-form-actions">
					<button type="button" class="btn btn-primary" id="epcCommerceSubmit">
						<i class="fas fa-upload"></i> Import to warehouse price lists
					</button>
				</div>
			</form>
			<div id="epcCommerceResult" class="epc-commerce-result" aria-live="polite"></div>
		</div>
	</div>

	<div class="hpanel epc-commerce-panel">
		<div class="panel-heading hbuilt epc-commerce-panel__head">
			<span><i class="fas fa-link"></i> Linked commerce lists</span>
			<span class="epc-commerce-panel__tools">
				<button type="button" class="btn btn-xs btn-default" id="epcCommerceReloadSources"><i class="fas fa-sync"></i> Reload</button>
				<button type="button" class="btn btn-xs btn-warning" id="epcCommerceRefreshAll"><i class="fas fa-cloud-download-alt"></i> Refresh all URLs</button>
			</span>
		</div>
		<div class="panel-body">
			<div class="epc-commerce-list-toolbar">
				<input type="search" class="form-control epc-commerce-filter" id="epcCommerceFilter" placeholder="Filter lists…" autocomplete="off" />
				<span class="epc-commerce-muted" id="epcCommerceListCount"><?php echo count($sources); ?> list<?php echo count($sources) === 1 ? '' : 's'; ?></span>
			</div>
			<div class="table-responsive epc-commerce-table-wrap">
				<table class="table table-striped table-condensed" id="epcCommerceSourcesTable">
					<thead>
						<tr>
							<th>List</th>
							<th>Role</th>
							<th>Margin</th>
							<th>Rows</th>
							<th>URL</th>
							<th>Updated</th>
							<th></th>
						</tr>
					</thead>
					<tbody id="epcCommerceSourcesBody">
					<?php if (count($sources) === 0): ?>
						<tr class="epc-commerce-empty-row"><td colspan="7" class="text-muted">No commerce lists yet — import a sales / purchase / inventory file above.</td></tr>
					<?php else: ?>
						<?php foreach ($sources as $src): ?>
							<?php echo epc_commerce_render_source_row($src, $backend); ?>
						<?php endforeach; ?>
					<?php endif; ?>
					</tbody>
				</table>
			</div>
			<div id="epcCommerceRefreshResult" class="epc-commerce-result" aria-live="polite"></div>
		</div>
	</div>

	<div class="hpanel epc-commerce-panel">
		<div class="panel-heading hbuilt">
			<i class="fas fa-table"></i> Expected columns
		</div>
		<div class="panel-body">
			<p class="epc-commerce-help">Headers are matched flexibly (Brand/Manufacturer, Article/SKU/Number, Name/Description, Qty/Stock, Price/Sales price, Cost/Purchase, Supplier/Vendor).</p>
			<pre class="epc-commerce-pre">Brand,Article,Name,Qty,Price
Brand,Article,Name,Qty,Cost,Supplier
Brand,Article,Name,Stock,Cost</pre>
			<p class="epc-commerce-help"><strong>Cron</strong> — refresh every linked file URL nightly:</p>
			<pre class="epc-commerce-pre">wget -q -O /dev/null 'https://www.epartscart.com/epc-upload-commerce-prices.php?token=…&amp;key=&lt;tech_key&gt;&amp;action=refresh_all'</pre>
		</div>
	</div>
</div>
