<?php
/**
 * P1 #13 — Role-based CP Home (dashboards per role)
 *
 * Detects user role and renders a tailored dashboard with relevant
 * KPI tiles, quick actions, and module shortcuts.
 * Roles: admin, finance, warehouse, sales, support, viewer
 */

if (!defined('EPC_CP_ROLE_HOME_VERSION')) {
    define('EPC_CP_ROLE_HOME_VERSION', '1.0.0');
}

/* ─── role definitions ─── */

function epc_cp_roles(): array
{
    return array(
        'admin' => array(
            'label'       => 'Administrator',
            'icon'        => 'fa-user-shield',
            'color'       => '#ef4444',
            'description' => 'Full platform access with system configuration',
        ),
        'finance' => array(
            'label'       => 'Finance Manager',
            'icon'        => 'fa-calculator',
            'color'       => '#10b981',
            'description' => 'Invoicing, payments, GL, and financial reporting',
        ),
        'warehouse' => array(
            'label'       => 'Warehouse Manager',
            'icon'        => 'fa-warehouse',
            'color'       => '#f59e0b',
            'description' => 'Inventory, stock levels, receiving, and shipping',
        ),
        'sales' => array(
            'label'       => 'Sales Representative',
            'icon'        => 'fa-handshake',
            'color'       => '#3b82f6',
            'description' => 'Orders, quotes, customers, and sales pipeline',
        ),
        'support' => array(
            'label'       => 'Customer Support',
            'icon'        => 'fa-headset',
            'color'       => '#8b5cf6',
            'description' => 'Tickets, returns, customer inquiries',
        ),
        'viewer' => array(
            'label'       => 'Read-Only Viewer',
            'icon'        => 'fa-eye',
            'color'       => '#6b7280',
            'description' => 'View-only access to reports and dashboards',
        ),
    );
}

/* ─── role-specific dashboard tiles ─── */

