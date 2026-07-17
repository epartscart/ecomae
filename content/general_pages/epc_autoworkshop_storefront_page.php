<?php
/**
 * Storefront — Auto Workshop Online landing (/auto-workshop).
 * CMS content row points here; keep parser in HTML mode at end (eval'd into template).
 */
$langPrefix = '';
$uri = (string) ($_SERVER['REQUEST_URI'] ?? '');
if (preg_match('#^/(en|ar|ru|de|fr|es|it|pt|tr|zh|ja|ko)(/|$)#i', $uri, $m)) {
	$langPrefix = '/' . strtolower($m[1]);
}
$cp = isset($GLOBALS['DP_Config']->backend_dir) ? (string) $GLOBALS['DP_Config']->backend_dir : 'cp';
$guideUrl = '/' . $cp . '/control/portal/epc_autoworkshop_guide';
$partsSearch = $langPrefix . '/parts';
$katalog = $langPrefix . '/katalog-laximo';
?>
<style>
.epc-awo{--awo-ink:#0f172a;--awo-muted:#475569;--awo-accent:#0284c7;--awo-bg:#e0f2fe;font-family:"Segoe UI",system-ui,sans-serif;color:var(--awo-ink)}
.epc-awo__hero{background:linear-gradient(135deg,#0c4a6e 0%,#0284c7 55%,#7dd3fc 100%);color:#f8fafc;padding:48px 24px 56px;margin:0 -15px 28px}
.epc-awo__hero h1{margin:0 0 10px;font-size:clamp(1.75rem,4vw,2.4rem);font-weight:700;letter-spacing:-.02em}
.epc-awo__hero p{margin:0 0 20px;max-width:36rem;font-size:1.05rem;line-height:1.55;opacity:.95}
.epc-awo__cta{display:inline-flex;align-items:center;gap:8px;margin:0 10px 8px 0;padding:12px 18px;border-radius:8px;background:#fff;color:#0c4a6e;font-weight:600;text-decoration:none}
.epc-awo__cta--ghost{background:transparent;border:1px solid rgba(255,255,255,.55);color:#fff}
.epc-awo__grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:16px;margin:0 0 28px}
.epc-awo__card h3{margin:0 0 8px;font-size:1.05rem}
.epc-awo__card p{margin:0;color:var(--awo-muted);line-height:1.5;font-size:.95rem}
.epc-awo__card{padding:18px 0;border-top:1px solid #e2e8f0}
.epc-awo__note{color:var(--awo-muted);font-size:.9rem}
</style>
<section class="epc-awo">
	<div class="epc-awo__hero">
		<h1>Auto Workshop Online</h1>
		<p>Book service, identify the vehicle, pull the right OEM parts, and keep labour + parts on one job card — from check-in to invoice.</p>
		<a class="epc-awo__cta" href="<?php echo htmlspecialchars($partsSearch, ENT_QUOTES, 'UTF-8'); ?>">Find parts</a>
		<a class="epc-awo__cta epc-awo__cta--ghost" href="<?php echo htmlspecialchars($katalog, ENT_QUOTES, 'UTF-8'); ?>">OEM vehicle catalog</a>
	</div>

	<div class="epc-awo__grid">
		<div class="epc-awo__card">
			<h3>1. Vehicle check-in</h3>
			<p>Plate, VIN, odometer, and customer complaint — ready for the advisor desk.</p>
		</div>
		<div class="epc-awo__card">
			<h3>2. Job card</h3>
			<p>Labour hours plus parts from warehouse or OEM search, with estimate approval when needed.</p>
		</div>
		<div class="epc-awo__card">
			<h3>3. Repair &amp; QC</h3>
			<p>Track status through repair and quality check before handover.</p>
		</div>
		<div class="epc-awo__card">
			<h3>4. Invoice</h3>
			<p>Parts + labour billed together; warranty and history stay with the vehicle.</p>
		</div>
	</div>

	<p class="epc-awo__note">
		Workshop operators: open the
		<a href="<?php echo htmlspecialchars($guideUrl, ENT_QUOTES, 'UTF-8'); ?>">Control Panel guide</a>
		for CP workflow and Client ERP links.
	</p>
</section>
<?php
?>
