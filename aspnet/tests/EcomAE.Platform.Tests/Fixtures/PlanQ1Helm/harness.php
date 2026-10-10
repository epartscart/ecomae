<?php
// PHP 8.3 goldens for plan Q1-helm (auth SMTP). Portal / mailer leftovers stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function helm_norm_write($row)
{
	if (!is_array($row)) {
		return $row;
	}
	$row['path'] = 'SMTP_FILE';
	if (isset($row['content']) && is_string($row['content'])) {
		$row['content'] = preg_replace('/Last saved: [^\n]+/', 'Last saved: ISO', $row['content']);
	}
	return $row;
}

function helm_norm_send($row)
{
	if (!is_array($row)) {
		return $row;
	}
	if (isset($row['detail']) && is_string($row['detail']) && (strpos($row['detail'], 'mailer') !== false || strpos($row['detail'], 'Docpart') !== false)) {
		$row['detail'] = 'MAILER';
	}
	return $row;
}

function helm_dp($overrides = array())
{
	$dp = new DP_Config();
	foreach ($overrides as $k => $v) {
		$dp->$k = $v;
	}
	$GLOBALS['DP_Config'] = $dp;
	return $dp;
}

function helm_run_validate()
{
	$presets = epc_auth_smtp_recommended_presets();
	return array(
		array_keys($presets),
		$presets['gmail_tls'],
		$presets['hostinger_ssl']['smtp_port'],
		epc_auth_smtp_validate_input(array()),
		epc_auth_smtp_validate_input(array(
			'smtp_mode' => '1',
			'from_email' => 'ops@shop.example',
		)),
		epc_auth_smtp_validate_input(array(
			'smtp_mode' => '1',
			'from_email' => 'not-an-email',
			'smtp_host' => 'smtp.example',
			'smtp_port' => '587abc',
			'smtp_encryption' => 'foo',
			'smtp_password' => 'short1',
		)),
		epc_auth_smtp_validate_input(array(
			'smtp_mode' => '1',
			'from_email' => 'ops@gmail.com',
			'smtp_host' => 'smtp.Gmail.com',
			'smtp_port' => '465',
			'smtp_encryption' => 'tls',
			'smtp_username' => 'other@gmail.com',
			'smtp_password' => '123456789012345',
		)),
		epc_auth_smtp_validate_input(array(
			'smtp_mode' => '0',
			'from_email' => 'ops@shop.example',
		)),
		epc_auth_smtp_validate_input(array(
			'smtp_mode' => '1',
			'from_email' => 'ops@shop.example',
			'smtp_host' => 'smtp.hostinger.com',
			'smtp_port' => '465',
			'smtp_encryption' => 'ssl',
			'smtp_password' => '',
		)),
		epc_auth_smtp_validate_input(array(
			'smtp_mode' => '1',
			'from_email' => 'ops@shop.example',
			'smtp_host' => 'smtp.hostinger.com',
			'smtp_port' => '465',
			'smtp_encryption' => '',
			'smtp_username' => '',
			'smtp_password' => 'long-enough-pass',
		)),
	);
}

