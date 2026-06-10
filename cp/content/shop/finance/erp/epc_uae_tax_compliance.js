(function () {
	'use strict';

	var root = document.getElementById('epc_uae_tax_compliance_root');
	var cfg = window.EPC_UAE_TAX_COMPLIANCE || {};
	var ajaxUrl = (root && root.getAttribute('data-erp-ajax')) || cfg.ajaxUrl || '';
	var csrf = (root && root.getAttribute('data-csrf')) || cfg.csrf || '';
	if (!ajaxUrl) {
		return;
	}

	function showMsg(level, text) {
		var msg = document.getElementById('epc_erp_msg');
		if (!msg) {
			return;
		}
		msg.className = 'alert alert-' + level;
		msg.style.display = 'block';
		msg.textContent = text || '';
	}

	function postJson(fd) {
		return fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) { return r.json(); });
	}

	var fdCt = document.getElementById('epc_ct_adjustments_form');
	if (fdCt) {
		fdCt.addEventListener('submit', function (e) {
			e.preventDefault();
			var fd = new FormData(fdCt);
			fd.append('action', 'uae_tax_save_ct_adjustments');
			postJson(fd).then(function (j) {
				showMsg(j.status ? 'success' : 'danger', j.message || '');
				if (j.status) {
					location.reload();
				}
			});
		});
	}

	var btnFetch = document.getElementById('epc_fta_check_updates');
	if (btnFetch) {
		btnFetch.addEventListener('click', function () {
			btnFetch.disabled = true;
			var st = document.getElementById('epc_fta_status');
			if (st) {
				st.textContent = 'Fetching from FTA legislation.aspx…';
			}
			var fd = new FormData();
			fd.append('action', 'uae_tax_fta_fetch');
			fd.append('force', '1');
			fd.append('csrf_guard_key', csrf);
			postJson(fd)
				.then(function (j) {
					btnFetch.disabled = false;
					if (j.status || j.ok) {
						location.reload();
						return;
					}
					if (st) {
						st.textContent = j.message || 'Fetch failed';
					}
					showMsg('warning', j.message || 'Fetch failed');
				})
				.catch(function () {
					btnFetch.disabled = false;
					if (st) {
						st.textContent = 'Request failed';
					}
					showMsg('danger', 'Request failed — check network or try again.');
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
			fd.append('csrf_guard_key', csrf);
			postJson(fd)
				.then(function (j) {
					btnRegen.disabled = false;
					if (j.status || j.ok) {
						location.reload();
						return;
					}
					showMsg('warning', j.message || 'Regenerate failed');
				})
				.catch(function () {
					btnRegen.disabled = false;
					showMsg('danger', 'Request failed');
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
		fd.append('csrf_guard_key', csrf);
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
			.catch(function () {
				qaAsk.disabled = false;
				if (st) {
					st.textContent = 'Request failed';
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
})();
