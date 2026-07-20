<?php
/**
 * ecomae ERP — Setup Wizard (CLI)
 *
 * Run after Docker services are up AND the license has been activated
 * (activation installs core/dp_*.php + config.php — nothing below this point
 * works without them):
 *
 *   docker compose exec app php deploy/on-premises/activate-license.php YOUR_KEY
 *   docker compose exec app php deploy/on-premises/setup-wizard.php
 *
 * Performs:
 *   1. Verifies the core engine + config.php are installed
 *   2. Runs the platform's real, idempotent schema migrations
 *      (epc-post-deploy-setup-all.php — the same script every ecomae.com
 *      deploy runs; it creates the ERP/GL/CRM/etc. tables this app actually
 *      reads and writes, not a placeholder schema)
 *   3. Confirms license status
 *   4. Points you to /cp/ to finish company + admin setup through the
 *      product's own onboarding, so the first admin account and company
 *      profile are created by the same code path every tenant uses —
 *      not by this script guessing at table names.
 */

if (!defined('_ASTEXE_')) define('_ASTEXE_', true);

if (php_sapi_name() !== 'cli') {
	echo "CLI only — run: docker compose exec app php /var/www/html/deploy/on-premises/setup-wizard.php\n";
	exit(1);
}

set_time_limit(0);

echo "\n";
echo "╔══════════════════════════════════════════╗\n";
echo "║    ecomae ERP — Initial Setup Wizard     ║\n";
echo "╚══════════════════════════════════════════╝\n\n";

$docRoot = $_SERVER['DOCUMENT_ROOT'] ?? '/var/www/html';
$_SERVER['DOCUMENT_ROOT'] = $docRoot;
$appUrl = getenv('APP_URL') ?: 'https://localhost';

// Step 1: core engine present?
echo "[1/4] Checking core engine files...\n";
$coreFiles = array('/config.php', '/core/dp_core.php', '/core/dp_content.php', '/core/dp_module.php', '/core/dp_template.php');
$missing = array();
foreach ($coreFiles as $f) {
	if (!is_file($docRoot . $f)) {
		$missing[] = $f;
	}
}
if (!empty($missing)) {
	echo "  MISSING: " . implode(', ', $missing) . "\n";
	echo "  These are installed by license activation, not by this wizard.\n";
	echo "  Run: docker compose exec app php deploy/on-premises/activate-license.php YOUR_KEY\n";
	exit(1);
}
echo "  OK — core engine present\n";

// Step 2: connect + run real schema migrations
echo "\n[2/4] Running platform schema migrations...\n";
$setupAllScript = $docRoot . '/epc-post-deploy-setup-all.php';
if (!is_file($setupAllScript)) {
	echo "  FAILED: {$setupAllScript} not found — was the app code cloned correctly?\n";
	exit(1);
}
$exitCode = 0;
passthru('php ' . escapeshellarg($setupAllScript), $exitCode);
if ($exitCode !== 0) {
	echo "\n  Some migration steps failed — see output above. Fix and re-run this wizard; it is safe to run repeatedly.\n";
}

// Step 3: license status
echo "\n[3/4] Checking license...\n";
require_once __DIR__ . '/epc_license_manager.php';
$licInfo = epc_license_info();
if ($licInfo['valid']) {
	echo "  License: ACTIVE ({$licInfo['tier']}, expires: {$licInfo['expires']})\n";
} else {
	echo "  License: NOT ACTIVE (status: {$licInfo['status']})\n";
	echo "  Run: docker compose exec app php deploy/on-premises/activate-license.php YOUR_KEY\n";
}

// Step 4: hand off to the product's own onboarding
echo "\n[4/4] Finish setup in the browser...\n";
echo "  Open {$appUrl}/cp/ and complete company + first-admin onboarding there.\n";
echo "  Using the product's own onboarding (instead of this script inserting\n";
echo "  rows directly) keeps the company profile and admin account consistent\n";
echo "  with every other ecomae tenant — including validation, password rules,\n";
echo "  and the ERP module activation flow.\n";

echo "\n";
echo "╔══════════════════════════════════════════╗\n";
echo "║         Setup Complete!                  ║\n";
echo "╠══════════════════════════════════════════╣\n";
echo "║  URL: {$appUrl}\n";
echo "║  CP:  {$appUrl}/cp/\n";
echo "║  ERP: {$appUrl}/erp/\n";
echo "╚══════════════════════════════════════════╝\n\n";
