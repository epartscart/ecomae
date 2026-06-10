<?php
/**
 * Diagnose CP shop/prices/guide and shop/prices/prices_edit (routes, access, PHP render).
 * GET: token=epartscart-deploy-2026, key=tech_key
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
    http_response_code(403);
    exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
    http_response_code(403);
    exit(json_encode(array('status' => false, 'message' => 'Invalid key')));
}

$db = new PDO(
    'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
    $DP_Config->user,
    $DP_Config->password,
    array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$db->query('SET NAMES utf8');

$root = $_SERVER['DOCUMENT_ROOT'];
$backend = $DP_Config->backend_dir;

function epc_cp_diag_access(PDO $db, $contentId, $userId)
{
    $groups = array();
    $gq = $db->prepare('SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?');
    $gq->execute(array($userId));
    while ($g = $gq->fetchColumn()) {
        $groups[] = (int)$g;
    }
    $allowed = array();
    if ($contentId > 0) {
        $aq = $db->prepare('SELECT `group_id` FROM `content_access` WHERE `content_id` = ?');
        $aq->execute(array($contentId));
        while ($a = $aq->fetchColumn()) {
            $allowed[] = (int)$a;
        }
    }
    $ok = count($allowed) === 0;
    foreach ($groups as $gid) {
        if (in_array($gid, $allowed, true)) {
            $ok = true;
            break;
        }
    }
    return array('user_groups' => $groups, 'allowed_groups' => $allowed, 'access_ok' => $ok);
}

function epc_cp_diag_render($path, $DP_Config, $db, $userSession)
{
    if (!is_file($path)) {
        return array('ok' => false, 'error' => 'file missing', 'bytes' => 0);
    }
    define('_ASTEXE_', 1);
    $GLOBALS['DP_Config'] = $DP_Config;
    $GLOBALS['db_link'] = $db;
    $db_link = $db;
    $user_session = $userSession;
    ob_start();
    $err = null;
    try {
        include $path;
    } catch (Throwable $e) {
        $err = $e->getMessage();
    }
    $html = ob_get_clean();
    return array(
        'ok' => $err === null && strlen($html) > 50,
        'error' => $err,
        'bytes' => strlen($html),
        'preview' => substr(strip_tags($html), 0, 200),
    );
}

$users = $db->query(
    "SELECT `user_id`, `email` FROM `users` WHERE `unlocked` = 1 AND (`email` LIKE '%yawer%' OR `email` = 'taxofin2025@gmail.com') LIMIT 5"
)->fetchAll(PDO::FETCH_ASSOC);

$routes = array();
foreach (array('shop/prices/guide', 'shop/prices/prices_edit', 'shop/prices') as $url) {
    $st = $db->prepare('SELECT `id`, `url`, `published_flag`, `content_type`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0');
    $st->execute(array($url));
    $routes[$url] = $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

$sessionRow = null;
$render = array();
if (!empty($users)) {
    $uid = (int)$users[0]['user_id'];
    $sq = $db->prepare('SELECT * FROM `sessions` WHERE `user_id` = ? AND `type` = 1 ORDER BY `last_activiti_time` DESC LIMIT 1');
    $sq->execute(array($uid));
    $sessionRow = $sq->fetch(PDO::FETCH_ASSOC);
    if ($sessionRow) {
        $guideId = $routes['shop/prices/guide'] ? (int)$routes['shop/prices/guide']['id'] : 0;
        $editId = $routes['shop/prices/prices_edit'] ? (int)$routes['shop/prices/prices_edit']['id'] : 0;
        $access = array(
            'guide' => epc_cp_diag_access($db, $guideId, $uid),
            'prices_edit' => epc_cp_diag_access($db, $editId, $uid),
        );
        $render = array(
            'guide' => epc_cp_diag_render(
                $root . '/' . $backend . '/content/shop/prices_upload/prices_guide_page.php',
                $DP_Config,
                $db,
                $sessionRow
            ),
            'prices_edit' => epc_cp_diag_render(
                $root . '/' . $backend . '/content/shop/prices_edit/prices.php',
                $DP_Config,
                $db,
                $sessionRow
            ),
        );
    }
}

$acPath = $root . '/' . $backend . '/plugins/access_control/control.php';
$acSnippet = is_file($acPath) ? file_get_contents($acPath) : '';
$acHasEmptyAllow = strpos($acSnippet, 'count($allowed_groups) === 0') !== false;

echo json_encode(array(
    'status' => true,
    'users' => $users,
    'routes' => $routes,
    'session_found' => (bool)$sessionRow,
    'access' => isset($access) ? $access : null,
    'render' => $render,
    'access_control_empty_allow_fix' => $acHasEmptyAllow,
    'cp_plugins_access_mtime' => is_file($acPath) ? date('c', filemtime($acPath)) : null,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
