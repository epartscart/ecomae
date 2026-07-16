<?php
/**
 * Premium 3D skin for live custom storefront tenants.
 *
 *   php tests/erp_advanced/run_storefront_premium_3d_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/general_pages/epc_storefront_premium_3d.php';

$pass = 0;
$fail = 0;

function check(string $label, bool $cond): void
{
	global $pass, $fail;
	if ($cond) {
		$pass++;
		echo "  PASS  $label\n";
	} else {
		$fail++;
		echo "  FAIL  $label\n";
	}
}

function section(string $t): void
{
	echo "\n== $t ==\n";
}

section('Eligible packages (other live tenants)');
$eligible = epc_storefront_premium_3d_eligible_packages();
check('electronicae package eligible', in_array('electronics_retail_virgin', $eligible, true));
check('stylenlook package eligible', in_array('fashion_retail_namshi', $eligible, true));
check('jewellery package eligible', in_array('jewellery_retail_kiyasha', $eligible, true));
check('taxofinca package eligible', in_array('consulting_primeinvest', $eligible, true));
check('epartscart package NOT eligible by default', !in_array('automotive_spareparts_pro', $eligible, true));

section('Motif mapping');
check('electronics → electronics motif', epc_storefront_premium_3d_motif_for_package('electronics_retail_virgin') === 'electronics');
check('fashion → fashion motif', epc_storefront_premium_3d_motif_for_package('fashion_retail_namshi') === 'fashion');
check('jewellery → jewellery motif', epc_storefront_premium_3d_motif_for_package('jewellery_retail_kiyasha') === 'jewellery');
check('consulting → professional motif', epc_storefront_premium_3d_motif_for_package('consulting_primeinvest') === 'professional');
check('unknown → default motif', epc_storefront_premium_3d_motif_for_package('nope') === 'default');

section('Icons');
check('electronics icon', epc_storefront_premium_3d_icon_for_motif('electronics') === 'fa-microchip');
check('fashion icon', epc_storefront_premium_3d_icon_for_motif('fashion') === 'fa-shopping-bag');
check('jewellery icon', epc_storefront_premium_3d_icon_for_motif('jewellery') === 'fa-diamond');
check('professional icon', epc_storefront_premium_3d_icon_for_motif('professional') === 'fa-briefcase');

section('Assets on disk');
$css = $root . '/content/general_pages/epc_storefront_premium_3d.css';
$js = $root . '/content/general_pages/epc_storefront_premium_3d.js';
$php = $root . '/content/general_pages/epc_storefront_premium_3d.php';
check('CSS exists', is_file($css));
check('JS exists', is_file($js));
check('PHP helper exists', is_file($php));

$cssBody = (string) file_get_contents($css);
$jsBody = (string) file_get_contents($js);
check('CSS scopes body.epc-sf-premium-3d', strpos($cssBody, 'body.epc-sf-premium-3d') !== false);
check('CSS has hero canvas stage', strpos($cssBody, '.ind3d-stage') !== false && strpos($cssBody, '.ind3d-canvas') !== false);
check('CSS respects reduced motion', strpos($cssBody, 'prefers-reduced-motion') !== false);
check('CSS avoids forced purple shell', stripos($cssBody, 'purple') === false && stripos($cssBody, '#7c3aed') === false);
check('JS boots body.epc-sf-premium-3d', strpos($jsBody, 'body.epc-sf-premium-3d') !== false);
check('JS mounts tenant hero selectors', strpos($jsBody, '.epc-er-hero-banner') !== false && strpos($jsBody, '.epc-frn-hero-banner') !== false);
check('JS has jewellery + consulting heroes', strpos($jsBody, '.epc-jrk-hero-banner') !== false && strpos($jsBody, '.epc-cpi-hero-banner') !== false);
check('JS respects reduced motion', strpos($jsBody, 'prefers-reduced-motion') !== false);
check('JS motif set includes fashion/electronics', strpos($jsBody, 'fashion:') !== false && strpos($jsBody, 'electronics:') !== false);

section('Nero wiring');
$nero = (string) file_get_contents($root . '/templates/nero/desktop.php');
check('nero requires premium 3d helper', strpos($nero, 'epc_storefront_premium_3d.php') !== false);
check('nero emits head html', strpos($nero, 'epc_storefront_premium_3d_head_html') !== false);
check('nero emits footer script', strpos($nero, 'epc_storefront_premium_3d_footer_html') !== false);
check('nero sets body motif attrs', strpos($nero, 'data-epc-3d-motif') !== false);

section('Enable gate (no live DB — package empty ⇒ off)');
check('disabled without active package', epc_storefront_premium_3d_enabled() === false);
check('head html empty when disabled', epc_storefront_premium_3d_head_html() === '');
check('footer html empty when disabled', epc_storefront_premium_3d_footer_html() === '');
$meta = epc_storefront_premium_3d_body_meta();
check('body meta empty when disabled', ($meta['class'] ?? '') === '' && ($meta['motif'] ?? '') === '');

echo "\n";
echo $fail === 0 ? "ALL PASS ($pass)\n" : "FAILED: $fail / " . ($pass + $fail) . "\n";
exit($fail === 0 ? 0 : 1);
