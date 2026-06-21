<?php
/**
 * Advanced ERP — Standard process flows (document chains) per industry.
 *
 * Defines, for each industry, the full end-to-end document workflow: which
 * document is prepared at which step, by whom (role/level), the document code
 * (PR, RFQ, PO/LPO, GRN, Bill, PV, SO, DO, INV, RV, ...), and the accounting
 * impact. A runtime instantiates a flow for a transaction and advances it
 * document by document, enforcing the prescribed order.
 *
 * This drives both the in-app step-by-step guide and (optionally) live document
 * tracking. Additive: registry (pure) + new epc_flow_* runtime tables.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_flow_doc_catalog')) {
    /**
     * Master document dictionary shared across flows.
     *
     * @return array<string,array{name:string,short:string}>
     */
    function epc_flow_doc_catalog(): array
    {
        return array(
            'PR'   => array('name' => 'Purchase Requisition', 'short' => 'Internal request to buy'),
            'RFQ'  => array('name' => 'Request for Quotation', 'short' => 'Ask suppliers to quote'),
            'SQ'   => array('name' => 'Supplier Quotation', 'short' => 'Supplier price offer'),
            'PO'   => array('name' => 'Purchase Order / LPO', 'short' => 'Approved order to supplier'),
            'GRN'  => array('name' => 'Goods Receipt Note', 'short' => 'Receive goods into stock'),
            'BILL' => array('name' => 'Purchase Invoice / Bill', 'short' => 'Supplier bill booked to AP'),
            'BOE'  => array('name' => 'Bill of Entry / Customs', 'short' => 'Customs declaration + duty'),
            'PV'   => array('name' => 'Payment Voucher', 'short' => 'Pay supplier'),
            'WO'   => array('name' => 'Work / Job Order', 'short' => 'Production / making order'),
            'MI'   => array('name' => 'Material Issue', 'short' => 'Issue materials to job'),
            'QC'   => array('name' => 'Quality / Hallmark', 'short' => 'Inspection / certification'),
            'FG'   => array('name' => 'Finished Goods Receipt', 'short' => 'Receive output to stock'),
            'EST'  => array('name' => 'Estimate / Quotation', 'short' => 'Customer quote'),
            'SO'   => array('name' => 'Sales Order', 'short' => 'Confirmed customer order'),
            'DO'   => array('name' => 'Delivery Order', 'short' => 'Dispatch goods'),
            'INV'  => array('name' => 'Tax Invoice', 'short' => 'Bill customer + VAT'),
            'RV'   => array('name' => 'Receipt Voucher', 'short' => 'Customer payment received'),
            'CRN'  => array('name' => 'Credit Note', 'short' => 'Sales return / adjustment'),
            'BOQ'  => array('name' => 'Bill of Quantities', 'short' => 'Project scope + quantities'),
            'CON'  => array('name' => 'Contract', 'short' => 'Signed contract / agreement'),
            'IPC'  => array('name' => 'Progress Claim / IPC', 'short' => 'Interim payment certificate'),
            'RET'  => array('name' => 'Retention', 'short' => 'Retention withheld/released'),
            'SHIFT'=> array('name' => 'Shift Open/Close', 'short' => 'POS till session'),
            'ZRPT' => array('name' => 'Z-Report / Day Close', 'short' => 'POS daily reconciliation'),
            'LC'   => array('name' => 'Letter of Credit', 'short' => 'Trade finance instrument'),
        );
    }
}

if (!function_exists('epc_flow_step')) {
    /**
     * Helper to build a flow step.
     *
     * @return array<string,string>
     */
    function epc_flow_step(string $doc, string $role, string $stage, string $posting): array
    {
        return array('doc' => $doc, 'role' => $role, 'stage' => $stage, 'posting' => $posting);
    }
}

