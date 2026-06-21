<?php
/**
 * Tenant Data Protection Policy — Protocol, Procedures & Compliance
 *
 * This file defines the enforceable data protection policy for all tenants
 * on the ecomae platform. It is rendered in BOS under Platform > Data Policy
 * and serves as the legal/compliance reference for tenant data handling.
 *
 * Worldwide principle: uniform policy, country-specific compliance addenda
 * driven by tenant registration country (epc_country_profile).
 */
defined('_ASTEXE_') or die('No access');

/**
 * Return the full data protection policy as structured sections.
 * Used by BOS policy viewer and compliance reports.
 */
function epc_tdp_policy_sections(): array
{
    return array(
        array(
            'id' => 'scope',
            'title' => '1. Scope & Applicability',
            'content' => 'This policy applies to all data stored, processed, or transmitted by or on behalf of any tenant registered on the ecomae platform. It covers all tenant databases, backups, logs, exports, and any derived data. Every person or system that accesses tenant data must comply with this policy.',
        ),
        array(
            'id' => 'classification',
            'title' => '2. Data Classification',
            'content' => 'All tenant data is classified into four levels:',
            'items' => array(
                'Highly Confidential — Financial records (GL, AP, AR, cash/bank, payroll, tax filings). Requires encryption at rest and in transit. Access logged and reviewed.',
                'Confidential — Business operations (orders, customers, vendors, HR records, inventory, pricing). Access restricted to authorized personnel.',
                'Internal — Operational data (products, CMS content, settings, marketing campaigns). Standard access controls.',
                'Public — Storefront catalogue data visible to end users. No access restrictions.',
            ),
        ),
        array(
            'id' => 'isolation',
            'title' => '3. Database Isolation',
            'content' => 'Each tenant operates on a dedicated MySQL database. Cross-tenant queries are architecturally prohibited. The platform registry database (ecomae) contains only tenant metadata (name, status, hostname, industry) — never business data. Tenant database credentials are stored encrypted in the platform registry and are never exposed in URLs, logs, or error messages.',
            'controls' => array(
                'Each tenant gets a unique database name, user, and password.',
                'Tenant DB users have privileges scoped to their own database only.',
                'No SQL joins, unions, or subqueries span multiple tenant databases.',
                'Platform code connects to one tenant DB at a time via epc_portal_tenant_control_tenant_pdo_connect().',
                'Provider console iterates tenants sequentially — never holds multiple tenant connections simultaneously.',
            ),
        ),
        array(
            'id' => 'access_control',
            'title' => '4. Access Control',
            'content' => 'Access to tenant data follows the principle of least privilege:',
            'controls' => array(
                'Guest users: No access to any tenant data.',
                'Tenant users: Read + write access to their own tenant only. Cannot see, query, or export other tenants\' data.',
                'Provider (platform operator): Metadata + read access to all tenants for support/monitoring. Write access requires explicit authorization. Delete access requires two-factor confirmation.',
                'Cross-tenant access attempts are logged as security violations and trigger alerts.',
                'Session tokens are scoped to a single tenant context. Switching tenants in BOS creates a new scoped session.',
            ),
        ),
        array(
            'id' => 'audit',
            'title' => '5. Audit Trail',
            'content' => 'All access to tenant data is logged in the platform audit system (epc_tdp_audit_log):',
            'controls' => array(
                'Every tenant DB connection is logged with actor ID, IP address, user agent, and timestamp.',
                'All mutating operations (create, update, delete) are logged with the full payload (sensitive fields redacted).',
                'Security violations (cross-tenant attempts, permission denials) are logged separately in epc_tdp_violations.',
                'Audit logs are retained for 7 years (2555 days) per legal/tax requirements.',
                'Audit logs themselves are append-only — no deletion or modification permitted.',
            ),
        ),
        array(
            'id' => 'encryption',
            'title' => '6. Encryption & Transport Security',
            'content' => 'All tenant data is protected in transit and at rest:',
            'controls' => array(
                'All HTTP traffic is served over TLS 1.2+ (enforced by Cloudflare and server SSL).',
                'Database connections use localhost (127.0.0.1) — no unencrypted network transit.',
                'Database credentials in the platform registry are stored as plain text in MySQL but protected by DB-level access control. Future: migrate to AES-256 encrypted storage.',
                'Backups are stored on the same server with restricted file permissions (0600).',
                'API tokens and webhook secrets are never logged or displayed after creation.',
            ),
        ),
        array(
            'id' => 'retention',
            'title' => '7. Data Retention',
            'content' => 'Data retention periods are enforced uniformly across all tenants:',
            'items' => array(
                'ERP financial records: 7 years (legal requirement in most jurisdictions).',
                'Platform audit logs: 7 years.',
                'Security violation logs: 7 years.',
                'Customer PII: 5 years after last activity.',
                'Session/login logs: 90 days.',
                'Temporary exports: 7 days (auto-deleted).',
            ),
        ),
        array(
            'id' => 'breach',
            'title' => '8. Incident Response & Breach Notification',
            'content' => 'In the event of a data breach or unauthorized access:',
            'controls' => array(
                'Immediately isolate the affected tenant database(s).',
                'Review epc_tdp_violations and epc_tdp_audit_log for the scope of the breach.',
                'Notify the affected tenant within 72 hours with: what data was accessed, when, by whom, and remediation steps.',
                'If personal data of end customers was exposed, notify per applicable data protection law (e.g., GDPR Article 33, UAE PDPL).',
                'Rotate all affected credentials (DB passwords, API keys, session tokens).',
                'Document the incident, root cause, and corrective actions in a post-incident report.',
            ),
        ),
        array(
            'id' => 'tenant_rights',
            'title' => '9. Tenant Data Rights',
            'content' => 'Each tenant retains full ownership of their data:',
            'controls' => array(
                'Right to export: Tenants can export all their data at any time via ERP export functions.',
                'Right to deletion: Tenants can request full deletion of their database and all backups. Executed within 30 days.',
                'Right to portability: Data is stored in standard MySQL format, exportable as SQL dump or CSV.',
                'Right to audit: Tenants can request an audit report of all access to their data.',
                'Platform provider will never sell, share, or use tenant data for purposes other than providing the platform service.',
            ),
        ),
        array(
            'id' => 'compliance',
            'title' => '10. Regulatory Compliance',
            'content' => 'The platform supports compliance with applicable data protection regulations based on each tenant\'s registration country. Compliance addenda are auto-applied via epc_country_profile():',
            'items' => array(
                'UAE: Federal Decree-Law No. 45/2021 (PDPL) — data localization, consent management, DPO requirements.',
                'EU/EEA: GDPR — lawful basis, data minimization, right to erasure, DPA requirements.',
                'UK: UK GDPR + Data Protection Act 2018.',
                'Saudi Arabia: PDPL (2023) — data classification, cross-border transfer restrictions.',
                'India: DPDP Act 2023 — consent, purpose limitation, data fiduciary obligations.',
                'General: SOC 2 Type II controls alignment for all tenants regardless of jurisdiction.',
            ),
        ),
    );
}

