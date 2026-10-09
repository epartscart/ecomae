<?php
// PHP 8.3 goldens for plan Q1-mark (printProductBlock markup).
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1m_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/shop/catalogue', 0777, true);
	@mkdir($doc . '/lang', 0777, true);
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	copy($root . '/content/shop/catalogue/helper.php', $doc . '/content/shop/catalogue/helper.php');
	file_put_contents($doc . '/lang/dp_lang.php', <<<'PHP'
<?php
function multilang_init()
{
	return array('lang_href' => (string) ($GLOBALS['__lang_href'] ?? ''));
}
function translate_str_by_id($id)
{
	if (is_int($id) || (is_string($id) && preg_match('/^-?\d+$/', $id))) {
		return 'T' . $id;
	}
	return (string) $id;
}
class DP_Content
{
	public $main_flag = true;
}
class DP_User
{
	public static function isAdmin()
	{
		return !empty($GLOBALS['__admin']);
	}
}
class DP_Config
{
	public $domain_path = 'https://shop.example/';
	public $backend_dir = 'cp';
}
$GLOBALS['DP_Content'] = new DP_Content();
$GLOBALS['DP_Config'] = new DP_Config();
PHP
	);
	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['__lang_href'] = (string) ($case['lang_href'] ?? '');
	$GLOBALS['__admin'] = !empty($case['admin']);
	require $doc . '/content/shop/catalogue/helper.php';
	$GLOBALS['DP_Content']->main_flag = !empty($case['main_flag']);
	$_COOKIE = isset($case['cookies']) && is_array($case['cookies']) ? $case['cookies'] : array();
	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$cleanup();
		echo json_encode(array('name' => $case['name'], 'output' => $html, 'result' => $GLOBALS['__result'] ?? null), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	});
	ob_start();
	printProductBlock($case['product']);
	$GLOBALS['__result'] = ob_get_clean();
	ob_start();
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1m_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1m_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
