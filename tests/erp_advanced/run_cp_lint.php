<?php
/**
 * CP render-safety lint (regression guard for the dp_core eval HTTP 500).
 *
 * `content_type='php'` CP pages are read as raw text and executed via
 *   eval(" ?>" . $html . "<?php ")
 * in dp_core. If a page's raw text ends while still in PHP mode (its last
 * `<?php` has no following `?>`), the framework's trailing template HTML (`<`)
 * breaks the eval parser → "unexpected token '<'" → HTTP 500.
 *
 * This lint scans CP content pages and FAILS if any page ends in open PHP
 * mode, so the class of bug we fixed cannot silently return.
 *
 *   php tests/erp_advanced/run_cp_lint.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

$root = dirname(__DIR__, 2);
$dir = $root . '/cp/content';

$pass_count = 0;
$fail_count = 0;
$bad = array();

/** True if the raw PHP file ends while still in PHP mode (no closing ?>). */
function ends_in_php_mode(string $src): bool
{
    // Position of the last PHP open tag and the last close tag.
    $lastOpen = max(strrpos($src, '<?php') === false ? -1 : strrpos($src, '<?php'),
        strrpos($src, '<?=') === false ? -1 : strrpos($src, '<?='));
    $lastClose = strrpos($src, '?>');
    if ($lastOpen < 0) {
        return false; // pure HTML page — fine
    }
    // In PHP mode at EOF when the last open tag comes after the last close tag.
    return $lastClose === false || $lastClose < $lastOpen;
}

$rii = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($dir, FilesystemIterator::SKIP_DOTS));
$scanned = 0;
foreach ($rii as $file) {
    if ($file->getExtension() !== 'php') {
        continue;
    }
    $path = $file->getPathname();
    // Only CP "content" pages are raw-eval'd; helper includes are required
    // normally. The registered content pages follow the *_page.php convention.
    if (substr($path, -9) !== '_page.php') {
        continue;
    }
    $scanned++;
    $src = (string) file_get_contents($path);
    if (ends_in_php_mode($src)) {
        $fail_count++;
        $bad[] = str_replace($root . '/', '', $path);
    } else {
        $pass_count++;
    }
}

echo "Scanned {$scanned} CP *_page.php files.\n";
if ($bad) {
    echo "\nPages ending in OPEN PHP mode (would crash dp_core eval):\n";
    foreach ($bad as $b) {
        echo "  FAIL  $b  — add a closing ?> at end of file\n";
    }
} else {
    echo "  PASS  every CP content page closes PHP mode (?> present)\n";
}

echo "\n========================================\n";
echo "CP LINT: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
