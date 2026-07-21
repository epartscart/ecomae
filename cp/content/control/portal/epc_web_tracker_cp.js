/**
 * CP Website tracker dashboard — must load via epc_cp_page_assets (not inline in .row).
 */
(function () {
	'use strict';
	var CFG = window.EPC_WEB_TRACKER_CP || {};
	var AJAX = CFG.ajaxUrl || '/cp/content/control/portal/ajax_epc_web_tracker.php';
	var IS_SUPER = !!CFG.isSuper;

	function $(id) { return document.getElementById(id); }
	function esc(s) {
		return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
			return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
		});
	}
	function dur(ms) {
		ms = parseInt(ms, 10) || 0;
		if (ms < 1000) return ms + ' ms';
		var s = Math.round(ms / 1000);
		if (s < 60) return s + 's';
		var m = Math.floor(s / 60), r = s % 60;
		if (m < 60) return m + 'm ' + r + 's';
		return Math.floor(m / 60) + 'h ' + (m % 60) + 'm';
	}
	function fmtTs(ts) {
		ts = parseInt(ts, 10) || 0;
		if (!ts) return '—';
		return new Date(ts * 1000).toLocaleString();
	}
	function table(headers, rowsHtml) {
		return '<table class="wt-table"><thead><tr>' + headers.map(function (h) {
			return '<th>' + esc(h) + '</th>';
		}).join('') + '</tr></thead><tbody>' + rowsHtml + '</tbody></table>';
	}
	/** Build storefront URL for a tracked path (opens the same page visitors saw). */
	function pageHref(path, hostname) {
		var p = String(path == null || path === '' ? '/' : path);
		if (p.charAt(0) !== '/' && p.indexOf('http') !== 0) {
			p = '/' + p;
		}
		if (p.indexOf('http://') === 0 || p.indexOf('https://') === 0) {
			return p;
		}
		var host = String(hostname || '').replace(/^https?:\/\//i, '').replace(/\/+$/, '');
		if (host) {
			return 'https://' + host + p;
		}
		return p;
	}
	function pageLink(path, hostname, label) {
		var p = String(path == null || path === '' ? '/' : path);
		var text = label != null ? label : p;
		return '<a class="wt-link wt-page-link" href="' + esc(pageHref(p, hostname))
			+ '" target="_blank" rel="noopener" title="Open page">' + esc(text) + '</a>';
	}

	function filterValues() {
		return {
			device: ($('wt_device') && $('wt_device').value) || '',
			country: ($('wt_country') && $('wt_country').value) || '',
			ip: ($('wt_ip') && $('wt_ip').value.trim()) || '',
			user_id: ($('wt_user_id') && $('wt_user_id').value.trim()) || '',
			user_type: ($('wt_user_type') && $('wt_user_type').value) || '',
			browser: ($('wt_browser') && $('wt_browser').value) || '',
			path: ($('wt_path') && $('wt_path').value.trim()) || ''
		};
	}

	function filterQuery() {
		var f = filterValues();
		var q = '';
		Object.keys(f).forEach(function (k) {
			if (f[k]) q += '&' + encodeURIComponent(k) + '=' + encodeURIComponent(f[k]);
		});
		return q;
	}

	function activeFilterChips(f) {
		var chips = [];
		if (f.device) chips.push('device ' + f.device);
		if (f.country) chips.push('country ' + f.country);
		if (f.ip) chips.push('IP ' + f.ip);
		if (f.user_id) chips.push('user #' + f.user_id);
		if (f.user_type) chips.push(f.user_type);
		if (f.browser) chips.push('browser ' + f.browser);
		if (f.path) chips.push('path ' + f.path);
		if (!chips.length) return '';
		return chips.map(function (c) {
			return '<span class="wt-filter-chip">' + esc(c) + '</span>';
		}).join('');
	}

	function fillSelect(el, items, valueKey, labelFn, current) {
		if (!el) return;
		var keep = current != null ? String(current) : String(el.value || '');
		var html = '<option value="">' + esc(el.options[0] ? el.options[0].text : 'All') + '</option>';
		(items || []).forEach(function (it) {
			var v = String(it[valueKey] || '');
			if (!v) return;
			var lab = labelFn ? labelFn(it) : v;
			html += '<option value="' + esc(v) + '"' + (v === keep ? ' selected' : '') + '>' + esc(lab) + '</option>';
		});
		el.innerHTML = html;
		if (keep) el.value = keep;
	}

	function applyFacets(d) {
		var facets = d.facets || {};
		fillSelect($('wt_country'), facets.countries || [], 'country_code', function (it) {
			var name = it.country_name || it.country_code;
			return (it.country_code || '') + (name && name !== it.country_code ? ' — ' + name : '') + ' (' + (it.sessions || 0) + ')';
		}, filterValues().country);
		var devices = facets.devices || [];
		if (devices.length) {
			fillSelect($('wt_device'), devices, 'device_type', function (it) {
				return (it.device_type || '') + ' (' + (it.sessions || 0) + ')';
			}, filterValues().device);
			// Ensure standard options remain if missing from facets
			['desktop', 'mobile', 'tablet'].forEach(function (d) {
				var el = $('wt_device');
				if (!el) return;
				var found = false;
				Array.prototype.forEach.call(el.options, function (o) { if (o.value === d) found = true; });
				if (!found) {
					var opt = document.createElement('option');
					opt.value = d;
					opt.textContent = d.charAt(0).toUpperCase() + d.slice(1);
					el.appendChild(opt);
				}
			});
		}
		fillSelect($('wt_browser'), facets.browsers || [], 'browser', function (it) {
			return (it.browser || '') + ' (' + (it.sessions || 0) + ')';
		}, filterValues().browser);
	}

	function setFilterAndLoad(patch) {
		Object.keys(patch || {}).forEach(function (k) {
			var map = {
				device: 'wt_device',
				country: 'wt_country',
				ip: 'wt_ip',
				user_id: 'wt_user_id',
				user_type: 'wt_user_type',
				browser: 'wt_browser',
				path: 'wt_path'
			};
			var id = map[k];
			if (id && $(id)) $(id).value = patch[k] == null ? '' : String(patch[k]);
		});
		load();
	}

	function clearFilters() {
		['wt_device', 'wt_country', 'wt_ip', 'wt_user_id', 'wt_user_type', 'wt_browser', 'wt_path'].forEach(function (id) {
			if ($(id)) $(id).value = '';
		});
		load();
	}

	function bindFilterClicks(root) {
		if (!root) return;
		Array.prototype.forEach.call(root.querySelectorAll('[data-filter-key]'), function (el) {
			el.addEventListener('click', function (ev) {
				ev.preventDefault();
				ev.stopPropagation();
				var key = el.getAttribute('data-filter-key');
				var val = el.getAttribute('data-filter-val') || '';
				if (!key) return;
				var patch = {};
				patch[key] = val;
				setFilterAndLoad(patch);
			});
		});
	}

	function load() {
		var siteEl = $('wt_site');
		var fromEl = $('wt_from');
		var toEl = $('wt_to');
		var status = $('wt_status');
		if (!siteEl || !fromEl || !toEl || !status) return;
		status.className = 'wt-status alert alert-info';
		status.textContent = 'Loading traffic…';
		var url = AJAX + '?action=dashboard&site_key=' + encodeURIComponent(siteEl.value)
			+ '&from=' + encodeURIComponent(fromEl.value)
			+ '&to=' + encodeURIComponent(toEl.value)
			+ filterQuery();
		fetch(url, { credentials: 'same-origin' }).then(function (r) {
			return r.json();
		}).then(function (j) {
			if (!j || !j.ok) {
				status.className = 'wt-status alert alert-danger';
				status.textContent = (j && (j.message || j.error)) || 'Failed to load';
				return;
			}
			render(j);
		}).catch(function () {
			status.className = 'wt-status alert alert-danger';
			status.textContent = 'Network error loading tracker data';
		});
	}

	function render(j) {
		var d = j.data || {};
		var s = d.summary || {};
		var f = j.filters || filterValues();
		applyFacets(d);
		var chips = activeFilterChips(f);
		$('wt_status').className = 'wt-status alert alert-success';
		$('wt_status').innerHTML = 'Updated · site ' + esc(j.site_key || '') + ' · ' + esc(fmtTs(j.from)) + ' → ' + esc(fmtTs(j.to))
			+ (j.db ? (' · db ' + esc(j.db)) : '')
			+ (chips ? (' · filters ' + chips) : ' · no extra filters');

		$('wt_kpis').innerHTML = [
			['Sessions', s.sessions],
			['Visitors', s.visitors],
			['Pageviews', s.pageviews],
			['Clicks', s.clicks],
			['Searches', s.searches],
			['Guests', s.guest_sessions],
			['Registered', s.registered_sessions],
			['Avg time', dur(s.avg_duration_ms)],
			['Avg pages', s.avg_pages],
			['Bounce %', s.bounce_rate]
		].map(function (x) {
			return '<div class="wt-kpi"><b>' + esc(x[1]) + '</b><span>' + esc(x[0]) + '</span></div>';
		}).join('');

		var daily = d.daily || [];
		var max = 1;
		daily.forEach(function (x) { if ((+x.pageviews) > max) max = +x.pageviews; });
		var bars = '<div class="wt-bars" title="Pageviews by day">' + daily.map(function (x) {
			var h = Math.max(4, Math.round(((+x.pageviews) / max) * 80));
			return '<i style="height:' + h + 'px" title="' + esc(x.date) + ': ' + esc(x.pageviews) + ' views / ' + esc(x.sessions) + ' sessions"></i>';
		}).join('') + '</div>';
		var dailyRows = daily.map(function (x) {
			return '<tr><td>' + esc(x.date) + '</td><td>' + esc(x.sessions) + '</td><td>' + esc(x.pageviews) + '</td></tr>';
		}).join('') || '<tr><td colspan="3" class="wt-muted">No data for this range/filters.</td></tr>';
		$('wt_daily').innerHTML = bars + table(['Date', 'Sessions', 'Pageviews'], dailyRows);

		var byTenant = d.by_tenant || [];
		var devices = d.devices || [];
		function deviceRows(list) {
			return list.map(function (x) {
				return '<tr><td><span class="wt-device-click" data-filter-key="device" data-filter-val="' + esc(x.device_type) + '" title="Filter by device">' + esc(x.device_type) + '</span></td>'
					+ '<td><span class="wt-device-click" data-filter-key="browser" data-filter-val="' + esc(x.browser) + '" title="Filter by browser">' + esc(x.browser) + '</span></td>'
					+ '<td>' + esc(x.os) + '</td><td>' + esc(x.sessions) + '</td></tr>';
			}).join('') || '<tr><td colspan="4" class="wt-muted">—</td></tr>';
		}
		if (IS_SUPER && (j.site_key === '_all' || byTenant.length)) {
			$('wt_side_a').innerHTML = table(['Site', 'Host', 'Sessions', 'Views', 'Visitors'],
				byTenant.map(function (x) {
					return '<tr><td><span class="wt-link" data-site="' + esc(x.site_key) + '">' + esc(x.site_key) + '</span></td><td>' + esc(x.hostname) + '</td><td>' + esc(x.sessions) + '</td><td>' + esc(x.pageviews) + '</td><td>' + esc(x.visitors) + '</td></tr>';
				}).join('') || '<tr><td colspan="5" class="wt-muted">No tenant traffic yet.</td></tr>'
			);
			$('wt_side_b').innerHTML = table(['Device', 'Browser', 'OS', 'Sessions'], deviceRows(devices));
		} else {
			$('wt_side_a').innerHTML = table(['Device', 'Browser', 'OS', 'Sessions'], deviceRows(devices));
			$('wt_side_b').innerHTML = '<p class="wt-muted">Use filters above (device, country, IP, user, path). Click a country, IP, device, or user in the tables to filter quickly.</p>';
		}

		$('wt_pages').innerHTML = table(['Path', 'Views', 'Sessions', 'Avg time', 'Scroll %'],
			(d.top_pages || []).map(function (x) {
				return '<tr><td>' + pageLink(x.path || '/')
					+ ' <span class="wt-device-click" data-filter-key="path" data-filter-val="' + esc(x.path || '/') + '" title="Filter by this path">filter</span></td>'
					+ '<td>' + esc(x.views) + '</td><td>' + esc(x.sessions) + '</td><td>' + esc(dur(x.avg_time_ms)) + '</td><td>' + esc(x.avg_scroll) + '</td></tr>';
			}).join('') || '<tr><td colspan="5" class="wt-muted">—</td></tr>'
		);

		$('wt_geo').innerHTML = table(['Country', 'City', 'Sessions'],
			(d.geo || []).map(function (x) {
				var code = x.country_code || '';
				var c = (x.country_name || code || 'Unknown');
				if (code) c += ' (' + code + ')';
				return '<tr><td><span class="wt-geo-click" data-filter-key="country" data-filter-val="' + esc(code) + '" title="Filter by country">' + esc(c) + '</span></td>'
					+ '<td>' + esc(x.city || '—') + '</td><td>' + esc(x.sessions) + '</td></tr>';
			}).join('') || '<tr><td colspan="3" class="wt-muted">—</td></tr>'
		);

		$('wt_search').innerHTML = table(['Query', 'Context', 'Hits', 'Sessions'],
			(d.searches || []).map(function (x) {
				return '<tr><td><strong>' + esc(x.search_query) + '</strong></td><td>' + esc(x.search_context) + '</td><td>' + esc(x.hits) + '</td><td>' + esc(x.sessions) + '</td></tr>';
			}).join('') || '<tr><td colspan="4" class="wt-muted">No searches captured yet.</td></tr>'
		);

		$('wt_clicks').innerHTML = table(['Path', 'Element', 'Text / href', 'Hits'],
			(d.top_clicks || []).map(function (x) {
				var el = (x.element_tag || '') + (x.element_id ? '#' + x.element_id : '');
				var tx = (x.element_text || x.element_href || '—');
				var hrefCell = x.element_href
					? '<a class="wt-link wt-page-link" href="' + esc(x.element_href) + '" target="_blank" rel="noopener">' + esc(tx) + '</a>'
					: esc(tx);
				return '<tr><td>' + pageLink(x.path || '/') + '</td><td>' + esc(el) + '</td><td>' + hrefCell + '</td><td>' + esc(x.hits) + '</td></tr>';
			}).join('') || '<tr><td colspan="4" class="wt-muted">—</td></tr>'
		);

		$('wt_refs').innerHTML = table(['Referrer', 'UTM source', 'Medium', 'Campaign', 'Sessions'],
			(d.referrers || []).map(function (x) {
				return '<tr><td>' + esc(x.host) + '</td><td>' + esc(x.utm_source || '—') + '</td><td>' + esc(x.utm_medium || '—') + '</td><td>' + esc(x.utm_campaign || '—') + '</td><td>' + esc(x.sessions) + '</td></tr>';
			}).join('') || '<tr><td colspan="5" class="wt-muted">—</td></tr>'
		);

		$('wt_sessions').innerHTML = table(['When', 'Who', 'IP', 'Geo', 'Device', 'Land → Exit', 'Pages', 'Clicks', 'Time', ''],
			(d.recent_sessions || []).map(function (x) {
				var who = x.is_registered == '1' || x.is_registered == 1
					? '<span class="wt-pill reg wt-user-click" data-filter-key="user_id" data-filter-val="' + esc(x.user_id) + '" title="Filter by this user">User #' + esc(x.user_id) + '</span>'
					: '<span class="wt-pill guest wt-user-click" data-filter-key="user_type" data-filter-val="guest" title="Filter guests">Guest</span>';
				if (IS_SUPER) who += ' <span class="wt-pill">' + esc(x.site_key) + '</span>';
				var geo = [x.city, x.country_code].filter(Boolean).join(', ') || '—';
				var geoHtml = x.country_code
					? '<span class="wt-geo-click" data-filter-key="country" data-filter-val="' + esc(x.country_code) + '" title="Filter by country">' + esc(geo) + '</span>'
					: esc(geo);
				var host = x.hostname || '';
				var land = x.landing_path || '/';
				var exitP = x.exit_path || '';
				var pathHtml = pageLink(land, host);
				if (exitP && exitP !== land) {
					pathHtml += ' → ' + pageLink(exitP, host);
				} else if (exitP) {
					pathHtml += ' → <span class="wt-muted">same</span>';
				}
				var ipRaw = (x.ip && String(x.ip).trim()) ? String(x.ip).trim() : '';
				var ipHtml = ipRaw
					? '<span class="wt-ip wt-ip-click" data-filter-key="ip" data-filter-val="' + esc(ipRaw) + '" title="Filter by this IP">' + esc(ipRaw) + '</span>'
					: '—';
				return '<tr class="wt-row-click" data-id="' + esc(x.id) + '" title="Open session timeline">'
					+ '<td>' + esc(fmtTs(x.last_seen_at)) + '</td><td>' + who + '</td>'
					+ '<td>' + ipHtml + '</td><td>' + geoHtml + '</td>'
					+ '<td><span class="wt-device-click" data-filter-key="device" data-filter-val="' + esc(x.device_type || '') + '" title="Filter by device">'
					+ esc((x.device_type || '') + ' / ' + (x.browser || '')) + '</span></td><td class="wt-paths">' + pathHtml + '</td>'
					+ '<td>' + esc(x.pageview_count) + '</td><td>' + esc(x.event_count) + '</td><td>' + esc(dur(x.duration_ms)) + '</td>'
					+ '<td><a href="#" class="wt-link wt-open" data-id="' + esc(x.id) + '">Timeline</a></td></tr>';
			}).join('') || '<tr><td colspan="10" class="wt-muted">No sessions for these filters.</td></tr>'
		);

		Array.prototype.forEach.call(document.querySelectorAll('#wt_side_a [data-site]'), function (a) {
			a.addEventListener('click', function () {
				$('wt_site').value = a.getAttribute('data-site');
				load();
			});
		});
		Array.prototype.forEach.call(document.querySelectorAll('#wt_sessions tr.wt-row-click'), function (row) {
			row.addEventListener('click', function (ev) {
				if (ev.target && ev.target.closest && ev.target.closest('a, [data-filter-key]')) {
					return;
				}
				var id = row.getAttribute('data-id');
				if (id) openSession(id);
			});
		});
		Array.prototype.forEach.call(document.querySelectorAll('.wt-open'), function (a) {
			a.addEventListener('click', function (ev) {
				ev.preventDefault();
				ev.stopPropagation();
				openSession(a.getAttribute('data-id'));
			});
		});
		bindFilterClicks(document.querySelector('.epc-wt'));
	}

	function openSession(id) {
		$('wt_session_body').innerHTML = 'Loading…';
		if (window.jQuery) { jQuery('#wt_session_modal').modal('show'); }
		else { $('wt_session_modal').style.display = 'block'; }
		var site = $('wt_site').value;
		fetch(AJAX + '?action=session&id=' + encodeURIComponent(id) + '&site_key=' + encodeURIComponent(site), { credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (j) {
				if (!j || !j.ok || !j.detail || !j.detail.session) {
					$('wt_session_body').innerHTML = '<p class="text-danger">Session not found.</p>';
					return;
				}
				var s = j.detail.session;
				var pvs = j.detail.pageviews || [];
				var evs = j.detail.events || [];
				var host = s.hostname || '';
				var html = '';
				html += '<p><strong>' + esc(s.site_key) + '</strong> · ' + esc(s.hostname)
					+ ' · <span class="wt-ip">IP ' + esc(s.ip || '—') + '</span>'
					+ ' · ' + (s.is_registered == 1 || s.is_registered == '1' ? 'Registered user #' + esc(s.user_id) : 'Guest')
					+ ' · ' + esc(s.city || '') + ' ' + esc(s.region || '') + ' ' + esc(s.country_name || s.country_code || '')
					+ ' · ' + esc(s.device_type) + ' / ' + esc(s.browser) + ' / ' + esc(s.os) + '</p>';
				html += '<p class="wt-muted">Landed ' + esc(fmtTs(s.first_seen_at)) + ' · Last ' + esc(fmtTs(s.last_seen_at))
					+ ' · Duration ' + esc(dur(s.duration_ms))
					+ ' · Referrer ' + esc(s.referrer_host || '(direct)')
					+ (s.utm_source ? ' · UTM ' + esc(s.utm_source) + '/' + esc(s.utm_medium) + '/' + esc(s.utm_campaign) : '')
					+ '</p>';
				html += '<p>Pages: ' + pageLink(s.landing_path || '/', host, 'Land ' + (s.landing_path || '/'));
				if (s.exit_path) {
					html += ' · ' + pageLink(s.exit_path, host, 'Exit ' + s.exit_path);
				}
				html += '</p>';
				html += '<h5>Page experience</h5><ul class="wt-timeline">';
				pvs.forEach(function (p) {
					var pathOnly = p.path || '/';
					var href = pageHref(pathOnly + (p.query ? '?' + p.query : ''), host);
					html += '<li class="wt-info-click" title="Open page">'
						+ '<strong>' + esc(fmtTs(p.ts)) + '</strong> '
						+ '<a class="wt-link wt-page-link" href="' + esc(href) + '" target="_blank" rel="noopener">' + esc(pathOnly) + '</a>'
						+ (p.query ? '<span class="wt-muted">?' + esc(p.query) + '</span>' : '')
						+ ' <span class="wt-muted">· ' + esc(p.title) + ' · on-page ' + esc(dur(p.time_on_page_ms))
						+ ' · scroll ' + esc(p.scroll_max_pct) + '% · load ' + esc(p.load_time_ms) + 'ms</span></li>';
				});
				html += '</ul><h5>Clicks &amp; events</h5><ul class="wt-timeline">';
				evs.forEach(function (e) {
					var line = '<strong>' + esc(fmtTs(e.ts)) + '</strong> <span class="wt-pill">' + esc(e.event_type) + '</span> ';
					if (e.event_type === 'search') {
						line += 'search “' + esc(e.search_query) + '” <span class="wt-muted">(' + esc(e.search_context) + ')</span>';
					} else if (e.event_type === 'click' || e.event_type === 'outbound') {
						line += esc(e.element_tag) + (e.element_id ? '#' + esc(e.element_id) : '')
							+ ' “' + esc(e.element_text) + '” ';
						if (e.element_href) {
							line += '<a class="wt-link wt-page-link" href="' + esc(e.element_href) + '" target="_blank" rel="noopener">→ ' + esc(e.element_href) + '</a> ';
						}
						line += ' <span class="wt-muted">@ ' + esc(e.x) + ',' + esc(e.y) + ' on '
							+ pageLink(e.path || '/', host) + '</span>';
					} else {
						line += (e.path ? pageLink(e.path, host) : '') + ' <span class="wt-muted">' + (e.meta_json ? esc(e.meta_json) : '') + '</span>';
					}
					html += '<li class="wt-info-click">' + line + '</li>';
				});
				if (!evs.length) html += '<li class="wt-muted">No click/search events.</li>';
				html += '</ul>';
				$('wt_session_body').innerHTML = html;
			});
	}

	function downloadCsv() {
		var siteEl = $('wt_site');
		var fromEl = $('wt_from');
		var toEl = $('wt_to');
		if (!siteEl || !fromEl || !toEl) return;
		var url = AJAX + '?action=csv&site_key=' + encodeURIComponent(siteEl.value)
			+ '&from=' + encodeURIComponent(fromEl.value)
			+ '&to=' + encodeURIComponent(toEl.value)
			+ filterQuery();
		// Full page navigation keeps auth cookies and triggers file download.
		window.location.href = url;
	}

	function init() {
		var reload = $('wt_reload');
		if (!reload || !document.querySelector('.epc-wt')) return;
		reload.addEventListener('click', load);
		var clearBtn = $('wt_clear_filters');
		if (clearBtn) clearBtn.addEventListener('click', clearFilters);
		var csvBtn = $('wt_csv');
		if (csvBtn) {
			csvBtn.addEventListener('click', downloadCsv);
		}
		var site = $('wt_site');
		if (site && site.tagName === 'SELECT') {
			site.addEventListener('change', load);
		}
		['wt_device', 'wt_country', 'wt_user_type', 'wt_browser'].forEach(function (id) {
			var el = $(id);
			if (el) el.addEventListener('change', load);
		});
		['wt_ip', 'wt_user_id', 'wt_path', 'wt_from', 'wt_to'].forEach(function (id) {
			var el = $(id);
			if (!el) return;
			el.addEventListener('keydown', function (ev) {
				if (ev.key === 'Enter') {
					ev.preventDefault();
					load();
				}
			});
		});
		load();
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}
})();
