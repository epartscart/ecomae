<?php
defined('_ASTEXE_') or die('No access');
/**
 * Sales and marketing — Opportunities (sales pipeline).
 * Stage board prospect -> qualified -> proposal -> negotiation -> won/lost,
 * with stage advance and a hand-off to Sales quotations. Reuses the CRM
 * opportunity engine; posts whitelisted save_opportunity / update_stage.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

if (!function_exists('epc_crm_pack_enabled') || !epc_crm_pack_enabled() || !epc_crm_user_can_access($db_link)) {
	echo '<div class="alert alert-warning"><strong>Opportunities</strong> requires the CRM/Finance pack.</div>';
	return;
}
epc_crm_ensure_schema($db_link);

$csrfLocal = isset($csrf) ? $csrf : '';
$stages = epc_crm_opportunity_stages();
$opps = epc_crm_list_opportunities($db_link);
$leads = epc_crm_list_leads($db_link);
$quoteUrl = epc_erp_tab_url($erpUrl, 'proposals', $date_from_str, $date_to_str, 'sales');
$leadsUrl = epc_erp_tab_url($erpUrl, 'leads', $date_from_str, $date_to_str, 'sales');

$byStage = array();
foreach ($stages as $k => $v) {
	$byStage[$k] = array();
}
$weighted = 0.0;
foreach ($opps as $o) {
	$s = (string) ($o['stage'] ?? 'prospect');
	if (!isset($byStage[$s])) {
		$byStage[$s] = array();
	}
	$byStage[$s][] = $o;
	if (!in_array($s, array('won', 'lost'), true)) {
		$weighted += ((float) $o['amount']) * ((int) $o['probability']) / 100.0;
	}
}
$openCount = count($opps) - count($byStage['won']) - count($byStage['lost']);

erp_page_header(
	'<i class="fa fa-filter"></i> Opportunities',
	'Sales pipeline — advance opportunities through the stages, then raise a sales quotation.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Sales and marketing'),
		array('label' => 'Opportunities'),
	),
	array(
		array('label' => 'Prospects & leads', 'url' => $leadsUrl, 'class' => 'btn-default', 'icon' => 'fa-user-plus'),
		array('label' => 'Sales quotations', 'url' => $quoteUrl, 'class' => 'btn-primary', 'icon' => 'fa-file-text'),
	)
);

erp_stat_cards(array(
	array('label' => 'Open opportunities', 'value' => (string) max(0, $openCount)),
	array('label' => 'Won', 'value' => (string) count($byStage['won'])),
	array('label' => 'Weighted pipeline', 'value' => epc_erp_money($weighted, 0)),
));
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="row">
<div class="col-md-4">
<?php
ob_start(); ?>
<form id="epc_opp_form" class="form">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
	<input type="hidden" name="id" value="0">
	<div class="form-group"><label>Title</label><input type="text" name="title" class="form-control input-sm" required></div>
	<div class="form-group"><label>From lead (optional)</label>
		<select name="lead_id" class="form-control input-sm">
			<option value="0">— none —</option>
			<?php foreach ($leads as $l): ?><option value="<?php echo (int) $l['id']; ?>"><?php echo epc_erp_h($l['company']); ?></option><?php endforeach; ?>
		</select>
	</div>
	<div class="row">
		<div class="col-xs-6 form-group"><label>Stage</label>
			<select name="stage" class="form-control input-sm">
				<?php foreach ($stages as $k => $v): ?><option value="<?php echo epc_erp_h($k); ?>"><?php echo epc_erp_h($v); ?></option><?php endforeach; ?>
			</select>
		</div>
		<div class="col-xs-6 form-group"><label>Probability %</label><input type="number" min="0" max="100" name="probability" value="10" class="form-control input-sm"></div>
	</div>
	<div class="row">
		<div class="col-xs-6 form-group"><label>Amount</label><input type="number" step="0.01" name="amount" class="form-control input-sm"></div>
		<div class="col-xs-6 form-group"><label>Close date</label><input type="date" name="close_date" class="form-control input-sm"></div>
	</div>
	<div class="form-group"><label>Notes</label><textarea name="notes" rows="2" class="form-control input-sm"></textarea></div>
	<button class="btn btn-success btn-sm" type="submit"><i class="fa fa-plus"></i> Save opportunity</button>
</form>
<?php
erp_section_card('New / edit opportunity', ob_get_clean(), array('icon' => 'fa-plus'));
?>
</div>
<div class="col-md-8">
<?php
$stageKeys = array_keys($stages);
ob_start();
echo '<div class="row">';
foreach ($stages as $sk => $sl) {
	$items = $byStage[$sk];
	$label = ($sk === 'won') ? 'success' : (($sk === 'lost') ? 'danger' : 'primary');
	echo '<div class="col-xs-6 col-md-4" style="margin-bottom:12px;">';
	echo '<div class="panel panel-default" style="margin-bottom:0;"><div class="panel-heading" style="padding:6px 10px;"><span class="label label-' . $label . '">' . epc_erp_h($sl) . '</span> <span class="badge">' . count($items) . '</span></div>';
	echo '<div class="panel-body" style="padding:8px;">';
	if (empty($items)) {
		echo '<p class="text-muted" style="margin:0;font-size:12px;">—</p>';
	}
	foreach ($items as $o) {
		$idx = array_search($sk, $stageKeys, true);
		$next = ($idx !== false && $idx < 3) ? $stageKeys[$idx + 1] : '';
		echo '<div style="border:1px solid #eee;border-radius:4px;padding:6px;margin-bottom:6px;">';
		echo '<strong style="font-size:12px;">' . epc_erp_h($o['title']) . '</strong>';
		if (!empty($o['lead_company'])) {
			echo '<br><small class="text-muted">' . epc_erp_h($o['lead_company']) . '</small>';
		}
		echo '<br><small>' . epc_erp_money($o['amount']) . ' · ' . (int) $o['probability'] . '%</small>';
		echo '<div style="margin-top:4px;">';
		if ($next !== '') {
			echo '<button class="btn btn-xs btn-primary epc-opp-advance" data-id="' . (int) $o['id'] . '" data-stage="' . epc_erp_h($next) . '" title="Advance to ' . epc_erp_h($stages[$next]) . '"><i class="fa fa-arrow-right"></i> ' . epc_erp_h($stages[$next]) . '</button> ';
		}
		if (!in_array($sk, array('won', 'lost'), true)) {
			echo '<button class="btn btn-xs btn-success epc-opp-advance" data-id="' . (int) $o['id'] . '" data-stage="won" title="Mark won"><i class="fa fa-trophy"></i></button> ';
			echo '<button class="btn btn-xs btn-default epc-opp-advance" data-id="' . (int) $o['id'] . '" data-stage="lost" title="Mark lost"><i class="fa fa-times"></i></button> ';
		}
		echo '<a class="btn btn-xs btn-link" href="' . epc_erp_h($quoteUrl) . '" title="Raise quotation"><i class="fa fa-file-text"></i></a>';
		echo '</div></div>';
	}
	echo '</div></div></div>';
}
echo '</div>';
erp_section_card('Pipeline board', ob_get_clean(), array('icon' => 'fa-columns'));
?>
</div>
</div>

<script>
(function () {
	var endpoint = <?php echo json_encode($erpAjaxEndpoint); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	var msg = document.getElementById('epc_erp_msg');
	function flash(ok, text) {
		if (!msg) { return; }
		msg.className = 'alert alert-' + (ok ? 'success' : 'danger');
		msg.style.display = 'block';
		msg.textContent = text;
	}
	function post(data, cb) {
		var fd = new FormData();
		Object.keys(data).forEach(function (k) { fd.append(k, data[k]); });
		fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (j) { flash(!!j.status, j.message || (j.status ? 'Saved' : 'Error')); if (j.status) { cb && cb(j); } })
			.catch(function () { flash(false, 'Network error'); });
	}
	var form = document.getElementById('epc_opp_form');
	if (form) {
		form.addEventListener('submit', function (ev) {
			ev.preventDefault();
			var d = { action: 'save_opportunity' };
			new FormData(form).forEach(function (v, k) { d[k] = v; });
			post(d, function () { setTimeout(function () { location.reload(); }, 600); });
		});
	}
	document.querySelectorAll('.epc-opp-advance').forEach(function (b) {
		b.addEventListener('click', function () {
			post({ action: 'update_stage', id: b.getAttribute('data-id'), stage: b.getAttribute('data-stage'), csrf_guard_key: csrf },
				function () { setTimeout(function () { location.reload(); }, 600); });
		});
	});
})();
</script>
