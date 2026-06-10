<?php
//Шаблон дизайна уведомления

//Настройки шаблона
$templates = array();
$templates_query = $db_link->prepare('SELECT * FROM `templates` WHERE `is_frontend` = 1 AND `current` = 1 LIMIT 1;');
$templates_query->execute();
$templates = $templates_query->fetch();
$templates = json_decode($templates['data_value'], true);

ob_start();
?>
<div style="text-align:center; padding:50px 50px 50px 50px; background:#f5f5f5; border-radius: 15px; font-family:Calibri; font-size: 16px;">
	<div style="text-align:left; max-width:1000px; display: block; padding: 30px; border-radius: 0px; background: white; margin: 50px auto; border-radius: 10px; border: 1px solid #cfcfcf; box-shadow: 0px 14px 28px #979797;">

		<div style="text-align:left;">
			<table border="0" cellspacing="0" cellpadding="0" width="100%" style="width: 100%; border: none; margin: 0; padding: 0;">
				<tr>
					<td class="dp_email_logo" valign="top" style="border: none; margin: 0; padding: 0;">
					<?php
					if(!empty($templates['logo_file']) && file_exists($_SERVER["DOCUMENT_ROOT"].$templates['logo_file'])){
					?>
						<a href="<?php echo $DP_Config->domain_path; ?>" target="_blank">
							<img style="max-height: 60px; max-width: 300px; margin-right: 50px;<?=($templates['bg_transparent_logo'] == 1)?' background:'. $templates['main_color'] .';':'';?> image-rendering: -webkit-optimize-contrast;" src="<?php echo $DP_Config->domain_path . substr($templates['logo_file'],1); ?>?v=<?php echo (int)$templates['version']; ?>" alt="logotype"/>
						</a>
					<?php
					}
					?>
					</td>
					<td class="dp_email_subject" valign="top" style="border: none; margin: 0; padding: 0; text-align:right; font-size: 18px; line-height: 16px;">
						<?php
						echo $email_subject;
						echo '<br/>';
						echo '<small style="font-size: 13px;">'. date("d.m.Y H:i", time()) .'</small>';
						?>
					</td>
				</tr>
			</table>
		</div>
		
		<div style="border-top: 1px solid #cfcfcf; margin: 22px 0px 18px 0px;"></div>
		
		<div style="text-align:left;">
		<?php
		echo $email_body;
		?>
		</div>
		
		<div style="border-top: 1px solid #cfcfcf; margin: 22px 0px 18px 0px;"></div>
		
		<p style="text-align: center; color: #5a5a5a; font-size: 11px; line-height: 1.1em; padding: 0; margin: 0; margin-bottom: -13px;">
			<?php echo translate_str_by_id(4929); ?>.
			<br/>
			<?php echo translate_str_by_id(4930); ?>.
		</p>
		
	</div>
</div>
<style type="text/css">
@media screen and (max-width: 767px) {
	.dp_email_logo {
		text-align: center !important;
	}
	.dp_email_subject {
		display: none !important;
	}
}
</style>
<?php
$email_body = ob_get_clean();

$email_body = str_replace('<div class="collapse" id="collapse_office_map_container">', '<div style="display:none;">', $email_body);

//Способ получения
$obtain_query = $db_link->prepare( 'SELECT * FROM `shop_obtaining_modes`;' );
$obtain_query->execute();
while($obtain_mode = $obtain_query->fetch()){
	$obtain_mode_caption = "<p>".translate_str_by_id(3507)." - <b>".$obtain_mode["caption"]."</b></p>";
	$email_body = str_replace($obtain_mode_caption, '<h4>'.translate_str_by_id(3507).' - '.$obtain_mode["caption"].'</h4>', $email_body);
	$obtain_mode_caption = '<p class="lead">'.translate_str_by_id(3507).' - '.$obtain_mode["caption"]."</p>";
	$email_body = str_replace($obtain_mode_caption, '<h4>'.translate_str_by_id(3507).' - '.$obtain_mode["caption"].'</h4>', $email_body);
}

$email_body = str_replace('<h4>', '<h4 style="font-family: Calibri; font-size: 16px; padding: 0; margin: 0; margin-top: 20px; margin-right: 0px; margin-bottom: 5px; margin-left: 0px; font-weight: bold; display: block;">', $email_body);
$email_body = str_replace(array('<table class="table">', '<table>'), '<table cellspacing="0" style="border-collapse: collapse; padding: 0; margin: 0; margin-top: 0px; margin-right: 0px; margin-bottom: 0px; margin-left: 0px; border: 1px solid #cfcfcf; font-family: Calibri; font-size: 12px;">', $email_body);
$email_body = str_replace('<tr>', '<tr style="border: 1px solid #cfcfcf;">', $email_body);
$email_body = str_replace('<th>', '<th style="border: 1px solid #cfcfcf; padding: 5px;">', $email_body);
$email_body = str_replace('<th colspan=', '<th style="border: 1px solid #cfcfcf; padding: 5px;" colspan=', $email_body);
$email_body = str_replace('<td>', '<td style="border: 1px solid #cfcfcf; padding: 5px;">', $email_body);
$email_body = str_replace('<td colspan=', '<td style="border: 1px solid #cfcfcf; padding: 5px;" colspan=', $email_body);
?>