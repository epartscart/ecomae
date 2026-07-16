<?php
/**
 * Marketing page — /blockchain
 * Full structure, process, and operator details for Blockchain BOS proof layer,
 * with dedicated premium 3D assets (not homepage/industry 3D).
 */
defined('_ASTEXE_') or die('No access');

function epc_ecomae_blockchain_page_assets(): string
{
	$cssPath = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_blockchain_3d.css';
	$jsPath = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_blockchain_3d.js';
	$v = '1';
	if (is_file($cssPath)) {
		$v = (string) filemtime($cssPath);
	}
	if (is_file($jsPath)) {
		$v = max((int) $v, (int) filemtime($jsPath));
		$v = (string) $v;
	}
	return '<link rel="stylesheet" href="/epc-static.php?f=content/general_pages/epc_ecomae_blockchain_3d.css&v='
		. rawurlencode($v) . '" />' . "\n"
		. '<script defer src="/epc-static.php?f=content/general_pages/epc_ecomae_blockchain_3d.js&v='
		. rawurlencode($v) . '"></script>' . "\n";
}

/**
 * @param array $params unused (route has no slug)
 */
function epc_ecomae_platform_page_blockchain(array $params = array()): string
{
	$base = rtrim(epc_ecomae_platform_base_url(), '/');
	$demoUrl = $base . '/platform/demo';
	$verifyUrl = '/epc-blockchain-verify.php';
	$docsUrl = $base . '/documentation';
	$bosUrl = $base . '/bos';

	ob_start();
	echo epc_ecomae_blockchain_page_assets();
	?>
<div id="ebc-page" class="ebc-page--3d" data-ebc-page="blockchain">
	<section class="ebc-hero" aria-labelledby="ebc-brand">
		<div class="ebc-hero__stage" aria-hidden="true">
			<div class="ebc-hero__ring"></div>
			<div class="ebc-hero__ring ebc-hero__ring--2"></div>
			<div class="ebc-hero__core"><i class="fa fa-link"></i></div>
			<div class="ebc-hero__chain">
				<div class="ebc-hero__block"><i class="fa fa-file-text-o"></i></div>
				<div class="ebc-hero__block"><i class="fa fa-cube"></i></div>
				<div class="ebc-hero__block"><i class="fa fa-check"></i></div>
				<div class="ebc-hero__block"><i class="fa fa-shield"></i></div>
				<div class="ebc-hero__block"><i class="fa fa-lock"></i></div>
			</div>
		</div>
		<div class="ebc-hero__content">
			<h1 class="ebc-brand" id="ebc-brand">ECOM AE<span>Blockchain BOS</span></h1>
			<p class="ebc-hero__headline">Cryptographic proof for every critical business fact — without leaving MySQL operations.</p>
			<p class="ebc-hero__lead">One Blockchain BOS Enterprise System: ERP, commerce, compliance, and workflows stay operational; the proof layer hashes, Merkle-anchors, and lets anyone verify authenticity.</p>
			<div class="ebc-cta">
				<a class="ebc-btn ebc-btn--primary" href="<?php echo epc_ecomae_h($demoUrl); ?>"><i class="fa fa-play-circle"></i> Request a demo</a>
				<a class="ebc-btn ebc-btn--ghost" href="<?php echo epc_ecomae_h($verifyUrl); ?>"><i class="fa fa-search"></i> Verify a proof</a>
			</div>
		</div>
	</section>

	<section class="ebc-section" id="structure" aria-labelledby="ebc-structure-title">
		<div class="ebc-section__head">
			<span class="ebc-kicker">Structure</span>
			<h2 id="ebc-structure-title">Five layers. One unified enterprise system.</h2>
			<p>MySQL remains the system of record. Blockchain proves selected facts — it does not store carts, sessions, or live stock.</p>
		</div>
		<div class="ebc-stack">
			<article class="ebc-layer">
				<div class="ebc-layer__num">1</div>
				<div>
					<h3>Commerce layer</h3>
					<p>Storefront, catalogue, checkout, B2B portal, multi-currency payments — day-to-day selling stays fast and familiar.</p>
				</div>
			</article>
			<article class="ebc-layer">
				<div class="ebc-layer__num">2</div>
				<div>
					<h3>Operations layer</h3>
					<p>Inventory with lot/serial/barcode, warehouses, procurement, GRN, RMA, and delivery workflows across tenants.</p>
				</div>
			</article>
			<article class="ebc-layer">
				<div class="ebc-layer__num">3</div>
				<div>
					<h3>Finance &amp; compliance</h3>
					<p>GL, AR/AP, VAT, Peppol e-invoicing, period locking — operational finance with audit-ready posting.</p>
				</div>
			</article>
			<article class="ebc-layer">
				<div class="ebc-layer__num">4</div>
				<div>
					<h3>Intelligence layer</h3>
					<p>AI advisor, forecasting, KPI recommendations, CRM pipeline — guidance on top of live operational data.</p>
				</div>
			</article>
			<article class="ebc-layer ebc-layer--proof">
				<div class="ebc-layer__num">5</div>
				<div>
					<h3>Blockchain proof layer</h3>
					<p>SHA-256 hashes of business facts, Merkle batch anchoring, and public verify — tamper-evident integrity for invoices, receipts, and returns.</p>
				</div>
			</article>
		</div>
	</section>

	<section class="ebc-section" id="process" aria-labelledby="ebc-process-title">
		<div class="ebc-section__head">
			<span class="ebc-kicker">Process</span>
			<h2 id="ebc-process-title">How a proof is created and verified</h2>
			<p>Best-effort after a successful business commit — proof recording never blocks an invoice, GRN, or RMA.</p>
		</div>
		<div class="ebc-process">
			<div class="ebc-step">
				<div class="ebc-step__orb">1</div>
				<h3>Commit</h3>
				<p>ERP saves the document (invoice, credit note, GRN, or RMA) in MySQL as usual.</p>
			</div>
			<div class="ebc-step">
				<div class="ebc-step__orb">2</div>
				<h3>Hash</h3>
				<p>Canonical payload → SHA-256 digest. Same facts always produce the same proof hash.</p>
			</div>
			<div class="ebc-step">
				<div class="ebc-step__orb">3</div>
				<h3>Anchor</h3>
				<p>Pending proofs batch into a Merkle root via the platform job <code>blockchain_anchor_batch</code>.</p>
			</div>
			<div class="ebc-step">
				<div class="ebc-step__orb">4</div>
				<h3>Verify</h3>
				<p>Anyone with the proof ID opens the public verify page or JSON API and checks integrity.</p>
			</div>
		</div>
	</section>

	<section class="ebc-section" id="modes" aria-labelledby="ebc-modes-title">
		<div class="ebc-section__head">
			<span class="ebc-kicker">Tenant modes</span>
			<h2 id="ebc-modes-title">Per-tenant blockchain policy</h2>
			<p>Operators set <code>blockchain_mode</code> on each tenant — proof stays controlled at fleet scale.</p>
		</div>
		<div class="ebc-modes">
			<div class="ebc-mode">
				<span class="ebc-mode__tag">Default</span>
				<h3>Anchor</h3>
				<p>Record SHA-256 proofs and Merkle-anchor batches. Production default for live enterprise tenants.</p>
			</div>
			<div class="ebc-mode">
				<span class="ebc-mode__tag">Reserved</span>
				<h3>Network</h3>
				<p>Reserved for permissioned network participation when an external anchor endpoint is wired.</p>
			</div>
			<div class="ebc-mode">
				<span class="ebc-mode__tag">Opt-out</span>
				<h3>Off</h3>
				<p>Disabled for that tenant — hooks skip silently so operations continue without proofs.</p>
			</div>
		</div>
	</section>

	<section class="ebc-section" id="records" aria-labelledby="ebc-records-title">
		<div class="ebc-section__head">
			<span class="ebc-kicker">What gets proven</span>
			<h2 id="ebc-records-title">Documents that auto-record proofs</h2>
			<p>Hooks fire after successful commit. If proof recording fails, the business transaction still stands.</p>
		</div>
		<ul class="ebc-facts">
			<li>
				<i class="fa fa-file-text-o" aria-hidden="true"></i>
				<div>
					<strong>Tax invoices</strong>
					<span>Validated e-invoices hashed on save — badge and verify link on the document view and print footer.</span>
				</div>
			</li>
			<li>
				<i class="fa fa-undo" aria-hidden="true"></i>
				<div>
					<strong>Credit notes</strong>
					<span>Credit-note save and create paths record a distinct <code>credit_note</code> proof identity.</span>
				</div>
			</li>
			<li>
				<i class="fa fa-truck" aria-hidden="true"></i>
				<div>
					<strong>Goods receipt (GRN)</strong>
					<span>Purchase receive with posted lines records a GRN proof and can surface a verify URL on success.</span>
				</div>
			</li>
			<li>
				<i class="fa fa-refresh" aria-hidden="true"></i>
				<div>
					<strong>RMA / returns</strong>
					<span>After-sales and warranty RMA create paths attach proofs for return authenticity.</span>
				</div>
			</li>
		</ul>
	</section>

	<section class="ebc-section" id="operators" aria-labelledby="ebc-ops-title">
		<div class="ebc-surfaces">
			<div>
				<div class="ebc-section__head">
					<span class="ebc-kicker">Operator surfaces</span>
					<h2 id="ebc-ops-title">Where proofs appear in the product</h2>
					<p>From a single invoice print to the Super CP fleet — proof is visible where work happens.</p>
				</div>
				<ul class="ebc-surfaces__list">
					<li>
						<strong>Invoice &amp; e-invoice detail</strong>
						Status badge plus Verify deep-link on the document screen.
					</li>
					<li>
						<strong>Invoice print</strong>
						Proof block and absolute verify URL after totals — audit-ready paper trail.
					</li>
					<li>
						<strong>ERP → Tax → Blockchain proofs</strong>
						Tenant-scoped list of proofs (also under Audit workbench).
					</li>
					<li>
						<strong>Super CP → Tenant Hub → Blockchain</strong>
						Fleet view across tenants with filters and aggregate stats.
					</li>
					<li>
						<strong>Public verify</strong>
						HTML form and <code>?format=json</code> API at <a href="<?php echo epc_ecomae_h($verifyUrl); ?>">/epc-blockchain-verify.php</a>.
					</li>
				</ul>
			</div>
			<aside class="ebc-surfaces__aside">
				<h3>Platform data model</h3>
				<p><strong>epc_bc_proofs</strong> stores per business-fact proofs. <strong>epc_bc_anchor_batches</strong> holds Merkle roots and anchor references. Cron drains pending proofs through the existing platform jobs worker.</p>
				<div class="ebc-cta">
					<a class="ebc-btn ebc-btn--ghost" href="<?php echo epc_ecomae_h($docsUrl); ?>">Documentation</a>
					<a class="ebc-btn ebc-btn--ghost" href="<?php echo epc_ecomae_h($bosUrl); ?>">BOS knowledge</a>
				</div>
			</aside>
		</div>
	</section>

	<section class="ebc-section ebc-closing" aria-labelledby="ebc-close-title">
		<div class="ebc-section__head">
			<span class="ebc-kicker">Next step</span>
			<h2 id="ebc-close-title">See Blockchain BOS on your industry pack</h2>
			<p>Spin a demo tenant, issue a document, and verify the proof publicly — same stack that runs production fleets.</p>
		</div>
		<div class="ebc-cta">
			<a class="ebc-btn ebc-btn--primary" href="<?php echo epc_ecomae_h($demoUrl); ?>">Get a 3-day demo</a>
			<a class="ebc-btn ebc-btn--ghost" href="<?php echo epc_ecomae_h($verifyUrl); ?>">Open verify</a>
		</div>
	</section>
</div>
	<?php
	return ob_get_clean();
}
