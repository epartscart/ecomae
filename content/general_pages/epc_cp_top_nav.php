<?php
/**
 * CP top process mega-menu (red + black) — mirrors ERP top-nav pattern.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_nav_tree.php';

function epc_cp_topnav_h($value): string
{
	return htmlspecialchars((string) $value, ENT_QUOTES, 'UTF-8');
}

/**
 * Render sticky CP top mega-menu from the same groups as the left rail.
 */
function epc_cp_render_top_nav(): void
{
	global $DP_Config;

	$groups = epc_cp_build_nav_tabs();
	if (count($groups) === 0) {
		return;
	}

	$homeUrl = function_exists('epc_cp_control_url')
		? epc_cp_control_url()
		: ('/' . trim((string) ($DP_Config->backend_dir ?? 'cp'), '/') . '/control');

	echo '<nav class="epc-cp-topnav" id="epc_cp_topnav" aria-label="Control panel modules">';
	echo '<div class="epc-cp-topnav-inner">';
	echo '<a class="epc-cp-topnav-brand" href="' . epc_cp_topnav_h($homeUrl) . '">';
	echo '<i class="fa fa-th-large" aria-hidden="true"></i><span>Control</span></a>';
	echo '<ul class="epc-cp-topnav-list" role="menubar">';

	foreach ($groups as $group) {
		$gKey = (string) $group['key'];
		$items = (array) ($group['items'] ?? array());
		if (count($items) === 0) {
			continue;
		}

		$isActive = false;
		foreach ($items as $item) {
			if (epc_cp_nav_url_is_active((string) ($item['url'] ?? ''))) {
				$isActive = true;
				break;
			}
		}

		$firstHref = (string) ($items[0]['url'] ?? $homeUrl);
		$tierClass = ((string) ($group['tier'] ?? '')) === 'advanced' ? ' epc-cp-topnav-item--advanced' : '';

		echo '<li class="epc-cp-topnav-item' . ($isActive ? ' is-active' : '') . $tierClass . '" data-group="' . epc_cp_topnav_h($gKey) . '" role="none">';
		echo '<button type="button" class="epc-cp-topnav-btn" role="menuitem" aria-haspopup="true" aria-expanded="false" data-topnav-toggle="' . epc_cp_topnav_h($gKey) . '">';
		echo '<i class="fa ' . epc_cp_topnav_h($group['icon']) . '" aria-hidden="true"></i>';
		echo '<span class="epc-cp-topnav-label" data-full="' . epc_cp_topnav_h($group['caption']) . '" data-short="' . epc_cp_topnav_h($group['short']) . '">' . epc_cp_topnav_h($group['short']) . '</span>';
		echo '<i class="fa fa-angle-down epc-cp-topnav-caret" aria-hidden="true"></i>';
		echo '</button>';

		echo '<div class="epc-cp-topnav-panel" role="menu" hidden data-topnav-panel="' . epc_cp_topnav_h($gKey) . '">';
		echo '<div class="epc-cp-topnav-panel-hd">';
		echo '<div class="epc-cp-topnav-panel-title"><i class="fa ' . epc_cp_topnav_h($group['icon']) . '"></i> ' . epc_cp_topnav_h($group['caption']);
		if (!empty($group['subtitle'])) {
			echo '<span class="epc-cp-topnav-panel-sub">' . epc_cp_topnav_h($group['subtitle']) . '</span>';
		}
		echo '</div>';
		echo '<a class="epc-cp-topnav-panel-hub" href="' . epc_cp_topnav_h($firstHref) . '">Open first <i class="fa fa-arrow-right"></i></a>';
		echo '</div>';

		// Split into columns of ~8 links for scannability.
		$colSize = 8;
		$chunks = array_chunk($items, $colSize);
		echo '<div class="epc-cp-topnav-cols">';
		foreach ($chunks as $chunk) {
			echo '<div class="epc-cp-topnav-col">';
			echo '<ul class="epc-cp-topnav-links">';
			foreach ($chunk as $item) {
				$url = (string) ($item['url'] ?? '#');
				$label = translate_str_by_id($item['caption'] ?? '');
				$icon = trim((string) ($item['fontawesome_class'] ?? ''));
				if ($icon === '') {
					$icon = 'fa fa-circle-o';
				} elseif (strpos($icon, 'fa ') !== 0 && strpos($icon, 'fas ') !== 0 && strpos($icon, 'far ') !== 0) {
					$icon = 'fa ' . ltrim($icon, '.');
				}
				$itemActive = epc_cp_nav_url_is_active($url);
				echo '<li' . ($itemActive ? ' class="is-active"' : '') . '>';
				echo '<a href="' . epc_cp_topnav_h($url) . '">';
				echo '<i class="' . epc_cp_topnav_h($icon) . '"></i> ' . epc_cp_topnav_h($label);
				echo '</a></li>';
			}
			echo '</ul></div>';
		}
		echo '</div></div>'; // cols + panel
		echo '</li>';
	}

	echo '</ul></div></nav>';
}
