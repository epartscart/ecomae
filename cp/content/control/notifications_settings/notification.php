<?php
//Страничный скрипт для настройки одного уведомления
defined('_ASTEXE_') or die('No access');


/*
Мультиязычность.
Для данной функции принцип редактирования проще, чем стандартный механизм с кастомными строками.
Здесь пользователь может редактировать исходные строки. При необходимости, он может откатывать их значения к default. При этом, id строк, записанные в таблицу не меняются никогда.

Это обусловлено тем, что уведомления являются предопределенными сразу в исходной версии и пользователь сам новые уведомления не может создавать. А доступность редактирования исходных строк вместо создания кастомных допустима, т.е. здесь есть функция отката к заводским настройкам.

Когда откатываем к заводским настройкам, то, из дефолтных строк берем значения и записываем их в поля на странице. В этот момент никакие действия со строками в БД не производятся. Затем, когда пользователь нажмет сохранить, то дефолтные значения запишутся в переводы строк, которые указаны в БД для соответствующего уведомления.
Т.е. в уведомлениях НИКОГДА не меняются id строк на кастомные или какие-другие. Переводы дефолтных строк никогда не меняются (такой функции нет). Переводы исходных строк могут редактироваться пользователем как угодно.
*/



//Если есть действия
if( isset($_POST['action']) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	//Делаем через транзакцию, т.е. могут потребоваться несколько запросов.
	try
	{
		//Старт транзакции
		if( ! $db_link->beginTransaction()  )
		{
			throw new Exception(translate_str_by_id(2132));
		}
		
		//Выполняем действия
		if( $_POST['action'] != 'save' )
		{
			throw new Exception("Incorrect parameter");
		}
		
		
		$notification_id = $_POST["notification_id"];
		
		
		//Получаем запись уведомления
		$notification_query = $db_link->prepare("SELECT * FROM `notifications_settings` WHERE `id` = ?;");
		$notification_query->execute( array($notification_id) );
		$notification = $notification_query->fetch();
		
		if( $notification == false)
		{
			throw new Exception(translate_str_by_id(2451));
		}
		
		
		/*
		Что может настраивать пользователь:
		- заголовок письма
		- текст письма
		- текст SMS

		- вкл/выкл E-mail
		- вкл/выкл SMS
		
		При этом данные настройки для E-mail и для Телефона можно делать только если у данного уведомления выставлен флаг foreseen
		*/
		
		
		//Для E-mail
		if( $notification['foreseen_email'] == 1 )
		{
			$email_on = (int)isset($_POST['email_on']);
			
			//Здесь нужно записать переводы в строки мультиязычности
			
			//Сохраняем заголовок письма
			if( $notification['email_subject'] != save_custom_translation($notification['email_subject'], $_POST['email_subject'], null, true) )
			{
				throw new Exception('Error saving Email subject');
			}
			//Сохраняем текст письма
			if( $notification['email_body'] != save_custom_translation($notification['email_body'], $_POST['email_body'], null, true) )
			{
				throw new Exception('Error saving Email body');
			}
		}
		else
		{
			$email_on = 0;
		}
		
		
		//Для телефона
		if( $notification['foreseen_sms'] == 1 )
		{
			$sms_on = (int)isset($_POST['sms_on']);
			
			//Здесь нужно записать переводы в строки мультиязычности
			
			//Сохраняем текст sms
			if( $notification['sms_body'] != save_custom_translation($notification['sms_body'], $_POST['sms_body'], null, true) )
			{
				throw new Exception('Error saving SMS body');
			}
		}
		else
		{
			$sms_on = 0;
		}
		
		
		//Здесь только остается записать настройки "Отправлять на Email" и "Отправлять по SMS"
		if( !$db_link->prepare("UPDATE `notifications_settings` SET `email_on` = ?, `sms_on` = ? WHERE `id` = ?;")->execute( array($email_on, $sms_on, $notification_id) ) )
		{
			throw new Exception(translate_str_by_id(2448));
		}
	}
	catch (Exception $e)
	{
		//Откатываем все изменения
		$db_link->rollBack();
		
		//Можно получить текст ошибки из throw: $e->getMessage()
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings/notification?notification_id=<?php echo $notification_id; ?>&error_message=<?php echo urlencode($e->getMessage()); ?>";
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
		location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings/notification?notification_id=<?php echo $notification_id; ?>&success_message=<?php echo urlencode($success_message); ?>";
	</script>
	<?php
	exit;
}
else//Действий нет - выводим страницу
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();

	$backend = trim((string) $DP_Config->backend_dir, '/');
	$baseCp = '/' . $backend;
	$listUrl = rtrim((string) $DP_Config->domain_path, '/') . '/' . $backend . '/control/notifications_settings';

	$epc_cn_h = static function ($v): string {
		return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
	};
	$epc_cn_t = static function ($id) use ($epc_cn_h): string {
		$v = function_exists('translate_str_by_id') ? translate_str_by_id($id) : $id;
		return $epc_cn_h($v === null || $v === false ? '' : $v);
	};

	$notification_query = $db_link->prepare('SELECT * FROM `notifications_settings` WHERE `id` = ?;');
	$notification_query->execute(array($_GET['notification_id'] ?? 0));
	$notification = $notification_query->fetch();
	if ($notification == false) {
		$warning_message = translate_str_by_id(2451);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path . $DP_Config->backend_dir; ?>/control/notifications_settings?warning_message=<?php echo urlencode($warning_message); ?>";
		</script>
		<?php
		exit;
	}

	$notification['caption'] = (string) (translate_str_by_id($notification['caption']) ?? '');
	$notification['description'] = (string) (translate_str_by_id($notification['description']) ?? '');
	$notification['event'] = (string) (translate_str_by_id($notification['event']) ?? '');
	$notification['email_subject'] = (string) (translate_str_by_id($notification['email_subject']) ?? '');
	$notification['email_body'] = (string) (translate_str_by_id($notification['email_body']) ?? '');
	$notification['sms_body'] = (string) (translate_str_by_id($notification['sms_body']) ?? '');
	$notification_vars = json_decode((string) $notification['vars'], true);
	if (!is_array($notification_vars)) {
		$notification_vars = array();
	}

	$email_body_js = json_encode($notification['email_body'], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	$default_email_subject_js = json_encode((string) (translate_str_by_id($notification['default_email_subject']) ?? ''), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	$default_email_body_js = json_encode((string) (translate_str_by_id($notification['default_email_body']) ?? ''), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	$default_sms_body_js = json_encode((string) (translate_str_by_id($notification['default_sms_body']) ?? ''), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

	require_once 'content/control/actions_alert.php';
	?>
	<div class="col-lg-12 epc-cn">
		<div class="epc-cn-hero">
			<h3><?php echo $epc_cn_h($notification['caption']); ?></h3>
			<p>Edit the e-mail and SMS template for this event. Use placeholders from the variables list. Restore factory text anytime, then Save.</p>
			<div class="epc-cn-hero__actions">
				<button type="button" class="btn btn-sm btn-primary" onclick="document.forms['save_notification_form'].submit();"><i class="fas fa-save"></i> <?php echo $epc_cn_t(2114); ?></button>
				<button type="button" class="btn btn-sm" onclick="set_default();"><i class="fas fa-undo"></i> <?php echo $epc_cn_t(2449); ?></button>
				<a class="btn btn-sm" href="<?php echo $epc_cn_h($listUrl); ?>"><i class="fas fa-envelope-open-text"></i> <?php echo $epc_cn_t(2450); ?></a>
				<a class="btn btn-sm" href="<?php echo $epc_cn_h($baseCp . '/control/communications'); ?>"><i class="fas fa-vial"></i> Test delivery</a>
				<a class="btn btn-sm" href="<?php echo $epc_cn_h($baseCp); ?>"><i class="fas fa-home"></i> <?php echo $epc_cn_t(2116); ?></a>
			</div>
		</div>

		<div class="epc-cn-guide">
			<h4>Editing guide</h4>
			<ol class="epc-cn-steps">
				<li><strong>Channels</strong>Enable e-mail and/or SMS only if this event supports them.</li>
				<li><strong>Placeholders</strong>Insert codes like <code>%order_id%</code> from the variables panel — they are replaced at send time.</li>
				<li><strong>Restore</strong>Loads factory subject/body into the form; click Save to persist.</li>
				<li><strong>Test</strong>After saving, verify SMTP/SMS from Communications.</li>
			</ol>
		</div>

		<form method="POST" name="save_notification_form">
			<input type="hidden" name="action" value="save" />
			<input type="hidden" name="notification_id" value="<?php echo $epc_cn_h((string) ($_GET['notification_id'] ?? '')); ?>" />
			<input type="hidden" name="csrf_guard_key" value="<?php echo $epc_cn_h((string) $user_session['csrf_guard_key']); ?>" />

			<div class="epc-cn-edit-grid">
				<div class="epc-cn-side">
					<div class="epc-cn-card">
						<div class="epc-cn-card__head">
							<div>
								<h4><?php echo $epc_cn_t(2452); ?></h4>
								<p>Event metadata</p>
							</div>
						</div>
						<div class="epc-cn-card__body">
							<dl class="epc-cn-kv">
								<dt>ID</dt><dd><?php echo (int) $notification['id']; ?></dd>
								<dt><?php echo $epc_cn_t(2453); ?></dt><dd><code><?php echo $epc_cn_h($notification['name']); ?></code></dd>
								<dt><?php echo $epc_cn_t(2277); ?></dt><dd><?php echo $epc_cn_h($notification['caption']); ?></dd>
								<dt><?php echo $epc_cn_t(2454); ?></dt><dd><?php echo $epc_cn_h($notification['event']); ?></dd>
								<dt><?php echo $epc_cn_t(2073); ?></dt><dd><?php echo $epc_cn_h($notification['description']); ?></dd>
								<dt><?php echo $epc_cn_t(2455); ?></dt>
								<dd><?php echo ((int) $notification['send_for_not_confirmed'] === 1) ? $epc_cn_t(2456) : $epc_cn_t(2457); ?></dd>
							</dl>
						</div>
					</div>

					<div class="epc-cn-card">
						<div class="epc-cn-card__head">
							<div>
								<h4><?php echo $epc_cn_t(2458); ?></h4>
								<p>Available placeholders</p>
							</div>
						</div>
						<div class="epc-cn-card__body" style="padding:0;">
							<table class="table epc-cn-vars">
								<thead>
									<tr>
										<th><?php echo $epc_cn_t(2459); ?></th>
										<th><?php echo $epc_cn_t(2460); ?></th>
									</tr>
								</thead>
								<tbody>
								<?php
								foreach ($notification_vars as $var) {
									$vc = (string) (translate_str_by_id($var['caption'] ?? '') ?? '');
									$vn = (string) ($var['name'] ?? '');
									?>
									<tr>
										<td><?php echo $epc_cn_h($vc); ?></td>
										<td><code>%<?php echo $epc_cn_h($vn); ?>%</code></td>
									</tr>
									<?php
								}
								if (!$notification_vars) {
									echo '<tr><td colspan="2" class="epc-cn-empty">No variables for this template.</td></tr>';
								}
								?>
								</tbody>
							</table>
						</div>
					</div>
				</div>

				<div>
					<div class="epc-cn-card is-email" style="margin-bottom:14px;">
						<div class="epc-cn-card__head">
							<div>
								<h4><i class="far fa-envelope"></i> <?php echo $epc_cn_t(2461); ?></h4>
								<p>Subject and HTML body</p>
							</div>
						</div>
						<div class="epc-cn-card__body">
							<?php if ((int) $notification['foreseen_email'] === 1) { ?>
								<div class="epc-cn-field">
									<label class="epc-cn-check">
										<input type="checkbox" name="email_on" id="email_on" <?php echo ((int) $notification['email_on'] === 1) ? 'checked="checked"' : ''; ?> />
										<?php echo $epc_cn_t(2462); ?>
									</label>
								</div>
								<div class="epc-cn-field">
									<label for="email_subject"><?php echo $epc_cn_t(2463); ?></label>
									<input class="form-control" type="text" name="email_subject" id="email_subject" value="<?php echo $epc_cn_h($notification['email_subject']); ?>" placeholder="<?php echo $epc_cn_t(2464); ?>" />
								</div>
								<div class="epc-cn-field">
									<label><?php echo $epc_cn_t(2465); ?></label>
									<div id="email_body_div"></div>
								</div>
							<?php } else { ?>
								<p class="epc-cn-meta"><?php echo $epc_cn_t(2466); ?></p>
							<?php } ?>
						</div>
					</div>

					<div class="epc-cn-card is-sms">
						<div class="epc-cn-card__head">
							<div>
								<h4><i class="fas fa-mobile-alt"></i> <?php echo $epc_cn_t(2467); ?></h4>
								<p>Short text for mobile</p>
							</div>
						</div>
						<div class="epc-cn-card__body">
							<?php if ((int) $notification['foreseen_sms'] === 1) { ?>
								<div class="epc-cn-field">
									<label class="epc-cn-check">
										<input type="checkbox" name="sms_on" id="sms_on" <?php echo ((int) $notification['sms_on'] === 1) ? 'checked="checked"' : ''; ?> />
										<?php echo $epc_cn_t(2468); ?>
									</label>
								</div>
								<div class="epc-cn-field">
									<label for="sms_body"><?php echo $epc_cn_t(2469); ?></label>
									<textarea class="form-control" name="sms_body" id="sms_body" rows="5" placeholder="<?php echo $epc_cn_t(2470); ?>"><?php echo $epc_cn_h($notification['sms_body']); ?></textarea>
								</div>
							<?php } else { ?>
								<p class="epc-cn-meta"><?php echo $epc_cn_t(2471); ?></p>
							<?php } ?>
						</div>
					</div>
				</div>
			</div>

			<div class="epc-cn-sticky">
				<button type="submit" class="btn btn-primary"><i class="fas fa-save"></i> <?php echo $epc_cn_t(2114); ?></button>
				<button type="button" class="btn btn-default" onclick="set_default();"><i class="fas fa-undo"></i> <?php echo $epc_cn_t(2449); ?></button>
				<a class="btn btn-default" href="<?php echo $epc_cn_h($listUrl); ?>"><?php echo $epc_cn_t(2450); ?></a>
				<span class="epc-cn-sticky__hint">Changes apply after Save · then test under Communications</span>
			</div>
		</form>
	</div>

	<script>
	window.EPC_COMMS_NOTIFY = { page: 'notification_edit' };
	<?php if ((int) $notification['foreseen_email'] === 1) { ?>
	function init_TinyMCE()
	{
		var email_body_div = document.getElementById('email_body_div');
		if (!email_body_div || typeof tinymce === 'undefined') { return; }
		email_body_div.innerHTML = '<textarea style="min-height:400px" class="tinymce_editor" id="email_body" name="email_body"></textarea>';
		tinymce.init({
			selector: 'textarea.tinymce_editor',
			toolbar: 'bold italic | fontselect | fontsizeselect | styleselect | forecolor | backcolor',
			plugins: ['code fullscreen textcolor']
		});
		document.getElementById('email_body').value = <?php echo $email_body_js; ?>;
	}
	init_TinyMCE();
	<?php } ?>

	function set_default()
	{
		<?php if ((int) $notification['foreseen_email'] === 1) { ?>
		document.getElementById('email_on').checked = true;
		document.getElementById('email_subject').value = <?php echo $default_email_subject_js; ?>;
		if (typeof tinymce !== 'undefined' && tinymce.get('email_body')) {
			tinymce.get('email_body').setContent(<?php echo $default_email_body_js; ?>);
		}
		<?php } ?>
		<?php if ((int) $notification['foreseen_sms'] === 1) { ?>
		document.getElementById('sms_on').checked = true;
		document.getElementById('sms_body').value = <?php echo $default_sms_body_js; ?>;
		<?php } ?>
		alert(<?php echo json_encode((string) translate_str_by_id(2472), JSON_UNESCAPED_UNICODE); ?>);
	}
	</script>
	<?php
}
?>
