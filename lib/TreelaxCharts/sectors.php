<?php
//Сначала настраиваем параметры или оставляем по умолчанию
$image_size=500;
if( isset($_GET["size"]))
{
	$image_size=$_GET["size"];//Значение стороны рисунка (квадрат) - исходный размер, который затем делится на 2 для плавности изображения
}
$base_angle=0;
if( isset($_GET["start_angle"]))
{
	$base_angle=$_GET["start_angle"];//Значение начального угла
}
$center_ellipse=15;
if( isset($_GET["inside_size"]))
{
	$center_ellipse=$_GET["inside_size"];//Размер внутреннего эллипса (большая полуось)
}
$slope=2;
if( isset($_GET["slope"]))
{
	$slope=$_GET["slope"];//Коэффициент наклона
	if($slope<1)$slope=1;//$slope - не должен быть меньше 1
}

$square = true;
if( isset( $_GET["square"] ) )
{
	if($_GET["square"] != NULL)
	{
		$square = $_GET["square"];
	}
}

$padding=$image_size*0.25;//Отспупы от краев (25% от размера исходного размера)
$scale=2;//Масштаб для выходного изображения (во сколько раз уменьшать для плавности линий)
$font_size=5;//Размер текст процентов (1-5)

//Формируем массив значений углов для каждого сектора и массив строк (содержащих проценты) для их последующего вывода
if( !isset($_GET['number']) )
{
	$_GET['number'] = 2;
}
$numberOfElements=(int)$_GET['number'];//Колличество элементов
$angles_array=array();//Массив с углами
$percents_array=array();//Массив со строками процентов
$isAnyPercents=0;//Проверяем, есть ли вообще голоса по данному опросу
for($i=0;$i<$numberOfElements;$i++)
{
	if( !isset($_GET["value$i"]) )
	{
		$_GET["value$i"] = 100;
	}
	
	array_push($angles_array, ((($_GET["value$i"])/100)*360));//Формируем массив со значениями углов
	array_push($percents_array, $_GET["value$i"]);//Формируем массив со строками процентов
	$isAnyPercents=$isAnyPercents+$percents_array[$i];//Суммируем все проценты
}


//Формируем массив центров секторов (x и y каждого сектора) и массив координат для текста редом с секторами
$angle_count=$base_angle;//Отсчет угла (от которого отмеряется текущий угол)
//$centers[][]=array();//Размер массива - колличество элементов (секторов)
$centers[] = array();
for($i=0;$i<$numberOfElements;$i++)
{
	$centers[$i] = array();
	
	//Определяем текущий r
	$sin=sin(deg2rad(($angles_array[$i]+$angle_count)-$angles_array[$i]/2));//Углы делим на два, т.к. нужна точка по середине
	$cos=cos(deg2rad(($angles_array[$i]+$angle_count)-$angles_array[$i]/2));//Углы делим на два, т.к. нужна точка по середине
	$angle_count=$angle_count+$angles_array[$i];//Передвигаем угол отсчета
	$a=$center_ellipse;//Большая полуось внутреннего эллипса
	$b=$a/$slope;//Малая полуось внутреннего эллипса
	//Находим радиус внутреннего эллипса для текущего сектора
	$r=($a*$b)/(sqrt((($b*$b)*($cos*$cos))+(($a*$a)*($sin*$sin))));//По уравнению эллипса
	//Формируем координаты каждого центра сектора
	$centers[$i]["x"]=($cos*$r+$image_size/2);//Координата по x для данного центра  эллипса
	$centers[$i]["y"]=($sin*$r+$image_size/2);//Координата по y для данного центра  эллипса
}


// create image
$image = imagecreatetruecolor($image_size, $image_size);//Создаем исходное изображение
require_once("colors.php");//Файл с определением цветов (подключаем после создания $image, т.к. цвета привязаны к $image)

imagefilledrectangle($image,0,0,$image_size,$image_size,$white);//Заполняем фон (белый)


//Делаем 3D эффект
$projection=((1/10)*$image_size);//Если коэффициент наклона от 1 до 2, то нужно эффект 3D уменьшить:
if($slope>=1 && $slope<2)
{
	$projection=$projection-($projection-$projection*($slope-1));//Вычитаем из $projection нужное колличество десятых частей
}
for ($j = $projection; $j > 0; $j--)//Передвижение по вертикали (Y)
{
	$angle_count=$base_angle;//Отсчет угла (от которого отмеряется текущий угол)
	for($i=0;$i<$numberOfElements;$i++)
	{
		if($angles_array[$i]==0)
		{continue;}//Если значение текущего сектора равно 0 (никто не голосовал), то пропускаем этот сектор
		imagefilledarc($image, (int)$centers[$i]["x"], (int)$centers[$i]["y"]+(int)$j, (int)(((int)$image_size-(int)$center_ellipse*2)-(int)$padding), (int)((int)((int)($image_size/(int)$slope)-(int)(($center_ellipse/(int)$slope)*2))-(int)$padding/(int)$slope), (int)$angle_count, (int)($angle_count+$angles_array[$i]), (int)$colors_array[$i][1], IMG_ARC_PIE);
		$angle_count=$angle_count+$angles_array[$i];//Увеличили угол отсчета
	}
}


