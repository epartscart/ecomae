<?php
/**
 * World-language + RTL/LTR layer (shared by storefront, tenant CP, Super CP, ERP).
 *
 * Strategy (honest, full world coverage):
 *   1. A comprehensive language table (~120 languages) with native name and
 *      writing direction. RTL languages (Arabic, Hebrew, Persian, Urdu, Pashto,
 *      Sindhi, Uyghur, Yiddish, Kurdish-Sorani, Divehi…) flip the WHOLE layout.
 *   2. Curated built-in dictionaries for the major languages → first-class,
 *      instant, correct UI strings.
 *   3. Google Translate widget as automatic fallback for every other language,
 *      so a user is never stuck (built-in dictionary always wins when present).
 *   4. Country → default-language map so the right language is pre-selected by
 *      the tenant's / visitor's country; the user can override (saved per user).
 *
 * Pure functions (no DB), so they are unit-testable and reusable everywhere.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_i18n_rtl_codes')) {
    /** @return array<int,string> ISO-639-1/3 codes that are written right-to-left. */
    function epc_i18n_rtl_codes(): array
    {
        return array('ar', 'he', 'fa', 'ur', 'ps', 'sd', 'ug', 'yi', 'ckb', 'dv', 'arc', 'ku');
    }
}

if (!function_exists('epc_i18n_languages')) {
    /**
     * Comprehensive language table. Each entry: native name + direction.
     * (Built-in dictionaries exist for the majors; the rest fall back to
     * Google Translate but still flip correctly for RTL.)
     *
     * @return array<string,array{name:string,native:string,dir:string}>
     */
    function epc_i18n_languages(): array
    {
        static $langs = null;
        if ($langs !== null) {
            return $langs;
        }
        $rtl = array_flip(epc_i18n_rtl_codes());
        // code => [English name, native name]
        $base = array(
            'en' => array('English', 'English'),
            'ar' => array('Arabic', 'العربية'),
            'zh' => array('Chinese', '中文'),
            'hi' => array('Hindi', 'हिन्दी'),
            'es' => array('Spanish', 'Español'),
            'fr' => array('French', 'Français'),
            'ru' => array('Russian', 'Русский'),
            'pt' => array('Portuguese', 'Português'),
            'de' => array('German', 'Deutsch'),
            'ja' => array('Japanese', '日本語'),
            'ko' => array('Korean', '한국어'),
            'it' => array('Italian', 'Italiano'),
            'tr' => array('Turkish', 'Türkçe'),
            'ur' => array('Urdu', 'اردو'),
            'fa' => array('Persian', 'فارسی'),
            'he' => array('Hebrew', 'עברית'),
            'bn' => array('Bengali', 'বাংলা'),
            'pa' => array('Punjabi', 'ਪੰਜਾਬੀ'),
            'id' => array('Indonesian', 'Bahasa Indonesia'),
            'ms' => array('Malay', 'Bahasa Melayu'),
            'nl' => array('Dutch', 'Nederlands'),
            'pl' => array('Polish', 'Polski'),
            'uk' => array('Ukrainian', 'Українська'),
            'vi' => array('Vietnamese', 'Tiếng Việt'),
            'th' => array('Thai', 'ไทย'),
            'sw' => array('Swahili', 'Kiswahili'),
            'tl' => array('Tagalog', 'Tagalog'),
            'ta' => array('Tamil', 'தமிழ்'),
            'te' => array('Telugu', 'తెలుగు'),
            'mr' => array('Marathi', 'मराठी'),
            'gu' => array('Gujarati', 'ગુજરાતી'),
            'kn' => array('Kannada', 'ಕನ್ನಡ'),
            'ml' => array('Malayalam', 'മലയാളം'),
            'or' => array('Odia', 'ଓଡ଼ିଆ'),
            'ne' => array('Nepali', 'नेपाली'),
            'si' => array('Sinhala', 'සිංහල'),
            'my' => array('Burmese', 'မြန်မာ'),
            'km' => array('Khmer', 'ខ្មែរ'),
            'lo' => array('Lao', 'ລາວ'),
            'ps' => array('Pashto', 'پښتو'),
            'sd' => array('Sindhi', 'سنڌي'),
            'ckb' => array('Kurdish (Sorani)', 'کوردی'),
            'ku' => array('Kurdish (Kurmanji)', 'Kurdî'),
            'dv' => array('Divehi', 'ދިވެހި'),
            'ug' => array('Uyghur', 'ئۇيغۇرچە'),
            'yi' => array('Yiddish', 'ייִדיש'),
            'am' => array('Amharic', 'አማርኛ'),
            'ha' => array('Hausa', 'Hausa'),
            'yo' => array('Yoruba', 'Yorùbá'),
            'ig' => array('Igbo', 'Igbo'),
            'zu' => array('Zulu', 'isiZulu'),
            'xh' => array('Xhosa', 'isiXhosa'),
            'af' => array('Afrikaans', 'Afrikaans'),
            'so' => array('Somali', 'Soomaali'),
            'rw' => array('Kinyarwanda', 'Ikinyarwanda'),
            'mg' => array('Malagasy', 'Malagasy'),
            'el' => array('Greek', 'Ελληνικά'),
            'cs' => array('Czech', 'Čeština'),
            'sk' => array('Slovak', 'Slovenčina'),
            'hu' => array('Hungarian', 'Magyar'),
            'ro' => array('Romanian', 'Română'),
            'bg' => array('Bulgarian', 'Български'),
            'sr' => array('Serbian', 'Српски'),
            'hr' => array('Croatian', 'Hrvatski'),
            'bs' => array('Bosnian', 'Bosanski'),
            'sl' => array('Slovenian', 'Slovenščina'),
            'mk' => array('Macedonian', 'Македонски'),
            'sq' => array('Albanian', 'Shqip'),
            'lt' => array('Lithuanian', 'Lietuvių'),
            'lv' => array('Latvian', 'Latviešu'),
            'et' => array('Estonian', 'Eesti'),
            'fi' => array('Finnish', 'Suomi'),
            'sv' => array('Swedish', 'Svenska'),
            'no' => array('Norwegian', 'Norsk'),
            'da' => array('Danish', 'Dansk'),
            'is' => array('Icelandic', 'Íslenska'),
            'ga' => array('Irish', 'Gaeilge'),
            'cy' => array('Welsh', 'Cymraeg'),
            'eu' => array('Basque', 'Euskara'),
            'ca' => array('Catalan', 'Català'),
            'gl' => array('Galician', 'Galego'),
            'mt' => array('Maltese', 'Malti'),
            'ka' => array('Georgian', 'ქართული'),
            'hy' => array('Armenian', 'Հայերեն'),
            'az' => array('Azerbaijani', 'Azərbaycan'),
            'kk' => array('Kazakh', 'Қазақ'),
            'ky' => array('Kyrgyz', 'Кыргызча'),
            'uz' => array('Uzbek', 'Oʻzbek'),
            'tg' => array('Tajik', 'Тоҷикӣ'),
            'tk' => array('Turkmen', 'Türkmen'),
            'mn' => array('Mongolian', 'Монгол'),
            'bo' => array('Tibetan', 'བོད་སྐད'),
            'as' => array('Assamese', 'অসমীয়া'),
            'sa' => array('Sanskrit', 'संस्कृतम्'),
            'su' => array('Sundanese', 'Basa Sunda'),
            'jv' => array('Javanese', 'Basa Jawa'),
            'ceb' => array('Cebuano', 'Cebuano'),
            'haw' => array('Hawaiian', 'ʻŌlelo Hawaiʻi'),
            'mi' => array('Maori', 'Māori'),
            'sm' => array('Samoan', 'Gagana Samoa'),
            'fj' => array('Fijian', 'Vosa Vakaviti'),
            'to' => array('Tongan', 'Lea fakatonga'),
            'lb' => array('Luxembourgish', 'Lëtzebuergesch'),
            'fo' => array('Faroese', 'Føroyskt'),
            'ht' => array('Haitian Creole', 'Kreyòl Ayisyen'),
            'la' => array('Latin', 'Latina'),
            'eo' => array('Esperanto', 'Esperanto'),
            'co' => array('Corsican', 'Corsu'),
            'fy' => array('Frisian', 'Frysk'),
            'gd' => array('Scots Gaelic', 'Gàidhlig'),
            'hmn' => array('Hmong', 'Hmoob'),
            'ny' => array('Chichewa', 'Chichewa'),
            'st' => array('Sesotho', 'Sesotho'),
            'sn' => array('Shona', 'chiShona'),
            'tt' => array('Tatar', 'Татар'),
            'wo' => array('Wolof', 'Wolof'),
            'lg' => array('Luganda', 'Luganda'),
        );
        $langs = array();
        foreach ($base as $code => $pair) {
            $code = trim($code);
            $langs[$code] = array(
                'name' => $pair[0],
                'native' => $pair[1],
                'dir' => isset($rtl[$code]) ? 'rtl' : 'ltr',
            );
        }
        return $langs;
    }
}

