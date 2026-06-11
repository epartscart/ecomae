<?php
/**
 * Public 3-day demo portal for ecomae.com.
 *
 * One shared demo credential, shown on the marketing page, that lets a prospect
 * explore the **storefront (frontend)** and the **ERP** — but NOT the CP
 * (tenant control panel). The session auto-expires after 3 days.
 *
 * Design:
 *   - Runs only against the dedicated demo tenant DB (seeded with the
 *     multi-industry demo data); never a live tenant, never epartscart.
 *   - Route scope gating: storefront + ERP namespaces are allowed; every other
 *     CP route is denied (the prospect is bounced back to the storefront/ERP).
 *   - Read-mostly sandbox: explorers can click around; the demo space is reset
 *     so the next visitor gets a clean slate.
 *
 * Pure functions (no DB) so the credential/expiry/gating logic is unit-tested.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!defined('EPC_DEMO_DURATION_DAYS')) {
    define('EPC_DEMO_DURATION_DAYS', 3);
}

if (!function_exists('epc_demo_credentials')) {
    /**
     * The shared, public demo login shown on the marketing page.
     * (Not a real tenant operator login — only unlocks the sandboxed demo.)
     *
     * @return array{email:string,password:string,note:string}
     */
    function epc_demo_credentials(): array
    {
        return array(
            'email' => 'demo@ecomae.com',
            'password' => 'demo1234',
            'note' => 'Shared 3-day demo. Storefront + ERP only. No CP. Data resets periodically.',
        );
    }
}

if (!function_exists('epc_demo_check_credentials')) {
    /** Constant-time-ish check of the supplied demo credential. */
    function epc_demo_check_credentials(string $email, string $password): bool
    {
        $c = epc_demo_credentials();
        return hash_equals($c['email'], strtolower(trim($email)))
            && hash_equals($c['password'], $password);
    }
}

if (!function_exists('epc_demo_issue_session')) {
    /**
     * Issue a demo session valid for EPC_DEMO_DURATION_DAYS.
     *
     * @return array{token:string,issued_at:int,expires_at:int,scope:array<int,string>}
     */
    function epc_demo_issue_session(?int $now = null): array
    {
        $now = $now ?? time();
        $expires = $now + (EPC_DEMO_DURATION_DAYS * 86400);
        return array(
            'token' => 'demo_' . bin2hex(random_bytes(16)),
            'issued_at' => $now,
            'expires_at' => $expires,
            'scope' => array('storefront', 'erp'),
        );
    }
}

if (!function_exists('epc_demo_session_valid')) {
    /** A session is valid while now < expires_at. */
    function epc_demo_session_valid(array $session, ?int $now = null): bool
    {
        $now = $now ?? time();
        return isset($session['expires_at']) && $now < (int) $session['expires_at'];
    }
}

if (!function_exists('epc_demo_seconds_remaining')) {
    function epc_demo_seconds_remaining(array $session, ?int $now = null): int
    {
        $now = $now ?? time();
        $left = (int) ($session['expires_at'] ?? 0) - $now;
        return $left > 0 ? $left : 0;
    }
}

if (!function_exists('epc_demo_days_remaining')) {
    /** Whole days left, rounded up (so day-1 of a 3-day trial reads "3 days left"). */
    function epc_demo_days_remaining(array $session, ?int $now = null): int
    {
        $secs = epc_demo_seconds_remaining($session, $now);
        return (int) ceil($secs / 86400);
    }
}

if (!function_exists('epc_demo_classify_route')) {
    /**
     * Classify a request path as 'storefront', 'erp' or 'cp'.
     * The ERP lives under the CP path (shop/finance/erp/*) but is explicitly
     * allowed in the demo; every other CP route is 'cp' (denied).
     */
    function epc_demo_classify_route(string $path): string
    {
        $p = strtolower(trim($path));
        $p = ltrim($p, '/');
        // strip a leading cp/ or backend-dir segment for matching
        $stripped = preg_replace('#^(cp|backend|admin)/#', '', $p);

        $erpPrefixes = array(
            'shop/finance/erp',     // ERP dashboard, guide, modules
            'shop/finance/erp/',
        );
        foreach ($erpPrefixes as $pre) {
            if ($stripped === rtrim($pre, '/') || strpos($stripped, rtrim($pre, '/') . '/') === 0) {
                return 'erp';
            }
        }

        // Anything addressed to the control panel that isn't the ERP is CP.
        if (preg_match('#^(cp|backend|admin)(/|$)#', $p)) {
            return 'cp';
        }

        return 'storefront';
    }
}

if (!function_exists('epc_demo_route_allowed')) {
    /** Demo allows storefront + ERP; denies all other CP routes. */
    function epc_demo_route_allowed(string $path): bool
    {
        return epc_demo_classify_route($path) !== 'cp';
    }
}

if (!function_exists('epc_demo_guard')) {
    /**
     * Gate a demo request. Returns the action the front controller should take.
     *
     * @return array{allow:bool,reason:string,redirect:string}
     */
    function epc_demo_guard(array $session, string $path, ?int $now = null): array
    {
        if (!epc_demo_session_valid($session, $now)) {
            return array('allow' => false, 'reason' => 'expired', 'redirect' => '/demo?expired=1');
        }
        if (!epc_demo_route_allowed($path)) {
            return array('allow' => false, 'reason' => 'cp_blocked', 'redirect' => '/shop/finance/erp/dashboard');
        }
        return array('allow' => true, 'reason' => 'ok', 'redirect' => '');
    }
}

