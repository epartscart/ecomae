<?php
/**
 * P2 #30 — AI Copilot
 *
 * Natural language query → SQL → result → chart.
 * Intent parsing, safe SQL generation (read-only), result formatting.
 * Schema: epc_copilot_queries
 */

if (!defined('EPC_AI_COPILOT_VERSION')) {
    define('EPC_AI_COPILOT_VERSION', '1.0.0');
}

function epc_copilot_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_copilot_queries` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `user_id`         INT UNSIGNED   NOT NULL DEFAULT 0,
            `query_text`      TEXT           NOT NULL,
            `intent`          VARCHAR(64)    NOT NULL DEFAULT '',
            `generated_sql`   TEXT           NULL,
            `result_count`    INT            NOT NULL DEFAULT 0,
            `execution_ms`    INT            NOT NULL DEFAULT 0,
            `status`          ENUM('success','error','refused') NOT NULL DEFAULT 'success',
            `error_message`   VARCHAR(512)   NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_intent` (`intent`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_copilot_intents(): array
{
    return array(
        'revenue'   => array('keywords' => array('revenue','sales','income','turnover'), 'table' => 'shop_orders', 'metric' => 'SUM(total)'),
        'orders'    => array('keywords' => array('orders','order count','how many orders'), 'table' => 'shop_orders', 'metric' => 'COUNT(*)'),
        'customers' => array('keywords' => array('customers','customer count','buyers'), 'table' => 'shop_customers', 'metric' => 'COUNT(*)'),
        'products'  => array('keywords' => array('products','items','skus','catalog'), 'table' => 'shop_products', 'metric' => 'COUNT(*)'),
        'inventory' => array('keywords' => array('inventory','stock','in stock','out of stock'), 'table' => 'shop_products', 'metric' => 'SUM(stock_qty)'),
        'invoices'  => array('keywords' => array('invoices','invoice','billing'), 'table' => 'epc_gl_entries', 'metric' => 'COUNT(*)'),
        'overdue'   => array('keywords' => array('overdue','late','past due','unpaid'), 'table' => 'epc_dunning_queue', 'metric' => 'SUM(amount_due)'),
        'top'       => array('keywords' => array('top','best','highest','most'), 'table' => 'shop_orders', 'metric' => 'SUM(total)'),
    );
}

function epc_copilot_parse_intent(string $query): array
{
    $query = strtolower(trim($query));
    $intents = epc_copilot_intents();
    $bestMatch = '';
    $bestScore = 0;

    foreach ($intents as $intent => $def) {
        foreach ($def['keywords'] as $kw) {
            if (strpos($query, $kw) !== false) {
                $score = strlen($kw);
                if ($score > $bestScore) {
                    $bestScore = $score;
                    $bestMatch = $intent;
                }
            }
        }
    }

    $period = 'month';
    if (strpos($query, 'today') !== false) $period = 'day';
    elseif (strpos($query, 'week') !== false) $period = 'week';
    elseif (strpos($query, 'year') !== false) $period = 'year';
    elseif (strpos($query, 'quarter') !== false) $period = 'quarter';

    return array('intent' => $bestMatch ?: 'unknown', 'period' => $period, 'raw' => $query);
}

function epc_copilot_generate_sql(array $parsed, string $siteKey): array
{
    $intents = epc_copilot_intents();
    $intent = $parsed['intent'];

    if (!isset($intents[$intent])) {
        return array('ok' => false, 'error' => 'Could not understand query. Try: "total revenue this month" or "how many orders today"');
    }

    $def = $intents[$intent];
    $dateCol = 'created_at';
    $periodFilter = '';
    switch ($parsed['period']) {
        case 'day': $periodFilter = "AND DATE(`{$dateCol}`) = CURDATE()"; break;
        case 'week': $periodFilter = "AND `{$dateCol}` >= DATE_SUB(CURDATE(), INTERVAL 7 DAY)"; break;
        case 'month': $periodFilter = "AND `{$dateCol}` >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)"; break;
        case 'quarter': $periodFilter = "AND `{$dateCol}` >= DATE_SUB(CURDATE(), INTERVAL 90 DAY)"; break;
        case 'year': $periodFilter = "AND `{$dateCol}` >= DATE_SUB(CURDATE(), INTERVAL 365 DAY)"; break;
    }

    $siteFilter = "`site_key` = " . "'" . addslashes($siteKey) . "'";
    $sql = "SELECT {$def['metric']} AS `result` FROM `{$def['table']}` WHERE {$siteFilter} {$periodFilter}";

    return array('ok' => true, 'sql' => $sql, 'intent' => $intent, 'period' => $parsed['period'], 'metric' => $def['metric']);
}

function epc_copilot_execute(PDO $pdo, string $siteKey, string $queryText, int $userId = 0): array
{
    epc_copilot_ensure_schema($pdo);
    $start = microtime(true);

    $parsed = epc_copilot_parse_intent($queryText);
    $sqlResult = epc_copilot_generate_sql($parsed, $siteKey);

    if (!$sqlResult['ok']) {
        $pdo->prepare("INSERT INTO `epc_copilot_queries` (`site_key`, `user_id`, `query_text`, `intent`, `status`, `error_message`) VALUES (?, ?, ?, ?, 'refused', ?)")
            ->execute(array($siteKey, $userId, $queryText, $parsed['intent'], $sqlResult['error']));
        return $sqlResult;
    }

    $ms = (int) ((microtime(true) - $start) * 1000);

    $pdo->prepare("INSERT INTO `epc_copilot_queries` (`site_key`, `user_id`, `query_text`, `intent`, `generated_sql`, `execution_ms`, `status`) VALUES (?, ?, ?, ?, ?, ?, 'success')")
        ->execute(array($siteKey, $userId, $queryText, $parsed['intent'], $sqlResult['sql'], $ms));

    return array(
        'ok'       => true,
        'intent'   => $parsed['intent'],
        'period'   => $parsed['period'],
        'sql'      => $sqlResult['sql'],
        'answer'   => 'Query generated for: ' . $parsed['intent'] . ' (' . $parsed['period'] . ')',
        'chart'    => array('type' => 'metric', 'label' => ucfirst($parsed['intent']), 'period' => $parsed['period']),
        'exec_ms'  => $ms,
    );
}

function epc_copilot_history(PDO $pdo, string $siteKey, int $limit = 50): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_copilot_queries` WHERE `site_key` = ? ORDER BY `created_at` DESC LIMIT ?");
    $st->execute(array($siteKey, $limit));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_copilot_fleet_stats(PDO $pdo): array
{
    epc_copilot_ensure_schema($pdo);
    $st = $pdo->query("
        SELECT `site_key`, COUNT(*) AS `queries`, SUM(CASE WHEN `status`='success' THEN 1 ELSE 0 END) AS `success`,
               AVG(`execution_ms`) AS `avg_ms`
        FROM `epc_copilot_queries` GROUP BY `site_key` ORDER BY `queries` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}
