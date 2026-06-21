<?php
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();

if (DP_User::getUserId() == 0)
{
	?>
	<script>location = '<?=$DP_Config->domain_path;?>'</script>
	<?php
	exit();
}
$select_return = $db_link->prepare("SELECT * FROM `shop_orders_returns` WHERE `id` = ? AND `user_id` = ?;");
$select_return->execute( array($_GET["return_id"], $user_id));

$return_result = $select_return->fetch();

if(!empty($return_result)){
	$return_select = $db_link->prepare("SELECT *, (SELECT `caption` FROM `shop_orders_returns_reasons` WHERE `shop_orders_returns_items`.`reason_id` = `id`) as `reason` FROM `shop_orders_returns_items` WHERE `return_id` = ?;");
    $return_select->execute([$_GET["return_id"]]);
    $returns_items_arr = [];
    $items_id_arr = [];
    while ($result = $return_select->fetch()) {
        $returns_items_arr[$result["item_id"]] = $result;
        $items_id_arr[] = $result["item_id"];
    }

    $items_str = implode(",", $items_id_arr);
	
	// Делаем все сообщения по заявке на возврат прочитанными
	if((int)$_GET["return_id"] > 0){
		$db_link->prepare("UPDATE `shop_orders_messages` SET `read` = 1 WHERE `return_id` = ? AND `is_customer` = 0;")->execute( array($_GET["return_id"]) );
	}
?>
    <h3><label for=""><?php echo translate_str_by_id(3802); ?> <?= $_GET["return_id"]; ?></label></h3>
    <label for=""><?php echo translate_str_by_id(3803); ?>: </label>
<?php
    $query_reason = $db_link->prepare("SELECT *, (SELECT `caption` FROM `shop_orders_returns_statuses` WHERE `shop_orders_returns`.`status_id` = id) as `status` FROM `shop_orders_returns`  WHERE `id` = ?;");
    $query_reason->execute([$_GET["return_id"]]);

    $status_result = $query_reason->fetch();
    echo $status_result["status"];
?>
    <p class="lead" style="border-bottom: 2px solid; padding-bottom: 10px;"><strong><?php echo translate_str_by_id(3498); ?></strong></p>
    <div style="overflow: hidden; overflow-x: auto;">
        <table class="table">
            <tr>
                <th style="vertical-align: middle; white-space: nowrap;"><?php echo translate_str_by_id(2070); ?></th>
                <th style="vertical-align: middle; white-space: nowrap;"><?php echo translate_str_by_id(2071); ?></th>
                <th style="vertical-align: middle;"><?php echo translate_str_by_id(2102); ?></th>
                <th style="vertical-align: middle; white-space: nowrap;"><?php echo translate_str_by_id(2751); ?></th>
                <th style="vertical-align: middle; white-space: nowrap; text-align:center;"><?php echo translate_str_by_id(4526); ?></th>
                <th style="vertical-align: middle; white-space: nowrap;"><?php echo translate_str_by_id(3251); ?></th>
            </tr>
            <?php

            //Флаг - по всем ли позициям принято решение
            $confirm = true;

            //ПОЛЯ ИТОГО ПО ЗАКАЗУ
            $count_need_total = 0;//Итого количество
            $price_sum_total = 0;//Итого сумма

            //ПОЛУЧАЕМ ВСЕ ПОЗИЦИИ ЗАКАЗА

            //Запрос наименований
            $SELECT_product_name = "`t2_name`";

            //Запрос артикула
            $SELECT_product_article = "`t2_article`";

            //Запрос производителя
            $SELECT_product_manufacturer = "`t2_manufacturer`";

            //Сумма позиции
            $SELECT_item_price_sum = "`price`*`count_need`";

            //СЛОЖНЫЙ ВЛОЖЕННЫЙ ЗАПРОС
            $SELECT_ORDER_ITEMS = "SELECT *, 
            $SELECT_product_name AS `product_name`, 
            $SELECT_item_price_sum AS `price_sum`, 
            $SELECT_product_article AS `article`, 
            $SELECT_product_manufacturer AS `manufacturer` 
            FROM `shop_orders_items` WHERE `id` IN (" . $items_str . ");";

            $order_items_query = $db_link->prepare($SELECT_ORDER_ITEMS);
            $order_items_query->execute();

            $order_id = '';

            while ($order_item = $order_items_query->fetch()) {

                $order_id = $order_item["order_id"];

                $item_id = $order_item["id"];
                $item_count_need = $order_item["count_need"];
                $item_price = $order_item["price"];
                $item_price_sum = $order_item["price_sum"];
                $item_product_name = $order_item["product_name"];
                $item_product_manufacturer = $order_item["manufacturer"];
                $item_product_article = $order_item["article"];

                //Считаем поля ИТОГО ПО ЗАКАЗУ (если статус позиции позволяет)
                $count_need_total += $item_count_need;
                $disabled_success = "";
                $disabled_reject = "";
                if ($returns_items_arr[$item_id]['return_success'] == '1') {
                    $return_item_status = translate_str_by_id(3804);
                    $disabled_success = "disabled";
                    $price_sum_total += $item_price_sum;
                } else if ($returns_items_arr[$item_id]['return_success'] == '0') {
                    $return_item_status = translate_str_by_id(3805);
                    $disabled_reject = "disabled";
                } else {
                    $return_item_status = translate_str_by_id(3806);
                    $confirm = false;
                }


                ?>
                <tr style="border-top: 1px solid; ">
                    <td style="vertical-align: middle; white-space: nowrap;"><?php echo $item_product_manufacturer; ?></td>
                    <td style="vertical-align: middle; white-space: nowrap;"><?php echo $item_product_article; ?></td>
                    <td style="vertical-align: middle; width: 100%; min-width: 200px; max-width: 800px; word-wrap: break-word;"><?php echo $item_product_name; ?></td>
                    <td style="vertical-align: middle; white-space: nowrap;"><?php echo number_format($item_price, 2, '.', ' '); ?></td>
                    <td style="vertical-align: middle; white-space: nowrap; text-align:center;"><?php echo $item_count_need; ?></td>
                    <td style="vertical-align: middle; white-space: nowrap;"><?php echo number_format($item_price_sum, 2, '.', ' '); ?></td>
                </tr>
                <tr style="border-bottom: 1px solid;">
                    <td colspan="6">

                        <div class="col-lg-12 col-sm-12 col-md-12" style="margin-top: 10px;">
                            <label for="">Фото товара: </label>
                            <div style="display: flex;">

                                <?php
                                $query = $db_link->prepare("SELECT * FROM `shop_orders_returns_items_images` WHERE `return_item_id` = ?;");
                                $query->execute([$returns_items_arr[$item_id]["id"]]);
                                while ($result_img_blob = $query->fetch()) {
																		echo '<div class="place_return_item_img"><img onclick="openImage(this)" class="return_item_img" src = "data:image/png;base64,' . base64_encode(file_get_contents($result_img_blob['image'])) . '"></div>';
																	}
                                ?>
                            </div>
                        </div>
                        <div class="col-lg-12 col-sm-12 col-md-12" style="margin-top: 10px;">
                            <label for=""><?php echo translate_str_by_id(3807); ?>: </label><?= " " . $returns_items_arr[$item_id]["reason"]; ?>
                        </div>
                        <div class="col-lg-12 col-sm-12 col-md-12" style="margin-top: 10px;">
                            <label for=""><?php echo translate_str_by_id(4581); ?>: </label> <br>
                            <div>
                                <?= $returns_items_arr[$item_id]["comment"]; ?>
                            </div>
                        </div>
                        <div class="col-lg-12 col-sm-12 col-md-12" style="margin-top: 10px;">
                            <label for=""><?php echo translate_str_by_id(2081); ?>: </label> <br>
                            <div>
                                <?= $return_item_status; ?>
                            </div>
                        </div>
                    </td>
                </tr>
                <tr>
                    <td colspan="6" style="padding-top: 10px; border-top: 2px solid;">

                    </td>
                </tr>
                <?php
            }//while - по позициям заказа
            ?>
        </table>
        <p class="lead"><?php echo translate_str_by_id(3811); ?>: <?= $price_sum_total; ?> руб.</p>
    </div>
    </div>
    </div>
    </div>

	<script>
		function openImage(image)
		{
			let w = window.open("");
			w.document.write(image.outerHTML);
		}
	</script>

	<link rel="stylesheet" href="<?=$DP_Config->domain_path . "content/shop/returns/assets/return.css";?>">

<?php
	require_once $_SERVER["DOCUMENT_ROOT"] . "/content/shop/returns/return_messages.php";
}else{
	echo translate_str_by_id(5691);
}
?>