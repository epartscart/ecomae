<?php
/**
 * Create/use dummy commerce Excel+CSV files and run S/P/L ingest pipeline (no live DB).
 *
 *   php tests/erp_advanced/run_commerce_dummy_file_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

$root = dirname(__DIR__, 2);
define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/shop/docpart/epc_commerce_price_ingest.php';

$pass = 0;
$fail = 0;
function check(string $label, bool $cond, string $detail = ''): void
{
	global $pass, $fail;
	if ($cond) {
		$pass++;
		echo "  PASS  $label" . ($detail !== '' ? " — $detail" : '') . "\n";
	} else {
		$fail++;
		echo "  FAIL  $label" . ($detail !== '' ? " — $detail" : '') . "\n";
	}
}

$fixtureDir = $root . '/tests/erp_advanced/fixtures/commerce';
$gen = $fixtureDir . '/generate_dummy_files.py';
echo "== Generate dummy Excel/CSV fixtures ==\n";
check('generator exists', is_file($gen));
$cmd = 'python3 ' . escapeshellarg($gen);
exec($cmd . ' 2>&1', $out, $code);
check('generator exit 0', $code === 0, implode(' | ', $out));

$files = array(
	'sales_dummy.csv',
	'purchase_dummy.csv',
	'inventory_dummy.csv',
	'sales_dummy.xlsx',
	'purchase_dummy.xlsx',
	'inventory_dummy.xlsx',
);
foreach ($files as $f) {
	$path = $fixtureDir . '/' . $f;
	check('fixture ' . $f, is_file($path) && filesize($path) > 20, is_file($path) ? (filesize($path) . ' bytes') : 'missing');
}

$outDir = sys_get_temp_dir() . '/epc_commerce_dummy_' . getmypid();
@mkdir($outDir, 0755, true);

/**
 * @return array<string,mixed>
 */
function run_pipeline(string $path, string $role, string $base, float $margin, string $outDir): array
{
	$converted = epc_commerce_excel_to_csv($path);
	if (empty($converted['ok'])) {
		return array('ok' => false, 'step' => 'excel_to_csv', 'message' => (string) $converted['message']);
	}
	$csvPath = (string) $converted['path'];
	$tmp = ($csvPath !== $path);
	$read = epc_commerce_read_source_rows($csvPath, $role);
	if (empty($read['ok'])) {
		if ($tmp) {
			@unlink($csvPath);
		}
		return array('ok' => false, 'step' => 'read', 'message' => (string) $read['message']);
	}
	$agg = epc_commerce_aggregate_rows($role, $read['rows'], $base, $margin);
	$written = array();
	foreach ($agg as $listName => $lines) {
		$outCsv = $outDir . '/' . preg_replace('/[^A-Za-z0-9_\-\.]+/', '_', $listName) . '.csv';
		epc_commerce_write_docpart_csv($outCsv, $lines);
		$written[$listName] = array(
			'path' => $outCsv,
			'rows' => $lines,
			'count' => count($lines),
		);
	}
	if ($tmp) {
		@unlink($csvPath);
	}
	return array(
		'ok' => count($written) > 0,
		'step' => 'done',
		'message' => (string) $converted['message'],
		'source_rows' => count($read['rows']),
		'lists' => $written,
	);
}

function price_of(array $lists, string $list, string $article): float
{
	if (!isset($lists[$list]['rows'])) {
		return -1.0;
	}
	foreach ($lists[$list]['rows'] as $row) {
		if ((string) $row['article'] === $article) {
			return (float) $row['price'];
		}
	}
	return -1.0;
}

function qty_of(array $lists, string $list, string $article): int
{
	if (!isset($lists[$list]['rows'])) {
		return -1;
	}
	foreach ($lists[$list]['rows'] as $row) {
		if ((string) $row['article'] === $article) {
			return (int) $row['exist'];
		}
	}
	return -1;
}

echo "\n== Sales CSV → EPC-S (highest price) ==\n";
$sales = run_pipeline($fixtureDir . '/sales_dummy.csv', 'sales', 'EPC', 0, $outDir);
check('sales pipeline ok', !empty($sales['ok']), (string) ($sales['message'] ?? $sales['step'] ?? ''));
check('sales source rows', (int) ($sales['source_rows'] ?? 0) === 6, (string) ($sales['source_rows'] ?? 0));
check('sales list EPC-S', isset($sales['lists']['EPC-S']), implode(',', array_keys($sales['lists'] ?? array())));
check('OC47 highest=18.90', abs(price_of($sales['lists'] ?? array(), 'EPC-S', 'OC47') - 18.90) < 0.001, (string) price_of($sales['lists'] ?? array(), 'EPC-S', 'OC47'));
check('OC47 qty summed=5', qty_of($sales['lists'] ?? array(), 'EPC-S', 'OC47') === 5, (string) qty_of($sales['lists'] ?? array(), 'EPC-S', 'OC47'));
check('MANN highest=22.00', abs(price_of($sales['lists'] ?? array(), 'EPC-S', 'W71945') - 22.00) < 0.001);

