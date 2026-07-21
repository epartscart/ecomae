<?php
/**
 * ERP Automation Centre — Accounting + Business Process Automation.
 *
 * Graphical hub: catalogue status, visual pipelines, installable templates,
 * and a node-style workflow builder (trigger → steps → actions).
 *
 * Used by tabs: workflow_automation, accounting_automation
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_automation_catalogue.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_workflow_builder.php';

$_wfOk = true;
$_wfErr = '';
try {
	if (isset($db_link) && $db_link instanceof PDO) {
		epc_workflow_ensure_schema($db_link);
	}
} catch (Throwable $e) {
	$_wfOk = false;
	$_wfErr = $e->getMessage();
}

$csrfLocal = isset($csrf) ? $csrf : '';
$tabKey = (isset($tab) && is_string($tab) && $tab !== '') ? $tab : 'workflow_automation';
$autoView = isset($_GET['auto_view']) ? (string) $_GET['auto_view'] : '';
if ($autoView === '') {
	$autoView = ($tabKey === 'accounting_automation') ? 'accounting' : 'hub';
}
$allowedViews = array('hub', 'accounting', 'processes', 'builder', 'runs');
if (!in_array($autoView, $allowedViews, true)) {
	$autoView = 'hub';
}

$_wfSiteKey = '';
if (function_exists('epc_erp_site_key')) {
	$_wfSiteKey = epc_erp_site_key();
} elseif (isset($DP_Config) && isset($DP_Config->site_key)) {
	$_wfSiteKey = (string) $DP_Config->site_key;
} elseif (isset($site_key)) {
	$_wfSiteKey = (string) $site_key;
}

$statusPack = array('items' => array(), 'kpis' => array());
if (isset($db_link) && $db_link instanceof PDO) {
	try {
		$statusPack = epc_erp_automation_status_list($db_link, $_wfSiteKey);
	} catch (Throwable $e) {
		$statusPack = array('items' => array(), 'kpis' => array());
	}
}
$kpis = $statusPack['kpis'] ?? array();
$items = $statusPack['items'] ?? array();
$acctItems = array_values(array_filter($items, static function ($i) { return ($i['category'] ?? '') === 'accounting'; }));
$procItems = array_values(array_filter($items, static function ($i) { return ($i['category'] ?? '') === 'process'; }));

$wfBase = epc_erp_tab_url($erpUrl, $tabKey, $date_from_str, $date_to_str, ($tabKey === 'accounting_automation' ? 'finance' : 'setup'));
$autoUrl = static function ($view) use ($wfBase) {
	$sep = (strpos($wfBase, '?') === false) ? '?' : '&';
	return $wfBase . $sep . 'auto_view=' . rawurlencode($view);
};

$workflows = array();
$runs = array();
if ($_wfOk && isset($db_link) && $db_link instanceof PDO && $_wfSiteKey !== '') {
	try {
		$workflows = epc_workflow_list($db_link, $_wfSiteKey);
	} catch (Throwable $e) {
		$workflows = array();
	}
	try {
		// Recent runs across workflows
		$st = $db_link->prepare(
			'SELECT r.*, w.name AS workflow_name FROM epc_workflow_runs r
			 LEFT JOIN epc_workflows w ON w.id = r.workflow_id
			 WHERE r.site_key = ? ORDER BY r.started_at DESC LIMIT 40'
		);
		$st->execute(array($_wfSiteKey));
		$runs = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	} catch (Throwable $e) {
		$runs = array();
	}
}

$templates = epc_erp_automation_workflow_templates();
$triggerTypes = epc_workflow_trigger_types();
$actionTypes = epc_workflow_action_types();
$erpAjax = isset($erpAjaxUrl) ? (string) $erpAjaxUrl : '';

$editId = isset($_GET['wf_id']) ? (int) $_GET['wf_id'] : 0;
$editWf = null;
if ($autoView === 'builder' && $editId > 0 && isset($db_link) && $db_link instanceof PDO) {
	try {
		$editWf = epc_workflow_get($db_link, $editId);
	} catch (Throwable $e) {
		$editWf = null;
	}
}

erp_page_header(
	'<i class="fa fa-magic"></i> Automation Centre',
	'Accounting automation and every business process — enable, visualize, and run from one place.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => ($tabKey === 'accounting_automation' ? 'General ledger' : 'System administration')),
		array('label' => 'Automation Centre'),
	),
	array(
		array('label' => 'Enable all accounting', 'url' => '#', 'class' => 'btn-primary epc-auto-enable-all', 'icon' => 'fa-bolt', 'attrs' => 'data-category="accounting"'),
		array('label' => 'New workflow', 'url' => $autoUrl('builder'), 'class' => 'btn-default', 'icon' => 'fa-plus'),
	)
);

if ($_wfErr !== '') {
	echo '<div class="alert alert-warning"><i class="fa fa-exclamation-triangle"></i> ' . epc_erp_h($_wfErr) . '</div>';
}

$statusBadge = static function ($status) {
	$map = array('active' => 'success', 'available' => 'warning', 'missing' => 'default');
	$cls = $map[$status] ?? 'default';
	return '<span class="label label-' . $cls . '">' . htmlspecialchars(ucfirst((string) $status), ENT_QUOTES, 'UTF-8') . '</span>';
};

$renderPipeline = static function (array $steps, string $tone = 'blue') {
	$colors = array(
		'blue' => array('#0c4a6e', '#0369a1', '#0ea5e9'),
		'teal' => array('#134e4a', '#0f766e', '#14b8a6'),
		'amber' => array('#78350f', '#b45309', '#f59e0b'),
	);
	$c = $colors[$tone] ?? $colors['blue'];
	$html = '<div class="epc-auto-pipeline" role="list">';
	$n = count($steps);
	foreach ($steps as $i => $label) {
		$html .= '<div class="epc-auto-pipeline__step" role="listitem" style="--epc-step:' . epc_erp_h($c[$i % 3]) . '">';
		$html .= '<span class="epc-auto-pipeline__num">' . ($i + 1) . '</span>';
		$html .= '<span class="epc-auto-pipeline__label">' . htmlspecialchars((string) $label, ENT_QUOTES, 'UTF-8') . '</span>';
		$html .= '</div>';
		if ($i < $n - 1) {
			$html .= '<div class="epc-auto-pipeline__arrow" aria-hidden="true"><i class="fa fa-chevron-right"></i></div>';
		}
	}
	$html .= '</div>';
	return $html;
};
?>
<style>
.epc-auto-hero{background:linear-gradient(135deg,#0b3d4a 0%,#0e7490 48%,#22d3ee 100%);color:#fff;border-radius:14px;padding:22px 24px;margin-bottom:16px;position:relative;overflow:hidden}
.epc-auto-hero::after{content:"";position:absolute;right:-40px;top:-40px;width:220px;height:220px;border-radius:50%;background:rgba(255,255,255,.08)}
.epc-auto-hero h3{margin:0 0 6px;font-size:20px;font-weight:800;color:#fff}
.epc-auto-hero p{margin:0;max-width:720px;opacity:.95}
.epc-auto-kpis{display:flex;flex-wrap:wrap;gap:10px;margin:14px 0 18px}
.epc-auto-kpis .kpi{flex:1 1 120px;background:#fff;border:1px solid #dbeafe;border-radius:12px;padding:12px 14px;box-shadow:0 1px 3px rgba(14,116,144,.08)}
.epc-auto-kpis .kpi .lbl{font-size:10px;text-transform:uppercase;letter-spacing:.06em;color:#64748b;font-weight:700}
.epc-auto-kpis .kpi .val{font-size:22px;font-weight:800;color:#0f172a;margin-top:2px}
.epc-auto-tabs{display:flex;flex-wrap:wrap;gap:6px;margin-bottom:16px}
.epc-auto-tabs a{display:inline-flex;align-items:center;gap:6px;padding:8px 14px;border-radius:999px;border:1px solid #e2e8f0;background:#fff;color:#334155;font-weight:600;font-size:12px;text-decoration:none}
.epc-auto-tabs a.active{background:linear-gradient(135deg,#0e7490,#22d3ee);color:#fff;border-color:transparent}
.epc-auto-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:12px;margin-bottom:18px}
.epc-auto-card{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:14px 16px;display:flex;flex-direction:column;gap:8px;transition:transform .15s,box-shadow .15s;min-height:180px}
.epc-auto-card:hover{transform:translateY(-2px);box-shadow:0 8px 20px rgba(15,23,42,.08)}
.epc-auto-card__head{display:flex;align-items:flex-start;justify-content:space-between;gap:8px}
.epc-auto-card__title{margin:0;font-size:14px;font-weight:800;color:#0f172a}
.epc-auto-card__title i{color:#0e7490;margin-right:6px}
.epc-auto-card__desc{margin:0;font-size:12px;color:#64748b;line-height:1.45;flex:1}
.epc-auto-card__actions{display:flex;flex-wrap:wrap;gap:6px;margin-top:4px}
.epc-auto-pipeline{display:flex;flex-wrap:wrap;align-items:center;gap:4px;margin:4px 0}
.epc-auto-pipeline__step{display:flex;align-items:center;gap:6px;background:linear-gradient(180deg,#f8fafc,#f1f5f9);border:1px solid #e2e8f0;border-left:3px solid var(--epc-step,#0ea5e9);border-radius:8px;padding:6px 8px;min-width:72px}
.epc-auto-pipeline__num{width:18px;height:18px;border-radius:50%;background:var(--epc-step,#0ea5e9);color:#fff;font-size:10px;font-weight:800;display:inline-flex;align-items:center;justify-content:center}
.epc-auto-pipeline__label{font-size:10px;font-weight:700;color:#334155;line-height:1.2}
.epc-auto-pipeline__arrow{color:#94a3b8;font-size:10px;padding:0 2px}
.epc-auto-canvas{background:#0b1220;border-radius:14px;padding:18px;margin-bottom:16px;overflow-x:auto}
.epc-auto-flow{display:flex;align-items:stretch;gap:0;min-width:max-content;padding:8px 4px}
.epc-auto-node{width:160px;background:#111827;border:1px solid #334155;border-radius:12px;padding:12px;color:#e2e8f0;position:relative;box-shadow:0 8px 24px rgba(0,0,0,.35)}
.epc-auto-node--trigger{border-color:#22d3ee;background:linear-gradient(180deg,#083344,#0f172a)}
.epc-auto-node--condition{border-color:#f59e0b}
.epc-auto-node--action{border-color:#34d399}
.epc-auto-node__kind{font-size:9px;text-transform:uppercase;letter-spacing:.08em;opacity:.7;margin-bottom:4px}
.epc-auto-node__title{font-size:13px;font-weight:700;margin:0 0 4px}
.epc-auto-node__meta{font-size:11px;opacity:.75}
.epc-auto-connector{width:36px;display:flex;align-items:center;justify-content:center;color:#22d3ee;position:relative}
.epc-auto-connector::before{content:"";position:absolute;left:0;right:0;height:2px;background:linear-gradient(90deg,#22d3ee,#34d399);top:50%}
.epc-auto-connector i{position:relative;z-index:1;background:#0b1220;padding:2px}
.epc-auto-map{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:10px;margin:12px 0}
.epc-auto-map__item{background:#fff;border:1px solid #e2e8f0;border-radius:10px;padding:10px 12px}
.epc-auto-map__item h5{margin:0 0 6px;font-size:12px;font-weight:800}
@keyframes epcAutoPulse{0%,100%{box-shadow:0 0 0 0 rgba(34,211,238,.45)}70%{box-shadow:0 0 0 10px rgba(34,211,238,0)}}
.epc-auto-node--trigger{animation:epcAutoPulse 2.4s ease-out infinite}
@media (max-width:720px){
	.epc-auto-pipeline{flex-direction:column;align-items:stretch}
	.epc-auto-pipeline__arrow{transform:rotate(90deg);align-self:center}
}
</style>

<div class="epc-auto-hero">
	<h3><i class="fa fa-magic"></i> Accounting &amp; Business Process Automation</h3>
	<p>See every automation available for this ERP tenant, turn them on, watch the graphical pipeline, and build custom trigger → condition → action workflows.</p>
</div>

<div class="epc-auto-kpis">
	<div class="kpi"><div class="lbl">Total</div><div class="val"><?php echo (int)($kpis['total'] ?? 0); ?></div></div>
	<div class="kpi"><div class="lbl">Active</div><div class="val" style="color:#059669"><?php echo (int)($kpis['active'] ?? 0); ?></div></div>
	<div class="kpi"><div class="lbl">Available</div><div class="val" style="color:#d97706"><?php echo (int)($kpis['available'] ?? 0); ?></div></div>
	<div class="kpi"><div class="lbl">Accounting</div><div class="val"><?php echo (int)($kpis['accounting'] ?? 0); ?></div></div>
	<div class="kpi"><div class="lbl">Processes</div><div class="val"><?php echo (int)($kpis['process'] ?? 0); ?></div></div>
	<div class="kpi"><div class="lbl">Workflows</div><div class="val"><?php echo (int)($kpis['workflows_active'] ?? 0); ?>/<?php echo (int)($kpis['workflows_total'] ?? 0); ?></div></div>
</div>

<nav class="epc-auto-tabs" aria-label="Automation views">
	<a class="<?php echo $autoView === 'hub' ? 'active' : ''; ?>" href="<?php echo epc_erp_h($autoUrl('hub')); ?>"><i class="fa fa-th-large"></i> Hub</a>
	<a class="<?php echo $autoView === 'accounting' ? 'active' : ''; ?>" href="<?php echo epc_erp_h($autoUrl('accounting')); ?>"><i class="fa fa-university"></i> Accounting</a>
	<a class="<?php echo $autoView === 'processes' ? 'active' : ''; ?>" href="<?php echo epc_erp_h($autoUrl('processes')); ?>"><i class="fa fa-sitemap"></i> Business processes</a>
	<a class="<?php echo $autoView === 'builder' ? 'active' : ''; ?>" href="<?php echo epc_erp_h($autoUrl('builder')); ?>"><i class="fa fa-cogs"></i> Workflow builder</a>
	<a class="<?php echo $autoView === 'runs' ? 'active' : ''; ?>" href="<?php echo epc_erp_h($autoUrl('runs')); ?>"><i class="fa fa-history"></i> Run history</a>
</nav>

<div id="epc_auto_msg" class="alert" style="display:none;margin-bottom:12px"></div>

<?php if ($autoView === 'hub' || $autoView === 'accounting'): ?>
	<div class="ef-window">
		<div class="ef-title"><i class="fa fa-university"></i> Accounting automation</div>
		<div class="ef-body">
			<p class="text-muted" style="margin-top:0">Order-to-cash, period close, bank recon, collections, report schedules and GL posting — each with a visual pipeline.</p>
			<div class="epc-auto-grid">
			<?php foreach ($acctItems as $item): ?>
				<div class="epc-auto-card" data-auto-id="<?php echo epc_erp_h($item['id']); ?>">
					<div class="epc-auto-card__head">
						<h4 class="epc-auto-card__title"><i class="fa <?php echo epc_erp_h($item['icon']); ?>"></i><?php echo epc_erp_h($item['name']); ?></h4>
						<?php echo $statusBadge($item['status']); ?>
					</div>
					<p class="epc-auto-card__desc"><?php echo epc_erp_h($item['desc']); ?></p>
					<?php echo $renderPipeline($item['pipeline'] ?? array(), 'blue'); ?>
					<div class="epc-auto-card__actions">
						<?php if (($item['status'] ?? '') !== 'active'): ?>
							<button type="button" class="btn btn-primary btn-xs epc-auto-activate" data-id="<?php echo epc_erp_h($item['id']); ?>"><i class="fa fa-bolt"></i> Enable</button>
						<?php else: ?>
							<button type="button" class="btn btn-default btn-xs epc-auto-deactivate" data-id="<?php echo epc_erp_h($item['id']); ?>"><i class="fa fa-pause"></i> Disable</button>
						<?php endif; ?>
						<?php if (!empty($item['tab'])): ?>
							<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, $item['tab'], $date_from_str, $date_to_str, $item['area'] ?? '')); ?>"><i class="fa fa-external-link"></i> Open</a>
						<?php endif; ?>
					</div>
				</div>
			<?php endforeach; ?>
			</div>
		</div>
	</div>
<?php endif; ?>

<?php if ($autoView === 'hub' || $autoView === 'processes'): ?>
	<div class="ef-window">
		<div class="ef-title"><i class="fa fa-sitemap"></i> Business process automation</div>
		<div class="ef-body">
			<p class="text-muted" style="margin-top:0">Approvals, inventory alerts, onboarding, AML, 3-way match, subscriptions, RMA — enable and install matching workflow templates.</p>
			<div class="epc-auto-grid">
			<?php foreach ($procItems as $item): ?>
				<div class="epc-auto-card" data-auto-id="<?php echo epc_erp_h($item['id']); ?>">
					<div class="epc-auto-card__head">
						<h4 class="epc-auto-card__title"><i class="fa <?php echo epc_erp_h($item['icon']); ?>"></i><?php echo epc_erp_h($item['name']); ?></h4>
						<?php echo $statusBadge($item['status']); ?>
					</div>
					<p class="epc-auto-card__desc"><?php echo epc_erp_h($item['desc']); ?></p>
					<?php echo $renderPipeline($item['pipeline'] ?? array(), 'teal'); ?>
					<div class="epc-auto-card__actions">
						<?php if (($item['status'] ?? '') !== 'active'): ?>
							<button type="button" class="btn btn-primary btn-xs epc-auto-activate" data-id="<?php echo epc_erp_h($item['id']); ?>"><i class="fa fa-bolt"></i> Enable</button>
						<?php else: ?>
							<button type="button" class="btn btn-default btn-xs epc-auto-deactivate" data-id="<?php echo epc_erp_h($item['id']); ?>"><i class="fa fa-pause"></i> Disable</button>
						<?php endif; ?>
						<?php if (!empty($item['workflow_template'])): ?>
							<button type="button" class="btn btn-info btn-xs epc-auto-install" data-template="<?php echo epc_erp_h($item['workflow_template']); ?>"><i class="fa fa-download"></i> Install workflow</button>
						<?php endif; ?>
						<?php if (!empty($item['tab'])): ?>
							<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, $item['tab'], $date_from_str, $date_to_str, $item['area'] ?? '')); ?>"><i class="fa fa-external-link"></i> Open</a>
						<?php endif; ?>
					</div>
				</div>
			<?php endforeach; ?>
			</div>
		</div>
	</div>

	<div class="ef-window">
		<div class="ef-title"><i class="fa fa-magic"></i> Installable workflow templates</div>
		<div class="ef-body">
			<div class="epc-auto-map">
			<?php foreach ($templates as $tid => $tpl): ?>
				<div class="epc-auto-map__item">
					<h5><?php echo epc_erp_h($tpl['name']); ?></h5>
					<p class="text-muted" style="font-size:12px;margin:0 0 8px"><?php echo epc_erp_h($tpl['description']); ?></p>
					<span class="label label-info"><?php echo epc_erp_h($tpl['trigger_type']); ?></span>
					<span class="label label-default"><?php echo count($tpl['steps'] ?? array()); ?> steps</span>
					<div style="margin-top:8px">
						<button type="button" class="btn btn-primary btn-xs epc-auto-install" data-template="<?php echo epc_erp_h($tid); ?>"><i class="fa fa-download"></i> Install</button>
					</div>
				</div>
			<?php endforeach; ?>
			</div>
		</div>
	</div>
<?php endif; ?>

<?php if ($autoView === 'builder'): ?>
	<?php
	$_f = static function ($key, $default = '') use ($editWf) {
		if (!$editWf) {
			return $default;
		}
		return isset($editWf[$key]) ? $editWf[$key] : $default;
	};
	$steps = (isset($editWf['steps']) && is_array($editWf['steps'])) ? $editWf['steps'] : array(
		array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'Notify team', 'config' => array('title' => 'Workflow ran', 'message' => 'OK')),
	);
	$trigCfg = $_f('trigger_config', array());
	if (is_array($trigCfg)) {
		$trigCfgJson = json_encode($trigCfg, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	} else {
		$trigCfgJson = (string) $trigCfg;
	}
	?>
	<div class="ef-window">
		<div class="ef-title"><i class="fa fa-cogs"></i> <?php echo $editWf ? 'Edit' : 'New'; ?> workflow — graphical builder</div>
		<div class="ef-body">
			<form id="epc_auto_wf_form" method="post" action="<?php echo epc_erp_h($erpAjax); ?>" class="epc-erp-form">
				<input type="hidden" name="action" value="workflow_save">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="id" value="<?php echo (int) $_f('id', 0); ?>">

				<div class="row">
					<div class="col-sm-6">
						<div class="ef-section"><span class="ef-section-title">General</span>
							<table class="ef-grid">
								<tr><td style="width:120px"><label>Name</label></td><td><input type="text" name="name" class="form-control input-sm" required value="<?php echo epc_erp_h($_f('name')); ?>"></td></tr>
								<tr><td><label>Description</label></td><td><textarea name="description" class="form-control input-sm" rows="2"><?php echo epc_erp_h($_f('description')); ?></textarea></td></tr>
								<tr><td><label>Active</label></td><td><label><input type="checkbox" name="active" value="1"<?php echo !empty($editWf['active']) || !$editWf ? ' checked' : ''; ?>> Enable</label></td></tr>
							</table>
						</div>
					</div>
					<div class="col-sm-6">
						<div class="ef-section"><span class="ef-section-title">Trigger</span>
							<table class="ef-grid">
								<tr><td style="width:120px"><label>Type</label></td><td>
									<select name="trigger_type" class="form-control input-sm" id="epc_auto_trigger_type">
									<?php foreach ($triggerTypes as $tt): ?>
										<option value="<?php echo epc_erp_h($tt['type']); ?>"<?php echo ($_f('trigger_type', 'event') === $tt['type']) ? ' selected' : ''; ?>><?php echo epc_erp_h($tt['label']); ?></option>
									<?php endforeach; ?>
									</select>
								</td></tr>
								<tr><td><label>Config JSON</label></td><td><textarea name="trigger_config" class="form-control input-sm" rows="3" placeholder='{"event_type":"order.placed"}'><?php echo epc_erp_h($trigCfgJson); ?></textarea></td></tr>
							</table>
						</div>
					</div>
				</div>

				<!-- Graphical canvas preview -->
				<div class="epc-auto-canvas" id="epc_auto_canvas">
					<div class="epc-auto-flow" id="epc_auto_flow_preview">
						<div class="epc-auto-node epc-auto-node--trigger">
							<div class="epc-auto-node__kind">Trigger</div>
							<div class="epc-auto-node__title" id="epc_auto_prev_trigger"><?php echo epc_erp_h($_f('trigger_type', 'event')); ?></div>
							<div class="epc-auto-node__meta">Starts the chain</div>
						</div>
						<?php foreach ($steps as $si => $step):
							$stype = (string) ($step['step_type'] ?? 'action');
							$nodeClass = $stype === 'condition' ? 'epc-auto-node--condition' : 'epc-auto-node--action';
							$label = (string) ($step['label'] ?? ($step['action_type'] ?? 'Step'));
							if ($label === '' && !empty($step['config']['label'])) {
								$label = (string) $step['config']['label'];
							}
						?>
						<div class="epc-auto-connector"><i class="fa fa-arrow-right"></i></div>
						<div class="epc-auto-node <?php echo $nodeClass; ?>">
							<div class="epc-auto-node__kind"><?php echo epc_erp_h(ucfirst($stype)); ?></div>
							<div class="epc-auto-node__title"><?php echo epc_erp_h($label !== '' ? $label : ($step['action_type'] ?? 'Step')); ?></div>
							<div class="epc-auto-node__meta"><?php echo epc_erp_h((string) ($step['action_type'] ?? '')); ?></div>
						</div>
						<?php endforeach; ?>
					</div>
				</div>

				<div class="ef-section"><span class="ef-section-title">Steps</span>
					<table class="ef-grid" id="epc_auto_steps_table"><thead><tr><th>#</th><th>Type</th><th>Action</th><th>Label</th><th>Config (JSON)</th><th></th></tr></thead><tbody>
					<?php foreach ($steps as $si => $step):
						$cfg = $step['config'] ?? array();
						$cfgStr = is_string($cfg) ? $cfg : json_encode($cfg, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
						$lbl = (string) ($step['label'] ?? '');
						if ($lbl === '' && is_array($cfg) && !empty($cfg['label'])) {
							$lbl = (string) $cfg['label'];
						}
					?>
						<tr>
							<td><?php echo $si + 1; ?></td>
							<td>
								<select name="steps[<?php echo $si; ?>][step_type]" class="form-control input-sm">
									<?php foreach (array('action','condition','delay') as $st): ?>
										<option value="<?php echo $st; ?>"<?php echo (($step['step_type'] ?? 'action') === $st) ? ' selected' : ''; ?>><?php echo ucfirst($st); ?></option>
									<?php endforeach; ?>
								</select>
							</td>
							<td>
								<select name="steps[<?php echo $si; ?>][action_type]" class="form-control input-sm">
									<option value="">—</option>
									<?php foreach ($actionTypes as $at): ?>
										<option value="<?php echo epc_erp_h($at['type']); ?>"<?php echo (($step['action_type'] ?? '') === $at['type']) ? ' selected' : ''; ?>><?php echo epc_erp_h($at['label']); ?></option>
									<?php endforeach; ?>
								</select>
							</td>
							<td><input type="text" name="steps[<?php echo $si; ?>][label]" class="form-control input-sm" value="<?php echo epc_erp_h($lbl); ?>"></td>
							<td><input type="text" name="steps[<?php echo $si; ?>][config]" class="form-control input-sm" value="<?php echo epc_erp_h($cfgStr); ?>"></td>
							<td></td>
						</tr>
					<?php endforeach; ?>
					</tbody></table>
					<button type="button" class="btn btn-default btn-xs" id="epc_auto_add_step" style="margin-top:8px"><i class="fa fa-plus"></i> Add step</button>
				</div>

				<div class="ef-actions" style="margin-top:12px">
					<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save workflow</button>
					<?php if ($editId > 0): ?>
						<button type="button" class="btn btn-info btn-sm epc-auto-run-now" data-id="<?php echo (int)$editId; ?>"><i class="fa fa-play"></i> Run now</button>
					<?php endif; ?>
					<a href="<?php echo epc_erp_h($autoUrl('hub')); ?>" class="btn btn-default btn-sm">Back</a>
				</div>
			</form>
		</div>
	</div>

	<div class="ef-window">
		<div class="ef-title"><i class="fa fa-list"></i> Existing workflows</div>
		<div class="ef-body">
			<?php if (empty($workflows)): ?>
				<p class="text-muted">No workflows yet — install a template from Business processes, or create one above.</p>
			<?php else: ?>
				<table class="ef-grid"><thead><tr><th>Name</th><th>Trigger</th><th>Status</th><th>Runs</th><th>Last</th><th></th></tr></thead><tbody>
				<?php foreach ($workflows as $w): ?>
					<tr>
						<td><strong><?php echo epc_erp_h($w['name'] ?? ''); ?></strong></td>
						<td><span class="label label-info"><?php echo epc_erp_h($w['trigger_type'] ?? ''); ?></span></td>
						<td><?php echo !empty($w['active']) ? '<span class="label label-success">Active</span>' : '<span class="label label-default">Off</span>'; ?></td>
						<td><?php echo (int)($w['run_count'] ?? 0); ?></td>
						<td><small><?php echo epc_erp_h($w['last_run_at'] ?? '—'); ?></small></td>
						<td>
							<a class="btn btn-xs btn-default" href="<?php echo epc_erp_h($autoUrl('builder') . '&wf_id=' . (int)$w['id']); ?>"><i class="fa fa-pencil"></i></a>
							<button type="button" class="btn btn-xs btn-info epc-auto-run-now" data-id="<?php echo (int)$w['id']; ?>"><i class="fa fa-play"></i></button>
						</td>
					</tr>
				<?php endforeach; ?>
				</tbody></table>
			<?php endif; ?>
		</div>
	</div>
<?php endif; ?>

<?php if ($autoView === 'runs'): ?>
	<div class="ef-window">
		<div class="ef-title"><i class="fa fa-history"></i> Recent automation runs</div>
		<div class="ef-toolbar">
			<button type="button" class="btn btn-primary btn-xs" id="epc_auto_tick"><i class="fa fa-refresh"></i> Run scheduled tick now</button>
		</div>
		<div class="ef-body">
			<?php if (empty($runs)): ?>
				<p class="text-muted">No runs recorded yet. Enable automations and use <em>Run now</em> on a workflow, or wait for the schedule tick.</p>
			<?php else: ?>
				<table class="ef-grid"><thead><tr><th>When</th><th>Workflow</th><th>Status</th><th>Duration</th><th>Error</th></tr></thead><tbody>
				<?php foreach ($runs as $r): ?>
					<tr>
						<td><small><?php echo epc_erp_h($r['started_at'] ?? ''); ?></small></td>
						<td><?php echo epc_erp_h($r['workflow_name'] ?? ('#' . (int)($r['workflow_id'] ?? 0))); ?></td>
						<td><?php
							$st = (string)($r['status'] ?? '');
							$cls = $st === 'success' ? 'success' : ($st === 'failed' ? 'danger' : 'default');
							echo '<span class="label label-' . $cls . '">' . epc_erp_h($st) . '</span>';
						?></td>
						<td><?php echo (int)($r['duration_ms'] ?? 0); ?> ms</td>
						<td><small><?php echo epc_erp_h($r['error_message'] ?? ''); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody></table>
			<?php endif; ?>
		</div>
	</div>
<?php endif; ?>

<script>
(function(){
	var ajaxUrl = <?php echo json_encode($erpAjax); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	var actionTypes = <?php echo json_encode(array_map(static function ($a) { return array('type'=>$a['type'],'label'=>$a['label']); }, $actionTypes)); ?>;
	var msg = document.getElementById('epc_auto_msg');
	function show(ok, text){
		if (!msg) return;
		msg.style.display = 'block';
		msg.className = 'alert alert-' + (ok ? 'success' : 'danger');
		msg.innerHTML = text;
	}
	function post(data, cb){
		data.csrf_guard_key = csrf;
		var body = Object.keys(data).map(function(k){
			return encodeURIComponent(k) + '=' + encodeURIComponent(data[k] == null ? '' : data[k]);
		}).join('&');
		var xhr = new XMLHttpRequest();
		xhr.open('POST', ajaxUrl, true);
		xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
		xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
		xhr.onload = function(){
			var res = {};
			try { res = JSON.parse(xhr.responseText); } catch(e) { res = {ok:false, message:xhr.responseText}; }
			cb(res);
		};
		xhr.onerror = function(){ cb({ok:false, message:'Network error'}); };
		xhr.send(body);
	}
	document.querySelectorAll('.epc-auto-activate').forEach(function(btn){
		btn.addEventListener('click', function(){
			post({action:'automation_activate', id: btn.getAttribute('data-id')}, function(res){
				show(!!res.ok, res.message || (res.ok ? 'Enabled' : 'Failed'));
				if (res.ok) setTimeout(function(){ location.reload(); }, 600);
			});
		});
	});
	document.querySelectorAll('.epc-auto-deactivate').forEach(function(btn){
		btn.addEventListener('click', function(){
			post({action:'automation_deactivate', id: btn.getAttribute('data-id')}, function(res){
				show(!!res.ok, res.message || (res.ok ? 'Disabled' : 'Failed'));
				if (res.ok) setTimeout(function(){ location.reload(); }, 600);
			});
		});
	});
	document.querySelectorAll('.epc-auto-install').forEach(function(btn){
		btn.addEventListener('click', function(){
			post({action:'automation_install_template', template_id: btn.getAttribute('data-template')}, function(res){
				show(!!res.ok, res.message || (res.ok ? 'Installed' : 'Failed'));
				if (res.ok) setTimeout(function(){ location.reload(); }, 700);
			});
		});
	});
	document.querySelectorAll('.epc-auto-run-now').forEach(function(btn){
		btn.addEventListener('click', function(){
			post({action:'workflow_run', id: btn.getAttribute('data-id')}, function(res){
				show(!!res.ok, res.message || (res.ok ? 'Run complete' : 'Failed'));
			});
		});
	});
	document.querySelectorAll('.epc-auto-enable-all').forEach(function(btn){
		btn.addEventListener('click', function(e){
			e.preventDefault();
			post({action:'automation_enable_category', category: btn.getAttribute('data-category') || 'accounting'}, function(res){
				show(!!res.ok, res.message || (res.ok ? 'Enabled' : 'Failed'));
				if (res.ok) setTimeout(function(){ location.reload(); }, 700);
			});
		});
	});
	var tick = document.getElementById('epc_auto_tick');
	if (tick) {
		tick.addEventListener('click', function(){
			post({action:'automation_tick'}, function(res){
				show(!!res.ok, res.message || (res.ok ? 'Tick complete' : 'Failed'));
				if (res.ok) setTimeout(function(){ location.reload(); }, 800);
			});
		});
	}
	var addBtn = document.getElementById('epc_auto_add_step');
	var tbody = document.querySelector('#epc_auto_steps_table tbody');
	if (addBtn && tbody) {
		addBtn.addEventListener('click', function(){
			var i = tbody.querySelectorAll('tr').length;
			var opts = actionTypes.map(function(a){ return '<option value="'+a.type+'">'+a.label+'</option>'; }).join('');
			var tr = document.createElement('tr');
			tr.innerHTML = '<td>'+(i+1)+'</td>'+
				'<td><select name="steps['+i+'][step_type]" class="form-control input-sm"><option value="action">Action</option><option value="condition">Condition</option><option value="delay">Delay</option></select></td>'+
				'<td><select name="steps['+i+'][action_type]" class="form-control input-sm"><option value="">—</option>'+opts+'</select></td>'+
				'<td><input type="text" name="steps['+i+'][label]" class="form-control input-sm" value="Step '+(i+1)+'"></td>'+
				'<td><input type="text" name="steps['+i+'][config]" class="form-control input-sm" value="{}"></td><td></td>';
			tbody.appendChild(tr);
		});
	}
})();
</script>
