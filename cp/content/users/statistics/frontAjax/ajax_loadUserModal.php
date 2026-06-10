<?php
    header('Content-Type: application/json;charset=utf-8;');
    //Конфигурация CMS
    require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
    $DP_Config = new DP_Config;

    //Подключение к БД
    try
    {
        $db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
    }
    catch (PDOException $e)
    {
        $answer = array();
        $answer['status'] = false;
        $answer['message'] = "No DB connect";
        exit( json_encode($answer) );
    }
    $db_link->query("SET NAMES utf8;");
	
	// -------------------------------------------------------------------------------
	//Подключение мультиязычности
	require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
	$multilang_params = multilang_init();
	// -------------------------------------------------------------------------------
	
    // -------------------------------------------------------------------------------
    //Защита от CSRF-атак
    require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
    // -------------------------------------------------------------------------------

    //Для работы с пользователями
    require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

    $customer_id = $_POST['customer_id'];

    $INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = ".(int)$customer_id." AND `income`=1 AND `active` = 1), 0)";
    $ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = ".(int)$customer_id." AND `income`=0 AND `active` = 1),0)";

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
    $main_url = $DP_Config->domain_path.$DP_Config->backend_dir;
    $profile = DP_User::getUserProfileById($customer_id);
    $select_group = $db_link->prepare("SELECT `value` FROM `groups` WHERE `id` = ?;");
    $select_group->execute([$profile['groups'][0]]);
    $select_group_result = $select_group->fetch();
    ob_start();