if (!function_exists('epc_i18n_is_supported')) {
    function epc_i18n_is_supported(string $code): bool
    {
        $langs = epc_i18n_languages();
        return isset($langs[$code]);
    }
}

if (!function_exists('epc_i18n_is_rtl')) {
    function epc_i18n_is_rtl(string $code): bool
    {
        return in_array($code, epc_i18n_rtl_codes(), true);
    }
}

if (!function_exists('epc_i18n_dir')) {
    function epc_i18n_dir(string $code): string
    {
        return epc_i18n_is_rtl($code) ? 'rtl' : 'ltr';
    }
}

if (!function_exists('epc_i18n_html_attrs')) {
    /** Attributes for the <html> tag: dir + lang. Drives the full layout flip. */
    function epc_i18n_html_attrs(string $code): string
    {
        $code = epc_i18n_is_supported($code) ? $code : 'en';
        return 'lang="' . htmlspecialchars($code, ENT_QUOTES) . '" dir="' . epc_i18n_dir($code) . '"';
    }
}

if (!function_exists('epc_i18n_country_lang')) {
    /**
     * Default UI language for a country (ISO-3166 alpha-2). Falls back to 'en'.
     */
    function epc_i18n_country_lang(string $country): string
    {
        $map = array(
            'AE' => 'ar', 'SA' => 'ar', 'QA' => 'ar', 'KW' => 'ar', 'BH' => 'ar', 'OM' => 'ar',
            'EG' => 'ar', 'JO' => 'ar', 'IQ' => 'ar', 'LB' => 'ar', 'LY' => 'ar', 'DZ' => 'ar',
            'MA' => 'ar', 'TN' => 'ar', 'SD' => 'ar', 'YE' => 'ar', 'SY' => 'ar', 'PS' => 'ar',
            'IR' => 'fa', 'AF' => 'fa', 'IL' => 'he', 'PK' => 'ur', 'TR' => 'tr',
            'IN' => 'hi', 'BD' => 'bn', 'LK' => 'si', 'NP' => 'ne', 'MV' => 'dv',
            'CN' => 'zh', 'TW' => 'zh', 'HK' => 'zh', 'SG' => 'zh',
            'JP' => 'ja', 'KR' => 'ko', 'TH' => 'th', 'VN' => 'vi', 'MM' => 'my',
            'KH' => 'km', 'LA' => 'lo', 'ID' => 'id', 'MY' => 'ms', 'PH' => 'tl',
            'RU' => 'ru', 'UA' => 'uk', 'KZ' => 'kk', 'UZ' => 'uz', 'AZ' => 'az',
            'DE' => 'de', 'AT' => 'de', 'CH' => 'de', 'FR' => 'fr', 'BE' => 'fr',
            'ES' => 'es', 'MX' => 'es', 'AR' => 'es', 'CO' => 'es', 'CL' => 'es', 'PE' => 'es',
            'PT' => 'pt', 'BR' => 'pt', 'IT' => 'it', 'NL' => 'nl', 'PL' => 'pl',
            'GR' => 'el', 'CZ' => 'cs', 'SK' => 'sk', 'HU' => 'hu', 'RO' => 'ro',
            'BG' => 'bg', 'RS' => 'sr', 'HR' => 'hr', 'SE' => 'sv', 'NO' => 'no',
            'DK' => 'da', 'FI' => 'fi', 'IS' => 'is', 'EE' => 'et', 'LV' => 'lv', 'LT' => 'lt',
            'KE' => 'sw', 'TZ' => 'sw', 'NG' => 'en', 'ZA' => 'en', 'ET' => 'am', 'SO' => 'so',
            'US' => 'en', 'GB' => 'en', 'CA' => 'en', 'AU' => 'en', 'NZ' => 'en', 'IE' => 'en',
        );
        $country = strtoupper(trim($country));
        return $map[$country] ?? 'en';
    }
}

