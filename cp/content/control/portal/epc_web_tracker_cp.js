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
			+ '&to=' + encodeURIComponent(toEl.value);
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
		$('wt_status').className = 'wt-status alert alert-success';
		$('wt_status').textContent = 'Updated · site ' + (j.site_key || '') + ' · ' + fmtTs(j.from) + ' → ' + fmtTs(j.to)
			+ (j.db ? (' · db ' + j.db) : '');

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
		}).join('') || '<tr><td colspan="3" class="wt-muted">No data yet — browse the storefront to generate traffic.</td></tr>';
		$('wt_daily').innerHTML = bars + table(['Date', 'Sessions', 'Pageviews'], dailyRows);

		var byTenant = d.by_tenant || [];
		var devices = d.devices || [];
		if (IS_SUPER && (j.site_key === '_all' || byTenant.length)) {
			$('wt_side_a').innerHTML = table(['Site', 'Host', 'Sessions', 'Views', 'Visitors'],
				byTenant.map(function (x) {
					return '<tr><td><span class="wt-link" data-site="' + esc(x.site_key) + '">' + esc(x.site_key) + '</span></td><td>' + esc(x.hostname) + '</td><td>' + esc(x.sessions) + '</td><td>' + esc(x.pageviews) + '</td><td>' + esc(x.visitors) + '</td></tr>';
				}).join('') || '<tr><td colspan="5" class="wt-muted">No tenant traffic yet.</td></tr>'
			);
			$('wt_side_b').innerHTML = table(['Device', 'Browser', 'OS', 'Sessions'],
				devices.map(function (x) {
					return '<tr><td>' + esc(x.device_type) + '</td><td>' + esc(x.browser) + '</td><td>' + esc(x.os) + '</td><td>' + esc(x.sessions) + '</td></tr>';
				}).join('') || '<tr><td colspan="4" class="wt-muted">—</td></tr>'
			);
		} else {
			$('wt_side_a').innerHTML = table(['Device', 'Browser', 'OS', 'Sessions'],
				devices.map(function (x) {
					return '<tr><td>' + esc(x.device_type) + '</td><td>' + esc(x.browser) + '</td><td>' + esc(x.os) + '</td><td>' + esc(x.sessions) + '</td></tr>';
				}).join('') || '<tr><td colspan="4" class="wt-muted">—</td></tr>'
			);
			$('wt_side_b').innerHTML = '<p class="wt-muted">Open a recent session below for the full click-by-click timeline, page experience (time on page, scroll), geography, and search events.</p>';
		}

		$('wt_pages').innerHTML = table(['Path', 'Views', 'Sessions', 'Avg time', 'Scroll %'],
			(d.top_pages || []).map(function (x) {
				return '<tr><td>' + esc(x.path) + '</td><td>' + esc(x.views) + '</td><td>' + esc(x.sessions) + '</td><td>' + esc(dur(x.avg_time_ms)) + '</td><td>' + esc(x.avg_scroll) + '</td></tr>';
			}).join('') || '<tr><td colspan="5" class="wt-muted">—</td></tr>'
		);

		$('wt_geo').innerHTML = table(['Country', 'City', 'Sessions'],
			(d.geo || []).map(function (x) {
				var c = (x.country_name || x.country_code || 'Unknown');
				if (x.country_code) c += ' (' + x.country_code + ')';
				return '<tr><td>' + esc(c) + '</td><td>' + esc(x.city || '—') + '</td><td>' + esc(x.sessions) + '</td></tr>';
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
				return '<tr><td>' + esc(x.path) + '</td><td>' + esc(el) + '</td><td>' + esc(tx) + '</td><td>' + esc(x.hits) + '</td></tr>';
			}).join('') || '<tr><td colspan="4" class="wt-muted">—</td></tr>'
		);

		$('wt_refs').innerHTML = table(['Referrer', 'UTM source', 'Medium', 'Campaign', 'Sessions'],
			(d.referrers || []).map(function (x) {
				return '<tr><td>' + esc(x.host) + '</td><td>' + esc(x.utm_source || '—') + '</td><td>' + esc(x.utm_medium || '—') + '</td><td>' + esc(x.utm_campaign || '—') + '</td><td>' + esc(x.sessions) + '</td></tr>';
			}).join('') || '<tr><td colspan="5" class="wt-muted">—</td></tr>'
		);

		$('wt_sessions').innerHTML = table(['When', 'Who', 'Geo', 'Device', 'Land → Exit', 'Pages', 'Clicks', 'Time', ''],
			(d.recent_sessions || []).map(function (x) {
				var who = x.is_registered == '1' || x.is_registered == 1
					? '<span class="wt-pill reg">User #' + esc(x.user_id) + '</span>'
					: '<span class="wt-pill guest">Guest</span>';
				if (IS_SUPER) who += ' <span class="wt-pill">' + esc(x.site_key) + '</span>';
				var geo = [x.city, x.country_code].filter(Boolean).join(', ') || '—';
				var path = esc(x.landing_path || '/') + ' → ' + esc(x.exit_path || '—');
				return '<tr><td>' + esc(fmtTs(x.last_seen_at)) + '</td><td>' + who + '</td><td>' + esc(geo) + '</td><td>' + esc((x.device_type || '') + ' / ' + (x.browser || '')) + '</td><td>' + path + '</td><td>' + esc(x.pageview_count) + '</td><td>' + esc(x.event_count) + '</td><td>' + esc(dur(x.duration_ms)) + '</td><td><a class="wt-link wt-open" data-id="' + esc(x.id) + '">Timeline</a></td></tr>';
			}).join('') || '<tr><td colspan="9" class="wt-muted">No sessions yet.</td></tr>'
		);

		Array.prototype.forEach.call(document.querySelectorAll('#wt_side_a [data-site]'), function (a) {
			a.addEventListener('click', function () {
				$('wt_site').value = a.getAttribute('data-site');
				load();
			});
		});
		Array.prototype.forEach.call(document.querySelectorAll('.wt-open'), function (a) {
			a.addEventListener('click', function (ev) {
				ev.preventDefault();
				openSession(a.getAttribute('data-id'));
			});
		});
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
				var html = '';
				html += '<p><strong>' + esc(s.site_key) + '</strong> · ' + esc(s.hostname) + ' · IP ' + esc(s.ip)
					+ ' · ' + (s.is_registered == 1 || s.is_registered == '1' ? 'Registered user #' + esc(s.user_id) : 'Guest')
					+ ' · ' + esc(s.city || '') + ' ' + esc(s.region || '') + ' ' + esc(s.country_name || s.country_code || '')
					+ ' · ' + esc(s.device_type) + ' / ' + esc(s.browser) + ' / ' + esc(s.os) + '</p>';
				html += '<p class="wt-muted">Landed ' + esc(fmtTs(s.first_seen_at)) + ' · Last ' + esc(fmtTs(s.last_seen_at))
					+ ' · Duration ' + esc(dur(s.duration_ms))
					+ ' · Referrer ' + esc(s.referrer_host || '(direct)')
					+ (s.utm_source ? ' · UTM ' + esc(s.utm_source) + '/' + esc(s.utm_medium) + '/' + esc(s.utm_campaign) : '')
					+ '</p>';
				html += '<h5>Page experience</h5><ul class="wt-timeline">';
				pvs.forEach(function (p) {
					html += '<li><strong>' + esc(fmtTs(p.ts)) + '</strong> ' + esc(p.path)
						+ (p.query ? '?' + esc(p.query) : '')
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
							+ ' “' + esc(e.element_text) + '” '
							+ (e.element_href ? '→ ' + esc(e.element_href) : '')
							+ ' <span class="wt-muted">@ ' + esc(e.x) + ',' + esc(e.y) + ' on ' + esc(e.path) + '</span>';
					} else {
						line += esc(e.path || '') + ' <span class="wt-muted">' + (e.meta_json ? esc(e.meta_json) : '') + '</span>';
					}
					html += '<li>' + line + '</li>';
				});
				if (!evs.length) html += '<li class="wt-muted">No click/search events.</li>';
				html += '</ul>';
				$('wt_session_body').innerHTML = html;
			});
	}

	function init() {
		var reload = $('wt_reload');
		if (!reload || !document.querySelector('.epc-wt')) return;
		reload.addEventListener('click', load);
		var site = $('wt_site');
		if (site && site.tagName === 'SELECT') {
			site.addEventListener('change', load);
		}
		load();
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}
})();