//Рисуем верхнюю поверхность секторов
$angle_count=$base_angle;//Отсчет угла (от которого отмеряется текущий угол)
for($i=0;$i<$numberOfElements;$i++)
{
	if($angles_array[$i]==0)
	{continue;}//Если значение текущего сектора равно 0 (никто не голосовал), то пропускаем этот сектор
	imagefilledarc($image, (int)$centers[$i]["x"], (int)$centers[$i]["y"], ((int)$image_size-(int)$center_ellipse*2)-(int)$padding, (((int)$image_size/(int)$slope)-((int)$center_ellipse/(int)$slope)*2)-(int)$padding/(int)$slope, (int)$angle_count, (int)$angle_count+(int)$angles_array[$i] , (int)$colors_array[$i][0], IMG_ARC_PIE);
	$angle_count=$angle_count+$angles_array[$i];//Увеличили угол отсчета
}

/*
//ФОРМИРУЕМ ТЕКСТ
//ДАЛЕЕ ДЛЯ ТЕКСТА
$text_points[][]=array();//Массив для координат текста
$angle_count=$base_angle;//Отсчет угла (от которого отмеряется текущий угол)
for($i=0;$i<$numberOfElements;$i++)
{	
	$sin=sin(deg2rad(($angles_array[$i]+$angle_count)-$angles_array[$i]/2));//Углы делим на два, т.к. нужна точка по середине
	$cos=cos(deg2rad(($angles_array[$i]+$angle_count)-$angles_array[$i]/2));//Углы делим на два, т.к. нужна точка по середине
	$angle_count=$angle_count+$angles_array[$i];//Передвигаем угол отсчета
	
	$A=($image_size-$padding)/2;//Большая полуось внешнего эллипса
	$B=$A/$slope;//Малая полуось внешнего эллипса
	//Находим радиус внешнего эллипса (на краю сектора по середине)
	$R=(($A*$B)/(sqrt((($B*$B)*($cos*$cos))+(($A*$A)*($sin*$sin)))));//По уравнению эллипса
	//Находим длину и ширину текущей строки
	$str_width=strlen("$percents_array[$i]%")*imagefontwidth($font_size);//Умножаем длину символа в пикселях на колличество символов в строке
	$str_height=imagefontheight($font_size);//Определяем высоту строки в пикселях;
	//Формируем координаты каждого текста в соответствии с четвертью (четверти в обратном порядке по сраснению с традиционным из алгебры и геометрии - Y направлен вниз)
	if($cos>0 && $cos<=1)//Правее оси Y
	{
		if($sin>0 && $sin<=1)//I
		{
			$text_points[$i]["x"]=($cos*$A+$image_size/2);//Координата по x для данного текста
			$text_points[$i]["y"]=($sin*$B+$image_size/2)+$projection;//Координата по y для данного текста
		}
		else //IV
		{
			$text_points[$i]["x"]=($cos*$A+$image_size/2);//Координата по x для данного текста
			$text_points[$i]["y"]=($sin*$B+$image_size/2)-$str_height;//Координата по y для данного текста
		}
	}
	else//Левее оси Y (включительно)
	{
		if($sin>0 && $sin<=1)//II
		{
			$text_points[$i]["x"]=($cos*$A+$image_size/2)-$str_width;//Координата по x для данного текста
			$text_points[$i]["y"]=($sin*$B+$image_size/2)+$projection;//Координата по y для данного текста
		}
		else //III
		{
			$text_points[$i]["x"]=($cos*$A+$image_size/2)-$str_width;//Координата по x для данного текста
			$text_points[$i]["y"]=($sin*$B+$image_size/2)-$str_height;//Координата по y для данного текста
		}
	}
}

//Выводим текст
$angle_count=$base_angle;//Отсчет угла (от которого отмеряется текущий угол)
for($i=0;$i<$numberOfElements;$i++)
{
	if($percents_array[$i]==0)
	{continue;}//Если значение текущего сектора равно 0 (никто не голосовал), то пропускаем этот сектор
	imagestring($image, $font_size, $text_points[$i]["x"], $text_points[$i]["y"], "$percents_array[$i]%", $black);
}
*/


//Если сумма всех процентов равна 0 (еще никто не голосовал), то просто выводим пустой эллипс с текстом (еще небыло голосов)
if($isAnyPercents==0)
{
	//3D:
	for ($j = $projection; $j > 0; $j--)//Передвижение по вертикали (Y)
	{
		imagefilledarc($image, $image_size/2, $image_size/2+$j, $image_size-$padding, ($image_size/$slope)-$padding/$slope, 0, 360 , $empty_sectors[1], IMG_ARC_PIE);
	}
	//Верхняя часть:
	imagefilledarc($image, $image_size/2, $image_size/2, $image_size-$padding, ($image_size/$slope)-$padding/$slope, 0, 360, $empty_sectors[0], IMG_ARC_PIE);
}



if($square == true)
{
    //Квадратный вариант
    $image_to_show=imagecreatetruecolor(($image_size/$scale),($image_size/$scale));//Создаем изображение для вывода
    imagecopyresampled($image_to_show, $image, 0,0,0,0,($image_size/$scale),($image_size/$scale),$image_size,$image_size);//Копируем с масштабом в 2 раза меньше
}
else
{
    //Прямоугольный вариант
    $image_to_show=imagecreatetruecolor(($image_size/$scale),($image_size/$scale/1.5));//Создаем изображение для вывода
    imagecopyresampled($image_to_show, $image, 0,0,0,70,($image_size/$scale),($image_size/$scale),$image_size,$image_size);//Копируем с масштабом в 2 раза меньше
}




imagecolortransparent ($image_to_show, $white);//Делаем прозрачным белый цвет

//Выводим изображение
header('Content-type: image/png');
imagepng($image_to_show);
imagedestroy($image_to_show);
?> 