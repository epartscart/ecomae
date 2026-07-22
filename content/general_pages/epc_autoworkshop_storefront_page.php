<?php
/**
 * Storefront — Auto Workshop Online (/auto-workshop)
 * Book service + track job status.
 */
$langPrefix = '';
$uri = (string) ($_SERVER['REQUEST_URI'] ?? '');
if (preg_match('#^/(en|ar|ru|de|fr|es|it|pt|tr|zh|ja|ko)(/|$)#i', $uri, $m)) {
	$langPrefix = '/' . strtolower($m[1]);
}
$cp = isset($GLOBALS['DP_Config']->backend_dir) ? (string) $GLOBALS['DP_Config']->backend_dir : 'cp';
$guideUrl = '/' . $cp . '/control/portal/epc_autoworkshop_guide';
$lp = $langPrefix !== '' ? $langPrefix : '/en';
$partsSearch = $lp . '/parts';
$katalog = $lp . '/katalog-laximo';
$ajaxPublic = '/content/shop/workshop/ajax_workshop_public.php';
$h = static function ($v): string {
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
};
?>
<style>
.epc-awo{--ink:#111827;--muted:#6b7280;--accent:#dc2626;--accent-dark:#991b1b;--sand:#fff5f5;--line:#fecaca;font-family:"DM Sans","Segoe UI",sans-serif;color:var(--ink)}
.epc-awo__hero{position:relative;margin:0 -15px 28px;padding:56px 24px 64px;overflow:hidden;
background:
 radial-gradient(700px 280px at 88% 8%, rgba(254,202,202,.35), transparent 55%),
 linear-gradient(125deg,#450a0a 0%,#7f1d1d 38%,#dc2626 100%);
background-size:cover;color:#ffffff!important}
.epc-awo__hero::after{content:"";position:absolute;inset:0;background:url("data:image/svg+xml,%3Csvg width='60' height='60' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M0 60V0h60' fill='none' stroke='%23ffffff' stroke-opacity='.07'/%3E%3C/svg%3E");pointer-events:none}
.epc-awo__hero-inner{position:relative;z-index:1;max-width:720px;color:#ffffff!important}
.epc-awo__brand{display:inline-block;font-size:12px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:#fecaca!important;margin-bottom:10px}
.epc-awo__hero h1,.epc-awo__hero .epc-awo__title{margin:0 0 12px;font-size:clamp(2rem,4.5vw,2.75rem);font-weight:800;letter-spacing:-.03em;line-height:1.1;color:#ffffff!important;text-shadow:0 1px 2px rgba(0,0,0,.25)}
.epc-awo__hero p,.epc-awo__hero .epc-awo__lead{margin:0 0 22px;font-size:1.05rem;line-height:1.55;max-width:34rem;color:#fee2e2!important;opacity:1!important}
.epc-awo__cta{display:inline-flex;align-items:center;gap:8px;margin:0 10px 8px 0;padding:12px 18px;border-radius:8px;background:#ffffff;color:#991b1b!important;font-weight:700;text-decoration:none!important;border:1px solid #ffffff;box-shadow:0 4px 14px rgba(0,0,0,.18)}
.epc-awo__cta:hover,.epc-awo__cta:focus{background:#fef2f2;color:#7f1d1d!important;text-decoration:none!important}
.epc-awo__cta--ghost{background:transparent;border:1px solid rgba(255,255,255,.75);color:#ffffff!important;box-shadow:none}
.epc-awo__cta--ghost:hover,.epc-awo__cta--ghost:focus{background:rgba(255,255,255,.14);color:#ffffff!important}
.epc-awo__wrap{max-width:1100px;margin:0 auto 40px;padding:0 8px}
.epc-awo__grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:14px;margin:0 0 28px}
.epc-awo__step{padding:14px 14px 16px;border-radius:12px;background:#fff;border:1px solid var(--line);box-shadow:0 2px 10px rgba(127,29,29,.05)}
.epc-awo__step strong{display:block;font-size:1rem;margin-bottom:4px;color:#991b1b}
.epc-awo__step span{color:var(--muted);font-size:.92rem;line-height:1.45}
.epc-awo__panels{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:18px}
.epc-awo__panel{background:var(--sand);border:1px solid var(--line);border-radius:14px;padding:20px}
.epc-awo__panel h2{margin:0 0 12px;font-size:1.15rem;color:#7f1d1d}
.epc-awo__panel label{display:block;font-size:12px;font-weight:700;margin:0 0 4px;color:#7f1d1d}
.epc-awo__panel input,.epc-awo__panel textarea,.epc-awo__panel select{width:100%;margin:0 0 10px;padding:9px 11px;border:1px solid #f1a9a9;border-radius:8px;font:inherit;background:#fff}
.epc-awo__panel input:focus,.epc-awo__panel textarea:focus,.epc-awo__panel select:focus{outline:0;border-color:#dc2626;box-shadow:0 0 0 3px rgba(220,38,38,.15)}
.epc-awo__panel button{appearance:none;border:0;border-radius:8px;background:#dc2626;color:#fff;font-weight:700;padding:11px 16px;cursor:pointer}
.epc-awo__panel button:hover{background:#b91c1c}
.epc-awo__msg{display:none;margin:0 0 10px;padding:10px 12px;border-radius:8px;font-size:13px}
.epc-awo__msg.is-ok{display:block;background:#dcfce7;color:#166534}
.epc-awo__msg.is-err{display:block;background:#fee2e2;color:#991b1b}
.epc-awo__track-result{display:none;margin-top:12px;padding:12px;background:#fff;border-radius:10px;border:1px solid var(--line);font-size:14px;line-height:1.5}
.epc-awo__note{margin:24px 0 0;color:var(--muted);font-size:.9rem}
.epc-awo__note a{color:#b91c1c;font-weight:600}
@keyframes epcAwoFade{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:none}}
.epc-awo__hero-inner,.epc-awo__grid,.epc-awo__panels{animation:epcAwoFade .55s ease both}
.epc-awo__grid{animation-delay:.08s}
.epc-awo__panels{animation-delay:.16s}
</style>

<section class="epc-awo">
	<div class="epc-awo__hero">
		<div class="epc-awo__hero-inner">
			<span class="epc-awo__brand">eParts Cart · Auto Workshop</span>
			<h1 class="epc-awo__title">Repair workshop</h1>
			<p class="epc-awo__lead">Book service, track your job, and keep parts + labour on one garage job card — from check-in to ready for collection.</p>
			<a class="epc-awo__cta" href="#book">Book service</a>
			<a class="epc-awo__cta epc-awo__cta--ghost" href="#track">Track job</a>
		</div>
	</div>

	<div class="epc-awo__wrap">
		<div class="epc-awo__grid">
			<div class="epc-awo__step"><strong>1. Check-in</strong><span>Plate, VIN, odometer, and what needs fixing.</span></div>
			<div class="epc-awo__step"><strong>2. Estimate</strong><span>Labour hours and parts on one job card for approval.</span></div>
			<div class="epc-awo__step"><strong>3. Repair &amp; QC</strong><span>Bay and technician assigned; quality check before handover.</span></div>
			<div class="epc-awo__step"><strong>4. Ready</strong><span>Collect your vehicle; invoice history stays with the job.</span></div>
		</div>

		<div class="epc-awo__panels">
			<div class="epc-awo__panel" id="book">
				<h2>Book a service</h2>
				<div class="epc-awo__msg" id="book-msg"></div>
				<form id="epc-awo-book">
					<label>Your name *</label>
					<input name="customer_name" required>
					<label>Phone *</label>
					<input name="customer_phone" required placeholder="+971…">
					<label>E-mail</label>
					<input name="customer_email" type="email">
					<label>Plate *</label>
					<input name="plate" required placeholder="D-12345">
					<label>Make / model / year</label>
					<input name="make" placeholder="Make" style="margin-bottom:6px">
					<input name="model" placeholder="Model" style="margin-bottom:6px">
					<input name="year" placeholder="Year">
					<label>VIN</label>
					<input name="vin">
					<label>Odometer (km)</label>
					<input name="odometer" type="number" min="0">
					<label>What needs doing? *</label>
					<textarea name="complaint" rows="3" required></textarea>
					<button type="submit">Submit booking</button>
				</form>
			</div>

			<div class="epc-awo__panel" id="track">
				<h2>Track your job</h2>
				<div class="epc-awo__msg" id="track-msg"></div>
				<form id="epc-awo-track">
					<label>Job number or plate *</label>
					<input name="ref" required placeholder="WS-… or D-12345">
					<label>Phone (optional, for privacy match)</label>
					<input name="phone" placeholder="+971…">
					<button type="submit">Check status</button>
				</form>
				<div class="epc-awo__track-result" id="track-result"></div>
				<p style="margin:16px 0 0;font-size:13px;color:#5b6578">
					Need parts while you wait?
					<a href="<?php echo $h($partsSearch); ?>">Parts search</a> ·
					<a href="<?php echo $h($katalog); ?>">OEM catalog</a>
				</p>
			</div>
		</div>

		<p class="epc-awo__note">
			Workshop staff: open the
			<a href="<?php echo $h($guideUrl); ?>">Control Panel garage guide</a>
			for the floor board, bays, and job cards.
		</p>
	</div>
</section>

<script>
(function(){
	var ajaxUrl = <?php echo json_encode($ajaxPublic, JSON_UNESCAPED_SLASHES); ?>;
	function msg(id, text, ok){
		var el = document.getElementById(id);
		if(!el) return;
		el.textContent = text || '';
		el.className = 'epc-awo__msg ' + (text ? (ok ? 'is-ok' : 'is-err') : '');
	}
	function post(action, form, cb){
		var fd = new FormData(form);
		fd.append('action', action);
		fetch(ajaxUrl, { method:'POST', body: fd, credentials:'same-origin' })
			.then(function(r){ return r.json(); })
			.then(cb)
			.catch(function(){ cb({ status:false, message:'Network error' }); });
	}
	var book = document.getElementById('epc-awo-book');
	if(book){
		book.addEventListener('submit', function(ev){
			ev.preventDefault();
			post('book', book, function(res){
				if(res && res.status){
					msg('book-msg', (res.message || 'Booked') + (res.job_no ? (' Job: ' + res.job_no) : ''), true);
					book.reset();
				} else {
					msg('book-msg', (res && res.message) || 'Could not book', false);
				}
			});
		});
	}
	var track = document.getElementById('epc-awo-track');
	if(track){
		track.addEventListener('submit', function(ev){
			ev.preventDefault();
			post('track', track, function(res){
				var box = document.getElementById('track-result');
				if(res && res.status && res.job){
					msg('track-msg', '', true);
					box.style.display = 'block';
					box.innerHTML = '<strong>' + res.job.job_no + '</strong> — ' + res.job.status_label +
						'<br>' + (res.job.plate || '') + ' · ' + (res.job.make || '') + ' ' + (res.job.model || '') +
						'<br>Customer: ' + (res.job.customer_name || '') +
						(res.job.grand_total ? ('<br>Job total: AED ' + Number(res.job.grand_total).toFixed(2)) : '');
				} else {
					box.style.display = 'none';
					msg('track-msg', (res && res.message) || 'Not found', false);
				}
			});
		});
	}
})();
</script>
<?php
?>
