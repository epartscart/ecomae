/**
 * Communications + Notification settings CP helpers.
 * Config: window.EPC_COMMS_NOTIFY = { page, ajaxUrl, reloadUrl, csrf, messages }
 */
(function () {
	'use strict';

	function cfg() {
		return window.EPC_COMMS_NOTIFY || {};
	}

	function msg(key, fallback) {
		var m = cfg().messages || {};
		return m[key] || fallback || key;
	}

	function postTest(type, contact) {
		var c = cfg();
		if (!c.ajaxUrl) {
			alert(msg('ajaxMissing', 'Test endpoint is not configured.'));
			return;
		}
		if (!window.jQuery) {
			alert(msg('jqueryMissing', 'jQuery is required for test send.'));
			return;
		}
		jQuery.ajax({
			type: 'POST',
			async: true,
			url: c.ajaxUrl,
			dataType: 'text',
			data: {
				contact: contact,
				type: type,
				csrf_guard_key: c.csrf || ''
			},
			success: function (answer) {
				var answerOb;
				try {
					answerOb = JSON.parse(answer);
				} catch (e) {
					alert(msg('badResponse', 'Unexpected server response.'));
					if (c.reloadUrl) {
						location = c.reloadUrl;
					}
					return;
				}
				if (typeof answerOb.status === 'undefined') {
					alert(msg('badResponse', 'Unexpected server response.'));
				} else if (answerOb.status === true) {
					alert(type === 'email' ? msg('emailOk', 'Test e-mail sent.') : msg('smsOk', 'Test SMS sent.'));
				} else {
					alert(answerOb.message || msg('sendFailed', 'Send failed.'));
				}
				if (c.reloadUrl) {
					location = c.reloadUrl;
				}
			},
			error: function () {
				alert(msg('networkError', 'Network error while sending test.'));
			}
		});
	}

	window.epcCnTestEmail = function () {
		var el = document.getElementById('email_for_test');
		var value = el ? String(el.value || '').trim() : '';
		if (!value) {
			alert(msg('emailRequired', 'Enter an e-mail address.'));
			return;
		}
		postTest('email', value);
	};

	window.epcCnTestSms = function () {
		var el = document.getElementById('phone_for_test');
		var value = el ? String(el.value || '').trim() : '';
		if (!value) {
			alert(msg('phoneRequired', 'Enter a phone number.'));
			return;
		}
		postTest('phone', value);
	};

	function applyNotifyFilter() {
		var root = document.getElementById('epc-cn-notify-root');
		if (!root) {
			return;
		}
		var qEl = document.getElementById('epc-cn-notify-q');
		var q = qEl ? String(qEl.value || '').toLowerCase().trim() : '';
		var filter = root.getAttribute('data-filter') || 'all';
		var rows = root.querySelectorAll('tbody tr[data-notify-row]');
		var shown = 0;
		for (var i = 0; i < rows.length; i++) {
			var row = rows[i];
			var hay = String(row.getAttribute('data-search') || '');
			var emailOn = row.getAttribute('data-email-on') === '1';
			var smsOn = row.getAttribute('data-sms-on') === '1';
			var matchQ = !q || hay.indexOf(q) !== -1;
			var matchF = true;
			if (filter === 'email') {
				matchF = emailOn;
			} else if (filter === 'sms') {
				matchF = smsOn;
			} else if (filter === 'off') {
				matchF = !emailOn && !smsOn;
			}
			var show = matchQ && matchF;
			row.style.display = show ? '' : 'none';
			if (show) {
				shown++;
			}
		}
		var countEl = document.getElementById('epc-cn-notify-count');
		if (countEl) {
			countEl.textContent = shown + ' / ' + rows.length;
		}
	}

	function initNotifyFilters() {
		var root = document.getElementById('epc-cn-notify-root');
		if (!root) {
			return;
		}
		var qEl = document.getElementById('epc-cn-notify-q');
		if (qEl) {
			qEl.addEventListener('input', applyNotifyFilter);
		}
		var chips = root.querySelectorAll('[data-filter-chip]');
		for (var i = 0; i < chips.length; i++) {
			chips[i].addEventListener('click', function (ev) {
				var chip = ev.currentTarget;
				var value = chip.getAttribute('data-filter-chip') || 'all';
				root.setAttribute('data-filter', value);
				for (var j = 0; j < chips.length; j++) {
					chips[j].classList.toggle('is-active', chips[j] === chip);
				}
				applyNotifyFilter();
			});
		}
		applyNotifyFilter();
	}

	function ready(fn) {
		if (document.readyState === 'loading') {
			document.addEventListener('DOMContentLoaded', fn);
		} else {
			fn();
		}
	}

	ready(function () {
		var page = cfg().page || '';
		if (page === 'notifications' || document.getElementById('epc-cn-notify-root')) {
			initNotifyFilters();
		}
	});
})();
