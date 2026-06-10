<?php
/**
 * Скрипт модуля для баланса покупателя
*/
defined('_ASTEXE_') or die('No access');


//Получаем данные по валюте магазина
$stmt = $db_link->prepare('SELECT * FROM `shop_currencies` WHERE `iso_code` = :iso_code;');
$stmt->bindValue(':iso_code', $DP_Config->shop_currency);
$stmt->execute();
$currency_record = $stmt->fetch(PDO::FETCH_ASSOC);
$currency_sign = $currency_record["sign"];
//Строка для обозначения валюты
if($DP_Config->currency_show_mode == "no")
{
	$currency_indicator = "";
}
else if($DP_Config->currency_show_mode == "sign_before" || $DP_Config->currency_show_mode == "sign_after")
{
	$currency_indicator = $currency_sign;
}
else
{
	$currency_indicator = $currency_record["caption_short"];
}





require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();


if($user_id > 0)
{
    ?>
    <div class="epc-balance-panel epc-balance-panel--active">
        <div class="epc-balance-panel__icon"><i class="fa fa-credit-card" aria-hidden="true"></i></div>
        <div class="epc-balance-panel__content">
            <span class="epc-balance-panel__eyebrow">Customer account</span>
            <h2><?php echo translate_str_by_id(4655); ?></h2>
        <?php
		$stmt = $db_link->prepare('SELECT *,( IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = :user_id AND `income`=1 AND `active` = 1), 0) - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = :user_id AND `income`=0 AND `active` = 1),0) ) AS `balance` FROM `shop_users_accounting` WHERE `user_id` = :user_id AND `active` = 1;');
		$stmt->bindValue(':user_id', $user_id);
		$stmt->execute();
		$balance_record = $stmt->fetch(PDO::FETCH_ASSOC);
        
        $balance = $balance_record !== false ? $balance_record["balance"] : 0;
        if($balance == "")
        {
            $balance = 0;
        }
		
		
		//Строка с балансом:
		$balance = number_format($balance, 2, '.', '');
		//Индикатор валюты перед ценой
		if($DP_Config->currency_show_mode == "sign_before")
		{
			$balance = $currency_indicator." ".$balance;
		}
		//Индикатор валюты после цены
		else if($DP_Config->currency_show_mode == "sign_after" || $DP_Config->currency_show_mode == "short_name_after")
		{
			$balance = $balance." ".$currency_indicator;
		}
		?>
            <div class="epc-balance-panel__amount"><?php echo $balance; ?></div>
            <p>Your available account balance is shown here after login.</p>
        </div>
    </div>
    <?php
}
else
{
	?>
	<div class="epc-balance-panel epc-balance-panel--login">
		<div class="epc-balance-panel__icon"><i class="fa fa-lock" aria-hidden="true"></i></div>
		<div class="epc-balance-panel__content">
			<span class="epc-balance-panel__eyebrow">Secure customer area</span>
			<h2>Login required to view balance</h2>
			<p>Your balance is private account information. Please log in or register below to see your available credit, payments and account status.</p>
			<div class="epc-balance-panel__chips">
				<span><i class="fa fa-shield" aria-hidden="true"></i> Protected account data</span>
				<span><i class="fa fa-user" aria-hidden="true"></i> Customer only</span>
			</div>
		</div>
	</div>
	<?php
}
?>