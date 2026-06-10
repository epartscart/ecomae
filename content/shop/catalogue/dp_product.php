<?php
/**
 * Определение класса продукта
*/
class DP_Product
{
    //Данные из таблицы shop_catalogue_products
    public $id;
    public $category_id;
    public $caption;
    public $alias;
    public $title_tag;
    public $description_tag;
    public $keyword_tag;
    public $robots_tag;
    
    //Изображения (список id изображений)
    public $images = array();
    
    //Магазины (список объектов "Магазин", в каждом из которых список объектов "Склад", в котором информация о наличии и цене)
    public $offices = array();
}
?>