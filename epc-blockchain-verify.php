<?php
/**
 * Public Blockchain BOS proof verification endpoint.
 *
 *   GET /epc-blockchain-verify.php?proof=prf_xxx
 *   GET /epc-blockchain-verify.php?hash=<sha256>
 *
 * Returns JSON. No auth required — only proof metadata is exposed (no private payload body).
 */

declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');

require_once __DIR__ . '/content/general_pages/epc_blockchain_bos.php';
epc_bc_bos_register_job_handlers();

$key = trim((string)($_GET['proof'] ?? $_GET['hash'] ?? $_GET['id'] ?? ''));
if ($key === '') {
    http_response_code(400);
    echo json_encode([
        'ok' => false,
        'error' => 'Provide ?proof=<proof_uid> or ?hash=<sha256>',
        'product' => epc_bc_bos_product_name(),
    ], JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

$result = epc_bc_bos_verify($key);
if (empty($result['ok'])) {
    http_response_code(503);
}
echo json_encode($result, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
