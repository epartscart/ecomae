/**
 * CP Settings — Epart / Cross / Laximo status panels (kept out of eval'd page PHP).
 */
(function () {
	'use strict';

	function timeText(ts) {
		if (!ts) {
			return 'never';
		}
		return new Date(ts * 1000).toLocaleString();
	}

	function fillUmapi(box) {
		fetch('/api/umapi_proxy.php?action=status', { credentials: 'same-origin', cache: 'no-store' })
			.then(function (response) { return response.json(); })
			.then(function (data) {
				var ok = data && data.connected;
				var counts = data && data.counts ? data.counts : {};
				var sections = data && data.sections ? data.sections : {};
				var usage = data && data.usage ? data.usage : {};
				var usageBar = '';
				if (usage && usage.daily_limit) {
					var pct = usage.pct_used || 0;
					var barColor = pct >= 100 ? '#ef4444' : (pct >= 80 ? '#f59e0b' : '#22c55e');
					usageBar =
						'<br><strong>Daily API usage:</strong> ' + (usage.today_live || 0) + ' / ' + usage.daily_limit +
						' live calls (' + pct + '%)' +
						(usage.remaining != null ? ', remaining ' + usage.remaining : '') +
						'<div style="margin-top:6px; height:8px; background:#e2e8f0; border-radius:4px; overflow:hidden;">' +
						'<div style="width:' + Math.min(100, pct) + '%; height:100%; background:' + barColor + ';"></div></div>';
					if (usage.by_source_today && usage.by_source_today.length) {
						usageBar += '<br><strong>Top sources today:</strong> ' + usage.by_source_today.slice(0, 5).map(function (x) {
							return x.source + ' (' + x.live + ' live)';
						}).join(', ');
					}
					if (usage.by_action_today && usage.by_action_today.length) {
						usageBar += '<br><strong>By action today:</strong> ' + usage.by_action_today.slice(0, 6).map(function (x) {
							return x.action + ' (' + x.live + ' live)';
						}).join(', ');
					}
					usageBar += '<br><strong>Full report:</strong> /epc-umapi-daily-report.php (tech key required)';
				}
				box.style.borderColor = ok ? '#bbf7d0' : '#fed7aa';
				box.style.background = ok ? '#f0fdf4' : '#fff7ed';
				box.innerHTML =
					'<strong>Epart catalog connection:</strong> ' + (ok ? '<span style="color:#15803d;">Connected</span>' : '<span style="color:#b45309;">Not connected / using saved data if available</span>') +
					'<br><strong>Status:</strong> ' + (data && data.message ? data.message : 'No status yet') +
					'<br><strong>Last check:</strong> ' + timeText(data && data.last_checked) +
					usageBar +
					'<br><strong>Saved catalog:</strong> Manufacturers ' + (counts.manufacturers || 0) + ', Brands ' + (counts.brands || 0) + ', Models ' + (counts.models || 0) + ', Modifications ' + (counts.modifications || 0) + ', VINs ' + (counts.vins || 0) +
					(data.cache_rows ? ('<br><strong>API cache rows:</strong> ' + data.cache_rows) : '') +
					'<br><strong>Manufacturer sections:</strong> Passenger ' + (sections.passenger || 0) + ', Commercial ' + (sections.commercial || 0) + ', Motorbike ' + (sections.motorbike || 0) +
					(data.offline_ready ? '<br><strong>Offline mode:</strong> <span style="color:#15803d;">Saved data available</span>' : '<br><strong>Offline mode:</strong> <span style="color:#b91c1c;">Not ready — run warm script</span>') +
					((data.action_required && data.action_required.length) ? ('<br><strong style="color:#b45309;">Action required:</strong><br>' + data.action_required.map(function (x) { return '• ' + x; }).join('<br>')) : '');
			})
			.catch(function () {
				box.innerHTML = '<strong>Epart catalog connection:</strong> status unavailable';
			});
	}

	function fillCross(box) {
		fetch('/api/crossbase_status.php?sample=C110J', { credentials: 'same-origin', cache: 'no-store' })
			.then(function (response) { return response.json(); })
			.then(function (data) {
				var ok = data && data.connected;
				box.style.borderColor = ok ? '#bbf7d0' : '#fed7aa';
				box.style.background = ok ? '#f0fdf4' : '#fff7ed';
				box.innerHTML =
					'<strong>Cross-reference API:</strong> ' + (ok ? '<span style="color:#15803d;">Connected</span>' : (data.used_stale_cache ? '<span style="color:#b45309;">Offline — using saved cache</span>' : '<span style="color:#b45309;">Not connected</span>')) +
					'<br><strong>Data source:</strong> Epart catalog cross-reference service' +
					'<br><strong>Status:</strong> ' + (data && data.message ? data.message : 'No status yet') +
					'<br><strong>HTTP code:</strong> ' + (data && data.status_code ? data.status_code : 0) +
					'<br><strong>Sample check:</strong> ' + (data && data.sample ? data.sample : 'C110J') + ' - ' + (data && data.references_total ? data.references_total : 0) + ' references, ' + (data && data.rows_parsed ? data.rows_parsed : 0) + ' rows parsed' +
					'<br><strong>Response time:</strong> ' + (data && data.response_ms ? data.response_ms : 0) + ' ms' +
					'<br><strong>Last check:</strong> ' + timeText(data && data.last_checked) +
					(data.cache ? ('<br><strong>HTML cache:</strong> ' + (data.cache.files_total || 0) + ' files (fresh ' + (data.cache.files_fresh || 0) + ', stale ' + (data.cache.files_stale || 0) + ')') : '') +
					(data.cp_cross_rows !== undefined ? ('<br><strong>CP crosses saved:</strong> ' + data.cp_cross_rows + (data.local_crosses_on ? ' (local crosses ON)' : ' (local crosses OFF)')) : '') +
					(data.offline_ready ? '<br><strong>Offline mode:</strong> <span style="color:#15803d;">Cache / local crosses available</span>' : '<br><strong>Offline mode:</strong> <span style="color:#b91c1c;">Not ready — warm cache + sync crosses</span>') +
					((data.action_required && data.action_required.length) ? ('<br><strong style="color:#b45309;">Action required:</strong><br>' + data.action_required.map(function (x) { return '• ' + x; }).join('<br>')) : '');
			})
			.catch(function () {
				box.innerHTML = '<strong>Cross-reference API:</strong> status unavailable';
			});
	}

	function fillLaximo(box) {
		fetch('/api/laximo_proxy.php?action=status', { credentials: 'same-origin', cache: 'no-store' })
			.then(function (response) { return response.json(); })
			.then(function (data) {
				var catOk = data && data.services && data.services.cat && data.services.cat.connected;
				var docOk = data && data.services && data.services.doc && data.services.doc.connected;
				box.style.borderColor = catOk ? '#bbf7d0' : '#fed7aa';
				box.style.background = catOk ? '#f0fdf4' : '#fff7ed';
				box.innerHTML =
					'<strong>Laximo OEM Catalog:</strong> ' +
					(catOk ? '<span style="color:#15803d;">CAT Connected</span>' : '<span style="color:#b45309;">CAT Not Connected</span>') +
					' | ' +
					(docOk ? '<span style="color:#15803d;">DOC Connected</span>' : '<span style="color:#b45309;">DOC Not Connected</span>') +
					'<br><strong>CAT Login:</strong> ' + (data.cat_login || 'not set') +
					' | <strong>DOC Login:</strong> ' + (data.doc_login || 'not set') +
					'<br><strong>Status:</strong> ' + (data.message || 'unknown') +
					'<br><strong>Saved catalogs:</strong> ' + (data.services && data.services.cat ? data.services.cat.catalogs_count : 0) + ' brands' +
					'<br><strong>API cache rows:</strong> ' + (data.cache_rows || 0) +
					'<br><strong>Last check:</strong> ' + timeText(data.last_checked) +
					(data.offline_ready ? '<br><strong>Offline mode:</strong> <span style="color:#15803d;">Saved data available</span>' : '<br><strong>Offline mode:</strong> <span style="color:#b91c1c;">Not ready — sync required</span>') +
					'<br><strong>Service docs:</strong> <a href="https://doc.laximo.ru/en/home" target="_blank" rel="noopener">doc.laximo.ru</a>' +
					'<br><strong>Demo:</strong> <a href="https://wsdemo.laximo.ru/index.php?task=catalogs" target="_blank" rel="noopener">CAT demo</a> | <a href="https://wsdemo.laximo.ru/index.php?task=aftermarket" target="_blank" rel="noopener">DOC demo</a>' +
					'<br><small style="color:#64748b;">Config: laximo_cat_login, laximo_cat_key, laximo_doc_login, laximo_doc_key in DP_Config</small>';
			})
			.catch(function () {
				box.innerHTML = '<strong>Laximo OEM Catalog:</strong> status unavailable (API proxy not accessible)';
			});
	}

	var umapi = document.getElementById('epc_umapi_cp_status');
	if (umapi) {
		fillUmapi(umapi);
	}
	var cross = document.getElementById('epc_crossbase_cp_status');
	if (cross) {
		fillCross(cross);
	}
	var laximo = document.getElementById('epc_laximo_cp_status');
	if (laximo) {
		fillLaximo(laximo);
	}
})();
