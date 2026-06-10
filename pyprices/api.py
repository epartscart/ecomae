#!C:/xampp/htdocs/pyprices/Scripts/python.exe
# Windows/XAMPP: use venv Python above. On Linux, replace with e.g. #!/path/to/pyprices/bin/python

# --------------------------------------------------------------

#Для MySQL
import mysql.connector
from mysql.connector import errorcode

#Системные
import cgi
import sys
import json
import time

#Для работы с файлами и директориями
import shutil
import os
os.environ['OPENBLAS_NUM_THREADS'] = '1' #Без этого не шли тестовые запросы по URL к самому себе на sweb
from os import listdir
from os.path import isfile, join

#Свои
import item_to_handle #Класс задания на обработку (один элемент массива заданий)
import docpart_imap #Класс работы с почтой
import file_receiver #Классы для получения файлов
import price_file_handler #Класс обработки одного файла из задания

# --------------------------------------------------------------
# Необходимо для корректной работы с Apache
print("Content-Type: text/html\n")
# --------------------------------------------------------------
#Объявляем переменные до объявления функций

post_data = {} #Сюда будут записаны POST-параметры
imap_status = None #Статус подключения к почте
list_to_handle = [] #Массив заданий
list_to_handle_incorrect = [] #Массив заданий, которые не прошли валидацию (тоже будет возвращен - можно будет посмотреть, что не так с кажды объектом)
list_to_handle_email = {} #Сгруппированный массив (словарь) для получения файлов с почты. Группируются по адресу отправителя

#Отдельный от объектов заданий, список для ошибок
errors_general_list = []

#ID запуска
launch_id = 0

# --------------------------------------------------------------

#Функция получения нужного параметра из config.php. param - строка с именем нужного параметра из config.php
def get_config_php_param(param):
    
    try:
        with open("../config.php", "r", encoding='utf-8') as config_php:
            config_php_content = config_php.read()
            
            #Получаем значение ключа из config.php
            config_php_content = config_php_content.split('public $'+param+' = \'')[1] #Получили то, что справа
            config_php_content = config_php_content.split('\'')[0] #Получили то, что слева
            
            return config_php_content
        
    except Exception:
        exit_pyprices(False, "Could not read CMS config")

# --------------------------------------------------------------

#Общая функция выхода
def exit_pyprices(status, message, to_del_launch_folder = True):
    
    #Структура ответа
    answer = {
        'status':status, 
        'message':message,
        'imap_status':imap_status,
        'list_to_handle':list_to_handle,
        'list_to_handle_incorrect':list_to_handle_incorrect,
        'list_to_handle_email':list_to_handle_email,
        'errors_general_list':errors_general_list
        }
    
    #Ответ в JSON-строке
    answer_str = json.dumps(answer, default=vars)
    
    
    #Удаляем папку запуска tmp/launch_id
    if to_del_launch_folder:
        #Если папка существует
        if os.path.isdir('tmp/' + str(launch_id) ):
            shutil.rmtree('tmp/' + str(launch_id), ignore_errors=True)
    
    

    #Перед выходом записываем данные в учетную запись запуска и отключаемся от БД
    if 'db_link' in globals() or 'db_link' in locals():
        if db_link.is_connected():
            cursor = db_link.cursor()
            try:
                cursor.execute("UPDATE `shop_docpart_pyprices_launches` SET `time_end` = %s, `is_normal_exit` = %s, `answer` = %s, `normal_exit_status` = %s, `passed` = %s WHERE `id` = %s", ( int(time.time()), 1, answer_str, status, True, launch_id ) )
                #Коммитим тразакцию, чтобы UPDATE применился
                db_link.commit()
            except Exception as err:
                pass
            
            #Закрываем соединение с БД
            db_link.close()
    
    
    #Выходим
    print( answer_str )
    exit()

# --------------------------------------------------------------

