<?php
/**
 * Multi-industry demo / sample data for the marketing live-demo.
 *
 * Pure, self-contained sample datasets for several industries (jewellery,
 * trading/import, construction, retail/POS, manufacturing). Used to show a
 * realistic ERP preview on ecomae.com and to seed an isolated demo space for
 * trials/training. It NEVER touches a live tenant ledger — it returns data
 * the marketing/demo view renders, and (optionally) can be loaded into a
 * dedicated demo DB.
 *
 * Pure functions (no DB), so unit-testable and safe to call anywhere.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_demo_industries')) {
    /** @return array<int,array{code:string,name:string,currency:string}> */
    function epc_demo_industries(): array
    {
        return array(
            array('code' => 'jewellery', 'name' => 'Jewellery & Bullion', 'currency' => 'AED'),
            array('code' => 'trading', 'name' => 'Trading / Import-Export', 'currency' => 'AED'),
            array('code' => 'construction', 'name' => 'Construction & Contracting', 'currency' => 'AED'),
            array('code' => 'retail', 'name' => 'Retail / POS', 'currency' => 'AED'),
            array('code' => 'manufacturing', 'name' => 'Manufacturing', 'currency' => 'AED'),
        );
    }
}

if (!function_exists('epc_demo_dataset')) {
    /**
     * A realistic demo dataset for one industry: company, products, customers,
     * and a set of orders with cost so margins/KPIs compute correctly.
     *
     * @return array<string,mixed>
     */
    function epc_demo_dataset(string $industry): array
    {
        $sets = array(
            'jewellery' => array(
                'company' => array('name' => 'Al Noor Jewellers LLC', 'trn' => '100xxxxxxxx0003', 'currency' => 'AED'),
                'products' => array(
                    array('sku' => 'GR-22K-01', 'name' => '22K Gold Ring', 'price' => 1800, 'cost' => 1500, 'stock' => 12),
                    array('sku' => 'DP-01', 'name' => 'Diamond Pendant', 'price' => 5200, 'cost' => 4100, 'stock' => 4),
                    array('sku' => 'SB-925-01', 'name' => 'Silver Bracelet', 'price' => 320, 'cost' => 210, 'stock' => 30),
                ),
                'customers' => array('Khan Trading', 'Aisha Retail', 'Gold Souk Partners'),
                'orders' => array(
                    array('cust' => 0, 'sku' => 'GR-22K-01', 'qty' => 2, 'paid' => true),
                    array('cust' => 1, 'sku' => 'DP-01', 'qty' => 1, 'paid' => false),
                    array('cust' => 2, 'sku' => 'SB-925-01', 'qty' => 5, 'paid' => true),
                ),
                'doc_chain' => array('Purchase Requisition', 'LPO/PO', 'GRN (weight+purity)', 'Job/Making Order', 'Hallmark/QC', 'Quotation', 'SO', 'DO', 'Tax Invoice', 'Receipt'),
            ),
            'trading' => array(
                'company' => array('name' => 'Gulf Spare Imports FZE', 'trn' => '100xxxxxxxx0007', 'currency' => 'AED'),
                'products' => array(
                    array('sku' => 'BRK-PAD-01', 'name' => 'Brake Pad Set', 'price' => 220, 'cost' => 140, 'stock' => 180),
                    array('sku' => 'OIL-FLT-01', 'name' => 'Oil Filter', 'price' => 45, 'cost' => 22, 'stock' => 600),
                    array('sku' => 'ALT-12V-01', 'name' => 'Alternator 12V', 'price' => 780, 'cost' => 560, 'stock' => 35),
                ),
                'customers' => array('AutoFix Garage', 'Desert Motors', 'CityParts WLL'),
                'orders' => array(
                    array('cust' => 0, 'sku' => 'BRK-PAD-01', 'qty' => 20, 'paid' => true),
                    array('cust' => 1, 'sku' => 'ALT-12V-01', 'qty' => 3, 'paid' => true),
                    array('cust' => 2, 'sku' => 'OIL-FLT-01', 'qty' => 100, 'paid' => false),
                ),
                'doc_chain' => array('PR', 'RFQ', 'PO', 'LC', 'Bill of Entry/Customs', 'GRN', 'Landed Cost', 'Purchase Invoice', 'SO', 'DO', 'Tax Invoice', 'Receipt'),
            ),
            'construction' => array(
                'company' => array('name' => 'Emirates BuildCo Contracting', 'trn' => '100xxxxxxxx0011', 'currency' => 'AED'),
                'products' => array(
                    array('sku' => 'RMC-G40', 'name' => 'Ready-Mix Concrete G40 (m3)', 'price' => 280, 'cost' => 210, 'stock' => 0),
                    array('sku' => 'RBR-T12', 'name' => 'Rebar T12 (ton)', 'price' => 2600, 'cost' => 2200, 'stock' => 40),
                    array('sku' => 'LBR-DAY', 'name' => 'Labour (day)', 'price' => 180, 'cost' => 120, 'stock' => 0),
                ),
                'customers' => array('Marina Towers JV', 'DEWA Substation Project', 'Villa 42 Client'),
                'orders' => array(
                    array('cust' => 0, 'sku' => 'RMC-G40', 'qty' => 120, 'paid' => false),
                    array('cust' => 1, 'sku' => 'RBR-T12', 'qty' => 8, 'paid' => true),
                    array('cust' => 2, 'sku' => 'LBR-DAY', 'qty' => 60, 'paid' => false),
                ),
                'doc_chain' => array('BOQ', 'Contract', 'Subcontract/PO', 'Material Requisition', 'GRN', 'Progress Claim (IPC)', 'Retention', 'Certification', 'Tax Invoice', 'Receipt'),
            ),
            'retail' => array(
                'company' => array('name' => 'QuickMart Retail LLC', 'trn' => '100xxxxxxxx0019', 'currency' => 'AED'),
                'products' => array(
                    array('sku' => 'BEV-COLA', 'name' => 'Cola 330ml', 'price' => 3, 'cost' => 1.6, 'stock' => 5000),
                    array('sku' => 'SNK-CHIP', 'name' => 'Chips 150g', 'price' => 5, 'cost' => 2.8, 'stock' => 3000),
                    array('sku' => 'GRC-RICE', 'name' => 'Basmati Rice 5kg', 'price' => 38, 'cost' => 27, 'stock' => 800),
                ),
                'customers' => array('Walk-in POS', 'Loyalty Member A', 'Corporate Hamper Order'),
                'orders' => array(
                    array('cust' => 0, 'sku' => 'BEV-COLA', 'qty' => 240, 'paid' => true),
                    array('cust' => 1, 'sku' => 'GRC-RICE', 'qty' => 10, 'paid' => true),
                    array('cust' => 2, 'sku' => 'SNK-CHIP', 'qty' => 200, 'paid' => true),
                ),
                'doc_chain' => array('Shift Open', 'POS Sale', 'Tender (cash/card)', 'Receipt', 'Z-Report/Day-Close', 'Stock Replenishment'),
            ),
            'manufacturing' => array(
                'company' => array('name' => 'Sharjah Plastics Mfg', 'trn' => '100xxxxxxxx0023', 'currency' => 'AED'),
                'products' => array(
                    array('sku' => 'FG-CRATE', 'name' => 'Plastic Crate (finished)', 'price' => 24, 'cost' => 15, 'stock' => 1200),
                    array('sku' => 'FG-PIPE', 'name' => 'PVC Pipe 4" (finished)', 'price' => 56, 'cost' => 38, 'stock' => 400),
                    array('sku' => 'FG-SHEET', 'name' => 'HDPE Sheet (finished)', 'price' => 130, 'cost' => 92, 'stock' => 150),
                ),
                'customers' => array('Logistics FZCO', 'Plumbing Supplies LLC', 'Packaging House'),
                'orders' => array(
                    array('cust' => 0, 'sku' => 'FG-CRATE', 'qty' => 300, 'paid' => true),
                    array('cust' => 1, 'sku' => 'FG-PIPE', 'qty' => 80, 'paid' => false),
                    array('cust' => 2, 'sku' => 'FG-SHEET', 'qty' => 25, 'paid' => true),
                ),
                'doc_chain' => array('Sales Forecast', 'BOM', 'Work Order', 'Material Issue', 'WIP', 'FG Receipt', 'QC', 'SO', 'DO', 'Tax Invoice', 'Receipt'),
            ),
        );
        $set = $sets[$industry] ?? $sets['trading'];
        $set['industry'] = $industry;
        return $set;
    }
}

