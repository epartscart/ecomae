<?php
/**
 * Power BI connector — catalog, config rules, API wiring (no live DB required).
 *
 *   php tests/erp_advanced/run_power_bi_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/general_pages/epc_power_bi.php';

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

section('Capabilities honesty');
$caps = epc_power_bi_capabilities();
check('version set', ($caps['version'] ?? '') === EPC_POWER_BI_VERSION);
check('web connector available', !empty($caps['available_now']['web_connector_json']) && !empty($caps['available_now']['web_connector_csv']));
check('api key auth available', !empty($caps['available_now']['api_key_auth']));
check('azure listed as needing credentials', isset($caps['needs_customer_credentials']['azure_ad_app']));
check('embed token not claimed in phase A', in_array('azure_embed_token_generation', $caps['not_in_scope_phase_a'], true));
check('connect guide has steps', count($caps['connect_guide'] ?? array()) >= 4);

section('Dataset catalog');
$catalog = epc_power_bi_dataset_catalog('https://www.ecomae.com');
$ids = array_column($catalog, 'id');
foreach (array('catalog', 'kpis', 'orders', 'sales', 'stock', 'gl', 'metrics') as $id) {
	check("dataset $id present", in_array($id, $ids, true));
}
$kpis = null;
foreach ($catalog as $ds) {
	if (($ds['id'] ?? '') === 'kpis') {
		$kpis = $ds;
		break;
	}
}
check('kpis path correct', ($kpis['path'] ?? '') === 'https://www.ecomae.com/epc-api/v1/powerbi/kpis');
check('kpis supports csv', in_array('csv', $kpis['formats'] ?? array(), true));

section('Embed URL allowlist');
check('allows app.powerbi.com view', epc_power_bi_embed_url_allowed('https://app.powerbi.com/view?r=abc'));
check('allows app.powerbi.us', epc_power_bi_embed_url_allowed('https://app.powerbi.us/reportEmbed?id=1'));
check('rejects http', !epc_power_bi_embed_url_allowed('http://app.powerbi.com/view?r=abc'));
check('rejects foreign host', !epc_power_bi_embed_url_allowed('https://evil.example/hack'));
check('rejects empty', !epc_power_bi_embed_url_allowed(''));

section('KPI / orders column contracts');
$lib = (string) file_get_contents($root . '/content/general_pages/epc_power_bi.php');
check('kpis defines metric/value headers', strpos($lib, "'site_key', 'metric', 'value'") !== false);
check('orders dataset defines columns', strpos($lib, "'site_key', 'order_id'") !== false);
check('azure phase string present', strpos($lib, 'needs_azure') !== false);

section('API wiring');
$api = (string) file_get_contents($root . '/content/general_pages/epc_api_v1.php');
check('dispatch has powerbi/catalog', strpos($api, "case 'powerbi/catalog'") !== false);
check('dispatch has powerbi/kpis', strpos($api, "case 'powerbi/kpis'") !== false);
check('dispatch has powerbi/sales', strpos($api, "case 'powerbi/sales'") !== false);
check('dispatch has powerbi/metrics', strpos($api, "case 'powerbi/metrics'") !== false);
check('auth_powerbi helper exists', strpos($api, 'function epc_api_v1_auth_powerbi') !== false);
check('health lists powerbi endpoints', strpos($api, '/epc-api/v1/powerbi/kpis') !== false);

section('CP + docs + setup');
check('CP page exists', is_file($root . '/cp/content/control/portal/epc_power_bi.php'));
check('setup script exists', is_file($root . '/epc-power-bi-setup.php'));
check('docs exist', is_file($root . '/docs/POWER_BI.md'));
$openapi = (string) file_get_contents($root . '/docs/epc-api-v1-openapi.json');
check('openapi has PowerBI tag', strpos($openapi, 'PowerBI') !== false);
check('openapi has /powerbi/kpis', strpos($openapi, '/powerbi/kpis') !== false);
$hub = (string) file_get_contents($root . '/content/general_pages/epc_integrations_helpers.php');
check('integrations hub lists power_bi', strpos($hub, "'power_bi'") !== false);
$guide = (string) file_get_contents($root . '/cp/content/control/portal/epc_api_documentation_guide.php');
check('API guide documents read:bi', strpos($guide, 'read:bi') !== false);

section('Date parser');
$_GET = array('from' => '2026-01-15', 'to' => '2026-07-01');
$from = epc_power_bi_parse_date_param('from');
$to = epc_power_bi_parse_date_param('to');
check('parses from date', gmdate('Y-m-d', $from) === '2026-01-15');
check('parses to date', gmdate('Y-m-d', $to) === '2026-07-01');

section('Wants CSV');
$_GET = array('format' => 'csv');
check('format=csv detected', epc_power_bi_wants_csv());
$_GET = array('format' => 'json');
check('format=json not csv', !epc_power_bi_wants_csv());

echo "\n";
echo $fail === 0 ? "ALL PASS ($pass)\n" : "FAILED: $fail / " . ($pass + $fail) . "\n";
exit($fail === 0 ? 0 : 1);