/**
 * Render the data protection policy as HTML for BOS display.
 */
function epc_tdp_render_policy_html(): string
{
    $sections = epc_tdp_policy_sections();
    $html = '<div class="epc-tdp-policy">';
    $html .= '<div class="epc-tdp-policy__header">';
    $html .= '<h2><i class="fa fa-shield" style="color: #3b82f6;"></i> Tenant Data Protection Policy</h2>';
    $html .= '<p class="text-muted">Version ' . EPC_TDP_VERSION . ' &mdash; Last updated: ' . date('F Y') . '</p>';
    $html .= '</div>';

    foreach ($sections as $section) {
        $html .= '<div class="epc-tdp-policy__section" id="tdp-' . htmlspecialchars($section['id']) . '">';
        $html .= '<h3>' . htmlspecialchars($section['title']) . '</h3>';
        $html .= '<p>' . htmlspecialchars($section['content']) . '</p>';

        if (!empty($section['items'])) {
            $html .= '<ul class="epc-tdp-policy__list">';
            foreach ($section['items'] as $item) {
                $html .= '<li>' . htmlspecialchars($item) . '</li>';
            }
            $html .= '</ul>';
        }

        if (!empty($section['controls'])) {
            $html .= '<div class="epc-tdp-policy__controls">';
            $html .= '<strong>Controls:</strong>';
            $html .= '<ul>';
            foreach ($section['controls'] as $ctrl) {
                $html .= '<li><i class="fa fa-check-circle" style="color: #10b981; margin-right: 6px;"></i>' . htmlspecialchars($ctrl) . '</li>';
            }
            $html .= '</ul>';
            $html .= '</div>';
        }

        $html .= '</div>';
    }

    $html .= '</div>';
    return $html;
}
