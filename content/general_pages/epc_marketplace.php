<?php
/**
 * P2 #40 — Platform Marketplace (Tenant App Store)
 *
 * App catalog, tenant installs, ratings/reviews, developer submissions,
 * versioning, category browsing, and usage tracking.
 * Schema: epc_marketplace_apps, epc_marketplace_installs, epc_marketplace_reviews
 */

if (!defined('EPC_MARKETPLACE_VERSION')) {
    define('EPC_MARKETPLACE_VERSION', '1.0.0');
}

function epc_marketplace_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_marketplace_apps` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `app_key`         VARCHAR(64)    NOT NULL UNIQUE,
            `name`            VARCHAR(128)   NOT NULL,
            `description`     TEXT           NOT NULL DEFAULT '',
            `short_desc`      VARCHAR(256)   NOT NULL DEFAULT '',
            `category`        VARCHAR(32)    NOT NULL DEFAULT 'general',
            `developer`       VARCHAR(128)   NOT NULL DEFAULT '',
            `version`         VARCHAR(16)    NOT NULL DEFAULT '1.0.0',
            `icon`            VARCHAR(32)    NOT NULL DEFAULT 'fa-puzzle-piece',
            `pricing`         ENUM('free','freemium','paid','contact') NOT NULL DEFAULT 'free',
            `price_monthly`   DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `features`        JSON           NULL,
            `requirements`    JSON           NULL,
            `screenshots`     JSON           NULL,
            `downloads`       INT UNSIGNED   NOT NULL DEFAULT 0,
            `avg_rating`      DECIMAL(2,1)   NOT NULL DEFAULT 0.0,
            `review_count`    INT UNSIGNED   NOT NULL DEFAULT 0,
            `status`          ENUM('draft','review','published','deprecated') NOT NULL DEFAULT 'draft',
            `published_at`    DATETIME       NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_category` (`category`),
            INDEX `idx_status` (`status`),
            INDEX `idx_rating` (`avg_rating` DESC)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_marketplace_installs` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `app_id`          INT UNSIGNED   NOT NULL,
            `site_key`        VARCHAR(64)    NOT NULL,
            `installed_version` VARCHAR(16)  NOT NULL DEFAULT '1.0.0',
            `status`          ENUM('active','disabled','uninstalled') NOT NULL DEFAULT 'active',
            `config`          JSON           NULL,
            `installed_by`    INT UNSIGNED   NOT NULL DEFAULT 0,
            `installed_at`    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_install` (`app_id`, `site_key`),
            INDEX `idx_site` (`site_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_marketplace_reviews` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `app_id`          INT UNSIGNED   NOT NULL,
            `site_key`        VARCHAR(64)    NOT NULL,
            `rating`          TINYINT UNSIGNED NOT NULL DEFAULT 5,
            `title`           VARCHAR(128)   NOT NULL DEFAULT '',
            `review_text`     TEXT           NOT NULL DEFAULT '',
            `reviewer_name`   VARCHAR(128)   NOT NULL DEFAULT '',
            `helpful_count`   INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_app` (`app_id`),
            INDEX `idx_site` (`site_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_marketplace_builtin_apps(): array
{
    return array(
        array('app_key' => 'advanced_analytics', 'name' => 'Advanced Analytics', 'category' => 'analytics', 'icon' => 'fa-line-chart', 'short_desc' => 'Deep-dive dashboards, custom KPIs, cohort analysis, and predictive trends.', 'pricing' => 'freemium'),
        array('app_key' => 'email_marketing', 'name' => 'Email Marketing Suite', 'category' => 'marketing', 'icon' => 'fa-envelope', 'short_desc' => 'Campaign builder, segmentation, A/B testing, open/click tracking.', 'pricing' => 'paid', 'price_monthly' => 49.00),
        array('app_key' => 'live_chat', 'name' => 'Live Chat & Helpdesk', 'category' => 'support', 'icon' => 'fa-comments', 'short_desc' => 'Real-time customer chat, ticket management, knowledge base, auto-routing.', 'pricing' => 'freemium'),
        array('app_key' => 'shipping_rates', 'name' => 'Multi-Carrier Shipping', 'category' => 'logistics', 'icon' => 'fa-truck', 'short_desc' => 'Real-time rates from DHL, FedEx, Aramex, USPS. Label generation, tracking.', 'pricing' => 'paid', 'price_monthly' => 29.00),
        array('app_key' => 'pos_integration', 'name' => 'POS Integration', 'category' => 'sales', 'icon' => 'fa-cash-register', 'short_desc' => 'Connect in-store POS with online inventory. Unified sales reporting.', 'pricing' => 'paid', 'price_monthly' => 79.00),
        array('app_key' => 'social_commerce', 'name' => 'Social Commerce', 'category' => 'marketing', 'icon' => 'fa-share-alt', 'short_desc' => 'Sell on Instagram, Facebook, TikTok. Auto-sync products and orders.', 'pricing' => 'freemium'),
        array('app_key' => 'loyalty_program', 'name' => 'Loyalty & Rewards', 'category' => 'marketing', 'icon' => 'fa-star', 'short_desc' => 'Points program, tiered rewards, referral bonuses, birthday discounts.', 'pricing' => 'paid', 'price_monthly' => 39.00),
        array('app_key' => 'tax_compliance', 'name' => 'Tax Compliance Engine', 'category' => 'finance', 'icon' => 'fa-calculator', 'short_desc' => 'Multi-jurisdiction VAT/GST calculation, filing reminders, exemption management.', 'pricing' => 'paid', 'price_monthly' => 59.00),
    );
}

function epc_marketplace_seed_apps(PDO $pdo): int
{
    epc_marketplace_ensure_schema($pdo);
    $apps = epc_marketplace_builtin_apps();
    $inserted = 0;
    foreach ($apps as $app) {
        $st = $pdo->prepare("SELECT COUNT(*) FROM `epc_marketplace_apps` WHERE `app_key`=?");
        $st->execute(array($app['app_key']));
        if ((int)$st->fetchColumn() === 0) {
            $pdo->prepare("INSERT INTO `epc_marketplace_apps` (`app_key`,`name`,`category`,`icon`,`short_desc`,`pricing`,`price_monthly`,`status`,`published_at`) VALUES (?,?,?,?,?,?,?,'published',NOW())")
                ->execute(array($app['app_key'], $app['name'], $app['category'], $app['icon'], $app['short_desc'], $app['pricing'], (float)($app['price_monthly']??0)));
            $inserted++;
        }
    }
    return $inserted;
}

function epc_marketplace_browse(PDO $pdo, string $category = '', string $search = ''): array
{
    epc_marketplace_ensure_schema($pdo);
    $where = "`status`='published'";
    $params = array();
    if ($category !== '') { $where .= " AND `category`=?"; $params[] = $category; }
    if ($search !== '') { $where .= " AND (`name` LIKE ? OR `short_desc` LIKE ?)"; $params[] = "%{$search}%"; $params[] = "%{$search}%"; }
    $st = $pdo->prepare("SELECT * FROM `epc_marketplace_apps` WHERE {$where} ORDER BY `avg_rating` DESC, `downloads` DESC");
    $st->execute($params);
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($rows as &$r) {
        $r['features'] = json_decode($r['features']?:'[]', true);
        $r['screenshots'] = json_decode($r['screenshots']?:'[]', true);
    }
    return $rows;
}

function epc_marketplace_install(PDO $pdo, int $appId, string $siteKey, int $userId = 0): array
{
    epc_marketplace_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT `version` FROM `epc_marketplace_apps` WHERE `id`=? AND `status`='published'");
    $st->execute(array($appId));
    $ver = $st->fetchColumn();
    if (!$ver) return array('ok' => false, 'error' => 'App not found or not published');

    $pdo->prepare("INSERT INTO `epc_marketplace_installs` (`app_id`,`site_key`,`installed_version`,`installed_by`) VALUES (?,?,?,?) ON DUPLICATE KEY UPDATE `status`='active', `installed_version`=VALUES(`installed_version`), `updated_at`=NOW()")
        ->execute(array($appId, $siteKey, $ver, $userId));
    $pdo->prepare("UPDATE `epc_marketplace_apps` SET `downloads`=`downloads`+1 WHERE `id`=?")->execute(array($appId));

    return array('ok' => true, 'app_id' => $appId, 'version' => $ver);
}

function epc_marketplace_uninstall(PDO $pdo, int $appId, string $siteKey): array
{
    $pdo->prepare("UPDATE `epc_marketplace_installs` SET `status`='uninstalled' WHERE `app_id`=? AND `site_key`=?")->execute(array($appId, $siteKey));
    return array('ok' => true);
}

function epc_marketplace_tenant_apps(PDO $pdo, string $siteKey): array
{
    $st = $pdo->prepare("SELECT i.*, a.`name`, a.`icon`, a.`category`, a.`version` AS `latest_version` FROM `epc_marketplace_installs` i JOIN `epc_marketplace_apps` a ON i.`app_id`=a.`id` WHERE i.`site_key`=? AND i.`status`='active' ORDER BY a.`name`");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_marketplace_add_review(PDO $pdo, int $appId, string $siteKey, array $data): array
{
    $rating = max(1, min(5, (int)($data['rating']??5)));
    $pdo->prepare("INSERT INTO `epc_marketplace_reviews` (`app_id`,`site_key`,`rating`,`title`,`review_text`,`reviewer_name`) VALUES (?,?,?,?,?,?)")
        ->execute(array($appId, $siteKey, $rating, (string)($data['title']??''), (string)($data['review_text']??''), (string)($data['reviewer_name']??'')));

    $st = $pdo->prepare("SELECT AVG(`rating`) AS `avg`, COUNT(*) AS `cnt` FROM `epc_marketplace_reviews` WHERE `app_id`=?");
    $st->execute(array($appId));
    $agg = $st->fetch(PDO::FETCH_ASSOC);
    $pdo->prepare("UPDATE `epc_marketplace_apps` SET `avg_rating`=?, `review_count`=? WHERE `id`=?")->execute(array(round((float)$agg['avg'], 1), (int)$agg['cnt'], $appId));

    return array('ok' => true);
}

function epc_marketplace_fleet_stats(PDO $pdo): array
{
    epc_marketplace_ensure_schema($pdo);
    $st = $pdo->query("SELECT COUNT(*) AS `total_apps`, SUM(CASE WHEN `status`='published' THEN 1 ELSE 0 END) AS `published`, SUM(`downloads`) AS `total_downloads`, AVG(`avg_rating`) AS `avg_rating` FROM `epc_marketplace_apps`");
    $apps = $st->fetch(PDO::FETCH_ASSOC) ?: array();
    $st2 = $pdo->query("SELECT COUNT(DISTINCT `site_key`) AS `tenants_using` FROM `epc_marketplace_installs` WHERE `status`='active'");
    $installs = $st2->fetch(PDO::FETCH_ASSOC) ?: array();
    return array_merge($apps, $installs);
}
