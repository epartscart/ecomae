<?php
/**
 * Hostinger hPanel vs CloudPanel — where to manage VPS vs websites.
 * Local / after deploy: epc-hostinger-hpanel-navigation.php?token=epartscart-deploy-2026
 * Live: https://www.ecomae.com/epc-hostinger-hpanel-navigation.php?token=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$token = epc_deploy_token();
$platformIp = epc_portal_platform_ip();
$vpsHost = 'srv1672837.hstgr.cloud';
$vpsLabel = 'srv1672837';
$clpPort = 8443;
$clpUrl = 'https://' . $platformIp . ':' . $clpPort;
$hpanelUrl = 'https://hpanel.hostinger.com';
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$siteUser = 'ecomae';

header('Content-Type: text/html; charset=utf-8');
?><!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Hostinger hPanel vs CloudPanel — ECOM AE platform</title>
<style>
:root { --bg:#0f172a; --card:#1e293b; --text:#e2e8f0; --muted:#94a3b8; --accent:#38bdf8; --ok:#4ade80; --warn:#fbbf24; --bad:#f87171; --hostinger:#673de6; --clp:#22c55e; }
* { box-sizing:border-box; }
body { margin:0; font-family:system-ui,-apple-system,Segoe UI,sans-serif; background:var(--bg); color:var(--text); line-height:1.55; }
.wrap { max-width:980px; margin:0 auto; padding:32px 20px 64px; }
h1 { font-size:1.65rem; margin:0 0 8px; }
.lead { color:var(--muted); margin:0 0 28px; }
.banner { background:#1e3a5f; border:1px solid #2563eb; border-radius:12px; padding:16px 20px; margin:0 0 22px; }
.banner-warn { background:#451a03; border-color:#92400e; }
.banner strong { color:var(--accent); }
.banner-warn strong { color:var(--warn); }
.card { background:var(--card); border:1px solid #334155; border-radius:12px; padding:20px 22px; margin:0 0 18px; }
h2 { font-size:1.1rem; margin:0 0 12px; color:var(--accent); }
h3 { font-size:0.95rem; margin:16px 0 8px; color:var(--muted); }
table { width:100%; border-collapse:collapse; font-size:14px; }
th,td { text-align:left; padding:10px 12px; border-bottom:1px solid #334155; vertical-align:top; }
th { color:var(--muted); font-weight:600; width:28%; }
code, pre { font-family:ui-monospace,Consolas,monospace; background:#0b1220; }
code { padding:2px 6px; border-radius:4px; }
pre { padding:12px 14px; border-radius:8px; overflow-x:auto; font-size:13px; margin:10px 0; }
ul,ol { margin:8px 0 0; padding-left:20px; }
li { margin:6px 0; }
.tag { display:inline-block; padding:2px 8px; border-radius:999px; font-size:12px; font-weight:600; }
.tag-h { background:#4c1d95; color:#c4b5fd; }
.tag-c { background:#14532d; color:var(--ok); }
.tag-warn { background:#713f12; color:var(--warn); }
a { color:var(--accent); }
.steps { counter-reset: step; list-style:none; padding-left:0; }
.steps > li { counter-increment: step; margin:14px 0; padding-left:36px; position:relative; }
.steps > li::before { content: counter(step); position:absolute; left:0; top:0; width:26px; height:26px; line-height:26px; text-align:center; background:#334155; border-radius:50%; font-size:13px; font-weight:700; }
.split { display:grid; grid-template-columns:1fr 1fr; gap:14px; }
@media (max-width:720px) { .split { grid-template-columns:1fr; } }
.panel-box { border-radius:10px; padding:14px 16px; border:1px solid #475569; }
.panel-h { border-color:#7c3aed; background:rgba(103,61,230,0.08); }
.panel-c { border-color:#16a34a; background:rgba(34,197,94,0.06); }
.muted { color:var(--muted); font-size:13px; }
.nav-path { font-size:13px; color:var(--muted); margin-top:4px; }
</style>
</head>
<body>
<div class="wrap">
<h1>Hostinger hPanel vs CloudPanel</h1>
<p class="lead">VPS <code><?php echo htmlspecialchars($vpsLabel, ENT_QUOTES, 'UTF-8'); ?></code> · <code><?php echo htmlspecialchars($vpsHost, ENT_QUOTES, 'UTF-8'); ?></code> · IP <code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code> · Ubuntu 26.04</p>

<div class="banner banner-warn">
<strong>You only see “Operating system” in hPanel?</strong> That section is for OS reinstall/upgrade only — not websites, nginx, MySQL, or SSL. Your hosting control panel for <code>www.ecomae.com</code>, <code>www.epartscart.com</code>, and <code>www.taxofinca.com</code> is <strong>CloudPanel</strong> on the VPS (separate login), plus optional SSH.
</div>

<div class="card">
<h2>Two different control panels</h2>
<div class="split">
<div class="panel-box panel-h">
<span class="tag tag-h">Hostinger hPanel</span>
<p><a href="<?php echo htmlspecialchars($hpanelUrl, ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener">hpanel.hostinger.com</a></p>
<p class="muted">Manages the <em>VPS machine</em>: power, OS image, snapshots, network firewall at the datacenter edge, browser SSH terminal.</p>
</div>
<div class="panel-box panel-c">
<span class="tag tag-c">CloudPanel</span>
<p><a href="<?php echo htmlspecialchars($clpUrl, ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener"><?php echo htmlspecialchars($clpUrl, ENT_QUOTES, 'UTF-8'); ?></a></p>
<p class="muted">Manages <em>websites on the VPS</em>: PHP sites, nginx vhosts, MySQL DBs, Let’s Encrypt SSL, file manager.</p>
</div>
</div>
<table style="margin-top:16px">
<tr><th>Task</th><th>Use</th></tr>
<tr><td>Open ports 80 / 443 for public HTTPS</td><td><span class="tag tag-h">hPanel</span> VPS → Security → Firewall</td></tr>
<tr><td>Reinstall or upgrade Ubuntu</td><td><span class="tag tag-h">hPanel</span> VPS → Operating system <span class="tag tag-warn">wipes CloudPanel unless you restore</span></td></tr>
<tr><td>Add site, SSL, database</td><td><span class="tag tag-c">CloudPanel</span> → Sites</td></tr>
<tr><td>epartscart / taxofinca nginx aliases</td><td><span class="tag tag-c">CloudPanel</span> site <code><?php echo htmlspecialchars($platformSite, ENT_QUOTES, 'UTF-8'); ?></code> → Vhost / domain aliases (Model C — same docroot)</td></tr>
<tr><td>Deploy PHP from repo</td><td><code>python tools/push_one.py …</code> (when origin responds) or SSH + files</td></tr>
</table>
</div>

<div class="card">
<h2>Step-by-step: open Hostinger firewall (from what you see now)</h2>
<ol class="steps">
<li>Go to <a href="<?php echo htmlspecialchars($hpanelUrl, ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener">hpanel.hostinger.com</a> and log in.</li>
<li>Left menu: <strong>VPS</strong> (or <strong>Servers</strong> → your VPS).</li>
<li>Select VPS <strong><?php echo htmlspecialchars($vpsLabel, ENT_QUOTES, 'UTF-8'); ?></strong> (hostname <code><?php echo htmlspecialchars($vpsHost, ENT_QUOTES, 'UTF-8'); ?></code>, IP <code><?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code>).</li>
<li>Open <strong>Security</strong> → <strong>Firewall</strong> (sometimes under Overview → Firewall — not under Operating system).</li>
<li>Add inbound rules: <strong>TCP 80</strong> and <strong>TCP 443</strong> from <strong>Anywhere</strong> (0.0.0.0/0).</li>
<li>Optional for CloudPanel UI from your PC: <strong>TCP <?php echo (int) $clpPort; ?></strong> from your IP only (less safe if open to the world).</li>
<li>Click <strong>Sync</strong> / apply firewall to this VM if the UI shows a sync button.</li>
<li>Confirm VPS status is <strong>Running</strong> (not Recreating).</li>
</ol>
<p class="muted">Automated (when site is up): <code>epc-hostinger-firewall-open-web.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>&amp;apply=1</code> — optional <code>hostinger_token=</code> from hPanel → Account → API.</p>
</div>

<div class="card">
<h2>Step-by-step: open CloudPanel (website control panel)</h2>
<ol class="steps">
<li>Ensure VPS is <strong>Running</strong> and firewall allows <strong>443</strong> (and <strong><?php echo (int) $clpPort; ?></strong> if you use the panel URL directly).</li>
<li>In browser open: <a href="<?php echo htmlspecialchars($clpUrl, ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener"><code><?php echo htmlspecialchars($clpUrl, ENT_QUOTES, 'UTF-8'); ?></code></a> (accept self-signed / Let’s Encrypt warning if shown).</li>
<li>Log in as CloudPanel user <code>admin</code> (password: your saved CloudPanel admin password, or <code>CLP_PASS</code> env used by deploy scripts — not the same as hPanel login).</li>
<li>Dashboard → <strong>Sites</strong>. Primary platform site: <code><?php echo htmlspecialchars($platformSite, ENT_QUOTES, 'UTF-8'); ?></code>, docroot <code><?php echo htmlspecialchars($platformDocroot, ENT_QUOTES, 'UTF-8'); ?></code>, Linux user <code><?php echo htmlspecialchars($siteUser, ENT_QUOTES, 'UTF-8'); ?></code>.</li>
<li><strong>epartscart</strong> and <strong>taxofinca</strong> are tenant domains on the same docroot (nginx aliases on <code><?php echo htmlspecialchars($platformSite, ENT_QUOTES, 'UTF-8'); ?></code>) — do not create a separate CloudPanel site per tenant unless you intentionally want a second disk footprint.</li>
</ol>
<p class="muted">Deploy scripts talk to CloudPanel on the server at <code>https://127.0.0.1:8443</code> via <code>epc_clp_*</code> helpers in <code>content/general_pages/epc_cloudpanel_helpers.php</code> (login user <code>admin</code>, CLI <code>clpctl</code>).</p>
</div>

<div class="card">
<h2>Where things are in hPanel (VPS <?php echo htmlspecialchars($vpsLabel, ENT_QUOTES, 'UTF-8'); ?>)</h2>
<table>
<tr><th>Section</th><th>What it does</th></tr>
<tr><td><strong>Overview</strong></td><td>Status (Running / Recreating), public IP, plan, link to <strong>Browser terminal</strong> (web SSH as root)</td></tr>
<tr><td><strong>Operating system</strong></td><td>Change OS version or reinstall only — <em>not</em> CloudPanel, not site files. Ubuntu 24→26 upgrade may remove CloudPanel until reinstalled.</td></tr>
<tr><td><strong>Security → Firewall</strong></td><td>Hostinger edge firewall — must allow 80/443 for <code>www.ecomae.com</code> and tenants</td></tr>
<tr><td><strong>Backups / Snapshots</strong></td><td>Hostinger-level VM snapshots (separate from <code>/home/ecomae/backups/</code> on disk)</td></tr>
<tr><td><strong>Settings</strong></td><td>Hostname, SSH keys, reverse DNS</td></tr>
<tr><td><strong>Not in hPanel</strong></td><td>nginx sites, PHP, MySQL databases, Let’s Encrypt for domains → <strong>CloudPanel</strong> or SSH</td></tr>
</table>
</div>

<div class="card">
<h2>If CloudPanel is missing after Ubuntu 26 upgrade</h2>
<p>OS reinstall/upgrade via hPanel often wipes <code>/usr/bin/clpctl</code> and the panel service. Ubuntu <strong>26.04</strong> may not be on CloudPanel’s official support matrix (22.04 / 24.04 are documented). Check <a href="https://www.cloudpanel.io/docs/v2/getting-started/other/cloudpanel-cli/" target="_blank" rel="noopener">CloudPanel docs</a> before installing on 26.</p>
<h3>Check from SSH (hPanel → Overview → Browser terminal, or <code>ssh root@<?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?></code>)</h3>
<pre>systemctl is-active clp-agent 2>/dev/null || echo "clp-agent not running"
command -v clpctl &amp;&amp; clpctl --version
ss -tlnp | grep <?php echo (int) $clpPort; ?> || echo "nothing listening on <?php echo (int) $clpPort; ?>"</pre>
<h3>Reinstall CloudPanel (official CE installer — verify OS support first)</h3>
<pre># Only on a fresh or intentionally wiped VPS — backs up /home/ecomae first!
# Docs: https://www.cloudpanel.io/docs/v2/getting-started/
curl -sS https://installer.cloudpanel.io/ce/v2/install.sh -o /tmp/clp-install.sh
bash /tmp/clp-install.sh</pre>
<p>After install: recreate site <code><?php echo htmlspecialchars($platformSite, ENT_QUOTES, 'UTF-8'); ?></code>, restore docroot and DB from <code>/home/ecomae/backups/</code> or local <code>production-backups/modelc-*</code>, then run:</p>
<ul>
<li><code>epc-restore-modelc.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?></code></li>
<li><code>epc-tenants-connectivity-fix.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>&amp;apply=1&amp;clp_pass=…</code></li>
<li><code>epc-vps-post-upgrade-checklist.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?></code></li>
</ul>
<p>If CloudPanel cannot run on Ubuntu 26, consider hPanel → Operating system → reinstall <strong>Ubuntu 24.04 LTS</strong>, then install CloudPanel, then restore backups.</p>
</div>

<div class="card">
<h2>Alternative without CloudPanel (SSH only)</h2>
<pre>ssh root@<?php echo htmlspecialchars($platformIp, ENT_QUOTES, 'UTF-8'); ?>

systemctl status nginx
systemctl status mariadb || systemctl status mysql
systemctl status php8.3-fpm 2>/dev/null || systemctl status php*-fpm

ls -la <?php echo htmlspecialchars($platformDocroot, ENT_QUOTES, 'UTF-8'); ?>
nginx -t &amp;&amp; systemctl reload nginx</pre>
<p>MySQL databases used by the stack: <code>ecomae</code> (Super CP registry), shared commerce DB (tenant storefront data). Config: <code>config.php</code> / <code>config.local.php</code> under docroot.</p>
</div>

<div class="card">
<h2>Restore ecomae / epartscart after VPS is healthy</h2>
<ol>
<li>hPanel: VPS <strong>Running</strong>, firewall <strong>80+443</strong> open.</li>
<li>CloudPanel or SSH: nginx + PHP-FPM + MariaDB active; site <code><?php echo htmlspecialchars($platformSite, ENT_QUOTES, 'UTF-8'); ?></code> docroot populated.</li>
<li>From repo <code>deploy-epartscart</code> when HTTPS works:
<pre>curl.exe -sS -o NUL -w "%%{http_code}\n" --connect-timeout 10 https://www.ecomae.com/
python tools/push_one.py epc-hostinger-hpanel-navigation.php
python tools/push_one.py epc-vps-post-upgrade-checklist.php</pre>
</li>
<li>Re-apply tenant nginx + SSL: <code>epc-tenants-connectivity-fix.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>&amp;apply=1&amp;clp_pass=…</code></li>
<li>Verify: <a href="https://www.ecomae.com/" target="_blank" rel="noopener">www.ecomae.com</a>, <a href="https://www.epartscart.com/" target="_blank" rel="noopener">www.epartscart.com</a>, <a href="https://www.taxofinca.com/" target="_blank" rel="noopener">www.taxofinca.com</a></li>
</ol>
<p><code>push_one.py</code> uploads via <code>epc-ecomae-platform-fix.php</code> using env <code>CLP_PASS</code> and token <code><?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?></code>. If origin times out (522), fix firewall first, then retry.</p>
</div>

<div class="card">
<h2>Project scripts using CloudPanel (<code>epc_clp_*</code>)</h2>
<table>
<tr><th>Helper / script</th><th>Purpose</th></tr>
<tr><td><code>epc_cloudpanel_helpers.php</code></td><td><code>epc_clp_run</code>, <code>epc_clp_web_login</code>, vhost/SSL automation</td></tr>
<tr><td><code>epc-tenants-connectivity-fix.php</code></td><td>Model C aliases, SSL, permissions</td></tr>
<tr><td><code>epc-epartscart-supercp-cutover.php</code></td><td>epartscart on shared docroot</td></tr>
<tr><td><code>epc-hostinger-firewall-open-web.php</code></td><td>Open 80/443 via API or prints hPanel steps</td></tr>
<tr><td><code>epc-platform-full-backup.php</code></td><td>Full backup including CloudPanel inventory</td></tr>
</table>
</div>

<p class="muted" style="margin-top:24px">Generated <?php echo gmdate('Y-m-d H:i:s'); ?> UTC · Related: <a href="epc-vps-post-upgrade-checklist.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>">post-upgrade checklist</a> · <a href="epc-epartscart-dns-migration-guide.php?token=<?php echo htmlspecialchars($token, ENT_QUOTES, 'UTF-8'); ?>">DNS guide</a></p>
</div>
</body>
</html>
