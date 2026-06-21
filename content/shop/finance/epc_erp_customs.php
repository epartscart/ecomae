<?php
/**
 * Advanced ERP — Customs module (UAE / Dubai Customs Mirsal-2 style).
 *
 * Import/export declarations + Bill of Entry, HS-code tariff lookup, duty
 * computation (customs duty on CIF + 5% import VAT), deposit/guarantee
 * tracking, and a declaration payload export. Country tariff logic is a pack so
 * other jurisdictions can be added the same way as the industry packs.
 *
 * Duty flows into landed cost via epc_scm_landed_cost_* when present.
 *
 * Additive: new epc_cust_* tables.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_cust_ensure_schema')) {
    function epc_cust_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cust_declarations` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `decl_no` varchar(40) NOT NULL DEFAULT '',
            `type` varchar(12) NOT NULL DEFAULT 'import',
            `regime` varchar(24) NOT NULL DEFAULT 'import_for_home',
            `country` varchar(2) NOT NULL DEFAULT 'AE',
            `supplier_id` int(11) NOT NULL DEFAULT 0,
            `currency` varchar(3) NOT NULL DEFAULT 'AED',
            `fx_rate` decimal(16,6) NOT NULL DEFAULT 1.000000,
            `freight` decimal(16,2) NOT NULL DEFAULT 0.00,
            `insurance` decimal(16,2) NOT NULL DEFAULT 0.00,
            `goods_value` decimal(16,2) NOT NULL DEFAULT 0.00,
            `cif_value` decimal(16,2) NOT NULL DEFAULT 0.00,
            `duty_total` decimal(16,2) NOT NULL DEFAULT 0.00,
            `vat_total` decimal(16,2) NOT NULL DEFAULT 0.00,
            `status` varchar(16) NOT NULL DEFAULT 'draft',
            `source_type` varchar(32) DEFAULT NULL,
            `source_id` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_status` (`status`),
            KEY `x_type` (`type`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Customs declarations / Bill of Entry'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cust_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `declaration_id` int(11) NOT NULL,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `hs_code` varchar(20) NOT NULL DEFAULT '',
            `description` varchar(255) DEFAULT NULL,
            `qty` decimal(16,3) NOT NULL DEFAULT 0.000,
            `unit_value` decimal(16,2) NOT NULL DEFAULT 0.00,
            `line_value` decimal(16,2) NOT NULL DEFAULT 0.00,
            `duty_rate` decimal(7,3) NOT NULL DEFAULT 0.000,
            `duty_amount` decimal(16,2) NOT NULL DEFAULT 0.00,
            `vat_amount` decimal(16,2) NOT NULL DEFAULT 0.00,
            PRIMARY KEY (`id`),
            KEY `x_decl` (`declaration_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Customs declaration lines'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cust_deposits` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `declaration_id` int(11) NOT NULL DEFAULT 0,
            `type` varchar(16) NOT NULL DEFAULT 'guarantee',
            `amount` decimal(16,2) NOT NULL DEFAULT 0.00,
            `status` varchar(16) NOT NULL DEFAULT 'held',
            `reference` varchar(60) DEFAULT NULL,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_decl` (`declaration_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Customs deposits / guarantees'");
    }
}

if (!function_exists('epc_cust_country_pack')) {
    /**
     * Per-country customs config. Default UAE/Dubai: 5% standard duty, 5% VAT,
     * with common HS-code overrides (tobacco/alcohol higher, many staples 0).
     *
     * @return array<string,mixed>
     */
    function epc_cust_country_pack(string $country = 'AE'): array
    {
        $packs = array(
            'AE' => array(
                'label' => 'United Arab Emirates (Dubai Customs / Mirsal 2)',
                'default_duty' => 5.0,
                'vat_rate' => 5.0,
                'vat_on' => 'cif_plus_duty',
                'hs_overrides' => array(
                    '2402' => 100.0, // cigars/cigarettes
                    '2203' => 50.0,  // beer/alcohol
                    '2204' => 50.0,
                    '3004' => 0.0,   // medicaments
                    '1006' => 0.0,   // rice
                    '0401' => 0.0,   // milk
                ),
            ),
        );
        return $packs[$country] ?? $packs['AE'];
    }
}

