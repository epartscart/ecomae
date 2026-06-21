<?php
/**
 * Advanced ERP — Worldwide multi-currency.
 *
 * - Full ISO-4217 currency catalog (code, name, symbol, minor-unit digits).
 * - Per-tenant config: base/home currency + enabled transacting currencies.
 * - Dated exchange-rate table (per currency pair) with as-of lookup.
 * - Conversion engine + realised/unrealised FX gain-loss on settlement.
 *
 * Additive: two new tables (config rows in epc_erp settings + a rate table).
 * Existing stored amounts are never altered; conversion is computed on read.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_ccy_catalog')) {
    /**
     * ISO-4217 catalog. Value = array(name, symbol, decimals).
     *
     * @return array<string,array{0:string,1:string,2:int}>
     */
    function epc_ccy_catalog(): array
    {
        return array(
            'AED' => array('UAE Dirham', 'د.إ', 2), 'AFN' => array('Afghani', '؋', 2),
            'ALL' => array('Lek', 'L', 2), 'AMD' => array('Armenian Dram', '֏', 2),
            'ANG' => array('Netherlands Antillean Guilder', 'ƒ', 2), 'AOA' => array('Kwanza', 'Kz', 2),
            'ARS' => array('Argentine Peso', '$', 2), 'AUD' => array('Australian Dollar', '$', 2),
            'AWG' => array('Aruban Florin', 'ƒ', 2), 'AZN' => array('Azerbaijan Manat', '₼', 2),
            'BAM' => array('Convertible Mark', 'KM', 2), 'BBD' => array('Barbados Dollar', '$', 2),
            'BDT' => array('Taka', '৳', 2), 'BGN' => array('Bulgarian Lev', 'лв', 2),
            'BHD' => array('Bahraini Dinar', '.د.ب', 3), 'BIF' => array('Burundi Franc', 'FBu', 0),
            'BMD' => array('Bermudian Dollar', '$', 2), 'BND' => array('Brunei Dollar', '$', 2),
            'BOB' => array('Boliviano', 'Bs.', 2), 'BRL' => array('Brazilian Real', 'R$', 2),
            'BSD' => array('Bahamian Dollar', '$', 2), 'BTN' => array('Ngultrum', 'Nu.', 2),
            'BWP' => array('Pula', 'P', 2), 'BYN' => array('Belarusian Ruble', 'Br', 2),
            'BZD' => array('Belize Dollar', '$', 2), 'CAD' => array('Canadian Dollar', '$', 2),
            'CDF' => array('Congolese Franc', 'FC', 2), 'CHF' => array('Swiss Franc', 'CHF', 2),
            'CLP' => array('Chilean Peso', '$', 0), 'CNY' => array('Yuan Renminbi', '¥', 2),
            'COP' => array('Colombian Peso', '$', 2), 'CRC' => array('Costa Rican Colon', '₡', 2),
            'CUP' => array('Cuban Peso', '$', 2), 'CVE' => array('Cabo Verde Escudo', '$', 2),
            'CZK' => array('Czech Koruna', 'Kč', 2), 'DJF' => array('Djibouti Franc', 'Fdj', 0),
            'DKK' => array('Danish Krone', 'kr', 2), 'DOP' => array('Dominican Peso', 'RD$', 2),
            'DZD' => array('Algerian Dinar', 'دج', 2), 'EGP' => array('Egyptian Pound', 'E£', 2),
            'ERN' => array('Nakfa', 'Nfk', 2), 'ETB' => array('Ethiopian Birr', 'Br', 2),
            'EUR' => array('Euro', '€', 2), 'FJD' => array('Fiji Dollar', '$', 2),
            'FKP' => array('Falkland Islands Pound', '£', 2), 'GBP' => array('Pound Sterling', '£', 2),
            'GEL' => array('Lari', '₾', 2), 'GHS' => array('Ghana Cedi', '₵', 2),
            'GIP' => array('Gibraltar Pound', '£', 2), 'GMD' => array('Dalasi', 'D', 2),
            'GNF' => array('Guinean Franc', 'FG', 0), 'GTQ' => array('Quetzal', 'Q', 2),
            'GYD' => array('Guyana Dollar', '$', 2), 'HKD' => array('Hong Kong Dollar', '$', 2),
            'HNL' => array('Lempira', 'L', 2), 'HRK' => array('Kuna', 'kn', 2),
            'HTG' => array('Gourde', 'G', 2), 'HUF' => array('Forint', 'Ft', 2),
            'IDR' => array('Rupiah', 'Rp', 2), 'ILS' => array('New Israeli Sheqel', '₪', 2),
            'INR' => array('Indian Rupee', '₹', 2), 'IQD' => array('Iraqi Dinar', 'ع.د', 3),
            'IRR' => array('Iranian Rial', '﷼', 2), 'ISK' => array('Iceland Krona', 'kr', 0),
            'JMD' => array('Jamaican Dollar', '$', 2), 'JOD' => array('Jordanian Dinar', 'د.ا', 3),
            'JPY' => array('Yen', '¥', 0), 'KES' => array('Kenyan Shilling', 'KSh', 2),
            'KGS' => array('Som', 'с', 2), 'KHR' => array('Riel', '៛', 2),
            'KMF' => array('Comorian Franc', 'CF', 0), 'KRW' => array('Won', '₩', 0),
            'KWD' => array('Kuwaiti Dinar', 'د.ك', 3), 'KYD' => array('Cayman Islands Dollar', '$', 2),
            'KZT' => array('Tenge', '₸', 2), 'LAK' => array('Lao Kip', '₭', 2),
            'LBP' => array('Lebanese Pound', 'ل.ل', 2), 'LKR' => array('Sri Lanka Rupee', 'Rs', 2),
            'LRD' => array('Liberian Dollar', '$', 2), 'LSL' => array('Loti', 'L', 2),
            'LYD' => array('Libyan Dinar', 'ل.د', 3), 'MAD' => array('Moroccan Dirham', 'د.م.', 2),
            'MDL' => array('Moldovan Leu', 'L', 2), 'MGA' => array('Malagasy Ariary', 'Ar', 2),
            'MKD' => array('Denar', 'ден', 2), 'MMK' => array('Kyat', 'K', 2),
            'MNT' => array('Tugrik', '₮', 2), 'MOP' => array('Pataca', 'MOP$', 2),
            'MRU' => array('Ouguiya', 'UM', 2), 'MUR' => array('Mauritius Rupee', '₨', 2),
            'MVR' => array('Rufiyaa', '.ރ', 2), 'MWK' => array('Malawi Kwacha', 'MK', 2),
            'MXN' => array('Mexican Peso', '$', 2), 'MYR' => array('Malaysian Ringgit', 'RM', 2),
            'MZN' => array('Mozambique Metical', 'MT', 2), 'NAD' => array('Namibia Dollar', '$', 2),
            'NGN' => array('Naira', '₦', 2), 'NIO' => array('Cordoba Oro', 'C$', 2),
            'NOK' => array('Norwegian Krone', 'kr', 2), 'NPR' => array('Nepalese Rupee', 'रू', 2),
            'NZD' => array('New Zealand Dollar', '$', 2), 'OMR' => array('Rial Omani', 'ر.ع.', 3),
            'PAB' => array('Balboa', 'B/.', 2), 'PEN' => array('Sol', 'S/', 2),
            'PGK' => array('Kina', 'K', 2), 'PHP' => array('Philippine Peso', '₱', 2),
            'PKR' => array('Pakistan Rupee', '₨', 2), 'PLN' => array('Zloty', 'zł', 2),
            'PYG' => array('Guarani', '₲', 0), 'QAR' => array('Qatari Rial', 'ر.ق', 2),
            'RON' => array('Romanian Leu', 'lei', 2), 'RSD' => array('Serbian Dinar', 'дин', 2),
            'RUB' => array('Russian Ruble', '₽', 2), 'RWF' => array('Rwanda Franc', 'FRw', 0),
            'SAR' => array('Saudi Riyal', 'ر.س', 2), 'SBD' => array('Solomon Islands Dollar', '$', 2),
            'SCR' => array('Seychelles Rupee', '₨', 2), 'SDG' => array('Sudanese Pound', 'ج.س.', 2),
            'SEK' => array('Swedish Krona', 'kr', 2), 'SGD' => array('Singapore Dollar', '$', 2),
            'SHP' => array('Saint Helena Pound', '£', 2), 'SLE' => array('Leone', 'Le', 2),
            'SOS' => array('Somali Shilling', 'Sh', 2), 'SRD' => array('Surinam Dollar', '$', 2),
            'SSP' => array('South Sudanese Pound', '£', 2), 'STN' => array('Dobra', 'Db', 2),
            'SVC' => array('El Salvador Colon', '₡', 2), 'SYP' => array('Syrian Pound', '£', 2),
            'SZL' => array('Lilangeni', 'L', 2), 'THB' => array('Baht', '฿', 2),
            'TJS' => array('Somoni', 'ЅМ', 2), 'TMT' => array('Turkmenistan New Manat', 'm', 2),
            'TND' => array('Tunisian Dinar', 'د.ت', 3), 'TOP' => array('Pa’anga', 'T$', 2),
            'TRY' => array('Turkish Lira', '₺', 2), 'TTD' => array('Trinidad and Tobago Dollar', '$', 2),
            'TWD' => array('New Taiwan Dollar', 'NT$', 2), 'TZS' => array('Tanzanian Shilling', 'TSh', 2),
            'UAH' => array('Hryvnia', '₴', 2), 'UGX' => array('Uganda Shilling', 'USh', 0),
            'USD' => array('US Dollar', '$', 2), 'UYU' => array('Peso Uruguayo', '$U', 2),
            'UZS' => array('Uzbekistan Sum', 'soʻm', 2), 'VES' => array('Bolívar Soberano', 'Bs.', 2),
            'VND' => array('Dong', '₫', 0), 'VUV' => array('Vatu', 'VT', 0),
            'WST' => array('Tala', 'WS$', 2), 'XAF' => array('CFA Franc BEAC', 'FCFA', 0),
            'XCD' => array('East Caribbean Dollar', '$', 2), 'XOF' => array('CFA Franc BCEAO', 'CFA', 0),
            'XPF' => array('CFP Franc', '₣', 0), 'YER' => array('Yemeni Rial', '﷼', 2),
            'ZAR' => array('Rand', 'R', 2), 'ZMW' => array('Zambian Kwacha', 'ZK', 2),
            'ZWL' => array('Zimbabwe Dollar', '$', 2),
        );
    }
}

