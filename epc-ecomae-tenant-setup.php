<?php
/**
 * epc-ecomae-tenant-setup.php
 * Register ecomae.com as its own tenant with dedicated ERP + CP database.
 * Uses erp_only_shared architecture (like ASAP-C, Spare247) since www.ecomae.com
 * is the platform host — commerce storefront is the existing marketing site.
 *
 * Run once on the server:
 *   php epc-ecomae-tenant-setup.php
 * Or via browser with token:
 *   https://www.ecomae.com/epc-ecomae-tenant-setup.php?token=epartscart-deploy-2026
 */

// Token gate
$token = 'epartscart-deploy-2026';
$isCli = (php_sapi_name() === 'cli');
if (!$isCli) {
    $reqToken = $_GET['token'] ?? $_POST['token'] ?? '';
    if ($reqToken !== $token) {
        http_response_code(403);
        echo 'Forbidden';
        exit;
    }
    header('Content-Type: text/plain; charset=utf-8');
}

$_SERVER['DOCUMENT_ROOT'] = $isCli
    ? dirname(__FILE__)
    : ($_SERVER['DOCUMENT_ROOT'] ?? dirname(__FILE__));

// Define the access constant required by platform includes
if (!defined('_ASTEXE_')) {
    define('_ASTEXE_', 1);
}

echo "=== ecomae.com tenant self-registration ===\n\n";

// Load platform PDO — need the full dependency chain
try {
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_data.php';
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
} catch (Throwable $e) {
    echo "FAIL: could not load platform libraries: " . $e->getMessage() . "\n";
    exit(1);
}

$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
    echo "FAIL: cannot connect to platform database\n";
    exit(1);
}
echo "OK: platform DB connected\n";

// Ensure schema
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
epc_portal_db_ensure($platformPdo);
echo "OK: portal schema ensured\n";

// Check if ecomae_corp tenant already exists
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
$existing = $platformPdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
$existing->execute(array('ecomae_corp'));
$existingRow = $existing->fetch(PDO::FETCH_ASSOC);

if ($existingRow) {
    echo "INFO: tenant 'ecomae_corp' already exists (status: " . ($existingRow['status'] ?? '?') . ")\n";
    echo "  DB: " . ($existingRow['db_name'] ?? '?') . "\n";
    echo "  Trade name: " . ($existingRow['trade_name'] ?? '?') . "\n";
    echo "\nIf you need to re-provision, delete the row first.\n";

    // Still ensure the ERP tables are set up
    echo "\n--- Ensuring ERP tables in existing DB ---\n";
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
    $tenantPdo = epc_portal_tenant_control_tenant_pdo($existingRow);
    if ($tenantPdo instanceof PDO) {
        ecomae_ensure_erp_tables($tenantPdo);
        echo "OK: ERP tables verified\n";
    } else {
        echo "WARN: could not connect to tenant DB to verify tables\n";
    }
    echo "\nDONE (existing tenant)\n";
    exit(0);
}

// Database credentials for ecomae_corp
// The DB + user must be pre-created on MySQL before running this script:
//   sudo mysql -e "CREATE DATABASE IF NOT EXISTS \`ecomae_corp\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
//   sudo mysql -e "CREATE USER IF NOT EXISTS 'ecomae_corp'@'localhost' IDENTIFIED BY 'EcomAE2026SecurE!';"
//   sudo mysql -e "GRANT ALL PRIVILEGES ON \`ecomae_corp\`.* TO 'ecomae_corp'@'localhost'; FLUSH PRIVILEGES;"
$dbName = 'ecomae_corp';
$dbUser = 'ecomae_corp';
$dbPass = 'EcomAE2026SecurE!';

echo "Connecting to database: $dbName (user: $dbUser)\n";

// Verify the database is accessible
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
try {
    $testPdo = new PDO(
        'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8mb4',
        $dbUser,
        $dbPass,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
    );
    echo "OK: database connection verified\n";
} catch (Exception $e) {
    echo "FAIL: cannot connect to DB '$dbName': " . $e->getMessage() . "\n";
    echo "\nPlease create the database and user first:\n";
    echo "  sudo mysql -e \"CREATE DATABASE IF NOT EXISTS \`$dbName\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;\"\n";
    echo "  sudo mysql -e \"CREATE USER IF NOT EXISTS '$dbUser'@'localhost' IDENTIFIED BY '$dbPass';\"\n";
    echo "  sudo mysql -e \"GRANT ALL PRIVILEGES ON \`$dbName\`.* TO '$dbUser'@'localhost'; FLUSH PRIVILEGES;\"\n";
    exit(1);
}

// Register the tenant
$saveData = array(
    'site_key'        => 'ecomae_corp',
    'hostname'        => 'www.ecomae.com',
    'industry_code'   => 'platform_host',
    'status'          => 'live',
    'trade_name'      => 'ecomae',
    'hub_name'        => 'ecomae Platform',
    'from_email'      => 'ecomaedxb@gmail.com',
    'db_name'         => $dbName,
    'db_user'         => $dbUser,
    'db_password'     => $dbPass,
    'notes'           => 'ecomae.com own company tenant — platform host with dedicated ERP + CP',
    'hosted_on'       => 'platform',
    'erp_only_shared' => 1,
    'country_code'    => 'AE',
);

