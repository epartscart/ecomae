<?php
//Скрипт для подключения виджета каталога levam
defined('_ASTEXE_') or die('No access');

//START - Каталог Levam (НОВЫЙ ВАРИАНТ - С ВИДЖЕТОМ)
if( isset($DP_Config->levam_code) )
{
	if($DP_Config->levam_code != '')
	{
		?>
		<style>
		/*Стили для адаптации самого каталога*/
		.oem_widget_std_block
		{
			width:210px!important;
		}
		.oem_widget_left_sidebar2 + .w-100 + .pa-0 .oem_widget_std_block,
		.oem_widget_marks .oem_widget_std_block,
		.oem_widget_std_block
		{
			height:60px!important;
			padding-top:0!important;
			width:210px!important;
		}
		@media screen and (max-width: 600px)
		{
			.oem_widget_left_sidebar2 + .w-100 + .pa-0 .oem_widget_std_block,
			.oem_widget_marks .oem_widget_std_block,
			.oem_widget_std_block
			{
				height:60px!important;
				min-height:60px!important;
				padding-top:0!important;
				width:190px!important;
			}
		}
		.oem_widget_left_sidebar2 + .w-100 + .pa-0 .v-responsive__sizer,
		.oem_widget_left_sidebar2 + .w-100 + .pa-0 .v-image__image,
		.oem_widget_left_sidebar2 + .w-100 + .pa-0 .v-image,
		.oem_widget_mark_block .v-image,
		.oem_widget_std_block .v-image,
		.oem_widget_top_car_image_wrap,
		.oem_widget_top_car_image
		{
			display:none;
			height:0!important;
			width:0!important;
			margin-top:0!important;
			padding:0!important;
		}
		.oem_widget_std_block_wrap_img
		{
			height:10px!important;
		}
		</style>
		<br>
		<script id="levam_oem_catalog" src="https://widgets.levam.net/oem-widget/loader.js" lang="ru" code="<?php echo $DP_Config->levam_code; ?>" async></script>
		<?php
	}
}
//~END - Каталог Levam (НОВЫЙ ВАРИАНТ - С ВИДЖЕТОМ)
?>