if (!function_exists('epc_flow_registry')) {
    /**
     * Per-industry standard document chains. Each flow is an ordered list of
     * steps. Industries not listed fall back to 'general'.
     *
     * @return array<string,array{label:string,steps:array<int,array<string,string>>}>
     */
    function epc_flow_registry(): array
    {
        $S = 'epc_flow_step';

        $general = array(
            $S('PR', 'Store/Buyer', 'Procure', 'No GL'),
            $S('RFQ', 'Buyer', 'Procure', 'No GL'),
            $S('SQ', 'Buyer', 'Procure', 'No GL'),
            $S('PO', 'Purchase Manager', 'Procure', 'Commitment only'),
            $S('GRN', 'Store Keeper', 'Receive', 'Inventory +, GR/IR +'),
            $S('BILL', 'Accounts Payable', 'Invoice', 'GR/IR -, AP +, Input VAT +'),
            $S('PV', 'Accounts/Treasury', 'Pay', 'AP -, Bank -'),
            $S('EST', 'Sales', 'Quote', 'No GL'),
            $S('SO', 'Sales', 'Order', 'Commitment only'),
            $S('DO', 'Warehouse', 'Deliver', 'Inventory -, COGS +'),
            $S('INV', 'Accounts Receivable', 'Invoice', 'AR +, Revenue +, Output VAT +'),
            $S('RV', 'Accounts/Treasury', 'Collect', 'Bank +, AR -'),
        );

        $reg = array();
        $reg['general'] = array('label' => 'General Trading / Retail', 'steps' => $general);

        // Jewellery & Diamond — buy bullion -> make -> hallmark -> sell.
        $reg['jewellery_diamond'] = array('label' => 'Jewellery & Diamond', 'steps' => array(
            $S('PR', 'Showroom/Buyer', 'Procure', 'No GL'),
            $S('RFQ', 'Buyer', 'Procure', 'No GL'),
            $S('SQ', 'Buyer', 'Procure', 'No GL'),
            $S('PO', 'Purchase Manager', 'Procure', 'Commitment (gold rate, weight, purity)'),
            $S('GRN', 'Strong-room Keeper', 'Receive', 'Metal/Stone stock + (by weight)'),
            $S('BILL', 'Accounts', 'Invoice', 'Stock GR/IR -, AP +, Input VAT +'),
            $S('PV', 'Treasury', 'Pay', 'AP -, Bank -'),
            $S('WO', 'Production', 'Making', 'WIP +, Metal stock -'),
            $S('MI', 'Strong-room', 'Making', 'Issue metal/stones to karigar'),
            $S('QC', 'QC/Hallmark', 'Quality', 'No GL (assay/hallmark)'),
            $S('FG', 'Production', 'Finish', 'Finished jewellery stock +, WIP -, making charge capitalised'),
            $S('EST', 'Sales', 'Quote', 'Metal value (today rate) + making + stones + VAT'),
            $S('SO', 'Sales', 'Order', 'Commitment only'),
            $S('DO', 'Showroom', 'Deliver', 'Finished stock -, COGS +'),
            $S('INV', 'Cashier/Accounts', 'Invoice', 'AR/Cash +, Revenue +, Output VAT +'),
            $S('RV', 'Cashier', 'Collect', 'Cash/Bank +, AR -'),
            $S('CRN', 'Sales/Accounts', 'Return', 'Old-gold buy-back / exchange, Revenue -'),
        ));

        // Trading / Import-Export — with LC + customs + landed cost.
        $reg['trading_import'] = array('label' => 'Trading / Import-Export', 'steps' => array(
            $S('PR', 'Procurement', 'Procure', 'No GL'),
            $S('RFQ', 'Procurement', 'Procure', 'No GL'),
            $S('SQ', 'Procurement', 'Procure', 'No GL'),
            $S('PO', 'Purchase Manager', 'Procure', 'Commitment only'),
            $S('LC', 'Treasury/Trade Finance', 'Finance', 'LC margin +, Bank -'),
            $S('BOE', 'Clearing/Customs', 'Import', 'Customs duty + import VAT'),
            $S('GRN', 'Warehouse', 'Receive', 'Inventory +, GR/IR +'),
            $S('BILL', 'Accounts Payable', 'Invoice', 'GR/IR -, AP +, landed cost added'),
            $S('PV', 'Treasury', 'Pay', 'AP -, Bank - (multi-currency FX g/l)'),
            $S('EST', 'Sales', 'Quote', 'No GL'),
            $S('SO', 'Sales', 'Order', 'Commitment only'),
            $S('DO', 'Warehouse', 'Deliver', 'Inventory -, COGS +'),
            $S('INV', 'Accounts Receivable', 'Invoice', 'AR +, Revenue +, Output VAT +'),
            $S('RV', 'Treasury', 'Collect', 'Bank +, AR -'),
        ));

        // Construction / Contracting — BOQ -> contract -> progress -> retention.
        $reg['construction'] = array('label' => 'Construction / Contracting', 'steps' => array(
            $S('BOQ', 'Estimation', 'Tender', 'No GL'),
            $S('CON', 'Management', 'Award', 'Contract value (memo)'),
            $S('PR', 'Site/Project', 'Procure', 'No GL'),
            $S('PO', 'Procurement', 'Procure', 'Commitment only'),
            $S('GRN', 'Site Store', 'Receive', 'Project material/WIP +'),
            $S('BILL', 'Accounts Payable', 'Invoice', 'AP +, Input VAT +'),
            $S('IPC', 'QS/Project', 'Progress', 'WIP/Revenue (% complete), AR +'),
            $S('RET', 'Accounts', 'Progress', 'Retention receivable withheld'),
            $S('INV', 'Accounts Receivable', 'Invoice', 'AR +, Revenue +, Output VAT +'),
            $S('RV', 'Treasury', 'Collect', 'Bank +, AR -'),
        ));

        // Retail + POS — shift/till driven.
        $reg['retail_pos'] = array('label' => 'Retail + POS', 'steps' => array(
            $S('PO', 'Buyer', 'Procure', 'Commitment only'),
            $S('GRN', 'Store', 'Receive', 'Inventory +, GR/IR +'),
            $S('BILL', 'Accounts', 'Invoice', 'AP +, Input VAT +'),
            $S('SHIFT', 'Cashier', 'Open', 'Opening float'),
            $S('INV', 'POS', 'Sell', 'Cash/Card +, Revenue +, Output VAT +, Inventory -, COGS +'),
            $S('ZRPT', 'Cashier/Supervisor', 'Close', 'Till reconciliation, variances'),
            $S('RV', 'Accounts', 'Banking', 'Bank +, Cash -'),
        ));

        // Process / discrete manufacturing.
        $reg['manufacturing'] = array('label' => 'Manufacturing', 'steps' => array(
            $S('PR', 'Planning', 'Plan', 'No GL'),
            $S('PO', 'Procurement', 'Procure', 'Commitment only'),
            $S('GRN', 'Stores', 'Receive', 'Raw material +, GR/IR +'),
            $S('BILL', 'Accounts Payable', 'Invoice', 'AP +, Input VAT +'),
            $S('WO', 'Production', 'Produce', 'Open work order'),
            $S('MI', 'Stores', 'Produce', 'Raw material -, WIP +'),
            $S('QC', 'Quality', 'Quality', 'No GL'),
            $S('FG', 'Production', 'Finish', 'Finished goods +, WIP - (mat+labour+overhead)'),
            $S('SO', 'Sales', 'Order', 'Commitment only'),
            $S('DO', 'Warehouse', 'Deliver', 'FG -, COGS +'),
            $S('INV', 'Accounts Receivable', 'Invoice', 'AR +, Revenue +, Output VAT +'),
            $S('RV', 'Treasury', 'Collect', 'Bank +, AR -'),
        ));

        // Rental / Leasing.
        $reg['rental'] = array('label' => 'Rental / Leasing', 'steps' => array(
            $S('EST', 'Sales', 'Quote', 'No GL'),
            $S('CON', 'Sales/Legal', 'Agreement', 'Rental contract (memo)'),
            $S('DO', 'Operations', 'Handover', 'Asset on-rent (memo)'),
            $S('INV', 'Accounts', 'Bill', 'AR +, Rental revenue (accrued) +, Output VAT +'),
            $S('RV', 'Treasury', 'Collect', 'Bank +, AR -'),
            $S('CRN', 'Accounts', 'Return', 'Damage/early-return adjustment'),
        ));

        return $reg;
    }
}