$result = epc_portal_save_tenant($platformPdo, $saveData);
if (empty($result['ok'])) {
    echo "FAIL: tenant save returned: " . json_encode($result) . "\n";
    exit(1);
}
echo "OK: tenant 'ecomae_corp' registered in platform\n";

// Connect to the new tenant DB and set up base tables
$newRow = $platformPdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
$newRow->execute(array('ecomae_corp'));
$row = $newRow->fetch(PDO::FETCH_ASSOC);

if ($row) {
    $tenantPdo = epc_portal_tenant_control_tenant_pdo($row);
    if ($tenantPdo instanceof PDO) {
        echo "\n--- Setting up ERP base tables ---\n";
        ecomae_ensure_erp_tables($tenantPdo);
        echo "OK: ERP tables created\n";

        // Set company profile
        ecomae_set_company_profile($tenantPdo);
        echo "OK: company profile set (UAE, AED)\n";
    } else {
        echo "WARN: could not connect to new tenant DB for table setup\n";
    }
}

echo "\n=== DONE ===\n";
echo "Tenant 'ecomae_corp' (ecomae) is now registered.\n";
echo "Access ERP via BOS: select 'ecomae' from tenant dropdown → ERP modules\n";
echo "Direct ERP URL: https://www.ecomae.com/cp/client-erp/asap/ecomae_corp/\n";

// --- helper functions ---

