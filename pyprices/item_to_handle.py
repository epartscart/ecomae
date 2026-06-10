#Здесь определение класса для задания (элемент массива list_to_handle)

class item_to_handle:
    #Конструктор. Здесь проверяем все параметры и инициализируем объект. Аргумент - объект, полученный из POST-запроса (один элемент из массива. На основе этого элемента нужно инициализировать этот объект).
    def __init__(self, post_item, db_link, imap_status):
        
        # Сначала иницализируем переменные объекта. Есть особенность питона - поля класса и поля объекта. Нас интересуют поля объекта, поэтому сначала создаем их в конструкторе, а не объявляем прямо в классе.
        
        self.price_id = None #ID прайс-листа из таблицы shop_docpart_prices
        self.source = None #Источник файла (Варианты: local_path, email, ftp, url)
        self.file_name_substring = None #Подстрока в имени незапакованного файла
        self.file_name_substring_arch = None #Подстрока в имени архива (если целевой файл запакован)
        self.file_encoding = "auto" #Кодировка файла для текстовых файлов TXT и CSV (UTF-8 по-умолчанию заменили на auto - для автоопределения)
        self.cols_delimiter = ";" #Разделитель колонок для текстовых файлов TXT и CSV
        self.clear_old_records = True #Флаг Предварительно очистить старые строки
        self.rows_per_query = 500 #Количество строк в одном INSERT-запросе
        #Порядок расположения колонок в файле
        self.col_name = None
        self.col_article = None
        self.col_manufacturer = None
        self.col_price = None
        self.col_exist = None
        self.col_storage = 0
        self.col_min_order = 0
        self.col_time_to_exe = 0
        self.cols_to_left = 0 #Количество строк пропустить
        #Если source == local_path
        self.local_path = None
        self.del_file_from_local_path = True #Флаг - удалить исходный файл из local_path
        self.tmp_folder_name = None #Уникальное имя временной папки, в которой находится исходный файл
        #Если source == email
        self.email_price_sender = None #Адрес отправителя
        self.email_message_header_substring = None #Подстрока в заголовке письма, по которой искать файлы
        self.not_mark_seen_email_messages = False #Не помечать письмо прочитанным после обработки
        #Если source == ftp
        self.ftp_host = None
        self.ftp_username = None
        self.ftp_password = None
        self.ftp_folder = None #Папка на FTP-сервере, из которой брать файлы
        #Если source == url
        self.url = None #Полная ссылка на файл для скачивания про протоколу http/https
        #Поля валидации
        self.validation_messages = [] #Сюда пишем причины, если объект не прошел валидацию. Корректным считается объект, у которого поля прошли валидацию, а также price_id не дублируется с остальными объектами
        self.validated = False #Флаг - объект успешно прошел валидацию
        
        # --------------------------------------------------------------
        
        self.error_messages = [] #Сюда пишем ошибки, по причине которых нельзя обработать данный объект (помимо валидации). Сюда будут добавляться сообщения по мере выполнения блок-схемы
        
        # --------------------------------------------------------------
        
        self.other_messages = [] #Сюда можем писать остальные сообщения, например, какие-нибудь логи для отладки
        
        # --------------------------------------------------------------
        
        #Еще технические поля
        self.records_handled = 0 #Количество обработанных строк (при прямых INSERT-запросах - это количество импортированных строк)
        self.last_updated = 0 #Сюда будет записано время последнего обновления
        self.client_task_id = 0 #Глобальный сквозной ID задания
        
        # --------------------------------------------------------------
        
        #Теперь начинаем инициализации/проверки полей объекта на основе полученного аргумента

        # ------------------
        
        #Проверяем price_id
        #Обязательный параметр - price_id
        self.init_param("price_id", post_item, param_type="int")
        if self.price_id != None:
            #Проверяем наличие такого price_id в БД
            cursor = db_link.cursor()
            cursor.execute(("SELECT COUNT(*) FROM `shop_docpart_prices` WHERE `id` = %s;"), [self.price_id])
            count_prices_record = cursor.fetchone()
            if int(count_prices_record[0]) != int(1):
                self.validation_messages.append('Прайс-лист с ID ' + str(self.price_id) + ' не найден ')

        # ------------------
        
        #Обязательный параметр - source
        self.init_param("source", post_item)
        if self.source != None:
            if self.source != "local_path" and self.source != "email" and self.source != "ftp" and self.source != "url":
                self.validation_messages.append('The source value is incorrect: ' + str(self.source) )
            else:
                #Значение source допустимо. В зависимости от него проверяем далее параметры
                if self.source == "local_path":
                    #Должен быть параметр local_path
                    self.init_param("local_path", post_item)
                    #Необязательный параметр del_file_from_local_path
                    self.init_param("del_file_from_local_path", post_item, required=False, param_type="bool")
                    #Если предыдущий параметр (флаг del_file_from_local_path установлен в True), то, в таком случае следует ОБЯЗАТЕЛЬНЫЙ параметр tmp_folder_name (уникальное имя папки, в которой находится исходный файл). Удаляется именно сама папка с уникальным именем при флаге del_file_from_local_path == True
                    if self.del_file_from_local_path == True:
                        #Обязательный в таком случае tmp_folder_name
                        self.init_param("tmp_folder_name", post_item)
                elif self.source == "email":
                    if imap_status == False:
                        self.validation_messages.append( 'The object cannot be processed due to lack of connection to the IMAP mail server' )
                    #Должны быть параметры для получения файла с почты
                    #Обязательный параметр - адрес отправителя
                    self.init_param("email_price_sender", post_item)
                    #Необязательный параметр - подстрока в заголовке письма
                    self.init_param("email_message_header_substring", post_item, required=False)
                    #Необязательный параметр - Флаг "Не помечать письмо прочитанным после обработки"
                    self.init_param("not_mark_seen_email_messages", post_item, required=False, param_type="bool")
                elif self.source == "ftp":
                    #Должны быть параметры для работы с FTP
                    #Обязательный параметр - хост
                    self.init_param("ftp_host", post_item)
                    #Обязательный параметр - пользователь
                    self.init_param("ftp_username", post_item)
                    #Обязательный параметр - пароль
                    self.init_param("ftp_password", post_item)
                    #НЕ обязательный параметр - папка FTP
                    self.init_param("ftp_folder", post_item, required=False)
                elif self.source == "url":
                    #Обязательный параметр - url
                    self.init_param("url", post_item)
                else:
                    self.validation_messages.append('Something wrong with the parameter source')
        
        # ------------------
        
        #Необязательный file_name_substring
        self.init_param("file_name_substring", post_item, required=False)
        
        # ------------------
        
        #Необязательный file_name_substring_arch
        self.init_param("file_name_substring_arch", post_item, required=False)
        
        # ------------------
        
        #Необязательный file_encoding
        self.init_param("file_encoding", post_item, required=False)
        
        # ------------------
        
        #Необязательный cols_delimiter
        self.init_param("cols_delimiter", post_item, required=False)
        
        # ------------------
        
        #Необязательный clear_old_records
        self.init_param("clear_old_records", post_item, required=False, param_type="bool")
        
        # ------------------
        
        #Необязательный rows_per_query
        self.init_param("rows_per_query", post_item, param_type="int", required=False)
        #Запрещаем указывать значение вне диапазона от 1 до 5000
        if self.rows_per_query < 1 or self.rows_per_query > 5000:
            self.rows_per_query = 500 #Установили обратно по-умолчанию
        
        # ------------------
        
        #Порядок расположения колонок - обязательные
        self.init_param("col_name", post_item, param_type="int")
        self.init_param("col_article", post_item, param_type="int")
        self.init_param("col_manufacturer", post_item, param_type="int")
        self.init_param("col_price", post_item, param_type="int")
        self.init_param("col_exist", post_item, param_type="int")
        #Необязательные
        self.init_param("col_storage", post_item, param_type="int", required=False)
        self.init_param("col_min_order", post_item, param_type="int", required=False)
        self.init_param("col_time_to_exe", post_item, param_type="int", required=False)
        #Необязательный cols_to_left
        self.init_param("cols_to_left", post_item, param_type="int", required=False)
        
        # ------------------
        
        #Обязательный client_task_id (Глобальный ID задания)
        self.init_param("client_task_id", post_item, param_type="int")
        if not self.client_task_id > 0:
            self.validation_messages.append('client_task_id has an incorrect value ' + str(client_task_id) )
        else:
            #Значение технически корректное - ищем такое задание в таблице начатых заданий
            cursor = db_link.cursor()
            cursor.execute("SELECT COUNT(*) FROM `shop_docpart_pyprices_tasks` WHERE `id` = %s AND `price_id` = %s AND (`pyprices_launche_id` = %s OR ISNULL(`pyprices_launche_id`) )", (self.client_task_id, self.price_id, 0) )
            count_tasks_record = cursor.fetchone()
            if int(count_tasks_record[0]) != int(1):
                self.validation_messages.append('Task with client_task_id ' + str(self.client_task_id) + ' not found' )
        
        # ------------------
        
        #Без этого, объект не преобразуется в JSON-строку для ответа
        self.validation_messages = list(self.validation_messages)
        
        #Если validation_messages не пустой, значит объект не прошел валидацию
        if len(self.validation_messages) > 0:
            self.validated = False
        else:
            self.validated = True
    
    
    # --------------------------------------------------------------
    #Метод иницализации параметра
    def init_param(self, param_name, post_item, param_type="str", required=True):
        
        #Если такого параметра нет в post_item
        if param_name not in post_item:
            #Если такой параметр обязателен, то, добавляем сообщение (объект считается не валидным)
            if required:
                self.validation_messages.append('Parameter not found in object: ' + str(param_name) )
            
            return
        else:
            #Параметр есть
            
            #Если параметр строковый
            if param_type == "str":
                #То, проверяем сначала на None
                if post_item[param_name] is not None:
                    #Если не None - приводим к строке
                    param_value = str(post_item[param_name])
                else:
                    #Оставляем None
                    param_value = None
            else:
                #Тип параметра - не строка. Проводим здесь к строке, а далее он уже корректно обработается на int или bool
                param_value = str(post_item[param_name])
            
            
            #Если тип параметра должен быть int
            if param_type == 'int':
                #Если полученный тип не число
                if not param_value.isdigit():
                    #И при этом, параметр обязательный
                    if required:
                        self.validation_messages.append('Required parameter ' + param_name + ' has an incorrect value ' + str(param_value)  )
                    
                    return
                else:
                    param_value = int(param_value)
            
            
            #Если тип параметра должен быть bool
            if param_type == 'bool':
                param_value = param_value.lower()
                if param_value in ('y', 'yes', 't', 'true', 'on', '1'):
                    param_value = True
                else:
                    param_value = False
            
            #Инициализируем значение
            setattr(self, param_name, param_value)