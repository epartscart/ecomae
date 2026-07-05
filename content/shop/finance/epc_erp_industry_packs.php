<?php
/**
 * Advanced ERP — Industry Packs (specialized accounting + process style).
 *
 * Where epc_erp_industry.php configures product/inventory *fields*, this layer
 * adds the rest of what makes an ERP "fit" an industry:
 *   - costing method (fifo / weighted_avg / specific / standard)
 *   - the document/process flow (the chain of steps a transaction follows)
 *   - chart-of-accounts presets + posting rules unique to the sector
 *   - feature flags (which specialized modules to switch on)
 *
 * It is a *registry*: onboarding a new client's niche industry is a config
 * entry here, not new code. A set of concrete, unit-tested accounting helpers
 * back the trickier sectors (jewellery gold valuation, construction retention,
 * oil & gas JV split, POS till reconciliation, rental accrual).
 *
 * Pure config + math. No DB schema changes; safe for live tenants.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_ind_costing_methods')) {
    /**
     * @return array<string,string>
     */
    function epc_ind_costing_methods(): array
    {
        return array(
            'fifo' => 'First in, first out',
            'weighted_avg' => 'Weighted average',
            'specific' => 'Specific identification',
            'standard' => 'Standard costing',
        );
    }
}

if (!function_exists('epc_erp_industry_packs')) {
    /**
     * Specialized industry registry. Each pack:
     *   label, costing, uoms[], process_flow[], coa_presets[], posting_rules[],
     *   features[] (flags), base (the epc_erp_industry.php key to inherit
     *   product fields from, when one fits).
     *
     * @return array<string,array<string,mixed>>
     */
    function epc_erp_industry_packs(): array
    {
        return array(
            'jewellery_diamond' => array(
                'label' => 'Jewellery & Diamond',
                'base' => 'jewellery',
                'costing' => 'specific',
                'uoms' => array('pcs', 'gram', 'carat', 'tola', 'pair'),
                'process_flow' => array('Estimate', 'Customer approval', 'Making / job work', 'Hallmark & QC', 'Sale (weight + making)', 'Old-gold exchange'),
                'coa_presets' => array('Gold Stock (by purity)', 'Stone/Diamond Stock', 'Making Charges Income', 'Gold on Loan', 'Metal Loan Payable', 'Old Gold Purchase'),
                'posting_rules' => array(
                    'valuation' => 'Metal valued at gold-rate-of-the-day x net weight x purity; stones at specific cost; making billed separately.',
                    'old_gold' => 'Old gold inbound credited to customer at agreed purity x rate, debited to Old Gold Purchase.',
                ),
                'features' => array('weight_tracking', 'purity_karat', 'making_charges', 'gold_rate_valuation', 'stone_4c', 'hallmark'),
            ),
            'oil_gas' => array(
                'label' => 'Oil & Gas (upstream/downstream)',
                'base' => 'industrial_manufacturing',
                'costing' => 'weighted_avg',
                'uoms' => array('barrel', 'litre', 'MT', 'scf', 'm3'),
                'process_flow' => array('AFE / well budget', 'Production volume capture', 'JV cost allocation', 'Royalty & severance', 'Partner billing', 'Revenue distribution'),
                'coa_presets' => array('Wells / Leases (cost center)', 'JV Partner Receivable', 'Royalty Payable', 'Severance Tax Payable', 'Production Revenue', 'Lifting Cost'),
                'posting_rules' => array(
                    'jv' => 'Costs split across partners by working interest %; each partner billed their share.',
                    'royalty' => 'Royalty accrued as % of production revenue to lessor.',
                ),
                'features' => array('cost_centers', 'joint_venture', 'dual_uom', 'production_volumes', 'royalty'),
            ),
            'trading_import_export' => array(
                'label' => 'Trading / Import-Export',
                'base' => 'general',
                'costing' => 'weighted_avg',
                'uoms' => array('pcs', 'box', 'carton', 'pallet', 'container', 'kg', 'MT'),
                'process_flow' => array('Quotation', 'Sales/Purchase order', 'LC / payment terms', 'Shipment & Bill of Entry', 'Landed cost', 'Margin settlement'),
                'coa_presets' => array('Goods in Transit', 'Import Duty Recoverable', 'Landed Cost Clearing', 'Letter of Credit Margin', 'Broker Commission'),
                'posting_rules' => array(
                    'landed' => 'Freight/duty/insurance capitalised to stock via landed-cost allocation.',
                    'fx' => 'Multi-currency purchases revalued at settlement; FX gain/loss posted.',
                ),
                'features' => array('landed_cost', 'multi_currency', 'lc_management', 'bill_of_entry'),
            ),
            'wholesale_distribution' => array(
                'label' => 'Wholesale / Distribution',
                'base' => 'general',
                'costing' => 'weighted_avg',
                'uoms' => array('pcs', 'box', 'carton', 'pallet', 'dozen'),
                'process_flow' => array('Price list / scheme', 'Order (route/van)', 'Pick & dispatch', 'Delivery & ePOD', 'Invoice', 'Collection'),
                'coa_presets' => array('Trade Receivable', 'Volume Rebate Accrual', 'Route Cash', 'Damaged/Returns', 'Distribution Cost'),
                'posting_rules' => array(
                    'pricing' => 'Tiered price lists with volume break discounts; rebate accrued per scheme.',
                ),
                'features' => array('price_lists', 'volume_discounts', 'route_van_sales', 'rebates', 'credit_control'),
            ),
            'retail_pos' => array(
                'label' => 'Retail + POS',
                'base' => 'general',
                'costing' => 'weighted_avg',
                'uoms' => array('pcs', 'pack', 'box'),
                'process_flow' => array('Open shift / float', 'Scan & sell', 'Tender (cash/card/wallet)', 'Receipt', 'Shift Z-report', 'Day close & banking'),
                'coa_presets' => array('Till Cash', 'Card Clearing', 'Wallet Clearing', 'Sales (taxable)', 'Cash Over/Short', 'Loyalty Liability'),
                'posting_rules' => array(
                    'shift' => 'Each till shift reconciled by tender; cash over/short posted to a variance account.',
                    'loyalty' => 'Points accrued as a liability, released on redemption.',
                ),
                'features' => array('barcode', 'shift_till', 'tender_types', 'loyalty', 'day_close_zreport'),
            ),
            'construction_contracting' => array(
                'label' => 'Construction & Contracting',
                'base' => 'building_construction',
                'costing' => 'specific',
                'uoms' => array('pcs', 'kg', 'ton', 'bag', 'm', 'sqm', 'cum', 'lot'),
                'process_flow' => array('BOQ / estimate', 'Contract & budget', 'Progress (IPC) claim', 'Retention', 'Certification', 'Final account'),
                'coa_presets' => array('Work in Progress', 'Contract Receivable', 'Retention Receivable', 'Advance from Client', 'Subcontractor Payable', 'Cost of Contract'),
                'posting_rules' => array(
                    'progress' => 'Revenue recognised on % completion; retention withheld per contract and released on certification.',
                    'wip' => 'Costs accumulate in WIP and release to COGS as work is certified.',
                ),
                'features' => array('boq', 'progress_billing', 'retention', 'wip', 'subcontractors', 'cost_centers'),
            ),
            'real_estate' => array(
                'label' => 'Real Estate & Property',
                'base' => 'services_professional',
                'costing' => 'specific',
                'uoms' => array('unit', 'sqft', 'sqm', 'plot'),
                'process_flow' => array('Listing', 'Reservation', 'Sale/Lease agreement', 'Payment plan / installments', 'Handover', 'Service charge billing'),
                'coa_presets' => array('Properties for Sale', 'Installment Receivable', 'Deferred Revenue', 'Security Deposit', 'Service Charge Income', 'Brokerage Income'),
                'posting_rules' => array(
                    'installments' => 'Sale revenue deferred and recognised per handover/POC; installments tracked as receivable.',
                ),
                'features' => array('payment_plans', 'lease_management', 'service_charges', 'deposits'),
            ),
            'rental_leasing' => array(
                'label' => 'Rental & Leasing / Equipment hire',
                'base' => 'services_professional',
                'costing' => 'standard',
                'uoms' => array('day', 'week', 'month', 'unit', 'hour'),
                'process_flow' => array('Quote', 'Reservation', 'Check-out', 'Rental period accrual', 'Check-in & condition', 'Invoice + deposit settle'),
                'coa_presets' => array('Rental Assets', 'Rental Income', 'Unearned Rental', 'Security Deposit', 'Maintenance Expense'),
                'posting_rules' => array(
                    'accrual' => 'Rental income accrued per elapsed period; deposit held as liability and refunded net of damages.',
                ),
                'features' => array('reservations', 'period_accrual', 'deposits', 'asset_availability'),
            ),
            'logistics_freight' => array(
                'label' => 'Logistics & Freight Forwarding',
                'base' => 'services_professional',
                'costing' => 'specific',
                'uoms' => array('shipment', 'container', 'kg', 'cbm', 'km'),
                'process_flow' => array('Booking', 'Job file', 'Cost accrual (carriers)', 'Selling rate', 'Job P&L', 'Invoice'),
                'coa_presets' => array('Job WIP', 'Accrued Carrier Cost', 'Freight Income', 'Customs Disbursement', 'Job Profit'),
                'posting_rules' => array(
                    'job' => 'Each job file accrues buy/sell; job profit = sell - accrued buy.',
                ),
                'features' => array('job_costing', 'accruals', 'multi_currency', 'cost_centers'),
            ),
            'manufacturing_process' => array(
                'label' => 'Process Manufacturing (chemicals/food/pharma)',
                'base' => 'industrial_manufacturing',
                'costing' => 'weighted_avg',
                'uoms' => array('kg', 'litre', 'MT', 'batch', 'drum'),
                'process_flow' => array('Formula/recipe', 'Batch order', 'Material issue', 'Yield & QC', 'Batch costing', 'FG receipt'),
                'coa_presets' => array('Raw Materials', 'WIP', 'Finished Goods', 'Yield Variance', 'Production Overhead'),
                'posting_rules' => array(
                    'batch' => 'Batch cost = materials + labour + overhead; yield variance booked on under/over recovery.',
                ),
                'features' => array('formula_recipe', 'batch_costing', 'yield_variance', 'qc', 'expiry'),
            ),
            'metal_steel' => array(
                'label' => 'Metal, Steel & Fabrication',
                'base' => 'industrial_manufacturing',
                'costing' => 'weighted_avg',
                'uoms' => array('kg', 'MT', 'pcs', 'm', 'sheet', 'coil'),
                'process_flow' => array('Enquiry', 'Cutting plan', 'Job/work order', 'Scrap recovery', 'Weighbridge', 'Dispatch'),
                'coa_presets' => array('Raw Metal Stock', 'Scrap Income', 'WIP', 'Fabrication Cost'),
                'posting_rules' => array(
                    'scrap' => 'Scrap recovered credited to scrap income; net consumption costed by weight.',
                ),
                'features' => array('weight_tracking', 'scrap', 'job_work', 'weighbridge'),
            ),
            'agriculture' => array(
                'label' => 'Agriculture & Commodities',
                'base' => 'agriculture',
                'costing' => 'weighted_avg',
                'uoms' => array('kg', 'ton', 'bag', 'crate', 'litre'),
                'process_flow' => array('Procurement (farm)', 'Grading', 'Storage (cold/silo)', 'Processing', 'Sale (grade-based)', 'Settlement'),
                'coa_presets' => array('Crop/Commodity Stock', 'Grading Loss', 'Cold Storage Cost', 'Procurement Advance'),
                'posting_rules' => array(
                    'grading' => 'Grade-based valuation; grading/shrinkage loss expensed.',
                ),
                'features' => array('grading', 'batch_lot', 'shrinkage', 'weight_tracking'),
            ),
            'automotive_workshop' => array(
                'label' => 'Automotive Garage / Workshop',
                'base' => 'auto_parts',
                'costing' => 'weighted_avg',
                'uoms' => array('pcs', 'set', 'litre', 'hour'),
                'process_flow' => array('Vehicle check-in', 'Job card (parts + labour)', 'Estimate approval', 'Repair', 'QC & test', 'Invoice + warranty'),
                'coa_presets' => array('Parts Stock', 'Labour Income', 'Parts Income', 'Sublet/Outwork', 'Warranty Provision'),
                'posting_rules' => array(
                    'jobcard' => 'Job card bills parts at sale price + labour at hourly rate; warranty jobs posted to provision.',
                ),
                'features' => array('job_card', 'labour_hours', 'vehicle_history', 'warranty'),
            ),
            'pharma_healthcare' => array(
                'label' => 'Pharma & Healthcare distribution',
                'base' => 'pharma',
                'costing' => 'fifo',
                'uoms' => array('pcs', 'box', 'strip', 'bottle', 'vial', 'ml'),
                'process_flow' => array('Procurement', 'Batch & expiry receipt', 'Quarantine/QC', 'Sale (batch FEFO)', 'Returns (expiry)', 'Recall'),
                'coa_presets' => array('Drug Stock (by batch)', 'Expiry Write-off', 'Quarantine Stock', 'Sales Return'),
                'posting_rules' => array(
                    'fefo' => 'Dispatch first-expiry-first-out; near-expiry provisioned; recalls reversed by batch.',
                ),
                'features' => array('batch_expiry', 'fefo', 'quarantine', 'recall', 'cold_chain'),
            ),
            'fnb_restaurant' => array(
                'label' => 'Food & Beverage / Restaurant',
                'base' => 'hospitality_fnb',
                'costing' => 'weighted_avg',
                'uoms' => array('plate', 'portion', 'kg', 'gram', 'litre', 'ml', 'pcs'),
                'process_flow' => array('Recipe/BOM', 'Purchase', 'Kitchen issue', 'POS sale', 'Wastage', 'Day close'),
                'coa_presets' => array('Food Stock', 'Beverage Stock', 'Wastage', 'Food Sales', 'Beverage Sales'),
                'posting_rules' => array(
                    'recipe' => 'Menu items consume ingredients by recipe; wastage expensed; POS sales close daily.',
                ),
                'features' => array('recipe_bom', 'pos', 'wastage', 'modifiers', 'table_service'),
            ),
            'textile_apparel' => array(
                'label' => 'Textile & Apparel manufacturing',
                'base' => 'fashion',
                'costing' => 'weighted_avg',
                'uoms' => array('pcs', 'm', 'yard', 'roll', 'kg', 'dozen'),
                'process_flow' => array('Style/tech-pack', 'Cutting', 'Stitching', 'Finishing', 'Pack (size ratio)', 'Dispatch'),
                'coa_presets' => array('Fabric Stock', 'Trims Stock', 'WIP (by process)', 'Finished Garments', 'Job Work'),
                'posting_rules' => array(
                    'matrix' => 'Size/colour matrix variants; WIP costed by process stage.',
                ),
                'features' => array('size_colour_matrix', 'process_wip', 'job_work', 'cutting_plan'),
            ),
            'printing_packaging' => array(
                'label' => 'Printing & Packaging',
                'base' => 'industrial_manufacturing',
                'costing' => 'specific',
                'uoms' => array('pcs', 'sheet', 'roll', 'kg', 'sqm', 'ream'),
                'process_flow' => array('Estimate (job spec)', 'Prepress', 'Print run', 'Finishing', 'Wastage/makeready', 'Delivery'),
                'coa_presets' => array('Paper/Substrate Stock', 'Ink Stock', 'Job WIP', 'Wastage', 'Print Income'),
                'posting_rules' => array(
                    'job' => 'Each print job costed specifically incl. makeready wastage.',
                ),
                'features' => array('job_estimate', 'wastage', 'specific_costing'),
            ),
            'education' => array(
                'label' => 'Education / Training institute',
                'base' => 'services_professional',
                'costing' => 'standard',
                'uoms' => array('course', 'session', 'hour', 'seat', 'month'),
                'process_flow' => array('Enrollment', 'Fee schedule', 'Installment billing', 'Attendance', 'Certification', 'Renewal'),
                'coa_presets' => array('Fee Receivable', 'Deferred Fee Income', 'Tuition Income', 'Scholarship/Discount'),
                'posting_rules' => array(
                    'fees' => 'Tuition deferred and recognised over the term; installments tracked as receivable.',
                ),
                'features' => array('enrollment', 'fee_plans', 'deferred_revenue', 'attendance'),
            ),
            'hospitality_hotel' => array(
                'label' => 'Hospitality / Hotel',
                'base' => 'services_professional',
                'costing' => 'standard',
                'uoms' => array('room-night', 'cover', 'pax', 'hour'),
                'process_flow' => array('Reservation', 'Check-in', 'Folio (room + POS)', 'Night audit', 'Check-out', 'City ledger'),
                'coa_presets' => array('Room Revenue', 'F&B Revenue', 'Guest Ledger', 'City Ledger', 'Advance Deposits'),
                'posting_rules' => array(
                    'folio' => 'Charges accumulate on guest folio; night audit posts room revenue; city ledger for corporate AR.',
                ),
                'features' => array('reservations', 'folio', 'night_audit', 'city_ledger', 'pos'),
            ),
        );
    }
}

