<?php
/**
 * CP route shop/customer_mgmt — redirect to Customer management hub.
 */
defined('_ASTEXE_') or die('No access');

$backend = htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');
header('Location: /' . $backend . '/shop/customer_mgmt/customer_mgmt', true, 302);
exit;
