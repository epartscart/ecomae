<?php
/**
 * CLI tests for the central theme-token layer. No DB.
 *
 *   php tests/erp_advanced/run_theme_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_theme.php';

$pass_count = 0;
$fail_count = 0;
function check(string $label, bool $cond): void
{
    global $pass_count, $fail_count;
    if ($cond) {
        $pass_count++;
        echo "  PASS  $label\n";
    } else {
        $fail_count++;
        echo "  FAIL  $label\n";
    }
}
function section(string $t): void
{
    echo "\n== $t ==\n";
}

section('Presets exist');
$p = epc_theme_presets();
check('blue preset', isset($p['blue']) && $p['blue']['name'] === 'Blue & Black');
check('red preset', isset($p['red']) && $p['red']['name'] === 'Red & Black');
check('teal preset', isset($p['teal']));
check('each preset has accent + bg + glow', (function () use ($p) {
    foreach (array('blue', 'red', 'teal') as $k) {
        foreach (array('accent', 'accent2', 'bg0', 'glow', 'card_brd', 'text') as $f) {
            if (empty($p[$k][$f])) {
                return false;
            }
        }
    }
    return true;
})());

section('Blue & Black accents');
$blue = epc_theme_get('blue');
check('blue accent is a blue hex', strtolower($blue['accent']) === '#2f6dff');
check('blue base near-black', $blue['bg0'] === '#05070e');

section('Red & Black accents');
$red = epc_theme_get('red');
check('red accent is a red hex', strtolower($red['accent']) === '#ff2b4d');
check('red base near-black', $red['bg0'] === '#0a0608');

section('Resolution + fallback');
check('default is blue', epc_theme_default() === 'blue');
check('unknown key -> default', epc_theme_get('rainbow')['key'] === epc_theme_default());
check('case-insensitive', epc_theme_get('RED')['key'] === 'red');
check('resolve empty setting -> default', epc_theme_resolve_setting('') === 'blue');
check('resolve red setting -> red', epc_theme_resolve_setting('red') === 'red');
check('resolve junk -> default', epc_theme_resolve_setting('zzz') === 'blue');

section('CSS variable block');
$cssBlue = epc_theme_css_vars('blue');
check('sets generic --epc-accent', strpos($cssBlue, '--epc-accent:#2f6dff') !== false);
check('overrides ERP --erp-accent', strpos($cssBlue, '--erp-accent:#2f6dff') !== false);
check('overrides ERP bg', strpos($cssBlue, '--erp-bg-0:#05070e') !== false);
check('wrapped in :root', strpos($cssBlue, ':root{') === 0);
$cssRed = epc_theme_css_vars('red');
check('red sets --erp-accent red', strpos($cssRed, '--erp-accent:#ff2b4d') !== false);
check('blue and red css differ', $cssBlue !== $cssRed);

section('Style tag + swatch');
$tag = epc_theme_style_tag('red');
check('style tag has id', strpos($tag, 'id="epc-theme"') !== false);
check('style tag carries data-theme=red', strpos($tag, 'data-theme="red"') !== false);
check('style tag contains vars', strpos($tag, '--erp-accent:#ff2b4d') !== false);
$sw = epc_theme_swatch_html('blue');
check('swatch shows name', strpos($sw, 'Blue &amp; Black') !== false || strpos($sw, 'Blue & Black') !== false);
check('swatch carries data-theme', strpos($sw, 'data-theme="blue"') !== false);

echo "\n========================================\n";
echo "THEME TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
