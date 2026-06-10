<?php
/**
 * Super CP — Catalog & Price PRO API client keys (create / revoke / scopes / quotas).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_api_clients.php';

function epc_acm_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
	echo '<div class="alert alert-warning">API client provisioning is available on <strong>www.ecomae.com</strong> Super CP only.</div>';
	return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	global $DP_Config;
	echo '<div class="alert alert-warning">Please <a href="/' . epc_acm_h((string) $DP_Config->backend_dir) . '/">log in to Super CP</a>.</div>';
	return;
}

$pdo = epc_api_clients_platform_pdo();
if (!$pdo instanceof PDO) {
	echo '<div class="alert alert-danger">Platform database unavailable.</div>';
	return;
}

epc_api_clients_ensure_table($pdo);

$flash = null;
$newKeyPlain = null;
$backend = (string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp');
$token = 'epartscart-deploy-2026';
$catalogActions = epc_api_clients_catalog_actions();

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
	$action = (string) ($_POST['epc_acm_action'] ?? '');
	$id = (int) ($_POST['client_id'] ?? 0);

	if ($action === 'create') {
		$product = (string) ($_POST['product'] ?? 'catalog');
		if (!in_array($product, array('catalog', 'price_pro', 'both'), true)) {
			$product = 'catalog';
		}
		$label = trim((string) ($_POST['label'] ?? ''));
		$email = trim((string) ($_POST['contact_email'] ?? ''));
		$dailyLimit = max(1, min(1000000, (int) ($_POST['daily_limit'] ?? 1000)));
		$selected = isset($_POST['allowed_actions']) && is_array($_POST['allowed_actions'])
			? array_values(array_intersect($catalogActions, array_map('strval', $_POST['allowed_actions'])))
			: array();
		$actionsJson = ($product === 'price_pro' || $selected === array()) ? '[]' : json_encode($selected, JSON_UNESCAPED_UNICODE);

		if ($label === '') {
			$flash = array('ok' => false, 'message' => 'Label is required.');
		} else {
			$plain = epc_api_clients_make_key($product === 'price_pro' ? 'price_pro' : 'catalog');
			$hash = hash('sha256', $plain);
			$prefix = substr($plain, 0, 24);
			$now = time();
			$pdo->prepare(
				'INSERT INTO `epc_api_clients` (`client_key_hash`, `client_key_prefix`, `product`, `label`, `contact_email`,
				 `active`, `daily_limit`, `calls_today`, `calls_reset_date`, `allowed_actions_json`, `time_created`, `time_updated`)
				 VALUES (?, ?, ?, ?, ?, 1, ?, 0, CURDATE(), ?, ?, ?)'
			)->execute(array($hash, $prefix, $product, $label, $email, $dailyLimit, $actionsJson, $now, $now));
			$newKeyPlain = $plain;
			$flash = array('ok' => true, 'message' => 'Client created. Copy the key below — it will not be shown again.');
		}
	} elseif ($action === 'revoke' && $id > 0) {
		$pdo->prepare('UPDATE `epc_api_clients` SET `active` = 0, `time_updated` = ? WHERE `id` = ?')->execute(array(time(), $id));
		$flash = array('ok' => true, 'message' => 'Client revoked (active = 0).');
	} elseif ($action === 'activate' && $id > 0) {
		$pdo->prepare('UPDATE `epc_api_clients` SET `active` = 1, `time_updated` = ? WHERE `id` = ?')->execute(array(time(), $id));
		$flash = array('ok' => true, 'message' => 'Client re-activated.');
	} elseif ($action === 'rotate' && $id > 0) {
		$row = $pdo->prepare('SELECT `product` FROM `epc_api_clients` WHERE `id` = ? LIMIT 1');
		$row->execute(array($id));
		$product = (string) $row->fetchColumn();
		if ($product === 'both') {
			$product = 'catalog';
		}
		if (!in_array($product, array('catalog', 'price_pro'), true)) {
			$product = 'catalog';
		}
		$plain = epc_api_clients_make_key($product);
		$pdo->prepare(
			'UPDATE `epc_api_clients` SET `client_key_hash` = ?, `client_key_prefix` = ?, `time_updated` = ? WHERE `id` = ?'
		)->execute(array(hash('sha256', $plain), substr($plain, 0, 24), time(), $id));
		$newKeyPlain = $plain;
		$flash = array('ok' => true, 'message' => 'Key rotated. Copy the new key below — it will not be shown again.');
	} elseif ($action === 'update' && $id > 0) {
		$dailyLimit = max(1, min(1000000, (int) ($_POST['daily_limit'] ?? 1000)));
		$label = trim((string) ($_POST['label'] ?? ''));
		$email = trim((string) ($_POST['contact_email'] ?? ''));
		$product = (string) ($_POST['product'] ?? 'catalog');
		if (!in_array($product, array('catalog', 'price_pro', 'both'), true)) {
			$product = 'catalog';
		}
		$selected = isset($_POST['allowed_actions']) && is_array($_POST['allowed_actions'])
			? array_values(array_intersect($catalogActions, array_map('strval', $_POST['allowed_actions'])))
			: array();
		$actionsJson = ($product === 'price_pro' || $selected === array()) ? '[]' : json_encode($selected, JSON_UNESCAPED_UNICODE);
		$pdo->prepare(
			'UPDATE `epc_api_clients` SET `label` = ?, `contact_email` = ?, `product` = ?, `daily_limit` = ?,
			 `allowed_actions_json` = ?, `time_updated` = ? WHERE `id` = ?'
		)->execute(array($label, $email, $product, $dailyLimit, $actionsJson, time(), $id));
		$flash = array('ok' => true, 'message' => 'Client updated.');
	} elseif ($action === 'reset_quota' && $id > 0) {
		$pdo->prepare(
			'UPDATE `epc_api_clients` SET `calls_today` = 0, `calls_reset_date` = CURDATE(), `time_updated` = ? WHERE `id` = ?'
		)->execute(array(time(), $id));
		$flash = array('ok' => true, 'message' => 'Daily quota counter reset.');
	}
}

$clients = $pdo->query(
	'SELECT * FROM `epc_api_clients` ORDER BY `active` DESC, `id` DESC'
)->fetchAll(PDO::FETCH_ASSOC);
$activeCount = 0;
foreach ($clients as $c) {
	if (!empty($c['active'])) {
		$activeCount++;
	}
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array('class' => 'epc-portal-settings'));
?>

<div class="epc-portal-settings">
	<div class="hpanel">
		<div class="panel-body">
			<div class="epc-acm-hero">
				<h3><i class="fa fa-key"></i> Catalog &amp; Price PRO API clients</h3>
				<p style="margin:0;opacity:.92">Issue <code>X-API-Key</code> credentials for external integrations. Catalog keys authenticate via <code>/api/v1/catalog.php</code>; Price PRO via <code>/api/v1/price/lookup.php</code>. Keys are SHA-256 hashed — plain text shown once at create/rotate.</p>
			</div>

			<div class="row" style="margin-bottom:16px">
				<div class="col-sm-3"><div class="well well-sm text-center"><div class="text-muted small">Active clients</div><strong><?php echo (int) $activeCount; ?></strong></div></div>
				<div class="col-sm-3"><div class="well well-sm text-center"><div class="text-muted small">Total rows</div><strong><?php echo count($clients); ?></strong></div></div>
				<div class="col-sm-6"><div class="well well-sm"><div class="text-muted small">Marketing docs</div><a href="https://www.ecomae.com/platform/api-services" target="_blank" rel="noopener">/platform/api-services</a></div></div>
			</div>

			<?php if ($flash !== null): ?>
			<div class="alert alert-<?php echo !empty($flash['ok']) ? 'success' : 'danger'; ?>">
				<?php echo epc_acm_h($flash['message'] ?? ''); ?>
				<?php if ($newKeyPlain !== null): ?>
				<div class="epc-acm-key"><?php echo epc_acm_h($newKeyPlain); ?></div>
				<p class="text-muted" style="margin:0;font-size:12px">Store in a password manager. Never paste into tickets or marketing HTML.</p>
				<?php endif; ?>
			</div>
			<?php endif; ?>

			<p>
				<a class="btn btn-default btn-sm" href="/<?php echo epc_acm_h($backend); ?>/control/portal/epc_api_documentation_guide"><i class="fa fa-code"></i> Tenant ERP API guide</a>
				<a class="btn btn-default btn-sm" href="https://www.ecomae.com/epc-api-clients-setup.php?token=<?php echo epc_acm_h($token); ?>" target="_blank" rel="noopener"><i class="fa fa-flask"></i> Sandbox setup script</a>
			</p>

			<div class="hpanel">
				<div class="panel-heading"><h4>Create client</h4></div>
				<div class="panel-body">
					<form method="post" class="form-horizontal">
						<input type="hidden" name="epc_acm_action" value="create">
						<div class="form-group">
							<label class="col-sm-2 control-label">Product</label>
							<div class="col-sm-4">
								<select name="product" class="form-control">
									<option value="catalog">Catalog API</option>
									<option value="price_pro">Price PRO</option>
									<option value="both">Both (catalog + price)</option>
								</select>
							</div>
						</div>
						<div class="form-group">
							<label class="col-sm-2 control-label">Label</label>
							<div class="col-sm-6">
								<input type="text" name="label" class="form-control" required placeholder="Acme Garage Portal">
							</div>
						</div>
						<div class="form-group">
							<label class="col-sm-2 control-label">Contact email</label>
							<div class="col-sm-6">
								<input type="email" name="contact_email" class="form-control" placeholder="dev@partner.com">
							</div>
						</div>
						<div class="form-group">
							<label class="col-sm-2 control-label">Daily limit</label>
							<div class="col-sm-3">
								<input type="number" name="daily_limit" class="form-control" value="1000" min="1" max="1000000">
							</div>
						</div>
						<div class="form-group">
							<label class="col-sm-2 control-label">Catalog scopes</label>
							<div class="col-sm-10">
								<p class="text-muted" style="font-size:12px;margin-bottom:8px">Leave all unchecked for full catalog access. Price PRO ignores action scopes.</p>
								<div class="epc-acm-actions-grid">
									<?php foreach ($catalogActions as $act): ?>
									<label><input type="checkbox" name="allowed_actions[]" value="<?php echo epc_acm_h($act); ?>"> <?php echo epc_acm_h($act); ?></label>
									<?php endforeach; ?>
								</div>
							</div>
						</div>
						<div class="form-group">
							<div class="col-sm-offset-2 col-sm-10">
								<button type="submit" class="btn btn-primary"><i class="fa fa-plus"></i> Create &amp; show key</button>
							</div>
						</div>
					</form>
				</div>
			</div>

			<div class="table-responsive">
				<table class="table table-striped table-bordered">
					<thead>
						<tr>
							<th>ID</th>
							<th>Label</th>
							<th>Product</th>
							<th>Key prefix</th>
							<th>Quota</th>
							<th>Scopes</th>
							<th>Status</th>
							<th>Actions</th>
						</tr>
					</thead>
					<tbody>
					<?php if ($clients === array()): ?>
						<tr><td colspan="8" class="text-muted">No API clients yet. Create one above or run the sandbox setup script.</td></tr>
					<?php else: foreach ($clients as $c):
						$cid = (int) $c['id'];
						$limit = max(1, (int) ($c['daily_limit'] ?? 1000));
						$used = (int) ($c['calls_today'] ?? 0);
						$allowed = epc_api_clients_parse_allowed_actions((string) ($c['allowed_actions_json'] ?? ''));
						$scopeLabel = $allowed ? implode(', ', $allowed) : 'all';
						$rowClass = empty($c['active']) ? 'text-muted' : '';
						?>
						<tr class="<?php echo $rowClass; ?>">
							<td><?php echo $cid; ?></td>
							<td>
								<strong><?php echo epc_acm_h($c['label'] ?? ''); ?></strong>
								<?php if (!empty($c['contact_email'])): ?><br><small><a href="mailto:<?php echo epc_acm_h($c['contact_email']); ?>"><?php echo epc_acm_h($c['contact_email']); ?></a></small><?php endif; ?>
							</td>
							<td><code><?php echo epc_acm_h($c['product'] ?? ''); ?></code></td>
							<td><code><?php echo epc_acm_h($c['client_key_prefix'] ?? ''); ?>…</code></td>
							<td><?php echo $used; ?> / <?php echo $limit; ?></td>
							<td style="font-size:11px;max-width:180px"><?php echo epc_acm_h($scopeLabel); ?></td>
							<td><?php echo !empty($c['active']) ? '<span class="label label-success">active</span>' : '<span class="label label-default">revoked</span>'; ?></td>
							<td style="white-space:nowrap">
								<button type="button" class="btn btn-xs btn-default" data-toggle="collapse" data-target="#acm-edit-<?php echo $cid; ?>">Edit</button>
								<form method="post" style="display:inline" onsubmit="return confirm('Rotate key? Old key stops working immediately.')">
									<input type="hidden" name="epc_acm_action" value="rotate">
									<input type="hidden" name="client_id" value="<?php echo $cid; ?>">
									<button type="submit" class="btn btn-xs btn-warning">Rotate</button>
								</form>
								<form method="post" style="display:inline">
									<input type="hidden" name="epc_acm_action" value="reset_quota">
									<input type="hidden" name="client_id" value="<?php echo $cid; ?>">
									<button type="submit" class="btn btn-xs btn-default">Reset quota</button>
								</form>
								<?php if (!empty($c['active'])): ?>
								<form method="post" style="display:inline" onsubmit="return confirm('Revoke this client?')">
									<input type="hidden" name="epc_acm_action" value="revoke">
									<input type="hidden" name="client_id" value="<?php echo $cid; ?>">
									<button type="submit" class="btn btn-xs btn-danger">Revoke</button>
								</form>
								<?php else: ?>
								<form method="post" style="display:inline">
									<input type="hidden" name="epc_acm_action" value="activate">
									<input type="hidden" name="client_id" value="<?php echo $cid; ?>">
									<button type="submit" class="btn btn-xs btn-success">Activate</button>
								</form>
								<?php endif; ?>
							</td>
						</tr>
						<tr class="collapse" id="acm-edit-<?php echo $cid; ?>">
							<td colspan="8" style="background:#f8fafc">
								<form method="post" class="form-inline" style="padding:10px">
									<input type="hidden" name="epc_acm_action" value="update">
									<input type="hidden" name="client_id" value="<?php echo $cid; ?>">
									<input type="text" name="label" class="form-control input-sm" value="<?php echo epc_acm_h($c['label'] ?? ''); ?>" placeholder="Label" style="width:160px">
									<input type="email" name="contact_email" class="form-control input-sm" value="<?php echo epc_acm_h($c['contact_email'] ?? ''); ?>" placeholder="Email" style="width:180px">
									<select name="product" class="form-control input-sm">
										<option value="catalog"<?php echo ($c['product'] ?? '') === 'catalog' ? ' selected' : ''; ?>>catalog</option>
										<option value="price_pro"<?php echo ($c['product'] ?? '') === 'price_pro' ? ' selected' : ''; ?>>price_pro</option>
										<option value="both"<?php echo ($c['product'] ?? '') === 'both' ? ' selected' : ''; ?>>both</option>
									</select>
									<input type="number" name="daily_limit" class="form-control input-sm" value="<?php echo (int) ($c['daily_limit'] ?? 1000); ?>" min="1" style="width:90px" title="Daily limit">
									<?php foreach ($catalogActions as $act): ?>
									<label style="font-weight:normal;margin:0 8px 0 0;font-size:11px">
										<input type="checkbox" name="allowed_actions[]" value="<?php echo epc_acm_h($act); ?>"<?php echo ($allowed && in_array($act, $allowed, true)) ? ' checked' : ''; ?>> <?php echo epc_acm_h($act); ?>
									</label>
									<?php endforeach; ?>
									<button type="submit" class="btn btn-primary btn-sm">Save</button>
								</form>
							</td>
						</tr>
					<?php endforeach; endif; ?>
					</tbody>
				</table>
			</div>

			<div class="hpanel">
				<div class="panel-heading"><h4>Test with curl</h4></div>
				<div class="panel-body">
					<pre style="background:#0f172a;color:#e2e8f0;padding:12px;border-radius:8px;font-size:12px;overflow-x:auto"># Catalog — manufacturers
curl -s -H "X-API-Key: epc_catalog_YOUR_KEY" \
  "https://www.ecomae.com/api/v1/catalog.php?action=manufacturers&amp;section=passenger"

# Price PRO — article lookup
curl -s -H "X-API-Key: epc_pricepro_YOUR_KEY" \
  "https://www.ecomae.com/api/v1/price/lookup.php?brand=BOSCH&amp;article=0986424590"

# Without key (expect 401)
curl -s "https://www.ecomae.com/api/v1/catalog.php?action=status"</pre>
				</div>
			</div>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
