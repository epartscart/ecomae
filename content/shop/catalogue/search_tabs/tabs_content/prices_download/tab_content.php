<?php
/**
 * Storefront search tab: download published price list for the buyer's markup group.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$userProfile = DP_User::getUserProfile();
$group_id = 0;
if (is_array($userProfile) && !empty($userProfile['groups'][0])) {
	$group_id = (int) $userProfile['groups'][0];
}
$file = $_SERVER['DOCUMENT_ROOT'] . '/content/files/Documents/prices_tmp/prices_' . $group_id . '.csv';
$has_file = ($group_id > 0 && is_file($file));
?>
<style>
.epc-pdl-tab{background:linear-gradient(135deg,#0f172a 0%,#1e3a8a 100%);color:#eff6ff;border-radius:10px;padding:16px 18px;margin:8px 0}
.epc-pdl-tab h4{margin:0 0 6px;font-size:16px;font-weight:700}
.epc-pdl-tab p{margin:0 0 10px;opacity:.9;font-size:13px}
.epc-pdl-tab .epc-pdl-meta{font-size:12px;opacity:.8;margin-bottom:12px}
.epc-pdl-tab a.epc-pdl-dl{display:inline-block;background:#fff;color:#1e3a8a;padding:8px 14px;border-radius:8px;font-weight:600;text-decoration:none}
.epc-pdl-tab a.epc-pdl-dl:hover{opacity:.92}
.epc-pdl-tab .epc-pdl-empty{opacity:.9;font-size:13px;margin:0}
</style>
<div class="epc-pdl-tab">
	<h4><?php echo function_exists('translate_str_by_id') ? translate_str_by_id(4185) : 'Your price list'; ?></h4>
	<?php if ($has_file) { ?>
		<p>CSV with your account markup group prices. Open in Excel / Google Sheets.</p>
		<div class="epc-pdl-meta">
			<?php echo function_exists('translate_str_by_id') ? translate_str_by_id(3763) : 'Updated'; ?>:
			<?php echo date('d.m.Y H:i', filemtime($file)); ?>
			· <?php echo number_format(filesize($file)); ?> bytes
			· group <?php echo (int) $group_id; ?>
		</div>
		<a class="epc-pdl-dl" href="<?php echo '/content/files/Documents/prices_tmp/prices_' . (int) $group_id . '.csv?v=' . time(); ?>">
			<i class="fa fa-download"></i>
			<?php echo function_exists('translate_str_by_id') ? translate_str_by_id(4186) : 'Download price list'; ?>
		</a>
	<?php } else { ?>
		<p class="epc-pdl-empty"><?php echo function_exists('translate_str_by_id') ? translate_str_by_id(4187) : 'No price list is published for your group yet. Contact your account manager.'; ?></p>
	<?php } ?>
</div>
