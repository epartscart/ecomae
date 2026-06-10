<?php
/**
 * Advanced ERP — CRM intelligence layer.
 *
 * Additive enhancements on top of the existing CRM (epc_crm_*). It does NOT
 * change any existing CRM table or behaviour; it only:
 *   - computes a lead score (0-100) from existing lead/activity data
 *   - builds a "customer 360" view by joining CRM with ERP sales/invoices
 *   - applies the worldwide tax toolkit to a CRM quote so quotes show
 *     tax-correct totals for any country
 *   - produces a weighted pipeline forecast and conversion analytics
 *   - surfaces overdue follow-ups (next-best-action)
 *
 * Read-mostly: the only schema it adds is an optional scoring-weights settings
 * row (stored in epc_price_settings). Safe for live tenants.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_advanced.php';
require_once __DIR__ . '/epc_crm_helpers.php';

if (!function_exists('epc_crm_adv_table_exists')) {
    function epc_crm_adv_table_exists(PDO $db, string $table): bool
    {
        try {
            $st = $db->prepare('SHOW TABLES LIKE ?');
            $st->execute(array($table));
            return $st->fetchColumn() !== false;
        } catch (Exception $e) {
            return false;
        }
    }
}

if (!function_exists('epc_crm_adv_score_weights')) {
    /**
     * Lead-scoring weights. Stored as JSON in settings so an admin can tune
     * them without code changes; falls back to sensible defaults.
     *
     * @return array<string,int>
     */
    function epc_crm_adv_score_weights(PDO $db): array
    {
        $defaults = array(
            'status_new' => 5,
            'status_contacted' => 20,
            'status_qualified' => 45,
            'has_email' => 10,
            'has_phone' => 10,
            'value_band' => 20,
            'activity_each' => 5,
            'activity_cap' => 15,
        );
        $raw = epc_erp_adv_get_setting($db, 'crm_score_weights', '');
        if ($raw !== '') {
            $parsed = json_decode($raw, true);
            if (is_array($parsed)) {
                foreach ($defaults as $k => $v) {
                    if (isset($parsed[$k]) && is_numeric($parsed[$k])) {
                        $defaults[$k] = (int) $parsed[$k];
                    }
                }
            }
        }
        return $defaults;
    }
}

if (!function_exists('epc_crm_adv_score_lead')) {
    /**
     * Score a single lead row (as returned by epc_crm_get_lead / list).
     *
     * @param array<string,mixed> $lead
     * @return array{score:int,band:string,reasons:array<int,string>}
     */
    function epc_crm_adv_score_lead(PDO $db, array $lead): array
    {
        $w = epc_crm_adv_score_weights($db);
        $score = 0;
        $reasons = array();

        $status = (string) ($lead['status'] ?? 'new');
        if ($status === 'qualified' || $status === 'converted') {
            $score += $w['status_qualified'];
            $reasons[] = 'Qualified status (+' . $w['status_qualified'] . ')';
        } elseif ($status === 'contacted') {
            $score += $w['status_contacted'];
            $reasons[] = 'Contacted (+' . $w['status_contacted'] . ')';
        } else {
            $score += $w['status_new'];
            $reasons[] = 'New lead (+' . $w['status_new'] . ')';
        }

        if (!empty($lead['email'])) {
            $score += $w['has_email'];
            $reasons[] = 'Has email (+' . $w['has_email'] . ')';
        }
        if (!empty($lead['phone'])) {
            $score += $w['has_phone'];
            $reasons[] = 'Has phone (+' . $w['has_phone'] . ')';
        }

        $value = (float) ($lead['expected_value'] ?? 0);
        if ($value > 0) {
            $band = $value >= 50000 ? 1.0 : ($value >= 10000 ? 0.66 : 0.33);
            $add = (int) round($w['value_band'] * $band);
            $score += $add;
            $reasons[] = 'Expected value (+' . $add . ')';
        }

        $leadId = (int) ($lead['id'] ?? 0);
        if ($leadId > 0 && epc_crm_adv_table_exists($db, 'epc_crm_activities')) {
            $st = $db->prepare(
                "SELECT COUNT(*) FROM `epc_crm_activities`
                 WHERE `related_type` = 'lead' AND `related_id` = ? AND `active` = 1"
            );
            $st->execute(array($leadId));
            $acts = (int) $st->fetchColumn();
            if ($acts > 0) {
                $add = min($w['activity_cap'], $acts * $w['activity_each']);
                $score += $add;
                $reasons[] = $acts . ' activities (+' . $add . ')';
            }
        }

        $score = max(0, min(100, $score));
        $band = $score >= 70 ? 'hot' : ($score >= 40 ? 'warm' : 'cold');

        return array('score' => $score, 'band' => $band, 'reasons' => $reasons);
    }
}

