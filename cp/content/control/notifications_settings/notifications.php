<?php
//Страничный скрипт отображения таблицы notifications_settings (менеджер уведомлений)
defined('_ASTEXE_') or die('No access');


//Если есть действия
if( isset($_POST['action']) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	//Действие - восстановление настроек по умолчанию
	/*
	Мультиязычность:
	- текст письма, заголовок письма, текст sms: переводы на все языки восстанавливаем из значений по умолчанию
	- оставшиеся настройки делаем в таблице notifications_settings (email_on, sms_on)
	*/
	if( $_POST['action'] == 'set_default' )
	{
		//Массив с ID уведомлений
		$notifications_ids = json_decode($_POST['notifications_ids'], true);
		
		//Делаем через транзакцию
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception( translate_str_by_id(2132) );
			}
			
			//Выполняем действия
			if( !is_array($notifications_ids) )
			{
				throw new Exception( 'Incorrect parameter' );
			}
			
			
			//Получаем список языков сайта
			$langs_query = $db_link->prepare("SELECT * FROM `lang_languages`");
			$langs_query->execute();
			$langs = $langs_query->fetchAll();
			
			
			//По каждому уведомлению
			//Готовим запрос - общий для всех
			$restore_default_str_query = $db_link->prepare("UPDATE `lang_text_strings_translation` SET `value` = (SELECT `value` FROM (SELECT * FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `str_id` = ?) AS `sub_table` ) WHERE `lang_code` = ? AND `str_id` = ?;");
			for( $i = 0 ; $i < count($notifications_ids) ; $i++ )
			{
				//Получаем объект уведомления
				$notification_query = $db_link->prepare("SELECT * FROM `notifications_settings` WHERE `id` = ?;");
				$notification_query->execute( array($notifications_ids[$i]) );
				$notification = $notification_query->fetch();
				
				if( !$notification )
				{
					throw new Exception( 'Notification not found' );
				}
				
				
				//Для тех уведомлений, которые еще не перенесены в мультиязычность - ничего не делаем
				if( 
				!is_numeric($notification['default_email_subject']) || 
				!is_numeric($notification['default_email_body']) ||
				!is_numeric($notification['default_sms_body']) ||
				!is_numeric($notification['email_subject']) || 
				!is_numeric($notification['email_body']) ||
				!is_numeric($notification['sms_body'])
					)
				{
					continue;
				}
				
				//Здесь знаем id всех строк данного уведомления
				
				
				//Для каждого языка
				for( $lg=0 ; $lg < count($langs) ; $lg++ )
				{
					//Если предусмотрено письмо
					if( $notification['foreseen_email'] == 1 )
					{
						//Откатываем заголовок письма
						if( ! $restore_default_str_query->execute( array( $langs[$lg]['lang_code'], $notification['default_email_subject'], $langs[$lg]['lang_code'], $notification['email_subject'] ) ) )
						{
							throw new Exception( 'Error restoring email_subject' );
						}
						
						//Откатываем текст письма
						if( ! $restore_default_str_query->execute( array( $langs[$lg]['lang_code'], $notification['default_email_body'], $langs[$lg]['lang_code'], $notification['email_body'] ) ) )
						{
							throw new Exception( 'Error restoring email_body' );
						}
					}
					
					//Если предусмотрено sms
					if( $notification['foreseen_sms'] == 1 )
					{
						//Откатываем текст sms
						if( ! $restore_default_str_query->execute( array( $langs[$lg]['lang_code'], $notification['default_sms_body'], $langs[$lg]['lang_code'], $notification['sms_body'] ) ) )
						{
							throw new Exception( 'Error restoring sms_body' );
						}
					}
				}//~for по каждому языку
				
				
				
				//Остается только восстановить настройки email_on, sms_on
				if( ! $db_link->prepare( "UPDATE `notifications_settings` SET `email_on` = `foreseen_email`, `sms_on` = `foreseen_sms` WHERE `id` = ?;" )->execute( array($notifications_ids[$i]) ) )
				{
					throw new Exception( 'Error restoring email_on and sms_on' );
				}
			}//~for по каждому уведомлению
		}
		catch (Exception $e)
		{
			//Откатываем все изменения
			$db_link->rollBack();
			
			
			//Можно получить текст ошибки из throw: $e->getMessage()
			
			//Переадресация с сообщением о результатах выполнения
			$error_message = $e->getMessage();
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?error_message=<?php echo urlencode($error_message); ?>";
			</script>
			<?php
			exit;
		}

		//Дошли до сюда, значит выполнено ОК
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		
		
		//Переадресация с сообщением о результатах выполнения
		$success_message = translate_str_by_id(2157);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?success_message=<?php echo urlencode($success_message); ?>";
		</script>
		<?php
		exit;
	}
	//Отправлять на email и Отправлять на Телефон
	else if( $_POST['action'] == 'set_send' )
	{
		$type = $_POST['type'];//E-mail или Телефон
		$notification_id = $_POST['notification_id'];
		$set_send = $_POST['set_send'];//Вкл или выкл
		
		
		//$type используется в SQL-запросах. Проверяем значение
		if( $type != 'email' && $type != 'sms' )
		{
			exit;
		}
		
		
		
		//Отключать отправку можно в любом случае.
		//Включать можно только, если предусмотрен соответствующий способ отправки для данного уведомления
		if( $set_send == 1 )
		{
			//Проверяем, предусмотрен ли данный способ отправки по этому уведомлению
			$foreseen_query = $db_link->prepare("SELECT * FROM `notifications_settings` WHERE `id` = ?;");
			$foreseen_query->execute( array($notification_id) );
			$foreseen_record = $foreseen_query->fetch();
			
			if( $foreseen_record['foreseen_'.$type] == 0 )
			{
				//Переадресация с сообщением о результатах выполнения
				$warning_message = translate_str_by_id(2474);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?warning_message=<?php echo urlencode($warning_message); ?>";
				</script>
				<?php
				exit;
			}
		}
		
		
		
		//Включаем/отключаем
		if( !$db_link->prepare("UPDATE `notifications_settings` SET `".$type."_on` = ? WHERE `id` = ?;")->execute( array($set_send, $notification_id) ) )
		{
			//Переадресация с сообщением о результатах выполнения
			$error_message = translate_str_by_id(2473);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?error_message=<?php echo urlencode($error_message); ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			//Переадресация с сообщением о результатах выполнения
			$success_message = translate_str_by_id(2157);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?success_message=<?php echo urlencode($success_message); ?>";
			</script>
			<?php
			exit;
		}
	}
}
else//Действий нет - выводим страницу
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();

	$backend = trim((string) $DP_Config->backend_dir, '/');
	$baseCp = '/' . $backend;
	$editBase = rtrim((string) $DP_Config->domain_path, '/') . '/' . $backend . '/control/notifications_settings/notification?notification_id=';

	$epc_cn_h = static function ($v): string {
		return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
	};
	$epc_cn_t = static function ($id) use ($epc_cn_h): string {
		$v = function_exists('translate_str_by_id') ? translate_str_by_id($id) : $id;
		return $epc_cn_h($v === null || $v === false ? '' : $v);
	};

	$rows = array();
	$elements_query = $db_link->prepare('SELECT * FROM `notifications_settings` ORDER BY `id` ASC;');
	$elements_query->execute();
	while ($element_record = $elements_query->fetch(PDO::FETCH_ASSOC)) {
		$caption = (string) (translate_str_by_id($element_record['caption']) ?? '');
		$event = (string) (translate_str_by_id($element_record['event']) ?? '');
		$description = (string) (translate_str_by_id($element_record['description']) ?? '');
		$rows[] = array(
			'id' => (int) $element_record['id'],
			'name' => (string) $element_record['name'],
			'caption' => $caption,
			'event' => $event,
			'description' => $description,
			'email_on' => (int) $element_record['email_on'],
			'sms_on' => (int) $element_record['sms_on'],
			'foreseen_email' => (int) $element_record['foreseen_email'],
			'foreseen_sms' => (int) $element_record['foreseen_sms'],
		);
	}

	$for_js_ids = array();
	foreach ($rows as $r) {
		$for_js_ids[] = $r['id'];
	}

	require_once 'content/control/actions_alert.php';
	?>
	<div class="col-lg-12 epc-cn" id="epc-cn-notify-root" data-filter="all">
		<div class="epc-cn-hero">
			<h3>Notification settings</h3>
			<p>Turn e-mail and SMS on or off for each shop event, edit message templates, and restore factory text when needed. Delivery itself is configured under Communications, SMTP, and SMS Operators.</p>
			<div class="epc-cn-hero__actions">
				<button type="button" class="btn btn-sm btn-primary" onclick="set_default_checked();"><i class="fas fa-undo"></i> <?php echo $epc_cn_t(2449); ?></button>
				<a class="btn btn-sm" href="<?php echo $epc_cn_h($baseCp . '/control/communications'); ?>"><i class="fas fa-broadcast-tower"></i> Communications</a>
				<a class="btn btn-sm" href="<?php echo $epc_cn_h($baseCp . '/control/sms-operatory'); ?>"><i class="fas fa-mobile-alt"></i> SMS operators</a>
				<a class="btn btn-sm" href="<?php echo $epc_cn_h($baseCp); ?>"><i class="fas fa-home"></i> <?php echo $epc_cn_t(2116); ?></a>
			</div>
		</div>

		<div class="epc-cn-quick">
			<a class="is-email" href="<?php echo $epc_cn_h($baseCp . '/control/config?need_config_group=3'); ?>">
				<span class="epc-cn-quick__icon"><i class="far fa-envelope"></i></span>
				<strong>SMTP first</strong>
				<span>Templates only send when e-mail/SMTP is configured.</span>
			</a>
			<a class="is-sms" href="<?php echo $epc_cn_h($baseCp . '/control/sms-operatory'); ?>">
				<span class="epc-cn-quick__icon"><i class="fas fa-mobile-alt"></i></span>
				<strong>SMS operator</strong>
				<span>Activate one operator and set the sender number.</span>
			</a>
			<a class="is-templates" href="<?php echo $epc_cn_h($baseCp . '/control/communications'); ?>">
				<span class="epc-cn-quick__icon"><i class="fas fa-vial"></i></span>
				<strong>Test delivery</strong>
				<span>Send a test e-mail or SMS from Communications.</span>
			</a>
			<a href="<?php echo $epc_cn_h($baseCp . '/control/cp-guideline'); ?>">
				<span class="epc-cn-quick__icon"><i class="fas fa-book"></i></span>
				<strong>CP guideline</strong>
				<span>Where notifications fit in the order workflow.</span>
			</a>
		</div>

		<div class="epc-cn-guide">
			<h4>How to use this page</h4>
			<ol class="epc-cn-steps">
				<li><strong>Scan channels</strong>Green = on, red = off. Click the icon to toggle e-mail or SMS for that event.</li>
				<li><strong>Edit a template</strong>Open the pencil to change subject, HTML body, or SMS text and placeholders.</li>
				<li><strong>Restore defaults</strong>Tick rows (or one row’s undo) to reset text and channel flags to factory values.</li>
				<li><strong>Verify send</strong>After changes, test from Communications so SMTP / SMS credentials are confirmed.</li>
			</ol>
		</div>

		<form name="set_default_form" method="POST">
			<input type="hidden" name="action" value="set_default" />
			<input type="hidden" name="notifications_ids" id="notifications_ids" value="" />
			<input type="hidden" name="csrf_guard_key" value="<?php echo $epc_cn_h((string) $user_session['csrf_guard_key']); ?>" />
		</form>

		<div class="epc-cn-filter">
			<input type="search" id="epc-cn-notify-q" placeholder="Search caption, code, event…" autocomplete="off" />
			<button type="button" class="epc-cn-chip is-active" data-filter-chip="all">All</button>
			<button type="button" class="epc-cn-chip" data-filter-chip="email">E-mail on</button>
			<button type="button" class="epc-cn-chip" data-filter-chip="sms">SMS on</button>
			<button type="button" class="epc-cn-chip" data-filter-chip="off">Both off</button>
			<span class="epc-cn-count" id="epc-cn-notify-count"><?php echo count($rows); ?> / <?php echo count($rows); ?></span>
		</div>

		<div class="epc-cn-table-wrap table-responsive">
			<table class="table epc-cn-table" data-sort="false">
				<thead>
					<tr>
						<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
						<th>ID</th>
						<th><?php echo $epc_cn_t(2277); ?></th>
						<th><?php echo $epc_cn_t(2453); ?></th>
						<th><?php echo $epc_cn_t(2479); ?></th>
						<th><?php echo $epc_cn_t(2073); ?></th>
						<th class="text-center"><?php echo $epc_cn_t(2480); ?></th>
						<th class="text-center"><?php echo $epc_cn_t(2481); ?></th>
						<th class="text-center"><?php echo $epc_cn_t(2113); ?></th>
					</tr>
				</thead>
				<tbody>
				<?php
				foreach ($rows as $element_record) {
					$id = $element_record['id'];
					$href = $editBase . $id;
					$search = strtolower($element_record['caption'] . ' ' . $element_record['name'] . ' ' . $element_record['event'] . ' ' . $element_record['description'] . ' ' . $id);
					?>
					<tr data-notify-row
						data-search="<?php echo $epc_cn_h($search); ?>"
						data-email-on="<?php echo (int) $element_record['email_on']; ?>"
						data-sms-on="<?php echo (int) $element_record['sms_on']; ?>">
						<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $id; ?>');" id="checked_<?php echo $id; ?>" name="checked_<?php echo $id; ?>"/></td>
						<td><a href="<?php echo $epc_cn_h($href); ?>"><?php echo $id; ?></a></td>
						<td><a href="<?php echo $epc_cn_h($href); ?>"><?php echo $epc_cn_h($element_record['caption']); ?></a></td>
						<td><a href="<?php echo $epc_cn_h($href); ?>"><span class="epc-cn-muted"><?php echo $epc_cn_h($element_record['name']); ?></span></a></td>
						<td><a href="<?php echo $epc_cn_h($href); ?>"><?php echo $epc_cn_h($element_record['event']); ?></a></td>
						<td><a href="<?php echo $epc_cn_h($href); ?>"><span class="epc-cn-muted"><?php echo $epc_cn_h($element_record['description']); ?></span></a></td>
						<td class="text-center">
							<form method="POST" name="set_send_email_<?php echo $id; ?>">
								<input type="hidden" name="csrf_guard_key" value="<?php echo $epc_cn_h((string) $user_session['csrf_guard_key']); ?>" />
								<input type="hidden" name="action" value="set_send" />
								<input type="hidden" name="type" value="email" />
								<input type="hidden" name="notification_id" value="<?php echo $id; ?>" />
								<?php if ((int) $element_record['foreseen_email'] !== 1) { ?>
									<span class="epc-cn-toggle is-na" title="E-mail not available for this event"><i class="far fa-circle"></i></span>
								<?php } elseif ((int) $element_record['email_on'] === 1) { ?>
									<input type="hidden" name="set_send" value="0" />
									<button type="button" class="epc-cn-toggle is-on" title="<?php echo $epc_cn_t(2482); ?>" onclick="forms['set_send_email_<?php echo $id; ?>'].submit();"><i class="fas fa-check-circle"></i></button>
								<?php } else { ?>
									<input type="hidden" name="set_send" value="1" />
									<button type="button" class="epc-cn-toggle is-off" title="<?php echo $epc_cn_t(2483); ?>" onclick="forms['set_send_email_<?php echo $id; ?>'].submit();"><i class="fas fa-minus-circle"></i></button>
								<?php } ?>
							</form>
						</td>
						<td class="text-center">
							<form method="POST" name="set_send_sms_<?php echo $id; ?>">
								<input type="hidden" name="csrf_guard_key" value="<?php echo $epc_cn_h((string) $user_session['csrf_guard_key']); ?>" />
								<input type="hidden" name="action" value="set_send" />
								<input type="hidden" name="type" value="sms" />
								<input type="hidden" name="notification_id" value="<?php echo $id; ?>" />
								<?php if ((int) $element_record['foreseen_sms'] !== 1) { ?>
									<span class="epc-cn-toggle is-na" title="SMS not available for this event"><i class="far fa-circle"></i></span>
								<?php } elseif ((int) $element_record['sms_on'] === 1) { ?>
									<input type="hidden" name="set_send" value="0" />
									<button type="button" class="epc-cn-toggle is-on" title="<?php echo $epc_cn_t(2484); ?>" onclick="forms['set_send_sms_<?php echo $id; ?>'].submit();"><i class="fas fa-check-circle"></i></button>
								<?php } else { ?>
									<input type="hidden" name="set_send" value="1" />
									<button type="button" class="epc-cn-toggle is-off" title="<?php echo $epc_cn_t(2485); ?>" onclick="forms['set_send_sms_<?php echo $id; ?>'].submit();"><i class="fas fa-minus-circle"></i></button>
								<?php } ?>
							</form>
						</td>
						<td class="text-center">
							<span class="epc-cn-actions">
								<button type="button" class="is-reset" title="<?php echo $epc_cn_t(2449); ?>" onclick="set_default_one(<?php echo $id; ?>);"><i class="fas fa-undo"></i></button>
								<a class="is-edit" href="<?php echo $epc_cn_h($href); ?>" title="<?php echo $epc_cn_t(2486); ?>"><i class="fas fa-edit"></i></a>
							</span>
						</td>
					</tr>
					<?php
				}
				if (!$rows) {
					echo '<tr><td colspan="9" class="epc-cn-empty">No notification templates found.</td></tr>';
				}
				?>
				</tbody>
			</table>
		</div>
	</div>

	<script>
	window.EPC_COMMS_NOTIFY = { page: 'notifications' };
	var elements_id_array = <?php echo json_encode($for_js_ids); ?>;
	var elements_array = elements_id_array.map(function (id) { return 'checked_' + id; });

	function set_default_checked()
	{
		var notifications_ids = getCheckedElements();
		if (notifications_ids.length === 0) {
			alert(<?php echo json_encode((string) translate_str_by_id(2475), JSON_UNESCAPED_UNICODE); ?>);
			return;
		}
		if (!confirm(<?php echo json_encode((string) translate_str_by_id(2476), JSON_UNESCAPED_UNICODE); ?>)) {
			return;
		}
		document.getElementById('notifications_ids').value = JSON.stringify(notifications_ids);
		document.forms['set_default_form'].submit();
	}
	function set_default_one(notification_id)
	{
		if (!confirm(<?php echo json_encode((string) translate_str_by_id(2477), JSON_UNESCAPED_UNICODE); ?>)) {
			return;
		}
		document.getElementById('notifications_ids').value = '[' + notification_id + ']';
		document.forms['set_default_form'].submit();
	}
	function on_check_uncheck_all()
	{
		var state = document.getElementById('check_uncheck_all').checked;
		for (var i = 0; i < elements_array.length; i++) {
			var el = document.getElementById(elements_array[i]);
			if (el) { el.checked = state; }
		}
	}
	function on_one_check_changed()
	{
		for (var i = 0; i < elements_array.length; i++) {
			var el = document.getElementById(elements_array[i]);
			if (el && el.checked === false) {
				document.getElementById('check_uncheck_all').checked = false;
				break;
			}
		}
	}
	function getCheckedElements()
	{
		var checked_ids = [];
		for (var i = 0; i < elements_array.length; i++) {
			var el = document.getElementById(elements_array[i]);
			if (el && el.checked === true) {
				checked_ids.push(elements_id_array[i]);
			}
		}
		return checked_ids;
	}
	</script>
	<?php
}
?>