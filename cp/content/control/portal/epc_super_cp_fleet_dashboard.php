<?php
/**
 * Super CP Fleet Dashboard — View all CP instances across all industries.
 * Provides fleet-wide visibility into tenant CPs, industry coverage, and operational metrics.
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

// Get all registered tenants
$tenants = array();
try {
	$pdo = epc_portal_pdo();
	$stmt = $pdo->query("SELECT * FROM epc_portal_tenants WHERE active = 1 ORDER BY company_name");
	$tenants = $stmt->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$tenants = array();
}

$totalTenants = count($tenants);
$totalIndustries = count($industryGroups);
$activeNow = 0;
$tenantsByIndustry = array();

foreach ($tenants as $t) {
	$ind = $t['industry_code'] ?? 'general';
	if (!isset($tenantsByIndustry[$ind])) {
		$tenantsByIndustry[$ind] = array();
	}
	$tenantsByIndustry[$ind][] = $t;
	if (!empty($t['last_activity']) && (time() - strtotime($t['last_activity'])) < 3600) {
		$activeNow++;
	}
}

?>
<style>
.scp-fleet{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}
.scp-fleet-hero{background:linear-gradient(135deg,#0f172a,#1e293b);padding:32px;border-radius:16px;margin-bottom:24px;position:relative;overflow:hidden}
.scp-fleet-hero::after{content:'';position:absolute;inset:0;background:radial-gradient(circle at 80% 20%,rgba(99,102,241,.15),transparent 50%),radial-gradient(circle at 20% 80%,rgba(56,189,248,.1),transparent 50%);pointer-events:none}
.scp-fleet-hero h1{color:#fff;font-size:28px;font-weight:800;margin:0 0 8px;position:relative;z-index:1}
.scp-fleet-hero p{color:#94a3b8;font-size:14px;margin:0;position:relative;z-index:1}
.scp-stats-row{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:16px;margin-bottom:24px}
.scp-stat-card{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:20px;text-align:center;transition:all .2s}
.scp-stat-card:hover{transform:translateY(-2px);box-shadow:0 4px 12px rgba(0,0,0,.05)}
.scp-stat-card__val{font-size:32px;font-weight:800;color:#3b82f6}
@supports ((-webkit-background-clip: text) or (background-clip: text)) {
.scp-stat-card__val{background:linear-gradient(135deg,#3b82f6,#8b5cf6);-webkit-background-clip:text;background-clip:text;-webkit-text-fill-color:transparent;color:transparent}
}
.scp-stat-card__label{font-size:12px;color:#64748b;margin-top:4px}
.scp-fleet-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:16px;margin:24px 0}
.scp-tenant-card{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:16px;transition:all .2s}
.scp-tenant-card:hover{border-color:#3b82f6;box-shadow:0 4px 16px rgba(59,130,246,.1)}
.scp-tenant-card__head{display:flex;align-items:center;gap:12px;margin-bottom:12px}
.scp-tenant-card__icon{width:40px;height:40px;border-radius:10px;display:flex;align-items:center;justify-content:center;color:#fff;font-size:16px}
.scp-tenant-card__name{font-size:14px;font-weight:700;color:#1e293b}
.scp-tenant-card__industry{font-size:11px;color:#64748b}
.scp-tenant-card__meta{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px}
.scp-tenant-card__badge{padding:3px 8px;border-radius:4px;font-size:10px;font-weight:600}
.scp-tenant-card__actions{display:flex;gap:6px;flex-wrap:wrap}
.scp-tenant-card__btn{padding:6px 12px;border-radius:6px;font-size:11px;font-weight:600;text-decoration:none;color:#fff;transition:all .15s;display:inline-flex;align-items:center;gap:4px}
.scp-tenant-card__btn:hover{transform:scale(1.03);text-decoration:none}
.scp-tenant-card__btn--cp{background:#7c3aed}
.scp-tenant-card__btn--erp{background:#059669}
.scp-tenant-card__btn--site{background:#0369a1}
.scp-tenant-card__btn--bos{background:#dc2626}
.scp-industry-section{margin:32px 0}
.scp-industry-section h3{font-size:16px;font-weight:700;color:#1e293b;margin:0 0 12px;display:flex;align-items:center;gap:8px}
.scp-industry-section h3 .count{background:#f1f5f9;padding:2px 8px;border-radius:4px;font-size:11px;color:#64748b;font-weight:400}
.scp-search{margin-bottom:24px}
.scp-search input{width:100%;max-width:500px;padding:12px 16px 12px 42px;border:1px solid #e2e8f0;border-radius:10px;font-size:14px;outline:none;transition:border-color .2s}
.scp-search input:focus{border-color:#3b82f6}
.scp-search{position:relative}
.scp-search i{position:absolute;left:14px;top:50%;transform:translateY(-50%);color:#94a3b8}
@keyframes fadeUp{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:translateY(0)}}
.scp-tenant-card{animation:fadeUp .4s ease-out both}
</style>

<div class="scp-fleet">
	<div class="scp-fleet-hero">
		<h1><i class="fa fa-th-large"></i> Super CP — Fleet Dashboard</h1>
		<p>All Control Panel instances across <?php echo $totalIndustries; ?> industries. Manage, monitor, and access any tenant CP from one place.</p>
	</div>

	<!-- Fleet Metrics -->
	<div class="scp-stats-row">
		<div class="scp-stat-card">
			<div class="scp-stat-card__val"><?php echo $totalTenants; ?></div>
			<div class="scp-stat-card__label">Total CP Instances</div>
		</div>
		<div class="scp-stat-card">
			<div class="scp-stat-card__val"><?php echo $totalIndustries; ?></div>
			<div class="scp-stat-card__label">Industry Groups</div>
		</div>
		<div class="scp-stat-card">
			<div class="scp-stat-card__val"><?php echo count($tenantsByIndustry); ?></div>
			<div class="scp-stat-card__label">Active Industries</div>
		</div>
		<div class="scp-stat-card">
			<div class="scp-stat-card__val"><?php echo $activeNow; ?></div>
			<div class="scp-stat-card__label">Active Now</div>
		</div>
	</div>

	<!-- Search -->
	<div class="scp-search">
		<i class="fa fa-search"></i>
		<input type="text" id="scpFleetSearch" placeholder="Search tenants by name, industry, or domain..." onkeyup="scpFilterFleet(this.value)" />
	</div>

	<!-- Industry Group Cards (all 28) -->
	<?php foreach ($industryGroups as $gk => $ginfo) {
		$groupTenants = $tenantsByIndustry[$gk] ?? array();
		$primary = $ginfo['color_scheme']['primary'] ?? '#3b82f6';
		?>
	<div class="scp-industry-section" data-industry="<?php echo epc_scp_h($gk); ?>">
		<h3>
			<span style="width:28px;height:28px;border-radius:6px;background:<?php echo epc_scp_h($primary); ?>;display:inline-flex;align-items:center;justify-content:center"><i class="fa <?php echo epc_scp_h($ginfo['icon']); ?>" style="color:#fff;font-size:12px"></i></span>
			<?php echo epc_scp_h($ginfo['label']); ?>
			<span class="count"><?php echo count($groupTenants); ?> tenant<?php echo count($groupTenants) !== 1 ? 's' : ''; ?></span>
		</h3>
		<?php if (empty($groupTenants)) { ?>
		<div class="scp-fleet-grid">
			<div class="scp-tenant-card" style="border-style:dashed;opacity:.7">
				<div class="scp-tenant-card__head">
					<div class="scp-tenant-card__icon" style="background:<?php echo epc_scp_h($primary); ?>"><i class="fa <?php echo epc_scp_h($ginfo['icon']); ?>"></i></div>
					<div>
						<div class="scp-tenant-card__name" style="color:#64748b">Demo: <?php echo epc_scp_h($ginfo['label']); ?></div>
						<div class="scp-tenant-card__industry">Try the demo for this industry</div>
					</div>
				</div>
				<div class="scp-tenant-card__actions">
					<a href="<?php echo $base; ?>/demo/<?php echo epc_scp_h($gk); ?>/" class="scp-tenant-card__btn scp-tenant-card__btn--cp"><i class="fa fa-th-large"></i> Demo CP</a>
					<a href="<?php echo $base; ?>/demo/<?php echo epc_scp_h($gk); ?>/shop/finance/erp" class="scp-tenant-card__btn scp-tenant-card__btn--erp"><i class="fa fa-calculator"></i> Demo ERP</a>
					<a href="https://<?php echo epc_scp_h($gk); ?>.ecomae.com" class="scp-tenant-card__btn scp-tenant-card__btn--site"><i class="fa fa-globe"></i> Site</a>
				</div>
			</div>
		</div>
		<?php } else { ?>
		<div class="scp-fleet-grid">
			<?php foreach ($groupTenants as $idx => $t) {
				$tName = $t['company_name'] ?? $t['site_key'] ?? 'Tenant';
				$tDomain = $t['domain'] ?? '';
				$tSiteKey = $t['site_key'] ?? '';
				?>
			<div class="scp-tenant-card" data-name="<?php echo epc_scp_h(strtolower($tName . ' ' . $tDomain)); ?>" style="animation-delay:<?php echo ($idx * 0.05); ?>s">
				<div class="scp-tenant-card__head">
					<div class="scp-tenant-card__icon" style="background:<?php echo epc_scp_h($primary); ?>"><i class="fa <?php echo epc_scp_h($ginfo['icon']); ?>"></i></div>
					<div>
						<div class="scp-tenant-card__name"><?php echo epc_scp_h($tName); ?></div>
						<div class="scp-tenant-card__industry"><?php echo epc_scp_h($tDomain ?: $tSiteKey); ?></div>
					</div>
				</div>
				<div class="scp-tenant-card__meta">
					<span class="scp-tenant-card__badge" style="background:#dcfce7;color:#166534">Active</span>
					<span class="scp-tenant-card__badge" style="background:#e0e7ff;color:#3730a3"><?php echo epc_scp_h($ginfo['label']); ?></span>
				</div>
				<div class="scp-tenant-card__actions">
					<a href="<?php echo $base; ?>/<?php echo epc_scp_h($tSiteKey); ?>/" class="scp-tenant-card__btn scp-tenant-card__btn--cp"><i class="fa fa-th-large"></i> CP</a>
					<a href="<?php echo $base; ?>/<?php echo epc_scp_h($tSiteKey); ?>/shop/finance/erp" class="scp-tenant-card__btn scp-tenant-card__btn--erp"><i class="fa fa-calculator"></i> ERP</a>
					<?php if ($tDomain) { ?>
					<a href="https://<?php echo epc_scp_h($tDomain); ?>" class="scp-tenant-card__btn scp-tenant-card__btn--site" target="_blank"><i class="fa fa-globe"></i> Site</a>
					<?php } ?>
				</div>
			</div>
			<?php } ?>
		</div>
		<?php } ?>
	</div>
	<?php } ?>

	<!-- Demo Credentials -->
	<div style="margin:40px 0;padding:24px;background:linear-gradient(135deg,#f0f9ff,#ede9fe);border:1px solid #c7d2fe;border-radius:12px">
		<h3 style="margin:0 0 8px;font-size:16px;color:#1e293b"><i class="fa fa-key" style="color:#6366f1"></i> Public Demo Credentials</h3>
		<p style="margin:0;font-size:13px;color:#475569">Email: <code style="background:#e0e7ff;padding:2px 6px;border-radius:3px">demo@ecomae.com</code> &nbsp;|&nbsp; Password: <code style="background:#e0e7ff;padding:2px 6px;border-radius:3px">demo2026</code></p>
		<p style="margin:8px 0 0;font-size:12px;color:#64748b">Use these credentials to access any industry demo CP or ERP above.</p>
	</div>
</div>

<script>
function scpFilterFleet(q) {
	q = q.toLowerCase().trim();
	document.querySelectorAll('.scp-industry-section').forEach(function(sec) {
		var cards = sec.querySelectorAll('.scp-tenant-card');
		var anyVisible = false;
		cards.forEach(function(card) {
			var text = (card.getAttribute('data-name') || '') + ' ' + card.textContent.toLowerCase();
			var show = !q || text.indexOf(q) !== -1;
			card.style.display = show ? '' : 'none';
			if (show) anyVisible = true;
		});
		var sectionText = sec.textContent.toLowerCase();
		sec.style.display = (!q || anyVisible || sectionText.indexOf(q) !== -1) ? '' : 'none';
	});
}
</script>
