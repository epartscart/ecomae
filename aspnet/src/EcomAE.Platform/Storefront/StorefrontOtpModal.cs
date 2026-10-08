using System.Text;
using EcomAE.Platform.Auth;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The six-box e-mail code modal (<c>epc_otp_modal_render()</c>) shown by the registration form, the storefront
/// e-mail sign-in widget and the CP modern login. One instance per page: the stylesheet is printed once, before the
/// first modal, as the PHP function's static flag does. The output is PHP's byte for byte.
/// </summary>
public sealed class StorefrontOtpModal
{
    public const string DefaultSendUrl = "/content/general_pages/epc_auth_api_send_code.php";
    public const string VerifyOnlyUrl = "/content/general_pages/epc_auth_api_verify_only.php";
    public const string VerifyCodeUrl = "/content/general_pages/epc_auth_api_verify_code.php";

    private const string Styles =
        "<style id=\"epc-otp-modal-styles\">\n"
        + "/* ── OTP modal overlay ── */\n"
        + ".epc-otp-overlay{position:fixed;inset:0;background:rgba(0,0,0,.55);z-index:99999;display:flex;align-items:center;justify-content:center;opacity:0;pointer-events:none;transition:opacity .2s}\n"
        + ".epc-otp-overlay.is-open{opacity:1;pointer-events:auto}\n"
        + ".epc-otp-card{background:#fff;border-radius:16px;padding:36px 32px 28px;max-width:420px;width:calc(100% - 32px);box-shadow:0 24px 60px rgba(0,0,0,.18);text-align:center;transform:translateY(12px);transition:transform .22s;position:relative}\n"
        + ".epc-otp-overlay.is-open .epc-otp-card{transform:translateY(0)}\n"
        + ".epc-otp-close{position:absolute;top:14px;right:16px;background:none;border:none;font-size:20px;line-height:1;cursor:pointer;color:#999;padding:4px}\n"
        + ".epc-otp-close:hover{color:#333}\n"
        + ".epc-otp-logo{max-height:48px;max-width:160px;margin:0 auto 14px;display:block}\n"
        + ".epc-otp-heading{font-size:20px;font-weight:700;color:#111;margin:0 0 8px}\n"
        + ".epc-otp-subtext{font-size:14px;color:#555;margin:0 0 20px;line-height:1.5}\n"
        + ".epc-otp-subtext strong{color:#222}\n"
        + "/* ── 6-box row ── */\n"
        + ".epc-otp-boxes{display:flex;gap:8px;justify-content:center;margin:0 0 20px}\n"
        + ".epc-otp-box{width:46px;height:54px;border:2px solid #d1d5db;border-radius:10px;font-size:22px;font-weight:700;text-align:center;color:#111;outline:none;transition:border-color .15s,box-shadow .15s;-moz-appearance:textfield}\n"
        + ".epc-otp-box::-webkit-outer-spin-button,.epc-otp-box::-webkit-inner-spin-button{-webkit-appearance:none;margin:0}\n"
        + ".epc-otp-box:focus{border-color:#2563eb;box-shadow:0 0 0 3px rgba(37,99,235,.15)}\n"
        + ".epc-otp-box.is-filled{border-color:#10b981}\n"
        + ".epc-otp-box.is-error{border-color:#ef4444;animation:epc-shake .3s}\n"
        + "@keyframes epc-shake{0%,100%{transform:translateX(0)}25%{transform:translateX(-4px)}75%{transform:translateX(4px)}}\n"
        + "/* ── Continue button ── */\n"
        + ".epc-otp-btn{display:block;width:100%;padding:13px;border:none;border-radius:10px;background:#2563eb;color:#fff;font-size:15px;font-weight:700;cursor:pointer;transition:background .15s,opacity .15s}\n"
        + ".epc-otp-btn:hover:not(:disabled){background:#1d4ed8}\n"
        + ".epc-otp-btn:disabled{opacity:.5;cursor:not-allowed}\n"
        + "/* ── Status message ── */\n"
        + ".epc-otp-msg{min-height:18px;font-size:13px;margin:10px 0 0;text-align:center}\n"
        + ".epc-otp-msg.is-ok{color:#059669}\n"
        + ".epc-otp-msg.is-err{color:#dc2626}\n"
        + "/* ── Resend ── */\n"
        + ".epc-otp-resend{font-size:13px;color:#666;margin:14px 0 0;line-height:1.5}\n"
        + ".epc-otp-resend-btn{background:none;border:none;padding:0;color:#2563eb;font-weight:600;cursor:pointer;font-size:13px;text-decoration:underline}\n"
        + ".epc-otp-resend-btn:disabled{color:#999;cursor:not-allowed;text-decoration:none}\n"
        + ".epc-otp-resend-timer{font-weight:600;color:#888}\n"
        + "/* ── Mobile ── */\n"
        + "@media(max-width:480px){.epc-otp-card{padding:28px 18px 22px}.epc-otp-box{width:40px;height:48px;font-size:18px}}\n"
        + "</style>";

