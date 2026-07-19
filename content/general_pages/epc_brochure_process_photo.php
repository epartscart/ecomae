<?php
/**
 * Dummy presentation photo for brochure processes.
 * Returns an SVG "CP screen" mock unique to title + area — no auth required.
 *
 * GET: t=title&a=area&i=fa-icon
 */
declare(strict_types=1);

header('Content-Type: image/svg+xml; charset=utf-8');
header('Cache-Control: public, max-age=86400');
header('X-Content-Type-Options: nosniff');

$title = trim((string) ($_GET['t'] ?? 'Process'));
$area = trim((string) ($_GET['a'] ?? 'Control Panel'));
$icon = preg_replace('/[^a-z0-9\-]/', '', strtolower((string) ($_GET['i'] ?? 'cube'))) ?? 'cube';
$icon = preg_replace('/^fa-/', '', $icon) ?? 'cube';
if ($title === '') {
	$title = 'Process';
}
if (function_exists('mb_substr')) {
	$title = mb_substr($title, 0, 48);
	$area = mb_substr($area, 0, 40);
} else {
	$title = substr($title, 0, 48);
	$area = substr($area, 0, 40);
}

$hash = crc32($title . '|' . $area);
$palettes = array(
	array('#0f172a', '#1e293b', '#0ea5e9', '#38bdf8'),
	array('#14532d', '#166534', '#22c55e', '#86efac'),
	array('#7c2d12', '#9a3412', '#f97316', '#fdba74'),
	array('#312e81', '#3730a3', '#818cf8', '#c7d2fe'),
	array('#134e4a', '#115e59', '#14b8a6', '#5eead4'),
	array('#4c0519', '#9f1239', '#f43f5e', '#fda4af'),
	array('#1e3a5f', '#0c4a6e', '#0284c7', '#7dd3fc'),
	array('#3b0764', '#581c87', '#a855f7', '#d8b4fe'),
);
$p = $palettes[$hash % count($palettes)];
$bg = $p[0];
$panel = $p[1];
$accent = $p[2];
$accent2 = $p[3];
$seed = abs($hash) % 97;

function epc_br_photo_esc(string $s): string
{
	return htmlspecialchars($s, ENT_QUOTES | ENT_XML1, 'UTF-8');
}

$t = epc_br_photo_esc($title);
$a = epc_br_photo_esc($area !== '' ? $area : 'Control Panel');
$i = epc_br_photo_esc($icon);

