<?php
/**
 * Garage login shortcut in site header (top bar).
 */
defined('_ASTEXE_') or die('No access');

$epc_garage_lang = isset($multilang_params['lang_href']) ? rtrim((string) $multilang_params['lang_href'], '/') : '/en';
$epc_garage_is_staff = class_exists('DP_User') && (DP_User::isAdmin() || DP_User::isBackendGroup());
$epc_garage_href = $epc_garage_is_staff
	? ($epc_garage_lang . '/garage/manager')
	: ($epc_garage_lang . '/garage/login');
$epc_garage_label = $epc_garage_is_staff ? 'Garage Manager' : 'Garage login';
?>
<div class="new-header-user-box epc-garage-header-link">
	<a href="<?php echo htmlspecialchars($epc_garage_href, ENT_QUOTES, 'UTF-8'); ?>" title="<?php echo htmlspecialchars($epc_garage_label, ENT_QUOTES, 'UTF-8'); ?>">
		<i class="fa fa-wrench" aria-hidden="true"></i> <?php echo htmlspecialchars($epc_garage_label, ENT_QUOTES, 'UTF-8'); ?>
	</a>
</div>
<style>
.epc-garage-header-link a{color:#a5f3fc!important;font-weight:700}
.epc-garage-header-link a:hover{color:#fff!important}
.epc-garage-header-link .fa{color:#22d3ee;margin-right:4px}
</style>
