<?php
/**
 * Storefront — Garage staff login gate.
 * URL: /en/garage/login
 * Staff with CP/backend access continue to Garage Manager; others see instructions.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/workshop/epc_workshop_helpers.php';

$lang = isset($multilang_params['lang_href']) ? rtrim((string)$multilang_params['lang_href'], '/') : '/en';
$managerUrl = $lang . '/garage/manager';
$cpWorkshop = '/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/shop/workshop/workshop';
$cpLogin = '/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/';
$bookUrl = $lang . '/auto-workshop';
$myGarage = $lang . '/garazh';

$isStaff = epc_ws_staff_ok();
// After staff login, continue into Garage Manager. Guests stay on this gate.
if ($isStaff && empty($_GET['stay'])) {
	if (!headers_sent()) {
		header('Location: ' . $managerUrl, true, 302);
		exit;
	}
	echo '<script>location=' . json_encode($managerUrl) . ';</script>';
	return;
}
?>
<style>
.epc-gl{--ink:#0b1220;--muted:#5b6578;--teal:#0e7490;font-family:"DM Sans","Source Sans 3","Segoe UI",sans-serif;color:var(--ink);max-width:920px;margin:0 auto 48px}
.epc-gl__hero{margin:0 0 22px;padding:36px 28px;border-radius:18px;color:#f8fafc;
background:radial-gradient(800px 240px at 90% -10%,rgba(125,211,252,.28),transparent 55%),linear-gradient(125deg,#0b1220 0%,#164e63 48%,#0e7490 100%);
box-shadow:0 18px 40px rgba(11,18,32,.22)}
.epc-gl__hero h1{margin:0 0 8px;font-size:clamp(1.8rem,4vw,2.4rem);font-weight:800;letter-spacing:-.03em}
.epc-gl__hero p{margin:0;opacity:.92;max-width:52ch;line-height:1.5}
.epc-gl__grid{display:grid;grid-template-columns:1.1fr .9fr;gap:16px}
@media(max-width:800px){.epc-gl__grid{grid-template-columns:1fr}}
.epc-gl__card{background:#fff;border:1px solid #d9e2e0;border-radius:16px;padding:22px;box-shadow:0 10px 28px rgba(15,23,42,.06)}
.epc-gl__card h2{margin:0 0 10px;font-size:1.15rem;font-weight:800}
.epc-gl__card p{color:var(--muted);line-height:1.5;margin:0 0 14px}
.epc-gl__btn{display:inline-flex;align-items:center;gap:8px;padding:11px 16px;border-radius:10px;background:var(--teal);color:#fff!important;font-weight:800;text-decoration:none!important;margin:0 8px 8px 0}
.epc-gl__btn--ghost{background:#fff;color:var(--ink)!important;border:1px solid #c9d4d1}
.epc-gl__list{margin:0;padding-left:18px;color:var(--muted);line-height:1.55}
.epc-gl__ok{background:#ecfeff;border:1px solid #a5f3fc;border-radius:12px;padding:12px 14px;margin:0 0 14px;font-weight:700;color:#155e75}
</style>

<div class="epc-gl">
	<header class="epc-gl__hero">
		<h1>Garage Manager login</h1>
		<p>Staff portal for the full garage workflow — appointments, check-in, job cards, bays, technicians, parts &amp; labour, QC and handover.</p>
	</header>

	<div class="epc-gl__grid">
		<div class="epc-gl__card">
			<h2><i class="fa fa-wrench"></i> Workshop staff</h2>
			<?php if ($isStaff): ?>
				<div class="epc-gl__ok">You are signed in with workshop access.</div>
				<a class="epc-gl__btn" href="<?php echo htmlspecialchars($managerUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-th-large"></i> Open Garage Manager</a>
				<a class="epc-gl__btn epc-gl__btn--ghost" href="<?php echo htmlspecialchars($cpWorkshop, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-desktop"></i> CP workshop desk</a>
			<?php else: ?>
				<p>Sign in with your <strong>Control Panel / workshop</strong> staff account to run the garage end-to-end.</p>
				<a class="epc-gl__btn" href="<?php echo htmlspecialchars($cpLogin, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-sign-in"></i> Staff CP login</a>
				<a class="epc-gl__btn epc-gl__btn--ghost" href="<?php echo htmlspecialchars($cpWorkshop, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-car"></i> Workshop desk</a>
				<p style="margin-top:12px;font-size:13px">After CP login, return here or open <em>Garage Manager</em> from the header.</p>
			<?php endif; ?>
		</div>
		<div class="epc-gl__card">
			<h2><i class="fa fa-users"></i> Customers</h2>
			<p>Vehicle garage, notepad, and service booking — no staff login required.</p>
			<a class="epc-gl__btn epc-gl__btn--ghost" href="<?php echo htmlspecialchars($myGarage, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-home"></i> My Garage</a>
			<a class="epc-gl__btn epc-gl__btn--ghost" href="<?php echo htmlspecialchars($bookUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-calendar"></i> Book / track service</a>
			<ul class="epc-gl__list">
				<li>Save vehicles &amp; VIN</li>
				<li>Parts notepad per car</li>
				<li>Book service &amp; track job status</li>
			</ul>
		</div>
	</div>
</div>
