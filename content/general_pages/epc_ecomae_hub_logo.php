<?php
/**
 * Reusable ECOM AE animated hub logo (CSS orbit, no heavy JS).
 * Compact variants for CP / ERP login and shell branding.
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return array<int, array{icon:string,title:string,sub:string,data:string,featured?:bool}>
 */
function epc_ecomae_hub_logo_nodes()
{
	return array(
		array('icon' => 'fa-shopping-cart', 'title' => 'Commerce', 'sub' => 'Orders', 'data' => 'Orders'),
		array('icon' => 'fa-cubes', 'title' => 'Inventory', 'sub' => 'Stock', 'data' => 'Stock'),
		array('icon' => 'fa-users', 'title' => 'CRM', 'sub' => 'Clients', 'data' => 'CRM'),
		array('icon' => 'fa-chart-line', 'title' => 'Dashboard', 'sub' => 'KPIs', 'data' => 'Analytics', 'featured' => true),
		array('icon' => 'fa-file-text-o', 'title' => 'Finance', 'sub' => 'GL & VAT', 'data' => 'GL'),
		array('icon' => 'fa-id-badge', 'title' => 'HR', 'sub' => 'Payroll', 'data' => 'HR'),
		array('icon' => 'fa-truck', 'title' => 'Logistics', 'sub' => 'Delivery', 'data' => 'Ship'),
		array('icon' => 'fa-cloud', 'title' => 'Tenants', 'sub' => 'Super CP', 'data' => 'Sync'),
	);
}

function epc_ecomae_hub_logo_image_url()
{
	if (function_exists('epc_ecomae_platform_logo_url')) {
		return epc_ecomae_platform_logo_url();
	}
	return '/content/general_pages/epc_ecomae_logo_svg.php';
}

function epc_ecomae_hub_logo_css_href()
{
	return '/content/general_pages/epc_ecomae_hub_logo_css.php';
}

function epc_ecomae_hub_logo_css_version()
{
	return '20260530cp';
}

function epc_cp_login_hero_css_href()
{
	return '/content/general_pages/epc_cp_login_hero_css.php';
}

function epc_cp_login_hero_css_version()
{
	return '20260621bosMatrix';
}

function epc_cp_login_css_version()
{
	return '20260621bosMatrix';
}

function epc_cp_login_enqueue()
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	$href = '/content/general_pages/epc_cp_login_css.php';
	$ver = htmlspecialchars(epc_cp_login_css_version(), ENT_QUOTES, 'UTF-8');
	echo '<link rel="stylesheet" href="' . htmlspecialchars($href, ENT_QUOTES, 'UTF-8') . '?v=' . $ver . '" />' . "\n";
}

function epc_cp_login_hero_enqueue()
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	$href = epc_cp_login_hero_css_href();
	$ver = htmlspecialchars(epc_cp_login_hero_css_version(), ENT_QUOTES, 'UTF-8');
	echo '<link rel="stylesheet" href="' . htmlspecialchars($href, ENT_QUOTES, 'UTF-8') . '?v=' . $ver . '" />' . "\n";
}

/**
 * Static ECOM AE mark for narrow CP/ERP shells (no orbit nodes).
 *
 * @param string $variant login|compact|header|micro
 * @param array{show_tagline?:bool,show_title?:bool,aria_label?:string,class?:string} $opts
 */
function epc_ecomae_static_logo($variant = 'compact', array $opts = array())
{
	$variant = in_array($variant, array('login', 'compact', 'header', 'micro'), true) ? $variant : 'compact';
	$showTagline = !array_key_exists('show_tagline', $opts) || !empty($opts['show_tagline']);
	$showTitle = !array_key_exists('show_title', $opts) || !empty($opts['show_title']);
	$aria = isset($opts['aria_label']) ? (string) $opts['aria_label'] : 'ECOM AE unified ERP and commerce cloud';
	$extraClass = isset($opts['class']) ? trim((string) $opts['class']) : '';
	$logo = epc_ecomae_hub_logo_image_url();
	$sizes = array(
		'micro' => 28,
		'header' => 36,
		'compact' => 44,
		'login' => 96,
	);
	$px = $sizes[$variant];

	ob_start();
	?>
<span class="ech-static ech-static--<?php echo htmlspecialchars($variant, ENT_QUOTES, 'UTF-8'); ?><?php echo $extraClass !== '' ? ' ' . htmlspecialchars($extraClass, ENT_QUOTES, 'UTF-8') : ''; ?>" role="img" aria-label="<?php echo htmlspecialchars($aria, ENT_QUOTES, 'UTF-8'); ?>">
	<img class="ech-static__logo" src="<?php echo htmlspecialchars($logo, ENT_QUOTES, 'UTF-8'); ?>" alt="" width="<?php echo (int) $px; ?>" height="<?php echo (int) $px; ?>" loading="lazy" />
	<?php if ($showTitle && $variant !== 'micro') { ?>
	<span class="ech-static__title">ECOM <span class="ech-static__ae">AE</span></span>
	<?php } ?>
	<?php if ($showTagline && $variant === 'login') { ?>
	<span class="ech-static__tagline">Unified ERP &amp; Commerce Cloud</span>
	<?php } elseif ($showTagline && $variant === 'compact') { ?>
	<span class="ech-static__tagline ech-static__tagline--short">ERP &amp; Commerce Cloud</span>
	<?php } ?>
</span>
	<?php
	return ob_get_clean();
}