function epc_cp_role_dashboard_tiles(string $role): array
{
    $tiles = array(
        'admin' => array(
            array('id' => 'total_orders',     'label' => 'Total Orders',        'icon' => 'fa-shopping-cart',  'color' => '#3b82f6', 'query' => 'orders_count'),
            array('id' => 'total_revenue',    'label' => 'Revenue (MTD)',        'icon' => 'fa-money',         'color' => '#10b981', 'query' => 'revenue_mtd'),
            array('id' => 'active_users',     'label' => 'Active Users',        'icon' => 'fa-users',         'color' => '#8b5cf6', 'query' => 'active_users'),
            array('id' => 'system_health',    'label' => 'System Health',       'icon' => 'fa-heartbeat',     'color' => '#ef4444', 'query' => 'system_health'),
            array('id' => 'pending_orders',   'label' => 'Pending Orders',      'icon' => 'fa-clock-o',       'color' => '#f59e0b', 'query' => 'pending_orders'),
            array('id' => 'low_stock',        'label' => 'Low Stock Items',     'icon' => 'fa-exclamation-triangle', 'color' => '#ef4444', 'query' => 'low_stock'),
        ),
        'finance' => array(
            array('id' => 'revenue_mtd',      'label' => 'Revenue (MTD)',        'icon' => 'fa-money',         'color' => '#10b981', 'query' => 'revenue_mtd'),
            array('id' => 'outstanding_ar',   'label' => 'Outstanding AR',      'icon' => 'fa-file-text-o',   'color' => '#ef4444', 'query' => 'outstanding_ar'),
            array('id' => 'outstanding_ap',   'label' => 'Outstanding AP',      'icon' => 'fa-credit-card',   'color' => '#f59e0b', 'query' => 'outstanding_ap'),
            array('id' => 'invoices_due',     'label' => 'Invoices Due Today',  'icon' => 'fa-calendar',      'color' => '#3b82f6', 'query' => 'invoices_due'),
            array('id' => 'bank_balance',     'label' => 'Cash Position',       'icon' => 'fa-university',    'color' => '#8b5cf6', 'query' => 'bank_balance'),
            array('id' => 'vat_liability',    'label' => 'VAT Liability',       'icon' => 'fa-balance-scale', 'color' => '#06b6d4', 'query' => 'vat_liability'),
        ),
        'warehouse' => array(
            array('id' => 'total_sku',        'label' => 'Total SKUs',          'icon' => 'fa-barcode',       'color' => '#3b82f6', 'query' => 'total_sku'),
            array('id' => 'low_stock',        'label' => 'Low Stock Alerts',    'icon' => 'fa-exclamation-triangle', 'color' => '#ef4444', 'query' => 'low_stock'),
            array('id' => 'pending_receive',  'label' => 'Pending Receipts',    'icon' => 'fa-truck',         'color' => '#f59e0b', 'query' => 'pending_receive'),
            array('id' => 'pending_ship',     'label' => 'Ready to Ship',       'icon' => 'fa-paper-plane',   'color' => '#10b981', 'query' => 'pending_ship'),
            array('id' => 'stock_value',      'label' => 'Stock Value',         'icon' => 'fa-cubes',         'color' => '#8b5cf6', 'query' => 'stock_value'),
            array('id' => 'returns_pending',  'label' => 'Returns Pending',     'icon' => 'fa-undo',          'color' => '#ec4899', 'query' => 'returns_pending'),
        ),
        'sales' => array(
            array('id' => 'my_orders',        'label' => 'My Orders (MTD)',     'icon' => 'fa-shopping-cart',  'color' => '#3b82f6', 'query' => 'my_orders'),
            array('id' => 'my_quotes',        'label' => 'Open Quotes',        'icon' => 'fa-file-o',        'color' => '#f59e0b', 'query' => 'my_quotes'),
            array('id' => 'my_revenue',       'label' => 'My Revenue (MTD)',    'icon' => 'fa-money',         'color' => '#10b981', 'query' => 'my_revenue'),
            array('id' => 'new_customers',    'label' => 'New Customers',       'icon' => 'fa-user-plus',     'color' => '#8b5cf6', 'query' => 'new_customers'),
            array('id' => 'conversion_rate',  'label' => 'Conversion Rate',    'icon' => 'fa-percent',       'color' => '#06b6d4', 'query' => 'conversion_rate'),
            array('id' => 'top_products',     'label' => 'Top Products',        'icon' => 'fa-star',          'color' => '#ec4899', 'query' => 'top_products'),
        ),
        'support' => array(
            array('id' => 'open_tickets',     'label' => 'Open Tickets',        'icon' => 'fa-ticket',        'color' => '#ef4444', 'query' => 'open_tickets'),
            array('id' => 'pending_returns',  'label' => 'Pending Returns',     'icon' => 'fa-undo',          'color' => '#f59e0b', 'query' => 'pending_returns'),
            array('id' => 'avg_response',     'label' => 'Avg Response Time',   'icon' => 'fa-clock-o',       'color' => '#3b82f6', 'query' => 'avg_response'),
            array('id' => 'resolved_today',   'label' => 'Resolved Today',      'icon' => 'fa-check-circle',  'color' => '#10b981', 'query' => 'resolved_today'),
            array('id' => 'escalated',        'label' => 'Escalated',           'icon' => 'fa-arrow-up',      'color' => '#ec4899', 'query' => 'escalated'),
            array('id' => 'satisfaction',     'label' => 'Satisfaction Score',   'icon' => 'fa-smile-o',       'color' => '#8b5cf6', 'query' => 'satisfaction'),
        ),
        'viewer' => array(
            array('id' => 'total_orders',     'label' => 'Total Orders',        'icon' => 'fa-shopping-cart',  'color' => '#3b82f6', 'query' => 'orders_count'),
            array('id' => 'total_revenue',    'label' => 'Revenue (MTD)',        'icon' => 'fa-money',         'color' => '#10b981', 'query' => 'revenue_mtd'),
            array('id' => 'total_sku',        'label' => 'Total SKUs',          'icon' => 'fa-barcode',       'color' => '#f59e0b', 'query' => 'total_sku'),
            array('id' => 'active_users',     'label' => 'Active Users',        'icon' => 'fa-users',         'color' => '#8b5cf6', 'query' => 'active_users'),
        ),
    );

    return $tiles[$role] ?? $tiles['viewer'];
}

