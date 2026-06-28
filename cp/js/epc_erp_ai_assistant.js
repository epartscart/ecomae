/**
 * ERP Devin AI Assistant — external JS (loaded via PHP proxy or <script src>).
 * Handles natural-language query submission for the AI Assistant tab.
 * Must be loaded AFTER DOMContentLoaded (or deferred) because the chat DOM lives in the tab body.
 */
(function () {
	'use strict';

	function init() {
		var form = document.getElementById('ai_form');
		if (!form) return;

		var widget = document.getElementById('ai_chat');
		if (!widget) return;

		var endpointEl = form.closest('[data-ai-endpoint]') || document.querySelector('[data-ai-endpoint]');
		var csrfEl     = form.closest('[data-ai-csrf]')     || document.querySelector('[data-ai-csrf]');
		var aiEndpoint = endpointEl ? endpointEl.getAttribute('data-ai-endpoint') : '';
		var aiCsrf     = csrfEl     ? csrfEl.getAttribute('data-ai-csrf')         : '';

		form.addEventListener('submit', function (e) {
			e.preventDefault();
			var inp = document.getElementById('ai_input');
			var q = inp ? inp.value.trim() : '';
			if (!q) return;
			aiAsk(q, aiEndpoint, aiCsrf);
			if (inp) inp.value = '';
		});

		// Wire up toolbar buttons (Clear / Help) — they use onclick attrs but those
		// are also stripped.  Re-bind via delegation on the ef-toolbar.
		var toolbar = form.closest('.ef-window');
		if (toolbar) {
			toolbar.addEventListener('click', function (e) {
				var btn = e.target.closest('button');
				if (!btn) return;
				var txt = btn.textContent.trim().toLowerCase();
				if (txt.indexOf('clear') !== -1) {
					aiClear();
				} else if (txt.indexOf('help') !== -1) {
					aiAsk('help', aiEndpoint, aiCsrf);
				}
			});
		}
	}

	function aiAsk(question, endpoint, csrf) {
		var chat = document.getElementById('ai_chat');
		var btn  = document.getElementById('ai_btn');
		if (!chat) return;

		if (question !== 'help') {
			chat.innerHTML += '<div class="ai-msg ai-user"><strong>You:</strong> ' + escH(question) + '</div>';
		}

		var loadId = 'ai_load_' + Date.now();
		chat.innerHTML += '<div class="ai-msg ai-system" id="' + loadId + '"><i class="fa fa-spinner fa-spin"></i> Thinking...</div>';
		chat.scrollTop = chat.scrollHeight;
		if (btn) btn.disabled = true;

		var fd = new FormData();
		fd.append('action', 'ai_assistant_query');
		fd.append('question', question);
		if (csrf) fd.append('csrf_guard_key', csrf);

		fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (j) {
				var el = document.getElementById(loadId);
				if (!el) return;
				if (j.status && j.answer) {
					el.innerHTML = '<strong><i class="fa fa-robot" style="color:#1565c0"></i> Devin AI:</strong><br>' + renderMd(j.answer);
				} else {
					el.innerHTML = '<strong><i class="fa fa-exclamation-circle" style="color:#c62828"></i></strong> ' + (j.message || 'Error processing query');
				}
				chat.scrollTop = chat.scrollHeight;
				if (btn) btn.disabled = false;
			})
			.catch(function (err) {
				var el = document.getElementById(loadId);
				if (el) el.innerHTML = '<strong style="color:#c62828">Error:</strong> ' + err.message;
				if (btn) btn.disabled = false;
			});
	}

	function aiClear() {
		var chat = document.getElementById('ai_chat');
		if (chat) {
			chat.innerHTML = '<div class="ai-msg ai-system"><strong><i class="fa fa-robot" style="color:#1565c0"></i> Devin AI:</strong> Chat cleared. Ask me anything!</div>';
		}
	}

	function escH(s) {
		var d = document.createElement('div');
		d.textContent = s;
		return d.innerHTML;
	}

	function renderMd(md) {
		var html = md;
		html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
		var lines = html.split('\n');
		var out = [];
		var inTable = false;
		for (var i = 0; i < lines.length; i++) {
			var line = lines[i].trim();
			if (line.match(/^\|.*\|$/)) {
				if (line.match(/^\|[\s\-|]+\|$/)) continue;
				var cells = line.split('|').filter(function (c, idx, arr) { return idx > 0 && idx < arr.length - 1; });
				if (!inTable) {
					out.push('<table><thead><tr>');
					cells.forEach(function (c) { out.push('<th>' + c.trim() + '</th>'); });
					out.push('</tr></thead><tbody>');
					inTable = true;
				} else {
					out.push('<tr>');
					cells.forEach(function (c) { out.push('<td>' + c.trim() + '</td>'); });
					out.push('</tr>');
				}
			} else {
				if (inTable) { out.push('</tbody></table>'); inTable = false; }
				if (line.match(/^- /)) {
					out.push('<div style="margin-left:10px;">&bull; ' + line.substr(2) + '</div>');
				} else if (line !== '') {
					out.push('<div>' + line + '</div>');
				}
			}
		}
		if (inTable) out.push('</tbody></table>');
		return out.join('');
	}

	// Expose for external callers
	window.epcAiAssistant = { ask: aiAsk, clear: aiClear };

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}
})();
