<?php
/**
 * Dedicated sub-industry page — distinct presentation from the industry hub.
 *
 * No shared hub hero photo, hero video, or 3D animation. Layout/tone vary by
 * sub-industry slug. Expects variables from _base_template.php:
 * $industryData, $name, $icon, $primary, $accent, $activeSub, $seo*, $demoKey, …
 */
defined('_ASTEXE_') or die('No access');

$subLabel = (string) ($activeSub['label'] ?? '');
$subSlug = (string) ($activeSub['slug'] ?? '');
$subProductsMap = $industryData['sub_industry_products'] ?? array();
$subPack = (isset($subProductsMap[$subLabel]) && is_array($subProductsMap[$subLabel]))
	? $subProductsMap[$subLabel]
	: array();

$subDesc = trim((string) ($subPack['desc'] ?? ''));
if ($subDesc === '') {
	$subDesc = 'Specialized ' . $subLabel . ' operations on the ecomae ' . $name
		. ' platform — ERP workflows, control panel configuration, and storefront ready for this vertical.';
}
$subCats = array();
if (!empty($subPack['categories']) && is_array($subPack['categories'])) {
	foreach ($subPack['categories'] as $c) {
		$c = trim((string) $c);
		if ($c !== '') {
			$subCats[] = $c;
		}
	}
}
if ($subCats === array()) {
	$subCats = array('Core services', 'Products', 'Packages', 'Support', 'Premium', 'Analytics');
}
$subProds = array();
if (!empty($subPack['products']) && is_array($subPack['products'])) {
	$subProds = $subPack['products'];
}

// Cover image: prefer sub-specific photo / product art — never the hub hero photo
$hubHero = (string) ($industryData['hero_photo'] ?? '');
$subPhoto = trim((string) ($subPack['photo'] ?? ''));
if ($subPhoto !== '' && $subPhoto === $hubHero) {
	$subPhoto = '';
}
if ($subPhoto === '' && !empty($subProds[0]['image'])) {
	$subPhoto = (string) $subProds[0]['image'];
}
$hasPhoto = ($subPhoto !== '');

$pres = function_exists('epc_industry_seo_sub_presentation')
	? epc_industry_seo_sub_presentation($subSlug)
	: array('key' => 'atelier', 'label' => 'Atelier', 'tone' => 'warm');
$presKey = (string) ($pres['key'] ?? 'atelier');

// Enrich meta for this vertical
$catPreview = implode(', ', array_slice($subCats, 0, 6));
$seoTitle = $subLabel . ' — ' . $name . ' | ecomae';
$seoDesc = $subLabel . ' on ' . $seoHost . ': categories include ' . $catPreview
	. '. Dedicated ERP, CP and storefront for this ' . strtolower($name) . ' vertical.';
$seoPageName = $subLabel . ' — ' . $name;
$seoKeywords = strtolower($subLabel . ', ' . $name . ', ' . $catPreview . ', ERP, storefront, ecomae');
$ogImage = $hasPhoto ? $subPhoto : '';

// Sibling links
$siblings = array();
foreach (array_values($subIndustries) as $siLabel) {
	$siSlug = epc_industry_seo_sub_slug((string) $siLabel);
	if ($siSlug === '' || $siSlug === $subSlug) {
		continue;
	}
	$siblings[] = array('label' => (string) $siLabel, 'slug' => $siSlug, 'url' => $seoBase . '/' . $siSlug);
	if (count($siblings) >= 8) {
		break;
	}
}

$cpUrl = '/cp/demo/' . rawurlencode((string) $demoKey) . '/';
$erpUrl = $cpUrl . 'shop/finance/erp';
$hubUrl = $seoBase . '/';

$catIcons = array(
	'fa-diamond', 'fa-tags', 'fa-cubes', 'fa-wrench', 'fa-certificate', 'fa-star',
	'fa-shopping-bag', 'fa-cogs', 'fa-flask', 'fa-truck', 'fa-heartbeat', 'fa-leaf',
);

