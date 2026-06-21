(function () {
	'use strict';

	var initialized = false;

	function sourcesCfg() {
		return window.EPC_APAI_SOURCES || {};
	}

	function shouldInit() {
		var cfg = sourcesCfg();
		if (!cfg.ajaxUrl) {
			return false;
		}
		if (cfg.active) {
			return true;
		}
		return !!document.getElementById('epc-disc-src-form');
	}

	function qs(sel) {
		return document.querySelector(sel);
	}

	function postAction(action, extra) {
		var cfg = sourcesCfg();
		var fd = new FormData();
		fd.append('action', action);
		fd.append('site_key', cfg.siteKey || '');
		if (extra) {
			Object.keys(extra).forEach(function (k) {
				var v = extra[k];
				if (v !== undefined && v !== null) {
					fd.append(k, v);
				}
			});
		}
		return fetch(cfg.ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) {
				return r.text().then(function (text) {
					var data = null;
					if (text) {
						try {
							data = JSON.parse(text);
						} catch (e) {
							throw new Error(r.status + ' Invalid response');
						}
					}
					if (!data) {
						throw new Error('Empty server response');
					}
					if (!r.ok && data.message) {
						throw new Error(data.message);
					}
					return data;
				});
			});
	}

	function toast(msg, type) {
		var el = document.getElementById('epc-apai-disc-toast');
		if (!el) {
			el = document.createElement('div');
			el.id = 'epc-apai-disc-toast';
			el.style.cssText = 'position:fixed;bottom:20px;right:20px;z-index:9999;min-width:240px;max-width:420px;box-shadow:0 4px 12px rgba(0,0,0,.15)';
			document.body.appendChild(el);
		}
		el.className = 'alert alert-' + (type || 'info');
		el.textContent = msg;
		el.style.display = 'block';
		clearTimeout(toast._t);
		toast._t = setTimeout(function () {
			el.style.display = 'none';
		}, 4500);
	}

	function scopeLabel(src) {
		if (src.taxonomy_name) {
			return src.taxonomy_name;
		}
		if (src.product_line_slug) {
			return src.product_line_slug;
		}
		return 'All lines';
	}

	function authCellHtml(src) {
		if (src.last_test_ok) {
			return '<span class="label label-success">Login OK ✓</span>';
		}
		if (src.last_test_at && !src.last_test_ok) {
			return '<span class="label label-danger">Login failed</span>';
		}
		if (src.login_configured) {
			return '<span class="label label-info">Login configured</span>';
		}
		return '<span class="text-muted">—</span>';
	}

	function showTestResult(ok, message) {
		var el = qs('#epc-disc-src-test-result');
		if (!el) {
			return;
		}
		el.style.display = 'inline-block';
		el.className = 'epc-disc-src-test-result ' + (ok ? 'epc-disc-src-test-result--ok' : 'epc-disc-src-test-result--fail');
		el.textContent = message || (ok ? 'Login successful ✓' : 'Login failed');
	}

	function hideTestResult() {
		var el = qs('#epc-disc-src-test-result');
		if (el) {
			el.style.display = 'none';
			el.textContent = '';
			el.className = 'epc-disc-src-test-result';
		}
	}

	function toggleAuthFields(show) {
		var block = qs('#epc-disc-src-auth-fields');
		if (block) {
			block.classList.toggle('epc-disc-src-auth-fields--open', !!show);
			block.style.display = show ? 'block' : '';
		}
		toggleFormLoginOnly();
	}

	function toggleFormLoginOnly() {
		var authType = (qs('#epc-disc-src-auth-type') || {}).value || 'form_login';
		var block = qs('#epc-disc-src-auth-fields');
		if (block) {
			block.classList.toggle('epc-disc-src-auth-fields--form-login', authType === 'form_login');
		}
		document.querySelectorAll('.epc-disc-src-form-login-only').forEach(function (el) {
			el.style.display = authType === 'form_login' ? 'block' : 'none';
		});
	}

	function authPayloadFromForm() {
		var id = parseInt((qs('#epc-disc-src-id') || {}).value || '0', 10);
		var needsLogin = (qs('#epc-disc-src-requires-login') || {}).checked;
		var authType = needsLogin ? ((qs('#epc-disc-src-auth-type') || {}).value || 'form_login') : 'none';
		if (needsLogin && authType === 'none') {
			authType = 'form_login';
		}
		var payload = {
			domain: (qs('#epc-disc-src-domain') || {}).value || '',
			requires_login: needsLogin ? '1' : '0',
			auth_type: authType,
			auth_username: needsLogin ? ((qs('#epc-disc-src-auth-username') || {}).value || '') : '',
		};
		if (needsLogin) {
			var pwdVal = (qs('#epc-disc-src-auth-password') || {}).value || '';
			if (pwdVal !== '') {
				payload.auth_password = pwdVal;
			}
			payload.login_url = (qs('#epc-disc-src-login-url') || {}).value || '';
			payload.login_form_selector = (qs('#epc-disc-src-login-selector') || {}).value || '';
		}
		if (id > 0) {
			payload.id = id;
		}
		return payload;
	}

	function renderRow(src) {
		var originLabel = src.origin === 'custom' ? 'Custom' : 'Country pack';
		var originClass = src.origin === 'custom' ? 'label-success' : 'label-default';
		var tr = document.createElement('tr');
		tr.setAttribute('data-source-id', String(src.id));
		tr.setAttribute('data-editable', src.editable ? '1' : '0');
		var statusHtml = src.enabled ? '<span class="text-success">Enabled</span>' : '<span class="text-muted">Disabled</span>';
		var lastCrawl = src.last_crawl ? new Date(src.last_crawl * 1000).toLocaleString(undefined, { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }) : '—';
		var actions = '<button type="button" class="btn btn-xs btn-warning epc-disc-src-toggle">' + (src.enabled ? 'Disable' : 'Enable') + '</button>';
		if (src.editable) {
			actions = '<button type="button" class="btn btn-xs btn-default epc-disc-src-edit">Edit</button> ' + actions +
				' <button type="button" class="btn btn-xs btn-danger epc-disc-src-delete">×</button>';
		}
		tr.innerHTML =
			'<td><span class="label ' + originClass + '">' + originLabel + '</span></td>' +
			'<td><code>' + (src.domain || '') + '</code></td>' +
			'<td>' + (src.label || '') + '</td>' +
			'<td>' + scopeLabel(src) + '</td>' +
			'<td>' + authCellHtml(src) + '</td>' +
			'<td>' + statusHtml + '</td>' +
			'<td>' + lastCrawl + '</td>' +
			'<td class="epc-disc-src-actions">' + actions + '</td>';
		var editBtn = tr.querySelector('.epc-disc-src-edit');
		if (editBtn) {
			editBtn.setAttribute('data-source', JSON.stringify(src));
		}
		return tr;
	}

	function refreshList() {
		return postAction('list_discovery_sources', {}).then(function (data) {
			var tbody = qs('#epc-disc-src-tbody');
			var countEl = qs('#epc-disc-src-count');
			if (!tbody) {
				return data;
			}
			tbody.innerHTML = '';
			var sources = data.sources || [];
			if (!sources.length) {
				tbody.innerHTML = '<tr class="epc-disc-src-empty"><td colspan="8" class="text-muted">No sources — run setup seed or add a custom website.</td></tr>';
			} else {
				sources.forEach(function (src) {
					tbody.appendChild(renderRow(src));
				});
			}
			if (countEl) {
				countEl.textContent = String(sources.length);
			}
			bindRowActions();
			return data;
		});
	}

	function resetForm() {
		var form = qs('#epc-disc-src-form');
		if (!form) {
			return;
		}
		form.reset();
		var idInput = qs('#epc-disc-src-id');
		if (idInput) {
			idInput.value = '0';
		}
		var enabled = qs('#epc-disc-src-enabled');
		if (enabled) {
			enabled.checked = true;
		}
		var requiresLogin = qs('#epc-disc-src-requires-login');
		if (requiresLogin) {
			requiresLogin.checked = false;
		}
		toggleAuthFields(false);
		hideTestResult();
		var pwd = qs('#epc-disc-src-auth-password');
		if (pwd) {
			pwd.value = '';
			pwd.placeholder = 'Enter password';
		}
		var submit = qs('#epc-disc-src-submit');
		if (submit) {
			submit.innerHTML = '<i class="fa fa-plus"></i> Add custom source';
		}
		var cancel = qs('#epc-disc-src-cancel');
		if (cancel) {
			cancel.style.display = 'none';
		}
	}

	function bindForm() {
		var form = qs('#epc-disc-src-form');
		if (!form) {
			return;
		}
		var requiresLogin = qs('#epc-disc-src-requires-login');
		if (requiresLogin) {
			requiresLogin.addEventListener('change', function () {
				toggleAuthFields(requiresLogin.checked);
				hideTestResult();
			});
		}
		var authType = qs('#epc-disc-src-auth-type');
		if (authType) {
			authType.addEventListener('change', function () {
				toggleFormLoginOnly();
				hideTestResult();
			});
		}

		var testBtn = qs('#epc-disc-src-test-login');
		if (testBtn) {
			testBtn.addEventListener('click', function () {
				var payload = authPayloadFromForm();
				if (!payload.requires_login || payload.requires_login === '0') {
					showTestResult(false, 'Check Requires login first');
					return;
				}
				if (!payload.auth_username) {
					showTestResult(false, 'Enter username');
					return;
				}
				if (!payload.auth_password && !payload.id) {
					showTestResult(false, 'Enter password');
					return;
				}
				testBtn.disabled = true;
				hideTestResult();
				postAction('test_source_login', payload)
					.then(function (data) {
						showTestResult(!!data.ok, data.message || (data.ok ? 'Login successful ✓' : 'Login failed'));
						if (payload.id) {
							return refreshList();
						}
					})
					.catch(function (err) {
						showTestResult(false, 'Login failed: ' + (err.message || String(err)));
					})
					.finally(function () {
						testBtn.disabled = false;
					});
			});
		}

		form.addEventListener('submit', function (e) {
			e.preventDefault();
			var id = parseInt((qs('#epc-disc-src-id') || {}).value || '0', 10);
			var taxSel = qs('#epc-disc-src-taxonomy');
			var taxOpt = taxSel && taxSel.selectedOptions ? taxSel.selectedOptions[0] : null;
			var payload = authPayloadFromForm();
			payload.label = (qs('#epc-disc-src-label') || {}).value || '';
			payload.taxonomy_node_id = taxSel ? taxSel.value : '0';
			payload.product_line_slug = taxOpt ? (taxOpt.getAttribute('data-slug') || '') : '';
			payload.enabled = (qs('#epc-disc-src-enabled') || {}).checked ? '1' : '0';
			var submit = qs('#epc-disc-src-submit');
			if (submit) {
				submit.disabled = true;
			}
			postAction('add_discovery_source', payload)
				.then(function (data) {
					toast(data.message || 'Source saved', 'success');
					resetForm();
					return refreshList();
				})
				.catch(function (err) {
					toast(err.message || String(err), 'danger');
				})
				.finally(function () {
					if (submit) {
						submit.disabled = false;
					}
				});
		});

		var cancel = qs('#epc-disc-src-cancel');
		if (cancel) {
			cancel.addEventListener('click', function () {
				resetForm();
			});
		}

		['#epc-disc-src-auth-username', '#epc-disc-src-auth-password', '#epc-disc-src-login-url', '#epc-disc-src-domain'].forEach(function (sel) {
			var el = qs(sel);
			if (el) {
				el.addEventListener('input', hideTestResult);
			}
		});
	}

	function bindRowActions() {
		document.querySelectorAll('#epc-disc-src-tbody .epc-disc-src-edit').forEach(function (btn) {
			btn.onclick = function () {
				var raw = btn.getAttribute('data-source');
				if (!raw) {
					return;
				}
				var src = JSON.parse(raw);
				var idInput = qs('#epc-disc-src-id');
				if (idInput) {
					idInput.value = String(src.id);
				}
				var domain = qs('#epc-disc-src-domain');
				if (domain) {
					domain.value = src.domain || '';
				}
				var label = qs('#epc-disc-src-label');
				if (label) {
					label.value = src.label || '';
				}
				var tax = qs('#epc-disc-src-taxonomy');
				if (tax) {
					tax.value = String(src.taxonomy_node_id || 0);
				}
				var enabled = qs('#epc-disc-src-enabled');
				if (enabled) {
					enabled.checked = !!src.enabled;
				}
				var needsLogin = !!(src.login_configured || (src.auth_type && src.auth_type !== 'none'));
				var requiresLoginEl = qs('#epc-disc-src-requires-login');
				if (requiresLoginEl) {
					requiresLoginEl.checked = needsLogin;
				}
				toggleAuthFields(needsLogin);
				var authTypeEl = qs('#epc-disc-src-auth-type');
				if (authTypeEl) {
					authTypeEl.value = src.auth_type && src.auth_type !== 'none' ? src.auth_type : 'form_login';
				}
				var authUser = qs('#epc-disc-src-auth-username');
				if (authUser) {
					authUser.value = src.auth_username || '';
				}
				var authPwd = qs('#epc-disc-src-auth-password');
				if (authPwd) {
					authPwd.value = '';
					authPwd.placeholder = needsLogin ? 'Leave blank to keep existing' : 'Enter password';
				}
				var loginUrl = qs('#epc-disc-src-login-url');
				if (loginUrl) {
					loginUrl.value = src.login_url || '';
				}
				var loginSel = qs('#epc-disc-src-login-selector');
				if (loginSel) {
					loginSel.value = src.login_form_selector || '';
				}
				toggleFormLoginOnly();
				if (src.last_test_at) {
					showTestResult(!!src.last_test_ok, src.last_test_message || (src.last_test_ok ? 'Login successful ✓' : 'Login failed'));
				} else {
					hideTestResult();
				}
				var submit = qs('#epc-disc-src-submit');
				if (submit) {
					submit.innerHTML = '<i class="fa fa-save"></i> Save custom source';
				}
				var cancel = qs('#epc-disc-src-cancel');
				if (cancel) {
					cancel.style.display = 'inline-block';
				}
			};
		});

		document.querySelectorAll('#epc-disc-src-tbody .epc-disc-src-toggle').forEach(function (btn) {
			btn.onclick = function () {
				var tr = btn.closest('tr');
				var id = parseInt(tr.getAttribute('data-source-id'), 10);
				postAction('toggle_discovery_source', { id: id })
					.then(function (data) {
						toast(data.message || 'Updated', 'success');
						return refreshList();
					})
					.catch(function (err) {
						toast(err.message || String(err), 'danger');
					});
			};
		});

		document.querySelectorAll('#epc-disc-src-tbody .epc-disc-src-delete').forEach(function (btn) {
			btn.onclick = function () {
				var tr = btn.closest('tr');
				if (tr.getAttribute('data-editable') !== '1') {
					return;
				}
				var id = parseInt(tr.getAttribute('data-source-id'), 10);
				if (!window.confirm('Remove this custom source?')) {
					return;
				}
				postAction('delete_discovery_source', { id: id })
					.then(function (data) {
						toast(data.message || 'Removed', 'success');
						return refreshList();
					})
					.catch(function (err) {
						toast(err.message || String(err), 'danger');
					});
			};
		});
	}

	function init() {
		if (initialized || !shouldInit()) {
			return;
		}
		initialized = true;
		bindForm();
		bindRowActions();
		if ((qs('#epc-disc-src-requires-login') || {}).checked) {
			toggleAuthFields(true);
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}

	document.addEventListener('epc-apai-tab-loaded', function () {
		initialized = false;
		init();
	});
})();
