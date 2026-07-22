<?php
/**
 * CP — Bulk upload hub: review storefront uploads, process for customers,
 * push to cart / shop quote / ERP CRM quote.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/bulk_upload/epc_bulk_helpers.php';

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$db_link->query('SET NAMES utf8;');
	} catch (Exception $e) {
		echo '<div class="alert alert-danger">Database connection failed.</div>';
		return;
	}
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	if (!headers_sent()) {
		header('Content-Type: application/json; charset=utf-8');
	}
	require __DIR__ . '/ajax_bulk_cp.php';
	die();
}

try {
	epc_bulk_ensure_history_schema($db_link);
	$dash = epc_bulk_dashboard($db_link);
} catch (Throwable $e) {
	echo '<div class="alert alert-danger">Bulk upload data unavailable: ' . htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8') . '</div>';
	return;
}

$price_profiles = array();
try {
	$pq = $db_link->query(
		'SELECT pp.`group_id`, pp.`code`, g.`value`
		 FROM `epc_price_profiles` pp
		 INNER JOIN `groups` g ON g.`id` = pp.`group_id`
		 ORDER BY pp.`id` ASC'
	);
	while ($row = $pq->fetch(PDO::FETCH_ASSOC)) {
		$row['caption'] = function_exists('translate_str_by_key')
			? translate_str_by_key($row['value'])
			: (function_exists('translate_str_by_id') ? translate_str_by_id($row['value']) : $row['code']);
		$price_profiles[] = $row;
	}
} catch (Throwable $e) {
	$price_profiles = array();
}

$backend = isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp';
$hubUrl = '/' . $backend . '/shop/bulk_upload';
$quotesUrl = '/' . $backend . '/shop/quote-requests';
$crmUrl = '/' . $backend . '/shop/finance/erp?tab=crm';
$erpUrl = '/' . $backend . '/shop/finance/erp';
$cartsUrl = '/' . $backend . '/shop/orders/carts';
$storefrontUrl = '/en/shop/bulk-upload';
$ajaxUrl = '/' . $backend . '/content/shop/bulk_upload/ajax_bulk_cp.php';
$csrf = '';
$user_session = DP_User::getAdminSession();
if (is_array($user_session) && !empty($user_session['csrf_guard_key'])) {
	$csrf = (string)$user_session['csrf_guard_key'];
}

$cssVer = @filemtime($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_bulk_cp.css') ?: time();
echo '<link rel="stylesheet" href="/content/general_pages/epc_bulk_cp.css?v=' . rawurlencode((string)$cssVer) . '">';
echo '<div class="col-lg-12 epc-bu-hub">';
?>
<div class="epc-bu">
	<header class="epc-bu-brandbar">
		<div class="epc-bu-brandbar__mark" aria-hidden="true"><i class="fa fa-file-excel-o"></i></div>
		<div>
			<div class="epc-bu-brandbar__name">Bulk Upload Control</div>
			<div class="epc-bu-brandbar__sub">Review every storefront spare-parts list, run the same match engine from CP, then push selected lines to a customer cart, shop quote, or ERP CRM quote.</div>
		</div>
		<div class="epc-bu-brandbar__actions">
			<a class="epc-bu-chip-link" href="<?php echo epc_bulk_h($storefrontUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Storefront</a>
			<a class="epc-bu-chip-link" href="<?php echo epc_bulk_h($quotesUrl); ?>"><i class="fa fa-file-text-o"></i> Quotes</a>
			<a class="epc-bu-chip-link" href="<?php echo epc_bulk_h($crmUrl); ?>"><i class="fa fa-briefcase"></i> ERP quotes</a>
			<a class="epc-bu-chip-link" href="<?php echo epc_bulk_h($cartsUrl); ?>"><i class="fa fa-shopping-cart"></i> Carts</a>
		</div>
	</header>

	<div class="epc-bu-kpi" role="group" aria-label="Bulk upload metrics">
		<div class="epc-bu-kpi__item">
			<div class="epc-bu-kpi__icon"><i class="fa fa-database"></i></div>
			<div><div class="epc-bu-kpi__val"><?php echo (int)$dash['total']; ?></div><div class="epc-bu-kpi__label">All uploads</div></div>
		</div>
		<div class="epc-bu-kpi__item">
			<div class="epc-bu-kpi__icon epc-bu-kpi__icon--amber"><i class="fa fa-eye"></i></div>
			<div><div class="epc-bu-kpi__val"><?php echo (int)$dash['unreviewed']; ?></div><div class="epc-bu-kpi__label">Needs review</div></div>
		</div>
		<div class="epc-bu-kpi__item">
			<div class="epc-bu-kpi__icon epc-bu-kpi__icon--green"><i class="fa fa-calendar"></i></div>
			<div><div class="epc-bu-kpi__val"><?php echo (int)$dash['today']; ?></div><div class="epc-bu-kpi__label">Today</div></div>
		</div>
		<div class="epc-bu-kpi__item">
			<div class="epc-bu-kpi__icon"><i class="fa fa-globe"></i></div>
			<div><div class="epc-bu-kpi__val"><?php echo (int)$dash['storefront']; ?></div><div class="epc-bu-kpi__label">From website</div></div>
		</div>
		<div class="epc-bu-kpi__item">
			<div class="epc-bu-kpi__icon epc-bu-kpi__icon--slate"><i class="fa fa-check-circle"></i></div>
			<div><div class="epc-bu-kpi__val"><?php echo (int)$dash['available_today']; ?></div><div class="epc-bu-kpi__label">Matched today</div></div>
		</div>
	</div>

	<div id="epc_bu_msg" class="alert epc-bu-msg" style="display:none;"></div>

	<div class="epc-bu-tabs" role="tablist">
		<button type="button" class="epc-bu-tab is-active" data-tab="inbox"><i class="fa fa-inbox"></i> Review inbox</button>
		<button type="button" class="epc-bu-tab" data-tab="process"><i class="fa fa-upload"></i> Process for customer</button>
		<button type="button" class="epc-bu-tab" data-tab="detail"><i class="fa fa-list"></i> Open upload</button>
	</div>

	<section class="epc-bu-panel is-on" data-panel="inbox">
		<div class="epc-bu-section">
			<div class="epc-bu-section__head">
				<h3><i class="fa fa-inbox"></i> Customer &amp; CP uploads</h3>
				<span>Same history as storefront — visible here for every customer</span>
			</div>
			<div class="epc-bu-section__body">
				<div class="epc-bu-toolbar">
					<label style="font-size:12px;font-weight:700;"><input type="checkbox" id="epc_bu_filter_unreviewed" checked> Needs review only</label>
					<select id="epc_bu_filter_source" class="form-control input-sm" style="width:auto;display:inline-block;">
						<option value="">All sources</option>
						<option value="storefront">Storefront</option>
						<option value="cp">CP</option>
					</select>
					<input type="text" id="epc_bu_filter_q" class="form-control input-sm" style="width:200px;display:inline-block;" placeholder="File or user id">
					<button type="button" class="btn btn-sm btn-default" id="epc_bu_refresh_inbox"><i class="fa fa-refresh"></i> Refresh</button>
				</div>
				<div id="epc_bu_inbox_body"><div class="epc-bu-empty">Loading…</div></div>
			</div>
		</div>
	</section>

	<section class="epc-bu-panel" data-panel="process">
		<div class="epc-bu-section">
			<div class="epc-bu-section__head">
				<h3><i class="fa fa-upload"></i> Process list for a customer</h3>
				<span>Brand · Part · Qty columns — same engine as the website</span>
			</div>
			<div class="epc-bu-section__body">
				<div class="epc-bu-form">
					<div class="epc-bu-field">
						<label>Customer</label>
						<div style="display:flex;gap:8px;">
							<input type="text" id="epc_bu_customer_q" placeholder="Email, name, phone, or user id">
							<button type="button" class="btn btn-default" id="epc_bu_customer_search"><i class="fa fa-search"></i></button>
						</div>
						<div id="epc_bu_customer_pick" class="epc-bu-customer-pick"></div>
						<div id="epc_bu_selected_customer" class="epc-bu-selected">No customer selected</div>
					</div>
					<div class="epc-bu-field">
						<label>Price profile</label>
						<select id="epc_bu_group_id">
							<option value="">Customer default</option>
							<?php foreach ($price_profiles as $pp): ?>
								<option value="<?php echo (int)$pp['group_id']; ?>"><?php echo epc_bulk_h($pp['caption'] . ' (#' . $pp['group_id'] . ')'); ?></option>
							<?php endforeach; ?>
						</select>
					</div>
					<div class="epc-bu-field">
						<label>Priority</label>
						<select id="epc_bu_priority">
							<option value="price">Best price</option>
							<option value="delivery">Fastest delivery</option>
						</select>
					</div>
					<div class="epc-bu-field">
						<label>Excel / CSV</label>
						<input type="file" id="epc_bu_file" accept=".xlsx,.csv,.txt,.tsv">
					</div>
				</div>
				<p style="margin-top:14px;">
					<button type="button" class="btn btn-primary" id="epc_bu_process"><i class="fa fa-cogs"></i> Match &amp; open results</button>
				</p>
			</div>
		</div>
	</section>

	<section class="epc-bu-panel" data-panel="detail">
		<div class="epc-bu-section">
			<div class="epc-bu-section__head">
				<h3><i class="fa fa-list-alt"></i> Upload detail &amp; actions</h3>
				<span>Add to cart · shop quote · ERP CRM quote</span>
			</div>
			<div class="epc-bu-section__body">
				<div id="epc_bu_detail_meta" class="epc-bu-detail-meta"></div>
				<div class="epc-bu-toolbar">
					<button type="button" class="btn btn-success btn-sm" id="epc_bu_add_cart"><i class="fa fa-shopping-cart"></i> Add selected to customer cart</button>
					<button type="button" class="btn btn-primary btn-sm" id="epc_bu_shop_quote"><i class="fa fa-file-text-o"></i> Create shop quote</button>
					<button type="button" class="btn btn-info btn-sm" id="epc_bu_crm_quote"><i class="fa fa-briefcase"></i> Create ERP quote</button>
					<button type="button" class="btn btn-default btn-sm" id="epc_bu_mark_reviewed"><i class="fa fa-check"></i> Mark reviewed</button>
					<a class="btn btn-default btn-sm" href="<?php echo epc_bulk_h($erpUrl); ?>"><i class="fa fa-th-large"></i> ERP hub</a>
				</div>
				<div id="epc_bu_detail_body"><div class="epc-bu-empty">Open an upload from the inbox, or process a new file.</div></div>
			</div>
		</div>
	</section>
</div>
</div>
<script>
window.EPC_BU = {
	ajaxUrl: <?php echo json_encode($ajaxUrl); ?>,
	hubUrl: <?php echo json_encode($hubUrl); ?>,
	csrf: <?php echo json_encode($csrf); ?>
};
</script>
<?php
$jsVer = @filemtime(__DIR__ . '/bulk_upload_hub.js') ?: time();
echo '<script src="/' . epc_bulk_h($backend) . '/content/shop/bulk_upload/bulk_upload_hub.js?v=' . rawurlencode((string)$jsVer) . '"></script>';
?>