if (!function_exists('epc_flow_for_industry')) {
    /**
     * @return array{label:string,steps:array<int,array<string,string>>}
     */
    function epc_flow_for_industry(string $industry): array
    {
        $reg = epc_flow_registry();
        return $reg[$industry] ?? $reg['general'];
    }
}

if (!function_exists('epc_flow_describe')) {
    /**
     * Human-readable numbered steps for the guide, resolving doc names.
     *
     * @return array<int,array<string,string>>
     */
    function epc_flow_describe(string $industry): array
    {
        $flow = epc_flow_for_industry($industry);
        $cat = epc_flow_doc_catalog();
        $out = array();
        $i = 1;
        foreach ($flow['steps'] as $s) {
            $doc = $s['doc'];
            $out[] = array(
                'no' => (string) $i,
                'doc_code' => $doc,
                'doc_name' => $cat[$doc]['name'] ?? $doc,
                'role' => $s['role'],
                'stage' => $s['stage'],
                'posting' => $s['posting'],
            );
            $i++;
        }
        return $out;
    }
}

/* --------------------------- Flow runtime ---------------------------- */

if (!function_exists('epc_flow_ensure_schema')) {
    function epc_flow_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_flow_instances` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `industry` varchar(40) NOT NULL DEFAULT 'general',
            `ref` varchar(60) NOT NULL DEFAULT '',
            `step_index` int(11) NOT NULL DEFAULT 0,
            `status` varchar(12) NOT NULL DEFAULT 'open',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_ref` (`ref`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Process-flow instances'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_flow_documents` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `instance_id` int(11) NOT NULL,
            `doc_code` varchar(10) NOT NULL DEFAULT '',
            `doc_no` varchar(60) NOT NULL DEFAULT '',
            `step_index` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_instance` (`instance_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Documents prepared within a flow'");
    }
}

