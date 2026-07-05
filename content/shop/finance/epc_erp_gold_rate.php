<?php
/**
 * Gold Rate Module — fetch live gold rates from APIs or manual entry.
 * Supports multiple karats (24K, 22K, 21K, 18K), multiple currencies.
 * Can fetch from GoldAPI.io, metals-api.com, or custom endpoint.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_gold_rate_ensure_schema')) {
    function epc_gold_rate_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_gold_rates` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `rate_date` date NOT NULL,
            `karat` varchar(10) NOT NULL DEFAULT '24K',
            `currency` varchar(3) NOT NULL DEFAULT 'AED',
            `buy_rate` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `sell_rate` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `unit` varchar(10) NOT NULL DEFAULT 'gram' COMMENT 'gram,ounce,tola,kg',
            `source` varchar(50) NOT NULL DEFAULT 'manual' COMMENT 'manual,api,web_scrape',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `ux_date_karat_cur` (`company_id`, `rate_date`, `karat`, `currency`),
            KEY `x_date` (`rate_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Daily gold rates'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_gold_rate_config` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `api_provider` varchar(50) NOT NULL DEFAULT 'manual' COMMENT 'manual,goldapi,metals_api,custom',
            `api_key` varchar(200) NOT NULL DEFAULT '',
            `api_endpoint` varchar(500) NOT NULL DEFAULT '',
            `base_currency` varchar(3) NOT NULL DEFAULT 'AED',
            `karats` varchar(100) NOT NULL DEFAULT '24K,22K,21K,18K',
            `auto_fetch` tinyint(1) NOT NULL DEFAULT 0,
            `fetch_time` varchar(5) NOT NULL DEFAULT '09:00',
            `margin_buy_pct` decimal(5,2) NOT NULL DEFAULT 0.00,
            `margin_sell_pct` decimal(5,2) NOT NULL DEFAULT 1.50,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Gold rate API configuration'");
    }

    function epc_gold_rate_get_today(PDO $db, int $companyId, string $karat = '24K', string $currency = 'AED'): ?array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_gold_rates` WHERE `company_id` = ? AND `rate_date` = CURDATE() AND `karat` = ? AND `currency` = ? LIMIT 1");
        $stmt->execute([$companyId, $karat, $currency]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }

    function epc_gold_rate_set(PDO $db, array $data): int
    {
        $stmt = $db->prepare("INSERT INTO `epc_gold_rates` (`company_id`,`rate_date`,`karat`,`currency`,`buy_rate`,`sell_rate`,`unit`,`source`,`time_created`)
            VALUES (?,?,?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `buy_rate` = VALUES(`buy_rate`), `sell_rate` = VALUES(`sell_rate`), `source` = VALUES(`source`)");
        $stmt->execute([$data['company_id'] ?? 0, $data['rate_date'] ?? date('Y-m-d'), $data['karat'] ?? '24K', $data['currency'] ?? 'AED', $data['buy_rate'] ?? 0, $data['sell_rate'] ?? 0, $data['unit'] ?? 'gram', $data['source'] ?? 'manual', time()]);
        return (int) $db->lastInsertId();
    }

    function epc_gold_rate_fetch_api(PDO $db, int $companyId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_gold_rate_config` WHERE `company_id` = ? LIMIT 1");
        $stmt->execute([$companyId]);
        $cfg = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$cfg || $cfg['api_provider'] === 'manual') {
            return ['ok' => false, 'error' => 'No API configured — use manual entry'];
        }

        $karats = array_map('trim', explode(',', $cfg['karats']));
        $results = [];
        $karatFactors = ['24K' => 1.0, '22K' => 0.9167, '21K' => 0.875, '18K' => 0.75, '14K' => 0.5833];

        if ($cfg['api_provider'] === 'goldapi') {
            $url = 'https://www.goldapi.io/api/XAU/' . $cfg['base_currency'];
            $ctx = stream_context_create(['http' => ['header' => "x-access-token: " . $cfg['api_key'] . "\r\n", 'timeout' => 10]]);
            $response = @file_get_contents($url, false, $ctx);
            if ($response === false) return ['ok' => false, 'error' => 'API request failed'];
            $json = json_decode($response, true);
            if (!isset($json['price_gram_24k'])) return ['ok' => false, 'error' => 'Invalid API response'];

            $price24k = (float) $json['price_gram_24k'];
            foreach ($karats as $k) {
                $factor = $karatFactors[$k] ?? 1.0;
                $basePrice = $price24k * $factor;
                $buyRate = $basePrice * (1 - ($cfg['margin_buy_pct'] / 100));
                $sellRate = $basePrice * (1 + ($cfg['margin_sell_pct'] / 100));
                epc_gold_rate_set($db, ['company_id' => $companyId, 'karat' => $k, 'currency' => $cfg['base_currency'], 'buy_rate' => $buyRate, 'sell_rate' => $sellRate, 'source' => 'api']);
                $results[$k] = ['buy' => round($buyRate, 2), 'sell' => round($sellRate, 2)];
            }
        } elseif ($cfg['api_provider'] === 'metals_api') {
            $url = 'https://metals-api.com/api/latest?access_key=' . $cfg['api_key'] . '&base=' . $cfg['base_currency'] . '&symbols=XAU';
            $response = @file_get_contents($url);
            if ($response === false) return ['ok' => false, 'error' => 'API request failed'];
            $json = json_decode($response, true);
            if (!isset($json['rates']['XAU'])) return ['ok' => false, 'error' => 'Invalid API response'];
            $ozPrice = 1 / (float) $json['rates']['XAU'];
            $gramPrice = $ozPrice / 31.1035;
            foreach ($karats as $k) {
                $factor = $karatFactors[$k] ?? 1.0;
                $basePrice = $gramPrice * $factor;
                $buyRate = $basePrice * (1 - ($cfg['margin_buy_pct'] / 100));
                $sellRate = $basePrice * (1 + ($cfg['margin_sell_pct'] / 100));
                epc_gold_rate_set($db, ['company_id' => $companyId, 'karat' => $k, 'currency' => $cfg['base_currency'], 'buy_rate' => $buyRate, 'sell_rate' => $sellRate, 'source' => 'api']);
                $results[$k] = ['buy' => round($buyRate, 2), 'sell' => round($sellRate, 2)];
            }
        } elseif ($cfg['api_provider'] === 'custom') {
            $response = @file_get_contents($cfg['api_endpoint']);
            if ($response === false) return ['ok' => false, 'error' => 'Custom API request failed'];
            $json = json_decode($response, true);
            if (!is_array($json)) return ['ok' => false, 'error' => 'Invalid custom API response'];
            $results = $json;
        }

        return ['ok' => true, 'rates' => $results, 'timestamp' => date('Y-m-d H:i:s')];
    }

    function epc_gold_rate_history(PDO $db, int $companyId, string $karat = '24K', int $days = 30): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_gold_rates` WHERE `company_id` = ? AND `karat` = ? AND `rate_date` >= DATE_SUB(CURDATE(), INTERVAL ? DAY) ORDER BY `rate_date` ASC");
        $stmt->execute([$companyId, $karat, $days]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }
}
