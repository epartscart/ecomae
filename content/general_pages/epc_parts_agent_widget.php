<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_parts_agent.php';

if (!isset($DP_Config)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$DP_Config = new DP_Config();
}

if (!epc_agent_enabled($DP_Config)) {
	return;
}

$epc_agent_branding = epc_agent_widget_branding($DP_Config);
if (empty($epc_agent_branding['enabled'])) {
	return;
}

$epc_agent_name = (string) ($epc_agent_branding['agent_name'] ?? 'Assistant');
$epc_agent_subtitle = (string) ($epc_agent_branding['subtitle'] ?? '');
$epc_agent_teaser = (string) ($epc_agent_branding['teaser_text'] ?? 'Tap to chat →');
$epc_agent_placeholder = (string) ($epc_agent_branding['placeholder'] ?? 'Type your question…');
$epc_agent_logo = trim((string) ($epc_agent_branding['logo_url'] ?? ''));
$epc_agent_launcher_label = preg_match('/\bassistant\b/i', $epc_agent_name) ? $epc_agent_name : $epc_agent_name;
$epc_agent_stack_above = !empty($epc_agent_branding['is_marketing']);
?>
<div id="epc-parts-agent" class="epc-parts-agent epc-parts-agent--blue<?php echo $epc_agent_stack_above ? ' epc-parts-agent--stacked' : ''; ?>" aria-live="polite">
	<div class="epc-parts-agent__teaser epc-parts-agent__teaser--hidden" id="epc-agent-teaser" role="status">
		<button type="button" class="epc-parts-agent__teaser-close" id="epc-agent-teaser-close" aria-label="Dismiss">&times;</button>
		<div class="epc-parts-agent__teaser-avatar"><?php if ($epc_agent_logo !== '') { ?><img src="<?php echo htmlspecialchars($epc_agent_logo, ENT_QUOTES, 'UTF-8'); ?>" alt="" /><?php } else { ?><i class="fa fa-comments" aria-hidden="true"></i><?php } ?></div>
		<div class="epc-parts-agent__teaser-body">
			<strong><?php echo htmlspecialchars($epc_agent_name, ENT_QUOTES, 'UTF-8'); ?></strong>
			<span><?php echo htmlspecialchars($epc_agent_teaser, ENT_QUOTES, 'UTF-8'); ?> <em>Tap to chat →</em></span>
		</div>
	</div>

	<button type="button" class="epc-parts-agent__launcher" id="epc-agent-launcher" aria-expanded="false" aria-controls="epc-agent-panel" title="<?php echo htmlspecialchars($epc_agent_name . ' — click to chat', ENT_QUOTES, 'UTF-8'); ?>">
		<span class="epc-parts-agent__launcher-rings" aria-hidden="true">
			<span class="epc-parts-agent__ring"></span>
			<span class="epc-parts-agent__ring epc-parts-agent__ring--2"></span>
		</span>
		<span class="epc-parts-agent__launcher-icon"><?php if ($epc_agent_logo !== '') { ?><img src="<?php echo htmlspecialchars($epc_agent_logo, ENT_QUOTES, 'UTF-8'); ?>" alt="" /><?php } else { ?><i class="fa fa-comments" aria-hidden="true"></i><?php } ?></span>
		<span class="epc-parts-agent__launcher-label"><?php echo htmlspecialchars($epc_agent_launcher_label, ENT_QUOTES, 'UTF-8'); ?></span>
		<span class="epc-parts-agent__live-badge" id="epc-agent-pulse">Live</span>
	</button>

	<div class="epc-parts-agent__panel epc-parts-agent__panel--hidden" id="epc-agent-panel" role="dialog" aria-label="<?php echo htmlspecialchars($epc_agent_name . ' chat', ENT_QUOTES, 'UTF-8'); ?>">
		<div class="epc-parts-agent__head">
			<div class="epc-parts-agent__head-main">
				<div class="epc-parts-agent__avatar"><?php if ($epc_agent_logo !== '') { ?><img src="<?php echo htmlspecialchars($epc_agent_logo, ENT_QUOTES, 'UTF-8'); ?>" alt="" /><?php } else { ?><i class="fa fa-comments" aria-hidden="true"></i><?php } ?></div>
				<div>
					<div class="epc-parts-agent__title" id="epc-agent-title"><?php echo htmlspecialchars($epc_agent_name, ENT_QUOTES, 'UTF-8'); ?></div>
					<div class="epc-parts-agent__subtitle" id="epc-agent-subtitle"><?php echo htmlspecialchars($epc_agent_subtitle, ENT_QUOTES, 'UTF-8'); ?></div>
				</div>
			</div>
			<button type="button" class="epc-parts-agent__close" id="epc-agent-close" aria-label="Close chat">&times;</button>
		</div>
		<div class="epc-parts-agent__messages" id="epc-agent-messages"></div>
		<div class="epc-parts-agent__chips" id="epc-agent-chips"></div>
		<form class="epc-parts-agent__form" id="epc-agent-form" autocomplete="off">
			<input type="text" class="epc-parts-agent__input" id="epc-agent-input" placeholder="<?php echo htmlspecialchars($epc_agent_placeholder, ENT_QUOTES, 'UTF-8'); ?>" maxlength="500" />
			<button type="submit" class="epc-parts-agent__send" id="epc-agent-send" aria-label="Send">
				<i class="fa fa-paper-plane" aria-hidden="true"></i>
			</button>
		</form>
	</div>
