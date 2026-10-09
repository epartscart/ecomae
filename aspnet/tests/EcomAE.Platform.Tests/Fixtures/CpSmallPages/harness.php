<?php
// Runs the small CP/storefront includes listed in cases.json (one process per case) and records PHP 8.3 output.
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_small_' . substr(md5(uniqid('', true)), 0, 12);
	$backend = (string) (($case['config']['backend_dir'] ?? 'cp'));
	foreach (array(
		'/content/users',
		'/content/general_pages',
		'/content/shop/pos',
		'/content/shop/document_control',
		'/license',
		'/lang',
		'/' . $backend . '/content/shop/bulk_upload',
		'/' . $backend . '/content/shop/payments',
		'/' . $backend . '/content/shop/pos',
		'/' . $backend . '/content/shop/channels',
		'/' . $backend . '/content/shop/marketing',
		'/' . $backend . '/content/shop/tenant_hub',
		'/' . $backend . '/content/shop/customer_mgmt',
		'/' . $backend . '/content/shop/procurement',
		'/' . $backend . '/content/shop/prices_upload',
		'/' . $backend . '/content/shop/logistics',
		'/' . $backend . '/content/shop/document_control',
		'/' . $backend . '/content/shop/order_process',
		'/' . $backend . '/content/shop/catalogue',
		'/' . $backend . '/content/shop/returns',
		'/' . $backend . '/content/control',
		'/' . $backend . '/content/control/portal',
		'/' . $backend . '/tmp/suppliers_api_log',
	) as $dir) {
		@mkdir($doc . $dir, 0777, true);
	}

	file_put_contents(
		$doc . '/content/users/dp_user.php',
		'<?php if (!class_exists("DP_User")) { class DP_User { public static function isAdmin() { return !empty($GLOBALS["__case_is_admin"]); } public static function getAdminSession() { return $GLOBALS["__case_admin_session"] ?? false; } public static function getUserId() { return 1; } public static function getName() { return "Op"; } } }'
	);
	file_put_contents(
		$doc . '/config.php',
		'<?php if (!class_exists("DP_Config")) { class DP_Config { public $backend_dir = "cp"; public $host = "127.0.0.1"; public $db = "x"; public $user = "x"; public $password = "x"; public $suppliers_api_debug = 0; public function __construct() { foreach (($GLOBALS["__case_config"] ?? array()) as $k => $v) { $this->$k = $v; } } } }'
	);
	file_put_contents(
		$doc . '/content/general_pages/epc_portal.php',
		'<?php function epc_portal_apply_config($c) {} function epc_portal_cp_industry_session_key() { return "epc_cp_industry_filter"; } function epc_portal_industries() { return array("auto_parts" => array("name" => "Auto"), "fashion" => array("name" => "Fashion")); } function epc_portal_is_super_cp_host() { return !empty($GLOBALS["__case_is_super"]); } function epc_portal_is_platform_operator() { return !empty($GLOBALS["__case_is_operator"]); }'
	);
	file_put_contents($doc . '/content/general_pages/epc_eparts_product_route.php', '<?php');
	file_put_contents($doc . '/content/shop/document_control/epc_document_control_helpers.php', '<?php');

	foreach (($case['stubs'] ?? array()) as $rel => $body) {
		$path = $doc . '/' . ltrim($rel, '/');
		@mkdir(dirname($path), 0777, true);
		file_put_contents($path, $body);
	}

	$cleanup = function () use ($doc) {
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};

	$GLOBALS['__case_is_admin'] = !empty($case['is_admin']);
	$GLOBALS['__case_is_super'] = !empty($case['is_super']);
	$GLOBALS['__case_is_operator'] = !empty($case['is_operator']);
	$GLOBALS['__case_admin_session'] = array_key_exists('admin_session', $case) ? $case['admin_session'] : false;
	$GLOBALS['__case_config'] = $case['config'] ?? array('backend_dir' => 'cp');
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_SERVER['HTTP_HOST'] = $case['host'] ?? 'localhost';
	$_GET = $case['get'] ?? array();
	$_POST = array();
	$_COOKIE = array();
	$_REQUEST = array_merge($_GET, $_POST);
	if (session_status() !== PHP_SESSION_ACTIVE) {
		session_start();
	}
	$_SESSION = $case['session'] ?? array();
	$DP_Config = new stdClass();
	foreach (($case['config'] ?? array('backend_dir' => 'cp')) as $key => $value) {
		$DP_Config->$key = $value;
	}
	$GLOBALS['DP_Config'] = $DP_Config;
	$DP_Template = new stdClass();
	$DP_Template->name = $case['template'] ?? 'bootstrap_admin';
	foreach (($case['vars'] ?? array()) as $key => $value) {
		$$key = $value;
	}
	function translate_str_by_id($key) { return '{' . $key . '}'; }
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$headers = array();
		foreach (headers_list() as $header) {
			$headers[] = $header;
		}
		$code = http_response_code();
		$cleanup();
		echo json_encode(
			array(
				'name' => $case['name'],
				'output' => $html,
				'result' => $GLOBALS['__result'] ?? null,
				'headers' => $headers,
				'status' => $code,
				'session' => $_SESSION ?? array(),
			),
			JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR
		);
	});
	ob_start();
	if (isset($case['prelude'])) {
		eval($case['prelude']);
	}
	if (isset($case['file'])) {
		$include = $root . '/' . $case['file'];
		if (!empty($case['copy_file'])) {
			$copy = $doc . '/' . $case['file'];
			@mkdir(dirname($copy), 0777, true);
			copy($include, $copy);
			$include = $copy;
		}
		include $include;
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
