<?php
/**
 * BOS Module — Commerce Isolation Audit
 *
 * Provides a visual panel inside BOS for running and reviewing the
 * commerce data isolation audit. Shows results for all 5 checks:
 * 1. ERP DB isolation (dedicated DBs)
 * 2. Price ID ownership uniqueness
 * 3. Orphan price data
 * 4. Query scoping (static analysis)
 * 5. Registry credentials
 *
 * @since PR #35
 */
defined('_ASTEXE_') or define('_ASTEXE_', 1);
?>
<div id="epcIsolationAudit" style="padding:24px;">
    <div style="display:flex;align-items:center;gap:16px;margin-bottom:24px;">
        <div style="width:48px;height:48px;border-radius:12px;background:linear-gradient(135deg,#ef4444,#dc2626);display:flex;align-items:center;justify-content:center;">
            <i class="fa fa-shield" style="color:#fff;font-size:22px;"></i>
        </div>
        <div>
            <h3 style="margin:0;font-size:20px;color:#1e293b;">Commerce Isolation Audit</h3>
            <p style="margin:2px 0 0;font-size:13px;color:#64748b;">Verify tenant data isolation across the shared docpart database</p>
        </div>
        <button id="epcRunAuditBtn" onclick="epcRunIsolationAudit()" style="margin-left:auto;padding:10px 20px;background:#0ea5e9;color:#fff;border:none;border-radius:8px;cursor:pointer;font-size:14px;font-weight:600;display:flex;align-items:center;gap:8px;">
            <i class="fa fa-play"></i> Run Audit
        </button>
    </div>

    <div id="epcAuditLastRun" style="margin-bottom:24px;padding:16px;background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;">
        <div style="font-size:13px;color:#64748b;">Loading last audit result...</div>
    </div>

    <div id="epcAuditResults" style="display:none;">
        <!-- Populated by JS after audit run -->
    </div>

    <div style="margin-top:24px;">
        <h4 style="color:#1e293b;font-size:16px;margin-bottom:12px;"><i class="fa fa-exclamation-triangle" style="color:#f59e0b;margin-right:8px;"></i>Recent Violations</h4>
        <div id="epcViolationsList" style="background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;overflow:hidden;">
            <div style="padding:16px;color:#64748b;font-size:13px;">Loading...</div>
        </div>
    </div>

    <div style="margin-top:32px;padding:16px;background:#fffbeb;border-radius:10px;border:1px solid #fde68a;">
        <h4 style="color:#92400e;margin:0 0 8px;font-size:14px;"><i class="fa fa-info-circle" style="margin-right:6px;"></i>How This Works</h4>
        <ul style="margin:0;padding-left:20px;color:#92400e;font-size:13px;line-height:1.8;">
            <li><strong>ERP DB Isolation:</strong> Verifies shared ERP tenants use dedicated databases (not docpart/ecomae).</li>
            <li><strong>Price ID Ownership:</strong> Ensures no price_id is assigned to multiple tenant offices.</li>
            <li><strong>Orphan Data:</strong> Detects price rows with no parent price list (data leak risk).</li>
            <li><strong>Query Scoping:</strong> Static analysis of all PHP files querying shop_docpart_prices_data for price_id filter.</li>
            <li><strong>Registry Credentials:</strong> Connection test for each shared ERP tenant's DB credentials.</li>
        </ul>
    </div>
</div>