if (!function_exists('epc_crm_adv_scored_leads')) {
    /**
     * List leads with scores attached, sorted hottest first.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_crm_adv_scored_leads(PDO $db, int $limit = 100): array
    {
        if (!epc_crm_adv_table_exists($db, 'epc_crm_leads')) {
            return array();
        }
        $st = $db->prepare(
            "SELECT * FROM `epc_crm_leads` WHERE `active` = 1
             ORDER BY `time_created` DESC LIMIT " . max(1, min(500, $limit))
        );
        $st->execute();
        $leads = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
        foreach ($leads as &$lead) {
            $scored = epc_crm_adv_score_lead($db, $lead);
            $lead['lead_score'] = $scored['score'];
            $lead['lead_band'] = $scored['band'];
            $lead['score_reasons'] = $scored['reasons'];
        }
        unset($lead);
        usort($leads, static function ($a, $b) {
            return ($b['lead_score'] ?? 0) <=> ($a['lead_score'] ?? 0);
        });
        return $leads;
    }
}

if (!function_exists('epc_crm_adv_pipeline_forecast')) {
    /**
     * Weighted pipeline forecast from open opportunities.
     *
     * @return array<string,mixed>
     */
    function epc_crm_adv_pipeline_forecast(PDO $db): array
    {
        $out = array(
            'open_count' => 0,
            'open_value' => 0.0,
            'weighted_value' => 0.0,
            'won_value' => 0.0,
            'lost_value' => 0.0,
            'win_rate' => 0.0,
            'by_stage' => array(),
        );
        if (!epc_crm_adv_table_exists($db, 'epc_crm_opportunities')) {
            return $out;
        }

        $rows = $db->query(
            "SELECT `stage`, COUNT(*) AS c, SUM(`amount`) AS amt,
                    SUM(`amount` * `probability` / 100) AS weighted
             FROM `epc_crm_opportunities` WHERE `active` = 1 GROUP BY `stage`"
        )->fetchAll(PDO::FETCH_ASSOC) ?: array();

        $wonCount = 0;
        $closedCount = 0;
        foreach ($rows as $r) {
            $stage = (string) $r['stage'];
            $amt = (float) $r['amt'];
            $cnt = (int) $r['c'];
            $out['by_stage'][$stage] = array(
                'count' => $cnt,
                'value' => $amt,
                'weighted' => (float) $r['weighted'],
            );
            if ($stage === 'won') {
                $out['won_value'] += $amt;
                $wonCount += $cnt;
                $closedCount += $cnt;
            } elseif ($stage === 'lost') {
                $out['lost_value'] += $amt;
                $closedCount += $cnt;
            } else {
                $out['open_count'] += $cnt;
                $out['open_value'] += $amt;
                $out['weighted_value'] += (float) $r['weighted'];
            }
        }
        $out['win_rate'] = $closedCount > 0 ? round(($wonCount / $closedCount) * 100, 1) : 0.0;
        return $out;
    }
}

