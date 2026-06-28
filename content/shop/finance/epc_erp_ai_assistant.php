<?php
/**
 * Devin AI Tenant Assistant — embedded in ERP for tenant-level help.
 * Tenants can ask natural language questions about their own ERP data.
 *
 * Architecture: Pattern-matched queries against tenant's isolated DB.
 * No external API calls — all processing is local, ensuring tenant data
 * never leaves the platform.
 *
 * Example queries:
 *   "what is our gram inventory with necklaces and bangles?"
 *   "show me total gold stock weight"
 *   "how many open repairs?"
 *   "what are our top 5 customers by revenue?"
 *   "show purchase orders this month"
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

function epc_ai_assistant_query(PDO $db, string $question): array
{
    $q = strtolower(trim($question));
    if ($q === '') {
        return array('answer' => 'Please ask a question about your ERP data.', 'data' => null, 'type' => 'text');
    }

    // Detect jewellery tenant for weight-specific queries
    $jwFile = __DIR__ . '/epc_erp_jewellery_integration.php';
    $isJw = false;
    if (is_file($jwFile)) {
        require_once $jwFile;
        if (function_exists('epc_jw_is_jewellery_tenant')) {
            $isJw = epc_jw_is_jewellery_tenant($db);
        }
    }

    // --- Inventory queries ---
    if (preg_match('/\b(gram|weight|stock)\b.*\b(inventory|items?)\b/i', $q)
        || preg_match('/\b(inventory|items?)\b.*\b(gram|weight|stock)\b/i', $q)
        || preg_match('/\bwhat.*(inventory|stock|items?)\b/i', $q)) {

        $filters = array();
        if (preg_match('/necklace/i', $q)) $filters[] = "i.name LIKE '%necklace%'";
        if (preg_match('/bangle/i', $q)) $filters[] = "i.name LIKE '%bangle%'";
        if (preg_match('/ring/i', $q)) $filters[] = "i.name LIKE '%ring%'";
        if (preg_match('/earring/i', $q)) $filters[] = "i.name LIKE '%earring%'";
        if (preg_match('/pendant/i', $q)) $filters[] = "i.name LIKE '%pendant%'";
        if (preg_match('/diamond/i', $q)) $filters[] = "i.name LIKE '%diamond%' OR i.jw_metal_type = 'Diamond'";
        if (preg_match('/gold/i', $q)) $filters[] = "i.jw_metal_type = 'Gold'";
        if (preg_match('/silver/i', $q)) $filters[] = "i.jw_metal_type = 'Silver'";
        if (preg_match('/platinum/i', $q)) $filters[] = "i.jw_metal_type = 'Platinum'";
        if (preg_match('/pearl/i', $q)) $filters[] = "i.jw_metal_type = 'Pearl'";

        $where = '';
        if (!empty($filters)) {
            $where = ' AND (' . implode(' OR ', $filters) . ')';
        }

        try {
            $sql = "SELECT i.sku, i.name, i.jw_metal_type AS metal, i.jw_karat AS karat,
                           i.jw_gross_wt AS gross_wt, i.jw_net_wt AS net_wt,
                           COALESCE(s.jw_weight_on_hand, i.jw_gross_wt) AS stock_weight,
                           i.sales_price, i.standard_cost
                    FROM epc_erp_inv_items i
                    LEFT JOIN epc_erp_inv_stock s ON s.item_id = i.id
                    WHERE i.active = 1{$where}
                    ORDER BY i.jw_gross_wt DESC LIMIT 50";
            $rows = $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);

            $totalWt = 0;
            $totalVal = 0;
            $count = count($rows);
            foreach ($rows as $r) {
                $totalWt += (float)$r['stock_weight'];
                $totalVal += (float)$r['standard_cost'];
            }

            $filterDesc = empty($filters) ? 'all items' : trim(str_replace(
                array("i.name LIKE '%", "%'", "i.jw_metal_type = '", "'", " OR "),
                array('', '', '', '', ', '),
                implode(', ', $filters)
            ));

            $answer = "Found **{$count} items** matching \"{$filterDesc}\".\n\n"
                . "Total stock weight: **" . number_format($totalWt, 3) . " grams**\n"
                . "Total value: **AED " . number_format($totalVal, 2) . "**\n\n"
                . "| SKU | Item | Metal | Karat | Weight (g) | Value (AED) |\n"
                . "|-----|------|-------|-------|-----------|-------------|\n";
            foreach ($rows as $r) {
                $answer .= "| {$r['sku']} | {$r['name']} | {$r['metal']} | {$r['karat']} | "
                    . number_format((float)$r['stock_weight'], 3) . " | "
                    . number_format((float)$r['standard_cost'], 2) . " |\n";
            }
            return array('answer' => $answer, 'data' => $rows, 'type' => 'table');
        } catch (Throwable $e) {
            return array('answer' => 'Error querying inventory: ' . $e->getMessage(), 'data' => null, 'type' => 'error');
        }
    }

    // --- Total stock weight ---
    if (preg_match('/\b(total|sum)\b.*\b(gold|metal|weight|stock)\b/i', $q)) {
        try {
            $rows = $db->query(
                "SELECT i.jw_metal_type AS metal, i.jw_karat AS karat,
                        SUM(COALESCE(s.jw_weight_on_hand, i.jw_gross_wt)) AS total_wt,
                        SUM(i.standard_cost) AS total_val,
                        COUNT(*) AS cnt
                 FROM epc_erp_inv_items i
                 LEFT JOIN epc_erp_inv_stock s ON s.item_id = i.id
                 WHERE i.active = 1 AND i.jw_metal_type != ''
                 GROUP BY i.jw_metal_type, i.jw_karat
                 ORDER BY total_wt DESC"
            )->fetchAll(PDO::FETCH_ASSOC);

            $answer = "**Stock Summary by Metal & Karat:**\n\n"
                . "| Metal | Karat | Items | Total Weight (g) | Total Value (AED) |\n"
                . "|-------|-------|-------|-----------------|-------------------|\n";
            foreach ($rows as $r) {
                $answer .= "| {$r['metal']} | {$r['karat']} | {$r['cnt']} | "
                    . number_format((float)$r['total_wt'], 3) . " | "
                    . number_format((float)$r['total_val'], 2) . " |\n";
            }
            return array('answer' => $answer, 'data' => $rows, 'type' => 'table');
        } catch (Throwable $e) {
            return array('answer' => 'Error: ' . $e->getMessage(), 'data' => null, 'type' => 'error');
        }
    }

    // --- Repair queries ---
    if (preg_match('/\b(repair|service|fix|workshop)\b/i', $q)) {
        try {
            $rows = $db->query(
                "SELECT status, COUNT(*) AS cnt, SUM(estimated_cost) AS est_total
                 FROM epc_erp_jw_repairs GROUP BY status ORDER BY FIELD(status,'received','in_progress','ready','delivered','invoiced')"
            )->fetchAll(PDO::FETCH_ASSOC);

            $total = 0;
            $answer = "**Repair Jobs Summary:**\n\n"
                . "| Status | Count | Est. Cost (AED) |\n"
                . "|--------|-------|-----------------|\n";
            foreach ($rows as $r) {
                $total += (int)$r['cnt'];
                $answer .= "| " . ucfirst(str_replace('_', ' ', $r['status'])) . " | {$r['cnt']} | "
                    . number_format((float)$r['est_total'], 2) . " |\n";
            }
            $answer .= "\nTotal repairs: **{$total}**";
            return array('answer' => $answer, 'data' => $rows, 'type' => 'table');
        } catch (Throwable $e) {
            return array('answer' => 'Error: ' . $e->getMessage(), 'data' => null, 'type' => 'error');
        }
    }

    // --- Sales / revenue queries ---
    if (preg_match('/\b(sales?|revenue|sold|selling|invoice)\b/i', $q)) {
        try {
            $rows = $db->query(
                "SELECT so.so_no, so.title, so.total_amount, so.status,
                        COALESCE(c.name, c2.name, '') AS customer, so.time_created
                 FROM epc_erp_sales_orders so
                 LEFT JOIN epc_erp_contacts c ON c.id = so.contact_id
                 LEFT JOIN epc_erp_contacts c2 ON c2.linked_user_id = so.customer_user_id AND so.customer_user_id > 0
                 ORDER BY so.time_created DESC LIMIT 20"
            )->fetchAll(PDO::FETCH_ASSOC);

            $totalRev = 0;
            foreach ($rows as $r) $totalRev += (float)$r['total_amount'];

            $answer = "**Sales Orders** (latest " . count($rows) . "):\n"
                . "Total revenue: **AED " . number_format($totalRev, 2) . "**\n\n"
                . "| SO # | Customer | Title | Amount (AED) | Status |\n"
                . "|------|----------|-------|-------------|--------|\n";
            foreach ($rows as $r) {
                $answer .= "| {$r['so_no']} | " . ($r['customer'] ?: '—') . " | {$r['title']} | "
                    . number_format((float)$r['total_amount'], 2) . " | {$r['status']} |\n";
            }
            return array('answer' => $answer, 'data' => $rows, 'type' => 'table');
        } catch (Throwable $e) {
            return array('answer' => 'Error: ' . $e->getMessage(), 'data' => null, 'type' => 'error');
        }
    }

    // --- Purchase queries ---
    if (preg_match('/\b(purchase|po|procure|buy|bought|supplier)\b/i', $q)) {
        try {
            $rows = $db->query(
                "SELECT po_no, title, total_amount, jw_metal_weight_gm, jw_karat, status, time_created
                 FROM epc_erp_purchase_orders ORDER BY time_created DESC LIMIT 20"
            )->fetchAll(PDO::FETCH_ASSOC);

            $totalAmt = 0;
            $totalWt = 0;
            foreach ($rows as $r) {
                $totalAmt += (float)$r['total_amount'];
                $totalWt += (float)$r['jw_metal_weight_gm'];
            }

            $answer = "**Purchase Orders** (latest " . count($rows) . "):\n"
                . "Total amount: **AED " . number_format($totalAmt, 2) . "**";
            if ($totalWt > 0) {
                $answer .= " | Total metal weight: **" . number_format($totalWt, 3) . " g**";
            }
            $answer .= "\n\n| PO # | Title | Weight (g) | Karat | Amount (AED) | Status |\n"
                . "|------|-------|-----------|-------|-------------|--------|\n";
            foreach ($rows as $r) {
                $answer .= "| {$r['po_no']} | {$r['title']} | "
                    . number_format((float)$r['jw_metal_weight_gm'], 3) . " | {$r['jw_karat']} | "
                    . number_format((float)$r['total_amount'], 2) . " | {$r['status']} |\n";
            }
            return array('answer' => $answer, 'data' => $rows, 'type' => 'table');
        } catch (Throwable $e) {
            return array('answer' => 'Error: ' . $e->getMessage(), 'data' => null, 'type' => 'error');
        }
    }

    // --- Customer queries ---
    if (preg_match('/\b(customer|client|buyer)\b/i', $q)) {
        try {
            $rows = $db->query(
                "SELECT c.name, c.phone, c.email, c.country,
                        COUNT(so.id) AS order_count, SUM(so.total_amount) AS total_spent
                 FROM epc_erp_contacts c
                 LEFT JOIN epc_erp_sales_orders so ON so.customer_user_id = c.linked_user_id
                 WHERE c.contact_type = 'customer'
                 GROUP BY c.id
                 ORDER BY total_spent DESC LIMIT 20"
            )->fetchAll(PDO::FETCH_ASSOC);

            $answer = "**Customers** (" . count($rows) . " found):\n\n"
                . "| Name | Phone | Country | Orders | Total Spent (AED) |\n"
                . "|------|-------|---------|--------|-------------------|\n";
            foreach ($rows as $r) {
                $answer .= "| {$r['name']} | {$r['phone']} | {$r['country']} | {$r['order_count']} | "
                    . number_format((float)$r['total_spent'], 2) . " |\n";
            }
            return array('answer' => $answer, 'data' => $rows, 'type' => 'table');
        } catch (Throwable $e) {
            return array('answer' => 'Error: ' . $e->getMessage(), 'data' => null, 'type' => 'error');
        }
    }

    // --- Trial balance / GL queries ---
    if (preg_match('/\b(trial\s*balance|gl|general\s*ledger|journal|weight.*ledger)\b/i', $q)) {
        if ($isJw) {
            try {
                $rows = $db->query(
                    "SELECT account_code, account_name,
                            SUM(weight_in) AS wt_in, SUM(weight_out) AS wt_out,
                            SUM(value_debit) AS val_dr, SUM(value_credit) AS val_cr
                     FROM epc_erp_jw_weight_ledger
                     GROUP BY account_code, account_name
                     ORDER BY account_code"
                )->fetchAll(PDO::FETCH_ASSOC);

                $answer = "**Dual Trial Balance (Weight + Value):**\n\n"
                    . "| Account | Name | Wt In (g) | Wt Out (g) | Debit (AED) | Credit (AED) |\n"
                    . "|---------|------|----------|-----------|------------|-------------|\n";
                foreach ($rows as $r) {
                    $answer .= "| {$r['account_code']} | {$r['account_name']} | "
                        . number_format((float)$r['wt_in'], 3) . " | "
                        . number_format((float)$r['wt_out'], 3) . " | "
                        . number_format((float)$r['val_dr'], 2) . " | "
                        . number_format((float)$r['val_cr'], 2) . " |\n";
                }
                return array('answer' => $answer, 'data' => $rows, 'type' => 'table');
            } catch (Throwable $e) {
                return array('answer' => 'Error: ' . $e->getMessage(), 'data' => null, 'type' => 'error');
            }
        }
        return array('answer' => 'Trial balance data is available for jewellery industry tenants. Please ensure your industry profile is set to "jewellery".', 'data' => null, 'type' => 'text');
    }

    // --- Dashboard / overview ---
    if (preg_match('/\b(dashboard|overview|summary|kpi|status|how.*doing)\b/i', $q)) {
        try {
            $inv = $db->query("SELECT COUNT(*) AS cnt, SUM(standard_cost) AS val FROM epc_erp_inv_items WHERE active = 1")->fetch(PDO::FETCH_ASSOC);
            $so = $db->query("SELECT COUNT(*) AS cnt, SUM(total_amount) AS val FROM epc_erp_sales_orders")->fetch(PDO::FETCH_ASSOC);
            $po = $db->query("SELECT COUNT(*) AS cnt, SUM(total_amount) AS val FROM epc_erp_purchase_orders")->fetch(PDO::FETCH_ASSOC);
            $rep = array('cnt' => 0);
            try { $rep = $db->query("SELECT COUNT(*) AS cnt FROM epc_erp_jw_repairs WHERE status IN ('received','in_progress')")->fetch(PDO::FETCH_ASSOC); } catch (Throwable $e) {}

            $answer = "**ERP Dashboard Summary:**\n\n"
                . "- Inventory: **{$inv['cnt']}** items, value **AED " . number_format((float)$inv['val'], 2) . "**\n"
                . "- Sales Orders: **{$so['cnt']}**, total **AED " . number_format((float)$so['val'], 2) . "**\n"
                . "- Purchase Orders: **{$po['cnt']}**, total **AED " . number_format((float)$po['val'], 2) . "**\n"
                . "- Open Repairs: **{$rep['cnt']}**\n";

            if ($isJw) {
                try {
                    $wt = $db->query("SELECT SUM(COALESCE(s.jw_weight_on_hand, i.jw_gross_wt)) AS w FROM epc_erp_inv_items i LEFT JOIN epc_erp_inv_stock s ON s.item_id = i.id WHERE i.active = 1")->fetchColumn();
                    $answer .= "- Total Stock Weight: **" . number_format((float)$wt, 3) . " grams**\n";
                } catch (Throwable $e) {}
            }
            return array('answer' => $answer, 'data' => null, 'type' => 'text');
        } catch (Throwable $e) {
            return array('answer' => 'Error: ' . $e->getMessage(), 'data' => null, 'type' => 'error');
        }
    }

    // --- Help / capabilities ---
    if (preg_match('/\b(help|what can you|capabilities|commands?|how to)\b/i', $q)) {
        $answer = "**I can help you with these ERP queries:**\n\n"
            . "- **Inventory:** \"What is our gram inventory with necklaces and bangles?\"\n"
            . "- **Stock summary:** \"Show total gold stock weight\"\n"
            . "- **Sales:** \"Show me our sales orders\" or \"What's our revenue?\"\n"
            . "- **Purchases:** \"Show purchase orders\" or \"What did we buy?\"\n"
            . "- **Repairs:** \"How many open repairs?\" or \"Repair status\"\n"
            . "- **Customers:** \"Who are our top customers?\"\n"
            . "- **Trial balance:** \"Show trial balance\" or \"GL summary\"\n"
            . "- **Dashboard:** \"Give me an overview\" or \"How are we doing?\"\n\n"
            . "Just type your question naturally — I'll query your ERP data and respond.";
        return array('answer' => $answer, 'data' => null, 'type' => 'text');
    }

    // --- Fallback ---
    return array(
        'answer' => "I understand you're asking about: **\"{$question}\"**\n\n"
            . "I can help with inventory, sales, purchases, repairs, customers, and financial queries. "
            . "Try asking:\n"
            . "- \"What is our gram inventory with necklaces?\"\n"
            . "- \"Show me sales orders\"\n"
            . "- \"How many open repairs?\"\n"
            . "- \"Give me a dashboard overview\"",
        'data' => null,
        'type' => 'text',
    );
}