if (!function_exists('epc_erp_industry_pack')) {
    /**
     * @return array<string,mixed>|null
     */
    function epc_erp_industry_pack(string $key): ?array
    {
        $packs = epc_erp_industry_packs();
        return $packs[$key] ?? null;
    }
}

if (!function_exists('epc_erp_industry_pack_keys')) {
    /**
     * @return array<int,string>
     */
    function epc_erp_industry_pack_keys(): array
    {
        return array_keys(epc_erp_industry_packs());
    }
}

if (!function_exists('epc_erp_pack_inventory_fields')) {
    /**
     * Resolve the product / inventory field blueprint a pack releases. Fields are
     * inherited from the pack's `base` industry catalog entry, and each is given
     * a default classification (inventory vs non_inventory):
     *   - a pack may override per field via an optional `field_roles` map;
     *   - otherwise service-style bases default to non_inventory, goods to inventory.
     *
     * @return array<int,array<string,mixed>> each: field_key,label,field_type,options?,field_role
     */
    function epc_erp_pack_inventory_fields(string $packKey): array
    {
        $pack = epc_erp_industry_pack($packKey);
        if ($pack === null) {
            return array();
        }
        require_once __DIR__ . '/epc_erp_industry.php';
        $baseKey = (string) ($pack['base'] ?? 'general');
        $bp = function_exists('epc_erp_industry_blueprint') ? epc_erp_industry_blueprint($baseKey) : null;
        if ($bp === null || empty($bp['fields'])) {
            return array();
        }
        // Service-style industries describe non-stock items by default.
        $serviceBases = array('services_professional', 'hospitality_fnb');
        $defaultRole = in_array($baseKey, $serviceBases, true) ? 'non_inventory' : 'inventory';
        $overrides = isset($pack['field_roles']) && is_array($pack['field_roles']) ? $pack['field_roles'] : array();

        $out = array();
        foreach ($bp['fields'] as $f) {
            $key = (string) ($f['field_key'] ?? '');
            if ($key === '') {
                continue;
            }
            $role = isset($overrides[$key]) ? (string) $overrides[$key] : $defaultRole;
            $role = ($role === 'non_inventory') ? 'non_inventory' : 'inventory';
            $out[] = array(
                'field_key' => $key,
                'label' => (string) ($f['label'] ?? $key),
                'field_type' => (string) ($f['field_type'] ?? 'text'),
                'options' => isset($f['options']) && is_array($f['options']) ? $f['options'] : null,
                'field_role' => $role,
            );
        }
        return $out;
    }
}

