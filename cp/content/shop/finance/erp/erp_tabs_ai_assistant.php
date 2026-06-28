<?php
/**
 * Devin AI Assistant — ERP tab for natural language tenant data queries.
 * Suntech ef-window style.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
include __DIR__ . '/erp_entry_form_css.php';

$csrfLocal = isset($csrf) ? $csrf : '';
$erpAjaxEndpoint = isset($erpAjaxUrl) ? $erpAjaxUrl : '';

erp_page_header(
	'<i class="fa fa-robot"></i> AI Assistant',
	'Ask questions about your ERP data in natural language. Powered by Devin AI.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'AI Assistant'),
	)
);
?>
<div class="ef-window">
	<div class="ef-title"><i class="fa fa-robot"></i> Devin AI Assistant</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="aiClear()"><i class="fa fa-eraser"></i> Clear</button>
		<button class="btn btn-default btn-xs" onclick="aiAsk('help')"><i class="fa fa-question-circle"></i> Help</button>
	</div>
	<div class="ef-body" style="min-height:400px;">
		<div id="ai_chat" style="max-height:500px;overflow-y:auto;padding:8px;background:#fafafa;border:1px solid #e0e0e0;border-radius:4px;margin-bottom:10px;font-size:13px;">
			<div class="ai-msg ai-system" style="padding:8px 12px;background:#e3f2fd;border-radius:6px;margin-bottom:8px;">
				<strong><i class="fa fa-robot" style="color:#1565c0"></i> Devin AI:</strong>
				Hello! I can help you query your ERP data. Try asking:
				<ul style="margin:4px 0 0 16px;padding:0;font-size:12px;">
					<li>"What is our gram inventory with necklaces and bangles?"</li>
					<li>"Show me total gold stock weight"</li>
					<li>"How many open repairs?"</li>
					<li>"Show me our sales orders"</li>
					<li>"Give me a dashboard overview"</li>
				</ul>
			</div>
		</div>
		<form id="ai_form" onsubmit="return aiSubmit(event)" style="display:flex;gap:6px;">
			<input type="text" id="ai_input" class="form-control input-sm" placeholder="Ask me anything about your ERP data..." style="flex:1;border:1px solid #90caf9;border-radius:4px;padding:6px 10px;font-size:13px;" autocomplete="off">
			<button type="submit" class="btn btn-primary btn-sm" id="ai_btn"><i class="fa fa-paper-plane"></i> Ask</button>
		</form>
		<div style="margin-top:6px;font-size:11px;color:#888;">
			<i class="fa fa-info-circle"></i> All queries run against your tenant's data only. No data leaves the platform.
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=AI ASSISTANT</span>
		<span>Powered by Devin</span>
	</div>
</div>
<style>
.ai-msg { padding:8px 12px; border-radius:6px; margin-bottom:8px; line-height:1.5; }
.ai-user { background:#e8eaf6; text-align:right; }
.ai-system { background:#e3f2fd; }
.ai-msg table { width:100%; border-collapse:collapse; margin:4px 0; font-size:11px; }
.ai-msg th { background:#1565c0; color:#fff; padding:3px 6px; text-align:left; }
.ai-msg td { padding:3px 6px; border-bottom:1px solid #e0e0e0; }
.ai-msg strong { font-weight:600; }
</style>
<script>
var aiEndpoint = <?php echo json_encode($erpAjaxEndpoint); ?>;
var aiCsrf = <?php echo json_encode($csrfLocal); ?>;

function aiSubmit(e) {
	e.preventDefault();
	var inp = document.getElementById('ai_input');
	var q = inp.value.trim();
	if (!q) return false;
	aiAsk(q);
	inp.value = '';
	return false;
}

function aiAsk(question) {
	var chat = document.getElementById('ai_chat');
	var btn = document.getElementById('ai_btn');

	// Show user message
	if (question !== 'help') {
		chat.innerHTML += '<div class="ai-msg ai-user"><strong>You:</strong> ' + escH(question) + '</div>';
	}

	// Show loading
	var loadId = 'ai_load_' + Date.now();
	chat.innerHTML += '<div class="ai-msg ai-system" id="' + loadId + '"><i class="fa fa-spinner fa-spin"></i> Thinking...</div>';
	chat.scrollTop = chat.scrollHeight;
	btn.disabled = true;

	var fd = new FormData();
	fd.append('action', 'ai_assistant_query');
	fd.append('question', question);
	fd.append('csrf_guard_key', aiCsrf);

	fetch(aiEndpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
		.then(function(r) { return r.json(); })
		.then(function(j) {
			var el = document.getElementById(loadId);
			if (!el) return;
			if (j.status && j.answer) {
				el.innerHTML = '<strong><i class="fa fa-robot" style="color:#1565c0"></i> Devin AI:</strong><br>' + renderMd(j.answer);
			} else {
				el.innerHTML = '<strong><i class="fa fa-exclamation-circle" style="color:#c62828"></i></strong> ' + (j.message || 'Error processing query');
			}
			chat.scrollTop = chat.scrollHeight;
			btn.disabled = false;
		})
		.catch(function(err) {
			var el = document.getElementById(loadId);
			if (el) el.innerHTML = '<strong style="color:#c62828">Error:</strong> ' + err.message;
			btn.disabled = false;
		});
}

function aiClear() {
	document.getElementById('ai_chat').innerHTML = '<div class="ai-msg ai-system"><strong><i class="fa fa-robot" style="color:#1565c0"></i> Devin AI:</strong> Chat cleared. Ask me anything!</div>';
}

function escH(s) {
	var d = document.createElement('div');
	d.textContent = s;
	return d.innerHTML;
}

function renderMd(md) {
	// Simple markdown rendering for tables, bold, lists, line breaks
	var html = md;
	// Bold
	html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
	// Tables — detect lines starting with |
	var lines = html.split('\n');
	var out = [];
	var inTable = false;
	for (var i = 0; i < lines.length; i++) {
		var line = lines[i].trim();
		if (line.match(/^\|.*\|$/)) {
			if (line.match(/^\|[\s\-|]+\|$/)) continue; // separator row
			var cells = line.split('|').filter(function(c, idx, arr) { return idx > 0 && idx < arr.length - 1; });
			if (!inTable) {
				out.push('<table><thead><tr>');
				cells.forEach(function(c) { out.push('<th>' + c.trim() + '</th>'); });
				out.push('</tr></thead><tbody>');
				inTable = true;
			} else {
				out.push('<tr>');
				cells.forEach(function(c) { out.push('<td>' + c.trim() + '</td>'); });
				out.push('</tr>');
			}
		} else {
			if (inTable) { out.push('</tbody></table>'); inTable = false; }
			// Lists
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
</script>