if (!function_exists('epc_ccy_info')) {
    /**
     * @return array{code:string,name:string,symbol:string,decimals:int}|null
     */
    function epc_ccy_info(string $code): ?array
    {
        $code = strtoupper(trim($code));
        $cat = epc_ccy_catalog();
        if (!isset($cat[$code])) {
            return null;
        }
        return array('code' => $code, 'name' => $cat[$code][0], 'symbol' => $cat[$code][1], 'decimals' => $cat[$code][2]);
    }
}

if (!function_exists('epc_ccy_format')) {
    /**
     * Format an amount per the currency's minor units + symbol.
     */
    function epc_ccy_format(float $amount, string $code, bool $symbolBefore = true): string
    {
        $info = epc_ccy_info($code);
        $dec = $info ? $info['decimals'] : 2;
        $sym = $info ? $info['symbol'] : $code;
        $num = number_format($amount, $dec);
        return $symbolBefore ? ($sym . ' ' . $num) : ($num . ' ' . $sym);
    }
}

if (!function_exists('epc_ccy_round')) {
    /**
     * Round to the currency's minor-unit precision.
     */
    function epc_ccy_round(float $amount, string $code): float
    {
        $info = epc_ccy_info($code);
        $dec = $info ? $info['decimals'] : 2;
        return round($amount, $dec);
    }
}