</div>

<style>
.epc-parts-agent {
	--epc-agent-accent: #0284c7;
	--epc-agent-accent-dark: #075985;
	--epc-agent-accent-deep: #8a131c;
	--epc-agent-accent-glow: rgba(14, 165, 233, .55);
	--epc-agent-head-bg: linear-gradient(135deg, #5a0f16 0%, #8a131c 45%, #0284c7 100%);
	--epc-agent-panel: #fff;
	position: fixed;
	right: 18px;
	bottom: 18px;
	z-index: 10050;
	font-family: inherit;
}
.epc-parts-agent__launcher {
	position: relative;
	display: flex;
	align-items: center;
	gap: 10px;
	border: 2px solid rgba(255, 255, 255, .35);
	border-radius: 999px;
	padding: 13px 20px 13px 14px;
	background: linear-gradient(120deg, #0284c7 0%, #0284c7 35%, #075985 100%);
	background-size: 200% 200%;
	color: #fff;
	box-shadow:
		0 10px 32px var(--epc-agent-accent-glow),
		0 0 0 0 rgba(14, 165, 233, .45);
	cursor: pointer;
	font-size: 13px;
	font-weight: 800;
	letter-spacing: .04em;
	text-transform: uppercase;
	transition: transform .2s ease, box-shadow .2s ease;
	animation: epcAgentFloat 3.2s ease-in-out infinite, epcAgentShimmer 4s ease infinite, epcAgentShadowPulse 2.4s ease-in-out infinite;
	overflow: visible;
	isolation: isolate;
}
.epc-parts-agent__launcher-rings {
	position: absolute;
	inset: -4px;
	pointer-events: none;
	z-index: -1;
}
.epc-parts-agent__ring {
	position: absolute;
	inset: 0;
	border-radius: 999px;
	border: 2px solid rgba(14, 165, 233, .55);
	animation: epcAgentRing 2.4s ease-out infinite;
	opacity: 0;
}
.epc-parts-agent__ring--2 { animation-delay: 1.2s; }
.epc-parts-agent__teaser-avatar,
.epc-parts-agent__avatar {
	flex-shrink: 0;
	width: 40px;
	height: 40px;
	border-radius: 12px;
	background: linear-gradient(135deg, var(--epc-agent-accent), var(--epc-agent-accent-dark));
	color: #fff;
	display: flex;
	align-items: center;
	justify-content: center;
	font-size: 18px;
	overflow: hidden;
}
.epc-parts-agent__launcher-icon {
	display: flex;
	align-items: center;
	justify-content: center;
	width: 34px;
	height: 34px;
	border-radius: 50%;
	background: rgba(255, 255, 255, .22);
	font-size: 17px;
	animation: epcAgentIconBounce 2.8s ease-in-out infinite;
	overflow: hidden;
}
.epc-parts-agent__launcher-icon img,
.epc-parts-agent__avatar img,
.epc-parts-agent__teaser-avatar img {
	width: 100%;
	height: 100%;
	object-fit: cover;
	display: block;
}
.epc-parts-agent__launcher-label {
	display: inline;
	line-height: 1.1;
	text-shadow: 0 1px 2px rgba(0, 0, 0, .15);
}
.epc-parts-agent__live-badge {
	position: absolute;
	top: -6px;
	right: -4px;
	padding: 3px 8px;
	border-radius: 999px;
	background: linear-gradient(135deg, #22c55e, #16a34a);
	color: #fff;
	font-size: 9px;
	font-weight: 800;
	letter-spacing: .06em;
	border: 2px solid #fff;
	box-shadow: 0 2px 8px rgba(34, 197, 94, .5);
	animation: epcAgentLivePulse 1.6s ease-in-out infinite;
}
.epc-parts-agent__pulse--off { display: none; }
.epc-parts-agent__launcher:hover {
	transform: translateY(-4px) scale(1.03);
	box-shadow:
		0 16px 40px rgba(14, 165, 233, .65),
		0 0 24px rgba(56, 189, 248, .4);
	animation-play-state: paused;
}
.epc-parts-agent--open .epc-parts-agent__launcher {
	animation: none;
	transform: scale(.96);
	opacity: .95;
	box-shadow: 0 6px 20px rgba(2, 132, 199, .35);
}
.epc-parts-agent--open .epc-parts-agent__launcher-rings,
.epc-parts-agent--open .epc-parts-agent__live-badge { display: none; }
@keyframes epcAgentFloat {
	0%, 100% { transform: translateY(0); }
	50% { transform: translateY(-5px); }
}
@keyframes epcAgentShimmer {
	0%, 100% { background-position: 0% 50%; }
	50% { background-position: 100% 50%; }
}
@keyframes epcAgentShadowPulse {
	0%, 100% { box-shadow: 0 10px 32px var(--epc-agent-accent-glow), 0 0 0 0 rgba(14, 165, 233, .4); }
	50% { box-shadow: 0 14px 36px rgba(14, 165, 233, .6), 0 0 0 10px rgba(14, 165, 233, 0); }
}
@keyframes epcAgentRing {
	0% { transform: scale(1); opacity: .7; }
	100% { transform: scale(1.55); opacity: 0; }
}
@keyframes epcAgentIconBounce {
	0%, 100% { transform: rotate(0deg) scale(1); }
	25% { transform: rotate(-8deg) scale(1.06); }
	75% { transform: rotate(8deg) scale(1.06); }
}
@keyframes epcAgentLivePulse {
	0%, 100% { transform: scale(1); }
	50% { transform: scale(1.08); }
}
.epc-parts-agent__teaser {
	position: absolute;
	right: 0;
	bottom: calc(100% + 14px);
	width: min(320px, calc(100vw - 36px));
	display: flex;
	align-items: flex-start;
	gap: 10px;
	padding: 14px 36px 14px 14px;
	background: linear-gradient(135deg, #fff 0%, #f0f9ff 100%);
	border-radius: 16px;
	box-shadow: 0 16px 48px rgba(15, 23, 42, .2);
	border: 2px solid rgba(14, 165, 233, .35);
	cursor: pointer;
	animation: epcAgentTeaserIn .5s ease, epcAgentTeaserWiggle 4s ease-in-out 1s infinite;
}
.epc-parts-agent__teaser--hidden { display: none; }
.epc-parts-agent__teaser-body em {
	color: var(--epc-agent-accent-dark);
	font-style: normal;
	font-weight: 700;
}
@keyframes epcAgentTeaserIn {
	from { opacity: 0; transform: translateY(12px) scale(.96); }
	to { opacity: 1; transform: translateY(0) scale(1); }
}
@keyframes epcAgentTeaserWiggle {
	0%, 88%, 100% { transform: translateX(0); }
	90% { transform: translateX(-3px); }
	92% { transform: translateX(3px); }
	94% { transform: translateX(-2px); }
	96% { transform: translateX(2px); }
}
@keyframes epcAgentSlideUp {
	from { opacity: 0; transform: translateY(8px); }
	to { opacity: 1; transform: translateY(0); }
}
.epc-parts-agent__teaser-close {
	position: absolute;
	top: 6px;
	right: 8px;
	border: none;
	background: transparent;
	font-size: 18px;
	line-height: 1;
	color: #94a3b8;
	cursor: pointer;
}
.epc-parts-agent__teaser-body { font-size: 13px; line-height: 1.45; color: #334155; }
.epc-parts-agent--stacked { bottom: 96px; }
.epc-parts-agent__teaser-body strong { display: block; color: #171717; margin-bottom: 2px; }
.epc-parts-agent__panel {
	position: absolute;
	right: 0;
	bottom: calc(100% + 12px);
	width: min(380px, calc(100vw - 24px));
	height: min(520px, calc(100vh - 100px));
	background: var(--epc-agent-panel);
	border-radius: 16px;
	box-shadow: 0 20px 60px rgba(15, 23, 42, .22);
	border: 1px solid #e2e8f0;
	display: flex;
	flex-direction: column;
	overflow: hidden;
	animation: epcAgentSlideUp .25s ease;
}
.epc-parts-agent__panel--hidden { display: none; }
.epc-parts-agent__head {
	display: flex;
	align-items: center;
	justify-content: space-between;
	padding: 14px 14px 12px;
	background: var(--epc-agent-head-bg);
	color: #fff;
}
.epc-parts-agent__head-main { display: flex; align-items: center; gap: 10px; }
.epc-parts-agent__title { font-weight: 700; font-size: 15px; }
.epc-parts-agent__subtitle { font-size: 11px; opacity: .75; margin-top: 2px; }
.epc-parts-agent__close {
	border: none;
	background: rgba(255,255,255,.12);
	color: #fff;
	width: 32px;
	height: 32px;
	border-radius: 8px;
	font-size: 20px;
	cursor: pointer;
	line-height: 1;
}
.epc-parts-agent__messages {
	flex: 1;
	overflow-y: auto;
	padding: 14px;
	background: #fff;
	display: flex;
	flex-direction: column;
	gap: 10px;
}
.epc-parts-agent__msg {
	max-width: 92%;
	padding: 10px 12px;
	border-radius: 12px;
	font-size: 13px;
	line-height: 1.5;
	word-break: break-word;
}
.epc-parts-agent__msg--agent {
	align-self: flex-start;
	background: #fff;
	border: 1px solid #e2e8f0;
	color: #1e293b;
	border-bottom-left-radius: 4px;
}
.epc-parts-agent__msg--user {
	align-self: flex-end;
	background: linear-gradient(135deg, var(--epc-agent-accent), var(--epc-agent-accent-dark));
	color: #fff;
	border-bottom-right-radius: 4px;
}
.epc-parts-agent__msg--typing {
	opacity: .7;
	font-style: italic;
}
.epc-parts-agent__msg a {
	color: var(--epc-agent-accent-dark);
	font-weight: 600;
	text-decoration: underline;
}
.epc-parts-agent__msg--user a { color: #fff; }
.epc-parts-agent__msg strong { font-weight: 700; }
.epc-parts-agent__links {
	margin-top: 8px;
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
}
.epc-parts-agent__link-btn {
	display: inline-block;
	padding: 5px 10px;
	border-radius: 999px;
	background: #f0f9ff;
	border: 1px solid #fca5a5;
	color: #8a131c !important;
	font-size: 12px;
	font-weight: 600;
	text-decoration: none !important;
}
.epc-parts-agent__chips {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
	padding: 0 12px 8px;
	background: #fff;
}
.epc-parts-agent__chip {
	border: 1px solid #cbd5e1;
	background: #fff;
	border-radius: 999px;
	padding: 5px 10px;
	font-size: 11px;
	cursor: pointer;
	color: #475569;
	transition: border-color .15s, color .15s;
}
.epc-parts-agent__chip:hover {
	border-color: var(--epc-agent-accent);
	color: var(--epc-agent-accent-dark);
}
.epc-parts-agent__form {
	display: flex;
	gap: 8px;
	padding: 10px 12px 12px;
	border-top: 1px solid #e2e8f0;
	background: #fff;
}
.epc-parts-agent__input {
	flex: 1;
	border: 1px solid #cbd5e1;
	border-radius: 10px;
	padding: 10px 12px;
	font-size: 13px;
	outline: none;
}
.epc-parts-agent__input:focus {
	border-color: var(--epc-agent-accent);
	box-shadow: 0 0 0 3px rgba(14, 165, 233, .15);
}
.epc-parts-agent__send {
	border: none;
	border-radius: 10px;
	width: 42px;
	background: var(--epc-agent-accent);
	color: #fff;
	cursor: pointer;
	font-size: 15px;
}
.epc-parts-agent__send:disabled { opacity: .5; cursor: wait; }
@media (max-width: 479px) {
	.epc-parts-agent { right: 12px; bottom: 12px; }
	.epc-parts-agent__launcher {
		padding: 12px 14px 12px 12px;
		font-size: 11px;
		gap: 8px;
	}
	.epc-parts-agent__launcher-icon {
		width: 30px;
		height: 30px;
		font-size: 15px;
	}
	.epc-parts-agent__panel {
		position: fixed;
		right: 0;
		left: 0;
		bottom: 0;
		width: 100%;
		height: min(85vh, 560px);
		border-radius: 16px 16px 0 0;
	}
}
@media (prefers-reduced-motion: reduce) {
	.epc-parts-agent__launcher,
	.epc-parts-agent__ring,
	.epc-parts-agent__launcher-icon,
	.epc-parts-agent__live-badge,
	.epc-parts-agent__teaser {
		animation: none !important;
	}
}
</style>

<script>
(function () {
	var root = document.getElementById('epc-parts-agent');
	if (!root) { return; }

	var API = '/api/epc_parts_agent.php';
	var sessionKey = 'epc_agent_session_id';
	var historyKey = 'epc_agent_chat_history';
	var visitorKey = 'epc_agent_visitor_id';
	var openedKey = 'epc_agent_opened_visit';
	var teaserKey = 'epc_agent_teaser_dismissed';

	var launcher = document.getElementById('epc-agent-launcher');
	var panel = document.getElementById('epc-agent-panel');
	var closeBtn = document.getElementById('epc-agent-close');
	var messages = document.getElementById('epc-agent-messages');
	var chips = document.getElementById('epc-agent-chips');
	var form = document.getElementById('epc-agent-form');
	var input = document.getElementById('epc-agent-input');
	var sendBtn = document.getElementById('epc-agent-send');
	var teaser = document.getElementById('epc-agent-teaser');
	var teaserClose = document.getElementById('epc-agent-teaser-close');
	var pulse = document.getElementById('epc-agent-pulse');

	var sessionId = localStorage.getItem(sessionKey) || '';
	var visitorId = localStorage.getItem(visitorKey) || '';
	var chatLog = [];
	var bootstrapData = null;
	var busy = false;
	var historyLoaded = false;

	if (!visitorId) {
		visitorId = 'v' + Math.random().toString(36).slice(2) + Date.now().toString(36);
		localStorage.setItem(visitorKey, visitorId);
	}

	function escHtml(s) {
		return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
	}

	function renderMarkdownLite(text) {
		var safe = escHtml(text);
		safe = safe.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
		safe = safe.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>');
		safe = safe.replace(/\n/g, '<br>');
		return safe;
	}

	function saveChatLocal() {
		try {
			localStorage.setItem(sessionKey, sessionId || '');
			localStorage.setItem(historyKey, JSON.stringify({
				session_id: sessionId || '',
				visitor_id: visitorId,
				messages: chatLog,
				updated: Date.now()
			}));
		} catch (e) { /* quota */ }
	}

	function loadChatLocal() {
		try {
			var raw = localStorage.getItem(historyKey);
			if (!raw) { return false; }
			var data = JSON.parse(raw);
			if (!data || !Array.isArray(data.messages) || !data.messages.length) { return false; }
			if (data.session_id) { sessionId = data.session_id; }
			chatLog = data.messages.slice();
			return true;
		} catch (e) {
			return false;
		}
	}

	function renderChatLog(list) {
		if (!messages) { return; }
		messages.innerHTML = '';
		(list || []).forEach(function (item) {
			appendMessage(item.role, item.text, item.links || [], true);
		});
	}

	function hasChatHistory() {
		return chatLog.length > 0;
	}

	function scrollMessages() {
		if (messages) {
			messages.scrollTop = messages.scrollHeight;
		}
	}

	function appendMessage(role, text, links, skipLog) {
		if (!messages) { return; }
		var el = document.createElement('div');
		el.className = 'epc-parts-agent__msg epc-parts-agent__msg--' + role;
		el.innerHTML = renderMarkdownLite(text);
		if (links && links.length) {
			var wrap = document.createElement('div');
			wrap.className = 'epc-parts-agent__links';
			links.forEach(function (link) {
				if (!link || !link.url) { return; }
				var a = document.createElement('a');
				a.className = 'epc-parts-agent__link-btn';
				a.href = link.url;
				a.target = '_blank';
				a.rel = 'noopener';
				a.textContent = link.label || 'Open';
				wrap.appendChild(a);
			});
			el.appendChild(wrap);
		}
		messages.appendChild(el);
		if (!skipLog) {
			chatLog.push({ role: role, text: text || '', links: links || [] });
			saveChatLocal();
		}
		scrollMessages();
	}

	function setChips(list) {
		if (!chips) { return; }
		chips.innerHTML = '';
		(list || []).forEach(function (label) {
			var btn = document.createElement('button');
			btn.type = 'button';
			btn.className = 'epc-parts-agent__chip';
			btn.textContent = label;
			btn.onclick = function () { sendMessage(label); };
			chips.appendChild(btn);
		});
	}

	function setOpen(open) {
		if (!panel || !launcher) { return; }
		panel.classList.toggle('epc-parts-agent__panel--hidden', !open);
		root.classList.toggle('epc-parts-agent--open', open);
		launcher.setAttribute('aria-expanded', open ? 'true' : 'false');
		if (open) {
			hideTeaser();
			if (pulse) { pulse.classList.add('epc-parts-agent__pulse--off'); }
			sessionStorage.setItem(openedKey, '1');
			if (!historyLoaded) {
				restoreChatHistory(false);
			}
			setTimeout(function () { if (input) { input.focus(); } }, 200);
		}
	}

	function hideTeaser() {
		if (teaser) { teaser.classList.add('epc-parts-agent__teaser--hidden'); }
		sessionStorage.setItem(teaserKey, '1');
	}

	function showTeaser() {
		if (sessionStorage.getItem(teaserKey) === '1') { return; }
		if (teaser) { teaser.classList.remove('epc-parts-agent__teaser--hidden'); }
	}

	function fetchJson(url, opts) {
		return fetch(url, opts || {}).then(function (r) { return r.json(); });
	}

	function showTyping() {
		var el = document.createElement('div');
		el.className = 'epc-parts-agent__msg epc-parts-agent__msg--agent epc-parts-agent__msg--typing';
		el.id = 'epc-agent-typing';
		el.textContent = 'Checking stock & catalog…';
		messages.appendChild(el);
		scrollMessages();
	}

	function hideTyping() {
		var el = document.getElementById('epc-agent-typing');
		if (el && el.parentNode) { el.parentNode.removeChild(el); }
	}

	function handleReply(data) {
		if (!data || !data.ok || !data.reply) { return; }
		if (data.session_id) {
			sessionId = data.session_id;
			localStorage.setItem(sessionKey, sessionId);
		}
		appendMessage('agent', data.reply.text || '', data.reply.links || []);
		if (data.reply.suggestions) {
			setChips(data.reply.suggestions);
		}
	}

	function restoreChatHistory(forceServer) {
		var hadLocal = loadChatLocal();
		if (hadLocal) {
			renderChatLog(chatLog);
			historyLoaded = true;
		}
		if (!sessionId) {
			if (!hadLocal) { historyLoaded = true; }
			return Promise.resolve();
		}
		return fetchJson(API + '?action=history&session_id=' + encodeURIComponent(sessionId), { credentials: 'same-origin' })
			.then(function (data) {
				if (!data || !data.ok || !Array.isArray(data.messages)) { return; }
				if (data.session_id) {
					sessionId = data.session_id;
					localStorage.setItem(sessionKey, sessionId);
				}
				if (data.messages.length >= chatLog.length) {
					chatLog = data.messages.map(function (m) {
						return {
							role: m.role === 'user' ? 'user' : 'agent',
							text: m.text || '',
							links: m.links || []
						};
					});
					renderChatLog(chatLog);
					saveChatLocal();
				}
				historyLoaded = true;
			})
			.catch(function () {
				historyLoaded = true;
			});
	}

	function readCookie(name) {
		var m = document.cookie.match(new RegExp('(?:^|; )' + name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '=([^;]*)'));
		return m ? decodeURIComponent(m[1]) : '';
	}

	function setGeoCookie(code) {
		if (!code) { return; }
		document.cookie = 'epc_country=' + encodeURIComponent(code) + '; path=/; max-age=' + (86400 * 30) + '; SameSite=Lax';
	}

	function ensureVisitorGeo() {
		if (readCookie('epc_country')) {
			return Promise.resolve();
		}
		try {
			var stored = JSON.parse(localStorage.getItem('epc_ip_country') || 'null');
			if (stored && stored.code) {
				setGeoCookie(stored.code);
				return Promise.resolve();
			}
		} catch (e) {}
		if (!window.fetch) { return Promise.resolve(); }
		return fetch('https://ipapi.co/json/', { credentials: 'omit' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (!data || !data.country_code) { return; }
				var payload = {
					code: String(data.country_code).toUpperCase(),
					name: data.country_name || ''
				};
				localStorage.setItem('epc_ip_country', JSON.stringify(payload));
				setGeoCookie(payload.code);
			})
			.catch(function () {});
	}

	function appendClientGeo(body) {
		var cc = readCookie('epc_country');
		if (cc) { body.append('client_country_code', cc); }
		try {
			var stored = JSON.parse(localStorage.getItem('epc_ip_country') || 'null');
			if (stored && stored.name) {
				body.append('client_country_name', stored.name);
			}
		} catch (e) {}
	}

	function sendMessage(text, quick) {
		if (busy || !text) { return; }
		busy = true;
		if (sendBtn) { sendBtn.disabled = true; }
		appendMessage('user', text);
		if (input) { input.value = ''; }
		setChips([]);
		showTyping();

		var body = new FormData();
		body.append('action', 'chat');
		body.append('message', text);
		if (sessionId) { body.append('session_id', sessionId); }
		if (quick) { body.append('quick', quick); }
		appendClientGeo(body);

		fetchJson(API, { method: 'POST', body: body, credentials: 'same-origin' })
			.then(function (data) {
				hideTyping();
				handleReply(data);
			})
			.catch(function () {
				hideTyping();
				appendMessage('agent', 'Sorry — connection error. Please try again or use WhatsApp.');
			})
			.finally(function () {
				busy = false;
				if (sendBtn) { sendBtn.disabled = false; }
			});
	}

	function applyBranding(data) {
		if (!data) { return; }
		var titleEl = document.getElementById('epc-agent-title');
		var subtitleEl = document.getElementById('epc-agent-subtitle');
		if (titleEl && data.agent_name) { titleEl.textContent = data.agent_name; }
		if (subtitleEl && data.subtitle) { subtitleEl.textContent = data.subtitle; }
		if (input && data.placeholder) { input.placeholder = data.placeholder; }
	}

	function initBootstrap() {
		loadChatLocal();
		ensureVisitorGeo();
		if (chatLog.length) {
			historyLoaded = false;
		}

		fetchJson(API + '?action=bootstrap', { credentials: 'same-origin' })
			.then(function (data) {
				if (!data || !data.ok) { return; }
				bootstrapData = data;
				applyBranding(data);
				var delay = data.proactive_delay_ms || 1200;
				var firstVisit = sessionStorage.getItem(openedKey) !== '1';
				var returning = hasChatHistory() || !!sessionId;

				if (returning) {
					showTeaser();
					if (pulse && chatLog.length) { pulse.classList.remove('epc-parts-agent__pulse--off'); }
					restoreChatHistory(true);
					return;
				}

				setTimeout(function () {
					if (firstVisit) {
						showTeaser();
						setTimeout(function () { setOpen(true); }, 400);
						if (data.greeting) {
							appendMessage('agent', data.greeting, []);
						}
						if (data.quick_actions) {
							setChips(data.quick_actions.map(function (a) { return a.label; }));
						}
						historyLoaded = true;
					} else {
						showTeaser();
					}
				}, delay);
			})
			.catch(function () { /* silent */ });
	}

	if (launcher) {
		launcher.addEventListener('click', function () {
			var isOpen = !panel.classList.contains('epc-parts-agent__panel--hidden');
			setOpen(!isOpen);
			if (!isOpen && !hasChatHistory() && bootstrapData && bootstrapData.greeting) {
				appendMessage('agent', bootstrapData.greeting, []);
				if (bootstrapData.quick_actions) {
					setChips(bootstrapData.quick_actions.map(function (a) { return a.label; }));
				}
				historyLoaded = true;
			}
		});
	}
	if (messages) {
		messages.addEventListener('click', function (e) {
			var a = e.target.closest('a');
			if (a && a.href) {
				a.target = '_blank';
				a.rel = 'noopener';
			}
		});
	}
	if (closeBtn) { closeBtn.addEventListener('click', function () { setOpen(false); }); }
	if (teaser) { teaser.addEventListener('click', function (e) { if (e.target === teaserClose) { return; } setOpen(true); }); }
	if (teaserClose) { teaserClose.addEventListener('click', function (e) { e.stopPropagation(); hideTeaser(); }); }

	if (form) {
		form.addEventListener('submit', function (e) {
			e.preventDefault();
			var text = input ? input.value.trim() : '';
			if (text) { sendMessage(text); }
		});
	}

	initBootstrap();
})();
</script>
