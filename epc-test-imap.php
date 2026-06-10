<?php
/**
 * Test Gmail IMAP for price uploads (config.php prices_email_* → pyprices).
 * GET/POST: token=epartscart-deploy-2026&key=<tech_key>
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_GET['token'] ?? $_POST['token'] ?? '') !== $deployToken) {
	http_response_code(403);
	exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;

$techKey = (string)($_GET['key'] ?? $_POST['key'] ?? '');
if ($techKey === '' || $techKey !== $DP_Config->tech_key) {
	http_response_code(403);
	exit(json_encode(['status' => false, 'message' => 'Invalid tech_key']));
}

$server = trim((string)$DP_Config->prices_email_server);
$port = trim((string)$DP_Config->prices_email_port);
$encryption = strtolower(trim((string)$DP_Config->prices_email_encryption));
$username = trim((string)$DP_Config->prices_email_username);
$password = (string)$DP_Config->prices_email_password;

$expected = [
	'server' => 'imap.gmail.com',
	'port' => '993',
	'encryption' => 'ssl',
	'username' => 'epartscart@gmail.com',
];

$result = [
	'status' => false,
	'tested_at' => date('c'),
	'config' => [
		'prices_email_server' => $server,
		'prices_email_port' => $port,
		'prices_email_encryption' => $encryption,
		'prices_email_username' => $username,
		'password_configured' => $password !== '',
		'password_length' => strlen($password),
	],
	'expected_gmail' => $expected,
	'settings_match_gmail' => (
		$server === $expected['server']
		&& $port === $expected['port']
		&& ($encryption === 'ssl' || $encryption === 'ssl/tls')
		&& strcasecmp($username, $expected['username']) === 0
	),
	'php_imap_extension' => function_exists('imap_open'),
	'connection' => null,
	'pyprices' => null,
	'hints' => [],
];

if ($server === '' || $username === '' || $password === '') {
	$result['message'] = 'IMAP not configured in config.php (prices_email_server, username, password)';
	$result['hints'][] = 'Use CP → site mail settings or epc-restore-config-email-block.php with a Gmail App Password (not your normal password).';
	echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

/**
 * @return array<string,mixed>
 */
function epc_test_imap_via_python(string $host, string $port, string $user, string $password): array
{
	$py = <<<'PY'
import imaplib
import json
import sys

host, port, user, password = sys.argv[1:5]
out = {"ok": False, "error": ""}
try:
    M = imaplib.IMAP4_SSL(host, int(port))
    M.login(user, password)
    typ, data = M.select("INBOX", readonly=True)
    out["ok"] = typ == "OK"
    out["messages"] = int(data[0]) if typ == "OK" and data and data[0] else 0
    M.logout()
except Exception as e:
    out["error"] = str(e)
print(json.dumps(out))
PY;
	$tmp = tempnam(sys_get_temp_dir(), 'epc_imap_');
	if ($tmp === false) {
		return ['ok' => false, 'error' => 'tempnam failed'];
	}
	$pyFile = $tmp . '.py';
	rename($tmp, $pyFile);
	file_put_contents($pyFile, $py);

	$candidates = ['python3', 'python', '/usr/bin/python3', '/usr/local/bin/python3'];
	$lastOut = [];
	$lastCode = 1;
	foreach ($candidates as $bin) {
		$cmd = escapeshellcmd($bin) . ' ' . escapeshellarg($pyFile) . ' '
			. escapeshellarg($host) . ' ' . escapeshellarg($port) . ' '
			. escapeshellarg($user) . ' ' . escapeshellarg($password) . ' 2>&1';
		$lastOut = [];
		$lastCode = 1;
		@exec($cmd, $lastOut, $lastCode);
		if ($lastCode === 0 && count($lastOut) > 0) {
			@unlink($pyFile);
			$decoded = json_decode(implode("\n", $lastOut), true);
			return is_array($decoded) ? $decoded : ['ok' => false, 'error' => 'Invalid JSON from Python', 'raw' => implode("\n", $lastOut)];
		}
	}
	@unlink($pyFile);
	return [
		'ok' => false,
		'error' => 'Python IMAP test did not run',
		'exec_output' => implode("\n", array_slice($lastOut, 0, 5)),
	];
}

$portPart = $port !== '' ? $port : '993';
$pythonImap = epc_test_imap_via_python($server, $portPart, $username, $password);
$result['python_imap'] = $pythonImap;