if (!function_exists('epc_cust_duty_rate')) {
    /**
     * Resolve the duty rate for an HS code using the country pack (longest HS
     * prefix match wins; else the default duty).
     */
    function epc_cust_duty_rate(string $hsCode, string $country = 'AE'): float
    {
        $pack = epc_cust_country_pack($country);
        $best = null;
        $bestLen = -1;
        foreach ((array) $pack['hs_overrides'] as $prefix => $rate) {
            $p = (string) $prefix;
            if (strncmp($hsCode, $p, strlen($p)) === 0 && strlen($p) > $bestLen) {
                $best = (float) $rate;
                $bestLen = strlen($p);
            }
        }
        return $best !== null ? $best : (float) $pack['default_duty'];
    }
}

if (!function_exists('epc_cust_compute')) {
    /**
     * Compute CIF, duty and import VAT for a declaration.
     *
     * CIF = goods + freight + insurance (in declaration currency * fx -> base).
     * Duty is per line on its share of CIF (line_value/goods * cif) * duty_rate.
     * VAT (5%) is charged on (CIF + duty) for import-for-home.
     *
     * @param array{goods_value?:float,freight?:float,insurance?:float,fx_rate?:float,country?:string,regime?:string} $hdr
     * @param array<int,array{hs_code:string,qty:float,unit_value:float}> $lines
     * @return array<string,mixed>
     */
    function epc_cust_compute(array $hdr, array $lines): array
    {
        $country = (string) ($hdr['country'] ?? 'AE');
        $pack = epc_cust_country_pack($country);
        $fx = (float) ($hdr['fx_rate'] ?? 1.0);
        $freight = round((float) ($hdr['freight'] ?? 0) * $fx, 2);
        $insurance = round((float) ($hdr['insurance'] ?? 0) * $fx, 2);

        $goods = 0.0;
        $computed = array();
        foreach ($lines as $ln) {
            $lv = round((float) $ln['qty'] * (float) $ln['unit_value'] * $fx, 2);
            $goods = round($goods + $lv, 2);
            $computed[] = array('hs_code' => (string) $ln['hs_code'], 'qty' => (float) $ln['qty'], 'unit_value' => (float) $ln['unit_value'], 'line_value' => $lv);
        }
        $cif = round($goods + $freight + $insurance, 2);

        $regime = (string) ($hdr['regime'] ?? 'import_for_home');
        $vatApplies = $regime === 'import_for_home';

        $dutyTotal = 0.0;
        $vatTotal = 0.0;
        $allocated = 0.0;
        $n = count($computed);
        foreach ($computed as $i => &$ln) {
            // Apportion CIF to the line by its goods-value share.
            $share = $goods > 0 ? ($ln['line_value'] / $goods) : (1 / max(1, $n));
            $lineCif = ($i === $n - 1) ? round($cif - $allocated, 2) : round($cif * $share, 2);
            $allocated = round($allocated + $lineCif, 2);
            $rate = epc_cust_duty_rate($ln['hs_code'], $country);
            $duty = round($lineCif * $rate / 100, 2);
            $vat = $vatApplies ? round(($lineCif + $duty) * (float) $pack['vat_rate'] / 100, 2) : 0.0;
            $ln['line_cif'] = $lineCif;
            $ln['duty_rate'] = $rate;
            $ln['duty_amount'] = $duty;
            $ln['vat_amount'] = $vat;
            $dutyTotal = round($dutyTotal + $duty, 2);
            $vatTotal = round($vatTotal + $vat, 2);
        }
        unset($ln);

        return array(
            'country' => $country,
            'regime' => $regime,
            'goods_value' => $goods,
            'freight' => $freight,
            'insurance' => $insurance,
            'cif_value' => $cif,
            'duty_total' => $dutyTotal,
            'vat_total' => $vatTotal,
            'total_payable' => round($dutyTotal + $vatTotal, 2),
            'lines' => $computed,
        );
    }
}

