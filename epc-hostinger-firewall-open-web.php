<?php
/**
 * Platform VPS firewall — open inbound HTTP/HTTPS for direct GoDaddy tenant DNS.
 * Applies to ALL tenants on the ecomae platform (not epartscart-specific hosting).
 * Hostinger = where the platform VPS lives; epartscart is a tenant domain only.
 * https://www.ecomae.com/epc-hostinger-firewall-open-web.php?token=...&apply=1
 * Optional: hostinger_token=... (hPanel API token) or env HOSTINGER_API_TOKEN
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(180);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$apply = !empty($_GET['apply']);
$platformIp = epc_portal_platform_ip();
$hostname = 'www.epartscart.com';
$bare = 'epartscart.com';
$apiToken = trim((string) ($_GET['hostinger_token'] ?? getenv('HOSTINGER_API_TOKEN') ?: ''));
$vmId = (int) ($_GET['vm_id'] ?? getenv('HOSTINGER_VM_ID') ?: 0);
$firewallId = (int) ($_GET['firewall_id'] ?? getenv('HOSTINGER_FIREWALL_ID') ?: 0);

function epc_fw_run(string $cmd): string
{
	$r = epc_clp_run_cmd($cmd);
	$out = isset($r['output']) ? trim((string) $r['output']) : '';
	$code = isset($r['code']) ? (int) $r['code'] : -1;
	return ($out !== '' ? $out : '(empty)') . ' [exit=' . $code . ']';
}

function epc_fw_api(string $token, string $method, string $path, ?array $body = null): array
{
	$url = 'https://developers.hostinger.com' . $path;
	$headers = "Authorization: Bearer {$token}\r\nAccept: application/json\r\n";
	$content = '';
	if ($body !== null) {
		$content = json_encode($body);
		$headers .= "Content-Type: application/json\r\n";
	}
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => $method,
			'header' => $headers,
			'content' => $content,
			'timeout' => 60,
			'ignore_errors' => true,
		),
		'ssl' => array('verify_peer' => true),
	));
	$raw = @file_get_contents($url, false, $ctx);
	$status = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$status = (int) $m[1];
	}
	$json = is_string($raw) ? json_decode($raw, true) : null;
	return array('status' => $status, 'body' => $json !== null ? $json : $raw);
}

function epc_fw_probe(string $url, string $hostHeader = ''): string
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 15, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$t0 = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	return ($body !== false && $code > 0 ? 'OK' : 'FAIL') . " HTTP {$code} {$ms}ms";
}

echo "=== Platform VPS firewall — open web for direct tenant DNS ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'platform_ip=' . $platformIp . "\n";
echo 'server_time=' . gmdate('c') . "\n\n";

echo "=== BEFORE probes ===\n";
echo "  origin http://127.0.0.1/ Host {$hostname}: " . epc_fw_probe('http://127.0.0.1/', $hostname) . "\n";
echo "  hairpin http://{$platformIp}/ Host {$hostname}: " . epc_fw_probe('http://' . $platformIp . '/', $hostname) . "\n";
echo "  public https://{$hostname}/: " . epc_fw_probe('https://' . $hostname . '/') . "\n";
echo "  public https://{$hostname}/cp/: " . epc_fw_probe('https://' . $hostname . '/cp/') . "\n\n";

echo "=== Listening ports ===\n";
echo epc_fw_run('ss -tlnp 2>/dev/null | grep -E ":80 |:443 " || netstat -tlnp 2>/dev/null | grep -E ":80 |:443 "') . "\n\n";

echo "=== OS firewall (ufw / iptables) ===\n";
echo 'ufw: ' . epc_fw_run('sudo -n ufw status 2>&1 || ufw status 2>&1') . "\n";
echo 'iptables 80/443: ' . epc_fw_run('sudo -n iptables -L INPUT -n 2>&1 | grep -E "dpt:(80|443)" | head -10') . "\n\n";

$changes = array();

if ($apply) {
	echo "=== Apply OS-level allow 80/443 ===\n";
	foreach (array(
		'sudo -n ufw allow 80/tcp comment "epc-direct-dns"',
		'sudo -n ufw allow 443/tcp comment "epc-direct-dns"',
		'sudo -n ufw reload',
		'sudo -n iptables -C INPUT -p tcp --dport 80 -j ACCEPT 2>/dev/null || sudo -n iptables -I INPUT -p tcp --dport 80 -j ACCEPT',
		'sudo -n iptables -C INPUT -p tcp --dport 443 -j ACCEPT 2>/dev/null || sudo -n iptables -I INPUT -p tcp --dport 443 -j ACCEPT',
		'runuser -u root -- ufw allow 80/tcp 2>&1',
		'runuser -u root -- ufw allow 443/tcp 2>&1',
	) as $cmd) {
		$out = epc_fw_run($cmd);
		echo $cmd . ': ' . $out . "\n";
		if (stripos($out, 'password is required') === false && stripos($out, '[exit=0]') !== false) {
			$changes[] = $cmd;
		}
	}
	echo "\n";

	if ($apiToken !== '') {
		echo "=== Hostinger VPS API firewall ===\n";
		$vms = epc_fw_api($apiToken, 'GET', '/api/vps/v1/virtual-machines');
		echo 'list_vms HTTP ' . $vms['status'] . "\n";
		if ($vmId <= 0 && is_array($vms['body'])) {
			$list = $vms['body']['data'] ?? $vms['body'];
			if (is_array($list)) {
				foreach ($list as $vm) {
					$ips = $vm['ipv4'] ?? array();
					$ip = is_array($ips) && isset($ips[0]['address']) ? (string) $ips[0]['address'] : '';
					if ($ip === $platformIp || $ip === '') {
						$vmId = (int) ($vm['id'] ?? 0);
						if ($ip === $platformIp) {
							break;
						}
					}
				}
			}
		}
		echo 'vm_id=' . ($vmId > 0 ? (string) $vmId : 'NOT_FOUND') . "\n";

		if ($firewallId <= 0) {
			$fws = epc_fw_api($apiToken, 'GET', '/api/vps/v1/firewall');
			echo 'list_firewalls HTTP ' . $fws['status'] . "\n";
			if (is_array($fws['body'])) {
				$flist = $fws['body']['data'] ?? $fws['body'];
				if (is_array($flist)) {
					foreach ($flist as $fw) {
						$name = (string) ($fw['name'] ?? '');
						if (stripos($name, 'web') !== false || stripos($name, 'http') !== false || $firewallId <= 0) {
							$firewallId = (int) ($fw['id'] ?? 0);
						}
					}
				}
			}
		}
		echo 'firewall_id=' . ($firewallId > 0 ? (string) $firewallId : 'NOT_FOUND') . "\n";

		if ($firewallId > 0) {
			foreach (array('80', '443') as $port) {
				$rule = array(
					'protocol' => 'TCP',
					'port' => $port,
					'source' => 'any',
					'sourceDetail' => 'any',
				);
				$res = epc_fw_api($apiToken, 'POST', '/api/vps/v1/firewall/' . $firewallId . '/rules', $rule);
				echo "create_rule TCP {$port}: HTTP {$res['status']} " . json_encode($res['body']) . "\n";
				if ($res['status'] >= 200 && $res['status'] < 300) {
					$changes[] = "Hostinger API allow TCP {$port}";
				}
			}
			if ($vmId > 0) {
				$act = epc_fw_api($apiToken, 'POST', '/api/vps/v1/firewall/' . $firewallId . '/activate/' . $vmId);
				echo 'activate_firewall HTTP ' . $act['status'] . "\n";
				$sync = epc_fw_api($apiToken, 'POST', '/api/vps/v1/firewall/' . $firewallId . '/sync/' . $vmId);
				echo 'sync_firewall HTTP ' . $sync['status'] . "\n";
				if ($sync['status'] >= 200 && $sync['status'] < 300) {
					$changes[] = 'Hostinger firewall synced to VM ' . $vmId;
				}
			}
		}
		echo "\n";
	} else {
		echo "=== Hostinger API skipped ===\n";
		echo "Pass hostinger_token= (hPanel → API) or set HOSTINGER_API_TOKEN env.\n";
		echo "Manual hPanel: VPS → Security → Firewall → Add rule TCP 80 + 443 from Anywhere → Sync.\n\n";
	}
} else {
	echo "Dry run — add apply=1 to attempt firewall changes.\n\n";
}

echo "=== AFTER probes ===\n";
echo "  hairpin http://{$platformIp}/ Host {$hostname}: " . epc_fw_probe('http://' . $platformIp . '/', $hostname) . "\n";
echo "  public https://{$hostname}/: " . epc_fw_probe('https://' . $hostname . '/') . "\n";
echo "  public https://{$hostname}/cp/: " . epc_fw_probe('https://' . $hostname . '/cp/') . "\n";
echo "  public https://www.taxofinca.com/: " . epc_fw_probe('https://www.taxofinca.com/') . "\n\n";

echo "=== GoDaddy DNS (direct — NO Cloudflare) ===\n";
echo "A  @   → {$platformIp}   TTL 600\n";
echo "A  www → {$platformIp}   TTL 600\n";
echo "Remove CNAME/A records pointing to Cloudflare (104.x / 172.x).\n";
echo "Do NOT orange-cloud / proxy — DNS only at GoDaddy.\n";
echo "Optional apex redirect handled by nginx (301 epartscart.com → www).\n\n";

echo "=== Changes applied ===\n";
if ($changes === array()) {
	echo "NONE (OS sudo blocked and/or no Hostinger API token).\n";
	echo "BLOCKER: Platform VPS firewall drops direct public 80/443.\n";
	echo "Fix in hPanel (ecomae VPS): Security → Firewall → accept TCP 80,443 from anywhere → Sync.\n";
} else {
	foreach ($changes as $c) {
		echo "  + {$c}\n";
	}
}

echo "\nVerify externally (mandatory):\n";
echo "  curl -skI https://{$hostname}/\n";
echo "  curl -skI https://{$hostname}/cp/\n";
echo "Probe: epc-epartscart-connectivity-probe.php?token=" . epc_deploy_token() . "\n";
