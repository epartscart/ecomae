<?php
/**
 * Integrations hub — Super CP + Tenant CP central listing of platform integrations.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_integrations_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	echo '<div class="alert alert-warning">Admin login required.</div>';
	return;
}

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$rows = epc_integrations_hub_rows($pdo, $isSuper);
$categories = epc_integrations_categories();
$backend = epc_int_backend();
$guideUrl = '/' . $backend . '/control/portal/epc_integrations_guide';
$marketLabel = 'United Arab Emirates';
if (!$isSuper) {
	$marketFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_country_profile.php';
	if (is_readable($marketFile)) {
		require_once $marketFile;
		$marketLabel = epc_tenant_country_market_label($pdo);
	}
}

$activeCount = 0;
$guideCount = 0;
foreach ($rows as $row) {
	if (!empty($row['active'])) {
		$activeCount++;
	}
	if (!empty($row['guide'])) {
		$guideCount++;
	}
}
$totalCount = count($rows);

$byCategory = array();
foreach ($categories as $catKey => $catMeta) {
	$byCategory[$catKey] = array();
}
foreach ($rows as $row) {
	$cat = (string) ($row['category'] ?? 'platform');
	if (!isset($byCategory[$cat])) {
		$byCategory[$cat] = array();
	}
	$byCategory[$cat][] = $row;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'class' => 'epc-inthub',
));
?>

<div id="epc-inthub-root" class="epc-inthub-shell">
	<div class="epc-inthub-brand">
		<div>
			<div class="epc-inthub-brand__mark"><?php echo $isSuper ? 'Ecomae · Super CP' : 'EPartsCart · Tenant CP'; ?></div>
			<h2>Integrations Hub</h2>
			<p>Enable, configure, and test every connected module from one place. Each card links to settings and an operator guide.</p>
		</div>
		<div class="epc-inthub-brand__actions">
			<a class="btn btn-sm btn-primary" href="<?php echo epc_int_h($guideUrl); ?>"><i class="fa fa-book"></i> Full guide</a>
			<?php if ($isSuper) { ?>
			<a class="btn btn-sm btn-default" href="/<?php echo epc_int_h($backend); ?>/control/portal/epc_tenant_features"><i class="fa fa-sliders"></i> Tenant features</a>
			<a class="btn btn-sm btn-default" href="/<?php echo epc_int_h($backend); ?>/control/portal/epc_mobile_apps"><i class="fa fa-mobile-alt"></i> Mobile apps</a>
			<?php } else { ?>
			<a class="btn btn-sm btn-default" href="/<?php echo epc_int_h($backend); ?>/control/portal/epc_tenant_email_settings"><i class="fa fa-envelope"></i> Email / SMTP</a>
			<a class="btn btn-sm btn-default" href="/<?php echo epc_int_h($backend); ?>/control/portal/epc_mobile_apps"><i class="fa fa-mobile-alt"></i> Mobile apps</a>
			<?php } ?>
		</div>
	</div>

	<div class="epc-inthub-stats">
		<div class="epc-inthub-stat">
			<div class="epc-inthub-stat__val"><?php echo (int) $activeCount; ?></div>
			<div class="epc-inthub-stat__lbl">Active</div>
		</div>
		<div class="epc-inthub-stat">
			<div class="epc-inthub-stat__val"><?php echo (int) $totalCount; ?></div>
			<div class="epc-inthub-stat__lbl">In catalog</div>
		</div>
		<div class="epc-inthub-stat">
			<div class="epc-inthub-stat__val"><?php echo (int) $guideCount; ?></div>
			<div class="epc-inthub-stat__lbl">Guides ready</div>
		</div>
	</div>

	<?php if (!$isSuper) { ?>
	<div class="epc-inthub-market">
		<i class="fa fa-globe"></i>
		<div>
			<strong>Your market:</strong> <?php echo epc_int_h($marketLabel); ?>
			<span> — Tax, Auto Price discovery sources, and ERP defaults follow your registered country.</span>
		</div>
	</div>
	<?php } ?>

	<div class="epc-inthub-toolbar">
		<div class="epc-inthub-search">
			<i class="fa fa-search"></i>
			<input type="search" data-inthub-search placeholder="Search integrations…" aria-label="Search integrations">
		</div>
		<div class="epc-inthub-chips">
			<button type="button" class="epc-inthub-chip is-active" data-inthub-chip="all">All</button>
			<?php foreach ($categories as $catKey => $catMeta) {
				if (empty($byCategory[$catKey])) {
					continue;
				}
				?>
			<button type="button" class="epc-inthub-chip" data-inthub-chip="<?php echo epc_int_h($catKey); ?>">
				<?php echo epc_int_h($catMeta['label']); ?>
			</button>
			<?php } ?>
		</div>
	</div>

	<div class="epc-inthub-empty" data-inthub-empty>
		No integrations match your filter. Clear search or choose another category.
	</div>

	<?php foreach ($categories as $catKey => $catMeta) {
		$items = $byCategory[$catKey] ?? array();
		if (!$items) {
			continue;
		}
		?>
	<section class="epc-inthub-section" data-inthub-section data-category="<?php echo epc_int_h($catKey); ?>">
		<div class="epc-inthub-section__head">
			<h3><i class="fa <?php echo epc_int_h($catMeta['icon']); ?>"></i><?php echo epc_int_h($catMeta['label']); ?></h3>
			<span><?php echo epc_int_h($catMeta['blurb']); ?></span>
		</div>
		<div class="epc-inthub-grid">
			<?php foreach ($items as $row) {
				$searchHay = strtolower($row['label'] . ' ' . $row['blurb'] . ' ' . $row['key'] . ' ' . $catMeta['label']);
				$canConfigure = !empty($row['configure_url']) && (empty($row['super_only']) || $isSuper);
				?>
			<article
				class="epc-inthub-card<?php echo empty($row['active']) ? ' is-inactive' : ''; ?>"
				data-inthub-card
				data-category="<?php echo epc_int_h($row['category']); ?>"
				data-search="<?php echo epc_int_h($searchHay); ?>"
			>
				<div class="epc-inthub-card__top">
					<div class="epc-inthub-card__icon" style="background:<?php echo epc_int_h($row['color']); ?>">
						<i class="fa <?php echo epc_int_h($row['icon']); ?>"></i>
					</div>
					<div>
						<span class="epc-inthub-pill <?php echo !empty($row['active']) ? 'epc-inthub-pill--on' : 'epc-inthub-pill--off'; ?>">
							<?php echo !empty($row['active']) ? 'Active' : 'Inactive'; ?>
						</span>
						<?php if (!empty($row['super_only'])) { ?>
						<span class="epc-inthub-pill epc-inthub-pill--super">Super CP</span>
						<?php } ?>
					</div>
				</div>
				<h4 class="epc-inthub-card__title"><?php echo epc_int_h($row['label']); ?></h4>
				<p class="epc-inthub-card__blurb"><?php echo epc_int_h($row['blurb']); ?></p>
				<div class="epc-inthub-card__actions">
					<?php if ($canConfigure) { ?>
					<a class="btn btn-sm btn-primary" href="<?php echo epc_int_h($row['configure_url']); ?>"><i class="fa fa-cog"></i> Configure</a>
					<?php } elseif (!empty($row['super_only']) && !$isSuper) { ?>
					<span class="epc-inthub-card__muted">Configured on ecomae.com</span>
					<?php } ?>
					<?php if (!empty($row['guide'])) { ?>
					<a class="btn btn-sm btn-default" href="<?php echo epc_int_h($row['guide']); ?>"><i class="fa fa-book"></i> Guide</a>
					<?php } ?>
				</div>
			</article>
			<?php } ?>
		</div>
	</section>
	<?php } ?>

	<div class="epc-inthub-playbook">
		<div class="epc-inthub-step">
			<div class="epc-inthub-step__n">1</div>
			<h4>Enable</h4>
			<p>Super CP toggles features per tenant under <a href="/<?php echo epc_int_h($backend); ?>/control/portal/epc_tenant_features">Tenant features</a>.</p>
		</div>
		<div class="epc-inthub-step">
			<div class="epc-inthub-step__n">2</div>
			<h4>Configure</h4>
			<p>Open <em>Configure</em> on each card — SMTP, payment keys, pixels, store URLs, and API clients.</p>
		</div>
		<div class="epc-inthub-step">
			<div class="epc-inthub-step__n">3</div>
			<h4>Test &amp; go live</h4>
			<p>Use built-in test actions, then follow the <a href="<?php echo epc_int_h($guideUrl); ?>">Integrations guide</a> checklist for each module.</p>
		</div>
	</div>
</div>

<?php epc_cp_page_frame_close(); ?>
