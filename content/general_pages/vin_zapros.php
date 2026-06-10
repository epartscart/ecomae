<?php

/*
Страничный скрипт для VIN-запросов
*/

defined('_ASTEXE_') or die('No access');

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

//Для отправки уведомлений
require_once( $_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php" );

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_id = DP_User::getUserId();//ID пользователя
$userProfile = DP_User::getUserProfile();//Профиль пользователя
$user_session = DP_User::getUserSession();

require_once($_SERVER["DOCUMENT_ROOT"]."/content/general/actions_alert.php");//Вывод сообщений о результатах выполнения действий
	

// Выборка из БД
$req_fields = $db_link->prepare("SELECT * FROM `vin_fields` WHERE `show` = '1' AND `required` = '1' ORDER BY `order`;");
$req_fields->execute();

$dop_fields = $db_link->prepare("SELECT * FROM `vin_fields` WHERE `show` = '1' AND `required` = '0' ORDER BY `order`;");
$dop_fields->execute();

$dop_fields_show = $dop_fields->fetchAll(PDO::FETCH_ASSOC);





//Значения полей формы поумолчанию
$field_default = array("client_fio"=>"","client_email"=>"","client_phone"=>"","client_vin"=>"","client_mark"=>"","client_model"=>"","client_year"=>"","client_engine"=>"","client_body"=>"","client_kpp"=>"","client_city"=>"","client_drive"=>"");
$transmission = array("akpp"=>translate_str_by_id(4052), "mkpp"=>translate_str_by_id(4053), "robot"=>translate_str_by_id(4054));

if($user_id > 0)
{
	$fio = "";
	if( isset($userProfile['surname']) ){
		if($fio != '')
		{
			$fio .= ' ';
		}
		$fio .= $userProfile['surname'];
	}
	if( isset($userProfile['name']) ){
		if($fio != '')
		{
			$fio .= ' ';
		}
		$fio .= $userProfile['name'];
	}
	$field_default['client_fio'] = $fio;
	
	if( isset($userProfile['email']) ){
		$field_default['client_email'] = $userProfile['email'];
	}
	if( isset($userProfile['phone']) ){
		$field_default['client_phone'] = $userProfile['phone'];
	}
	
	
	
	
	if( isset($_COOKIE["seller_request"]) )
	{
		$cars_query = $db_link->prepare('SELECT * FROM `shop_docpart_garage` WHERE `id` = ? AND `user_id` = ?;');
		$cars_query->execute( array($_COOKIE["seller_request"], $user_id) );
		$car_record = $cars_query->fetch();
	}
	else
	{
		$cars_query = $db_link->prepare('SELECT * FROM `shop_docpart_garage` WHERE `user_id` = ? AND `active` = 1;');
		$cars_query->execute( array($user_id) );
		$car_record = $cars_query->fetch();
	}
	
	if( !empty($car_record) )
	{
		if(!empty($car_record["vin"]))
		{
			$field_default['client_vin'] = $car_record["vin"];
		}
		if(!empty($car_record["frame"]))
		{
			$field_default['client_vin'] = $car_record["frame"];
		}
		if(!empty($car_record["marka"]))
		{
			$field_default['client_mark'] = $car_record["marka"];
		}
		if(!empty($car_record["model"]))
		{
			$field_default['client_model'] = $car_record["model"];
		}
		if(!empty($car_record["year"]))
		{
			$field_default['client_year'] = $car_record["year"];
		}
		if(!empty($car_record["engine_value"]))
		{
			$field_default['client_engine'] = $car_record["engine_value"];
		}
		if(!empty($car_record["body_type"]))
		{
			$field_default['client_body'] = $car_record["body_type"];
		}
		if(!empty($transmission[$car_record["transmission"]]))
		{
			$field_default['client_kpp'] = $transmission[$car_record["transmission"]];
		}
	}
}
?>



<link href="/content/general_pages/vin_zapros/vin_zapros.css" rel="stylesheet" type="text/css"/>


<div class="section-form">
    <form class="request-seller" method="POST" id="requestSeller" enctype="multipart/form-data">
   			<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
        <div class="all-input <?= (!empty($dop_fields_show)) ? ' ' : 'only-main';?>">
            <div class="main-input">
                <h2><?php echo translate_str_by_id(5590); ?></h2>

								<?php
									while( $field = $req_fields->fetch() ) {

                    if ($field['name'] == 'client_vin' ) {
						?>
                        <div class="input-shell">
                            <label for="<?=$field['name']?>"><?php echo translate_str_by_id(5212); ?><span>*</span>
                                <div class="question" data-hystmodal="#vinModal">
                                    <svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 15 15" fill="none">
                                        <path d="M7.5 13.75C4.04813 13.75 1.25 10.9519 1.25 7.5C1.25 4.04813 4.04813 1.25 7.5 1.25C10.9519 1.25 13.75 4.04813 13.75 7.5C13.75 10.9519 10.9519 13.75 7.5 13.75ZM7.5 12.5C8.82608 12.5 10.0979 11.9732 11.0355 11.0355C11.9732 10.0979 12.5 8.82608 12.5 7.5C12.5 6.17392 11.9732 4.90215 11.0355 3.96447C10.0979 3.02678 8.82608 2.5 7.5 2.5C6.17392 2.5 4.90215 3.02678 3.96447 3.96447C3.02678 4.90215 2.5 6.17392 2.5 7.5C2.5 8.82608 3.02678 10.0979 3.96447 11.0355C4.90215 11.9732 6.17392 12.5 7.5 12.5ZM6.875 9.375H8.125V10.625H6.875V9.375ZM8.125 8.34687V8.75H6.875V7.8125C6.875 7.64674 6.94085 7.48777 7.05806 7.37056C7.17527 7.25335 7.33424 7.1875 7.5 7.1875C7.67755 7.18749 7.85144 7.13706 8.00145 7.04208C8.15146 6.9471 8.27142 6.81148 8.34736 6.65099C8.4233 6.4905 8.45211 6.31175 8.43043 6.13553C8.40875 5.95931 8.33747 5.79287 8.2249 5.65557C8.11232 5.51827 7.96307 5.41577 7.79451 5.35998C7.62596 5.30419 7.44502 5.29742 7.27277 5.34044C7.10051 5.38346 6.94401 5.47452 6.82148 5.60301C6.69895 5.7315 6.61542 5.89215 6.58063 6.06625L5.35438 5.82062C5.4304 5.44068 5.60594 5.08773 5.86308 4.79787C6.12021 4.508 6.4497 4.29161 6.81787 4.17083C7.18604 4.05004 7.57968 4.02918 7.95855 4.11038C8.33742 4.19159 8.68794 4.37195 8.97426 4.63302C9.26059 4.89408 9.47245 5.2265 9.58819 5.59629C9.70394 5.96608 9.71942 6.35997 9.63304 6.73769C9.54666 7.11542 9.36153 7.46344 9.09658 7.74616C8.83162 8.02889 8.49633 8.23619 8.125 8.34687Z" fill="#ED3C38"/>
                                    </svg>
                                </div>
                            </label>
                            <input type="text" name="<?=$field['name']?>" id="<?=$field['name']?>" placeholder="<?=translate_str_by_id($field['example'])?>" value="<?=($field_default[$field['name']])?$field_default[$field['name']]:'';?>" required>
                        </div>
                    <?php
					} else {
						?>
                        <div class="input-shell">
                        <label for="<?=$field['name']?>"><?=translate_str_by_id($field['caption'])?><span>*</span></label>
                        <input type="text" name="<?=$field['name']?>" id="<?=$field['name']?>" placeholder="<?=translate_str_by_id($field['example'])?>" value="<?=($field_default[$field['name']])?$field_default[$field['name']]:'';?>" required>
                    </div> 
                    <?php
					}
                    }
                ?>

                <div class="input-shell">
                    <label for="client_parts"><?php echo translate_str_by_id(4043); ?></label>
                    <textarea style="display: inherit;" name="client_parts" id="client_parts" placeholder="<?php echo translate_str_by_id(4056); ?>"></textarea>
                </div>
                <div class="input-shell" id="captcha">
                    <label for=""><?php echo translate_str_by_id(4067); ?><span>*</span></label>
                    <input type="text" name="capcha_input" id="capcha_input" autocomplete="off" placeholder="<?php echo translate_str_by_id(4067); ?>" required>
                    <div class="captha-img">
                        <img src="/lib/captcha/captcha.php" id="capcha-image">
                        <a href="javascript:void(0);" onclick="document.getElementById('capcha-image').src='/lib/captcha/captcha.php?rid=' + Math.random();">
                            <img src="/lib/captcha/refresh.png" border="0"/>
                        </a>
                    </div>
                </div>
                <div class="input-shell input-file-row">
                    <label class="input-file" for="client_img"><?php echo translate_str_by_id(5591); ?><br>
                    <div class="input-file-text"></div>
                        <input type="file" name="client_img[]" multiple accept="image/*" id="client_img">
                        <span><?php echo translate_str_by_id(3181); ?></span>
                        <div class="input-text-img">
                          <li><?php echo translate_str_by_id(5592); ?>: png, jpeg, jpg, bmp;</li>
                          <li><?php echo translate_str_by_id(5593); ?>;</li>
                          <li><?php echo translate_str_by_id(5594); ?>.</li>
                        </div>
                    </label>
                    
                    <div class="input-file-list"></div>  
                </div>
            </div>
            <?php if (!empty($dop_fields_show)) :?>
                <div class="second-input">
                    <h2><?php echo translate_str_by_id(5595); ?></h2>
                    <?php
                    foreach ($dop_fields_show as $dop_fields_key => $dop_fields_value) {
                        ?>
                        <div class="input-shell">
                            <label for="<?=$dop_fields_value['name']?>"><?=translate_str_by_id($dop_fields_value['caption'])?></label>
                            <input type="text" name="<?=$dop_fields_value['name']?>" id="<?=$dop_fields_value['name']?>" placeholder="<?=translate_str_by_id($dop_fields_value['example'])?>" value="<?=($field_default[$dop_fields_value['name']])?$field_default[$dop_fields_value['name']]:'';?>" >
                        </div>
                        <?php
                    }
					?>
                </div>
            <?php endif; ?>

        </div>
        <div class="send-input">
						<?php
							//Подключаем общий модуль принятия пользовательского соглашения
							require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/users_agreement_module.php");
						?>
            <div class="button-shell">
                <button type="submit" class="btnForm btn btn-ar btn-primary"><?php echo translate_str_by_id(4800); ?></button>
            </div>
        </div>
    </form>

  <div class="section-form__helper">
      <div class="section-form__helper_img">
          <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100" fill="none">
              <g clip-path="url(#clip0_1_210)">
                <path d="M25 29.1667C25 23.6414 27.1949 18.3423 31.102 14.4353C35.009 10.5283 40.308 8.33337 45.8333 8.33337C51.3587 8.33337 56.6577 10.5283 60.5647 14.4353C64.4717 18.3423 66.6667 23.6414 66.6667 29.1667C66.6667 34.692 64.4717 39.9911 60.5647 43.8981C56.6577 47.8051 51.3587 50 45.8333 50C40.308 50 35.009 47.8051 31.102 43.8981C27.1949 39.9911 25 34.692 25 29.1667ZM20.0917 61.1292C26.7708 57.0584 35.8542 54.1667 45.8333 54.1667C47.6958 54.1667 49.5292 54.2667 51.3167 54.4584C52.0324 54.5351 52.716 54.796 53.3009 55.2156C53.8858 55.6352 54.352 56.1991 54.6541 56.8524C54.9563 57.5058 55.084 58.2262 55.0248 58.9436C54.9657 59.661 54.7217 60.3508 54.3167 60.9459C51.4941 65.0884 49.9895 69.9873 50 75C50 78.8334 50.8625 82.4584 52.3958 85.6959C52.6946 86.3266 52.8302 87.0222 52.7901 87.719C52.75 88.4157 52.5355 89.0912 52.1664 89.6835C51.7973 90.2757 51.2853 90.7658 50.6774 91.1087C50.0696 91.4516 49.3853 91.6363 48.6875 91.6459L45.8333 91.6667C36.5458 91.6667 27.7708 91.0834 21.1958 89.3417C17.925 88.475 14.8458 87.2334 12.5125 85.3584C10.0417 83.375 8.33334 80.6042 8.33334 77.0834C8.33334 73.8042 9.82501 70.7375 11.85 68.1709C13.9083 65.5667 16.7542 63.1709 20.0917 61.1292ZM70.8333 87.5C70.8335 86.4795 71.2082 85.4945 71.8863 84.7318C72.5645 83.9692 73.499 83.4819 74.5125 83.3625L75.0083 83.3334C76.0703 83.3345 77.0918 83.7412 77.864 84.4702C78.6363 85.1993 79.101 86.1957 79.1632 87.2559C79.2255 88.316 78.8805 89.36 78.1989 90.1743C77.5173 90.9887 76.5504 91.5121 75.4958 91.6375L75 91.6667C73.8949 91.6667 72.8351 91.2277 72.0537 90.4463C71.2723 89.6649 70.8333 88.6051 70.8333 87.5ZM73.1958 67.7084C73.4252 67.3112 73.7791 67.0008 74.2028 66.8254C74.6265 66.6499 75.0963 66.6191 75.5392 66.7378C75.9822 66.8565 76.3736 67.118 76.6528 67.4818C76.932 67.8457 77.0833 68.2914 77.0833 68.75C77.0833 69.3125 76.8833 69.8125 75.7458 70.75L75.1417 71.225L74.6833 71.5667C74.375 71.7959 74.0083 72.0667 73.6708 72.3417L73.0917 72.825C72.1787 73.6051 71.479 74.6044 71.0583 75.7292C70.7174 76.7249 70.7659 77.8128 71.1941 78.7743C71.6224 79.7357 72.3986 80.4995 73.3668 80.9121C74.3351 81.3247 75.4236 81.3556 76.4137 80.9986C77.4038 80.6416 78.2221 79.9231 78.7042 78.9875L79.1083 78.6667L80.5958 77.5417L81.0417 77.1834L81.7958 76.525C83.4875 74.9667 85.4167 72.4917 85.4167 68.75C85.4182 66.4585 84.6642 64.2305 83.2712 62.4109C81.8783 60.5914 79.9243 59.282 77.7118 58.6854C75.4993 58.0889 73.1518 58.2385 71.033 59.1112C68.9142 59.9839 67.1423 61.5309 65.9917 63.5125C65.4353 64.4679 65.2812 65.6051 65.5633 66.6741C65.8453 67.743 66.5405 68.6561 67.4958 69.2125C68.4512 69.7689 69.5884 69.923 70.6574 69.641C71.7263 69.3589 72.6394 68.6637 73.1958 67.7084Z" fill="#F57636"/>
              </g>
              <defs>
                <clipPath id="clip0_1_210">
                  <rect width="100" height="100" fill="white"/>
                </clipPath>
              </defs>
            </svg>
      </div>
      <div class="section-form__helper_text">
          <?php echo translate_str_by_id(4091); ?>
      </div>
  </div>
</div>


<script>
	// Отображение картинок у поля "Прикрепить фото"
	var dt = new DataTransfer();
		
	jQuery('.input-file #client_img').on('change', function(){
		let $files_list = jQuery(this).closest('.input-file').next();
	
		for(var i = 0; i < this.files.length; i++){
			let file = this.files.item(i);
			dt.items.add(file);
		
			let reader = new FileReader();
			reader.readAsDataURL(file);
			reader.onloadend = function(){
				let new_file_input = '<div class="input-file-list-item">' +
					'<img class="input-file-list-img" src="' + reader.result + '">' +
					'<span class="input-file-list-name">' + file.name + '</span>' +
					'<a href="#" onclick="removeFilesItem(this); return false;" class="input-file-list-remove btn-ar btn-primary">&#x2715</a>' +
				'</div>';
				$files_list.append(new_file_input); 
			}
		};
		this.files = dt.files;
	});
	
	// Отправка на почту данных из формы
	jQuery(document).on('submit', '#requestSeller', function(e) {
		e.preventDefault();

    let form = jQuery(this),
        inputs = jQuery( '#requestSeller' ).find( 'select, textarea, input' ).not('[name=csrf_guard_key]');

    let requestSeller_data = new FormData(document.getElementById('requestSeller'));
	
    let checkError = false;

		// Проверка на пустоту
    jQuery(inputs).each(function(){
        inputValue = jQuery(this).val();

        if (jQuery(this).prop('required') && inputValue == '') {
            checkError = true;
        }
    });

    // Проверяем все обязательные поля
    if (checkError == false) {
      // Проверяем соглашение на политику конфиденциальности
      if (document.getElementById("users_agreement").checked) {
          
			jQuery("*[name]").removeClass('error-input');

			jQuery.ajax({
            url: '/content/general_pages/vin_zapros/send_vin_email.php',
            method: 'POST',
            dataType: 'json',
            data: requestSeller_data,
            contentType: false,
            processData: false,
            success: function(data){
                if(data.status == false) {
                    let messageError = '<?php echo translate_str_by_id(5596); ?>';
                    if(data.message != "") messageError = data.message;
                    alert(messageError);
                    if(data.inputs != '') {
                        $('[name='+data.inputs+']').addClass('error-input');
                    }
                // Если ошибок нет (0)
                }else{
                    alert('<?php echo translate_str_by_id(4050); ?>');
                    jQuery(inputs).val('');
                    jQuery('.input-file-list').html('');
                    jQuery('#client_img').val('');
					jQuery('.captha-img a').trigger('click');
					
                }
            },
			error: function (e, ajaxOptions, thrownError){
				alert('<?php echo translate_str_by_id(2122); ?>');
				console.log('<?php echo translate_str_by_id(2122); ?>: '+ e.status +' - '+ thrownError);
				return;
			}
        });

      } else {
          alert("<?php echo translate_str_by_id(4040); ?>");
      }
    } else {
        alert("<?php echo translate_str_by_id(5597); ?>");
    };
	});

	function removeFilesItem(target){
		let name = jQuery(target).prev().text();
		let input = jQuery(target).closest('.input-file-row').find('input[type=file]');
		$(target).closest('.input-file-list-item').remove();	
		for(let i = 0; i < dt.items.length; i++){
			if(name === dt.items[i].getAsFile().name){
				dt.items.remove(i);
			}
		}
		input[0].files = dt.files;
	};

</script>
    
<!-- Подключение модалки -->
<script>
	jQuery(document).ready(function() {
		const vinModal = new HystModal({
			linkAttributeName: "data-hystmodal",
		});
	})

</script>
