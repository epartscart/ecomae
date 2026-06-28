<?php
/**
 * ERP AI Voice Command System
 *
 * Provides voice-controlled navigation and actions across the entire ERP.
 * Uses the Web Speech API (SpeechRecognition) for browser-native voice input.
 *
 * Capabilities:
 *   - Navigate to any ERP tab by voice ("open purchase orders", "show inventory")
 *   - Query data ("show me today's sales", "what's the stock value")
 *   - Execute actions ("prepare a purchase order", "create invoice")
 *   - Run reports ("inventory report by gram", "show AR aging")
 *   - Check status ("who is busy now", "task status", "process flow")
 *   - Compliance ("run compliance test", "show audit trail")
 *
 * Integration: Included at the end of erp_main.php inside the ERP shell.
 */
defined('_ASTEXE_') or die('No access');
?>
<!-- ERP AI Voice Command Widget -->
<div id="epc_voice_widget" class="epc-voice-widget" style="display:none">
	<div class="epc-voice-panel" id="epc_voice_panel" style="display:none">
		<div class="epc-voice-panel-hd">
			<i class="fa fa-microphone"></i> ERP Voice Command
			<button type="button" class="epc-voice-close" onclick="epcVoice.closePanel()">&times;</button>
		</div>
		<div class="epc-voice-panel-body">
			<div id="epc_voice_status" class="epc-voice-status">Click the mic or say <strong>"Hey ERP"</strong> to start</div>
			<div id="epc_voice_transcript" class="epc-voice-transcript"></div>
			<div id="epc_voice_result" class="epc-voice-result"></div>
			<div class="epc-voice-examples">
				<div class="epc-voice-examples-title">Try saying:</div>
				<div class="epc-voice-example" onclick="epcVoice.simulateCommand(this.textContent)">"Open purchase orders"</div>
				<div class="epc-voice-example" onclick="epcVoice.simulateCommand(this.textContent)">"Show inventory report"</div>
				<div class="epc-voice-example" onclick="epcVoice.simulateCommand(this.textContent)">"Show me today's sales"</div>
				<div class="epc-voice-example" onclick="epcVoice.simulateCommand(this.textContent)">"Who is busy now?"</div>
				<div class="epc-voice-example" onclick="epcVoice.simulateCommand(this.textContent)">"Create a purchase order"</div>
				<div class="epc-voice-example" onclick="epcVoice.simulateCommand(this.textContent)">"Show dashboard"</div>
				<div class="epc-voice-example" onclick="epcVoice.simulateCommand(this.textContent)">"Open jewellery repairs"</div>
				<div class="epc-voice-example" onclick="epcVoice.simulateCommand(this.textContent)">"Run compliance test"</div>
			</div>
		</div>
	</div>
	<button type="button" class="epc-voice-fab" id="epc_voice_fab" title="Voice Command (Alt+V)">
		<i class="fa fa-microphone" id="epc_voice_fab_icon"></i>
	</button>
</div>

