<?php
/**
 * Fix Laximo catalog CMS page — update content path to the correct file.
 *
 * Usage: curl "https://www.epartscart.com/epc-fix-laximo-page.php?token=epartscart-deploy-2026"
 */
header('Content-Type: text/plain; charset=utf-8');

if (php_sapi_name() !== 'cli') {
    require_once __DIR__ . '/epc_deploy_auth.php';
    epc_deploy_require_token();
}

require_once __DIR__ . '/config.php';
$cfg = new DP_Config();

try {
    $db = new PDO(
        'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4',
        $cfg->user,
        $cfg->password,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
} catch (Exception $e) {
    exit("DB connection failed: " . $e->getMessage() . "\n");
}

// Find the existing CMS page
$stmt = $db->prepare("SELECT `id`, `url`, `content_type`, `content`, `value`, `title_tag` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1");
$stmt->execute(['katalog-laximo']);
$page = $stmt->fetch(PDO::FETCH_ASSOC);

if (!$page) {
    echo "No CMS page found for 'katalog-laximo'. Creating one...\n";
    $ins = $db->prepare("INSERT INTO `content` (`url`, `content_type`, `content`, `value`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `robots_tag`, `main_flag`, `published_flag`, `is_frontend`, `modules_array`, `css_js`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)");
    $ins->execute([
        'katalog-laximo',
        'php',
        '/content/laximo/index.php',
        'OEM Parts Catalog',
        'OEM Parts Catalog',
        'Search original OEM parts by vehicle brand, VIN, or part name.',
        'OEM parts, vehicle catalog, VIN search',
        '', '', 0, 1, 1, '[]', ''
    ]);
    echo "Created CMS page (id=" . $db->lastInsertId() . ") with content=/content/laximo/index.php\n";
} else {
    echo "Found CMS page:\n";
    echo "  id: " . $page['id'] . "\n";
    echo "  url: " . $page['url'] . "\n";
    echo "  content_type: " . $page['content_type'] . "\n";
    echo "  content: " . $page['content'] . "\n";
    echo "  value: " . $page['value'] . "\n";
    echo "  title_tag: " . $page['title_tag'] . "\n";

    $correctPath = '/content/laximo/index.php';
    $fullPath = $_SERVER['DOCUMENT_ROOT'] . $correctPath;

    echo "\nTarget file: " . $fullPath . "\n";
    echo "File exists: " . (file_exists($fullPath) ? 'YES' : 'NO') . "\n";

    if ($page['content'] !== $correctPath) {
        echo "\nUpdating content path from '" . $page['content'] . "' to '" . $correctPath . "'...\n";
        $upd = $db->prepare("UPDATE `content` SET `content` = ? WHERE `id` = ?");
        $upd->execute([$correctPath, $page['id']]);
        echo "Updated!\n";
    } else {
        echo "\nContent path is already correct.\n";

        // Check if the file actually exists
        $oldPath = $_SERVER['DOCUMENT_ROOT'] . $page['content'];
        echo "Checking path: " . $oldPath . "\n";
        echo "DOCUMENT_ROOT: " . $_SERVER['DOCUMENT_ROOT'] . "\n";

        // Try alternative paths
        $altPaths = [
            $_SERVER['DOCUMENT_ROOT'] . '/content/laximo/index.php',
            dirname(__DIR__) . '/content/laximo/index.php',
            __DIR__ . '/content/laximo/index.php',
        ];
        foreach ($altPaths as $alt) {
            echo "  " . $alt . " => " . (file_exists($alt) ? 'EXISTS' : 'missing') . "\n";
        }
    }
}

// Also verify laximo tables exist
try {
    $stmt = $db->query("SHOW TABLES LIKE 'epc_laximo%'");
    $tables = $stmt->fetchAll(PDO::FETCH_COLUMN);
    echo "\nLaximo tables: " . (empty($tables) ? 'NONE' : implode(', ', $tables)) . "\n";
} catch (Exception $e) {
    echo "Table check failed: " . $e->getMessage() . "\n";
}

echo "\nDone.\n";