// Fake UI chrome: sidebar + workspace + chart bars — reads as a presentation screenshot.
echo '<?xml version="1.0" encoding="UTF-8"?>';
?>
<svg xmlns="http://www.w3.org/2000/svg" width="960" height="540" viewBox="0 0 960 540" role="img" aria-label="<?= $t ?>">
  <defs>
    <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="<?= $bg ?>"/>
      <stop offset="100%" stop-color="<?= $panel ?>"/>
    </linearGradient>
    <linearGradient id="a" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0%" stop-color="<?= $accent ?>"/>
      <stop offset="100%" stop-color="<?= $accent2 ?>"/>
    </linearGradient>
    <filter id="soft" x="-20%" y="-20%" width="140%" height="140%">
      <feDropShadow dx="0" dy="8" stdDeviation="12" flood-color="#000" flood-opacity=".35"/>
    </filter>
  </defs>
  <rect width="960" height="540" fill="url(#g)"/>
  <circle cx="<?= 120 + ($seed % 40) ?>" cy="<?= 80 + ($seed % 30) ?>" r="160" fill="<?= $accent ?>" opacity=".12"/>
  <circle cx="<?= 820 - ($seed % 50) ?>" cy="<?= 420 ?>" r="200" fill="<?= $accent2 ?>" opacity=".1"/>

  <!-- Window chrome -->
  <g filter="url(#soft)">
    <rect x="48" y="40" width="864" height="460" rx="16" fill="#0b1220" opacity=".92"/>
    <rect x="48" y="40" width="864" height="36" rx="16" fill="#111827"/>
    <rect x="48" y="64" width="864" height="12" fill="#111827"/>
    <circle cx="72" cy="58" r="5" fill="#f87171"/>
    <circle cx="90" cy="58" r="5" fill="#fbbf24"/>
    <circle cx="108" cy="58" r="5" fill="#34d399"/>
    <rect x="140" y="50" width="220" height="16" rx="4" fill="#1f2937"/>

    <!-- Sidebar -->
    <rect x="60" y="88" width="180" height="396" rx="10" fill="#111827"/>
    <rect x="76" y="108" width="110" height="10" rx="3" fill="<?= $accent ?>" opacity=".9"/>
    <?php for ($n = 0; $n < 7; $n++) {
		$y = 140 + $n * 36;
		$on = ($n === ($seed % 7));
		$fill = $on ? $accent : '#1f2937';
		$w = $on ? 148 : (100 + (($seed + $n * 13) % 40));
		echo '<rect x="76" y="' . $y . '" width="' . $w . '" height="18" rx="5" fill="' . $fill . '" opacity="' . ($on ? '1' : '.75') . '"/>';
	} ?>

    <!-- Main canvas -->
    <rect x="256" y="88" width="640" height="396" rx="10" fill="#0f172a"/>
    <rect x="276" y="108" width="200" height="14" rx="4" fill="#e2e8f0" opacity=".9"/>
    <rect x="276" y="132" width="320" height="10" rx="3" fill="#64748b" opacity=".8"/>

    <!-- KPI tiles -->
    <?php for ($n = 0; $n < 3; $n++) {
		$x = 276 + $n * 200;
		echo '<rect x="' . $x . '" y="168" width="180" height="88" rx="10" fill="#1e293b"/>';
		echo '<rect x="' . ($x + 16) . '" y="186" width="70" height="8" rx="2" fill="' . $accent2 . '" opacity=".7"/>';
		echo '<rect x="' . ($x + 16) . '" y="206" width="' . (90 + (($seed + $n) % 50)) . '" height="22" rx="4" fill="#f8fafc" opacity=".85"/>';
	} ?>

    <!-- Chart bars -->
    <rect x="276" y="280" width="380" height="180" rx="10" fill="#1e293b"/>
    <?php
	$bars = array(48, 72, 56, 96, 64, 88, 40, 78);
	for ($n = 0; $n < 8; $n++) {
		$h = 30 + (($bars[$n] + $seed) % 90);
		$x = 296 + $n * 42;
		$y = 420 - $h;
		echo '<rect x="' . $x . '" y="' . $y . '" width="28" height="' . $h . '" rx="4" fill="url(#a)" opacity="' . (0.55 + ($n % 3) * 0.12) . '"/>';
	}
	?>

    <!-- Side photo panel -->
    <rect x="672" y="280" width="204" height="180" rx="10" fill="#1e293b"/>
    <rect x="688" y="296" width="172" height="96" rx="8" fill="url(#a)" opacity=".55"/>
    <circle cx="774" cy="344" r="28" fill="#fff" opacity=".2"/>
    <path d="M760 348 l10 10 18-22" stroke="#fff" stroke-width="4" fill="none" opacity=".75" stroke-linecap="round" stroke-linejoin="round"/>
    <rect x="688" y="408" width="120" height="10" rx="3" fill="#94a3b8" opacity=".7"/>
    <rect x="688" y="426" width="80" height="8" rx="3" fill="#64748b" opacity=".7"/>
  </g>

  <!-- Caption plate -->
  <rect x="48" y="430" width="864" height="70" fill="#000" opacity=".55"/>
  <text x="72" y="458" fill="<?= $accent2 ?>" font-family="Segoe UI, Helvetica, Arial, sans-serif" font-size="13" font-weight="700" letter-spacing="2"><?= strtoupper($a) ?></text>
  <text x="72" y="486" fill="#ffffff" font-family="Segoe UI, Helvetica, Arial, sans-serif" font-size="26" font-weight="800"><?= $t ?></text>
  <text x="880" y="486" fill="#ffffff" font-family="Segoe UI, Helvetica, Arial, sans-serif" font-size="12" opacity=".55" text-anchor="end">#<?= $i ?></text>
</svg>
<?php
exit;
