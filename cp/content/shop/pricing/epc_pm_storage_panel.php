<?php
/**
 * CP Price Management — warehouse / supplier margin panel.
 * Levels (top → bottom): supplier overall → supplier brand → supplier article.
 *
 * Expects: $db_link, $user_session, $backend, message/error flash vars by reference via return.
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_pricing_ensure_storage_schema')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php';
}

epc_pricing_ensure_storage_schema($db_link);

/**
 * @return array{0:string,1:string} message, error
 */
function epc_pm_storage_handle_post(PDO $db_link, array $post): array
{
	$message = '';
	$error = '';
	$action = (string) ($post['action'] ?? '');
	try {
		if ($action === 'save_storage_rule') {
			$storageId = (int) ($post['storage_id'] ?? 0);
			$margin = (float) str_replace(',', '.', (string) ($post['margin_percent'] ?? '0'));
			$visible = !empty($post['visible']) ? 1 : 0;
			if ($storageId <= 0) {
				throw new Exception('Select a supplier / warehouse');
			}
			if ($margin < -100 || $margin > 1000) {
				throw new Exception('Supplier margin must be from -100 to 1000');
			}
			$stmt = $db_link->prepare(
				'INSERT INTO `epc_price_storage_rules` (`storage_id`, `margin_percent`, `visible`, `updated_at`)
				 VALUES (?, ?, ?, ?)
				 ON DUPLICATE KEY UPDATE `margin_percent` = VALUES(`margin_percent`), `visible` = VALUES(`visible`), `updated_at` = VALUES(`updated_at`)'
			);
			$stmt->execute(array($storageId, number_format($margin, 2, '.', ''), $visible, time()));
			$message = 'Supplier overall margin saved';
		} elseif ($action === 'delete_storage_rule') {
			$stmt = $db_link->prepare('DELETE FROM `epc_price_storage_rules` WHERE `id` = ?');
			$stmt->execute(array((int) ($post['rule_id'] ?? 0)));
			$message = 'Supplier overall rule deleted';
		} elseif ($action === 'save_storage_brand_rule') {
			$storageId = (int) ($post['storage_id'] ?? 0);
			$manufacturer = epc_pricing_normalize_brand($post['manufacturer'] ?? '');
			$margin = (float) str_replace(',', '.', (string) ($post['margin_percent'] ?? '0'));
			$visible = !empty($post['visible']) ? 1 : 0;
			if ($storageId <= 0 || $manufacturer === '') {
				throw new Exception('Select supplier and enter brand');
			}
			if ($margin < -100 || $margin > 1000) {
				throw new Exception('Margin must be from -100 to 1000');
			}
			$stmt = $db_link->prepare(
				'INSERT INTO `epc_price_storage_brand_rules`
					(`storage_id`, `manufacturer`, `margin_percent`, `visible`, `updated_at`)
				 VALUES (?, ?, ?, ?, ?)
				 ON DUPLICATE KEY UPDATE
					`margin_percent` = VALUES(`margin_percent`),
					`visible` = VALUES(`visible`),
					`updated_at` = VALUES(`updated_at`)'
			);
			$stmt->execute(array($storageId, $manufacturer, number_format($margin, 2, '.', ''), $visible, time()));
			$message = 'Supplier brand rule saved';
		} elseif ($action === 'delete_storage_brand_rule') {
			$stmt = $db_link->prepare('DELETE FROM `epc_price_storage_brand_rules` WHERE `id` = ?');
			$stmt->execute(array((int) ($post['rule_id'] ?? 0)));
			$message = 'Supplier brand rule deleted';
		} elseif ($action === 'save_storage_article_rule') {
			$storageId = (int) ($post['storage_id'] ?? 0);
			$manufacturer = epc_pricing_normalize_brand($post['manufacturer'] ?? '');
			$article = epc_pricing_normalize_article($post['article'] ?? '');
			$margin = (float) str_replace(',', '.', (string) ($post['margin_percent'] ?? '0'));
			$visible = !empty($post['visible']) ? 1 : 0;
			if ($storageId <= 0 || $manufacturer === '' || $article === '') {
				throw new Exception('Select supplier, brand, and article');
			}
			if ($margin < -100 || $margin > 1000) {
				throw new Exception('Margin must be from -100 to 1000');
			}
			$stmt = $db_link->prepare(
				'INSERT INTO `epc_price_storage_article_rules`
					(`storage_id`, `manufacturer`, `article`, `margin_percent`, `visible`, `updated_at`)
				 VALUES (?, ?, ?, ?, ?, ?)
				 ON DUPLICATE KEY UPDATE
					`margin_percent` = VALUES(`margin_percent`),
					`visible` = VALUES(`visible`),
					`updated_at` = VALUES(`updated_at`)'
			);
			$stmt->execute(array(
				$storageId,
				$manufacturer,
				$article,
				number_format($margin, 2, '.', ''),
				$visible,
				time(),
			));
			$message = 'Supplier article rule saved';
		} elseif ($action === 'delete_storage_article_rule') {
			$stmt = $db_link->prepare('DELETE FROM `epc_price_storage_article_rules` WHERE `id` = ?');
			$stmt->execute(array((int) ($post['rule_id'] ?? 0)));
			$message = 'Supplier article rule deleted';
		}
	} catch (Exception $e) {
		$error = $e->getMessage();
	}
	return array($message, $error);
}

