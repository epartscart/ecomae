<?php
/**
 * Register the public frontend ERP live-demo dashboard page (url: erp-demo).
 * Token-gated, additive, ecomae-only (runs against the docroot's own DB).
 *
 * Run: https://www.ecomae.com/epc-erp-demo-frontend-setup.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = isset($GLOBALS['DP_Config']) ? $GLOBALS['DP_Config'] : new DP_Config();

try {
    $pdo = new PDO(
        'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
        $cfg->user,
        $cfg->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
    );
} catch (PDOException $e) {
    http_response_code(500);
    exit(json_encode(array('status' => false, 'message' => 'DB connect failed')));
}

$url = 'erp-demo';
$alias = 'erp-demo';
$value = 'ERP live demo';
$phpPath = '/content/shop/finance/erp_demo_dashboard.php';
$now = time();

$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
$existing->execute(array($url));
$contentId = (int) $existing->fetchColumn();

if ($contentId > 0) {
    $pdo->prepare(
        'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 1, `system_flag` = 0,
         `content` = ?, `title_tag` = ?, `description_tag` = ?, `parent` = 0, `level` = 1, `alias` = ?, `value` = ?, `time_edited` = ?, `robots_tag` = \'\'
         WHERE `id` = ?'
    )->execute(array($phpPath, $value, $value, $alias, $value, $now, $contentId));
} else {
    $pdo->prepare(
        'INSERT INTO `content`
        (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
         `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
         `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
         VALUES (0, ?, 1, ?, ?, 0, ?, 1, \'php\', ?, ?, ?, \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 90)'
    )->execute(array(
        $url, $alias, $value, 'Public ERP live-demo dashboard (sample data)',
        $phpPath, $value, $value, $now, $now,
    ));
    $contentId = (int) $pdo->lastInsertId();
}

// Fully public: no content_access rows means all visitors (incl. guests) can view.
$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));

$base = isset($cfg->domain_path) ? rtrim((string) $cfg->domain_path, '/') : '';
echo json_encode(array(
    'status' => true,
    'message' => 'Frontend ERP demo page registered',
    'content_id' => $contentId,
    'url' => $url,
    'php' => $phpPath,
    'links' => array(
        'demo' => $base . '/erp-demo?demo=1',
        'demo_en' => $base . '/en/erp-demo?demo=1',
    ),
), JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
