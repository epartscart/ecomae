<?php
/**
 * Serve ERP portal inline CSS (particles, bos-hero, dark standalone shell)
 * for ASP.NET /erp/login parity — same rules as epc_erp_portal_inline_css.php.
 */
declare(strict_types=1);

header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=604800');

define('_ASTEXE_', 1);
ob_start();
require __DIR__ . '/epc_erp_portal_inline_css.php';
$html = ob_get_clean();

if (preg_match('/<style[^>]*>(.*)<\/style>/s', $html, $m)) {
    echo $m[1];
} else {
    echo $html;
}
