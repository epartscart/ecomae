<?php
/**
 * Render the custom-storefront tenant homes (header + home + newsletter + footer)
 * into static HTML snapshots, byte-equivalent to what templates/nero/desktop.php
 * emits for each package on its live host.
 *
 * ASP.NET serves these snapshots on /storefront/app for the matching hosts
 * (electronicae, stylenlook, thejewellerytrend, taxofinca) so the tenant homes
 * are same-to-same with the PHP reference without executing PHP per request.
 *
 * Run from the repo root (CLI):
 *   php scripts/render_php_home_snapshots.php
 * Output: content/general_pages/epc_rendered_homes/<package>.html
 * With a live platform DB the snapshots pick up real site settings; without one
 * the helpers fall back to their built-in defaults (same as PHP does).
 */

error_reporting(E_ERROR | E_PARSE);

$root = dirname(__DIR__);
define('_ASTEXE_', 1);

/*
 * Headers include /modules/shop/catalogue/dp_menu.php (DB-driven mega menu).
 * On the live server config.php + DB exist and the real menu renders.
 * Without config.php (local/CI) use a shadow DOCUMENT_ROOT where dp_menu.php
 * is a no-op stub — the mega panel ships empty (it is display:none by default).
 */
$docRoot = $root;
if (!is_file($root . '/config.php')) {
	$shadow = sys_get_temp_dir() . '/epc-snapshot-root';
	if (!is_dir($shadow)) {
		mkdir($shadow, 0755, true);
	}
	$linkChildren = function (string $srcDir, string $dstDir, array $skip) {
		if (!is_dir($dstDir)) {
			mkdir($dstDir, 0755, true);
		}
		foreach (scandir($srcDir) as $entry) {
			if ($entry === '.' || $entry === '..' || in_array($entry, $skip, true)) {
				continue;
			}
			$link = $dstDir . '/' . $entry;
			if (!file_exists($link) && !is_link($link)) {
				@symlink($srcDir . '/' . $entry, $link);
			}
		}
	};
	$linkChildren($root, $shadow, array('modules'));
	$linkChildren($root . '/modules', $shadow . '/modules', array('shop'));
	$linkChildren($root . '/modules/shop', $shadow . '/modules/shop', array('catalogue'));
	$linkChildren($root . '/modules/shop/catalogue', $shadow . '/modules/shop/catalogue', array('dp_menu.php'));
	file_put_contents(
		$shadow . '/modules/shop/catalogue/dp_menu.php',
		"<?php /* snapshot stub — live render uses the real DB-driven menu */\n"
	);
	$docRoot = $shadow;
}
$_SERVER['DOCUMENT_ROOT'] = $docRoot;

$packages = array(
	'electronics_retail_virgin' => array(
		'host' => 'www.electronicae.com',
		'header' => 'epc_portal_electronics_retail_header.php',
		'home' => 'epc_portal_electronics_retail_home.php',
		'footer' => 'epc_portal_electronics_retail_footer.php',
		'industry' => 'electronics',
		'css' => array(
			'/content/general_pages/epc_electronics_retail.css?v=20260621',
			'/content/general_pages/epc_electronics_retail_virgin_hero.css?v=20260621',
			'/content/general_pages/epc_electronicae_storefront.css?v=20260621',
		),
		'accent' => '#e10a0a',
		'bg' => '#fafafa',
	),
	'fashion_retail_namshi' => array(
		'host' => 'www.stylenlook.com',
		'header' => 'epc_portal_fashion_retail_namshi_header.php',
		'home' => 'epc_portal_fashion_retail_namshi_home.php',
		'footer' => 'epc_portal_fashion_retail_namshi_footer.php',
		'industry' => 'fashion',
		'css' => array(
			'/content/general_pages/epc_fashion_retail_namshi.css?v=20260621',
			'/content/general_pages/epc_fashion_retail_namshi_hero.css?v=20260621',
		),
		'accent' => '#c026d3',
		'bg' => '#fdf4ff',
	),
	'jewellery_retail_kiyasha' => array(
		'host' => 'www.thejewellerytrend.com',
		'header' => 'epc_portal_jewellery_retail_kiyasha_header.php',
		'home' => 'epc_portal_jewellery_retail_kiyasha_home.php',
		'footer' => 'epc_portal_jewellery_retail_kiyasha_footer.php',
		'industry' => 'jewellery',
		'css' => array(
			'/content/general_pages/epc_jewellery_retail_kiyasha.css?v=20260621',
			'/content/general_pages/epc_jewellery_retail_kiyasha_hero.css?v=20260621',
		),
		'accent' => '#b8860b',
		'bg' => '#fffbeb',
	),
	'consulting_primeinvest' => array(
		'host' => 'www.taxofinca.com',
		'header' => 'epc_portal_consulting_primeinvest_header.php',
		'home' => 'epc_portal_consulting_primeinvest_home.php',
		'footer' => 'epc_portal_consulting_primeinvest_footer.php',
		'industry' => 'tax_advisory',
		'css' => array(
			'/content/general_pages/epc_consulting_primeinvest.css?v=20260621',
			'/content/general_pages/epc_consulting_primeinvest_hero.css?v=20260621',
		),
		'accent' => '#0f766e',
		'bg' => '#f0fdfa',
	),
);

