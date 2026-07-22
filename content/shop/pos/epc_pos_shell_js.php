<?php
/**
 * POS CP shell — load page JS from desktop.php footer (outside .row content pane).
 */
defined('_ASTEXE_') or die('No access');

function epc_pos_cp_footer_scripts(string $contentUrl): void
{
	$contentUrl = trim($contentUrl, '/');
	$ver = '20260722posui1';
	$map = array(
		'shop/pos/terminal' => '/content/shop/pos/epc_pos_terminal_js.php',
		'control/portal/epc_pos_tenant_manage' => '/content/shop/pos/epc_pos_tenant_manage_js.php',
	);
	if (!isset($map[$contentUrl])) {
		return;
	}
	$src = $map[$contentUrl] . '?v=' . rawurlencode($ver);
	echo '<script src="' . htmlspecialchars($src, ENT_QUOTES, 'UTF-8') . '"></script>' . "\n";
}
