<?php
// Echoes the cart <script> of PHP modules/shop/bottom_panel/bottom_panel.php (lines "<script>" .. "</script>" around
// updateCartInfo/showAdded/hideAdded) for every case. No database: the inputs are the session's csrf_guard_key,
// whether the "session" cookie is set, and the current front template id.
// Usage: php harness.php /workspace > golden.json
$root = rtrim($argv[1] ?? '/workspace', '/');
$source = file_get_contents($root . '/modules/shop/bottom_panel/bottom_panel.php');
$marker = strpos($source, '//Функция обновления информации по корзине');
$start = strrpos(substr($source, 0, $marker), '<script>');
$end = strpos($source, '</script>', $marker) + strlen('</script>');
$snippet = substr($source, $start, $end - $start);
$cases = array(
	array('name' => 'guest_session_other_template', 'csrf' => 'CSRF_A', 'session_cookie' => true, 'template' => 12),
	array('name' => 'no_session_cookie', 'csrf' => '', 'session_cookie' => false, 'template' => 12),
	array('name' => 'template_63', 'csrf' => 'CSRF_B', 'session_cookie' => true, 'template' => 63),
	array('name' => 'template_63_no_cookie', 'csrf' => 'CSRF_C', 'session_cookie' => false, 'template' => 63),
);
$results = array();
foreach ($cases as $case) {
	$user_session = array('csrf_guard_key' => $case['csrf']);
	$DP_Template = new stdClass();
	$DP_Template->id = $case['template'];
	$_COOKIE = $case['session_cookie'] ? array('session' => 'tok') : array();
	ob_start();
	eval('?>' . $snippet);
	$results[] = $case + array('html' => ob_get_clean());
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE), "\n";
