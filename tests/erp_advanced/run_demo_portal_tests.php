<?php
/**
 * CLI tests for the public 3-day demo portal (epc_demo_portal). No DB.
 *
 *   php tests/erp_advanced/run_demo_portal_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_demo.php';
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_demo_portal.php';

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

section('Demo credentials');
$c = epc_demo_credentials();
check('email + password present', !empty($c['email']) && !empty($c['password']));
check('correct credential accepted', epc_demo_check_credentials($c['email'], $c['password']));
check('case-insensitive email accepted', epc_demo_check_credentials(strtoupper($c['email']), $c['password']));
check('wrong password rejected', !epc_demo_check_credentials($c['email'], 'nope'));
check('wrong email rejected', !epc_demo_check_credentials('hacker@x.com', $c['password']));

section('3-day session lifecycle');
$t0 = 1_000_000_000;
$sess = epc_demo_issue_session($t0);
check('token issued', strpos($sess['token'], 'demo_') === 0);
check('scope = storefront + erp (no cp)', $sess['scope'] === array('storefront', 'erp'));
check('expires exactly 3 days later', $sess['expires_at'] === $t0 + (3 * 86400));
check('valid immediately', epc_demo_session_valid($sess, $t0));
check('valid at day 2', epc_demo_session_valid($sess, $t0 + 2 * 86400));
check('valid just before expiry', epc_demo_session_valid($sess, $t0 + 3 * 86400 - 1));
check('EXPIRED at day 3', !epc_demo_session_valid($sess, $t0 + 3 * 86400));
check('EXPIRED at day 4', !epc_demo_session_valid($sess, $t0 + 4 * 86400));
check('days remaining = 3 at start', epc_demo_days_remaining($sess, $t0) === 3);
check('days remaining = 1 on last day', epc_demo_days_remaining($sess, $t0 + 2 * 86400 + 100) === 1);
check('seconds remaining 0 after expiry', epc_demo_seconds_remaining($sess, $t0 + 5 * 86400) === 0);

section('Route scope: storefront + ERP allowed, CP denied');
check('home is storefront', epc_demo_classify_route('/') === 'storefront');
check('product page storefront', epc_demo_classify_route('/shop/product/123') === 'storefront');
check('ERP dashboard classified erp', epc_demo_classify_route('/cp/shop/finance/erp/dashboard') === 'erp');
check('ERP guide classified erp', epc_demo_classify_route('cp/shop/finance/erp/guide') === 'erp');
check('ERP without cp prefix still erp', epc_demo_classify_route('shop/finance/erp/dashboard') === 'erp');
check('public erp-demo classified erp', epc_demo_classify_route('/erp-demo') === 'erp');
check('public erp-demo allowed', epc_demo_route_allowed('/erp-demo?demo=1'));
check('tenant CP home classified cp', epc_demo_classify_route('/cp/shop/control/home') === 'cp');
check('usermanager CP classified cp', epc_demo_classify_route('cp/shop/usermanager/users') === 'cp');
check('bare /cp classified cp', epc_demo_classify_route('/cp') === 'cp');
check('storefront allowed', epc_demo_route_allowed('/shop/product/1'));
check('ERP allowed', epc_demo_route_allowed('/cp/shop/finance/erp/dashboard'));
check('CP denied', !epc_demo_route_allowed('/cp/shop/control/home'));

section('Guard decisions');
$g1 = epc_demo_guard($sess, '/cp/shop/finance/erp/dashboard', $t0);
check('valid+ERP -> allow', $g1['allow'] === true && $g1['reason'] === 'ok');
$g2 = epc_demo_guard($sess, '/cp/shop/control/home', $t0);
check('valid+CP -> deny, bounce to ERP', $g2['allow'] === false && $g2['reason'] === 'cp_blocked' && strpos($g2['redirect'], 'erp-demo') !== false);
$g3 = epc_demo_guard($sess, '/cp/shop/finance/erp/dashboard', $t0 + 10 * 86400);
check('expired -> deny regardless of route', $g3['allow'] === false && $g3['reason'] === 'expired');

section('Launch links + marketing HTML');
$links = epc_demo_launch_links('https://ecomae.com');
check('storefront link has demo flag', strpos($links['storefront'], 'demo=1') !== false);
check('erp link targets erp demo', strpos($links['erp'], 'erp-demo') !== false);
check('industry launch links built (5)', count($links['industries']) === 5);
check('industry erp link carries industry code', strpos($links['industries'][0]['erp'], 'industry=') !== false);
$html = epc_demo_portal_html('https://ecomae.com');
check('HTML shows demo email', strpos($html, 'demo@ecomae.com') !== false);
check('HTML shows 3-DAY badge', strpos($html, '3-DAY FREE DEMO') !== false);
check('HTML has Open ERP button', strpos($html, 'Open ERP demo') !== false);
check('HTML states CP not in demo', stripos($html, 'Control panel is not part of the demo') !== false);
check('HTML lists industry chips', substr_count($html, 'epc-demo-chip') >= 5);

echo "\n========================================\n";
echo "DEMO PORTAL TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
