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
 * JS logic lives in /cp/js/epc_erp_voice_command.js (loaded as external script).
 */
defined('_ASTEXE_') or die('No access');
?>
<!-- ERP AI Voice Command Widget -->
<div id="epc_voice_widget" class="epc-voice-widget" style="display:none"
	data-erp-url="<?php echo htmlspecialchars(isset($erpUrl) ? $erpUrl : '', ENT_QUOTES, 'UTF-8'); ?>"
	data-date-from="<?php echo htmlspecialchars(isset($date_from_str) ? $date_from_str : '', ENT_QUOTES, 'UTF-8'); ?>"
	data-date-to="<?php echo htmlspecialchars(isset($date_to_str) ? $date_to_str : '', ENT_QUOTES, 'UTF-8'); ?>">
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