if (!function_exists('epc_erp_pack_apply_fields')) {
    /**
     * Seed the pack's product/inventory field definitions into the tenant's
     * field-def table. Additive + idempotent: re-applying refreshes labels/types
     * but never deletes fields and never overwrites a role the admin has changed
     * (role is only set when a field is first inserted).
     *
     * @return array<string,mixed>
     */
    function epc_erp_pack_apply_fields(PDO $db, string $packKey, int $adminId = 0): array
    {
        require_once __DIR__ . '/epc_erp_inventory.php';
        $fields = epc_erp_pack_inventory_fields($packKey);
        $seeded = 0;
        $sort = 100;
        foreach ($fields as $f) {
            $sort += 10;
            $f['sort_order'] = $sort;
            $f['source_pack'] = $packKey;
            if (epc_erp_inv_field_upsert($db, $f)) {
                $seeded++;
            }
        }
        return array('status' => true, 'fields_seeded' => $seeded);
    }
}

/* ==========================================================================
 * Specialized, unit-tested accounting helpers
 * ======================================================================== */

if (!function_exists('epc_ind_jewellery_value')) {
    /**
     * Jewellery line valuation.
     *
     * @param float $netWeightG metal net weight in grams
     * @param float $purityKarat e.g. 22 (out of 24)
     * @param float $goldRatePerG rate of the day for 24K per gram
     * @param float $makingPerG making charge per gram
     * @param float $stoneValue specific stone/diamond cost
     * @param float $vatPercent VAT applied (jurisdiction dependent; many apply
     *        VAT on making + stones only, not investment metal)
     * @param bool $vatOnMetal whether VAT also applies to metal value
     * @return array<string,float>
     */
    function epc_ind_jewellery_value(float $netWeightG, float $purityKarat, float $goldRatePerG, float $makingPerG = 0.0, float $stoneValue = 0.0, float $vatPercent = 0.0, bool $vatOnMetal = false): array
    {
        $purityFactor = max(0.0, min(1.0, $purityKarat / 24.0));
        $metalValue = round($netWeightG * $purityFactor * $goldRatePerG, 2);
        $making = round($netWeightG * $makingPerG, 2);
        $stones = round($stoneValue, 2);
        $taxableBase = $making + $stones + ($vatOnMetal ? $metalValue : 0.0);
        $vat = round($taxableBase * $vatPercent / 100, 2);
        $total = round($metalValue + $making + $stones + $vat, 2);
        return array(
            'metal_value' => $metalValue,
            'making_charge' => $making,
            'stone_value' => $stones,
            'pure_weight_g' => round($netWeightG * $purityFactor, 3),
            'vat' => $vat,
            'total' => $total,
        );
    }
}

