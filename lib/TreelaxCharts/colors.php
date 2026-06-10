<?php
//Определяем цвета:
$white    = imagecolorallocate($image, 255, 255, 255);
$black    = imagecolorallocate($image, 0, 0, 0);

$empty_sectors = array();

$empty_sectors[0] = imagecolorallocate($image, 245, 245, 245);
$empty_sectors[1] = imagecolorallocate($image, 235, 235, 235);


$colors_array = array();
for( $c = 0 ; $c < 25; $c++ )
{
	$colors_array[$c] = array();
}


$colors_array[0][0] = imagecolorallocate($image, 139, 196, 78);//Зеленый
$colors_array[0][1] = imagecolorallocate($image, 100, 150, 50);

$colors_array[1][0] = imagecolorallocate($image, 240, 240, 240);//Серый
$colors_array[1][1] = imagecolorallocate($image, 200, 200, 200);

$colors_array[2][0]  = imagecolorallocate($image, 115, 138, 64);//Красный
$colors_array[2][1]  = imagecolorallocate($image, 70, 85, 39);

$colors_array[3][0]  = imagecolorallocate($image, 94, 72, 119);//Зеленый
$colors_array[3][1]  = imagecolorallocate($image, 47, 36, 60);

$colors_array[4][0] = imagecolorallocate($image, 53, 127, 147);//Светло-зеленый
$colors_array[4][1] = imagecolorallocate($image, 21, 51, 59);

$colors_array[5][0] = imagecolorallocate($image, 184, 111, 49);//Синий
$colors_array[5][1] = imagecolorallocate($image, 74, 44, 20);

$colors_array[6][0]  = imagecolorallocate($image, 68, 113, 165);//Светло-оранжевый
$colors_array[6][1]  = imagecolorallocate($image, 27, 45, 66);

$colors_array[7][0]  = imagecolorallocate($image, 168, 69, 66);//Синий
$colors_array[7][1]  = imagecolorallocate($image, 68, 28, 26);

$colors_array[8][0]  = imagecolorallocate($image, 135, 163, 77);//Желтый
$colors_array[8][1]  = imagecolorallocate($image, 54, 66, 31);

$colors_array[9][0]  = imagecolorallocate($image, 112, 87, 141);//Желтый
$colors_array[9][1]  = imagecolorallocate($image, 45, 35, 57);

$colors_array[10][0]  = imagecolorallocate($image, 64, 150, 173);//Морской
$colors_array[10][1]  = imagecolorallocate($image, 26, 60, 70);

$colors_array[11][0]  = imagecolorallocate($image, 217, 130, 60);//Оранжерый
$colors_array[11][1]  = imagecolorallocate($image, 94, 57, 26);

$colors_array[12][0]  = imagecolorallocate($image, 78, 127, 187);//Синий
$colors_array[12][1]  = imagecolorallocate($image, 42, 69, 102);

$colors_array[13][0]  = imagecolorallocate($image, 190, 79, 76);//Зеленый
$colors_array[13][1]  = imagecolorallocate($image, 124, 51, 49);

$colors_array[14][0]  = imagecolorallocate($image, 153, 185, 88);//Сиреневый
$colors_array[14][1]  = imagecolorallocate($image, 113, 137, 65);

$colors_array[15][0]  = imagecolorallocate($image, 126, 99, 160);//Светло-желтый
$colors_array[15][1]  = imagecolorallocate($image, 84, 65, 106);

$colors_array[16][0]  = imagecolorallocate($image, 74, 170, 196);//Розовый
$colors_array[16][1]  = imagecolorallocate($image, 41, 95, 109);

$colors_array[17][0]  = imagecolorallocate($image, 244, 148, 69);//Светло-оранжевый
$colors_array[17][1]  = imagecolorallocate($image, 108, 66, 30);

$colors_array[18][0]  = imagecolorallocate($image, 145, 167, 205);//Светло-красный
$colors_array[18][1]  = imagecolorallocate($image, 58, 67, 82);

$colors_array[19][0]  = imagecolorallocate($image, 207, 145, 144);//Зеленый
$colors_array[19][1]  = imagecolorallocate($image, 83, 58, 58);

$colors_array[20][0]  = imagecolorallocate($image, 183, 203, 148);//Синий
$colors_array[20][1]  = imagecolorallocate($image, 74, 82, 60);

$colors_array[21][0]  = imagecolorallocate($image, 167, 153, 187);//Желто-зеленый
$colors_array[21][1]  = imagecolorallocate($image, 67, 62, 75);

$colors_array[22][0]  = imagecolorallocate($image, 143, 193, 211);//Красный
$colors_array[22][1]  = imagecolorallocate($image, 58, 78, 85);

$colors_array[23][0]  = imagecolorallocate($image, 246, 179, 142);//Сиреневый
$colors_array[23][1]  = imagecolorallocate($image, 99, 72, 57);

$colors_array[24][0]  = imagecolorallocate($image, 186, 198, 221);//Оранжевый
$colors_array[24][1]  = imagecolorallocate($image, 75, 80, 89);
?>