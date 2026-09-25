/* Prices-send page behaviour — twin of the inline scripts in prices_send.php.
 * Expects window.EPC_PS = { ajaxUrl, pageUrl, csrf, labels, users, storages }. */
(function () {
	var cfg = window.EPC_PS || {};
	var ajaxUrl = cfg.ajaxUrl || '/cp/prices-send/action';
	var pageUrl = cfg.pageUrl || '/cp/prices-send-app';
	var csrf = cfg.csrf || '';
	var L = cfg.labels || {};
	var users_array = cfg.users || [];
	var elements_id_array = cfg.storages || [];

	function $(id) { return document.getElementById(id); }
	function status(html) { var s = $('create_prices_status'); if (s) s.innerHTML = html; }
	function esc(t) { return String(t == null ? '' : t).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
	function setCookie(name, obj) {
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = name + '=' + encodeURIComponent(JSON.stringify(obj)) + '; path=/; expires=' + date.toUTCString();
	}
	function getCookie(name) {
		var prefix = name + '=';
		var parts = String(document.cookie || '').split(';');
		for (var i = 0; i < parts.length; i++) {
			var part = parts[i].replace(/^\s+/, '');
			if (part.indexOf(prefix) === 0) return decodeURIComponent(part.substring(prefix.length));
		}
		return undefined;
	}

	function post(request_object, ok, fail) {
		var body = 'request_object=' + encodeURIComponent(JSON.stringify(request_object)) + '&csrf_guard_key=' + encodeURIComponent(csrf);
		return fetch(ajaxUrl, {
			method: 'POST',
			body: body,
			credentials: 'same-origin',
			headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'Accept': 'application/json', 'X-Requested-With': 'fetch' }
		}).then(function (r) { return r.json().then(function (j) { j.__http = r.status; return j; }); })
			.then(ok)
			.catch(function () { if (fail) fail(); });
	}

	/* Step 1 filter / sort (cookie-driven like PHP) */
	window.filterUsers = function () {
		setCookie('users_filter_send_prices', {
			user_id: $('user_id').value, group_id: $('group_id').value, email: $('email').value,
			cellphone: $('cellphone').value, surname: $('surname').value
		});
		location = pageUrl;
	};
	window.unsetFilterUsers = function () {
		setCookie('users_filter_send_prices', { user_id: '', group_id: -1, email: '', cellphone: '', surname: '' });
		location = pageUrl;
	};
	window.sortUsers = function (field) {
		var asc_desc = 'asc';
		var cur = getCookie('users_sort_send_prices');
		if (cur) {
			try { cur = JSON.parse(cur); if (cur.field == field) asc_desc = cur.asc_desc == 'asc' ? 'desc' : 'asc'; } catch (e) { }
		}
		setCookie('users_sort_send_prices', { field: field, asc_desc: asc_desc });
		location = pageUrl;
	};

	/* Users checkboxes */
	window.on_check_uncheck_all_users = function () {
		var state = $('check_uncheck_all_users').checked;
		users_array.forEach(function (id) { var el = $('checked_users_' + id); if (el) el.checked = state; });
	};
	window.on_one_check_changed_users = function () {
		for (var i = 0; i < users_array.length; i++) {
			var el = $('checked_users_' + users_array[i]);
			if (el && !el.checked) { $('check_uncheck_all_users').checked = false; return; }
		}
	};
	function get_users_list() {
		var out = [];
		users_array.forEach(function (id) { var el = $('checked_users_' + id); if (el && el.checked) out.push(id); });
		return out;
	}

	/* Storage checkboxes */
	window.on_check_uncheck_all = function () {
		var state = $('check_uncheck_all').checked;
		elements_id_array.forEach(function (id) { var el = $('checked_' + id); if (el) el.checked = state; });
	};
	window.on_one_check_changed = function () {
		for (var i = 0; i < elements_id_array.length; i++) {
			var el = $('checked_' + elements_id_array[i]);
			if (el && !el.checked) { $('check_uncheck_all').checked = false; return; }
		}
	};
	function getCheckedElements() {
		var out = [];
		elements_id_array.forEach(function (id) { var el = $('checked_' + id); if (el && el.checked) out.push(id); });
		return out;
	}

	/* Catalogue tree (native replacement for the webix tree) */
	var tree = $('container_A');
	function treeBoxes() { return tree ? tree.querySelectorAll('input.epc-ps-cat') : []; }
	window.catalogue_tree = {
		checkAll: function () { treeBoxes().forEach(function (b) { b.checked = true; }); },
		uncheckAll: function () { treeBoxes().forEach(function (b) { b.checked = false; }); },
		getChecked: function () { var out = []; treeBoxes().forEach(function (b) { if (b.checked) out.push(parseInt(b.value, 10)); }); return out; }
	};
	if (tree) {
		tree.addEventListener('click', function (e) {
			var t = e.target;
			if (t.classList.contains('epc-ps-tree-toggle')) {
				var li = t.closest('li');
				if (li) li.classList.toggle('epc-ps-collapsed');
				e.preventDefault();
			}
		});
		tree.addEventListener('change', function (e) {
			var t = e.target;
			if (t.classList.contains('epc-ps-cat')) {
				var li = t.closest('li');
				if (li) li.querySelectorAll('input.epc-ps-cat').forEach(function (b) { b.checked = t.checked; });
			}
		});
	}

	function collectFilters(request_object) {
		var profile = parseInt($('epc_ps_profile_group').value, 10) || 0;
		request_object.profile_group_ids = profile > 0 ? [profile] : [];
		request_object.filter_brand = ($('epc_ps_filter_brand').value || '').trim();
		request_object.filter_article = ($('epc_ps_filter_article').value || '').trim();
		if (profile > 0 && (!request_object.group_id_my_list_emails || request_object.group_id_my_list_emails == 0)) {
			request_object.group_id_my_list_emails = profile;
		}
	}
	function emailGroup(emails_list) {
		if (emails_list == '') return 0;
		var sel = $('group_id_my_list_emails');
		return sel.options.selectedIndex >= 0 ? sel.options[sel.options.selectedIndex].value : 0;
	}
	function selectedOffice() { var sel = $('offices'); return sel.options.selectedIndex >= 0 ? sel.options[sel.options.selectedIndex].value : 0; }

	window.epcPsLinkStorages = function () {
		var arr_storages = getCheckedElements();
		if (arr_storages.length == 0) { alert(L['3678'] || 'Select at least one storage'); return; }
		var profile = parseInt($('epc_ps_profile_group').value, 10) || 0;
		status('<div class="text-muted"><i class="fa fa-spinner fa-spin"></i> Linking storages…</div>');
		post({
			action: 'ensure_office_storage_links', offices: selectedOffice(), arr_storages: arr_storages,
			group_ids: profile > 0 ? [profile, 2, 4, 5, 6, 7] : [2, 4, 5, 6, 7]
		}, function (answer) {
			if (answer && answer.status) status('<div class="alert alert-success">' + esc(answer.message || 'Linked') + '. Reload to refresh Linked column.</div>');
			else status('<div class="alert alert-danger">' + esc(answer && answer.message ? answer.message : 'Link failed') + '</div>');
		}, function () { status('<div class="alert alert-danger">Link failed</div>'); });
	};

	window.create_prices = function () {
		var btn = $('send_prices_btn');
		btn.setAttribute('disabled', 'disabled');
		var users_list = get_users_list();
		var emails_list = $('my_list_emails').value;
		var group_id_my_list_emails = emailGroup(emails_list);
		var profile = parseInt($('epc_ps_profile_group').value, 10) || 0;
		if (users_list.length == 0 && emails_list == '' && profile <= 0) {
			alert('Select customers, enter emails, or choose a markup profile group.');
			return;
		}
		var arr_category = window.catalogue_tree.getChecked();
		var storages = 0;
		if (arr_category.length > 0) {
			var sel = $('storages');
			if (sel && sel.options.selectedIndex >= 0) storages = sel.options[sel.options.selectedIndex].value;
		}
		var arr_storages = getCheckedElements();
		if (arr_storages.length == 0) { alert(L['3678'] || 'Select at least one storage'); return false; }
		var request_object = {
			action: 'check_office_storages_map', offices: selectedOffice(), arr_storages: arr_storages, arr_category: arr_category,
			users_list: users_list, emails_list: emails_list, group_id_my_list_emails: group_id_my_list_emails, storages: storages
		};
		collectFilters(request_object);
		post(request_object, function (answer) {
			if (!answer || answer.status != true) {
				var msg = (L['3679'] || 'Storages are not linked to the shop') + ': ' + (answer && answer.message || '') + '. ' + (L['3680'] || 'Link them') + '.';
				if (answer && answer.can_link) msg += '\n\nClick “Link selected storages” then try Generate again.';
				alert(msg);
				return;
			}
			request_object.action = 'create_prices';
			status('<div class="text-muted"><i class="fa fa-spinner fa-spin"></i> Generating price profile…</div>');
			post(request_object, function (answer) {
				if (answer && answer.status == true) {
					var html = '<div class="alert alert-success">' + esc(answer.message || (L['3681'] || 'Price lists generated') + '.') + '</div>';
					if (answer.files && answer.files.length) {
						html += '<div class="epc-ps-file-list">';
						answer.files.forEach(function (f) {
							html += '<a class="btn btn-default btn-sm" href="' + esc(f.url) + '" download><i class="fa fa-download"></i> ' + esc(f.file) + ' (' + esc(f.rows) + ' rows, group ' + esc(f.group_id) + ')</a>';
						});
						html += '</div>';
					}
					status(html);
					btn.removeAttribute('disabled');
				} else {
					alert(answer && answer.message ? answer.message : (L['3682'] || 'Generation failed'));
					status('');
				}
			}, function () { alert('Generate failed'); status(''); });
		}, function () { alert('Generate failed'); });
	};

	window.send_prices = function () {
		var btn = $('send_prices_btn');
		btn.setAttribute('disabled', 'disabled');
		var users_list = get_users_list();
		var emails_list = $('my_list_emails').value;
		var group_id_my_list_emails = emailGroup(emails_list);
		if (users_list.length == 0 && emails_list == '') {
			alert(L['3675'] || 'Select customers or enter e-mails');
			btn.removeAttribute('disabled');
			return;
		}
		status('<div class="text-muted"><i class="fa fa-spinner fa-spin"></i> Sending…</div>');
		post({ action: 'send_prices', users_list: users_list, emails_list: emails_list, group_id_my_list_emails: group_id_my_list_emails }, function (answer) {
			if (answer && answer.status == true) {
				status('<div class="alert alert-success">' + esc(L['3676'] || 'Price lists sent') + (answer.sent ? ' (' + esc(answer.sent) + ' sent)' : '') + '</div>');
			} else {
				alert(answer && answer.message ? answer.message : (L['3677'] || 'Sending failed'));
				status('');
			}
			btn.removeAttribute('disabled');
		}, function () { alert('Send failed'); btn.removeAttribute('disabled'); });
	};

	/* Brand datalist */
	post({ action: 'list_brands', limit: 40 }, function (answer) {
		if (!answer || !answer.status || !answer.brands) return;
		var dl = $('epc_ps_brand_list');
		if (!dl) return;
		dl.innerHTML = '';
		answer.brands.forEach(function (b) {
			var opt = document.createElement('option');
			opt.value = b.brand;
			opt.label = b.brand + ' (' + b.count + ')';
			dl.appendChild(opt);
		});
	});
})();
