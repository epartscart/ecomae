<?php
defined('_ASTEXE_') or die('No access');

/**
 * Accounting / Inventory setup — tenant-configurable number sequences, inventory
 * valuation method, company defaults (currency, TRN, fiscal year) and rounding.
 * Writes only to the current tenant's database, so every client configures their
 * own values independently (multi-tenant, DB-per-tenant).
 */

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_industry_packs.php';

epc_erp_extended_ensure_schema($db_link);
epc_co_ensure_schema($db_link);

$setupMsg = '';
$setupErr = '';

$validMethods = array(
	'weighted_avg' => 'Weighted average cost',
	'fifo' => 'FIFO (first in, first out)',
	'standard' => 'Standard cost',
);

if (!empty($_POST['setup_action'])) {
	$postedCsrf = isset($_POST['csrf_guard_key']) ? (string) $_POST['csrf_guard_key'] : '';
	if ($csrf !== '' && !hash_equals($csrf, $postedCsrf)) {
		$setupErr = 'Security token mismatch — please reload and try again.';
	} else {
		try {
			$act = (string) $_POST['setup_action'];
			if ($act === 'save_sequences') {
				$year = (int) date('Y');
				foreach (array_keys(epc_erp_voucher_prefix_map()) as $vt) {
					$prefix = isset($_POST['prefix'][$vt]) ? trim((string) $_POST['prefix'][$vt]) : '';
					$pad = isset($_POST['pad'][$vt]) ? (int) $_POST['pad'][$vt] : 5;
					$nextNo = isset($_POST['next_no'][$vt]) ? (int) $_POST['next_no'][$vt] : 0;
					if ($prefix !== '') {
						epc_erp_platform_setting_set($db_link, 'voucher_prefix_' . $vt, $prefix);
					}
					if ($pad >= 1 && $pad <= 10) {
						epc_erp_platform_setting_set($db_link, 'voucher_pad_' . $vt, (string) $pad);
					}
					// "Next number" = last_seq + 1 for the current year. Only move the
					// counter when the admin enters a value (and never below issued).
					if ($nextNo > 0) {
						$cur = $db_link->prepare('SELECT `last_seq` FROM `epc_erp_voucher_sequences` WHERE `voucher_type`=? AND `year`=?');
						$cur->execute(array($vt, $year));
						$lastSeq = (int) $cur->fetchColumn();
						$wantLast = $nextNo - 1;
						if ($wantLast >= $lastSeq) {
							$db_link->prepare(
								'INSERT INTO `epc_erp_voucher_sequences` (`voucher_type`,`year`,`last_seq`) VALUES (?,?,?)
								 ON DUPLICATE KEY UPDATE `last_seq`=VALUES(`last_seq`)'
							)->execute(array($vt, $year, $wantLast));
						}
					}
				}
				$setupMsg = 'Number sequences saved. New documents will use these prefixes immediately.';
			} elseif ($act === 'save_valuation') {
				$method = isset($_POST['valuation_method']) ? (string) $_POST['valuation_method'] : 'weighted_avg';
				if (!isset($validMethods[$method])) {
					$method = 'weighted_avg';
				}
				epc_erp_platform_setting_set($db_link, 'inventory_valuation_method', $method);
				$setupMsg = 'Inventory valuation method saved (' . $validMethods[$method] . ').';
			} elseif ($act === 'save_company') {
				$profile = epc_co_profile_get($db_link);
				$profile['legal_name'] = trim((string) ($_POST['legal_name'] ?? $profile['legal_name']));
				$profile['trade_name'] = trim((string) ($_POST['trade_name'] ?? $profile['trade_name']));
				$profile['trn'] = trim((string) ($_POST['trn'] ?? $profile['trn']));
				$profile['country'] = trim((string) ($_POST['country'] ?? $profile['country']));
				$profile['base_currency'] = strtoupper(trim((string) ($_POST['base_currency'] ?? $profile['base_currency'])));
				$profile['fy_start'] = trim((string) ($_POST['fy_start'] ?? $profile['fy_start']));
				epc_co_profile_save($db_link, $profile);
				$dtr = isset($_POST['default_tax_rate']) ? (string) (float) $_POST['default_tax_rate'] : '';
				if ($dtr !== '') {
					epc_erp_platform_setting_set($db_link, 'default_tax_rate', $dtr);
				}
				$rounding = isset($_POST['amount_rounding']) ? (int) $_POST['amount_rounding'] : 2;
				if ($rounding >= 0 && $rounding <= 4) {
					epc_erp_platform_setting_set($db_link, 'amount_rounding', (string) $rounding);
				}
				$setupMsg = 'Company defaults saved.';
			} elseif ($act === 'save_industry') {
				$pack = isset($_POST['industry_pack']) ? (string) $_POST['industry_pack'] : '';
				if ($pack !== '' && epc_erp_industry_pack($pack) === null) {
					$pack = '';
				}
				epc_erp_platform_setting_set($db_link, 'active_industry_pack', $pack);
				$setupMsg = $pack === ''
					? 'Industry pack cleared (generic / multi-industry).'
					: 'Industry pack set to ' . (string) (epc_erp_industry_pack($pack)['label'] ?? $pack) . '.';
			}
		} catch (Exception $e) {
			$setupErr = 'Could not save: ' . $e->getMessage();
		}
	}
}

