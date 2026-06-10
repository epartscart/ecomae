<?php
/**
 * Плагин для обработки содержимого корзины
 * 
 * Если пользователь Авторизован (главным образом, если пользователь авторизовался в этом же переходе - в плагине аутентификации):
 * - если в куки корзины есть товары - записываем в таблицу корзины id пользователя, а из куки эти товары удаляем. Т.о. эти товары теперь относятся к конкретному пользователю
 * 
 * Если пользователь не авторизован - пока ничего не делаем
 * 
*/
/*
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

$user_id = DP_User::getUserId();

//Пользователь авторизован
if($user_id > 0)
{
    //В куки корзины есть товары
    if( isset( $_COOKIE["products_in_cart"] ))
    {
        $products_in_cart_cookie = json_decode($_COOKIE["products_in_cart"], true);
        
		if( count($products_in_cart_cookie) > 0 )
		{
			for($i=0; $i < count($products_in_cart_cookie); $i++)
			{
				if( $products_in_cart_cookie[$i] != (int)$products_in_cart_cookie[$i] )
				{
					continue;
				}
				
				$stmt = $db_link->prepare('UPDATE `shop_carts` SET `user_id` = :user_id WHERE `id`=:id AND `user_id`=0;');
				$stmt->bindValue(':user_id', $user_id);
				$stmt->bindValue(':id', $products_in_cart_cookie[$i]);
				$stmt->execute();
			}
		}
    }
    
    //Удаляем куки корзины
    setcookie("products_in_cart", "", time() - 3600, "/");
}
*/
?>