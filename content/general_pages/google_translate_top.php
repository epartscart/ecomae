<?php
defined('_ASTEXE_') or die('No access');

$epc_cms_active_langs = array('en');
if (isset($db_link) && $db_link instanceof PDO) {
	try {
		$epc_cms_lang_q = $db_link->query('SELECT `lang_code` FROM `lang_languages` WHERE `active` = 1');
		if ($epc_cms_lang_q) {
			$epc_cms_active_langs = array();
			while ($epc_cms_lang_row = $epc_cms_lang_q->fetch(PDO::FETCH_ASSOC)) {
				$code = strtolower(trim((string) ($epc_cms_lang_row['lang_code'] ?? '')));
				if ($code !== '') {
					$epc_cms_active_langs[] = $code;
				}
			}
			if ($epc_cms_active_langs === array()) {
				$epc_cms_active_langs = array('en');
			}
		}
	} catch (Exception $e) {
		$epc_cms_active_langs = array('en', 'ar');
	}
}
$epc_cms_current_lang = isset($multilang_params['lang']) ? strtolower((string) $multilang_params['lang']) : 'en';
$epc_cf_country = strtoupper(trim((string) ($_SERVER['HTTP_CF_IPCOUNTRY'] ?? '')));
if ($epc_cf_country === 'XX' || $epc_cf_country === 'T1') {
	$epc_cf_country = '';
}
?>
<style>
	.epc-google-translate-top {
		position: relative;
		z-index: 10000;
		background: #ffffff;
		border-bottom: 1px solid #e5e5e5;
		padding: 6px 12px;
		text-align: right;
		min-height: 42px;
	}
	.epc-google-translate-top__inner {
		display: inline-flex;
		align-items: center;
		gap: 8px;
		max-width: 100%;
	}
	.epc-google-translate-top__label {
		color: #444;
		font-size: 13px;
		line-height: 1;
		white-space: nowrap;
	}
	.epc-google-translate-top__status {
		color: #667085;
		font-size: 12px;
		line-height: 1.2;
		max-width: 320px;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	.epc-native-translate-select {
		appearance: none;
		-webkit-appearance: none;
		background: #fff url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='11' height='7' viewBox='0 0 11 7'%3E%3Cpath fill='%23555' d='M5.5 7 0 0h11z'/%3E%3C/svg%3E") no-repeat right 10px center;
		border: 1px solid #d7d7d7;
		border-radius: 4px;
		color: #333;
		cursor: pointer;
		font-size: 13px;
		height: 32px;
		line-height: 32px;
		max-width: 240px;
		min-width: 185px;
		padding: 0 32px 0 10px;
		display: inline-block;
	}
	.epc-native-translate-select:focus {
		border-color: #5b9dd9;
		box-shadow: 0 0 0 2px rgba(91, 157, 217, 0.18);
		outline: none;
	}
	#google_translate_element {
		height: 0;
		left: -9999px;
		overflow: hidden;
		position: absolute;
		top: -9999px;
		width: 0;
	}
	#google_translate_element .goog-te-gadget {
		color: transparent;
		font-size: 0;
		line-height: 1;
	}
	#google_translate_element .goog-te-gadget span {
		display: none;
	}
	#google_translate_element .goog-te-combo {
		background: #fff;
		border: 1px solid #d7d7d7;
		border-radius: 4px;
		color: #333;
		cursor: pointer;
		font-size: 13px;
		height: 32px;
		margin: 0;
		max-width: 260px;
		min-width: 185px;
		padding: 0 8px;
	}
	@media (max-width: 767px) {
		.epc-google-translate-top {
			text-align: center;
			padding-left: 6px;
			padding-right: 6px;
		}
		.epc-google-translate-top__inner {
			justify-content: center;
		}
		.epc-google-translate-top__label {
			display: none;
		}
		.epc-google-translate-top__status {
			display: none;
		}
	}
