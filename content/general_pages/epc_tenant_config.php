<?php
/**
 * P1 #19 — Tenant Self-Service Config Portal
 *
 * Allows tenant admins to configure their own settings without
 * provider intervention: branding, business info, tax, shipping,
 * payment methods, email templates, and feature toggles.
 * Schema: epc_tenant_config
 */

if (!defined('EPC_TENANT_CONFIG_VERSION')) {
    define('EPC_TENANT_CONFIG_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_tenant_config_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_tenant_config` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `config_group`    VARCHAR(64)    NOT NULL,
            `config_key`      VARCHAR(128)   NOT NULL,
            `config_value`    TEXT           NOT NULL,
            `value_type`      ENUM('string','int','float','bool','json') NOT NULL DEFAULT 'string',
            `label`           VARCHAR(128)   NOT NULL DEFAULT '',
            `description`     VARCHAR(512)   NOT NULL DEFAULT '',
            `editable`        TINYINT(1)     NOT NULL DEFAULT 1,
            `updated_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_config` (`site_key`, `config_group`, `config_key`),
            INDEX `idx_group` (`config_group`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_tenant_config_history` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `config_group`    VARCHAR(64)    NOT NULL,
            `config_key`      VARCHAR(128)   NOT NULL,
            `old_value`       TEXT           NULL,
            `new_value`       TEXT           NOT NULL,
            `changed_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `changed_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site_key` (`site_key`, `config_key`),
            INDEX `idx_changed` (`changed_at`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── config groups with defaults ─── */

function epc_tenant_config_groups(): array
{
    return array(
        'branding' => array(
            'label' => 'Branding & Appearance',
            'icon'  => 'fa-paint-brush',
            'fields' => array(
                array('key' => 'company_name',    'label' => 'Company Name',        'type' => 'string', 'default' => ''),
                array('key' => 'logo_url',        'label' => 'Logo URL',            'type' => 'string', 'default' => ''),
                array('key' => 'favicon_url',     'label' => 'Favicon URL',         'type' => 'string', 'default' => ''),
                array('key' => 'primary_color',   'label' => 'Primary Brand Color', 'type' => 'string', 'default' => '#0d6efd'),
                array('key' => 'secondary_color', 'label' => 'Secondary Color',     'type' => 'string', 'default' => '#6c757d'),
                array('key' => 'login_message',   'label' => 'Login Welcome Text',  'type' => 'string', 'default' => ''),
            ),
        ),
        'business' => array(
            'label' => 'Business Information',
            'icon'  => 'fa-building',
            'fields' => array(
                array('key' => 'legal_name',      'label' => 'Legal Entity Name',   'type' => 'string', 'default' => ''),
                array('key' => 'tax_id',          'label' => 'Tax ID / TRN',        'type' => 'string', 'default' => ''),
                array('key' => 'registration_no', 'label' => 'Trade License No.',   'type' => 'string', 'default' => ''),
                array('key' => 'address_line1',   'label' => 'Address Line 1',      'type' => 'string', 'default' => ''),
                array('key' => 'address_line2',   'label' => 'Address Line 2',      'type' => 'string', 'default' => ''),
                array('key' => 'city',            'label' => 'City',                'type' => 'string', 'default' => ''),
                array('key' => 'country',         'label' => 'Country',             'type' => 'string', 'default' => ''),
                array('key' => 'phone',           'label' => 'Phone',               'type' => 'string', 'default' => ''),
                array('key' => 'email',           'label' => 'Contact Email',       'type' => 'string', 'default' => ''),
                array('key' => 'website',         'label' => 'Website URL',         'type' => 'string', 'default' => ''),
            ),
        ),
        'tax' => array(
            'label' => 'Tax Configuration',
            'icon'  => 'fa-percent',
            'fields' => array(
                array('key' => 'vat_enabled',     'label' => 'VAT Enabled',         'type' => 'bool',   'default' => '1'),
                array('key' => 'vat_rate',        'label' => 'Default VAT Rate %',  'type' => 'float',  'default' => '5'),
                array('key' => 'vat_inclusive',    'label' => 'Prices Include VAT', 'type' => 'bool',   'default' => '1'),
                array('key' => 'tax_report_period','label' => 'Tax Report Period',  'type' => 'string', 'default' => 'quarterly'),
            ),
        ),
        'shipping' => array(
            'label' => 'Shipping Settings',
            'icon'  => 'fa-truck',
            'fields' => array(
                array('key' => 'free_shipping_min','label' => 'Free Shipping Min Order','type' => 'float','default' => '0'),
                array('key' => 'default_carrier',  'label' => 'Default Carrier',       'type' => 'string','default' => ''),
                array('key' => 'shipping_origin',  'label' => 'Ship From Location',    'type' => 'string','default' => ''),
                array('key' => 'handling_days',    'label' => 'Handling Days',          'type' => 'int',  'default' => '1'),
            ),
        ),
        'payments' => array(
            'label' => 'Payment Methods',
            'icon'  => 'fa-credit-card',
            'fields' => array(
                array('key' => 'cod_enabled',     'label' => 'Cash on Delivery',    'type' => 'bool',   'default' => '1'),
                array('key' => 'card_enabled',    'label' => 'Card Payments',       'type' => 'bool',   'default' => '0'),
                array('key' => 'bank_transfer',   'label' => 'Bank Transfer',       'type' => 'bool',   'default' => '1'),
                array('key' => 'payment_terms',   'label' => 'Default Net Terms',   'type' => 'string', 'default' => 'Net 30'),
            ),
        ),
        'emails' => array(
            'label' => 'Email Settings',
            'icon'  => 'fa-envelope',
            'fields' => array(
                array('key' => 'from_name',       'label' => 'From Name',           'type' => 'string', 'default' => ''),
                array('key' => 'from_email',      'label' => 'From Email',          'type' => 'string', 'default' => ''),
                array('key' => 'reply_to',        'label' => 'Reply-To Email',      'type' => 'string', 'default' => ''),
                array('key' => 'order_confirm',   'label' => 'Order Confirmation',  'type' => 'bool',   'default' => '1'),
                array('key' => 'shipping_notify', 'label' => 'Shipping Notification','type' => 'bool',  'default' => '1'),
                array('key' => 'invoice_email',   'label' => 'Invoice Email',       'type' => 'bool',   'default' => '1'),
            ),
        ),
        'features' => array(
            'label' => 'Feature Toggles',
            'icon'  => 'fa-toggle-on',
            'fields' => array(
                array('key' => 'reviews_enabled', 'label' => 'Product Reviews',     'type' => 'bool',   'default' => '1'),
                array('key' => 'wishlist_enabled','label' => 'Wishlist',             'type' => 'bool',   'default' => '1'),
                array('key' => 'compare_enabled', 'label' => 'Product Compare',     'type' => 'bool',   'default' => '0'),
                array('key' => 'live_chat',       'label' => 'Live Chat Widget',    'type' => 'bool',   'default' => '0'),
                array('key' => 'multilang',       'label' => 'Multi-Language',       'type' => 'bool',   'default' => '0'),
                array('key' => 'b2b_mode',        'label' => 'B2B Mode',            'type' => 'bool',   'default' => '0'),
            ),
        ),
    );
}

/* ─── get config value ─── */

function epc_tenant_config_get(PDO $pdo, string $siteKey, string $group, string $key): string
{
    epc_tenant_config_ensure_schema($pdo);

    $st = $pdo->prepare("SELECT `config_value` FROM `epc_tenant_config` WHERE `site_key` = ? AND `config_group` = ? AND `config_key` = ?");
    $st->execute(array($siteKey, $group, $key));
    $val = $st->fetchColumn();

    if ($val !== false) {
        return (string) $val;
    }

    $groups = epc_tenant_config_groups();
    if (isset($groups[$group])) {
        foreach ($groups[$group]['fields'] as $field) {
            if ($field['key'] === $key) {
                return $field['default'];
            }
        }
    }

    return '';
}

/* ─── get all config for a group ─── */

function epc_tenant_config_group(PDO $pdo, string $siteKey, string $group): array
{
    epc_tenant_config_ensure_schema($pdo);

    $groups = epc_tenant_config_groups();
    if (!isset($groups[$group])) {
        return array();
    }

    $st = $pdo->prepare("SELECT `config_key`, `config_value`, `value_type` FROM `epc_tenant_config` WHERE `site_key` = ? AND `config_group` = ?");
    $st->execute(array($siteKey, $group));
    $stored = array();
    foreach ($st->fetchAll(PDO::FETCH_ASSOC) ?: array() as $row) {
        $stored[$row['config_key']] = $row['config_value'];
    }

    $result = array();
    foreach ($groups[$group]['fields'] as $field) {
        $result[] = array(
            'key'     => $field['key'],
            'label'   => $field['label'],
            'type'    => $field['type'],
            'value'   => $stored[$field['key']] ?? $field['default'],
            'default' => $field['default'],
        );
    }
    return $result;
}

/* ─── get all config for a tenant ─── */

function epc_tenant_config_all(PDO $pdo, string $siteKey): array
{
    $groups = epc_tenant_config_groups();
    $result = array();

    foreach ($groups as $groupKey => $groupDef) {
        $result[$groupKey] = array(
            'label'  => $groupDef['label'],
            'icon'   => $groupDef['icon'],
            'fields' => epc_tenant_config_group($pdo, $siteKey, $groupKey),
        );
    }

    return $result;
}

/* ─── set config value ─── */

function epc_tenant_config_set(PDO $pdo, string $siteKey, string $group, string $key, string $value, int $userId = 0): array
{
    epc_tenant_config_ensure_schema($pdo);

    $groups = epc_tenant_config_groups();
    if (!isset($groups[$group])) {
        return array('ok' => false, 'error' => 'Invalid config group');
    }
    $fieldDef = null;
    foreach ($groups[$group]['fields'] as $f) {
        if ($f['key'] === $key) {
            $fieldDef = $f;
            break;
        }
    }
    if (!$fieldDef) {
        return array('ok' => false, 'error' => 'Invalid config key');
    }

    $oldValue = epc_tenant_config_get($pdo, $siteKey, $group, $key);

    $st = $pdo->prepare("
        INSERT INTO `epc_tenant_config`
            (`site_key`, `config_group`, `config_key`, `config_value`, `value_type`, `label`, `updated_by`)
        VALUES (?, ?, ?, ?, ?, ?, ?)
        ON DUPLICATE KEY UPDATE
            `config_value` = VALUES(`config_value`),
            `updated_by` = VALUES(`updated_by`),
            `updated_at` = NOW()
    ");
    $st->execute(array($siteKey, $group, $key, $value, $fieldDef['type'], $fieldDef['label'], $userId));

    $pdo->prepare("
        INSERT INTO `epc_tenant_config_history`
            (`site_key`, `config_group`, `config_key`, `old_value`, `new_value`, `changed_by`)
        VALUES (?, ?, ?, ?, ?, ?)
    ")->execute(array($siteKey, $group, $key, $oldValue, $value, $userId));

    return array('ok' => true, 'key' => $key, 'old_value' => $oldValue, 'new_value' => $value);
}

/* ─── bulk set config ─── */

function epc_tenant_config_bulk_set(PDO $pdo, string $siteKey, string $group, array $values, int $userId = 0): array
{
    $results = array();
    foreach ($values as $key => $value) {
        $results[$key] = epc_tenant_config_set($pdo, $siteKey, $group, $key, (string) $value, $userId);
    }
    return array('ok' => true, 'results' => $results);
}

/* ─── config change history ─── */

function epc_tenant_config_history(PDO $pdo, string $siteKey, int $limit = 50): array
{
    epc_tenant_config_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT * FROM `epc_tenant_config_history`
        WHERE `site_key` = ?
        ORDER BY `changed_at` DESC
        LIMIT ?
    ");
    $st->execute(array($siteKey, $limit));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── fleet config overview (BOS) ─── */

function epc_tenant_config_fleet(PDO $pdo): array
{
    epc_tenant_config_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `site_key`,
               COUNT(*) AS `configs_set`,
               COUNT(DISTINCT `config_group`) AS `groups_configured`,
               MAX(`updated_at`) AS `last_updated`
        FROM `epc_tenant_config`
        GROUP BY `site_key`
        ORDER BY `configs_set` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── export / import ─── */

function epc_tenant_config_export(PDO $pdo, string $siteKey): array
{
    $st = $pdo->prepare("SELECT `config_group`, `config_key`, `config_value`, `value_type` FROM `epc_tenant_config` WHERE `site_key` = ? ORDER BY `config_group`, `config_key`");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_tenant_config_import(PDO $pdo, string $siteKey, array $configs, int $userId = 0): array
{
    $imported = 0;
    foreach ($configs as $c) {
        $r = epc_tenant_config_set($pdo, $siteKey, (string) ($c['config_group'] ?? ''), (string) ($c['config_key'] ?? ''), (string) ($c['config_value'] ?? ''), $userId);
        if (!empty($r['ok'])) {
            $imported++;
        }
    }
    return array('ok' => true, 'imported' => $imported);
}
