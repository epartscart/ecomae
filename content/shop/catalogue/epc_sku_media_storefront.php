<?php
/**
 * Storefront renderer for SKU photos + multi-type specifications.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_sku_media.php';

if (!function_exists('epc_sku_media_render_storefront')) {
	/**
	 * Render customer-facing photos + specs for a product / brand+article.
	 *
	 * @param array{product_id?:int,brand?:string,article?:string,show_photos?:bool,show_specs?:bool} $opts
	 */
	function epc_sku_media_render_storefront(PDO $db, array $opts = array()): void
	{
		$productId = (int) ($opts['product_id'] ?? 0);
		$brand = (string) ($opts['brand'] ?? '');
		$article = (string) ($opts['article'] ?? '');
		$showPhotos = !array_key_exists('show_photos', $opts) || !empty($opts['show_photos']);
		$showSpecs = !array_key_exists('show_specs', $opts) || !empty($opts['show_specs']);

		$profile = epc_sku_media_resolve_for_product($db, $productId, $brand, $article);
		if (!is_array($profile) || (string) ($profile['status'] ?? '') === 'hidden') {
			return;
		}
		if ((string) ($profile['status'] ?? '') === 'draft') {
			return;
		}
		$payload = epc_sku_media_full_payload($db, (int) $profile['id']);
		if (!is_array($payload)) {
			return;
		}
		$photos = $showPhotos ? ($payload['photos'] ?? array()) : array();
		$groups = $showSpecs ? ($payload['spec_groups'] ?? array()) : array();
		$groups = array_values(array_filter($groups, static function ($g) {
			return !empty($g['rows']);
		}));
		if (!$photos && !$groups) {
			return;
		}

		static $cssDone = false;
		if (!$cssDone) {
			$cssDone = true;
			$css = '/content/shop/catalogue/epc_sku_media.css?v=' . (string) @filemtime(__DIR__ . '/epc_sku_media.css');
			echo '<link rel="stylesheet" href="' . htmlspecialchars($css, ENT_QUOTES, 'UTF-8') . '">';
		}

		$h = static function ($s): string {
			return htmlspecialchars((string) $s, ENT_QUOTES, 'UTF-8');
		};
		$uid = 'epcSkuGal' . (int) $profile['id'];

		echo '<div class="epc-sku-storefront" id="' . $h($uid) . '">';
		if ($photos) {
			$primary = $photos[0];
			foreach ($photos as $ph) {
				if (!empty($ph['is_primary'])) {
					$primary = $ph;
					break;
				}
			}
			echo '<div class="epc-sku-storefront__gallery">';
			echo '<div><div class="epc-sku-storefront__main"><img id="' . $h($uid) . '_main" src="' . $h($primary['url'] ?? '') . '" alt="' . $h($primary['alt'] ?? $profile['title'] ?? '') . '"></div>';
			$cap = (string) ($primary['caption'] ?? '');
			echo '<div class="epc-sku-storefront__caption" id="' . $h($uid) . '_cap">' . $h($cap) . '</div></div>';
			if (count($photos) > 1) {
				echo '<div class="epc-sku-storefront__thumbs" role="list">';
				foreach ($photos as $i => $ph) {
					$active = ($ph['id'] ?? null) === ($primary['id'] ?? null) ? ' is-active' : '';
					echo '<button type="button" class="' . $active . '" data-src="' . $h($ph['url'] ?? '') . '" data-alt="' . $h($ph['alt'] ?? '') . '" data-cap="' . $h($ph['caption'] ?? '') . '" aria-label="Photo ' . ($i + 1) . '">';
					echo '<img src="' . $h($ph['url'] ?? '') . '" alt="">';
					echo '</button>';
				}
				echo '</div>';
			}
			echo '</div>';
			if (count($photos) > 1) {
				echo '<script>(function(){var r=document.getElementById(' . json_encode($uid) . ');if(!r)return;var m=document.getElementById(' . json_encode($uid . '_main') . ');var c=document.getElementById(' . json_encode($uid . '_cap') . ');r.querySelectorAll(".epc-sku-storefront__thumbs button").forEach(function(b){b.addEventListener("click",function(){r.querySelectorAll(".epc-sku-storefront__thumbs button").forEach(function(x){x.classList.remove("is-active")});b.classList.add("is-active");if(m){m.src=b.getAttribute("data-src");m.alt=b.getAttribute("data-alt")||"";}if(c){c.textContent=b.getAttribute("data-cap")||"";}});});})();</script>';
			}
		}

		if ($groups) {
			echo '<div class="epc-sku-storefront__specs">';
			foreach ($groups as $g) {
				echo '<section class="epc-sku-storefront__group">';
				echo '<h3><i class="fa ' . $h($g['icon'] ?? 'fa-list') . '"></i> ' . $h($g['name'] ?? 'Specifications') . '</h3>';
				echo '<table><tbody>';
				foreach ($g['rows'] as $row) {
					$label = (string) ($row['label'] ?? '');
					$display = (string) ($row['display'] ?? $row['value'] ?? '');
					$type = (string) ($row['value_type'] ?? 'text');
					echo '<tr><th scope="row">' . $h($label) . '</th><td>';
					if ($type === 'rich') {
						echo $display; // intentional rich content from CP operators
					} else {
						echo $h($display);
					}
					echo '</td></tr>';
				}
				echo '</tbody></table></section>';
			}
			echo '</div>';
		}
		echo '</div>';
	}
}
