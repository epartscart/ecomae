<?php
defined('_ASTEXE_') or die('No access');

if ($DP_Config->return_available != '1')
    exit(translate_str_by_id(5684));
$items_ids = json_decode($_GET["items"], true);

$items_str = implode(",", $items_ids);
$office_id = 0;

//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();
?>
<link rel="stylesheet" href="../../content/shop/returns/assets/return.css">
<p class="lead"><?php echo translate_str_by_id(3498); ?></p>
<?php if($DP_Config->retention_percentage_text != '' && $DP_Config->retention_percentage_text != '0') : ?>
<div class="notification">
    <?php echo translate_str_by_id($DP_Config->retention_percentage_text); ?>
</div>
<?php endif; ?>
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
            $SELECT_product_manufacturer AS `manufacturer`, 
			(SELECT `user_id` FROM `shop_orders` WHERE `shop_orders`.`id` = `shop_orders_items`.`order_id`) AS `customer_id` 
            FROM `shop_orders_items`
						WHERE `id` IN (" . $items_str . ")
            AND `status` IN (SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `check_for_return` = 1)
            AND `id` NOT IN (SELECT `item_id` FROM `shop_orders_returns_items`);";

        $order_items_query = $db_link->prepare($SELECT_ORDER_ITEMS);
        $order_items_query->execute();
		
		$access_flag = false;
		
        while ($order_item = $order_items_query->fetch()) {
			
			if( $order_item["customer_id"] != $user_id){
				$access_flag = false;
				break;
			}
			$access_flag = true;
			
            $item_id = $order_item["id"];
            $item_status = $order_item["status"];
            $item_count_need = $order_item["count_need"];
            $item_price = $order_item["price"];
            $item_price_sum = $order_item["price_sum"];
            $item_product_type = $order_item["product_type"];
            $item_product_id = $order_item["product_id"];
            $item_product_name = $order_item["product_name"];
            $item_product_manufacturer = $order_item["manufacturer"];
            $item_product_article = $order_item["article"];
            $item_t2_time_to_exe = $order_item["t2_time_to_exe"];
            $item_t2_time_to_exe_guaranteed = $order_item["t2_time_to_exe_guaranteed"];
            $office_id = $order_item["t2_office_id"];
            //Срок доставки для продуктов типа 2
            if ($item_t2_time_to_exe < $item_t2_time_to_exe_guaranteed) {
                $item_t2_time_to_exe = $item_t2_time_to_exe . " - " . $item_t2_time_to_exe_guaranteed;
            }
            $item_t2_time_to_exe = $item_t2_time_to_exe . " дн.";

            //Считаем поля ИТОГО ПО ЗАКАЗУ (если статус позиции позволяет)
            $count_need_total += $item_count_need;
            $price_sum_total += $item_price_sum;


            ?>
            <style>
                textarea {
                    width: 100%; /* Ширина поля в процентах */
                    height: 200px; /* Высота поля в пикселах */
                    resize: none; /* Запрещаем изменять размер */
                }
            </style>
            <tr style="">
                <td style="vertical-align: middle; white-space: nowrap;"><?php echo $item_product_manufacturer; ?></td>
                <td style="vertical-align: middle; white-space: nowrap;"><?php echo $item_product_article; ?></td>
                <td style="vertical-align: middle; width: 100%; min-width: 200px; max-width: 800px; word-wrap: break-word;"><?php echo $item_product_name; ?></td>
								<td style="vertical-align: middle; white-space: nowrap;">
                    <input type="hidden" id="item_price_<?=$item_id;?>" value="<?=$item_price;?>">
                    <?php echo number_format($item_price, 2, '.', ' '); ?>
                </td>
                <td style="vertical-align: middle; white-space: nowrap; text-align:center;">
                    <input type="number" style="width: 70px;" onblur="check_val();" data-count="<?=$item_count_need;?>" id="item_count_<?=$item_id;?>" value="<?=$item_count_need;?>">
                </td>
								<td style="vertical-align: middle; white-space: nowrap;"><?php echo number_format($item_price_sum, 2, '.', ' '); ?></td>
            </tr>
            <tr>
                <td colspan="6">
                    <div class="return_options">
                        <form class="return_options_data" enctype="multipart/form-data" method="post">
                            <input type="hidden" name="item_id" value="<?= $item_id; ?>">
                            <input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>">
                            <input type="hidden" name="tech_key" value="<?=$DP_Config->tech_key;?>";>

                            <div class="col-lg-6 col-sm-6 col-md-12" style="margin-top: 10px;">
                                <label for=""><?php echo translate_str_by_id(3807); ?></label>
                                <select name="reason_id" id="" class="form-control">
                                    <option value="-1"><?php echo translate_str_by_id(4579); ?></option>
                                    <?php
                                    $query_reason = $db_link->prepare("SELECT * FROM `shop_orders_returns_reasons`;");
                                    $query_reason->execute();

                                    while ($result_reason = $query_reason->fetch())
                                        echo '<option value="' . $result_reason["id"] . '">' . $result_reason["caption"] . '</option>';
                                    ?>
                                </select>
                            </div>
                            <div class="col-lg-6 col-sm-6 col-md-12" style="margin-top: 10px;">
                                <label for=""><?php echo translate_str_by_id(4580); ?></label> <input name="images[]" type="file" multiple
                                                                         id="file_form_db" class="form-control"/>
                            </div>
                            <div class="col-lg-12 col-sm-6 col-md-12" style="margin-top: 10px;">
                                <label for=""><?php echo translate_str_by_id(3571); ?></label> <br><textarea name="comment" id="" cols="30" rows="10"
                                                                                placeholder="<?php echo translate_str_by_id(3571); ?>"></textarea>
                            </div>
                        </form>
                    </div>
                </td>
            </tr>
            <?php
        }//while - по позициям заказа
        ?>
    </table>

</div>
<?php
if($access_flag === true){
	if ($DP_Config->retention_percentage > 0) {
		$price_sum_total = $price_sum_total - ($price_sum_total / 100) * $DP_Config->retention_percentage;
	}
?>
<p class="lead"><?php echo translate_str_by_id(3811); ?>: <span id="total_price_span"><?= $price_sum_total; ?></span></p>

<form id="return_info" enctype="multipart/form-data" method="post">
    <input type="hidden" name="user_id" value="<?= DP_User::getUserId(); ?>">
    <input type="hidden" name="return_id" id="return_id" value="">
    <input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>">
    <input type="hidden" name="tech_key" value="<?=$DP_Config->tech_key;?>";>
    <input type="hidden" name="office_id" value="<?=$office_id;?>";>
</form>

<p>
	<input type="button" id="btn_confirm_return" class="btn btn-ar btn-primary" value="<?php echo translate_str_by_id(4527); ?>" onclick="confirm_return()">
</p>

<script>
    let main_url = "<?php echo $DP_Config->domain_path;?>";
		<?php
        if ($DP_Config->retention_percentage > 0)
        {
            ?>
                var retention_percentage = "<?php echo $DP_Config->retention_percentage;?>";
            <?
        }
        else
        {
            ?>
                var retention_percentage = 0;
            <?
        }
    ?>
    var main_total_price = "<?php echo $price_sum_total;?>";
</script>
<script src="<?php echo $DP_Config->domain_path;?>content/shop/returns/assets/add_return.js.php"></script>
<?php
}else{
	echo translate_str_by_id(5685);
}
?>