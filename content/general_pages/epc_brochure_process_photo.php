<?php
/**
 * Unique topic-related presentation illustration for brochure processes.
 * topic=autoparts|inventory|money|orders|...&t=Title&a=Area
 */
declare(strict_types=1);

header('Content-Type: image/svg+xml; charset=utf-8');
header('Cache-Control: public, max-age=86400');
header('X-Content-Type-Options: nosniff');

$title = trim((string) ($_GET['t'] ?? 'Process'));
$area = trim((string) ($_GET['a'] ?? ''));
$topic = preg_replace('/[^a-z]/', '', strtolower((string) ($_GET['topic'] ?? 'platform'))) ?? 'platform';
$seedKey = trim((string) ($_GET['seed'] ?? ''));
if ($title === '') {
	$title = 'Process';
}
if (function_exists('mb_substr')) {
	$title = mb_substr($title, 0, 52);
	$area = mb_substr($area, 0, 40);
} else {
	$title = substr($title, 0, 52);
	$area = substr($area, 0, 40);
}

$palettes = array(
	'autoparts' => array('#1c1917', '#292524', '#f97316', '#fdba74', 'Auto parts'),
	'inventory' => array('#0f172a', '#1e293b', '#0ea5e9', '#7dd3fc', 'Inventory'),
	'money' => array('#14532d', '#166534', '#22c55e', '#bbf7d0', 'Money'),
	'orders' => array('#7c2d12', '#9a3412', '#f97316', '#fed7aa', 'Orders'),
	'logistics' => array('#164e63', '#155e75', '#06b6d4', '#a5f3fc', 'Logistics'),
	'shipping' => array('#164e63', '#155e75', '#06b6d4', '#a5f3fc', 'Shipping'),
	'customers' => array('#4c1d95', '#5b21b6', '#a78bfa', '#ddd6fe', 'Customers'),
	'ai' => array('#312e81', '#3730a3', '#818cf8', '#c7d2fe', 'AI'),
	'marketing' => array('#9f1239', '#be123c', '#fb7185', '#fecdd3', 'Marketing'),
	'documents' => array('#334155', '#475569', '#94a3b8', '#e2e8f0', 'Documents'),
	'platform' => array('#0c4a6e', '#075985', '#38bdf8', '#bae6fd', 'Platform'),
	'settings' => array('#18181b', '#27272a', '#a1a1aa', '#e4e4e7', 'Settings'),
	'erp' => array('#1e3a5f', '#1e40af', '#60a5fa', '#bfdbfe', 'ERP'),
	'procurement' => array('#3f3f46', '#52525b', '#f59e0b', '#fde68a', 'Procurement'),
	'suppliers' => array('#3f3f46', '#52525b', '#f59e0b', '#fde68a', 'Suppliers'),
	'content' => array('#134e4a', '#115e59', '#2dd4bf', '#99f6e4', 'Content'),
	'default' => array('#1e293b', '#334155', '#94a3b8', '#e2e8f0', 'Operations'),
	'operations' => array('#1e293b', '#334155', '#94a3b8', '#e2e8f0', 'Operations'),
);
if ($topic === 'shipping') {
	$topic = 'logistics';
}
if ($topic === 'suppliers') {
	$topic = 'procurement';
}
if ($topic === 'operations') {
	$topic = 'default';
}
$p = $palettes[$topic] ?? $palettes['platform'];
$bg = $p[0];
$panel = $p[1];
$accent = $p[2];
$accent2 = $p[3];
$topicLabel = $p[4];
$hash = abs(crc32(($seedKey !== '' ? $seedKey : $title) . '|' . $topic . '|' . $area));
$seed = $hash % 97;

function epc_br_esc(string $s): string
{
	return htmlspecialchars($s, ENT_QUOTES | ENT_XML1, 'UTF-8');
}

$t = epc_br_esc($title);
$tl = epc_br_esc($topicLabel);
$a = epc_br_esc($area !== '' ? $area : $topicLabel);

