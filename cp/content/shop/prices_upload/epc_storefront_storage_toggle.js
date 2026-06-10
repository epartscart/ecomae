(function () {
	'use strict';

	var cfg = window.EPC_STOREFRONT_STORAGE_TOGGLE || {};
	var ajaxUrl = cfg.ajaxUrl || '';
	var csrfKey = cfg.csrfKey || '';

	if (!ajaxUrl || !csrfKey) {
		return;
	}

	function epcSsfRowSavedEnabled(tr) {
		return tr.getAttribute('data-saved-enabled') === '1';
	}

	function epcSsfRowDesiredEnabled(input) {
		return !!input.checked;
	}

	function epcSsfUpdateRow(tr, disabled) {
		var badge = tr.querySelector('.epc-ssf-badge');
		if (!badge) {
			return;
		}
		if (disabled) {
			badge.className = 'label epc-ssf-badge label-warning';
			badge.textContent = 'Temporarily disabled';
		} else {
			badge.className = 'label epc-ssf-badge label-success';
			badge.textContent = 'Active';
		}
	}

	function epcSsfSyncSaveBtn(tr) {
		var input = tr.querySelector('.epc-ssf-toggle-input');
		var btn = tr.querySelector('.epc-ssf-save-btn');
		if (!input || !btn) {
			return;
		}
		var dirty = epcSsfRowDesiredEnabled(input) !== epcSsfRowSavedEnabled(tr);
		btn.disabled = !dirty || btn.classList.contains('epc-ssf-saving');
		tr.classList.toggle('epc-ssf-row-dirty', dirty);
	}

	function epcSsfToggleOk(answer) {
		return !!(answer && (answer.ok === true || answer.status === true));
	}

	function epcSsfToggleMessage(answer) {
		if (!answer) {
			return 'Could not save toggle.';
		}
		if (answer.message) {
			return String(answer.message);
		}
		if (answer.error) {
			return String(answer.error);
		}
		return 'Could not save toggle.';
	}

	function epcSsfFlashSaved(btn) {
		var original = btn.getAttribute('data-label') || btn.innerHTML;
		btn.setAttribute('data-label', original);
		btn.innerHTML = '<i class="fa fa-check"></i> Saved';
		btn.classList.add('btn-success');
		btn.classList.remove('btn-primary');
		window.setTimeout(function () {
			btn.innerHTML = original;
			btn.classList.add('btn-primary');
			btn.classList.remove('btn-success');
		}, 1800);
	}

	function epcSsfSaveRow(tr) {
		var input = tr.querySelector('.epc-ssf-toggle-input');
		var btn = tr.querySelector('.epc-ssf-save-btn');
		if (!input || !btn || btn.disabled) {
			return;
		}

		var entityType = input.getAttribute('data-entity-type') || 'storage';
		var entityId = parseInt(input.getAttribute('data-entity-id') || '0', 10);
		var enableStorefront = input.checked;
		var prevChecked = epcSsfRowSavedEnabled(tr);

		btn.disabled = true;
		btn.classList.add('epc-ssf-saving');
		input.disabled = true;

		jQuery.ajax({
			type: 'POST',
			url: ajaxUrl,
			dataType: 'json',
			data: {
				action: 'toggle',
				entity_type: entityType,
				entity_id: entityId,
				storefront_enabled: enableStorefront ? 1 : 0,
				csrf_guard_key: csrfKey
			},
			success: function (answer) {
				btn.classList.remove('epc-ssf-saving');
				input.disabled = false;
				if (epcSsfToggleOk(answer)) {
					tr.setAttribute('data-saved-enabled', enableStorefront ? '1' : '0');
					epcSsfUpdateRow(tr, !enableStorefront);
					epcSsfSyncSaveBtn(tr);
					epcSsfFlashSaved(btn);
				} else {
					input.checked = prevChecked;
					epcSsfSyncSaveBtn(tr);
					alert(epcSsfToggleMessage(answer));
				}
			},
			error: function () {
				btn.classList.remove('epc-ssf-saving');
				input.disabled = false;
				input.checked = prevChecked;
				epcSsfSyncSaveBtn(tr);
				alert('Request failed — toggle not saved.');
			}
		});
	}

	jQuery(function () {
		jQuery('#epc_storefront_storage_table tbody tr').each(function () {
			epcSsfSyncSaveBtn(this);
		});
	});

	jQuery(document).on('change', '.epc-ssf-toggle-input', function () {
		epcSsfSyncSaveBtn(this.closest('tr'));
	});

	jQuery(document).on('click', '.epc-ssf-save-btn', function () {
		epcSsfSaveRow(this.closest('tr'));
	});
})();
