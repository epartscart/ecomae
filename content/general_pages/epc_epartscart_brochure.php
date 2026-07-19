<?php
/**
 * eParts Cart public product brochure (storefront CMS page or direct include).
 * URL target: /brochure  (frontend content row)
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_marketing_brochure.php';

$print = isset($_GET['print']) && (string) $_GET['print'] === '1';
epc_brochure_render_and_exit('epartscart', array('print' => $print));
