<?php
/**
 * Страничный скрипт для предложения авторизоваться неавторизованному пользователю
*/
defined('_ASTEXE_') or die('No access');
?>


<div class="panel panel-primary">
<?php
//Единый механизм формы авторизации
$login_form_postfix = "login_offer";
$login_form_target = "shop/checkout/how_get";
require($_SERVER["DOCUMENT_ROOT"]."/modules/login/login_form_general.php");
?>

	
	<?php
	if( isset( $DP_Config->order_without_auth ) )
	{
		if( $DP_Config->order_without_auth == 1 )
		{
			?>
			<div class="panel-body" style="color:#777;">
				<a class="btn btn-ar btn-default" href="<?php echo $multilang_params['lang_href']; ?>/shop/checkout/how_get"><?php echo translate_str_by_id(4523); ?></a>
			</div>
			<?php
		}
	}
	?>

	
</div>





