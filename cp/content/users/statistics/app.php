<?php
defined('_ASTEXE_') or die('No access');

function getBlockStyle($arr) {
    $arrSizes = [
        'style="height: 100px;"',
        'style="height: 200px;"',
        'style="height: 300px;"',
        'style="height: 600px;"',
    ];
	
	if( ! isset($arr["count"]) ){
		$arr["count"] = null;
	}
	
    $arr["count"] = is_null($arr["count"]) ? $arr['count_out'] + $arr['count_in'] : $arr['count'];

    if ($arr["count"] == 0)
        $arr["style"] = $arrSizes[0];
    else if ($arr['count'] >= 50)
        $arr['style'] = $arrSizes[3];
    else if ($arr['count'] >= 5)
        $arr['style'] = $arrSizes[2];
    else
        $arr['style'] = $arrSizes[1];

    return $arr;
}

$main_url = $DP_Config->domain_path.$DP_Config->backend_dir;

$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = ".(int)$_GET["user_id"]." AND `income`=1 AND `active` = 1), 0)";
$ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = ".(int)$_GET["user_id"]." AND `income`=0 AND `active` = 1),0)";

$SQL = "SELECT ($INCOME_SQL-$ISSUE_SQL) AS `balance`;";

$query = $db_link->prepare($SQL);
$query->execute();
$row = $query->fetch();

$balance = $row["balance"];
if($balance == "")
{
    $balance = 0;
}

$balance = number_format($balance, 2, '.', ' ');

$orders = getBlockStyle(DP_User::getUserOrdersById($_GET['user_id']));
$cart = getBlockStyle(DP_User::getUserCartsById($_GET['user_id']));
$cars = getBlockStyle(DP_User::getUserCarsById($_GET['user_id']));
$operations = getBlockStyle(DP_User::getUserFinanceById($_GET['user_id']));
$queries = getBlockStyle(DP_User::getUserQueriesById($_GET['user_id']));
$returns = getBlockStyle(DP_User::getUserReturnsById($_GET['user_id']));
$items = getBlockStyle(DP_User::getUserOrdersItemsById($_GET['user_id']));
$messages = getBlockStyle(DP_User::getUserMessagesById($_GET['user_id']));

