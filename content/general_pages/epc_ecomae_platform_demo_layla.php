<?php
/**
 * Layla AI demo wizard — shared UI (hero avatar, typewriter speech, wizard form).
 */
defined('_ASTEXE_') or die('No access');

function epc_ecomae_demo_layla_avatar_url()
{
	return '/content/files/images/ecomae-platform/layla-avatar.svg';
}

function epc_ecomae_demo_layla_pitch_lines($demoDays = 3)
{
	$days = (int) $demoDays;
	return array(
		"Hi! I'm Layla — your ECOM AE demo specialist. Let's spin up your {$days}-day sandbox in minutes.",
		'Pick auto spare parts, fashion retail, or ERP only — full storefront or finance/CRM without a shop.',
		'ERP + CRM + optional e-commerce in one cloud. Go live in 24 hours after DNS — UAE e-invoice ready.',
		'Your sandbox is fully isolated on MySQL — explore modules, then convert or expire.',
		'Ready when you are — choose a path and I\'ll launch your personal demo tenant.',
	);
}

function epc_ecomae_demo_layla_styles()
{
	return <<<'CSS'
<style>
.epm-layla-hero{position:relative;margin:24px 0 32px;border-radius:24px;overflow:hidden;border:1px solid rgba(14,165,233,.28);background:linear-gradient(135deg,#1a0407 0%,#5a0f16 38%,#0a0a0a 100%);box-shadow:0 0 60px rgba(14,165,233,.12),inset 0 1px 0 rgba(255,255,255,.06)}
.epm-layla-hero__grid{display:grid;grid-template-columns:minmax(240px,1fr) minmax(280px,1.15fr);gap:clamp(16px,3vw,36px);align-items:center;padding:clamp(20px,4vw,40px)}
@media(max-width:860px){.epm-layla-hero__grid{grid-template-columns:1fr;text-align:center}}
.epm-layla-hero__badge-row{display:flex;flex-wrap:wrap;gap:10px;margin-bottom:12px}
@media(max-width:860px){.epm-layla-hero__badge-row{justify-content:center}}
.epm-layla-hero h1{font-size:clamp(26px,4.5vw,40px);font-weight:800;margin:0 0 10px;color:#fff;line-height:1.12}
.epm-layla-hero .lead{font-size:clamp(15px,2vw,17px);color:#cbd5e1;margin:0;line-height:1.6;max-width:560px}
@media(max-width:860px){.epm-layla-hero .lead{margin:0 auto}}
.epm-layla-stage{position:relative;display:flex;flex-direction:column;align-items:center;justify-content:center;min-height:320px}
.epm-layla-stage__glow{position:absolute;width:min(92%,340px);aspect-ratio:1;border-radius:50%;background:radial-gradient(circle,rgba(14,165,233,.22) 0%,transparent 68%);animation:epmLaylaPulse 3.2s ease-in-out infinite;pointer-events:none}
@keyframes epmLaylaPulse{0%,100%{transform:scale(1);opacity:.85}50%{transform:scale(1.06);opacity:1}}
.epm-layla-stage__avatar{position:relative;width:min(100%,320px);max-width:320px;z-index:2;animation:epmLaylaFloat 4.5s ease-in-out infinite;filter:drop-shadow(0 16px 40px rgba(14,165,233,.35))}
.epm-layla-stage__avatar svg{width:100%;height:auto;display:block}
@keyframes epmLaylaFloat{0%,100%{transform:translateY(0)}50%{transform:translateY(-10px)}}
.epm-layla-stage__avatar.is-speaking .layla-mouth > path{animation:epmLaylaLipSync .28s ease-in-out infinite alternate}
@keyframes epmLaylaLipSync{from{d:path("M208 292 Q240 312 272 292 Q240 302 208 292 Z")}to{d:path("M206 288 Q240 318 274 288 Q240 308 206 288 Z")}}
.epm-layla-speech{position:relative;margin-top:16px;width:min(100%,340px);z-index:3}
.epm-layla-speech__bubble{position:relative;background:rgba(23,23,23,.92);border:1px solid rgba(14,165,233,.35);border-radius:16px;padding:16px 18px 14px;box-shadow:0 8px 32px rgba(10,10,10,.45);min-height:72px;text-align:left}
.epm-layla-speech__bubble:before{content:"";position:absolute;top:-10px;left:50%;transform:translateX(-50%);border:10px solid transparent;border-bottom-color:rgba(14,165,233,.35)}
.epm-layla-speech__bubble:after{content:"";position:absolute;top:-8px;left:50%;transform:translateX(-50%);border:8px solid transparent;border-bottom-color:rgba(23,23,23,.92)}
.epm-layla-speech__label{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:8px;font-size:11px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:var(--epm-cyan)}
.epm-layla-speech__text{font-size:15px;line-height:1.55;color:#e2e8f0;margin:0;min-height:3.1em}
.epm-layla-speech__cursor{display:inline-block;width:2px;height:1em;background:var(--epm-cyan);margin-left:2px;vertical-align:text-bottom;animation:epmLaylaBlink .85s step-end infinite}
@keyframes epmLaylaBlink{50%{opacity:0}}
.epm-layla-speech__controls{display:flex;align-items:center;gap:8px;margin-top:10px;flex-wrap:wrap}
.epm-layla-voice-btn{display:inline-flex;align-items:center;gap:6px;padding:6px 12px;border-radius:999px;border:1px solid rgba(14,165,233,.35);background:rgba(14,165,233,.08);color:var(--epm-cyan);font-size:12px;font-weight:700;cursor:pointer;font-family:inherit}
.epm-layla-voice-btn:hover{background:rgba(14,165,233,.16)}
.epm-layla-voice-btn.is-on{border-color:#0ea5e9;background:rgba(14,165,233,.2);color:#fff}
.epm-layla-voice-hint{font-size:11px;color:var(--epm-muted);margin:0}
.epm-layla-wizard-grid{display:grid;grid-template-columns:minmax(260px,.9fr) minmax(320px,1.1fr);gap:28px;align-items:start;margin:8px 0 32px}
@media(max-width:900px){.epm-layla-wizard-grid{grid-template-columns:1fr}}
.epm-layla-wizard-card{background:linear-gradient(160deg,rgba(14,165,233,.06),var(--epm-card));border:1px solid rgba(14,165,233,.28);border-radius:20px;padding:22px 24px 26px;box-shadow:0 0 40px rgba(14,165,233,.08)}
.epm-layla-wizard-card h4{margin:0 0 14px;color:#fff;font-size:18px;display:flex;align-items:center;gap:10px}
.epm-demo-wizard{display:flex;flex-direction:column;gap:12px;min-height:280px}
.epm-demo-progress{display:none;height:10px;background:rgba(23,23,23,.5);border-radius:999px;overflow:hidden;margin:4px 0 8px;border:1px solid rgba(14,165,233,.15)}
.epm-demo-progress.is-active{display:block}
.epm-demo-progress__bar{height:100%;width:0;background:linear-gradient(90deg,#075985,#7c3aed,#0ea5e9);background-size:200% 100%;border-radius:999px;transition:width .35s ease;animation:epmDemoBarShine 2s linear infinite}
@keyframes epmDemoBarShine{0%{background-position:0% 50%}100%{background-position:200% 50%}}
.epm-demo-progress__label{font-size:12px;color:var(--epm-muted);margin:0 0 6px;display:none}
.epm-demo-progress.is-active+.epm-demo-progress__label,.epm-demo-progress.is-active~.epm-demo-progress__label{display:block}
.epm-demo-chat{background:#141414;border:1px solid rgba(14,165,233,.2);border-radius:12px;padding:14px 16px;max-height:220px;overflow-y:auto;flex:1}
.epm-demo-msg{margin:8px 0;padding:10px 14px;border-radius:10px;font-size:14px;line-height:1.45;max-width:92%}
.epm-demo-msg--bot{background:rgba(14,165,233,.12);color:#e2e8f0}
.epm-demo-msg--user{background:rgba(124,58,237,.25);color:#fff;margin-left:auto;text-align:right}
.epm-demo-step{display:none}
.epm-demo-step.active{display:block}
.epm-demo-industry{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin:10px 0}
@media(max-width:720px){.epm-demo-industry{grid-template-columns:1fr}}
.epm-demo-industry label.epm-demo-industry--erp{border-color:rgba(167,139,250,.35)}
.epm-demo-industry label.epm-demo-industry--erp:has(input:checked){border-color:#a78bfa;background:rgba(124,58,237,.12)}
.epm-demo-industry label{display:block;padding:16px 12px;border:2px solid rgba(14,165,233,.25);border-radius:12px;cursor:pointer;text-align:center;font-size:13px;transition:border-color .2s,background .2s,transform .2s}
.epm-demo-industry label:hover{border-color:rgba(14,165,233,.5);transform:translateY(-2px)}
.epm-demo-industry input{position:absolute;opacity:0;pointer-events:none}
.epm-demo-industry label:has(input:checked){border-color:var(--epm-cyan);background:rgba(14,165,233,.12);box-shadow:0 0 20px rgba(14,165,233,.12)}
.epm-demo-industry label i{display:block;font-size:22px;margin-bottom:8px;color:var(--epm-cyan)}
.epm-demo-actions{display:flex;gap:8px;margin-top:12px;flex-wrap:wrap}
.epm-layla-home{margin:36px 0;padding:0}
.epm-layla-home__inner{border-radius:24px;border:1px solid rgba(14,165,233,.25);background:linear-gradient(145deg,rgba(14,165,233,.08),rgba(23,23,23,.95));padding:clamp(20px,4vw,36px);display:grid;grid-template-columns:minmax(220px,.95fr) minmax(280px,1.05fr);gap:clamp(20px,3vw,32px);align-items:center}
@media(max-width:860px){.epm-layla-home__inner{grid-template-columns:1fr;text-align:center}}
.epm-layla-home__stage .epm-layla-stage{min-height:260px}
.epm-layla-home__cta{display:flex;flex-wrap:wrap;gap:10px;margin-top:18px}
@media(max-width:860px){.epm-layla-home__cta{justify-content:center}}
.epm-layla-home__pills{display:flex;flex-wrap:wrap;gap:8px;margin-top:14px}
@media(max-width:860px){.epm-layla-home__pills{justify-content:center}}
.epm-layla-pill{display:inline-flex;align-items:center;gap:6px;padding:8px 14px;border-radius:999px;border:1px solid rgba(14,165,233,.3);background:rgba(10,10,10,.5);color:#e2e8f0;font-size:13px;font-weight:600;text-decoration:none;transition:border-color .2s,background .2s}
.epm-layla-pill:hover{border-color:var(--epm-cyan);background:rgba(14,165,233,.1);color:#fff}
.epm-layla-pill i{color:var(--epm-cyan)}
/* Marketing splash (home first visit) */
.epc-layla-splash{position:fixed;inset:0;z-index:10080;display:flex;align-items:center;justify-content:center;padding:clamp(12px,3vw,24px);background:rgba(10,10,10,.88);backdrop-filter:blur(8px);animation:epcLaylaSplashIn .45s ease}
.epc-layla-splash--hidden{display:none!important}
@keyframes epcLaylaSplashIn{from{opacity:0}to{opacity:1}}
.epc-layla-splash__card{position:relative;width:min(960px,100%);max-height:min(92vh,820px);overflow:auto;border-radius:28px;border:1px solid rgba(14,165,233,.35);background:linear-gradient(145deg,#1a0407 0%,#5a0f16 42%,#0a0a0a 100%);box-shadow:0 0 80px rgba(14,165,233,.18),0 24px 80px rgba(10,10,10,.65);animation:epcLaylaSplashCard .5s ease .05s both}
@keyframes epcLaylaSplashCard{from{opacity:0;transform:translateY(24px) scale(.97)}to{opacity:1;transform:none}}
.epc-layla-splash__close{position:absolute;top:14px;right:14px;z-index:3;width:40px;height:40px;border:none;border-radius:12px;background:rgba(23,23,23,.75);color:#e2e8f0;font-size:22px;line-height:1;cursor:pointer;border:1px solid rgba(148,163,184,.25)}
.epc-layla-splash__close:hover{background:rgba(14,165,233,.15);color:#fff;border-color:rgba(14,165,233,.4)}
.epc-layla-splash__grid{display:grid;grid-template-columns:minmax(240px,1fr) minmax(260px,1.1fr);gap:clamp(16px,3vw,32px);align-items:center;padding:clamp(24px,4vw,40px)}
@media(max-width:860px){.epc-layla-splash__grid{grid-template-columns:1fr;text-align:center}}
.epc-layla-splash__copy h2{font-size:clamp(24px,4vw,34px);font-weight:800;color:#fff;margin:0 0 10px;line-height:1.15}
.epc-layla-splash__copy p{color:#cbd5e1;font-size:clamp(15px,2vw,17px);line-height:1.6;margin:0 0 18px;max-width:520px}
@media(max-width:860px){.epc-layla-splash__copy p{margin-left:auto;margin-right:auto}}
.epc-layla-splash__cta{display:flex;flex-wrap:wrap;gap:10px}
@media(max-width:860px){.epc-layla-splash__cta{justify-content:center}}
.epc-layla-splash__stage .epm-layla-stage{min-height:280px}
/* Footer floating widget (after splash dismiss) */
.epc-layla-footer-widget{position:fixed;right:18px;bottom:18px;z-index:10060;font-family:inherit}
.epc-layla-footer-widget--hidden{display:none!important}
.epc-layla-footer-widget__launcher{position:relative;display:flex;align-items:center;gap:10px;border:2px solid rgba(255,255,255,.28);border-radius:999px;padding:12px 18px 12px 12px;background:linear-gradient(120deg,#0284c7 0%,#075985 45%,#7c3aed 100%);background-size:200% 200%;color:#fff;box-shadow:0 10px 32px rgba(14,165,233,.45);cursor:pointer;font-size:12px;font-weight:800;letter-spacing:.04em;text-transform:uppercase;animation:epcLaylaFabFloat 3.2s ease-in-out infinite,epcLaylaFabShimmer 4s ease infinite}
.epc-layla-footer-widget__launcher:hover{transform:translateY(-3px);box-shadow:0 14px 40px rgba(14,165,233,.55)}
.epc-layla-footer-widget--open .epc-layla-footer-widget__launcher{animation:none;opacity:.92}
.epc-layla-footer-widget__launcher-avatar{width:38px;height:38px;border-radius:50%;overflow:hidden;border:2px solid rgba(255,255,255,.45);flex-shrink:0;background:#171717}
.epc-layla-footer-widget__launcher-avatar svg{width:100%;height:100%;display:block}
.epc-layla-footer-widget__live{position:absolute;top:-6px;right:-2px;padding:3px 8px;border-radius:999px;background:linear-gradient(135deg,#0ea5e9,#075985);color:#fff;font-size:9px;font-weight:800;border:2px solid #fff}
@keyframes epcLaylaFabFloat{0%,100%{transform:translateY(0)}50%{transform:translateY(-5px)}}
@keyframes epcLaylaFabShimmer{0%,100%{background-position:0% 50%}50%{background-position:100% 50%}}
.epc-layla-footer-widget__panel{position:absolute;right:0;bottom:calc(100% + 12px);width:min(380px,calc(100vw - 24px));max-height:min(520px,calc(100vh - 100px));display:flex;flex-direction:column;overflow:hidden;border-radius:18px;border:1px solid rgba(14,165,233,.3);background:linear-gradient(160deg,#171717,#0a0a0a);box-shadow:0 20px 60px rgba(10,10,10,.55);animation:epcLaylaPanelUp .25s ease}
.epc-layla-footer-widget__panel--hidden{display:none}
@keyframes epcLaylaPanelUp{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:none}}
.epc-layla-footer-widget__head{display:flex;align-items:center;justify-content:space-between;padding:12px 14px;border-bottom:1px solid rgba(14,165,233,.2);color:#fff}
.epc-layla-footer-widget__head strong{font-size:14px}
.epc-layla-footer-widget__head span{display:block;font-size:11px;color:#94a3b8;margin-top:2px;font-weight:400}
.epc-layla-footer-widget__head-close{border:none;background:rgba(255,255,255,.1);color:#fff;width:32px;height:32px;border-radius:8px;font-size:18px;cursor:pointer}
.epc-layla-footer-widget__body{padding:14px 16px 16px;overflow-y:auto}
.epc-layla-footer-widget__body .epm-layla-stage{min-height:220px}
.epc-layla-footer-widget__body .epm-layla-speech{width:100%}
.epc-layla-footer-widget__actions{display:flex;flex-wrap:wrap;gap:8px;margin-top:12px}
@media(max-width:479px){.epc-layla-footer-widget{right:12px;bottom:12px}.epc-layla-footer-widget__panel{position:fixed;left:0;right:0;bottom:0;width:100%;max-height:min(85vh,560px);border-radius:18px 18px 0 0}}
@media(prefers-reduced-motion:reduce){.epc-layla-splash,.epc-layla-splash__card,.epc-layla-footer-widget__launcher{animation:none!important}}
</style>
CSS;
}

function epc_ecomae_demo_layla_avatar_inline()
{
	static $svg = null;
	if ($svg === null) {
		$path = $_SERVER['DOCUMENT_ROOT'] . '/content/files/images/ecomae-platform/layla-avatar.svg';
		$svg = is_readable($path) ? (string) file_get_contents($path) : '';
	}
	return $svg;
}

function epc_ecomae_demo_layla_stage_html($demoDays = 3, $idPrefix = 'epm-layla')
{
	$lines = epc_ecomae_demo_layla_pitch_lines($demoDays);
	$linesJson = json_encode(array_values($lines), JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT);
	$avatarSvg = epc_ecomae_demo_layla_avatar_inline();
	ob_start();
	?>
<div class="epm-layla-stage" id="<?php echo epc_ecomae_h($idPrefix); ?>-stage">
	<div class="epm-layla-stage__glow" aria-hidden="true"></div>
	<div class="epm-layla-stage__avatar" id="<?php echo epc_ecomae_h($idPrefix); ?>-avatar" role="img" aria-label="Layla — ECOM AE AI demo specialist"><?php echo $avatarSvg; ?></div>
	<div class="epm-layla-speech" id="<?php echo epc_ecomae_h($idPrefix); ?>-speech">
		<div class="epm-layla-speech__bubble">
			<div class="epm-layla-speech__label">
				<span><i class="fa fa-microphone"></i> Layla · AI demo wizard</span>
				<span class="epm-layla-speech__live" aria-live="polite"><i class="fa fa-circle" style="font-size:8px;color:#0ea5e9"></i> Live</span>
			</div>
			<p class="epm-layla-speech__text" id="<?php echo epc_ecomae_h($idPrefix); ?>-text" data-lines="<?php echo epc_ecomae_h($linesJson); ?>"></p>
		</div>
		<div class="epm-layla-speech__controls">
			<button type="button" class="epm-layla-voice-btn" id="<?php echo epc_ecomae_h($idPrefix); ?>-voice" aria-pressed="false" title="Enable voice (requires click)"><i class="fa fa-volume-off"></i> Voice off</button>
			<p class="epm-layla-voice-hint">Tap Voice to hear Layla · browser may require your click first</p>
		</div>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_demo_layla_wizard_html(array $demo, array $presets, $pref = '', $flash = null)
{
	$days = (int) $demo['days'];
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
	$demoCountries = epc_countries_registration_options();
	ob_start();
	?>
	<?php if (is_array($flash)) { ?>
	<div class="epm-alert <?php echo !empty($flash['ok']) ? 'ok' : 'err'; ?>"><?php echo epc_ecomae_h($flash['message']); ?>
		<?php if (!empty($flash['ok']) && !empty($flash['storefront'])) { ?>
		<br /><strong>Check your email</strong> for login details.
		<br /><a href="<?php echo epc_ecomae_h($flash['storefront']); ?>" style="color:inherit">Open storefront →</a>
		<?php } ?>
	</div>
	<?php } ?>
	<div class="epm-layla-wizard-grid">
		<div>
			<h2 class="epm-section-title" style="margin-top:0">What you get</h2>
			<ul class="epm-feature-list">
				<?php foreach ($demo['includes'] as $inc) { ?><li><?php echo epc_ecomae_h($inc); ?></li><?php } ?>
				<li>Isolated MySQL — never mixed with production data</li>
			</ul>
			<h3 class="epm-section-title" style="font-size:20px">Phase 1–2 industries</h3>
			<ul class="epm-feature-list">
				<?php foreach ($presets as $p) { ?>
				<li><strong><?php echo epc_ecomae_h($p['label']); ?></strong></li>
				<?php } ?>
			</ul>
		</div>
		<div class="epm-layla-wizard-card epm-form">
			<h4><i class="fa fa-magic"></i> Launch your sandbox</h4>
			<p class="epm-demo-progress__label" id="epm-demo-progress-label" style="display:none">Provisioning your isolated tenant…</p>
			<div class="epm-demo-progress" id="epm-demo-progress" aria-hidden="true"><div class="epm-demo-progress__bar" id="epm-demo-progress-bar"></div></div>
			<div class="epm-demo-wizard" id="epm-demo-wizard">
				<div class="epm-demo-chat" id="epm-demo-chat" aria-live="polite"></div>
				<div class="epm-demo-step active" data-step="industry">
					<div class="epm-demo-industry">
						<label><input type="radio" name="epm_industry" value="auto_parts"<?php echo ($pref === '' || $pref === 'auto_parts') ? ' checked' : ''; ?>><span><i class="fa fa-car"></i>Auto spare parts</span><small style="display:block;margin-top:6px;opacity:.85">Storefront + CP + ERP</small></label>
						<label><input type="radio" name="epm_industry" value="fashion"<?php echo $pref === 'fashion' ? ' checked' : ''; ?>><span><i class="fa fa-shopping-bag"></i>Fashion retail</span><small style="display:block;margin-top:6px;opacity:.85">Storefront + CP + ERP</small></label>
						<label class="epm-demo-industry--erp"><input type="radio" name="epm_industry" value="erp_only"<?php echo $pref === 'erp_only' ? ' checked' : ''; ?>><span><i class="fa fa-university"></i>ERP only</span><small style="display:block;margin-top:6px;opacity:.85">No storefront — ERP / CRM / finance</small></label>
					</div>
					<p style="font-size:12px;color:var(--epm-muted)">ERP-only skips the e-commerce shop — ideal for back-office teams. More industries in Phase 3.</p>
					<div class="epm-demo-actions"><button type="button" class="epm-btn epm-btn--primary" id="epm-demo-next-industry">Continue →</button></div>
				</div>
				<div class="epm-demo-step" data-step="details">
					<div class="form-group"><label>Your name</label><input class="form-control" id="epm-demo-name" required autocomplete="name" /></div>
					<div class="form-group"><label>Work email</label><input class="form-control" type="email" id="epm-demo-email" required autocomplete="email" /></div>
					<div class="form-group"><label>Country <span style="color:var(--epm-cyan)">*</span></label>
						<select class="form-control" id="epm-demo-country" required>
							<option value="">— Select country —</option>
							<?php foreach ($demoCountries as $cc => $cname) { ?>
							<option value="<?php echo epc_ecomae_h($cc); ?>"<?php echo $cc === 'AE' ? ' selected' : ''; ?>><?php echo epc_ecomae_h($cname); ?></option>
							<?php } ?>
						</select>
					</div>
					<div class="form-group"><label>Phone number <span style="color:var(--epm-cyan)">*</span></label><input class="form-control" type="tel" id="epm-demo-phone" required autocomplete="tel" placeholder="+971 50 123 4567" /></div>
					<div class="form-group"><label>Company</label><input class="form-control" id="epm-demo-company" autocomplete="organization" /></div>
					<label style="font-size:13px;display:flex;gap:8px;align-items:flex-start;margin:12px 0">
						<input type="checkbox" id="epm-demo-terms" value="1" />
						<span>I agree this is a <strong>sandbox demo only</strong> — not production. Demo may be deleted after <?php echo $days; ?> days. One demo per email per 24h.</span>
					</label>
					<div class="epm-demo-actions">
						<button type="button" class="epm-btn epm-btn--ghost" id="epm-demo-back">← Back</button>
						<button type="button" class="epm-btn epm-btn--primary" id="epm-demo-submit">Launch my demo</button>
					</div>
					<p id="epm-demo-status" style="font-size:13px;color:var(--epm-muted);margin-top:10px"></p>
				</div>
				<div class="epm-demo-step" data-step="done">
					<div class="epm-alert ok" id="epm-demo-success"></div>
				</div>
			</div>
		</div>
	</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_demo_layla_home_section(array $demo, $base)
{
	$days = (int) $demo['days'];
	$demoUrl = epc_ecomae_h($base . 'platform/demo');
	ob_start();
	?>
<section class="epm-wrap epm-layla-home" aria-labelledby="epm-layla-home-title">
	<div class="epm-layla-home__inner">
		<div class="epm-layla-home__stage">
			<?php echo epc_ecomae_demo_layla_stage_html($days, 'epm-layla-home'); ?>
		</div>
		<div>
			<div class="epm-badge"><i class="fa fa-magic"></i> AI demo wizard · <?php echo $days; ?> days free</div>
			<h2 class="epm-section-title" id="epm-layla-home-title" style="margin-top:8px">Meet Layla — launch your sandbox now</h2>
			<p class="epm-section-lead" style="margin-bottom:0">Answer a few questions and we provision an isolated tenant — full storefront or ERP-only. Auto parts, fashion, and ERP-only ready today.</p>
			<div class="epm-layla-home__pills">
				<a class="epm-layla-pill" href="<?php echo $demoUrl; ?>?industry=auto_parts"><i class="fa fa-car"></i> Auto spare parts</a>
				<a class="epm-layla-pill" href="<?php echo $demoUrl; ?>?industry=fashion"><i class="fa fa-shopping-bag"></i> Fashion retail</a>
				<a class="epm-layla-pill" href="<?php echo $demoUrl; ?>?industry=erp_only"><i class="fa fa-university"></i> ERP only</a>
			</div>
			<div class="epm-layla-home__cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo $demoUrl; ?>"><i class="fa fa-play-circle"></i> Start AI demo wizard</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/pricing">View pricing</a>
			</div>
		</div>
	</div>
</section>
	<?php
	return ob_get_clean();
}

function epc_ecomae_demo_layla_splash_html(array $demo, $base)
{
	$days = (int) $demo['days'];
	$demoUrl = epc_ecomae_h($base . 'platform/demo');
	ob_start();
	?>
<div id="epc-layla-splash" class="epc-layla-splash epc-layla-splash--hidden" role="dialog" aria-modal="true" aria-labelledby="epc-layla-splash-title">
	<div class="epc-layla-splash__card">
		<button type="button" class="epc-layla-splash__close" id="epc-layla-splash-close" aria-label="Close Layla demo">&times;</button>
		<div class="epc-layla-splash__grid">
			<div class="epc-layla-splash__stage">
				<?php echo epc_ecomae_demo_layla_stage_html($days, 'epm-layla-splash'); ?>
			</div>
			<div class="epc-layla-splash__copy">
				<div class="epm-badge"><i class="fa fa-magic"></i> AI demo wizard · <?php echo $days; ?> days free</div>
				<h2 id="epc-layla-splash-title">Meet Layla — your ECOM AE demo guide</h2>
				<p>Watch Layla walk you through a live <?php echo $days; ?>-day sandbox on isolated MySQL — full commerce or ERP-only (no storefront).</p>
				<div class="epc-layla-splash__cta">
					<a class="epm-btn epm-btn--primary" href="<?php echo $demoUrl; ?>"><i class="fa fa-play-circle"></i> Start demo wizard</a>
					<button type="button" class="epm-btn epm-btn--ghost" id="epc-layla-splash-later">Maybe later</button>
				</div>
			</div>
		</div>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_demo_layla_footer_widget_html(array $demo, $base)
{
	$days = (int) $demo['days'];
	$demoUrl = epc_ecomae_h($base . 'platform/demo');
	$avatarMini = epc_ecomae_demo_layla_avatar_inline();
	ob_start();
	?>
<div id="epc-layla-footer-widget" class="epc-layla-footer-widget epc-layla-footer-widget--hidden" aria-live="polite">
	<button type="button" class="epc-layla-footer-widget__launcher" id="epc-layla-footer-launcher" aria-expanded="false" aria-controls="epc-layla-footer-panel" title="Layla — AI demo wizard">
		<span class="epc-layla-footer-widget__launcher-avatar" aria-hidden="true"><?php echo $avatarMini; ?></span>
		<span>Layla · AI demo</span>
		<span class="epc-layla-footer-widget__live">Live</span>
	</button>
	<div class="epc-layla-footer-widget__panel epc-layla-footer-widget__panel--hidden" id="epc-layla-footer-panel" role="dialog" aria-label="Layla AI demo wizard">
		<div class="epc-layla-footer-widget__head">
			<div>
				<strong>Layla · AI demo wizard</strong>
				<span><?php echo $days; ?>-day sandbox · commerce or ERP-only</span>
			</div>
			<button type="button" class="epc-layla-footer-widget__head-close" id="epc-layla-footer-close" aria-label="Close panel">&times;</button>
		</div>
		<div class="epc-layla-footer-widget__body">
			<?php echo epc_ecomae_demo_layla_stage_html($days, 'epm-layla-widget'); ?>
			<div class="epc-layla-footer-widget__actions">
				<a class="epm-btn epm-btn--primary" href="<?php echo $demoUrl; ?>"><i class="fa fa-play-circle"></i> Open full wizard</a>
				<button type="button" class="epm-btn epm-btn--ghost" id="epc-layla-footer-reopen-splash">Replay intro</button>
			</div>
		</div>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_demo_layla_marketing_scripts($demoDays = 3, $base = '/', $enableSplash = false)
{
	$splash = $enableSplash ? 'true' : 'false';
	ob_start();
	?>
<script>
(function(){
	var SPLASH_KEY='epc_layla_splash_dismissed';
	var splashEnabled=<?php echo $splash; ?>;
	var splash=document.getElementById('epc-layla-splash');
	var footer=document.getElementById('epc-layla-footer-widget');
	var splashClose=document.getElementById('epc-layla-splash-close');
	var splashLater=document.getElementById('epc-layla-splash-later');
	var footerLauncher=document.getElementById('epc-layla-footer-launcher');
	var footerPanel=document.getElementById('epc-layla-footer-panel');
	var footerClose=document.getElementById('epc-layla-footer-close');
	var footerReopen=document.getElementById('epc-layla-footer-reopen-splash');
	function dismissed(){try{return localStorage.getItem(SPLASH_KEY)==='1';}catch(e){return false;}}
	function setDismissed(){
		try{localStorage.setItem(SPLASH_KEY,'1');}catch(e){}
		showFooter();
		hideSplash();
	}
	function showSplash(){
		if(!splash)return;
		splash.classList.remove('epc-layla-splash--hidden');
		document.documentElement.style.overflow='hidden';
		if(window.epcLaylaEnableVoice)window.epcLaylaEnableVoice();
	}
	function hideSplash(){
		if(!splash)return;
		splash.classList.add('epc-layla-splash--hidden');
		document.documentElement.style.overflow='';
	}
	function showFooter(){
		if(footer)footer.classList.remove('epc-layla-footer-widget--hidden');
	}
	function setFooterOpen(open){
		if(!footerPanel||!footerLauncher)return;
		footerPanel.classList.toggle('epc-layla-footer-widget__panel--hidden',!open);
		footer.classList.toggle('epc-layla-footer-widget--open',open);
		footerLauncher.setAttribute('aria-expanded',open?'true':'false');
		if(open&&window.epcLaylaEnableVoice)window.epcLaylaEnableVoice();
	}
	if(splashClose)splashClose.addEventListener('click',setDismissed);
	if(splashLater)splashLater.addEventListener('click',setDismissed);
	if(splash){
		splash.addEventListener('click',function(e){
			if(e.target===splash)setDismissed();
		});
	}
	if(footerLauncher){
		footerLauncher.addEventListener('click',function(){
			var open=footerPanel&&!footerPanel.classList.contains('epc-layla-footer-widget__panel--hidden');
			setFooterOpen(!open);
		});
	}
	if(footerClose)footerClose.addEventListener('click',function(){setFooterOpen(false);});
	if(footerReopen){
		footerReopen.addEventListener('click',function(){
			setFooterOpen(false);
			showSplash();
		});
	}
	if(splashEnabled&&!dismissed()){
		showSplash();
	}else{
		showFooter();
	}
})();
</script>
	<?php
	return ob_get_clean();
}

function epc_ecomae_demo_layla_scripts($demoDays = 3, $prefIndustry = 'auto_parts', $marketingChrome = false)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
	$days = (int) $demoDays;
	$pref = preg_replace('/[^a-z0-9_]/', '', (string) $prefIndustry);
	if ($pref === '') {
		$pref = 'auto_parts';
	}
	ob_start();
	?>
<script>
(function(){
	var prefixes=['epm-layla','epm-layla-splash','epm-layla-widget'];
	var synth=window.speechSynthesis;
	var voiceOn=<?php echo $marketingChrome ? 'true' : 'false'; ?>;
	var laylaVoice=null;
	var FEMALE=/samantha|karen|victoria|zira|susan|fiona|kate|tessa|moira|female|google.*english.*female|microsoft.*zira|microsoft.*jenny|microsoft.*aria|jenny|aria|allison|ava|sarah|linda|heather|serena|salli|joanna|ivy|nicole/i;
	var MALE=/david|mark|alex|daniel|fred|james|tom|male|google.*english.*male|microsoft.*david|microsoft.*mark|guy|ryan|brian|matthew/i;
	function qs(id){return document.getElementById(id);}
	function enVoices(){
		if(!synth)return [];
		var vs=synth.getVoices();
		var en=vs.filter(function(v){return/^en(-US)?/i.test(v.lang);});
		if(!en.length)en=vs.filter(function(v){return/en/i.test(v.lang);});
		return en.length?en:vs;
	}
	function pickVoice(){
		if(!synth)return null;
		if(laylaVoice)return laylaVoice;
		var en=enVoices();
		var i,v;
		for(i=0;i<en.length;i++){
			v=en[i];
			if(FEMALE.test(v.name)&&!MALE.test(v.name)){laylaVoice=v;return v;}
		}
		for(i=0;i<en.length;i++){
			v=en[i];
			if(!MALE.test(v.name)){laylaVoice=v;return v;}
		}
		laylaVoice=en[0]||null;
		return laylaVoice;
	}
	if(synth){synth.onvoiceschanged=function(){laylaVoice=null;pickVoice();};}
	window.epcLaylaEnableVoice=function(){
		voiceOn=true;
		prefixes.forEach(function(p){
			var btn=qs(p+'-voice');
			if(btn){
				btn.classList.add('is-on');
				btn.setAttribute('aria-pressed','true');
				btn.innerHTML='<i class="fa fa-volume-up"></i> Voice on';
			}
		});
		pickVoice();
	};
	function setAvatarSpeaking(on){
		prefixes.forEach(function(p){
			var av=qs(p+'-avatar');
			if(av)av.classList.toggle('is-speaking',!!on);
		});
	}
	function typeLine(el,text,cb){
		if(!el){if(cb)cb();return;}
		el.textContent='';
		var i=0,cursor=document.createElement('span');
		cursor.className='epm-layla-speech__cursor';
		el.appendChild(cursor);
		setAvatarSpeaking(true);
		function tick(){
			if(i<=text.length){
				el.firstChild&&el.firstChild.nodeType===3&&el.removeChild(el.firstChild);
				el.insertBefore(document.createTextNode(text.slice(0,i)),cursor);
				i++;
				window.setTimeout(tick,22+Math.random()*18);
			}else{
				setAvatarSpeaking(false);
				if(cb)cb();
			}
		}
		tick();
	}
	function speakText(text){
		if(!voiceOn||!synth||!text)return;
		try{synth.cancel();var u=new SpeechSynthesisUtterance(text);u.rate=1.02;u.pitch=1.05;var v=pickVoice();if(v)u.voice=v;u.onstart=function(){setAvatarSpeaking(true);};u.onend=u.onerror=function(){setAvatarSpeaking(false);};synth.speak(u);}catch(e){}
	}
	function initPitch(prefix){
		var textEl=qs(prefix+'-text');
		var voiceBtn=qs(prefix+'-voice');
		if(!textEl)return;
		var lines=[];
		try{lines=JSON.parse(textEl.getAttribute('data-lines')||'[]');}catch(e){lines=[];}
		if(!lines.length)return;
		var idx=0,paused=false;
		function cycle(){
			if(paused||document.hidden)return;
			var line=lines[idx%lines.length];
			idx++;
			typeLine(textEl,line,function(){
				speakText(line);
				window.setTimeout(cycle,4800);
			});
		}
		cycle();
		if(voiceBtn){
			voiceBtn.classList.toggle('is-on',voiceOn);
			voiceBtn.setAttribute('aria-pressed',voiceOn?'true':'false');
			voiceBtn.innerHTML=voiceOn?'<i class="fa fa-volume-up"></i> Voice on':'<i class="fa fa-volume-off"></i> Voice off';
			voiceBtn.addEventListener('click',function(){
				voiceOn=!voiceOn;
				prefixes.forEach(function(p){
					var b=qs(p+'-voice');
					if(!b)return;
					b.classList.toggle('is-on',voiceOn);
					b.setAttribute('aria-pressed',voiceOn?'true':'false');
					b.innerHTML=voiceOn?'<i class="fa fa-volume-up"></i> Voice on':'<i class="fa fa-volume-off"></i> Voice off';
				});
				if(voiceOn)pickVoice();
				if(!voiceOn&&synth)synth.cancel();
			});
		}
		document.addEventListener('visibilitychange',function(){paused=document.hidden;});
	}
	prefixes.forEach(initPitch);
	/* Wizard (demo page only) */
	var chat=qs('epm-demo-chat');
	if(!chat)return;
	var steps=document.querySelectorAll('.epm-demo-step');
	var progress=qs('epm-demo-progress');
	var progressBar=qs('epm-demo-progress-bar');
	var progressLabel=qs('epm-demo-progress-label');
	var industry='<?php echo epc_ecomae_h($pref); ?>';
	var mainText=qs('epm-layla-text');
	function laylaSay(text){
		if(mainText){
			var lines=[];
			try{lines=JSON.parse(mainText.getAttribute('data-lines')||'[]');}catch(e){}
			mainText.setAttribute('data-lines',JSON.stringify([text].concat(lines.slice(0,4))));
			typeLine(mainText,text,function(){speakText(text);});
		}else{speakText(text);}
	}
	function say(text,isUser){
		var d=document.createElement('div');
		d.className='epm-demo-msg epm-demo-msg--'+(isUser?'user':'bot');
		d.textContent=text;
		chat.appendChild(d);
		chat.scrollTop=chat.scrollHeight;
		if(!isUser)laylaSay(text);
	}
	function go(step){steps.forEach(function(s){s.classList.toggle('active',s.getAttribute('data-step')===step);});}
	function setProgress(pct,label){
		if(!progress||!progressBar)return;
		progress.classList.add('is-active');
		progress.setAttribute('aria-hidden','false');
		progressBar.style.width=Math.max(0,Math.min(100,pct))+'%';
		if(progressLabel){
			if(label)progressLabel.textContent=label;
			progressLabel.style.display=pct>0?'block':'none';
		}
	}
	var intro=document.createElement('div');
	intro.className='epm-demo-msg epm-demo-msg--bot';
	intro.textContent="Hi! I'm Layla. Which industry should I set up for your <?php echo $days; ?>-day sandbox?";
	chat.appendChild(intro);
	var nextBtn=qs('epm-demo-next-industry');
	if(nextBtn)nextBtn.addEventListener('click',function(){
		var r=document.querySelector('input[name="epm_industry"]:checked');
		if(!r){say('Please pick auto parts, fashion, or ERP only.',false);return;}
		industry=r.value;
		var industryMsg=r.value==='auto_parts'?'Auto spare parts — eParts Cart style!':r.value==='fashion'?'Fashion retail — Namshi style!':'ERP only — finance, CRM, and operations (no storefront).';
		say(industryMsg,false);
		say('Tell me your name, work email, country, phone number, and company — country and phone are required.',false);
		setProgress(25,'Industry selected — almost there…');
		go('details');
	});
	var backBtn=qs('epm-demo-back');
	if(backBtn)backBtn.addEventListener('click',function(){setProgress(10,'Choose your industry');go('industry');});
	var submitBtn=qs('epm-demo-submit');
	var demoDial=<?php echo json_encode(epc_countries_dial_codes()); ?>;
	var demoCountry=qs('epm-demo-country');
	if(demoCountry){
		demoCountry.addEventListener('change',function(){
			var ph=qs('epm-demo-phone');
			var cc=demoCountry.value||'';
			var p=demoDial[cc]?'+'+demoDial[cc]:'';
			if(ph&&p&&!(ph.value||'').trim())ph.value=p+' ';
			if(ph)ph.placeholder=p?('e.g. '+p+' 50 123 4567'):'+971 50 123 4567';
		});
		demoCountry.dispatchEvent(new Event('change'));
	}
	if(submitBtn)submitBtn.addEventListener('click',function(){
		var name=qs('epm-demo-name').value.trim();
		var email=qs('epm-demo-email').value.trim();
		var country=(qs('epm-demo-country')||{}).value||'';
		var phone=qs('epm-demo-phone').value.trim();
		var company=qs('epm-demo-company').value.trim();
		var terms=qs('epm-demo-terms').checked;
		var status=qs('epm-demo-status');
		var phoneDigits=phone.replace(/\D/g,'');
		if(!name||!email){status.textContent='Name and email required.';return;}
		if(!country){status.textContent='Please select your country.';return;}
		if(phoneDigits.length<7){status.textContent='Valid phone number required (7+ digits).';return;}
		if(!terms){status.textContent='Please accept the demo terms.';return;}
		say(name+' · '+email+' · '+phone+(company?' · '+company:''),true);
		status.textContent='Provisioning sandbox…';
		setProgress(40,'Creating isolated database…');
		var btn=this;btn.disabled=true;
		var body=new URLSearchParams({contact_name:name,contact_email:email,contact_phone:phone,company:company,country_code:country,industry_code:industry,terms:'1'});
		var tick=40;
		var erpOnly=industry==='erp_only';
		var timer=window.setInterval(function(){
			tick=Math.min(92,tick+4);
			var lbl=erpOnly
				?(tick<55?'Provisioning ERP shell…':tick<75?'Seeding finance & CRM modules…':tick<88?'Sending ERP login email…':'Finalising tenant…')
				:(tick<55?'Cloning storefront schema…':tick<75?'Seeding industry catalogue…':tick<88?'Sending login email…':'Finalising tenant…');
			setProgress(tick,lbl);
		},900);
		fetch('/epc-demo-provision-public.php',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:body})
		.then(function(r){
			return r.text().then(function(t){
				var j;
				try{j=t?JSON.parse(t):null;}catch(e){j=null;}
				if(!j)throw new Error(t||('Server error HTTP '+r.status));
				if(!r.ok&&!j.ok)j._http=r.status;
				return j;
			});
		})
		.then(function(j){
			window.clearInterval(timer);
			btn.disabled=false;
			if(j.ok){
				setProgress(100,'Demo ready!');
				var doneMsg=(j.demo_erp_only||industry==='erp_only')
					?'Done! Check your email for ERP demo CP login — no storefront on this path.'
					:'Done! Check your email for storefront, CP, and ERP links.';
				say(doneMsg,false);
				var ok=qs('epm-demo-success');
				if(ok)ok.textContent=j.message||'Demo ready — check your email.';
				go('done');
				status.textContent='';
			}else{
				setProgress(0,'');
				if(progress)progress.classList.remove('is-active');
				if(progressLabel)progressLabel.style.display='none';
				say(j.message||'Could not provision demo.',false);
				status.textContent=j.message||'Error';
			}
		})
		.catch(function(err){
			window.clearInterval(timer);
			btn.disabled=false;
			setProgress(0,'');
			if(progress)progress.classList.remove('is-active');
			if(progressLabel)progressLabel.style.display='none';
			var msg=(err&&err.message)?String(err.message):'Network error — try again.';
			status.textContent=msg.indexOf('Network')===0?'Network error — try again.':msg;
			say(msg.indexOf('Could not')===0||msg.indexOf('Server')===0?msg:'Network error — please retry in a moment.',false);
		});
	});
})();
</script>
	<?php
	return ob_get_clean();
}