/* ─── role-specific quick actions ─── */

function epc_cp_role_quick_actions(string $role): array
{
    $actions = array(
        'admin' => array(
            array('label' => 'New Order',          'icon' => 'fa-plus',          'url' => '/cp/content/shop/orders/new_order.php'),
            array('label' => 'User Management',    'icon' => 'fa-users',         'url' => '/cp/content/shop/users/user_list.php'),
            array('label' => 'System Settings',    'icon' => 'fa-cog',           'url' => '/cp/content/shop/settings/settings.php'),
            array('label' => 'Import Data',         'icon' => 'fa-upload',        'url' => '/cp/content/shop/import/import.php'),
            array('label' => 'View Reports',        'icon' => 'fa-bar-chart',     'url' => '/cp/content/shop/reports/reports.php'),
            array('label' => 'ERP Suite',           'icon' => 'fa-building',      'url' => '/cp/content/shop/finance/epc_erp_home.php'),
        ),
        'finance' => array(
            array('label' => 'New Invoice',         'icon' => 'fa-plus',          'url' => '/cp/content/shop/finance/invoices/new.php'),
            array('label' => 'Record Payment',      'icon' => 'fa-money',         'url' => '/cp/content/shop/finance/payments/record.php'),
            array('label' => 'GL Journal',          'icon' => 'fa-book',          'url' => '/cp/content/shop/finance/gl/journal.php'),
            array('label' => 'Bank Reconciliation', 'icon' => 'fa-university',    'url' => '/cp/content/shop/finance/bank_recon.php'),
            array('label' => 'E-Invoice Submit',    'icon' => 'fa-paper-plane',   'url' => '/cp/content/shop/finance/epc_einvoice.php'),
            array('label' => 'Financial Reports',   'icon' => 'fa-line-chart',    'url' => '/cp/content/shop/finance/reports.php'),
        ),
        'warehouse' => array(
            array('label' => 'Receive Stock',       'icon' => 'fa-truck',         'url' => '/cp/content/shop/inventory/receive.php'),
            array('label' => 'Ship Order',          'icon' => 'fa-paper-plane',   'url' => '/cp/content/shop/inventory/ship.php'),
            array('label' => 'Stock Count',         'icon' => 'fa-clipboard',     'url' => '/cp/content/shop/inventory/count.php'),
            array('label' => 'Stock Transfer',      'icon' => 'fa-exchange',      'url' => '/cp/content/shop/inventory/transfer.php'),
            array('label' => 'Low Stock Report',    'icon' => 'fa-exclamation-triangle', 'url' => '/cp/content/shop/inventory/low_stock.php'),
            array('label' => 'Returns Processing',  'icon' => 'fa-undo',          'url' => '/cp/content/shop/inventory/returns.php'),
        ),
        'sales' => array(
            array('label' => 'New Quote',           'icon' => 'fa-file-o',        'url' => '/cp/content/shop/quotes/new_quote.php'),
            array('label' => 'New Order',           'icon' => 'fa-plus',          'url' => '/cp/content/shop/orders/new_order.php'),
            array('label' => 'Customer Lookup',     'icon' => 'fa-search',        'url' => '/cp/content/shop/customers/search.php'),
            array('label' => 'My Pipeline',         'icon' => 'fa-filter',        'url' => '/cp/content/shop/sales/pipeline.php'),
            array('label' => 'Price Lists',         'icon' => 'fa-tags',          'url' => '/cp/content/shop/pricing/lists.php'),
            array('label' => 'Sales Report',        'icon' => 'fa-bar-chart',     'url' => '/cp/content/shop/reports/sales.php'),
        ),
        'support' => array(
            array('label' => 'New Ticket',          'icon' => 'fa-plus',          'url' => '/cp/content/shop/support/new_ticket.php'),
            array('label' => 'My Queue',            'icon' => 'fa-inbox',         'url' => '/cp/content/shop/support/queue.php'),
            array('label' => 'Process Return',      'icon' => 'fa-undo',          'url' => '/cp/content/shop/support/returns.php'),
            array('label' => 'Customer Lookup',     'icon' => 'fa-search',        'url' => '/cp/content/shop/customers/search.php'),
            array('label' => 'Knowledge Base',      'icon' => 'fa-book',          'url' => '/cp/content/shop/support/kb.php'),
        ),
        'viewer' => array(
            array('label' => 'View Reports',        'icon' => 'fa-bar-chart',     'url' => '/cp/content/shop/reports/reports.php'),
            array('label' => 'View Orders',         'icon' => 'fa-shopping-cart',  'url' => '/cp/content/shop/orders/order_list.php'),
        ),
    );

    return $actions[$role] ?? $actions['viewer'];
}

