<?php
/**
 * Страничный скрипт для страницы вывода функций магазина
*/
defined('_ASTEXE_') or die('No access');
?>
<h1><?php echo translate_str_by_id(4409); ?></h1>

<div class="cat-item">
	<a href="<?php echo $multilang_params['lang_href']; ?>/shop/cart">
		<?php echo translate_str_by_id(4410); ?>
	</a>
</div>

<div class="cat-item">
	<a href="<?php echo $multilang_params['lang_href']; ?>/shop/orders">
		<?php echo translate_str_by_id(4411); ?>
	</a>
</div>
