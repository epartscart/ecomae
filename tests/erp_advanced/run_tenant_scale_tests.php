<?php
/**
 * Smoke tests for 1000+ tenant scale foundations (no live MySQL required).
 *
 *   php tests/erp_advanced/run_tenant_scale_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);

$root = dirname(__DIR__, 2);
require_once $root . '/content/general_pages/epc_tenant_pdo.php';
require_once $root . '/content/general_pages/epc_platform_jobs.php';

$pass_count = 0;
$fail_count = 0;

function check(string $label, bool $cond): void
{
    global $pass_count, $fail_count;
    if ($cond) {
        $pass_count++;
        echo "  PASS  $label\n";
    } else {
        $fail_count++;
        echo "  FAIL  $label\n";
    }
}

function section(string $t): void
{
    echo "\n== $t ==\n";
}

section('PDO helper API');
check('epc_tenant_pdo exists', function_exists('epc_tenant_pdo'));
check('epc_tenant_pdo_from_row exists', function_exists('epc_tenant_pdo_from_row'));
check('epc_tenant_row_uses_dedicated_db exists', function_exists('epc_tenant_row_uses_dedicated_db'));
check('pool max defined', defined('EPC_TENANT_PDO_POOL_MAX') && (int)EPC_TENANT_PDO_POOL_MAX >= 8);

[$pdoMissing, $errMissing] = epc_tenant_pdo('', 'x', 'y', 'z');
check('rejects empty host', $pdoMissing === null && $errMissing !== '');

[$pdoBad, $errBad] = epc_tenant_pdo('127.0.0.1', 'no_such_db_scale_test', 'no_user', 'bad', ['timeout' => 1]);
check('failed connect returns error string', $pdoBad === null && is_string($errBad) && $errBad !== '');

section('Dedicated-db detection');
check('flag dedicated_db=1', epc_tenant_row_uses_dedicated_db(['dedicated_db' => 1, 'db_name' => 'docpart']));
check('scale_policy dedicated_mysql', epc_tenant_row_uses_dedicated_db(['scale_policy' => 'dedicated_mysql']));
check('erp_only_shared', epc_tenant_row_uses_dedicated_db(['erp_only_shared' => 1, 'db_name' => 'acme']));
check('non-docpart db_name', epc_tenant_row_uses_dedicated_db(['db_name' => 'tenant_acme']));
check('shared docpart is not dedicated', !epc_tenant_row_uses_dedicated_db(['db_name' => 'docpart', 'dedicated_db' => 0]));

section('from_row credential aliases');
[$pdoRow, $errRow] = epc_tenant_pdo_from_row([
    'db_host' => '127.0.0.1',
    'db_name' => 'no_such_db_scale_test',
    'db_user' => 'u',
    'db_password' => 'p',
], ['timeout' => 1]);
check('accepts db_password alias', $pdoRow === null && $errRow !== '');

[$pdoRow2, $errRow2] = epc_tenant_pdo_from_row([
    'db_host' => '127.0.0.1',
    'db' => 'no_such_db_scale_test',
    'user' => 'u',
    'password' => 'p',
], ['timeout' => 1]);
check('accepts profile-style aliases', $pdoRow2 === null && $errRow2 !== '');

section('Platform jobs API');
check('enqueue exists', function_exists('epc_platform_jobs_enqueue'));
check('claim exists', function_exists('epc_platform_jobs_claim'));
check('dispatch exists', function_exists('epc_platform_jobs_dispatch'));
check('run_batch exists', function_exists('epc_platform_jobs_run_batch'));

$noop = epc_platform_jobs_dispatch([
    'job_type' => 'noop',
    'tenant_key' => '',
    'payload_json' => json_encode(['ping' => 1]),
]);
check('noop handler ok', !empty($noop['ok']) && ($noop['result']['echo']['ping'] ?? null) === 1);

$unknown = epc_platform_jobs_dispatch(['job_type' => 'not_a_real_job_type_xyz']);
check('unknown job fails cleanly', empty($unknown['ok']) && !empty($unknown['error']));

$handlerHit = false;
epc_platform_jobs_register_handler('scale_test_custom', static function ($tenantKey, $payload) use (&$handlerHit) {
    $handlerHit = ($tenantKey === 'acme' && ($payload['x'] ?? 0) === 2);
    return ['ok' => true, 'result' => ['custom' => true]];
});
$custom = epc_platform_jobs_dispatch([
    'job_type' => 'scale_test_custom',
    'tenant_key' => 'acme',
    'payload_json' => json_encode(['x' => 2]),
]);
check('custom handler registered', $handlerHit && !empty($custom['ok']));

section('Cron entrypoint');
$cron = $root . '/epc-platform-jobs-cron.php';
check('cron file exists', is_file($cron));
$cronSrc = (string)file_get_contents($cron);
check('cron requires jobs library', strpos($cronSrc, 'epc_platform_jobs.php') !== false);

section('Source wiring');
$tenantPhp = (string)file_get_contents($root . '/content/general_pages/epc_portal_tenant.php');
check('resolve mentions dedicated_db', strpos($tenantPhp, 'dedicated_db') !== false);
check('save_tenant writes scale_policy', strpos($tenantPhp, 'scale_policy') !== false);

$dbPhp = (string)file_get_contents($root . '/content/general_pages/epc_portal_db.php');
check('schema adds dedicated_db column', strpos($dbPhp, "'dedicated_db'") !== false);
check('schema adds scale_policy column', strpos($dbPhp, "'scale_policy'") !== false);

$onboardUi = (string)file_get_contents($root . '/content/shop/tenant_hub/epc_tenant_onboard_panel.php');
check('onboard UI has scale_policy select', strpos($onboardUi, 'name="scale_policy"') !== false);
check('onboard UI defaults dedicated', strpos($onboardUi, 'dedicated_mysql') !== false);

$docs = $root . '/docs/TENANT_SCALE_1000.md';
check('scale docs exist', is_file($docs));

echo "\n----------------------------\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
exit($fail_count > 0 ? 1 : 0);
