<?php
/**
 * CP eval'd content: inline <script> inside .row/main pane renders as plain text.
 * Strip scripts from main content before eval and re-emit in desktop.php footer.
 */
defined('_ASTEXE_') or die('No access');

function epc_cp_footer_scripts_reset(): void
{
	$GLOBALS['epc_cp_footer_scripts'] = array();
}

function epc_cp_footer_scripts_append(string $scriptHtml): void
{
	$scriptHtml = trim($scriptHtml);
	if ($scriptHtml === '') {
		return;
	}
	if (!isset($GLOBALS['epc_cp_footer_scripts']) || !is_array($GLOBALS['epc_cp_footer_scripts'])) {
		$GLOBALS['epc_cp_footer_scripts'] = array();
	}
	$GLOBALS['epc_cp_footer_scripts'][] = $scriptHtml;
}

function epc_cp_extract_scripts_from_html(string $html): string
{
	if ($html === '') {
		return $html;
	}
	$pattern = '#<script\b[^>]*>.*?</script\s*>#is';
	return preg_replace_callback(
		$pattern,
		function (array $m): string {
			epc_cp_footer_scripts_append($m[0]);
			return '';
		},
		$html
	) ?? $html;
}

/**
 * Move <style> blocks out of the main pane too — with <base href="/cp/templates/...">
 * unreloated CSS/JS in .row can appear as literal "code structure" text in some browsers.
 */
function epc_cp_extract_styles_from_html(string $html): string
{
	if ($html === '' || stripos($html, '<style') === false) {
		return $html;
	}
	if (!isset($GLOBALS['epc_cp_footer_styles']) || !is_array($GLOBALS['epc_cp_footer_styles'])) {
		$GLOBALS['epc_cp_footer_styles'] = array();
	}
	$pattern = '#<style\b[^>]*>.*?</style\s*>#is';
	return preg_replace_callback(
		$pattern,
		function (array $m): string {
			$GLOBALS['epc_cp_footer_styles'][] = $m[0];
			return '';
		},
		$html
	) ?? $html;
}

function epc_cp_prepare_cp_page_content(string $content): string
{
	$content = epc_cp_extract_styles_from_html($content);
	return epc_cp_extract_scripts_from_html($content);
}

function epc_cp_prepare_cp_modules(array &$positionsHtmlArray): void
{
	foreach ($positionsHtmlArray as $position => $modulesHtml) {
		$positionsHtmlArray[$position] = epc_cp_prepare_cp_page_content((string) $modulesHtml);
	}
}

function epc_cp_main_pane_begin_marker(): string
{
	return '<!--epc-cp-main-begin-->';
}

function epc_cp_main_pane_end_marker(): string
{
	return '<!--epc-cp-main-end-->';
}

function epc_cp_relocate_main_pane_scripts(string $html): string
{
	$begin = epc_cp_main_pane_begin_marker();
	$end = epc_cp_main_pane_end_marker();
	$start = strpos($html, $begin);
	$stop = strpos($html, $end);
	if ($start === false || $stop === false || $stop <= $start) {
		return $html;
	}
	$start += strlen($begin);
	$main = substr($html, $start, $stop - $start);
	// PHP-eval'd pages still emit inline <style>/<script> into the main pane;
	// with <base href="/cp/templates/..."> those tags can render as visible text.
	$main = epc_cp_extract_styles_from_html($main);
	$main = epc_cp_extract_scripts_from_html($main);
	return substr($html, 0, $start) . $main . substr($html, $stop);
}

function epc_cp_strip_main_pane_markers(string $html): string
{
	return str_replace(
		array(epc_cp_main_pane_begin_marker(), epc_cp_main_pane_end_marker()),
		'',
		$html
	);
}

function epc_cp_finalize_cp_html(string $html): string
{
	$html = epc_cp_relocate_main_pane_scripts($html);
	$html = epc_cp_strip_main_pane_markers($html);
	$html = epc_cp_boc_first_paint_patch($html);
	if (function_exists('epc_portal_demo_cp_rewrite_nav_urls')) {
		$html = epc_portal_demo_cp_rewrite_nav_urls($html);
	}
	return $html;
}

