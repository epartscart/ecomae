<?php
/**
 * Модальное окно для страницы одного заказа
*/
defined('_ASTEXE_') or die('No access');
?>
<style>
#my_modal .modal-header, h4, .close {
  background-color: #00E5EE;
  text-align: center;
  font-size: 30px;
  background: #5b656e;
  border: 3px solid #FFF;
}
#my_modal h4{
  border:none;
  font-size: 25px;
}
#my_modal .modal-footer {
  background: #5b656e;
	border: 3px solid #FFF;
}
</style>
<div id="my_modal" class="container">
  <div class="modal fade" id="Modal1" role="dialog">
	<div class="modal-dialog">
 
	  <div class="modal-content">
		<div class="modal-header" style="padding:10px 15px;">
		  <b style="position: relative; top: 6px;"><?php echo translate_str_by_id(5301); ?></b>
		  <button type="button" class="close" data-dismiss="modal">&times;</button>
		  <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(5322); ?>');"><i class="fa fa-info"></i></button>
		</div>
		<div class="modal-body" style="color:#666; padding:40px 50px;">
			<div class="row">
				<div class="row">
					<div class="col-lg-4">
						<label><?php echo translate_str_by_id(2752); ?>:</label><br/>
						<select onchange="retun_reload();" id="retun_exist_select" class="form-control"></select>
					</div>
					<div class="col-lg-8">
						<label><?php echo translate_str_by_id(2081); ?>:</label><br/>
						<select onchange="retun_reload();" id="retun_status_select" class="form-control"></select>
					</div>
					<div class="col-lg-12 hidden">
						<table style="margin-top:15px; margin-bottom:0px;">
							<tr>
								<td><input disabled="true" style="width:25px; height:25px; cursor:pointer;" type="checkbox" id="retun_money"/></td>
								<td style="line-height: 1.2em; padding: 2px 5px 0px 5px; width: 100%;"><label style="cursor:pointer; margin:0;" for="retun_money"><?php echo translate_str_by_id(5323); ?></label></td>
							</tr>
							<tr>
								<td id="retun_money_small" colspan="2" style="padding: 0px 7px 1px;"></td>
							</tr>
						</table>
					</div>
				</div>
			</div>
		</div>
		<div class="modal-footer">
			<div id="retun_loader" style="display:none;">
				<img style="height: 31px; margin-right: 5px; margin-top: 2px;" src="/content/files/images/ajax-loader-transparent.gif"/>
				<a disabled class="btn btn-success pull-right"><?php echo translate_str_by_id(5388); ?></a>
			</div>
			<a id="retun_next" onClick="retun_next();" class="btn btn-success pull-right"><?php echo translate_str_by_id(4388); ?></a>
		</div>
	  </div>
 
	</div>
  </div>