#Функция инициализации массива заданий
def init_list_to_handle(post_data):
    
    #В POST-аргументах должен быть аргумент list_to_handle
    if "list_to_handle" not in post_data:
        exit_pyprices(False, "No list_to_handle in POST")
    
    
    #Аругумент list_to_handle есть в POST-запросе. После json.loads() он должен стать массивом
    try:
        list_to_handle_loaded = json.loads(post_data['list_to_handle'].value)
    except json.decoder.JSONDecodeError:
        exit_pyprices(False, "Failed to parse list_to_handle from the received string into a Python variable")
    
    
    #Распарсили строку list_to_handle в Python-переменную. Теперь создаем объекты item_to_handle и добавляем их в массив заданий. Цикл по распарсенному массиву. task на итерации цикла - это еще не проверенный объект
    prices_id_appended = [] #Список price_id уже добавленных заданий (чтобы исключить дублирующиеся объекты) 
    for task in list_to_handle_loaded:
        #Создаем объект item_to_handle
        item = item_to_handle.item_to_handle(task, db_link, imap_status)
        #Связываем этот объект задания с данным запуском pyprices
        try:
            cursor = db_link.cursor()
            cursor.execute( "UPDATE `shop_docpart_pyprices_tasks` SET `pyprices_launche_id` = %s WHERE `id` = %s", ( launch_id, item.client_task_id ) )
            db_link.commit()
        except Exception as err:
            exit_pyprices(False, str(err))
        #Если объект получился корректным, добавляем его в список на обработку. Если не корректный - добавляем в список некорректных объектов для возможности его возврата - чтобы можно было посмотреть, что не так.
        if item.validated and item.price_id not in prices_id_appended:
            #Сам объект добавляем на обработку
            list_to_handle.append(item)
            #price_id из объекта добавляем в список добавленных (для фильтра дублирующихся price_id)
            prices_id_appended.append(item.price_id)
        else:
            if item.validated:
                item.validation_messages.append('The object will not be processed because the list_to_handle already has an object with the same price_id already added for processing')
            list_to_handle_incorrect.append(item)
        
# --------------------------------------------------------------

# Получаем POST-запрос (post_data - содержит POST-параметры). Для обращения к параметру нужно: post_data['param'].value
post_data = cgi.FieldStorage(environ=dict(os.environ, REQUEST_METHOD='POST'))

# --------------------------------------------------------------

# Первый делом проверяем наличие поля key (защита от постороннего доступа)
if "key" not in post_data:
    exit_pyprices(False, "No key")

#Ключ получен. Проверяем его корректность

#Получаем значение ключа из config.php
key_in_config_php = get_config_php_param('tech_key')

#Сравниваем с ключем из POST-запроса
if key_in_config_php != post_data['key'].value:
    exit_pyprices(False, "Incorrect key")

# --------------------------------------------------------------

#Инициализация рабочих параметров (настройки подключения к БД, подключение к своей почте)

work_params = {
    #Подключение к БД
    'db_host': get_config_php_param('host'),
    'db_user': get_config_php_param('user'),
    'db_password': get_config_php_param('password'),
    'db_name': get_config_php_param('db'),
    
    #Подключение к почте
    'prices_email_server': get_config_php_param('prices_email_server'),
    'prices_email_encryption': get_config_php_param('prices_email_encryption'),
    'prices_email_port': get_config_php_param('prices_email_port'),
    'prices_email_username': get_config_php_param('prices_email_username'),
    'prices_email_password': get_config_php_param('prices_email_password')
}


# --------------------------------------------------------------
#Подключаемся к БД. Если нет подключения к БД - сразу выходим, т.к. прайсы нужно импортировать в БД. Если нет подключения, то, не сможем импортировать.
try:
    db_link = mysql.connector.connect( user = work_params['db_user'], password = work_params['db_password'], host = work_params['db_host'], database = work_params['db_name'] )
except mysql.connector.Error as err:
    exit_pyprices(False, str(err))

# --------------------------------------------------------------

#Если это был просто запрос на тестирование подключения к БД - выходим
if "just_test_db" in post_data:
    exit_pyprices(True, 'Database connection available')

# --------------------------------------------------------------