/**
 * Eliminate the blue legacy-CP flash on BOS/BOC console pages: when the rendered
 * page contains the BOC console, mark <body> as epc-boc-mode and inject a tiny
 * first-paint <style> into <head> so the legacy chrome is hidden and the dark
 * control-room background paints immediately (no flash before the JS/body style
 * lower in the document is reached).
 */
function epc_cp_boc_first_paint_patch(string $html): string
{
	if (strpos($html, 'class="epc-boc"') === false || stripos($html, 'epc-boc-first-paint') !== false) {
		return $html;
	}
	// Add the mode class to <body> (idempotent) so head CSS can target it.
	$html = preg_replace_callback(
		'/<body\b[^>]*\bclass\s*=\s*"([^"]*)"/i',
		function ($m) {
			if (strpos($m[0], 'epc-boc-mode') !== false) {
				return $m[0];
			}
			return str_replace('class="' . $m[1] . '"', 'class="' . $m[1] . ' epc-boc-mode"', $m[0]);
		},
		$html,
		1
	);
	$css = '<style id="epc-boc-first-paint">'
		. 'html:has(body.epc-boc-mode),body.epc-boc-mode{background:#0b1220!important;}'
		. 'body.epc-boc-mode #header,body.epc-boc-mode #menu,body.epc-boc-mode .navbar-static-side,'
		. 'body.epc-boc-mode nav.navbar-default,body.epc-boc-mode #top-navigation,body.epc-boc-mode .epc-cp-topbar,'
		. 'body.epc-boc-mode .footer,body.epc-boc-mode #right-sidebar,body.epc-boc-mode .splash,'
		. 'body.epc-boc-mode #navigation{display:none!important;}'
		. 'body.epc-boc-mode #wrapper,body.epc-boc-mode .content{margin:0!important;padding:0!important;'
		. 'width:100%!important;max-width:100%!important;min-height:100vh!important;background:#0b1220!important;}'
		. '</style>';
	if (stripos($html, '</head>') !== false) {
		$html = preg_replace('/<\/head>/i', $css . '</head>', $html, 1);
	}
	return $html;
}

function epc_cp_render_relocated_footer_scripts(): void
{
	if (!empty($GLOBALS['epc_cp_footer_styles']) && is_array($GLOBALS['epc_cp_footer_styles'])) {
		foreach ($GLOBALS['epc_cp_footer_styles'] as $styleHtml) {
			echo $styleHtml . "\n";
		}
	}
	if (empty($GLOBALS['epc_cp_footer_scripts']) || !is_array($GLOBALS['epc_cp_footer_scripts'])) {
		return;
	}
	foreach ($GLOBALS['epc_cp_footer_scripts'] as $scriptHtml) {
		echo $scriptHtml . "\n";
	}
}

/**
 * Redirect from CP page PHP (eval-safe).
 * Do not put <script>location="<?php echo … ?>" in page source — script relocation strips
 * those blocks before eval and the footer emits literal PHP, which base href then resolves
 * under /cp/templates/bootstrap_admin/.
 *
 * @param string $path Path after backend dir, e.g. /shop/prices?error_message=foo
 */
function epc_cp_redirect(string $path): void
{
	$path = '/' . ltrim($path, '/');
	$prefix = function_exists('epc_cp_nav_url_prefix') ? epc_cp_nav_url_prefix() : '/cp';
	$url = (strpos($path, $prefix . '/') === 0 || $path === $prefix) ? $path : $prefix . $path;
	if (function_exists('epc_portal_demo_cp_scope_cp_path')) {
		$url = epc_portal_demo_cp_scope_cp_path($url);
	}
	if (!headers_sent()) {
		header('Location: ' . $url, true, 302);
		exit;
	}
	$script = '<script>location=' . json_encode($url) . ';</script>';
	if (function_exists('epc_cp_footer_scripts_append')) {
		epc_cp_footer_scripts_append($script);
	} else {
		echo $script;
	}
	exit;
}
