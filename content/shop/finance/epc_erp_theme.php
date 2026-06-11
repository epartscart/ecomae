<?php
/**
 * Central theme-token layer for ALL surfaces.
 *
 * One switch re-skins everything — marketing site, Super CP, tenant CP, ERP
 * login/dashboard, storefront — because every surface reads the same CSS
 * variables. Pick a preset and `epc_theme_style_tag()` emits a `:root` block
 * that overrides both the generic `--epc-*` tokens and the existing ERP
 * `--erp-*` variables.
 *
 * Presets:
 *   - 'blue'  : Blue & Black   (royal/electric blue accents on near-black)
 *   - 'red'   : Red & Black    (crimson/ember accents on near-black)
 *   - 'teal'  : Teal (original default)
 *
 * Pure (no DB) so it is unit-testable; a tenant/operator setting selects which
 * preset is active.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_theme_presets')) {
    /**
     * @return array<string,array<string,string>>
     */
    function epc_theme_presets(): array
    {
        return array(
            'blue' => array(
                'name' => 'Blue & Black',
                'bg0' => '#05070e',
                'bg1' => '#0a0f1c',
                'bg2' => '#0f1830',
                'accent' => '#2f6dff',
                'accent2' => '#1ea8ff',
                'accent3' => '#5b8cff',
                'glow' => 'rgba(47,109,255,0.28)',
                'card' => 'rgba(18,28,52,0.55)',
                'card_brd' => 'rgba(90,140,255,0.18)',
                'text' => '#e8f0ff',
                'muted' => '#8aa0c4',
                'up' => '#19d39a',
                'down' => '#ff5d73',
            ),
            'red' => array(
                'name' => 'Red & Black',
                'bg0' => '#0a0608',
                'bg1' => '#140a0d',
                'bg2' => '#200f15',
                'accent' => '#ff2b4d',
                'accent2' => '#ff6a3d',
                'accent3' => '#ff4f7b',
                'glow' => 'rgba(255,43,77,0.28)',
                'card' => 'rgba(40,18,24,0.55)',
                'card_brd' => 'rgba(255,90,120,0.18)',
                'text' => '#ffeef0',
                'muted' => '#c79aa3',
                'up' => '#19d39a',
                'down' => '#ff5d73',
            ),
            'teal' => array(
                'name' => 'Teal (default)',
                'bg0' => '#070b14',
                'bg1' => '#0c1322',
                'bg2' => '#111c30',
                'accent' => '#16e0a3',
                'accent2' => '#2bb8ff',
                'accent3' => '#9b6bff',
                'glow' => 'rgba(22,224,163,0.28)',
                'card' => 'rgba(20,30,50,0.55)',
                'card_brd' => 'rgba(120,160,220,0.16)',
                'text' => '#e8f0ff',
                'muted' => '#8aa0c4',
                'up' => '#16e0a3',
                'down' => '#ff5d73',
            ),
        );
    }
}

if (!function_exists('epc_theme_default')) {
    /** Default preset key when none is set. */
    function epc_theme_default(): string
    {
        return 'blue';
    }
}

if (!function_exists('epc_theme_get')) {
    /**
     * Resolve a preset by key; unknown → default.
     *
     * @return array<string,string>
     */
    function epc_theme_get(string $key): array
    {
        $presets = epc_theme_presets();
        $k = strtolower(trim($key));
        if (!isset($presets[$k])) {
            $k = epc_theme_default();
        }
        $p = $presets[$k];
        $p['key'] = $k;
        return $p;
    }
}

if (!function_exists('epc_theme_css_vars')) {
    /**
     * `:root` block setting both the generic `--epc-*` tokens and the ERP
     * `--erp-*` variables so a single preset re-skins every surface.
     */
    function epc_theme_css_vars(string $key): string
    {
        $t = epc_theme_get($key);
        return ':root{'
            . '--epc-bg:' . $t['bg0'] . ';'
            . '--epc-bg-1:' . $t['bg1'] . ';'
            . '--epc-bg-2:' . $t['bg2'] . ';'
            . '--epc-accent:' . $t['accent'] . ';'
            . '--epc-accent-2:' . $t['accent2'] . ';'
            . '--epc-accent-3:' . $t['accent3'] . ';'
            . '--epc-glow:' . $t['glow'] . ';'
            . '--epc-card:' . $t['card'] . ';'
            . '--epc-text:' . $t['text'] . ';'
            . '--epc-muted:' . $t['muted'] . ';'
            // override the existing ERP theme variables too:
            . '--erp-bg-0:' . $t['bg0'] . ';'
            . '--erp-bg-1:' . $t['bg1'] . ';'
            . '--erp-bg-2:' . $t['bg2'] . ';'
            . '--erp-card:' . $t['card'] . ';'
            . '--erp-card-brd:' . $t['card_brd'] . ';'
            . '--erp-accent:' . $t['accent'] . ';'
            . '--erp-accent-2:' . $t['accent2'] . ';'
            . '--erp-accent-3:' . $t['accent3'] . ';'
            . '--erp-text:' . $t['text'] . ';'
            . '--erp-muted:' . $t['muted'] . ';'
            . '--erp-up:' . $t['up'] . ';'
            . '--erp-down:' . $t['down'] . ';'
            . '}';
    }
}