if (!$result['php_imap_extension']) {
	$result['hints'][] = 'PHP imap extension is not installed (optional). Pyprices uses Python imaplib for e-mail imports.';
	if (!empty($pythonImap['ok'])) {
		$result['status'] = true;
		$result['message'] = 'Python IMAP login successful (pyprices path)';
		$result['connection'] = [
			'ok' => true,
			'via' => 'python_imaplib',
			'messages' => (int)($pythonImap['messages'] ?? 0),
		];
	} else {
		$result['message'] = 'Python IMAP login failed';
		$pyErr = (string)($pythonImap['error'] ?? '');
		if (stripos($pyErr, 'AUTHENTICATIONFAILED') !== false || stripos($pyErr, 'Invalid credentials') !== false) {
			$result['hints'][] = 'Gmail rejected login. Create a new App Password at https://myaccount.google.com/apppasswords and update config.php prices_email_password.';
			$result['hints'][] = 'Use the full Gmail address as username. Enable IMAP in Gmail → Settings → See all settings → Forwarding and POP/IMAP.';
		}
	}
	// Continue to pyprices probe below (do not exit early).
} else {
$flags = '/imap/ssl/novalidate-cert';
if ($encryption === 'tls' || $encryption === 'starttls') {
	$flags = '/imap/tls/novalidate-cert';
} elseif ($encryption === 'off' || $encryption === '') {
	$flags = '/imap/notls';
}
$mailbox = '{' . $server . ':' . $portPart . $flags . '}INBOX';

$prev = error_reporting(0);
$mbox = @imap_open(
	$mailbox,
	$username,
	$password,
	0,
	1,
	['DISABLE_AUTHENTICATOR' => 'GSSAPI']
);
$imapErr = (string)imap_last_error();
error_reporting($prev);

if ($mbox) {
	$check = imap_check($mbox);
	$result['connection'] = [
		'ok' => true,
		'mailbox' => $mailbox,
		'messages' => $check ? (int)$check->Nmsgs : imap_num_msg($mbox),
		'recent' => $check ? (int)$check->Recent : null,
	];
	@imap_close($mbox);
	$result['status'] = true;
	$result['message'] = 'IMAP login successful';
} else {
	$result['connection'] = [
		'ok' => false,
		'mailbox' => $mailbox,
		'error' => $imapErr !== '' ? $imapErr : 'imap_open failed',
	];
	$result['message'] = 'IMAP login failed';
	if (stripos($imapErr, 'auth') !== false || stripos($imapErr, 'credentials') !== false) {
		$result['hints'][] = 'Use a Gmail App Password: https://myaccount.google.com/apppasswords (16 characters, no spaces). Enable IMAP in Gmail settings.';
		$result['hints'][] = 'Account must have 2-Step Verification enabled before App Passwords work.';
	}
	if (stripos($imapErr, 'certificate') !== false) {
		$result['hints'][] = 'Certificate issue — server uses /novalidate-cert; check host firewall allows outbound port 993.';
	}
}
} // end PHP imap branch

// Pyprices probe (empty task list still reports imap_status when email tasks would run)
$pyUrl = rtrim($DP_Config->domain_path, '/') . '/pyprices/api.py';
$post = http_build_query(['key' => $DP_Config->tech_key, 'list_to_handle' => '[]']);
$ch = curl_init($pyUrl);
curl_setopt_array($ch, [
	CURLOPT_RETURNTRANSFER => true,
	CURLOPT_POST => true,
	CURLOPT_POSTFIELDS => $post,
	CURLOPT_TIMEOUT => 25,
	CURLOPT_SSL_VERIFYHOST => 0,
	CURLOPT_SSL_VERIFYPEER => 0,
]);
$pyBody = (string)curl_exec($ch);
$pyCode = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
curl_close($ch);
$pyJson = json_decode($pyBody, true);
$result['pyprices'] = [
	'http_code' => $pyCode,
	'imap_status' => is_array($pyJson) ? ($pyJson['imap_status'] ?? null) : null,
	'message' => is_array($pyJson) ? ($pyJson['message'] ?? '') : substr($pyBody, 0, 300),
];

if ($result['status'] && isset($result['pyprices']['imap_status']) && $result['pyprices']['imap_status'] === false) {
	$result['hints'][] = 'PHP IMAP works but pyprices reports imap_status=false — redeploy pyprices or check pyprices reads the same config.php prices_email_* values.';
}

echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
