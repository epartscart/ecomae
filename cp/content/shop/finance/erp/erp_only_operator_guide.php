<?php
/**
 * ERP-only client operator guide — daily use after onboarding.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_erp_modules.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';

function epc_eoog_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$backend = htmlspecialchars((string) $GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');
$erpHome = epc_eoog_h(epc_erp_cp_shell_launcher_url());
$isErpOnly = function_exists('epc_portal_is_erp_only_tenant') && epc_portal_is_erp_only_tenant();
$enabledMods = epc_portal_erp_modules_enabled();
$modRegistry = epc_portal_erp_modules_registry();
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260529">
<style>
.epc-eoog { max-width: 880px; margin: 0 auto; }
.epc-eoog h3 { margin-top: 20px; font-size: 15px; font-weight: 700; }
.epc-eoog .step { border-left: 4px solid #0ea5e9; padding: 10px 14px; margin: 10px 0; background: #f8fafc; border-radius: 0 6px 6px 0; }
</style>
<div class="col-lg-12 epc-erp-shell epc-eoog">
	<div class="hpanel">
		<div class="panel-heading"><i class="fa fa-book"></i> ERP-only operator guide</div>
		<div class="panel-body">
			<?php if (!$isErpOnly): ?>
			<div class="alert alert-info">This site also has a storefront. For ERP-only tenants, commerce modules are hidden and login opens this shell directly.</div>
			<?php endif; ?>

			<h3>1. Sign in</h3>
			<div class="step">
				Open <a href="<?php echo $erpHome; ?>"><?php echo $erpHome; ?></a> or go to
				<a href="https://www.ecomae.com/cp/">https://www.ecomae.com/cp/</a> and log in with your email and password.
				You land in the <strong>ERP Suite</strong> for your company — Finance, CRM, HR, Custom &amp; Shipping, and Document control.
				<?php if (function_exists('epc_portal_is_shared_erp_cp_session') && epc_portal_is_shared_erp_cp_session()): ?>
				<p class="text-muted small" style="margin-top:8px">Your session is bound to your company database on www.ecomae.com (shared ERP hosting).</p>
				<?php endif; ?>
			</div>

			<h3>2. Switch company (multi-entity)</h3>
			<div class="step">
				If your operator enabled <strong>Multi-entity</strong>, open ERP → Finance area → <strong>Multi-entity</strong> tab.
				Select the legal entity before posting journals, invoices, or payroll. Each entity can have its own TRN and bank accounts.
			</div>

			<h3>3. Your enabled modules</h3>
			<div class="step">
				<p>Your operator enabled these ERP areas for this tenant:</p>
				<ul>
					<?php foreach ($enabledMods as $mid):
						if (!isset($modRegistry[$mid])) continue; ?>
					<li><strong><?php echo epc_eoog_h($modRegistry[$mid]['label']); ?></strong> — <?php echo epc_eoog_h($modRegistry[$mid]['desc']); ?></li>
					<?php endforeach; ?>
				</ul>
				<p class="text-muted small">Sidebar tabs you do not see are turned off at platform level — contact your administrator to request access.</p>
			</div>

			<h3>4. Users &amp; access</h3>
			<div class="step">
				Administrators manage users in CP → Users. ERP → Staff assigns department tabs (Finance-only, HR-only, etc.).
				Change your password after first login.
			</div>

			<h3>5. No storefront</h3>
			<div class="step">
				Shop, catalogue, and cart are disabled on ERP-only deployments.
				There is no customer-facing web shop — all work happens in the ERP shell at www.ecomae.com/cp/.
			</div>

			<p class="text-muted small" style="margin-top:16px">Hosted on ECOM AE · Shared ERP on www.ecomae.com · Support via your platform operator.</p>
		</div>
	</div>
</div>