// Topic-specific foreground shapes (related visuals, not random).
ob_start();
switch ($topic) {
	case 'money':
		// Banknotes + coins
		echo '<rect x="120" y="150" width="280" height="140" rx="10" fill="' . $accent . '" opacity=".9" transform="rotate(-8 260 220)"/>';
		echo '<rect x="150" y="170" width="220" height="20" rx="4" fill="#fff" opacity=".35"/>';
		echo '<circle cx="200" cy="230" r="28" fill="#fff" opacity=".25"/>';
		echo '<rect x="420" y="180" width="280" height="140" rx="10" fill="' . $accent2 . '" opacity=".85" transform="rotate(6 560 250)"/>';
		echo '<rect x="450" y="200" width="220" height="18" rx="4" fill="#14532d" opacity=".35"/>';
		echo '<circle cx="680" cy="340" r="36" fill="' . $accent . '" opacity=".8"/>';
		echo '<circle cx="740" cy="360" r="28" fill="' . $accent2 . '" opacity=".75"/>';
		echo '<circle cx="620" cy="370" r="22" fill="#fff" opacity=".3"/>';
		break;
	case 'autoparts':
		// Gear + wrench + car silhouette
		echo '<circle cx="280" cy="250" r="70" fill="none" stroke="' . $accent . '" stroke-width="18"/>';
		echo '<circle cx="280" cy="250" r="28" fill="' . $accent2 . '"/>';
		for ($i = 0; $i < 8; $i++) {
			$ang = ($i * 45 + $seed) * M_PI / 180;
			$x = 280 + cos($ang) * 78;
			$y = 250 + sin($ang) * 78;
			echo '<rect x="' . ($x - 10) . '" y="' . ($y - 16) . '" width="20" height="32" rx="4" fill="' . $accent . '" transform="rotate(' . ($i * 45) . ' ' . $x . ' ' . $y . ')"/>';
		}
		echo '<rect x="520" y="200" width="220" height="40" rx="8" fill="' . $accent2 . '" transform="rotate(25 630 220)"/>';
		echo '<rect x="680" y="160" width="36" height="120" rx="8" fill="' . $accent . '"/>';
		echo '<ellipse cx="650" cy="360" rx="120" ry="36" fill="#fff" opacity=".12"/>';
		echo '<path d="M540 340 h180 l40 30 h60 v20 h-300 z" fill="' . $accent2 . '" opacity=".55"/>';
		break;
	case 'inventory':
		// Warehouse shelves + boxes
		for ($r = 0; $r < 3; $r++) {
			$y = 150 + $r * 90;
			echo '<rect x="140" y="' . $y . '" width="680" height="12" fill="' . $accent2 . '" opacity=".5"/>';
			for ($c = 0; $c < 5; $c++) {
				$x = 170 + $c * 120 + (($seed + $r + $c) % 3) * 4;
				$h = 40 + (($seed + $c * 7) % 28);
				echo '<rect x="' . $x . '" y="' . ($y - $h) . '" width="70" height="' . $h . '" rx="4" fill="' . (($c + $r) % 2 ? $accent : $accent2) . '" opacity=".85"/>';
			}
		}
		break;
	case 'orders':
		// Parcels stack
		echo '<rect x="220" y="280" width="160" height="120" rx="8" fill="' . $accent . '"/>';
		echo '<rect x="220" y="280" width="160" height="24" fill="#fff" opacity=".2"/>';
		echo '<path d="M220 304 L300 250 L380 304" fill="' . $accent2 . '"/>';
		echo '<rect x="420" y="240" width="150" height="160" rx="8" fill="' . $accent2 . '" opacity=".9"/>';
		echo '<rect x="420" y="240" width="150" height="28" fill="#fff" opacity=".2"/>';
		echo '<rect x="600" y="260" width="140" height="140" rx="8" fill="' . $accent . '" opacity=".8"/>';
		echo '<line x1="600" y1="330" x2="740" y2="330" stroke="#fff" stroke-width="3" opacity=".35"/>';
		echo '<line x1="670" y1="260" x2="670" y2="400" stroke="#fff" stroke-width="3" opacity=".35"/>';
		break;
	case 'shipping':
	case 'logistics':
		// Truck
		echo '<rect x="180" y="250" width="280" height="110" rx="12" fill="' . $accent . '"/>';
		echo '<rect x="460" y="280" width="160" height="80" rx="10" fill="' . $accent2 . '"/>';
		echo '<polygon points="620,280 700,280 740,330 740,360 620,360" fill="' . $accent . '"/>';
		echo '<rect x="640" y="295" width="50" height="30" rx="4" fill="#fff" opacity=".35"/>';
		echo '<circle cx="260" cy="370" r="28" fill="#0f172a"/><circle cx="260" cy="370" r="14" fill="#94a3b8"/>';
		echo '<circle cx="520" cy="370" r="28" fill="#0f172a"/><circle cx="520" cy="370" r="14" fill="#94a3b8"/>';
		echo '<circle cx="680" cy="370" r="28" fill="#0f172a"/><circle cx="680" cy="370" r="14" fill="#94a3b8"/>';
		break;
	case 'customers':
		// People circles
		$xs = array(280, 420, 560, 700);
		foreach ($xs as $i => $x) {
			$y = 220 + (($seed + $i * 11) % 40);
			echo '<circle cx="' . $x . '" cy="' . $y . '" r="36" fill="' . ($i % 2 ? $accent : $accent2) . '" opacity=".9"/>';
			echo '<circle cx="' . $x . '" cy="' . ($y + 70) . '" r="48" fill="' . ($i % 2 ? $accent2 : $accent) . '" opacity=".45"/>';
		}
		break;
	case 'ai':
		// Neural nodes
		for ($i = 0; $i < 12; $i++) {
			$x = 180 + ($i % 4) * 160;
			$y = 170 + intdiv($i, 4) * 90;
			echo '<circle cx="' . $x . '" cy="' . $y . '" r="16" fill="' . $accent . '"/>';
			if ($i % 4 < 3) {
				echo '<line x1="' . $x . '" y1="' . $y . '" x2="' . ($x + 160) . '" y2="' . $y . '" stroke="' . $accent2 . '" stroke-width="3" opacity=".5"/>';
			}
			if ($i < 8) {
				echo '<line x1="' . $x . '" y1="' . $y . '" x2="' . $x . '" y2="' . ($y + 90) . '" stroke="' . $accent2 . '" stroke-width="3" opacity=".35"/>';
			}
		}
		break;
	case 'documents':
		for ($i = 0; $i < 4; $i++) {
			$x = 220 + $i * 130;
			$y = 160 + ($i % 2) * 20;
			echo '<rect x="' . $x . '" y="' . $y . '" width="110" height="150" rx="6" fill="#fff" opacity="' . (0.75 - $i * 0.08) . '"/>';
			echo '<rect x="' . ($x + 14) . '" y="' . ($y + 24) . '" width="80" height="8" rx="2" fill="' . $accent . '" opacity=".7"/>';
			echo '<rect x="' . ($x + 14) . '" y="' . ($y + 44) . '" width="70" height="6" rx="2" fill="' . $panel . '" opacity=".5"/>';
			echo '<rect x="' . ($x + 14) . '" y="' . ($y + 60) . '" width="75" height="6" rx="2" fill="' . $panel . '" opacity=".4"/>';
		}
		break;
	case 'marketing':
		echo '<rect x="200" y="180" width="360" height="200" rx="16" fill="' . $accent . '" opacity=".85"/>';
		echo '<circle cx="280" cy="250" r="40" fill="#fff" opacity=".25"/>';
		echo '<rect x="340" y="220" width="180" height="14" rx="4" fill="#fff" opacity=".5"/>';
		echo '<rect x="340" y="250" width="140" height="10" rx="3" fill="#fff" opacity=".35"/>';
		echo '<rect x="600" y="200" width="160" height="160" rx="20" fill="' . $accent2 . '" opacity=".8"/>';
		echo '<circle cx="680" cy="260" r="28" fill="#fff" opacity=".35"/>';
		echo '<rect x="640" y="310" width="80" height="10" rx="3" fill="#fff" opacity=".45"/>';
		break;
	case 'suppliers':
	case 'procurement':
		echo '<rect x="200" y="200" width="200" height="160" rx="10" fill="' . $accent . '"/>';
		echo '<rect x="230" y="230" width="140" height="12" rx="3" fill="#fff" opacity=".4"/>';
		echo '<rect x="230" y="260" width="120" height="10" rx="3" fill="#fff" opacity=".3"/>';
		echo '<rect x="480" y="180" width="240" height="200" rx="12" fill="' . $accent2 . '" opacity=".85"/>';
		echo '<path d="M520 240 h160 M520 280 h140 M520 320 h150" stroke="#fff" stroke-width="8" opacity=".35" stroke-linecap="round"/>';
		break;
	default:
		// Generic but topic-colored workspace
		echo '<rect x="160" y="160" width="640" height="240" rx="16" fill="' . $panel . '" opacity=".9"/>';
		echo '<rect x="190" y="190" width="180" height="180" rx="12" fill="' . $accent . '" opacity=".45"/>';
		echo '<rect x="400" y="190" width="360" height="40" rx="8" fill="' . $accent2 . '" opacity=".55"/>';
		echo '<rect x="400" y="250" width="280" height="20" rx="6" fill="#fff" opacity=".2"/>';
		echo '<rect x="400" y="290" width="320" height="20" rx="6" fill="#fff" opacity=".15"/>';
		echo '<rect x="400" y="330" width="200" height="20" rx="6" fill="#fff" opacity=".12"/>';
}
$scene = ob_get_clean();

