<?php
/*
Определение класса элемента древовидного списка
*/

// --------------------------------- Start PHP - класс ---------------------------------
//Класс категория товара
class DP_TreeListItem
{
    public $id;//ID элемента
    public $count;//Количество вложенных элементов
    public $level;//Уровень вложенности
    public $value;//Значение
    public $value_lang_str_id;//ID строки с мультиязычностью
    public $parent;//ID родителя
    public $image;//Имя изображения
    public $order;//Порядок отображения
	public $open;//Узел раскрыт в дереве редактора
	
	public $image_url;//Переменная для хранения изображения при работе в браузере
	
	public $is_new;//Флаг - является новым элементом. Для удобства работы в редакторе
    
    public $data = array();//Массив с вложенными объектами категорий (в таблице БД это поле отсутствует)
    
	public $alias;
	public $url;
	
    public function __construct()
    {
        $this->id = 0;
       
        $this->count = 0;
        $this->level = 0;
        $this->value = "";
        $this->value_lang_str_id = 0;
        $this->parent = 0;
        $this->image = "";
		$this->image_url = "";
		$this->order = 0;
		$this->is_new = 0;
		$this->open = true;
		$this->alias = "";
		$this->url = "";
    }
}//~class DP_CatalogueCategory
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - класс ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
?>