if (!function_exists('epc_ind_construction_progress_bill')) {
    /**
     * Progress (interim payment certificate) billing with retention.
     *
     * @param float $contractValue total contract value
     * @param float $percentComplete 0..100 cumulative completion
     * @param float $previouslyCertified amount certified in prior IPCs (net of nothing; gross work done)
     * @param float $retentionPercent retention withheld %
     * @param float $vatPercent
     * @return array<string,float>
     */
    function epc_ind_construction_progress_bill(float $contractValue, float $percentComplete, float $previouslyCertified = 0.0, float $retentionPercent = 5.0, float $vatPercent = 0.0): array
    {
        $pct = max(0.0, min(100.0, $percentComplete));
        $workDoneToDate = round($contractValue * $pct / 100, 2);
        $thisPeriodGross = round($workDoneToDate - $previouslyCertified, 2);
        if ($thisPeriodGross < 0) {
            $thisPeriodGross = 0.0;
        }
        $retention = round($thisPeriodGross * $retentionPercent / 100, 2);
        $netBeforeVat = round($thisPeriodGross - $retention, 2);
        $vat = round($netBeforeVat * $vatPercent / 100, 2);
        return array(
            'work_done_to_date' => $workDoneToDate,
            'this_period_gross' => $thisPeriodGross,
            'retention' => $retention,
            'net_before_vat' => $netBeforeVat,
            'vat' => $vat,
            'net_payable' => round($netBeforeVat + $vat, 2),
        );
    }
}

