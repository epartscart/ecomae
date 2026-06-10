<?php
    function uploadReturn() {
        global $db_link;

        $query_select = $db_link->prepare("SELECT `id` FROM `shop_orders_returns_statuses` WHERE `caption` = 'Оформлена'");
        $query_select->execute();
        $result_id = $query_select->fetch();

        if (!$result_id)
            throw new Exception(translate_str_by_id(4572).".");

        $query = $db_link->prepare("INSERT INTO `shop_orders_returns` (`status_id`, `user_id`,`sum`) VALUES (?, ?, ?)");

        if (!$query->execute([$result_id["id"], $_POST["user_id"], $_POST["total_sum"]]))
            throw new Exception(translate_str_by_id(4573).".");

        return $db_link->lastInsertId();
    }

    function uploadItem($item, $return_id, $key) {
        global $db_link;

        //Делаем разбиение, если необходимо
        $item["item_id"] = checkItemToCut($item, $key);

        //Добавляем запись позиции возврата
        $sql = $db_link->prepare("INSERT INTO `shop_orders_returns_items` (`comment`, `reason_id`, `return_id`, `item_id`,`count_need`) VALUES (?, ?, ?, ?, ?)");
        if (!$sql->execute( array( htmlentities($item["comment"]), (int) $item["reason_id"], $return_id, $item["item_id"], $item["count"] ) ))
            throw new Exception(translate_str_by_id(4574).".");

        $return_item_id = $db_link->lastInsertId();

        //Загружаем изображение
        uploadImages($item, $return_item_id);
    }

    function checkItemToCut($item,$key) {
        global $db_link;

        //Получаем все колонки таблицы
        $new_order_item_id = $item["item_id"];
        $SQL_get_col_orders_items = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='shop_orders_items' AND
	                        COLUMN_NAME NOT IN ('id')";

        $SQL_get_col_orders_items = $db_link->prepare($SQL_get_col_orders_items);
        $SQL_get_col_orders_items->execute();

        $SQL_get_col_orders_items_res = $SQL_get_col_orders_items->fetchAll();

        //Формируем колонки в строку
        $str_params = "";
        $already_saved = [];
        foreach ($SQL_get_col_orders_items_res as $key_c => $col_ar)
        {
            if (in_array($col_ar, $already_saved))
                continue;
            if ($key_c > 0)
                $str_params .= ', ';

            $str_params .= $col_ar[0];
            $already_saved[] = $col_ar;
        }

        //Получаем позицию
        $select = $db_link->prepare("SELECT * FROM `shop_orders_items` WHERE `id` = ?;");
        $select->execute([$item["item_id"]]);
        $res_i = $select->fetch();

        //
        if ($item["count"] < $res_i["count_need"])
        {
            // Удаляем id клонируемой позиции
            $sql = "UPDATE `tmp` SET `count_need` = ? WHERE `id` = ?;";
            $query = $db_link->prepare($sql);
            $query->execute(array((int)$item["count"], (int)$item["item_id"]));

            // Клонируем запись в таблицу позиций
            $sql = "INSERT INTO `shop_orders_items`  (".$str_params.") SELECT ".$str_params." FROM `tmp` WHERE `id` = ?;";
            $query = $db_link->prepare($sql);

            if($query->execute([(int)$item["item_id"]])){

                //Актуализируем идентификаторы
                $new_order_item_id = $db_link->lastInsertId();
                $_POST["items"][$key]["item_id"] = $new_order_item_id;
                $_FILES["images"]["tmp_name"][$new_order_item_id] = $_FILES["images"]["tmp_name"][$item["item_id"]];
                $_POST["change_count_arr"][] =
                    [
                        "count"  => (int)$item["count"],
                        "item_info" => $res_i,
                        "new_order_item_id" => $new_order_item_id
                    ];
            }
        }

        return $new_order_item_id;
    }

    function changeStatus($arr_items_ids, $status_id = null) {
        global $db_link;

        if (is_null($status_id))
        {
            //Запрос нового статуса
            $select_status = $db_link->prepare("SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_return` = 1;");
            $select_status->execute();
            $res_select_status = $select_status->fetch();
            $new_status = $res_select_status['id'];
        }
        else
            $new_status = $status_id;

        $select_order_id = $db_link->prepare("SELECT `order_id` FROM `shop_orders_items` WHERE `id` = ?");
        $select_order_id->execute([$arr_items_ids[0]]);
        $order_id = $select_order_id->fetch();
        $order_id = $order_id["order_id"];

        if ($new_status > 0){
            $db_link->prepare("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` IN (".implode(',' , $arr_items_ids).")")->execute([$new_status]);
            $db_link->prepare("INSERT INTO `shop_orders_logs` (`id`, `order_id`, `time`, `user_id`, `is_manager`, `text`, `is_robot`) VALUES (NULL, ".$order_id.", ". time() .", '0', '0', translate_str_by_id(5689).' [". implode(', ' , $arr_items_ids) ."] '.translate_str_by_id(5690), '1')")->execute();
        }
    }

    //Проверка прав доступа и возможности возврата
    function checkData() {
        global $db_link;
        global $DP_Config;

        if ($_POST["tech_key"] != $DP_Config->tech_key) {
            throw new Exception("Forbidden");
        }

        foreach ($_POST["items"] as $key => $item)
        {
            $sql = $db_link->prepare("SELECT COUNT(*) as `count` FROM `shop_orders_returns_items` WHERE `item_id` = ?;");
            $sql->execute(array($item["item_id"]));
            $result_sql_items = $sql->fetch();

            if ($result_sql_items["count"] > 0) {
                throw new Exception(translate_str_by_id(4571));
            }
        }
    }

		//Загрузка изображений
		function uploadImages($item, $return_item_id) {
			global $db_link;
			$total_file_size = 15728640;
			$total_size = 0;
			$available_format = ['image/png', 'image/jpeg', 'image/jpg','image/bmp'];
			foreach ($_FILES['images']['type'] as $types)
					foreach ($types as $type)
							if (!in_array($type, $available_format))
									throw new Exception(translate_str_by_id(4575));


			foreach ($_FILES['images']['size'] as $key => $sizes)
					foreach ($sizes as $size)
					{
							if ($size > 5242880)
									throw new Exception(translate_str_by_id(4576));
							$total_size += $size;
							if ($total_size > $total_file_size)
									throw new Exception(translate_str_by_id(4577));
					}

			$uploaddir = $_SERVER["DOCUMENT_ROOT"] . '/content/files/returns_images/';

			if (!file_exists($uploaddir))
					mkdir($uploaddir);

			foreach ($_FILES["images"]["tmp_name"][$item["item_id"]] as $key => $file) {
					$filename = tempnam( $uploaddir, '');
					file_put_contents($filename, file_get_contents($file));

					$query = $db_link->prepare("INSERT INTO `shop_orders_returns_items_images` (`return_item_id`, `image`) VALUES (?, ?)");
					if (!$query->execute([$return_item_id, $filename]))
							throw new Exception(translate_str_by_id(4578));
			}
		}


    //Отправка уведомлений
    function sendNotify($return_id) {
        global $db_link;

        //Для отправки уведомлений
        require_once( $_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php" );

        //$return_id
        $office_id = $_POST["office_id"];

        $managers_query = $db_link->prepare('SELECT `users` FROM `shop_offices` WHERE `id` = ?;');
        $managers_query->execute( array($office_id) );
        $managers_record = $managers_query->fetch();
        if( $managers_record != false ) {
            //Список ID менеджеров
            $managers_list = json_decode($managers_record["users"], true);

            //Формируем массив получателей
            $persons = array();//Массив получателей
            for ($i = 0; $i < count($managers_list); $i++)
                $persons[] = array('type' => 'user_id', 'user_id' => (int)$managers_list[$i]);
        }

        $notify_vars["return_id"] = $return_id;

        $send_result = send_notify('return_new_manager', $notify_vars, $persons);
        if ($send_result["status"])
        {
            $persons = array();
            $persons[] = array('type' => 'user_id', 'user_id' => (int)$_POST["user_id"]);
            $send_result = send_notify('return_new_customer', $notify_vars, $persons);
        }
    }

    //Создание временной таблицы
    function createTmpForCut($arr_items_ids) {
        global $db_link;
        // Клонируем запись во временную таблицу
        $sql = "CREATE TEMPORARY TABLE `tmp` SELECT * FROM `shop_orders_items` WHERE `id` IN (".implode(",",$arr_items_ids).");";
        $tmp_query = $db_link->prepare($sql);
        $tmp_query->execute();
    }