/**
 * @return list<array<string,mixed>>
 */
function epc_pm_storage_list(PDO $db_link): array
{
	$rows = array();
	try {
		$q = $db_link->query(
			"SELECT `id`, `name`, `short_name`, `interface_type`, `hidden`
			 FROM `shop_storages`
			 ORDER BY COALESCE(NULLIF(TRIM(`short_name`), ''), `name`) ASC"
		);
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$label = trim((string) ($row['short_name'] !== '' ? $row['short_name'] : $row['name']));
			$rows[] = array(
				'id' => (int) $row['id'],
				'name' => $label !== '' ? $label : ('Warehouse #' . (int) $row['id']),
				'hidden' => !empty($row['hidden']),
				'interface_type' => (int) ($row['interface_type'] ?? 0),
			);
		}
	} catch (Exception $e) {
	}
	return $rows;
}

/**
 * @return array{overall:list,brand:list,article:list,counts:array}
 */
function epc_pm_storage_rules_bundle(PDO $db_link): array
{
	$overall = array();
	$brand = array();
	$article = array();
	try {
		$q = $db_link->query(
			"SELECT r.*, COALESCE(NULLIF(TRIM(s.`short_name`), ''), s.`name`) AS storage_name
			 FROM `epc_price_storage_rules` r
			 LEFT JOIN `shop_storages` s ON s.`id` = r.`storage_id`
			 ORDER BY storage_name ASC"
		);
		$overall = $q ? $q->fetchAll(PDO::FETCH_ASSOC) : array();
	} catch (Exception $e) {
	}
	try {
		$q = $db_link->query(
			"SELECT r.*, COALESCE(NULLIF(TRIM(s.`short_name`), ''), s.`name`) AS storage_name
			 FROM `epc_price_storage_brand_rules` r
			 LEFT JOIN `shop_storages` s ON s.`id` = r.`storage_id`
			 ORDER BY storage_name ASC, r.`manufacturer` ASC"
		);
		$brand = $q ? $q->fetchAll(PDO::FETCH_ASSOC) : array();
	} catch (Exception $e) {
	}
	try {
		$q = $db_link->query(
			"SELECT r.*, COALESCE(NULLIF(TRIM(s.`short_name`), ''), s.`name`) AS storage_name
			 FROM `epc_price_storage_article_rules` r
			 LEFT JOIN `shop_storages` s ON s.`id` = r.`storage_id`
			 ORDER BY storage_name ASC, r.`manufacturer` ASC, r.`article` ASC"
		);
		$article = $q ? $q->fetchAll(PDO::FETCH_ASSOC) : array();
	} catch (Exception $e) {
	}
	return array(
		'overall' => $overall,
		'brand' => $brand,
		'article' => $article,
		'counts' => array(
			'overall' => count($overall),
			'brand' => count($brand),
			'article' => count($article),
		),
	);
}

/**
 * Render the warehouse pricing section HTML.
 *
 * @param list<array<string,mixed>> $storages
 * @param array{overall:list,brand:list,article:list,counts:array} $bundle
 */
