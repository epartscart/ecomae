/* Enterprise CRM board behaviour — twin of the inline script in crm_main.php.
 * Expects window.EPC_CRM = { ajaxUrl, csrf, actionPrefix, currency, openLeadId }. */
(function () {
	var cfg = window.EPC_CRM || {};
	var ajaxUrl = cfg.ajaxUrl || '/cp/crm/action';
	var csrf = cfg.csrf || '';
	var actionPrefix = cfg.actionPrefix || '';
	var cur = cfg.currency || 'AED';
	var msg = document.getElementById('epc_crm_msg');

	function act(name) { return actionPrefix + name; }

	function showMsg(ok, text) {
		if (!msg) return;
		msg.style.display = 'block';
		msg.className = 'alert epc-crm-msg ' + (ok ? 'alert-success' : 'alert-danger');
		msg.textContent = text;
	}

	function post(action, data, cb) {
		var fd = new FormData();
		fd.append('action', action);
		fd.append('csrf_guard_key', csrf);
		fd.append('confirmWrites', '1');
		for (var k in data) {
			if (data.hasOwnProperty(k)) fd.append(k, data[k]);
		}
		fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin', headers: { 'Accept': 'application/json', 'X-Requested-With': 'fetch' } })
			.then(function (r) { return r.json(); })
			.then(function (res) {
				if (cb) cb(res);
				else if (res.status) { showMsg(true, res.message); setTimeout(function () { location.reload(); }, 600); }
				else showMsg(false, res.message || 'Error');
			})
			.catch(function () { showMsg(false, 'Request failed'); });
	}

	function bindCrmForm(id, action) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function (e) {
			e.preventDefault();
			var fd = new FormData(f);
			var data = {};
			fd.forEach(function (v, k) { if (k !== 'csrf_guard_key' && k !== 'confirmWrites' && k !== 'returnUrl' && k !== 'action') data[k] = v; });
			post(action, data);
		});
	}
	bindCrmForm('epc_crm_lead_form', act('save_lead'));
	bindCrmForm('epc_crm_opp_form', act('save_opportunity'));
	bindCrmForm('epc_crm_act_form', act('save_activity'));
	bindCrmForm('epc_crm_quote_form', act('save_quote'));
	bindCrmForm('epc_crm_ticket_form', act('save_ticket'));
	bindCrmForm('epc_crm_project_form', act('save_project'));
	bindCrmForm('epc_crm_contract_form', act('save_contract'));
	bindCrmForm('epc_crm_expense_form', act('save_expense'));

	document.querySelectorAll('.epc-crm-convert').forEach(function (btn) {
		btn.addEventListener('click', function () {
			post(act('convert_lead'), { lead_id: btn.getAttribute('data-id') });
		});
	});
	document.querySelectorAll('form.epc-crm-convert-form').forEach(function (f) {
		f.addEventListener('submit', function (e) {
			e.preventDefault();
			var fd = new FormData(f);
			post(act('convert_lead'), { lead_id: fd.get('lead_id') });
		});
	});

	document.querySelectorAll('.epc-crm-stage-select').forEach(function (sel) {
		sel.addEventListener('change', function () {
			var card = sel.closest('.epc-crm-card');
			var id = card ? card.getAttribute('data-id') : 0;
			post(act('update_stage'), { id: id, stage: sel.value }, function (res) {
				if (res.status) location.reload();
				else showMsg(false, res.message);
			});
		});
	});

	// Pipeline drag-and-drop: drag a card into another column to change its stage.
	document.querySelectorAll('.epc-crm-card[draggable="true"]').forEach(function (card) {
		card.addEventListener('dragstart', function (e) {
			e.dataTransfer.setData('text/plain', card.getAttribute('data-id'));
			e.dataTransfer.effectAllowed = 'move';
			card.classList.add('epc-crm-dragging');
		});
		card.addEventListener('dragend', function () { card.classList.remove('epc-crm-dragging'); });
	});
	document.querySelectorAll('.epc-crm-pipeline-drop').forEach(function (zone) {
		zone.addEventListener('dragover', function (e) { e.preventDefault(); zone.classList.add('epc-crm-drop-over'); });
		zone.addEventListener('dragleave', function () { zone.classList.remove('epc-crm-drop-over'); });
		zone.addEventListener('drop', function (e) {
			e.preventDefault();
			zone.classList.remove('epc-crm-drop-over');
			var id = e.dataTransfer.getData('text/plain');
			var stage = zone.getAttribute('data-stage');
			if (!id || !stage) return;
			post(act('update_stage'), { id: id, stage: stage }, function (res) {
				if (res.status) location.reload();
				else showMsg(false, res.message);
			});
		});
	});

	// Timeline modal (activities + quotes + linked order history).
	var tlModal = document.getElementById('epc_crm_timeline_modal');
	var tlBody = document.getElementById('epc_crm_timeline_body');
	var tlTitle = document.getElementById('epc_crm_timeline_title');
	function closeTimeline() {
		if (!tlModal) return;
		tlModal.classList.remove('is-open');
		setTimeout(function () { tlModal.style.display = 'none'; }, 200);
	}
	if (tlModal) {
		tlModal.querySelector('.epc-crm-timeline-backdrop').addEventListener('click', closeTimeline);
		tlModal.querySelector('.epc-crm-timeline-close').addEventListener('click', closeTimeline);
	}
	function escapeHtml(s) {
		return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
			return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
		});
	}
	function ymd(unix) { return unix ? new Date(unix * 1000).toISOString().slice(0, 10) : ''; }
	function renderTimeline(j) {
		if (!j || !j.status) { tlBody.innerHTML = '<p class="text-danger">' + escapeHtml(j && j.message || 'Failed to load') + '</p>'; return; }
		var html = '';
		var e = j.entity || {};
		html += '<div class="epc-crm-tl-item" style="border-left-color:#428bca;">';
		html += '<strong>' + escapeHtml(e.caption || e.title || e.company || '') + '</strong>';
		html += '</div>';

		if (j.has_commerce) {
			html += '<h5><i class="fa fa-shopping-cart"></i> Order history</h5>';
			if (j.linked_user_id > 0 && j.orders && j.orders.length) {
				j.orders.forEach(function (o) {
					html += '<div class="epc-crm-tl-item">';
					html += '<strong>Order #' + o.id + '</strong> · ' + escapeHtml(o.statusName || o.status_name || '—');
					html += '<div class="tl-meta">' + ymd(o.time) + ' · ' + Number(o.priceTotalWtVat || o.price_total_wt_vat || 0).toFixed(2) + ' ' + cur + (o.paid ? ' · Paid' : '') + '</div>';
					html += '</div>';
				});
			} else if (j.linked_user_id > 0) {
				html += '<p class="text-muted">No orders yet for this linked customer.</p>';
			} else {
				html += '<p class="text-muted">No storefront customer linked yet — set an opportunity\'s linked customer, or match by lead email.</p>';
			}
		}

		html += '<h5><i class="fa fa-file-text-o"></i> Quotes</h5>';
		if (j.quotes && j.quotes.length) {
			j.quotes.forEach(function (q) {
				html += '<div class="epc-crm-tl-item"><strong>' + escapeHtml(q.quoteNumber || q.quote_number) + '</strong> · ' + escapeHtml(q.status) + '<div class="tl-meta">' + Number(q.subtotal || 0).toFixed(2) + ' ' + cur + '</div></div>';
			});
		} else {
			html += '<p class="text-muted">No quotes yet.</p>';
		}

		html += '<h5><i class="fa fa-calendar"></i> Activities</h5>';
		if (j.activities && j.activities.length) {
			j.activities.forEach(function (a) {
				var due = a.dueDate || a.due_date;
				html += '<div class="epc-crm-tl-item"><strong>' + escapeHtml(a.activityType || a.activity_type) + '</strong>'
					+ (due ? ' · ' + ymd(due) : '')
					+ (a.done ? ' <span class="label label-success">Done</span>' : ' <span class="label label-warning">Open</span>')
					+ '<div class="tl-meta">' + escapeHtml(a.notes || '') + '</div></div>';
			});
		} else {
			html += '<p class="text-muted">No activities logged yet.</p>';
		}
		tlBody.innerHTML = html;
	}
	document.querySelectorAll('.epc-crm-timeline').forEach(function (btn) {
		btn.addEventListener('click', function () {
			var type = btn.getAttribute('data-entity-type');
			var id = btn.getAttribute('data-entity-id');
			tlTitle.textContent = 'Timeline — ' + (btn.getAttribute('data-label') || '');
			tlBody.innerHTML = '<p class="text-muted">Loading…</p>';
			tlModal.style.display = 'block';
			requestAnimationFrame(function () { tlModal.classList.add('is-open'); });
			post(act('get_timeline'), { entity_type: type, entity_id: id }, renderTimeline);
		});
	});

	document.querySelectorAll('.epc-crm-won-hint').forEach(function (btn) {
		btn.addEventListener('click', function () {
			post(act('won_hint'), { opportunity_id: btn.getAttribute('data-id') }, function (res) {
				showMsg(res.status, res.message || res.hint || 'OK');
			});
		});
	});

	document.querySelectorAll('.epc-crm-toggle-act').forEach(function (btn) {
		btn.addEventListener('click', function () {
			post(act('toggle_activity'), { id: btn.getAttribute('data-id'), done: btn.getAttribute('data-done') });
		});
	});

	document.querySelectorAll('.epc-crm-quote-preview').forEach(function (btn) {
		btn.addEventListener('click', function () {
			post(act('quote_preview'), { quote_id: btn.getAttribute('data-id') }, function (j) {
				if (j && j.preview_url) window.open(j.preview_url, '_blank');
				else showMsg(false, (j && j.message) || 'Could not build preview');
			});
		});
	});
	document.querySelectorAll('.epc-crm-quote-email').forEach(function (btn) {
		btn.addEventListener('click', function () {
			var em = prompt('Send proposal to email (leave blank for customer default):', '');
			if (em === null) return;
			post(act('quote_email'), { quote_id: btn.getAttribute('data-id'), email: em });
		});
	});
	document.querySelectorAll('.epc-crm-accept-quote').forEach(function (btn) {
		btn.addEventListener('click', function () {
			if (!confirm('Accept quote and create shop order stub?')) return;
			post(act('accept_quote'), { quote_id: btn.getAttribute('data-id'), post_cash: '1' });
		});
	});
	document.querySelectorAll('.epc-crm-approve-expense').forEach(function (btn) {
		btn.addEventListener('click', function () {
			post(act('approve_expense'), { expense_id: btn.getAttribute('data-id'), post_cash: '1' });
		});
	});
	document.querySelectorAll('.epc-crm-quote-tax').forEach(function (btn) {
		btn.addEventListener('click', function () {
			post(act('quote_tax'), { quote_id: btn.getAttribute('data-id') }, function (j) {
				var tax = (j && j.tax) ? j.tax : null;
				if (!j || !j.status || !tax) { showMsg(false, (j && j.message) || 'Tax calc failed'); return; }
				showMsg(true, 'Quote tax: ' + Number(tax.subtotal || 0).toFixed(2) + ' + ' + Number(tax.tax_amount || 0).toFixed(2) + ' ' + (tax.tax_label || 'Tax') + ' = ' + Number(tax.total || 0).toFixed(2) + ' ' + (tax.currency || cur));
			});
		});
	});

	// Lead edit / delete
	var leadForm = document.getElementById('epc_crm_lead_form');
	var leadReset = document.getElementById('epc_crm_lead_reset');
	var leadSubmit = document.getElementById('epc_crm_lead_submit');
	function resetLeadForm() {
		if (!leadForm) return;
		leadForm.reset();
		var idEl = document.getElementById('epc_crm_lead_id');
		if (idEl) idEl.value = '0';
		if (leadSubmit) leadSubmit.textContent = 'Add lead';
		if (leadReset) leadReset.style.display = 'none';
	}
	if (leadReset) leadReset.addEventListener('click', resetLeadForm);
	function editLead(id) {
		post(act('get_lead'), { id: id }, function (j) {
			var L = (j && j.lead) ? j.lead : null;
			if (!j || !j.status || !L) { showMsg(false, (j && j.message) || 'Lead not found'); return; }
			document.getElementById('epc_crm_lead_id').value = L.id || 0;
			document.getElementById('epc_crm_lead_company').value = L.company || '';
			document.getElementById('epc_crm_lead_contact').value = L.contact_name || '';
			document.getElementById('epc_crm_lead_email').value = L.email || '';
			document.getElementById('epc_crm_lead_phone').value = L.phone || '';
			document.getElementById('epc_crm_lead_source').value = L.source || 'web';
			document.getElementById('epc_crm_lead_status').value = L.status || 'new';
			document.getElementById('epc_crm_lead_value').value = L.expected_value || '';
			document.getElementById('epc_crm_lead_notes').value = L.notes || '';
			if (leadSubmit) leadSubmit.textContent = 'Save lead #' + L.id;
			if (leadReset) leadReset.style.display = 'inline-block';
			window.scrollTo({ top: 0, behavior: 'smooth' });
			showMsg(true, 'Editing lead #' + L.id + ' (score ' + (L.lead_score || 0) + ' · ' + (L.lead_band || '') + ')');
		});
	}
	document.querySelectorAll('.epc-crm-edit-lead').forEach(function (btn) {
		btn.addEventListener('click', function () { editLead(btn.getAttribute('data-id')); });
	});
	document.querySelectorAll('.epc-crm-delete-lead').forEach(function (btn) {
		btn.addEventListener('click', function (e) {
			e.preventDefault();
			if (!confirm('Archive this lead?')) return;
			post(act('delete_lead'), { id: btn.getAttribute('data-id') });
		});
	});
	if (leadForm && cfg.openLeadId > 0) { editLead(cfg.openLeadId); }

	// Detail drawer (tickets / projects)
	var drawer = document.getElementById('epc_crm_drawer');
	var drawerBody = document.getElementById('epc_crm_drawer_body');
	var drawerTitle = document.getElementById('epc_crm_drawer_title');
	function openDrawer(title) {
		if (!drawer) return;
		drawerTitle.textContent = title || 'Details';
		drawerBody.innerHTML = '<p class="text-muted">Loading…</p>';
		drawer.classList.add('open');
	}
	function closeDrawer() { if (drawer) drawer.classList.remove('open'); }
	if (drawer) {
		drawer.querySelector('.epc-crm-drawer-backdrop').addEventListener('click', closeDrawer);
		drawer.querySelector('.epc-crm-drawer-close').addEventListener('click', closeDrawer);
	}

	function openTicket(id) {
		openDrawer('Ticket #' + id);
		post(act('get_ticket'), { id: id }, function (j) {
			var t = (j && j.ticket) ? j.ticket : null;
			if (!j || !j.status || !t) { drawerBody.innerHTML = '<p class="text-danger">Failed to load ticket</p>'; return; }
			var html = '<p><strong>' + escapeHtml(t.subject || '') + '</strong><br>';
			html += '<span class="label label-warning">' + escapeHtml(t.status || '') + '</span> ';
			html += '<span class="label label-info">' + escapeHtml(t.priority || '') + '</span></p>';
			html += '<form id="epc_crm_ticket_update" class="form-inline" style="margin-bottom:12px;">';
			html += '<select name="status" class="form-control input-sm">';
			['open', 'pending', 'resolved', 'closed'].forEach(function (s) {
				html += '<option value="' + s + '"' + (t.status === s ? ' selected' : '') + '>' + s + '</option>';
			});
			html += '</select> ';
			html += '<select name="priority" class="form-control input-sm">';
			['low', 'normal', 'high', 'urgent'].forEach(function (s) {
				html += '<option value="' + s + '"' + (t.priority === s ? ' selected' : '') + '>' + s + '</option>';
			});
			html += '</select> ';
			html += '<input type="text" name="message" class="form-control input-sm" placeholder="Reply / note" style="max-width:220px;"> ';
			html += '<button type="submit" class="btn btn-sm btn-primary">Update</button></form>';
			html += '<div class="epc-crm-ticket-thread">';
			(t.messages || []).forEach(function (m) {
				html += '<div class="msg"><div class="meta">' + (m.time_created ? new Date(m.time_created * 1000).toISOString().slice(0, 16).replace('T', ' ') : '') + (Number(m.is_staff) ? ' · Staff' : ' · Customer') + '</div>' + escapeHtml(m.body || '') + '</div>';
			});
			if (!(t.messages || []).length) html += '<p class="text-muted">No messages yet.</p>';
			html += '</div>';
			drawerBody.innerHTML = html;
			var f = document.getElementById('epc_crm_ticket_update');
			if (f) {
				f.addEventListener('submit', function (e) {
					e.preventDefault();
					var fd = new FormData(f);
					post(act('update_ticket_status'), {
						id: id,
						status: fd.get('status'),
						priority: fd.get('priority'),
						message: fd.get('message') || ''
					});
				});
			}
		});
	}
	document.querySelectorAll('.epc-crm-open-ticket').forEach(function (btn) {
		btn.addEventListener('click', function () { openTicket(btn.getAttribute('data-id')); });
	});

	function openProject(id) {
		openDrawer('Project #' + id);
		post(act('get_project'), { id: id }, function (j) {
			var p = (j && j.project) ? j.project : null;
			if (!j || !j.status || !p) { drawerBody.innerHTML = '<p class="text-danger">Failed to load project</p>'; return; }
			var html = '<p><strong>' + escapeHtml(p.name || '') + '</strong><br>';
			html += 'Status: ' + escapeHtml(p.status || '') + ' · Progress: ' + Number(p.progress_pct || 0) + '%</p>';
			html += '<form id="epc_crm_task_form" class="form-inline" style="margin-bottom:12px;">';
			html += '<input type="text" name="title" class="form-control input-sm" placeholder="Task title" required> ';
			html += '<select name="status" class="form-control input-sm"><option value="todo">To do</option><option value="doing">Doing</option><option value="done">Done</option></select> ';
			html += '<input type="number" name="progress_pct" class="form-control input-sm" placeholder="%" value="0" min="0" max="100" style="width:70px;"> ';
			html += '<button type="submit" class="btn btn-sm btn-primary">Add task</button></form>';
			html += '<table class="table table-condensed"><thead><tr><th>Task</th><th>Status</th><th>%</th></tr></thead><tbody>';
			(p.tasks || []).forEach(function (t) {
				html += '<tr><td>' + escapeHtml(t.title || '') + '</td><td>' + escapeHtml(t.status || '') + '</td><td>' + Number(t.progress_pct || 0) + '%</td></tr>';
			});
			if (!(p.tasks || []).length) html += '<tr><td colspan="3" class="text-muted">No tasks yet.</td></tr>';
			html += '</tbody></table>';
			drawerBody.innerHTML = html;
			var f = document.getElementById('epc_crm_task_form');
			if (f) {
				f.addEventListener('submit', function (e) {
					e.preventDefault();
					var fd = new FormData(f);
					post(act('save_project_task'), {
						project_id: id,
						title: fd.get('title'),
						status: fd.get('status'),
						progress_pct: fd.get('progress_pct') || 0
					});
				});
			}
		});
	}
	document.querySelectorAll('.epc-crm-open-project').forEach(function (btn) {
		btn.addEventListener('click', function () { openProject(btn.getAttribute('data-id')); });
	});
	if (cfg.openTicketId > 0) { openTicket(cfg.openTicketId); }
	if (cfg.openProjectId > 0) { openProject(cfg.openProjectId); }

	function render360(j) {
		var box = document.getElementById('epc_crm_360_result');
		if (!box) return;
		var c = (j && j.customer360) ? j.customer360 : null;
		if (!j || !j.status || !c) {
			box.style.display = 'block';
			box.innerHTML = '<p class="text-danger">' + escapeHtml((j && j.message) || 'Failed') + '</p>';
			return;
		}
		var html = '<div class="epc-crm-360-grid">';
		html += '<div class="epc-crm-360-tile"><div class="lbl">Opportunities</div><div class="val">' + Number((c.opportunities && c.opportunities.count) || 0) + '</div><div class="text-muted" style="font-size:11px;">Open ' + Number((c.opportunities && c.opportunities.open_value) || 0).toFixed(0) + ' · Won ' + Number((c.opportunities && c.opportunities.won_value) || 0).toFixed(0) + '</div></div>';
		html += '<div class="epc-crm-360-tile"><div class="lbl">Quotes</div><div class="val">' + Number((c.quotes && c.quotes.count) || 0) + '</div><div class="text-muted" style="font-size:11px;">Accepted ' + Number((c.quotes && c.quotes.accepted) || 0) + ' · ' + Number((c.quotes && c.quotes.value) || 0).toFixed(0) + ' ' + cur + '</div></div>';
		html += '<div class="epc-crm-360-tile"><div class="lbl">Tickets</div><div class="val">' + Number((c.tickets && c.tickets.open) || 0) + '</div><div class="text-muted" style="font-size:11px;">Open of ' + Number((c.tickets && c.tickets.total) || 0) + '</div></div>';
		html += '<div class="epc-crm-360-tile"><div class="lbl">Customer ID</div><div class="val">#' + Number(c.user_id || 0) + '</div><div class="text-muted" style="font-size:11px;">CRM + commerce link</div></div>';
		html += '</div>';
		box.style.display = 'block';
		box.innerHTML = html;
	}
	var form360 = document.getElementById('epc_crm_360_form');
	if (form360) {
		form360.addEventListener('submit', function (e) {
			e.preventDefault();
			var uid = document.getElementById('epc_crm_360_user').value;
			post(act('customer_360'), { user_id: uid }, render360);
		});
	}
	document.querySelectorAll('.epc-crm-load-360').forEach(function (btn) {
		btn.addEventListener('click', function () {
			var uid = btn.getAttribute('data-user-id');
			var input = document.getElementById('epc_crm_360_user');
			if (input) input.value = uid;
			post(act('customer_360'), { user_id: uid }, render360);
			window.scrollTo({ top: 0, behavior: 'smooth' });
		});
	});

	// Opportunity edit (linked customer)
	document.querySelectorAll('.epc-crm-edit-opp').forEach(function (btn) {
		btn.addEventListener('click', function () {
			post(act('get_opportunity'), { id: btn.getAttribute('data-id') }, function (j) {
				var o = (j && j.opportunity) ? j.opportunity : null;
				if (!j || !j.status || !o) { showMsg(false, (j && j.message) || 'Not found'); return; }
				var linked = prompt('Linked shop customer user ID (0 = none):', String(o.linked_user_id || 0));
				if (linked === null) return;
				post(act('save_opportunity'), {
					id: o.id,
					title: o.title,
					lead_id: o.lead_id || 0,
					stage: o.stage,
					amount: o.amount,
					probability: o.probability,
					close_date: o.close_date ? ymd(o.close_date) : '',
					linked_user_id: linked,
					notes: o.notes || ''
				});
			});
		});
	});
})();
