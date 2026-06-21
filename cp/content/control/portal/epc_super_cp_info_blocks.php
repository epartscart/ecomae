<?php
/**
 * Super CP — CMS info blocks for platform and tenant storefronts.
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

$flash = '';
$flashClass = 'info';
$editId = max(0, (int) ($_GET['edit'] ?? 0));
$editRow = null;
$filterPlacement = isset($_GET['placement']) ? (string) $_GET['placement'] : '';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_scp_action'])) {
	$action = (string) $_POST['epc_scp_action'];
	if ($action === 'save_info_block') {
		$res = epc_scp_info_block_save($pdo, $_POST, max(0, (int) ($_POST['id'] ?? 0)));
		if (!empty($res['ok'])) {
			$flash = 'Info block saved.';
			$flashClass = 'success';
			$editId = 0;
		} else {
			$flash = (string) ($res['message'] ?? 'Save failed');
			$flashClass = 'danger';
		}
	}
	if ($action === 'delete_info_block') {
		epc_scp_info_block_delete($pdo, max(0, (int) ($_POST['id'] ?? 0)));
		$flash = 'Info block deleted.';
		$flashClass = 'success';
		$editId = 0;
	}
}

$blocks = epc_scp_info_blocks_list($pdo, $filterPlacement);
$tenants = epc_scp_tenant_options($pdo);
$placements = epc_scp_info_placements();
$backend = epc_scp_backend();

if ($editId > 0) {
	foreach ($blocks as $b) {
		if ((int) $b['id'] === $editId) {
			$editRow = $b;
			break;
		}
	}
	if ($editRow === null) {
		$all = epc_scp_info_blocks_list($pdo);
		foreach ($all as $b) {
			if ((int) $b['id'] === $editId) {
				$editRow = $b;
				break;
			}
		}
	}
}
?>
<div class="col-lg-12 epc-scp-panel epc-scp-info-blocks">
<?php
epc_scp_render_hero(
	'Super CP',
	'Content & info blocks',
	'Manage CMS info blocks shown on platform marketing pages, tenant storefronts, checkout, and CP notices.',
	array(
		array('label' => 'Industry settings', 'icon' => 'fa-cog', 'url' => '/' . $backend . '/control/portal/industry_settings'),
		array('label' => 'Operator guide', 'icon' => 'fa-book', 'url' => epc_scp_operator_guide_url(), 'primary' => true),
	)
);
?>
<?php epc_scp_render_workspace_intro('info_blocks'); ?>

<?php if ($flash !== '') { ?>
<div class="alert alert-<?php echo epc_scp_h($flashClass); ?>"><?php echo epc_scp_h($flash); ?></div>
<?php } ?>

<div class="epc-scp-filter-bar">
	<form method="get" class="form-inline">
		<select name="placement" class="form-control input-sm">
			<option value="">All placements</option>
			<?php foreach ($placements as $k => $label) { ?>
			<option value="<?php echo epc_scp_h($k); ?>"<?php echo $filterPlacement === $k ? ' selected' : ''; ?>><?php echo epc_scp_h($label); ?></option>
			<?php } ?>
		</select>
		<button type="submit" class="btn btn-default btn-sm">Filter</button>
	</form>
</div>

<div class="row">
	<div class="col-md-5">
		<div class="epc-scp-form-card">
			<h4><i class="fa fa-<?php echo $editRow ? 'edit' : 'plus'; ?>"></i> <?php echo $editRow ? 'Edit block' : 'New block'; ?></h4>
			<form method="post">
				<input type="hidden" name="epc_scp_action" value="save_info_block" />
				<input type="hidden" name="id" value="<?php echo (int) ($editRow['id'] ?? 0); ?>" />
				<div class="form-group"><label>Block key</label><input class="form-control input-sm" name="block_key" required pattern="[a-z0-9_-]+" value="<?php echo epc_scp_h($editRow['block_key'] ?? ''); ?>" placeholder="summer_promo" /></div>
				<div class="form-group"><label>Title</label><input class="form-control input-sm" name="title" required value="<?php echo epc_scp_h($editRow['title'] ?? ''); ?>" /></div>
				<div class="form-group"><label>Scope</label>
					<select class="form-control input-sm" name="scope">
						<option value="platform"<?php echo ($editRow['scope'] ?? 'platform') === 'platform' ? ' selected' : ''; ?>>Platform</option>
						<option value="tenant"<?php echo ($editRow['scope'] ?? '') === 'tenant' ? ' selected' : ''; ?>>Tenant</option>
					</select>
				</div>
				<div class="form-group"><label>Tenant</label>
					<select class="form-control input-sm" name="site_key">
						<option value="">—</option>
						<?php foreach ($tenants as $t) { ?>
						<option value="<?php echo epc_scp_h($t['site_key']); ?>"<?php echo ($editRow['site_key'] ?? '') === $t['site_key'] ? ' selected' : ''; ?>><?php echo epc_scp_h($t['label']); ?></option>
						<?php } ?>
					</select>
				</div>
				<div class="form-group"><label>Placement</label>
					<select class="form-control input-sm" name="placement">
						<?php foreach ($placements as $k => $label) { ?>
						<option value="<?php echo epc_scp_h($k); ?>"<?php echo ($editRow['placement'] ?? 'homepage') === $k ? ' selected' : ''; ?>><?php echo epc_scp_h($label); ?></option>
						<?php } ?>
					</select>
				</div>
				<div class="form-group"><label>Locale</label><input class="form-control input-sm" name="locale" maxlength="8" value="<?php echo epc_scp_h($editRow['locale'] ?? 'en'); ?>" /></div>
				<div class="form-group"><label>Sort order</label><input class="form-control input-sm" type="number" name="sort_order" value="<?php echo epc_scp_h($editRow['sort_order'] ?? '0'); ?>" /></div>
				<div class="form-group"><label>HTML content</label><textarea class="form-control" name="content_html" rows="6"><?php echo epc_scp_h($editRow['content_html'] ?? ''); ?></textarea></div>
				<div class="checkbox"><label><input type="checkbox" name="active" value="1"<?php echo !isset($editRow['active']) || !empty($editRow['active']) ? ' checked' : ''; ?>> Active</label></div>
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<?php if ($editRow) { ?><a class="btn btn-default btn-sm" href="<?php echo epc_scp_h('/' . $backend . '/control/portal/epc_super_cp_info_blocks'); ?>">Cancel</a><?php } ?>
			</form>
		</div>
	</div>
	<div class="col-md-7">
		<div class="epc-scp-table-card">
			<h4><i class="fa fa-th-large"></i> Blocks (<?php echo count($blocks); ?>)</h4>
			<div class="table-responsive">
				<table class="table table-striped table-bordered table-condensed epc-scp-data-table">
					<thead><tr><th>Key / title</th><th>Placement</th><th>Scope</th><th>Locale</th><th></th></tr></thead>
					<tbody>
					<?php if (count($blocks) === 0) { ?>
						<tr><td colspan="5" class="epc-scp-empty-cell">
							<?php
							epc_scp_render_empty_state(
								'No info blocks yet',
								'Create a block for homepage, checkout, or CP notices — platform-wide or scoped to one tenant.',
								array(
									array('label' => 'Industry settings', 'icon' => 'fa-cog', 'url' => '/' . $backend . '/control/portal/industry_settings'),
									array('label' => 'Operator guide', 'icon' => 'fa-book', 'url' => epc_scp_operator_guide_url(), 'primary' => true),
								)
							);
							?>
						</td></tr>
					<?php } ?>
					<?php foreach ($blocks as $b) { ?>
						<tr class="<?php echo empty($b['active']) ? 'text-muted' : ''; ?>">
							<td>
								<strong><?php echo epc_scp_h($b['title']); ?></strong>
								<div><code><?php echo epc_scp_h($b['block_key']); ?></code><?php if (empty($b['active'])) { ?> <span class="label label-default">off</span><?php } ?></div>
							</td>
							<td><?php echo epc_scp_h($placements[$b['placement']] ?? $b['placement']); ?></td>
							<td><?php echo epc_scp_h($b['scope']); ?><?php if (!empty($b['site_key'])) { ?><br><code><?php echo epc_scp_h($b['site_key']); ?></code><?php } ?></td>
							<td><?php echo epc_scp_h($b['locale']); ?></td>
							<td class="epc-scp-actions-cell">
								<a class="btn btn-xs btn-default" href="?edit=<?php echo (int) $b['id']; ?><?php echo $filterPlacement !== '' ? '&placement=' . urlencode($filterPlacement) : ''; ?>"><i class="fa fa-edit"></i></a>
								<form method="post" style="display:inline" onsubmit="return confirm('Delete this block?');">
									<input type="hidden" name="epc_scp_action" value="delete_info_block" />
									<input type="hidden" name="id" value="<?php echo (int) $b['id']; ?>" />
									<button type="submit" class="btn btn-xs btn-danger"><i class="fa fa-trash"></i></button>
								</form>
							</td>
						</tr>
					<?php } ?>
					</tbody>
				</table>
			</div>
		</div>
	</div>
</div>
</div>