if (!function_exists('epc_demo_kpis')) {
    /**
     * Dashboard KPIs computed from a demo dataset (same math as the live
     * data-link summary), so the marketing demo shows believable numbers.
     *
     * @return array<string,mixed>
     */
    function epc_demo_kpis(string $industry): array
    {
        $set = epc_demo_dataset($industry);
        $bySku = array();
        foreach ($set['products'] as $p) {
            $bySku[$p['sku']] = $p;
        }
        $revenue = 0.0;
        $cost = 0.0;
        $paidRevenue = 0.0;
        $paid = 0;
        $unpaid = 0;
        $stockValue = 0.0;
        foreach ($set['products'] as $p) {
            $stockValue += $p['cost'] * $p['stock'];
        }
        foreach ($set['orders'] as $o) {
            $p = $bySku[$o['sku']] ?? null;
            if (!$p) {
                continue;
            }
            $line = $p['price'] * $o['qty'];
            $lcost = $p['cost'] * $o['qty'];
            $revenue += $line;
            $cost += $lcost;
            if (!empty($o['paid'])) {
                $paid++;
                $paidRevenue += $line;
            } else {
                $unpaid++;
            }
        }
        return array(
            'industry' => $industry,
            'company' => $set['company']['name'],
            'currency' => $set['company']['currency'],
            'revenue' => round($revenue, 2),
            'cogs' => round($cost, 2),
            'gross_margin' => round($revenue - $cost, 2),
            'gross_margin_pct' => $revenue > 0 ? round((($revenue - $cost) / $revenue) * 100, 1) : 0.0,
            'orders' => count($set['orders']),
            'paid_orders' => $paid,
            'unpaid_orders' => $unpaid,
            'ar_outstanding' => round($revenue - $paidRevenue, 2),
            'customers' => count($set['customers']),
            'products' => count($set['products']),
            'stock_value' => round($stockValue, 2),
            'doc_chain' => $set['doc_chain'],
        );
    }
}

if (!function_exists('epc_demo_all_kpis')) {
    /**
     * KPIs for every demo industry — drives the marketing "switch industry"
     * live-demo selector.
     *
     * @return array<string,array<string,mixed>>
     */
    function epc_demo_all_kpis(): array
    {
        $out = array();
        foreach (epc_demo_industries() as $ind) {
            $out[$ind['code']] = epc_demo_kpis($ind['code']);
        }
        return $out;
    }
}
