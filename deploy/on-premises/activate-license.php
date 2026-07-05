<?php
/**
 * ecomae ERP — License Activation CLI
 *
 * Usage: php activate-license.php LICENSE_KEY
 *        php activate-license.php --offline /path/to/cert.txt
 *
 * Called by install.sh after Docker services are up.
 */

if (!defined('_ASTEXE_')) define('_ASTEXE_', true);

require_once __DIR__ . '/epc_license_manager.php';

if (php_sapi_name() !== 'cli') {
    echo "CLI only\n";
    exit(1);
}

$args = $argv;
array_shift($args);

if (empty($args)) {
    echo "Usage:\n";
    echo "  php activate-license.php LICENSE_KEY           # Online activation\n";
    echo "  php activate-license.php --offline cert.txt    # Import offline certificate\n";
    echo "  php activate-license.php --request             # Generate offline activation request\n";
    echo "  php activate-license.php --status              # Check license status\n";
    exit(1);
}

$mode = $args[0] ?? '';

if ($mode === '--offline' && isset($args[1])) {
    $certFile = $args[1];
    if (!is_file($certFile)) {
        echo "Error: Certificate file not found: {$certFile}\n";
        exit(1);
    }
    $certContent = trim(file_get_contents($certFile));
    $manager = new EpcLicenseManager();
    $result = $manager->importOfflineCert($certContent);

    if ($result['success']) {
        echo "License activated successfully (offline).\n";
        exit(0);
    } else {
        echo "Activation failed: " . $result['error'] . "\n";
        exit(1);
    }
}

if ($mode === '--request') {
    $manager = new EpcLicenseManager();
    $file = $manager->generateOfflineRequest();
    echo "Offline activation request generated: {$file}\n";
    echo "Upload this file to your ecomae BOS portal → On-Premises → Offline Activation\n";
    echo "Then download the activation certificate and run:\n";
    echo "  php activate-license.php --offline /path/to/downloaded-cert.txt\n";
    exit(0);
}

if ($mode === '--status') {
    $manager = new EpcLicenseManager();
    $info = $manager->validate();
    echo "License Status:\n";
    echo "  Key:      " . (getenv('LICENSE_KEY') ?: 'not set') . "\n";
    echo "  Status:   " . $info['status'] . "\n";
    echo "  Valid:    " . ($info['valid'] ? 'YES' : 'NO') . "\n";
    echo "  Tier:     " . $info['tier'] . "\n";
    echo "  Modules:  " . implode(', ', $info['modules']) . "\n";
    echo "  Max users:" . $info['users_max'] . "\n";
    echo "  Expires:  " . $info['expires'] . "\n";
    if ($info['grace_remaining'] > 0) {
        echo "  WARNING:  In grace period — {$info['grace_remaining']} days remaining!\n";
    }
    exit($info['valid'] ? 0 : 1);
}

// Online activation
$licenseKey = $mode;
if (!preg_match('/^LIC-/', $licenseKey)) {
    echo "Error: Invalid license key format. Expected: LIC-YYYY-XXXX-XXXX\n";
    exit(1);
}

putenv("LICENSE_KEY={$licenseKey}");
$manager = new EpcLicenseManager($licenseKey);
$result = $manager->activateOnline();

if ($result['success']) {
    echo "License activated successfully!\n";
    echo "  Key:  {$licenseKey}\n";
    $info = $manager->validate();
    echo "  Tier: {$info['tier']}\n";
    echo "  Modules: " . implode(', ', $info['modules']) . "\n";
    exit(0);
} else {
    echo "Online activation failed: " . ($result['error'] ?? 'Unknown error') . "\n";
    echo "\nFor air-gapped servers, use offline activation:\n";
    echo "  php activate-license.php --request\n";
    exit(1);
}
