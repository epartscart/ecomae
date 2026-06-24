<?php
/**
 * Frontend ERP portal — separate login for finance/ERP team (no CP access required).
 * URL: /shop/erp (registered via epc-erp-frontend-setup.php)
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';

global $DP_Config;

$user_id = (int)DP_User::getUserId();
$logged_in = $user_id > 0;
$has_access = false;
if ($logged_in && isset($db_link) && $db_link instanceof PDO) {
	$has_access = epc_erp_user_can_access($db_link);
}

$fe = epc_erp_frontend_urls();
$lang = epc_erp_lang_href();
$portal_home = ($lang !== '' ? $lang : '') . '/shop/erp';
$portal_guide = ($lang !== '' ? $lang : '') . '/shop/erp/guide';
?>
<style>
.epc-erp-portal-wrap { margin: 0 0 32px; }
.epc-erp-portal-wrap .hpanel { background: #fff; border: 1px solid #dce4ef; border-radius: 8px; margin-bottom: 18px; }
.epc-erp-portal-wrap .panel-heading { padding: 14px 16px; background: #f5f7fa; border-bottom: 1px solid #dce4ef; font-weight: 700; border-radius: 8px 8px 0 0; }
.epc-erp-portal-wrap .panel-body { padding: 16px; }
.epc-erp-login-panel { background: #fff; border: 1px solid #e1e7ef; border-radius: 10px; padding: 22px; margin-bottom: 20px; }
.epc-erp-login-panel h2 { margin: 0 0 8px; font-size: 22px; color: #172536; }
.epc-erp-login-lead { color: #64748b; line-height: 1.55; margin-bottom: 14px; }
/* BOS-style Matrix particle rain on ERP login */
.epc-erp-login-bg { position: fixed; inset: 0; z-index: 0; overflow: hidden; background: linear-gradient(155deg, #020617 0%, #0f172a 38%, #1e3a8a 72%, #0c4a6e 100%); }
.epc-erp-login-bg__grid { position: absolute; inset: 0; background-image: linear-gradient(rgba(14,165,233,.07) 1px, transparent 1px), linear-gradient(90deg, rgba(14,165,233,.07) 1px, transparent 1px); background-size: 60px 60px; animation: erpGridDrift 20s linear infinite; }
@keyframes erpGridDrift { 0% { transform: translate(0,0); } 100% { transform: translate(60px,60px); } }
.epc-erp-login-bg__particles { position: absolute; inset: 0; }
.epc-erp-login-bg__particle { position: absolute; border-radius: 50%; will-change: transform; }
.epc-erp-login-bg__glow { position: absolute; border-radius: 50%; filter: blur(80px); opacity: .35; }
.epc-erp-login-bg__glow--1 { width: 400px; height: 400px; background: radial-gradient(circle, rgba(14,165,233,.4), transparent 70%); top: -100px; left: 10%; animation: erpGlow1 8s ease-in-out infinite alternate; }
.epc-erp-login-bg__glow--2 { width: 350px; height: 350px; background: radial-gradient(circle, rgba(99,102,241,.35), transparent 70%); bottom: -80px; right: 15%; animation: erpGlow2 10s ease-in-out infinite alternate; }
@keyframes erpGlow1 { 0% { transform: translate(0,0) scale(1); } 100% { transform: translate(40px,30px) scale(1.15); } }
@keyframes erpGlow2 { 0% { transform: translate(0,0) scale(1); } 100% { transform: translate(-30px,-40px) scale(1.2); } }
@keyframes erpFloat { 0% { transform: translateY(-10vh); opacity: 0; } 5% { opacity: 1; } 50% { opacity: .9; } 100% { transform: translateY(105vh); opacity: 0; } }
@keyframes erpFloatDrift { 0% { transform: translateY(-10vh) translateX(0); opacity: 0; } 8% { opacity: 1; } 25% { transform: translateY(20vh) translateX(15px); } 50% { transform: translateY(50vh) translateX(-10px); opacity: .8; } 75% { transform: translateY(75vh) translateX(20px); } 100% { transform: translateY(110vh) translateX(-5px); opacity: 0; } }
@keyframes erpFloatStreak { 0% { transform: translateY(-5vh) scaleY(1); opacity: 0; } 5% { opacity: 1; } 50% { transform: translateY(50vh) scaleY(3); opacity: .7; } 100% { transform: translateY(110vh) scaleY(1); opacity: 0; } }
@media (prefers-reduced-motion: reduce) { .epc-erp-login-bg__particle, .epc-erp-login-bg__grid, .epc-erp-login-bg__glow { animation: none !important; } }
.epc-erp-login-wrap--matrix { position: relative; z-index: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; min-height: 100vh; padding: 28px 20px 40px; }
.epc-erp-login-card-head { text-align: center; margin-bottom: 18px; color: #fff; }
.epc-erp-login-card-head__badge { display: inline-flex; align-items: center; gap: 6px; background: rgba(16,185,129,.15); border: 1px solid rgba(52,211,153,.4); border-radius: 999px; padding: 6px 14px; font-size: 10px; font-weight: 800; letter-spacing: .1em; text-transform: uppercase; color: #6ee7b7; margin-bottom: 12px; }
.epc-erp-login-card-head h1 { margin: 0 0 6px; font-size: clamp(22px,4vw,28px); font-weight: 800; color: #fff; letter-spacing: -.03em; }
.epc-erp-login-card-head p { margin: 0; font-size: 14px; color: rgba(226,232,240,.9); }
.epc-erp-login-wrap--matrix .epc-erp-login-panel { max-width: 440px; width: 100%; background: transparent; border: 0; padding: 0; margin: 0; }
.epc-erp-login-wrap--matrix .epc-erp-login-panel h2 { display: none; }
.epc-erp-login-wrap--matrix .epc-erp-login-lead { display: none; }
.epc-erp-login-wrap--matrix .panel { border-radius: 18px; border: 1px solid rgba(226,232,240,.9); box-shadow: 0 24px 48px rgba(2,6,23,.35), 0 0 0 1px rgba(255,255,255,.04); }
.epc-erp-login-wrap--matrix .panel .panel-body { padding: 32px 28px 28px; }
.epc-erp-login-wrap--matrix .btn-success, .epc-erp-login-wrap--matrix .btn-primary { background: linear-gradient(135deg, #0ea5e9 0%, #2563eb 100%); border: none; box-shadow: 0 4px 14px rgba(14,165,233,.35); }
.epc-erp-login-wrap--matrix .btn-success:hover, .epc-erp-login-wrap--matrix .btn-primary:hover { background: linear-gradient(135deg, #22d3ee 0%, #0ea5e9 100%); }
.epc-erp-login-features--card { list-style: none; padding: 0; margin: 20px 0 0; display: grid; grid-template-columns: 1fr; gap: 8px; }
.epc-erp-login-features--card li { display: flex; align-items: center; gap: 10px; font-size: 13px; font-weight: 500; color: #64748b; }
.epc-erp-login-features--card li i { width: 28px; height: 28px; font-size: 12px; display: flex; align-items: center; justify-content: center; background: #e0f2fe; color: #0369a1; border-radius: 8px; }
</style>

<div class="epc-erp-portal-wrap">
<?php if (!$logged_in): ?>
	<div class="epc-erp-login-bg" id="erpLoginBg">
		<div class="epc-erp-login-bg__grid"></div>
		<div class="epc-erp-login-bg__particles" id="erpParticles"></div>
		<div class="epc-erp-login-bg__glow epc-erp-login-bg__glow--1"></div>
		<div class="epc-erp-login-bg__glow epc-erp-login-bg__glow--2"></div>
	</div>
	<div class="epc-erp-login-wrap--matrix">
		<div class="epc-erp-login-card-head">
			<span class="epc-erp-login-card-head__badge"><i class="fa fa-calculator"></i> ERP Finance</span>
			<h1>Department sign-in</h1>
			<p>Sales, Logistics, Finance, Purchase, Accounts, Marketing, HR, Admin</p>
		</div>
		<div class="epc-erp-login-panel">
			<h2><i class="fa fa-lock"></i> ERP Finance — sign in</h2>
			<p class="epc-erp-login-lead">
				Sign in with your department ERP account. Each role sees only their workflow tabs.
			</p>
			<div class="panel panel-primary">
			<?php
			$login_form_postfix = 'erp_finance';
			require $_SERVER['DOCUMENT_ROOT'] . '/modules/login/login_form_general.php';
			?>
			</div>
			<ul class="epc-erp-login-features--card">
				<li><i class="fa fa-line-chart"></i> Revenue, purchases, cash &amp; bank</li>
				<li><i class="fa fa-file-text-o"></i> E-invoicing, VAT returns, P&amp;L</li>
				<li><i class="fa fa-users"></i> HR, payroll, time &amp; attendance</li>
				<li><i class="fa fa-cubes"></i> Inventory, warehousing, fixed assets</li>
			</ul>
		</div>
	</div>
<?php elseif (!$has_access): ?>
	<div class="alert alert-warning">
		<strong>Access denied.</strong> Your account is signed in but does not have ERP Finance access.
		Site administrators and CP backend staff can open ERP here automatically; finance-only staff need the ERP team group
		(<code>epc-erp-frontend-setup.php?token=...&amp;email=YOUR@EMAIL</code>).
		You can also use the control panel:
		<a href="/<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/shop/finance/erp">CP ERP Finance</a>.
	</div>
<?php else: ?>
	<?php
	$user_session = epc_erp_resolve_user_session();
	extract(epc_erp_configure_portal_urls('frontend'));
	$erp_include = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/finance/erp/erp_main.php';
	if (!is_file($erp_include)) {
		echo '<div class="alert alert-danger">ERP module files not found on server.</div>';
	} else {
		include $erp_include;
	}
	?>
<?php endif; ?>
</div>
<script>
(function(){
	function epcErpHidePreloader(){
		var pre = document.getElementById('preloader');
		var stat = document.getElementById('status');
		if (pre) { pre.style.display = 'none'; }
		if (stat) { stat.style.display = 'none'; }
		if (document.body) { document.body.style.overflow = 'visible'; }
	}
	epcErpHidePreloader();
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', epcErpHidePreloader);
	}
	setTimeout(epcErpHidePreloader, 200);

	/* BOS-style Matrix particle rain */
	var container = document.getElementById('erpParticles');
	if (!container) return;
	var colors = [
		'rgba(14, 165, 233, .7)', 'rgba(14, 165, 233, .35)',
		'rgba(56, 189, 248, .7)', 'rgba(56, 189, 248, .4)',
		'rgba(99, 102, 241, .6)', 'rgba(99, 102, 241, .3)',
		'rgba(168, 85, 247, .5)', 'rgba(16, 185, 129, .4)',
		'rgba(255, 255, 255, .4)', 'rgba(255, 255, 255, .15)'
	];
	var total = 180;
	for (var i = 0; i < total; i++) {
		var p = document.createElement('div');
		p.className = 'epc-erp-login-bg__particle';
		p.style.left = Math.random() * 100 + '%';
		p.style.top = Math.random() * 10 + '%';
		var speed = Math.random();
		var dur;
		if (speed < 0.3) { dur = 2.5 + Math.random() * 3.5; }
		else if (speed < 0.7) { dur = 6 + Math.random() * 6; }
		else { dur = 12 + Math.random() * 14; }
		var sizeRnd = Math.random();
		var size;
		if (sizeRnd < 0.50) { size = 1 + Math.random() * 1.5; }
		else if (sizeRnd < 0.80) { size = 2.5 + Math.random() * 2; }
		else if (sizeRnd < 0.95) { size = 4.5 + Math.random() * 3.5; }
		else { size = 1.5 + Math.random(); }
		p.style.width = size + 'px';
		p.style.height = (sizeRnd >= 0.95 ? size * 4 : size) + 'px';
		if (sizeRnd >= 0.95) { p.style.borderRadius = size + 'px'; }
		var anim;
		if (sizeRnd >= 0.95) { anim = 'erpFloatStreak'; }
		else if (Math.random() < 0.35) { anim = 'erpFloatDrift'; }
		else { anim = 'erpFloat'; }
		p.style.animationName = anim;
		p.style.animationDuration = dur + 's';
		p.style.animationDelay = (Math.random() * dur) + 's';
		p.style.animationTimingFunction = 'linear';
		p.style.animationIterationCount = 'infinite';
		var color = colors[Math.floor(Math.random() * colors.length)];
		p.style.background = color;
		if (size > 4) {
			p.style.boxShadow = '0 0 ' + (size * 3) + 'px ' + color + ', 0 0 ' + (size * 6) + 'px ' + color.replace(/[\d.]+\)$/, '0.15)');
		} else if (size > 2) {
			p.style.boxShadow = '0 0 ' + (size * 2) + 'px ' + color;
		}
		container.appendChild(p);
	}
})();
</script>