echo "\n== Purchase CSV → *.P (margin 20%, multi-supplier) ==\n";
$purch = run_pipeline($fixtureDir . '/purchase_dummy.csv', 'purchase', 'EPC', 20.0, $outDir);
check('purchase pipeline ok', !empty($purch['ok']));
$pLists = array_keys($purch['lists'] ?? array());
sort($pLists);
check('three supplier lists', count($pLists) === 3, implode(',', $pLists));
check('ACME.P present', isset($purch['lists']['ACME-Parts.P']) || isset($purch['lists']['ACME.P']));
// list name uses sanitized supplier: "ACME Parts" → "ACME-Parts.P"
$acmeKey = isset($purch['lists']['ACME-Parts.P']) ? 'ACME-Parts.P' : 'ACME.P';
$betaKey = isset($purch['lists']['BETA-Supply.P']) ? 'BETA-Supply.P' : 'BETA.P';
check('ACME OC47 lowest cost→9.60', abs(price_of($purch['lists'] ?? array(), $acmeKey, 'OC47') - 9.60) < 0.001, (string) price_of($purch['lists'] ?? array(), $acmeKey, 'OC47'));
check('BETA OC47 cost 7.50→9.00', abs(price_of($purch['lists'] ?? array(), $betaKey, 'OC47') - 9.00) < 0.001, (string) price_of($purch['lists'] ?? array(), $betaKey, 'OC47'));

echo "\n== Inventory CSV → EPC-L (margin 25%) ==\n";
$inv = run_pipeline($fixtureDir . '/inventory_dummy.csv', 'inventory', 'EPC', 25.0, $outDir);
check('inventory pipeline ok', !empty($inv['ok']));
check('inventory list EPC-L', isset($inv['lists']['EPC-L']));
check('OC47 qty=20', qty_of($inv['lists'] ?? array(), 'EPC-L', 'OC47') === 20, (string) qty_of($inv['lists'] ?? array(), 'EPC-L', 'OC47'));
check('OC47 price 9*1.25=11.25', abs(price_of($inv['lists'] ?? array(), 'EPC-L', 'OC47') - 11.25) < 0.001, (string) price_of($inv['lists'] ?? array(), 'EPC-L', 'OC47'));
check('VALEO present', abs(price_of($inv['lists'] ?? array(), 'EPC-L', 'VF123') - 18.125) < 0.01 || abs(price_of($inv['lists'] ?? array(), 'EPC-L', 'VF123') - 18.13) < 0.01, (string) price_of($inv['lists'] ?? array(), 'EPC-L', 'VF123'));

echo "\n== Sales XLSX → same rules ==\n";
$salesX = run_pipeline($fixtureDir . '/sales_dummy.xlsx', 'sales', 'EPC', 0, $outDir);
check('xlsx convert ok', !empty($salesX['ok']), (string) ($salesX['message'] ?? ''));
check('xlsx source rows', (int) ($salesX['source_rows'] ?? 0) === 6, (string) ($salesX['source_rows'] ?? 0));
check('xlsx OC47=18.90', abs(price_of($salesX['lists'] ?? array(), 'EPC-S', 'OC47') - 18.90) < 0.001, (string) price_of($salesX['lists'] ?? array(), 'EPC-S', 'OC47'));

echo "\n== Purchase XLSX ==\n";
$purchX = run_pipeline($fixtureDir . '/purchase_dummy.xlsx', 'purchase', 'EPC', 20.0, $outDir);
check('purchase xlsx ok', !empty($purchX['ok']), (string) ($purchX['message'] ?? ''));
check('purchase xlsx lists>=2', count($purchX['lists'] ?? array()) >= 2, (string) count($purchX['lists'] ?? array()));

echo "\n== Inventory XLSX ==\n";
$invX = run_pipeline($fixtureDir . '/inventory_dummy.xlsx', 'inventory', 'EPC', 25.0, $outDir);
check('inventory xlsx ok', !empty($invX['ok']), (string) ($invX['message'] ?? ''));
check('inventory xlsx OC47 qty', qty_of($invX['lists'] ?? array(), 'EPC-L', 'OC47') === 20);

echo "\n== Docpart CSV samples written ==\n";
$samples = glob($outDir . '/*.csv') ?: array();
check('output CSVs created', count($samples) >= 3, count($samples) . ' files in ' . $outDir);
foreach (array_slice($samples, 0, 6) as $sample) {
	$head = file($sample, FILE_IGNORE_NEW_LINES);
	echo '  - ' . basename($sample) . ' (' . max(0, count($head) - 1) . " rows)\n";
	if (is_array($head) && count($head) > 0) {
		echo '      ' . $head[0] . "\n";
		if (isset($head[1])) {
			echo '      ' . $head[1] . "\n";
		}
	}
}

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
echo "Fixtures: $fixtureDir\n";
echo "Output:   $outDir\n";
exit($fail > 0 ? 1 : 0);
