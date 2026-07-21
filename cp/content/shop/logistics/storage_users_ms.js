/**
 * Init storekeepers multi-select AFTER CP footer reloads jQuery
 * (mid-page plugin attaches are wiped by the second jquery.min.js).
 */
(function ($) {
	'use strict';
	if (!$ || !$.fn || !$.fn.multipleSelect) {
		return;
	}
	var $sel = $('#users_selector');
	if (!$sel.length) {
		return;
	}
	var placeholder = $sel.attr('data-ms-placeholder') || '...';
	if (!$sel.data('multipleSelect')) {
		$sel.multipleSelect({ placeholder: placeholder, width: '100%' });
	}
	if (Array.isArray(window.epcStorageUsersSelected)) {
		$sel.multipleSelect('setSelects', window.epcStorageUsersSelected);
	}
})(window.jQuery);