function epc_ecomae_hub_logo_enqueue()
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	$href = htmlspecialchars(epc_ecomae_hub_logo_css_href(), ENT_QUOTES, 'UTF-8');
	$ver = htmlspecialchars(epc_ecomae_hub_logo_css_version(), ENT_QUOTES, 'UTF-8');
	echo '<link rel="stylesheet" href="' . $href . '?v=' . $ver . '" />' . "\n";
}

/**
 * @param string $variant login|login-panel|compact|header|micro
 * @param array{show_tagline?:bool,show_title?:bool,aria_label?:string,class?:string} $opts
 */
function epc_ecomae_hub_logo($variant = 'login', array $opts = array())
{
	$variant = in_array($variant, array('login', 'login-panel', 'compact', 'header', 'micro'), true) ? $variant : 'login';
	$showTagline = !array_key_exists('show_tagline', $opts) || !empty($opts['show_tagline']);
	$showTitle = !array_key_exists('show_title', $opts) || !empty($opts['show_title']);
	$aria = isset($opts['aria_label']) ? (string) $opts['aria_label'] : 'ECOM AE unified ERP and commerce cloud';
	$extraClass = isset($opts['class']) ? trim((string) $opts['class']) : '';
	$logo = epc_ecomae_hub_logo_image_url();
	$nodes = epc_ecomae_hub_logo_nodes();
	if ($variant === 'micro') {
		$orbitDur = '32s';
	} elseif ($variant === 'header') {
		$orbitDur = '34s';
	} elseif ($variant === 'compact') {
		$orbitDur = '36s';
	} elseif ($variant === 'login-panel') {
		$orbitDur = '38s';
	} else {
		$orbitDur = '40s';
	}

	ob_start();
	?>
<div class="ech-hub ech-hub--<?php echo htmlspecialchars($variant, ENT_QUOTES, 'UTF-8'); ?><?php echo $extraClass !== '' ? ' ' . htmlspecialchars($extraClass, ENT_QUOTES, 'UTF-8') : ''; ?>" role="img" aria-label="<?php echo htmlspecialchars($aria, ENT_QUOTES, 'UTF-8'); ?>" style="--ech-orbit-duration:<?php echo htmlspecialchars($orbitDur, ENT_QUOTES, 'UTF-8'); ?>">
	<div class="ech-hub__glow" aria-hidden="true"></div>
	<div class="ech-hub__orbit-spin" aria-hidden="true">
		<?php
		$i = 0;
		foreach ($nodes as $n) {
			$deg = 270 + ($i * 45);
			$featured = !empty($n['featured']);
			$cls = 'ech-hub__node' . ($featured ? ' ech-hub__node--featured' : '');
			$delay = number_format($i * 0.09, 2, '.', '');
			$i++;
			?>
		<div class="<?php echo $cls; ?>" style="--ech-i: <?php echo (int) $deg; ?>deg; --ech-delay: <?php echo htmlspecialchars($delay, ENT_QUOTES, 'UTF-8'); ?>s">
			<div class="ech-hub__node-inner">
				<span class="ech-hub__node-tile" title="<?php echo htmlspecialchars($n['title'], ENT_QUOTES, 'UTF-8'); ?>"><i class="fa <?php echo htmlspecialchars($n['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i></span>
				<?php if ($variant === 'login' || $variant === 'login-panel') { ?>
				<strong><?php echo htmlspecialchars($n['title'], ENT_QUOTES, 'UTF-8'); ?></strong>
				<?php } ?>
			</div>
		</div>
			<?php
		}
		?>
	</div>
	<div class="ech-hub__core">
		<div class="ech-hub__core-pulse" aria-hidden="true"></div>
		<?php if ($variant === 'micro') { ?>
		<span class="ech-hub__glyph" aria-hidden="true">e</span>
		<?php } else { ?>
		<img class="ech-hub__logo" src="<?php echo htmlspecialchars($logo, ENT_QUOTES, 'UTF-8'); ?>" alt="" width="64" height="64" loading="lazy" />
		<?php } ?>
		<?php if ($showTitle && $variant !== 'micro') { ?>
		<p class="ech-hub__title">ECOM <span class="ech-hub__ae">AE</span></p>
		<?php } ?>
		<?php if ($showTagline && ($variant === 'login' || $variant === 'login-panel')) { ?>
		<p class="ech-hub__tagline">Unified ERP &amp; Commerce Cloud</p>
		<?php } elseif ($showTagline && $variant === 'compact') { ?>
		<p class="ech-hub__tagline ech-hub__tagline--short">ERP &amp; Commerce Cloud</p>
		<?php } ?>
	</div>
</div>
	<?php
	return ob_get_clean();
}
