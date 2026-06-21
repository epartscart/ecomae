<?php
/**
 * Tenant CP panel: temporary storefront ON/OFF for warehouses and price lists.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_storage_flags.php';
epc_ssf_ensure_schema($db_link);
$epc_ssf_rows = epc_ssf_cp_list_rows($db_link);

$epc_ssf_audit = array();
try {
	$aq = $db_link->query(
		'SELECT `entity_type`, `entity_id`, `entity_name`, `storefront_disabled`, `user_label`, `created_at`
		 FROM `epc_storefront_storage_toggle_audit`
		 ORDER BY `id` DESC LIMIT 8;'
	);
	$epc_ssf_audit = $aq->fetchAll(PDO::FETCH_ASSOC) ?: array();
} catch (Throwable $e) {
	$epc_ssf_audit = array();
}
?>
<div class="col-lg-12">
	<div class="hpanel epc-storefront-storage-panel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-store"></i> Storefront availability — warehouses &amp; price lists
		</div>
		<div class="panel-body">
			<p class="alert alert-warning" style="margin-bottom:12px;">
				<strong>Temporary disable for storefront</strong> — hides a warehouse or price list from part search and pricing on the public site.
				CP price management is unchanged; data is not deleted. Set the switch, then click <strong>Save</strong> — changes apply to the public storefront immediately after save.
			</p>
			<div class="table-responsive">
				<table class="table table-striped table-hover" id="epc_storefront_storage_table">
					<thead>
						<tr>
							<th>ID</th>
							<th>Name</th>
							<th>Type</th>
							<th class="text-center">Storefront status</th>
							<th class="text-center">Temporary disable for storefront</th>
							<th class="text-center">Save</th>
						</tr>
					</thead>
					<tbody>
					<?php foreach ($epc_ssf_rows as $epc_row): ?>
						<?php
						$epc_disabled = !empty($epc_row['storefront_disabled']);
						$epc_type = (string) ($epc_row['entity_type'] ?? 'storage');
						$epc_id = (int) ($epc_row['entity_id'] ?? 0);
						$epc_name = htmlspecialchars((string) ($epc_row['name'] ?? ''), ENT_QUOTES, 'UTF-8');
						$epc_short = trim((string) ($epc_row['short_name'] ?? ''));
						if ($epc_short !== '' && strcasecmp($epc_short, (string) ($epc_row['name'] ?? '')) !== 0) {
							$epc_name .= ' <small class="text-muted">(' . htmlspecialchars($epc_short, ENT_QUOTES, 'UTF-8') . ')</small>';
						}
						?>
						<tr data-entity-type="<?php echo htmlspecialchars($epc_type, ENT_QUOTES, 'UTF-8'); ?>" data-entity-id="<?php echo $epc_id; ?>" data-saved-enabled="<?php echo $epc_disabled ? '0' : '1'; ?>">
							<td><?php echo $epc_id; ?></td>
							<td><?php echo $epc_name; ?></td>
							<td><?php echo htmlspecialchars((string) ($epc_row['type_label'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
							<td class="text-center">
								<span class="label epc-ssf-badge <?php echo $epc_disabled ? 'label-warning' : 'label-success'; ?>">
									<?php echo $epc_disabled ? 'Temporarily disabled' : 'Active'; ?>
								</span>
							</td>
							<td class="text-center">
								<label class="epc-ssf-switch" title="ON = visible on storefront, OFF = temporarily hidden">
									<input type="checkbox"
										class="epc-ssf-toggle-input"
										data-entity-type="<?php echo htmlspecialchars($epc_type, ENT_QUOTES, 'UTF-8'); ?>"
										data-entity-id="<?php echo $epc_id; ?>"
										<?php echo $epc_disabled ? '' : ' checked'; ?> />
									<span class="epc-ssf-slider"></span>
								</label>
							</td>
							<td class="text-center">
								<button type="button"
									class="btn btn-xs btn-primary epc-ssf-save-btn"
									disabled
									title="Save storefront visibility for this row">
									<i class="fa fa-floppy-o"></i> Save
								</button>
							</td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>
			<?php if (!empty($epc_ssf_audit)): ?>
			<p class="text-muted" style="margin-top:10px;margin-bottom:4px;">
				<small>Recent toggle activity — only real CP operator changes are recorded going forward.
				Automated verify probes are read-only and no longer write toggle state.</small>
			</p>
			<ul class="list-unstyled" style="font-size:12px;color:#666;">
				<?php foreach ($epc_ssf_audit as $epc_a): ?>
				<?php
				$epc_label = trim((string) ($epc_a['user_label'] ?? ''));
				$epc_is_probe = ($epc_label === 'verify-probe' || $epc_label === 'verify-probe-restore'
					|| stripos($epc_label, 'verify-probe') !== false);
				$epc_display_label = $epc_label;
				if ($epc_is_probe) {
					$epc_display_label = '[historical probe — no longer writes] ' . $epc_label;
				}
				?>
				<li<?php echo $epc_is_probe ? ' style="opacity:0.65;font-style:italic;"' : ''; ?>>
					<?php echo htmlspecialchars((string) ($epc_a['created_at'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>
					— <?php echo htmlspecialchars((string) ($epc_a['entity_name'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>
					→ <?php echo ((int) ($epc_a['storefront_disabled'] ?? 0) === 1) ? 'disabled' : 'enabled'; ?>
					<?php if ($epc_display_label !== ''): ?>
						<em>(<?php echo htmlspecialchars($epc_display_label, ENT_QUOTES, 'UTF-8'); ?>)</em>
					<?php endif; ?>
				</li>
				<?php endforeach; ?>
			</ul>
			<?php endif; ?>
		</div>
	</div>
</div>
