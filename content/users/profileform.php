<?php
/**
 * Страничный скрипт для страницы "Мои данные"
 * 
*/
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


if(DP_User::getUserId() == 0)
{
    echo translate_str_by_id(4709);
}
else//Пользователь авторизован - выводим его данные
{
    $user_profile = DP_User::getUserProfile();//Получаем данные пользователя
    $user_session = DP_User::getUserSession();
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
    $epc_profile_user_id = (int)DP_User::getUserId();

    if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_request_currency_change'])) {
        require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
        $requested_iso = preg_replace('/[^0-9]/', '', (string)($_POST['epc_requested_currency'] ?? ''));
        $note = trim((string)($_POST['epc_currency_change_note'] ?? ''));
        epc_trade_request_currency_change($db_link, $epc_profile_user_id, $requested_iso, $note);
        require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
        require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/notify_helper.php';
        require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_currency.php';
        $body = epc_build_customer_profile_html($epc_profile_user_id);
        $cur_rows = epc_currency_records($db_link, $DP_Config);
        if ($requested_iso !== '' && isset($cur_rows[$requested_iso]['caption_short'])) {
            $body .= '<p><strong>Requested currency:</strong> ' . htmlspecialchars($cur_rows[$requested_iso]['caption_short'], ENT_QUOTES, 'UTF-8') . '</p>';
        }
        if ($note !== '') {
            $body .= '<p><strong>Customer note:</strong> ' . htmlspecialchars($note, ENT_QUOTES, 'UTF-8') . '</p>';
        }
        $body .= '<p>Customer requested a dealing currency change. Review in CP → Users → Customer approvals.</p>';
        send_notify('reg_notify_admin', array('user_profile' => $body), epc_staff_notify_persons($epc_profile_user_id), true);
        echo '<div class="alert alert-success">Currency change request sent to the manager.</div>';
        $user_profile = DP_User::getUserProfile();
    }
	?>
	
	
	
	<!-- Здесь храним html для формы ввода кода подтверждения телефона -->
	<div id="phone_code_store" style="display:none;" class="hidden">
		<form method="GET" action="<?php echo $multilang_params['lang_href']; ?>/users/confirm_contact">
			<input type="hidden" name="u_id" value="<?php echo DP_User::getUserId(); ?>" />
			<input type="hidden" name="type" value="phone" />
		
			<div class="input-group">
				<input value="" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4708); ?>" name="code" id="code" />
				<span class="input-group-btn">
					<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(4521); ?></button>
				</span>
			</div>
		</form>
	</div>
	
	
    
    <table class="table">
	<?php
	$epc_trade_status = epc_trade_approval_status($db_link, $epc_profile_user_id);
	$epc_trade_type = epc_trade_profile_get($db_link, $epc_profile_user_id, 'epc_customer_type', '');
	if ($epc_trade_type !== '' || $epc_trade_status !== 'approved') {
		echo '<tr><td><b>Trade account</b></td><td>';
		if ($epc_trade_type !== '') {
			echo epc_trade_customer_type_label($epc_trade_type);
		}
		echo '<br><span class="label label-' . ($epc_trade_status === 'approved' ? 'success' : ($epc_trade_status === 'pending' ? 'warning' : 'danger')) . '">' . htmlspecialchars(ucfirst($epc_trade_status), ENT_QUOTES, 'UTF-8') . '</span>';
		if ($epc_trade_status === 'pending') {
			echo '<br><small>You can browse and add to cart. Checkout opens after manager approval.</small>';
		}
		if ($epc_trade_status === 'approved') {
			$cur_iso = epc_trade_user_currency_iso($db_link, $epc_profile_user_id);
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_currency.php';
			$cur_rows = epc_currency_records($db_link, $DP_Config);
			$cur_lbl = isset($cur_rows[$cur_iso]['caption_short']) ? $cur_rows[$cur_iso]['caption_short'] : $cur_iso;
			echo '<br><strong>Dealing currency:</strong> ' . htmlspecialchars($cur_lbl, ENT_QUOTES, 'UTF-8');
			if (epc_trade_profile_get($db_link, $epc_profile_user_id, 'epc_currency_change_requested', '') === '1') {
				echo ' <span class="label label-info">Change pending</span>';
			}
		}
		echo '</td></tr>';
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_customer_vat.php';
	$epc_profile_vat = epc_uae_customer_vat_resolve($db_link, $epc_profile_user_id);
	echo '<tr><td><b>VAT treatment</b></td><td>'
		. htmlspecialchars($epc_profile_vat['vat_type_label'], ENT_QUOTES, 'UTF-8')
		. '<br><small>Prices shown: <strong>' . htmlspecialchars($epc_profile_vat['display_mode'] === 'inclusive' ? 'incl. VAT' : 'excl. VAT', ENT_QUOTES, 'UTF-8') . '</strong>'
		. ($epc_profile_vat['price_label'] !== '' ? ' · ' . htmlspecialchars($epc_profile_vat['price_label'], ENT_QUOTES, 'UTF-8') : '')
		. '</small></td></tr>';
	if ($epc_trade_type === 'wholesale') {
		$epc_tax_cert = epc_trade_profile_get($db_link, $epc_profile_user_id, 'epc_tax_exempt_cert_path', '');
		$epc_tax_status = epc_trade_profile_get($db_link, $epc_profile_user_id, 'epc_tax_exempt_cert_status', '');
		echo '<tr><td><b>Tax-exempt certificate</b></td><td>';
		if ($epc_tax_cert !== '') {
			echo '<span class="label label-' . ($epc_tax_status === 'approved' ? 'success' : 'warning') . '">' . htmlspecialchars($epc_tax_status !== '' ? ucwords(str_replace('_', ' ', $epc_tax_status)) : 'Uploaded', ENT_QUOTES, 'UTF-8') . '</span>';
			echo ' <a href="' . htmlspecialchars($epc_tax_cert, ENT_QUOTES, 'UTF-8') . '" target="_blank" rel="noopener">View file</a><br>';
		} else {
			echo '<span class="text-muted">Upload your tax-exempt certificate for manager review.</span><br>';
		}
		echo '<form id="epc-tax-exempt-form" style="margin-top:8px;" enctype="multipart/form-data">';
		echo '<input type="hidden" name="csrf_guard_key" value="' . htmlspecialchars($user_session['csrf_guard_key'] ?? '', ENT_QUOTES, 'UTF-8') . '" />';
		echo '<input type="file" name="tax_exempt_cert" accept=".pdf,.jpg,.jpeg,.png,.webp" class="form-control" style="max-width:320px;display:inline-block;" /> ';
		echo '<button type="button" class="btn btn-sm btn-primary" onclick="epcUploadTaxExempt();">Upload</button>';
		echo '<div id="epc-tax-exempt-msg" class="small" style="margin-top:6px;"></div></form>';
		echo '</td></tr>';
	}
	//Регистрационный вариант
	$all_reg_variants_query = $db_link->prepare('SELECT COUNT(*) FROM `reg_variants`;');
	$all_reg_variants_query->execute();
	if( $all_reg_variants_query->fetchColumn() > 1)
	{
	    //Теперь запрос своего варианта
		$user_reg_variant_query = $db_link->prepare( 'SELECT * FROM `reg_variants` WHERE `id` = ?;' );
	    $user_reg_variant_query->execute( array($user_profile["reg_variant"]) );
	    $user_reg_variant_record = $user_reg_variant_query->fetch();
	    
	    echo "<tr> <td><b>".translate_str_by_id(4646)."</b></td> <td>".translate_str_by_id($user_reg_variant_record["caption"])."</td></tr>";
	}//в противном случае не выводим регистрационный вариант
	
	
	
	
	//Контакты email/phone
	?>
	<script>
	// ---------------------------------------------------------------------------------------------------
	//Настройка html в соответствии с контактом
	function set_contact_html(contact, contact_confirmed, type)
	{
		//Кнопки
		var button_confirm = '<div class="form-group"><a onclick="contacts_works_action_widgets(\''+type+'\', \'confirm\');" class="btn btn-ar btn-primary" href="javascript:void(0);"><i class="fa fa-check-square-o"></i> <?php echo translate_str_by_id(4521); ?></a></div>';
		var button_set = '<div class="form-group"><a onclick="contacts_works_action_widgets(\''+type+'\', \'set\');" class="btn btn-ar btn-primary" href="javascript:void(0);" ><i class="fa fa-pencil"></i> <?php echo translate_str_by_id(4733); ?></a></div>';
		var button_change = '<div class="form-group"><a onclick="contacts_works_action_widgets(\''+type+'\', \'change\', \''+contact+'\', '+contact_confirmed+');" class="btn btn-ar btn-primary" href="javascript:void(0);"><i class="fa fa-pencil"></i> <?php echo translate_str_by_id(4734); ?></a></div>';
		
		
		if( contact == '' )
		{
			//Контакт не указан
			document.getElementById(type+'_work').innerHTML = '<div class="form-inline"> <div class="form-group"><?php echo translate_str_by_id(3253); ?> </div> ' + button_set + '</div>';
		}
		else
		{
			//Контакт указан
			if( parseInt(contact_confirmed) == 1 )
			{
				//Подтвержден
				document.getElementById(type+'_work').innerHTML = '<div class="form-inline"> <div class="form-group">' + contact + ' <i class="fa fa-check-circle" style="color:#0A0;cursor:pointer;" title="<?php echo translate_str_by_id(3546); ?>"></i> </div> ' + button_change + '</div>';
			}
			else
			{
				//НЕ подтвержден
				document.getElementById(type+'_work').innerHTML = '<div class="form-inline"> <div class="form-group">' + contact + ' <i class="fa fa-exclamation-triangle" style="color:#F00;cursor:pointer;" title="<?php echo translate_str_by_id(3545); ?>"></i> </div> '+button_confirm+' '+button_change + '</div>';
			}
		}
	}
	// ---------------------------------------------------------------------------------------------------
	//Получение виджетов при нажатии кнопок Указать, Подтвердить, Сменить
	function contacts_works_action_widgets(type, action, contact = '', contact_confirmed = 0)
	{
		let mask = null;
    <?php if( (int) $DP_Config->show_phone_mask === 1 ) {
        
  	    switch($DP_Config->country_phone_mask){
  			case 'ru' :
  				echo 'mask = "+7 (999) 999-99-99";//Россия';
  			break;
  			case 'kz' :
  				echo 'mask = "+7 (999) 999-99-99";//Казахстан';
  			break;
  			case 'by' :
  				echo 'mask = "+375 (99) 999-99-99";//Белоруссия';
  			break;
  			case 'ua' :
  				echo 'mask = "+380 (99) 999-9999";//Украина';
  			break;
  		}
        
      } ?>
		if( action == 'set' )
		{

			document.getElementById( type+'_work' ).innerHTML = '<div class="form-inline"> <div class="form-group"> <input class="form-control" type="text" id="'+type+'_contact_input" /> </div> <div class="form-group"> <button onclick="contacts_works_execute(\''+type+'\', \'set\');" class="btn btn-ar btn-primary" style="margin-bottom:0!important;"><i class="fa fa-check"></i> <?php echo translate_str_by_id(2189); ?></button> </div> <div class="form-group"> <button onclick="set_contact_html(\'\', 0, \''+type+'\');" class="btn btn-ar btn-default"><?php echo translate_str_by_id(2190); ?></button> </div> </div>';
			if(mask && type == "phone") $("#"+type+"_contact_input").inputmask({"mask": mask});

			document.getElementById(type+'_contact_input').focus();
		}
		else if( action == 'confirm' )
		{
			contacts_works_execute(type, action);
		}
		else if( action == 'change' )
		{
			document.getElementById( type+'_work' ).innerHTML = '<div class="form-inline"> <div class="form-group"> <input class="form-control" type="text" id="'+type+'_contact_input" /> </div> <div class="form-group"> <button onclick="contacts_works_execute(\''+type+'\', \'change\');" class="btn btn-ar btn-primary"><i class="fa fa-check"></i> <?php echo translate_str_by_id(2189); ?></button> </div> <div class="form-group"> <button onclick="set_contact_html(\''+contact+'\', '+contact_confirmed+', \''+type+'\');" class="btn btn-ar"><?php echo translate_str_by_id(2190); ?></button> </div> </div>';
			if(mask && type == "phone") $("#"+type+"_contact_input").inputmask({"mask": mask});

			document.getElementById(type+'_contact_input').focus();
		}
	}
	// ---------------------------------------------------------------------------------------------------
	//Выполнение действий Указать, Подтвердить, Сменить
	function contacts_works_execute(type, action)
	{
		var contact = '';
		if( document.getElementById( type+'_contact_input' ) != undefined )
		{
			contact = document.getElementById( type+'_contact_input' ).value;
		}
		
		
		<?php
		//Защита от CSRF-атак
		$user_session = DP_User::getUserSession();
		?>
		
		
		jQuery.ajax({
			type: "POST",
			async: false, //Запрос синхронный
			url: "/content/users/ajax_contacts_works.php",
			dataType: "text",//Тип возвращаемого значения
			data: "type="+type+"&action="+action+"&contact="+contact+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer){
				
				//console.log(answer);
				
				var answer_ob = JSON.parse(answer);
				
				//В случае ошибки - с виджетами ничего делать не нужно. Просто показываем сообщение с ошибкой
				
				//Если некорректный парсинг ответа
				if( typeof answer_ob.status === "undefined" )
				{
					alert("<?php echo translate_str_by_id(2429); ?>");
				}
				else
				{
					if( answer_ob.status == true )
					{
						//УСПЕХ
						/*
						На данный момент для всех действий (Указать, Подтвердить, Сменить) - в случае успешного выполнения - отправляется код подтверждения
						*/
						//Для email
						if( answer_ob.type == 'email' )
						{
							//Сообщение
							if( answer_ob.action == 'set' || answer_ob.action == 'confirm' )
							{
								alert('<?php echo translate_str_by_id(4735); ?>');
							}
							else if( answer_ob.action == 'change' )
							{
								alert('<?php echo translate_str_by_id(4736); ?>');
							}
							
							//Переотображаем страницу (клиент пока увидит текущий статус контакта)
							location = '<?php echo $multilang_params['lang_href']; ?>/users/profile';
						}
						//Для телефона
						else
						{
							//Сообщение
							if( answer_ob.action == 'set' || answer_ob.action == 'confirm' )
							{
								alert('<?php echo translate_str_by_id(4737); ?>');
							}
							else if( answer_ob.action == 'change' )
							{
								alert('<?php echo translate_str_by_id(4738); ?>');
							}
							
							//Отображаем форму для кода
							document.getElementById('phone_work').innerHTML = document.getElementById('phone_code_store').innerHTML;
						}
					}
					else
					{
						alert(answer_ob.message);
					}
				}
				
			}
		});
	}
	// ---------------------------------------------------------------------------------------------------
	</script>
	<?php
	//Доступные способы связи
	$available_communications = DP_User::available_communications();//Получаем доступные способы связи
	//Телефон (если доступны все виды связи или только телефон)
	if( $available_communications["all"] || $available_communications["sms"] )
	{
		?>
        <tr> 
			<td><b><?php echo translate_str_by_id(1312); ?></b></td>
			<td id="phone_work"></td>
		</tr>
		<script>
		set_contact_html('<?php echo $user_profile['phone']; ?>', <?php echo (int)$user_profile['phone_confirmed']; ?>, 'phone');//Инициализация при загрузке страницы
		</script>
        <?php
	}
	//E-mail (1. Если доступны все виды связи. 2. Если доступны не все виды и при этом не доступен телефон (если включен SMS, но нет E-mail, то E-mail не показываем) )
	if( $available_communications["all"] ||  ( !$available_communications["all"] && !$available_communications["sms"] )  )
	{
		?>
        <tr> 
			<td><b>E-mail</b></td>
			<td id="email_work"></td>
		</tr>
		<script>
		set_contact_html('<?php echo $user_profile['email']; ?>', <?php echo (int)$user_profile['email_confirmed']; ?>, 'email');//Инициализация при загрузке страницы
		</script>
		<?php
	}
	

	
	//Перед выводом профиля получаем имена колонок таблицы users, чтобы отфильтровать их при выводе профиля
	$users_table_columns_query = $db_link->prepare("SELECT `COLUMN_NAME` FROM `INFORMATION_SCHEMA`.`COLUMNS` WHERE TABLE_NAME = 'users' AND `TABLE_SCHEMA` = '".$DP_Config->db."';");
	$users_table_columns_query->execute();
	$users_table_columns = array();
	while( $col_record =  $users_table_columns_query->fetch() )
	{
		$users_table_columns[] = $col_record['COLUMN_NAME'];
	}
   
	//Выводим поля профиля пользователя
    foreach($user_profile as $key => $value)
    {
		//Фильтруем все, что не относится к users_profiles и что не нужно показывать пользователю
        if( array_search($key, $users_table_columns ) !== false )
        {
            continue;
        }
        
        //Получаем название поля
        $parameter = "";
        if($key == "user_id")
        {
            $parameter = translate_str_by_id(3007);
        }
        else if($key == "groups")
        {
            $parameter = translate_str_by_id(3547);
            $groups_names = "";
            //Получаем названия групп
            for($i=0; $i < count($value); $i++)
            {
				$group_query = $db_link->prepare('SELECT * FROM `groups` WHERE `id` = ?;');
				$group_query->execute( array($value[$i]) );
                $group_record = $group_query->fetch();
                if($groups_names != "")
                {
                    $groups_names .= ";<br>";
                }
                $groups_names .= translate_str_by_id($group_record["value"]);
            }
            $value = $groups_names;//Для вывода
        }
        else
        {
            //Название из таблицы регистрационны полей
			$field_caption_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `name`=?;');
			$field_caption_query->execute( array($key) );
            $field_caption_record = $field_caption_query->fetch();
            $parameter = translate_str_by_id($field_caption_record["caption"]);
        }
        
        ?>
        <tr> <td><b><?php echo $parameter; ?></b></td> <td><?php echo $value?></td></tr>
        <?php
    }//foreach($user_profile AS $key => $value)
    ?>
    
    </table>
    
    <a class="btn btn-ar btn-primary" href="<?php echo $multilang_params['lang_href']; ?>/users/editform"><?php echo translate_str_by_id(4739); ?></a>