function ecomae_ensure_erp_tables(PDO $db)
{
    // Company profile
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_co_profile` (
        `field` VARCHAR(64) NOT NULL PRIMARY KEY,
        `value` TEXT NOT NULL,
        `updated_at` INT NOT NULL DEFAULT 0
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    // GL Chart of Accounts
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_gl_accounts` (
        `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
        `code` VARCHAR(20) NOT NULL,
        `name` VARCHAR(120) NOT NULL,
        `type` VARCHAR(20) NOT NULL DEFAULT 'expense',
        `parent_id` INT UNSIGNED DEFAULT NULL,
        `level` TINYINT UNSIGNED NOT NULL DEFAULT 1,
        `is_active` TINYINT(1) NOT NULL DEFAULT 1,
        `balance` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `currency` CHAR(3) NOT NULL DEFAULT 'AED',
        `created_at` INT NOT NULL DEFAULT 0,
        `updated_at` INT NOT NULL DEFAULT 0,
        UNIQUE KEY `code` (`code`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    // GL Journal Entries
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_gl_journal` (
        `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
        `entry_date` DATE NOT NULL,
        `reference` VARCHAR(64) NOT NULL DEFAULT '',
        `memo` VARCHAR(255) NOT NULL DEFAULT '',
        `account_code` VARCHAR(20) NOT NULL,
        `debit` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `credit` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `currency` CHAR(3) NOT NULL DEFAULT 'AED',
        `source_module` VARCHAR(32) NOT NULL DEFAULT 'manual',
        `source_id` INT UNSIGNED DEFAULT NULL,
        `posted_by` INT UNSIGNED DEFAULT NULL,
        `created_at` INT NOT NULL DEFAULT 0,
        KEY `entry_date` (`entry_date`),
        KEY `account_code` (`account_code`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    // AP Vendors
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_ap_vendors` (
        `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
        `name` VARCHAR(120) NOT NULL,
        `code` VARCHAR(20) NOT NULL DEFAULT '',
        `email` VARCHAR(120) NOT NULL DEFAULT '',
        `phone` VARCHAR(30) NOT NULL DEFAULT '',
        `country` CHAR(2) NOT NULL DEFAULT 'AE',
        `tax_id` VARCHAR(30) NOT NULL DEFAULT '',
        `payment_terms` VARCHAR(20) NOT NULL DEFAULT 'net30',
        `balance` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `currency` CHAR(3) NOT NULL DEFAULT 'AED',
        `is_active` TINYINT(1) NOT NULL DEFAULT 1,
        `created_at` INT NOT NULL DEFAULT 0,
        `updated_at` INT NOT NULL DEFAULT 0,
        UNIQUE KEY `code` (`code`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    // AR Customers
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_ar_customers` (
        `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
        `name` VARCHAR(120) NOT NULL,
        `code` VARCHAR(20) NOT NULL DEFAULT '',
        `email` VARCHAR(120) NOT NULL DEFAULT '',
        `phone` VARCHAR(30) NOT NULL DEFAULT '',
        `country` CHAR(2) NOT NULL DEFAULT 'AE',
        `tax_id` VARCHAR(30) NOT NULL DEFAULT '',
        `payment_terms` VARCHAR(20) NOT NULL DEFAULT 'net30',
        `balance` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `currency` CHAR(3) NOT NULL DEFAULT 'AED',
        `is_active` TINYINT(1) NOT NULL DEFAULT 1,
        `created_at` INT NOT NULL DEFAULT 0,
        `updated_at` INT NOT NULL DEFAULT 0,
        UNIQUE KEY `code` (`code`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    // Cash & Bank accounts
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_cash_bank` (
        `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
        `name` VARCHAR(120) NOT NULL,
        `type` VARCHAR(20) NOT NULL DEFAULT 'bank',
        `account_number` VARCHAR(40) NOT NULL DEFAULT '',
        `iban` VARCHAR(34) NOT NULL DEFAULT '',
        `swift` VARCHAR(16) NOT NULL DEFAULT '',
        `currency` CHAR(3) NOT NULL DEFAULT 'AED',
        `balance` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `gl_account_code` VARCHAR(20) NOT NULL DEFAULT '',
        `is_active` TINYINT(1) NOT NULL DEFAULT 1,
        `created_at` INT NOT NULL DEFAULT 0,
        `updated_at` INT NOT NULL DEFAULT 0
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    // HR Employees
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_hr_employees` (
        `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
        `employee_id` VARCHAR(20) NOT NULL,
        `first_name` VARCHAR(60) NOT NULL,
        `last_name` VARCHAR(60) NOT NULL,
        `email` VARCHAR(120) NOT NULL DEFAULT '',
        `phone` VARCHAR(30) NOT NULL DEFAULT '',
        `department` VARCHAR(60) NOT NULL DEFAULT '',
        `position` VARCHAR(60) NOT NULL DEFAULT '',
        `hire_date` DATE DEFAULT NULL,
        `salary` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `currency` CHAR(3) NOT NULL DEFAULT 'AED',
        `country` CHAR(2) NOT NULL DEFAULT 'AE',
        `status` VARCHAR(20) NOT NULL DEFAULT 'active',
        `created_at` INT NOT NULL DEFAULT 0,
        `updated_at` INT NOT NULL DEFAULT 0,
        UNIQUE KEY `employee_id` (`employee_id`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    // Inventory items
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_inv_items` (
        `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
        `sku` VARCHAR(40) NOT NULL,
        `name` VARCHAR(180) NOT NULL,
        `category` VARCHAR(60) NOT NULL DEFAULT '',
        `unit` VARCHAR(10) NOT NULL DEFAULT 'EA',
        `cost_price` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `sell_price` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `qty_on_hand` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `reorder_level` DECIMAL(18,4) NOT NULL DEFAULT 0,
        `currency` CHAR(3) NOT NULL DEFAULT 'AED',
        `is_active` TINYINT(1) NOT NULL DEFAULT 1,
        `created_at` INT NOT NULL DEFAULT 0,
        `updated_at` INT NOT NULL DEFAULT 0,
        UNIQUE KEY `sku` (`sku`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    // Audit log
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_audit_log` (
        `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
        `module` VARCHAR(32) NOT NULL,
        `action` VARCHAR(32) NOT NULL,
        `entity_type` VARCHAR(32) NOT NULL DEFAULT '',
        `entity_id` INT UNSIGNED DEFAULT NULL,
        `actor_id` INT UNSIGNED DEFAULT NULL,
        `actor_ip` VARCHAR(45) NOT NULL DEFAULT '',
        `payload` TEXT NULL,
        `created_at` INT NOT NULL DEFAULT 0,
        KEY `module` (`module`),
        KEY `created_at` (`created_at`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    echo "  - epc_co_profile\n";
    echo "  - epc_erp_gl_accounts\n";
    echo "  - epc_erp_gl_journal\n";
    echo "  - epc_erp_ap_vendors\n";
    echo "  - epc_erp_ar_customers\n";
    echo "  - epc_erp_cash_bank\n";
    echo "  - epc_erp_hr_employees\n";
    echo "  - epc_erp_inv_items\n";
    echo "  - epc_erp_audit_log\n";
}

function ecomae_set_company_profile(PDO $db)
{
    $now = time();
    $profile = array(
        'company_name'  => 'ecomae',
        'trade_name'    => 'ecomae Platform',
        'country'       => 'AE',
        'currency'      => 'AED',
        'city'          => 'Dubai',
        'industry'      => 'Technology / SaaS Platform',
        'tax_id'        => '',
        'email'         => 'ecomaedxb@gmail.com',
        'phone'         => '',
        'fiscal_year_start' => '01-01',
        'timezone'      => 'Asia/Dubai',
        'date_format'   => 'Y-m-d',
    );

    $stmt = $db->prepare(
        'INSERT INTO `epc_co_profile` (`field`, `value`, `updated_at`)
         VALUES (?, ?, ?)
         ON DUPLICATE KEY UPDATE `value` = VALUES(`value`), `updated_at` = VALUES(`updated_at`)'
    );
    foreach ($profile as $field => $value) {
        $stmt->execute(array($field, $value, $now));
    }
}
