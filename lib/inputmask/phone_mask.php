<?php
/**
 * Маска ввода телефона, скрипт подключается в нижнею панель клиентской стороны и в шаблоне панели управления
*/
defined('_ASTEXE_') or die('No access');

if( (int) $DP_Config->show_phone_mask === 1 && 
	$DP_Content->id != 264 //Свойства магазина - не отображаем маску
)
{
?>
	<script src="/lib/inputmask/jquery.inputmask.min.js"></script>
	<script type="text/javascript">
	jQuery(document).ready(function($)
	{
		
		<?php
		switch($DP_Config->country_phone_mask){
			case 'ru' :
				echo 'var mask = "+7 (999) 999-99-99";//Россия';
			break;
			case 'kz' :
				echo 'var mask = "+7 (999) 999-99-99";//Казахстан';
			break;
			case 'by' :
				echo 'var mask = "+375 (99) 999-99-99";//Белоруссия';
			break;
			case 'ua' :
				echo 'var mask = "+380 (99) 999-9999";//Украина';
			break;
		}
		?>
		
		
		
		
		////////////////////////////////////////////////////////////////////
		
		
		// Добавляем маску для полей
		$('#phone').inputmask({"mask": mask});
		$('#phone_contact_input').inputmask({"mask": mask});
		$('#cellphone').inputmask({"mask": mask});// Страница "Форма регистрации" - /users/registration
		$('#phone_input').inputmask({"mask": mask});// Страница формы "Доставка по адресу" - /shop/checkout/how_get
		$('#phone_not_auth').inputmask({"mask": mask});// Страница формы "Самовывоз" для неавторизованного пользователя - /shop/checkout/how_get
		$('#client_phone').inputmask({"mask": mask});// Страница "VIN-запрос" - /vin-zapros
		$('#tel').inputmask({"mask": mask});
		$('#telefon').inputmask({"mask": mask});
		
		$('.phone-simple-register').inputmask({"mask": mask});
		$("input[name='phone']").inputmask({"mask": mask});
		
		
		////////////////////////////////////////////////////////////////////
		
		// Форма авторизации в панели управления
		if(document.getElementById("auth_contact_select")){
			$("select[name='auth_contact_select']").change(function() {
				if( $(this).val() == "email" )
				{
					$("input[name='auth_contact']").inputmask('remove');
				}
				else
				{
					$("input[name='auth_contact']").inputmask({"mask": mask});
				}
			});
			
			if( document.getElementById("auth_contact_select").value == "email" )
			{
				$("input[name='auth_contact']").inputmask('remove');
			}
			else
			{
				$("input[name='auth_contact']").inputmask({"mask": mask});
			}
		}
		
		////////////////////////////////////////////////////////////////////
		
		// Форма авторизации
		if(document.getElementById("auth_contact_selectheader_top_tab")){
			$("select[name='auth_contact_type']").change(function() {
				if( $(this).val() == "email" )
				{
					$("input[name='auth_contact']").inputmask('remove');
				}
				else
				{
					$("input[name='auth_contact']").inputmask({"mask": mask});
				}
			});
			
			if( document.getElementById("auth_contact_selectheader_top_tab").value == "email" )
			{
				$("input[name='auth_contact']").inputmask('remove');
			}
			else
			{
				$("input[name='auth_contact']").inputmask({"mask": mask});
			}
		}
		
		////////////////////////////////////////////////////////////////////
		
		// Форма регистрации
		if(document.getElementById("reg_contact_select")){
			$("select[name='reg_contact_type']").change(function() {
				if( $(this).val() == "email" )
				{
					$("input[name='reg_contact']").inputmask('remove');
				}
				else
				{
					$("input[name='reg_contact']").inputmask({"mask": mask});
				}
			});
			
			if( document.getElementById("reg_contact_select").value == "email" )
			{
				$("input[name='reg_contact']").inputmask('remove');
			}
			else
			{
				$("input[name='reg_contact']").inputmask({"mask": mask});
			}
		}
		
		////////////////////////////////////////////////////////////////////
		
		// Форма восстановления пароля
		if(document.getElementById("forgot_password_contact_select")){
			$("select[name='forgot_password_contact_type']").change(function() {
				if( $(this).val() == "email" )
				{
					$("input[name='forgot_password_contact']").inputmask('remove');
				}
				else
				{
					$("input[name='forgot_password_contact']").inputmask({"mask": mask});
				}
			});
			
			if( document.getElementById("forgot_password_contact_select").value == "email" )
			{
				$("input[name='forgot_password_contact']").inputmask('remove');
			}
			else
			{
				$("input[name='forgot_password_contact']").inputmask({"mask": mask});
			}
		}
	});
	</script>
<?php
}// END - Маска ввода телефона
?>