if (!function_exists('epc_i18n_dictionaries')) {
    /**
     * Curated built-in UI strings for major languages. Keyed by language code,
     * then by string key. English is the canonical key set; other languages
     * provide overrides. Anything missing falls back to English/Google Translate.
     *
     * @return array<string,array<string,string>>
     */
    function epc_i18n_dictionaries(): array
    {
        static $d = null;
        if ($d !== null) {
            return $d;
        }
        $en = array(
            'dashboard' => 'Dashboard',
            'sales' => 'Sales',
            'purchases' => 'Purchases',
            'inventory' => 'Inventory',
            'customers' => 'Customers',
            'suppliers' => 'Suppliers',
            'products' => 'Products',
            'orders' => 'Orders',
            'invoice' => 'Invoice',
            'payment' => 'Payment',
            'reports' => 'Reports',
            'settings' => 'Settings',
            'finance' => 'Finance',
            'tax' => 'Tax',
            'cash' => 'Cash',
            'bank' => 'Bank',
            'total' => 'Total',
            'balance' => 'Balance',
            'paid' => 'Paid',
            'unpaid' => 'Unpaid',
            'save' => 'Save',
            'cancel' => 'Cancel',
            'search' => 'Search',
            'login' => 'Login',
            'logout' => 'Logout',
            'language' => 'Language',
            'welcome' => 'Welcome',
        );
        $d = array('en' => $en);

        $d['ar'] = array(
            'dashboard' => 'لوحة التحكم', 'sales' => 'المبيعات', 'purchases' => 'المشتريات',
            'inventory' => 'المخزون', 'customers' => 'العملاء', 'suppliers' => 'الموردون',
            'products' => 'المنتجات', 'orders' => 'الطلبات', 'invoice' => 'فاتورة',
            'payment' => 'الدفع', 'reports' => 'التقارير', 'settings' => 'الإعدادات',
            'finance' => 'المالية', 'tax' => 'الضريبة', 'cash' => 'النقد', 'bank' => 'البنك',
            'total' => 'الإجمالي', 'balance' => 'الرصيد', 'paid' => 'مدفوع', 'unpaid' => 'غير مدفوع',
            'save' => 'حفظ', 'cancel' => 'إلغاء', 'search' => 'بحث', 'login' => 'تسجيل الدخول',
            'logout' => 'تسجيل الخروج', 'language' => 'اللغة', 'welcome' => 'مرحباً',
        );
        $d['hi'] = array(
            'dashboard' => 'डैशबोर्ड', 'sales' => 'बिक्री', 'purchases' => 'खरीद',
            'inventory' => 'इन्वेंटरी', 'customers' => 'ग्राहक', 'suppliers' => 'आपूर्तिकर्ता',
            'products' => 'उत्पाद', 'orders' => 'ऑर्डर', 'invoice' => 'चालान', 'payment' => 'भुगतान',
            'reports' => 'रिपोर्ट', 'settings' => 'सेटिंग्स', 'finance' => 'वित्त', 'tax' => 'कर',
            'cash' => 'नकद', 'bank' => 'बैंक', 'total' => 'कुल', 'balance' => 'शेष',
            'paid' => 'भुगतान किया', 'unpaid' => 'अवैतनिक', 'save' => 'सहेजें', 'cancel' => 'रद्द करें',
            'search' => 'खोजें', 'login' => 'लॉग इन', 'logout' => 'लॉग आउट', 'language' => 'भाषा',
            'welcome' => 'स्वागत है',
        );
        $d['ur'] = array(
            'dashboard' => 'ڈیش بورڈ', 'sales' => 'فروخت', 'purchases' => 'خریداری',
            'inventory' => 'انوینٹری', 'customers' => 'گاہک', 'suppliers' => 'سپلائرز',
            'products' => 'مصنوعات', 'orders' => 'آرڈرز', 'invoice' => 'انوائس', 'payment' => 'ادائیگی',
            'reports' => 'رپورٹس', 'settings' => 'ترتیبات', 'finance' => 'مالیات', 'tax' => 'ٹیکس',
            'cash' => 'نقد', 'bank' => 'بینک', 'total' => 'کل', 'balance' => 'بیلنس',
            'paid' => 'ادا شدہ', 'unpaid' => 'غیر ادا شدہ', 'save' => 'محفوظ کریں', 'cancel' => 'منسوخ',
            'search' => 'تلاش', 'login' => 'لاگ ان', 'logout' => 'لاگ آؤٹ', 'language' => 'زبان',
            'welcome' => 'خوش آمدید',
        );
        $d['zh'] = array(
            'dashboard' => '仪表板', 'sales' => '销售', 'purchases' => '采购', 'inventory' => '库存',
            'customers' => '客户', 'suppliers' => '供应商', 'products' => '产品', 'orders' => '订单',
            'invoice' => '发票', 'payment' => '付款', 'reports' => '报告', 'settings' => '设置',
            'finance' => '财务', 'tax' => '税', 'cash' => '现金', 'bank' => '银行', 'total' => '合计',
            'balance' => '余额', 'paid' => '已付', 'unpaid' => '未付', 'save' => '保存',
            'cancel' => '取消', 'search' => '搜索', 'login' => '登录', 'logout' => '登出',
            'language' => '语言', 'welcome' => '欢迎',
        );
        $d['es'] = array(
            'dashboard' => 'Panel', 'sales' => 'Ventas', 'purchases' => 'Compras',
            'inventory' => 'Inventario', 'customers' => 'Clientes', 'suppliers' => 'Proveedores',
            'products' => 'Productos', 'orders' => 'Pedidos', 'invoice' => 'Factura',
            'payment' => 'Pago', 'reports' => 'Informes', 'settings' => 'Configuración',
            'finance' => 'Finanzas', 'tax' => 'Impuesto', 'cash' => 'Efectivo', 'bank' => 'Banco',
            'total' => 'Total', 'balance' => 'Saldo', 'paid' => 'Pagado', 'unpaid' => 'No pagado',
            'save' => 'Guardar', 'cancel' => 'Cancelar', 'search' => 'Buscar', 'login' => 'Acceder',
            'logout' => 'Salir', 'language' => 'Idioma', 'welcome' => 'Bienvenido',
        );
        $d['fr'] = array(
            'dashboard' => 'Tableau de bord', 'sales' => 'Ventes', 'purchases' => 'Achats',
            'inventory' => 'Inventaire', 'customers' => 'Clients', 'suppliers' => 'Fournisseurs',
            'products' => 'Produits', 'orders' => 'Commandes', 'invoice' => 'Facture',
            'payment' => 'Paiement', 'reports' => 'Rapports', 'settings' => 'Paramètres',
            'finance' => 'Finance', 'tax' => 'Taxe', 'cash' => 'Espèces', 'bank' => 'Banque',
            'total' => 'Total', 'balance' => 'Solde', 'paid' => 'Payé', 'unpaid' => 'Impayé',
            'save' => 'Enregistrer', 'cancel' => 'Annuler', 'search' => 'Rechercher',
            'login' => 'Connexion', 'logout' => 'Déconnexion', 'language' => 'Langue',
            'welcome' => 'Bienvenue',
        );
        $d['ru'] = array(
            'dashboard' => 'Панель', 'sales' => 'Продажи', 'purchases' => 'Закупки',
            'inventory' => 'Склад', 'customers' => 'Клиенты', 'suppliers' => 'Поставщики',
            'products' => 'Товары', 'orders' => 'Заказы', 'invoice' => 'Счёт', 'payment' => 'Оплата',
            'reports' => 'Отчёты', 'settings' => 'Настройки', 'finance' => 'Финансы', 'tax' => 'Налог',
            'cash' => 'Наличные', 'bank' => 'Банк', 'total' => 'Итого', 'balance' => 'Баланс',
            'paid' => 'Оплачено', 'unpaid' => 'Не оплачено', 'save' => 'Сохранить', 'cancel' => 'Отмена',
            'search' => 'Поиск', 'login' => 'Вход', 'logout' => 'Выход', 'language' => 'Язык',
            'welcome' => 'Добро пожаловать',
        );
        $d['fa'] = array(
            'dashboard' => 'داشبورد', 'sales' => 'فروش', 'purchases' => 'خریدها',
            'inventory' => 'موجودی', 'customers' => 'مشتریان', 'suppliers' => 'تأمین‌کنندگان',
            'products' => 'محصولات', 'orders' => 'سفارش‌ها', 'invoice' => 'فاکتور', 'payment' => 'پرداخت',
            'reports' => 'گزارش‌ها', 'settings' => 'تنظیمات', 'finance' => 'مالی', 'tax' => 'مالیات',
            'cash' => 'نقد', 'bank' => 'بانک', 'total' => 'مجموع', 'balance' => 'مانده',
            'paid' => 'پرداخت‌شده', 'unpaid' => 'پرداخت‌نشده', 'save' => 'ذخیره', 'cancel' => 'لغو',
            'search' => 'جستجو', 'login' => 'ورود', 'logout' => 'خروج', 'language' => 'زبان',
            'welcome' => 'خوش آمدید',
        );
        $d['he'] = array(
            'dashboard' => 'לוח בקרה', 'sales' => 'מכירות', 'purchases' => 'רכש',
            'inventory' => 'מלאי', 'customers' => 'לקוחות', 'suppliers' => 'ספקים',
            'products' => 'מוצרים', 'orders' => 'הזמנות', 'invoice' => 'חשבונית', 'payment' => 'תשלום',
            'reports' => 'דוחות', 'settings' => 'הגדרות', 'finance' => 'כספים', 'tax' => 'מס',
            'cash' => 'מזומן', 'bank' => 'בנק', 'total' => 'סה"כ', 'balance' => 'יתרה',
            'paid' => 'שולם', 'unpaid' => 'לא שולם', 'save' => 'שמור', 'cancel' => 'ביטול',
            'search' => 'חיפוש', 'login' => 'כניסה', 'logout' => 'יציאה', 'language' => 'שפה',
            'welcome' => 'ברוך הבא',
        );
        $d['de'] = array(
            'dashboard' => 'Dashboard', 'sales' => 'Verkauf', 'purchases' => 'Einkauf',
            'inventory' => 'Lager', 'customers' => 'Kunden', 'suppliers' => 'Lieferanten',
            'products' => 'Produkte', 'orders' => 'Bestellungen', 'invoice' => 'Rechnung',
            'payment' => 'Zahlung', 'reports' => 'Berichte', 'settings' => 'Einstellungen',
            'finance' => 'Finanzen', 'tax' => 'Steuer', 'cash' => 'Bargeld', 'bank' => 'Bank',
            'total' => 'Gesamt', 'balance' => 'Saldo', 'paid' => 'Bezahlt', 'unpaid' => 'Unbezahlt',
            'save' => 'Speichern', 'cancel' => 'Abbrechen', 'search' => 'Suchen', 'login' => 'Anmelden',
            'logout' => 'Abmelden', 'language' => 'Sprache', 'welcome' => 'Willkommen',
        );
        $d['pt'] = array(
            'dashboard' => 'Painel', 'sales' => 'Vendas', 'purchases' => 'Compras',
            'inventory' => 'Inventário', 'customers' => 'Clientes', 'suppliers' => 'Fornecedores',
            'products' => 'Produtos', 'orders' => 'Pedidos', 'invoice' => 'Fatura', 'payment' => 'Pagamento',
            'reports' => 'Relatórios', 'settings' => 'Configurações', 'finance' => 'Finanças', 'tax' => 'Imposto',
            'cash' => 'Dinheiro', 'bank' => 'Banco', 'total' => 'Total', 'balance' => 'Saldo',
            'paid' => 'Pago', 'unpaid' => 'Não pago', 'save' => 'Salvar', 'cancel' => 'Cancelar',
            'search' => 'Pesquisar', 'login' => 'Entrar', 'logout' => 'Sair', 'language' => 'Idioma',
            'welcome' => 'Bem-vindo',
        );
        return $d;
    }
}

