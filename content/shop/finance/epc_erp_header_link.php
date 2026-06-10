<?php
/**
 * ERP login shortcut in site header (top bar, next to user login).
 */
defined('_ASTEXE_') or die('No access');

$epc_erp_header_href = function_exists('epc_portal_erp_url')
	? epc_portal_erp_url(isset($multilang_params['lang_href']) ? (string) $multilang_params['lang_href'] : '')
	: ((isset($multilang_params['lang_href']) ? (string) $multilang_params['lang_href'] : '') . '/erp');
$epc_erp_header_label = translate_str_by_key('epc_menu_erp_login');
if ($epc_erp_header_label === '' || $epc_erp_header_label === 'epc_menu_erp_login') {
	$epc_erp_header_label = 'ERP Login';
}
?>
<div class="new-header-user-box epc-erp-header-link">
	<a href="<?php echo htmlspecialchars($epc_erp_header_href, ENT_QUOTES, 'UTF-8'); ?>" title="<?php echo htmlspecialchars($epc_erp_header_label, ENT_QUOTES, 'UTF-8'); ?>">
		<i class="fa fa-line-chart" aria-hidden="true"></i> <?php echo htmlspecialchars($epc_erp_header_label, ENT_QUOTES, 'UTF-8'); ?>
	</a>
</div>