?>
    <div class="customer-modal-info-block">
        <div class="customer-modal-info-block-header">
            <div>
                <?php echo translate_str_by_id(5579); ?>
            </div>
            <i class="far fa-window-close" id="close-customer-modal-info-<?=$customer_id;?>"></i>

        </div>
        <div class="customer-modal-info-block-main-content">
            <div class="customer-modal-info-block-left-side">
				<a class="btn btn-xs btn-primary" target="_blank" href="<?=$main_url;?>/users/usermanager/user?user_id=<?=$customer_id;?>"><i class="fa fa-user"></i> <?php echo translate_str_by_id(5580); ?></a>
			</div>
            <div class="customer-modal-info-block-left-side">
                <h2 style="font-size: 16px;"><?php echo translate_str_by_id(5581); ?></h2>
                <div class="info-str chet">
                    <div class="info-title">ID:</div>
                    <div class="info-value"><?=$customer_id;?></div>
                </div>
                <div class="info-str">
                    <div class="info-title"><?php echo translate_str_by_id(3664); ?>:</div>
                    <div class="info-value"><?=$select_group_result['value'];?></div>
                </div>
                <?php if ( isset($profile['mail_field']) && $profile['mail_field'] != '' ) : ?>
                    <div class="info-str chet">
                        <div class="info-title"><?php echo translate_str_by_id(5582); ?>:</div>
                        <div class="info-value"><?=$profile['mail_field'];?></div>
                    </div>
                <?php endif; ?>
                <?php if ($profile['email'] != '') : ?>
                    <div class="info-str chet">
                        <div class="info-title">Email:</div>
                        <div class="info-value"><?=$profile['email'];?></div>
                    </div>
                <?php endif; ?>
                <?php if ($profile['phone'] != '') : ?>
                    <div class="info-str">
                        <div class="info-title"><?php echo translate_str_by_id(1312); ?>:</div>
                        <div class="info-value"><?=$profile['phone'];?></div>
                    </div>
                <?php endif; ?>
                <?php
                $select_user_profile = $db_link->prepare('SELECT `data_value`, (SELECT `caption` FROM `reg_fields` WHERE `users_profiles`.`data_key` = `name`) as `caption` FROM `users_profiles` WHERE `user_id` = ?;');
                $select_user_profile->execute([$customer_id]);
                $counter = 0;
                while ($select_user_profile_result = $select_user_profile->fetch())
				{
                    $counter++;
                    ?>
                    <div class="info-str <?=(($counter % 2) == 0) ? 'chet' : '';?>">
                        <div class="info-title"><?=$select_user_profile_result['caption'];?>:</div>
                        <div class="info-value"><?=$select_user_profile_result['data_value'];?></div>
                    </div>
                    <?php
                }

                $orders = DP_User::getUserOrdersById($customer_id);
                $cart = DP_User::getUserCartsById($customer_id);
                $cars = DP_User::getUserCarsById($customer_id);
                $operations = DP_User::getUserFinanceById($customer_id);
                $queries = DP_User::getUserQueriesById($customer_id);
                $returns = DP_User::getUserReturnsById($customer_id);
                ?>
            </div>
            <div class="customer-modal-info-block-right-side">
                <div class="customer-modal-info-block-left-side">
                    <h2 style="font-size: 16px;"><?php echo translate_str_by_id(5583); ?></h2>
                    <div class="info-str chet">
                        <div class="info-title"><?php echo translate_str_by_id(863); ?>:</div>
                        <div class="info-value">
                            <a href="javascript:void()" onclick="locationToOrdersByUserId('<?=$main_url;?>/shop/orders/orders', '<?=$customer_id;?>')">
                                <?php echo translate_str_by_id(3593); ?></a> <?=$orders['count'];?> <?php echo translate_str_by_id(4496); ?> <?=$orders['total'];?>. <?php echo translate_str_by_id(5563); ?>: <?=$orders['total_debt'];?>.
                        </div>
                    </div>
                    <div class="info-str">
                        <div class="info-title"><?php echo translate_str_by_id(4410); ?>:</div>
                        <div class="info-value"><a href="javascript:void()" onclick="locationToCartsByUserId('<?=$main_url;?>/shop/orders/carts', '<?=$customer_id;?>')"><?php echo translate_str_by_id(5571); ?></a> <?=$cart['count'];?> <?php echo translate_str_by_id(4496); ?> <?=$cart['total'];?>.</div>
                    </div>

                    <div class="info-str chet">
                        <div class="info-title"><?php echo translate_str_by_id(5140); ?>:</div>
                        <div class="info-value"><a href="javascript:void()" onclick="locationToStatisticsByUserId('<?=$main_url;?>/shop/statistika', '<?=$customer_id;?>', '<?=time() - (90 * 86400);?>', '<?=time();?>')">Всего запросов</a> <?=$queries['count'];?>.</div>
                    </div>
                    <div class="info-str ">
                        <div class="info-title"><?php echo translate_str_by_id(3249); ?>:</div>
                        <div class="info-value">
                            <a href="javascript:void()" onclick="locationToFinanceByUserId('<?=$main_url;?>/shop/finance/account_operations', '<?=$customer_id;?>')"><?php echo translate_str_by_id(5568); ?></a> <?=$operations['count_in'] + $operations['count_out'];?>: <br>
                            - <?php echo translate_str_by_id(5569); ?> <?=$operations['count_in'];?> <?php echo translate_str_by_id(4496); ?> <?=$operations['total_in'];?>; <br>
                            - <?php echo translate_str_by_id(5570); ?> <?=$operations['count_out'];?> <?php echo translate_str_by_id(4496); ?> <?=$operations['total_out'];?>; <br>
                            - <?php echo translate_str_by_id(4655); ?> <?=$balance;?>.
                        </div>
                    </div>

                    <div class="info-str chet">
                        <div class="info-title"><?php echo translate_str_by_id(4669); ?>:</div>
                        <div class="info-value"><?php echo translate_str_by_id(5576); ?> <?=$cars['count'];?>.</div>
                    </div>

                    <div class="info-str">
                        <div class="info-title"><?php echo translate_str_by_id(4030); ?>:</div>
                        <div class="info-value"><a href="javascript:void()" onclick="locationToReturnsByUserId('<?=$main_url;?>/shop/returns-manager', '<?=$customer_id;?>')"><?php echo translate_str_by_id(5577); ?></a> <?=$returns['count'];?> <?php echo translate_str_by_id(4496); ?> <?=$returns['total'];?>.</div>
                    </div>
                </div>
            </div>
        </div>
    </div>
<?php
    $modal = ob_get_clean();
    exit(json_encode(['modal' => $modal, 'status' => true]));
?>