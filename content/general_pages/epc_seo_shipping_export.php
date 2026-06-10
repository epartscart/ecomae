<?php
/**
 * Shipping & export content for epartscart regional SEO (/shipping-export).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_seo_indexing.php';

global $DP_Config, $db_link, $multilang_params;

$lang = function_exists('epc_seo_current_lang_code') ? epc_seo_current_lang_code() : 'en';
$langHref = function_exists('epc_seo_lang_href') ? epc_seo_lang_href() : '/en';
$tenantCountry = ($db_link instanceof PDO) ? epc_seo_tenant_country_code($db_link) : 'AE';
$shipPhrase = epc_seo_regional_shipping_phrase($lang);
$partsUrl = rtrim((string) $DP_Config->domain_path, '/') . $langHref . '/' . ($DP_Config->chpu_search_config['level_1']['url'] ?? 'parts');
$brandsUrl = rtrim((string) $DP_Config->domain_path, '/') . $langHref . '/available-brands';

$copy = array(
	'en' => array(
		'title' => 'Shipping & export',
		'lead' => 'eParts Cart ships from our UAE warehouse to GCC countries, Pakistan, and worldwide export destinations.',
		'gcc' => 'GCC delivery: UAE, Saudi Arabia, Oman, Qatar, Bahrain, and Kuwait.',
		'pk' => 'Pakistan: express and economy options for auto parts export.',
		'world' => 'Worldwide export: contact us for customs documentation and freight quotes.',
		'cta_parts' => 'Search parts',
		'cta_brands' => 'Browse brands',
	),
	'ar' => array(
		'title' => 'الشحن والتصدير',
		'lead' => 'يشحن eParts Cart من مستودعنا في الإمارات إلى دول الخليج وباكستان ووجهات التصدير حول العالم.',
		'gcc' => 'توصيل الخليج: الإمارات، السعودية، عُمان، قطر، البحرين، والكويت.',
		'pk' => 'باكستان: خيارات شحن سريع واقتصادي لتصدير قطع الغيار.',
		'world' => 'تصدير عالمي: تواصل معنا لوثائق الجمارك وعروض الشحن.',
		'cta_parts' => 'بحث القطع',
		'cta_brands' => 'الماركات',
	),
	'ru' => array(
		'title' => 'Доставка и экспорт',
		'lead' => 'eParts Cart отправляет со склада ОАЭ в страны GCC, Пакистан и по всему миру.',
		'gcc' => 'GCC: ОАЭ, Саудовская Аравия, Оман, Катар, Бахрейн, Кувейт.',
		'pk' => 'Пакистан: экспресс и экономичная доставка автозапчастей.',
		'world' => 'Мировой экспорт: таможенные документы и расчёт фрахта по запросу.',
		'cta_parts' => 'Поиск запчастей',
		'cta_brands' => 'Бренды',
	),
);
$t = $copy[$lang] ?? $copy['en'];
?>
<div class="epc-shipping-export" style="max-width:860px;margin:0 auto;padding:8px 0 24px;">
	<h1 style="margin:0 0 12px;font-size:28px;"><?php echo htmlspecialchars($t['title'], ENT_QUOTES, 'UTF-8'); ?></h1>
	<p style="font-size:16px;line-height:1.6;color:#333;"><?php echo htmlspecialchars($t['lead'], ENT_QUOTES, 'UTF-8'); ?></p>
	<p style="font-size:15px;line-height:1.6;color:#555;margin-top:12px;"><strong><?php echo htmlspecialchars($shipPhrase, ENT_QUOTES, 'UTF-8'); ?></strong></p>
	<ul style="margin:16px 0 0 18px;line-height:1.7;font-size:15px;">
		<li><?php echo htmlspecialchars($t['gcc'], ENT_QUOTES, 'UTF-8'); ?></li>
		<li><?php echo htmlspecialchars($t['pk'], ENT_QUOTES, 'UTF-8'); ?></li>
		<li><?php echo htmlspecialchars($t['world'], ENT_QUOTES, 'UTF-8'); ?></li>
	</ul>
	<p style="margin-top:18px;font-size:14px;color:#666;">Tenant country profile: <?php echo htmlspecialchars($tenantCountry, ENT_QUOTES, 'UTF-8'); ?> (Dubai warehouse).</p>
	<p style="margin-top:16px;">
		<a class="btn btn-ar btn-primary" href="<?php echo htmlspecialchars($partsUrl, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($t['cta_parts'], ENT_QUOTES, 'UTF-8'); ?></a>
		&nbsp;
		<a class="btn btn-default" href="<?php echo htmlspecialchars($brandsUrl, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($t['cta_brands'], ENT_QUOTES, 'UTF-8'); ?></a>
	</p>
</div>
