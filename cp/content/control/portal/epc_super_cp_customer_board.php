<?php
/**
 * Super CP — Customer board (cross-tenant search, quick actions, ERP links).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_super_cp_platform.php';

if (!epc_scp_guard_super_admin()) {
	return;
}

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : (function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null);
if (!$pdo instanceof PDO) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}

$search = isset($_GET['q']) ? trim((string) $_GET['q']) : '';
$tenantFilter = isset($_GET['tenant']) ? (string) $_GET['tenant'] : '';
$page = max(1, (int) ($_GET['page'] ?? 1));
$board = epc_scp_customer_board_search($pdo, $search, $tenantFilter, $page, 50);
$tenants = epc_scp_tenant_options($pdo);
$backend = epc_scp_backend();
$selfUrl = '/' . $backend . '/control/portal/epc_super_cp_customer_board';
?>
<div class="col-lg-12 epc-scp-panel epc-scp-customer-board">
<?php
epc_scp_render_hero(
	'Super CP',
	'Customer board',
	'Search and filter customers across the platform and live tenant databases — open CRM or ERP in one click.',
	array(
		array('label' => 'Tenant hub', 'icon' => 'fa-cloud', 'url' => '/' . $backend . '/shop/tenant_hub/tenant_hub'),
		array('label' => 'Communication', 'icon' => 'fa-envelope', 'url' => '/' . $backend . '/control/portal/epc_super_cp_communication', 'primary' => true),
		array('label' => 'Operator guide', 'icon' => 'fa-book', 'url' => epc_scp_operator_guide_url()),
	)
);
?>
<?php epc_scp_render_workspace_intro('customer_board'); ?>

<div class="epc-scp-kpi">
	<div class="epc-scp-kpi__card">
		<div class="epc-scp-kpi__label">Results</div>
		<div class="epc-scp-kpi__val"><?php echo (int) $board['total']; ?></div>
		<div class="epc-scp-kpi__hint">Matching customers</div>
	</div>
	<div class="epc-scp-kpi__card">
		<div class="epc-scp-kpi__label">Platform</div>
		<div class="epc-scp-kpi__val"><?php echo (int) ($board['stats']['platform'] ?? 0); ?></div>
		<div class="epc-scp-kpi__hint">ecomae DB</div>
	</div>
	<div class="epc-scp-kpi__card">
		<div class="epc-scp-kpi__label">Tenants scanned</div>
		<div class="epc-scp-kpi__val"><?php echo (int) ($board['stats']['tenants_scanned'] ?? 0); ?></div>
		<div class="epc-scp-kpi__hint"><?php echo (int) ($board['stats']['tenants_with_hits'] ?? 0); ?> with hits</div>
	</div>
	<div class="epc-scp-kpi__card">
		<div class="epc-scp-kpi__label">Live tenants</div>
		<div class="epc-scp-kpi__val"><?php echo count($tenants); ?></div>
		<div class="epc-scp-kpi__hint">In registry</div>
	</div>
</div>

<div class="epc-scp-filter-bar">
	<form method="get" class="form-inline">
		<div class="form-group">
			<label class="sr-only">Search</label>
			<input type="search" name="q" class="form-control input-sm" placeholder="Email, phone, name, company…" value="<?php echo epc_scp_h($search); ?>" style="min-width:220px" />
		</div>
		<div class="form-group">
			<select name="tenant" class="form-control input-sm">
				<option value="">All sources</option>
				<option value="platform"<?php echo $tenantFilter === 'platform' ? ' selected' : ''; ?>>Platform only</option>
				<?php foreach ($tenants as $t) { ?>
				<option value="<?php echo epc_scp_h($t['site_key']); ?>"<?php echo $tenantFilter === $t['site_key'] ? ' selected' : ''; ?>><?php echo epc_scp_h($t['label']); ?></option>
				<?php } ?>
			</select>
		</div>
		<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-search"></i> Search</button>
		<?php if ($search !== '' || $tenantFilter !== '') { ?>
		<a class="btn btn-default btn-sm" href="<?php echo epc_scp_h($selfUrl); ?>">Clear</a>
		<?php } ?>
	</form>
</div>

<div class="epc-scp-table-card">
	<div class="table-responsive">
		<table class="table table-striped table-bordered table-condensed epc-scp-data-table">
			<thead>
				<tr>
					<th>Customer</th>
					<th>Email / phone</th>
					<th>Source</th>
					<th>Registered</th>
					<th>Quick actions</th>
				</tr>
			</thead>
			<tbody>
			<?php if (count($board['rows']) === 0) { ?>
				<tr><td colspan="5" class="epc-scp-empty-cell">
					<?php
					epc_scp_render_empty_state(
						'No customers found',
						$search !== '' || $tenantFilter !== ''
							? 'Try a broader search, clear filters, or pick another tenant from the registry.'
							: 'Search by email, phone, name, or company to find users across platform and tenant databases.',
						array(
							array('label' => 'Clear search', 'icon' => 'fa-eraser', 'url' => $selfUrl, 'primary' => ($search !== '' || $tenantFilter !== '')),
							array('label' => 'Operator guide', 'icon' => 'fa-book', 'url' => epc_scp_operator_guide_url()),
							array('label' => 'Tenant hub', 'icon' => 'fa-cloud', 'url' => '/' . $backend . '/shop/tenant_hub/tenant_hub'),
						)
					);
					?>
				</td></tr>
			<?php } ?>
			<?php foreach ($board['rows'] as $row) {
				$links = is_array($row['links'] ?? null) ? $row['links'] : array();
				?>
				<tr>
					<td>
						<strong><?php echo epc_scp_h($row['name'] ?? ''); ?></strong>
						<div class="text-muted small">#<?php echo (int) ($row['user_id'] ?? 0); ?></div>
					</td>
					<td>
						<?php if (!empty($row['email'])) { ?><div><a href="mailto:<?php echo epc_scp_h($row['email']); ?>"><?php echo epc_scp_h($row['email']); ?></a></div><?php } ?>
						<?php if (!empty($row['phone'])) { ?><div class="text-muted small"><?php echo epc_scp_h($row['phone']); ?></div><?php } ?>
					</td>
					<td>
						<span class="epc-scp-badge epc-scp-badge--tenant"><?php echo epc_scp_h($row['source_label'] ?? $row['source'] ?? ''); ?></span>
						<?php if (!empty($row['hostname'])) { ?><div class="text-muted small"><?php echo epc_scp_h($row['hostname']); ?></div><?php } ?>
					</td>
					<td><?php echo !empty($row['time_reg']) ? epc_scp_h(date('Y-m-d', (int) $row['time_reg'])) : '—'; ?></td>
					<td class="epc-scp-actions-cell">
						<?php if (!empty($links['crm'])) { ?>
						<a class="btn btn-xs btn-default" href="<?php echo epc_scp_h($links['crm']); ?>" target="_blank" rel="noopener"><i class="fa fa-address-book"></i> CRM</a>
						<?php } ?>
						<?php if (!empty($links['erp'])) { ?>
						<a class="btn btn-xs btn-primary" href="<?php echo epc_scp_h($links['erp']); ?>" target="_blank" rel="noopener"><i class="fa fa-university"></i> ERP</a>
						<?php } ?>
						<?php if (!empty($links['cp'])) { ?>
						<a class="btn btn-xs btn-default" href="<?php echo epc_scp_h($links['cp']); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> CP</a>
						<?php } ?>
					</td>
				</tr>
			<?php } ?>
			</tbody>
		</table>
	</div>
	<?php
	$pages = (int) ceil(max(1, $board['total']) / max(1, $board['per_page']));
	if ($pages > 1) {
		echo '<div class="epc-scp-pagination">';
		for ($p = 1; $p <= min($pages, 12); $p++) {
			$qs = http_build_query(array_filter(array('q' => $search, 'tenant' => $tenantFilter, 'page' => $p)));
			$cls = $p === $page ? 'btn-primary' : 'btn-default';
			echo '<a class="btn btn-xs ' . $cls . '" href="' . epc_scp_h($selfUrl . '?' . $qs) . '">' . $p . '</a> ';
		}
		echo '</div>';
	}
	?>
</div>
</div>