<style>
/* Voice Command Widget */
.epc-voice-widget{position:fixed;bottom:20px;right:20px;z-index:10000;font-family:inherit}
.epc-voice-fab{width:48px;height:48px;border-radius:50%;border:none;background:linear-gradient(180deg,#6b8fa3 0%,#4a7a8f 100%);color:#fff;font-size:20px;cursor:pointer;box-shadow:0 4px 12px rgba(0,0,0,.25);transition:all .2s;display:flex;align-items:center;justify-content:center}
.epc-voice-fab:hover{transform:scale(1.1);box-shadow:0 6px 20px rgba(0,0,0,.3)}
.epc-voice-fab.is-listening{background:linear-gradient(180deg,#e65100 0%,#bf360c 100%);animation:epc-voice-pulse 1.5s infinite}
@keyframes epc-voice-pulse{0%,100%{box-shadow:0 4px 12px rgba(0,0,0,.25)}50%{box-shadow:0 4px 20px rgba(230,81,0,.5)}}

.epc-voice-panel{position:fixed;bottom:80px;right:20px;width:360px;max-height:480px;background:#f0f4f7;border:1px solid #8faabc;border-radius:3px;box-shadow:0 8px 32px rgba(0,0,0,.2);overflow:hidden;display:flex;flex-direction:column}
.epc-voice-panel-hd{background:linear-gradient(180deg,#6b8fa3 0%,#4a7a8f 100%);color:#fff;padding:8px 14px;font-size:13px;font-weight:600;display:flex;align-items:center;gap:8px;justify-content:space-between}
.epc-voice-close{background:none;border:none;color:rgba(255,255,255,.8);font-size:18px;cursor:pointer;padding:0 4px}
.epc-voice-close:hover{color:#fff}
.epc-voice-panel-body{padding:12px;overflow-y:auto;flex:1}

.epc-voice-status{font-size:12px;color:#4a6a7a;padding:8px 10px;background:#d8e4ec;border:1px solid #b8c8d4;border-radius:3px;margin-bottom:10px;text-align:center}
.epc-voice-status.is-listening{background:#fff3e0;border-color:#ffb74d;color:#e65100}
.epc-voice-status.is-processing{background:#e3f2fd;border-color:#90caf9;color:#1565c0}
.epc-voice-status.is-success{background:#e8f5e9;border-color:#a5d6a7;color:#2e7d32}
.epc-voice-status.is-error{background:#ffebee;border-color:#ef9a9a;color:#c62828}

.epc-voice-transcript{font-size:14px;color:#2c4a5a;padding:8px 10px;background:#fff;border:1px solid #a8bcc8;border-radius:3px;margin-bottom:10px;min-height:36px;display:none}
.epc-voice-transcript.has-text{display:block}
.epc-voice-transcript .interim{color:#999;font-style:italic}

.epc-voice-result{font-size:12px;margin-bottom:10px;display:none}
.epc-voice-result.has-content{display:block}
.epc-voice-result .epc-voice-action{padding:8px 10px;background:#fff;border:1px solid #a8bcc8;border-radius:3px;margin-bottom:6px;cursor:pointer;transition:background .15s}
.epc-voice-result .epc-voice-action:hover{background:#eaf6fb}
.epc-voice-result .epc-voice-action .action-icon{color:#4a7a8f;margin-right:6px}
.epc-voice-result .epc-voice-action .action-label{font-weight:600;color:#2c4a5a}
.epc-voice-result .epc-voice-action .action-desc{font-size:11px;color:#6a8a9a;margin-top:2px}

.epc-voice-examples{margin-top:8px}
.epc-voice-examples-title{font-size:10px;font-weight:600;color:#6a8a9a;text-transform:uppercase;letter-spacing:.04em;margin-bottom:6px}
.epc-voice-example{font-size:11px;color:#4a7a8f;padding:4px 8px;background:#fff;border:1px solid #c8d8e0;border-radius:3px;margin-bottom:4px;cursor:pointer;transition:all .15s}
.epc-voice-example:hover{background:#eaf6fb;border-color:#8fb8cc;color:#2c4a5a}

@media(max-width:480px){
	.epc-voice-panel{width:calc(100vw - 20px);right:10px;bottom:70px;max-height:60vh}
	.epc-voice-fab{width:42px;height:42px;font-size:18px;bottom:10px;right:10px}
}
</style>

<script>
(function(){
	'use strict';

	// ERP base URL for navigation
	var erpBaseUrl = <?php echo json_encode(isset($erpUrl) ? $erpUrl : ''); ?>;
	var dateFrom = <?php echo json_encode(isset($date_from_str) ? $date_from_str : ''); ?>;
	var dateTo = <?php echo json_encode(isset($date_to_str) ? $date_to_str : ''); ?>;

	function tabUrl(tab) {
		var u = erpBaseUrl;
		if (u.indexOf('?') >= 0) {
			u += '&tab=' + encodeURIComponent(tab);
		} else {
			u += '?tab=' + encodeURIComponent(tab);
		}
		if (dateFrom) u += '&from=' + encodeURIComponent(dateFrom);
		if (dateTo) u += '&to=' + encodeURIComponent(dateTo);
		return u;
	}

	// ── Intent registry ──
	// Maps spoken phrases to ERP actions (navigation, queries, actions)
	var intents = [
		// Navigation — core modules
		{patterns: ['dashboard','home','main','overview','show dashboard','open dashboard','go to dashboard'], action: 'nav', tab: 'dashboard', label: 'Dashboard', icon: 'fa-dashboard'},
		{patterns: ['sales order','sales orders','open sales','show sales'], action: 'nav', tab: 'sales_orders', label: 'Sales Orders', icon: 'fa-shopping-cart'},
		{patterns: ['purchase order','purchase orders','open purchase','show purchase','po','p.o.'], action: 'nav', tab: 'purchase_orders', label: 'Purchase Orders', icon: 'fa-file-text-o'},
		{patterns: ['invoice','invoices','e-invoice','show invoice','open invoice'], action: 'nav', tab: 'invoices', label: 'Invoices', icon: 'fa-file-text-o'},
		{patterns: ['inventory','stock','show inventory','open inventory','inventory report'], action: 'nav', tab: 'inventory', label: 'Inventory', icon: 'fa-cubes'},
		{patterns: ['receivable','receivables','ar','a/r','accounts receivable','open receivable'], action: 'nav', tab: 'receivables', label: 'Receivables (A/R)', icon: 'fa-users'},
		{patterns: ['payable','payables','ap','a/p','accounts payable','open payable'], action: 'nav', tab: 'payables', label: 'Payables (A/P)', icon: 'fa-truck'},
		{patterns: ['cash','bank','cash and bank','open cash','show bank'], action: 'nav', tab: 'cash_bank', label: 'Cash & Bank', icon: 'fa-university'},
		{patterns: ['chart of account','coa','account chart','open coa'], action: 'nav', tab: 'coa', label: 'Chart of Accounts', icon: 'fa-list'},
		{patterns: ['general ledger','gl','g.l.','ledger','open ledger','show gl'], action: 'nav', tab: 'gl', label: 'General Ledger', icon: 'fa-book'},
		{patterns: ['profit and loss','p&l','p and l','income statement','show p&l'], action: 'nav', tab: 'pl', label: 'Profit & Loss', icon: 'fa-bar-chart'},
		{patterns: ['balance sheet','show balance sheet','open balance sheet'], action: 'nav', tab: 'balance_sheet', label: 'Balance Sheet', icon: 'fa-balance-scale'},
		{patterns: ['vat','vat return','uae vat','tax return','show vat'], action: 'nav', tab: 'vat_return', label: 'UAE VAT Return', icon: 'fa-percent'},
		{patterns: ['expense','expenses','expense report','show expense'], action: 'nav', tab: 'expense_reports', label: 'Expenses', icon: 'fa-credit-card'},
		{patterns: ['fixed asset','assets','show asset','open fixed asset'], action: 'nav', tab: 'fixed_assets', label: 'Fixed Assets', icon: 'fa-building'},
		{patterns: ['manufacturing','production','bom','show manufacturing'], action: 'nav', tab: 'manufacturing', label: 'Manufacturing', icon: 'fa-cogs'},
		{patterns: ['fulfilment','fulfillment','shipping','pick pack','show fulfilment'], action: 'nav', tab: 'fulfilment', label: 'Fulfilment', icon: 'fa-random'},
		{patterns: ['crm','customer relationship','show crm','open crm'], action: 'nav', tab: 'crm', label: 'CRM', icon: 'fa-handshake-o'},
		{patterns: ['contact','contacts','address book','show contact'], action: 'nav', tab: 'contacts', label: 'Contacts', icon: 'fa-address-book-o'},
		{patterns: ['staff','employee','team','show staff','open staff'], action: 'nav', tab: 'staff', label: 'Staff', icon: 'fa-id-badge'},
		{patterns: ['hr','human resource','show hr','open hr'], action: 'nav', tab: 'hr', label: 'HR', icon: 'fa-user-circle'},
		{patterns: ['payroll','salary','show payroll','open payroll','wps'], action: 'nav', tab: 'payroll', label: 'Payroll', icon: 'fa-money'},
		{patterns: ['workflow','process flow','task','tasks','who is busy','task status','show workflow'], action: 'nav', tab: 'workflow', label: 'Workflow / Process Flow', icon: 'fa-tasks'},
		{patterns: ['report','reports','show report','open report'], action: 'nav', tab: 'reports', label: 'Reports', icon: 'fa-table'},
		{patterns: ['audit','audit trail','show audit','compliance test','run compliance'], action: 'nav', tab: 'audit', label: 'Audit Trail', icon: 'fa-history'},
		{patterns: ['document','documents','doc vault','show document'], action: 'nav', tab: 'documents', label: 'Documents', icon: 'fa-folder-open-o'},
		{patterns: ['marketing','campaign','show marketing'], action: 'nav', tab: 'marketing', label: 'Marketing', icon: 'fa-bullhorn'},
		{patterns: ['rfq','request for quote','proposal','quotation','show rfq'], action: 'nav', tab: 'rfq', label: 'RFQ / Proposals', icon: 'fa-envelope-o'},
		{patterns: ['revenue','show revenue','revenue report'], action: 'nav', tab: 'revenue', label: 'Revenue', icon: 'fa-line-chart'},
		{patterns: ['setup','settings','configuration','module setup'], action: 'nav', tab: 'setup', label: 'Setup', icon: 'fa-cog'},

		// Navigation — jewellery modules
		{patterns: ['karat','karat master','show karat','open karat'], action: 'nav', tab: 'jw_karat', label: 'Karat Master', icon: 'fa-tachometer'},
		{patterns: ['metal purchase','open metal purchase'], action: 'nav', tab: 'jw_metal_purchase', label: 'Metal Purchase', icon: 'fa-shopping-cart'},
		{patterns: ['diamond purchase','open diamond purchase'], action: 'nav', tab: 'jw_diamond_purchase', label: 'Diamond Purchase', icon: 'fa-cart-plus'},
		{patterns: ['retail sales','pos','point of sale','open retail'], action: 'nav', tab: 'jw_retail_sales', label: 'Retail Sales (POS)', icon: 'fa-shopping-bag'},
		{patterns: ['metal sales','open metal sales'], action: 'nav', tab: 'jw_metal_sales', label: 'Metal Sales', icon: 'fa-exchange'},
		{patterns: ['repair','repairs','jewellery repair','open repair','workshop'], action: 'nav', tab: 'jw_repairs', label: 'Jewellery Repairs', icon: 'fa-wrench'},
		{patterns: ['trial balance','dual trial','weight trial','value trial'], action: 'nav', tab: 'jw_trial_balance', label: 'Dual Trial Balance', icon: 'fa-balance-scale'},
		{patterns: ['petty cash','open petty cash'], action: 'nav', tab: 'jw_petty_cash', label: 'Petty Cash', icon: 'fa-money'},
		{patterns: ['journal voucher','journal entry','jv','open journal'], action: 'nav', tab: 'jw_journal_voucher', label: 'Journal Voucher', icon: 'fa-book'},
		{patterns: ['stock balance','metal stock','stock by metal'], action: 'nav', tab: 'jw_stock_balance', label: 'Metal Stock Balance', icon: 'fa-balance-scale'},
		{patterns: ['stock verification','physical stock'], action: 'nav', tab: 'jw_stock_verification', label: 'Stock Verification', icon: 'fa-check-square'},
		{patterns: ['barcode','generate barcode','print barcode'], action: 'nav', tab: 'jw_barcode', label: 'Barcode Generation', icon: 'fa-barcode'},
		{patterns: ['sales analysis','sales trend','metal sales analysis'], action: 'nav', tab: 'jw_sales_analysis', label: 'Sales Analysis', icon: 'fa-bar-chart'},
		{patterns: ['seed data','sample data','test data','seed sample'], action: 'nav', tab: 'jw_seed_data', label: 'Seed Sample Data', icon: 'fa-database'},
		{patterns: ['design master','design','open design'], action: 'nav', tab: 'jw_design', label: 'Design Master', icon: 'fa-paint-brush'},
		{patterns: ['diamond master','diamond','open diamond'], action: 'nav', tab: 'jw_diamond', label: 'Diamond Master', icon: 'fa-diamond'},
		{patterns: ['pearl','pearl master','open pearl'], action: 'nav', tab: 'jw_pearl', label: 'Pearl Master', icon: 'fa-circle-o'},
		{patterns: ['color stone','colour stone','gemstone'], action: 'nav', tab: 'jw_color_stone', label: 'Color Stone Master', icon: 'fa-gem'},
		{patterns: ['currency','currency master','exchange rate'], action: 'nav', tab: 'jw_currency', label: 'Currency Master', icon: 'fa-money'},
		{patterns: ['rate type','rate master'], action: 'nav', tab: 'jw_rate_type', label: 'Rate Type Master', icon: 'fa-line-chart'},
		{patterns: ['purchase fixing','fix purchase rate'], action: 'nav', tab: 'jw_purchase_fixing', label: 'Purchase Fixing', icon: 'fa-lock'},
		{patterns: ['purchase window','purchase inquiry'], action: 'nav', tab: 'jw_purchase_window', label: 'Purchase Window', icon: 'fa-window-maximize'},
		{patterns: ['sales fixing','fix sales rate'], action: 'nav', tab: 'jw_sales_fixing', label: 'Sales Fixing', icon: 'fa-gavel'},
		{patterns: ['sales return','return','refund'], action: 'nav', tab: 'jw_sales_return', label: 'Sales Return', icon: 'fa-undo'},
		{patterns: ['pos advance','advance payment'], action: 'nav', tab: 'jw_pos_advance', label: 'POS Advance', icon: 'fa-credit-card-alt'},
		{patterns: ['tourist vat','tourist refund','vat refund'], action: 'nav', tab: 'jw_tourist_vat', label: 'Tourist VAT Refund', icon: 'fa-plane'},
		{patterns: ['metal stock master','metal master'], action: 'nav', tab: 'jw_metal_stock', label: 'Metal Stock Master', icon: 'fa-cubes'},

		// Action intents
		{patterns: ['create purchase order','prepare po','new purchase order','make po','prepare purchase order'], action: 'create', tab: 'purchase_orders', target: '#epc_erp_form_po', label: 'Create Purchase Order', icon: 'fa-plus'},
		{patterns: ['create sales order','new sales order','prepare so','make sales order'], action: 'create', tab: 'sales_orders', target: '#epc_erp_form_so', label: 'Create Sales Order', icon: 'fa-plus'},
		{patterns: ['create invoice','prepare invoice','new invoice','make invoice'], action: 'create', tab: 'invoices', target: '#epc_inv_form', label: 'Create Invoice', icon: 'fa-plus'},
		{patterns: ['new customer','create customer','add customer'], action: 'create', tab: 'receivables', target: '#epc_erp_form_customer', label: 'New Customer', icon: 'fa-user-plus'},
		{patterns: ['new supplier','create supplier','add supplier','new vendor'], action: 'create', tab: 'payables', target: '#epc_erp_form_supplier', label: 'New Supplier', icon: 'fa-plus'},
		{patterns: ['new item','create item','add inventory item','new sku'], action: 'create', tab: 'inventory', target: '#epc_inv_form_item', label: 'New Inventory Item', icon: 'fa-plus'},
		{patterns: ['new repair','create repair','repair receipt'], action: 'create', tab: 'jw_repairs', target: '#jw_repair_form_box', label: 'New Repair Receipt', icon: 'fa-plus'},
		{patterns: ['new metal purchase','create metal purchase'], action: 'create', tab: 'jw_metal_purchase', target: '#jw_mp_form', label: 'New Metal Purchase', icon: 'fa-plus'},

		// Query intents
		{patterns: ['today.s sales','today sales','sales today','what.s today','how much sold today'], action: 'query', query: 'today_sales', label: 'Today\'s Sales', icon: 'fa-line-chart'},
		{patterns: ['stock value','inventory value','what.s the stock','total stock'], action: 'query', query: 'stock_value', label: 'Stock Value', icon: 'fa-cubes'},
		{patterns: ['who is busy','busy now','staff busy','workload'], action: 'query', query: 'busy_staff', label: 'Staff Workload', icon: 'fa-users'},
		{patterns: ['performance','show performance','kpi','key performance'], action: 'query', query: 'performance', label: 'Performance KPIs', icon: 'fa-tachometer'},
		{patterns: ['find gap','gaps','find gaps','show gap'], action: 'query', query: 'gaps', label: 'Find Gaps', icon: 'fa-search'},
		{patterns: ['today.s report','daily report','today report'], action: 'query', query: 'daily_report', label: 'Today\'s Report', icon: 'fa-table'},
		{patterns: ['inventory.*gram','stock.*gram','report.*gram','by gram','weight report'], action: 'query', query: 'inv_by_weight', label: 'Inventory by Weight (grams)', icon: 'fa-balance-scale'},

		// System intents
		{patterns: ['help','what can you do','commands','voice help'], action: 'help', label: 'Voice Commands Help', icon: 'fa-question-circle'},
		{patterns: ['stop listening','stop','close','cancel','nevermind','never mind'], action: 'stop', label: 'Stop Listening', icon: 'fa-times'},
		{patterns: ['refresh','reload','refresh page'], action: 'refresh', label: 'Refresh Page', icon: 'fa-refresh'},
	];

	// ── Intent matching ──
	function matchIntent(text) {
		text = text.toLowerCase().trim();
		// Remove wake word if present
		text = text.replace(/^(hey erp|ok erp|erp)\s*/i, '');
		if (!text) return null;

		var bestMatch = null;
		var bestScore = 0;

		for (var i = 0; i < intents.length; i++) {
			var intent = intents[i];
			for (var j = 0; j < intent.patterns.length; j++) {
				var pattern = intent.patterns[j];
				var score = 0;

				// Exact match
				if (text === pattern) {
					score = 100;
				}
				// Regex pattern (contains dots for wildcards)
				else if (pattern.indexOf('.') >= 0) {
					try {
						if (new RegExp(pattern, 'i').test(text)) {
							score = 80;
						}
					} catch(e) {}
				}
				// Contains match
				else if (text.indexOf(pattern) >= 0) {
					score = 70 + (pattern.length / text.length * 20);
				}
				// Fuzzy: all words from pattern exist in text
				else {
					var words = pattern.split(/\s+/);
					var matchCount = 0;
					for (var w = 0; w < words.length; w++) {
						if (text.indexOf(words[w]) >= 0) matchCount++;
					}
					if (matchCount === words.length) {
						score = 50 + (matchCount * 10);
					} else if (matchCount > 0 && matchCount >= words.length * 0.6) {
						score = 30 + (matchCount * 8);
					}
				}

				if (score > bestScore) {
					bestScore = score;
					bestMatch = intent;
				}
			}
		}

		return bestScore >= 30 ? bestMatch : null;
	}

	// ── Execute intent ──
	function executeIntent(intent) {
		var resultDiv = document.getElementById('epc_voice_result');
		resultDiv.classList.add('has-content');

		if (intent.action === 'nav') {
			resultDiv.innerHTML = '<div class="epc-voice-action" onclick="location.href=\'' + tabUrl(intent.tab).replace(/'/g, "\\'") + '\'">'
				+ '<i class="fa ' + intent.icon + ' action-icon"></i>'
				+ '<span class="action-label">Navigating to ' + intent.label + '</span>'
				+ '<div class="action-desc">Opening module...</div></div>';
			speak('Opening ' + intent.label);
			setTimeout(function() { location.href = tabUrl(intent.tab); }, 1200);
		}
		else if (intent.action === 'create') {
			resultDiv.innerHTML = '<div class="epc-voice-action">'
				+ '<i class="fa ' + intent.icon + ' action-icon"></i>'
				+ '<span class="action-label">' + intent.label + '</span>'
				+ '<div class="action-desc">Navigating and opening form...</div></div>';
			speak('Opening ' + intent.label + ' form');
			setTimeout(function() { location.href = tabUrl(intent.tab) + '&voice_open=' + encodeURIComponent(intent.target); }, 1200);
		}
		else if (intent.action === 'query') {
			setStatus('processing', 'Querying: ' + intent.label + '...');
			resultDiv.innerHTML = '<div class="epc-voice-action">'
				+ '<i class="fa ' + intent.icon + ' action-icon"></i>'
				+ '<span class="action-label">' + intent.label + '</span>'
				+ '<div class="action-desc">Navigating to relevant module...</div></div>';
			speak(intent.label);
			// Navigate to relevant tab for the query
			var queryTab = 'dashboard';
			if (intent.query === 'today_sales') queryTab = 'sales_orders';
			else if (intent.query === 'stock_value' || intent.query === 'inv_by_weight') queryTab = 'inventory';
			else if (intent.query === 'busy_staff') queryTab = 'workflow';
			else if (intent.query === 'gaps') queryTab = 'reports';
			setTimeout(function() { location.href = tabUrl(queryTab); }, 1500);
		}
		else if (intent.action === 'help') {
			showHelp();
			speak('Here are the available voice commands');
		}
		else if (intent.action === 'stop') {
			stopListening();
			speak('Voice command stopped');
		}
		else if (intent.action === 'refresh') {
			speak('Refreshing page');
			setTimeout(function() { location.reload(); }, 800);
		}
	}

	// ── Speech synthesis ──
	function speak(text) {
		if ('speechSynthesis' in window) {
			var u = new SpeechSynthesisUtterance(text);
			u.rate = 1.1;
			u.pitch = 1;
			u.volume = 0.7;
			window.speechSynthesis.speak(u);
		}
	}

	// ── UI helpers ──
	var statusDiv, transcriptDiv, resultDiv, fab, fabIcon, panel;

	function init() {
		statusDiv = document.getElementById('epc_voice_status');
		transcriptDiv = document.getElementById('epc_voice_transcript');
		resultDiv = document.getElementById('epc_voice_result');
		fab = document.getElementById('epc_voice_fab');
		fabIcon = document.getElementById('epc_voice_fab_icon');
		panel = document.getElementById('epc_voice_panel');
		var widget = document.getElementById('epc_voice_widget');

		// Show widget
		widget.style.display = 'block';

		// FAB click handler
		fab.addEventListener('click', function() {
			if (panel.style.display === 'none') {
				panel.style.display = 'flex';
			}
			if (isListening) {
				stopListening();
			} else {
				startListening();
			}
		});

		// Open form target from voice_open parameter
		var params = new URLSearchParams(window.location.search);
		var voiceOpen = params.get('voice_open');
		if (voiceOpen) {
			setTimeout(function() {
				var el = document.querySelector(voiceOpen);
				if (el) {
					el.style.display = 'block';
					// Open parent FastTab if needed
					var ft = el.closest('.epc-d365-fasttab');
					if (ft && !ft.classList.contains('is-open')) {
						ft.classList.add('is-open');
						var hd = ft.querySelector('.epc-d365-ft-hd');
						if (hd) hd.setAttribute('aria-expanded', 'true');
					}
					el.scrollIntoView({behavior:'smooth', block:'center'});
					var firstInput = el.querySelector('input:not([type=hidden]),select,textarea');
					if (firstInput) firstInput.focus();
				}
			}, 500);
		}

		// Keyboard shortcut: Alt+V
		document.addEventListener('keydown', function(e) {
			if (e.altKey && e.key === 'v') {
				e.preventDefault();
				fab.click();
			}
		});
	}

	function setStatus(type, text) {
		if (!statusDiv) return;
		statusDiv.className = 'epc-voice-status' + (type ? ' is-' + type : '');
		statusDiv.innerHTML = text;
	}

	function showHelp() {
		if (!resultDiv) return;
		resultDiv.classList.add('has-content');
		var html = '<div style="font-size:12px;color:#2c4a5a">';
		html += '<div style="font-weight:600;margin-bottom:8px"><i class="fa fa-question-circle" style="color:#4a7a8f"></i> Available Commands:</div>';
		html += '<div style="margin-bottom:6px"><strong>Navigation:</strong> "Open [module]", "Show [tab]", "Go to [area]"</div>';
		html += '<div style="margin-bottom:6px"><strong>Create:</strong> "Create PO", "New invoice", "Add customer"</div>';
		html += '<div style="margin-bottom:6px"><strong>Reports:</strong> "Today\'s sales", "Stock value", "Inventory by gram"</div>';
		html += '<div style="margin-bottom:6px"><strong>Status:</strong> "Who is busy?", "Task status", "Performance"</div>';
		html += '<div style="margin-bottom:6px"><strong>System:</strong> "Refresh", "Help", "Stop"</div>';
		html += '<div style="margin-top:8px;font-size:11px;color:#6a8a9a">Keyboard shortcut: <kbd style="background:#d8e4ec;padding:1px 4px;border:1px solid #b8c8d4;border-radius:2px;font-size:10px">Alt+V</kbd></div>';
		html += '</div>';
		resultDiv.innerHTML = html;
	}

	// ── Speech Recognition ──
	var recognition = null;
	var isListening = false;

	function setupRecognition() {
		var SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
		if (!SpeechRecognition) {
			setStatus('error', '<i class="fa fa-exclamation-triangle"></i> Voice not supported in this browser. Use Chrome or Edge.');
			return false;
		}
		recognition = new SpeechRecognition();
		recognition.continuous = true;
		recognition.interimResults = true;
		recognition.lang = 'en-US';
		recognition.maxAlternatives = 3;

		recognition.onresult = function(event) {
			var final_transcript = '';
			var interim_transcript = '';
			for (var i = event.resultIndex; i < event.results.length; i++) {
				if (event.results[i].isFinal) {
					final_transcript += event.results[i][0].transcript;
				} else {
					interim_transcript += event.results[i][0].transcript;
				}
			}

			if (transcriptDiv) {
				transcriptDiv.classList.add('has-text');
				transcriptDiv.innerHTML = (final_transcript ? '<strong>' + escHtml(final_transcript) + '</strong>' : '')
					+ (interim_transcript ? ' <span class="interim">' + escHtml(interim_transcript) + '</span>' : '');
			}

			if (final_transcript) {
				processCommand(final_transcript.trim());
			}
		};

		recognition.onerror = function(event) {
			if (event.error === 'no-speech') {
				setStatus('', 'No speech detected. Try again.');
			} else if (event.error === 'aborted') {
				// User stopped
			} else {
				setStatus('error', 'Error: ' + event.error);
			}
		};

		recognition.onend = function() {
			if (isListening) {
				// Restart if still supposed to be listening
				try { recognition.start(); } catch(e) {}
			} else {
				fab.classList.remove('is-listening');
				fabIcon.className = 'fa fa-microphone';
				setStatus('', 'Click the mic or say <strong>"Hey ERP"</strong> to start');
			}
		};

		return true;
	}

	function startListening() {
		if (!recognition && !setupRecognition()) return;
		isListening = true;
		fab.classList.add('is-listening');
		fabIcon.className = 'fa fa-microphone-slash';
		setStatus('listening', '<i class="fa fa-circle" style="color:#e65100;animation:epc-voice-pulse 1s infinite"></i> Listening... Speak a command');
		if (resultDiv) { resultDiv.classList.remove('has-content'); resultDiv.innerHTML = ''; }
		try { recognition.start(); } catch(e) {}
	}

	function stopListening() {
		isListening = false;
		if (recognition) {
			try { recognition.stop(); } catch(e) {}
		}
		fab.classList.remove('is-listening');
		fabIcon.className = 'fa fa-microphone';
		setStatus('', 'Click the mic or press <strong>Alt+V</strong> to start');
	}

	function processCommand(text) {
		setStatus('processing', '<i class="fa fa-cog fa-spin"></i> Processing: "' + escHtml(text) + '"');
		var intent = matchIntent(text);
		if (intent) {
			setStatus('success', '<i class="fa fa-check"></i> Understood: <strong>' + escHtml(intent.label) + '</strong>');
			stopListening();
			executeIntent(intent);
		} else {
			setStatus('error', '<i class="fa fa-question"></i> Not understood: "' + escHtml(text) + '". Try "help" for commands.');
			speak('Sorry, I did not understand that command');
		}
	}

	function escHtml(t) {
		var d = document.createElement('div');
		d.appendChild(document.createTextNode(t));
		return d.innerHTML;
	}

	// ── Public API ──
	window.epcVoice = {
		closePanel: function() {
			stopListening();
			if (panel) panel.style.display = 'none';
		},
		simulateCommand: function(text) {
			text = text.replace(/^"|"$/g, '');
			if (transcriptDiv) {
				transcriptDiv.classList.add('has-text');
				transcriptDiv.innerHTML = '<strong>' + escHtml(text) + '</strong>';
			}
			processCommand(text);
		},
		startListening: startListening,
		stopListening: stopListening
	};

	// Init on DOM ready
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}
})();
</script>
