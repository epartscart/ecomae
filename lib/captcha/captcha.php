<?php
/**
 * Скрипт реализует генерацию изображения капчи
 * Принцип работы: при генерации нового изображения, его код записывается в куки браузера
 * Проверка правильности: сравниваются две строки - то, что записанов в куки и то, что ввел пользователь
*/

// Устанавливаем переменные bg_dir и ttf_dir, которые примут значение пути к папке со шрифтами и (если потребуется) изображениями
define ( 'DOCUMENT_ROOT', dirname ( __FILE__ ) );
define("bg_dir", DOCUMENT_ROOT."/bg/"); // Если скрипт отказывается работать, то скорее всего ваш сервер не поддерживает $HTTP_SERVER_VARS. В таком случае, закомментируйте эту строчку и раскомментируйте следующую.
define("ttf_dir", DOCUMENT_ROOT."/ttf/"); // Если скрипт отказывается работать, то скорее всего ваш сервер не поддерживает $HTTP_SERVER_VARS. В таком случае, закомментируйте эту строчку и раскомментируйте следующую.


$captcha = generate_code();//Генерируем последовательность

// Вносим в куки хэш капчи. Куки будет жить 120 секунд.
$cookie = md5($captcha);
$cookietime = time()+120; // Можно указать любое другое время
setcookie("captcha", $cookie, $cookietime, "/");
img_code($captcha); // Выводим изображение


//Далее реализация функций:
// ----------------------------------------------------------------------------------------------------------
// Функция генерации набора символов капчи
function generate_code() 
{    
	$chars = 'abdefhknrstyz23456789'; // Задаем символы, используемые в капче. Разделитель использовать не надо.
	$length = rand(4, 7); // Задаем длину капчи, в нашем случае - от 4 до 7
	$numChars = strlen($chars); // Узнаем, сколько у нас задано символов
	$str = '';
	for ($i = 0; $i < $length; $i++) 
	{
		$str .= substr($chars, rand(1, $numChars) - 1, 1);
	} // Генерируем код
	
	// Перемешиваем, на всякий случай
	$array_mix = preg_split('//', $str, -1, PREG_SPLIT_NO_EMPTY);
	srand ((float)microtime()*1000000);
	shuffle ($array_mix);
	// Возвращаем полученный код
	return implode("", $array_mix);
}
// ----------------------------------------------------------------------------------------------------------
//Функция генерации изображения
function img_code($code) // $code - код нашей капчи, который мы укажем при вызове функции
{
    // Отправляем браузеру Header'ы
	header("Expires: Mon, 26 Jul 1997 05:00:00 GMT");                   
	header("Last-Modified: " . gmdate("D, d M Y H:i:s", 10000) . " GMT");
	header("Cache-Control: no-store, no-cache, must-revalidate");         
	header("Cache-Control: post-check=0, pre-check=0", false);           
	header("Pragma: no-cache");                                           
	header("Content-Type:image/png");
    // Количество линий. Обратите внимание, что они накладываться будут дважды (за текстом и на текст). Поставим рандомное значение, от 3 до 7.
    $linenum = rand(3, 7); 
    // Задаем фоны для капчи. Можете нарисовать свой и загрузить его в папку /img. Рекомендуемый размер - 150х70. Фонов может быть сколько угодно
	$img_arr = array();
	if ($dh = opendir(bg_dir)) 
	{
		while (($file_bg = readdir($dh)) !== false) 
		{
			if($file_bg == "." || $file_bg == "..") continue;
			array_push($img_arr, $file_bg);
		}
		closedir($dh);
	}
    // Шрифты для капчи. Задавать можно сколько угодно, они будут выбираться случайно
	$font_ob = array("fname" => "AGENCYR.TTF", "size" => rand(20, 30));
	/*$font_arr = array();
	if ($dh = opendir(ttf_dir)) 
	{
		while (($file_ttf = readdir($dh)) !== false) 
		{
			if($file_ttf == "." || $file_ttf == "..") continue;
			array_push($font_arr, array("fname" => $file_ttf, "size" => rand(20, 30)));
		}
		closedir($dh);
	}*/
	
	
    // Генерируем "подстилку" для капчи со случайным фоном
	//$n = rand(0,sizeof($font_arr)-1);
	$n = 1;
	$img_fn = $img_arr[rand(0, sizeof($img_arr)-1)];
	$im = imagecreatefrompng (bg_dir.$img_fn); 
    // Рисуем линии на подстилке
	for ($i=0; $i<$linenum; $i++)
	{
		$color = imagecolorallocate($im, rand(0, 150), rand(0, 100), rand(0, 150)); // Случайный цвет c изображения
		imageline($im, rand(0, 20), rand(1, 50), rand(150, 180), rand(1, 50), $color);
	}
	$color = imagecolorallocate($im, rand(0, 200), 0, rand(0, 200)); // Опять случайный цвет. Уже для текста.

    // Накладываем текст капчи				
	$x = rand(0, 35);
	for($i = 0; $i < strlen($code); $i++) 
	{
		$x+=15;
		$letter=substr($code, $i, 1);
		imagettftext ($im, $font_ob["size"], rand(2, 4), $x, rand(50, 55), $color, ttf_dir.$font_ob["fname"], $letter);
	}

    // Опять линии, уже сверху текста
	for ($i=0; $i<$linenum; $i++)
	{
		$color = imagecolorallocate($im, rand(0, 255), rand(0, 200), rand(0, 255));
		imageline($im, rand(0, 20), rand(1, 50), rand(150, 180), rand(1, 50), $color);
	}
    // Возвращаем получившееся изображение
	ImagePNG ($im);
	ImageDestroy ($im);
}
?>