<?php
/**
 * Frontend ERP portal — separate login for finance/ERP team (no CP access required).
 * URL: /shop/erp (registered via epc-erp-frontend-setup.php)
 * BOS-style dark animated theme with particle rain.
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
.epc-erp-portal-wrap{margin:0;}
/* BOS-style Matrix particle rain on ERP login */
.epc-erp-login-bg{position:fixed;inset:0;z-index:0;overflow:hidden;background:linear-gradient(155deg,#020617 0%,#0f172a 38%,#1e3a8a 72%,#0c4a6e 100%);}
.epc-erp-login-bg__grid{position:absolute;inset:0;background-image:linear-gradient(rgba(14,165,233,.07) 1px,transparent 1px),linear-gradient(90deg,rgba(14,165,233,.07) 1px,transparent 1px);background-size:60px 60px;animation:erpGridDrift 20s linear infinite;}
@keyframes erpGridDrift{0%{transform:translate(0,0);}100%{transform:translate(60px,60px);}}
.epc-erp-login-bg__particles{position:absolute;inset:0;}
.epc-erp-login-bg__particle{position:absolute;border-radius:50%;will-change:transform;}
.epc-erp-login-bg__glow{position:absolute;border-radius:50%;filter:blur(80px);opacity:.35;}
.epc-erp-login-bg__glow--1{width:420px;height:420px;background:radial-gradient(circle,rgba(14,165,233,.4),transparent 70%);top:-120px;left:8%;animation:erpGlow1 8s ease-in-out infinite alternate;}
.epc-erp-login-bg__glow--2{width:380px;height:380px;background:radial-gradient(circle,rgba(99,102,241,.35),transparent 70%);bottom:-100px;right:12%;animation:erpGlow2 10s ease-in-out infinite alternate;}
.epc-erp-login-bg__glow--3{width:300px;height:300px;background:radial-gradient(circle,rgba(168,85,247,.3),transparent 70%);top:40%;left:55%;animation:erpGlow3 12s ease-in-out infinite alternate;}
@keyframes erpGlow1{0%{transform:translate(0,0) scale(1);}100%{transform:translate(40px,30px) scale(1.15);}}
@keyframes erpGlow2{0%{transform:translate(0,0) scale(1);}100%{transform:translate(-30px,-40px) scale(1.2);}}
@keyframes erpGlow3{0%{transform:translate(0,0) scale(.9);}100%{transform:translate(20px,-20px) scale(1.1);}}
@keyframes erpFloat{0%{transform:translateY(-10vh);opacity:0;}5%{opacity:1;}50%{opacity:.9;}100%{transform:translateY(105vh);opacity:0;}}
@keyframes erpFloatDrift{0%{transform:translateY(-10vh) translateX(0);opacity:0;}8%{opacity:1;}25%{transform:translateY(20vh) translateX(15px);}50%{transform:translateY(50vh) translateX(-10px);opacity:.8;}75%{transform:translateY(75vh) translateX(20px);}100%{transform:translateY(110vh) translateX(-5px);opacity:0;}}
@keyframes erpFloatStreak{0%{transform:translateY(-5vh) scaleY(1);opacity:0;}5%{opacity:1;}50%{transform:translateY(50vh) scaleY(3);opacity:.7;}100%{transform:translateY(110vh) scaleY(1);opacity:0;}}
@media(prefers-reduced-motion:reduce){.epc-erp-login-bg__particle,.epc-erp-login-bg__grid,.epc-erp-login-bg__glow{animation:none!important;}}
.epc-erp-login-wrap--matrix{position:relative;z-index:1;display:flex;flex-direction:column;align-items:center;justify-content:center;min-height:100vh;padding:28px 20px 40px;}
.epc-erp-login-card-head{text-align:center;margin-bottom:22px;color:#fff;}
.epc-erp-login-card-head__brand-icon{width:64px;height:64px;border-radius:18px;background:linear-gradient(135deg,#0ea5e9 0%,#6366f1 100%);display:inline-flex;align-items:center;justify-content:center;font-size:26px;color:#fff;box-shadow:0 8px 28px rgba(14,165,233,.4),0 0 50px rgba(99,102,241,.15);margin-bottom:14px;}
.epc-erp-login-card-head__badge{display:inline-flex;align-items:center;gap:6px;background:rgba(16,185,129,.15);border:1px solid rgba(52,211,153,.4);border-radius:999px;padding:6px 14px;font-size:10px;font-weight:800;letter-spacing:.1em;text-transform:uppercase;color:#6ee7b7;margin-bottom:12px;}
.epc-erp-login-card-head h1{margin:0 0 6px;font-size:clamp(22px,4vw,28px);font-weight:800;color:#fff;letter-spacing:-.03em;}
.epc-erp-login-card-head p{margin:0;font-size:14px;color:rgba(226,232,240,.9);}
.epc-erp-login-wrap--matrix .epc-erp-login-panel{max-width:440px;width:100%;background:transparent;border:0;padding:0;margin:0;}
.epc-erp-login-wrap--matrix .epc-erp-login-panel h2{display:none;}
.epc-erp-login-wrap--matrix .epc-erp-login-lead{display:none;}
.epc-erp-login-wrap--matrix .panel{border-radius:20px;border:1px solid rgba(99,102,241,.25);background:rgba(15,23,42,.7);backdrop-filter:blur(16px);-webkit-backdrop-filter:blur(16px);box-shadow:0 24px 48px rgba(2,6,23,.5),0 0 0 1px rgba(255,255,255,.04);}
.epc-erp-login-wrap--matrix .panel .panel-heading{font-size:17px;font-weight:700;color:#fff;background:transparent;border:none;padding:20px 28px 0;}
.epc-erp-login-wrap--matrix .panel .panel-body{padding:20px 28px 28px;}
.epc-erp-login-wrap--matrix .panel input[type="email"],
.epc-erp-login-wrap--matrix .panel input[type="password"],
.epc-erp-login-wrap--matrix .panel input[type="text"],
.epc-erp-login-wrap--matrix .panel .form-control{background:rgba(15,23,42,.6);border:1px solid rgba(99,102,241,.3);border-radius:10px;color:#f1f5f9;padding:11px 14px;font-size:14px;box-shadow:none;transition:border-color .2s,box-shadow .2s;}
.epc-erp-login-wrap--matrix .panel input:focus,
.epc-erp-login-wrap--matrix .panel .form-control:focus{border-color:#0ea5e9;box-shadow:0 0 0 3px rgba(14,165,233,.2);background:rgba(15,23,42,.8);outline:none;}
.epc-erp-login-wrap--matrix .panel input::placeholder{color:rgba(148,163,184,.6);}
.epc-erp-login-wrap--matrix .panel .input-group{display:block;margin-bottom:12px;width:100%;border:none;background:none;}
.epc-erp-login-wrap--matrix .panel .input-group .input-group-addon{display:none;}
.epc-erp-login-wrap--matrix .panel label{color:rgba(203,213,225,.9);font-size:13px;}
.epc-erp-login-wrap--matrix .panel .checkbox{color:rgba(203,213,225,.8);}
.epc-erp-login-wrap--matrix .btn-success,.epc-erp-login-wrap--matrix .btn-primary{background:linear-gradient(135deg,#0ea5e9 0%,#2563eb 100%);border:none;box-shadow:0 4px 14px rgba(14,165,233,.35);color:#fff;width:100%;padding:11px 16px;font-weight:700;border-radius:10px;font-size:14.5px;transition:.2s;}
.epc-erp-login-wrap--matrix .btn-success:hover,.epc-erp-login-wrap--matrix .btn-primary:hover{background:linear-gradient(135deg,#38bdf8 0%,#0ea5e9 100%);box-shadow:0 6px 20px rgba(14,165,233,.45);}
.epc-erp-login-wrap--matrix .btn-warning{background:transparent;border:none;color:rgba(203,213,225,.8);box-shadow:none;text-decoration:underline;font-weight:500;font-size:13px;padding:8px 0;width:auto;}
.epc-erp-login-wrap--matrix .btn-warning:hover{color:#38bdf8;}
.epc-erp-login-features--card{list-style:none;padding:0;margin:20px 0 0;display:grid;grid-template-columns:1fr;gap:8px;}
.epc-erp-login-features--card li{display:flex;align-items:center;gap:10px;font-size:13px;font-weight:500;color:rgba(203,213,225,.9);}
.epc-erp-login-features--card li i{width:28px;height:28px;font-size:12px;display:flex;align-items:center;justify-content:center;background:rgba(14,165,233,.15);color:#38bdf8;border-radius:8px;}
</style>

<div class="epc-erp-portal-wrap">
<?php if (!$logged_in): ?>
	<div class="epc-erp-login-bg" id="erpLoginBg">
		<div class="epc-erp-login-bg__grid"></div>
		<div class="epc-erp-login-bg__particles" id="erpParticles"></div>
		<div class="epc-erp-login-bg__glow epc-erp-login-bg__glow--1"></div>
		<div class="epc-erp-login-bg__glow epc-erp-login-bg__glow--2"></div>
		<div class="epc-erp-login-bg__glow epc-erp-login-bg__glow--3"></div>
	</div>
	<div class="epc-erp-login-wrap--matrix">
		<div class="epc-erp-login-card-head">
			<div class="epc-erp-login-card-head__brand-icon"><i class="fa fa-line-chart"></i></div>
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
	<div class="alert alert-warning" style="position:relative;z-index:1;max-width:600px;margin:80px auto;background:rgba(15,23,42,.7);border:1px solid rgba(99,102,241,.25);color:#e2e8f0;backdrop-filter:blur(12px);border-radius:14px;padding:24px;">
		<strong>Access denied.</strong> Your account is signed in but does not have ERP Finance access.
		Site administrators and CP backend staff can open ERP here automatically; finance-only staff need the ERP team group.
		You can also use the control panel:
		<a href="/<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/shop/finance/erp" style="color:#38bdf8;">CP ERP Finance</a>.
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
	var anims = ['erpFloat', 'erpFloatDrift', 'erpFloatStreak'];
	var total = 180;
	for (var i = 0; i < total; i++) {
		var p = document.createElement('div');
		p.className = 'epc-erp-login-bg__particle';
		var size = Math.random() < 0.15 ? (3 + Math.random() * 4) : (1 + Math.random() * 2.5);
		var left = Math.random() * 100;
		var dur = 4 + Math.random() * 22;
		var delay = Math.random() * -30;
		var color = colors[Math.floor(Math.random() * colors.length)];
		var anim = anims[Math.floor(Math.random() * anims.length)];
		p.style.cssText = 'width:' + size + 'px;height:' + size + 'px;left:' + left + '%;background:' + color + ';animation:' + anim + ' ' + dur + 's linear ' + delay + 's infinite;';
		container.appendChild(p);
	}
})();
</script>
