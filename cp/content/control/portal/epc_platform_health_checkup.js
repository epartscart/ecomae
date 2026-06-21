(function () {
	'use strict';
	var cfg = window.EPC_PHC || {};
	var apiUrl = cfg.apiUrl;
	var storageKey = cfg.storageKey || 'epc_platform_health_checkup_last_run';
	var root = document.getElementById('epc_phc_root');
	var results = document.getElementById('epc_phc_results');
	var lastRunEl = document.getElementById('epc_phc_last_run');
	if (!root || !results || !apiUrl) return;

	var lastPayload = null;

	function badge(ok, warn) {
		if (ok) return '<span class="badge-ok">OK</span>';
		if (warn) return '<span class="badge-warn">WARN</span>';
		return '<span class="badge-fail">FAIL</span>';
	}

	function render(data) {
		lastPayload = data;
		var fails = data.public_failures || 0;
		var nginxOk = data.nginx && data.nginx.nginx_ok;
		var backupOk = data.backup && data.backup.ok;
		var overall = data.overall_ok;
		var html = '';
		html += '<div class="epc-phc__summary">';
		html += '<div class="epc-phc__stat' + (overall ? ' epc-phc__stat--ok' : ' epc-phc__stat--bad') + '"><strong>' + (overall ? 'PASS' : 'REVIEW') + '</strong><span>Overall</span></div>';
		html += '<div class="epc-phc__stat' + (fails === 0 ? ' epc-phc__stat--ok' : ' epc-phc__stat--bad') + '"><strong>' + fails + '</strong><span>URL failures</span></div>';
		html += '<div class="epc-phc__stat' + (nginxOk ? ' epc-phc__stat--ok' : ' epc-phc__stat--bad') + '"><strong>' + (nginxOk ? 'OK' : '!') + '</strong><span>Nginx</span></div>';
		html += '<div class="epc-phc__stat' + (backupOk ? ' epc-phc__stat--ok' : '') + '"><strong>' + (data.backup && data.backup.age_hours != null ? data.backup.age_hours + 'h' : '—') + '</strong><span>Backup age</span></div>';
		if (data.opcache && data.opcache.status) {
			html += '<div class="epc-phc__stat epc-phc__stat--ok"><strong>' + (data.opcache.status.hit_rate || '—') + '%</strong><span>OPcache hit</span></div>';
		}
		html += '</div>';

		html += '<section><h3>Tenant URLs — storefront &amp; CP</h3><table><thead><tr><th>Tenant</th><th>URL</th><th>HTTP</th><th>ms</th><th>Origin</th><th>Status</th></tr></thead><tbody>';
		(data.url_checks || []).forEach(function (r) {
			html += '<tr><td>' + r.label + '</td><td><a href="' + r.url + '" target="_blank" rel="noopener">' + r.url + '</a></td>';
			html += '<td>' + (r.public_http || '—') + '</td><td>' + r.public_ms + '</td><td>' + (r.origin_http || '—') + ' / ' + r.origin_ms + 'ms</td>';
			html += '<td>' + badge(r.public_ok) + (r.ssl_note ? '<br><small>' + r.ssl_note + '</small>' : '') + '</td></tr>';
		});
		html += '</tbody></table></section>';

		html += '<section><h3>SSL per hostname</h3><table><thead><tr><th>Host</th><th>Expires</th><th>Days left</th><th>Status</th></tr></thead><tbody>';
		(data.ssl_checks || []).forEach(function (r) {
			html += '<tr><td>' + r.host + '</td><td>' + (r.expires || r.error || '—') + '</td><td>' + (r.days_left != null ? r.days_left : '—') + '</td><td>' + badge(r.ok, !r.ok && r.days_left > 0) + '</td></tr>';
		});
		html += '</tbody></table><p class="epc-phc-note">' + (data.cloudflare_ssl_note || '') + '</p></section>';

		html += '<section><h3>ERP / tenant DB isolation</h3><table><thead><tr><th>Site</th><th>Hostname</th><th>Registry DB</th><th>Runtime override</th><th>Status</th></tr></thead><tbody>';
		(data.erp_isolation || []).forEach(function (r) {
			html += '<tr><td>' + r.site_key + '</td><td>' + r.hostname + '</td><td>' + r.registry_db + '</td><td>' + r.runtime_db + '</td><td>' + badge(r.ok) + '</td></tr>';
		});
		html += '</tbody></table></section>';

		html += '<section><h3>Nginx &amp; CloudPanel</h3><table><tbody>';
		var ng = data.nginx || {};
		html += '<tr><td>listen 8080 blocks</td><td>' + (ng.listen_8080_blocks || 0) + '</td><td>' + badge((ng.listen_8080_blocks || 0) >= 1) + '</td></tr>';
		html += '<tr><td>server_name www.ecomae.com lines</td><td>' + (ng.server_name_ecomae_lines || 0) + '</td><td>' + badge((ng.server_name_ecomae_lines || 0) >= 2) + '</td></tr>';
		html += '<tr><td>Orphan tenant vhosts</td><td>' + ((ng.orphan_configs || []).join(', ') || 'none') + '</td><td>' + badge((ng.orphan_configs || []).length === 0) + '</td></tr>';
		html += '<tr><td>Backend :8080</td><td>HTTP ' + (ng.backend_8080_http || '—') + '</td><td>' + badge((ng.backend_8080_http || 0) !== 404) + '</td></tr>';
		html += '</tbody></table><p class="epc-phc-note"><strong>Operator:</strong> ' + (ng.operator_reminder || '') + '</p></section>';

		html += '<section><h3>Backup freshness</h3>';
		if (data.backup && data.backup.latest) {
			html += '<p>Latest: <code>' + data.backup.latest + '</code> · ' + data.backup.latest_at + ' · age ' + data.backup.age_hours + 'h ' + badge(data.backup.ok) + '</p>';
		} else {
			html += '<p class="text-warning">No backup archives found under /home/ecomae/backups — run epc-platform-full-backup.php</p>';
		}
		html += '</section>';

		html += '<section><h3>Google indexing (robots &amp; sitemap)</h3><table><thead><tr><th>Host</th><th>robots.txt</th><th>Sitemap</th><th>Status</th></tr></thead><tbody>';
		(data.indexing || []).forEach(function (r) {
			html += '<tr><td>' + r.host + '</td><td>' + r.robots_http + '</td><td>' + r.sitemap_http + '</td><td>' + badge(r.sitemap_ok) + '</td></tr>';
		});
		html += '</tbody></table><p class="epc-phc-note">Submit each sitemap in Search Console. Marketing: /epc-ecomae-sitemap.xml</p></section>';

		html += '<section><h3>VPS resources</h3><table><tbody>';
		if (data.load) {
			html += '<tr><td>Load avg</td><td>' + data.load['1m'] + ' / ' + data.load['5m'] + ' / ' + data.load['15m'] + '</td></tr>';
		}
		if (data.disk && data.disk.total_gb) {
			html += '<tr><td>Disk</td><td>' + data.disk.used_pct + '% used · ' + data.disk.free_gb + ' GB free / ' + data.disk.total_gb + ' GB</td></tr>';
		}
		if (data.opcache && data.opcache.status) {
			html += '<tr><td>OPcache</td><td>' + data.opcache.status.cached_scripts + ' scripts · hit ' + data.opcache.status.hit_rate + '%</td></tr>';
		}
		html += '</tbody></table></section>';

		html += '<p class="epc-phc-note text-muted">Report generated: ' + (data.time || '') + '</p>';
		results.innerHTML = html;
	}

	function run() {
		root.classList.add('is-loading');
		fetch(apiUrl, { credentials: 'omit' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				render(data);
				var stamp = new Date().toISOString();
				try { localStorage.setItem(storageKey, stamp); } catch (e) {}
				if (lastRunEl) lastRunEl.textContent = 'Last run: ' + stamp;
			})
			.catch(function (err) {
				results.innerHTML = '<div class="alert alert-danger">Checkup failed: ' + err + '</div>';
			})
			.finally(function () { root.classList.remove('is-loading'); });
	}

	var runBtn = document.getElementById('epc_phc_run');
	if (runBtn) runBtn.addEventListener('click', run);

	var exportBtn = document.getElementById('epc_phc_export_csv');
	if (exportBtn) {
		exportBtn.addEventListener('click', function () {
			if (!lastPayload || !lastPayload.url_checks) {
				alert('Run checkup first.');
				return;
			}
			var lines = ['tenant,url,http,ms,ok'];
			lastPayload.url_checks.forEach(function (r) {
				lines.push([r.label, r.url, r.public_http, r.public_ms, r.public_ok ? 'ok' : 'fail'].join(','));
			});
			var blob = new Blob([lines.join('\n')], { type: 'text/csv' });
			var a = document.createElement('a');
			a.href = URL.createObjectURL(blob);
			a.download = 'epc-platform-health-' + (new Date().toISOString().slice(0, 10)) + '.csv';
			a.click();
		});
	}

	try {
		var prev = localStorage.getItem(storageKey);
		if (prev && lastRunEl) lastRunEl.textContent = 'Last run: ' + prev;
	} catch (e) {}
})();
