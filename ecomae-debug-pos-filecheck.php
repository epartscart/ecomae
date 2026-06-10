<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$files = array(
	'cp/content/shop/pos/epc_pos_terminal_page.php',
	'cp/content/shop/pos/epc_pos_terminal.php',
	'cp/content/shop/pos/epc_pos_terminal_markup.php',
);
foreach ($files as $rel) {
	$path = __DIR__ . '/' . $rel;
	$raw = is_file($path) ? (string) file_get_contents($path) : '';
	$hasClose = preg_match('/\?>/', $raw) ? 'yes' : 'no';
	$hasRender = strpos($raw, 'epc_pos_terminal_render_markup') !== false ? 'yes' : 'no';
	echo $rel . ' bytes=' . strlen($raw) . ' close_tag=' . $hasClose . ' render_fn=' . $hasRender . "\n";
	if ($hasClose === 'yes') {
		if (preg_match_all('/\?>/', $raw, $m, PREG_OFFSET_CAPTURE)) {
			foreach ($m[0] as $hit) {
				$pos = (int) $hit[1];
				echo '  at ' . $pos . ': ' . substr(str_replace("\n", '\\n', $raw), max(0, $pos - 40), 80) . "\n";
			}
		}
	}
}
