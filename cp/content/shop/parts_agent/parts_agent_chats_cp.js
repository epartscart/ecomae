(function () {
	var root = document.getElementById('epc-agent-cp-root');
	var cfg = window.EPC_AGENT_CP || {};
	var ajaxUrl = cfg.ajaxUrl || (root ? (root.getAttribute('data-ajax-url') || '') : '') || '/cp/content/shop/parts_agent/ajax_epc_parts_agent_cp.php';
	var csrfKey = cfg.csrfKey || (root ? (root.getAttribute('data-csrf') || '') : '') || '';
	var offset = 0;
	var limit = 50;
	var total = 0;

	var qInput = document.getElementById('epc-agent-q');
	var fromInput = document.getElementById('epc-agent-from');
	var toInput = document.getElementById('epc-agent-to');
	var tbody = document.getElementById('epc-agent-tbody');
	var msgBox = document.getElementById('epc-agent-msg');
	var statsBox = document.getElementById('epc-agent-stats');
	var pageInfo = document.getElementById('epc-agent-page-info');
	var prevBtn = document.getElementById('epc-agent-prev');
	var nextBtn = document.getElementById('epc-agent-next');
	var detailWrap = document.getElementById('epc-agent-detail');
	var detailTitle = document.getElementById('epc-agent-detail-title');
	var detailSub = document.getElementById('epc-agent-detail-sub');
	var detailBody = document.getElementById('epc-agent-detail-body');
	var cfgEnabled = document.getElementById('epc-agent-cfg-enabled');
	var cfgDomain = document.getElementById('epc-agent-cfg-domain');
	var cfgName = document.getElementById('epc-agent-cfg-name');
	var cfgLogo = document.getElementById('epc-agent-cfg-logo');
	var cfgSubtitle = document.getElementById('epc-agent-cfg-subtitle');
	var cfgGreeting = document.getElementById('epc-agent-cfg-greeting');
	var cfgPrompt = document.getElementById('epc-agent-cfg-prompt');
	var cfgTeaser = document.getElementById('epc-agent-cfg-teaser');
	var cfgPlaceholder = document.getElementById('epc-agent-cfg-placeholder');
	var cfgStatus = document.getElementById('epc-agent-cfg-status');

	if (detailWrap) {
		detailWrap.style.display = 'block';
		detailWrap.classList.add('is-empty');
	}

	function showMsg(text, ok) {
		if (!msgBox) { return; }
		msgBox.style.display = 'block';
		msgBox.className = 'alert alert-' + (ok ? 'success' : 'danger');
		msgBox.textContent = text;
	}

	function fmtTs(ts) {
		if (!ts) return '—';
		var d = new Date(ts * 1000);
		return d.toLocaleString();
	}

	function escHtml(s) {
		return String(s || '')
			.replace(/&/g, '&amp;')
			.replace(/</g, '&lt;')
			.replace(/>/g, '&gt;')
			.replace(/"/g, '&quot;');
	}

	function renderMarkdownLite(text) {
		var safe = escHtml(text);
		safe = safe.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
		safe = safe.replace(/`([^`]+)`/g, '<code>$1</code>');
		return safe;
	}

	function getCsrfKey() {
		if (csrfKey) { return csrfKey; }
		var local = document.getElementById('epc-agent-cp-csrf');
		if (local && local.value) { return local.value; }
		var global = document.querySelector('input[name="csrf_guard_key"]');
		if (global && global.value) { return global.value; }
		return '';
	}

	function xhrGet(params, cb) {
		var csrf = getCsrfKey();
		if (!csrf) {
			cb({ status: false, message: 'CSRF token missing. Please reload this CP page.' });
			return;
		}
		if (!ajaxUrl) {
			cb({ status: false, message: 'AJAX URL missing. Reload the page or check parts_agent_chats_config.php.' });
			return;
		}
		params.csrf_guard_key = csrf;
		var body = [];
		Object.keys(params).forEach(function (k) {
			if (params[k] !== '' && params[k] != null) {
				body.push(encodeURIComponent(k) + '=' + encodeURIComponent(params[k]));
			}
		});
		var xhr = new XMLHttpRequest();
		xhr.open('POST', ajaxUrl, true);
		xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded; charset=UTF-8');
		xhr.onload = function () {
			var data;
			try { data = JSON.parse(xhr.responseText); } catch (e) {
				var snippet = String(xhr.responseText || '').replace(/\s+/g, ' ').slice(0, 160);
				cb({ status: false, message: 'Bad response (' + xhr.status + ')' + (snippet ? ': ' + snippet : '') });
				return;
			}
			cb(data);
		};
		xhr.onerror = function () { cb({ status: false, message: 'Network error' }); };
		xhr.send(body.join('&'));
	}

	function loadConfig() {
		xhrGet({ action: 'get_config' }, function (data) {
			if (!data || !data.status) { return; }
			var cfgData = data.config || {};
			var defaults = data.defaults || {};
			if (cfgEnabled) { cfgEnabled.checked = cfgData.enabled !== 0 && cfgData.enabled !== '0'; }
			if (cfgDomain) { cfgDomain.value = cfgData.domain || defaults.domain || ''; }
			if (cfgName) { cfgName.value = cfgData.agent_name || defaults.agent_name || ''; }
			if (cfgLogo) { cfgLogo.value = cfgData.logo_url || defaults.logo_url || ''; }
			if (cfgSubtitle) { cfgSubtitle.value = cfgData.subtitle || defaults.subtitle || ''; }
			if (cfgGreeting) { cfgGreeting.value = cfgData.greeting || defaults.greeting || ''; }
			if (cfgPrompt) { cfgPrompt.value = cfgData.system_prompt || ''; }
			if (cfgTeaser) { cfgTeaser.value = cfgData.teaser_text || defaults.teaser_text || ''; }
			if (cfgPlaceholder) { cfgPlaceholder.value = cfgData.placeholder || defaults.placeholder || ''; }
		});
	}

	function saveConfig() {
		if (cfgStatus) { cfgStatus.textContent = 'Saving…'; }
		xhrGet({
			action: 'save_config',
			enabled: (cfgEnabled && cfgEnabled.checked) ? 1 : 0,
			domain: cfgDomain ? cfgDomain.value.trim() : '',
			agent_name: cfgName ? cfgName.value.trim() : '',
			logo_url: cfgLogo ? cfgLogo.value.trim() : '',
			subtitle: cfgSubtitle ? cfgSubtitle.value.trim() : '',
			greeting: cfgGreeting ? cfgGreeting.value.trim() : '',
			system_prompt: cfgPrompt ? cfgPrompt.value.trim() : '',
			teaser_text: cfgTeaser ? cfgTeaser.value.trim() : '',
			placeholder: cfgPlaceholder ? cfgPlaceholder.value.trim() : ''
		}, function (data) {
			if (!data || !data.status) {
				if (cfgStatus) { cfgStatus.textContent = (data && data.message) ? data.message : 'Save failed'; }
				showMsg((data && data.message) ? data.message : 'Could not save configuration', false);
				return;
			}
			if (cfgStatus) { cfgStatus.textContent = 'Saved'; }
			showMsg('Agent configuration saved.', true);
		});
	}

	function loadStats() {
		xhrGet({ action: 'stats' }, function (data) {
			if (!data || !data.status || !data.stats) return;
			var s = data.stats;
			var html = '';
			html += '<div class="epc-agent-stat"><strong>' + (s.total_sessions || 0) + '</strong><span>Total sessions</span></div>';
			html += '<div class="epc-agent-stat"><strong>' + (s.sessions_today || 0) + '</strong><span>Sessions today</span></div>';
			html += '<div class="epc-agent-stat"><strong>' + (s.messages_today || 0) + '</strong><span>Messages today</span></div>';
			html += '<div class="epc-agent-stat"><strong>' + (s.logged_in_sessions || 0) + '</strong><span>Logged-in customers</span></div>';
			html += '<div class="epc-agent-stat"><strong>' + (s.guest_sessions || 0) + '</strong><span>Guest visitors</span></div>';
			statsBox.innerHTML = html;
		});
	}

	function formatCustomerCell(row) {
		var c = row.customer || {};
		var country = c.country_label || c.ip_country_name || c.profile_country_name || c.market_country_name || '';
		if (c.type === 'user') {
			var html = escHtml(c.name || 'Customer');
			if (country) {
				html += '<br><span class="epc-agent-country">' + escHtml(country) + '</span>';
			}
			if (c.email) {
				html += '<br><span style="color:#888;font-size:12px;">' + escHtml(c.email) + '</span>';
			}
			return html;
		}
		if (c.type === 'guest') {
			if (country) {
				var guestHtml = 'Guest · <span class="epc-agent-country">' + escHtml(country) + '</span>';
				if (c.ip) {
					guestHtml += '<br><span style="color:#888;font-size:12px;">IP ' + escHtml(c.ip) + '</span>';
				}
				return guestHtml;
			}
		}
		return escHtml(c.label || '—');
	}

	function formatCountryDetail(session) {
		var country = session.country || {};
		var c = session.customer || {};
		var parts = country.parts || c.country_parts || [];
		if (parts.length) {
			return parts.map(function (p) { return escHtml(p); }).join(' · ');
		}
		var primary = country.primary || c.country_label || '';
		if (primary) {
			return escHtml(primary);
		}
		return 'Unknown';
	}

	function loadList() {
		tbody.innerHTML = '<tr><td colspan="6">Loading…</td></tr>';
		xhrGet({
			action: 'list',
			q: qInput.value.trim(),
			date_from: fromInput.value,
			date_to: toInput.value,
			limit: limit,
			offset: offset
		}, function (data) {
			if (!data || !data.status) {
				var err = (data && (data.message || data.error)) ? (data.message || data.error) : 'Load failed';
				tbody.innerHTML = '<tr><td colspan="6">' + escHtml(err) + '</td></tr>';
				showMsg(err, false);
				return;
			}
			total = data.total || 0;
			var rows = data.sessions || [];
			if (data.auto_synced) {
				showMsg('Auto-synced ' + data.auto_synced + ' session file(s) from server temp into the database.', true);
			}
			if (!rows.length) {
				tbody.innerHTML = '<tr><td colspan="6">No chat sessions found. Open the storefront chat widget, send a message, then click Sync temp files.</td></tr>';
			} else {
				tbody.innerHTML = rows.map(function (row) {
					var customerHtml = formatCustomerCell(row);
					return '<tr>' +
						'<td>' + escHtml(fmtTs(row.updated_at)) + '</td>' +
						'<td><code>' + escHtml(row.session_id) + '</code></td>' +
						'<td>' + escHtml(String(row.message_count || 0)) + '</td>' +
						'<td><div class="epc-agent-preview" title="' + escHtml((row.customer && row.customer.label) ? row.customer.label : '') + '">' + customerHtml + '</div></td>' +
						'<td><div class="epc-agent-preview" title="' + escHtml(row.last_user_text || '') + '">' + escHtml(row.last_user_text || '—') + '</div></td>' +
						'<td><button type="button" class="btn btn-primary btn-xs epc-agent-view" data-session="' + escHtml(row.session_id) + '">View</button></td>' +
						'</tr>';
				}).join('');
			}
			var from = total ? offset + 1 : 0;
			var to = Math.min(offset + limit, total);
			pageInfo.textContent = from + '–' + to + ' of ' + total;
			prevBtn.disabled = offset <= 0;
			nextBtn.disabled = offset + limit >= total;
		});
	}

	function renderDetail(detail) {
		var session = detail.session || {};
		var messages = detail.messages || [];
		detailTitle.textContent = 'Session ' + (session.session_id || '');

		var customerLine = '';
		if (session.customer && session.customer.label) {
			customerLine = session.customer.label.replace(/\s·\sCountry:.*$/, '');
		} else if (session.user_id && parseInt(session.user_id, 10) > 0) {
			customerLine = 'User ID ' + session.user_id;
		} else {
			customerLine = 'Guest';
		}

		var html = '';
		html += '<div><strong>Created:</strong> ' + escHtml(fmtTs(session.created_at)) + ' · ';
		html += '<strong>Updated:</strong> ' + escHtml(fmtTs(session.updated_at)) + '</div>';
		html += '<div><strong>Customer:</strong> ' + escHtml(customerLine) + '</div>';
		html += '<div><strong>Country:</strong> <span class="epc-agent-meta-country">' + formatCountryDetail(session) + '</span></div>';
		if (session.client_ip) {
			html += '<div><strong>IP:</strong> ' + escHtml(session.client_ip) + '</div>';
		}
		if (session.user_agent) {
			html += '<div style="margin-top:4px;color:#888;"><strong>Browser:</strong> ' + escHtml(session.user_agent) + '</div>';
		}
		detailSub.className = 'epc-agent-meta-block';
		detailSub.innerHTML = html;

		if (!messages.length) {
			detailBody.innerHTML = '<p>No messages logged for this session.</p>';
			return;
		}

		detailBody.innerHTML = messages.map(function (m) {
			var role = m.role === 'user' ? 'user' : 'agent';
			var linksHtml = '';
			if (m.reply_links_json) {
				try {
					var links = JSON.parse(m.reply_links_json);
					if (links && links.length) {
						linksHtml = '<div class="epc-agent-links">' + links.map(function (lnk) {
							var href = lnk.url || lnk.href || '#';
							var label = lnk.label || lnk.title || href;
							return '<a href="' + escHtml(href) + '" target="_blank" rel="noopener">' + escHtml(label) + '</a>';
						}).join('') + '</div>';
					}
				} catch (e) {}
			}
			return '<div class="epc-agent-bubble ' + role + '">' +
				renderMarkdownLite(m.message_text || '') +
				linksHtml +
				'<time>' + escHtml(fmtTs(m.created_at)) + '</time>' +
				'</div>';
		}).join('');
	}

	function openDetail(sessionId) {
		xhrGet({ action: 'detail', session_id: sessionId }, function (data) {
			if (!data || !data.status) {
				showMsg((data && data.message) ? data.message : 'Could not load session', false);
				return;
			}
			renderDetail(data.detail || {});
			if (detailWrap) {
				detailWrap.style.display = 'block';
				detailWrap.classList.remove('is-empty');
				detailWrap.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
			}
		});
	}

	function exportCsv() {
		var csrf = getCsrfKey();
		if (!csrf) {
			showMsg('CSRF token missing. Please reload this CP page.', false);
			return;
		}
		var form = document.createElement('form');
		form.method = 'POST';
		form.action = ajaxUrl;
		form.style.display = 'none';
		var fields = {
			action: 'export_csv',
			csrf_guard_key: csrf,
			q: qInput ? qInput.value.trim() : '',
			date_from: fromInput ? fromInput.value : '',
			date_to: toInput ? toInput.value : ''
		};
		Object.keys(fields).forEach(function (k) {
			var input = document.createElement('input');
			input.type = 'hidden';
			input.name = k;
			input.value = fields[k];
			form.appendChild(input);
		});
		document.body.appendChild(form);
		form.submit();
		document.body.removeChild(form);
	}

	document.getElementById('epc-agent-search').addEventListener('click', function () {
		offset = 0;
		loadList();
	});
	document.getElementById('epc-agent-reset').addEventListener('click', function () {
		qInput.value = '';
		fromInput.value = '';
		toInput.value = '';
		offset = 0;
		loadList();
	});
	document.getElementById('epc-agent-sync').addEventListener('click', function () {
		xhrGet({ action: 'sync' }, function (data) {
			if (!data || !data.status) {
				showMsg((data && data.message) ? data.message : 'Sync failed', false);
				return;
			}
			showMsg('Synced ' + (data.synced || 0) + ' session file(s) from server temp.', true);
			loadStats();
			loadList();
		});
	});
	var exportBtn = document.getElementById('epc-agent-export');
	if (exportBtn) {
		exportBtn.addEventListener('click', exportCsv);
	}
	prevBtn.addEventListener('click', function () {
		offset = Math.max(0, offset - limit);
		loadList();
	});
	nextBtn.addEventListener('click', function () {
		if (offset + limit < total) {
			offset += limit;
			loadList();
		}
	});
	document.getElementById('epc-agent-detail-close').addEventListener('click', function () {
		if (!detailWrap) { return; }
		detailTitle.textContent = 'Chat detail';
		detailSub.innerHTML = '';
		detailBody.innerHTML = '<div class="epc-agent-empty">Select a session to read the transcript.</div>';
		detailWrap.classList.add('is-empty');
	});
	tbody.addEventListener('click', function (e) {
		var btn = e.target.closest('.epc-agent-view');
		if (!btn) return;
		openDetail(btn.getAttribute('data-session'));
	});
	document.getElementById('epc-agent-cfg-save').addEventListener('click', saveConfig);

	loadConfig();
	loadStats();
	loadList();
})();