if (!function_exists('epc_i18n_t')) {
    /**
     * Translate a key into the target language using built-in dictionaries.
     * Falls back to English, then to a humanized key. (Languages without a
     * built-in dictionary are handled client-side by the Google Translate
     * widget, which translates the rendered English.)
     */
    function epc_i18n_t(string $key, string $lang = 'en', string $default = ''): string
    {
        $dicts = epc_i18n_dictionaries();
        if (isset($dicts[$lang][$key])) {
            return $dicts[$lang][$key];
        }
        if (isset($dicts['en'][$key])) {
            return $dicts['en'][$key];
        }
        if ($default !== '') {
            return $default;
        }
        return ucfirst(str_replace('_', ' ', $key));
    }
}

if (!function_exists('epc_i18n_has_builtin')) {
    /** Whether a language has a curated built-in dictionary (vs Google fallback). */
    function epc_i18n_has_builtin(string $lang): bool
    {
        $dicts = epc_i18n_dictionaries();
        return isset($dicts[$lang]) && $lang !== 'en' ? true : ($lang === 'en');
    }
}

if (!function_exists('epc_i18n_resolve_lang')) {
    /**
     * Resolve the active language with priority:
     *   explicit user pref > cookie/session > country default > 'en'.
     *
     * @param array<string,mixed> $ctx {user_lang, cookie_lang, country}
     */
    function epc_i18n_resolve_lang(array $ctx): string
    {
        foreach (array('user_lang', 'cookie_lang') as $k) {
            if (!empty($ctx[$k]) && epc_i18n_is_supported((string) $ctx[$k])) {
                return (string) $ctx[$k];
            }
        }
        if (!empty($ctx['country'])) {
            $byCountry = epc_i18n_country_lang((string) $ctx['country']);
            if (epc_i18n_is_supported($byCountry)) {
                return $byCountry;
            }
        }
        return 'en';
    }
}