$outDir = $root . '/content/general_pages/epc_rendered_homes';
if (!is_dir($outDir)) {
	mkdir($outDir, 0755, true);
}

// Minimal shims for template context normally provided by the front controller.
if (!class_exists('EpcSnapshotObj')) {
	#[\AllowDynamicProperties]
	class EpcSnapshotObj
	{
		public function __get($k)
		{
			return null;
		}

		public function __call($m, $a)
		{
			return null;
		}
	}
}

if (!function_exists('translate_str_by_id')) {
	function translate_str_by_id($id, $lang = null)
	{
		return '';
	}
}

// Guest visitor — same as the public PHP home for a signed-out shopper.
if (!class_exists('DP_User')) {
	class DP_User
	{
		public static function getUserId()
		{
			return 0;
		}

		public static function __callStatic($m, $a)
		{
			return null;
		}
	}
}

$failures = 0;
foreach ($packages as $package => $cfg) {
	$_SERVER['HTTP_HOST'] = $cfg['host'];
	$GLOBALS['multilang_params'] = array('lang_href' => '/en');
	$multilang_params = $GLOBALS['multilang_params'];
	$DP_Content = new EpcSnapshotObj();
	$DP_Content->main_flag = true;
	$DP_Content->id = 0;
	$DP_Content->value = '';
	$GLOBALS['DP_Content'] = $DP_Content;
	$DP_Config = new EpcSnapshotObj();
	$GLOBALS['DP_Config'] = $DP_Config;
	$DP_Template = new EpcSnapshotObj();
	$DP_Template->data_value = new EpcSnapshotObj();
	$GLOBALS['DP_Template'] = $DP_Template;

	require_once $docRoot . '/content/general_pages/epc_portal.php';

	$parts = array();
	// Same CSS links desktop.php enqueues for the package (plus shared animations).
	foreach ($cfg['css'] as $href) {
		$parts[] = '<link rel="stylesheet" href="' . htmlspecialchars($href, ENT_QUOTES, 'UTF-8') . '" />';
	}
	$parts[] = '<link rel="stylesheet" href="/content/general_pages/epc_storefront_animations.css?v=20260621" />';

	$ok = true;
	foreach (array('header', 'home') as $section) {
		$file = $docRoot . '/content/general_pages/' . $cfg[$section];
		try {
			ob_start();
			require $file;
			$parts[] = trim((string) ob_get_clean());
		} catch (\Throwable $e) {
			while (ob_get_level() > 0) {
				ob_end_clean();
			}
			fwrite(STDERR, "FAIL {$package} {$section}: {$e->getMessage()} @ {$e->getFile()}:{$e->getLine()}\n");
			$ok = false;
			break;
		}
	}

	// Newsletter band desktop.php appends between home and footer for custom storefronts.
	if ($ok) {
		try {
			require_once $docRoot . '/content/general_pages/epc_storefront_worldclass.php';
			if (function_exists('epc_storefront_newsletter_section')) {
				$parts[] = trim((string) epc_storefront_newsletter_section($cfg['accent'], $cfg['bg'], $cfg['industry']));
			}
		} catch (\Throwable $e) {
			fwrite(STDERR, "WARN {$package} newsletter: {$e->getMessage()}\n");
		}
	}

	if ($ok) {
		$file = $docRoot . '/content/general_pages/' . $cfg['footer'];
		try {
			ob_start();
			require $file;
			$parts[] = trim((string) ob_get_clean());
		} catch (\Throwable $e) {
			while (ob_get_level() > 0) {
				ob_end_clean();
			}
			fwrite(STDERR, "FAIL {$package} footer: {$e->getMessage()} @ {$e->getFile()}:{$e->getLine()}\n");
			$ok = false;
		}
	}

	if (!$ok) {
		$failures++;
		continue;
	}

	$html = "<!-- rendered from PHP package {$package} for {$cfg['host']} — regenerate with scripts/render_php_home_snapshots.php -->\n"
		. implode("\n", array_filter($parts)) . "\n";
	$out = $outDir . '/' . $package . '.html';
	file_put_contents($out, $html);
	printf("OK  %-28s %s bytes → %s\n", $package, number_format(strlen($html)), $out);
}

exit($failures > 0 ? 1 : 0);