if (!function_exists('epc_ccy_ensure_schema')) {
    function epc_ccy_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ccy_config` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `base_currency` varchar(3) NOT NULL DEFAULT 'AED',
            `enabled_csv` text,
            `symbol_before` tinyint(1) NOT NULL DEFAULT 1,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tenant currency config (single row)'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ccy_rates` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `from_ccy` varchar(3) NOT NULL,
            `to_ccy` varchar(3) NOT NULL,
            `rate` decimal(18,8) NOT NULL DEFAULT 0,
            `as_of` int(11) NOT NULL DEFAULT 0,
            `source` varchar(32) NOT NULL DEFAULT 'manual',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_pair_date` (`from_ccy`,`to_ccy`,`as_of`),
            KEY `x_pair` (`from_ccy`,`to_ccy`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Dated FX rates'");
    }
}

if (!function_exists('epc_ccy_set_config')) {
    /**
     * @param array<int,string> $enabled enabled currency codes
     */
    function epc_ccy_set_config(PDO $db, string $baseCurrency, array $enabled, bool $symbolBefore = true): void
    {
        epc_ccy_ensure_schema($db);
        $base = strtoupper(trim($baseCurrency));
        $enabled = array_values(array_unique(array_map(static function ($c) {
            return strtoupper(trim($c));
        }, $enabled)));
        if (!in_array($base, $enabled, true)) {
            $enabled[] = $base;
        }
        $now = time();
        $exists = (int) $db->query("SELECT COUNT(*) FROM `epc_ccy_config`")->fetchColumn();
        if ($exists > 0) {
            $db->prepare("UPDATE `epc_ccy_config` SET `base_currency`=?, `enabled_csv`=?, `symbol_before`=?, `time_updated`=? WHERE `id`=(SELECT id FROM (SELECT MIN(id) id FROM `epc_ccy_config`) t)")
               ->execute(array($base, implode(',', $enabled), $symbolBefore ? 1 : 0, $now));
        } else {
            $db->prepare("INSERT INTO `epc_ccy_config` (`base_currency`,`enabled_csv`,`symbol_before`,`time_updated`) VALUES (?,?,?,?)")
               ->execute(array($base, implode(',', $enabled), $symbolBefore ? 1 : 0, $now));
        }
    }
}

if (!function_exists('epc_ccy_get_config')) {
    /**
     * @return array<string,mixed>
     */
    function epc_ccy_get_config(PDO $db): array
    {
        epc_ccy_ensure_schema($db);
        $row = $db->query("SELECT * FROM `epc_ccy_config` ORDER BY `id` LIMIT 1")->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            return array('base_currency' => 'AED', 'enabled' => array('AED'), 'symbol_before' => true);
        }
        $enabled = array_filter(array_map('trim', explode(',', (string) $row['enabled_csv'])));
        return array(
            'base_currency' => (string) $row['base_currency'],
            'enabled' => array_values($enabled),
            'symbol_before' => (bool) $row['symbol_before'],
        );
    }
}

if (!function_exists('epc_ccy_set_rate')) {
    /**
     * Store a dated rate: 1 unit of from_ccy = rate units of to_ccy.
     */
    function epc_ccy_set_rate(PDO $db, string $from, string $to, float $rate, int $asOf = 0): void
    {
        epc_ccy_ensure_schema($db);
        $from = strtoupper(trim($from));
        $to = strtoupper(trim($to));
        $asOf = $asOf > 0 ? $asOf : time();
        $now = time();
        $db->prepare(
            "INSERT INTO `epc_ccy_rates` (`from_ccy`,`to_ccy`,`rate`,`as_of`,`source`,`time_created`)
             VALUES (?,?,?,?, 'manual', ?)
             ON DUPLICATE KEY UPDATE `rate`=VALUES(`rate`), `source`=VALUES(`source`)"
        )->execute(array($from, $to, $rate, $asOf, $now));
    }
}

if (!function_exists('epc_ccy_get_rate')) {
    /**
     * Most recent rate at or before $asOf for from->to. Tries the direct pair,
     * then the inverse, then triangulation through the base currency.
     * Returns 1.0 for same-currency.
     */
    function epc_ccy_get_rate(PDO $db, string $from, string $to, int $asOf = 0): ?float
    {
        epc_ccy_ensure_schema($db);
        $from = strtoupper(trim($from));
        $to = strtoupper(trim($to));
        if ($from === $to) {
            return 1.0;
        }
        $asOf = $asOf > 0 ? $asOf : time();

        $direct = epc_ccy_rate_raw($db, $from, $to, $asOf);
        if ($direct !== null) {
            return $direct;
        }
        $inv = epc_ccy_rate_raw($db, $to, $from, $asOf);
        if ($inv !== null && $inv != 0.0) {
            return round(1.0 / $inv, 8);
        }
        // triangulate via base
        $cfg = epc_ccy_get_config($db);
        $base = $cfg['base_currency'];
        if ($base !== $from && $base !== $to) {
            $fb = epc_ccy_get_rate($db, $from, $base, $asOf);
            $bt = epc_ccy_get_rate($db, $base, $to, $asOf);
            if ($fb !== null && $bt !== null) {
                return round($fb * $bt, 8);
            }
        }
        return null;
    }
}

if (!function_exists('epc_ccy_rate_raw')) {
    function epc_ccy_rate_raw(PDO $db, string $from, string $to, int $asOf): ?float
    {
        $st = $db->prepare("SELECT `rate` FROM `epc_ccy_rates` WHERE `from_ccy`=? AND `to_ccy`=? AND `as_of`<=? ORDER BY `as_of` DESC LIMIT 1");
        $st->execute(array($from, $to, $asOf));
        $v = $st->fetchColumn();
        return $v === false ? null : (float) $v;
    }
}

if (!function_exists('epc_ccy_convert')) {
    /**
     * Convert an amount from->to using the as-of rate, rounded to the target
     * currency's precision.
     *
     * @return array{amount:float,rate:float}|null
     */
    function epc_ccy_convert(PDO $db, float $amount, string $from, string $to, int $asOf = 0): ?array
    {
        $rate = epc_ccy_get_rate($db, $from, $to, $asOf);
        if ($rate === null) {
            return null;
        }
        return array('amount' => epc_ccy_round($amount * $rate, $to), 'rate' => $rate);
    }
}

if (!function_exists('epc_ccy_fx_gain_loss')) {
    /**
     * Realised FX gain/loss when a foreign-currency invoice settles at a
     * different rate than it was booked.
     *
     * @param float $foreignAmount invoice amount in foreign currency
     * @param float $bookedRate rate at invoice date (foreign->base)
     * @param float $settledRate rate at payment date (foreign->base)
     * @param string $baseCcy for rounding
     * @return array<string,float> base values + gain_loss (+ = gain)
     */
    function epc_ccy_fx_gain_loss(float $foreignAmount, float $bookedRate, float $settledRate, string $baseCcy = 'AED'): array
    {
        $bookedBase = epc_ccy_round($foreignAmount * $bookedRate, $baseCcy);
        $settledBase = epc_ccy_round($foreignAmount * $settledRate, $baseCcy);
        return array(
            'booked_base' => $bookedBase,
            'settled_base' => $settledBase,
            'gain_loss' => epc_ccy_round($settledBase - $bookedBase, $baseCcy),
        );
    }
}