if (!function_exists('epc_i18n_google_widget')) {
    /**
     * Google Translate fallback widget markup for languages without a built-in
     * dictionary. Built-in dictionaries always take priority; this guarantees
     * full world coverage for everything else.
     */
    function epc_i18n_google_widget(string $lang = ''): string
    {
        $pl = $lang !== '' ? ",pageLanguage:'en',includedLanguages:''" : '';
        return "<div id=\"google_translate_element\"></div>\n"
            . "<script type=\"text/javascript\">\n"
            . "function googleTranslateElementInit(){new google.translate.TranslateElement({pageLanguage:'en'" . $pl . "},'google_translate_element');}\n"
            . "</script>\n"
            . "<script src=\"//translate.google.com/translate_a/element.js?cb=googleTranslateElementInit\"></script>";
    }
}

if (!function_exists('epc_i18n_rtl_css')) {
    /**
     * Minimal CSS that mirrors the layout for RTL languages. Loaded only when
     * the active language is RTL (the <html dir="rtl"> attribute does most of
     * the work; this fixes float/text-align/margins that don't auto-flip).
     */
    function epc_i18n_rtl_css(): string
    {
        return "html[dir=rtl]{text-align:right}"
            . "html[dir=rtl] .epc-row,html[dir=rtl] .epc-flex{flex-direction:row-reverse}"
            . "html[dir=rtl] .epc-left{float:right}html[dir=rtl] .epc-right{float:left}"
            . "html[dir=rtl] .epc-ml{margin-left:0;margin-right:auto}"
            . "html[dir=rtl] .epc-sidebar{right:0;left:auto}"
            . "html[dir=rtl] .epc-text-left{text-align:right}html[dir=rtl] .epc-text-right{text-align:left}"
            . "html[dir=rtl] table th,html[dir=rtl] table td{text-align:right}";
    }
}
