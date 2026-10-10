<?php
// PHP 8.3 goldens for plan Q1-slip (BOS login). Leftover parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function slip_login($email, $password)
{
	$_POST = array('email' => $email, 'password' => $password);
	return epc_bos_ajax_login_secure();
}

function slip_run_missing()
{
	return array(
		slip_login('', 'x'),
		slip_login('a@b.c', ''),
		slip_login('  ', '  '),
		slip_login('a@b.c', 'x'),
	);
}

function slip_run_stores(PDO $pdo, $secret)
{
	$bcrypt = password_hash('secret1', PASSWORD_BCRYPT);
	$pdo->exec('DELETE FROM `users`');
	$pdo->exec('DELETE FROM `admin`');
	$ins = $pdo->prepare('INSERT INTO `users` (`email`,`password`,`site_key`) VALUES (?,?,?)');
	$ins->execute(array('empty@x.com', '', ''));
	$ins->execute(array('bcrypt@x.com', $bcrypt, 'shop'));
	$ins->execute(array('md5s@x.com', md5('secret1' . $secret), 'shop'));
	$ins->execute(array('md5p@x.com', md5('secret1'), 'shop'));
	$insA = $pdo->prepare('INSERT INTO `admin` (`email`,`pass`) VALUES (?,?)');
	$insA->execute(array('alias@x.com', $bcrypt));
	$GLOBALS['SLIP_PLATFORM'] = null;
	$out = array();
	$out[] = slip_login('none@x.com', 'secret1');
	$out[] = slip_login('empty@x.com', 'secret1');
	$out[] = slip_login('bcrypt@x.com', 'wrong');
	$out[] = slip_login('bcrypt@x.com', 'secret1');
	$out[] = slip_login('md5s@x.com', 'secret1');
	$out[] = slip_login('md5p@x.com', 'secret1');
	$out[] = slip_login('alias@x.com', 'secret1');
	return $out;
}

function slip_run_roles(PDO $pdo, $secret)
{
	$bcrypt = password_hash('secret1', PASSWORD_BCRYPT);
	$pdo->exec('DELETE FROM `users`');
	$pdo->exec('DELETE FROM `admin`');
	$pdo->exec('DELETE FROM `groups`');
	$pdo->exec('DELETE FROM `users_groups_bind`');
	$ins = $pdo->prepare('INSERT INTO `users` (`email`,`password`,`site_key`) VALUES (?,?,?)');
	$ins->execute(array('guest@x.com', $bcrypt, ''));
	$ins->execute(array('tenant@x.com', $bcrypt, 'Acme'));
	$ins->execute(array('admin@ecomae.com', $bcrypt, ''));
	$ins->execute(array('ops@x.com', $bcrypt, ''));
	$insA = $pdo->prepare('INSERT INTO `admin` (`email`,`pass`) VALUES (?,?)');
	$insA->execute(array('root@x.com', $bcrypt));
	$pdo->exec("INSERT INTO `groups` (`id`,`parent`,`for_backend`) VALUES (9,0,1)");
	$pdo->exec("INSERT INTO `users_groups_bind` (`user_id`,`group_id`) VALUES (4,9)");
	$GLOBALS['SLIP_PLATFORM'] = null;
	return array(
		slip_login('guest@x.com', 'secret1'),
		slip_login('tenant@x.com', 'secret1'),
		slip_login('root@x.com', 'secret1'),
		slip_login('admin@ecomae.com', 'secret1'),
		slip_login('ops@x.com', 'secret1'),
	);
}

function slip_run_session(PDO $pdo, $secret)
{
	$bcrypt = password_hash('secret1', PASSWORD_BCRYPT);
	$pdo->exec('DELETE FROM `users`');
	$ins = $pdo->prepare('INSERT INTO `users` (`email`,`password`,`site_key`) VALUES (?,?,?)');
	$ins->execute(array('hello@ecomae.com', $bcrypt, ''));
	$GLOBALS['SLIP_PLATFORM'] = $pdo;
	$GLOBALS['SLIP_CONTEXT'] = null;
	$out = slip_login('hello@ecomae.com', 'secret1');
	return array(
		$out,
		$GLOBALS['SLIP_CONTEXT'],
		(int) $GLOBALS['SLIP_UPGRADE'],
		(int) $GLOBALS['SLIP_AUDIT'],
	);
}

function slip_write_config($doc, $dbName, $password, $secret)
{
	file_put_contents($doc . '/config.php', "<?php
class DP_Config {
	public \$host = '127.0.0.1';
	public \$db = '" . $dbName . "';
	public \$user = 'ecomae';
	public \$password = '" . $password . "';
	public \$secret_succession = '" . $secret . "';
}
");
}

function slip_schema(PDO $pdo)
{
	$pdo->exec('CREATE TABLE `users` (
		`id` int NOT NULL AUTO_INCREMENT,
		`email` varchar(190) NOT NULL DEFAULT \'\',
		`password` varchar(255) NOT NULL DEFAULT \'\',
		`site_key` varchar(64) NOT NULL DEFAULT \'\',
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `admin` (
		`id` int NOT NULL AUTO_INCREMENT,
		`email` varchar(190) NOT NULL DEFAULT \'\',
		`pass` varchar(255) NOT NULL DEFAULT \'\',
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `epc_cp_users` (
		`id` int NOT NULL AUTO_INCREMENT,
		`email` varchar(190) NOT NULL DEFAULT \'\',
		`password` varchar(255) NOT NULL DEFAULT \'\',
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `groups` (
		`id` int NOT NULL,
		`parent` int NOT NULL DEFAULT 0,
		`for_backend` int NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `users_groups_bind` (
		`user_id` int NOT NULL,
		`group_id` int NOT NULL
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

if (!function_exists('epc_portal_platform_operator_pdo')) {
	function epc_portal_platform_operator_pdo()
	{
		return $GLOBALS['SLIP_PLATFORM'] ?? null;
	}
}
if (!function_exists('epc_bos_set_context')) {
	function epc_bos_set_context(array $ctx): void
	{
		$GLOBALS['SLIP_CONTEXT'] = $ctx;
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1s_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	$secret = 'succ';
	slip_schema($pdo);
	if ($case['name'] === 'missing') {
		file_put_contents($doc . '/config.php', "<?php throw new Exception('no-config');\n");
	} else {
		slip_write_config($doc, $dbName, $password, $secret);
	}
	$cleanup = function () use ($admin, $dbName, $doc) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		foreach (glob($doc . '/*') ?: array() as $file) {
			@unlink($file);
		}
		@rmdir($doc);
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['__secret'] = $secret;
	$GLOBALS['SLIP_PLATFORM'] = null;
	$GLOBALS['SLIP_CONTEXT'] = null;
	$GLOBALS['SLIP_REGEN'] = 0;
	$GLOBALS['SLIP_UPGRADE'] = 0;
	$GLOBALS['SLIP_AUDIT'] = 0;
	if (session_status() !== PHP_SESSION_ACTIVE) {
		session_start();
	}
	$_SESSION['epc_csrf_bos'] = 'fixedcsrf';
	require $root . '/content/general_pages/epc_bos_ajax_login.php';
	register_shutdown_function($cleanup);
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1slip_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1slip_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
