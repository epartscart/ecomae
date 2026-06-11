<?php
/**
 * CP hub route shop/document_control — landing redirect.
 */
defined('_ASTEXE_') or die('No access');

$backend = htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');
$url = '/' . $backend . '/shop/document_control/document_control';
header('Location: ' . $url, true, 302);
echo '<p>Redirecting to <a href="' . htmlspecialchars($url, ENT_QUOTES, 'UTF-8') . '">Document Control</a>...</p>';

?>
