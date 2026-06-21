<?php
/**
 * Единый скрипт фунции добавления товаров в корзину
*/

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();
?>

<script>
//Обработка кнопки "Купить"
function purchase_action(div_id)
{
    let product_object_div = document.getElementById(div_id);
    
    //console.log('product_object_div:');
    //console.log(product_object_div);
    
    let product_object = new Object;//Объект продукта, который добавляем в корзину
    product_object.product_type = 1;//Каталожный продукт
    product_object.product_id = product_object_div.getAttribute("product_id");
    product_object.office_id = product_object_div.getAttribute("office_id");
    product_object.storage_id = product_object_div.getAttribute("storage_id");
    product_object.storage_record_id = product_object_div.getAttribute("storage_record_id");
    product_object.price = product_object_div.getAttribute("price");
    product_object.time_to_exe = product_object_div.getAttribute("time_to_exe");
    product_object.time_to_exe_guaranteed = product_object_div.getAttribute("time_to_exe");
    product_object.exist = product_object_div.getAttribute("exist");
    product_object.check_hash = product_object_div.getAttribute("check_hash");
	
	//Текущее количество
	let current_count_need = 1;
	if($(".count_need_"+div_id)){
		current_count_need = parseInt($(".count_need_"+div_id).val());
	}
	if(current_count_need > 0){
		product_object.count_need = current_count_need;
	}else{
		product_object.count_need = 1;
	}
	
    //Данные в корзину можно класть сразу целым перечнем - поэтому приводим к массиву
    let product_objects = new Array;
    product_objects.push(product_object);
	
    jQuery.ajax({
        type: "POST",
        async: false, //Запрос синхронный
        url: "/content/shop/order_process/ajax_add_to_basket.php",
        dataType: "json",//Тип возвращаемого значения
        data: "product_objects="+encodeURI(JSON.stringify(product_objects))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
        success: function(answer)
        {
            if(answer.status == true)
            {
                //alert("Добавлено");
                //location = "/shop/cart";
				
				updateCartInfo();//Обновление корзины снизу
				showAdded();//Показываем лэйбл снизу
            }
            else
            {
                if(answer.code == "already")
                {
                    alert("<?php echo translate_str_by_id(4336); ?>");
                }
                else
                {
                    alert("<?php echo translate_str_by_id(4524); ?>");
                }
            }
        }
    });
}

//Функция добавления требуемого количества
function plusCountNeed(product_record_id, count, min_count)
{
	if(min_count === undefined){
		min_count = 1;
	}
	
	//Текущее количество
	let current_count_need = parseInt($(".count_need_"+product_record_id).val()) + parseInt(min_count);
	
	//Если максимальное количество на складе 0 то поставим 1
	if(count < 1){
		count = 1;
	}
	
	//Если не привышено максимальное значение то увеличиваем
	if(current_count_need <= count){
		$(".count_need_"+product_record_id).val(current_count_need);
	}else{
		//alert("Превышено наличие на складе");
	}
}
	
//Функция вычитания требуемого количества
function minusCountNeed(product_record_id, count, min_count)
{
	if(min_count === undefined){
		min_count = 1;
	}
	
	//Текущее количество
	let current_count_need = parseInt($(".count_need_"+product_record_id).val()) - parseInt(min_count);
	
	//Если максимальное количество на складе 0 то поставим 1
	if(count < 1){
		count = 1;
	}
	
	//Если не привышено максимальное значение то увеличиваем
	if(current_count_need >= parseInt(min_count)){
		$(".count_need_"+product_record_id).val(current_count_need);
	}else{
		//alert("Ошибка уменьшения количества");
	}
}
	
//Функция изменения количества при ручном вводе в поле
function onKeyUpCountNeed(product_record_id, count, count_min)
{
	if(count_min === undefined){
		count_min = 1;
	}
	
	//Текущее количество
	let current_count_need = parseInt($(".count_need_"+product_record_id).val());
	
	//Если введено допустимое значение
	if((current_count_need <= count && current_count_need >= count_min) && ((getDecimal((current_count_need / count_min))*1) == 0))
	{
		
	}
	else//Просто исправляем обратно
	{
		alert("<?php echo translate_str_by_id(4313); ?>");
		$(".count_need_"+product_record_id).val(count_min);
	}
}

// Возвращает дробную часть
function getDecimal(num) {
	let str = "" + num;
	let zeroPos = str.indexOf(".");
	if (zeroPos == -1) return 0;
	str = str.slice(zeroPos);
	return +str;
}
</script>