/* ─── role-specific sidebar modules ─── */

function epc_cp_role_modules(string $role): array
{
    $modules = array(
        'admin'     => array('orders', 'customers', 'inventory', 'finance', 'erp', 'users', 'settings', 'reports', 'import'),
        'finance'   => array('finance', 'erp', 'orders', 'reports'),
        'warehouse' => array('inventory', 'orders', 'reports'),
        'sales'     => array('orders', 'customers', 'quotes', 'reports', 'pricing'),
        'support'   => array('support', 'orders', 'customers', 'returns'),
        'viewer'    => array('reports', 'orders'),
    );

    return $modules[$role] ?? $modules['viewer'];
}

/* ─── detect user role from session/DB ─── */

function epc_cp_detect_role($dbLink = null, int $userId = 0): string
{
    if (isset($_SESSION['epc_user_role']) && $_SESSION['epc_user_role'] !== '') {
        return (string) $_SESSION['epc_user_role'];
    }

    if ($dbLink && $userId > 0) {
        if ($dbLink instanceof PDO) {
            $st = $dbLink->prepare("SELECT `role` FROM `users` WHERE `id` = ? LIMIT 1");
            $st->execute(array($userId));
            $role = $st->fetchColumn();
        } elseif (function_exists('mysqli_prepare')) {
            $st = mysqli_prepare($dbLink, "SELECT `role` FROM `users` WHERE `id` = ? LIMIT 1");
            if ($st) {
                mysqli_stmt_bind_param($st, 'i', $userId);
                mysqli_stmt_execute($st);
                $result = mysqli_stmt_get_result($st);
                $row = mysqli_fetch_assoc($result);
                $role = $row['role'] ?? '';
                mysqli_stmt_close($st);
            }
        }

        if (!empty($role)) {
            $validRoles = array_keys(epc_cp_roles());
            if (in_array($role, $validRoles, true)) {
                $_SESSION['epc_user_role'] = $role;
                return $role;
            }
        }
    }

    return 'admin';
}

/* ─── tile data resolver ─── */