if (!function_exists('epc_theme_style_tag')) {
    /** Ready-to-inject <style> with the preset's variables (data-epc-theme tag). */
    function epc_theme_style_tag(string $key): string
    {
        $t = epc_theme_get($key);
        return '<style id="epc-theme" data-theme="' . htmlspecialchars($t['key'], ENT_QUOTES, 'UTF-8') . '">'
            . epc_theme_css_vars($key)
            . '</style>';
    }
}

if (!function_exists('epc_theme_swatch_html')) {
    /** Small preview swatch for the theme picker. */
    function epc_theme_swatch_html(string $key): string
    {
        $t = epc_theme_get($key);
        $esc = static function ($s) {
            return htmlspecialchars((string) $s, ENT_QUOTES, 'UTF-8');
        };
        return '<div class="epc-theme-swatch" data-theme="' . $esc($t['key']) . '" '
            . 'style="background:linear-gradient(160deg,' . $esc($t['bg0']) . ',' . $esc($t['bg1']) . ');'
            . 'border:1px solid ' . $esc($t['card_brd']) . ';border-radius:12px;padding:14px 16px;color:' . $esc($t['text']) . '">'
            . '<span style="display:inline-block;width:14px;height:14px;border-radius:50%;background:' . $esc($t['accent']) . ';box-shadow:0 0 10px ' . $esc($t['glow']) . '"></span> '
            . '<span style="display:inline-block;width:14px;height:14px;border-radius:50%;background:' . $esc($t['accent2']) . ';margin-right:8px"></span>'
            . $esc($t['name'])
            . '</div>';
    }
}

if (!function_exists('epc_theme_resolve_setting')) {
    /**
     * Resolve the active theme key from a stored setting value (string),
     * falling back to the default. Keeps theme selection DB-agnostic.
     */
    function epc_theme_resolve_setting($value): string
    {
        if (is_string($value) && $value !== '') {
            $t = epc_theme_get($value);
            return $t['key'];
        }
        return epc_theme_default();
    }
}

if (!function_exists('epc_theme_surface_map')) {
    /**
     * Per-surface theme assignment. Professional scheme: the whole business
     * platform is Blue & Black; only the consumer storefront is Red & Black so
     * the shopper-facing site has a distinct identity.
     *
     * @return array<string,string> surface => preset key
     */
    function epc_theme_surface_map(): array
    {
        return array(
            'marketing' => 'blue',
            'supercp' => 'blue',
            'tenantcp' => 'blue',
            'erp' => 'blue',
            'storefront' => 'red',
        );
    }
}

if (!function_exists('epc_theme_for_surface')) {
    /**
     * Resolve the active preset key for a given surface.
     *
     * Precedence: a global EPC_UI_THEME env override wins for every surface;
     * otherwise a per-surface override EPC_UI_THEME_<SURFACE>; otherwise the
     * surface map; otherwise the default.
     */
    function epc_theme_for_surface(string $surface): string
    {
        $global = getenv('EPC_UI_THEME');
        if (is_string($global) && $global !== '') {
            return epc_theme_get($global)['key'];
        }
        $s = strtolower(trim($surface));
        $envKey = 'EPC_UI_THEME_' . strtoupper($s);
        $perSurface = getenv($envKey);
        if (is_string($perSurface) && $perSurface !== '') {
            return epc_theme_get($perSurface)['key'];
        }
        $map = epc_theme_surface_map();
        if (isset($map[$s])) {
            return epc_theme_get($map[$s])['key'];
        }
        return epc_theme_default();
    }
}

if (!function_exists('epc_theme_style_tag_for_surface')) {
    /** Ready-to-inject <style> for a surface, honouring overrides + map. */
    function epc_theme_style_tag_for_surface(string $surface): string
    {
        return epc_theme_style_tag(epc_theme_for_surface($surface));
    }
}
