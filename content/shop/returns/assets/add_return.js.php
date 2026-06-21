<?php
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
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------
?>
let total_file_size = 0;
function check_return_data(formElem) {
    let form = new FormData(formElem);
    let flag = {
        status:true,
        message:""
    };
    let available_format = ['image/png', 'image/jpeg', 'image/jpg','image/bmp'];
    form.getAll('images[]').forEach(el => {
        if (!available_format.includes(el.type))
        {
            flag.message = "<?php echo translate_str_by_id(4924); ?>"
            flag.status = false;
            return flag;
        }

        if (el.size > 5242880)
        {
            flag.message = "<?php echo translate_str_by_id(4925); ?>"
            flag.status = false;
            return flag;
        }
        total_file_size = total_file_size + el.size;
    });


    if (total_file_size > 15728640)
    {
        flag.message = "<?php echo translate_str_by_id(4926); ?>"
        flag.status = false;
        return flag;
    }

    if (form.get('reason_id') == -1 || form.getAll('images[]')[0].name == '' || form.get('comment') == "")
    {
        flag.message = "<?php echo translate_str_by_id(3127); ?>.";
        flag.status = false;
        return flag;
    }
    return flag;
}
async function fetchDataReturn(forms, count_arr, total_sum) {
	let formElem = document.querySelector("#return_info");
    let request_body = new FormData(formElem);

    forms.forEach((el,key) => {
        let sub_data = new FormData(el);
        request_body.append("items["+key+"][item_id]", sub_data.get("item_id"));
        request_body.append("items["+key+"][reason_id]", sub_data.get("reason_id"));
        request_body.append("items["+key+"][comment]", sub_data.get("comment"));
				request_body.append("items["+key+"][count]", count_arr[sub_data.get("item_id")]);
        sub_data.getAll('images[]').forEach((image, index) => {
            request_body.append("images["+sub_data.get('item_id')+"]["+index+"]", image);
        });
    });

		request_body.append("total_sum", total_sum);

    let response = await fetch(main_url + 'content/shop/returns/ajax/ajax_load_returns_data.php', {
        method: 'POST',
        body: request_body
    });
    let json = await response.json();

    if (json.status != false)
        location = main_url + "<?php echo $multilang_params['lang_href_slash_after']; ?>shop/returns/returns_list";
    else
    {
        alert(json.error_message);
				jQuery("#btn_confirm_return").prop('disabled', false);
    }

}

function getItemsIdsFromUrl() {
	let params = window
			.location
			.search
			.replace('?','')
			.split('&')
			.reduce(
					function(p,e){
							var a = e.split('=');
							p[ decodeURIComponent(a[0])] = decodeURIComponent(a[1]);
							return p;
					},
					{}
			);

	return JSON.parse(params['items']);
}

function check_val() {
	let total_price = 0;
	let ids = getItemsIdsFromUrl();

	ids.forEach((id) => {
			let max_count = Number(document.querySelector('#item_count_' + id).dataset.count);
			let price = Number(document.querySelector('#item_price_' + id).value);
			let count = Number(document.querySelector('#item_count_' + id).value);
			if (max_count < count || count <= 0)
			{
					document.querySelector('#item_count_' + id).value = max_count;
					document.getElementById("total_price_span").textContent = main_total_price;
					total_price += price * max_count;
			}
			else
					total_price += price * count;
	});

	document.getElementById("total_price_span").textContent = total_price - (total_price / 100) * retention_percentage;
}

function confirm_return() {
		jQuery("#btn_confirm_return").prop('disabled', true);
    let ids = getItemsIdsFromUrl();
    let count_need_arr = {};
    ids.forEach((item_id) => {
        count_need_arr[item_id] = document.querySelector('#item_count_' + item_id).value;
    });

    let total_sum = document.querySelector('#total_price_span').textContent;

    total_file_size = 0;
    let flag = true;
    let message = "";
    let forms = document.querySelectorAll('.return_options_data');
    forms.forEach(el => {
        let check_obj = check_return_data(el);
        if (check_obj.status === false)
        {
            flag = false;
            message = check_obj.message;
        }
    });

		if (flag) {
			fetchDataReturn(forms, count_need_arr, total_sum);
		}
	else
	{
			 alert(message);
			jQuery("#btn_confirm_return").prop('disabled', false);
	}
}