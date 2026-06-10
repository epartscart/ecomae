<?php
/*
	Скрипт формирования основных стилий сайта
*/
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;

if( $_POST["key"] !== $DP_Config->secret_succession )
{
	$result["status"] = false;
	$result["message"] = "Forbidden";
	$result["code"] = 501;
	exit(json_encode($result));
}

/*
$dark 						- Темный цвет текста
$light 						- Светлый цвет текста

$main_bg 					- Основной цвет сайта
$main_color 				- Цвет текста на основном цвете сайта

$header_bg 					- Цвет шапки сайта
$header_color 				- Цвет текста шапки сайта

$navbar_bg 					- Цвет верхнего меню сайта
$navbar_color 				- Цвет текста верхнего меню сайта
$navbar_color_hover 		- Цвет текста при наведении верхнего меню сайта

$link_color 				- Цвет ссылок сайта
$link_color_hover 			- Цвет ссылок при наведении сайта

$container_type 			- Тип верски (блочная, на всю ширину)
*/

$dir = $_POST["dir"];// Папка шаблона
$data_value = json_decode($_POST["data_value"], true);// Настройки шаблона

// Некоторые параметры были убраны из этого шаблона поэтому выставляем их вручную что бы избежать ошибки
$data_value['main_style'] = 'dark';
$data_value['header_style'] = 'light';
$data_value['navbar_style'] = 'light';
$data_value['container_type'] = 'full';
$data_value['menu_category_show'] = '0';
$data_value['cnt_category_after_hidden'] = '0';
$data_value['header_color'] = '#ffffff';
$data_value['navbar_color'] = '#ffffff';

foreach($data_value as &$value){
	if($value[0] == '#'){
        $value = substr($value, 1);
    }
}

$bg_transparent_logo = (int) $data_value['bg_transparent_logo'];

$dark = '222'; // Цвет текста сайта
$light = 'fff';

$main_bg = $data_value['main_color'];
if($data_value['main_style'] == 'light'){
	$main_color = $dark;
}else{
	$main_color = $light;
}

$header_bg = $data_value['header_color'];
if($data_value['header_style'] == 'light'){
	$header_color = $dark;
}else{
	$header_color = $light;
}

$navbar_bg = $data_value['navbar_color'];
$navbar_bg_dark = $data_value['navbar_color'];
if($data_value['navbar_style'] == 'light'){
	$navbar_color = $dark;
	$navbar_color_hover = $main_bg;
	$navbar_bg_dark = "444";
}else if($data_value['navbar_style'] == 'dark'){
	$navbar_color = $light;
	$navbar_color_hover = $main_bg;
}else if($data_value['navbar_style'] == 'inverse'){
	if($data_value['main_style'] == 'light'){// Цвет шрифта на основном цвете
		$navbar_color = $dark;
		$navbar_color_hover = $light;
	}else{
		$navbar_color = $light;
		$navbar_color_hover = $dark;
	}
}

$link_color = $data_value['link_color'];// Цвет ссылок
$container_type = $data_value['container_type'];// Ширина контейнера

if($data_value['main_style'] == 'light'){// Цвет шрифта на основном цвете
	$link_color_hover = $dark;
}else{
	$link_color_hover = $main_bg;
}

if($data_value['schearch_line_bg']){// Цвет фона строки поиска
	$schearch_line_bg = $data_value['schearch_line_bg'];
}else{
	$schearch_line_bg = 'F5F5F5';
}


//---------------------------------------------------------------------------------------------------
	

// Формируем цвет градиента, используется в некоторых элементах, например в кнопках
$result = hexToRgb($main_bg);

foreach($result as &$item){
	$item -= 10;
	if($item < 0){
		$item = 0;
	}
}

$main_bg_gradient = rgbToHex(array($result['red'], $result['green'], $result['blue']));

// Перевод цвета из HEX в RGB
function hexToRgb($color){
    // Проверяем наличие # в начале, если есть, то отрезаем ее
    if($color[0] == '#'){
        $color = substr($color, 1);
    }
   
    // Разбираем строку на массив
    if(strlen($color) == 6){// если hex цвет в полной форме - 6 символов
        list($red, $green, $blue) = array(
            $color[0] . $color[1],
            $color[2] . $color[3],
            $color[4] . $color[5]
        );
    }elseif(strlen($cvet) == 3){// если hex цвет в сокращенной форме - 3 символа
        list($red, $green, $blue) = array(
            $color[0]. $color[0],
            $color[1]. $color[1],
            $color[2]. $color[2]
        );
    }else{
        return false; 
    }
 
    // Переводим шестнадцатиричные числа в десятичные
    $red = hexdec($red); 
    $green = hexdec($green);
    $blue = hexdec($blue);
     
    // Вернем результат
    return array(
        'red' => $red, 
        'green' => $green, 
        'blue' => $blue
    );
}

// Перевод цвета из RGB в HEX
function rgbToHex($color){
	$red = dechex($color[0]); 
    $green = dechex($color[1]);
    $blue = dechex($color[2]);
	
	if($red === '0'){
		$red = '00';
	}
	if($green === '0'){
		$green = '00';
	}
	if($blue === '0'){
		$blue = '00';
	}
	
    return $red . $green . $blue;
}


//---------------------------------------------------------------------------------------------------


// Формируем файл стилей

ob_start();

require_once($_SERVER["DOCUMENT_ROOT"]."/templates/$dir/assets/css/generate_style/style.css");

$content = ob_get_contents();

ob_end_clean();

/*
$f = fopen($_SERVER["DOCUMENT_ROOT"]."/templates/$dir/assets/css/style_color.css", 'w');
fwrite($f, $content);
fclose($f);
*/

$content = str_replace(array("\t","\r","\n","     ","    ","   ","  "),'',$content);

$f = fopen($_SERVER["DOCUMENT_ROOT"]."/templates/$dir/assets/css/style_all.css", 'w');
fwrite($f, file_get_contents($_SERVER["DOCUMENT_ROOT"]."/templates/$dir/assets/css/all.css"));
fwrite($f, $content);
fclose($f);

$result["status"] = true;
$result["message"] = "OK";
$result["code"] = 200;
exit(json_encode($result));
?>