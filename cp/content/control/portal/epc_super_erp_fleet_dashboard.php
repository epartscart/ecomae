<?php
/**
 * Super ERP Fleet Dashboard — View all ERP instances + BOS full control.
 * Fleet-wide ERP visibility with industry-specific module status, KPI aggregation, and BOS control.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_super_cp_platform.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_consolidation.php';

if (!epc_scp_guard_super_admin()) {
	return;
}

$backend = epc_scp_backend();
$base = '/' . $backend;
$industryGroups = epc_industry_groups();

// Get tenants with ERP status
$tenants = array();
try {
	$pdo = epc_portal_pdo();
	$stmt = $pdo->query("SELECT * FROM epc_portal_tenants WHERE active = 1 ORDER BY company_name");
	$tenants = $stmt->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$tenants = array();
}

$totalERPs = count($tenants);
$totalIndustries = count($industryGroups);

// ERP module list (from PR #121)
$erpModules = array(
	'sla' => 'SLA Agreements',
	'tickets' => 'Ticket System',
	'doc_attachment' => 'Document Attachments',
	'customer_groups' => 'Customer Groups',
	'gold_scheme' => 'Gold Scheme',
	'gold_rate' => 'Gold Rate API',
	'ecommerce_integration' => 'E-Commerce Integration',
	'data_migration' => 'Data Migration',
	'crm_integration' => 'CRM Integration',
	'report_scheduler' => 'Report Scheduler',
	'aml_compliance' => 'AML Compliance',
	'jewellery_tag' => 'Jewellery TAG',
	'tourist_refund' => 'Tourist Refund',
	'card_reader' => 'Card Reader',
	'shortcut_icons' => 'Shortcut Icons',
	'drilldown' => 'Report Drill-Down',
	'barcode_purchase' => 'Barcode Purchase',
	'fix_unfix' => 'Fix/Unfix Trading',
	'virtual_warehouse' => 'Virtual Warehouse',
	'rfid' => 'RFID System',
	'inventory_report' => 'Inventory Report',
	'landed_cost_v2' => 'Landed Cost V2',
);

?>
<style>
.sep-fleet{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}
.sep-hero{background:linear-gradient(135deg,#0f172a 0%,#1e1b4b 50%,#0f172a 100%);padding:32px;border-radius:16px;margin-bottom:24px;position:relative;overflow:hidden}
.sep-hero::before{content:'';position:absolute;inset:0;background:radial-gradient(circle at 30% 50%,rgba(16,185,129,.15),transparent 40%),radial-gradient(circle at 70% 30%,rgba(139,92,246,.12),transparent 40%);pointer-events:none}
.sep-hero::after{content:'';position:absolute;inset:0;background:url("data:image/svg+xml,%3Csvg width='40' height='40' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M0 0h40v40H0z' fill='none'/%3E%3Ccircle cx='20' cy='20' r='0.5' fill='white' opacity='0.1'/%3E%3C/svg%3E");animation:drift 20s linear infinite}
@keyframes drift{from{transform:translateX(0)}to{transform:translateX(-40px)}}
.sep-hero h1{color:#fff;font-size:28px;font-weight:800;margin:0 0 8px;position:relative;z-index:1}
.sep-hero p{color:#94a3b8;font-size:14px;margin:0;position:relative;z-index:1}
.sep-hero .bos-link{display:inline-flex;align-items:center;gap:8px;margin-top:12px;padding:8px 16px;background:rgba(220,38,38,.15);border:1px solid rgba(220,38,38,.4);border-radius:8px;color:#f87171;font-size:12px;font-weight:600;text-decoration:none;transition:all .2s;position:relative;z-index:1}
.sep-hero .bos-link:hover{background:rgba(220,38,38,.25);transform:translateY(-1px)}
.sep-stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:14px;margin-bottom:24px}
.sep-stat{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:18px;text-align:center;transition:all .2s}
.sep-stat:hover{transform:translateY(-2px);box-shadow:0 4px 12px rgba(0,0,0,.05)}
.sep-stat__val{font-size:28px;font-weight:800;background:linear-gradient(135deg,#10b981,#3b82f6);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.sep-stat__label{font-size:11px;color:#64748b;margin-top:2px}
.sep-tabs{display:flex;gap:4px;margin-bottom:20px;border-bottom:2px solid #e2e8f0;padding-bottom:0}
.sep-tab{padding:10px 20px;font-size:13px;font-weight:600;color:#64748b;cursor:pointer;border-bottom:2px solid transparent;margin-bottom:-2px;transition:all .2s}
.sep-tab:hover{color:#1e293b}
.sep-tab--active{color:#3b82f6;border-bottom-color:#3b82f6}
.sep-panel{display:none}
.sep-panel--active{display:block}
.sep-erp-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:16px}
.sep-erp-card{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:18px;transition:all .2s;animation:fadeUp .4s ease-out both}
.sep-erp-card:hover{border-color:#10b981;box-shadow:0 4px 16px rgba(16,185,129,.1)}
.sep-erp-card__head{display:flex;align-items:center;gap:12px;margin-bottom:14px}
.sep-erp-card__icon{width:44px;height:44px;border-radius:10px;display:flex;align-items:center;justify-content:center;color:#fff;font-size:18px}
.sep-erp-card__title{font-size:15px;font-weight:700;color:#1e293b}
.sep-erp-card__subtitle{font-size:11px;color:#64748b}
.sep-erp-card__modules{display:flex;flex-wrap:wrap;gap:4px;margin-bottom:14px}
.sep-erp-card__module{padding:3px 8px;border-radius:4px;font-size:9px;font-weight:600;background:#ecfdf5;color:#059669}
.sep-erp-card__actions{display:flex;gap:6px;flex-wrap:wrap}
.sep-erp-card__btn{padding:7px 14px;border-radius:6px;font-size:11px;font-weight:600;text-decoration:none;color:#fff;display:inline-flex;align-items:center;gap:5px;transition:all .15s}
.sep-erp-card__btn:hover{transform:scale(1.03);text-decoration:none}
.sep-erp-card__btn--erp{background:#059669}
.sep-erp-card__btn--cp{background:#7c3aed}
.sep-erp-card__btn--bos{background:#dc2626}
.sep-module-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(250px,1fr));gap:12px}
.sep-module-card{padding:16px;background:#fff;border:1px solid #e2e8f0;border-radius:10px;display:flex;align-items:center;gap:12px;transition:all .2s}
.sep-module-card:hover{border-color:#3b82f6;box-shadow:0 2px 8px rgba(59,130,246,.08)}
.sep-module-card__icon{width:36px;height:36px;border-radius:8px;background:#eff6ff;display:flex;align-items:center;justify-content:center}
.sep-module-card__icon i{color:#3b82f6;font-size:14px}
.sep-module-card__info{flex:1}
.sep-module-card__name{font-size:13px;font-weight:600;color:#1e293b}
.sep-module-card__status{font-size:11px;color:#059669}
.sep-bos-section{padding:24px;background:linear-gradient(135deg,#fef2f2,#fee2e2);border:1px solid #fecaca;border-radius:12px;margin:24px 0}
.sep-bos-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:12px;margin-top:16px}
.sep-bos-action{padding:14px;background:#fff;border:1px solid #fecaca;border-radius:8px;text-align:center;text-decoration:none;color:#1e293b;transition:all .2s;display:block}
.sep-bos-action:hover{border-color:#dc2626;box-shadow:0 2px 8px rgba(220,38,38,.1);transform:translateY(-2px);text-decoration:none}
.sep-bos-action i{font-size:20px;color:#dc2626;display:block;margin-bottom:6px}
.sep-bos-action__title{font-size:12px;font-weight:600}
.sep-bos-action__desc{font-size:10px;color:#64748b;margin-top:2px}
@keyframes fadeUp{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:translateY(0)}}
</style>

<div class="sep-fleet">
	<div class="sep-hero">
		<h1><i class="fa fa-calculator"></i> Super ERP — Fleet Command Center</h1>
		<p>All ERP instances across <?php echo $totalIndustries; ?> industries. Full visibility, module status, and BOS fleet control.</p>
		<a href="/bos/" class="bos-link"><i class="fa fa-server"></i> Open BOS — Full Fleet Control</a>
	</div>

	<!-- Metrics -->
	<div class="sep-stats">
		<div class="sep-stat">
			<div class="sep-stat__val"><?php echo $totalERPs; ?></div>
			<div class="sep-stat__label">ERP Instances</div>
		</div>
		<div class="sep-stat">
			<div class="sep-stat__val"><?php echo count($erpModules); ?></div>
			<div class="sep-stat__label">ERP Modules</div>
		</div>
		<div class="sep-stat">
			<div class="sep-stat__val"><?php echo $totalIndustries; ?></div>
			<div class="sep-stat__label">Industry Groups</div>
		</div>
		<div class="sep-stat">
			<div class="sep-stat__val">5</div>
			<div class="sep-stat__label">Live Tenants</div>
		</div>
		<div class="sep-stat">
			<div class="sep-stat__val">22</div>
			<div class="sep-stat__label">Feature Modules</div>
		</div>
	</div>

	<!-- Tabs -->
	<div class="sep-tabs">
		<div class="sep-tab sep-tab--active" onclick="sepSwitchTab('instances')">ERP Instances</div>
		<div class="sep-tab" onclick="sepSwitchTab('modules')">Modules (22)</div>
		<div class="sep-tab" onclick="sepSwitchTab('bos')">BOS Control</div>
	</div>

	<!-- Panel: ERP Instances -->
	<div class="sep-panel sep-panel--active" id="sepPanelInstances">
		<div class="sep-erp-grid">
			<?php
			// Show live tenants first
			$liveShown = array(
				array('name' => 'eParts Cart', 'domain' => 'epartscart.com', 'key' => 'epartscart', 'icon' => 'fa-car', 'color' => '#dc2626', 'industry' => 'Automotive'),
				array('name' => 'Electronicae', 'domain' => 'electronicae.com', 'key' => 'electronicae', 'icon' => 'fa-microchip', 'color' => '#1e40af', 'industry' => 'Electronics'),
				array('name' => 'Style N Look', 'domain' => 'stylenlook.com', 'key' => 'stylenlook', 'icon' => 'fa-shopping-bag', 'color' => '#9333ea', 'industry' => 'Fashion'),
				array('name' => 'The Jewellery Trend', 'domain' => 'thejewellerytrend.com', 'key' => 'thejewellerytrend', 'icon' => 'fa-diamond', 'color' => '#b45309', 'industry' => 'Jewellery'),
				array('name' => 'Taxofinca', 'domain' => 'taxofinca.com', 'key' => 'taxofinca', 'icon' => 'fa-briefcase', 'color' => '#0369a1', 'industry' => 'Professional Services'),
			);
			foreach ($liveShown as $idx => $live) { ?>
			<div class="sep-erp-card" style="animation-delay:<?php echo ($idx * 0.06); ?>s">
				<div class="sep-erp-card__head">
					<div class="sep-erp-card__icon" style="background:<?php echo epc_scp_h($live['color']); ?>"><i class="fa <?php echo epc_scp_h($live['icon']); ?>"></i></div>
					<div>
						<div class="sep-erp-card__title"><?php echo epc_scp_h($live['name']); ?></div>
						<div class="sep-erp-card__subtitle"><?php echo epc_scp_h($live['domain']); ?> • <?php echo epc_scp_h($live['industry']); ?></div>
					</div>
				</div>
				<div class="sep-erp-card__modules">
					<span class="sep-erp-card__module">SLA</span>
					<span class="sep-erp-card__module">Tickets</span>
					<span class="sep-erp-card__module">Reports</span>
					<span class="sep-erp-card__module">+19 more</span>
				</div>
				<div class="sep-erp-card__actions">
					<a href="<?php echo $base; ?>/<?php echo epc_scp_h($live['key']); ?>/shop/finance/erp" class="sep-erp-card__btn sep-erp-card__btn--erp"><i class="fa fa-calculator"></i> Open ERP</a>
					<a href="<?php echo $base; ?>/<?php echo epc_scp_h($live['key']); ?>/" class="sep-erp-card__btn sep-erp-card__btn--cp"><i class="fa fa-th-large"></i> CP</a>
				</div>
			</div>
			<?php } ?>

			<!-- Demo instances per industry group -->
			<?php $dIdx = count($liveShown); foreach ($industryGroups as $gk => $ginfo) {
				$primary = $ginfo['color_scheme']['primary'] ?? '#3b82f6';
				?>
			<div class="sep-erp-card" style="animation-delay:<?php echo ($dIdx++ * 0.04); ?>s;border-style:dashed;opacity:.85">
				<div class="sep-erp-card__head">
					<div class="sep-erp-card__icon" style="background:<?php echo epc_scp_h($primary); ?>"><i class="fa <?php echo epc_scp_h($ginfo['icon']); ?>"></i></div>
					<div>
						<div class="sep-erp-card__title">Demo: <?php echo epc_scp_h($ginfo['label']); ?></div>
						<div class="sep-erp-card__subtitle"><?php echo epc_scp_h($gk); ?>.ecomae.com • Demo environment</div>
					</div>
				</div>
				<div class="sep-erp-card__modules">
					<?php $mods = array_slice(array_values($erpModules), 0, 4);
					foreach ($mods as $m) { ?>
					<span class="sep-erp-card__module"><?php echo epc_scp_h($m); ?></span>
					<?php } ?>
				</div>
				<div class="sep-erp-card__actions">
					<a href="<?php echo $base; ?>/demo/<?php echo epc_scp_h($gk); ?>/shop/finance/erp" class="sep-erp-card__btn sep-erp-card__btn--erp"><i class="fa fa-calculator"></i> Demo ERP</a>
					<a href="<?php echo $base; ?>/demo/<?php echo epc_scp_h($gk); ?>/" class="sep-erp-card__btn sep-erp-card__btn--cp"><i class="fa fa-th-large"></i> Demo CP</a>
				</div>
			</div>
			<?php } ?>
		</div>
	</div>

	<!-- Panel: Modules -->
	<div class="sep-panel" id="sepPanelModules">
		<p style="margin:0 0 16px;color:#64748b;font-size:13px">All 22 ERP feature modules available across every industry tenant. Each module auto-creates its schema on first access.</p>
		<div class="sep-module-grid">
			<?php foreach ($erpModules as $modKey => $modLabel) { ?>
			<div class="sep-module-card">
				<div class="sep-module-card__icon"><i class="fa fa-check-circle"></i></div>
				<div class="sep-module-card__info">
					<div class="sep-module-card__name"><?php echo epc_scp_h($modLabel); ?></div>
					<div class="sep-module-card__status">Active — All tenants</div>
				</div>
			</div>
			<?php } ?>
		</div>
	</div>

	<!-- Panel: BOS Control -->
	<div class="sep-panel" id="sepPanelBos">
		<div class="sep-bos-section">
			<h3 style="margin:0 0 4px;font-size:18px;color:#991b1b"><i class="fa fa-server"></i> Business Operating System — Full Fleet Control</h3>
			<p style="margin:0;font-size:13px;color:#7f1d1d">Platform-wide administration for all tenants, ERP instances, and infrastructure.</p>
			<div class="sep-bos-grid">
				<a href="/bos/" class="sep-bos-action">
					<i class="fa fa-dashboard"></i>
					<div class="sep-bos-action__title">BOS Dashboard</div>
					<div class="sep-bos-action__desc">Fleet overview and health metrics</div>
				</a>
				<a href="/bos/#tenants" class="sep-bos-action">
					<i class="fa fa-users"></i>
					<div class="sep-bos-action__title">Tenant Management</div>
					<div class="sep-bos-action__desc">Add, suspend, configure tenants</div>
				</a>
				<a href="/bos/#modules" class="sep-bos-action">
					<i class="fa fa-th-large"></i>
					<div class="sep-bos-action__title">Module Registry</div>
					<div class="sep-bos-action__desc">Enable/disable modules per tenant</div>
				</a>
				<a href="/bos/#billing" class="sep-bos-action">
					<i class="fa fa-credit-card"></i>
					<div class="sep-bos-action__title">Subscription Billing</div>
					<div class="sep-bos-action__desc">Plans, invoices, MRR tracking</div>
				</a>
				<a href="/bos/#health" class="sep-bos-action">
					<i class="fa fa-heartbeat"></i>
					<div class="sep-bos-action__title">Health Monitor</div>
					<div class="sep-bos-action__desc">Uptime, errors, performance</div>
				</a>
				<a href="/bos/#backup" class="sep-bos-action">
					<i class="fa fa-database"></i>
					<div class="sep-bos-action__title">Backup & Recovery</div>
					<div class="sep-bos-action__desc">Daily backups, point-in-time restore</div>
				</a>
				<a href="/bos/#security" class="sep-bos-action">
					<i class="fa fa-shield"></i>
					<div class="sep-bos-action__title">Security Center</div>
					<div class="sep-bos-action__desc">MFA, audit logs, isolation checks</div>
				</a>
				<a href="/bos/#deployment" class="sep-bos-action">
					<i class="fa fa-rocket"></i>
					<div class="sep-bos-action__title">Deployment</div>
					<div class="sep-bos-action__desc">Release management, rollbacks</div>
				</a>
			</div>
		</div>
	</div>

	<!-- Demo Credentials -->
	<div style="margin:24px 0;padding:20px;background:#f0fdf4;border:1px solid #bbf7d0;border-radius:10px">
		<h4 style="margin:0 0 6px;font-size:14px;color:#166534"><i class="fa fa-key"></i> Demo Access</h4>
		<p style="margin:0;font-size:12px;color:#15803d">Email: <code>demo@ecomae.com</code> | Password: <code>demo2026</code> — Works for all demo ERP instances above.</p>
	</div>
</div>

<script>
function sepSwitchTab(panel) {
	document.querySelectorAll('.sep-tab').forEach(function(t) { t.classList.remove('sep-tab--active'); });
	document.querySelectorAll('.sep-panel').forEach(function(p) { p.classList.remove('sep-panel--active'); });
	event.target.classList.add('sep-tab--active');
	var el = document.getElementById('sepPanel' + panel.charAt(0).toUpperCase() + panel.slice(1));
	if (el) el.classList.add('sep-panel--active');
}
</script>
