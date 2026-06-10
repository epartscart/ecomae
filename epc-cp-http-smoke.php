<?php
/**
 * HTTP smoke: fetch CP pages using latest admin session cookie (user 5 / taxofin).
 * GET: token, key
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

$st = $db->prepare(
    'SELECT s.`session`, s.`2fa_session`, s.`user_id`, u.`email`
     FROM `sessions` s
     INNER JOIN `users` u ON u.`user_id` = s.`user_id`
     WHERE s.`type` = 1 AND s.`user_id` = 5
     ORDER BY s.`last_activiti_time` DESC LIMIT 1'
);
$st->execute();
$row = $st->fetch(PDO::FETCH_ASSOC);
if (!$row) {
    exit(json_encode(array('status' => false, 'message' => 'No admin session for user 5')));
}

$host = 'www.epartscart.com';
$backend = $DP_Config->backend_dir;
$cookie = 'admin_session=' . urlencode($row['session']) . '; admin_u_id=' . (int)$row['user_id'];
if (!empty($row['2fa_session'])) {
    $cookie .= '; 2fa=' . urlencode($row['2fa_session']);
}

$paths = array(
    '/' . $backend . '/shop/prices/guide',
    '/' . $backend . '/shop/prices/prices_edit',
    '/' . $backend . '/shop/prices',
);

$out = array('session' => array('email' => $row['email'], 'has_2fa' => !empty($row['2fa_session'])));
foreach ($paths as $path) {
    $url = 'https://' . $host . $path;
    $ch = curl_init($url);
    curl_setopt_array($ch, array(
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_FOLLOWLOCATION => true,
        CURLOPT_TIMEOUT => 45,
        CURLOPT_HTTPHEADER => array('Cookie: ' . $cookie),
        CURLOPT_SSL_VERIFYPEER => false,
    ));
    $html = curl_exec($ch);
    $code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);
    $hasSplash = stripos($html, 'splash-title') !== false;
    $hasLogin = stripos($html, 'Log in form') !== false || stripos($html, 'login_form') !== false;
    $has2fa = stripos($html, '2fa') !== false && stripos($html, 'verification') !== false;
    $hasGuide = stripos($html, 'Price upload') !== false || stripos($html, 'documentation') !== false;
    $hasPricesEdit = stripos($html, 'Edit price list records') !== false || stripos($html, 'show_table') !== false;
    $has403 = stripos($html, '403') !== false || stripos($html, 'Forbidden') !== false;
    $fatal = preg_match('/Fatal error|Parse error|Uncaught/i', $html) ? true : false;
    $out[$path] = array(
        'http' => $code,
        'bytes' => strlen($html),
        'splash_visible' => $hasSplash && stripos($html, "splash').css('display', 'none')") === false,
        'login_page' => $hasLogin,
        'likely_2fa' => $has2fa,
        'has_guide_content' => $hasGuide,
        'has_prices_edit_content' => $hasPricesEdit,
        'has_403' => $has403,
        'php_fatal' => $fatal,
        'title_snippet' => preg_match('/<title>([^<]+)</i', $html, $m) ? $m[1] : '',
    );
}

echo json_encode(array('status' => true, 'pages' => $out), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