<script>
(function() {
    'use strict';

    // Load latest audit on page load
    loadLatestAudit();
    loadViolations();

    window.epcRunIsolationAudit = function() {
        var btn = document.getElementById('epcRunAuditBtn');
        var results = document.getElementById('epcAuditResults');
        btn.disabled = true;
        btn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Running...';
        results.style.display = 'none';

        var fd = new FormData();
        fd.append('bos_action', 'isolation_audit');
        fd.append('sub_action', 'run_audit');

        fetch('/bos/?action=ajax', { method: 'POST', body: fd, credentials: 'same-origin' })
            .then(function(r) { return r.json(); })
            .then(function(data) {
                btn.disabled = false;
                btn.innerHTML = '<i class="fa fa-play"></i> Run Audit';
                if (data.ok && data.audit) {
                    renderAuditResults(data.audit);
                } else {
                    results.style.display = 'block';
                    results.innerHTML = '<div style="padding:16px;color:#dc2626;">Error: ' + esc(data.error || 'Unknown error') + '</div>';
                }
            })
            .catch(function(e) {
                btn.disabled = false;
                btn.innerHTML = '<i class="fa fa-play"></i> Run Audit';
                results.style.display = 'block';
                results.innerHTML = '<div style="padding:16px;color:#dc2626;">Network error: ' + esc(e.message) + '</div>';
            });
    };

    function loadLatestAudit() {
        var fd = new FormData();
        fd.append('bos_action', 'isolation_audit');
        fd.append('sub_action', 'latest_run');

        fetch('/bos/?action=ajax', { method: 'POST', body: fd, credentials: 'same-origin' })
            .then(function(r) { return r.json(); })
            .then(function(data) {
                var el = document.getElementById('epcAuditLastRun');
                if (data.ok && data.latest_run) {
                    var run = data.latest_run;
                    var report = null;
                    try { report = JSON.parse(run.report_json); } catch(e) {}
                    var overall = report ? report.overall : 'N/A';
                    var color = overall === 'PASS' ? '#10b981' : (overall === 'WARN' ? '#f59e0b' : '#ef4444');
                    el.innerHTML =
                        '<div style="display:flex;align-items:center;gap:12px;">' +
                        '<div style="width:40px;height:40px;border-radius:50%;background:' + color + '20;display:flex;align-items:center;justify-content:center;">' +
                        '<i class="fa ' + (overall === 'PASS' ? 'fa-check' : (overall === 'WARN' ? 'fa-exclamation' : 'fa-times')) + '" style="color:' + color + ';font-size:18px;"></i></div>' +
                        '<div><div style="font-size:15px;font-weight:600;color:#1e293b;">Last Audit: <span style="color:' + color + ';">' + esc(overall) + '</span></div>' +
                        '<div style="font-size:12px;color:#94a3b8;">' + esc(run.run_at) + ' — ' + run.passed + ' passed, ' + run.failed + ' failed, ' + run.warnings + ' warnings</div></div></div>';
                } else {
                    el.innerHTML = '<div style="font-size:13px;color:#94a3b8;">No previous audit runs found. Click "Run Audit" to start.</div>';
                }
            })
            .catch(function() {
                document.getElementById('epcAuditLastRun').innerHTML = '<div style="font-size:13px;color:#94a3b8;">Unable to load last audit.</div>';
            });
    }

    function loadViolations() {
        var fd = new FormData();
        fd.append('bos_action', 'isolation_audit');
        fd.append('sub_action', 'recent_violations');

        fetch('/bos/?action=ajax', { method: 'POST', body: fd, credentials: 'same-origin' })
            .then(function(r) { return r.json(); })
            .then(function(data) {
                var el = document.getElementById('epcViolationsList');
                if (data.ok && data.violations && data.violations.length > 0) {
                    var html = '<table style="width:100%;border-collapse:collapse;font-size:13px;">';
                    html += '<tr style="background:#f1f5f9;"><th style="padding:8px 12px;text-align:left;color:#64748b;">Time</th><th style="padding:8px 12px;text-align:left;color:#64748b;">Tenant</th><th style="padding:8px 12px;text-align:left;color:#64748b;">Actor</th><th style="padding:8px 12px;text-align:left;color:#64748b;">Detail</th></tr>';
                    data.violations.forEach(function(v) {
                        html += '<tr style="border-top:1px solid #e2e8f0;">';
                        html += '<td style="padding:8px 12px;color:#64748b;white-space:nowrap;">' + esc(v.created_at) + '</td>';
                        html += '<td style="padding:8px 12px;font-weight:600;color:#dc2626;">' + esc(v.site_key) + '</td>';
                        html += '<td style="padding:8px 12px;color:#475569;">' + esc(v.actor) + '</td>';
                        html += '<td style="padding:8px 12px;color:#475569;max-width:400px;overflow:hidden;text-overflow:ellipsis;">' + esc(v.detail) + '</td>';
                        html += '</tr>';
                    });
                    html += '</table>';
                    el.innerHTML = html;
                } else {
                    el.innerHTML = '<div style="padding:16px;color:#10b981;font-size:13px;"><i class="fa fa-check-circle" style="margin-right:6px;"></i>No violations recorded.</div>';
                }
            })
            .catch(function() {
                document.getElementById('epcViolationsList').innerHTML = '<div style="padding:16px;color:#94a3b8;font-size:13px;">Unable to load violations.</div>';
            });
    }

    function renderAuditResults(audit) {
        var el = document.getElementById('epcAuditResults');
        el.style.display = 'block';

        var overallColor = audit.overall === 'PASS' ? '#10b981' : (audit.overall === 'WARN' ? '#f59e0b' : '#ef4444');
        var html = '<div style="padding:20px;background:' + overallColor + '10;border-radius:12px;border:2px solid ' + overallColor + '40;margin-bottom:20px;">';
        html += '<div style="display:flex;align-items:center;gap:12px;">';
        html += '<div style="width:48px;height:48px;border-radius:50%;background:' + overallColor + '20;display:flex;align-items:center;justify-content:center;">';
        html += '<i class="fa ' + (audit.overall === 'PASS' ? 'fa-check-circle' : (audit.overall === 'WARN' ? 'fa-exclamation-circle' : 'fa-times-circle')) + '" style="color:' + overallColor + ';font-size:24px;"></i></div>';
        html += '<div><div style="font-size:20px;font-weight:700;color:' + overallColor + ';">' + esc(audit.overall) + '</div>';
        html += '<div style="font-size:13px;color:#64748b;">' + esc(audit.timestamp) + ' — ' + audit.summary.passed + ' passed, ' + audit.summary.failed + ' failed, ' + audit.summary.warnings + ' warnings</div></div></div></div>';

        // Render each check
        var checks = audit.checks || {};
        Object.keys(checks).forEach(function(key) {
            var check = checks[key];
            var icon = check.status === 'PASS' ? 'fa-check-circle' : (check.status === 'WARN' ? 'fa-exclamation-triangle' : 'fa-times-circle');
            var color = check.status === 'PASS' ? '#10b981' : (check.status === 'WARN' ? '#f59e0b' : '#ef4444');

            html += '<div style="padding:16px;background:#fff;border-radius:10px;border:1px solid #e2e8f0;margin-bottom:12px;">';
            html += '<div style="display:flex;align-items:center;gap:10px;margin-bottom:8px;">';
            html += '<i class="fa ' + icon + '" style="color:' + color + ';font-size:16px;"></i>';
            html += '<span style="font-weight:600;color:#1e293b;">' + esc(check.name) + '</span>';
            html += '<span style="margin-left:auto;padding:2px 10px;background:' + color + '15;color:' + color + ';border-radius:6px;font-size:12px;font-weight:600;">' + esc(check.status) + '</span>';
            html += '</div>';
            if (check.message) {
                html += '<div style="font-size:13px;color:#64748b;margin-bottom:8px;">' + esc(check.message) + '</div>';
            }
            if (check.error) {
                html += '<div style="font-size:13px;color:#dc2626;">Error: ' + esc(check.error) + '</div>';
            }

            // Detailed results
            if (check.details && check.details.length > 0) {
                html += '<div style="margin-top:8px;padding:8px;background:#f8fafc;border-radius:6px;font-size:12px;font-family:monospace;max-height:200px;overflow-y:auto;">';
                check.details.forEach(function(d) {
                    var dColor = d.ok === true ? '#10b981' : (d.ok === false ? '#ef4444' : '#64748b');
                    html += '<div style="padding:2px 0;color:' + dColor + ';">' + (d.ok === true ? 'OK' : (d.ok === false ? 'FAIL' : '')) + ' ' + esc(JSON.stringify(d)) + '</div>';
                });
                html += '</div>';
            }

            // Unscoped files
            if (check.unscoped && check.unscoped.length > 0) {
                html += '<div style="margin-top:8px;padding:8px;background:#fef2f2;border-radius:6px;font-size:12px;">';
                html += '<div style="font-weight:600;color:#991b1b;margin-bottom:4px;">Unscoped files:</div>';
                check.unscoped.forEach(function(u) {
                    var riskColor = u.risk === 'HIGH' ? '#dc2626' : '#64748b';
                    html += '<div style="padding:2px 0;color:' + riskColor + ';">[' + esc(u.risk) + '] ' + esc(u.file) + (u.admin ? ' (admin)' : '') + '</div>';
                });
                html += '</div>';
            }
            html += '</div>';
        });

        el.innerHTML = html;

        // Refresh last run display
        loadLatestAudit();
    }

    function esc(s) {
        if (s === null || s === undefined) return '';
        var d = document.createElement('div');
        d.textContent = String(s);
        return d.innerHTML;
    }
})();
</script>
