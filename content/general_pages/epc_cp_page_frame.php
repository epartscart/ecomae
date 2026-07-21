<?php
/**
 * CP page frame — full-width content wrapper matching /cp/control dashboard layout.
 * On Super CP (BOS), automatically opens the main BOS topnav shell so portal
 * modules never drop into a nested legacy "detail window".
 */
defined('_ASTEXE_') or die('No access');

function epc_cp_register_page_assets(array $css = [], array $js = []): void
{
	if (!isset($GLOBALS['epc_cp_page_assets']) || !is_array($GLOBALS['epc_cp_page_assets'])) {
		$GLOBALS['epc_cp_page_assets'] = array('css' => array(), 'js' => array());
	}
	foreach ($css as $href) {
		$href = trim((string) $href);
		if ($href !== '') {
			$GLOBALS['epc_cp_page_assets']['css'][$href] = true;
		}
	}
	foreach ($js as $src) {
		$src = trim((string) $src);
		if ($src !== '') {
			$GLOBALS['epc_cp_page_assets']['js'][$src] = true;
		}
	}
}

function epc_cp_page_frame_open(array $opts = array()): void
{
	$shellFile = __DIR__ . '/epc_boc_page_shell.php';
	if (is_file($shellFile)) {
		require_once $shellFile;
		if (function_exists('epc_boc_should_use_page_shell') && epc_boc_should_use_page_shell()) {
			$title = '';
			if (!empty($opts['hero']['title'])) {
				$title = (string) $opts['hero']['title'];
			}
			epc_boc_page_shell_open($title !== '' ? array('title' => $title) : array());
			$GLOBALS['epc_cp_page_frame_opened_boc'] = true;
		}
	}
	$classes = array('col-lg-12', 'epc-cp-page-frame');
	if (!empty($opts['class'])) {
		$classes[] = (string) $opts['class'];
	}
	echo '<div class="' . htmlspecialchars(implode(' ', $classes), ENT_QUOTES, 'UTF-8') . '">';
	if (!empty($opts['hero']) && is_array($opts['hero'])) {
		epc_cp_page_frame_hero($opts['hero']);
	}
}

function epc_cp_page_frame_hero(array $hero): void
{
	$badge = (string) ($hero['badge'] ?? $hero['eyebrow'] ?? '');
	$title = (string) ($hero['title'] ?? '');
	$sub = (string) ($hero['sub'] ?? $hero['subtitle'] ?? '');
	$allowHtmlSub = !empty($hero['html_sub']);
	?>
<div class="epc-scp-dashboard__hero epc-cp-page-header">
	<div>
		<?php if ($badge !== '') { ?>
		<span class="epc-scp-dashboard__badge"><?php echo htmlspecialchars($badge, ENT_QUOTES, 'UTF-8'); ?></span>
		<?php } ?>
		<?php if ($title !== '') { ?>
		<h2 class="epc-scp-dashboard__title"><?php echo htmlspecialchars($title, ENT_QUOTES, 'UTF-8'); ?></h2>
		<?php } ?>
		<?php if ($sub !== '') { ?>
		<p class="epc-scp-dashboard__sub"><?php echo $allowHtmlSub ? $sub : htmlspecialchars($sub, ENT_QUOTES, 'UTF-8'); ?></p>
		<?php } ?>
	</div>
	<?php if (!empty($hero['actions']) && is_array($hero['actions'])) { ?>
	<div class="epc-scp-dashboard__hero-actions">
		<?php foreach ($hero['actions'] as $act) {
			if (!is_array($act)) {
				continue;
			}
			$btnClass = !empty($act['primary']) ? 'btn-primary' : 'btn-default';
			?>
		<a class="btn btn-sm <?php echo $btnClass; ?>" href="<?php echo htmlspecialchars((string) ($act['url'] ?? '#'), ENT_QUOTES, 'UTF-8'); ?>">
			<?php if (!empty($act['icon'])) { ?><i class="fa <?php echo htmlspecialchars((string) $act['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i> <?php } ?>
			<?php echo htmlspecialchars((string) ($act['label'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>
		</a>
		<?php } ?>
	</div>
	<?php } ?>
</div>
	<?php
}

function epc_cp_page_frame_close(): void
{
	echo '</div>';
	if (!empty($GLOBALS['epc_cp_page_frame_opened_boc'])) {
		$shellFile = __DIR__ . '/epc_boc_page_shell.php';
		if (is_file($shellFile)) {
			require_once $shellFile;
			if (function_exists('epc_boc_page_shell_close')) {
				epc_boc_page_shell_close();
			}
		}
		$GLOBALS['epc_cp_page_frame_opened_boc'] = false;
	}
}
