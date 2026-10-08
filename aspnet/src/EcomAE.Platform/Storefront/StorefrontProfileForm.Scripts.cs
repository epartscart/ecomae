namespace EcomAE.Platform.Storefront;

public static partial class StorefrontProfileForm
{
    private const string ContactScript = """
	<script>
	// ---------------------------------------------------------------------------------------------------
	//Настройка html в соответствии с контактом
	function set_contact_html(contact, contact_confirmed, type)
	{
		//Кнопки
		var button_confirm = '<div class="form-group"><a onclick="contacts_works_action_widgets(\''+type+'\', \'confirm\');" class="btn btn-ar btn-primary" href="javascript:void(0);"><i class="fa fa-check-square-o"></i> {{T:4521}}</a></div>';
		var button_set = '<div class="form-group"><a onclick="contacts_works_action_widgets(\''+type+'\', \'set\');" class="btn btn-ar btn-primary" href="javascript:void(0);" ><i class="fa fa-pencil"></i> {{T:4733}}</a></div>';
		var button_change = '<div class="form-group"><a onclick="contacts_works_action_widgets(\''+type+'\', \'change\', \''+contact+'\', '+contact_confirmed+');" class="btn btn-ar btn-primary" href="javascript:void(0);"><i class="fa fa-pencil"></i> {{T:4734}}</a></div>';
		
		
		if( contact == '' )
		{
			//Контакт не указан
			document.getElementById(type+'_work').innerHTML = '<div class="form-inline"> <div class="form-group">{{T:3253}} </div> ' + button_set + '</div>';
		}
		else
		{
			//Контакт указан
			if( parseInt(contact_confirmed) == 1 )
			{
				//Подтвержден
				document.getElementById(type+'_work').innerHTML = '<div class="form-inline"> <div class="form-group">' + contact + ' <i class="fa fa-check-circle" style="color:#0A0;cursor:pointer;" title="{{T:3546}}"></i> </div> ' + button_change + '</div>';
			}
			else
			{
				//НЕ подтвержден
				document.getElementById(type+'_work').innerHTML = '<div class="form-inline"> <div class="form-group">' + contact + ' <i class="fa fa-exclamation-triangle" style="color:#F00;cursor:pointer;" title="{{T:3545}}"></i> </div> '+button_confirm+' '+button_change + '</div>';
			}
		}
	}
	// ---------------------------------------------------------------------------------------------------
	//Получение виджетов при нажатии кнопок Указать, Подтвердить, Сменить
	function contacts_works_action_widgets(type, action, contact = '', contact_confirmed = 0)
	{
		let mask = null;
    {{MASK}}		if( action == 'set' )
		{

			document.getElementById( type+'_work' ).innerHTML = '<div class="form-inline"> <div class="form-group"> <input class="form-control" type="text" id="'+type+'_contact_input" /> </div> <div class="form-group"> <button onclick="contacts_works_execute(\''+type+'\', \'set\');" class="btn btn-ar btn-primary" style="margin-bottom:0!important;"><i class="fa fa-check"></i> {{T:2189}}</button> </div> <div class="form-group"> <button onclick="set_contact_html(\'\', 0, \''+type+'\');" class="btn btn-ar btn-default">{{T:2190}}</button> </div> </div>';
			if(mask && type == "phone") $("#"+type+"_contact_input").inputmask({"mask": mask});

			document.getElementById(type+'_contact_input').focus();
		}
		else if( action == 'confirm' )
		{
			contacts_works_execute(type, action);
		}
		else if( action == 'change' )
		{
			document.getElementById( type+'_work' ).innerHTML = '<div class="form-inline"> <div class="form-group"> <input class="form-control" type="text" id="'+type+'_contact_input" /> </div> <div class="form-group"> <button onclick="contacts_works_execute(\''+type+'\', \'change\');" class="btn btn-ar btn-primary"><i class="fa fa-check"></i> {{T:2189}}</button> </div> <div class="form-group"> <button onclick="set_contact_html(\''+contact+'\', '+contact_confirmed+', \''+type+'\');" class="btn btn-ar">{{T:2190}}</button> </div> </div>';
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
		
		
				
		
		jQuery.ajax({
			type: "POST",
			async: false, //Запрос синхронный
			url: "/content/users/ajax_contacts_works.php",
			dataType: "text",//Тип возвращаемого значения
			data: "type="+type+"&action="+action+"&contact="+contact+"&csrf_guard_key={{CSRF}}",
			success: function(answer){
				
				//console.log(answer);
				
				var answer_ob = JSON.parse(answer);
				
				//В случае ошибки - с виджетами ничего делать не нужно. Просто показываем сообщение с ошибкой
				
				//Если некорректный парсинг ответа
				if( typeof answer_ob.status === "undefined" )
				{
					alert("{{T:2429}}");
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
								alert('{{T:4735}}');
							}
							else if( answer_ob.action == 'change' )
							{
								alert('{{T:4736}}');
							}
							
							//Переотображаем страницу (клиент пока увидит текущий статус контакта)
							location = '{{LANG}}/users/profile';
						}
						//Для телефона
						else
						{
							//Сообщение
							if( answer_ob.action == 'set' || answer_ob.action == 'confirm' )
							{
								alert('{{T:4737}}');
							}
							else if( answer_ob.action == 'change' )
							{
								alert('{{T:4738}}');
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
""";

    private const string TaxExemptScript = """
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
""";
}
