<?php
/**
 * Full Control Panel brochure — graphical presentation (default) + catalogue view.
 * Auto-updates from live ERP nav + capabilities catalog on every render.
 *
 * Modes: client | super | all
 * Views: deck (graphical) | catalog (printable rows)
 * Brands: ecomae | epartscart
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_marketing_brochure.php';
require_once __DIR__ . '/epc_cp_brochure_live.php';

/**
 * @return array<string, array<int, array{name:string,does:string,url:string,scope:string,icon?:string,id?:string}>>
 */
function epc_cp_brochure_load_inventory(): array
{
	$live = epc_cp_brochure_build_live_inventory();
	return $live['areas'];
}

/**
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
 * @return array{areas: array<string, array<int, array<string,mixed>>>, meta: array<string,mixed>}
 */
function epc_cp_brochure_filtered_bundle(string $scope): array
{
	$scope = preg_replace('/[^a-z]/', '', strtolower($scope));
	if (!in_array($scope, array('client', 'super', 'all'), true)) {
		$scope = 'client';
	}
	$live = epc_cp_brochure_build_live_inventory();
	$out = array();
	foreach ($live['areas'] as $area => $items) {
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
	$total = 0;
	foreach ($out as $items) {
		$total += count($items);
	}
	$meta = $live['meta'];
	$meta['total'] = $total;
	$meta['area_count'] = count($out);
	$meta['scope'] = $scope;
	return array('areas' => $out, 'meta' => $meta);
}

/** @deprecated use epc_cp_brochure_filtered_bundle */
function epc_cp_brochure_filtered_inventory(string $scope): array
{
	return epc_cp_brochure_filtered_bundle($scope)['areas'];
}

function epc_cp_brochure_css(array $p): string
{
	$base = epc_brochure_css($p);
	$extra = <<<'CSS'
@import url('https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css');
.epc-br--deck{max-width:1200px}
.epc-br__live{display:inline-flex;align-items:center;gap:8px;padding:6px 12px;border-radius:999px;background:rgba(255,255,255,.12);border:1px solid rgba(255,255,255,.22);font-size:.78rem;font-weight:700;letter-spacing:.04em;text-transform:uppercase;color:#fff;margin:0 0 14px}
.epc-br__live i{color:var(--br-accent);animation:epc-br-pulse 1.8s ease-in-out infinite}
@keyframes epc-br-pulse{0%,100%{opacity:1}50%{opacity:.35}}
.epc-br__hero--deck{min-height:min(78vh,620px);margin:0 0 0;width:100vw;margin-left:calc(50% - 50vw);margin-right:calc(50% - 50vw)}
.epc-br__hero--deck .epc-br__hero-inner{max-width:720px;padding:clamp(48px,8vw,96px) clamp(20px,6vw,72px)}
.epc-br__hero-media{animation:epc-br-ken 22s ease-in-out infinite alternate}
@keyframes epc-br-ken{from{transform:scale(1)}to{transform:scale(1.06)}}
.epc-br__stats{display:grid;grid-template-columns:repeat(4,1fr);gap:1px;background:rgba(15,23,42,.1);margin:0 0 36px;border-radius:0;overflow:hidden}
.epc-br__stats div{background:var(--br-white);padding:22px 18px;position:relative;overflow:hidden}
.epc-br__stats div::after{content:'';position:absolute;left:0;bottom:0;height:3px;width:100%;background:linear-gradient(90deg,var(--br-accent),var(--br-accent2));transform:scaleX(0);transform-origin:left;animation:epc-br-bar .8s .2s ease forwards}
@keyframes epc-br-bar{to{transform:scaleX(1)}}
.epc-br__stats strong{display:block;font-family:Syne,sans-serif;font-size:1.65rem;letter-spacing:-.03em;line-height:1}
.epc-br__stats span{display:block;margin-top:6px;font-size:.82rem;color:var(--br-muted);font-weight:600}
.epc-br__mosaic{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:14px;margin:0 0 48px}
.epc-br__tile{position:relative;min-height:160px;overflow:hidden;color:#fff;text-decoration:none;display:flex;flex-direction:column;justify-content:flex-end;padding:16px;isolation:isolate;transition:transform .25s ease}
.epc-br__tile:hover{transform:translateY(-3px)}
.epc-br__tile-bg{position:absolute;inset:0;background-size:cover;background-position:center;opacity:.45;z-index:0;transform:scale(1.02);transition:transform .5s ease}
.epc-br__tile:hover .epc-br__tile-bg{transform:scale(1.08)}
.epc-br__tile-veil{position:absolute;inset:0;background:linear-gradient(160deg,rgba(0,0,0,.15),rgba(0,0,0,.88));z-index:1}
.epc-br__tile > *{position:relative;z-index:2}
.epc-br__tile i{font-size:1.35rem;margin-bottom:10px;color:var(--br-accent)}
.epc-br__tile strong{font-family:Syne,sans-serif;font-size:1.05rem;letter-spacing:-.02em;display:block}
.epc-br__tile span{font-size:.78rem;opacity:.85;margin-top:4px}
.epc-br__tile em{font-style:normal;font-size:.72rem;font-weight:700;letter-spacing:.08em;text-transform:uppercase;opacity:.7;margin-bottom:6px}
.epc-br__toolbar{display:flex;flex-wrap:wrap;gap:10px;align-items:center;justify-content:space-between;margin:0 0 18px}
.epc-br__search{flex:1;min-width:200px;max-width:360px;padding:10px 14px;border:1px solid rgba(15,23,42,.14);background:#fff;font:inherit;border-radius:8px}
.epc-br__filters{display:flex;flex-wrap:wrap;gap:8px;margin:0}
.epc-br__filters a{padding:7px 13px;border-radius:8px;border:1px solid rgba(15,23,42,.15);text-decoration:none;color:var(--br-ink);font-size:.85rem;font-weight:600}
.epc-br__filters a.is-on{background:var(--br-accent);color:#fff;border-color:transparent}
.epc-br__viewtoggle a{font-size:.85rem}
.epc-br__area{margin:0 0 48px;scroll-margin-top:72px}
.epc-br__area-banner{position:relative;min-height:180px;margin:0 0 18px;overflow:hidden;color:#fff;display:grid;grid-template-columns:1.2fr .8fr;gap:0}
.epc-br__area-banner-copy{position:relative;z-index:2;padding:28px 24px;background:linear-gradient(110deg,var(--br-ink) 0%,var(--br-ink2) 70%,transparent 100%)}
.epc-br__area-banner-copy h2{margin:0 0 8px;font-family:Syne,sans-serif;font-size:1.7rem;letter-spacing:-.02em}
.epc-br__area-banner-copy p{margin:0;color:rgba(255,255,255,.82);max-width:36em;font-size:.95rem}
.epc-br__area-banner-copy .epc-br__count{display:inline-block;margin-top:12px;padding:4px 10px;background:var(--br-accent);font-size:.75rem;font-weight:700;letter-spacing:.06em;text-transform:uppercase}
.epc-br__area-banner-media{position:absolute;inset:0;background-size:cover;background-position:center;opacity:.55}
.epc-br__cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(250px,1fr));gap:14px}
.epc-br__card{background:#fff;border:1px solid rgba(15,23,42,.08);padding:18px 16px 16px;cursor:pointer;transition:border-color .2s,transform .2s;min-height:168px;display:flex;flex-direction:column}
.epc-br__card:hover,.epc-br__card:focus{border-color:var(--br-accent);transform:translateY(-2px);outline:none}
.epc-br__card-ico{width:42px;height:42px;display:inline-flex;align-items:center;justify-content:center;background:linear-gradient(135deg,rgba(0,0,0,.04),rgba(0,0,0,.02));border:1px solid rgba(15,23,42,.08);color:var(--br-accent);font-size:1.1rem;margin-bottom:12px}
.epc-br__card h3{margin:0 0 8px;font-family:Syne,sans-serif;font-size:1rem;letter-spacing:-.01em;line-height:1.25}
.epc-br__card p{margin:0;flex:1;font-size:.86rem;color:var(--br-muted);line-height:1.45;display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;overflow:hidden}
.epc-br__card-foot{display:flex;justify-content:space-between;align-items:center;gap:8px;margin-top:12px;font-size:.72rem}
.epc-br__scope{display:inline-block;font-size:.65rem;font-weight:700;letter-spacing:.06em;text-transform:uppercase;padding:2px 6px;border-radius:4px;background:rgba(15,23,42,.06)}
.epc-br__scope--super{background:rgba(2,132,199,.12);color:#075985}
.epc-br__scope--both{background:rgba(220,38,38,.1);color:#991b1b}
.epc-br__card code{font-size:.68rem;color:var(--br-accent);max-width:55%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.epc-br__modal{position:fixed;inset:0;z-index:40;display:none;align-items:center;justify-content:center;padding:20px;background:rgba(10,10,10,.55);backdrop-filter:blur(4px)}
.epc-br__modal.is-open{display:flex;animation:epc-br-fade .2s ease}
@keyframes epc-br-fade{from{opacity:0}to{opacity:1}}
.epc-br__modal-panel{background:#fff;max-width:520px;width:100%;padding:28px 24px;border-left:4px solid var(--br-accent);max-height:85vh;overflow:auto}
.epc-br__modal-panel h3{font-family:Syne,sans-serif;margin:0 0 10px;font-size:1.35rem}
.epc-br__modal-panel p{margin:0 0 14px;color:var(--br-muted);line-height:1.55}
.epc-br__modal-panel code{display:block;font-size:.8rem;color:var(--br-accent);word-break:break-all;margin-bottom:16px}
.epc-br__sources{font-size:.78rem;color:var(--br-muted);margin:8px 0 0}
/* catalogue (text) view */
.epc-br__toc{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:8px;margin:0 0 36px}
.epc-br__toc a{display:block;padding:10px 12px;background:#fff;border-left:3px solid var(--br-accent);text-decoration:none;color:var(--br-ink);font-size:.9rem;font-weight:600}
.epc-br__toc a span{display:block;font-weight:500;color:var(--br-muted);font-size:.75rem;margin-top:2px}
.epc-br__area-head{display:flex;justify-content:space-between;align-items:baseline;gap:12px;border-bottom:2px solid var(--br-ink);padding-bottom:8px;margin-bottom:12px}
.epc-br__area-head h2{margin:0;font-family:Syne,sans-serif;font-size:1.35rem}
.epc-br__fn{display:grid;grid-template-columns:minmax(140px,220px) 1fr minmax(90px,120px);gap:8px 14px;padding:10px 0;border-bottom:1px solid rgba(15,23,42,.08);font-size:.9rem}
.epc-br__fn p{margin:0;color:var(--br-muted)}
.epc-br__fn code{font-size:.72rem;color:var(--br-accent);word-break:break-all}
.epc-br__legend{font-size:.85rem;color:var(--br-muted);margin:0 0 24px}
.epc-br__card.is-hidden,.epc-br__area.is-hidden,.epc-br__tile.is-hidden{display:none!important}
@media (max-width:860px){
  .epc-br__stats{grid-template-columns:1fr 1fr}
  .epc-br__area-banner{grid-template-columns:1fr;min-height:150px}
  .epc-br__fn{grid-template-columns:1fr}
}
@media print{
  .epc-br__filters,.epc-br__search,.epc-br__toolbar,.epc-br__modal,.epc-br__viewtoggle{display:none!important}
  .epc-br__hero-media{animation:none}
  .epc-br__card{break-inside:avoid;box-shadow:none}
  .epc-br__tile{break-inside:avoid}
}
CSS;
	return $base . $extra;
}

/**
 * @param array{scope?:string,brand?:string,print?:bool,base_path?:string,view?:string} $opts
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
	$view = preg_replace('/[^a-z]/', '', strtolower((string) ($opts['view'] ?? ($_GET['view'] ?? 'deck'))));
	if ($view !== 'catalog') {
		$view = 'deck';
	}

	$p = epc_brochure_profile($brand);
	$bundle = epc_cp_brochure_filtered_bundle($scope);
	$inv = $bundle['areas'];
	$meta = $bundle['meta'];
	$total = (int) $meta['total'];
	$areaCount = (int) $meta['area_count'];
	$syncedAt = date('d M Y · H:i', (int) ($meta['generated_at'] ?? time()));
	$sources = isset($meta['sources']) && is_array($meta['sources']) ? $meta['sources'] : array();

	$basePath = (string) ($opts['base_path'] ?? '/brochure/cp');
	$link = static function (string $s, string $v = '') use ($basePath): string {
		$q = 'scope=' . rawurlencode($s);
		if ($v !== '') {
			$q .= '&view=' . rawurlencode($v);
		}
		return epc_brochure_h($basePath . '?' . $q);
	};

	$scopeLabel = array(
		'client' => 'Client CP (tenant)',
		'super' => 'Super CP (platform)',
		'all' => 'All CP functions',
	);
	$visuals = epc_cp_brochure_area_visuals();
	$productBrochure = '/brochure';
	$css = epc_cp_brochure_css($p);
	$title = epc_brochure_h($p['name'] . ' — Full Control Panel brochure');
	$desc = epc_brochure_h('Graphical map of every Control Panel function (' . $total . ' items) — auto-synced. ' . ($scopeLabel[$scope] ?? $scope) . '.');
	$cover = epc_brochure_h($p['cover']);
	$autoPrint = !empty($opts['print']) ? '<script>window.addEventListener("load",function(){setTimeout(function(){window.print()},400)});</script>' : '';

	$filters = '<div class="epc-br__filters">'
		. '<a class="' . ($scope === 'client' ? 'is-on' : '') . '" href="' . $link('client', $view) . '">Client CP</a>'
		. '<a class="' . ($scope === 'super' ? 'is-on' : '') . '" href="' . $link('super', $view) . '">Super CP</a>'
		. '<a class="' . ($scope === 'all' ? 'is-on' : '') . '" href="' . $link('all', $view) . '">All</a>'
		. '</div>';

	$viewToggle = '<div class="epc-br__viewtoggle epc-br__filters">'
		. '<a class="' . ($view === 'deck' ? 'is-on' : '') . '" href="' . $link($scope, 'deck') . '"><i class="fa fa-th-large"></i> Graphical</a>'
		. '<a class="' . ($view === 'catalog' ? 'is-on' : '') . '" href="' . $link($scope, 'catalog') . '"><i class="fa fa-list"></i> Catalogue</a>'
		. '</div>';

	$bodyInner = ($view === 'catalog')
		? epc_cp_brochure_render_catalog_body($inv, $scopeLabel[$scope] ?? $scope, $total)
		: epc_cp_brochure_render_deck_body($inv, $visuals, $total, $areaCount, $syncedAt, $sources);

	$js = <<<'JS'
(function(){
  var search=document.getElementById('epc-br-search');
  var status=document.getElementById('epc-br-status');
  var modal=document.getElementById('epc-br-modal');
  var mTitle=document.getElementById('epc-br-modal-title');
  var mBody=document.getElementById('epc-br-modal-body');
  var mUrl=document.getElementById('epc-br-modal-url');
  var mClose=document.getElementById('epc-br-modal-close');
  function filter(){
    var q=(search&&search.value||'').toLowerCase().trim();
    var cards=document.querySelectorAll('.epc-br__card[data-q]');
    var shown=0;
    cards.forEach(function(c){
      var hit=!q||(c.getAttribute('data-q')||'').indexOf(q)!==-1;
      c.classList.toggle('is-hidden',!hit);
      if(hit) shown++;
    });
    document.querySelectorAll('.epc-br__area').forEach(function(area){
      var any=area.querySelector('.epc-br__card:not(.is-hidden)');
      area.classList.toggle('is-hidden',!any);
    });
    if(status) status.textContent=q?('Showing '+shown+' matches'):('Showing all '+shown+' functions');
  }
  if(search){search.addEventListener('input',filter);filter();}
  function openCard(card){
    if(!modal||!card) return;
    mTitle.textContent=card.getAttribute('data-name')||'';
    mBody.textContent=card.getAttribute('data-does')||'';
    var u=card.getAttribute('data-url')||'';
    mUrl.textContent=u||'Route available inside Control Panel';
    mUrl.style.display=u?'block':'none';
    modal.classList.add('is-open');
    modal.setAttribute('aria-hidden','false');
  }
  document.addEventListener('click',function(e){
    var card=e.target.closest('.epc-br__card[data-name]');
    if(card){openCard(card);return;}
    if(e.target===modal||e.target===mClose){modal.classList.remove('is-open');modal.setAttribute('aria-hidden','true');}
  });
  document.addEventListener('keydown',function(e){
    if(e.key==='Escape'&&modal){modal.classList.remove('is-open');}
  });
})();
JS;

	return '<!DOCTYPE html><html lang="en"><head><meta charset="utf-8">'
		. '<meta name="viewport" content="width=device-width,initial-scale=1">'
		. '<title>' . $title . '</title>'
		. '<meta name="description" content="' . $desc . '">'
		. '<meta name="robots" content="index,follow">'
		. '<meta property="og:title" content="' . $title . '">'
		. '<meta property="og:image" content="' . epc_brochure_h(rtrim((string) $p['url'], '/') . $p['cover']) . '">'
		. '<style>' . $css . '</style></head><body>'
		. '<div class="epc-br epc-br--deck">'
		. '<div class="epc-br__bar"><a class="epc-br__brand" href="' . epc_brochure_h($p['url']) . '">' . epc_brochure_h($p['name']) . '</a>'
		. '<div class="epc-br__actions">'
		. '<a class="epc-br__btn epc-br__btn--ghost" href="' . epc_brochure_h($productBrochure) . '">Product brochure</a>'
		. '<button type="button" class="epc-br__btn epc-br__btn--ghost" onclick="window.print()">Print / PDF</button>'
		. '<a class="epc-br__btn epc-br__btn--pri" href="' . epc_brochure_h($p['cp_url']) . '">Open Control Panel</a>'
		. '</div></div>'
		. '<header class="epc-br__hero epc-br__hero--deck">'
		. '<img class="epc-br__hero-media" src="' . $cover . '" alt="' . epc_brochure_h($p['name'] . ' Control Panel') . '" width="1600" height="900">'
		. '<div class="epc-br__hero-veil" aria-hidden="true"></div>'
		. '<div class="epc-br__hero-inner">'
		. '<div class="epc-br__live"><i class="fa fa-circle"></i> Live synced · ' . epc_brochure_h($syncedAt) . '</div>'
		. '<div class="epc-br__eyebrow">Control Panel · graphical presentation</div>'
		. '<h1>' . epc_brochure_h($p['name']) . '</h1>'
		. '<p>Every operator capability as a visual map — <strong>' . $total . '</strong> functions across '
		. $areaCount . ' areas. Auto-updates when ERP modules or platform capabilities change.</p>'
		. '<div class="epc-br__hero-ctas">'
		. '<a class="epc-br__btn epc-br__btn--pri" href="#areas">Explore areas</a>'
		. '<a class="epc-br__btn epc-br__btn--ghost" href="' . epc_brochure_h($p['cp_url']) . '">Open /cp</a>'
		. '</div></div></header>'
		. '<div class="epc-br__toolbar" style="margin-top:28px">'
		. $filters . $viewToggle
		. '</div>'
		. $bodyInner
		. '<footer class="epc-br__foot">'
		. '<div><strong>' . epc_brochure_h($p['name']) . ' Control Panel</strong><br>'
		. '<span style="color:var(--br-muted)">' . epc_brochure_h($p['legal']) . ' · ' . $total . ' functions · auto-synced</span>'
		. '<p class="epc-br__sources">Sources: ' . epc_brochure_h(implode(' · ', $sources)) . '</p></div>'
		. '<div style="text-align:right">'
		. '<a href="' . epc_brochure_h($productBrochure) . '">Product overview brochure →</a><br>'
		. '<a href="' . epc_brochure_h($p['cp_url']) . '">Open /cp →</a><br>'
		. '<a href="mailto:' . epc_brochure_h($p['contact_email']) . '">' . epc_brochure_h($p['contact_email']) . '</a>'
		. '</div></footer></div>'
		. '<div class="epc-br__modal" id="epc-br-modal" aria-hidden="true" role="dialog">'
		. '<div class="epc-br__modal-panel">'
		. '<h3 id="epc-br-modal-title"></h3>'
		. '<p id="epc-br-modal-body"></p>'
		. '<code id="epc-br-modal-url"></code>'
		. '<button type="button" class="epc-br__btn epc-br__btn--pri" id="epc-br-modal-close">Close</button>'
		. '</div></div>'
		. '<script>' . $js . '</script>'
		. $autoPrint
		. '</body></html>';
}

/**
 * @param array<string, array<int, array<string,mixed>>> $inv
 * @param array<string, array{icon:string,image:string,blurb:string}> $visuals
 * @param array<int, string> $sources
 */
function epc_cp_brochure_render_deck_body(array $inv, array $visuals, int $total, int $areaCount, string $syncedAt, array $sources): string
{
	$stats = '<div class="epc-br__stats">'
		. '<div><strong>' . $total . '</strong><span>Functions mapped</span></div>'
		. '<div><strong>' . $areaCount . '</strong><span>Visual areas</span></div>'
		. '<div><strong>Live</strong><span>Auto-sync on open</span></div>'
		. '<div><strong>PDF</strong><span>Print-ready deck</span></div>'
		. '</div>';

	$mosaic = '<nav class="epc-br__mosaic" id="areas" aria-label="CP areas">';
	$i = 0;
	foreach ($inv as $area => $items) {
		$aid = 'area-' . preg_replace('/[^a-z0-9]+/', '-', strtolower($area));
		$v = $visuals[$area] ?? array('icon' => 'fa-cube', 'image' => '/content/general_pages/marketing_screens/og_cover.png', 'blurb' => '');
		$img = epc_brochure_h((string) $v['image']);
		$icon = epc_brochure_h((string) $v['icon']);
		$delay = min(0.08 * $i, 0.6);
		$mosaic .= '<a class="epc-br__tile" href="#' . epc_brochure_h($aid) . '" style="animation:epc-br-fade .5s ' . $delay . 's both">'
			. '<span class="epc-br__tile-bg" style="background-image:url(' . $img . ')"></span>'
			. '<span class="epc-br__tile-veil" aria-hidden="true"></span>'
			. '<em>' . count($items) . ' functions</em>'
			. '<i class="fa ' . $icon . '" aria-hidden="true"></i>'
			. '<strong>' . epc_brochure_h($area) . '</strong>'
			. '<span>' . epc_brochure_h((string) ($v['blurb'] ?? '')) . '</span>'
			. '</a>';
		$i++;
	}
	$mosaic .= '</nav>';

	$toolbar = '<div class="epc-br__toolbar">'
		. '<input type="search" class="epc-br__search" id="epc-br-search" placeholder="Search functions…" autocomplete="off">'
		. '<p class="epc-br__legend" id="epc-br-status" style="margin:0">Showing all ' . $total . ' functions</p>'
		. '</div>';

	$areasHtml = '';
	foreach ($inv as $area => $items) {
		$aid = 'area-' . preg_replace('/[^a-z0-9]+/', '-', strtolower($area));
		$v = $visuals[$area] ?? array('icon' => 'fa-cube', 'image' => '/content/general_pages/marketing_screens/og_cover.png', 'blurb' => '');
		$img = epc_brochure_h((string) $v['image']);
		$areasHtml .= '<section class="epc-br__area" id="' . epc_brochure_h($aid) . '" data-area="' . epc_brochure_h(strtolower($area)) . '">';
		$areasHtml .= '<div class="epc-br__area-banner">'
			. '<div class="epc-br__area-banner-media" style="background-image:url(' . $img . ')"></div>'
			. '<div class="epc-br__area-banner-copy">'
			. '<h2><i class="fa ' . epc_brochure_h((string) $v['icon']) . '"></i> ' . epc_brochure_h($area) . '</h2>'
			. '<p>' . epc_brochure_h((string) ($v['blurb'] !== '' ? $v['blurb'] : 'Operator capabilities in this area.')) . '</p>'
			. '<span class="epc-br__count">' . count($items) . ' capabilities</span>'
			. '</div></div>';
		$areasHtml .= '<div class="epc-br__cards">';
		foreach ($items as $item) {
			$sc = (string) ($item['scope'] ?? 'client');
			$scopeClass = $sc === 'super' ? ' epc-br__scope--super' : ($sc === 'both' ? ' epc-br__scope--both' : '');
			$url = trim((string) ($item['url'] ?? ''));
			$name = (string) ($item['name'] ?? '');
			$does = (string) ($item['does'] ?? '');
			$icon = preg_replace('/[^a-z0-9\-]/', '', strtolower((string) ($item['icon'] ?? 'fa-cube')));
			if (strpos($icon, 'fa-') !== 0) {
				$icon = 'fa-cube';
			}
			$q = strtolower($name . ' ' . $does . ' ' . $url . ' ' . $area);
			$areasHtml .= '<article class="epc-br__card" tabindex="0" role="button"'
				. ' data-q="' . epc_brochure_h($q) . '"'
				. ' data-name="' . epc_brochure_h($name) . '"'
				. ' data-does="' . epc_brochure_h($does) . '"'
				. ' data-url="' . epc_brochure_h($url) . '">'
				. '<span class="epc-br__card-ico"><i class="fa ' . epc_brochure_h($icon) . '" aria-hidden="true"></i></span>'
				. '<h3>' . epc_brochure_h($name) . '</h3>'
				. '<p>' . epc_brochure_h($does) . '</p>'
				. '<div class="epc-br__card-foot">'
				. '<span class="epc-br__scope' . $scopeClass . '">' . epc_brochure_h($sc) . '</span>'
				. ($url !== '' ? '<code>' . epc_brochure_h($url) . '</code>' : '<span></span>')
				. '</div></article>';
		}
		$areasHtml .= '</div></section>';
	}

	return $stats
		. '<section class="epc-br__sec"><h2>Explore by area</h2>'
		. '<p>Visual tiles jump into each Control Panel domain. Cards open full detail — synced from live ERP navigation and the platform capabilities catalog.</p></section>'
		. $mosaic
		. $toolbar
		. $areasHtml;
}

/**
 * @param array<string, array<int, array<string,mixed>>> $inv
 */
function epc_cp_brochure_render_catalog_body(array $inv, string $scopeLabel, int $total): string
{
	$toc = '';
	foreach ($inv as $area => $items) {
		$aid = 'area-' . preg_replace('/[^a-z0-9]+/', '-', strtolower($area));
		$toc .= '<a href="#' . epc_brochure_h($aid) . '">' . epc_brochure_h($area)
			. '<span>' . count($items) . ' functions</span></a>';
	}
	$html = '<p class="epc-br__legend">Catalogue view — <strong>' . epc_brochure_h($scopeLabel) . '</strong> — '
		. $total . ' functions. Switch to Graphical for the visual deck.</p>'
		. '<section class="epc-br__sec"><h2>Jump to area</h2></section>'
		. '<nav class="epc-br__toc">' . $toc . '</nav>';
	foreach ($inv as $area => $items) {
		$aid = 'area-' . preg_replace('/[^a-z0-9]+/', '-', strtolower($area));
		$html .= '<section class="epc-br__area" id="' . epc_brochure_h($aid) . '">';
		$html .= '<div class="epc-br__area-head"><h2>' . epc_brochure_h($area) . '</h2><em>' . count($items) . ' items</em></div>';
		foreach ($items as $item) {
			$sc = (string) ($item['scope'] ?? 'client');
			$scopeClass = $sc === 'super' ? ' epc-br__scope--super' : ($sc === 'both' ? ' epc-br__scope--both' : '');
			$url = trim((string) ($item['url'] ?? ''));
			$html .= '<div class="epc-br__fn">';
			$html .= '<div><strong>' . epc_brochure_h($item['name'] ?? '') . '</strong><br>';
			$html .= '<span class="epc-br__scope' . $scopeClass . '">' . epc_brochure_h($sc) . '</span></div>';
			$html .= '<p>' . epc_brochure_h($item['does'] ?? '') . '</p>';
			$html .= '<div>' . ($url !== '' ? '<code>' . epc_brochure_h($url) . '</code>' : '—') . '</div>';
			$html .= '</div>';
		}
		$html .= '</section>';
	}
	return $html;
}

function epc_cp_full_brochure_render_and_exit(array $opts = array()): void
{
	if (!headers_sent()) {
		header('Content-Type: text/html; charset=utf-8');
		header('X-Robots-Tag: index, follow');
		header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
	}
	echo epc_cp_full_brochure_render_html($opts);
	exit;
}
