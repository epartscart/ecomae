<?php
    error_reporting(0);

    require_once($_SERVER["DOCUMENT_ROOT"] . "/config.php");
    require_once ($_SERVER["DOCUMENT_ROOT"]. "/content/shop/returns/ajax/helper.php");

    //Соединение с основной БД
    $DP_Config = new DP_Config;//Конфигурация CMS

    //Подключение к БД
    try {
        $db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
    } catch (PDOException $e) {
        exit("No DB connect");
    }
    $db_link->query("SET NAMES utf8;");

    $answer = [
        "status" => true,
        "data" => "success"
    ];
	
	// -------------------------------------------------------------------------------
	//Подключение мультиязычности
	require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
	$multilang_params = multilang_init();
	// -------------------------------------------------------------------------------
	
    require_once($_SERVER["DOCUMENT_ROOT"] . "/content/users/stop_csrf.php");

    $_POST["change_count_arr"] = []; //для отслеживания разбитых позиций

    //Собираем идентификаторы позиций
    $arr_items_ids = [];
    foreach ($_POST["items"] as $key => $item)
        $arr_items_ids[] = $item["item_id"];

    createTmpForCut($arr_items_ids);//Временная таблица

    $db_link->beginTransaction();
    try {

        checkData(); //Проверка прав доступа
        $return_id = uploadReturn();//Загрузка записи возврата

        foreach ($_POST["items"] as $key => $item)
            uploadItem($item, $return_id, $key);//загрузка записи позиции возврата

    } catch (Exception $e) {
        $db_link->rollBack();

        $answer = [
            "status" => false,
            "error_message" => $e->getMessage()
        ];
        exit(json_encode($answer));
    }

    sendNotify($return_id); //Отправка уведомлений

    $db_link->commit();

    $arr_items_ids = [];
    foreach ($_POST["items"] as $item)
        $arr_items_ids[] = $item["item_id"];

    changeStatus($arr_items_ids);//Смена статуса позиций

    //Обновляем данные по разбитым позициям
    if (count($_POST["change_count_arr"]) > 0)
    {
        foreach($_POST["change_count_arr"] as $item)
        {
            $item_info = $item["item_info"];
            $item_id = $item_info["id"];
            $new_order_item_id = $item["new_order_item_id"];
            $order_id = $item_info["order_id"];
            $time = time();
            $_GET["count"] = $item["count"];

            // Уменьшаем количество у исходной позиции
            $sql = "UPDATE `shop_orders_items` SET `count_need` = ? WHERE `id` = ?;";
            $query = $db_link->prepare($sql);
            $query->execute(array((int) $item_info["count_need"] - (int)$item['count'], $item_info["id"] ));

            // Добавляем детальные записи по товарам из каталога
            if($item_info['product_type'] == 1){
                $sql = "INSERT INTO `shop_orders_items_details` 
					(`id`, `order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`) 
					SELECT 
					NULL, `order_id`, $new_order_item_id, `office_id`, `storage_id`, `storage_record_id`, ".((int)$_GET["count"]).", `count_issued`, `count_canceled`, `price_purchase` FROM `shop_orders_items_details` WHERE `order_item_id` = $item_id";
                $query = $db_link->prepare($sql);
                if(!$query->execute()){
                    throw new Exception(translate_str_by_id(5632));
                }
            }

            // Пишем лог
            if( $db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`is_robot`,`text`) VALUES (?, ?, ?, ?, ?,?);')->execute( array($order_id, $time, '', 0, 1, translate_str_by_id(5686).' ID '.$new_order_item_id.' '.translate_str_by_id(5687).' ID '.$item_id.' '.translate_str_by_id(5688).' '.((int)$_GET["count"])) ) != true )
            {
                throw new Exception(translate_str_by_id(5633));
            }

            // Уменьшаем количество у исходной позиции детальной записи по товарам из каталога
            if($item_info['product_type'] == 1){
                $sql = "UPDATE `shop_orders_items_details` SET `count_reserved` = ? WHERE `order_item_id` = ?;";
                $query = $db_link->prepare($sql);
                if( $query->execute(array( (((int)$item_info['count_need']) - ((int)$_GET["count"])), $item_id )) != true )
                {
                    throw new Exception(translate_str_by_id(5635));
                }
            }

            // Пишем лог
            if( $db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`is_robot`,`text`) VALUES (?, ?, ?, ?, ?,?);')->execute( array($order_id, $time, '', 0, 1,''.translate_str_by_id(5636).' ID '.$new_order_item_id.' '.translate_str_by_id(5637).' ID '.$item_id.'. '.translate_str_by_id(5638).' '.$item_info['count_need'].' '.translate_str_by_id(5639).' '.(((int)$item_info['count_need']) - ((int)$_GET["count"]))) ) != true )
            {
                throw new Exception(translate_str_by_id(5633)." 2");
            }

        }
    }

    exit(json_encode($answer));
?>