</div>
<script>
<?php
$items_statuses = array();
$query = $db_link->prepare('SELECT * FROM `shop_orders_items_statuses_ref` ORDER BY `order`;');
$query->execute();
while($record = $query->fetch()){
	$record["name"] = translate_str_by_id($record["name"]);
	$items_statuses[$record['id']] = $record;
}
?>
var items_statuses = JSON.parse('<?=json_encode($items_statuses);?>');// Статусы позиций
var retun_item_id = 0;// ID позиции
var retun_paid_sum = 0;// Оплачено клиентом
var retun_price = 0;// Стоимомость позиции
var retun_count = 0;// Количество позиции
var retun_status = 0;// Статус позиции
var original_status = 0;// Статус позиции изначальный
function btn_modal_clicked(item_id, paid_sum, price, count, status){
	
	retun_item_id = item_id;
	retun_paid_sum = paid_sum;
	retun_price = price;
	retun_count = count;
	retun_status = status;
	original_status = status;
	
	let options = '<option selected value="'+count+'">Все</option>';
	for(let i = 1; i < count; i++){
		options += '<option value="'+i+'">'+i+'</option>';
	}
	document.getElementById('retun_exist_select').innerHTML = options;
	
	options = '';
	for (let status_id in items_statuses){
		let selected = '';
		if((status_id*1) === retun_status){
			selected = 'selected';
		}
		options += '<option '+selected+' value="'+status_id+'">'+items_statuses[status_id].name+'</option>';
	}
	document.getElementById('retun_status_select').innerHTML = options;
	
	retun_reload();
	
	$("#Modal1").modal();
}
function retun_reload(){
	let n = 0;
	n = document.getElementById("retun_exist_select").options.selectedIndex;
	retun_count = document.getElementById("retun_exist_select").options[n].value;
	
	n = document.getElementById("retun_status_select").options.selectedIndex;
	retun_status = document.getElementById("retun_status_select").options[n].value;
	
	// Если статус отмененный, отображаем интерфейс возврата средств на баланс
	if(items_statuses[retun_status].count_flag == 0){
		if(items_statuses[original_status].count_flag == 0){
			document.getElementById('retun_money').checked = false;
			document.getElementById('retun_money').disabled = true;
			document.getElementById('retun_money_small').style.background = '#ff6d6d';
			document.getElementById('retun_money_small').style.color = '#fff';
			document.getElementById('retun_money_small').innerHTML = '<small><?php echo translate_str_by_id(5324); ?></small>';
		}else{
			if(retun_paid_sum >= (retun_price * retun_count)){
				document.getElementById('retun_money').checked = true;
				document.getElementById('retun_money').disabled = false;
				document.getElementById('retun_money_small').style.background = '#62cb31';
				document.getElementById('retun_money_small').style.color = '#fff';
				document.getElementById('retun_money_small').innerHTML = '<small><?php echo translate_str_by_id(5325); ?></small>';
			}else{
				document.getElementById('retun_money').checked = false;
				document.getElementById('retun_money').disabled = true;
				document.getElementById('retun_money_small').style.background = '#ff6d6d';
				document.getElementById('retun_money_small').style.color = '#fff';
				document.getElementById('retun_money_small').innerHTML = '<small><?php echo translate_str_by_id(5326); ?></small>';
			}
		}
	}else{
		document.getElementById('retun_money').checked = false;
		document.getElementById('retun_money').disabled = true;
		document.getElementById('retun_money_small').style.background = '#eee';
		document.getElementById('retun_money_small').style.color = '#222';
		document.getElementById('retun_money_small').innerHTML = '<small><?php echo translate_str_by_id(5327); ?></small>';
	}
}
//Выставить статус для позиций заказа
function retun_next()
{
	//Список позиций
	var orders_items = new Array();
	orders_items.push(retun_item_id);
	if(orders_items.length == 0)
	{
		alert("<?php echo translate_str_by_id(5328); ?>");
		return;
	}
	
	//Выставляемый статус
	var needStatus = retun_status;
	
	//Возврат средств на баланс
	var retun_money = 0;
	if(document.getElementById('retun_money').checked){
		retun_money = 1;
	}
	
	//Количество позиций
	var needСount = retun_count;
	
	document.getElementById("retun_next").style.display = 'none';
	document.getElementById("retun_loader").style.display = 'inline-block';
	
	jQuery.ajax({
			type: "GET",
			async: false, //Запрос синхронный
			url: "/content/shop/protocol/set_order_item_status.php",
			dataType: "json",//Тип возвращаемого значения
			data: "retun=1&initiator=1&orders_items="+JSON.stringify(orders_items)+"&status="+needStatus+"&count="+needСount+"&retun_money="+retun_money+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer)
			{
				if(answer.status == true)
				{
					//Обновляем страницу
					location='/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $order_id; ?>&success_message='+encodeURI('<?php echo translate_str_by_id(2722); ?>');
				}
				else
				{
					if( typeof answer.message != undefined )
					{
						alert(answer.message);
					}
					else
					{
						alert("<?php echo translate_str_by_id(3560); ?>");
					}
					document.getElementById("retun_next").style.display = 'inline-block';
					document.getElementById("retun_loader").style.display = 'none';
				}
			},
			error: function(answer)
			{
				alert("Ошибка сервера");
				document.getElementById("retun_next").style.display = 'inline-block';
				document.getElementById("retun_loader").style.display = 'none';
			}
	});
}
</script>