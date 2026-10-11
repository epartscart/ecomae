namespace EcomAE.Platform.Presentation;

/// <summary>
/// Catalog, SEO, guide, preview, styles and scripts for the public Free Tools page.
/// Path: <c>content/general_pages/epc_ecomae_free_tools.php</c>.
/// </summary>
internal static class PhpPlanQ1HillData
{
    public static readonly string Styles = """
<style>
.eft-wrap{padding-top:24px}
.eft-hero{max-width:820px;margin:0 auto 28px;text-align:center}
.eft-hero h1{font-family:Syne,sans-serif;font-size:2.1rem;margin:10px 0}
.eft-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:18px}
.eft-card{display:flex;flex-direction:column;gap:8px;padding:20px;border:1px solid rgba(148,163,184,.25);border-radius:14px;background:linear-gradient(160deg,rgba(23,23,23,.6),rgba(31,31,31,.4));text-decoration:none;color:inherit;transition:transform .15s,border-color .15s}
.eft-card:hover{transform:translateY(-3px);border-color:var(--epm-cyan,#0ea5e9)}
.eft-card>i{font-size:26px;color:var(--epm-cyan,#0ea5e9)}
.eft-card h3{margin:2px 0;font-size:1.15rem}
.eft-card p{color:var(--epm-muted,#94a3b8);font-size:.92rem;margin:0;flex:1}
.eft-card__tag{align-self:flex-start;font-size:.7rem;text-transform:uppercase;letter-spacing:.06em;padding:3px 8px;border-radius:999px;background:rgba(14,165,233,.14);color:var(--epm-cyan,#0ea5e9)}
.eft-card__cta{font-weight:700;color:var(--epm-cyan,#0ea5e9);font-size:.9rem}
.eft-card--off{opacity:.62}
.eft-card__cta--off{color:#fbbf24}
.eft-faq{max-width:820px;margin:34px auto 0}
.eft-faq h2{font-size:1.4rem;margin:0 0 14px;color:var(--epm-ink,#e6edf6)}
.eft-faq__item{border:1px solid rgba(148,163,184,.22);border-radius:10px;padding:12px 16px;margin-bottom:10px;background:rgba(148,163,184,.05)}
.eft-faq__item summary{cursor:pointer;font-weight:700;color:var(--epm-ink,#e6edf6);font-size:.98rem}
.eft-faq__a{color:var(--epm-muted,#94a3b8);font-size:.92rem;margin-top:8px;line-height:1.55}
.eft-upsell{margin:34px auto 0;max-width:760px;text-align:center;padding:24px;border:1px dashed rgba(148,163,184,.3);border-radius:14px}
.eft-back{display:inline-block;margin-bottom:14px;color:var(--epm-muted,#94a3b8);text-decoration:none;font-weight:600;font-size:.9rem}
.eft-tool-head{display:flex;gap:16px;align-items:flex-start;margin-bottom:18px}
.eft-tool-head>i{font-size:30px;color:var(--epm-cyan,#0ea5e9);margin-top:6px}
.eft-tool-head h1{font-family:Syne,sans-serif;margin:0 0 4px;font-size:1.7rem}
.eft-tool-head p{color:var(--epm-muted,#94a3b8);margin:0;max-width:640px}
.eft-gate,.eft-app{border:1px solid rgba(148,163,184,.25);border-radius:14px;padding:22px;background:rgba(23,23,23,.5)}
.eft-form-row{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:12px;margin:12px 0}
.eft-wrap label{display:flex;flex-direction:column;gap:5px;font-size:.85rem;font-weight:600;color:var(--epm-muted,#94a3b8)}
.eft-wrap input,.eft-wrap select{padding:9px 11px;border-radius:9px;border:1px solid rgba(148,163,184,.3);background:rgba(10,10,10,.6);color:#e2e8f0;font:inherit}
.eft-split{display:grid;grid-template-columns:1fr 1fr;gap:20px}
.eft-inputs{display:flex;flex-direction:column;gap:12px}
.eft-fieldset{font-weight:800;color:#e2e8f0;margin-top:6px;border-bottom:1px solid rgba(148,163,184,.2);padding-bottom:4px}
.eft-line{display:grid;grid-template-columns:2fr 1fr 1fr;gap:8px;margin-bottom:8px}
.eft-result{border:1px solid rgba(148,163,184,.2);border-radius:12px;padding:18px;min-height:160px;background:rgba(10,10,10,.4)}
.eft-result__empty{color:var(--epm-muted,#94a3b8);text-align:center;padding:40px 0}
.eft-result table{width:100%;border-collapse:collapse;font-size:.92rem}
.eft-result td{padding:7px 4px;border-bottom:1px solid rgba(148,163,184,.14)}
.eft-result td:last-child{text-align:right;font-variant-numeric:tabular-nums}
.eft-result .eft-total{font-weight:800;font-size:1.05rem;color:var(--epm-cyan,#0ea5e9)}
.eft-app__bar{display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:8px;margin-bottom:14px;font-size:.85rem;color:var(--epm-muted,#94a3b8)}
.eft-link{background:transparent;border:0;color:var(--epm-cyan,#0ea5e9);cursor:pointer;font:inherit;font-weight:600;padding:0}
.eft-msg{font-size:.85rem;margin-top:8px;min-height:18px}
.eft-msg.is-err{color:#f87171}.eft-msg.is-ok{color:#34d399}
.eft-result__actions{display:flex;gap:8px;margin-top:14px;flex-wrap:wrap}
.eft-result__actions button{font-size:.82rem;padding:7px 12px}
.eft-rules{max-width:740px;margin:8px auto 0;font-size:.9rem;color:var(--epm-muted,#94a3b8);background:rgba(14,165,233,.06);border:1px solid rgba(14,165,233,.18);border-radius:10px;padding:10px 14px}
.eft-rules i{color:var(--epm-cyan,#0ea5e9)}
.eft-hint{font-size:.8rem;color:var(--epm-muted,#94a3b8);margin:2px 0 0;font-weight:500}
.eft-wrap textarea{padding:9px 11px;border-radius:9px;border:1px solid rgba(148,163,184,.3);background:rgba(10,10,10,.6);color:#e2e8f0;font:inherit;font-family:ui-monospace,Menlo,Consolas,monospace;font-size:.82rem;resize:vertical}
.eft-csv input[type=file]{padding:7px;background:rgba(10,10,10,.4)}
.eft-find{list-style:none;padding:0;margin:6px 0 0}
.eft-find li{display:flex;gap:8px;align-items:flex-start;padding:6px 8px;border-radius:8px;margin-bottom:5px;font-size:.86rem;font-weight:500}
.eft-find li.is-pass{background:rgba(52,211,153,.1);color:#bbf7d0}
.eft-find li.is-warn{background:rgba(251,191,36,.1);color:#fde68a}
.eft-find li.is-fail{background:rgba(248,113,113,.12);color:#fecaca}
.eft-find li i{margin-top:2px}
.eft-result h4{margin:14px 0 4px;font-size:.95rem;color:#e2e8f0}
.eft-result a{color:var(--epm-cyan,#0ea5e9)}
.eft-prev-gallery{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:14px;margin:8px 0 4px}
.eft-prev-gallery figure{margin:0;border:1px solid rgba(148,163,184,.25);border-radius:12px;overflow:hidden;background:rgba(10,10,10,.4)}
.eft-prev-gallery img{width:100%;display:block;height:150px;object-fit:cover;object-position:top}
.eft-prev-gallery figcaption{padding:8px 10px;font-size:.8rem;color:var(--epm-muted,#94a3b8)}
.eft-prev{margin:6px 0 18px;border:1px solid rgba(148,163,184,.25);border-radius:12px;overflow:hidden;max-width:520px}
.eft-prev img{width:100%;display:block}
.eft-prev figcaption{padding:8px 12px;font-size:.82rem;color:var(--epm-muted,#94a3b8);background:rgba(10,10,10,.4)}
.eft-guide{margin:18px 0;padding:18px 20px;border:1px solid rgba(148,163,184,.22);border-radius:14px;background:linear-gradient(180deg,rgba(23,23,23,.5),rgba(10,10,10,.3))}
.eft-guide>h3{margin:0 0 12px;font-size:1.05rem;color:#e2e8f0;display:flex;align-items:center;gap:8px}
.eft-guide>h3 i{color:var(--epm-cyan,#0ea5e9)}
.eft-guide__grid{display:grid;grid-template-columns:1fr 1fr;gap:24px}
.eft-guide__col h4{margin:12px 0 4px;font-size:.82rem;text-transform:uppercase;letter-spacing:.04em;color:var(--epm-cyan,#0ea5e9)}
.eft-guide__col h4:first-child{margin-top:0}
.eft-guide__col p{margin:0;font-size:.9rem;line-height:1.55;color:#cbd5e1}
.eft-guide__list{list-style:none;padding:0;margin:0}
.eft-guide__list li{font-size:.9rem;color:#cbd5e1;padding:3px 0;display:flex;gap:8px;align-items:flex-start}
.eft-guide__list li i{color:#34d399;margin-top:3px}
.eft-guide__how i{color:var(--epm-cyan,#0ea5e9)}
.eft-guide__csv code{display:block;font-size:.8rem;background:rgba(10,10,10,.6);border:1px solid rgba(148,163,184,.2);border-radius:8px;padding:8px 10px;color:#fca5a5;overflow-x:auto}
.eft-preview{margin:16px 0 22px;border:1px solid rgba(148,163,184,.28);border-radius:14px;overflow:hidden;background:linear-gradient(180deg,rgba(23,23,23,.7),rgba(10,10,10,.5));box-shadow:0 10px 30px rgba(10,10,10,.35)}
.eft-preview__bar{display:flex;align-items:center;gap:7px;padding:9px 14px;background:rgba(10,10,10,.55);border-bottom:1px solid rgba(148,163,184,.18)}
.eft-preview__dot{width:10px;height:10px;border-radius:50%;background:#475569}
.eft-preview__dot:nth-child(1){background:#f87171}.eft-preview__dot:nth-child(2){background:#fbbf24}.eft-preview__dot:nth-child(3){background:#34d399}
.eft-preview__title{margin-left:8px;font-size:.82rem;font-weight:700;color:#cbd5e1;letter-spacing:.01em}
.eft-preview__badge{margin-left:auto;font-size:.66rem;font-weight:800;letter-spacing:.06em;color:#141414;background:#fbbf24;border-radius:999px;padding:3px 9px}
.eft-preview__body{padding:16px 18px}
.eft-preview__head{display:flex;align-items:baseline;justify-content:space-between;gap:12px;margin-bottom:12px;padding-bottom:12px;border-bottom:1px dashed rgba(148,163,184,.25)}
.eft-preview__hlabel{font-size:.8rem;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8}
.eft-preview__hvalue{font-size:1.5rem;font-weight:800;color:#34d399}
.eft-preview__tbl{width:100%;border-collapse:collapse}
.eft-preview__tbl td{padding:6px 0;font-size:.9rem;color:#cbd5e1;border-bottom:1px solid rgba(148,163,184,.1)}
.eft-preview__tbl td:last-child{text-align:right;font-weight:700;color:#e2e8f0}
.eft-preview__chip{margin-top:12px;display:inline-flex;align-items:center;gap:7px;font-size:.82rem;font-weight:700;color:#34d399;background:rgba(16,185,129,.12);border:1px solid rgba(16,185,129,.3);border-radius:999px;padding:5px 12px}
.eft-preview__foot{padding:11px 18px;font-size:.85rem;color:#cbd5e1;background:rgba(10,10,10,.45);border-top:1px solid rgba(148,163,184,.18)}
.eft-preview__foot i{color:var(--epm-cyan,#0ea5e9);margin-right:6px}
.eft-gallery{margin:34px 0 8px;text-align:center}
.eft-gallery>h2{margin:0 0 6px;font-size:1.4rem;color:#e2e8f0;display:flex;gap:9px;align-items:center;justify-content:center}
.eft-gallery>h2 i{color:var(--epm-cyan,#0ea5e9)}
.eft-gallery__lead{max-width:640px;margin:0 auto 20px;color:#94a3b8;font-size:.95rem}
.eft-gallery__grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:18px;text-align:left}
.eft-gallery__item{text-decoration:none;color:inherit;display:block;transition:transform .15s}
.eft-gallery__item:hover{transform:translateY(-4px)}
.eft-gallery__item .eft-preview{margin:0;height:100%}
@media(max-width:640px){.eft-guide__grid{grid-template-columns:1fr}}
.eft-gate__tabs{display:flex;gap:6px;margin-bottom:16px;border-bottom:1px solid rgba(148,163,184,.2)}
.eft-tab{background:transparent;border:0;border-bottom:2px solid transparent;color:var(--epm-muted,#94a3b8);font:inherit;font-weight:700;font-size:.92rem;padding:8px 14px;cursor:pointer}
.eft-tab.is-active{color:var(--epm-cyan,#0ea5e9);border-bottom-color:var(--epm-cyan,#0ea5e9)}
.eft-pass{position:relative;display:flex}
.eft-pass input{flex:1;padding-right:38px}
.eft-eye{position:absolute;right:6px;top:50%;transform:translateY(-50%);background:transparent;border:0;color:var(--epm-muted,#94a3b8);cursor:pointer;padding:4px}
.eft-reset{margin-top:14px;padding-top:14px;border-top:1px dashed rgba(148,163,184,.25)}
.eft-reset h4{margin:0 0 4px;color:#e2e8f0;font-size:.95rem}
.eft-app__menu{display:flex;align-items:center;gap:14px;flex-wrap:wrap}
.eft-link--danger{color:#f87171}
.eft-unavailable{display:flex;gap:14px;align-items:center;padding:18px 20px;border:1px solid rgba(251,191,36,.35);background:rgba(251,191,36,.08);border-radius:14px;margin-bottom:8px}
.eft-unavailable i{font-size:26px;color:#fbbf24}
.eft-unavailable strong{display:block;color:#fde68a;font-size:1rem}
.eft-unavailable span{color:var(--epm-muted,#94a3b8);font-size:.9rem}
.eft-danger{margin:0 0 16px;padding:16px 18px;border:1px solid rgba(248,113,113,.35);background:rgba(248,113,113,.06);border-radius:12px}
.eft-danger h4{margin:0 0 6px;color:#fecaca;font-size:.98rem}
.eft-danger p{color:var(--epm-muted,#94a3b8);font-size:.88rem;margin:0 0 10px}
.epm-btn--danger{background:#dc2626;border-color:#dc2626;color:#fff}
.eft-saves{margin:0 0 16px;padding:14px 16px;border:1px solid rgba(148,163,184,.25);border-radius:12px;background:rgba(10,10,10,.4)}
.eft-saves h4{margin:0 0 8px;color:#e2e8f0;font-size:.95rem}
.eft-saves table{width:100%;border-collapse:collapse;font-size:.86rem}
.eft-saves td{padding:6px 4px;border-bottom:1px solid rgba(148,163,184,.14)}
@media(max-width:760px){.eft-split{grid-template-columns:1fr}.eft-guide__grid{grid-template-columns:1fr;gap:8px}}
</style>
""";

