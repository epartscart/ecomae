<?php
/**
 * P1 #18 — AI Classification Service
 *
 * HS code lookup, product category auto-tagging, description enrichment.
 * Uses rule-based classification with keyword matching and HS code database.
 * Schema: epc_ai_classifications, epc_hs_codes
 */

if (!defined('EPC_AI_CLASSIFICATION_VERSION')) {
    define('EPC_AI_CLASSIFICATION_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_ai_class_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_hs_codes` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `code`            VARCHAR(12)    NOT NULL,
            `description`     VARCHAR(512)   NOT NULL,
            `chapter`         VARCHAR(4)     NOT NULL DEFAULT '',
            `section`         VARCHAR(128)   NOT NULL DEFAULT '',
            `duty_rate`       DECIMAL(5,2)   NOT NULL DEFAULT 0.00,
            `keywords`        TEXT           NOT NULL,
            UNIQUE KEY `uk_code` (`code`),
            INDEX `idx_chapter` (`chapter`),
            FULLTEXT KEY `ft_desc` (`description`, `keywords`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_ai_classifications` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `product_id`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `sku`             VARCHAR(64)    NOT NULL DEFAULT '',
            `input_text`      TEXT           NOT NULL,
            `hs_code`         VARCHAR(12)    NOT NULL DEFAULT '',
            `category`        VARCHAR(128)   NOT NULL DEFAULT '',
            `subcategory`     VARCHAR(128)   NOT NULL DEFAULT '',
            `tags`            JSON           NULL,
            `confidence`      DECIMAL(5,2)   NOT NULL DEFAULT 0.00,
            `method`          ENUM('rule','keyword','hs_lookup','manual') NOT NULL DEFAULT 'rule',
            `reviewed`        TINYINT(1)     NOT NULL DEFAULT 0,
            `reviewed_by`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site_product` (`site_key`, `product_id`),
            INDEX `idx_hs` (`hs_code`),
            INDEX `idx_category` (`category`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── category rules ─── */

function epc_ai_category_rules(): array
{
    return array(
        array(
            'category'    => 'Auto Parts',
            'subcategory' => 'Engine Components',
            'keywords'    => array('engine', 'piston', 'crankshaft', 'camshaft', 'valve', 'cylinder', 'gasket', 'timing belt', 'oil pump'),
            'hs_prefix'   => '8409',
        ),
        array(
            'category'    => 'Auto Parts',
            'subcategory' => 'Brakes',
            'keywords'    => array('brake', 'disc', 'pad', 'caliper', 'rotor', 'brake fluid', 'abs'),
            'hs_prefix'   => '8708',
        ),
        array(
            'category'    => 'Auto Parts',
            'subcategory' => 'Suspension',
            'keywords'    => array('shock', 'absorber', 'strut', 'spring', 'suspension', 'bushing', 'ball joint', 'tie rod', 'stabilizer'),
            'hs_prefix'   => '8708',
        ),
        array(
            'category'    => 'Auto Parts',
            'subcategory' => 'Electrical',
            'keywords'    => array('alternator', 'starter', 'ignition', 'spark plug', 'battery', 'wiring', 'sensor', 'relay', 'fuse'),
            'hs_prefix'   => '8511',
        ),
        array(
            'category'    => 'Auto Parts',
            'subcategory' => 'Filters',
            'keywords'    => array('air filter', 'oil filter', 'fuel filter', 'cabin filter', 'filter element'),
            'hs_prefix'   => '8421',
        ),
        array(
            'category'    => 'Auto Parts',
            'subcategory' => 'Body Parts',
            'keywords'    => array('bumper', 'fender', 'hood', 'door', 'mirror', 'windshield', 'grille', 'panel', 'trunk'),
            'hs_prefix'   => '8708',
        ),
        array(
            'category'    => 'Electronics',
            'subcategory' => 'Consumer Electronics',
            'keywords'    => array('phone', 'tablet', 'laptop', 'computer', 'monitor', 'speaker', 'headphone', 'camera', 'charger'),
            'hs_prefix'   => '8471',
        ),
        array(
            'category'    => 'Fashion',
            'subcategory' => 'Clothing',
            'keywords'    => array('shirt', 'dress', 'pants', 'jeans', 'jacket', 'coat', 'sweater', 'skirt', 'blouse', 't-shirt'),
            'hs_prefix'   => '6109',
        ),
        array(
            'category'    => 'Fashion',
            'subcategory' => 'Footwear',
            'keywords'    => array('shoe', 'boot', 'sandal', 'sneaker', 'heel', 'slipper', 'loafer'),
            'hs_prefix'   => '6403',
        ),
        array(
            'category'    => 'Jewellery',
            'subcategory' => 'Fine Jewellery',
            'keywords'    => array('gold', 'silver', 'diamond', 'ring', 'necklace', 'bracelet', 'earring', 'pendant', 'chain', 'gemstone'),
            'hs_prefix'   => '7113',
        ),
        array(
            'category'    => 'Jewellery',
            'subcategory' => 'Watches',
            'keywords'    => array('watch', 'wristwatch', 'timepiece', 'chronograph'),
            'hs_prefix'   => '9101',
        ),
    );
}

/* ─── classify product text ─── */

function epc_ai_classify(string $text): array
{
    $text = strtolower(trim($text));
    if ($text === '') {
        return array('category' => 'Uncategorized', 'subcategory' => '', 'confidence' => 0, 'tags' => array(), 'hs_code' => '', 'method' => 'rule');
    }

    $rules = epc_ai_category_rules();
    $bestMatch = null;
    $bestScore = 0;
    $matchedKeywords = array();

    foreach ($rules as $rule) {
        $score = 0;
        $matched = array();
        foreach ($rule['keywords'] as $kw) {
            if (strpos($text, $kw) !== false) {
                $score += strlen($kw);
                $matched[] = $kw;
            }
        }
        if ($score > $bestScore) {
            $bestScore = $score;
            $bestMatch = $rule;
            $matchedKeywords = $matched;
        }
    }

    if (!$bestMatch || $bestScore < 3) {
        return array('category' => 'Uncategorized', 'subcategory' => '', 'confidence' => 0, 'tags' => array(), 'hs_code' => '', 'method' => 'rule');
    }

    $maxPossible = array_sum(array_map('strlen', $bestMatch['keywords']));
    $confidence = min(99, round(($bestScore / max(1, $maxPossible)) * 100, 2));

    return array(
        'category'    => $bestMatch['category'],
        'subcategory' => $bestMatch['subcategory'],
        'confidence'  => $confidence,
        'tags'        => $matchedKeywords,
        'hs_code'     => $bestMatch['hs_prefix'],
        'method'      => 'keyword',
    );
}

/* ─── classify and store ─── */

function epc_ai_classify_and_store(PDO $pdo, string $siteKey, array $product): array
{
    epc_ai_class_ensure_schema($pdo);

    $text = implode(' ', array_filter(array(
        (string) ($product['name'] ?? ''),
        (string) ($product['description'] ?? ''),
        (string) ($product['brand'] ?? ''),
        (string) ($product['sku'] ?? ''),
    )));

    $result = epc_ai_classify($text);

    $st = $pdo->prepare("
        INSERT INTO `epc_ai_classifications`
            (`site_key`, `product_id`, `sku`, `input_text`, `hs_code`, `category`, `subcategory`, `tags`, `confidence`, `method`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array(
        $siteKey,
        (int) ($product['id'] ?? $product['product_id'] ?? 0),
        (string) ($product['sku'] ?? ''),
        substr($text, 0, 1000),
        $result['hs_code'],
        $result['category'],
        $result['subcategory'],
        json_encode($result['tags']),
        $result['confidence'],
        $result['method'],
    ));

    $result['classification_id'] = (int) $pdo->lastInsertId();
    $result['ok'] = true;
    return $result;
}

/* ─── batch classify ─── */

function epc_ai_classify_batch(PDO $pdo, string $siteKey, array $products): array
{
    $results = array();
    $classified = 0;
    $uncategorized = 0;

    foreach ($products as $product) {
        $r = epc_ai_classify_and_store($pdo, $siteKey, $product);
        $results[] = $r;
        if ($r['category'] !== 'Uncategorized') {
            $classified++;
        } else {
            $uncategorized++;
        }
    }

    return array(
        'ok'            => true,
        'total'         => count($products),
        'classified'    => $classified,
        'uncategorized' => $uncategorized,
        'results'       => $results,
    );
}

/* ─── HS code lookup ─── */

function epc_ai_hs_lookup(PDO $pdo, string $query): array
{
    epc_ai_class_ensure_schema($pdo);

    if (preg_match('/^\d{4,}$/', $query)) {
        $st = $pdo->prepare("SELECT * FROM `epc_hs_codes` WHERE `code` LIKE ? LIMIT 20");
        $st->execute(array($query . '%'));
    } else {
        $st = $pdo->prepare("SELECT * FROM `epc_hs_codes` WHERE MATCH(`description`, `keywords`) AGAINST(? IN BOOLEAN MODE) LIMIT 20");
        $st->execute(array($query));
    }

    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── seed common HS codes ─── */

function epc_ai_seed_hs_codes(PDO $pdo): int
{
    epc_ai_class_ensure_schema($pdo);

    $codes = array(
        array('8409', 'Parts for spark-ignition engines', '84', 'Machinery', 5.00, 'engine piston crankshaft camshaft valve cylinder'),
        array('8421', 'Filtering or purifying machinery', '84', 'Machinery', 5.00, 'filter oil air fuel cabin'),
        array('8511', 'Electrical ignition equipment', '85', 'Electrical', 5.00, 'alternator starter ignition spark plug'),
        array('8708', 'Parts for motor vehicles', '87', 'Vehicles', 5.00, 'brake suspension body bumper fender'),
        array('8471', 'Automatic data processing machines', '84', 'Machinery', 5.00, 'computer laptop tablet monitor'),
        array('6109', 'T-shirts, singlets and vests', '61', 'Textiles', 5.00, 'shirt t-shirt vest singlet'),
        array('6403', 'Footwear with outer soles of rubber', '64', 'Footwear', 5.00, 'shoe boot sneaker sandal'),
        array('7113', 'Articles of jewellery', '71', 'Precious metals', 5.00, 'gold silver ring necklace bracelet earring'),
        array('9101', 'Wrist-watches', '91', 'Clocks/watches', 5.00, 'watch wristwatch chronograph timepiece'),
        array('8544', 'Insulated wire and cable', '85', 'Electrical', 5.00, 'wire cable wiring harness connector'),
    );

    $inserted = 0;
    foreach ($codes as $c) {
        $st = $pdo->prepare("INSERT IGNORE INTO `epc_hs_codes` (`code`, `description`, `chapter`, `section`, `duty_rate`, `keywords`) VALUES (?, ?, ?, ?, ?, ?)");
        $st->execute($c);
        $inserted += $st->rowCount();
    }

    return $inserted;
}

/* ─── classification stats ─── */

function epc_ai_class_stats(PDO $pdo, string $siteKey): array
{
    epc_ai_class_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT `category`, COUNT(*) AS `count`, AVG(`confidence`) AS `avg_confidence`,
               SUM(CASE WHEN `reviewed` = 1 THEN 1 ELSE 0 END) AS `reviewed`
        FROM `epc_ai_classifications`
        WHERE `site_key` = ?
        GROUP BY `category`
        ORDER BY `count` DESC
    ");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── fleet stats (BOS) ─── */

function epc_ai_class_fleet_stats(PDO $pdo): array
{
    epc_ai_class_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `site_key`,
               COUNT(*) AS `total_classified`,
               SUM(CASE WHEN `category` = 'Uncategorized' THEN 1 ELSE 0 END) AS `uncategorized`,
               AVG(`confidence`) AS `avg_confidence`,
               SUM(CASE WHEN `reviewed` = 1 THEN 1 ELSE 0 END) AS `reviewed`,
               MAX(`created_at`) AS `last_run`
        FROM `epc_ai_classifications`
        GROUP BY `site_key`
        ORDER BY `total_classified` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── review classification ─── */

function epc_ai_review(PDO $pdo, int $classificationId, string $category, string $subcategory, string $hsCode, int $reviewerId): bool
{
    $st = $pdo->prepare("
        UPDATE `epc_ai_classifications`
        SET `category` = ?, `subcategory` = ?, `hs_code` = ?, `reviewed` = 1, `reviewed_by` = ?, `method` = 'manual'
        WHERE `id` = ?
    ");
    return $st->execute(array($category, $subcategory, $hsCode, $reviewerId, $classificationId));
}