function epc_pm_storage_render_section(array $storages, array $bundle, string $csrf, string $backend): void
{
	$h = 'epc_pm_h';
	if (!function_exists($h)) {
		$h = static function ($v) {
			return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
		};
	}
	$counts = $bundle['counts'];
	?>
<style>
.epc-pm-wh-levels {
	display: grid;
	grid-template-columns: repeat(3, minmax(0, 1fr));
	gap: 10px;
	margin: 0 0 18px;
}
@media (max-width: 900px) {
	.epc-pm-wh-levels { grid-template-columns: 1fr; }
}
.epc-pm-wh-level {
	border: 1px solid #e2e8f0;
	border-radius: 12px;
	padding: 12px 14px;
	background: linear-gradient(180deg, #f8fafc 0%, #fff 70%);
}
.epc-pm-wh-level strong {
	display: block;
	font-size: 13px;
	color: #0f172a;
	margin-bottom: 4px;
}
.epc-pm-wh-level span {
	font-size: 12px;
	color: #64748b;
	line-height: 1.4;
}
.epc-pm-wh-level em {
	display: inline-block;
	margin-top: 6px;
	font-style: normal;
	font-size: 11px;
	font-weight: 800;
	color: #0f766e;
	background: #ccfbf1;
	border-radius: 999px;
	padding: 2px 8px;
}
.epc-pm-wh-tabs {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
	margin-bottom: 14px;
}
.epc-pm-wh-tabs button {
	border: 1px solid #cbd5e1;
	background: #fff;
	border-radius: 999px;
	padding: 7px 12px;
	font-size: 12px;
	font-weight: 700;
	cursor: pointer;
	color: #334155;
}
.epc-pm-wh-tabs button.is-active {
	background: #0f766e;
	border-color: #0f766e;
	color: #fff;
}
.epc-pm-wh-pane[hidden] { display: none !important; }
.epc-pm-wh-badge {
	display: inline-block;
	min-width: 1.5em;
	padding: 1px 6px;
	border-radius: 999px;
	background: rgba(15, 23, 42, .08);
	font-size: 11px;
	margin-left: 4px;
}
.epc-pm-wh-tabs button.is-active .epc-pm-wh-badge {
	background: rgba(255,255,255,.22);
}
</style>

<div class="epc-pm-section" id="epc-pm-step-wh">
	<div class="epc-pm-section-head">
		<div class="epc-pm-section-num">W</div>
		<div>
			<h2>Warehouse / supplier pricing</h2>
			<p>
				Apply margins by <strong>supplier</strong>, then refine by <strong>brand of that supplier</strong>,
				then by <strong>article of that supplier</strong>. These run <em>before</em> customer profile rules.
			</p>
		</div>
	</div>
	<div class="epc-pm-section-body">
		<div class="epc-pm-callout" style="border-left-color:#0f766e;background:#f0fdfa;margin-top:0;">
			<strong>Stack order:</strong>
			Purchase → <em>Supplier %</em> → <em>Supplier brand %</em> → <em>Supplier article %</em>
			→ Profile % → Profile brand % → Profile article % → Guest %
		</div>

		<div class="epc-pm-wh-levels">
			<div class="epc-pm-wh-level">
				<strong>1. By supplier</strong>
				<span>One overall margin for every part from that warehouse / supplier.</span>
				<em><?= (int) $counts['overall']; ?> rule(s)</em>
			</div>
			<div class="epc-pm-wh-level">
				<strong>2. By brand of supplier</strong>
				<span>Extra (or different) margin for a manufacturer inside one supplier only.</span>
				<em><?= (int) $counts['brand']; ?> rule(s)</em>
			</div>
			<div class="epc-pm-wh-level">
				<strong>3. By article of supplier</strong>
				<span>Most specific — one article number inside one supplier.</span>
				<em><?= (int) $counts['article']; ?> rule(s)</em>
			</div>
		</div>

		<?php if (!$storages) { ?>
			<p class="text-muted" style="margin:0;">No warehouses found. Add suppliers under
				<a href="/<?= $h($backend); ?>/shop/logistics/storages">Logistics → Storages</a> first.</p>
		<?php } else { ?>
		<div class="epc-pm-wh-tabs" role="tablist" aria-label="Supplier pricing levels">
			<button type="button" class="is-active" data-wh-tab="overall">Supplier overall <span class="epc-pm-wh-badge"><?= (int) $counts['overall']; ?></span></button>
			<button type="button" data-wh-tab="brand">Supplier + brand <span class="epc-pm-wh-badge"><?= (int) $counts['brand']; ?></span></button>
			<button type="button" data-wh-tab="article">Supplier + article <span class="epc-pm-wh-badge"><?= (int) $counts['article']; ?></span></button>
		</div>

		<!-- Overall -->
		<div class="epc-pm-wh-pane" data-wh-pane="overall">
			<form method="post" class="epc-pm-form-grid">
				<input type="hidden" name="csrf_guard_key" value="<?= $h($csrf); ?>" />
				<input type="hidden" name="action" value="save_storage_rule" />
				<div>
					<label>Supplier / warehouse</label>
					<select class="form-control" name="storage_id" required>
						<option value="">Select…</option>
						<?php foreach ($storages as $st) { ?>
						<option value="<?= (int) $st['id']; ?>"><?= $h($st['name']); ?><?= !empty($st['hidden']) ? ' (hidden)' : ''; ?></option>
						<?php } ?>
					</select>
				</div>
				<div>
					<label>Overall margin %</label>
					<input class="form-control" type="number" step="0.01" name="margin_percent" value="10" required />
				</div>
				<div>
					<label>Visibility</label>
					<select class="form-control" name="visible">
						<option value="1">Show offers from this supplier</option>
						<option value="0">Hide entire supplier</option>
					</select>
				</div>
				<div class="epc-pm-form-actions" style="grid-column:1/-1;border:none;padding-top:0;">
					<button class="btn btn-primary" type="submit"><i class="fa fa-save"></i> Save supplier margin</button>
				</div>
			</form>
			<?php if ($bundle['overall']) { ?>
			<div class="table-responsive" style="margin-top:16px;">
				<table class="table table-striped table-condensed">
					<thead><tr><th>Supplier</th><th>Margin %</th><th>Visible</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($bundle['overall'] as $row) { ?>
						<tr>
							<td><?= $h($row['storage_name'] ?: ('#' . $row['storage_id'])); ?></td>
							<td><?= $h(number_format((float) $row['margin_percent'], 2, '.', '')); ?>%</td>
							<td><?= !empty($row['visible']) ? 'Show' : 'Hide'; ?></td>
							<td>
								<form method="post" style="display:inline;" onsubmit="return confirm('Delete this supplier rule?');">
									<input type="hidden" name="csrf_guard_key" value="<?= $h($csrf); ?>" />
									<input type="hidden" name="action" value="delete_storage_rule" />
									<input type="hidden" name="rule_id" value="<?= (int) $row['id']; ?>" />
									<button class="btn btn-xs btn-default" type="submit">Delete</button>
								</form>
							</td>
						</tr>
					<?php } ?>
					</tbody>
				</table>
			</div>
			<?php } else { ?>
			<p class="text-muted" style="margin-top:14px;margin-bottom:0;">No supplier overall rules yet.</p>
			<?php } ?>
		</div>

		<!-- Brand -->
		<div class="epc-pm-wh-pane" data-wh-pane="brand" hidden>
			<form method="post" class="epc-pm-form-grid">
				<input type="hidden" name="csrf_guard_key" value="<?= $h($csrf); ?>" />
				<input type="hidden" name="action" value="save_storage_brand_rule" />
				<div>
					<label>Supplier / warehouse</label>
					<select class="form-control" name="storage_id" required>
						<option value="">Select…</option>
						<?php foreach ($storages as $st) { ?>
						<option value="<?= (int) $st['id']; ?>"><?= $h($st['name']); ?></option>
						<?php } ?>
					</select>
				</div>
				<div>
					<label>Brand (manufacturer)</label>
					<input class="form-control" name="manufacturer" placeholder="e.g. TOYOTA" required />
				</div>
				<div>
					<label>Extra margin %</label>
					<input class="form-control" type="number" step="0.01" name="margin_percent" value="5" required />
				</div>
				<div>
					<label>Visibility</label>
					<select class="form-control" name="visible">
						<option value="1">Show</option>
						<option value="0">Hide this brand from supplier</option>
					</select>
				</div>
				<div class="epc-pm-form-actions" style="grid-column:1/-1;border:none;padding-top:0;">
					<button class="btn btn-primary" type="submit"><i class="fa fa-save"></i> Save supplier brand rule</button>
				</div>
			</form>
			<?php if ($bundle['brand']) { ?>
			<div class="table-responsive" style="margin-top:16px;">
				<table class="table table-striped table-condensed">
					<thead><tr><th>Supplier</th><th>Brand</th><th>Margin %</th><th>Visible</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($bundle['brand'] as $row) { ?>
						<tr>
							<td><?= $h($row['storage_name'] ?: ('#' . $row['storage_id'])); ?></td>
							<td><?= $h($row['manufacturer']); ?></td>
							<td><?= $h(number_format((float) $row['margin_percent'], 2, '.', '')); ?>%</td>
							<td><?= !empty($row['visible']) ? 'Show' : 'Hide'; ?></td>
							<td>
								<form method="post" style="display:inline;" onsubmit="return confirm('Delete this brand rule?');">
									<input type="hidden" name="csrf_guard_key" value="<?= $h($csrf); ?>" />
									<input type="hidden" name="action" value="delete_storage_brand_rule" />
									<input type="hidden" name="rule_id" value="<?= (int) $row['id']; ?>" />
									<button class="btn btn-xs btn-default" type="submit">Delete</button>
								</form>
							</td>
						</tr>
					<?php } ?>
					</tbody>
				</table>
			</div>
			<?php } else { ?>
			<p class="text-muted" style="margin-top:14px;margin-bottom:0;">No supplier brand rules yet.</p>
			<?php } ?>
		</div>

		<!-- Article -->
		<div class="epc-pm-wh-pane" data-wh-pane="article" hidden>
			<form method="post" class="epc-pm-form-grid">
				<input type="hidden" name="csrf_guard_key" value="<?= $h($csrf); ?>" />
				<input type="hidden" name="action" value="save_storage_article_rule" />
				<div>
					<label>Supplier / warehouse</label>
					<select class="form-control" name="storage_id" required>
						<option value="">Select…</option>
						<?php foreach ($storages as $st) { ?>
						<option value="<?= (int) $st['id']; ?>"><?= $h($st['name']); ?></option>
						<?php } ?>
					</select>
				</div>
				<div>
					<label>Brand</label>
					<input class="form-control" name="manufacturer" placeholder="e.g. AISIN" required />
				</div>
				<div>
					<label>Article number</label>
					<input class="form-control" name="article" placeholder="e.g. CMT033" required />
				</div>
				<div>
					<label>Extra margin %</label>
					<input class="form-control" type="number" step="0.01" name="margin_percent" value="2" required />
				</div>
				<div>
					<label>Visibility</label>
					<select class="form-control" name="visible">
						<option value="1">Show</option>
						<option value="0">Hide this article from supplier</option>
					</select>
				</div>
				<div class="epc-pm-form-actions" style="grid-column:1/-1;border:none;padding-top:0;">
					<button class="btn btn-primary" type="submit"><i class="fa fa-save"></i> Save supplier article rule</button>
				</div>
			</form>
			<?php if ($bundle['article']) { ?>
			<div class="table-responsive" style="margin-top:16px;">
				<table class="table table-striped table-condensed">
					<thead><tr><th>Supplier</th><th>Brand</th><th>Article</th><th>Margin %</th><th>Visible</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($bundle['article'] as $row) { ?>
						<tr>
							<td><?= $h($row['storage_name'] ?: ('#' . $row['storage_id'])); ?></td>
							<td><?= $h($row['manufacturer']); ?></td>
							<td><?= $h($row['article']); ?></td>
							<td><?= $h(number_format((float) $row['margin_percent'], 2, '.', '')); ?>%</td>
							<td><?= !empty($row['visible']) ? 'Show' : 'Hide'; ?></td>
							<td>
								<form method="post" style="display:inline;" onsubmit="return confirm('Delete this article rule?');">
									<input type="hidden" name="csrf_guard_key" value="<?= $h($csrf); ?>" />
									<input type="hidden" name="action" value="delete_storage_article_rule" />
									<input type="hidden" name="rule_id" value="<?= (int) $row['id']; ?>" />
									<button class="btn btn-xs btn-default" type="submit">Delete</button>
								</form>
							</td>
						</tr>
					<?php } ?>
					</tbody>
				</table>
			</div>
			<?php } else { ?>
			<p class="text-muted" style="margin-top:14px;margin-bottom:0;">No supplier article rules yet.</p>
			<?php } ?>
		</div>
		<?php } ?>
	</div>
</div>
<script>
(function () {
	var root = document.getElementById('epc-pm-step-wh');
	if (!root) return;
	var tabs = root.querySelectorAll('[data-wh-tab]');
	var panes = root.querySelectorAll('[data-wh-pane]');
	function show(name) {
		Array.prototype.forEach.call(tabs, function (btn) {
			btn.classList.toggle('is-active', btn.getAttribute('data-wh-tab') === name);
		});
		Array.prototype.forEach.call(panes, function (pane) {
			pane.hidden = pane.getAttribute('data-wh-pane') !== name;
		});
	}
	Array.prototype.forEach.call(tabs, function (btn) {
		btn.addEventListener('click', function () { show(btn.getAttribute('data-wh-tab') || 'overall'); });
	});
	var hash = (window.location.hash || '').replace(/^#/, '');
	if (hash === 'epc-pm-wh-brand') show('brand');
	else if (hash === 'epc-pm-wh-article') show('article');
})();
</script>
	<?php
}
