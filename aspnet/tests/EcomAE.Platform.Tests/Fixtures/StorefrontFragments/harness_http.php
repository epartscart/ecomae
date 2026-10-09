<?php
// Serves the authoritative content/shop/catalogue/set_cookie_products_style.php through PHP's built-in web server
// (the CLI SAPI does not keep response headers) and records status, body and the Set-Cookie header for each query.
// Usage: php harness_http.php /workspace > golden_http.json
$root = rtrim($argv[1] ?? '/workspace', '/');
$queries = json_decode(file_get_contents(__DIR__ . '/cases_http.json'), true);

$doc = sys_get_temp_dir() . '/ecomae_cpw_http_' . substr(md5(uniqid('', true)), 0, 12);
mkdir($doc, 0777, true);
file_put_contents($doc . '/router.php', '<?php define("_ASTEXE_", 1); include ' . var_export($root . '/content/shop/catalogue/set_cookie_products_style.php', true) . ';');

$socket = stream_socket_server('tcp://127.0.0.1:0', $errno, $errstr);
$port = (int) substr(strrchr(stream_socket_get_name($socket, false), ':'), 1);
fclose($socket);
$process = proc_open(
	array(PHP_BINARY, '-S', '127.0.0.1:' . $port, '-t', $doc, $doc . '/router.php'),
	array(0 => array('file', '/dev/null', 'r'), 1 => array('file', '/dev/null', 'w'), 2 => array('file', '/dev/null', 'w')),
	$pipes
);
for ($i = 0; $i < 50; $i++) {
	$probe = @fsockopen('127.0.0.1', $port);
	if ($probe) {
		fclose($probe);
		break;
	}
	usleep(100000);
}

$results = array();
foreach ($queries as $case) {
	$context = stream_context_create(array('http' => array('ignore_errors' => true, 'timeout' => 10)));
	$body = file_get_contents('http://127.0.0.1:' . $port . '/?' . $case['query'], false, $context);
	$status = 0;
	$cookies = array();
	foreach ($http_response_header as $line) {
		if (preg_match('#^HTTP/\S+ (\d+)#', $line, $m)) {
			$status = (int) $m[1];
		} elseif (stripos($line, 'Set-Cookie:') === 0) {
			$cookies[] = preg_replace('/Expires=[^;]+/i', 'Expires=<date>', preg_replace('/Max-Age=\d+/i', 'Max-Age=<n>', trim(substr($line, 11))));
		}
	}
	$results[] = array('name' => $case['name'], 'query' => $case['query'], 'status' => $status, 'body' => $body, 'cookies' => $cookies);
}
proc_terminate($process);
proc_close($process);
@unlink($doc . '/router.php');
@rmdir($doc);
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
