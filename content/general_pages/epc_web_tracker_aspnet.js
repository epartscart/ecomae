/**
 * ASP.NET CP Website tracker — PHP same-to-same render + improved charts.
 * Endpoints: /cp/web-tracker/dashboard|session|csv
 */
(function () {
	'use strict';
	var CFG = window.EPC_WEB_TRACKER_CP || {};
	var EP = CFG.endpoints || {};
	var DASH = EP.dashboard || '/cp/web-tracker/dashboard';
	var SESS = EP.session || '/cp/web-tracker/session';
	var CSV = EP.csv || '/cp/web-tracker/csv';
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
	function pageHref(path, hostname) {
		var p = String(path == null || path === '' ? '/' : path);
		if (p.charAt(0) !== '/' && p.indexOf('http') !== 0) p = '/' + p;
		if (p.indexOf('http://') === 0 || p.indexOf('https://') === 0) return p;
		var host = String(hostname || '').replace(/^https?:\/\//i, '').replace(/\/+$/, '');
		return host ? ('https://' + host + p) : p;
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
				device: 'wt_device', country: 'wt_country', ip: 'wt_ip', user_id: 'wt_user_id',
				user_type: 'wt_user_type', browser: 'wt_browser', path: 'wt_path'
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
	function svgLineChart(daily) {
		var w = 560, h = 140, pad = 18;
		if (!daily.length) {
			return '<p class="wt-muted">No data for this range/filters.</p>';
		}
		var max = 1;
		daily.forEach(function (x) { if ((+x.pageviews) > max) max = +x.pageviews; });
		var n = daily.length;
		var pts = daily.map(function (x, i) {
			var px = pad + (n === 1 ? (w - 2 * pad) / 2 : (i * (w - 2 * pad) / (n - 1)));
			var py = h - pad - ((+x.pageviews) / max) * (h - 2 * pad);
			return { x: px, y: py, d: x };
		});
		var poly = pts.map(function (p) { return p.x.toFixed(1) + ',' + p.y.toFixed(1); }).join(' ');
		var area = pad + ',' + (h - pad) + ' ' + poly + ' ' + (pts[pts.length - 1].x.toFixed(1)) + ',' + (h - pad);
		var dots = pts.map(function (p) {
			return '<circle cx="' + p.x.toFixed(1) + '" cy="' + p.y.toFixed(1) + '" r="3.2" fill="#0284c7">'
				+ '<title>' + esc(p.d.date) + ': ' + esc(p.d.pageviews) + ' views / ' + esc(p.d.sessions) + ' sessions</title></circle>';
		}).join('');
		return '<svg class="wt-chart-line" viewBox="0 0 ' + w + ' ' + h + '" role="img" aria-label="Pageviews by day">'
			+ '<polyline fill="rgba(14,165,233,.12)" stroke="none" points="' + area + '"></polyline>'
			+ '<polyline fill="none" stroke="#0284c7" stroke-width="2.5" points="' + poly + '"></polyline>'
			+ dots + '</svg>';
	}
	function mixDiagram(s) {
		var guests = +s.guest_sessions || 0;
		var regs = +s.registered_sessions || 0;
		var total = guests + regs;
		var guestPct = total > 0 ? Math.round(100 * guests / total) : 50;
		var funnelSessions = +s.sessions || 0;
		var funnelViews = +s.pageviews || 0;
		var funnelClicks = +s.clicks || 0;
		var funnelSearch = +s.searches || 0;
		return '<div class="wt-mix">'
			+ '<div class="wt-donut" style="--guest-pct:' + guestPct + '%" data-center="' + esc(total || 0) + '"></div>'
			+ '<div class="wt-mix-legend">'
			+ '<div><i style="background:#0ea5e9"></i>Guests <strong>' + esc(guests) + '</strong> (' + guestPct + '%)</div>'
			+ '<div><i style="background:#059669"></i>Registered <strong>' + esc(regs) + '</strong> (' + (100 - guestPct) + '%)</div>'
			+ '<div class="wt-muted">Bounce ' + esc(s.bounce_rate) + '% · avg pages ' + esc(s.avg_pages) + ' · avg time ' + esc(dur(s.avg_duration_ms)) + '</div>'
			+ '<div class="wt-funnel">'
			+ '<div class="wt-funnel__step" style="width:100%">Sessions <span>' + esc(funnelSessions) + '</span></div>'
			+ '<div class="wt-funnel__step" style="width:92%">Pageviews <span>' + esc(funnelViews) + '</span></div>'
			+ '<div class="wt-funnel__step" style="width:78%">Clicks <span>' + esc(funnelClicks) + '</span></div>'
			+ '<div class="wt-funnel__step" style="width:64%">Searches <span>' + esc(funnelSearch) + '</span></div>'
			+ '</div></div></div>';
	}
	function geoBars(geo) {
		var list = (geo || []).slice(0, 8);
		if (!list.length) return '';
		var max = 1;
		list.forEach(function (x) { if ((+x.sessions) > max) max = +x.sessions; });
		return '<div class="wt-hbar" aria-label="Top countries">' + list.map(function (x) {
			var code = x.country_code || '??';
			var pct = Math.max(4, Math.round(100 * (+x.sessions) / max));
			return '<div class="wt-hbar__row">'
				+ '<span class="wt-geo-click" data-filter-key="country" data-filter-val="' + esc(code) + '">' + esc(code) + '</span>'
				+ '<div class="wt-hbar__track"><div class="wt-hbar__fill" style="width:' + pct + '%"></div></div>'
				+ '<span>' + esc(x.sessions) + '</span></div>';
		}).join('') + '</div>';
	}

	function load() {
		var siteEl = $('wt_site');
		var fromEl = $('wt_from');
		var toEl = $('wt_to');
		var status = $('wt_status');
		if (!siteEl || !fromEl || !toEl || !status) return;
		status.className = 'wt-status alert alert-info';
		status.textContent = 'Loading traffic…';
		var url = DASH + '?site_key=' + encodeURIComponent(siteEl.value)
			+ '&from=' + encodeURIComponent(fromEl.value)
			+ '&to=' + encodeURIComponent(toEl.value)
			+ filterQuery();
		fetch(url, { credentials: 'same-origin' }).then(function (r) {
			if (r.status === 401 || r.status === 403) {
				throw new Error('auth');
			}
			return r.json();
		}).then(function (j) {
			if (!j || !j.ok) {
				status.className = 'wt-status alert alert-danger';
				status.textContent = (j && (j.message || j.error)) || 'Failed to load';
				return;
			}
			if (IS_SUPER && j.site_options && siteEl.tagName === 'SELECT') {
				var cur = siteEl.value;
				var opts = j.site_options;
				siteEl.innerHTML = opts.map(function (sk) {
					var lab = sk === '_all' ? 'All sites (Super)' : sk;
					return '<option value="' + esc(sk) + '"' + (sk === cur ? ' selected' : '') + '>' + esc(lab) + '</option>';
				}).join('');
				if (cur) siteEl.value = cur;
			}
			render(j);
		}).catch(function (err) {
			status.className = 'wt-status alert alert-danger';
			status.textContent = err && err.message === 'auth'
				? 'Sign in required to load tracker data.'
				: 'Network error loading tracker data';
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
		$('wt_daily').innerHTML = svgLineChart(daily) + bars + table(['Date', 'Sessions', 'Pageviews'], dailyRows);

		if ($('wt_mix')) {
			$('wt_mix').innerHTML = mixDiagram(s);
		}

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
			$('wt_side_b').innerHTML = '<p class="wt-muted">Use filters above (device, country, IP, user, path). Click a country, IP, device, or user in the tables to filter quickly. Charts update after Apply filters.</p>';
		}

		$('wt_pages').innerHTML = table(['Path', 'Views', 'Sessions', 'Avg time', 'Scroll %'],
			(d.top_pages || []).map(function (x) {
				return '<tr><td>' + pageLink(x.path || '/')
					+ ' <span class="wt-device-click" data-filter-key="path" data-filter-val="' + esc(x.path || '/') + '" title="Filter by this path">filter</span></td>'
					+ '<td>' + esc(x.views) + '</td><td>' + esc(x.sessions) + '</td><td>' + esc(dur(x.avg_time_ms)) + '</td><td>' + esc(x.avg_scroll) + '</td></tr>';
			}).join('') || '<tr><td colspan="5" class="wt-muted">—</td></tr>'
		);

		$('wt_geo').innerHTML = geoBars(d.geo) + table(['Country', 'City', 'Sessions'],
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
				if (exitP && exitP !== land) pathHtml += ' → ' + pageLink(exitP, host);
				else if (exitP) pathHtml += ' → <span class="wt-muted">same</span>';
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
				if (ev.target && ev.target.closest && ev.target.closest('a, [data-filter-key]')) return;
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
		var modal = $('wt_session_modal');
		var body = $('wt_session_body');
		if (!modal || !body) return;
		body.innerHTML = 'Loading…';
		modal.classList.add('is-open');
		modal.style.display = 'block';
		var site = $('wt_site').value;
		fetch(SESS + '?id=' + encodeURIComponent(id) + '&site_key=' + encodeURIComponent(site), { credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (j) {
				if (!j || !j.ok || !j.detail || !j.detail.session) {
					body.innerHTML = '<p class="text-danger">Session not found.</p>';
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
					+ (s.utm_source ? ' · UTM ' + esc(s.utm_source) : '')
					+ '</p>';
				html += '<p>Pages: ' + pageLink(s.landing_path || '/', host, 'Land ' + (s.landing_path || '/'));
				if (s.exit_path) html += ' · ' + pageLink(s.exit_path, host, 'Exit ' + s.exit_path);
				html += '</p><h5>Page experience</h5><ul class="wt-timeline">';
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
						line += ' <span class="wt-muted">@ ' + esc(e.x) + ',' + esc(e.y) + ' on ' + pageLink(e.path || '/', host) + '</span>';
					} else {
						line += (e.path ? pageLink(e.path, host) : '');
					}
					html += '<li class="wt-info-click">' + line + '</li>';
				});
				if (!evs.length) html += '<li class="wt-muted">No click/search events.</li>';
				html += '</ul>';
				body.innerHTML = html;
			});
	}

	function closeModal() {
		var modal = $('wt_session_modal');
		if (!modal) return;
		modal.classList.remove('is-open');
		modal.style.display = 'none';
	}

	function downloadCsv() {
		var siteEl = $('wt_site');
		var fromEl = $('wt_from');
		var toEl = $('wt_to');
		if (!siteEl || !fromEl || !toEl) return;
		window.location.href = CSV + '?site_key=' + encodeURIComponent(siteEl.value)
			+ '&from=' + encodeURIComponent(fromEl.value)
			+ '&to=' + encodeURIComponent(toEl.value)
			+ filterQuery();
	}

	function init() {
		var reload = $('wt_reload');
		if (!reload || !document.querySelector('.epc-wt')) return;
		reload.addEventListener('click', load);
		var clearBtn = $('wt_clear_filters');
		if (clearBtn) clearBtn.addEventListener('click', clearFilters);
		var csvBtn = $('wt_csv');
		if (csvBtn) csvBtn.addEventListener('click', downloadCsv);
		var closeBtn = $('wt_session_close');
		if (closeBtn) closeBtn.addEventListener('click', closeModal);
		var modal = $('wt_session_modal');
		if (modal) {
			modal.addEventListener('click', function (ev) {
				if (ev.target === modal) closeModal();
			});
		}
		var site = $('wt_site');
		if (site && site.tagName === 'SELECT') site.addEventListener('change', load);
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
