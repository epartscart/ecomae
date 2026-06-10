<?php
declare(strict_types=1);

$kind = isset($_GET['kind']) ? strtolower((string)$_GET['kind']) : '';
$id = (int)($_GET['id'] ?? 0);

if ($id < 1 || !in_array($kind, array('supplier', 'manufacturer'), true)) {
    http_response_code(400);
    header('Content-Type: text/plain; charset=utf-8');
    exit('Bad request');
}

$folder = $kind === 'manufacturer' ? 'MANUFACTURERS' : 'SUPPLIERS';
$url = 'https://image.umapi.ru/' . $folder . '/' . $id . '.png';

$context = stream_context_create(array(
    'http' => array(
        'timeout' => 10,
        'header' => "User-Agent: ePartsCart-UmapiImage/1.0\r\nAccept: image/*\r\n",
    ),
    'ssl' => array(
        'verify_peer' => true,
        'verify_peer_name' => true,
    ),
));

$data = @file_get_contents($url, false, $context);
if ($data === false || strlen($data) < 64) {
    http_response_code(404);
    header('Content-Type: text/plain; charset=utf-8');
    exit('Not found');
}

$type = 'image/png';
if (isset($http_response_header) && is_array($http_response_header)) {
    foreach ($http_response_header as $line) {
        if (stripos($line, 'Content-Type:') === 0) {
            $type = trim(substr($line, 13));
            break;
        }
    }
}

header('Content-Type: ' . $type);
header('Cache-Control: public, max-age=604800, immutable');
header('X-Content-Type-Options: nosniff');
echo $data;