<?php
$epc_dealing_currency = (int)epc_trade_user_currency_iso($db_link, $epc_profile_user_id);
if ($epc_trade_status === 'approved' && $epc_dealing_currency > 0) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_currency.php';
	$epc_cur_rows = epc_trade_currency_options($db_link, $DP_Config);
	$epc_cur_label = isset($epc_cur_rows[$epc_dealing_currency]['caption_short']) ? $epc_cur_rows[$epc_dealing_currency]['caption_short'] : (string)$epc_dealing_currency;
	$epc_change_req = epc_trade_profile_get($db_link, $epc_profile_user_id, 'epc_currency_change_requested', '') === '1';
	$epc_req_iso = epc_trade_profile_get($db_link, $epc_profile_user_id, 'epc_currency_change_requested_iso', '');
	?>
	<div class="panel panel-default" style="margin-top:24px;">
		<div class="panel-heading"><strong>Dealing currency</strong></div>
		<div class="panel-body">
			<p>Your approved dealing currency is <strong><?php echo htmlspecialchars($epc_cur_label, ENT_QUOTES, 'UTF-8'); ?></strong>. Prices and checkout use this currency until a manager approves a change.</p>
			<?php if ($epc_change_req) { ?>
				<div class="alert alert-info">You have a pending currency change request<?php
					if ($epc_req_iso !== '' && isset($epc_cur_rows[$epc_req_iso]['caption_short'])) {
						echo ' to <strong>' . htmlspecialchars($epc_cur_rows[$epc_req_iso]['caption_short'], ENT_QUOTES, 'UTF-8') . '</strong>';
					}
				?>. A manager will review it.</div>
			<?php } else { ?>
				<form method="post" action="">
					<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>" />
					<input type="hidden" name="epc_request_currency_change" value="1">
					<div class="form-group">
						<label>Request a different currency</label>
						<select name="epc_requested_currency" class="form-control" style="max-width:280px;" required>
							<?php foreach ($epc_cur_rows as $iso => $crow) {
								if ((int)$iso === $epc_dealing_currency) { continue; }
								echo '<option value="' . (int)$iso . '">' . htmlspecialchars($crow['caption_short'] . ' (' . $crow['iso_name'] . ')', ENT_QUOTES, 'UTF-8') . '</option>';
							} ?>
						</select>
					</div>
					<div class="form-group">
						<label>Reason (optional)</label>
						<textarea name="epc_currency_change_note" class="form-control" rows="2" style="max-width:480px;" placeholder="Why do you need a different currency?"></textarea>
					</div>
					<button type="submit" class="btn btn-default">Submit currency change request</button>
				</form>
			<?php } ?>
		</div>
	</div>
	<?php
}
?>
<script>
function epcUploadTaxExempt() {
	var form = document.getElementById('epc-tax-exempt-form');
	if (!form) { return; }
	var fd = new FormData(form);
	var msg = document.getElementById('epc-tax-exempt-msg');
	if (msg) { msg.textContent = 'Uploading…'; }
	fetch('/content/users/ajax_epc_tax_exempt_upload.php', { method: 'POST', body: fd, credentials: 'same-origin' })
		.then(function(r) { return r.json(); })
		.then(function(data) {
			if (msg) {
				msg.textContent = data.message || (data.status ? 'Uploaded.' : 'Upload failed.');
				msg.className = 'small text-' + (data.status ? 'success' : 'danger');
			}
			if (data.status) { window.location.reload(); }
		})
		.catch(function() {
			if (msg) { msg.textContent = 'Upload failed.'; msg.className = 'small text-danger'; }
		});
}
</script>
    
<?php
}//else//Пользователь не авторизован
?>