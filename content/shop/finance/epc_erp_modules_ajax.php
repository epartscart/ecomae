<?php
/**
 * Unified AJAX handler for ERP modules (22-feature batch).
 * Routes module operations to the appropriate backend functions.
 * Called from CP ERP tabs via AJAX POST.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_erp_modules_ajax_handler')) {
    function epc_erp_modules_ajax_handler(PDO $db, int $companyId, int $userId, string $userName): array
    {
        $module = $_POST['module'] ?? $_GET['module'] ?? '';
        $action = $_POST['action'] ?? $_GET['action'] ?? '';

        if (empty($module) || empty($action)) {
            return ['ok' => false, 'error' => 'Module and action required'];
        }

        // Load the appropriate module file
        $moduleFiles = [
            'sla' => 'epc_erp_sla.php',
            'tickets' => 'epc_erp_tickets.php',
            'doc_attachment' => 'epc_erp_doc_attachment.php',
            'customer_groups' => 'epc_erp_customer_groups.php',
            'gold_scheme' => 'epc_erp_gold_scheme.php',
            'gold_rate' => 'epc_erp_gold_rate.php',
            'ecommerce_integration' => 'epc_erp_ecommerce_integration.php',
            'data_migration' => 'epc_erp_data_migration.php',
            'crm_integration' => 'epc_erp_crm_integration.php',
            'report_scheduler' => 'epc_erp_report_scheduler.php',
            'aml_compliance' => 'epc_erp_aml_compliance.php',
            'jewellery_tag' => 'epc_erp_jewellery_tag.php',
            'tourist_refund' => 'epc_erp_tourist_refund.php',
            'card_reader' => 'epc_erp_card_reader.php',
            'shortcut_icons' => 'epc_erp_shortcut_icons.php',
            'drilldown' => 'epc_erp_drilldown.php',
            'barcode_purchase' => 'epc_erp_barcode_purchase.php',
            'fix_unfix' => 'epc_erp_fix_unfix.php',
            'virtual_warehouse' => 'epc_erp_virtual_warehouse.php',
            'rfid' => 'epc_erp_rfid.php',
            'inventory_report' => 'epc_erp_inventory_report.php',
            'landed_cost_v2' => 'epc_erp_landed_cost_v2.php',
        ];

        if (!isset($moduleFiles[$module])) {
            return ['ok' => false, 'error' => 'Unknown module: ' . $module];
        }

        $filePath = __DIR__ . '/' . $moduleFiles[$module];
        if (!file_exists($filePath)) {
            return ['ok' => false, 'error' => 'Module file not found'];
        }
        require_once $filePath;

        // Route by module + action
        switch ($module) {
            case 'sla':
                epc_sla_ensure_schema($db);
                if ($action === 'list') return ['ok' => true, 'data' => epc_sla_list($db, $companyId)];
                if ($action === 'create') return ['ok' => true, 'id' => epc_sla_create($db, array_merge($_POST, ['company_id' => $companyId]))];
                break;

            case 'tickets':
                epc_tickets_ensure_schema($db);
                if ($action === 'list') return ['ok' => true, 'data' => epc_tickets_list($db, $companyId, $_POST['status'] ?? '')];
                if ($action === 'create') return ['ok' => true, 'id' => epc_tickets_create($db, array_merge($_POST, ['company_id' => $companyId]))];
                if ($action === 'reply') return ['ok' => true, 'id' => epc_tickets_add_reply($db, (int) ($_POST['ticket_id'] ?? 0), array_merge($_POST, ['author_id' => $userId, 'author_name' => $userName]))];
                break;

            case 'doc_attachment':
                epc_doc_attach_ensure_schema($db);
                if ($action === 'list') return ['ok' => true, 'data' => epc_doc_attach_list($db, $_POST['entity_type'] ?? '', (int) ($_POST['entity_id'] ?? 0))];
                if ($action === 'upload') return epc_doc_attach_upload($db, array_merge($_POST, ['company_id' => $companyId, 'uploaded_by' => $userId, 'uploaded_by_name' => $userName]), $_FILES['file'] ?? []);
                if ($action === 'delete') return ['ok' => epc_doc_attach_delete($db, (int) ($_POST['id'] ?? 0))];
                break;

            case 'customer_groups':
                epc_cust_groups_ensure_schema($db);
                if ($action === 'list') return ['ok' => true, 'data' => epc_cust_groups_list($db, $companyId)];
                if ($action === 'create') return ['ok' => true, 'id' => epc_cust_groups_create($db, array_merge($_POST, ['company_id' => $companyId]))];
                if ($action === 'assign') return ['ok' => epc_cust_groups_assign($db, (int) ($_POST['group_id'] ?? 0), (int) ($_POST['customer_id'] ?? 0))];
                break;

            case 'gold_scheme':
                epc_gold_scheme_ensure_schema($db);
                if ($action === 'list_schemes') { $stmt = $db->prepare("SELECT * FROM `epc_gold_schemes` WHERE `company_id` = ? ORDER BY `scheme_name`"); $stmt->execute([$companyId]); return ['ok' => true, 'data' => $stmt->fetchAll(PDO::FETCH_ASSOC)]; }
                if ($action === 'create') return ['ok' => true, 'id' => epc_gold_scheme_create($db, array_merge($_POST, ['company_id' => $companyId]))];
                if ($action === 'enroll') return ['ok' => true, 'id' => epc_gold_scheme_enroll($db, array_merge($_POST, ['company_id' => $companyId]))];
                if ($action === 'pay') return ['ok' => true, 'id' => epc_gold_scheme_pay_installment($db, (int) ($_POST['enrollment_id'] ?? 0), $_POST)];
                if ($action === 'matured') return ['ok' => true, 'data' => epc_gold_scheme_check_maturity($db, $companyId)];
                break;

            case 'gold_rate':
                epc_gold_rate_ensure_schema($db);
                if ($action === 'today') return ['ok' => true, 'data' => epc_gold_rate_get_today($db, $companyId, $_POST['karat'] ?? '24K', $_POST['currency'] ?? 'AED')];
                if ($action === 'set') return ['ok' => true, 'id' => epc_gold_rate_set($db, array_merge($_POST, ['company_id' => $companyId]))];
                if ($action === 'fetch_api') return epc_gold_rate_fetch_api($db, $companyId);
                if ($action === 'history') return ['ok' => true, 'data' => epc_gold_rate_history($db, $companyId, $_POST['karat'] ?? '24K', (int) ($_POST['days'] ?? 30))];
                break;

            case 'ecommerce_integration':
                epc_ecom_int_ensure_schema($db);
                if ($action === 'list') return ['ok' => true, 'data' => epc_ecom_int_list_connections($db, $companyId)];
                if ($action === 'connect') return ['ok' => true, 'id' => epc_ecom_int_connect($db, array_merge($_POST, ['company_id' => $companyId]))];
                break;

            case 'data_migration':
                epc_data_mig_ensure_schema($db);
                if ($action === 'templates') return ['ok' => true, 'data' => epc_data_mig_get_templates()];
                if ($action === 'upload') return epc_data_mig_upload($db, array_merge($_POST, ['company_id' => $companyId, 'user_id' => $userId, 'user_name' => $userName]), $_FILES['file'] ?? []);
                break;

            case 'crm_integration':
                epc_crm_int_ensure_schema($db);
                if ($action === 'list') return ['ok' => true, 'data' => epc_crm_int_list($db, $companyId)];
                break;

            case 'report_scheduler':
                epc_report_sched_ensure_schema($db);
                if ($action === 'list') return ['ok' => true, 'data' => epc_report_sched_list($db, $companyId)];
                if ($action === 'create') return ['ok' => true, 'id' => epc_report_sched_create($db, array_merge($_POST, ['company_id' => $companyId, 'created_by' => $userId]))];
                if ($action === 'send') return epc_report_sched_send($db, (int) ($_POST['schedule_id'] ?? 0));
                break;

            case 'aml_compliance':
                epc_aml_ensure_schema($db);
                if ($action === 'check') {
                    $res = epc_aml_check_transaction(
                        $db,
                        $companyId,
                        (int) ($_POST['customer_id'] ?? 0),
                        (float) ($_POST['amount'] ?? 0),
                        (string) ($_POST['currency'] ?? 'AED'),
                        array(
                            'customer_name' => (string) ($_POST['customer_name'] ?? ''),
                            'transaction_type' => (string) ($_POST['transaction_type'] ?? 'cash_sale'),
                            'reference' => (string) ($_POST['reference'] ?? ''),
                        )
                    );
                    return array('ok' => true, 'data' => $res) + $res;
                }
                if ($action === 'default_rules') return ['ok' => true, 'data' => epc_aml_default_rules($companyId)];
                if ($action === 'seed_rules') return epc_aml_seed_rules($db, $companyId);
                if ($action === 'kyc_save') return epc_aml_kyc_save($db, array_merge($_POST, ['company_id' => $companyId]));
                if ($action === 'report_generate') {
                    return epc_aml_generate_report(
                        $db,
                        (string) ($_POST['report_type'] ?? 'compliance_summary'),
                        (string) ($_POST['period_from'] ?? date('Y-m-01')),
                        (string) ($_POST['period_to'] ?? date('Y-m-d')),
                        $userId
                    );
                }
                break;

            case 'jewellery_tag':
                epc_jw_tag_ensure_schema($db);
                if ($action === 'create') return ['ok' => true, 'id' => epc_jw_tag_create($db, array_merge($_POST, ['company_id' => $companyId]))];
                if ($action === 'sell') return ['ok' => epc_jw_tag_sell($db, (int) ($_POST['tag_id'] ?? 0), (int) ($_POST['invoice_id'] ?? 0), (int) ($_POST['salesman_id'] ?? 0))];
                if ($action === 'search') return ['ok' => true, 'data' => epc_jw_tag_search($db, $companyId, $_POST)];
                break;

            case 'tourist_refund':
                epc_tourist_refund_ensure_schema($db);
                if ($action === 'create') return epc_tourist_refund_create($db, array_merge($_POST, ['company_id' => $companyId]));
                if ($action === 'validate') return epc_tourist_refund_validate($db, $_POST['barcode'] ?? '');
                break;

            case 'card_reader':
                epc_card_reader_ensure_schema($db);
                if ($action === 'scan') return epc_card_reader_save_scan($db, array_merge($_POST, ['company_id' => $companyId, 'scanned_by' => $userId]), $_FILES ?? []);
                if ($action === 'list') return ['ok' => true, 'data' => epc_card_reader_get_customer_scans($db, (int) ($_POST['customer_id'] ?? 0))];
                break;

            case 'shortcut_icons':
                epc_shortcuts_ensure_schema($db);
                if ($action === 'list') return ['ok' => true, 'data' => epc_shortcuts_list($db, $userId)];
                if ($action === 'add') return ['ok' => true, 'id' => epc_shortcuts_add($db, array_merge($_POST, ['company_id' => $companyId, 'user_id' => $userId]))];
                if ($action === 'reorder') return ['ok' => epc_shortcuts_reorder($db, $userId, json_decode($_POST['ids'] ?? '[]', true) ?: [])];
                if ($action === 'delete') return ['ok' => epc_shortcuts_delete($db, (int) ($_POST['id'] ?? 0), $userId)];
                break;

            case 'drilldown':
                epc_drilldown_ensure_schema($db);
                if ($action === 'journals') return ['ok' => true, 'data' => epc_drilldown_get_journal_entries($db, $companyId, $_POST['account_code'] ?? '', $_POST['from'] ?? date('Y-01-01'), $_POST['to'] ?? date('Y-12-31'))];
                if ($action === 'invoices') return ['ok' => true, 'data' => epc_drilldown_get_invoices($db, $companyId, (int) ($_POST['customer_id'] ?? 0), $_POST['from'] ?? date('Y-01-01'), $_POST['to'] ?? date('Y-12-31'))];
                break;

            case 'barcode_purchase':
                epc_barcode_purchase_ensure_schema($db);
                if ($action === 'create') return ['ok' => true, 'id' => epc_barcode_purchase_create($db, array_merge($_POST, ['company_id' => $companyId]))];
                if ($action === 'lookup') return ['ok' => true, 'data' => epc_barcode_purchase_lookup($db, $companyId, $_POST['barcode'] ?? '')];
                if ($action === 'sell') return ['ok' => epc_barcode_purchase_sell($db, (int) ($_POST['id'] ?? 0), (int) ($_POST['customer_id'] ?? 0), (int) ($_POST['invoice_id'] ?? 0))];
                break;

            case 'fix_unfix':
                epc_fix_unfix_ensure_schema($db);
                if ($action === 'create') return ['ok' => true, 'id' => epc_fix_unfix_create($db, array_merge($_POST, ['company_id' => $companyId]))];
                if ($action === 'settle') return epc_fix_unfix_settle($db, (int) ($_POST['id'] ?? 0), (float) ($_POST['settle_rate'] ?? 0));
                if ($action === 'report') return ['ok' => true, 'data' => epc_fix_unfix_report($db, $companyId, $_POST['from'] ?? '', $_POST['to'] ?? '')];
                break;

            case 'virtual_warehouse':
                epc_vwh_ensure_schema($db);
                if ($action === 'list') return ['ok' => true, 'data' => epc_vwh_list_warehouses($db, $companyId, $_POST['type'] ?? '')];
                if ($action === 'create') return ['ok' => true, 'id' => epc_vwh_create_warehouse($db, array_merge($_POST, ['company_id' => $companyId]))];
                if ($action === 'transfer') return ['ok' => true, 'id' => epc_vwh_create_transfer($db, array_merge($_POST, ['company_id' => $companyId, 'created_by' => $userId]), json_decode($_POST['lines'] ?? '[]', true) ?: [])];
                break;

            case 'rfid':
                epc_rfid_ensure_schema($db);
                if ($action === 'register') return ['ok' => true, 'id' => epc_rfid_register_tag($db, array_merge($_POST, ['company_id' => $companyId, 'registered_by' => $userId]))];
                if ($action === 'start_session') return ['ok' => true, 'id' => epc_rfid_start_scan_session($db, array_merge($_POST, ['company_id' => $companyId, 'scanned_by' => $userId, 'scanned_by_name' => $userName]))];
                if ($action === 'scan') return epc_rfid_process_scan($db, (int) ($_POST['session_id'] ?? 0), $_POST['rfid_epc'] ?? '', $companyId, (int) ($_POST['rssi'] ?? 0));
                if ($action === 'list') return ['ok' => true, 'data' => epc_rfid_list_tags($db, $companyId, (int) ($_POST['warehouse_id'] ?? 0))];
                break;

            case 'inventory_report':
                epc_inv_report_ensure_schema($db);
                if ($action === 'by_category') return ['ok' => true, 'data' => epc_inv_report_by_category($db, $companyId, (int) ($_POST['parent_id'] ?? 0))];
                if ($action === 'abc') return ['ok' => true, 'data' => epc_inv_report_abc_analysis($db, $companyId)];
                if ($action === 'aging') return ['ok' => true, 'data' => epc_inv_report_aging($db, $companyId)];
                break;

            case 'landed_cost_v2':
                epc_landed_cost_v2_ensure_schema($db);
                if ($action === 'list') return ['ok' => true, 'data' => epc_landed_cost_v2_list($db, $companyId)];
                if ($action === 'calculate') return epc_landed_cost_v2_calculate($db, (int) ($_POST['sheet_id'] ?? 0));
                if ($action === 'post') return epc_landed_cost_v2_post($db, (int) ($_POST['sheet_id'] ?? 0));
                break;
        }

        return ['ok' => false, 'error' => 'Unknown action: ' . $action . ' for module: ' . $module];
    }
}
