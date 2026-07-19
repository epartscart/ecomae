<?php
/**
 * Full Control Panel brochure — every Client CP / Super CP functionality.
 * Modes: client | super | all
 * Brands: ecomae | epartscart (visual theme only)
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_marketing_brochure.php';

/**
 * @return array<string, array<int, array{name:string,does:string,url:string,scope:string}>>
 */
function epc_cp_brochure_load_inventory(): array
{
	static $inv = null;
	if ($inv === null) {
		$path = __DIR__ . '/epc_cp_brochure_inventory.php';
		$inv = is_file($path) ? require $path : array();
		if (!is_array($inv)) {
			$inv = array();
		}
	}
	return $inv;
}

/**
 * Drop stub duplicate rows (same URL as a clearer name).
 *
 * @param array<int, array{name:string,does:string,url:string,scope:string}> $items
 * @return array<int, array{name:string,does:string,url:string,scope:string}>
 */
function epc_cp_brochure_dedupe_items(array $items): array
{
	$byUrl = array();
	$noUrl = array();
	foreach ($items as $item) {
		$url = trim((string) ($item['url'] ?? ''));
		$name = trim((string) ($item['name'] ?? ''));
		$does = trim((string) ($item['does'] ?? ''));
		$isStub = (strpos($name, 'Epc ') === 0)
			|| ($does === 'Open from left CP menu.');
		if ($url === '') {
			$noUrl[] = $item;
			continue;
		}
		if (!isset($byUrl[$url])) {
			$byUrl[$url] = $item;
			continue;
		}
		$prev = $byUrl[$url];
		$prevStub = (strpos((string) $prev['name'], 'Epc ') === 0)
			|| ((string) $prev['does'] === 'Open from left CP menu.');
		if ($prevStub && !$isStub) {
			$byUrl[$url] = $item;
		}
	}
	$out = array_values($byUrl);
	foreach ($noUrl as $item) {
		$out[] = $item;
	}
	usort($out, static function ($a, $b) {
		return strcasecmp((string) $a['name'], (string) $b['name']);
	});
	return $out;
}

/**
 * @return array<string, array<int, array{name:string,does:string,url:string,scope:string}>>
 */
function epc_cp_brochure_filtered_inventory(string $scope): array
{
	$scope = preg_replace('/[^a-z]/', '', strtolower($scope));
	if (!in_array($scope, array('client', 'super', 'all'), true)) {
		$scope = 'client';
	}
	$raw = epc_cp_brochure_load_inventory();
	$out = array();
	foreach ($raw as $area => $items) {
		if (!is_array($items)) {
			continue;
		}
		$keep = array();
		foreach ($items as $item) {
			if (!is_array($item)) {
				continue;
			}
			$s = (string) ($item['scope'] ?? 'client');
			if ($scope === 'all') {
				$keep[] = $item;
			} elseif ($scope === 'client' && ($s === 'client' || $s === 'both')) {
				$keep[] = $item;
			} elseif ($scope === 'super' && ($s === 'super' || $s === 'both')) {
				$keep[] = $item;
			}
		}
		$keep = epc_cp_brochure_dedupe_items($keep);
		if ($keep) {
			$out[(string) $area] = $keep;
		}
	}
	return $out;
}

