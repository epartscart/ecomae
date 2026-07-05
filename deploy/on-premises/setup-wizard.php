<?php
/**
 * ecomae ERP — Setup Wizard (CLI)
 *
 * Run after Docker services are up:
 *   docker compose exec app php /var/www/html/deploy/on-premises/setup-wizard.php
 *
 * Performs:
 *   1. Database schema import
 *   2. Admin user creation
 *   3. Company profile setup
 *   4. Industry selection + module activation
 *   5. Initial data seeding
 *   6. License verification
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

$dbHost = getenv('DB_HOST') ?: 'db';
$dbPort = getenv('DB_PORT') ?: '3306';
$dbName = getenv('DB_DATABASE') ?: 'ecomae_erp';
$dbUser = getenv('DB_USERNAME') ?: 'ecomae';
$dbPass = getenv('DB_PASSWORD') ?: '';
$appUrl = getenv('APP_URL') ?: 'https://localhost';

// Connect to database
echo "[1/6] Connecting to database...\n";
try {
    $pdo = new PDO(
        "mysql:host={$dbHost};port={$dbPort};dbname={$dbName};charset=utf8mb4",
        $dbUser,
        $dbPass,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
    echo "  OK — MySQL " . $pdo->query('SELECT VERSION()')->fetchColumn() . "\n";
} catch (PDOException $e) {
    echo "  FAILED: " . $e->getMessage() . "\n";
    exit(1);
}

// Import schema
echo "\n[2/6] Importing database schema...\n";
$schemaFile = __DIR__ . '/schema/ecomae_base_schema.sql';
if (is_file($schemaFile)) {
    $sql = file_get_contents($schemaFile);
    $pdo->exec($sql);
    echo "  OK — Schema imported\n";
} else {
    echo "  Schema file not found — creating minimal tables...\n";
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS epc_users (
            id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            username VARCHAR(100) NOT NULL UNIQUE,
            email VARCHAR(255) NOT NULL,
            password_hash VARCHAR(255) NOT NULL,
            role ENUM('admin','manager','user','readonly') DEFAULT 'user',
            first_name VARCHAR(100) DEFAULT '',
            last_name VARCHAR(100) DEFAULT '',
            is_active TINYINT(1) DEFAULT 1,
            last_login DATETIME NULL,
            created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
            updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

        CREATE TABLE IF NOT EXISTS epc_company_profile (
            id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            company_name VARCHAR(255) NOT NULL,
            trade_name VARCHAR(255) DEFAULT '',
            industry VARCHAR(100) DEFAULT '',
            country CHAR(2) DEFAULT 'AE',
            currency CHAR(3) DEFAULT 'AED',
            timezone VARCHAR(50) DEFAULT 'Asia/Dubai',
            trn VARCHAR(50) DEFAULT '',
            address TEXT,
            phone VARCHAR(50) DEFAULT '',
            email VARCHAR(255) DEFAULT '',
            logo_url VARCHAR(500) DEFAULT '',
            fiscal_year_start TINYINT DEFAULT 1,
            created_at DATETIME DEFAULT CURRENT_TIMESTAMP
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

        CREATE TABLE IF NOT EXISTS epc_site_settings (
            setting_key VARCHAR(100) PRIMARY KEY,
            setting_value TEXT,
            updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

        CREATE TABLE IF NOT EXISTS epc_modules (
            id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            module_code VARCHAR(50) NOT NULL UNIQUE,
            module_name VARCHAR(100) NOT NULL,
            is_active TINYINT(1) DEFAULT 0,
            activated_at DATETIME NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    ");
    echo "  OK — Minimal schema created\n";
}

// Admin user
echo "\n[3/6] Creating admin user...\n";
$adminUser = 'admin';
$adminPass = bin2hex(random_bytes(8));
$adminEmail = 'admin@' . parse_url($appUrl, PHP_URL_HOST);

$existing = $pdo->query("SELECT COUNT(*) FROM epc_users WHERE username = 'admin'")->fetchColumn();
if ($existing > 0) {
    echo "  Admin user already exists — skipping\n";
} else {
    $hash = password_hash($adminPass, PASSWORD_BCRYPT, ['cost' => 12]);
    $stmt = $pdo->prepare("INSERT INTO epc_users (username, email, password_hash, role, first_name) VALUES (?, ?, ?, 'admin', 'System')");
    $stmt->execute([$adminUser, $adminEmail, $hash]);
    echo "  Created: {$adminUser} / {$adminPass}\n";
    echo "  ⚠️  SAVE THIS PASSWORD — it won't be shown again!\n";
}

// Company profile
echo "\n[4/6] Setting up company profile...\n";
$companyName = getenv('COMPANY_NAME') ?: 'My Company';
$companyCountry = getenv('COMPANY_COUNTRY') ?: 'AE';
$companyCurrency = getenv('COMPANY_CURRENCY') ?: 'AED';
$companyTrn = getenv('COMPANY_TRN') ?: '';

$existing = $pdo->query("SELECT COUNT(*) FROM epc_company_profile")->fetchColumn();
if ($existing > 0) {
    echo "  Company profile already exists — skipping\n";
} else {
    $stmt = $pdo->prepare("INSERT INTO epc_company_profile (company_name, country, currency, trn) VALUES (?, ?, ?, ?)");
    $stmt->execute([$companyName, $companyCountry, $companyCurrency, $companyTrn]);
    echo "  Company: {$companyName} ({$companyCountry}, {$companyCurrency})\n";
}

// Modules
echo "\n[5/6] Activating ERP modules...\n";
$coreModules = [
    'finance' => 'Finance & GL',
    'inventory' => 'Inventory Management',
    'sales' => 'Sales & Invoicing',
    'procurement' => 'Procurement & PO',
    'hr' => 'HR & Payroll',
    'crm' => 'CRM & Contacts',
    'reporting' => 'Reports & BI',
    'compliance' => 'Compliance & Audit',
];

foreach ($coreModules as $code => $name) {
    $stmt = $pdo->prepare("INSERT IGNORE INTO epc_modules (module_code, module_name, is_active, activated_at) VALUES (?, ?, 1, NOW())");
    $stmt->execute([$code, $name]);
}
echo "  Activated " . count($coreModules) . " core modules\n";

// Site settings
echo "\n[6/6] Configuring site settings...\n";
$settings = [
    'app_url' => $appUrl,
    'timezone' => getenv('TIMEZONE') ?: 'UTC',
    'deployment_type' => 'on_premises',
    'installed_at' => date('Y-m-d H:i:s'),
    'version' => '1.0.0',
];

$stmt = $pdo->prepare("INSERT INTO epc_site_settings (setting_key, setting_value) VALUES (?, ?) ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value)");
foreach ($settings as $key => $value) {
    $stmt->execute([$key, $value]);
}
echo "  OK — {$appUrl}\n";

// License check
echo "\n";
require_once __DIR__ . '/epc_license_manager.php';
$licInfo = epc_license_info();
if ($licInfo['valid']) {
    echo "License: ACTIVE ({$licInfo['tier']}, expires: {$licInfo['expires']})\n";
} else {
    echo "License: NOT ACTIVATED — run: php activate-license.php YOUR_KEY\n";
}

echo "\n";
echo "╔══════════════════════════════════════════╗\n";
echo "║         Setup Complete!                  ║\n";
echo "╠══════════════════════════════════════════╣\n";
echo "║  URL: {$appUrl}\n";
echo "║  CP:  {$appUrl}/cp/\n";
echo "║  ERP: {$appUrl}/erp/\n";
echo "╚══════════════════════════════════════════╝\n\n";