#К БД подключились. Добавляем запись в таблицу запусков и получаем ID запуска
#SQL-запрос
cursor = db_link.cursor()
try:
    #В учетную запись запуска записываем полученный объект со списком заданий в том, виде, как он пришел в POST
    posted_list_to_handle = ''
    if 'list_to_handle' in post_data:
        posted_list_to_handle = post_data['list_to_handle'].value
    
    #Добавляем учетную запись запуска
    cursor.execute( "INSERT INTO `shop_docpart_pyprices_launches` (`time_start`, `list_to_handle`, `pid`, `passed`) VALUES (%s, %s, %s, %s)", ( int(time.time()), posted_list_to_handle, os.getpid(), False ) )
    
    #Коммитим тразакцию, чтобы ID запуска зафиксировался
    db_link.commit()
    
    #Получаем ID запуска
    launch_id = cursor.lastrowid
    if not launch_id:
        raise Exception('launch_id not defined')
    
except Exception as err:
    exit_pyprices(False, str(err))

# --------------------------------------------------------------

#Проверка подключения к почте. Статус подключения пишем в флаг. Если подключения нет, то, работать продолжаем, НО, на этапе валидации POST-параметров отбросим задания по обновлению с почты (с записью в лог)

#Создаем объект класса docpart_imap для работы с почтой
imap_client = docpart_imap.docpart_imap(work_params['prices_email_server'], work_params['prices_email_encryption'], work_params['prices_email_port'], work_params['prices_email_username'], work_params['prices_email_password'])

#Получаем статус подключения к почте
imap_status = imap_client.get_imap_status()

# --------------------------------------------------------------
#Инициализация переменных

#Инициализация массива заданий. Передаем объект с POST-аргументами
init_list_to_handle(post_data)

if len(list_to_handle) == 0:
    exit_pyprices(False, "There are no any correct tasks to handle")

# --------------------------------------------------------------

#Папка tmp - постоянная и она не должна удаляться никогда
#Пересоздаем папку для текущего запуска модуля tmp/<launch_id>

#Если tmp нет, создаем tmp
if not os.path.isdir('tmp'):
    os.mkdir('tmp')
    
#Если tmp не оказалось
if not os.path.isdir('tmp'):
    exit_pyprices(False, "There was no tmp folder, and it was not possible to create it. Check the rights settings in the price list module")

#На всякий случай проверяем наличие папки tmp/<launch_id>
if os.path.isdir('tmp/' + str(launch_id) ):
    #Выходим. По непонятной причине произошло Duplicated launch_id, т.е. папка с именем launch_id уже есть. Такого быть не должно. В функцию exit_pyprices() передаем to_del_launch_folder = False, т.е. при выходе из модуля, папку не удаляем, чтобы выяснить, что в ней и как она там оказалась
    exit_pyprices(False, 'Duplicated launch_id', to_del_launch_folder = False)

#Создаем tmp/<launch_id>
os.mkdir('tmp/' + str(launch_id) )

#В итоге проверяем наличие рабочей папки для текущего запуска
if not os.path.isdir('tmp/' + str(launch_id) ):
    exit_pyprices(False, 'Failed to create folder tmp/launch_id ('+ str(launch_id) +')')

# --------------------------------------------------------------

# Дошли до цикла получения файлов

for task in list_to_handle:
    
    if task.source == "local_path":
        #Создаем объект получателя файла
        with file_receiver.file_receiver_local_path(task, launch_id) as receiver:
            #Получем файл
            try:
                receiver.get_file()
            except Exception as err:
                #При получении файла произошла какая-то ошибка. Текст из err добавляем в список error_messages данного задания
                task.error_messages.append( str(err) )
    if task.source == "ftp":

        #Создаем объект получателя файла
        with file_receiver.file_receiver_ftp(task, launch_id) as receiver:
            #Получем файл(ы)
            try:
                receiver.get_file()
            except Exception as err:
                #При получении файла(ов) произошла какая-то ошибка. Текст из err добавляем в список error_messages данного задания
                task.error_messages.append( str(err) )

    if task.source == "url":
        
        #Создаем объект получателя файла
        with file_receiver.file_receiver_url(task, launch_id) as receiver:
            #Получаем файл
            try:
                receiver.get_file()
            except Exception as err:
                task.error_messages.append( "An error occurred while trying to get a file from a URL: " + str(err) )
            
    if task.source == "email":
        #Если способ получения файла - email, то добавляем этот объект задания в сгруппированный массив
        #Если в сгруппированном массиве еще нет группы (списка) с таким адресом отправителя - создаем
        if task.email_price_sender not in list_to_handle_email:
            list_to_handle_email[task.email_price_sender] = []
        #Добавляем объект задания в группу
        list_to_handle_email[task.email_price_sender].append(task)