if (!function_exists('epc_cust_declaration_save')) {
    /**
     * Compute + persist a declaration with its lines.
     *
     * @param array<string,mixed> $hdr
     * @param array<int,array<string,mixed>> $lines
     * @return array<string,mixed> {id, ...compute result}
     */
    function epc_cust_declaration_save(PDO $db, array $hdr, array $lines): array
    {
        epc_cust_ensure_schema($db);
        $calc = epc_cust_compute($hdr, $lines);
        $now = time();
        $db->prepare(
            "INSERT INTO `epc_cust_declarations`
             (`decl_no`,`type`,`regime`,`country`,`supplier_id`,`currency`,`fx_rate`,`freight`,`insurance`,`goods_value`,`cif_value`,`duty_total`,`vat_total`,`status`,`source_type`,`source_id`,`time_created`)
             VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?, 'draft', ?,?,?)"
        )->execute(array(
            (string) ($hdr['decl_no'] ?? ''),
            (string) ($hdr['type'] ?? 'import'),
            $calc['regime'],
            $calc['country'],
            (int) ($hdr['supplier_id'] ?? 0),
            (string) ($hdr['currency'] ?? 'AED'),
            (float) ($hdr['fx_rate'] ?? 1.0),
            $calc['freight'],
            $calc['insurance'],
            $calc['goods_value'],
            $calc['cif_value'],
            $calc['duty_total'],
            $calc['vat_total'],
            (string) ($hdr['source_type'] ?? ''),
            (int) ($hdr['source_id'] ?? 0),
            $now,
        ));
        $declId = (int) $db->lastInsertId();
        $ins = $db->prepare("INSERT INTO `epc_cust_lines` (`declaration_id`,`item_id`,`hs_code`,`description`,`qty`,`unit_value`,`line_value`,`duty_rate`,`duty_amount`,`vat_amount`) VALUES (?,?,?,?,?,?,?,?,?,?)");
        foreach ($calc['lines'] as $i => $ln) {
            $ins->execute(array(
                $declId,
                (int) ($lines[$i]['item_id'] ?? 0),
                $ln['hs_code'],
                (string) ($lines[$i]['description'] ?? ''),
                $ln['qty'],
                $ln['unit_value'],
                $ln['line_value'],
                $ln['duty_rate'],
                $ln['duty_amount'],
                $ln['vat_amount'],
            ));
        }
        $calc['id'] = $declId;
        return $calc;
    }
}

if (!function_exists('epc_cust_deposit_add')) {
    /**
     * @param array<string,mixed> $data type(guarantee|deposit), amount, reference
     */
    function epc_cust_deposit_add(PDO $db, int $declId, array $data): int
    {
        epc_cust_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_cust_deposits` (`declaration_id`,`type`,`amount`,`status`,`reference`,`time_created`) VALUES (?,?,?, 'held', ?,?)")
           ->execute(array($declId, (string) ($data['type'] ?? 'guarantee'), round((float) ($data['amount'] ?? 0), 2), (string) ($data['reference'] ?? ''), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_cust_deposit_refund')) {
    function epc_cust_deposit_refund(PDO $db, int $depositId): void
    {
        epc_cust_ensure_schema($db);
        $db->prepare("UPDATE `epc_cust_deposits` SET `status`='refunded' WHERE `id`=?")->execute(array($depositId));
    }
}

if (!function_exists('epc_cust_export_payload')) {
    /**
     * Build a declaration payload (Mirsal-style flat structure) for transmission
     * to the customs gateway via the integration layer.
     *
     * @return array<string,mixed>
     */
    function epc_cust_export_payload(PDO $db, int $declId): array
    {
        epc_cust_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_cust_declarations` WHERE `id`=?");
        $st->execute(array($declId));
        $d = $st->fetch(PDO::FETCH_ASSOC);
        if (!$d) {
            throw new Exception('Declaration not found');
        }
        $ls = $db->prepare("SELECT * FROM `epc_cust_lines` WHERE `declaration_id`=?");
        $ls->execute(array($declId));
        $lines = $ls->fetchAll(PDO::FETCH_ASSOC);
        $payloadLines = array();
        foreach ($lines as $l) {
            $payloadLines[] = array(
                'hsCode' => $l['hs_code'],
                'description' => $l['description'],
                'quantity' => (float) $l['qty'],
                'unitValue' => (float) $l['unit_value'],
                'lineValue' => (float) $l['line_value'],
                'dutyRate' => (float) $l['duty_rate'],
                'dutyAmount' => (float) $l['duty_amount'],
                'vatAmount' => (float) $l['vat_amount'],
            );
        }
        return array(
            'declarationNumber' => $d['decl_no'],
            'declarationType' => $d['type'],
            'regime' => $d['regime'],
            'countryCode' => $d['country'],
            'currency' => $d['currency'],
            'cifValue' => (float) $d['cif_value'],
            'dutyTotal' => (float) $d['duty_total'],
            'vatTotal' => (float) $d['vat_total'],
            'lines' => $payloadLines,
        );
    }
}
