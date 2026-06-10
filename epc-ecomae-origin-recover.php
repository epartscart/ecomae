<?php
/**
 * URGENT: www.ecomae.com nginx default 404 — vhost missing from disk.
 * Operator one-pager + localhost probes. Deploy blocked until vhost restored.
 *
 * After vhost fix (CloudPanel):
 *   https://www.ecomae.com/epc-ecomae-origin-recover.php?token=epartscart-deploy-2026
 *   https://www.ecomae.com/epc-ecomae-origin-recover.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

$apply = !empty($_GET['apply']);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

function epc_recover_probe(string $url, string $host): array
{
	$headers = $host !== '' ? ("Host: {$host}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 8, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$t0 = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$snippet = is_string($body) ? substr(preg_replace('/\s+/', ' ', strip_tags($body)), 0, 80) : '';
	return array('code' => $code, 'ms' => $ms, 'snippet' => $snippet, 'ok' => $code >= 200 && $code < 500);
}

echo "=== ECOMAE ORIGIN RECOVER — OPERATOR ONE-PAGE ===\n";
echo 'time=' . date('c') . "  apply=" . ($apply ? '1' : '0') . "\n\n";

echo "SYMPTOM\n";
echo "  Public https://www.ecomae.com/ returns HTTP 404 with body footer \"nginx\".\n";
echo "  That is nginx DEFAULT vhost — www.ecomae.com site config is NOT on disk.\n";
echo "  push_one / platform-fix deploys fail with the same 404 until fixed.\n\n";

echo "FIX IN CLOUDPANEL (3 steps — Kodee)\n";
echo "  1) SAVE VHOST\n";
echo "     CloudPanel https://31.97.216.247:8443 → Sites → www.ecomae.com → Vhost tab.\n";
echo "     Replace entire editor with go-live-logs/ecomae-vhost-paste-for-kodee.txt\n";
echo "     (repo deploy-epartscart). Must include server_name www.ecomae.com on 443 AND\n";
echo "     listen 8080 backend block. Click Save.\n\n";
echo "  2) VERIFY ON DISK (SSH as root)\n";
echo "     grep -c 'server_name www.ecomae.com' /etc/nginx/sites-enabled/www.ecomae.com.conf\n";
echo "     Expected: 2  (443 front + 8080 PHP backend). If file missing or count 0 → Save failed.\n";
echo "     grep -E 'listen 8080|server_name www.ecomae.com' /etc/nginx/sites-enabled/www.ecomae.com.conf\n\n";
echo "  3) TEST + RELOAD\n";
echo "     nginx -t && systemctl reload nginx\n";
echo "     curl -sI -H 'Host: www.ecomae.com' http://127.0.0.1/ | head -1\n";
echo "     Expected: HTTP/1.1 200 or 301/302 — NOT 404.\n\n";

echo "LOCALHOST PROBE COMMANDS (run on VPS after step 1)\n";
echo "  curl -sI -H 'Host: www.ecomae.com' http://127.0.0.1/\n";
echo "  curl -sI -H 'Host: www.ecomae.com' http://127.0.0.1/cp/\n";
echo "  curl -sI -H 'Host: www.ecomae.com' http://127.0.0.1/index.php\n";
echo "  curl -sI -H 'Host: www.epartscart.com' http://127.0.0.1/\n";
echo "  ls -la /etc/nginx/sites-enabled/ | grep ecomae\n\n";

echo "AFTER ORIGIN 200: from dev machine run\n";
echo "  python tools/ecomae_restore_when_up.py\n";
echo "  then epc-ecomae-force-marketing-home.php?token=...&apply=1\n\n";

echo str_repeat('=', 60) . "\n";
echo "LIVE PROBES FROM THIS SERVER\n\n";

$probes = array(
	array('label' => 'origin / www.ecomae.com', 'url' => 'http://127.0.0.1/', 'host' => 'www.ecomae.com'),
	array('label' => 'origin /cp/ www.ecomae.com', 'url' => 'http://127.0.0.1/cp/', 'host' => 'www.ecomae.com'),
	array('label' => 'origin index.php', 'url' => 'http://127.0.0.1/index.php', 'host' => 'www.ecomae.com'),
	array('label' => 'origin / www.epartscart.com', 'url' => 'http://127.0.0.1/', 'host' => 'www.epartscart.com'),
);

echo "=== BEFORE (127.0.0.1) ===\n";
foreach ($probes as $p) {
	$r = epc_recover_probe($p['url'], $p['host']);
	echo "  {$p['label']}: HTTP {$r['code']} {$r['ms']}ms";
	if ($r['snippet'] !== '') {
		echo " — {$r['snippet']}";
	}
	echo "\n";
}

echo "\n=== vhost file on disk ===\n";
foreach (array(
	'test -f /etc/nginx/sites-enabled/www.ecomae.com.conf && echo EXISTS || echo MISSING',
	"grep -c 'server_name www.ecomae.com' /etc/nginx/sites-enabled/www.ecomae.com.conf 2>/dev/null || echo 0",
) as $cmd) {
	echo epc_clp_run_cmd($cmd)['output'] . "\n";
}

echo "\n=== Services ===\n";
foreach (array(
	'ss -lntp | grep -E ":80|:443|:8080" || true',
	'pgrep -a nginx | head -3 || true',
	'pgrep -a "php-fpm|php8" | head -3 || true',
) as $cmd) {
	echo epc_clp_run_cmd($cmd)['output'] . "\n";
}

if ($apply) {
	echo "\n=== APPLY (reload only — does NOT recreate missing vhost) ===\n";
	foreach (array(
		'nginx -t 2>&1',
		'systemctl reload nginx 2>&1 || service nginx reload 2>&1',
	) as $cmd) {
		$r = epc_clp_run_cmd($cmd);
		echo $cmd . ': ' . trim($r['output']) . " [exit={$r['code']}]\n";
	}
}

echo "\n=== AFTER (127.0.0.1) ===\n";
foreach ($probes as $p) {
	$r = epc_recover_probe($p['url'], $p['host']);
	echo "  {$p['label']}: HTTP {$r['code']} {$r['ms']}ms\n";
}

$home = epc_recover_probe('http://127.0.0.1/', 'www.ecomae.com');
if ($home['code'] === 404 || $home['code'] === 0) {
	echo "\nBLOCKED: vhost still missing or wrong. Use CloudPanel Save (steps 1–3 above).\n";
} elseif ($home['code'] >= 200 && $home['code'] < 400) {
	echo "\nORIGIN UP — run python tools/ecomae_restore_when_up.py from dev machine.\n";
}