if (!function_exists('epc_ind_oilgas_jv_split')) {
    /**
     * Split a joint cost across partners by working interest. Interests are
     * normalised if they do not sum to 100. The largest share absorbs any
     * rounding remainder so the splits reconcile exactly to the total.
     *
     * @param float $amount cost to split
     * @param array<string,float> $interests partner => working interest %
     * @return array<string,float> partner => amount
     */
    function epc_ind_oilgas_jv_split(float $amount, array $interests): array
    {
        $sum = array_sum($interests);
        if ($sum <= 0) {
            return array();
        }
        $out = array();
        $allocated = 0.0;
        $maxKey = null;
        $maxVal = -1.0;
        foreach ($interests as $k => $wi) {
            $share = round($amount * ($wi / $sum), 2);
            $out[$k] = $share;
            $allocated = round($allocated + $share, 2);
            if ($wi > $maxVal) {
                $maxVal = $wi;
                $maxKey = $k;
            }
        }
        $remainder = round($amount - $allocated, 2);
        if ($maxKey !== null && abs($remainder) >= 0.01) {
            $out[$maxKey] = round($out[$maxKey] + $remainder, 2);
        }
        return $out;
    }
}

if (!function_exists('epc_ind_pos_shift_reconcile')) {
    /**
     * Till/shift reconciliation by tender. Cash expected = float + cash sales -
     * payouts; variance = counted - expected (negative = short).
     *
     * @param array<string,mixed> $shift float_amount, cash_sales, card_sales,
     *        wallet_sales, payouts, counted_cash, counted_card, counted_wallet
     * @return array<string,mixed>
     */
    function epc_ind_pos_shift_reconcile(array $shift): array
    {
        $float = (float) ($shift['float_amount'] ?? 0);
        $cashSales = (float) ($shift['cash_sales'] ?? 0);
        $payouts = (float) ($shift['payouts'] ?? 0);
        $expectedCash = round($float + $cashSales - $payouts, 2);
        $countedCash = (float) ($shift['counted_cash'] ?? 0);
        $cashVar = round($countedCash - $expectedCash, 2);

        $cardExpected = (float) ($shift['card_sales'] ?? 0);
        $cardVar = round((float) ($shift['counted_card'] ?? $cardExpected) - $cardExpected, 2);
        $walletExpected = (float) ($shift['wallet_sales'] ?? 0);
        $walletVar = round((float) ($shift['counted_wallet'] ?? $walletExpected) - $walletExpected, 2);

        $totalVar = round($cashVar + $cardVar + $walletVar, 2);
        return array(
            'expected_cash' => $expectedCash,
            'cash_variance' => $cashVar,
            'card_variance' => $cardVar,
            'wallet_variance' => $walletVar,
            'total_variance' => $totalVar,
            'status' => abs($totalVar) < 0.01 ? 'balanced' : ($totalVar < 0 ? 'short' : 'over'),
        );
    }
}