function epc_cp_brochure_css(array $p): string
{
	$base = epc_brochure_css($p);
	$extra = <<<'CSS'
.epc-br__toc{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:8px;margin:0 0 36px}
.epc-br__toc a{display:block;padding:10px 12px;background:#fff;border-left:3px solid var(--br-accent);text-decoration:none;color:var(--br-ink);font-size:.9rem;font-weight:600}
.epc-br__toc a span{display:block;font-weight:500;color:var(--br-muted);font-size:.75rem;margin-top:2px}
.epc-br__area{margin:0 0 32px;break-inside:avoid}
.epc-br__area-head{display:flex;justify-content:space-between;align-items:baseline;gap:12px;border-bottom:2px solid var(--br-ink);padding-bottom:8px;margin-bottom:12px}
.epc-br__area-head h2{margin:0;font-family:Syne,sans-serif;font-size:1.35rem}
.epc-br__area-head em{font-style:normal;font-size:.85rem;color:var(--br-muted);font-weight:600}
.epc-br__fn{display:grid;grid-template-columns:minmax(140px,220px) 1fr minmax(90px,120px);gap:8px 14px;padding:10px 0;border-bottom:1px solid rgba(15,23,42,.08);font-size:.9rem;align-items:start}
.epc-br__fn strong{font-family:Syne,sans-serif;font-size:.92rem;letter-spacing:-.01em}
.epc-br__fn p{margin:0;color:var(--br-muted);line-height:1.4}
.epc-br__fn code{font-size:.72rem;color:var(--br-accent);word-break:break-all}
.epc-br__scope{display:inline-block;font-size:.65rem;font-weight:700;letter-spacing:.06em;text-transform:uppercase;padding:2px 6px;border-radius:4px;background:rgba(15,23,42,.06)}
.epc-br__scope--super{background:rgba(2,132,199,.12);color:#075985}
.epc-br__scope--both{background:rgba(220,38,38,.1);color:#991b1b}
.epc-br__filters{display:flex;flex-wrap:wrap;gap:8px;margin:0 0 20px}
.epc-br__filters a{padding:6px 12px;border-radius:999px;border:1px solid rgba(15,23,42,.15);text-decoration:none;color:var(--br-ink);font-size:.85rem;font-weight:600}
.epc-br__filters a.is-on{background:var(--br-accent);color:#fff;border-color:transparent}
.epc-br__legend{font-size:.85rem;color:var(--br-muted);margin:0 0 24px}
@media (max-width:720px){
  .epc-br__fn{grid-template-columns:1fr}
}
@media print{
  .epc-br__filters{display:none!important}
  .epc-br__fn{grid-template-columns:200px 1fr 100px}
  .epc-br__area{break-inside:avoid}
}
CSS;
	return $base . $extra;
}

/**
 * @param array{scope?:string,brand?:string,print?:bool,base_path?:string} $opts
 */
function epc_cp_full_brochure_render_html(array $opts = array()): string
{
	$brand = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($opts['brand'] ?? 'epartscart')));
	if ($brand !== 'ecomae') {
		$brand = 'epartscart';
	}
	$scope = preg_replace('/[^a-z]/', '', strtolower((string) ($opts['scope'] ?? 'client')));
	if (!in_array($scope, array('client', 'super', 'all'), true)) {
		$scope = 'client';
	}
	$p = epc_brochure_profile($brand);
	$inv = epc_cp_brochure_filtered_inventory($scope);
	$total = 0;
	foreach ($inv as $items) {
		$total += count($items);
	}
	$basePath = (string) ($opts['base_path'] ?? '/brochure/cp');
	$q = static function (string $s) use ($basePath): string {
		return epc_brochure_h($basePath . '?scope=' . rawurlencode($s));
	};

	$scopeLabel = array(
		'client' => 'Client CP (tenant)',
		'super' => 'Super CP (platform)',
		'all' => 'All CP functions',
	);

	$toc = '';
	foreach ($inv as $area => $items) {
		$aid = 'area-' . preg_replace('/[^a-z0-9]+/', '-', strtolower($area));
		$toc .= '<a href="#' . epc_brochure_h($aid) . '">' . epc_brochure_h($area)
			. '<span>' . count($items) . ' functions</span></a>';
	}

	$areasHtml = '';
	foreach ($inv as $area => $items) {
		$aid = 'area-' . preg_replace('/[^a-z0-9]+/', '-', strtolower($area));
		$areasHtml .= '<section class="epc-br__area" id="' . epc_brochure_h($aid) . '">';
		$areasHtml .= '<div class="epc-br__area-head"><h2>' . epc_brochure_h($area) . '</h2><em>' . count($items) . ' items</em></div>';
		foreach ($items as $item) {
			$sc = (string) ($item['scope'] ?? 'client');
			$scopeClass = $sc === 'super' ? ' epc-br__scope--super' : ($sc === 'both' ? ' epc-br__scope--both' : '');
			$url = trim((string) ($item['url'] ?? ''));
			$areasHtml .= '<div class="epc-br__fn">';
			$areasHtml .= '<div><strong>' . epc_brochure_h($item['name'] ?? '') . '</strong><br>';
			$areasHtml .= '<span class="epc-br__scope' . $scopeClass . '">' . epc_brochure_h($sc) . '</span></div>';
			$areasHtml .= '<p>' . epc_brochure_h($item['does'] ?? '') . '</p>';
			$areasHtml .= '<div>' . ($url !== '' ? '<code>' . epc_brochure_h($url) . '</code>' : '—') . '</div>';
			$areasHtml .= '</div>';
		}
		$areasHtml .= '</section>';
	}

	$css = epc_cp_brochure_css($p);
	$title = epc_brochure_h($p['name'] . ' — Full Control Panel brochure');
	$desc = epc_brochure_h('Complete map of every Control Panel function (' . $total . ' items) — ' . ($scopeLabel[$scope] ?? $scope) . '.');
	$cover = epc_brochure_h($p['cover']);
	$autoPrint = !empty($opts['print']) ? '<script>window.addEventListener("load",function(){setTimeout(function(){window.print()},400)});</script>' : '';

	$productBrochure = $brand === 'ecomae' ? '/brochure' : '/brochure';

	return '<!DOCTYPE html><html lang="en"><head><meta charset="utf-8">'
		. '<meta name="viewport" content="width=device-width,initial-scale=1">'
		. '<title>' . $title . '</title>'
		. '<meta name="description" content="' . $desc . '">'
		. '<meta name="robots" content="index,follow">'
		. '<style>' . $css . '</style></head><body>'
		. '<div class="epc-br">'
		. '<div class="epc-br__bar"><a class="epc-br__brand" href="' . epc_brochure_h($p['url']) . '">' . epc_brochure_h($p['name']) . ' · CP</a>'
		. '<div class="epc-br__actions">'
		. '<a class="epc-br__btn epc-br__btn--ghost" href="' . epc_brochure_h($productBrochure) . '">Product brochure</a>'
		. '<button type="button" class="epc-br__btn epc-br__btn--ghost" onclick="window.print()">Print / PDF</button>'
		. '<a class="epc-br__btn epc-br__btn--pri" href="' . epc_brochure_h($p['cp_url']) . '">Open Control Panel</a>'
		. '</div></div>'
		. '<header class="epc-br__hero" style="min-height:min(48vh,380px)">'
		. '<img class="epc-br__hero-media" src="' . $cover . '" alt="" width="1600" height="900">'
		. '<div class="epc-br__hero-veil" aria-hidden="true"></div>'
		. '<div class="epc-br__hero-inner">'
		. '<div class="epc-br__eyebrow">Control Panel · full catalogue</div>'
		. '<h1>Every CP function</h1>'
		. '<p>Complete operator map — <strong>' . (int) $total . '</strong> capabilities across '
		. count($inv) . ' areas. Use Print / PDF for sales and training packs. Filter Client CP vs Super CP below.</p>'
		. '</div></header>'
		. '<div class="epc-br__filters">'
		. '<a class="' . ($scope === 'client' ? 'is-on' : '') . '" href="' . $q('client') . '">Client CP</a>'
		. '<a class="' . ($scope === 'super' ? 'is-on' : '') . '" href="' . $q('super') . '">Super CP</a>'
		. '<a class="' . ($scope === 'all' ? 'is-on' : '') . '" href="' . $q('all') . '">All</a>'
		. '</div>'
		. '<p class="epc-br__legend">Showing <strong>' . epc_brochure_h($scopeLabel[$scope]) . '</strong> — '
		. (int) $total . ' functions after de-duplicating menu stubs. Paths are typical <code>/cp/…</code> routes.</p>'
		. '<section class="epc-br__sec"><h2>Jump to area</h2></section>'
		. '<nav class="epc-br__toc" aria-label="CP areas">' . $toc . '</nav>'
		. $areasHtml
		. '<footer class="epc-br__foot">'
		. '<div><strong>' . epc_brochure_h($p['name']) . ' Control Panel</strong><br>'
		. '<span style="color:var(--br-muted)">' . epc_brochure_h($p['legal']) . ' · ' . (int) $total . ' functions listed</span></div>'
		. '<div style="text-align:right">'
		. '<a href="' . epc_brochure_h($productBrochure) . '">Product overview brochure →</a><br>'
		. '<a href="' . epc_brochure_h($p['cp_url']) . '">Open /cp →</a><br>'
		. '<a href="mailto:' . epc_brochure_h($p['contact_email']) . '">' . epc_brochure_h($p['contact_email']) . '</a>'
		. '</div></footer></div>'
		. $autoPrint
		. '</body></html>';
}

function epc_cp_full_brochure_render_and_exit(array $opts = array()): void
{
	if (!headers_sent()) {
		header('Content-Type: text/html; charset=utf-8');
		header('X-Robots-Tag: index, follow');
	}
	echo epc_cp_full_brochure_render_html($opts);
	exit;
}