if (!function_exists('epc_demo_launch_links')) {
    /**
     * Launch targets shown on the marketing demo block.
     *
     * @return array{storefront:string,erp:string,industries:array<int,array{code:string,name:string,storefront:string,erp:string}>}
     */
    function epc_demo_launch_links(string $base = ''): array
    {
        $base = rtrim($base, '/');
        $industries = array();
        if (function_exists('epc_demo_industries')) {
            foreach (epc_demo_industries() as $ind) {
                $industries[] = array(
                    'code' => $ind['code'],
                    'name' => $ind['name'],
                    'storefront' => $base . '/?demo=1&industry=' . rawurlencode($ind['code']),
                    'erp' => $base . '/shop/finance/erp/dashboard?demo=1&industry=' . rawurlencode($ind['code']),
                );
            }
        }
        return array(
            'storefront' => $base . '/?demo=1',
            'erp' => $base . '/shop/finance/erp/dashboard?demo=1',
            'industries' => $industries,
        );
    }
}

if (!function_exists('epc_demo_portal_html')) {
    /**
     * Marketing-page demo block: shows the shared credential + launch buttons +
     * an industry picker. Pure HTML/CSS (no JS dependency); theme-friendly.
     */
    function epc_demo_portal_html(string $base = ''): string
    {
        $c = epc_demo_credentials();
        $links = epc_demo_launch_links($base);
        $esc = static function ($s) {
            return htmlspecialchars((string) $s, ENT_QUOTES, 'UTF-8');
        };

        $chips = '';
        foreach ($links['industries'] as $ind) {
            $chips .= '<a class="epc-demo-chip" href="' . $esc($ind['erp']) . '">' . $esc($ind['name']) . '</a>';
        }

        $days = (int) EPC_DEMO_DURATION_DAYS;

        return '
<section class="epc-demo-portal" id="demo" dir="auto">
  <div class="epc-demo-card">
    <div class="epc-demo-badge">' . $days . '-DAY FREE DEMO &middot; NO SIGNUP</div>
    <h2>Try ecomae live</h2>
    <p class="epc-demo-sub">Explore the <strong>storefront</strong> and the full <strong>ERP</strong> with shared demo access. (Control panel is not part of the demo.)</p>

    <div class="epc-demo-cred">
      <div><span>Demo email</span><code>' . $esc($c['email']) . '</code></div>
      <div><span>Demo password</span><code>' . $esc($c['password']) . '</code></div>
    </div>

    <div class="epc-demo-actions">
      <a class="epc-demo-btn primary" href="' . $esc($links['erp']) . '">Open ERP demo</a>
      <a class="epc-demo-btn" href="' . $esc($links['storefront']) . '">View storefront</a>
    </div>

    <div class="epc-demo-industries">
      <span>Preview your industry:</span>
      ' . $chips . '
    </div>

    <p class="epc-demo-note">' . $esc($c['note']) . '</p>
  </div>
</section>
<style>
.epc-demo-portal{padding:48px 16px;display:flex;justify-content:center}
.epc-demo-card{max-width:760px;width:100%;background:linear-gradient(160deg,var(--epc-bg,#0b1220),var(--epc-bg-1,#111a2e));
  border:1px solid var(--epc-glow,rgba(0,229,176,.25));border-radius:20px;padding:32px;color:var(--epc-text,#e8eef7);
  box-shadow:0 20px 60px rgba(0,0,0,.45)}
.epc-demo-badge{display:inline-block;font-size:12px;letter-spacing:.12em;font-weight:700;
  color:#04140f;background:var(--epc-accent,#00e5b0);padding:6px 12px;border-radius:999px;margin-bottom:14px}
.epc-demo-card h2{margin:0 0 8px;font-size:30px}
.epc-demo-sub{margin:0 0 20px;color:#9fb2c9}
.epc-demo-cred{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:20px}
.epc-demo-cred>div{flex:1 1 220px;background:rgba(255,255,255,.04);border:1px solid rgba(255,255,255,.08);
  border-radius:12px;padding:12px 14px}
.epc-demo-cred span{display:block;font-size:11px;text-transform:uppercase;letter-spacing:.08em;color:#7e93ad;margin-bottom:6px}
.epc-demo-cred code{font-size:18px;color:var(--epc-accent,#00e5b0);font-weight:700}
.epc-demo-actions{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:22px}
.epc-demo-btn{padding:12px 22px;border-radius:12px;font-weight:600;text-decoration:none;
  border:1px solid rgba(255,255,255,.18);color:#e8eef7;transition:.2s}
.epc-demo-btn.primary{background:linear-gradient(90deg,var(--epc-accent,#00e5b0),var(--epc-accent-2,#00b894));color:#04140f;border:0}
.epc-demo-btn:hover{transform:translateY(-2px);box-shadow:0 10px 24px var(--epc-glow,rgba(0,229,176,.25))}
.epc-demo-industries{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin-bottom:16px}
.epc-demo-industries>span{color:#7e93ad;font-size:13px}
.epc-demo-chip{padding:6px 12px;border-radius:999px;background:var(--epc-glow,rgba(0,229,176,.08));
  border:1px solid var(--epc-glow,rgba(0,229,176,.25));color:var(--epc-text,#cfe);text-decoration:none;font-size:13px}
.epc-demo-chip:hover{filter:brightness(1.3)}
.epc-demo-note{margin:8px 0 0;color:#6f839b;font-size:12px}
</style>';
    }
}