    public static readonly string ScriptsTemplate = """
(function(){
	var wrap=document.querySelector('[data-eft-tool]');
	if(!wrap)return;
	var tool=wrap.getAttribute('data-eft-tool');
	var ENDPOINT=__ENDPOINT__;
	var tokenKey='eft_token';
	function post(action,data){
		data.action=action;
		return fetch(ENDPOINT,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)}).then(function(r){return r.json();});
	}
	function el(id){return document.getElementById(id);}
	function esch(s){return String(s==null?'':s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}
	var curAcc=null;
	function showApp(acc){
		curAcc=acc;
		if(el('eft_gate'))el('eft_gate').hidden=true;
		el('eft_app').hidden=false;
		el('eft_who').textContent=acc.company?('Signed in: '+acc.company+' ('+acc.email+')'):('Signed in: '+acc.email);
		el('eft_country_label').textContent=acc.country;
		wrap.setAttribute('data-eft-country',acc.country);
	}
	function showGate(){if(el('eft_app'))el('eft_app').hidden=true;if(el('eft_gate'))el('eft_gate').hidden=false;}
	if(wrap.getAttribute('data-eft-inactive')){return;}
	var saved=localStorage.getItem(tokenKey);
	if(saved){
		post('whoami',{token:saved}).then(function(r){if(r&&r.ok){showApp(r.account);}});
	}
	// Show/hide password fields.
	wrap.querySelectorAll('.eft-eye').forEach(function(b){
		b.addEventListener('click',function(){
			var inp=el(b.getAttribute('data-eye'));if(!inp)return;
			inp.type=inp.type==='password'?'text':'password';
			var ic=b.querySelector('i');if(ic)ic.className=inp.type==='password'?'fa fa-eye':'fa fa-eye-slash';
		});
	});
	// Register / Log in tabs.
	var tabR=el('eft_tab_register'),tabL=el('eft_tab_login');
	function selectTab(login){
		if(!tabR)return;
		tabR.classList.toggle('is-active',!login);tabL.classList.toggle('is-active',login);
		el('eft_pane_register').hidden=login;el('eft_pane_login').hidden=!login;
	}
	if(tabR)tabR.addEventListener('click',function(){selectTab(false);});
	if(tabL)tabL.addEventListener('click',function(){selectTab(true);});
	// Register.
	var reg=el('eft_register');
	if(reg)reg.addEventListener('click',function(){
		var msg=el('eft_gate_msg');msg.className='eft-msg';msg.textContent='Working…';
		post('register',{email:el('eft_email').value,company:el('eft_company').value,country:el('eft_country').value,password:el('eft_password').value}).then(function(r){
			if(r&&r.ok){localStorage.setItem(tokenKey,r.token);msg.className='eft-msg is-ok';msg.textContent=r.message||'';showApp(r.account);}
			else{msg.className='eft-msg is-err';msg.textContent=(r&&r.message)||'Could not register.';if(r&&r.exists){selectTab(true);el('eft_login_email').value=el('eft_email').value;}}
		}).catch(function(){msg.className='eft-msg is-err';msg.textContent='Network error.';});
	});
	// Log in.
	var lg=el('eft_login');
	if(lg)lg.addEventListener('click',function(){
		var msg=el('eft_login_msg');msg.className='eft-msg';msg.textContent='Signing in…';
		post('login',{email:el('eft_login_email').value,password:el('eft_login_password').value}).then(function(r){
			if(r&&r.ok){localStorage.setItem(tokenKey,r.token);msg.className='eft-msg is-ok';msg.textContent=r.message||'';showApp(r.account);}
			else{msg.className='eft-msg is-err';msg.textContent=(r&&r.message)||'Could not sign in.';if(r&&r.needs_register){selectTab(false);el('eft_email').value=el('eft_login_email').value;}}
		}).catch(function(){msg.className='eft-msg is-err';msg.textContent='Network error.';});
	});
	// Forgot password -> request reset code.
	var fg=el('eft_forgot');
	if(fg)fg.addEventListener('click',function(){
		var msg=el('eft_login_msg');var em=el('eft_login_email').value;
		if(!em){msg.className='eft-msg is-err';msg.textContent='Enter your email above first.';return;}
		msg.className='eft-msg';msg.textContent='Sending reset code…';
		post('request_reset',{email:em}).then(function(r){
			msg.className='eft-msg '+(r&&r.ok?'is-ok':'is-err');msg.textContent=(r&&r.message)||'Could not send code.';
			if(r&&r.ok){el('eft_reset').hidden=false;}
		}).catch(function(){msg.className='eft-msg is-err';msg.textContent='Network error.';});
	});
	// Confirm reset.
	var rs=el('eft_reset_submit');
	if(rs)rs.addEventListener('click',function(){
		var msg=el('eft_reset_msg');msg.className='eft-msg';msg.textContent='Updating…';
		post('confirm_reset',{email:el('eft_login_email').value,code:el('eft_reset_code').value,password:el('eft_reset_password').value}).then(function(r){
			if(r&&r.ok){localStorage.setItem(tokenKey,r.token);msg.className='eft-msg is-ok';msg.textContent=r.message||'';showApp(r.account);}
			else{msg.className='eft-msg is-err';msg.textContent=(r&&r.message)||'Could not reset.';}
		}).catch(function(){msg.className='eft-msg is-err';msg.textContent='Network error.';});
	});
	// Log out.
	var lo=el('eft_logout');
	if(lo)lo.addEventListener('click',function(){localStorage.removeItem(tokenKey);curAcc=null;showGate();selectTab(true);if(el('eft_login_msg')){el('eft_login_msg').className='eft-msg is-ok';el('eft_login_msg').textContent='Signed out.';}});
	// My saved results.
	var ms=el('eft_my_saves');
	if(ms)ms.addEventListener('click',function(){
		var box=el('eft_saves');
		if(!box.hidden){box.hidden=true;return;}
		box.hidden=false;box.innerHTML='<h4>My saved results</h4><p>Loading…</p>';
		post('list',{token:localStorage.getItem(tokenKey)}).then(function(r){
			if(!r||!r.ok){box.innerHTML='<h4>My saved results</h4><p>Could not load.</p>';return;}
			if(!r.saves||!r.saves.length){box.innerHTML='<h4>My saved results</h4><p>No saved results yet. Run a calculation and press <strong>Save</strong>.</p>';return;}
			var h='<h4>My saved results ('+r.saves.length+')</h4><table>';
			r.saves.forEach(function(s){var d=new Date((s.time_created||0)*1000);h+='<tr><td>'+esch(s.title||s.tool)+'</td><td>'+esch(s.tool)+'</td><td>'+esch(s.country||'')+'</td><td>'+(isNaN(d)?'':d.toISOString().slice(0,10))+'</td></tr>';});
			box.innerHTML=h+'</table>';
		});
	});
	// Delete my data (cross-code confirmed).
	var dl0=el('eft_delete');
	if(dl0)dl0.addEventListener('click',function(){var b=el('eft_delete_box');b.hidden=!b.hidden;});
	var dcancel=el('eft_delete_cancel');
	if(dcancel)dcancel.addEventListener('click',function(){el('eft_delete_box').hidden=true;});
	var dsend=el('eft_delete_send');
	if(dsend)dsend.addEventListener('click',function(){
		var msg=el('eft_delete_msg');msg.className='eft-msg';msg.textContent='Emailing your confirmation code…';
		post('request_delete',{token:localStorage.getItem(tokenKey)}).then(function(r){
			msg.className='eft-msg '+(r&&r.ok?'is-ok':'is-err');msg.textContent=(r&&r.message)||'Could not send code.';
			if(r&&r.ok){el('eft_delete_confirm').hidden=false;el('eft_delete_confirm_btn').hidden=false;}
		}).catch(function(){msg.className='eft-msg is-err';msg.textContent='Network error.';});
	});
	var dconf=el('eft_delete_confirm_btn');
	if(dconf)dconf.addEventListener('click',function(){
		var msg=el('eft_delete_msg');msg.className='eft-msg';msg.textContent='Deleting…';
		post('confirm_delete',{token:localStorage.getItem(tokenKey),code:el('eft_delete_code').value}).then(function(r){
			if(r&&r.ok){localStorage.removeItem(tokenKey);curAcc=null;msg.className='eft-msg is-ok';msg.textContent=r.message||'Deleted.';setTimeout(function(){showGate();selectTab(false);},1200);}
			else{msg.className='eft-msg is-err';msg.textContent=(r&&r.message)||'Could not delete.';}
		}).catch(function(){msg.className='eft-msg is-err';msg.textContent='Network error.';});
	});
	var chg=el('eft_change_country');
	if(chg)chg.addEventListener('click',function(){showGate();});
	var addLine=el('eft_add_line');
	if(addLine)addLine.addEventListener('click',function(){
		var d=document.createElement('div');d.className='eft-line';
		d.innerHTML='<input type="text" class="eft-ln-desc" placeholder="Description" /><input type="number" step="0.01" class="eft-ln-qty" placeholder="Qty" /><input type="number" step="0.01" class="eft-ln-price" placeholder="Unit price" />';
		el('eft_lines').appendChild(d);
	});
	// CSV file -> textarea (so the same field is posted whether typed or uploaded).
	wrap.querySelectorAll('[data-eft-csv]').forEach(function(fi){
		fi.addEventListener('change',function(){
			var f=fi.files&&fi.files[0];if(!f)return;
			var rd=new FileReader();
			rd.onload=function(){
				var ta=wrap.querySelector('textarea[data-eft-field="csv"]');
				if(ta){ta.value=String(rd.result||'');}
			};
			rd.readAsText(f);
		});
	});
	function csvEscape(v){v=String(v==null?'':v);return /[",\n]/.test(v)?('"'+v.replace(/"/g,'""')+'"'):v;}
	function download(name,text){
		var blob=new Blob([text],{type:'text/csv'});
		var a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download=name;
		document.body.appendChild(a);a.click();document.body.removeChild(a);
	}
	function resultToCsv(r){
		var lines=[];
		function kv(rows){rows.forEach(function(row){lines.push(csvEscape(row[0])+','+csvEscape(row[1]));});}
		if(r.rows)kv(r.rows);
		if(r.rows_text)kv(r.rows_text);
		if(r.income){lines.push('Income statement');kv(r.income);}
		if(r.balance){lines.push('Balance sheet');kv(r.balance);}
		if(r.projection){lines.push('Year,Revenue,Gross profit,EBITDA,Net profit');r.projection.forEach(function(p){lines.push([p.year,p.revenue,p.gross_profit,p.ebitda,p.net_profit].map(csvEscape).join(','));});}
		if(r.doc_rows){lines.push('Document,Expiry,Days left,Status,Next reminder');r.doc_rows.forEach(function(d){lines.push([d.title,d.expiry,d.days_left,d.status,d.next_reminder].map(csvEscape).join(','));});}
		if(r.recommended){lines.push('Cover,Basis');r.recommended.forEach(function(x){lines.push(csvEscape(x.class)+','+csvEscape(x.basis));});}
		if(r.compliance){lines.push('');lines.push('Compliance');r.compliance.forEach(function(f){lines.push(csvEscape(f.level)+','+csvEscape(f.message));});}
		if(typeof r.net!=='undefined')lines.push(csvEscape(r.net_label||'Total')+','+csvEscape(r.net));
		return lines.join('\n');
	}
	function collect(){
		var data={};
		wrap.querySelectorAll('[data-eft-field]').forEach(function(i){data[i.getAttribute('data-eft-field')]=i.value;});
		if(tool==='einvoice'){
			var lines=[];
			wrap.querySelectorAll('.eft-line').forEach(function(l){
				lines.push({desc:l.querySelector('.eft-ln-desc').value,qty:l.querySelector('.eft-ln-qty').value,price:l.querySelector('.eft-ln-price').value});
			});
			data.lines=lines;
		}
		return data;
	}
	function money(cur,v){return cur+' '+Number(v).toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2});}
	function render(r){
		var box=el('eft_result');
		if(!r||!r.ok){box.innerHTML='<div class="eft-result__empty">'+((r&&r.message)||'Could not calculate.')+'</div>';return;}
		var cur=r.currency||'';var h='';
		function esc(s){return String(s==null?'':s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}
		function cell(v){return (typeof v==='number')?money(cur,v):esc(v);}
		function kvtable(rows){var t='<table>';rows.forEach(function(row){t+='<tr><td>'+esc(row[0])+'</td><td>'+cell(row[1])+'</td></tr>';});return t+'</table>';}
		function findings(list){if(!list||!list.length)return '';var t='<h4>Compliance check</h4><ul class="eft-find">';list.forEach(function(f){var ic=f.level==='pass'?'fa-check-circle':(f.level==='fail'?'fa-times-circle':'fa-exclamation-triangle');t+='<li class="is-'+esc(f.level)+'"><i class="fa '+ic+'"></i><span>'+esc(f.message)+'</span></li>';});return t+'</ul>';}
		// External Reporting: nested VAT + CT packs.
		if(r.vat||r.ct){
			h+='<p><small>Filing authority: <strong>'+esc(r.authority)+'</strong></small></p>';
			if(r.vat){h+='<h4>'+esc(r.vat_return_name)+'</h4>'+kvtable(r.vat.rows||[]);}
			if(r.ct){h+='<h4>'+esc(r.ct_return_name)+'</h4>'+kvtable(r.ct.rows||[]);}
		} else {
			if(r.rows)h+=kvtable(r.rows);
			if(r.rows_text)h+=kvtable(r.rows_text);
		}
		if(r.recommended&&r.recommended.length){h+='<h4>Compulsory / recommended cover</h4><table><tr><td><b>Cover</b></td><td><b>Basis</b></td></tr>';r.recommended.forEach(function(x){h+='<tr><td>'+esc(x.class)+'</td><td>'+esc(x.basis)+'</td></tr>';});h+='</table>';}
		if(r.emp_rows&&r.emp_rows.length){h+='<h4>Employee check</h4>'+kvtable(r.emp_rows);}
		if(r.doc_rows){h+='<h4>Documents</h4><table><tr><td><b>Document</b></td><td><b>Expiry</b></td><td><b>Days</b></td><td><b>Status</b></td><td><b>Next reminder</b></td></tr>';r.doc_rows.forEach(function(d){h+='<tr><td>'+esc(d.title)+'</td><td>'+esc(d.expiry)+'</td><td>'+esc(d.days_left)+'</td><td>'+esc(d.status)+'</td><td>'+esc(d.next_reminder)+'</td></tr>';});h+='</table>';}
		if(r.projection){h+='<h4>Projection</h4><table><tr><td><b>Year</b></td><td><b>Revenue</b></td><td><b>Gross</b></td><td><b>EBITDA</b></td><td><b>Net</b></td></tr>';r.projection.forEach(function(p){h+='<tr><td>'+esc(p.year)+'</td><td>'+money(cur,p.revenue)+'</td><td>'+money(cur,p.gross_profit)+'</td><td>'+money(cur,p.ebitda)+'</td><td>'+money(cur,p.net_profit)+'</td></tr>';});h+='</table>';}
		if(r.income){h+='<h4>Income statement</h4>'+kvtable(r.income);}
		if(r.balance){h+='<h4>Balance sheet</h4>'+kvtable(r.balance);}
		if(r.steps){h+='<table><tr><td><b>Spend range</b></td><td><b>Approver(s)</b></td></tr>';r.steps.forEach(function(s){h+='<tr><td>'+esc(s.range)+'</td><td>'+esc(s.approver)+' <small>('+esc(s.sla)+')</small></td></tr>';});h+='</table>';}
		if(r.lines&&(tool==='einvoice')){h+='<table><tr><td><b>Item</b></td><td><b>Amount</b></td></tr>';r.lines.forEach(function(l){h+='<tr><td>'+esc(l.desc||'')+' ×'+esc(l.qty)+'</td><td>'+money(cur,l.amount)+'</td></tr>';});h+='</table>';h+='<table><tr><td>Subtotal</td><td>'+money(cur,r.subtotal)+'</td></tr><tr><td>'+esc(r.label)+' ('+esc(r.rate)+'%)</td><td>'+money(cur,r.tax)+'</td></tr></table>';h+='<p><small>Invoice '+esc(r.number)+' · '+esc(r.date)+' · '+esc(r.scheme)+'<br/>Network: '+esc(r.network||'')+(r.endpoint?(' · Endpoint '+esc(r.endpoint)):'')+'<br/>UUID '+esc(r.uuid)+'</small></p>';}
		if(typeof r.net!=='undefined'&&!r.steps){h+='<table><tr class="eft-total"><td>'+esc(r.net_label||'Total')+'</td><td>'+((tool==='workflow'||tool==='taxkit'||tool==='docexpiry')?esc(r.net):money(cur,r.net))+'</td></tr></table>';}
		if(r.net_label&&r.steps){h+='<p class="eft-total">'+esc(r.net)+' '+esc(r.net_label)+'</p>';}
		if(r.authority_url){h+='<p><small><i class="fa fa-external-link"></i> Official authority: <a href="'+esc(r.authority_url)+'" target="_blank" rel="noopener">'+esc(r.authority_url)+'</a></small></p>';}
		if(r.note){h+='<p><small>'+esc(r.note)+'</small></p>';}
		h+=findings(r.compliance);
		h+='<div class="eft-result__actions"><button type="button" class="epm-btn epm-btn--ghost" id="eft_save"><i class="fa fa-save"></i> Save</button><button type="button" class="epm-btn epm-btn--ghost" id="eft_csv"><i class="fa fa-download"></i> Download CSV</button><button type="button" class="epm-btn epm-btn--ghost" onclick="window.print()"><i class="fa fa-print"></i> Print / PDF</button></div><p class="eft-msg" id="eft_save_msg"></p>';
		box.innerHTML=h;
		var dl=el('eft_csv');
		if(dl)dl.addEventListener('click',function(){download(tool+'-'+new Date().toISOString().slice(0,10)+'.csv',resultToCsv(r));});
		var sv=el('eft_save');
		if(sv)sv.addEventListener('click',function(){
			var m=el('eft_save_msg');m.className='eft-msg';m.textContent='Saving…';
			post('save',{token:localStorage.getItem(tokenKey),tool:tool,country:wrap.getAttribute('data-eft-country'),title:tool+' '+new Date().toISOString().slice(0,10),payload:r}).then(function(x){
				m.className='eft-msg '+(x&&x.ok?'is-ok':'is-err');m.textContent=(x&&x.ok)?'Saved to your free account.':((x&&x.message)||'Could not save.');
			});
		});
	}
	var comp=el('eft_compute');
	if(comp)comp.addEventListener('click',function(){
		post('compute',{token:localStorage.getItem(tokenKey),tool:tool,country:wrap.getAttribute('data-eft-country'),inputs:collect()}).then(render).catch(function(){el('eft_result').innerHTML='<div class="eft-result__empty">Network error.</div>';});
	});
})();
""";