function epc_cp_tile_resolve($dbLink, string $query, string $siteKey = ''): array
{
    $value = 0;
    $trend = '';
    $format = 'number';

    switch ($query) {
        case 'orders_count':
            $value = epc_cp_tile_query_scalar($dbLink, "SELECT COUNT(*) FROM `orders` WHERE MONTH(`created_at`) = MONTH(CURDATE()) AND YEAR(`created_at`) = YEAR(CURDATE())");
            break;
        case 'revenue_mtd':
            $value = epc_cp_tile_query_scalar($dbLink, "SELECT COALESCE(SUM(`total`), 0) FROM `orders` WHERE `status` != 'cancelled' AND MONTH(`created_at`) = MONTH(CURDATE()) AND YEAR(`created_at`) = YEAR(CURDATE())");
            $format = 'currency';
            break;
        case 'active_users':
            $value = epc_cp_tile_query_scalar($dbLink, "SELECT COUNT(*) FROM `users` WHERE `active` = 1");
            break;
        case 'pending_orders':
            $value = epc_cp_tile_query_scalar($dbLink, "SELECT COUNT(*) FROM `orders` WHERE `status` IN ('pending', 'processing')");
            break;
        case 'low_stock':
            $value = epc_cp_tile_query_scalar($dbLink, "SELECT COUNT(*) FROM `products` WHERE `stock_qty` <= `reorder_level` AND `stock_qty` >= 0");
            break;
        case 'outstanding_ar':
            $value = epc_cp_tile_query_scalar($dbLink, "SELECT COALESCE(SUM(`balance_due`), 0) FROM `invoices` WHERE `status` = 'outstanding'");
            $format = 'currency';
            break;
        case 'outstanding_ap':
            $value = epc_cp_tile_query_scalar($dbLink, "SELECT COALESCE(SUM(`amount_due`), 0) FROM `bills` WHERE `status` = 'unpaid'");
            $format = 'currency';
            break;
        case 'invoices_due':
            $value = epc_cp_tile_query_scalar($dbLink, "SELECT COUNT(*) FROM `invoices` WHERE `due_date` <= CURDATE() AND `status` = 'outstanding'");
            break;
        case 'total_sku':
            $value = epc_cp_tile_query_scalar($dbLink, "SELECT COUNT(*) FROM `products` WHERE `active` = 1");
            break;
        case 'pending_ship':
            $value = epc_cp_tile_query_scalar($dbLink, "SELECT COUNT(*) FROM `orders` WHERE `status` = 'ready_to_ship'");
            break;
        default:
            $value = 0;
            break;
    }

    return array('value' => $value, 'format' => $format, 'trend' => $trend);
}

function epc_cp_tile_query_scalar($dbLink, string $sql)
{
    try {
        if ($dbLink instanceof PDO) {
            $st = $dbLink->query($sql);
            return $st ? $st->fetchColumn() : 0;
        } elseif (function_exists('mysqli_query')) {
            $result = @mysqli_query($dbLink, $sql);
            if ($result) {
                $row = mysqli_fetch_row($result);
                return $row ? $row[0] : 0;
            }
        }
    } catch (\Exception $e) {
        return 0;
    }
    return 0;
}

/* ─── render role home HTML ─── */