if (!function_exists('epc_ind_rental_accrual')) {
    /**
     * Rental revenue accrued for elapsed days of a rental period.
     *
     * @return array<string,mixed>
     */
    function epc_ind_rental_accrual(float $ratePerDay, int $startTs, int $endTs, int $asOf): array
    {
        if ($endTs <= $startTs) {
            return array('elapsed_days' => 0, 'total_days' => 0, 'accrued' => 0.0, 'remaining' => 0.0);
        }
        $totalDays = (int) ceil(($endTs - $startTs) / 86400);
        $asOf = max($startTs, min($asOf, $endTs));
        $elapsedDays = (int) floor(($asOf - $startTs) / 86400);
        $elapsedDays = max(0, min($elapsedDays, $totalDays));
        $accrued = round($ratePerDay * $elapsedDays, 2);
        $total = round($ratePerDay * $totalDays, 2);
        return array(
            'elapsed_days' => $elapsedDays,
            'total_days' => $totalDays,
            'accrued' => $accrued,
            'remaining' => round($total - $accrued, 2),
            'total_contract' => $total,
        );
    }
}

if (!function_exists('epc_erp_resolve_pack_from_consolidation')) {
    /**
     * Resolve the ERP industry pack using the consolidation engine.
     *
     * Given a tenant's industry code, resolves the consolidated group
     * and returns the appropriate ERP pack from epc_erp_industry_packs().
     *
     * @param string $industryCode Tenant's industry code
     * @return array|null The ERP pack config, or null if not found
     */
    function epc_erp_resolve_pack_from_consolidation(string $industryCode): ?array
    {
        require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_consolidation.php';

        $group = epc_industry_get_group($industryCode);
        $erpBase = $group['erp_base'] ?? 'general';

        $packs = epc_erp_industry_packs();
        if (isset($packs[$erpBase])) {
            return $packs[$erpBase];
        }

        // Try direct match on industry code
        if (isset($packs[$industryCode])) {
            return $packs[$industryCode];
        }

        return null;
    }
}

if (!function_exists('epc_erp_costing_for_industry')) {
    /**
     * Get the costing method for a given industry based on consolidation group.
     *
     * @param string $industryCode Tenant's industry code
     * @return string Costing method key (fifo, weighted_avg, specific, standard)
     */
    function epc_erp_costing_for_industry(string $industryCode): string
    {
        require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_consolidation.php';
        $group = epc_industry_get_group($industryCode);
        return $group['costing_default'] ?? 'weighted_avg';
    }
}
