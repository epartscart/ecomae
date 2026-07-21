<?php
/**
 * CP utility: show this server's public IP (for warehouse / supplier allowlists).
 * Linked from Shop → Logistics → Storages.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (PDOException $e) {
	exit(json_encode(array('status' => false, 'message' => 'No DB connect')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
$multilang_params = multilang_init();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	exit('Forbidden');
}

/**
 * Fetch first public IPv4 from a list of plain-text IP endpoints.
 */
function epc_usefull_fetch_public_ipv4(): array
{
	$endpoints = array(
		'https://api.ipify.org',
		'https://ipv4.icanhazip.com',
		'http://checkip.amazonaws.com',
		'https://ifconfig.me/ip',
		'https://icanhazip.com',
	);
	$errors = array();
	foreach ($endpoints as $url) {
		$ip = '';
		$err = '';
		if (function_exists('curl_init')) {
			$ch = curl_init($url);
			curl_setopt_array($ch, array(
				CURLOPT_RETURNTRANSFER => true,
				CURLOPT_FOLLOWLOCATION => true,
				CURLOPT_CONNECTTIMEOUT => 4,
				CURLOPT_TIMEOUT => 6,
				CURLOPT_USERAGENT => 'ePartsCart-IP-Check/1.0',
				CURLOPT_SSL_VERIFYPEER => true,
			));
			$body = curl_exec($ch);
			if ($body === false) {
				$err = curl_error($ch);
			}
			$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
			curl_close($ch);
			if ($body !== false && $code >= 200 && $code < 300) {
				$ip = trim((string) $body);
			} elseif ($err === '' && $code > 0) {
				$err = 'HTTP ' . $code;
			}
		} else {
			$ctx = stream_context_create(array(
				'http' => array('timeout' => 6, 'user_agent' => 'ePartsCart-IP-Check/1.0'),
				'ssl' => array('verify_peer' => true, 'verify_peer_name' => true),
			));
			$body = @file_get_contents($url, false, $ctx);
			if ($body === false) {
				$err = 'file_get_contents failed';
			} else {
				$ip = trim((string) $body);
			}
		}
		if ($ip !== '' && preg_match('/\b(\d{1,3}(?:\.\d{1,3}){3})\b/', $ip, $m)) {
			$candidate = $m[1];
			if (filter_var($candidate, FILTER_VALIDATE_IP, FILTER_FLAG_IPV4 | FILTER_FLAG_NO_PRIV_RANGE | FILTER_FLAG_NO_RES_RANGE)) {
				return array('ok' => true, 'ip' => $candidate, 'source' => $url, 'errors' => $errors);
			}
			// Accept public-looking IPv4 even if NO_PRIV filter is strict on some hosts
			if (filter_var($candidate, FILTER_VALIDATE_IP, FILTER_FLAG_IPV4)) {
				return array('ok' => true, 'ip' => $candidate, 'source' => $url, 'errors' => $errors);
			}
		}
		$errors[] = $url . ': ' . ($err !== '' ? $err : 'no IPv4 in response');
	}

	$fallback = trim((string) ($_SERVER['SERVER_ADDR'] ?? ''));
	if ($fallback !== '' && filter_var($fallback, FILTER_VALIDATE_IP, FILTER_FLAG_IPV4)) {
		return array('ok' => true, 'ip' => $fallback, 'source' => 'SERVER_ADDR', 'errors' => $errors);
	}

	return array('ok' => false, 'ip' => '', 'source' => '', 'errors' => $errors);
}

$result = epc_usefull_fetch_public_ipv4();
$title = function_exists('translate_str_by_id') ? translate_str_by_id(4686) : 'Server public IP';
$help = function_exists('translate_str_by_id') ? translate_str_by_id(4687) : 'Use this IP when a supplier asks for your server allowlist.';
$fail = function_exists('translate_str_by_id') ? translate_str_by_id(4688) : 'Failed to get IP address';

header('Content-Type: text/html; charset=utf-8');
header('Cache-Control: no-store');
?>
<!DOCTYPE html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title><?php echo htmlspecialchars((string) $title, ENT_QUOTES, 'UTF-8'); ?></title>
	<style>
		body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;
			font-family:"Sora",ui-sans-serif,system-ui,sans-serif;background:linear-gradient(160deg,#fff7f7 0%,#ffffff 45%,#f8fafc 100%);color:#0f172a}
		.card{width:min(520px,92vw);background:#fff;border:1px solid rgba(220,38,38,.14);border-radius:16px;
			box-shadow:0 12px 32px rgba(185,28,28,.08);padding:28px 28px 22px}
		.kicker{font-size:11px;font-weight:800;letter-spacing:.12em;text-transform:uppercase;color:#dc2626;margin:0 0 8px}
		h1{margin:0 0 14px;font-size:20px;font-weight:800;letter-spacing:-.02em}
		.ip{font-size:34px;font-weight:800;letter-spacing:.02em;color:#7f1d1d;margin:0 0 12px;word-break:break-all}
		.help{margin:0;color:#57534e;font-size:14px;line-height:1.45}
		.meta{margin:14px 0 0;font-size:12px;color:#78716c}
		.err{color:#991b1b;font-weight:700}
		.back{display:inline-block;margin-top:18px;color:#dc2626;font-weight:700;text-decoration:none}
		.back:hover{text-decoration:underline}
	</style>
</head>
<body>
	<div class="card">
		<p class="kicker">Server utility</p>
		<h1><?php echo htmlspecialchars((string) $title, ENT_QUOTES, 'UTF-8'); ?></h1>
		<?php if (!empty($result['ok'])) { ?>
			<p class="ip"><?php echo htmlspecialchars((string) $result['ip'], ENT_QUOTES, 'UTF-8'); ?></p>
			<p class="help"><?php echo htmlspecialchars((string) $help, ENT_QUOTES, 'UTF-8'); ?></p>
			<p class="meta">Source: <?php echo htmlspecialchars((string) $result['source'], ENT_QUOTES, 'UTF-8'); ?></p>
		<?php } else { ?>
			<p class="err"><?php echo htmlspecialchars((string) $fail, ENT_QUOTES, 'UTF-8'); ?></p>
			<p class="help">Could not reach any public IP service from this server. Ask hosting for the outbound IPv4, or check firewall / egress rules.</p>
			<?php if (!empty($result['errors'])) { ?>
			<p class="meta"><?php echo htmlspecialchars(implode(' · ', array_slice($result['errors'], 0, 3)), ENT_QUOTES, 'UTF-8'); ?></p>
			<?php } ?>
		<?php } ?>
		<a class="back" href="/<?php echo htmlspecialchars(trim((string) ($DP_Config->backend_dir ?? 'cp'), '/'), ENT_QUOTES, 'UTF-8'); ?>/shop/logistics/storages">&larr; Back to storages</a>
	</div>
</body>
</html>
