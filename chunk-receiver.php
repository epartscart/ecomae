<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token(true);
header('Content-Type: text/plain; charset=utf-8');
@ini_set('memory_limit', '512M');

function deploy_copy_tree(string $src, string $dst): void
{
    $src = rtrim($src, '/\\');
    $it = new RecursiveIteratorIterator(
        new RecursiveDirectoryIterator($src, FilesystemIterator::SKIP_DOTS),
        RecursiveIteratorIterator::SELF_FIRST
    );
    foreach ($it as $item) {
        $rel = substr($item->getPathname(), strlen($src) + 1);
        $target = $dst . '/' . str_replace('\\', '/', $rel);
        if ($item->isDir()) {
            if (!is_dir($target)) {
                mkdir($target, 0755, true);
            }
        } else {
            $dir = dirname($target);
            if (!is_dir($dir)) {
                mkdir($dir, 0755, true);
            }
            copy($item->getPathname(), $target);
            @chmod($target, 0644);
        }
    }
}

$dest = '/tmp/docpart-epartscart-site.zip';
$idx = (int)($_POST['index'] ?? -1);
$data = (string)($_POST['data'] ?? '');
if ($idx === 0) {
    @unlink($dest);
}
if ($idx < 0 || $data === '') {
    exit('Bad chunk idx=' . $idx . ' len=' . strlen($data));
}
$bin = base64_decode($data, true);
if ($bin === false) {
    exit('Bad base64');
}
$written = file_put_contents($dest, $bin, FILE_APPEND);
$size = is_file($dest) ? filesize($dest) : 0;
echo 'OK chunk ' . $idx . ' wrote ' . $written . ' total ' . $size;

if (!empty($_POST['final'])) {
    echo "\nZip ready at " . $dest;
}