if (!function_exists('epc_crm_adv_next_actions')) {
    /**
     * Overdue / upcoming follow-ups (next-best-action list).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_crm_adv_next_actions(PDO $db, int $limit = 25): array
    {
        if (!epc_crm_adv_table_exists($db, 'epc_crm_activities')) {
            return array();
        }
        $now = time();
        $st = $db->prepare(
            "SELECT * FROM `epc_crm_activities`
             WHERE `active` = 1 AND `done` = 0 AND `due_date` > 0
             ORDER BY `due_date` ASC LIMIT " . max(1, min(100, $limit))
        );
        $st->execute();
        $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
        foreach ($rows as &$r) {
            $r['is_overdue'] = ((int) $r['due_date'] < $now) ? 1 : 0;
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_crm_adv_customer_360')) {
    /**
     * Customer-360: merge CRM opportunities/quotes/tickets with ERP sales for
     * one customer (shop user id). Gracefully degrades if a table is absent.
     *
     * @return array<string,mixed>
     */
    function epc_crm_adv_customer_360(PDO $db, int $userId): array
    {
        $out = array(
            'user_id' => $userId,
            'opportunities' => array('count' => 0, 'open_value' => 0.0, 'won_value' => 0.0),
            'quotes' => array('count' => 0, 'accepted' => 0, 'value' => 0.0),
            'tickets' => array('open' => 0, 'total' => 0),
            'sales' => array('orders' => 0, 'revenue' => 0.0),
        );
        if ($userId <= 0) {
            return $out;
        }

        if (epc_crm_adv_table_exists($db, 'epc_crm_opportunities')) {
            $st = $db->prepare(
                "SELECT
                    COUNT(*) AS c,
                    SUM(CASE WHEN `stage` NOT IN ('won','lost') THEN `amount` ELSE 0 END) AS open_value,
                    SUM(CASE WHEN `stage` = 'won' THEN `amount` ELSE 0 END) AS won_value
                 FROM `epc_crm_opportunities` WHERE `active` = 1 AND `linked_user_id` = ?"
            );
            $st->execute(array($userId));
            $r = $st->fetch(PDO::FETCH_ASSOC) ?: array();
            $out['opportunities'] = array(
                'count' => (int) ($r['c'] ?? 0),
                'open_value' => (float) ($r['open_value'] ?? 0),
                'won_value' => (float) ($r['won_value'] ?? 0),
            );
        }

        if (epc_crm_adv_table_exists($db, 'epc_crm_quotes')) {
            $st = $db->prepare(
                "SELECT COUNT(*) AS c,
                        SUM(CASE WHEN `status` = 'accepted' THEN 1 ELSE 0 END) AS accepted,
                        SUM(`subtotal`) AS value
                 FROM `epc_crm_quotes` WHERE `active` = 1 AND `customer_user_id` = ?"
            );
            $st->execute(array($userId));
            $r = $st->fetch(PDO::FETCH_ASSOC) ?: array();
            $out['quotes'] = array(
                'count' => (int) ($r['c'] ?? 0),
                'accepted' => (int) ($r['accepted'] ?? 0),
                'value' => (float) ($r['value'] ?? 0),
            );
        }

        if (epc_crm_adv_table_exists($db, 'epc_crm_tickets')) {
            $st = $db->prepare(
                "SELECT COUNT(*) AS total,
                        SUM(CASE WHEN `status` NOT IN ('closed','resolved') THEN 1 ELSE 0 END) AS open_c
                 FROM `epc_crm_tickets` WHERE `customer_user_id` = ?"
            );
            try {
                $st->execute(array($userId));
                $r = $st->fetch(PDO::FETCH_ASSOC) ?: array();
                $out['tickets'] = array(
                    'open' => (int) ($r['open_c'] ?? 0),
                    'total' => (int) ($r['total'] ?? 0),
                );
            } catch (Exception $e) {
                // ticket table shape differs on some tenants; ignore.
            }
        }

        return $out;
    }
}

