/**
 * Custom & Shipping declaration form — PDF parse, line items, save/delete (external, eval-safe).
 */
(function () {
	'use strict';

	var boot = {};
	var bootEl = document.getElementById('epc_cs_form_boot');
	if (bootEl && bootEl.textContent) {
		try {
			boot = JSON.parse(bootEl.textContent);
		} catch (e) {
			boot = {};
		}
	}

	var form = document.getElementById('epc_cs_declaration_form');
	var submitBtn = document.getElementById('epc_cs_submit_btn');
	var erpPostUrl = boot.erpPostUrl || '';
	var csFrom = boot.csFrom || '';
	var csTo = boot.csTo || '';
	var csDefaultCategory = boot.csDefaultCategory || '';
	var csReportsUrl = boot.csReportsUrl || '';
	var lineItemsInitial = boot.lineItemsInitial || [];
	var unitOptions = boot.unitOptions || ['PCS'];
	var volumeUnitOptions = boot.volumeUnitOptions || ['CBM'];
	var csrf = boot.csrf || '';

	if (!erpPostUrl) {
		return;
	}

	function epcCsEsc(s) {
		var d = document.createElement('div');
		d.textContent = s == null ? '' : String(s);
		return d.innerHTML;
	}

	function showMsg(ok, text) {
		var el = document.getElementById('epc_erp_msg');
		if (!el) {
			window.alert(text);
			return;
		}
		el.className = 'alert epc-erp-msg ' + (ok ? 'alert-success' : 'alert-danger');
		el.style.display = 'block';
		el.textContent = text;
	}

	function epcCsFetchJson(url, options) {
		return fetch(url, options).then(function (r) {
			var ct = (r.headers.get('content-type') || '').toLowerCase();
			if (ct.indexOf('application/json') === -1 && ct.indexOf('text/json') === -1) {
				return r.text().then(function (t) {
					var msg = 'Server returned an unexpected response';
					if (t.indexOf('epc-erp-isolation-block') >= 0) {
						msg = 'ERP session expired — log out and sign in again at your client ERP URL';
					} else if (t.indexOf('Access denied') >= 0) {
						msg = 'Access denied — refresh the page and try again';
					}
					throw new Error(msg);
				});
			}
			return r.json();
		});
	}

	function epcCsOpenPdfViewer(url, name) {
		url = (url || '').trim();
		name = name || 'Declaration PDF';
		if (!url) {
			showMsg(false, 'No PDF attached');
			return;
		}
		var modal = document.getElementById('epc_cs_pdf_modal') || document.getElementById('epc_cs_pdf_modal_global');
		var frame = document.getElementById('epc_cs_pdf_modal_frame') || document.getElementById('epc_cs_pdf_modal_global_frame');
		var empty = document.getElementById('epc_cs_pdf_modal_empty') || document.getElementById('epc_cs_pdf_modal_global_empty');
		var title = document.getElementById('epc_cs_pdf_modal_title') || document.getElementById('epc_cs_pdf_modal_global_title');
		var openLink = document.getElementById('epc_cs_pdf_modal_open') || document.getElementById('epc_cs_pdf_modal_global_open');
		if (!modal || !frame) {
			window.open(url, '_blank', 'noopener');
			return;
		}
		if (empty) empty.style.display = 'none';
		frame.style.display = 'block';
		frame.src = url + '#toolbar=1';
		if (title) title.innerHTML = '<i class="fa fa-file-pdf-o"></i> ' + epcCsEsc(name);
		if (openLink) {
			openLink.href = url;
			openLink.style.display = '';
		}
		modal.classList.add('is-open');
		modal.style.display = 'flex';
		modal.setAttribute('aria-hidden', 'false');
	}

	function epcCsOpenPdfFromButton(btn) {
		if (!btn) return;
		var url = (btn.getAttribute('data-pdf-url') || '').trim();
		var hasPdf = btn.getAttribute('data-has-pdf') === '1';
		var name = btn.getAttribute('data-pdf-name') || 'Declaration PDF';
		if (!hasPdf || !url) {
			showMsg(false, 'No PDF attached');
			return;
		}
		epcCsOpenPdfViewer(url, name);
	}

	function epcCsClosePdfModal() {
		document.querySelectorAll('#epc_cs_pdf_modal, #epc_cs_pdf_modal_global').forEach(function (modal) {
			modal.classList.remove('is-open');
			modal.style.display = 'none';
			modal.setAttribute('aria-hidden', 'true');
		});
		var frame = document.getElementById('epc_cs_pdf_modal_frame') || document.getElementById('epc_cs_pdf_modal_global_frame');
		if (frame) {
			frame.src = '';
			frame.style.display = 'block';
		}
	}

	['epc_cs_pdf_modal_close', 'epc_cs_pdf_modal_global_close'].forEach(function (id) {
		var btn = document.getElementById(id);
		if (btn) btn.addEventListener('click', function (ev) {
			ev.preventDefault();
			epcCsClosePdfModal();
		});
	});

	document.querySelectorAll('#epc_cs_pdf_modal, #epc_cs_pdf_modal_global').forEach(function (modal) {
		modal.addEventListener('click', function (ev) {
			if (ev.target === modal) epcCsClosePdfModal();
		});
	});

	document.addEventListener('click', function (ev) {
		var btn = ev.target && ev.target.closest ? ev.target.closest('.epc-cs-pdf-view-btn') : null;
		if (!btn) return;
		ev.preventDefault();
		ev.stopPropagation();
		epcCsOpenPdfFromButton(btn);
	});

	function epcCsNum(v) {
		var n = parseFloat(v);
		return isNaN(n) ? 0 : n;
	}

	function epcCsBuildUnitSelect(name, selected) {
		var h = '<select class="form-control input-sm" data-field="' + name + '">';
		unitOptions.forEach(function (u) {
			h += '<option value="' + epcCsEsc(u) + '"' + (u === selected ? ' selected' : '') + '>' + epcCsEsc(u) + '</option>';
		});
		return h + '</select>';
	}

	function epcCsAddLineRow(data) {
		data = data || {};
		var tbody = document.getElementById('epc_cs_line_items_body');
		if (!tbody) return;
		var tr = document.createElement('tr');
		tr.className = 'epc-cs-line-row';
		var autoCls = data._autofill ? ' epc-cs-autofill' : '';
		tr.innerHTML =
			'<td class="col-line epc-cs-line-no">1</td>' +
			'<td><input type="text" class="form-control input-sm' + autoCls + '" data-field="hs_code" value="' + epcCsEsc(data.hs_code || '') + '" required></td>' +
			'<td><input type="text" class="form-control input-sm' + autoCls + '" data-field="description" value="' + epcCsEsc(data.description || '') + '"></td>' +
			'<td><input type="text" class="form-control input-sm' + autoCls + '" data-field="country_of_origin" value="' + epcCsEsc(data.country_of_origin || '') + '" required></td>' +
			'<td><input type="number" step="any" min="0" class="form-control input-sm' + autoCls + '" data-field="quantity" value="' + epcCsEsc(data.quantity != null && data.quantity !== '' ? data.quantity : '') + '" required></td>' +
			'<td>' + epcCsBuildUnitSelect('unit', data.unit || 'PCS') + '</td>' +
			'<td><input type="number" step="any" min="0" class="form-control input-sm" data-field="foreign_value" value="' + epcCsEsc(data.foreign_value != null && data.foreign_value !== '' ? data.foreign_value : '') + '"></td>' +
			'<td><input type="text" class="form-control input-sm" data-field="currency" value="' + epcCsEsc(data.currency || 'AED') + '"></td>' +
			'<td><input type="number" step="any" min="0" class="form-control input-sm" data-field="cif_local_value" value="' + epcCsEsc(data.cif_local_value != null && data.cif_local_value !== '' ? data.cif_local_value : (data.amount != null ? data.amount : '')) + '"></td>' +
			'<td><input type="number" step="any" min="0" class="form-control input-sm" data-field="duty_rate" value="' + epcCsEsc(data.duty_rate != null && data.duty_rate !== '' ? data.duty_rate : '') + '"></td>' +
			'<td><input type="number" step="any" min="0" class="form-control input-sm" data-field="weight_net" value="' + epcCsEsc(data.weight_net != null && data.weight_net !== '' ? data.weight_net : '') + '"></td>' +
			'<td><input type="number" step="any" min="0" class="form-control input-sm" data-field="weight_gross" value="' + epcCsEsc(data.weight_gross != null && data.weight_gross !== '' ? data.weight_gross : (data.weight != null ? data.weight : '')) + '"></td>' +
			'<td><input type="number" step="any" min="0" class="form-control input-sm" data-field="packages_qty" value="' + epcCsEsc(data.packages_qty != null && data.packages_qty !== '' ? data.packages_qty : '') + '"></td>' +
			'<td><input type="text" class="form-control input-sm" data-field="income_type" value="' + epcCsEsc(data.income_type || '') + '"></td>' +
			'<td class="col-rm"><button type="button" class="btn btn-link btn-xs text-danger epc-cs-rm-line" title="Remove"><i class="fa fa-times"></i></button></td>';
		tbody.appendChild(tr);
		tr.querySelector('.epc-cs-rm-line').addEventListener('click', function () {
			if (tbody.querySelectorAll('.epc-cs-line-row').length <= 1) return;
			tr.remove();
			epcCsRenumberLines();
			epcCsUpdateTotals();
		});
		tr.addEventListener('input', epcCsUpdateTotals);
		tr.addEventListener('change', epcCsUpdateTotals);
		epcCsRenumberLines();
		epcCsUpdateTotals();
	}

	function epcCsRenumberLines() {
		document.querySelectorAll('#epc_cs_line_items_body .epc-cs-line-row').forEach(function (tr, i) {
			var cell = tr.querySelector('.epc-cs-line-no');
			if (cell) cell.textContent = String(i + 1);
		});
	}

	function epcCsCollectLineItems() {
		var items = [];
		document.querySelectorAll('#epc_cs_line_items_body .epc-cs-line-row').forEach(function (tr, i) {
			var row = { line_number: i + 1, volume: 0, volume_unit: 'CBM', amount: 0, weight: 0 };
			tr.querySelectorAll('[data-field]').forEach(function (el) {
				row[el.getAttribute('data-field')] = el.value;
			});
			row.amount = epcCsNum(row.cif_local_value || row.amount);
			row.weight = epcCsNum(row.weight_gross || row.weight);
			items.push(row);
		});
		return items;
	}

	function epcCsUpdateTotals() {
		var qty = 0, amt = 0;
		document.querySelectorAll('#epc_cs_line_items_body .epc-cs-line-row').forEach(function (tr) {
			qty += epcCsNum(tr.querySelector('[data-field="quantity"]').value);
			var cifEl = tr.querySelector('[data-field="cif_local_value"]');
			amt += epcCsNum(cifEl ? cifEl.value : 0);
		});
		var qEl = document.getElementById('epc_cs_tot_qty');
		var aEl = document.getElementById('epc_cs_tot_amt');
		if (qEl) qEl.textContent = qty;
		if (aEl) aEl.textContent = amt.toFixed(2);
	}

	function epcCsClearLineItems() {
		var tbody = document.getElementById('epc_cs_line_items_body');
		if (tbody) tbody.innerHTML = '';
	}

	function epcCsMarkAutofill(el, on) {
		if (!el) return;
		if (on) el.classList.add('epc-cs-autofill');
		else el.classList.remove('epc-cs-autofill');
	}

	function epcCsSetField(name, val, autofill) {
		var el = document.querySelector('[name="' + name + '"]');
		if (!el || val === undefined || val === null || val === '') return;
		el.value = val;
		epcCsMarkAutofill(el, !!autofill);
		var wrap = el.closest('[data-field-key]');
		if (wrap) {
			var lbl = wrap.querySelector('label');
			if (lbl && autofill) lbl.classList.add('epc-cs-autofill-label');
		}
	}

	function epcCsSetBoxField(key, val, autofill) {
		var el = document.querySelector('[name="boxes[' + key + ']"]');
		if (!el || val === undefined || val === null || val === '') return;
		el.value = val;
		epcCsMarkAutofill(el, !!autofill);
	}

	function epcCsFillMultiLines(containerId, inputClass, lines, autofill) {
		var container = document.getElementById(containerId);
		if (!container) return;
		container.innerHTML = '';
		(lines && lines.length ? lines : ['']).forEach(function (ln, idx) {
			var row = document.createElement('div');
			row.className = 'epc-cs-multi-row';
			row.innerHTML = '<input type="text" name="' + (containerId === 'epc_cs_box45_lines' ? 'box_45_lines[]' : 'box_54_lines[]') + '" class="form-control input-sm ' + inputClass + (autofill && idx === 0 ? ' epc-cs-autofill' : '') + '" value="' + epcCsEsc(ln) + '">' +
				'<button type="button" class="btn btn-link btn-xs text-danger epc-cs-rm-multi" title="Remove"><i class="fa fa-times"></i></button>';
			container.appendChild(row);
			row.querySelector('.epc-cs-rm-multi').addEventListener('click', function () {
				if (container.querySelectorAll('.epc-cs-multi-row').length <= 1) return;
				row.remove();
			});
		});
	}

	function epcCsShowPdfViewer(url, name) {
		var wrap = document.getElementById('epc_cs_pdf_viewer_wrap');
		var frame = document.getElementById('epc_cs_pdf_viewer_frame');
		var link = document.getElementById('epc_cs_pdf_open_link');
		var label = document.getElementById('epc_cs_pdf_viewer_label');
		if (wrap && url) wrap.style.display = 'block';
		if (frame && url) frame.src = url + '#toolbar=1';
		if (link && url) { link.href = url; link.style.display = ''; }
		if (label && name) label.textContent = name;
	}

	function epcCsApplyPdfData(payload) {
		if (!payload || !payload.form) return;
		var formData = payload.form;
		var parsed = payload.parsed || {};
		var autoKeys = parsed.autofill_keys || [];
		var coreMap = {
			company: 'company', declaration_type: 'declaration_type', declaration_date: 'declaration_date',
			entry_date: 'entry_date', customs_emirate: 'customs_emirate'
		};
		Object.keys(coreMap).forEach(function (k) {
			if (formData[k] !== undefined && formData[k] !== '') epcCsSetField(coreMap[k], formData[k], true);
		});
		var boxes = (formData.box_data && formData.box_data.boxes) ? formData.box_data.boxes : (parsed.boxes || {});
		Object.keys(boxes).forEach(function (bk) {
			epcCsSetBoxField(bk, boxes[bk], autoKeys.indexOf(bk) >= 0);
		});
		epcCsFillMultiLines('epc_cs_box45_lines', 'epc-cs-box45-input', formData.box_data ? formData.box_data.box_45_lines : parsed.box_45_lines, autoKeys.indexOf('box_45') >= 0);
		epcCsFillMultiLines('epc_cs_box54_lines', 'epc-cs-box54-input', formData.box_data ? formData.box_data.box_54_lines : parsed.box_54_lines, autoKeys.indexOf('box_54') >= 0);
		epcCsClearLineItems();
		var items = formData.line_items || parsed.line_items || [];
		if (items.length) {
			items.forEach(function (row) { row._autofill = true; epcCsAddLineRow(row); });
		} else {
			epcCsAddLineRow({});
		}
		var panel = document.getElementById('epc_cs_autofill_panel');
		if (panel) {
			panel.classList.remove('is-empty');
			try { panel.scrollIntoView({ behavior: 'smooth', block: 'start' }); } catch (e) { /* ignore */ }
		}
	}

	function epcCsInitMultiLineAdd(btnId, containerId, inputClass, placeholder) {
		var btn = document.getElementById(btnId);
		if (!btn) return;
		btn.addEventListener('click', function () {
			var container = document.getElementById(containerId);
			if (!container) return;
			var row = document.createElement('div');
			row.className = 'epc-cs-multi-row';
			row.innerHTML = '<input type="text" name="' + (containerId === 'epc_cs_box45_lines' ? 'box_45_lines[]' : 'box_54_lines[]') + '" class="form-control input-sm ' + inputClass + '" placeholder="' + epcCsEsc(placeholder || '') + '">' +
				'<button type="button" class="btn btn-link btn-xs text-danger epc-cs-rm-multi" title="Remove"><i class="fa fa-times"></i></button>';
			container.appendChild(row);
			row.querySelector('.epc-cs-rm-multi').addEventListener('click', function () {
				if (container.querySelectorAll('.epc-cs-multi-row').length <= 1) return;
				row.remove();
			});
		});
	}

	document.querySelectorAll('.epc-cs-rm-multi').forEach(function (btn) {
		btn.addEventListener('click', function () {
			var row = btn.closest('.epc-cs-multi-row');
			var container = row && row.parentNode;
			if (!row || !container || container.querySelectorAll('.epc-cs-multi-row').length <= 1) return;
			row.remove();
		});
	});

	epcCsInitMultiLineAdd('epc_cs_add_box45', 'epc_cs_box45_lines', 'epc-cs-box45-input', '[FOB] FRT: INS:');
	epcCsInitMultiLineAdd('epc_cs_add_box54', 'epc_cs_box54_lines', 'epc-cs-box54-input', 'DEPO amount [ref] account');

	var pdfBtn = document.getElementById('epc_cs_pdf_upload_btn');
	var pdfErrorEl = document.getElementById('epc_cs_pdf_error');
	var pdfErrorMsg = document.getElementById('epc_cs_pdf_error_msg');
	var pdfServerDiag = document.getElementById('epc_cs_pdf_server_diag');
	var pdfManualBtn = document.getElementById('epc_cs_pdf_manual_btn');

	function epcCsPdfOk(j) { return !!(j && (j.status || j.ok)); }

	function epcCsHidePdfError() {
		if (pdfErrorEl) pdfErrorEl.style.display = 'none';
		if (pdfServerDiag) pdfServerDiag.style.display = 'none';
	}

	function epcCsShowPdfError(msg, j) {
		if (pdfErrorEl) pdfErrorEl.style.display = 'block';
		if (pdfErrorMsg) pdfErrorMsg.textContent = msg || 'Parse failed';
		if (pdfServerDiag) {
			var showDiag = j && (j.pdftotext_available === false || (j.message && j.message.indexOf('pdftotext') >= 0));
			if (showDiag) {
				pdfServerDiag.style.display = 'block';
				var diagUrl = (j && j.pdftotext_diag_url) ? j.pdftotext_diag_url : '';
				pdfServerDiag.innerHTML = '<strong>Server:</strong> pdftotext (poppler-utils) missing on VPS.'
					+ (diagUrl ? ' <a href="' + epcCsEsc(diagUrl) + '" target="_blank" rel="noopener">Run server PDF diagnostic</a>' : '');
			} else {
				pdfServerDiag.style.display = 'none';
			}
		}
	}

	if (pdfManualBtn) {
		pdfManualBtn.addEventListener('click', function () { epcCsHidePdfError(); showMsg(true, 'Fill the declaration fields below manually.'); });
	}

	if (pdfBtn) {
		pdfBtn.addEventListener('click', function () {
			var fileEl = document.getElementById('epc_cs_pdf_file');
			var statusEl = document.getElementById('epc_cs_pdf_status');
			epcCsHidePdfError();
			if (!fileEl || !fileEl.files || !fileEl.files[0]) {
				showMsg(false, 'Choose a PDF file first');
				return;
			}
			var fd = new FormData();
			fd.append('action', 'cs_import_declaration_pdf');
			fd.append('declaration_pdf', fileEl.files[0]);
			fd.append('declaration_type_hint', (document.getElementById('epc_cs_pdf_type_hint') || {}).value || '');
			var editIdEl = document.querySelector('input[name="id"]');
			if (editIdEl && editIdEl.value) fd.append('exclude_id', editIdEl.value);
			fd.append('csrf_guard_key', csrf);
			if (statusEl) { statusEl.style.display = 'block'; statusEl.textContent = 'Parsing PDF…'; }
			pdfBtn.disabled = true;
			epcCsFetchJson(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (j) {
					pdfBtn.disabled = false;
					if (!epcCsPdfOk(j)) {
						if (statusEl) statusEl.textContent = j.message || 'Parse failed';
						epcCsShowPdfError(j.message || 'Parse failed', j);
						showMsg(false, j.message || 'Parse failed');
						return;
					}
					epcCsApplyPdfData(j);
					var tokEl = document.getElementById('epc_cs_pdf_token');
					var nameEl = document.getElementById('epc_cs_pdf_file_name');
					if (tokEl && j.pdf_token) tokEl.value = j.pdf_token;
					if (nameEl && j.pdf_file_name) nameEl.value = j.pdf_file_name;
					if (j.pdf_preview_url) epcCsShowPdfViewer(j.pdf_preview_url, j.pdf_file_name || 'Declaration PDF');
					var autoPanel = document.getElementById('epc_cs_autofill_panel');
					if (autoPanel) autoPanel.classList.remove('is-empty');
					var mapped = j.boxes_mapped || (j.parsed && j.parsed.boxes_mapped) || 0;
					var lines = j.line_items_count || 0;
					var msg = 'Mapped ' + mapped + ' box fields, ' + lines + ' HS line(s). Review green highlights.';
					if (j.parse_warning) {
						msg = j.parse_warning + ' ' + msg;
						if (j.pdftotext_available === false && pdfServerDiag) {
							pdfServerDiag.style.display = 'block';
							var diagUrl = j.pdftotext_diag_url || '';
							pdfServerDiag.innerHTML = '<strong>Server:</strong> pdftotext missing — partial import only.'
								+ (diagUrl ? ' <a href="' + epcCsEsc(diagUrl) + '" target="_blank" rel="noopener">Server PDF diagnostic</a>' : '');
							if (pdfErrorEl) { pdfErrorEl.style.display = 'block'; if (pdfErrorMsg) pdfErrorMsg.textContent = j.parse_warning; }
						}
					}
					if (statusEl) statusEl.textContent = msg;
					showMsg(true, msg);
				})
				.catch(function (err) {
					pdfBtn.disabled = false;
					var errMsg = (err && err.message) ? err.message : 'PDF upload request failed — check your connection and try again.';
					if (statusEl) statusEl.textContent = 'Request failed';
					epcCsShowPdfError(errMsg, null);
					showMsg(false, errMsg);
				});
		});
	}

	function epcCsInitLineItems() {
		var addBtn = document.getElementById('epc_cs_add_line_item');
		if (!document.getElementById('epc_cs_line_items_body')) return;
		if (lineItemsInitial && lineItemsInitial.length) {
			lineItemsInitial.forEach(function (row) { epcCsAddLineRow(row); });
		} else {
			epcCsAddLineRow({});
		}
		if (addBtn) addBtn.addEventListener('click', function () { epcCsAddLineRow({}); });
	}
	epcCsInitLineItems();

	function epcCsFollowRedirect(j) {
		var redirect = (j && j.redirect) ? j.redirect : ((j && j.data && j.data.redirect) ? j.data.redirect : '');
		if (redirect) {
			window.location.href = redirect;
		}
	}

	function epcCsBindDeleteButtons() {
		document.querySelectorAll('.epc-cs-delete-btn').forEach(function (btn) {
			if (btn.getAttribute('data-bound') === '1') return;
			btn.setAttribute('data-bound', '1');
			btn.addEventListener('click', function () {
				var id = btn.getAttribute('data-id');
				if (!id || !confirm('Delete declaration #' + id + '? This cannot be undone.')) return;
				var fd = new FormData();
				fd.append('action', 'cs_delete_declaration');
				fd.append('id', id);
				fd.append('from', csFrom || '');
				fd.append('to', csTo || '');
				fd.append('category', btn.getAttribute('data-category') || csDefaultCategory || '');
				fd.append('csrf_guard_key', csrf);
				epcCsFetchJson(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
					.then(function (j) {
						showMsg(!!(j.status || j.ok), j.message || ((j.status || j.ok) ? 'Deleted' : 'Error'));
						if (j.status || j.ok) {
							epcCsFollowRedirect(j);
							if (!j.redirect && !(j.data && j.data.redirect)) {
								window.location.href = csReportsUrl;
							}
						}
					})
					.catch(function (err) {
						showMsg(false, (err && err.message) ? err.message : 'Delete request failed');
					});
			});
		});
	}
	epcCsBindDeleteButtons();

	if (form) {
		form.addEventListener('submit', function (ev) {
			ev.preventDefault();
			var jsonEl = document.getElementById('epc_cs_line_items_json');
			if (jsonEl) {
				jsonEl.value = JSON.stringify(epcCsCollectLineItems());
			}
			var fd = new FormData(form);
			epcCsFetchJson(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (j) {
					showMsg(!!(j.status || j.ok), j.message || ((j.status || j.ok) ? 'Saved' : 'Error'));
					if (j.status || j.ok) {
						epcCsFollowRedirect(j);
					}
				})
				.catch(function (err) {
					showMsg(false, (err && err.message) ? err.message : 'Request failed');
				});
		});
	}

	if (submitBtn) {
		submitBtn.addEventListener('click', function () {
			var id = submitBtn.getAttribute('data-id');
			var fd = new FormData();
			fd.append('action', 'cs_submit_declaration');
			fd.append('id', id);
			fd.append('csrf_guard_key', csrf);
			epcCsFetchJson(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (j) {
					showMsg(!!(j.status || j.ok), j.message || 'Submitted');
					if (j.status || j.ok) { window.location.reload(); }
				})
				.catch(function (err) {
					showMsg(false, (err && err.message) ? err.message : 'Submit failed');
				});
		});
	}
})();
