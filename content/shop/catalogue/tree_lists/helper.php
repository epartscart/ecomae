<?php
/**
Скрирт для опеределения функций - чтобы исключить повторное определение (redeclare)
*/

// --------------------------------- Start PHP - метод ---------------------------------
//Метод добавит элемент в массив
function addItemToDump($item, $candidate_data, $candidate_id)
{
    //Знаем уровень level
    if($item->parent == $candidate_id)
    {
        array_push($candidate_data, $item);
        
        return $candidate_data;//Возвращаем массив с добавленной категорией
    }
    else
    {
        for($i=0; $i < count($candidate_data); $i++)
        {
            if($candidate_data[$i]->count == 0)
            {
                continue;
            }
            $current_count = count($candidate_data[$i]->data);//Сколько элементов в массиве до рекурсивного вызова
            $candidate_data[$i]->data = addItemToDump($item, $candidate_data[$i]->data, $candidate_data[$i]->id);
        }
        return $candidate_data;
    }
}
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - метод ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
?>