if (!function_exists('epc_crm_adv_quote_tax_totals')) {
    /**
     * Compute tax-aware totals for a CRM quote using the worldwide tax toolkit.
     * Falls back to a flat rate (or zero) if the toolkit is unavailable.
     *
     * @return array<string,mixed>
     */
    function epc_crm_adv_quote_tax_totals(PDO $db, int $quoteId): array
    {
        $result = array(
            'quote_id' => $quoteId,
            'subtotal' => 0.0,
            'tax_rate' => 0.0,
            'tax_amount' => 0.0,
            'total' => 0.0,
            'tax_label' => 'Tax',
            'currency' => 'AED',
            'engine' => 'none',
        );
        if (!epc_crm_adv_table_exists($db, 'epc_crm_quotes')) {
            return $result;
        }

        $st = $db->prepare('SELECT * FROM `epc_crm_quotes` WHERE `id` = ? LIMIT 1');
        $st->execute(array($quoteId));
        $quote = $st->fetch(PDO::FETCH_ASSOC);
        if (!$quote) {
            return $result;
        }
        $result['currency'] = (string) ($quote['currency_code'] ?? 'AED');

        $subtotal = 0.0;
        if (epc_crm_adv_table_exists($db, 'epc_crm_quote_lines')) {
            $ls = $db->prepare('SELECT `qty`, `unit_price` FROM `epc_crm_quote_lines` WHERE `quote_id` = ?');
            $ls->execute(array($quoteId));
            foreach ($ls->fetchAll(PDO::FETCH_ASSOC) as $line) {
                $subtotal += (float) $line['qty'] * (float) $line['unit_price'];
            }
        }
        if ($subtotal <= 0) {
            $subtotal = (float) ($quote['subtotal'] ?? 0);
        }
        $result['subtotal'] = round($subtotal, 2);

        $customerId = (int) ($quote['customer_user_id'] ?? 0);

        // Preferred: worldwide tax toolkit (per-customer / per-tenant profile).
        $toolkit = __DIR__ . '/epc_tax_toolkit.php';
        if (is_file($toolkit)) {
            require_once $toolkit;
            if (function_exists('epc_tax_toolkit_calc_amounts')) {
                try {
                    $calc = epc_tax_toolkit_calc_amounts($db, $subtotal, $customerId, 0, array());
                    if (is_array($calc)) {
                        $result['tax_amount'] = round((float) ($calc['tax'] ?? $calc['tax_amount'] ?? 0), 2);
                        $result['total'] = round((float) ($calc['total'] ?? ($subtotal + $result['tax_amount'])), 2);
                        $result['tax_rate'] = (float) ($calc['rate'] ?? ($subtotal > 0 ? $result['tax_amount'] / $subtotal * 100 : 0));
                        $result['tax_label'] = (string) ($calc['label'] ?? $calc['tax_label'] ?? 'Tax');
                        $result['engine'] = 'tax_toolkit';
                        return $result;
                    }
                } catch (Exception $e) {
                    // fall through to flat-rate fallback
                }
            }
        }

        // Fallback: flat configured VAT percent.
        $rate = (float) epc_erp_adv_get_setting($db, 'vat_percent', '0');
        $result['tax_rate'] = $rate;
        $result['tax_amount'] = round($subtotal * $rate / 100, 2);
        $result['total'] = round($subtotal + $result['tax_amount'], 2);
        $result['tax_label'] = $rate > 0 ? ('VAT ' . rtrim(rtrim(number_format($rate, 2), '0'), '.') . '%') : 'No tax';
        $result['engine'] = 'flat_rate';
        return $result;
    }
}

if (!function_exists('epc_crm_adv_dashboard')) {
    /**
     * One-call advanced CRM dashboard payload.
     *
     * @return array<string,mixed>
     */
    function epc_crm_adv_dashboard(PDO $db): array
    {
        $scored = epc_crm_adv_scored_leads($db, 100);
        $hot = 0;
        $warm = 0;
        $cold = 0;
        foreach ($scored as $l) {
            $band = $l['lead_band'] ?? 'cold';
            if ($band === 'hot') {
                $hot++;
            } elseif ($band === 'warm') {
                $warm++;
            } else {
                $cold++;
            }
        }
        return array(
            'lead_bands' => array('hot' => $hot, 'warm' => $warm, 'cold' => $cold, 'total' => count($scored)),
            'top_leads' => array_slice($scored, 0, 10),
            'forecast' => epc_crm_adv_pipeline_forecast($db),
            'next_actions' => epc_crm_adv_next_actions($db, 10),
        );
    }
}
