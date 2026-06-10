<?php
/**
 * Super CP — provider / operator console (CP body).
 *
 * Operator-side fleet view across all tenants, using the tested three-tier
 * control layer (epc_ctl_*). Isolation is preserved: the console connects to
 * each tenant's OWN database one at a time through a connection provider — there
 * is NO shared cross-tenant table and no cross-tenant SQL.
 *
 * Connection provider: the platform supplies the per-tenant PDO map by defining
 * epc_ctl_operator_connections() (returns tenantKey => PDO). When it is not
 * available this page falls back to the current connection so it still renders
 * safely on any environment. Additive; no existing table is modified.
 */
defined('_ASTEXE_') or die('No access');

$doc = $_SERVER['DOCUMENT_ROOT'];
require_once $doc . '/content/shop/finance/epc_erp_control.php';
@require_once $doc . '/content/shop/finance/epc_erp_modules.php';

$backend = '/' . htmlspecialchars((string) $GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');
$themeBase = $backend . '/content/shop/finance/erp/theme';

// Resolve the per-tenant connection map (operator hook, else current DB).
$connections = array();
if (function_exists('epc_ctl_operator_connections')) {
    try {
        $connections = epc_ctl_operator_connections();
    } catch (Exception $e) {
        $connections = array();
    }
}
if (empty($connections)) {
    if (isset($db_link) && $db_link instanceof PDO) {
        $connections = array('current' => $db_link);
    } else {
        try {
            $current = new PDO(
                'mysql:host=' . $GLOBALS['DP_Config']->host . ';dbname=' . $GLOBALS['DP_Config']->db,
                $GLOBALS['DP_Config']->user,
                $GLOBALS['DP_Config']->password,
                array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
            );
            $connections = array((string) $GLOBALS['DP_Config']->db => $current);
        } catch (Exception $e) {
            $connections = array();
        }
    }
}

$fleet = array('tenants' => array(), 'count' => 0, 'active' => 0, 'suspended' => 0, 'expired' => 0);
if (!empty($connections)) {
    try {
        $fleet = epc_ctl_fleet_overview($connections);
    } catch (Exception $e) {
    }
}

$esc = function ($v) {
    return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
};
$fmtDate = function ($ts) {
    $ts = (int) $ts;
    return $ts > 0 ? date('Y-m-d', $ts) : '&mdash;';
};
?>
<link rel="stylesheet" href="<?php echo $themeBase; ?>/erp_theme.css" />
<div class="erp-theme" style="background:transparent;min-height:auto">
<div class="erp-dash" style="padding-top:6px">
    <div class="erp-topbar">
        <div class="erp-brand">
            <div class="mark">S</div>
            <div class="name">Provider Console<small>Operator fleet control &middot; all tenants</small></div>
        </div>
        <div>
            <span class="erp-chip">Tenants: <b><?php echo (int) $fleet['count']; ?></b></span>
            <span class="erp-chip">Active: <b><?php echo (int) $fleet['active']; ?></b></span>
            <span class="erp-chip">Suspended: <b><?php echo (int) $fleet['suspended']; ?></b></span>
            <span class="erp-chip">Expired: <b><?php echo (int) $fleet['expired']; ?></b></span>
        </div>
    </div>

    <div class="erp-grid">
        <div class="erp-kpi" style="animation-delay:.05s"><div class="lbl">Total Tenants</div><div class="val" data-count="<?php echo (int) $fleet['count']; ?>">0</div><div class="delta up">fleet</div></div>
        <div class="erp-kpi" style="animation-delay:.12s"><div class="lbl">Active</div><div class="val" data-count="<?php echo (int) $fleet['active']; ?>">0</div><div class="delta up">live</div></div>
        <div class="erp-kpi" style="animation-delay:.19s"><div class="lbl">Suspended</div><div class="val" data-count="<?php echo (int) $fleet['suspended']; ?>">0</div><div class="delta down">paused</div></div>
        <div class="erp-kpi" style="animation-delay:.26s"><div class="lbl">Expired</div><div class="val" data-count="<?php echo (int) $fleet['expired']; ?>">0</div><div class="delta down">renew</div></div>
    </div>

    <div class="erp-panel" style="animation-delay:.3s">
        <h3>Fleet Overview <span>&mdash; each tenant on its own database (isolated)</span></h3>
        <div style="overflow-x:auto">
        <table style="width:100%;border-collapse:collapse;font-size:13px">
            <thead>
                <tr style="text-align:left;color:var(--erp-muted)">
                    <th style="padding:10px 8px;border-bottom:1px solid var(--erp-card-brd)">Tenant</th>
                    <th style="padding:10px 8px;border-bottom:1px solid var(--erp-card-brd)">Code</th>
                    <th style="padding:10px 8px;border-bottom:1px solid var(--erp-card-brd)">Plan</th>
                    <th style="padding:10px 8px;border-bottom:1px solid var(--erp-card-brd)">Status</th>
                    <th style="padding:10px 8px;border-bottom:1px solid var(--erp-card-brd)">Modules</th>
                    <th style="padding:10px 8px;border-bottom:1px solid var(--erp-card-brd)">Expires</th>
                </tr>
            </thead>
            <tbody>
            <?php if (empty($fleet['tenants'])): ?>
                <tr><td colspan="6" style="padding:16px 8px;color:var(--erp-muted)">No tenant connections available. Define <code>epc_ctl_operator_connections()</code> to return a map of tenantKey =&gt; PDO (one per tenant DB) to populate the fleet.</td></tr>
            <?php else: foreach ($fleet['tenants'] as $t): ?>
                <?php
                if (isset($t['error'])) {
                    echo '<tr><td colspan="6" style="padding:10px 8px;color:var(--erp-down)">' . $esc($t['key']) . ': ' . $esc($t['error']) . '</td></tr>';
                    continue;
                }
                $statusColor = $t['status'] === 'active' && empty($t['expired']) ? 'var(--erp-up)' : 'var(--erp-down)';
                $statusText = !empty($t['expired']) ? 'expired' : $t['status'];
                ?>
                <tr>
                    <td style="padding:10px 8px;border-bottom:1px solid rgba(120,160,220,0.06)"><?php echo $esc($t['display_name'] !== '' ? $t['display_name'] : $t['key']); ?></td>
                    <td style="padding:10px 8px;border-bottom:1px solid rgba(120,160,220,0.06)"><?php echo $esc($t['tenant_code']); ?></td>
                    <td style="padding:10px 8px;border-bottom:1px solid rgba(120,160,220,0.06)"><?php echo $esc($t['plan'] !== '' ? $t['plan'] : 'full'); ?></td>
                    <td style="padding:10px 8px;border-bottom:1px solid rgba(120,160,220,0.06)"><span style="color:<?php echo $statusColor; ?>;font-weight:600">&#9679; <?php echo $esc($statusText); ?></span></td>
                    <td style="padding:10px 8px;border-bottom:1px solid rgba(120,160,220,0.06)"><?php echo (int) $t['modules']; ?></td>
                    <td style="padding:10px 8px;border-bottom:1px solid rgba(120,160,220,0.06)"><?php echo $fmtDate($t['expires_at']); ?></td>
                </tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
        </div>
        <div style="margin-top:14px;color:var(--erp-muted);font-size:12px;line-height:1.7">
            Operator actions (provision / re-plan, suspend / reactivate, push compliance updates) run per tenant via <code>epc_ctl_provision_tenant()</code>, <code>epc_ctl_set_tenant_status()</code> on that tenant's own database. No cross-tenant data is ever co-mingled.
        </div>
    </div>
</div>
</div>

<script>
(function () {
    function countUp(el) {
        var target = parseFloat(el.getAttribute('data-count')) || 0;
        var start = null;
        function step(ts) {
            if (!start) start = ts;
            var p = Math.min((ts - start) / 1100, 1);
            el.textContent = Math.floor(target * (1 - Math.pow(1 - p, 3))).toLocaleString();
            if (p < 1) requestAnimationFrame(step);
        }
        requestAnimationFrame(step);
    }
    document.querySelectorAll('.erp-kpi .val').forEach(countUp);
})();
</script>
<?php
// End provider console.