# --------------------------------------------------------------

#Здесь пойдет цикл по сгруппированному массиву list_to_handle_email для получения файлов из почты
if len(list_to_handle_email) > 0:
    #Цикл по сгруппированному массиву для получения файлов с E-mail
    for email_price_sender, email_tasks in list_to_handle_email.items():
        
        #Создаем объект получателя файлов по E-mail. Он является тоже наследником file_receiver, но, учитывая специфику работы с почтой (по блок-схеме), работает по-другому в отличие от local_path, url и ftp. Т.е. почтовый класс работает одновременно с несколькими заданиями. Передавать email_price_sender нет необходимости, т.к. элементы email_tasks его содержат и у них он одинаковый
        with file_receiver.file_receiver_email(email_tasks, imap_client, launch_id) as receiver:
            #Получаем файлы для данной группы заданий (от одного и того же отправителя)
            try:
                receiver.get_files_for_tasks() #Другой метод, не get_file()
            except Exception as err:
                errors_general_list.append( "When receiving files from E-mail from " + str(email_price_sender) + " an error has occurred: " + str(err) )
                #task.error_messages.append( "При попытке получить файл по URL произошла ошибка: " + str(err) )

# --------------------------------------------------------------

#Здесь будет цикл по массиву list_to_handle для обработки полученных по каждому заданию файлов

#TODO. Цикл обработки заданий - импорт файлов в БД
#Цикл снова по списку заданий
for task in list_to_handle:
    
    #Получаем список файлов в директории задачи
    task_path = 'tmp/' + str(launch_id) + '/' + str(task.price_id)
    files_list = [f for f in listdir(task_path) if isfile(join(task_path, f))]
    
    #Если список файлов для данного задания пуст
    if len(files_list) == 0:
        task.error_messages.append('After all processing, the list of files for this task is empty. We do not do anything')
        continue
    
    
    
    #Перед началом обработки файлов, очищаем старые записи по данному price_id
    if task.clear_old_records:
        
        #SQL-запрос на очистку по price_id
        cursor = db_link.cursor()
        try:
            cursor.execute( "DELETE FROM `shop_docpart_prices_data` WHERE `price_id` = %s", (task.price_id, ) )
        except Exception as err:
            continue
            #print( str(err) ) #Здесь можно записать лог (если произошла ошибка выполнения запроса)
    
    
    
    task_records_handled = 0 #Количество обработанных строк из всех файлов данного задания
    #Цикл по кадому файлу в tmp/launch_id/<price_id>
    for file in files_list:
        #Здесь нужно передать файл в функцию (или метод) обработки
        with price_file_handler.PriceFileHandler(task, file, db_link, launch_id) as file_handler:
            try:
                file_handler.handle_file()
            except Exception as err:
                task.other_messages.append( str(err) )
            #Добавляем количество обработанных строк из данного файла
            task_records_handled = task_records_handled + file_handler.file_records_handled
    
    
    
    #Здесь нужно закоммитить изменения в БД по данному заданию (коммитим очистку старых записей и добавление новых)
    if task_records_handled > 0:
        try:
            #Фиксируем время обновления данного прайс-листа
            cursor = db_link.cursor()
        
            cursor.execute("UPDATE `shop_docpart_prices` SET `last_updated` = %s WHERE `id` = %s", ( int(time.time()), task.price_id ))
            
            #Коммитим тразакцию (позиции прайс-листа и время обновления)
            db_link.commit()
            
            #Эта информация может потребоваться контроллеру
            task.records_handled = task_records_handled
            task.last_updated = time.time()
            
        except Exception as err:
            #Откатываем транзакцию
            db_link.rollback()
            task.records_handled = 0
            task.last_updated = 0
            task.error_messages.append('An error occurred in the transaction commit block. Changes are not written to the database')
    else:
        #Для данного price_id не загружено (не обработано) ни одной строки - откатываем транзакцию
        db_link.rollback()
    

# --------------------------------------------------------------
#Модуль отработал корректно
exit_pyprices(True, 'The module worked correctly')