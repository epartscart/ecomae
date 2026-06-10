<?php
/**
 * Серверный скрипт для уточнения информации по получению заказа в точке выдачи
 * 
 * 
 * Если все товары находятся уже в точке выдачи - соообщаем, что можно приехать и забрать
 * 
 * Если какой либо товар отсутсвует в данной точке - определяем срок доставки на точку и сообщаем, что можем привезди к такому то времени
 * 
 * 
 * 
 * 1. Пробегаем по всем детальным записям корзины.
 * а) Если все записи относятся к данному магазину, то два варианта:
 * - все товары уже в наличии (ЗАКАЗ);
 * - товары ожидаются (ЗАКАЗ).
 * б) Если не все записи относятся к магазину. Удостоверяемся, что товары относятся к складам, с котороми работает выбранный магазин.
 * Если эти склады работают с данным магазином, считаем срок доставки (ЗАКАЗ);
 * Если эти магазины не работают - сообщаем, что в данном офисе не получится получить заказ (НЕ ВОЗМОЖНО ЗАКАЗАТЬ).
 * 
 * 
 * 2. Проверка детальной записи
 * 2.1. Удостоверяемся, что склад работает с выбранным магазином (ЕСЛИ НЕТ - ВЫХОД - СООБЩЕНИЕ О НЕВОЗМОЖНОСТИ ПОЛУЧИТЬ ЗДЕСЬ ЗАКАЗ)
 * 2.2. Удостоверяемся, что товар уже можно получить в данном магазине (ЕСЛИ НЕТ - считаем время в секундах, через которое товар окажется в этом магазине)
 * 
 * 
 * 
 * 
*/
//Соединение с БД
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();

$office_id = $_POST["office_id"];//Точка обслуживания, которую выбрал покупатель


//Получаем список складов, с котороми работает данная точка обслуживания
$office_storages = array();//Ассоциативный массив ("id склада" => "срок доставки до магазина")

$office_storages_query = $db_link->prepare('SELECT DISTINCT(`storage_id`) AS storage_id, additional_time FROM `shop_offices_storages_map` WHERE `office_id` = ?;');
$office_storages_query->execute( array($office_id) );
while($office_storage = $office_storages_query->fetch() )
{
    $office_storages[$office_storage["storage_id"]] = $office_storage["additional_time"];
}


//Получаем содержимое корзины
if($user_id > 0)
{
	//Поля для авторизованного пользователя
	$session_id = 0;
}
else
{
	//Поля для НЕ авторизованного пользователя
	$session_record = DP_User::getUserSession();
	if($session_record == false)
	{
		$result = array();
		$result["status"] = false;
		$result["code"] = "incorrect_session";
		$result["message"] = "Session error";
		exit(json_encode($result));
	}
	
	$session_id = $session_record["id"];
}

$cart_records = array();//Список записей корзины для этого пользователя
$cart_records_products_types = array();//Карта типов продуктов по каждой записи корзины

//Получаем содержимое его корзины из базы данных
$cart_records_query = $db_link->prepare('SELECT `id`, `product_type` FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ? AND `checked_for_order` = 1;');
$cart_records_query->execute( array($user_id, $session_id) );
while($cart_record = $cart_records_query->fetch() )
{
	array_push($cart_records, $cart_record["id"]);
	$cart_records_products_types[$cart_record["id"]] = $cart_record["product_type"];
}


$can_execute = true;//Флаг - магазин может выполнить данный заказ
$max_time = 0;//Максимальное время для выполнения заказа (самая долгая позиция)
$css_subclass = "alert-success";
$text_info = translate_str_by_id(4421);


//По всем записям корзины (по всем позициям)
for($i=0; $i < count($cart_records); $i++)
{
    $cart_record_id = $cart_records[$i];
    
    //В зависимости от типа продукта
    if($cart_records_products_types[$cart_record_id] == 1)
    {
        //Получаем детальные записи позиции корзины
        $cart_details_query = $db_link->prepare('SELECT * FROM `shop_carts_details` WHERE `cart_record_id` = ?;');
		$cart_details_query->execute( array($cart_record_id) );
        //1
        while( $detail = $cart_details_query->fetch() )
        {
            //2.
            //2.1.
            if($office_storages[$detail["storage_id"]] === NULL)//Точка обслуживания не работает с данным складом
            {
                $can_execute = false;
                break;
            }
            
            //2.2
            //Проверяем на складе, пришла ли уже эта поставка
            $detail_id = $detail["id"];
            $office_id = $detail["office_id"];
            $storage_id = $detail["storage_id"];
            $storage_record_id = $detail["storage_record_id"];
            
            $time_to_exe = 0;//Время, через которое можно будет получить товар из этой поставки
            
            $time_to_exe = $time_to_exe + $office_storages[$detail["storage_id"]]*3600;//Количество секунду для доставки со склада до магазина
            
            //Получаем соединение со складом
			$storage_record_query = $db_link->prepare('SELECT `arrival_time`, `time_to_exe` FROM `shop_storages_data` WHERE `id` = ?;');
			$storage_record_query->execute( array($storage_record_id) );
			$storage_record = $storage_record_query->fetch();
			$arrival_time = $storage_record["arrival_time"];
            
            //Осталось времени до поставки
            $to_arrival_time = $arrival_time - time();
            
            //Если поставка еще не пришла - добавляем к времени исполнения
            if($to_arrival_time > 0)
            {
                $time_to_exe = $time_to_exe + $to_arrival_time;
            }else{
				$time_to_exe = $time_to_exe + $storage_record["time_to_exe"] * 86400;// Переводим в секунды
			}
			
            //Нашли большее время
            if($time_to_exe > $max_time)
            {
                $max_time = $time_to_exe;
            }
            
        }//while() - по детальным записям корзины
    }
    else if($cart_records_products_types[$cart_record_id] == 2)
    {
		$cart_record_query = $db_link->prepare('SELECT `t2_storage_id`, `t2_time_to_exe` FROM `shop_carts` WHERE `id` = ?;');
		$cart_record_query->execute( array($cart_record_id) );
		
        $cart_record = $cart_record_query->fetch();
        
        if($office_storages[$cart_record["t2_storage_id"]] === NULL)//Точка обслуживания не работает с данным складом
        {
            $can_execute = false;
            //break;
        }
        
        //Теперь определяем время доставки:
        $time_to_exe = $cart_record["t2_time_to_exe"]*86400;//Сколько требуется времени на доставку в секундах
        
        //Нашли большее время
        if($time_to_exe > $max_time)
        {
            $max_time = $time_to_exe;
        }
    }
    
    
    
    
    
    //Нет смысла дальше проверять, т.к. по меньшей мере одна позиция со склада, с которым данный магази не работает
    if(!$can_execute)
    {
        $css_subclass = "alert-danger";
        $text_info = translate_str_by_id(4422);
        break;
    }
}//for($i) - по позициям корзины


