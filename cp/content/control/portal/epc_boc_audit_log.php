<?php
/**
 * BOC — Audit log. Immutable record of privileged operator actions across the
 * platform (credential reveals, tenant toggles, governance changes, etc.).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_kernel.php';

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
    echo '<div class="alert alert-warning">Audit log is available on <strong>BOC</strong> only.</div>';
    return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
    global $DP_Config;
    echo '<div class="alert alert-warning">Please <a href="/' . epc_boc_h((string) $DP_Config->backend_dir) . '/">log in to BOC</a>.</div>';
    return;
}

global $db_link;
if (!isset($db_link) || !($db_link instanceof PDO)) {
    echo '<div class="alert alert-danger">Platform database unavailable.</div>';
    return;
}

$areaFilter = isset($_GET['area']) ? preg_replace('/[^a-z0-9_.]/', '', (string) $_GET['area']) : '';
$rows = epc_boc_audit_recent($db_link, 500, $areaFilter);
$brand = epc_boc_brand();
?>
<div class="col-lg-12 epc-erp-shell" id="epc-boc-audit-log">
    <div class="hpanel"><div class="panel-body">
        <div style="background:linear-gradient(135deg,#3f2d5c,#5b3f86);color:#fff;border-radius:12px;padding:18px;margin-bottom:16px">
            <h3 style="margin:0 0 6px;color:#fff"><i class="fa fa-history"></i> BOC audit log</h3>
            <p style="margin:0;opacity:.92">Immutable record of privileged operator actions. <?php echo $areaFilter !== '' ? 'Filtered to area <strong>' . epc_boc_h($areaFilter) . '</strong>.' : 'Showing the latest 500 entries.'; ?></p>
        </div>
        <?php if (empty($rows)): ?>
        <p class="text-muted">No audited actions recorded yet.</p>
        <?php else: ?>
        <table class="table table-condensed table-striped" style="font-size:12px">
            <thead><tr><th>When</th><th>Operator</th><th>Area</th><th>Action</th><th>Target</th><th>IP</th><th>Detail</th></tr></thead>
            <tbody>
            <?php foreach ($rows as $row):
                $meta = '';
                if (!empty($row['meta'])) {
                    $decoded = json_decode((string) $row['meta'], true);
                    $meta = is_array($decoded) ? implode(', ', array_map(static function ($k, $v) { return $k . '=' . (is_scalar($v) ? $v : json_encode($v)); }, array_keys($decoded), array_values($decoded))) : (string) $row['meta'];
                }
            ?>
                <tr>
                    <td class="text-muted" style="white-space:nowrap"><?php echo epc_boc_h(date('Y-m-d H:i:s', (int) ($row['ts'] ?? 0))); ?></td>
                    <td><?php echo epc_boc_h($row['actor'] !== '' ? $row['actor'] : ('#' . (int) ($row['user_id'] ?? 0))); ?></td>
                    <td><code style="font-size:11px"><?php echo epc_boc_h($row['area'] ?? ''); ?></code></td>
                    <td><?php echo epc_boc_h($row['action'] ?? ''); ?></td>
                    <td class="text-muted"><?php echo epc_boc_h($row['target'] ?? ''); ?></td>
                    <td class="text-muted"><?php echo epc_boc_h($row['ip'] ?? ''); ?></td>
                    <td class="text-muted" style="font-size:11px"><?php echo epc_boc_h($meta); ?></td>
                </tr>
            <?php endforeach; ?>
            </tbody>
        </table>
        <?php endif; ?>
    </div></div>
</div>
