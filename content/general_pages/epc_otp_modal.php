<?php
/**
 * Reusable Skywork-style OTP modal — 6 individual digit boxes, auto-advance,
 * resend with 60 s cooldown, mobile-friendly.
 *
 * Usage (PHP):
 *   require_once '.../epc_otp_modal.php';
 *   echo epc_otp_modal_render([
 *     'modal_id'    => 'epc_otp_reg',        // unique per page (default: epc_otp_modal)
 *     'context'     => 'storefront',          // 'cp' or 'storefront'
 *     'tenant_key'  => '',
 *     'send_url'    => '/epc-auth-send-code.php',
 *     'verify_url'  => '/epc-auth-verify-code.php',  // or /epc-auth-otp-verify-only.php
 *     'return_url'  => '/en/',
 *     'logo_url'    => '/design/logo.png',    // optional
 *     'label'       => 'epartscart',          // brand name shown in sub-heading
 *     'on_success'  => 'location.href=data.redirect', // JS run on verify OK; data = server JSON
 *     'verify_only' => false,                 // true = no session/login (for reg email verify)
 *   ]);
 *
 * JS trigger (from any script on the page):
 *   EpcOtpModal['epc_otp_reg'].open('user@example.com');
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_otp_modal_styles_once(): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	echo '<style id="epc-otp-modal-styles">
/* ── OTP modal overlay ── */
.epc-otp-overlay{position:fixed;inset:0;background:rgba(0,0,0,.55);z-index:99999;display:flex;align-items:center;justify-content:center;opacity:0;pointer-events:none;transition:opacity .2s}
.epc-otp-overlay.is-open{opacity:1;pointer-events:auto}
.epc-otp-card{background:#fff;border-radius:16px;padding:36px 32px 28px;max-width:420px;width:calc(100% - 32px);box-shadow:0 24px 60px rgba(0,0,0,.18);text-align:center;transform:translateY(12px);transition:transform .22s;position:relative}
.epc-otp-overlay.is-open .epc-otp-card{transform:translateY(0)}
.epc-otp-close{position:absolute;top:14px;right:16px;background:none;border:none;font-size:20px;line-height:1;cursor:pointer;color:#999;padding:4px}
.epc-otp-close:hover{color:#333}
.epc-otp-logo{max-height:48px;max-width:160px;margin:0 auto 14px;display:block}
.epc-otp-heading{font-size:20px;font-weight:700;color:#111;margin:0 0 8px}
.epc-otp-subtext{font-size:14px;color:#555;margin:0 0 20px;line-height:1.5}
.epc-otp-subtext strong{color:#222}
/* ── 6-box row ── */
.epc-otp-boxes{display:flex;gap:8px;justify-content:center;margin:0 0 20px}
.epc-otp-box{width:46px;height:54px;border:2px solid #d1d5db;border-radius:10px;font-size:22px;font-weight:700;text-align:center;color:#111;outline:none;transition:border-color .15s,box-shadow .15s;-moz-appearance:textfield}
.epc-otp-box::-webkit-outer-spin-button,.epc-otp-box::-webkit-inner-spin-button{-webkit-appearance:none;margin:0}
.epc-otp-box:focus{border-color:#2563eb;box-shadow:0 0 0 3px rgba(37,99,235,.15)}
.epc-otp-box.is-filled{border-color:#10b981}
.epc-otp-box.is-error{border-color:#ef4444;animation:epc-shake .3s}
@keyframes epc-shake{0%,100%{transform:translateX(0)}25%{transform:translateX(-4px)}75%{transform:translateX(4px)}}
/* ── Continue button ── */
.epc-otp-btn{display:block;width:100%;padding:13px;border:none;border-radius:10px;background:#2563eb;color:#fff;font-size:15px;font-weight:700;cursor:pointer;transition:background .15s,opacity .15s}
.epc-otp-btn:hover:not(:disabled){background:#1d4ed8}
.epc-otp-btn:disabled{opacity:.5;cursor:not-allowed}
/* ── Status message ── */
.epc-otp-msg{min-height:18px;font-size:13px;margin:10px 0 0;text-align:center}
.epc-otp-msg.is-ok{color:#059669}
.epc-otp-msg.is-err{color:#dc2626}
/* ── Resend ── */
.epc-otp-resend{font-size:13px;color:#666;margin:14px 0 0;line-height:1.5}
.epc-otp-resend-btn{background:none;border:none;padding:0;color:#2563eb;font-weight:600;cursor:pointer;font-size:13px;text-decoration:underline}
.epc-otp-resend-btn:disabled{color:#999;cursor:not-allowed;text-decoration:none}
.epc-otp-resend-timer{font-weight:600;color:#888}
/* ── Mobile ── */
@media(max-width:480px){.epc-otp-card{padding:28px 18px 22px}.epc-otp-box{width:40px;height:48px;font-size:18px}}
</style>';
}

/**
 * Render the OTP modal HTML + JS.
 *
 * @param  array<string,mixed> $cfg
 * @return string HTML to echo into the page
 */
function epc_otp_modal_render(array $cfg = []): string
{
	ob_start();
	epc_otp_modal_styles_once();

	$id         = preg_replace('/[^a-z0-9_]/', '_', strtolower((string) ($cfg['modal_id'] ?? 'epc_otp_modal')));
	$context    = (string) ($cfg['context'] ?? 'storefront');
	$tenantKey  = (string) ($cfg['tenant_key'] ?? '');
	$sendUrl    = (string) ($cfg['send_url'] ?? '/epc-auth-send-code.php');
	$verifyUrl  = (string) ($cfg['verify_url'] ?? '/epc-auth-verify-code.php');
	$returnUrl  = (string) ($cfg['return_url'] ?? '');
	$logoUrl    = (string) ($cfg['logo_url'] ?? '');
	$label      = (string) ($cfg['label'] ?? 'epartscart');
	$onSuccess  = (string) ($cfg['on_success'] ?? '');
	$verifyOnly = !empty($cfg['verify_only']);

	$eid = htmlspecialchars($id, ENT_QUOTES, 'UTF-8');

	if ($onSuccess === '') {
		if ($verifyOnly) {
			$onSuccess = 'if(typeof epcOtpOnSuccess==="function")epcOtpOnSuccess(data);';
		} else {
			$onSuccess = 'if(data.redirect){location.href=data.redirect;}';
		}
	}

	// ── Logo HTML ──
	$logoHtml = '';
	if ($logoUrl !== '') {
		$logoHtml = '<img src="' . htmlspecialchars($logoUrl, ENT_QUOTES, 'UTF-8') . '" alt="' . htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '" class="epc-otp-logo">';
	}

	?>
<div class="epc-otp-overlay" id="<?php echo $eid; ?>_overlay" role="dialog" aria-modal="true" aria-labelledby="<?php echo $eid; ?>_heading">
  <div class="epc-otp-card">
    <button class="epc-otp-close" id="<?php echo $eid; ?>_close" aria-label="Close">&times;</button>
    <?php echo $logoHtml; ?>
    <h2 class="epc-otp-heading" id="<?php echo $eid; ?>_heading">Enter verification code</h2>
    <p class="epc-otp-subtext" id="<?php echo $eid; ?>_subtext">We&rsquo;ve sent a verification code to <strong id="<?php echo $eid; ?>_email_display"></strong>.<br>The code is valid for 5&nbsp;minutes.</p>
    <div class="epc-otp-boxes" id="<?php echo $eid; ?>_boxes">
      <input type="text" maxlength="1" inputmode="numeric" pattern="[0-9]" class="epc-otp-box" autocomplete="one-time-code" aria-label="Digit 1">
      <input type="text" maxlength="1" inputmode="numeric" pattern="[0-9]" class="epc-otp-box" autocomplete="off" aria-label="Digit 2">
      <input type="text" maxlength="1" inputmode="numeric" pattern="[0-9]" class="epc-otp-box" autocomplete="off" aria-label="Digit 3">
      <input type="text" maxlength="1" inputmode="numeric" pattern="[0-9]" class="epc-otp-box" autocomplete="off" aria-label="Digit 4">
      <input type="text" maxlength="1" inputmode="numeric" pattern="[0-9]" class="epc-otp-box" autocomplete="off" aria-label="Digit 5">
      <input type="text" maxlength="1" inputmode="numeric" pattern="[0-9]" class="epc-otp-box" autocomplete="off" aria-label="Digit 6">
    </div>
    <button class="epc-otp-btn" id="<?php echo $eid; ?>_btn" disabled>Continue</button>
    <p class="epc-otp-msg" id="<?php echo $eid; ?>_msg" aria-live="polite"></p>
    <p class="epc-otp-resend">
      Didn&rsquo;t receive the code? Please check your spam folder.&nbsp;
      <button class="epc-otp-resend-btn" id="<?php echo $eid; ?>_resend" disabled>Resend <span class="epc-otp-resend-timer" id="<?php echo $eid; ?>_timer"></span></button>
    </p>
  </div>
</div>
<script>
(function(){
'use strict';
window.EpcOtpModal=window.EpcOtpModal||{};
var ID=<?php echo json_encode($id); ?>;
var CTX=<?php echo json_encode($context); ?>;
var TENANT=<?php echo json_encode($tenantKey); ?>;
var SEND_URL=<?php echo json_encode($sendUrl); ?>;
var VERIFY_URL=<?php echo json_encode($verifyUrl); ?>;
var RETURN_URL=<?php echo json_encode($returnUrl); ?>;
var overlay=document.getElementById(ID+'_overlay');
var boxWrap=document.getElementById(ID+'_boxes');
var boxes=boxWrap?Array.from(boxWrap.querySelectorAll('.epc-otp-box')):[];
var btn=document.getElementById(ID+'_btn');
var msgEl=document.getElementById(ID+'_msg');
var resendBtn=document.getElementById(ID+'_resend');
var timerEl=document.getElementById(ID+'_timer');
var emailDisplay=document.getElementById(ID+'_email_display');
var closeBtn=document.getElementById(ID+'_close');
var currentEmail='';
var resendTimer=null;

function showMsg(t,ok){
  if(!msgEl)return;
  msgEl.textContent=t;
  msgEl.className='epc-otp-msg'+(ok?' is-ok':' is-err');
}
function clearMsg(){if(msgEl){msgEl.textContent='';msgEl.className='epc-otp-msg';}}

function getCode(){return boxes.map(function(b){return b.value;}).join('');}

function checkComplete(){
  var code=getCode();
  if(btn)btn.disabled=(code.length!==6);
}

function markBoxError(){
  boxes.forEach(function(b){b.classList.add('is-error');});
  setTimeout(function(){boxes.forEach(function(b){b.classList.remove('is-error');});},400);
}

function clearBoxes(){
  boxes.forEach(function(b){b.value='';b.classList.remove('is-filled','is-error');});
  if(boxes[0])boxes[0].focus();
  checkComplete();
}

// ── Auto-advance behaviour ──
boxes.forEach(function(box,i){
  box.addEventListener('input',function(){
    var v=box.value.replace(/[^0-9]/g,'');
    box.value=v.slice(-1);
    box.classList.toggle('is-filled',box.value!=='');
    if(box.value&&i<boxes.length-1)boxes[i+1].focus();
    checkComplete();
  });
  box.addEventListener('keydown',function(e){
    if(e.key==='Backspace'&&!box.value&&i>0){boxes[i-1].focus();boxes[i-1].value='';boxes[i-1].classList.remove('is-filled');checkComplete();}
    if(e.key==='ArrowLeft'&&i>0)boxes[i-1].focus();
    if(e.key==='ArrowRight'&&i<boxes.length-1)boxes[i+1].focus();
    if(e.key==='Enter'){e.preventDefault();if(getCode().length===6)doVerify();}
  });
  box.addEventListener('paste',function(e){
    e.preventDefault();
    var paste=((e.clipboardData||window.clipboardData).getData('text')||'').replace(/[^0-9]/g,'');
    paste.split('').slice(0,6).forEach(function(ch,j){
      if(boxes[i+j]){boxes[i+j].value=ch;boxes[i+j].classList.add('is-filled');}
    });
    var next=Math.min(i+paste.length,boxes.length-1);
    boxes[next].focus();
    checkComplete();
  });
  box.addEventListener('focus',function(){box.select();});
});

// ── Resend cooldown ──
function startResendTimer(secs){
  if(resendBtn)resendBtn.disabled=true;
  var remaining=secs||60;
  function tick(){
    if(timerEl)timerEl.textContent='('+remaining+'s)';
    if(remaining<=0){
      if(resendBtn)resendBtn.disabled=false;
      if(timerEl)timerEl.textContent='';
      return;
    }
    remaining--;
    resendTimer=setTimeout(tick,1000);
  }
  tick();
}

// ── Send OTP ──
function doSend(email,isResend){
  currentEmail=email||currentEmail;
  if(emailDisplay)emailDisplay.textContent=currentEmail;
  clearMsg();
  clearBoxes();
  if(isResend){showMsg('Sending…',true);}
  var body={email:currentEmail,tenant_key:TENANT,context:CTX};
  if(RETURN_URL)body.return_url=RETURN_URL;
  fetch(SEND_URL,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)})
  .then(function(r){return r.json();})
  .then(function(d){
    showMsg(d.message||'',!!d.ok);
    if(d.ok){startResendTimer(60);}
    else{if(resendBtn)resendBtn.disabled=false;}
  })
  .catch(function(){showMsg('Network error — please retry.',false);if(resendBtn)resendBtn.disabled=false;});
}

// ── Verify OTP ──
function doVerify(){
  var code=getCode();
  if(code.length!==6){markBoxError();return;}
  if(btn){btn.disabled=true;btn.textContent='Verifying…';}
  clearMsg();
  var body={email:currentEmail,code:code,tenant_key:TENANT,context:CTX};
  if(RETURN_URL)body.return_url=RETURN_URL;
  fetch(VERIFY_URL,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)})
  .then(function(r){return r.json();})
  .then(function(d){
    if(btn){btn.textContent='Continue';btn.disabled=false;}
    if(d.ok){
      clearMsg();
      close();
      var data=d;
      <?php echo $onSuccess; ?>
    } else {
      showMsg(d.message||'Invalid or expired code',false);
      markBoxError();
      clearBoxes();
    }
  })
  .catch(function(){
    if(btn){btn.textContent='Continue';btn.disabled=false;}
    showMsg('Network error — please retry.',false);
  });
}

// ── Open / Close ──
function open(email){
  currentEmail=email||'';
  if(emailDisplay)emailDisplay.textContent=currentEmail;
  clearBoxes();
  clearMsg();
  if(overlay)overlay.classList.add('is-open');
  doSend(currentEmail,false);
  setTimeout(function(){if(boxes[0])boxes[0].focus();},80);
}
function close(){
  if(overlay)overlay.classList.remove('is-open');
  if(resendTimer)clearTimeout(resendTimer);
}

// ── Event wiring ──
if(btn)btn.addEventListener('click',doVerify);
if(resendBtn)resendBtn.addEventListener('click',function(){doSend(currentEmail,true);});
if(closeBtn)closeBtn.addEventListener('click',close);
if(overlay)overlay.addEventListener('click',function(e){if(e.target===overlay)close();});
document.addEventListener('keydown',function(e){if(e.key==='Escape'&&overlay&&overlay.classList.contains('is-open'))close();});

// ── Expose API ──
window.EpcOtpModal[ID]={open:open,close:close,doSend:doSend};
})();
</script>
<?php
	return (string) ob_get_clean();
}
