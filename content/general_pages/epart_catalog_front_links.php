<?php
/*
 * Front page display data for Epart Catalog and Available Brands.
 * Embeds the live catalog search UI and brands grid inline on the homepage.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
if (!function_exists('epc_portal_is_auto_parts_site') || !epc_portal_is_auto_parts_site()) {
	return;
}
if (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()) {
	return;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_perf_cache.php';

function epart_front_render_widget(string $relPath, string $cacheKey, int $ttl = 900): string
{
	$cached = epc_perf_cache_get($cacheKey);
	if (is_string($cached) && $cached !== '') {
		return $cached;
	}

	$file = $_SERVER['DOCUMENT_ROOT'] . '/' . ltrim($relPath, '/');
	if (!is_file($file)) {
		return '';
	}

	ob_start();
	include $file;
	$html = (string) ob_get_clean();
	if ($html !== '') {
		epc_perf_cache_set($cacheKey, $html, $ttl);
	}
	return $html;
}

$host = !empty($_SERVER['HTTP_HOST']) ? strtolower((string) $_SERVER['HTTP_HOST']) : 'www.epartscart.com';
if (strpos($host, 'epartscart.com') === false) {
	return;
}
$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] != '') ? rtrim($multilang_params['lang_href'], '/') : '/en';
$cachePrefix = 'epart_front_widget:v3:' . preg_replace('/[^a-z0-9.\-]/', '', $host) . ':' . md5($lang_href) . ':';

$catalog_html = epart_front_render_widget('content/umapi_catalog.php', $cachePrefix . 'catalog', 900);
$brands_html = epart_front_render_widget('content/available_brands.php', $cachePrefix . 'brands', 900);
?>

<style>
	.epart-front-original-data {
		background: transparent;
		border: 0;
		border-radius: 0;
		box-shadow: none;
		margin-bottom: 32px;
		margin-left: auto;
		margin-right: auto;
		margin-top: 0;
		overflow: visible;
		padding: 0;
		position: relative;
	}
	.epart-front-original-data:before {
		content: none;
		display: none;
	}
	.epart-front-section {
		background: #fff;
		border: 1px solid #e2e8f0;
		border-radius: 14px;
		box-shadow: 0 4px 18px rgba(15, 23, 42, .06);
		margin-bottom: 28px;
		padding: 24px 24px 20px;
	}
	.epart-front-section:last-child {
		margin-bottom: 0;
	}
	.epart-front-section-head {
		align-items: center;
		display: flex;
		flex-wrap: wrap;
		gap: 12px 16px;
		justify-content: space-between;
		margin-bottom: 18px;
	}
	.epart-front-section-title {
		color: #0f172a;
		font-size: 28px;
		font-weight: 800;
		letter-spacing: -.02em;
		line-height: 1.15;
		margin: 0;
	}
	.epart-front-section-link {
		color: #2563eb;
		font-weight: 700;
		text-decoration: none;
		white-space: nowrap;
	}
	.epart-front-section-link:hover {
		color: #1d4ed8;
		text-decoration: underline;
	}
	.epart-front-original-data .epc-umapi,
	.epart-front-original-data .epc-brands {
		background: transparent;
		border: 0;
		border-radius: 0;
		box-shadow: none;
		margin: 0;
		padding: 0;
	}
	.epart-front-original-data .epc-umapi-title,
	.epart-front-original-data .epc-brands-title {
		display: none;
	}
	.epart-front-original-data .epc-brands-controls .btn-default[href] {
		display: none;
	}
	@media (max-width: 767px) {
		.epart-front-section {
			border-radius: 12px;
			padding: 16px 14px 14px;
		}
		.epart-front-section-title {
			font-size: 22px;
		}
	}
</style>

<div class="container epart-front-original-data">
	<section class="epart-front-section epart-front-section-catalog" aria-label="Epart Catalog">
		<div class="epart-front-section-head">
			<h2 class="epart-front-section-title">Epart Catalog</h2>
			<a class="epart-front-section-link" href="<?php echo htmlspecialchars($lang_href . '/umapi_catalog', ENT_QUOTES, 'UTF-8'); ?>">Open full catalog &rarr;</a>
		</div>
		<?php
		if ($catalog_html !== '') {
			echo $catalog_html;
		} else {
			?>
			<div class="alert alert-warning">Epart Catalog is temporarily unavailable. <a href="<?php echo htmlspecialchars($lang_href . '/umapi_catalog', ENT_QUOTES, 'UTF-8'); ?>">Open catalog page</a></div>
			<?php
		}
		?>
	</section>

	<section class="epart-front-section epart-front-section-brands" aria-label="Available brands">
		<div class="epart-front-section-head">
			<h2 class="epart-front-section-title">Available brands</h2>
			<a class="epart-front-section-link" href="<?php echo htmlspecialchars($lang_href . '/available-brands', ENT_QUOTES, 'UTF-8'); ?>">View all brands &rarr;</a>
		</div>
		<?php
		if ($brands_html !== '') {
			echo $brands_html;
		} else {
			?>
			<div class="alert alert-warning">Available brands are temporarily unavailable. <a href="<?php echo htmlspecialchars($lang_href . '/available-brands', ENT_QUOTES, 'UTF-8'); ?>">Open brands page</a></div>
			<?php
		}
		?>
	</section>

	<style>
		.epart-front-original-data .epc-umapi-panel {
			background: #f8fafc;
			border: 1px solid #e2e8f0;
			border-radius: 14px;
			box-shadow: inset 0 1px 0 rgba(255, 255, 255, .85);
		}
		.epart-front-original-data .epc-umapi-tab,
		.epart-front-original-data .epc-brands-tabs button,
		.epart-front-original-data .epc-brands-letters button,
		.epart-front-original-data .epc-umapi-alpha a,
		.epart-front-original-data .epc-umapi-manufacturer-tabs button {
			background: #fff;
			border: 1px solid #d7dee9;
			border-radius: 10px;
			color: #1f2937;
			font-weight: 800;
			box-shadow: 0 8px 18px rgba(15, 23, 42, .06);
		}
		.epart-front-original-data .epc-umapi-tab.active,
		.epart-front-original-data .epc-brands-tabs button.active,
		.epart-front-original-data .epc-brands-letters button.active,
		.epart-front-original-data .epc-umapi-manufacturer-tabs button.active {
			background: linear-gradient(135deg, #2563eb, #1d4ed8);
			border-color: #2563eb;
			color: #fff;
			box-shadow: 0 12px 24px rgba(37, 99, 235, .26);
		}
		.epart-front-original-data .epc-umapi-tab:hover,
		.epart-front-original-data .epc-brands-tabs button:hover,
		.epart-front-original-data .epc-brands-letters button:hover,
		.epart-front-original-data .epc-umapi-alpha a:hover,
		.epart-front-original-data .epc-umapi-manufacturer-tabs button:hover {
			border-color: #ef4444;
			color: #ef4444;
			text-decoration: none;
		}
		.epart-front-original-data .form-control,
		.epart-front-original-data input[type="search"] {
			background: #fff;
			border: 1px solid #cfd8e6;
			border-radius: 0;
			box-shadow: none;
			color: #111827;
			height: 42px;
		}
		.epart-front-original-data .btn-primary,
		.epart-front-original-data #epc-umapi-article-btn {
			background: linear-gradient(135deg, #ef4444, #dc2626);
			border-color: #ef4444;
			color: #fff;
			font-weight: 900;
			height: 42px;
			padding-left: 18px;
			padding-right: 18px;
		}
		.epart-front-original-data .btn-default {
			background: #fff;
			border-color: #cfd8e6;
			color: #1f2937;
			font-weight: 800;
			height: 42px;
			line-height: 28px;
		}
		.epart-front-original-data .btn-default:hover {
			border-color: #ef4444;
			color: #ef4444;
		}
		.epart-front-original-data .epc-umapi-step {
			background: #eef2ff;
			color: #475569;
			font-weight: 800;
		}
		.epart-front-original-data .epc-umapi-step.active,
		.epart-front-original-data .epc-umapi-step.clickable {
			background: #dbeafe;
			color: #1d4ed8;
		}
		.epart-front-original-data .epc-umapi-message,
		.epart-front-original-data .epc-brands-message {
			background: #fff7ed;
			border: 1px solid #fed7aa;
			border-left: 4px solid #ef4444;
			border-radius: 12px;
			color: #7c2d12;
			font-weight: 700;
			padding: 14px 16px;
		}
		.epart-front-original-data .epc-umapi-card,
		.epart-front-original-data .epc-umapi-row,
		.epart-front-original-data .epc-brand-item {
			background: #fff;
			border-color: #e2e8f0;
			color: #172536;
		}
		.epart-front-original-data .epc-umapi-card:hover,
		.epart-front-original-data .epc-umapi-row:hover,
		.epart-front-original-data .epc-brand-item:hover {
			border-color: #2563eb;
			box-shadow: 0 12px 24px rgba(37, 99, 235, .12);
			color: #1d4ed8;
		}
		.epart-front-original-data .epc-umapi-popular-section {
			display: block;
		}
		.epart-front-original-data .epc-brands-grid {
			max-height: 520px;
			overflow-y: auto;
			padding-right: 4px;
		}
	</style>
</div>
