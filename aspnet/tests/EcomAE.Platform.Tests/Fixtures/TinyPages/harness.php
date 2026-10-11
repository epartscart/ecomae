<?php
// Runs the small storefront/CP includes listed in cases.json (one process per case) and records PHP 8.3 output.
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_tiny_' . substr(md5(uniqid('', true)), 0, 12);
	foreach (array('/content/users', '/content/general_pages', '/license', '/lang') as $dir) {
		mkdir($doc . $dir, 0777, true);
	}
	file_put_contents(
		$doc . '/content/users/dp_user.php',
		'<?php if (!class_exists("DP_User")) { class DP_User { public static function getUserId() { return (int) $GLOBALS["__case_user_id"]; } public static function getUserSession() { return $GLOBALS["__case_csrf"] === null ? false : array("csrf_guard_key" => $GLOBALS["__case_csrf"]); } public static function getAdminSession() { return $GLOBALS["__case_admin_session"] ?? false; } public static function getAdminProfile() { return $GLOBALS["__case_admin_profile"] ?? array("name" => ""); } } }'
	);
	if (!empty($case['license'])) {
		file_put_contents($doc . '/license/license.lic', $case['license']);
	}

	$cleanup = function () use ($doc) {
		foreach (array('/content/users/dp_user.php', '/license/license.lic') as $file) {
			@unlink($doc . $file);
		}
		foreach (array('/content/users', '/content/general_pages', '/content', '/license', '/lang', '') as $dir) {
			@rmdir($doc . $dir);
		}
	};

	$GLOBALS['__case_user_id'] = $case['user_id'] ?? 0;
	$GLOBALS['__case_csrf'] = array_key_exists('csrf', $case) ? $case['csrf'] : null;
	$GLOBALS['__case_admin_session'] = $case['admin_session'] ?? false;
	$GLOBALS['__case_admin_profile'] = $case['admin_profile'] ?? array('name' => '');
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_GET = $case['get'] ?? array();
	$_POST = array();
	$_COOKIE = array();
	$DP_Config = new stdClass();
	foreach (($case['config'] ?? array()) as $key => $value) {
		$DP_Config->$key = $value;
	}
	$GLOBALS['DP_Config'] = $DP_Config;
	$DP_Content = new stdClass();
	$DP_Content->service_data = $case['content_service_data'] ?? array();
	$plugin_record = array('data_value' => $case['plugin_data_value'] ?? '{}');
	foreach (($case['vars'] ?? array()) as $key => $value) {
		$$key = $value;
	}
	function translate_str_by_id($key) { return '{' . $key . '}'; }
	define('_ASTEXE_', 1);
	register_shutdown_function(function () use ($case, $cleanup, $DP_Content) {
		$html = ob_get_clean();
		$vars = array();
		foreach (($case['vars_out'] ?? array()) as $name) {
			if ($name === 'DP_Content') {
				$vars[$name] = array(
					'value' => $DP_Content->value ?? null,
					'title_tag' => $DP_Content->title_tag ?? null,
					'description_tag' => $DP_Content->description_tag ?? null,
					'keywords_tag' => $DP_Content->keywords_tag ?? null,
					'author_tag' => $DP_Content->author_tag ?? null,
					'content_type' => $DP_Content->content_type ?? null,
					'content' => $DP_Content->content ?? null,
				);
				continue;
			}
			$vars[$name] = $GLOBALS[$name] ?? null;
		}
		$headers = array();
		foreach (headers_list() as $header) {
			$headers[] = preg_replace('/Expires=[^;]+/i', 'Expires=<date>', preg_replace('/Max-Age=\\d+/i', 'Max-Age=<n>', $header));
		}
		$cleanup();
		echo json_encode(
			array('name' => $case['name'], 'output' => $html, 'vars' => $vars, 'result' => $GLOBALS['__result'] ?? null, 'headers' => $headers),
			JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR
		);
	});
	ob_start();
	if (isset($case['file'])) {
		include $root . '/' . $case['file'];
	}
	if (isset($case['eval'])) {
		$GLOBALS['__result'] = eval($case['eval']);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$out = shell_exec(escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i . ' 2>/dev/null');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