//Если можно выполнить - определяем, через сколько
if($can_execute)
{
    if($max_time > 0)
    {
        $days = array(translate_str_by_id(4423), translate_str_by_id(4424), translate_str_by_id(4425), translate_str_by_id(4426), translate_str_by_id(4427), translate_str_by_id(4428), translate_str_by_id(4429));
        $months = array("", translate_str_by_id(4430), translate_str_by_id(4431), translate_str_by_id(4432), translate_str_by_id(4433), translate_str_by_id(4434), translate_str_by_id(4435), translate_str_by_id(4436), translate_str_by_id(4437), translate_str_by_id(4438), translate_str_by_id(4439), translate_str_by_id(4440), translate_str_by_id(4441));
        
		$time = $max_time + time();
		
		/*
		if(date("w", $time) == 6){
			$time += 86000 * 2;
		}
		if(date("w", $time) == 0){
			$time += 86000;
		}
		*/
		
        $text_info = translate_str_by_id(4442)." ".$days[date("w", $time)]." ".date("j", $time)." ".$months[date("n", $time)].". ".translate_str_by_id(4443);
        $css_subclass = "alert-warning";
    }
}
?>



<div class="execute_info">
	<p style="margin-bottom: 0;" class="alert alert-border <?php echo $css_subclass; ?>"><?php echo $text_info; ?></p>
	
	
	
	<?php
	// ------------------------------------------------------------------------------------------------------------------
	//Получаем все данные по офису
	$office_query = $db_link->prepare('SELECT * FROM `shop_offices` WHERE `id` = ?;');
	$office_query->execute(array($_POST["office_id"]));
	$office = $office_query->fetch();
	?>
	<div style="padding-left: 0;" class="timetable_show" onclick="show_hide_timetable_map();">
		<a style="display:inline-block; text-decoration:none;">
		<table>
			<tr>
				<td style="padding-right:5px;"><i class="fa fa-arrow-down" aria-hidden="true"></i></td>
				<td><?php echo translate_str_by_id(4444); ?></td>
			</tr>
		</table>
		</a>
	</div>
	<div class="timetable_map" id="timetable_map" style="display:none" state="hidden">
		<table class="table">
			<tr>
				<th><?php echo translate_str_by_id(4418); ?></th>
			</tr>
			<tr>
				<td>
					<span><?php echo translate_str_by_id(3376); ?>: <?php echo translate_str_by_id($office["city"]).", ".translate_str_by_id($office["address"]); ?></span> <a title="<?php echo translate_str_by_id(4445); ?>" style="cursor:pointer;" onClick="ymaps_init();" data-toggle="collapse" data-target="#collapse_office_map_container" aria-expanded="false" aria-controls="collapse_office_map_container"><i alt="sss" class="fa fa-map-o" aria-hidden="true"></i></a>
					<div class="collapse" id="collapse_office_map_container">
						<br/>
						<div style="width: 100%;" id="map" class="office_map_container"></div>
					</div>
				</td>
			</tr>
			<tr>
				<td><?php echo translate_str_by_id(4446); ?>: <?php echo str_replace(array("\n"), "<br>", translate_str_by_id($office["timetable"])); ?></td>
			</tr>
			<tr>
				<td><?php echo translate_str_by_id(1312); ?>: <?php echo $office["phone"]; ?></td>
			</tr>
		</table>
	</div>
	<?php
	// ------------------------------------------------------------------------------------------------------------------
	?>
	
	
	
    <div class="buttons">
    <?php
    if($can_execute)
    {
        ?>
        <a href="javascript:void(0);" onclick="nextStep();" class="btn btn-ar btn-primary"><?php echo translate_str_by_id(4447); ?></a>
        <?php
    }
    ?>
    </div>
</div>