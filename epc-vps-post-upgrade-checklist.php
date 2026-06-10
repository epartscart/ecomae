<?php
/**
 * Hostinger VPS post OS upgrade / recreate — recovery checklist.
 * https://www.ecomae.com/epc-vps-post-upgrade-checklist.php?token=epartscart-deploy-2026
 * Optional live probes (from server): &probe=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$token = epc_deploy_token();
$platformIp = epc_portal_platform_ip();
$vpsHost = 'srv1672837.hstgr.cloud';
$runProbe = !empty($_GET['probe']);

$sites = array(
	array('label' => 'ECOM AE platform (marketing)', 'url' => 'https://www.ecomae.com/', 'expect' => '200'),
	array('label' => 'ECOM AE Super CP', 'url' => 'https://www.ecomae.com/cp/', 'expect' => '200 or 302'),
	array('label' => 'epartscart tenant', 'url' => 'https://www.epartscart.com/', 'expect' => '200'),
	array('label' => 'epartscart CP', 'url' => 'https://www.epartscart.com/cp/', 'expect' => '200 or 302'),
	array('label' => 'taxofinca tenant', 'url' => 'https://www.taxofinca.com/', 'expect' => '200'),
);

function epc_vps_probe_url(string $url): array
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 15, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$t0 = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	$ok = $code >= 200 && $code < 400;
	if ($code === 0) {
		$ok = false;
	}
	return array(
		'code' => $code,
		'ms' => $ms,
		'ok' => $ok,
		'bytes' => is_string($body) ? strlen($body) : 0,
		'err' => $code === 0 ? 'no HTTP response (timeout / connection refused)' : '',
	);
}

$probeResults = array();
if ($runProbe) {
	foreach ($sites as $s) {
		$probeResults[] = array_merge($s, epc_vps_probe_url($s['url']));
	}
}

header('Content-Type: text/html; charset=utf-8');
?><!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>VPS post-upgrade recovery — ECOM AE platform</title>
<style>
:root { --bg:#0f172a; --card:#1e293b; --text:#e2e8f0; --muted:#94a3b8; --accent:#38bdf8; --ok:#4ade80; --warn:#fbbf24; --bad:#f87171; }
* { box-sizing:border-box; }
body { margin:0; font-family:system-ui,-apple-system,Segoe UI,sans-serif; background:var(--bg); color:var(--text); line-height:1.55; }
.wrap { max-width:960px; margin:0 auto; padding:32px 20px 64px; }
h1 { font-size:1.65rem; margin:0 0 8px; }
.lead { color:var(--muted); margin:0 0 28px; }
.banner { background:#451a03; border:1px solid #92400e; border-radius:12px; padding:16px 20px; margin:0 0 22px; }
.banner strong { color:var(--warn); }
.card { background:var(--card); border:1px solid #334155; border-radius:12px; padding:20px 22px; margin:0 0 18px; }
h2 { font-size:1.1rem; margin:0 0 12px; color:var(--accent); }
h3 { font-size:0.95rem; margin:16px 0 8px; color:var(--muted); }
table { width:100%; border-collapse:collapse; font-size:14px; }
th,td { text-align:left; padding:10px 12px; border-bottom:1px solid #334155; vertical-align:top; }
th { color:var(--muted); font-weight:600; }
code, pre { font-family:ui-monospace,Consolas,monospace; background:#0b1220; }
code { padding:2px 6px; border-radius:4px; }
pre { padding:12px 14px; border-radius:8px; overflow-x:auto; font-size:13px; margin:10px 0; }
ul,ol { margin:8px 0 0; padding-left:20px; }
li { margin:6px 0; }
.tag { display:inline-block; padding:2px 8px; border-radius:999px; font-size:12px; font-weight:600; }
.tag-ok { background:#14532d; color:var(--ok); }
.tag-warn { background:#713f12; color:var(--warn); }
.tag-bad { background:#7f1d1d; color:var(--bad); }
a { color:var(--accent); }
.probe-row-ok td:first-child::before { content:"✓ "; color:var(--ok); }
.probe-row-bad td:first-child::before { content:"✗ "; color:var(--bad); }
</style>
</head>
<body>
<div class="wrap">
<h1>VPS post-upgrade recovery checklist</h1>
<p class="lead">Platform VPS <code><?php echo htmlspecialchars($vpsHost, ENT_QUOTES, 'UTF-8'); ?></code> · IP <code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code> · Ubuntu upgrade / Hostinger <strong>Recreating</strong> window</p>

<div class="banner">
<strong>Infrastructure downtime, not DNS.</strong> While hPanel shows <em>Recreating</em> (often 24→26 upgrade, up to ~10 minutes), <code>www.ecomae.com</code>, <code>www.epartscart.com</code>, and <code>www.taxofinca.com</code> will time out or fail — this is expected. Do not change tenant DNS or run connectivity-fix until the VPS status is <strong>Running</strong> and SSH works.
</div>

<div class="card">
<h2>Phase 1 — While server is down (hPanel)</h2>
<ol>
<li>Hostinger hPanel → VPS → wait until status leaves <span class="tag tag-warn">Recreating</span> → <span class="tag tag-ok">Running</span></li>
<li>Confirm notification email / dashboard: recreate finished</li>
<li>SSH: <code>ssh root@<?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code> (or hostname <code><?php echo htmlspecialchars($vpsHost, ENT_QUOTES, 'UTF-8'); ?></code>)</li>
<li>On VPS, verify core services:
<pre>systemctl is-active nginx
systemctl is-active php*-fpm 2>/dev/null || systemctl is-active php8.3-fpm
systemctl is-active mariadb || systemctl is-active mysql
systemctl is-active clp-agent 2>/dev/null || echo "CloudPanel: check https://<?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?>:8443"</pre>
</li>
<li>CloudPanel UI: sites <code>www.ecomae.com</code> present, SSL valid, docroot <code>/home/ecomae/htdocs/www.ecomae.com</code></li>
</ol>
</div>

<div class="card">
<h2>Phase 2 — When server returns (HTTP verify)</h2>
<p>From your workstation, poll every <strong>2 minutes</strong> until marketing home responds:</p>
<pre>curl.exe -sS -o NUL -w "ecomae %%{http_code}\n" --connect-timeout 10 https://www.ecomae.com/
curl.exe -sS -o NUL -w "cp %%{http_code}\n" --connect-timeout 10 https://www.ecomae.com/cp/
curl.exe -sS -o NUL -w "epartscart %%{http_code}\n" --connect-timeout 10 https://www.epartscart.com/
curl.exe -sS -o NUL -w "taxofinca %%{http_code}\n" --connect-timeout 10 https://www.taxofinca.com/</pre>
<p>Or open this page with server-side probes: <a href="?token=<?php echo rawurlencode($token); ?>&amp;probe=1">?token=…&amp;probe=1</a></p>

<?php if ($runProbe): ?>
<h3>Live probe results (from this VPS)</h3>
<table>
<tr><th>Site</th><th>HTTP</th><th>Time</th><th>Status</th></tr>
<?php foreach ($probeResults as $r):
	$rowClass = $r['ok'] ? 'probe-row-ok' : 'probe-row-bad';
	$tag = $r['ok'] ? 'tag-ok' : 'tag-bad';
?>
<tr class="<?php echo $rowClass; ?>">
<td><?php echo htmlspecialchars($r['label'], ENT_QUOTES, 'UTF-8'); ?><br><a href="<?php echo htmlspecialchars($r['url'], ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener"><?php echo htmlspecialchars($r['url'], ENT_QUOTES, 'UTF-8'); ?></a></td>
<td><code><?php echo (int) $r['code']; ?></code></td>
<td><?php echo (int) $r['ms']; ?> ms</td>
<td><span class="tag <?php echo $tag; ?>"><?php echo $r['ok'] ? 'OK' : 'FAIL'; ?></span> <?php echo htmlspecialchars($r['err'], ENT_QUOTES, 'UTF-8'); ?></td>
</tr>
<?php endforeach; ?>
</table>
<?php endif; ?>

<table style="margin-top:16px">
<tr><th>URL</th><th>Expect</th></tr>
<?php foreach ($sites as $s): ?>
<tr>
<td><a href="<?php echo htmlspecialchars($s['url'], ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener"><?php echo htmlspecialchars($s['url'], ENT_QUOTES, 'UTF-8'); ?></a></td>
<td><?php echo htmlspecialchars($s['expect'], ENT_QUOTES, 'UTF-8'); ?></td>
</tr>
<?php endforeach; ?>
</table>

<h3>If 502 / 503 after VPS is Running</h3>
<p>Restart stack on VPS (SSH as root):</p>
<pre>systemctl restart mariadb || systemctl restart mysql
systemctl restart php8.3-fpm
systemctl restart nginx
# CloudPanel: Sites → www.ecomae.com → Restart, or CLI via clpctl if installed</pre>
<p>Then re-run probes above. Tenant nginx aliases: <code>epc-tenants-connectivity-fix.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>&amp;apply=1&amp;clp_pass=…</code></p>
</div>

<div class="card">
<h2>Phase 3 — Data / services missing after OS upgrade</h2>
<ul>
<li><strong>On VPS:</strong> <code>/home/ecomae/backups/</code> (platform snapshots)</li>
<li><strong>Local workspace:</strong> <code>backups/epartscart-migration-20260527/</code></li>
<li><strong>Local production:</strong> <code>production-backups/modelc-20260527-013139/</code></li>
<li>Restore helper: <code>epc-restore-modelc.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?></code></li>
<li>Re-apply tenant vhosts + registry: <code>epc-tenants-connectivity-fix.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>&amp;apply=1&amp;clp_pass=…</code></li>
<li>Re-verify marketing home: <code>epc-ecomae-force-marketing-home.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?></code> (dry run) then <code>&amp;apply=1</code> if needed</li>
<li><strong>Firewall after recreate:</strong> hPanel → VPS → Firewall → allow inbound TCP <code>80</code> and <code>443</code>. Script: <code>epc-hostinger-firewall-open-web.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>&amp;apply=1</code></li>
<li>Full connectivity report: <code>epc-epartscart-connectivity-probe.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?></code></li>
</ul>
</div>

<div class="card">
<h2>Phase 4 — Deploy this checklist (when HTTPS works)</h2>
<p>From repo root <code>deploy-epartscart</code> (after <code>curl https://www.ecomae.com/</code> returns 200):</p>
<pre>python tools/push_one.py epc-vps-post-upgrade-checklist.php</pre>
<p>Then open: <a href="https://www.ecomae.com/epc-vps-post-upgrade-checklist.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>">https://www.ecomae.com/epc-vps-post-upgrade-checklist.php?token=…</a></p>
</div>

<div class="card">
<h2>What to watch in hPanel</h2>
<ul>
<li>VPS status: <strong>Recreating</strong> → <strong>Running</strong> (do not test tenants until Running)</li>
<li>After upgrade: confirm same public IP <code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code> (if IP changed, update GoDaddy A records for all tenants)</li>
<li>Firewall rules: HTTP/HTTPS inbound not dropped by recreate</li>
<li>Backup snapshots still listed under VPS / Backups</li>
</ul>
</div>

<p style="color:var(--muted);font-size:13px;margin-top:24px">Generated <?php echo gmdate('Y-m-d H:i:s'); ?> UTC · <a href="epc-epartscart-dns-migration-guide.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>">DNS guide</a></p>
</div>
</body>
</html>