function helm_run_write()
{
	$bad = helm_norm_write(epc_auth_smtp_write_file_config(array(
		'smtp_mode' => '1',
		'from_email' => 'bad',
	)));
	$ok = epc_auth_smtp_write_file_config(array(
		'smtp_mode' => '1',
		'from_email' => 'ops@shop.example',
		'smtp_host' => 'smtp.hostinger.com',
		'smtp_port' => '465',
		'smtp_encryption' => 'ssl',
		'smtp_username' => 'ops@shop.example',
		'smtp_password' => 'mailbox-secret',
		'from_name' => "Shop O'Brien",
		'allow_mail_fallback' => '1',
		'disable_demo_otp_fallback' => '0',
	));
	$ok['mode'] = file_exists($ok['path']) ? (fileperms($ok['path']) & 0777) : 0;
	$ok['read'] = epc_auth_smtp_file_config();
	$ok = helm_norm_write($ok);
	$ok['content'] = is_file($_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php')
		? preg_replace('/Last saved: [^\n]+/', 'Last saved: ISO', (string) file_get_contents($_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php'))
		: '';
	$keep = helm_norm_write(epc_auth_smtp_write_file_config(array(
		'smtp_mode' => '1',
		'from_email' => 'ops@shop.example',
		'smtp_host' => 'smtp.hostinger.com',
		'smtp_port' => '587',
		'smtp_encryption' => 'tls',
		'smtp_username' => 'ops@shop.example',
		'smtp_password' => '',
		'from_name' => 'Shop',
	)));
	$keep['read_pass'] = epc_auth_smtp_file_config()['smtp_password'] ?? null;
	$emptyPass = helm_norm_write(epc_auth_smtp_write_file_config(array(
		'smtp_mode' => '0',
		'from_email' => 'hello@ecomae.com',
		'smtp_password' => '',
	)));
	@unlink($_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php');
	$firstEmpty = helm_norm_write(epc_auth_smtp_write_file_config(array(
		'smtp_mode' => '0',
		'from_email' => 'hello@ecomae.com',
		'smtp_password' => '',
	)));
	return array($bad, $ok, $keep, $firstEmpty);
}

function helm_run_effective()
{
	@unlink($_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php');
	@unlink($_SERVER['DOCUMENT_ROOT'] . '/config.local.php');
	helm_dp();
	$GLOBALS['HELM_SUPER'] = false;
	$GLOBALS['HELM_SETTINGS'] = array();
	$base = epc_auth_smtp_effective_config();
	$diagBase = epc_auth_smtp_diagnose();
	file_put_contents($_SERVER['DOCUMENT_ROOT'] . '/config.local.php', '<?php $epc_config_local = array("smtp_host" => "local.example", "from_name" => "Local");');
	helm_dp(array('smtp_host' => 'cfg.example', 'from_email' => 'from@cfg.example'));
	$withLocal = epc_auth_smtp_effective_config();
	file_put_contents(
		$_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php',
		"<?php\nreturn array('smtp_mode' => '1', 'smtp_host' => '0', 'smtp_port' => '587', 'smtp_encryption' => 'tls', 'smtp_username' => 'file@shop.example', 'smtp_password' => 'file-secret-1', 'from_email' => 'file@shop.example', 'from_name' => 'File', 'allow_mail_fallback' => '0');\n"
	);
	helm_dp(array('smtp_host' => 'cfg.example', 'from_email' => 'from@cfg.example', 'smtp_mode' => '0'));
	$fileOverlay = epc_auth_smtp_effective_config();
	$GLOBALS['HELM_SETTINGS'] = array(
		'integrations' => array(
			'smtp' => array(
				'use_tenant_smtp' => '1',
				'smtp_host' => 'tenant.example',
				'smtp_port' => '465',
				'smtp_encryption' => 'ssl',
				'smtp_username' => 'tenant@shop.example',
				'smtp_password' => 'tenant-secret',
				'from_email' => 'tenant@shop.example',
				'from_name' => 'Tenant',
			),
		),
	);
	$tenant = epc_auth_smtp_effective_config();
	$diagTenant = epc_auth_smtp_diagnose();
	$GLOBALS['HELM_SUPER'] = true;
	$super = epc_auth_smtp_effective_config();
	$GLOBALS['HELM_SUPER'] = false;
	$GLOBALS['HELM_SETTINGS'] = array(
		'integrations' => array(
			'smtp' => array(
				'use_tenant_smtp' => '0',
				'smtp_host' => 'ignored.example',
				'from_email' => 'ignored@shop.example',
			),
		),
	);
	$useOff = epc_auth_smtp_effective_config();
	$GLOBALS['HELM_SETTINGS'] = array(
		'integrations' => array(
			'smtp' => array(
				'use_tenant_smtp' => '1',
				'smtp_host' => '0',
				'smtp_port' => '465',
				'from_email' => 'keep@shop.example',
			),
		),
	);
	$emptyZero = epc_auth_smtp_effective_config();
	return array($base, $diagBase, $withLocal, $fileOverlay, $tenant, $diagTenant, $super, $useOff, $emptyZero);
}

function helm_run_send(PDO $pdo)
{
	@unlink($_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php');
	@unlink($_SERVER['DOCUMENT_ROOT'] . '/config.local.php');
	helm_dp();
	$GLOBALS['HELM_SUPER'] = false;
	$GLOBALS['HELM_SETTINGS'] = array();
	$classified = array(
		epc_auth_smtp_classify_error('Could not connect to host'),
		epc_auth_smtp_classify_error('SMTP 535 authentication failed'),
		epc_auth_smtp_classify_error('password is empty'),
		epc_auth_smtp_classify_error('unknown boom'),
		epc_auth_smtp_classify_error('connect() failed', array('AUTH LOGIN')),
	);
	$precheck = helm_norm_send(epc_auth_smtp_send_html('a@b.example', 'Hi', '<b>x</b>'));
	file_put_contents(
		$_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php',
		"<?php\nreturn array('smtp_mode' => '1', 'smtp_host' => 'smtp.example', 'smtp_port' => '587', 'smtp_encryption' => 'tls', 'smtp_username' => 'ops@shop.example', 'smtp_password' => 'file-secret-1', 'from_email' => 'ops@shop.example', 'from_name' => 'Ops', 'allow_mail_fallback' => '0');\n"
	);
	$missing = helm_norm_send(epc_auth_smtp_send_html('a@b.example', 'Hi', '<b>x</b>'));
	file_put_contents(
		$_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php',
		"<?php\nreturn array('smtp_mode' => '1', 'smtp_host' => 'smtp.example', 'smtp_port' => '587', 'smtp_encryption' => 'tls', 'smtp_username' => 'ops@shop.example', 'smtp_password' => 'file-secret-1', 'from_email' => 'ops@shop.example', 'from_name' => 'Ops', 'allow_mail_fallback' => '1');\n"
	);
	$GLOBALS['HELM_MAIL'] = true;
	$fallback = helm_norm_send(epc_auth_smtp_send_html('a@b.example', 'Hi', '<b>x</b>'));
	@mkdir($_SERVER['DOCUMENT_ROOT'] . '/lib/stubmail', 0777, true);
	file_put_contents($_SERVER['DOCUMENT_ROOT'] . '/lib/stubmail/mailer.php', "<?php\nclass DocpartMailer {\n public \$ErrorInfo=''; public \$SMTPDebug=0; public \$Debugoutput; public \$Host; public \$Port; public \$SMTPSecure; public \$SMTPAuth; public \$Username; public \$Password; public \$Sender; public \$CharSet; public \$Subject; public \$Body; public \$AltBody;\n public function __construct(\$x=null) {}\n public function isSMTP() {}\n public function isHTML(\$v) {}\n public function setFrom(\$e, \$n) { \$this->Sender = \$e; }\n public function addAddress(\$t) {}\n public function send() {\n  if (!empty(\$GLOBALS['HELM_SMTP_FAIL'])) { \$this->ErrorInfo = (string) \$GLOBALS['HELM_SMTP_FAIL']; return false; }\n  return true;\n }\n}\n");
	$GLOBALS['HELM_MAILER_PRESENT'] = true;
	$GLOBALS['HELM_SMTP_FAIL'] = '';
	$okSend = helm_norm_send(epc_auth_smtp_send_html('a@b.example', 'Hi', '<b>x</b>', ''));
	$mail = new DocpartMailer(true);
	epc_auth_smtp_apply_to_mailer($mail, epc_auth_smtp_effective_config());
	$applied = array(
		'Host' => $mail->Host,
		'Port' => $mail->Port,
		'SMTPSecure' => $mail->SMTPSecure,
		'SMTPAuth' => $mail->SMTPAuth,
		'Username' => $mail->Username,
		'Password' => $mail->Password,
		'Sender' => $mail->Sender,
	);
	$GLOBALS['HELM_SMTP_FAIL'] = 'Could not connect to host';
	$fail = helm_norm_send(epc_auth_smtp_send_html('a@b.example', 'Hi', '<b>x</b>'));
	$GLOBALS['HELM_MAIL'] = true;
	$failThenMail = helm_norm_send(epc_auth_smtp_send_html('a@b.example', 'Hi', '<b>x</b>'));
	@unlink($_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php');
	file_put_contents(
		$_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php',
		"<?php\nreturn array('smtp_mode' => '1', 'smtp_host' => 'smtp.example', 'smtp_port' => '587', 'smtp_encryption' => 'tls', 'smtp_username' => 'ops@shop.example', 'smtp_password' => 'file-secret-1', 'from_email' => 'ops@shop.example', 'from_name' => 'Ops', 'allow_mail_fallback' => '0', 'disable_demo_otp_fallback' => '0');\n"
	);
	$demo = array(
		epc_auth_otp_demo_fallback_allowed(''),
		epc_auth_otp_demo_fallback_allowed('shop'),
		epc_auth_otp_demo_fallback_allowed('demo_acme'),
		epc_auth_otp_demo_fallback_allowed('DEMO_x'),
	);
	file_put_contents(
		$_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php',
		"<?php\nreturn array('disable_demo_otp_fallback' => '1');\n"
	);
	$demo[] = epc_auth_otp_demo_fallback_allowed('demo_acme');
	file_put_contents(
		$_SERVER['DOCUMENT_ROOT'] . '/config.epc-smtp.php',
		"<?php\nreturn array('disable_demo_otp_fallback' => '0');\n"
	);
	$demo[] = epc_auth_otp_demo_fallback_allowed('demo_acme');
	$pdo->exec('CREATE TABLE IF NOT EXISTS `epc_auth_otp_requests` (
		`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`email` VARCHAR(120) NOT NULL,
		`code_hash` VARCHAR(64) NOT NULL,
		`tenant_key` VARCHAR(64) NOT NULL DEFAULT \'\',
		`context_json` TEXT NULL,
		`expires_at` INT NOT NULL,
		`ip_address` VARCHAR(45) NOT NULL DEFAULT \'\',
		`created_at` INT NOT NULL DEFAULT 0
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$ins = $pdo->prepare('INSERT INTO `epc_auth_otp_requests` (`email`,`code_hash`,`tenant_key`,`context_json`,`expires_at`,`created_at`) VALUES (?,?,?,?,?,?)');
	$ins->execute(array('ops@shop.example', 'h1', 'acme', '{"note":"x"}', 1760084000, 1760083200));
	$ins->execute(array('ops@shop.example', 'h2', 'beta', '{}', 1760084000, 1760083300));
	epc_auth_otp_store_operator_code($pdo, 0, '999999');
	epc_auth_otp_store_operator_code($pdo, 99, '999999');
	epc_auth_otp_store_operator_code($pdo, 1, '123456');
	epc_auth_otp_store_operator_code($pdo, 2, '0');
	$lookup = array(
		epc_auth_otp_operator_lookup($pdo, ''),
		epc_auth_otp_operator_lookup($pdo, 'not-an-email'),
		epc_auth_otp_operator_lookup($pdo, 'missing@shop.example'),
		epc_auth_otp_operator_lookup($pdo, 'OPS@shop.example'),
	);
	$rows = $pdo->query('SELECT `id`,`tenant_key`,`context_json` FROM `epc_auth_otp_requests` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
	return array($classified, $precheck, $missing, $fallback, $okSend, $applied, $fail, $failThenMail, $demo, $lookup, $rows);
}

function helm_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("date('c')", "date('c', (int) (\$GLOBALS['HELM_NOW'] ?? time()))", $code);
	$code = str_replace('$ctx[\'_operator_otp_at\'] = time();', '$ctx[\'_operator_otp_at\'] = (int) ($GLOBALS[\'HELM_NOW\'] ?? time());', $code);
	$code = str_replace(
		"if (!is_file(\$dbFile)) {\n\t\treturn array();\n\t}\n\trequire_once \$dbFile;",
		"if (is_file(\$dbFile)) {\n\t\trequire_once \$dbFile;\n\t}",
		$code
	);
	$code = str_replace(
		"\$mailerFile = \$_SERVER['DOCUMENT_ROOT'] . '/lib/DocpartMailer/docpart_mailer.php';",
		"\$mailerFile = \$_SERVER['DOCUMENT_ROOT'] . (( !empty(\$GLOBALS['HELM_MAILER_PRESENT']) ) ? '/lib/stubmail/mailer.php' : '/lib/missing-mailer.php');",
		$code
	);
	$code = str_replace(
		'@mail($to, $subject, $html, $headers)',
		'((bool) ($GLOBALS[\'HELM_MAIL\'] ?? false))',
		$code
	);
	file_put_contents($dest, $code);
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
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1h_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	helm_patch($root . '/content/general_pages/epc_auth_smtp.php', $doc . '/epc_auth_smtp.php');
	$cleanup = function () use ($admin, $dbName, $doc) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};
	class DP_Config
	{
		public $smtp_mode = '0';
		public $smtp_host = '';
		public $smtp_port = '';
		public $smtp_encryption = '';
		public $smtp_username = '';
		public $smtp_password = '';
		public $from_email = '';
		public $from_name = '';
	}
	if (!function_exists('epc_portal_is_super_cp_host')) {
		eval('function epc_portal_is_super_cp_host() { return !empty($GLOBALS["HELM_SUPER"]); }');
	}
	if (!function_exists('epc_portal_load_site_settings')) {
		eval('function epc_portal_load_site_settings() { return is_array($GLOBALS["HELM_SETTINGS"] ?? null) ? $GLOBALS["HELM_SETTINGS"] : array(); }');
	}
	if (!function_exists('epc_auth_otp_ensure_schema')) {
		eval('function epc_auth_otp_ensure_schema(PDO $pdo) { $pdo->exec("CREATE TABLE IF NOT EXISTS `epc_auth_otp_requests` (`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY, `email` VARCHAR(120) NOT NULL, `code_hash` VARCHAR(64) NOT NULL, `tenant_key` VARCHAR(64) NOT NULL DEFAULT \'\', `context_json` TEXT NULL, `expires_at` INT NOT NULL, `ip_address` VARCHAR(45) NOT NULL DEFAULT \'\', `created_at` INT NOT NULL DEFAULT 0) ENGINE=InnoDB DEFAULT CHARSET=utf8"); }');
	}
	$GLOBALS['HELM_NOW'] = 1760083200;
	$GLOBALS['HELM_MAIL'] = false;
	$GLOBALS['HELM_MAILER_PRESENT'] = false;
	$GLOBALS['HELM_SMTP_FAIL'] = '';
	$GLOBALS['HELM_SUPER'] = false;
	$GLOBALS['HELM_SETTINGS'] = array();
	$GLOBALS['db_link'] = $pdo;
	$_SERVER = array('DOCUMENT_ROOT' => $doc);
	$GLOBALS['DP_Config'] = new DP_Config();
	require $doc . '/epc_auth_smtp.php';
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1helm_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1helm_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
