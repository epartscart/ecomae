<?php
/**
 * CP route shop/pos — folder redirect to terminal.
 */
defined('_ASTEXE_') or die('No access');

global $DP_Config;
$backend = htmlspecialchars((string) ($DP_Config->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');
header('Location: /' . $backend . '/shop/pos/terminal', true, 302);
exit;

?>