    private bool _stylesDone;

    /// <summary>PHP <c>epc_otp_modal_styles_once()</c>.</summary>
    public string StylesOnce()
    {
        if (_stylesDone)
        {
            return string.Empty;
        }

        _stylesDone = true;
        return Styles;
    }

    /// <summary>PHP <c>epc_otp_modal_render($cfg)</c>; a null option is a key missing from <c>$cfg</c>.</summary>
    public string Render(Options options)
    {
        var styles = StylesOnce();
        var id = ModalId(options.ModalId ?? "epc_otp_modal");
        var context = options.Context ?? "storefront";
        var tenantKey = options.TenantKey ?? string.Empty;
        var sendUrl = options.SendUrl ?? DefaultSendUrl;
        var verifyUrl = options.VerifyUrl ?? (options.VerifyOnly ? VerifyOnlyUrl : VerifyCodeUrl);
        var returnUrl = options.ReturnUrl ?? string.Empty;
        var logoUrl = options.LogoUrl ?? string.Empty;
        var label = options.Label ?? "epartscart";
        var onSuccess = options.OnSuccess ?? string.Empty;
        var eid = StorefrontSupplierLpoNotifier.H(id);
        if (onSuccess.Length == 0)
        {
            onSuccess = options.VerifyOnly
                ? "if(typeof epcOtpOnSuccess===\"function\")epcOtpOnSuccess(data);"
                : "if(data.redirect){location.href=data.redirect;}";
        }

        var logoHtml = logoUrl.Length == 0
            ? string.Empty
            : "<img src=\"" + StorefrontSupplierLpoNotifier.H(logoUrl) + "\" alt=\"" + StorefrontSupplierLpoNotifier.H(label) + "\" class=\"epc-otp-logo\">";

        return styles + Markup(eid, logoHtml, onSuccess, id, context, tenantKey, sendUrl, verifyUrl, returnUrl);
    }

    /// <summary><c>preg_replace('/[^a-z0-9_]/', '_', strtolower($id))</c>, byte-wise like PHP.</summary>
    public static string ModalId(string raw)
    {
        var sb = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(raw))
        {
            var c = b is >= (byte)'A' and <= (byte)'Z' ? (char)(b + 32) : (char)b;
            sb.Append(c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' ? c : '_');
        }

        return sb.ToString();
    }

    private static string Json(string value) => OAuthStart.PhpJsonString(value);