function epc_cp_role_home_render(string $role, $dbLink = null): string
{
    $roleInfo = epc_cp_roles()[$role] ?? epc_cp_roles()['viewer'];
    $tiles = epc_cp_role_dashboard_tiles($role);
    $actions = epc_cp_role_quick_actions($role);

    $html = '<div class="epc-role-home" style="padding:20px;">';
    $html .= '<div style="margin-bottom:24px;">';
    $html .= '<h2 style="margin:0 0 4px 0;color:#1e293b;"><i class="fa ' . htmlspecialchars($roleInfo['icon']) . '" style="color:' . htmlspecialchars($roleInfo['color']) . ';margin-right:8px;"></i>' . htmlspecialchars($roleInfo['label']) . ' Dashboard</h2>';
    $html .= '<p style="margin:0;color:#64748b;font-size:14px;">' . htmlspecialchars($roleInfo['description']) . '</p>';
    $html .= '</div>';

    // KPI tiles
    $html .= '<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:16px;margin-bottom:24px;">';
    foreach ($tiles as $tile) {
        $data = $dbLink ? epc_cp_tile_resolve($dbLink, $tile['query']) : array('value' => '-', 'format' => 'number');
        $displayValue = $data['format'] === 'currency' ? number_format((float)$data['value'], 2) : number_format((float)$data['value']);
        $html .= '<div style="background:#fff;border-radius:12px;padding:20px;box-shadow:0 1px 3px rgba(0,0,0,0.1);border-left:4px solid ' . htmlspecialchars($tile['color']) . ';">';
        $html .= '<div style="font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:8px;">' . htmlspecialchars($tile['label']) . '</div>';
        $html .= '<div style="font-size:28px;font-weight:700;color:#1e293b;">' . ($data['format'] === 'currency' ? '<span style="font-size:16px;">$</span>' : '') . htmlspecialchars($displayValue) . '</div>';
        $html .= '<div style="margin-top:4px;"><i class="fa ' . htmlspecialchars($tile['icon']) . '" style="color:' . htmlspecialchars($tile['color']) . ';"></i></div>';
        $html .= '</div>';
    }
    $html .= '</div>';

    // Quick actions
    $html .= '<h3 style="color:#1e293b;margin:0 0 12px 0;"><i class="fa fa-bolt" style="color:#f59e0b;margin-right:6px;"></i>Quick Actions</h3>';
    $html .= '<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:12px;margin-bottom:24px;">';
    foreach ($actions as $action) {
        $html .= '<a href="' . htmlspecialchars($action['url']) . '" style="display:flex;align-items:center;gap:10px;padding:12px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;text-decoration:none;color:#334155;font-size:14px;transition:all 0.2s;" onmouseover="this.style.background=\'#e0f2fe\';this.style.borderColor=\'#93c5fd\'" onmouseout="this.style.background=\'#f8fafc\';this.style.borderColor=\'#e2e8f0\'">';
        $html .= '<i class="fa ' . htmlspecialchars($action['icon']) . '" style="color:#3b82f6;"></i>';
        $html .= htmlspecialchars($action['label']);
        $html .= '</a>';
    }
    $html .= '</div>';

    $html .= '</div>';
    return $html;
}

/* ─── role assignment ─── */

function epc_cp_role_assign($dbLink, int $userId, string $role): bool
{
    $validRoles = array_keys(epc_cp_roles());
    if (!in_array($role, $validRoles, true)) {
        return false;
    }

    if ($dbLink instanceof PDO) {
        $st = $dbLink->prepare("UPDATE `users` SET `role` = ? WHERE `id` = ?");
        return $st->execute(array($role, $userId));
    } elseif (function_exists('mysqli_prepare')) {
        $st = mysqli_prepare($dbLink, "UPDATE `users` SET `role` = ? WHERE `id` = ?");
        if ($st) {
            mysqli_stmt_bind_param($st, 'si', $role, $userId);
            $ok = mysqli_stmt_execute($st);
            mysqli_stmt_close($st);
            return $ok;
        }
    }
    return false;
}

/* ─── role permissions check ─── */

function epc_cp_role_can(string $role, string $permission): bool
{
    $permissions = array(
        'admin'     => array('orders.*', 'customers.*', 'inventory.*', 'finance.*', 'erp.*', 'users.*', 'settings.*', 'reports.*', 'import.*'),
        'finance'   => array('orders.view', 'finance.*', 'erp.*', 'reports.finance'),
        'warehouse' => array('orders.view', 'orders.fulfill', 'inventory.*', 'reports.inventory'),
        'sales'     => array('orders.*', 'customers.*', 'quotes.*', 'reports.sales', 'pricing.view'),
        'support'   => array('orders.view', 'customers.view', 'support.*', 'returns.*'),
        'viewer'    => array('orders.view', 'reports.view'),
    );

    $rolePerms = $permissions[$role] ?? $permissions['viewer'];

    foreach ($rolePerms as $perm) {
        if ($perm === $permission) {
            return true;
        }
        if (strpos($perm, '.*') !== false) {
            $prefix = str_replace('.*', '.', $perm);
            if (strpos($permission, $prefix) === 0 || $permission === str_replace('.*', '', $perm)) {
                return true;
            }
        }
    }
    return false;
}
