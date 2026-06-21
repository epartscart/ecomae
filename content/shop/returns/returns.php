<?php
defined('_ASTEXE_') or die('No access');

if (DP_User::getUserId() == 0)
{
    echo translate_str_by_id(4582);

}
else
{
$sql_select_name = "(SELECT `data_value` FROM `users_profiles` WHERE `data_key` = 'surname' AND `shop_orders_returns`.`user_id` = `user_id`)";
$sql_select_surname = "(SELECT `data_value` FROM `users_profiles` WHERE `data_key` = 'name' AND `shop_orders_returns`.`user_id` = `user_id`)";
$sql_client = "CONCAT($sql_select_name, ' ', $sql_select_surname)";

$WHERE_CONDITIONS = "";

//Индикатор непрочитанных сообщений
if(isset($_GET["read"]) && (int)$_GET["read"] === 0)
{
	$WHERE_CONDITIONS .= " AND `shop_orders_returns`.`id` IN(SELECT DISTINCT `return_id` FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 0 AND `return_id` > 0)";
}

$select = "SELECT *, `shop_orders_returns`.`id` as `main_return_id`, $sql_client as `client`, ";
$select .= "(SELECT COUNT(*) FROM `shop_orders_messages` WHERE `return_id` = `shop_orders_returns`.`id` AND `read` = 0 AND `is_customer` = 0) AS `count_not_viewed_msg` ";//Индикатор непрочитанных сообщений в заказах

$select .= "FROM `shop_orders_returns` INNER JOIN `shop_orders_returns_statuses` ON `shop_orders_returns`.`status_id` = `shop_orders_returns_statuses`.`id` WHERE `user_id` = ?".$WHERE_CONDITIONS.";";

$returns_query = $db_link->prepare($select);
$returns_query->execute([DP_User::getUserId()]);
?>
<input class="form-control" type="text"
       placeholder="<?php echo translate_str_by_id(4583); ?>" id="search-text"
       onkeyup="tableSearch()">
<div class="hr-line-dashed col-lg-12"></div>
<table cellpadding="1" cellspacing="1" id="orders_returns_table" class="table table-condensed table-striped"
       style="border-collapse: separate; border-spacing: 0 5px;">
    <thead>
    <tr>
        <th>ID</th>
        <th><?php echo translate_str_by_id(2081); ?></th>
        <th><?php echo translate_str_by_id(3244); ?></th>
        <th><?php echo translate_str_by_id(3811); ?></th>
        <th class="hidden"></th>
    </tr>
    </thead>
    <tbody>
    <?php
    $order_id = "";
    while ($return = $returns_query->fetch()) {
        $returns_items_select = $db_link->prepare("SELECT * FROM `shop_orders_returns_items` WHERE `return_id` = ?;");
        $returns_items_select->execute([$return["main_return_id"]]);

        $total_sum = 0;
        $str_items_for_filter = '';

        while ($returns_items_result = $returns_items_select->fetch()) {
            $orders_items_select = $db_link->prepare("SELECT * FROM `shop_orders_items` WHERE `id` = ?;");
            $orders_items_select->execute([$returns_items_result["item_id"]]);
            $orders_items_result = $orders_items_select->fetch();

            $total_sum += $orders_items_result["price"] * $orders_items_result["count_need"];
            $order_id = $orders_items_result["order_id"];
            $str_items_for_filter .= json_encode($orders_items_result);
        }

        ?>
        <tr style="background: <?= $return["color"]; ?>;">
            <td>
                <a href="<?= $DP_Config->domain_path; ?>shop/returns/return?return_id=<?= $return["main_return_id"]; ?>"> <?= $return["main_return_id"]; ?></a>
				<?php
				//Индикатор непрочитанных сообщений в заказах
				if($return["count_not_viewed_msg"] > 0){
				?>
				<small>
					<a style="white-space: nowrap;" class="dropdown-toggle label-menu-corner" href="/shop/returns/return?return_id=<?= $return["main_return_id"]; ?>">
						<i class="fa fa-envelope" aria-hidden="true"></i>
						<span style="font-size: 9px;"><?=$return["count_not_viewed_msg"];?></span>
					</a>
				</small>
				<?php
				}
				?>
            </td>
            <td><?= $return["caption"]; ?></td>
            <td><a href="<?= $DP_Config->domain_path; ?>shop/orders/order?order_id=<?= $order_id; ?>"
                   target="_blank"> <?= $order_id; ?></a></td>

            <td><?= $total_sum; ?></td>
            <td class="hidden"><?= $str_items_for_filter; ?>></td>

        </tr>
        <?php
    }
    ?>
    </tbody>
</table>

<script>
    function tableSearch() {
        var phrase = document.getElementById('search-text');
        var table = document.getElementById('orders_returns_table');
        var regPhrase = new RegExp(phrase.value, 'i');
        var flag = false;
        for (var i = 1; i < table.rows.length; i++) {
            flag = false;
            for (var j = table.rows[i].cells.length - 1; j >= 0; j--) {
                flag = regPhrase.test(table.rows[i].cells[j].innerHTML);
                if (flag) break;
            }
            if (flag) {
                table.rows[i].style.display = "";
            } else {
                table.rows[i].style.display = "none";
            }

        }
    }
</script>

<?php } ?>