// Current values for display.
$company = epc_co_profile_get($db_link);
$valMethod = epc_erp_platform_setting_get($db_link, 'inventory_valuation_method', 'weighted_avg');
if (!isset($validMethods[$valMethod])) {
	$valMethod = 'weighted_avg';
}
$activePack = epc_erp_platform_setting_get($db_link, 'active_industry_pack', '');
if ($activePack !== '' && epc_erp_industry_pack($activePack) === null) {
	$activePack = '';
}
$activePackDef = $activePack !== '' ? epc_erp_industry_pack($activePack) : null;
$defaultTaxRate = epc_erp_platform_setting_get($db_link, 'default_tax_rate', '5');
$amountRounding = (int) epc_erp_platform_setting_get($db_link, 'amount_rounding', '2');
$curYear = (int) date('Y');

$seqRows = array();
$labels = epc_erp_voucher_type_labels();
foreach (epc_erp_voucher_prefix_map() as $vt => $defPrefix) {
	$st = $db_link->prepare('SELECT `last_seq` FROM `epc_erp_voucher_sequences` WHERE `voucher_type`=? AND `year`=?');
	$st->execute(array($vt, $curYear));
	$lastSeq = (int) $st->fetchColumn();
	$prefix = epc_erp_voucher_prefix_for($db_link, $vt);
	$pad = epc_erp_voucher_pad_for($db_link, $vt);
	$nextNo = $lastSeq + 1;
	$preview = $prefix . $curYear . '-' . str_pad((string) $nextNo, $pad, '0', STR_PAD_LEFT);
	$seqRows[] = array(
		'type' => $vt,
		'label' => $labels[$vt] ?? $vt,
		'prefix' => $prefix,
		'pad' => $pad,
		'next_no' => $nextNo,
		'preview' => $preview,
	);
}
?>
<div class="epc-erp-hero">
	<h3><i class="fa fa-cogs"></i> Accounting &amp; inventory setup</h3>
	<p>Configure this tenant's <strong>number sequences</strong>, <strong>inventory valuation method</strong> and <strong>company defaults</strong> (base currency, TRN, fiscal year). Saved values apply only to this company — every tenant configures their own.</p>
</div>

<?php if ($setupMsg !== ''): ?>
<div class="alert alert-success" style="margin-bottom:14px;"><i class="fa fa-check-circle"></i> <?php echo epc_erp_h($setupMsg); ?></div>
<?php endif; ?>
<?php if ($setupErr !== ''): ?>
<div class="alert alert-danger" style="margin-bottom:14px;"><i class="fa fa-exclamation-triangle"></i> <?php echo epc_erp_h($setupErr); ?></div>
<?php endif; ?>

