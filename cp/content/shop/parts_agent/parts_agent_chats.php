<?php
/**
 * CP: review AI Parts Expert chat sessions and transcripts.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}
$epc_agent_cp_csrf = (is_array($user_session) && !empty($user_session['csrf_guard_key']))
	? (string)$user_session['csrf_guard_key'] : '';
$epc_agent_backend = (isset($DP_Config) && is_object($DP_Config) && !empty($DP_Config->backend_dir))
	? trim((string) $DP_Config->backend_dir, '/')
	: 'cp';
$epc_prices_url = '/' . $epc_agent_backend . '/shop/prices';
?>
<div class="hpanel epc-agent-cp">
	<div class="panel-heading hbuilt">AI agent — chats &amp; configuration</div>
	<div class="panel-body">
		<input type="hidden" id="epc-agent-cp-csrf" value="<?php echo htmlspecialchars($epc_agent_cp_csrf, ENT_QUOTES, 'UTF-8'); ?>">
		<p>Configure the storefront chat agent and review customer conversations.
			<a href="<?php echo htmlspecialchars($epc_prices_url, ENT_QUOTES, 'UTF-8'); ?>">Storefront price-list toggles</a>
			control what the agent can quote.
		</p>

		<div class="epc-agent-config">
			<div class="epc-agent-config__head"><strong>Agent configuration</strong> — per-tenant branding &amp; prompts</div>
			<div class="epc-agent-config__body">
				<div class="epc-agent-config__grid">
					<div class="form-group">
						<label><input type="checkbox" id="epc-agent-cfg-enabled" value="1" checked> Agent enabled on storefront</label>
					</div>
					<div class="form-group">
						<label>Website domain</label>
						<input type="text" id="epc-agent-cfg-domain" class="form-control input-sm" placeholder="https://www.example.com">
					</div>
					<div class="form-group">
						<label>Agent name</label>
						<input type="text" id="epc-agent-cfg-name" class="form-control input-sm" placeholder="AI Parts Expert">
					</div>
					<div class="form-group">
						<label>Logo URL</label>
						<input type="text" id="epc-agent-cfg-logo" class="form-control input-sm" placeholder="/content/files/images/logo.png">
					</div>
					<div class="form-group form-group--full">
						<label>Subtitle</label>
						<input type="text" id="epc-agent-cfg-subtitle" class="form-control input-sm" placeholder="VIN · Part numbers · Export markets">
					</div>
					<div class="form-group form-group--full">
						<label>Greeting message</label>
						<textarea id="epc-agent-cfg-greeting" class="form-control input-sm" rows="3" placeholder="Hi! I'm your assistant…"></textarea>
					</div>
					<div class="form-group form-group--full">
						<label>System prompt (operator notes — optional)</label>
						<textarea id="epc-agent-cfg-prompt" class="form-control input-sm" rows="3" placeholder="Extra guidance for agent replies…"></textarea>
						<p class="help-block" style="margin:6px 0 0;font-size:12px;color:#666;line-height:1.45;">
							Temporary price list toggles affect what the agent can quote to customers. The agent will not disclose disabled lists or admin controls.
							<a href="<?php echo htmlspecialchars($epc_prices_url, ENT_QUOTES, 'UTF-8'); ?>">Open storefront toggles on Prices</a>.
						</p>
					</div>
					<div class="form-group">
						<label>Teaser text</label>
						<input type="text" id="epc-agent-cfg-teaser" class="form-control input-sm">
					</div>
					<div class="form-group">
						<label>Input placeholder</label>
						<input type="text" id="epc-agent-cfg-placeholder" class="form-control input-sm">
					</div>
				</div>
				<div style="margin-top:12px;">
					<button type="button" class="btn btn-primary btn-sm" id="epc-agent-cfg-save">Save configuration</button>
					<span id="epc-agent-cfg-status" style="margin-left:10px;font-size:12px;color:#666;"></span>
				</div>
			</div>
		</div>

		<div id="epc-agent-stats" class="epc-agent-stats"></div>

		<div class="epc-agent-filters">
			<div class="form-group">
				<label>Search</label>
				<input type="text" id="epc-agent-q" class="form-control input-sm" style="width:220px;" placeholder="Session ID or message text">
			</div>
			<div class="form-group">
				<label>From</label>
				<input type="date" id="epc-agent-from" class="form-control input-sm" style="width:150px;">
			</div>
			<div class="form-group">
				<label>To</label>
				<input type="date" id="epc-agent-to" class="form-control input-sm" style="width:150px;">
			</div>
			<div class="form-group" style="padding-top:18px;">
				<button type="button" class="btn btn-primary btn-sm" id="epc-agent-search">Search</button>
				<button type="button" class="btn btn-default btn-sm" id="epc-agent-reset">Reset</button>
				<button type="button" class="btn btn-default btn-sm" id="epc-agent-sync">Sync temp files</button>
				<button type="button" class="btn btn-default btn-sm" id="epc-agent-export" title="Download filtered chats as CSV">Export CSV</button>
			</div>
		</div>

		<div id="epc-agent-msg" class="alert" style="display:none;margin-top:8px;"></div>

		<div class="table-responsive">
			<table class="table table-striped table-hover epc-agent-table">
				<thead>
					<tr>
						<th>Last activity</th>
						<th>Session</th>
						<th>Msgs</th>
						<th>Customer</th>
						<th>Last customer message</th>
						<th></th>
					</tr>
				</thead>
				<tbody id="epc-agent-tbody">
					<tr><td colspan="6">Loading…</td></tr>
				</tbody>
			</table>
		</div>

		<div class="epc-agent-pager">
			<button type="button" class="btn btn-default btn-sm" id="epc-agent-prev" disabled>Previous</button>
			<span id="epc-agent-page-info" style="margin:0 10px;font-size:13px;"></span>
			<button type="button" class="btn btn-default btn-sm" id="epc-agent-next" disabled>Next</button>
		</div>

		<div id="epc-agent-detail" class="epc-agent-detail-wrap">
			<div class="epc-agent-detail-head">
				<div>
					<strong id="epc-agent-detail-title">Chat detail</strong>
					<div id="epc-agent-detail-sub" class="epc-agent-meta"></div>
				</div>
				<button type="button" class="btn btn-default btn-xs" id="epc-agent-detail-close">Close</button>
			</div>
			<div id="epc-agent-detail-body" class="epc-agent-detail-body"></div>
		</div>
	</div>
</div>
