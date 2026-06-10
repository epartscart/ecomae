<?php
/**
 * Super CP — Price generation configs (markup rules for catalogue / API clients).
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

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_scp_action'])) {
	$action = (string) $_POST['epc_scp_action'];
	if ($action === 'save_price_config') {
		$res = epc_scp_price_config_save($pdo, $_POST, max(0, (int) ($_POST['id'] ?? 0)));
		if (!empty($res['ok'])) {
			$flash = 'Price config saved.';
			$flashClass = 'success';
			$editId = 0;
		} else {
			$flash = (string) ($res['message'] ?? 'Save failed');
			$flashClass = 'danger';
		}
	}
	if ($action === 'delete_price_config') {
		epc_scp_price_config_delete($pdo, max(0, (int) ($_POST['id'] ?? 0)));
		$flash = 'Price config deleted.';
		$flashClass = 'success';
		$editId = 0;
	}
}

$configs = epc_scp_price_configs_list($pdo);
$tenants = epc_scp_tenant_options($pdo);
$clientTypes = epc_scp_price_client_types();
$backend = epc_scp_backend();

if ($editId > 0) {
	foreach ($configs as $c) {
		if ((int) $c['id'] === $editId) {
			$editRow = $c;
			break;
		}
	}
}
?>
<div class="col-lg-12 epc-scp-panel epc-scp-price-configs">
<?php
epc_scp_render_hero(
	'Super CP',
	'Price generation configs',
	'Define markup rules for built-in catalogue, price lists, and API integrations — platform-wide or per tenant.',
	array(
		array('label' => 'Price lists', 'icon' => 'fa-tags', 'url' => '/' . $backend . '/shop/prices'),
		array('label' => 'Operator guide', 'icon' => 'fa-book', 'url' => epc_scp_operator_guide_url(), 'primary' => true),
	)
);
?>
<?php epc_scp_render_workspace_intro('price_configs'); ?>

<?php if ($flash !== '') { ?>
<div class="alert alert-<?php echo epc_scp_h($flashClass); ?>"><?php echo epc_scp_h($flash); ?></div>
<?php } ?>

<div class="row">
	<div class="col-md-5">
		<div class="epc-scp-form-card">
			<h4><i class="fa fa-<?php echo $editRow ? 'edit' : 'plus'; ?>"></i> <?php echo $editRow ? 'Edit config' : 'New config'; ?></h4>
			<form method="post" class="form-horizontal">
				<input type="hidden" name="epc_scp_action" value="save_price_config" />
				<input type="hidden" name="id" value="<?php echo (int) ($editRow['id'] ?? 0); ?>" />
				<div class="form-group"><label class="col-sm-4 control-label">Name</label><div class="col-sm-8"><input class="form-control input-sm" name="name" required value="<?php echo epc_scp_h($editRow['name'] ?? ''); ?>" /></div></div>
				<div class="form-group"><label class="col-sm-4 control-label">Scope</label><div class="col-sm-8">
					<select class="form-control input-sm" name="scope">
						<option value="platform"<?php echo ($editRow['scope'] ?? 'platform') === 'platform' ? ' selected' : ''; ?>>Platform default</option>
						<option value="tenant"<?php echo ($editRow['scope'] ?? '') === 'tenant' ? ' selected' : ''; ?>>Tenant override</option>
					</select>
				</div></div>
				<div class="form-group"><label class="col-sm-4 control-label">Tenant</label><div class="col-sm-8">
					<select class="form-control input-sm" name="site_key">
						<option value="">—</option>
						<?php foreach ($tenants as $t) { ?>
						<option value="<?php echo epc_scp_h($t['site_key']); ?>"<?php echo ($editRow['site_key'] ?? '') === $t['site_key'] ? ' selected' : ''; ?>><?php echo epc_scp_h($t['label']); ?></option>
						<?php } ?>
					</select>
				</div></div>
				<div class="form-group"><label class="col-sm-4 control-label">Client type</label><div class="col-sm-8">
					<select class="form-control input-sm" name="client_type">
						<?php foreach ($clientTypes as $k => $label) { ?>
						<option value="<?php echo epc_scp_h($k); ?>"<?php echo ($editRow['client_type'] ?? 'all') === $k ? ' selected' : ''; ?>><?php echo epc_scp_h($label); ?></option>
						<?php } ?>
					</select>
				</div></div>
				<div class="form-group"><label class="col-sm-4 control-label">Client ref</label><div class="col-sm-8"><input class="form-control input-sm" name="client_ref" placeholder="price list id, channel id…" value="<?php echo epc_scp_h($editRow['client_ref'] ?? ''); ?>" /></div></div>
				<div class="form-group"><label class="col-sm-4 control-label">Markup %</label><div class="col-sm-8"><input class="form-control input-sm" type="number" step="0.01" name="markup_percent" value="<?php echo epc_scp_h($editRow['markup_percent'] ?? '0'); ?>" /></div></div>
				<div class="form-group"><label class="col-sm-4 control-label">Fixed markup</label><div class="col-sm-8"><input class="form-control input-sm" type="number" step="0.0001" name="markup_fixed" value="<?php echo epc_scp_h($editRow['markup_fixed'] ?? '0'); ?>" /></div></div>
				<div class="form-group"><label class="col-sm-4 control-label">Currency</label><div class="col-sm-8"><input class="form-control input-sm" name="currency" maxlength="8" value="<?php echo epc_scp_h($editRow['currency'] ?? 'AED'); ?>" /></div></div>
				<div class="form-group"><label class="col-sm-4 control-label">Priority</label><div class="col-sm-8"><input class="form-control input-sm" type="number" name="priority" value="<?php echo epc_scp_h($editRow['priority'] ?? '100'); ?>" /></div></div>
				<div class="form-group"><label class="col-sm-4 control-label">Notes</label><div class="col-sm-8"><textarea class="form-control input-sm" name="notes" rows="2"><?php echo epc_scp_h($editRow['notes'] ?? ''); ?></textarea></div></div>
				<div class="form-group"><div class="col-sm-8 col-sm-offset-4">
					<label class="checkbox-inline"><input type="checkbox" name="active" value="1"<?php echo !isset($editRow['active']) || !empty($editRow['active']) ? ' checked' : ''; ?>> Active</label>
				</div></div>
				<div class="form-group"><div class="col-sm-8 col-sm-offset-4">
					<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
					<?php if ($editRow) { ?><a class="btn btn-default btn-sm" href="<?php echo epc_scp_h('/' . $backend . '/control/portal/epc_super_cp_price_configs'); ?>">Cancel</a><?php } ?>
				</div></div>
			</form>
		</div>
	</div>
	<div class="col-md-7">
		<div class="epc-scp-table-card">
			<h4><i class="fa fa-list"></i> Active rules (<?php echo count($configs); ?>)</h4>
			<div class="table-responsive">
				<table class="table table-striped table-bordered table-condensed epc-scp-data-table">
					<thead><tr><th>Name</th><th>Scope</th><th>Client</th><th>Markup</th><th>Priority</th><th></th></tr></thead>
					<tbody>
					<?php if (count($configs) === 0) { ?>
						<tr><td colspan="6" class="epc-scp-empty-cell">
							<?php
							epc_scp_render_empty_state(
								'No price configs yet',
								'Add a platform default markup rule, then tenant overrides where clients need different margins.',
								array(
									array('label' => 'Shop prices', 'icon' => 'fa-tags', 'url' => '/' . $backend . '/shop/prices'),
									array('label' => 'Operator guide', 'icon' => 'fa-book', 'url' => epc_scp_operator_guide_url(), 'primary' => true),
								)
							);
							?>
						</td></tr>
					<?php } ?>
					<?php foreach ($configs as $c) { ?>
						<tr class="<?php echo empty($c['active']) ? 'text-muted' : ''; ?>">
							<td><strong><?php echo epc_scp_h($c['name']); ?></strong><?php if (empty($c['active'])) { ?> <span class="label label-default">off</span><?php } ?></td>
							<td><?php echo epc_scp_h($c['scope']); ?><?php if (!empty($c['site_key'])) { ?><br><code><?php echo epc_scp_h($c['site_key']); ?></code><?php } ?></td>
							<td><?php echo epc_scp_h($clientTypes[$c['client_type']] ?? $c['client_type']); ?><?php if (!empty($c['client_ref'])) { ?><br><code><?php echo epc_scp_h($c['client_ref']); ?></code><?php } ?></td>
							<td><?php echo epc_scp_h($c['markup_percent']); ?>% + <?php echo epc_scp_h($c['markup_fixed']); ?> <?php echo epc_scp_h($c['currency']); ?></td>
							<td><?php echo (int) $c['priority']; ?></td>
							<td class="epc-scp-actions-cell">
								<a class="btn btn-xs btn-default" href="?edit=<?php echo (int) $c['id']; ?>"><i class="fa fa-edit"></i></a>
								<form method="post" style="display:inline" onsubmit="return confirm('Delete this config?');">
									<input type="hidden" name="epc_scp_action" value="delete_price_config" />
									<input type="hidden" name="id" value="<?php echo (int) $c['id']; ?>" />
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
