<?php
//Общий алгоритм отправки уведомлений менеджерам при поступлении оплаты через интернет-эквайринг

//Для отправки уведомлений
require_once( $_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php" );


//Если операция привязана к магазину, то, отправка будет только менеджерам конкретного магазина
$operation_query = $db_link->prepare('SELECT `office_id` FROM `shop_users_accounting` WHERE `id` = ?;');
$operation_query->execute( array($operation_id) );
$office_record = $operation_query->fetch();
$office_id = $office_record['office_id'];

$office_binding_value = array();
$sub_sql = '';
if( $office_id > 0 )
{
	$office_binding_value[] = $office_id;
	
	$sub_sql = ' WHERE `id` = ? ';
}

//Получаем список менеджеров офиса
$persons = array();
$persons_id_filter = array();
$offices_query = $db_link->prepare('SELECT * FROM `shop_offices` '.$sub_sql.';');
$offices_query->execute( $office_binding_value );
while( $office = $offices_query->fetch() ) 
{
	$users = json_decode($office["users"], true);
	
	for( $u=0 ; $u < count($users); $u++)
	{
		if( array_search((int)$users[$u], $persons_id_filter) !== false )
		{
			continue;
		}
		
		array_push($persons_id_filter, (int)$users[$u]);
		
		$persons[] = array('type'=>'user_id', 'user_id'=>(int)$users[$u]);
	}
}


$notify_vars = array();
$notify_vars['operation_id'] = $operation_id;
$notify_vars['amount'] = $amount;


//Отправляем уведомление (БЕЗ обработки результата)
send_notify('pay_by_site', $notify_vars, $persons);
?>