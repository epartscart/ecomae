<?php
declare(strict_types=1);
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
    exit('Forbidden');
}
header('Content-Type: text/plain');
$root = __DIR__;
$dirs = ['content', 'templates', 'modules', 'cp', 'api', 'lib', 'license', 'plugins'];
$n = 0;
foreach ($dirs as $d) {
    $path = $root . '/' . $d;
    if (!is_dir($path)) {
        echo "skip missing $d\n";
        continue;
    }
    $it = new RecursiveIteratorIterator(
        new RecursiveDirectoryIterator($path, FilesystemIterator::SKIP_DOTS),
        RecursiveIteratorIterator::SELF_FIRST
    );
    foreach ($it as $item) {
        $mode = $item->isDir() ? 0755 : 0644;
        if (@chmod($item->getPathname(), $mode)) {
            $n++;
        }
    }
}
@chmod($root . '/index.php', 0644);
@chmod($root . '/config.php', 0640);
echo "chmod ok on $n paths\n";