echo '<?xml version="1.0" encoding="UTF-8"?>';
?>
<svg xmlns="http://www.w3.org/2000/svg" width="960" height="540" viewBox="0 0 960 540" role="img" aria-label="<?= $t ?>">
  <defs>
    <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="<?= $bg ?>"/>
      <stop offset="100%" stop-color="<?= $panel ?>"/>
    </linearGradient>
  </defs>
  <rect width="960" height="540" fill="url(#g)"/>
  <circle cx="<?= 100 + ($seed % 60) ?>" cy="<?= 80 + ($seed % 40) ?>" r="180" fill="<?= $accent ?>" opacity=".12"/>
  <circle cx="<?= 820 - ($seed % 50) ?>" cy="420" r="200" fill="<?= $accent2 ?>" opacity=".1"/>
  <?= $scene ?>
  <rect x="0" y="400" width="960" height="140" fill="#000" opacity=".55"/>
  <text x="40" y="440" fill="<?= $accent2 ?>" font-family="Segoe UI, Helvetica, Arial, sans-serif" font-size="14" font-weight="700" letter-spacing="2"><?= strtoupper($tl) ?></text>
  <text x="40" y="478" fill="#ffffff" font-family="Segoe UI, Helvetica, Arial, sans-serif" font-size="28" font-weight="800"><?= $t ?></text>
  <text x="40" y="508" fill="#ffffff" font-family="Segoe UI, Helvetica, Arial, sans-serif" font-size="13" opacity=".65"><?= $a ?> · #<?= (string) ($hash % 10000) ?></text>
</svg>
<?php
exit;
