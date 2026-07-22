/**
 * Garage Manager portal — tabs, status, check-in, appointments.
 */
(function () {
	'use strict';
	var root = document.getElementById('epc-gms-root');
	if (!root) return;

	function ajaxUrl() { return root.getAttribute('data-ajax') || ''; }
	function csrf() { return root.getAttribute('data-csrf') || ''; }

	function msg(ok, text) {
		var el = document.getElementById('epc-gms-msg');
		if (!el) return;
		el.className = 'alert alert-' + (ok ? 'success' : 'danger') + ' epc-gms-msg';
		el.textContent = text || '';
		el.style.display = text ? 'block' : 'none';
	}

	function post(action, extra) {
		var fd = new FormData();
		fd.append('action', action);
		fd.append('csrf_guard_key', csrf());
		if (extra) {
			Object.keys(extra).forEach(function (k) {
				if (extra[k] !== undefined && extra[k] !== null) fd.append(k, extra[k]);
			});
		}
		return fetch(ajaxUrl(), {
			method: 'POST',
			body: fd,
			credentials: 'same-origin',
			headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
		}).then(function (r) {
			return r.text().then(function (t) {
				var j = null;
				try { j = t ? JSON.parse(t) : null; } catch (e) { j = null; }
				if (!j) throw new Error('Bad response');
				return j;
			});
		});
	}

	document.querySelectorAll('[data-gms-tab]').forEach(function (btn) {
		btn.addEventListener('click', function () {
			var tab = btn.getAttribute('data-gms-tab');
			document.querySelectorAll('[data-gms-tab]').forEach(function (b) {
				b.classList.toggle('is-on', b === btn);
			});
			document.querySelectorAll('[data-gms-panel]').forEach(function (p) {
				p.classList.toggle('is-on', p.getAttribute('data-gms-panel') === tab);
			});
		});
	});

	document.querySelectorAll('[data-job-status]').forEach(function (sel) {
		sel.addEventListener('change', function () {
			var id = sel.getAttribute('data-job-status');
			post('set_status', { job_id: id, status: sel.value }).then(function (j) {
				msg(!!j.status, j.message || (j.status ? 'Status updated' : 'Failed'));
				if (j.status) setTimeout(function () { location.reload(); }, 600);
			}).catch(function (e) { msg(false, e.message); });
		});
	});

	document.querySelectorAll('[data-convert-appt]').forEach(function (btn) {
		btn.addEventListener('click', function () {
			btn.disabled = true;
			post('convert_appointment', { appointment_id: btn.getAttribute('data-convert-appt') }).then(function (j) {
				msg(!!j.status, j.message || '');
				if (j.status) setTimeout(function () { location.reload(); }, 700);
				else btn.disabled = false;
			}).catch(function (e) { msg(false, e.message); btn.disabled = false; });
		});
	});

	var checkin = document.getElementById('epc-gms-checkin-form');
	if (checkin) {
		checkin.addEventListener('submit', function (e) {
			e.preventDefault();
			var fd = new FormData(checkin);
			var data = {};
			fd.forEach(function (v, k) { data[k] = v; });
			var labourSel = checkin.querySelector('[name="labour_code"]');
			if (labourSel && labourSel.value) {
				var opt = labourSel.options[labourSel.selectedIndex];
				data.labour_desc = opt.getAttribute('data-name') || opt.textContent;
				data.labour_hours = opt.getAttribute('data-hours') || '1';
				data.labour_rate = opt.getAttribute('data-rate') || '150';
			}
			post('create_job', data).then(function (j) {
				msg(!!j.status, j.message || '');
				if (j.status) setTimeout(function () { location.reload(); }, 700);
			}).catch(function (err) { msg(false, err.message); });
		});
	}

	var apptForm = document.getElementById('epc-gms-appt-form');
	if (apptForm) {
		apptForm.addEventListener('submit', function (e) {
			e.preventDefault();
			var fd = new FormData(apptForm);
			var data = {};
			fd.forEach(function (v, k) { data[k] = v; });
			if (data.time_slot_local) {
				data.time_slot = Math.floor(new Date(data.time_slot_local).getTime() / 1000);
			}
			post('create_appointment', data).then(function (j) {
				msg(!!j.status, j.message || '');
				if (j.status) setTimeout(function () { location.reload(); }, 700);
			}).catch(function (err) { msg(false, err.message); });
		});
	}

	var refresh = document.getElementById('epc-gms-appt-refresh');
	if (refresh) {
		refresh.addEventListener('click', function () { location.reload(); });
	}
})();
