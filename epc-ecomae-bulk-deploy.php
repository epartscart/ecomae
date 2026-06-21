<?php
/**
 * Bulk code deploy for ecomae (ecomae-only; epartscart storefront unaffected —
 * additive files only). Chunked tar.gz upload + filesystem extract into the
 * ecomae docroot(s). The per-file CloudPanel push deploy used a fixed file list
 * and never carried the bulk ERP/Super CP code, so this endpoint deploys the
 * full branch in one shot.
 *
 * Usage:
 *   POST ?token=...&mode=upload   index=<n> data=<b64chunk> [final=1]
 *   GET  ?token=...&mode=info
 *   GET  ?token=...&mode=deploy            (extracts into ecomae docroots)
 *   GET  ?token=...&mode=setup             (re-runs platform setup)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
@ini_set('memory_limit', '512M');
set_time_limit(0);

$tgz = '/tmp/ecomae_bulk_deploy.tgz';
$mode = (string) ($_GET['mode'] ?? $_POST['mode'] ?? '');

/** Resolve candidate docroots for ecomae (www first, then cp). */
function epc_bulk_docroots(): array
{
    $out = array();
    $candidates = array(
        'www' => array(
            '/home/ecomae/htdocs/www.ecomae.com',
        ),
        'cp' => array(
            '/home/ecomaecp/htdocs/cp.ecomae.com',
            '/home/ecomae/htdocs/cp.ecomae.com',
        ),
    );
    foreach ($candidates as $label => $paths) {
        foreach ($paths as $p) {
            if (is_dir($p)) {
                $out[$p] = $label;
                break;
            }
        }
    }
    return $out;
}

if ($mode === 'upload') {
    $idx = (int) ($_POST['index'] ?? -1);
    $data = (string) ($_POST['data'] ?? '');
    if ($idx === 0) {
        @unlink($tgz);
    }
    if ($idx < 0 || $data === '') {
        exit('Bad chunk idx=' . $idx . ' len=' . strlen($data) . "\n");
    }
    $bin = base64_decode($data, true);
    if ($bin === false) {
        exit("Bad base64\n");
    }
    file_put_contents($tgz, $bin, FILE_APPEND);
    $size = is_file($tgz) ? filesize($tgz) : 0;
    echo 'OK chunk ' . $idx . ' total ' . $size . "\n";
    if (!empty($_POST['final'])) {
        echo 'Upload complete: ' . $size . " bytes\n";
    }
    exit;
}

if ($mode === 'info') {
    if (!is_file($tgz)) {
        exit("No tarball uploaded yet\n");
    }
    echo 'Tarball: ' . filesize($tgz) . " bytes\n";
    $out = array();
    @exec('tar -tzf ' . escapeshellarg($tgz) . ' 2>&1 | wc -l', $out);
    echo 'Entries: ' . trim(implode('', $out)) . "\n";
    echo "Docroots:\n";
    foreach (epc_bulk_docroots() as $path => $label) {
        echo "  [{$label}] {$path}\n";
    }
    exit;
}

if ($mode === 'deploy') {
    if (!is_file($tgz)) {
        exit("No tarball uploaded\n");
    }
    // Integrity check of the archive before extracting anywhere.
    $chk = array();
    $code = 0;
    @exec('gzip -t ' . escapeshellarg($tgz) . ' 2>&1', $chk, $code);
    if ($code !== 0) {
        exit("Corrupt tarball (gzip -t failed): " . implode("\n", $chk) . "\n");
    }
    $roots = epc_bulk_docroots();
    if (!$roots) {
        exit("No ecomae docroot found on this host\n");
    }
    foreach ($roots as $path => $label) {
        $o = array();
        $c = 0;
        @exec('tar -xzf ' . escapeshellarg($tgz) . ' -C ' . escapeshellarg($path) . ' 2>&1', $o, $c);
        if ($c === 0) {
            echo "Extracted into [{$label}] {$path}\n";
        } else {
            echo "FAILED extract into [{$label}] {$path}: " . implode(' ', $o) . "\n";
        }
    }
    // Spot-check a few new files landed (path + mtime).
    $checks = array(
        'content/shop/finance/epc_erp_enterprise.php',
        'content/shop/finance/epc_country_profile.php',
        'content/shop/finance/epc_erp_theme.php',
        'content/shop/finance/epc_demo_portal.php',
    );
    $primary = array_keys($roots)[0];
    echo "\nSpot check in {$primary}:\n";
    foreach ($checks as $rel) {
        $full = $primary . '/' . $rel;
        echo '  ' . (is_file($full) ? 'OK   ' . date('Y-m-d H:i:s', (int) filemtime($full)) : 'MISS ') . ' ' . $rel . "\n";
    }
    exit;
}

if ($mode === 'setup') {
    $token = epc_deploy_token();
    foreach (array(
        'https://www.ecomae.com/epc-ecomae-setup.php?token=' . urlencode($token),
        'https://www.ecomae.com/epc-ecomae-platform-check.php?token=' . urlencode($token),
    ) as $url) {
        $body = @file_get_contents($url, false, stream_context_create(array(
            'http' => array('timeout' => 180),
            'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
        )));
        echo "\n=== {$url} ===\n" . substr((string) $body, 0, 3000) . "\n";
    }
    exit;
}

echo "epc-ecomae-bulk-deploy: specify ?mode=upload|info|deploy|setup\n";
