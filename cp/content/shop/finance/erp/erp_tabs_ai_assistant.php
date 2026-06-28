<?php
/**
 * Devin AI Assistant — ERP tab for natural language tenant data queries.
 * Suntech ef-window style.
 *
 * NOTE: All interactive JS lives in /cp/js/epc_erp_ai_assistant.js (loaded via
 * erp_desktop.php <head>).  The CP shell strips ALL <script> tags from
 * erp_main.php output, so we pass config values via data-* attributes.
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
<div class="ef-window" data-ai-endpoint="<?php echo epc_erp_h($erpAjaxEndpoint); ?>" data-ai-csrf="<?php echo epc_erp_h($csrfLocal); ?>">
	<div class="ef-title"><i class="fa fa-robot"></i> Devin AI Assistant</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-eraser"></i> Clear</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-question-circle"></i> Help</button>
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
		<form id="ai_form" style="display:flex;gap:6px;">
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