<h4 style="margin-top:6px;"><i class="fa fa-list-ol"></i> Number sequences (voucher numbering)</h4>
<p class="text-muted" style="margin-top:-6px;">Document numbers are built as <code>PREFIX</code> + year + zero-padded counter (e.g. <code>SO-<?php echo $curYear; ?>-00001</code>). Edit the prefix, padding width and the next number per document type. Changes affect the next document created.</p>
<form method="post" action="" style="margin-bottom:26px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="hidden" name="setup_action" value="save_sequences">
	<table class="table table-bordered table-condensed table-striped" style="background:#fff;">
		<thead><tr>
			<th>Document type</th><th>Prefix</th><th>Pad</th><th>Next number (<?php echo $curYear; ?>)</th><th>Next document preview</th>
		</tr></thead>
		<tbody>
		<?php foreach ($seqRows as $r): ?>
			<tr>
				<td><strong><?php echo epc_erp_h($r['type']); ?></strong> — <?php echo epc_erp_h($r['label']); ?></td>
				<td><input type="text" name="prefix[<?php echo epc_erp_h($r['type']); ?>]" value="<?php echo epc_erp_h($r['prefix']); ?>" class="form-control input-sm" style="width:90px;" maxlength="24"></td>
				<td><input type="number" name="pad[<?php echo epc_erp_h($r['type']); ?>]" value="<?php echo (int) $r['pad']; ?>" class="form-control input-sm" style="width:64px;" min="1" max="10"></td>
				<td><input type="number" name="next_no[<?php echo epc_erp_h($r['type']); ?>]" value="<?php echo (int) $r['next_no']; ?>" class="form-control input-sm" style="width:100px;" min="1"></td>
				<td><code><?php echo epc_erp_h($r['preview']); ?></code></td>
			</tr>
		<?php endforeach; ?>
		</tbody>
	</table>
	<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save number sequences</button>
</form>

<h4><i class="fa fa-cubes"></i> Inventory valuation method</h4>
<form method="post" action="" class="form-inline" style="margin-bottom:8px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="hidden" name="setup_action" value="save_valuation">
	<select name="valuation_method" class="form-control input-sm" style="min-width:260px;">
		<?php foreach ($validMethods as $mk => $ml): ?>
		<option value="<?php echo epc_erp_h($mk); ?>" <?php echo $valMethod === $mk ? 'selected' : ''; ?>><?php echo epc_erp_h($ml); ?></option>
		<?php endforeach; ?>
	</select>
	<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save valuation method</button>
</form>
<p class="text-muted" style="margin-bottom:26px;">
	Current costing engine applies <strong>weighted average cost</strong> across stock movements and valuation.
	FIFO and standard cost can be selected here and are recorded per tenant; their costing engines are being rolled out and the selected method is shown on the Inventory screen.
</p>

<h4><i class="fa fa-industry"></i> Industry pack</h4>
<p class="text-muted" style="margin-top:-6px;">Pick the industry to load its specialized units of measure, document process flow, chart-of-accounts presets and posting rules. This is per tenant — a jewellery client and an oil &amp; gas client run the same ERP with different structures.</p>
<form method="post" action="" class="form-inline" style="margin-bottom:10px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="hidden" name="setup_action" value="save_industry">
	<select name="industry_pack" class="form-control input-sm" style="min-width:320px;">
		<option value="">Generic / multi-industry (no pack)</option>
		<?php foreach (epc_erp_industry_packs() as $pk => $pdef): ?>
		<option value="<?php echo epc_erp_h($pk); ?>" <?php echo $activePack === $pk ? 'selected' : ''; ?>><?php echo epc_erp_h($pdef['label']); ?></option>
		<?php endforeach; ?>
	</select>
	<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Apply industry pack</button>