// Deterministic accent shift per slug so pages feel distinct within the industry palette
$hueShift = (int) (crc32($subSlug) % 40) - 20;
?>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title><?php echo htmlspecialchars($seoTitle); ?></title>
<meta name="description" content="<?php echo htmlspecialchars($seoDesc); ?>">
<meta name="keywords" content="<?php echo htmlspecialchars($seoKeywords); ?>">
<meta name="robots" content="index, follow">
<link rel="canonical" href="<?php echo htmlspecialchars($seoCanonical); ?>">
<meta property="og:type" content="website">
<meta property="og:title" content="<?php echo htmlspecialchars($seoPageName); ?>">
<meta property="og:description" content="<?php echo htmlspecialchars($seoDesc); ?>">
<?php if ($ogImage !== ''): ?>
<meta property="og:image" content="<?php echo htmlspecialchars($ogImage); ?>">
<?php endif; ?>
<meta property="og:url" content="<?php echo htmlspecialchars($seoCanonical); ?>">
<meta property="og:site_name" content="ecomae">
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:title" content="<?php echo htmlspecialchars($seoPageName); ?>">
<meta name="twitter:description" content="<?php echo htmlspecialchars($seoDesc); ?>">
<script type="application/ld+json">
<?php
$jsonLd = array(
	'@context' => 'https://schema.org',
	'@type' => 'CollectionPage',
	'name' => $seoPageName,
	'description' => $seoDesc,
	'url' => $seoCanonical,
	'isPartOf' => array('@type' => 'WebSite', 'name' => $name . ' — ecomae', 'url' => $hubUrl),
	'breadcrumb' => array(
		'@type' => 'BreadcrumbList',
		'itemListElement' => array(
			array('@type' => 'ListItem', 'position' => 1, 'name' => 'ecomae', 'item' => 'https://www.ecomae.com'),
			array('@type' => 'ListItem', 'position' => 2, 'name' => $name, 'item' => $hubUrl),
			array('@type' => 'ListItem', 'position' => 3, 'name' => $subLabel, 'item' => $seoCanonical),
		),
	),
	'mainEntity' => array(
		'@type' => 'ItemList',
		'name' => $subLabel . ' categories',
		'numberOfItems' => count($subCats),
		'itemListElement' => array(),
	),
);
foreach ($subCats as $i => $cat) {
	$jsonLd['mainEntity']['itemListElement'][] = array(
		'@type' => 'ListItem',
		'position' => $i + 1,
		'name' => $cat,
	);
}
echo json_encode($jsonLd, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
?>
</script>
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Fraunces:opsz,wght@9..144,600;9..144,700&family=DM+Sans:wght@400;500;600;700&display=swap" rel="stylesheet">
<style>
:root{
  --isp-primary:<?php echo htmlspecialchars($primary); ?>;
  --isp-accent:<?php echo htmlspecialchars($accent); ?>;
  --isp-ink:#0f172a;
  --isp-muted:#475569;
  --isp-line:#e2e8f0;
  --isp-paper:#f7f4ef;
  --isp-shift:<?php echo (int) $hueShift; ?>deg;
}
*{box-sizing:border-box;margin:0;padding:0}
body.isp-body{
  font-family:"DM Sans",system-ui,sans-serif;
  color:var(--isp-ink);
  background:#fafafa;
  line-height:1.55;
}
body.isp-body.tone-warm{background:var(--isp-paper)}
body.isp-body.tone-ink{background:#f1f5f9}
body.isp-body.tone-bright{background:#fff}
body.isp-body.tone-cool{background:#f0f7fa}
a{color:inherit}
.isp-top{
  position:sticky;top:0;z-index:40;
  display:flex;align-items:center;justify-content:space-between;gap:16px;
  padding:14px 22px;background:rgba(255,255,255,.92);backdrop-filter:blur(10px);
  border-bottom:1px solid var(--isp-line);
}
.isp-brand{display:flex;align-items:center;gap:10px;text-decoration:none;font-weight:700}
.isp-brand__mark{
  width:34px;height:34px;border-radius:10px;display:grid;place-items:center;color:#fff;
  background:linear-gradient(135deg,var(--isp-primary),var(--isp-accent));
  filter:hue-rotate(var(--isp-shift));
}
.isp-brand__sub{display:block;font-size:11px;font-weight:600;color:var(--isp-muted)}
.isp-top__nav{display:flex;gap:14px;align-items:center;flex-wrap:wrap}
.isp-top__nav a{font-size:13px;font-weight:600;text-decoration:none;color:var(--isp-muted)}
.isp-top__nav a:hover{color:var(--isp-primary)}
.isp-btn{
  display:inline-flex;align-items:center;gap:8px;padding:10px 16px;border-radius:999px;
  font-size:13px;font-weight:700;text-decoration:none;border:1px solid transparent;cursor:pointer;
}
.isp-btn--p{background:var(--isp-primary);color:#fff;filter:hue-rotate(var(--isp-shift))}
.isp-btn--g{background:transparent;border-color:var(--isp-line);color:var(--isp-ink)}
.isp-wrap{max-width:1120px;margin:0 auto;padding:0 22px}
.isp-crumb{padding:18px 0 0;font-size:12px;color:var(--isp-muted)}
.isp-crumb a{color:var(--isp-muted);text-decoration:none}
.isp-crumb a:hover{color:var(--isp-primary)}

/* —— Atelier: typographic band, no full-bleed hub hero —— */
.isp-pres-atelier .isp-mast{
  padding:36px 0 28px;border-bottom:1px solid var(--isp-line);
}
.isp-pres-atelier .isp-kicker{
  display:inline-block;font-size:11px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;
  color:var(--isp-primary);filter:hue-rotate(var(--isp-shift));margin-bottom:10px;
}
.isp-pres-atelier h1{
  font-family:Fraunces,Georgia,serif;font-size:clamp(2rem,4vw,3.2rem);font-weight:700;
  letter-spacing:-.02em;max-width:16ch;line-height:1.1;margin-bottom:14px;
}
.isp-pres-atelier .isp-lead{max-width:62ch;color:var(--isp-muted);font-size:1.05rem}
.isp-pres-atelier .isp-mast__row{display:grid;grid-template-columns:1.2fr .8fr;gap:28px;align-items:end}
.isp-pres-atelier .isp-figure{
  border-radius:18px;overflow:hidden;aspect-ratio:4/3;background:linear-gradient(145deg,var(--isp-primary),var(--isp-accent));
  filter:hue-rotate(var(--isp-shift));box-shadow:0 18px 40px rgba(15,23,42,.12);
}
.isp-pres-atelier .isp-figure img{width:100%;height:100%;object-fit:cover;display:block;mix-blend-mode:normal}
.isp-pres-atelier .isp-figure--pattern{
  display:grid;place-items:center;color:rgba(255,255,255,.9);font-size:64px;
  background:
    radial-gradient(circle at 30% 30%,rgba(255,255,255,.25),transparent 45%),
    linear-gradient(145deg,var(--isp-primary),var(--isp-accent));
}

/* —— Ledger: document strip, ink rails —— */
.isp-pres-ledger .isp-mast{
  margin:24px 0 0;padding:28px 28px 24px;background:#fff;border:1px solid var(--isp-line);
  border-left:6px solid var(--isp-primary);border-radius:4px 16px 16px 4px;
  filter:hue-rotate(0deg);
  box-shadow:0 10px 30px rgba(15,23,42,.04);
}
.isp-pres-ledger .isp-mast{border-left-color:var(--isp-accent)}
.isp-pres-ledger .isp-kicker{font-size:11px;font-weight:700;color:var(--isp-muted);letter-spacing:.08em;text-transform:uppercase}
.isp-pres-ledger h1{font-size:clamp(1.8rem,3vw,2.6rem);margin:8px 0 12px;font-weight:800}
.isp-pres-ledger .isp-lead{color:var(--isp-muted);max-width:70ch}
.isp-pres-ledger .isp-meta{display:flex;flex-wrap:wrap;gap:10px;margin-top:16px}
.isp-pres-ledger .isp-chip{
  font-size:11px;font-weight:700;padding:5px 10px;border-radius:6px;background:#f1f5f9;color:var(--isp-ink);
}

/* —— Mosaic: categories-first, no large photo —— */
.isp-pres-mosaic .isp-mast{padding:40px 0 8px;text-align:center}
.isp-pres-mosaic .isp-kicker{
  display:inline-block;padding:6px 12px;border-radius:999px;background:rgba(15,23,42,.06);
  font-size:11px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;margin-bottom:12px;
}
.isp-pres-mosaic h1{font-family:Fraunces,Georgia,serif;font-size:clamp(2rem,4vw,3rem);margin-bottom:12px}
.isp-pres-mosaic .isp-lead{margin:0 auto;max-width:58ch;color:var(--isp-muted)}

/* —— Dock: cool split bar —— */
.isp-pres-dock .isp-mast{
  margin-top:20px;display:grid;grid-template-columns:1fr 1fr;min-height:280px;
  border-radius:24px;overflow:hidden;border:1px solid var(--isp-line);background:#0b1220;color:#e2e8f0;
}
.isp-pres-dock .isp-mast__copy{padding:36px 32px;display:flex;flex-direction:column;justify-content:center}
.isp-pres-dock .isp-kicker{color:var(--isp-accent);font-size:11px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;margin-bottom:10px}
.isp-pres-dock h1{font-size:clamp(1.7rem,3vw,2.5rem);color:#fff;margin-bottom:12px;line-height:1.15}
.isp-pres-dock .isp-lead{color:#94a3b8}
.isp-pres-dock .isp-mast__media{position:relative;min-height:220px;background:linear-gradient(160deg,var(--isp-primary),#020617)}
.isp-pres-dock .isp-mast__media img{width:100%;height:100%;object-fit:cover;opacity:.85}
.isp-pres-dock .isp-mast__media--empty{
  display:grid;place-items:center;font-size:72px;color:rgba(255,255,255,.35);
}

.isp-actions{display:flex;flex-wrap:wrap;gap:10px;margin-top:22px}
.isp-pres-dock .isp-btn--g{border-color:rgba(255,255,255,.25);color:#fff}

.isp-section{padding:40px 0 20px}
.isp-section h2{
  font-size:1.35rem;font-weight:800;margin-bottom:8px;
}
.isp-section .isp-sec-lead{color:var(--isp-muted);font-size:14px;margin-bottom:22px;max-width:60ch}

.isp-cats{
  display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:14px;
}
.isp-cat{
  background:#fff;border:1px solid var(--isp-line);border-radius:16px;padding:18px 16px;
  transition:transform .25s,box-shadow .25s,border-color .25s;
}
.isp-cat:hover{transform:translateY(-3px);box-shadow:0 12px 28px rgba(15,23,42,.08);border-color:var(--isp-accent)}
.isp-cat__ico{
  width:40px;height:40px;border-radius:12px;display:grid;place-items:center;margin-bottom:12px;color:#fff;
  background:linear-gradient(135deg,var(--isp-primary),var(--isp-accent));
  filter:hue-rotate(var(--isp-shift));
}
.isp-cat__name{font-size:14px;font-weight:700}
.isp-pres-mosaic .isp-cats{grid-template-columns:repeat(auto-fill,minmax(180px,1fr))}
.isp-pres-mosaic .isp-cat{min-height:140px;display:flex;flex-direction:column;justify-content:flex-end;
  background:linear-gradient(180deg,#fff, #f8fafc)}

.isp-prods{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:16px}
.isp-prod{
  background:#fff;border:1px solid var(--isp-line);border-radius:16px;overflow:hidden;
}
.isp-prod__img{height:140px;background:#e2e8f0}
.isp-prod__img img{width:100%;height:100%;object-fit:cover;display:block}
.isp-prod__body{padding:14px}
.isp-prod__name{font-size:14px;font-weight:700;margin-bottom:4px}
.isp-prod__price{font-size:13px;font-weight:700;color:var(--isp-primary);filter:hue-rotate(var(--isp-shift))}

.isp-siblings{display:flex;flex-wrap:wrap;gap:8px}
.isp-siblings a{
  font-size:12px;font-weight:600;padding:8px 12px;border-radius:999px;text-decoration:none;
  background:#fff;border:1px solid var(--isp-line);color:var(--isp-muted);
}
.isp-siblings a:hover{border-color:var(--isp-primary);color:var(--isp-primary)}

.isp-foot{
  margin-top:48px;padding:28px 22px 36px;border-top:1px solid var(--isp-line);
  color:var(--isp-muted);font-size:12px;
}
.isp-foot__inner{max-width:1120px;margin:0 auto;display:flex;justify-content:space-between;gap:16px;flex-wrap:wrap}
.isp-foot a{color:var(--isp-muted)}

@media(max-width:800px){
  .isp-pres-atelier .isp-mast__row,
  .isp-pres-dock .isp-mast{grid-template-columns:1fr}
  .isp-top__nav .isp-hide-sm{display:none}
}
</style>
</head>
<body class="isp-body tone-<?php echo htmlspecialchars((string) ($pres['tone'] ?? 'warm')); ?> isp-pres-<?php echo htmlspecialchars($presKey); ?>">

<header class="isp-top">
  <a class="isp-brand" href="<?php echo htmlspecialchars($hubUrl); ?>">
    <span class="isp-brand__mark"><i class="fa <?php echo htmlspecialchars($icon); ?>"></i></span>
    <span><?php echo htmlspecialchars($name); ?>
      <span class="isp-brand__sub">Sub-industry · <?php echo htmlspecialchars((string) ($pres['label'] ?? 'Page')); ?></span>
    </span>
  </a>
  <nav class="isp-top__nav">
    <a class="isp-hide-sm" href="<?php echo htmlspecialchars($hubUrl); ?>">All <?php echo htmlspecialchars($name); ?></a>
    <a class="isp-hide-sm" href="#categories">Categories</a>
    <a class="isp-btn isp-btn--g" href="<?php echo htmlspecialchars($cpUrl); ?>">Open CP</a>
    <a class="isp-btn isp-btn--p" href="<?php echo htmlspecialchars($erpUrl); ?>">Launch ERP</a>
  </nav>
</header>

<main>
  <div class="isp-wrap">
    <nav class="isp-crumb" aria-label="Breadcrumb">
      <a href="https://www.ecomae.com/platform/industries">Industries</a> /
      <a href="<?php echo htmlspecialchars($hubUrl); ?>"><?php echo htmlspecialchars($name); ?></a> /
      <span><?php echo htmlspecialchars($subLabel); ?></span>
    </nav>

<?php if ($presKey === 'atelier'): ?>
    <header class="isp-mast">
      <div class="isp-mast__row">
        <div>
          <div class="isp-kicker"><?php echo htmlspecialchars($name); ?> vertical</div>
          <h1><?php echo htmlspecialchars($subLabel); ?></h1>
          <p class="isp-lead"><?php echo htmlspecialchars($subDesc); ?></p>
          <div class="isp-actions">
            <a class="isp-btn isp-btn--p" href="<?php echo htmlspecialchars($erpUrl); ?>"><i class="fa fa-calculator"></i> Try ERP for this vertical</a>
            <a class="isp-btn isp-btn--g" href="<?php echo htmlspecialchars($hubUrl); ?>">Back to <?php echo htmlspecialchars($name); ?></a>
          </div>
        </div>
        <?php if ($hasPhoto): ?>
        <div class="isp-figure"><img src="<?php echo htmlspecialchars($subPhoto); ?>" alt="<?php echo htmlspecialchars($subLabel); ?> — <?php echo htmlspecialchars($name); ?>" width="640" height="480"></div>
        <?php else: ?>
        <div class="isp-figure isp-figure--pattern" aria-hidden="true"><i class="fa <?php echo htmlspecialchars($icon); ?>"></i></div>
        <?php endif; ?>
      </div>
    </header>

<?php elseif ($presKey === 'ledger'): ?>
    <header class="isp-mast">
      <div class="isp-kicker">Operating sheet · <?php echo htmlspecialchars($name); ?></div>
      <h1><?php echo htmlspecialchars($subLabel); ?></h1>
      <p class="isp-lead"><?php echo htmlspecialchars($subDesc); ?></p>
      <div class="isp-meta">
        <span class="isp-chip"><?php echo count($subCats); ?> categories</span>
        <span class="isp-chip"><?php echo count($subProds); ?> sample offerings</span>
        <span class="isp-chip">ERP + CP + Store</span>
      </div>
      <div class="isp-actions">
        <a class="isp-btn isp-btn--p" href="<?php echo htmlspecialchars($erpUrl); ?>">Open ERP workspace</a>
        <a class="isp-btn isp-btn--g" href="<?php echo htmlspecialchars($cpUrl); ?>">Control Panel</a>
      </div>
    </header>

<?php elseif ($presKey === 'mosaic'): ?>
    <header class="isp-mast">
      <div class="isp-kicker">Category map</div>
      <h1><?php echo htmlspecialchars($subLabel); ?></h1>
      <p class="isp-lead"><?php echo htmlspecialchars($subDesc); ?></p>
      <div class="isp-actions" style="justify-content:center">
        <a class="isp-btn isp-btn--p" href="#categories">Browse categories</a>
        <a class="isp-btn isp-btn--g" href="<?php echo htmlspecialchars($hubUrl); ?>"><?php echo htmlspecialchars($name); ?> hub</a>
      </div>
    </header>

<?php else: /* dock */ ?>
    <header class="isp-mast">
      <div class="isp-mast__copy">
        <div class="isp-kicker"><?php echo htmlspecialchars($name); ?></div>
        <h1><?php echo htmlspecialchars($subLabel); ?></h1>
        <p class="isp-lead"><?php echo htmlspecialchars($subDesc); ?></p>
        <div class="isp-actions">
          <a class="isp-btn isp-btn--p" href="<?php echo htmlspecialchars($erpUrl); ?>">Launch ERP</a>
          <a class="isp-btn isp-btn--g" href="<?php echo htmlspecialchars($cpUrl); ?>">Open CP</a>
        </div>
      </div>
      <?php if ($hasPhoto): ?>
      <div class="isp-mast__media"><img src="<?php echo htmlspecialchars($subPhoto); ?>" alt=""></div>
      <?php else: ?>
      <div class="isp-mast__media isp-mast__media--empty" aria-hidden="true"><i class="fa <?php echo htmlspecialchars($icon); ?>"></i></div>
      <?php endif; ?>
    </header>
<?php endif; ?>

    <section class="isp-section" id="categories" aria-labelledby="isp-cat-title">
      <h2 id="isp-cat-title">Product &amp; service categories</h2>
      <p class="isp-sec-lead">Defined for <strong><?php echo htmlspecialchars($subLabel); ?></strong> — not the generic <?php echo htmlspecialchars($name); ?> hub catalogue.</p>
      <div class="isp-cats">
        <?php foreach ($subCats as $ci => $cat):
          $ico = $catIcons[$ci % count($catIcons)];
          ?>
        <article class="isp-cat">
          <div class="isp-cat__ico"><i class="fa <?php echo htmlspecialchars($ico); ?>"></i></div>
          <div class="isp-cat__name"><?php echo htmlspecialchars($cat); ?></div>
        </article>
        <?php endforeach; ?>
      </div>
    </section>

    <?php if ($subProds !== array()): ?>
    <section class="isp-section" id="offerings" aria-labelledby="isp-prod-title">
      <h2 id="isp-prod-title">Sample offerings</h2>
      <p class="isp-sec-lead">Illustrative products and services for this vertical’s storefront and ERP demos.</p>
      <div class="isp-prods">
        <?php foreach ($subProds as $p):
          $pName = (string) ($p['name'] ?? 'Offering');
          $pPrice = (string) ($p['price'] ?? '');
          $pImg = (string) ($p['image'] ?? '');
          ?>
        <article class="isp-prod">
          <div class="isp-prod__img">
            <?php if ($pImg !== ''): ?>
            <img src="<?php echo htmlspecialchars($pImg); ?>" alt="<?php echo htmlspecialchars($pName); ?>" loading="lazy">
            <?php endif; ?>
          </div>
          <div class="isp-prod__body">
            <div class="isp-prod__name"><?php echo htmlspecialchars($pName); ?></div>
            <?php if ($pPrice !== ''): ?><div class="isp-prod__price"><?php echo htmlspecialchars($pPrice); ?></div><?php endif; ?>
          </div>
        </article>
        <?php endforeach; ?>
      </div>
    </section>
    <?php endif; ?>

    <?php if ($siblings !== array()): ?>
    <section class="isp-section" aria-labelledby="isp-sib-title">
      <h2 id="isp-sib-title">More <?php echo htmlspecialchars($name); ?> verticals</h2>
      <p class="isp-sec-lead">Each link opens a dedicated sub-industry page with its own categories.</p>
      <div class="isp-siblings">
        <?php foreach ($siblings as $sib): ?>
        <a href="<?php echo htmlspecialchars($sib['url']); ?>"><?php echo htmlspecialchars($sib['label']); ?></a>
        <?php endforeach; ?>
      </div>
    </section>
    <?php endif; ?>
  </div>
</main>

<footer class="isp-foot">
  <div class="isp-foot__inner">
    <p>© <?php echo date('Y'); ?> Electronic World Group · <?php echo htmlspecialchars($subLabel); ?> on <?php echo htmlspecialchars($seoHost); ?></p>
    <p>
      <a href="<?php echo htmlspecialchars($hubUrl); ?>"><?php echo htmlspecialchars($name); ?> hub</a> ·
      <a href="https://www.ecomae.com/legal">Legal</a> ·
      <a href="https://www.ecomae.com/">ecomae.com</a>
    </p>
  </div>
</footer>
</body>
</html>
<?php
// Parent (_base_template.php) returns after including this file.