</style>
<div id="epc_google_translate_root"
	 class="epc-google-translate-top notranslate"
	 translate="no"
	 data-cms-langs="<?php echo htmlspecialchars(implode(',', $epc_cms_active_langs), ENT_QUOTES, 'UTF-8'); ?>"
	 data-cms-lang="<?php echo htmlspecialchars($epc_cms_current_lang, ENT_QUOTES, 'UTF-8'); ?>"
	 data-cf-country="<?php echo htmlspecialchars($epc_cf_country, ENT_QUOTES, 'UTF-8'); ?>">
	<div class="epc-google-translate-top__inner">
		<span class="epc-google-translate-top__label">Language</span>
		<span id="epc_translate_auto_status" class="epc-google-translate-top__status">Auto language: checking location...</span>
		<select id="epc_native_translate_select" class="epc-native-translate-select notranslate" aria-label="Select language" translate="no">
			<option value="en" selected>English</option>
			<option value="af">Afrikaans</option>
			<option value="sq">Shqip</option>
			<option value="am">አማርኛ</option>
			<option value="ar">العربية</option>
			<option value="hy">Հայերեն</option>
			<option value="az">Azərbaycanca</option>
			<option value="eu">Euskara</option>
			<option value="be">Беларуская</option>
			<option value="bn">বাংলা</option>
			<option value="bs">Bosanski</option>
			<option value="bg">Български</option>
			<option value="ca">Català</option>
			<option value="ceb">Cebuano</option>
			<option value="ny">Chichewa</option>
			<option value="zh-CN">中文（简体）</option>
			<option value="zh-TW">中文（繁體）</option>
			<option value="co">Corsu</option>
			<option value="hr">Hrvatski</option>
			<option value="cs">Čeština</option>
			<option value="da">Dansk</option>
			<option value="nl">Nederlands</option>
			<option value="eo">Esperanto</option>
			<option value="et">Eesti</option>
			<option value="tl">Filipino</option>
			<option value="fi">Suomi</option>
			<option value="fr">Français</option>
			<option value="fy">Frysk</option>
			<option value="gl">Galego</option>
			<option value="ka">ქართული</option>
			<option value="de">Deutsch</option>
			<option value="el">Ελληνικά</option>
			<option value="gu">ગુજરાતી</option>
			<option value="ht">Kreyòl Ayisyen</option>
			<option value="ha">Hausa</option>
			<option value="haw">ʻŌlelo Hawaiʻi</option>
			<option value="iw">עברית</option>
			<option value="hi">हिन्दी</option>
			<option value="hmn">Hmoob</option>
			<option value="hu">Magyar</option>
			<option value="is">Íslenska</option>
			<option value="ig">Igbo</option>
			<option value="id">Indonesia</option>
			<option value="ga">Gaeilge</option>
			<option value="it">Italiano</option>
			<option value="ja">日本語</option>
			<option value="jw">Basa Jawa</option>
			<option value="kn">ಕನ್ನಡ</option>
			<option value="kk">Қазақша</option>
			<option value="km">ខ្មែរ</option>
			<option value="ko">한국어</option>
			<option value="ku">Kurdî</option>
			<option value="ky">Кыргызча</option>
			<option value="lo">ລາວ</option>
			<option value="la">Latina</option>
			<option value="lv">Latviešu</option>
			<option value="lt">Lietuvių</option>
			<option value="lb">Lëtzebuergesch</option>
			<option value="mk">Македонски</option>
			<option value="mg">Malagasy</option>
			<option value="ms">Melayu</option>
			<option value="ml">മലയാളം</option>
			<option value="mt">Malti</option>
			<option value="mi">Māori</option>
			<option value="mr">मराठी</option>
			<option value="mn">Монгол</option>
			<option value="my">မြန်မာ</option>
			<option value="ne">नेपाली</option>
			<option value="no">Norsk</option>
			<option value="ps">پښتو</option>
			<option value="fa">فارسی</option>
			<option value="pl">Polski</option>
			<option value="pt">Português</option>
			<option value="pa">ਪੰਜਾਬੀ</option>
			<option value="ro">Română</option>
			<option value="ru">Русский</option>
			<option value="sm">Gagana Samoa</option>
			<option value="gd">Gàidhlig</option>
			<option value="sr">Српски</option>
			<option value="st">Sesotho</option>
			<option value="sn">Shona</option>
			<option value="sd">سنڌي</option>
			<option value="si">සිංහල</option>
			<option value="sk">Slovenčina</option>
			<option value="sl">Slovenščina</option>
			<option value="so">Soomaali</option>
			<option value="es">Español</option>
			<option value="su">Basa Sunda</option>
			<option value="sw">Kiswahili</option>
			<option value="sv">Svenska</option>
			<option value="tg">Тоҷикӣ</option>
			<option value="ta">தமிழ்</option>
			<option value="te">తెలుగు</option>
			<option value="th">ไทย</option>
			<option value="tr">Türkçe</option>
			<option value="uk">Українська</option>
			<option value="ur">اردو</option>
			<option value="uz">Oʻzbekcha</option>
			<option value="vi">Tiếng Việt</option>
			<option value="cy">Cymraeg</option>
			<option value="xh">IsiXhosa</option>
			<option value="yi">ייִדיש</option>
			<option value="yo">Yorùbá</option>
			<option value="zu">IsiZulu</option>
		</select>
		<div id="google_translate_element"></div>
	</div>
</div>
<script>
	window.epcCmsActiveLangs = <?php echo json_encode(array_values($epc_cms_active_langs), JSON_UNESCAPED_UNICODE); ?>;
	window.epcCmsCurrentLang = <?php echo json_encode($epc_cms_current_lang, JSON_UNESCAPED_UNICODE); ?>;
</script>
<script src="/content/general_pages/epc_google_translate_storefront.js?v=20260811b" defer></script>
