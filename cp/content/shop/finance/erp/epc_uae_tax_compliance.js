(function () {
	'use strict';

	if (window.__epcUaeTaxComplianceBound) {
		return;
	}
	window.__epcUaeTaxComplianceBound = true;

	var root = document.getElementById('epc_uae_tax_compliance_root');
	var cfg = window.EPC_UAE_TAX_COMPLIANCE || {};
	var ajaxUrl = (root && root.getAttribute('data-erp-ajax')) || cfg.ajaxUrl || '';
	var csrf = (root && root.getAttribute('data-csrf')) || cfg.csrf || '';
	if (!csrf) {
		var csrfInput = document.querySelector('input[name="csrf_guard_key"]');
		if (csrfInput && csrfInput.value) {
			csrf = csrfInput.value;
		}
	}
	// Standalone /erp portal: prefer the portal ajax door when data-attr is empty.
	if (!ajaxUrl) {
		var path = (location.pathname || '');
		if (path.indexOf('/erp') !== -1) {
			ajaxUrl = '/erp/ajax';
		} else if (path.indexOf('/cp/') !== -1 || path.indexOf('/shop/finance/erp') !== -1) {
			ajaxUrl = '/cp/content/shop/finance/erp/ajax_erp_endpoint.php';
		}
	}
	if (!ajaxUrl) {
		console.error('EPC tax compliance: ajax URL missing — Fetch legislation button disabled');
	}

	function showMsg(level, text) {
		var msg = document.getElementById('epc_erp_msg');
		if (!msg) {
			window.alert(text || '');
			return;
		}
		msg.className = 'alert alert-' + level;
		msg.style.display = 'block';
		msg.textContent = text || '';
	}

	function parseJsonResponse(r) {
		return r.text().then(function (t) {
			try {
				return JSON.parse(t);
			} catch (e) {
				throw new Error('Server returned invalid JSON (HTTP ' + r.status + '). Try refreshing the page.');
			}
		});
	}

	function postJson(fd) {
		if (!ajaxUrl) {
			return Promise.reject(new Error('AJAX endpoint not configured'));
		}
		return fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(parseJsonResponse);
	}

	var fdCt = document.getElementById('epc_ct_adjustments_form');
	if (fdCt) {
		fdCt.addEventListener('submit', function (e) {
			e.preventDefault();
			var fd = new FormData(fdCt);
			fd.append('action', 'uae_tax_save_ct_adjustments');
			if (csrf) {
				fd.append('csrf_guard_key', csrf);
			}
			postJson(fd).then(function (j) {
				showMsg(j.status ? 'success' : 'danger', j.message || '');
				if (j.status) {
					location.reload();
				}
			}).catch(function (err) {
				showMsg('danger', (err && err.message) || 'Request failed');
			});
		});
	}

	var btnFetch = document.getElementById('epc_fta_check_updates');
	if (btnFetch) {
		btnFetch.addEventListener('click', function () {
			if (!ajaxUrl) {
				showMsg('danger', 'AJAX endpoint missing — reload the page or open Tax compliance from ERP.');
				return;
			}
			btnFetch.disabled = true;
			var st = document.getElementById('epc_fta_status');
			if (st) {
				st.textContent = 'Fetching from FTA legislation.aspx…';
			}
			var fd = new FormData();
			fd.append('action', 'uae_tax_fta_fetch');
			fd.append('force', '1');
			if (csrf) {
				fd.append('csrf_guard_key', csrf);
			}
			postJson(fd)
				.then(function (j) {
					btnFetch.disabled = false;
					if (j.status || j.ok) {
						if (st) {
							st.textContent = j.message || 'Updated — reloading…';
						}
						location.reload();
						return;
					}
					if (st) {
						st.textContent = j.message || 'Fetch failed';
					}
					showMsg('warning', j.message || 'Fetch failed');
				})
				.catch(function (err) {
					btnFetch.disabled = false;
					var m = (err && err.message) ? err.message : 'Request failed — check network or try again.';
					if (st) {
						st.textContent = m;
					}
					showMsg('danger', m);
				});
		});
	}

	var btnRegen = document.getElementById('epc_fta_regen_summaries');
	if (btnRegen) {
		btnRegen.addEventListener('click', function () {
			if (!confirm('Regenerate ERP summaries for all legislation items?')) {
				return;
			}
			btnRegen.disabled = true;
			var fd = new FormData();
			fd.append('action', 'uae_tax_legislation_regen_summaries');
			if (csrf) {
				fd.append('csrf_guard_key', csrf);
			}
			postJson(fd)
				.then(function (j) {
					btnRegen.disabled = false;
					if (j.status || j.ok) {
						location.reload();
						return;
					}
					showMsg('warning', j.message || 'Regenerate failed');
				})
				.catch(function (err) {
					btnRegen.disabled = false;
					showMsg('danger', (err && err.message) || 'Request failed');
				});
		});
	}

	var qaAsk = document.getElementById('epc_leg_qa_ask');
	var qaInput = document.getElementById('epc_leg_qa_question');

	function epcLegQaRender(j) {
		var reply = document.getElementById('epc_leg_qa_reply');
		var ansEl = document.getElementById('epc_leg_qa_answer');
		var citeEl = document.getElementById('epc_leg_qa_citations');
		var metaEl = document.getElementById('epc_leg_qa_meta');
		if (!reply || !ansEl || !citeEl) {
			return;
		}
		var bullets = j.answer || [];
		var html = '<ul>';
		bullets.forEach(function (b) { html += '<li>' + String(b).replace(/</g, '&lt;') + '</li>'; });
		html += '</ul>';
		ansEl.innerHTML = html;
		citeEl.innerHTML = '';
		(j.citations || []).forEach(function (c) {
			var li = document.createElement('li');
			var title = c.title || 'Legislation';
			var pdf = c.pdf_url || '';
			var excerpt = c.summary_excerpt || '';
			var inner = '<strong>' + String(title).replace(/</g, '&lt;') + '</strong>';
			if (c.issue_date) {
				inner += ' <small class="text-muted">(' + String(c.issue_date).replace(/</g, '&lt;') + ')</small>';
			}
			if (pdf) {
				inner += ' — <a href="' + pdf.replace(/"/g, '&quot;') + '" target="_blank" rel="noopener"><i class="fa fa-file-pdf-o"></i> PDF</a>';
			}
			if (excerpt) {
				inner += '<details style="margin-top:4px;"><summary>Summary excerpt</summary><span style="color:#475569;">' + String(excerpt).replace(/</g, '&lt;') + '</span></details>';
			}
			li.innerHTML = inner;
			citeEl.appendChild(li);
		});
		if (metaEl) {
			metaEl.textContent = (j.disclaimer || '') + (j.confidence ? ' · Match score: ' + j.confidence : '');
		}
		reply.classList.add('visible');
	}

	function epcLegQaSubmit() {
		if (!qaInput || !qaAsk) {
			return;
		}
		var q = (qaInput.value || '').trim();
		if (!q) {
			return;
		}
		qaAsk.disabled = true;
		var st = document.getElementById('epc_leg_qa_status');
		if (st) {
			st.textContent = 'Searching legislation library…';
		}
		var fd = new FormData();
		fd.append('action', 'uae_tax_legislation_ask');
		fd.append('question', q);
		if (csrf) {
			fd.append('csrf_guard_key', csrf);
		}
		postJson(fd)
			.then(function (j) {
				qaAsk.disabled = false;
				if (st) {
					st.textContent = j.status || j.ok ? 'Done' : (j.message || 'Failed');
				}
				if (j.status || j.ok) {
					epcLegQaRender(j);
				} else {
					showMsg('warning', j.message || 'Ask failed');
				}
			})
			.catch(function (err) {
				qaAsk.disabled = false;
				if (st) {
					st.textContent = (err && err.message) || 'Request failed';
				}
			});
	}

	if (qaAsk && qaInput) {
		qaAsk.addEventListener('click', epcLegQaSubmit);
		qaInput.addEventListener('keydown', function (e) {
			if (e.key === 'Enter' && !e.shiftKey) {
				e.preventDefault();
				epcLegQaSubmit();
			}
		});
	}

	document.querySelectorAll('.epc-leg-qa-example').forEach(function (btn) {
		btn.addEventListener('click', function () {
			if (qaInput) {
				qaInput.value = btn.getAttribute('data-q') || '';
			}
			epcLegQaSubmit();
		});
	});

	function epcLegImplBadge(card, implStatus, done, total) {
		if (!card) {
			return;
		}
		var label = implStatus === 'implemented' ? 'Implemented' : (implStatus === 'in_progress' ? 'In progress' : 'Implementation pending');
		var color = implStatus === 'implemented' ? '#1a7f37' : (implStatus === 'in_progress' ? '#b8860b' : '#c0392b');
		var badges = card.querySelectorAll('.epc-leg-item-hd .label');
		badges.forEach(function (b) {
			var t = (b.textContent || '').toLowerCase();
			if (t.indexOf('implementation') !== -1 || t === 'implemented' || t === 'in progress') {
				b.textContent = label;
				b.style.background = color;
			}
		});
		var countEl = card.querySelector('.epc-leg-item-bd .text-muted');
		if (countEl && typeof done === 'number' && typeof total === 'number') {
			countEl.textContent = done + '/' + total + ' done';
		}
	}

	function epcLegApplyFilter(filterKey, pushUrl) {
		var bar = document.getElementById('epc_leg_filter_bar');
		var list = document.getElementById('epc_leg_list');
		var shownEl = document.getElementById('epc_leg_filter_shown');
		var emptyEl = document.getElementById('epc_leg_filter_empty');
		if (!list) {
			return;
		}
		filterKey = (filterKey || '').toLowerCase();
		if (filterKey === 'ct') {
			filterKey = 'corporate_tax';
		}
		var shown = 0;
		var total = 0;
		list.querySelectorAll('.epc-leg-item').forEach(function (row) {
			total++;
			var tt = (row.getAttribute('data-tax-type') || 'general').toLowerCase();
			var ok = !filterKey || tt === filterKey;
			row.style.display = ok ? '' : 'none';
			if (ok) {
				shown++;
			}
		});
		if (shownEl) {
			shownEl.textContent = String(shown);
		}
		if (emptyEl) {
			emptyEl.style.display = shown === 0 ? '' : 'none';
		}
		if (bar) {
			bar.setAttribute('data-filter', filterKey);
			bar.querySelectorAll('.epc-leg-filter-btn').forEach(function (btn) {
				var f = (btn.getAttribute('data-filter') || '').toLowerCase();
				if (f === filterKey) {
					btn.classList.add('active');
				} else {
					btn.classList.remove('active');
				}
			});
		}
		if (pushUrl) {
			try {
				var u = new URL(window.location.href);
				if (filterKey) {
					u.searchParams.set('tax_type', filterKey);
				} else {
					u.searchParams.delete('tax_type');
					u.searchParams.delete('leg_filter');
				}
				u.searchParams.set('tax_panel', 'legislation');
				window.history.replaceState({}, '', u.toString());
			} catch (e) {}
		}
	}

	document.querySelectorAll('.epc-leg-filter-btn').forEach(function (btn) {
		btn.addEventListener('click', function (e) {
			// Instant client-side filter — do not rely on a full reload.
			e.preventDefault();
			epcLegApplyFilter(btn.getAttribute('data-filter') || '', true);
		});
	});

	(function epcLegInitFilterFromUrl() {
		var bar = document.getElementById('epc_leg_filter_bar');
		if (!bar) {
			return;
		}
		var initial = bar.getAttribute('data-filter') || '';
		try {
			var u = new URL(window.location.href);
			initial = u.searchParams.get('tax_type') || u.searchParams.get('leg_filter') || initial || '';
		} catch (e) {}
		epcLegApplyFilter(initial, false);
	})();

	document.querySelectorAll('.epc-leg-check').forEach(function (cb) {
		cb.addEventListener('change', function () {
			var itemKey = cb.getAttribute('data-item-key') || '';
			var actionKey = cb.getAttribute('data-action-key') || '';
			var actionText = cb.getAttribute('data-action-text') || '';
			var card = cb.closest('.epc-leg-item');
			var allTexts = [];
			if (card) {
				card.querySelectorAll('.epc-leg-check').forEach(function (x) {
					var t = x.getAttribute('data-action-text') || '';
					if (t) {
						allTexts.push(t);
					}
				});
			}
			var fd = new FormData();
			fd.append('action', 'uae_tax_legislation_checklist_set');
			fd.append('item_key', itemKey);
			fd.append('action_key', actionKey);
			fd.append('action_text', actionText);
			fd.append('done', cb.checked ? '1' : '0');
			fd.append('all_actions_json', JSON.stringify(allTexts));
			if (csrf) {
				fd.append('csrf_guard_key', csrf);
			}
			postJson(fd)
				.then(function (j) {
					if (!(j.status || j.ok)) {
						cb.checked = !cb.checked;
						showMsg('warning', j.message || 'Could not save checklist step');
						return;
					}
					var row = cb.closest('.epc-leg-check-row');
					if (row) {
						if (cb.checked) {
							row.classList.add('is-done');
						} else {
							row.classList.remove('is-done');
						}
					}
					epcLegImplBadge(card, j.impl_status || 'pending', j.impl_done || 0, allTexts.length);
				})
				.catch(function (err) {
					cb.checked = !cb.checked;
					showMsg('danger', (err && err.message) || 'Request failed');
				});
		});
	});
})();