    private static string Markup(
        string eid,
        string logoHtml,
        string onSuccess,
        string id,
        string context,
        string tenantKey,
        string sendUrl,
        string verifyUrl,
        string returnUrl)
        =>
            "<div class=\"epc-otp-overlay\" id=\""
            + eid
            + "_overlay\" role=\"dialog\" aria-modal=\"true\" aria-labelledby=\""
            + eid
            + "_heading\">\n"
            + "  <div class=\"epc-otp-card\">\n"
            + "    <button class=\"epc-otp-close\" id=\""
            + eid
            + "_close\" aria-label=\"Close\">&times;</button>\n"
            + "    "
            + logoHtml
            + "    <h2 class=\"epc-otp-heading\" id=\""
            + eid
            + "_heading\">Enter verification code</h2>\n"
            + "    <p class=\"epc-otp-subtext\" id=\""
            + eid
            + "_subtext\">We&rsquo;ve sent a verification code to <strong id=\""
            + eid
            + "_email_display\"></strong>.<br>The code is valid for 5&nbsp;minutes.</p>\n"
            + "    <div class=\"epc-otp-boxes\" id=\""
            + eid
            + "_boxes\">\n"
            + "      <input type=\"text\" maxlength=\"1\" inputmode=\"numeric\" pattern=\"[0-9]\" class=\"epc-otp-box\" autocomplete=\"one-time-code\" aria-label=\"Digit 1\">\n"
            + "      <input type=\"text\" maxlength=\"1\" inputmode=\"numeric\" pattern=\"[0-9]\" class=\"epc-otp-box\" autocomplete=\"off\" aria-label=\"Digit 2\">\n"
            + "      <input type=\"text\" maxlength=\"1\" inputmode=\"numeric\" pattern=\"[0-9]\" class=\"epc-otp-box\" autocomplete=\"off\" aria-label=\"Digit 3\">\n"
            + "      <input type=\"text\" maxlength=\"1\" inputmode=\"numeric\" pattern=\"[0-9]\" class=\"epc-otp-box\" autocomplete=\"off\" aria-label=\"Digit 4\">\n"
            + "      <input type=\"text\" maxlength=\"1\" inputmode=\"numeric\" pattern=\"[0-9]\" class=\"epc-otp-box\" autocomplete=\"off\" aria-label=\"Digit 5\">\n"
            + "      <input type=\"text\" maxlength=\"1\" inputmode=\"numeric\" pattern=\"[0-9]\" class=\"epc-otp-box\" autocomplete=\"off\" aria-label=\"Digit 6\">\n"
            + "    </div>\n"
            + "    <button class=\"epc-otp-btn\" id=\""
            + eid
            + "_btn\" disabled>Continue</button>\n"
            + "    <p class=\"epc-otp-msg\" id=\""
            + eid
            + "_msg\" aria-live=\"polite\"></p>\n"
            + "    <p class=\"epc-otp-resend\">\n"
            + "      Didn&rsquo;t receive the code? Please check your spam folder.&nbsp;\n"
            + "      <button class=\"epc-otp-resend-btn\" id=\""
            + eid
            + "_resend\" disabled>Resend <span class=\"epc-otp-resend-timer\" id=\""
            + eid
            + "_timer\"></span></button>\n"
            + "    </p>\n"
            + "  </div>\n"
            + "</div>\n"
            + "<script>\n"
            + "(function(){\n"
            + "'use strict';\n"
            + "window.EpcOtpModal=window.EpcOtpModal||{};\n"
            + "var ID="
            + Json(id)
            + ";\n"
            + "var CTX="
            + Json(context)
            + ";\n"
            + "var TENANT="
            + Json(tenantKey)
            + ";\n"
            + "var SEND_URL="
            + Json(sendUrl)
            + ";\n"
            + "var VERIFY_URL="
            + Json(verifyUrl)
            + ";\n"
            + "var RETURN_URL="
            + Json(returnUrl)
            + ";\n"
            + "var overlay=document.getElementById(ID+'_overlay');\n"
            + "var boxWrap=document.getElementById(ID+'_boxes');\n"
            + "var boxes=boxWrap?Array.from(boxWrap.querySelectorAll('.epc-otp-box')):[];\n"
            + "var btn=document.getElementById(ID+'_btn');\n"
            + "var msgEl=document.getElementById(ID+'_msg');\n"
            + "var resendBtn=document.getElementById(ID+'_resend');\n"
            + "var timerEl=document.getElementById(ID+'_timer');\n"
            + "var emailDisplay=document.getElementById(ID+'_email_display');\n"
            + "var closeBtn=document.getElementById(ID+'_close');\n"
            + "var currentEmail='';\n"
            + "var resendTimer=null;\n"
            + "\n"
            + "function showMsg(t,ok){\n"
            + "  if(!msgEl)return;\n"
            + "  msgEl.textContent=t;\n"
            + "  msgEl.className='epc-otp-msg'+(ok?' is-ok':' is-err');\n"
            + "}\n"
            + "function clearMsg(){if(msgEl){msgEl.textContent='';msgEl.className='epc-otp-msg';}}\n"
            + "\n"
            + "function getCode(){return boxes.map(function(b){return b.value;}).join('');}\n"
            + "\n"
            + "function checkComplete(){\n"
            + "  var code=getCode();\n"
            + "  if(btn)btn.disabled=(code.length!==6);\n"
            + "}\n"
            + "\n"
            + "function markBoxError(){\n"
            + "  boxes.forEach(function(b){b.classList.add('is-error');});\n"
            + "  setTimeout(function(){boxes.forEach(function(b){b.classList.remove('is-error');});},400);\n"
            + "}\n"
            + "\n"
            + "function clearBoxes(){\n"
            + "  boxes.forEach(function(b){b.value='';b.classList.remove('is-filled','is-error');});\n"
            + "  if(boxes[0])boxes[0].focus();\n"
            + "  checkComplete();\n"
            + "}\n"
            + "\n"
            + "// ── Auto-advance behaviour ──\n"
            + "boxes.forEach(function(box,i){\n"
            + "  box.addEventListener('input',function(){\n"
            + "    var v=box.value.replace(/[^0-9]/g,'');\n"
            + "    box.value=v.slice(-1);\n"
            + "    box.classList.toggle('is-filled',box.value!=='');\n"
            + "    if(box.value&&i<boxes.length-1)boxes[i+1].focus();\n"
            + "    checkComplete();\n"
            + "  });\n"
            + "  box.addEventListener('keydown',function(e){\n"
            + "    if(e.key==='Backspace'&&!box.value&&i>0){boxes[i-1].focus();boxes[i-1].value='';boxes[i-1].classList.remove('is-filled');checkComplete();}\n"
            + "    if(e.key==='ArrowLeft'&&i>0)boxes[i-1].focus();\n"
            + "    if(e.key==='ArrowRight'&&i<boxes.length-1)boxes[i+1].focus();\n"
            + "    if(e.key==='Enter'){e.preventDefault();if(getCode().length===6)doVerify();}\n"
            + "  });\n"
            + "  box.addEventListener('paste',function(e){\n"
            + "    e.preventDefault();\n"
            + "    var paste=((e.clipboardData||window.clipboardData).getData('text')||'').replace(/[^0-9]/g,'');\n"
            + "    paste.split('').slice(0,6).forEach(function(ch,j){\n"
            + "      if(boxes[i+j]){boxes[i+j].value=ch;boxes[i+j].classList.add('is-filled');}\n"
            + "    });\n"
            + "    var next=Math.min(i+paste.length,boxes.length-1);\n"
            + "    boxes[next].focus();\n"
            + "    checkComplete();\n"
            + "  });\n"
            + "  box.addEventListener('focus',function(){box.select();});\n"
            + "});\n"
            + "\n"
            + "// ── Resend cooldown ──\n"
            + "function startResendTimer(secs){\n"
            + "  if(resendBtn)resendBtn.disabled=true;\n"
            + "  var remaining=secs||60;\n"
            + "  function tick(){\n"
            + "    if(timerEl)timerEl.textContent='('+remaining+'s)';\n"
            + "    if(remaining<=0){\n"
            + "      if(resendBtn)resendBtn.disabled=false;\n"
            + "      if(timerEl)timerEl.textContent='';\n"
            + "      return;\n"
            + "    }\n"
            + "    remaining--;\n"
            + "    resendTimer=setTimeout(tick,1000);\n"
            + "  }\n"
            + "  tick();\n"
            + "}\n"
            + "\n"
            + "function parseJsonResponse(r){\n"
            + "  return r.text().then(function(t){\n"
            + "    var d=null;\n"
            + "    try{d=t?JSON.parse(t):null;}catch(e){d=null;}\n"
            + "    if(!d||typeof d!=='object'){\n"
            + "      var hint=(r.status===403)?'Verification service blocked — please contact support.':'Network error — please retry.';\n"
            + "      if(r.status&&r.status!==200&&r.status!==400){hint='Request failed ('+r.status+'). Please retry.';}\n"
            + "      return {ok:false,message:hint};\n"
            + "    }\n"
            + "    if(d.ok===undefined&&!r.ok){d.ok=false;if(!d.message)d.message='Request failed ('+r.status+'). Please retry.';}\n"
            + "    return d;\n"
            + "  });\n"
            + "}\n"
            + "\n"
            + "// ── Send OTP ──\n"
            + "function doSend(email,isResend){\n"
            + "  currentEmail=email||currentEmail;\n"
            + "  if(emailDisplay)emailDisplay.textContent=currentEmail;\n"
            + "  clearMsg();\n"
            + "  clearBoxes();\n"
            + "  if(isResend){showMsg('Sending…',true);}\n"
            + "  var body={email:currentEmail,tenant_key:TENANT,context:CTX};\n"
            + "  if(RETURN_URL)body.return_url=RETURN_URL;\n"
            + "  fetch(SEND_URL,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)})\n"
            + "  .then(parseJsonResponse)\n"
            + "  .then(function(d){\n"
            + "    showMsg(d.message||'',!!d.ok);\n"
            + "    if(d.ok){startResendTimer(60);}\n"
            + "    else{if(resendBtn)resendBtn.disabled=false;}\n"
            + "  })\n"
            + "  .catch(function(){showMsg('Network error — please retry.',false);if(resendBtn)resendBtn.disabled=false;});\n"
            + "}\n"
            + "\n"
            + "// ── Verify OTP ──\n"
            + "function doVerify(){\n"
            + "  var code=getCode();\n"
            + "  if(code.length!==6){markBoxError();return;}\n"
            + "  if(btn){btn.disabled=true;btn.textContent='Verifying…';}\n"
            + "  clearMsg();\n"
            + "  var body={email:currentEmail,code:code,tenant_key:TENANT,context:CTX};\n"
            + "  if(RETURN_URL)body.return_url=RETURN_URL;\n"
            + "  fetch(VERIFY_URL,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)})\n"
            + "  .then(parseJsonResponse)\n"
            + "  .then(function(d){\n"
            + "    if(btn){btn.textContent='Continue';btn.disabled=false;}\n"
            + "    if(d.ok){\n"
            + "      clearMsg();\n"
            + "      close();\n"
            + "      var data=d;\n"
            + "      "
            + onSuccess
            + "    } else {\n"
            + "      showMsg(d.message||'Invalid or expired code',false);\n"
            + "      markBoxError();\n"
            + "      clearBoxes();\n"
            + "    }\n"
            + "  })\n"
            + "  .catch(function(){\n"
            + "    if(btn){btn.textContent='Continue';btn.disabled=false;}\n"
            + "    showMsg('Network error — please retry.',false);\n"
            + "  });\n"
            + "}\n"
            + "\n"
            + "// ── Open / Close ──\n"
            + "function open(email){\n"
            + "  currentEmail=email||'';\n"
            + "  if(emailDisplay)emailDisplay.textContent=currentEmail;\n"
            + "  clearBoxes();\n"
            + "  clearMsg();\n"
            + "  if(overlay)overlay.classList.add('is-open');\n"
            + "  doSend(currentEmail,false);\n"
            + "  setTimeout(function(){if(boxes[0])boxes[0].focus();},80);\n"
            + "}\n"
            + "function close(){\n"
            + "  if(overlay)overlay.classList.remove('is-open');\n"
            + "  if(resendTimer)clearTimeout(resendTimer);\n"
            + "}\n"
            + "\n"
            + "// ── Event wiring ──\n"
            + "if(btn)btn.addEventListener('click',doVerify);\n"
            + "if(resendBtn)resendBtn.addEventListener('click',function(){doSend(currentEmail,true);});\n"
            + "if(closeBtn)closeBtn.addEventListener('click',close);\n"
            + "if(overlay)overlay.addEventListener('click',function(e){if(e.target===overlay)close();});\n"
            + "document.addEventListener('keydown',function(e){if(e.key==='Escape'&&overlay&&overlay.classList.contains('is-open'))close();});\n"
            + "\n"
            + "// ── Expose API ──\n"
            + "window.EpcOtpModal[ID]={open:open,close:close,doSend:doSend};\n"
            + "})();\n"
            + "</script>\n";

    public sealed record Options
    {
        public string? ModalId { get; init; }

        public string? Context { get; init; }

        public string? TenantKey { get; init; }

        public string? SendUrl { get; init; }

        public string? VerifyUrl { get; init; }

        public string? ReturnUrl { get; init; }

        public string? LogoUrl { get; init; }

        public string? Label { get; init; }

        public string? OnSuccess { get; init; }

        public bool VerifyOnly { get; init; }
    }
}