if (!function_exists('epc_flow_start')) {
    function epc_flow_start(PDO $db, string $industry, string $ref): int
    {
        epc_flow_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_flow_instances` (`industry`,`ref`,`step_index`,`status`,`time_created`) VALUES (?,?,0, 'open', ?)")
           ->execute(array($industry, $ref, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_flow_next_expected')) {
    /**
     * The next document expected for an instance, or null when complete.
     *
     * @return array<string,string>|null
     */
    function epc_flow_next_expected(PDO $db, int $instanceId): ?array
    {
        epc_flow_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_flow_instances` WHERE `id`=?");
        $st->execute(array($instanceId));
        $inst = $st->fetch(PDO::FETCH_ASSOC);
        if (!$inst) {
            throw new Exception('Flow instance not found');
        }
        $flow = epc_flow_for_industry((string) $inst['industry']);
        $idx = (int) $inst['step_index'];
        if ($idx >= count($flow['steps'])) {
            return null;
        }
        $cat = epc_flow_doc_catalog();
        $step = $flow['steps'][$idx];
        return array('doc_code' => $step['doc'], 'doc_name' => $cat[$step['doc']]['name'] ?? $step['doc'], 'role' => $step['role'], 'stage' => $step['stage']);
    }
}

if (!function_exists('epc_flow_record_document')) {
    /**
     * Record that a document was prepared. Enforces the prescribed order:
     * the doc must match the next expected step (unless $strict is false).
     *
     * @return array<string,mixed>
     */
    function epc_flow_record_document(PDO $db, int $instanceId, string $docCode, string $docNo = '', bool $strict = true): array
    {
        epc_flow_ensure_schema($db);
        $expected = epc_flow_next_expected($db, $instanceId);
        if ($expected === null) {
            throw new Exception('Flow already complete');
        }
        if ($strict && $expected['doc_code'] !== $docCode) {
            throw new Exception('Out-of-sequence document: expected ' . $expected['doc_code'] . ', got ' . $docCode);
        }
        $st = $db->prepare("SELECT * FROM `epc_flow_instances` WHERE `id`=?");
        $st->execute(array($instanceId));
        $inst = $st->fetch(PDO::FETCH_ASSOC);
        $idx = (int) $inst['step_index'];
        $db->prepare("INSERT INTO `epc_flow_documents` (`instance_id`,`doc_code`,`doc_no`,`step_index`,`time_created`) VALUES (?,?,?,?,?)")
           ->execute(array($instanceId, $docCode, $docNo, $idx, time()));
        $newIdx = $idx + 1;
        $flow = epc_flow_for_industry((string) $inst['industry']);
        $complete = $newIdx >= count($flow['steps']);
        $db->prepare("UPDATE `epc_flow_instances` SET `step_index`=?, `status`=? WHERE `id`=?")
           ->execute(array($newIdx, $complete ? 'complete' : 'open', $instanceId));
        return array('recorded' => $docCode, 'step_index' => $newIdx, 'complete' => $complete);
    }
}

if (!function_exists('epc_flow_progress')) {
    /**
     * @return array<string,mixed> {industry, total_steps, done, percent, status, next}
     */
    function epc_flow_progress(PDO $db, int $instanceId): array
    {
        epc_flow_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_flow_instances` WHERE `id`=?");
        $st->execute(array($instanceId));
        $inst = $st->fetch(PDO::FETCH_ASSOC);
        if (!$inst) {
            throw new Exception('Flow instance not found');
        }
        $flow = epc_flow_for_industry((string) $inst['industry']);
        $total = count($flow['steps']);
        $done = (int) $inst['step_index'];
        return array(
            'industry' => (string) $inst['industry'],
            'total_steps' => $total,
            'done' => $done,
            'percent' => $total > 0 ? round($done / $total * 100, 1) : 0.0,
            'status' => (string) $inst['status'],
            'next' => epc_flow_next_expected($db, $instanceId),
        );
    }
}
