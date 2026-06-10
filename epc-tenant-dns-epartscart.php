<?php
/**
 * epartscart.com — tenant DNS on ECOM AE platform (Model C).
 * https://www.ecomae.com/epc-tenant-dns-epartscart.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$platformIp = epc_portal_platform_ip();
$hostname = 'www.epartscart.com';
$bare = 'epartscart.com';
$token = epc_deploy_token();
$platformHost = 'www.ecomae.com';

header('Content-Type: text/html; charset=utf-8');
?><!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>eParts Cart — tenant DNS (ECOM AE platform)</title>
<style>
:root { --bg:#0f172a; --card:#1e293b; --text:#e2e8f0; --muted:#94a3b8; --accent:#38bdf8; --ok:#4ade80; }
* { box-sizing:border-box; }
body { margin:0; font-family:system-ui,-apple-system,Segoe UI,sans-serif; background:var(--bg); color:var(--text); line-height:1.55; }
.wrap { max-width:880px; margin:0 auto; padding:32px 20px 64px; }
h1 { font-size:1.6rem; margin:0 0 8px; }
.lead { color:var(--muted); margin:0 0 24px; }
.card { background:var(--card); border:1px solid #334155; border-radius:12px; padding:20px 22px; margin:0 0 18px; }
h2 { font-size:1.05rem; margin:0 0 12px; color:var(--accent); }
table { width:100%; border-collapse:collapse; font-size:14px; }
th,td { text-align:left; padding:10px 12px; border-bottom:1px solid #334155; vertical-align:top; }
th { color:var(--muted); font-weight:600; width:30%; }
code { font-family:ui-monospace,Consolas,monospace; background:#0b1220; padding:2px 6px; border-radius:4px; }
ul { margin:8px 0 0; padding-left:20px; }
a { color:var(--accent); }
.tag { display:inline-block; padding:2px 8px; border-radius:999px; font-size:12px; background:#14532d; color:var(--ok); font-weight:600; }
</style>
</head>
<body>
<div class="wrap">
<h1>eParts Cart — platform tenant DNS</h1>
<p class="lead"><strong>epartscart.com is not separate hosting.</strong> It is a <strong>client tenant</strong> on the ECOM AE Super CP platform (Model C nginx alias). The storefront, CP, and ERP share the platform docroot with <code><?php echo htmlspecialchars($platformHost, ENT_QUOTES, 'UTF-8'); ?></code> and use the tenant commerce database.</p>

<div class="card">
<h2>Architecture</h2>
<table>
<tr><th>Platform</th><td><code>https://<?php echo htmlspecialchars($platformHost, ENT_QUOTES, 'UTF-8'); ?>/</code> — Super CP at <code>/cp/</code>, Tenant Hub at <code>/cp/shop/tenant_hub/</code></td></tr>
<tr><th>Tenant</th><td><code>https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/</code> — slug <code>epartscart</code>, status <span class="tag">live</span></td></tr>
<tr><th>Platform IP</th><td><code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code> (VPS — Hostinger is only where the platform server lives)</td></tr>
<tr><th>nginx</th><td><code>www.epartscart.com</code> + <code>epartscart.com</code> are <code>server_name</code> aliases on the <code>www.ecomae.com</code> vhost — no standalone CloudPanel site for epartscart</td></tr>
</table>
</div>

<div class="card">
<h2>GoDaddy DNS (exact)</h2>
<table>
<tr><th>Type</th><th>Name</th><th>Value</th><th>TTL</th></tr>
<tr><td><code>A</code></td><td><code>@</code></td><td><code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code></td><td><code>600</code></td></tr>
<tr><td><code>A</code></td><td><code>www</code></td><td><code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code></td><td><code>600</code></td></tr>
</table>
<p style="margin-top:12px;color:var(--muted)">DNS-only at GoDaddy (no Cloudflare orange-cloud). Remove old A/CNAME to previous servers or Cloudflare proxy IPs (104.x / 172.x).</p>
</div>

<div class="card">
<h2>URLs after DNS propagates</h2>
<ul>
<li>Storefront: <a href="https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/" target="_blank" rel="noopener">https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/</a></li>
<li>Tenant CP: <a href="https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/cp/" target="_blank" rel="noopener">https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/cp/</a></li>
<li>ERP: <a href="https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/erp" target="_blank" rel="noopener">https://<?php echo htmlspecialchars($hostname, ENT_QUOTES, 'UTF-8'); ?>/erp</a></li>
<li>Super CP (operators): <a href="https://<?php echo htmlspecialchars($platformHost, ENT_QUOTES, 'UTF-8'); ?>/cp/shop/tenant_hub/" target="_blank" rel="noopener">https://<?php echo htmlspecialchars($platformHost, ENT_QUOTES, 'UTF-8'); ?>/cp/shop/tenant_hub/</a></li>
</ul>
</div>

<div class="card">
<h2>Platform apply (server)</h2>
<ul>
<li>Cutover: <code>epc-epartscart-supercp-cutover.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>&amp;apply=1&amp;clp_pass=...</code></li>
<li>Firewall (all direct-DNS tenants): <code>epc-hostinger-firewall-open-web.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>&amp;apply=1</code></li>
<li>Probe: <code>epc-epartscart-connectivity-probe.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?></code></li>
</ul>
</div>

<p style="color:var(--muted);font-size:13px">Full migration checklist: <a href="epc-epartscart-dns-migration-guide.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>">epc-epartscart-dns-migration-guide.php</a></p>
</div>
</body>
</html>