?>
    <link rel="stylesheet" href="/<?php echo $DP_Config->backend_dir; ?>/content/users/statistics/assets/main.css">
    <div class="col-lg-12">
        <div class="hpanel">
            <div class="panel-heading hbuilt">
                <?php echo translate_str_by_id(2113); ?>
            </div>
            <div class="panel-body">
				<?php
				print_backend_button(array('background_color'=>'#b9babb', 'fontawesome_class'=>'fas fa-chevron-left', 'caption'=>translate_str_by_id(2961), 'url'=>$DP_Config->domain_path.$DP_Config->backend_dir.'/users/usermanager/user?user_id='.$_GET['user_id']));
				?>
            </div>
        </div>
    </div>
    <div class="col-lg-12">
        <div class="hpanel">
            <div class="panel-heading hbuilt">
                <?php echo translate_str_by_id(5301); ?>
            </div>
            <div class="panel-body">
                <div class="cards-wrapper">
                    <div class="stat-card">
                            <div class="stat-card-header">
                                <?php echo translate_str_by_id(5560); ?>
                            </div>
                            <div class="stat-card-body" <?php echo $queries['style']; ?>>
                                <table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
                                    <thead>
                                    <tr>
                                        <th><?php echo translate_str_by_id(2070); ?></th><th><?php echo translate_str_by_id(2071); ?></th><th><?php echo translate_str_by_id(2102); ?></th><th><?php echo translate_str_by_id(3250); ?>, <?php echo translate_str_by_id(5357); ?></th>
                                    </tr>
                                    </thead>
                                    <tbody>
                                    <?php
                                    foreach ($queries['data'] as $query) {
                                        ?>
                                        <tr class="tr-click" onclick="window.open('<?php echo $DP_Config->domain_path; ?>parts/<?php echo ($query['manufacturer'] == '') ? 'brands' : $query['manufacturer']; ?>/<?php echo $query["article"]; ?>')">
                                            <td><?php echo $query['manufacturer']; ?></td>
                                            <td><?php echo $query['article']; ?></td>
                                            <td><?php echo $query['name']; ?></td>
                                            <td><?php echo $query['time']; ?></td>
                                        </tr>
                                        <?
                                    }
                                    ?>
                                    </tbody>
                                </table>
                            </div>
                            <div class="stat-card-footer">
                                <span><?php echo translate_str_by_id(5561); ?> <?php echo $queries['count']; ?>.</span>
                                <button onclick="locationToStatisticsByUserId('<?php echo $main_url; ?>/shop/statistika', '<?php echo $_GET['user_id']?>', '<?php echo time() - (90 * 86400); ?>', '<?php echo time(); ?>')"><?php echo translate_str_by_id(5562); ?></button>
                            </div>
                        </div>
                    <div class="stat-card">
                        <div class="stat-card-header">
                            <?php echo translate_str_by_id(3583); ?>
                        </div>
                        <div class="stat-card-body" <?php echo $orders['style']; ?>>
                            <table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
                                <thead>
                                    <tr>
                                        <th><?php echo translate_str_by_id(3244); ?></th><th><?php echo translate_str_by_id(2081); ?></th><th><?php echo translate_str_by_id(3250); ?></th><th><?php echo translate_str_by_id(3251); ?></th><th><?php echo translate_str_by_id(5563); ?></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    <?php
                                        foreach ($orders['data'] as $order) {
                                            ?>
                                                <tr class="tr-click" onclick="window.open('<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $order["id"]; ?>', '_blank')">
                                                    <td><?php echo $order['id']; ?></td>
                                                    <td><?php echo $order['status']; ?></td>
                                                    <td><?php echo $order['date']; ?></td>
                                                    <td><?php echo $order['sum']; ?></td>
                                                    <td><?php echo $order['debt']; ?></td>
                                                </tr>
                                            <?
                                        }
                                    ?>
                                </tbody>
                            </table>
                        </div>
                        <div class="stat-card-footer">
                            <span>
                                <?php echo translate_str_by_id(3593); ?> <?php echo $orders['count']; ?> на сумму <?php echo $orders['total']; ?>. <br>
                                <?php echo translate_str_by_id(5564); ?> <?php echo $orders['total_debt']; ?>.
                            </span>
                            <button onclick="locationToOrdersByUserId('<?php echo $main_url; ?>/shop/orders/orders', '<?php echo $_GET['user_id']; ?>')"><?php echo translate_str_by_id(5565); ?></button>
                        </div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-header">
                            <?php echo translate_str_by_id(767); ?>
                        </div>
                        <div class="stat-card-body" <?php echo $items['style']; ?>>
                            <table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
                                <thead>
                                <tr>
                                    <th>ID</th><th><?php echo translate_str_by_id(2070); ?></th><th><?php echo translate_str_by_id(2071); ?></th><th><?php echo translate_str_by_id(3244); ?></th>
                                </tr>
                                </thead>
                                <tbody>
                                <?php
                                foreach ($items['data'] as $item) {
                                    $link = $DP_Config->domain_path.$DP_Config->backend_dir . "/shop/orders/order?order_id=" . $item['order_id'];
                                    ?>
                                    <tr class="tr-click" onclick="window.open('<?php echo $link; ?>', '_blank')">
                                        <td><?php echo $item['id']; ?></td>
                                        <td><?php echo $item['manufacturer']; ?></td>
                                        <td><?php echo $item['article']; ?></td>
                                        <td><?php echo $item['order_id']; ?></td>
                                    </tr>
                                    <?
                                }
                                ?>
                                </tbody>
                            </table>
                        </div>
                        <div class="stat-card-footer">
                            <span><?php echo translate_str_by_id(3615); ?> <?php echo $items['count']; ?>.</span>
                            <button onclick="locationToItemsByUserId('<?php echo $main_url; ?>/shop/orders/items', '<?php echo $_GET['user_id']?>')"><?php echo translate_str_by_id(5566); ?></button>
                        </div>
                    </div>
                    <div class="stat-card">
                            <div class="stat-card-header">
                                <?php echo translate_str_by_id(5567); ?>
                            </div>
                            <div class="stat-card-body" <?php echo $operations['style']; ?>>
                                <table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
                                    <thead>
                                    <tr>
                                        <th><?php echo translate_str_by_id(4401); ?></th><th><?php echo translate_str_by_id(3250); ?>, <?php echo translate_str_by_id(5357); ?></th><th><?php echo translate_str_by_id(2238); ?></th><th><?php echo translate_str_by_id(3251); ?></th><th><?php echo translate_str_by_id(3243); ?></th>
                                    </tr>
                                    </thead>
                                    <tbody>
                                    <?php
                                    foreach ($operations['data'] as $operation) {
                                        ?>
                                        <tr>
                                            <td><?php echo $operation['id']; ?></td>
                                            <td><?php echo $operation['time']; ?></td>
                                            <td><?php echo $operation['type']; ?></td>
                                            <td><?php echo $operation['amount']; ?></td>
                                            <td><?php echo $operation['order_id']; ?></td>
                                        </tr>
                                        <?
                                    }
                                    ?>
                                    </tbody>
                                </table>
                            </div>
                            <div class="stat-card-footer">
                            <span>
                                <?php echo translate_str_by_id(5568); ?> <?php echo $operations['count_in'] + $operations['count_out']; ?>: <br>
                                - <?php echo translate_str_by_id(5569); ?> <?php echo $operations['count_in']; ?> <?php echo translate_str_by_id(4496); ?> <?php echo $operations['total_in']; ?>; <br>
                                - <?php echo translate_str_by_id(5570); ?> <?php echo $operations['count_out']; ?> <?php echo translate_str_by_id(4496); ?> <?php echo $operations['total_out']; ?>; <br>
                                - <?php echo translate_str_by_id(4655); ?>: <?php echo  $balance; ?>.
                            </span>
                                <button onclick="locationToFinanceByUserId('<?php echo $main_url; ?>/shop/finance/account_operations', '<?php echo $_GET['user_id']; ?>')"><?php echo translate_str_by_id(5277); ?></button>
                            </div>
                        </div>
                    <div class="stat-card">
                        <div class="stat-card-header">
                            <?php echo translate_str_by_id(4410); ?>
                        </div>
                        <div class="stat-card-body" <?php echo $cart['style']; ?>>
                            <table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
                                <thead>
                                <tr>
                                    <th><?php echo translate_str_by_id(2102); ?></th><th><?php echo translate_str_by_id(2752); ?></th><th><?php echo translate_str_by_id(2751); ?></th><th><?php echo translate_str_by_id(233); ?></th>
                                </tr>
                                </thead>
                                <tbody>
                                <?php

                                    foreach ($cart['data'] as $item) {
                                        ?>
                                        <tr>
                                            <td><?php echo $item['name']; ?></td>
                                            <td><?php echo $item['count_need']; ?></td>
                                            <td><?php echo $item['price']; ?></td>
                                            <td><?php echo $item['storage']; ?></td>
                                        </tr>
                                        <?
                                    }
                                ?>
                                </tbody>
                            </table>
                        </div>
                        <div class="stat-card-footer">
                            <span><?php echo translate_str_by_id(5571); ?> <?php echo $cart['count']; ?> <?php echo translate_str_by_id(4496); ?> <?php echo $cart['total']; ?>.</span>
                            <button onclick="locationToCartsByUserId('<?php echo $main_url; ?>/shop/orders/carts', '<?php echo $_GET['user_id']; ?>')"><?php echo translate_str_by_id(5566); ?></button>
                        </div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-header">
                            <?php echo translate_str_by_id(5572); ?>
                        </div>
                        <div class="stat-card-body" <?php echo $messages['style']; ?>>
                            <table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
                                <thead>
                                <tr>
                                    <th><?php echo translate_str_by_id(2119); ?></th><th><?php echo translate_str_by_id(3244); ?></th><th><?php echo translate_str_by_id(5573); ?></th>
                                </tr>
                                </thead>
                                <tbody>
                                <?php
                                    foreach ($messages['data'] as $message) {
                                        $link = '';
                                        if ($message['order_id'] > 0)
                                            $link = $DP_Config->domain_path.$DP_Config->backend_dir . "/shop/orders/order?order_id=" . $message['order_id'];
                                        else
                                            $link = $DP_Config->domain_path.$DP_Config->backend_dir . "/shop/returns-manager?return_id=" . $message['return_id'];
                                        ?>
                                        <tr class="tr-click" onclick="window.open('<?php echo $link; ?>','_blank')">
                                            <td><?php echo $message['text']; ?></td>
                                            <td>
                                                <?php if ($message['order_id'] > 0) : ?>
                                                   <?php echo $message['order_id']; ?>
                                                <?php endif; ?>
                                            </td>
                                            <td>
                                                <?php if ($message['return_id'] > 0) : ?>
                                                    <?php echo $message['return_id']; ?>
                                                <?php endif; ?>
                                            </td>
                                        </tr>
                                        <?
                                    }
                                ?>
                                </tbody>
                            </table>
                        </div>
                        <div class="stat-card-footer">
                            <span><?php echo translate_str_by_id(5574); ?> <?php echo $messages['count']; ?>.</span>
                        </div>
                    </div>
                    <div class="stat-card" style="cursor: default;">
                        <div class="stat-card-header">
                            <?php echo translate_str_by_id(4669); ?>
                        </div>
                        <div class="stat-card-body" <?php echo $cars['style']; ?>>
                            <table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
                                <thead>
                                <tr>
                                    <th>VIN / FRAME</th><th><?php echo translate_str_by_id(2085); ?></th><th><?php echo translate_str_by_id(2086); ?></th><th><?php echo translate_str_by_id(5575); ?></th>
                                </tr>
                                </thead>
                                <tbody>
                                <?php
                                    if ($cars['count'] > 0)
                                    {
                                        foreach ($cars['data'] as $car) {
                                            ?>
                                            <tr>
                                                <td><?php echo $car['vin'].' / '.$car['frame']; ?></td>
                                                <td><?php echo $car['mark']; ?></td>
                                                <td><?php echo $car['model']; ?></td>
                                                <td><?php echo $car['engine_value']; ?></td>
                                            </tr>
                                            <?
                                        }
                                    }
                                ?>
                                </tbody>
                            </table>
                        </div>
                        <div class="stat-card-footer">
                            <?php echo translate_str_by_id(5576); ?> <?php echo $cars['count']; ?>.
                        </div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-header">
                            <?php echo translate_str_by_id(4030); ?>
                        </div>
                        <div class="stat-card-body" <?php echo $returns['style']; ?>>
                            <table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
                                <thead>
                                <tr>
                                    <td><?php echo translate_str_by_id(3817); ?></td><th><?php echo translate_str_by_id(3251); ?></th><th><?php echo translate_str_by_id(5194); ?></th>
                                </tr>
                                </thead>
                                <tbody>
                                    <?php
                                    foreach ($returns['data'] as $return) {
                                    ?>
                                    <tr class="tr-click"  onclick="window.open('<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/returns-manager?return_id=<?php echo $return["id"]; ?>', '_blank')">
                                        <td><?php echo $return['id']; ?></td>
                                        <td><?php echo $return['return_sum']; ?></td>
                                        <td><?php echo $return['return_complete'] == '1' ? translate_str_by_id(2456) : translate_str_by_id(2457); ?></td>
                                    </tr>
                                    <?
                                }
                                ?>
                                </tbody>
                            </table>
                        </div>
                        <div class="stat-card-footer">
                            <span>translate_str_by_id(5577) <?php echo $returns['count']; ?> на сумму <?php echo $returns['total']; ?>.</span>
                            <button onclick="locationToReturnsByUserId('<?php echo $main_url; ?>/shop/returns-manager', '<?php echo $_GET['user_id']?>')"><?php echo translate_str_by_id(5578); ?></button>
                        </div>
                    </div>
                </div>
        </div>
    </div>
    <script src="/<?php echo $DP_Config->backend_dir; ?>/content/users/statistics/assets/main.js"></script>
<?php
?>