</form>
<?php if ($activePackDef !== null): ?>
<div class="panel panel-default" style="max-width:900px;margin-bottom:26px;">
	<div class="panel-heading"><strong><?php echo epc_erp_h($activePackDef['label']); ?></strong> structure</div>
	<div class="panel-body">
		<p><strong>Units of measure:</strong>
			<?php foreach ((array) ($activePackDef['uoms'] ?? array()) as $u): ?>
			<span class="label label-info" style="margin-right:4px;"><?php echo epc_erp_h($u); ?></span>
			<?php endforeach; ?>
		</p>
		<p><strong>Process flow:</strong> <?php echo epc_erp_h(implode('  →  ', (array) ($activePackDef['process_flow'] ?? array()))); ?></p>
		<p><strong>Chart-of-accounts presets:</strong> <?php echo epc_erp_h(implode(', ', (array) ($activePackDef['coa_presets'] ?? array()))); ?></p>
		<p style="margin-bottom:0;"><strong>Features:</strong>
			<?php foreach ((array) ($activePackDef['features'] ?? array()) as $f): ?>
			<span class="label label-default" style="margin-right:4px;"><?php echo epc_erp_h($f); ?></span>
			<?php endforeach; ?>
		</p>
	</div>
</div>
<?php else: ?>
<p class="text-muted" style="margin-bottom:26px;">No industry pack applied — running generic multi-industry configuration.</p>
<?php endif; ?>

<h4><i class="fa fa-building-o"></i> Company defaults</h4>
<form method="post" action="" style="margin-bottom:10px;max-width:760px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="hidden" name="setup_action" value="save_company">
	<div class="row">
		<div class="form-group col-sm-6" style="padding:6px;">
			<label>Legal name</label>
			<input type="text" name="legal_name" value="<?php echo epc_erp_h($company['legal_name']); ?>" class="form-control input-sm">
		</div>
		<div class="form-group col-sm-6" style="padding:6px;">
			<label>Trade name</label>
			<input type="text" name="trade_name" value="<?php echo epc_erp_h($company['trade_name']); ?>" class="form-control input-sm">
		</div>
		<div class="form-group col-sm-4" style="padding:6px;">
			<label>Base currency</label>
			<input type="text" name="base_currency" value="<?php echo epc_erp_h($company['base_currency']); ?>" class="form-control input-sm" maxlength="3" placeholder="AED">
		</div>
		<div class="form-group col-sm-4" style="padding:6px;">
			<label>TRN / Tax registration no.</label>
			<input type="text" name="trn" value="<?php echo epc_erp_h($company['trn']); ?>" class="form-control input-sm">
		</div>
		<div class="form-group col-sm-4" style="padding:6px;">
			<label>Country (ISO-2)</label>
			<input type="text" name="country" value="<?php echo epc_erp_h($company['country']); ?>" class="form-control input-sm" maxlength="2" placeholder="AE">
		</div>
		<div class="form-group col-sm-4" style="padding:6px;">
			<label>Fiscal year start (MM-DD)</label>
			<input type="text" name="fy_start" value="<?php echo epc_erp_h($company['fy_start']); ?>" class="form-control input-sm" placeholder="01-01">
		</div>
		<div class="form-group col-sm-4" style="padding:6px;">
			<label>Default tax rate (%)</label>
			<input type="number" step="0.01" name="default_tax_rate" value="<?php echo epc_erp_h($defaultTaxRate); ?>" class="form-control input-sm">
		</div>
		<div class="form-group col-sm-4" style="padding:6px;">
			<label>Amount rounding (decimals)</label>
			<input type="number" name="amount_rounding" value="<?php echo (int) $amountRounding; ?>" class="form-control input-sm" min="0" max="4">
		</div>
	</div>
	<button type="submit" class="btn btn-primary btn-sm" style="margin:6px;"><i class="fa fa-save"></i> Save company defaults</button>
</form>
