<?php
/**
 * ERP global search — modules/tabs + real records.
 *
 * Usage:
 *   require_once '.../epc_erp_global_search.php';
 *   $results = epc_erp_global_search($db, $query, 20);
 */
defined('_ASTEXE_') or die('No access');

/**
 * Search ERP modules (tab/area names) and real records across several tables.
 *
 * Tenant scoping: the $db connection is already wired to the correct tenant
 * database by epc_erp_assert_tenant_db_context() before this is called
 * (same pattern as every other query in this module).
 *
 * Company scoping: tables that carry a company_id column are filtered with
 * epc_erp_active_company_id($db) — the same helper used everywhere else.
 * Tables that have no company_id column (suppliers, GL journals, purchase
 * orders, e-invoice documents) are tenant-scoped only, matching what the
 * existing sibling queries in ajax_erp.php do.
 *
 * @param PDO    $db    Tenant-scoped PDO connection (already context-asserted).
 * @param string $query Search string (minimum 2 characters recommended).
 * @param int    $limit Maximum total results returned.
 * @return array<int, array<string, mixed>>
 */
function epc_erp_global_search(PDO $db, string $query, int $limit = 20): array
{
    $query = trim($query);
    if (mb_strlen($query, 'UTF-8') < 2) {
        return array();
    }

    // ── 1. Resolve company id for tables that need it ──────────────────────
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
    $companyId = epc_erp_active_company_id($db);

    // ── 2. Load nav areas (tenant-filtered) ────────────────────────────────
    // erp_nav_areas.php defines both epc_erp_nav_areas_for_tenant() and the
    // helper functions we need (epc_erp_nav_label_plain, epc_erp_tab_to_area).
    require_once $_SERVER['DOCUMENT_ROOT'] . '/cp/content/shop/finance/erp/erp_nav_areas.php';

    $results  = array();
    $qLower   = mb_strtolower($query, 'UTF-8');
    $perType  = max(4, (int) floor($limit / 4));

    // ── 3. Module / tab name matches ───────────────────────────────────────
    // Use epc_erp_nav_areas_for_tenant() so jewellery / module-gating is
    // respected — same call the sidebar itself makes.
    $areas = epc_erp_nav_areas_for_tenant();
    foreach ($areas as $areaKey => $area) {
        if (empty($area['tabs']) || !is_array($area['tabs'])) {
            continue;
        }
        $areaLabelPlain = mb_strtolower(
            epc_erp_nav_label_plain($area['label'] ?? $areaKey),
            'UTF-8'
        );
        foreach ($area['tabs'] as $tabKey => $meta) {
            // Skip external links — they produce dead ERP URLs.
            if (!empty($meta['external'])) {
                continue;
            }
            $tabLabelPlain = mb_strtolower(
                epc_erp_nav_label_plain($meta['label'] ?? $tabKey),
                'UTF-8'
            );
            if (strpos($tabLabelPlain, $qLower) === false
                && strpos($areaLabelPlain, $qLower) === false
            ) {
                continue;
            }
            $results[] = array(
                'type'  => 'module',
                'label' => epc_erp_nav_label_plain($meta['label'] ?? $tabKey),
                'sub'   => epc_erp_nav_label_plain($area['label'] ?? $areaKey),
                'tab'   => $tabKey,
                'area'  => $areaKey,
                'icon'  => $meta['icon'] ?? 'fa-circle-o',
                // URL built via epc_erp_tab_url() exactly as the sidebar does.
                // from/to left empty — the ERP shell fills in current period.
                'url'   => epc_erp_tab_url(
                    '/cp/content/shop/finance/erp/erp_main_page.php',
                    $tabKey,
                    '',
                    '',
                    $areaKey
                ),
            );
        }
    }

    // ── 4. Escape LIKE wildcards ───────────────────────────────────────────
    $like = '%' . str_replace(array('%', '_'), array('\\%', '\\_'), $query) . '%';
    $n    = $perType;

    // ── 5. Customers (dp_users — tenant-scoped, no company_id column) ──────
    // Receivables / AR screen does not deep-link to individual users via ?id=
    // so we omit a direct record URL and just point at the receivables tab.
    // We only include customers whose ERP-side data is visible (no company
    // column on dp_users — tenant isolation is at the DB level).
    try {
        $st = $db->prepare(
            "SELECT `id`, `firstname`, `lastname`, `email`
               FROM `dp_users`
              WHERE (CONCAT(`firstname`,' ',`lastname`) LIKE ?
                     OR `email` LIKE ?
                     OR `id` = ?)
              LIMIT ?"
        );
        $st->execute(array($like, $like, is_numeric($query) ? (int) $query : -1, $n));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $name      = trim($r['firstname'] . ' ' . $r['lastname']);
            $name      = $name !== '' ? $name : $r['email'];
            $results[] = array(
                'type'        => 'record',
                'record_type' => 'customer',
                'label'       => $name,
                'sub'         => $r['email'],
                'tab'         => 'receivables',
                'area'        => 'ar',
                'icon'        => 'fa-user',
                // No deep-link — AR receivables screen doesn't read ?user_id=
                // directly to open a single customer record; point at the list.
                'url'         => epc_erp_tab_url(
                    '/cp/content/shop/finance/erp/erp_main_page.php',
                    'receivables',
                    '',
                    '',
                    'ar'
                ),
            );
        }
    } catch (Throwable $e) {
        // Table may not exist in all tenants — silently skip.
    }

    // ── 6. AR Invoices (epc_einvoice_documents) ────────────────────────────
    // Columns confirmed: invoice_number, buyer_name. No company_id column.
    // The e-invoice tab (sales > invoices) reads ?inv_id=&inv_action=view
    // which is handled by erp_tabs_einvoice.php — confirmed via grep.
    try {
        $st = $db->prepare(
            "SELECT `id`, `invoice_number`, `buyer_name`
               FROM `epc_einvoice_documents`
              WHERE `active` = 1
                AND (`invoice_number` LIKE ? OR `buyer_name` LIKE ?)
              ORDER BY `id` DESC
              LIMIT ?"
        );
        $st->execute(array($like, $like, $n));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $inv       = $r['invoice_number'] ?: ('INV #' . $r['id']);
            $buyer     = (string) ($r['buyer_name'] ?? '');
            $results[] = array(
                'type'        => 'record',
                'record_type' => 'invoice',
                'label'       => $inv . ($buyer !== '' ? ' — ' . $buyer : ''),
                'sub'         => $buyer,
                'tab'         => 'invoices',
                'area'        => 'sales',
                'icon'        => 'fa-file-text-o',
                'url'         => epc_erp_tab_url(
                    '/cp/content/shop/finance/erp/erp_main_page.php',
                    'invoices',
                    '',
                    '',
                    'sales'
                ) . '&inv_id=' . (int) $r['id'] . '&inv_action=view',
            );
        }
    } catch (Throwable $e) {}

    // ── 7. Purchase Orders (epc_erp_purchase_orders) ──────────────────────
    // Columns confirmed: po_no, supplier_id (no company_id column).
    // purchase_orders tab is in area 'purchasing'. The tab file reads
    // a selected PO via the list UI — no direct ?po_id= deep-link param —
    // so we point at the purchase_orders tab list.
    try {
        $st = $db->prepare(
            "SELECT p.`id`, p.`po_no`,
                    s.`name` AS supplier_name
               FROM `epc_erp_purchase_orders` p
               LEFT JOIN `epc_erp_suppliers` s ON s.`id` = p.`supplier_id`
              WHERE p.`po_no` LIKE ? OR s.`name` LIKE ?
              ORDER BY p.`id` DESC
              LIMIT ?"
        );
        $st->execute(array($like, $like, $n));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $poLabel   = $r['po_no'] ?: ('PO #' . $r['id']);
            $suppLabel = (string) ($r['supplier_name'] ?? '');
            $results[] = array(
                'type'        => 'record',
                'record_type' => 'po',
                'label'       => $poLabel . ($suppLabel !== '' ? ' — ' . $suppLabel : ''),
                'sub'         => $suppLabel,
                'tab'         => 'purchase_orders',
                'area'        => 'purchasing',
                'icon'        => 'fa-clipboard',
                'url'         => epc_erp_tab_url(
                    '/cp/content/shop/finance/erp/erp_main_page.php',
                    'purchase_orders',
                    '',
                    '',
                    'purchasing'
                ),
            );
        }
    } catch (Throwable $e) {}

    // ── 8. GL Journals (epc_erp_gl_journals) ──────────────────────────────
    // Columns confirmed: id, reference, description, active. No company_id.
    // The GL tab reads ?journal_id= — confirmed in erp_tabs_accounting.php.
    try {
        $st = $db->prepare(
            "SELECT `id`, `reference`, `description`
               FROM `epc_erp_gl_journals`
              WHERE `active` = 1
                AND (`reference` LIKE ? OR `description` LIKE ?)
              ORDER BY `id` DESC
              LIMIT ?"
        );
        $st->execute(array($like, $like, $n));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $ref       = $r['reference'] ?: ('Journal #' . $r['id']);
            $desc      = mb_substr((string) ($r['description'] ?? ''), 0, 60, 'UTF-8');
            $results[] = array(
                'type'        => 'record',
                'record_type' => 'journal',
                'label'       => $ref . ($desc !== '' ? ' — ' . $desc : ''),
                'sub'         => $desc,
                'tab'         => 'gl',
                'area'        => 'finance',
                'icon'        => 'fa-book',
                'url'         => epc_erp_tab_url(
                    '/cp/content/shop/finance/erp/erp_main_page.php',
                    'gl',
                    '',
                    '',
                    'finance'
                ) . '&journal_id=' . (int) $r['id'],
            );
        }
    } catch (Throwable $e) {}

    // ── 9. Insurance policies (epc_erp_ins_policies) ──────────────────────
    // Columns confirmed: id, company_id, policy_no, insurer. Scoped by company.
    // Insurance tab (risk > insurance) — no ?id= deep-link; point at tab list.
    try {
        $st = $db->prepare(
            "SELECT `id`, `policy_no`, `insurer`
               FROM `epc_erp_ins_policies`
              WHERE `company_id` = ?
                AND (`policy_no` LIKE ? OR `insurer` LIKE ?)
              ORDER BY `id` DESC
              LIMIT ?"
        );
        $st->execute(array($companyId, $like, $like, $n));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $pol       = $r['policy_no'] ?: ('Policy #' . $r['id']);
            $ins       = (string) ($r['insurer'] ?? '');
            $results[] = array(
                'type'        => 'record',
                'record_type' => 'policy',
                'label'       => $pol . ($ins !== '' ? ' — ' . $ins : ''),
                'sub'         => $ins,
                'tab'         => 'insurance',
                'area'        => 'risk',
                'icon'        => 'fa-shield',
                'url'         => epc_erp_tab_url(
                    '/cp/content/shop/finance/erp/erp_main_page.php',
                    'insurance',
                    '',
                    '',
                    'risk'
                ),
            );
        }
    } catch (Throwable $e) {}

    // ── 10. Tickets (epc_tickets) ──────────────────────────────────────────
    // Columns confirmed: id, company_id, ticket_no, subject. Scoped by company.
    // Tickets tab (no area in nav config directly; 'tickets' area if present).
    // We skip deep-link — no ?ticket_id= param is handled in the tickets tab.
    try {
        $st = $db->prepare(
            "SELECT `id`, `ticket_no`, `subject`
               FROM `epc_tickets`
              WHERE `company_id` = ?
                AND (`ticket_no` LIKE ? OR `subject` LIKE ?)
              ORDER BY `id` DESC
              LIMIT ?"
        );
        $st->execute(array($companyId, $like, $like, $n));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $tno       = $r['ticket_no'] ?: ('Ticket #' . $r['id']);
            $subj      = mb_substr((string) ($r['subject'] ?? ''), 0, 80, 'UTF-8');
            // 'service_mgmt' area has 'contracts' tab; tickets are in their own
            // area if enabled. Use epc_erp_tab_to_area() to resolve dynamically.
            $tabKey    = 'tickets';
            $areaKey2  = epc_erp_tab_to_area($tabKey);
            $results[] = array(
                'type'        => 'record',
                'record_type' => 'ticket',
                'label'       => $tno . ($subj !== '' ? ' — ' . $subj : ''),
                'sub'         => $subj,
                'tab'         => $tabKey,
                'area'        => $areaKey2,
                'icon'        => 'fa-life-ring',
                'url'         => epc_erp_tab_url(
                    '/cp/content/shop/finance/erp/erp_main_page.php',
                    $tabKey,
                    '',
                    '',
                    $areaKey2
                ),
            );
        }
    } catch (Throwable $e) {}

    // Employees/staff: epc_erp_staff_profiles links users to departments.
    // The table has no first/last name or employee_id column — only user_id,
    // department_code, display_name, job_title. The 'staff' tab in the 'people'
    // area renders a list but does not read a ?user_id= or ?id= query param to
    // open a single employee record, so we skip this record type rather than
    // produce a dead link (per task instructions).

    // ── 11. Cap and sort: modules first, then records ──────────────────────
    $modules = array();
    $records = array();
    foreach ($results as $item) {
        if ($item['type'] === 'module') {
            $modules[] = $item;
        } else {
            $records[] = $item;
        }
    }

    $combined = array_merge($modules, $records);
    return array_slice($combined, 0, $limit);
}