    public static Dictionary<string, Dictionary<string, string>> Catalog()
    {
        return new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["vat"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "VAT / GST Return", ["icon"] = "fa-file-text-o", ["tag"] = "Tax", ["blurb"] = "Work out output tax, input tax and net VAT/GST payable for a period — at your country rate." },
            ["ct"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Corporate Tax", ["icon"] = "fa-balance-scale", ["tag"] = "Tax", ["blurb"] = "Estimate corporate income tax on taxable profit using your country rate and small-profit relief." },
            ["payroll"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Payroll & Gratuity", ["icon"] = "fa-users", ["tag"] = "People", ["blurb"] = "Compute net pay and end-of-service gratuity per your country labour law." },
            ["ifrs"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "IFRS Financials", ["icon"] = "fa-line-chart", ["tag"] = "Finance", ["blurb"] = "Turn a trial balance into an IFRS-style Income Statement and Balance Sheet." },
            ["einvoice"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Electronic Invoicing", ["icon"] = "fa-qrcode", ["tag"] = "Compliance", ["blurb"] = "Create a compliant tax invoice with your country VAT, e-invoice scheme and Peppol/network endpoint." },
            ["extreport"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "External Reporting", ["icon"] = "fa-institution", ["tag"] = "Tax", ["blurb"] = "Generate a filing-ready VAT return and corporate tax return for your country authority — upload a CSV to auto-fill." },
            ["customs"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Customs & Logistics", ["icon"] = "fa-ship", ["tag"] = "Trade", ["blurb"] = "Work out CIF, customs duty, import VAT and landed cost per line — upload an invoice CSV." },
            ["insurance"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Insurance", ["icon"] = "fa-shield", ["tag"] = "Risk", ["blurb"] = "See the compulsory/recommended cover for your country and track a policy renewal with reminder lead times." },
            ["docexpiry"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Document Expiry", ["icon"] = "fa-calendar-check-o", ["tag"] = "Risk", ["blurb"] = "Track licences, visas and contracts — get days-left, status and reminder thresholds. Upload a CSV of documents." },
            ["valuation"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Business Valuation", ["icon"] = "fa-balance-scale", ["tag"] = "Finance", ["blurb"] = "Value your company by DCF, EBITDA-multiple and revenue-multiple, with an equity-value bridge." },
            ["finmodel"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Financial Model", ["icon"] = "fa-area-chart", ["tag"] = "Finance", ["blurb"] = "Project a multi-year P&L (revenue, gross profit, EBITDA, net profit) and download it as CSV." },
            ["taxkit"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Tax Worldwide Kit", ["icon"] = "fa-globe", ["tag"] = "Tax", ["blurb"] = "One-screen tax snapshot for your country: VAT/GST, corporate tax + relief, e-invoice scheme and filing authority." },
            ["hrcompliance"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "HR Compliance Worldwide", ["icon"] = "fa-id-card-o", ["tag"] = "HR", ["blurb"] = "Country labour-law card: end-of-service, leave, notice, probation, WPS/social security and the official authority link." },
            ["workflow"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Approval Workflow", ["icon"] = "fa-sitemap", ["tag"] = "Operations", ["blurb"] = "Design a spend-approval workflow with thresholds and approver tiers you can adopt today." },
        };
    }

    public static Dictionary<string, object?> Seo(string tool)
    {
        return tool switch
        {
            "" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free Business Tools — VAT, Corporate Tax, IFRS, Payroll & E-Invoice Calculators | ECOM AE",
                ["description"] = "Free worldwide business tools that work to your registered country's rules: VAT/GST and corporate tax calculators, IFRS financial statements, payroll & gratuity, e-invoicing (FTA, ZATCA, Peppol), customs & landed cost, business valuation, financial models, HR compliance and more. Register free.",
                ["keywords"] = "free business tools, free VAT calculator, free corporate tax calculator, free IFRS financials, free payroll calculator, free e-invoice generator, worldwide tax tools",
                ["h1"] = "Free business tools for your company",
                ["intro"] = "A suite of free, worldwide business tools that automatically work to the rules of the country you register with.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "What tools are included?", ["a"] = "You get 14 tools, including VAT / GST Return, Corporate Tax, Payroll & Gratuity, IFRS Financials, Electronic Invoicing, External Reporting and more." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "vat" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free VAT / GST Return Calculator (Worldwide, Country-Driven) | ECOM AE",
                ["description"] = "Free online VAT/GST return calculator. Work out output tax, recoverable input tax and net VAT payable or refundable at your own country rate (UAE 5%, Saudi 15%, UK 20% and more). Upload a CSV for an instant compliance check. Register free.",
                ["keywords"] = "free VAT calculator, GST return calculator, UAE VAT 201, Saudi VAT, VAT return online, input tax output tax, VAT compliance check",
                ["h1"] = "VAT / GST Return",
                ["intro"] = "Prepare a filing-ready VAT or GST return for any period — free. Enter your standard, zero-rated and exempt sales plus recoverable purchases, or upload a ledger CSV, and get net VAT payable or refundable at your registered country's rate, with a compliance check that flags missing TRN, rate mismatches and negative lines.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "ct" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free Corporate Tax Calculator (Country-Driven, Worldwide) | ECOM AE",
                ["description"] = "Free corporate income tax calculator. Estimate taxable profit and tax due using your country rate and small-business relief band (e.g. UAE 0% on first AED 375,000 then 9%). Upload a P&L/trial-balance CSV. Register free and save your results.",
                ["keywords"] = "free corporate tax calculator, UAE corporate tax 9%, small business relief, taxable profit, company tax estimate, corporate income tax",
                ["h1"] = "Corporate Tax",
                ["intro"] = "Estimate corporate income tax for a year — free. Enter revenue, deductible expenses and adjustments, or upload a P&L/trial-balance CSV, and get taxable profit and tax after your country's small-business relief band.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "payroll" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free Payroll & End-of-Service Gratuity Calculator (Worldwide) | ECOM AE",
                ["description"] = "Free payroll and end-of-service gratuity calculator driven by your country's labour law (UAE gratuity, Saudi end-of-service and more). Compute net pay, gratuity and statutory entitlements. Register free.",
                ["keywords"] = "free payroll calculator, end of service gratuity calculator, UAE gratuity, Saudi end of service, net salary calculator, WPS",
                ["h1"] = "Payroll & Gratuity",
                ["intro"] = "Compute net pay and end-of-service gratuity — free — using the labour law of the country you register with, including statutory leave, notice and gratuity accrual.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "ifrs" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free IFRS Financial Statements Generator (Trial Balance to Accounts) | ECOM AE",
                ["description"] = "Free IFRS financial statements tool. Turn a trial balance into an IFRS-style Income Statement and Balance Sheet with a built-in balance check. Upload your trial-balance CSV. Register free and save.",
                ["keywords"] = "free IFRS financial statements, trial balance to balance sheet, income statement generator, IFRS reporting, financial statements online",
                ["h1"] = "IFRS Financials",
                ["intro"] = "Turn a trial balance into an IFRS-style Income Statement and Balance Sheet — free. Upload a trial-balance CSV (account, debit, credit) and get a structured set of accounts with a completeness and balance check.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "einvoice" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free E-Invoice Generator — FTA, ZATCA & Peppol (Country-Driven) | ECOM AE",
                ["description"] = "Free electronic invoice generator that auto-picks your country e-invoicing scheme — UAE FTA / PINT-AE, Saudi ZATCA / FATOORA, EU Peppol and more. Create a compliant e-invoice for your company. Register free.",
                ["keywords"] = "free e-invoice generator, ZATCA invoice, FATOORA, UAE FTA e-invoice, Peppol PINT, electronic invoicing, tax invoice generator",
                ["h1"] = "Electronic Invoicing",
                ["intro"] = "Create a compliant electronic invoice — free. The tool automatically applies your registered country's e-invoicing scheme (UAE FTA/PINT-AE, Saudi ZATCA/FATOORA, EU Peppol) with the right fields and tax treatment.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "extreport" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free External Reporting — VAT 201 & Corporate Tax Returns (Worldwide) | ECOM AE",
                ["description"] = "Free external reporting tool that formats your VAT return (e.g. UAE VAT 201) and corporate tax return to your country's scheme. Upload a CSV and download a filing-ready report. Register free.",
                ["keywords"] = "free VAT 201, external reporting, corporate tax return, statutory reporting, country tax return generator",
                ["h1"] = "External Reporting",
                ["intro"] = "Produce filing-ready statutory returns — free. The tool formats your VAT return (such as the UAE VAT 201) and corporate tax return to the scheme of the country you register with.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "customs" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free Customs Duty & Landed Cost Calculator (HS Code, Worldwide) | ECOM AE",
                ["description"] = "Free customs duty and landed cost calculator. Compute CIF, HS-code duty, import VAT and landed cost per line from an invoice CSV, with a compliance check for unknown HS codes and zero-value lines. Register free.",
                ["keywords"] = "free landed cost calculator, customs duty calculator, HS code duty, import VAT, CIF value, customs clearance estimate",
                ["h1"] = "Customs & Logistics",
                ["intro"] = "Work out import duty and landed cost — free. Enter or upload an invoice/line CSV (HS code, quantity, value, freight, insurance) and get CIF, HS-driven duty, import VAT and landed cost per line.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "insurance" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free Business Insurance Premium & Renewal Tool (Worldwide) | ECOM AE",
                ["description"] = "Free insurance tool to estimate cover and premium and track policy renewals/expiry for your company, localized to your country. Register free and save your policies.",
                ["keywords"] = "free insurance calculator, business insurance premium, policy renewal tracker, sum insured, insurance expiry",
                ["h1"] = "Insurance",
                ["intro"] = "Estimate cover and premium and keep on top of renewals — free — for the country you register with.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "docexpiry" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free Document Expiry Tracker with Reminders (Worldwide) | ECOM AE",
                ["description"] = "Free document expiry tracker for licences, visas, trade permits and contracts, with country-driven reminder lead times. Upload a CSV and never miss a renewal. Register free.",
                ["keywords"] = "free document expiry tracker, licence renewal reminder, visa expiry, trade licence, compliance calendar",
                ["h1"] = "Document Expiry",
                ["intro"] = "Track every licence, visa, permit and contract expiry — free — with reminder lead days driven by your registered country.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "valuation" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free Business Valuation Calculator — DCF & Multiples (Worldwide) | ECOM AE",
                ["description"] = "Free business valuation calculator. Value your company by DCF, EBITDA multiple and revenue multiple with an equity-value bridge, in your country currency. Upload a P&L CSV. Register free.",
                ["keywords"] = "free business valuation, company valuation calculator, DCF, EBITDA multiple, revenue multiple, equity value",
                ["h1"] = "Business Valuation",
                ["intro"] = "Value a business — free — by DCF, EBITDA multiple and revenue multiple, with an EV-to-equity bridge in your country's currency.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "finmodel" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free Financial Model & 5-Year Projection Tool (Worldwide) | ECOM AE",
                ["description"] = "Free financial modelling tool. Build a 3–5 year P&L projection (revenue, gross profit, EBITDA, net profit) plus a cash snapshot, downloadable as CSV, in your country currency. Register free.",
                ["keywords"] = "free financial model, 5 year projection, P&L forecast, EBITDA projection, startup financial model, cash flow forecast",
                ["h1"] = "Financial Model",
                ["intro"] = "Build a multi-year financial projection — free. Enter base revenue, growth, margin and horizon and get a 3–5 year P&L plus cash snapshot you can download.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "taxkit" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free Worldwide Tax Kit — VAT, Corporate Tax & E-Invoice by Country | ECOM AE",
                ["description"] = "Free worldwide tax snapshot for your country: VAT/GST rate, corporate tax rate and relief threshold, e-invoice scheme, filing authority, return names and fiscal year. Compare countries. Register free.",
                ["keywords"] = "worldwide tax kit, country tax rates, VAT rate by country, corporate tax rate, e-invoice scheme, tax authority",
                ["h1"] = "Tax Worldwide Kit",
                ["intro"] = "Get a one-screen tax snapshot — free — for the country you register with: VAT/GST and corporate tax rates, relief thresholds, e-invoice scheme, filing authority and fiscal year.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "hrcompliance" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free HR Compliance Tool — Labour Law by Country (Worldwide) | ECOM AE",
                ["description"] = "Free HR compliance tool driven by your country labour law: gratuity/end-of-service, statutory leave, notice, probation, WPS/social-security flags and the official authority link. Register free.",
                ["keywords"] = "free HR compliance, labour law by country, statutory leave, end of service, WPS, probation notice period, MOHRE HRSD",
                ["h1"] = "HR Compliance Worldwide",
                ["intro"] = "Check labour-law compliance — free — for the country you register with: end-of-service, statutory leave, notice, probation and WPS/social-security flags, with the official authority link.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            "workflow" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Free Approval Workflow Builder — Spend Thresholds (Worldwide) | ECOM AE",
                ["description"] = "Free approval workflow builder. Design a spend-approval matrix with thresholds and approver tiers in your country currency, ready to adopt. Register free.",
                ["keywords"] = "free approval workflow, spend approval matrix, purchase requisition approval, approver tiers, procurement workflow",
                ["h1"] = "Approval Workflow",
                ["intro"] = "Design a spend-approval workflow — free — with thresholds and approver tiers in your country's currency that you can adopt today.",
                ["faq"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Are these tools really free?", ["a"] = "Yes. Register once with your email and password and use every tool free for your own company — no card required." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do the tools work for my country?", ["a"] = "Yes. Every tool is worldwide and auto-localises to the country you register with — currency, tax rate, e-invoice scheme and labour law — with a safe generic fallback for any country." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Do I need to register for each tool?", ["a"] = "No. One email is one account, and that single free account unlocks all the tools. You cannot register the same email twice — we simply sign you back in." }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["q"] = "Is my data safe?", ["a"] = "Yes. Your saved results are tied to your account and your data cannot be deleted without an email confirmation cross-code sent to you first." } },
            },
            _ => Seo(""),
        };
    }

    public static Dictionary<string, object?> Guide(string key)
    {
        return key switch
        {
            "vat" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Prepare a VAT/GST return for one period. Enter your standard, zero-rated and exempt sales plus recoverable purchases — or upload a sales/purchase ledger CSV — and get the net VAT payable or refundable, filing-ready.",
                ["who"] = "Any VAT/GST-registered business that wants to check its return before filing with the tax authority.",
                ["get"] = new List<object?> { "Output tax, recoverable input tax and net VAT payable/refundable", "A compliance check that flags missing TRN, rate mismatches and negative lines", "Downloadable CSV + print/PDF of the return", "Save the result to your free account" },
                ["how"] = "The VAT rate, label (VAT/GST) and currency come automatically from the country you register with — UAE 5%, Saudi 15%, and so on. No manual setup.",
                ["csv"] = "type,category,amount,trn — e.g. sale,standard,1000,100123456700003 / purchase,standard,400,",
            },
            "ct" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Estimate corporate income tax for a year. Enter revenue, deductible expenses and adjustments — or upload a P&L/trial-balance CSV — and get taxable profit and tax after the small-business relief band.",
                ["who"] = "Companies that want a quick corporate-tax estimate before preparing the formal return.",
                ["get"] = new List<object?> { "Taxable profit, relief band applied and tax due", "Loss / under-threshold handling (no tax when below relief)", "Compliance notes on adjustments", "Downloadable CSV + print/PDF, save to your account" },
                ["how"] = "The corporate-tax rate and relief threshold are driven by your registered country (e.g. UAE 0% on the first AED 375,000 then 9%).",
                ["csv"] = "type,amount — e.g. revenue,1000000 / expense,400000 / adjustment,25000",
            },
            "payroll" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Calculate monthly net pay and end-of-service gratuity for an employee from basic salary, allowances, deductions and years of service.",
                ["who"] = "Employers and HR/payroll staff who need a quick net-pay and gratuity figure.",
                ["get"] = new List<object?> { "Gross pay, deductions and net pay", "Statutory end-of-service gratuity for the years served", "Country currency applied", "Save / print the calculation" },
                ["how"] = "Gratuity rules and currency follow your registered country's labour law (e.g. UAE 21/30 days per year banding).",
            },
            "ifrs" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Build an IFRS-style income statement and balance sheet. Type the figures or upload a trial-balance CSV — the tool maps accounts, computes profit and runs a balance check.",
                ["who"] = "Owners, accountants and founders who want quick IFRS-format financial statements.",
                ["get"] = new List<object?> { "Income statement (revenue → operating profit)", "Balance sheet with an assets = equity + liabilities check", "Trial-balance totals and completeness flags", "Downloadable CSV + print/PDF" },
                ["how"] = "Currency comes from your registered country; the IFRS structure is the same worldwide.",
                ["csv"] = "account,debit,credit,classification — e.g. Sales,0,500000,revenue / COGS,300000,0,cogs",
            },
            "einvoice" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Generate a compliant electronic invoice with the correct tax and a network endpoint (Peppol/PINT) for your country. Add line items and get a tax invoice with a UUID.",
                ["who"] = "Businesses moving to mandatory e-invoicing (UAE FTA / PINT-AE, Saudi ZATCA/FATOORA, EU Peppol…).",
                ["get"] = new List<object?> { "Line totals, tax and grand total", "Country e-invoice scheme, network and endpoint", "Invoice number + UUID", "Print/PDF and save" },
                ["how"] = "The e-invoice scheme and network are selected automatically from your registered country — UAE PINT-AE, Saudi ZATCA, EU Peppol, with a generic fallback elsewhere.",
            },
            "extreport" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Produce an external filing pack — the VAT/GST return and the corporate-tax return together — formatted to your country's authority and return names.",
                ["who"] = "Finance teams preparing the periodic submissions they send to the tax authority.",
                ["get"] = new List<object?> { "VAT return pack (e.g. UAE VAT 201) and corporate-tax return pack", "The official filing authority and return names for your country", "Compliance checks on both packs", "Downloadable CSV + print/PDF" },
                ["how"] = "Authority, scheme and return names resolve from your registered country (UAE → FTA / VAT 201; Saudi → ZATCA).",
                ["csv"] = "VAT rows (type,category,amount,trn) and/or CT rows (type,amount) — both are read.",
            },
            "customs" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Work out landed cost for an import: CIF (goods + freight + insurance), customs duty by HS code, and import VAT. Enter one line or upload a multi-line CSV.",
                ["who"] = "Importers, traders and logistics teams estimating duty and total landed cost.",
                ["get"] = new List<object?> { "CIF value, duty (HS-driven) and import VAT", "Total payable and landed cost", "Compliance checks for missing/unknown HS codes and zero-value lines", "Downloadable CSV + print/PDF" },
                ["how"] = "Duty rates, HS overrides and import-VAT rate come from your registered country's customs pack (e.g. UAE/Dubai Customs Mirsal 2).",
                ["csv"] = "hs_code,qty,unit_value — e.g. 8516,10,250 (freight/insurance taken from the fields above).",
            },
            "insurance" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Estimate an insurance premium and see the cover that is compulsory or recommended for your country, with a renewal/expiry status.",
                ["who"] = "Businesses budgeting cover and tracking when policies must be renewed.",
                ["get"] = new List<object?> { "Premium from sum insured × rate (or a flat premium)", "Compulsory / recommended cover for your country", "Renewal status with an imminent-expiry warning", "Save / print the summary" },
                ["how"] = "The recommended-cover list and currency follow your registered country.",
            },
            "docexpiry" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Track licences, permits, IDs and contracts by expiry date. Get days-left, a status (valid/expiring/expired) and the next reminder date. Upload many documents via CSV.",
                ["who"] = "Operations, HR and admin teams managing trade licences, visas, passports and contracts.",
                ["get"] = new List<object?> { "Days-left and status per document", "Next reminder date from your lead-day thresholds", "Compliance flags for expired and soon-to-expire documents", "Downloadable CSV + print/PDF" },
                ["how"] = "Default reminder lead days follow your country; you can override them per document.",
                ["csv"] = "title,expiry,reminder_days — e.g. Trade Licence,2026-03-31,90,60,30",
            },
            "valuation" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Value your company three ways — discounted cash flow, EBITDA multiple and revenue multiple — and bridge enterprise value to equity value by removing net debt.",
                ["who"] = "Founders, buyers/sellers and advisors needing a fast, defensible valuation range.",
                ["get"] = new List<object?> { "DCF, EBITDA-multiple and revenue-multiple valuations", "Enterprise value → equity value bridge (EV − net debt)", "A blended equity value", "Downloadable CSV + print/PDF" },
                ["how"] = "Currency comes from your registered country; you control growth, discount rate and multiples.",
                ["csv"] = "Optional P&L CSV: type,amount (revenue / expense) to derive revenue and EBITDA.",
            },
            "finmodel" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Project a multi-year P&L — revenue, gross profit, EBITDA and net profit — from a base revenue, growth rate and margin assumptions, then export it.",
                ["who"] = "Anyone building a business plan, budget or fundraising model.",
                ["get"] = new List<object?> { "Year-by-year revenue, gross profit, EBITDA and net profit", "Up to 10 years of projection", "Downloadable CSV of the full model", "Save / print" },
                ["how"] = "Currency is set by your registered country; all drivers (growth, margins, tax) are yours to set.",
                ["csv"] = "Optional P&L CSV: type,amount (revenue) to set the base-year revenue.",
            },
            "taxkit" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "A one-screen tax snapshot for your country: VAT/GST rate, corporate-tax rate and relief threshold, e-invoice scheme and network, filing authority, return names and fiscal-year start.",
                ["who"] = "Anyone wanting a quick reference of the tax and compliance landscape for a country.",
                ["get"] = new List<object?> { "11 key tax & compliance facts at a glance", "E-invoice scheme + network for the country", "Authority and VAT/CT return names", "Save / print the snapshot" },
                ["how"] = "Reads your registered country automatically; use “change” to preview another country for comparison.",
            },
            "hrcompliance" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "A country labour-law card — end-of-service, leave, notice, probation, working hours, wage protection and the official authority link — plus an optional per-employee compliance check and end-of-service liability.",
                ["who"] = "Employers and HR teams that need to stay compliant with local labour law worldwide.",
                ["get"] = new List<object?> { "Statutory labour-law card for your country", "Official government authority link", "Optional per-employee compliance check + end-of-service liability", "Save / print" },
                ["how"] = "Uses the same labour-law engine as the full ERP, driven by your registered country (UAE → MOHRE/WPS, UK → GOV.UK, Saudi → HRSD).",
            },
            "workflow" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["what"] = "Design a spend-approval matrix — who approves what at each value tier — with SLA targets per step.",
                ["who"] = "Finance and operations teams setting up purchase/expense approval rules.",
                ["get"] = new List<object?> { "A tiered approval chain with limits", "Approver and SLA per tier", "Country currency on the limits", "Save / print the matrix" },
                ["how"] = "Currency on the limits follows your registered country.",
            },
            _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["what"] = "", ["who"] = "", ["get"] = new List<object?>(), ["how"] = "" },
        };
    }

    public static Dictionary<string, object?> Preview(string key)
    {
        return key switch
        {
            "vat" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Net VAT payable", ["value"] = "AED 3,000" },
                ["rows"] = new List<object?> { new List<object?> { "Output VAT (5%)", "AED 5,000" }, new List<object?> { "Recoverable input VAT", "AED 2,000" }, new List<object?> { "Standard-rated sales", "AED 100,000" } },
                ["chip"] = "Pass · computed at the 5% country rate (UAE FTA)",
            },
            "ct" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Corporate tax (9%)", ["value"] = "AED 20,250" },
                ["rows"] = new List<object?> { new List<object?> { "Taxable profit", "AED 600,000" }, new List<object?> { "Relief (0%) band", "AED 375,000" }, new List<object?> { "Taxable above relief", "AED 225,000" } },
                ["chip"] = "Pass · UAE small-business relief applied",
            },
            "payroll" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Net monthly pay", ["value"] = "AED 9,200" },
                ["rows"] = new List<object?> { new List<object?> { "Gross pay", "AED 10,000" }, new List<object?> { "Deductions", "AED 800" }, new List<object?> { "End-of-service gratuity (3 yrs)", "AED 5,250" } },
                ["chip"] = "UAE Federal Decree-Law 33/2021",
            },
            "ifrs" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Balance check", ["value"] = "Balanced ✓" },
                ["rows"] = new List<object?> { new List<object?> { "Revenue", "AED 1,000,000" }, new List<object?> { "Gross profit", "AED 620,000" }, new List<object?> { "Operating profit", "AED 240,000" } },
                ["chip"] = "Assets = equity + liabilities",
            },
            "einvoice" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Invoice total", ["value"] = "AED 10,500" },
                ["rows"] = new List<object?> { new List<object?> { "Subtotal", "AED 10,000" }, new List<object?> { "VAT (5%)", "AED 500" }, new List<object?> { "Scheme", "FTA (PINT-AE) + QR" } },
                ["chip"] = "Country e-invoice scheme applied",
            },
            "extreport" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "VAT 201 net payable", ["value"] = "AED 814" },
                ["rows"] = new List<object?> { new List<object?> { "Output VAT (Box 12)", "AED 8,052" }, new List<object?> { "Input VAT (Box 13)", "AED 7,238" }, new List<object?> { "Compliance", "5 issues flagged" } },
                ["chip"] = "FTA VAT 201 format · drill-down",
            },
            "customs" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Landed cost", ["value"] = "AED 13,450" },
                ["rows"] = new List<object?> { new List<object?> { "CIF value", "AED 11,000" }, new List<object?> { "Customs duty (5%)", "AED 550" }, new List<object?> { "Import VAT (5%)", "AED 578" } },
                ["chip"] = "HS-code driven duty",
            },
            "insurance" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Cover & renewal", ["value"] = "3 covers · 1 due" },
                ["rows"] = new List<object?> { new List<object?> { "Compulsory covers", "2" }, new List<object?> { "Recommended covers", "3" }, new List<object?> { "Next renewal", "in 28 days" } },
                ["chip"] = "Country cover profile",
            },
            "docexpiry" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Expiring soon", ["value"] = "2 documents" },
                ["rows"] = new List<object?> { new List<object?> { "Trade licence", "18 days · Warning" }, new List<object?> { "Visa", "45 days · OK" }, new List<object?> { "Reminder lead", "30 days" } },
                ["chip"] = "Country reminder lead days",
            },
            "valuation" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Equity value", ["value"] = "AED 4.2M" },
                ["rows"] = new List<object?> { new List<object?> { "Enterprise value (EBITDA ×6)", "AED 4,800,000" }, new List<object?> { "Net debt", "AED 600,000" }, new List<object?> { "Revenue multiple", "3.0x" } },
                ["chip"] = "DCF + EBITDA + revenue multiples",
            },
            "finmodel" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "5-year projection", ["value"] = "Net AED 248k" },
                ["rows"] = new List<object?> { new List<object?> { "Year 1 revenue", "AED 1,000,000" }, new List<object?> { "Year 5 revenue", "AED 1,610,000" }, new List<object?> { "Year 5 EBITDA", "AED 332,000" } },
                ["chip"] = "Multi-year P&L + cash snapshot",
            },
            "taxkit" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "UAE tax snapshot", ["value"] = "VAT 5% · CT 9%" },
                ["rows"] = new List<object?> { new List<object?> { "Corporate tax relief", "first AED 375,000" }, new List<object?> { "E-invoice scheme", "FTA (PINT-AE)" }, new List<object?> { "Authority", "Federal Tax Authority" } },
                ["chip"] = "Country-driven snapshot",
            },
            "hrcompliance" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "UAE labour card", ["value"] = "Compliant" },
                ["rows"] = new List<object?> { new List<object?> { "Gratuity", "21 / 30 days per year" }, new List<object?> { "Annual leave", "30 days" }, new List<object?> { "Authority", "MOHRE" } },
                ["chip"] = "Country labour law",
            },
            "workflow" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Approval matrix", ["value"] = "3 tiers" },
                ["rows"] = new List<object?> { new List<object?> { "Up to AED 5,000", "Manager" }, new List<object?> { "Up to AED 50,000", "Finance" }, new List<object?> { "Above AED 50,000", "CFO · SLA 24h" } },
                ["chip"] = "Adopt today",
            },
            _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["head"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "", ["value"] = "" }, ["rows"] = new List<object?>(), ["chip"] = "" },
        };
    }
}
