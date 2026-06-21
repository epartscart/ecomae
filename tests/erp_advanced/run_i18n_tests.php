<?php
/**
 * CLI tests for the world-language + RTL/LTR layer (epc_i18n).
 *
 *   php tests/erp_advanced/run_i18n_tests.php
 *
 * No DB required (pure functions).
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_i18n.php';

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

section('Language catalogue');
$langs = epc_i18n_languages();
check('large catalogue (>=100 languages)', count($langs) >= 100);
check('English present', isset($langs['en']) && $langs['en']['dir'] === 'ltr');
check('Arabic present with native name', isset($langs['ar']) && $langs['ar']['native'] === 'العربية');
check('Chinese native name', $langs['zh']['native'] === '中文');
check('every entry has name/native/dir', (function () use ($langs) {
    foreach ($langs as $l) {
        if (!isset($l['name'], $l['native'], $l['dir'])) {
            return false;
        }
        if ($l['dir'] !== 'rtl' && $l['dir'] !== 'ltr') {
            return false;
        }
    }
    return true;
})());

section('RTL / LTR direction (the "flip")');
check('Arabic is RTL', epc_i18n_is_rtl('ar') === true);
check('Hebrew is RTL', epc_i18n_is_rtl('he') === true);
check('Persian is RTL', epc_i18n_is_rtl('fa') === true);
check('Urdu is RTL', epc_i18n_is_rtl('ur') === true);
check('Pashto is RTL', epc_i18n_is_rtl('ps') === true);
check('Sindhi is RTL', epc_i18n_is_rtl('sd') === true);
check('English is LTR', epc_i18n_is_rtl('en') === false);
check('Hindi is LTR', epc_i18n_is_rtl('hi') === false);
check('Chinese is LTR', epc_i18n_is_rtl('zh') === false);
check('dir() returns rtl for ar', epc_i18n_dir('ar') === 'rtl');
check('dir() returns ltr for fr', epc_i18n_dir('fr') === 'ltr');
check('catalogue marks ar dir=rtl', $langs['ar']['dir'] === 'rtl');
check('catalogue marks ur dir=rtl', $langs['ur']['dir'] === 'rtl');

section('HTML attributes drive the page flip');
check('ar -> dir=rtl in html attrs', strpos(epc_i18n_html_attrs('ar'), 'dir="rtl"') !== false);
check('ar -> lang=ar', strpos(epc_i18n_html_attrs('ar'), 'lang="ar"') !== false);
check('en -> dir=ltr', strpos(epc_i18n_html_attrs('en'), 'dir="ltr"') !== false);
check('unknown lang falls back to en/ltr', strpos(epc_i18n_html_attrs('zzz'), 'dir="ltr"') !== false);
check('rtl css present for mirroring', strpos(epc_i18n_rtl_css(), "html[dir=rtl]") !== false);

section('Country -> default language');
check('AE -> Arabic', epc_i18n_country_lang('AE') === 'ar');
check('SA -> Arabic', epc_i18n_country_lang('SA') === 'ar');
check('IN -> Hindi', epc_i18n_country_lang('IN') === 'hi');
check('PK -> Urdu', epc_i18n_country_lang('PK') === 'ur');
check('IR -> Persian', epc_i18n_country_lang('IR') === 'fa');
check('IL -> Hebrew', epc_i18n_country_lang('IL') === 'he');
check('CN -> Chinese', epc_i18n_country_lang('CN') === 'zh');
check('FR -> French', epc_i18n_country_lang('FR') === 'fr');
check('lowercase country handled', epc_i18n_country_lang('ae') === 'ar');
check('unknown country -> en', epc_i18n_country_lang('XX') === 'en');

section('Built-in dictionaries (curated majors)');
check('Arabic dashboard translated', epc_i18n_t('dashboard', 'ar') === 'لوحة التحكم');
check('Hindi sales translated', epc_i18n_t('sales', 'hi') === 'बिक्री');
check('French invoice translated', epc_i18n_t('invoice', 'fr') === 'Facture');
check('Russian customers translated', epc_i18n_t('customers', 'ru') === 'Клиенты');
check('Chinese settings translated', epc_i18n_t('settings', 'zh') === '设置');
check('Urdu payment translated', epc_i18n_t('payment', 'ur') === 'ادائیگی');
check('English passthrough', epc_i18n_t('dashboard', 'en') === 'Dashboard');
check('missing key humanized', epc_i18n_t('some_new_key', 'ar') === 'Some new key');
check('missing lang falls back to English string', epc_i18n_t('dashboard', 'sw') === 'Dashboard');

section('Language resolution priority');
check('user pref wins', epc_i18n_resolve_lang(array('user_lang' => 'fr', 'cookie_lang' => 'de', 'country' => 'AE')) === 'fr');
check('cookie next', epc_i18n_resolve_lang(array('cookie_lang' => 'de', 'country' => 'AE')) === 'de');
check('country default next', epc_i18n_resolve_lang(array('country' => 'AE')) === 'ar');
check('empty -> en', epc_i18n_resolve_lang(array()) === 'en');
check('invalid user pref ignored, country used', epc_i18n_resolve_lang(array('user_lang' => 'zzz', 'country' => 'PK')) === 'ur');

section('Google Translate fallback (full world coverage)');
$w = epc_i18n_google_widget();
check('widget includes translate element', strpos($w, 'google_translate_element') !== false);
check('widget loads translate script', strpos($w, 'translate.google.com') !== false);
check('supported() true for catalogue lang', epc_i18n_is_supported('sw') === true);
check('supported() false for nonsense', epc_i18n_is_supported('zzz') === false);

echo "\n========================================\n";
echo "I18N TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
