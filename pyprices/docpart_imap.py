"""
Класс для работы с почтовым ящиком при обновлении прайс-листов

Использование класса:
- создаем объект, передаем в него настройки подключения к почте
- далее можем обращаться к методам

Методы:
- проверка статуса подключения к своей почте (подключен/не подключен)
- проверить письма от определенного отправителя
- скачать файлы из определенного письма
"""


#Библиотеки для работы с почтой
from imap_tools import MailBox, A

# --------------------------------------------------------------
# --------------------------------------------------------------

class docpart_imap:
    
    # --------------------------------------------------------------
    
    #В конструкторе иницализируем настройки подключения к почте
    def __init__(self, email_server, email_encryption, email_port, email_username, email_password):
        self.email_server = email_server
        self.email_encryption = email_encryption
        self.email_port = email_port
        self.email_username = email_username
        self.email_password = email_password
    
    # --------------------------------------------------------------
    
    #Метод получения статуса подключения к почте
    def get_imap_status(self):
        try:
            with MailBox(self.email_server, self.email_port).login(self.email_username, self.email_password, 'INBOX') as mailbox:
                return True
        except:
            return False
    
    # --------------------------------------------------------------
    
    #Получения писем от определенного отправителя. Вернет список объектов с описанием писем.
    def get_messages_by_sender(self, sender, need_mark_seen):
        
        messages = [] #Список для возврата
        
        #Подключаемся к почтовому ящику, папка "Входящие"
        with MailBox(self.email_server, self.email_port).login(self.email_username, self.email_password, 'INBOX') as mailbox:
            
            #При необходимости, можно не помечать письмо, как прочитанное, выставив mark_seen=False. Также можно фетчить письмо по uid и устанавливать флаг просмотра, как нужно (см. примеры)
            
            #Получаем письма от sender, непрочитанные (seen=False). Полученные письма помечаем, как прочитанные (mark_seen=True)
            #for msg in mailbox.fetch(A(seen=False, from_=sender), mark_seen=need_mark_seen):
            for msg in mailbox.fetch(A(A(from_=sender), A(seen=False)), mark_seen=need_mark_seen):   
                #Формируем объект с описанием письма, необходимым для анализа
                message = {}
                message['uid'] = msg.uid #ID письма
                message['date'] = msg.date #Дата
                message['from_'] = msg.from_ #От кого
                message['subject'] = msg.subject #Тема письма
                message['attachments'] = [] #Список вложений

                #Цикл по вложениям. Формируем список имен вложенных файлов
                for att in msg.attachments:  # list: imap_tools.MailAttachment
                    message['attachments'].append(att.filename)
                
                messages.append(message)
        
        return messages
        
    # --------------------------------------------------------------
    
    #Метод скачивания файла с именем filename из сообщения с message_uid в директорию dest_dir. Здесь нет учета случая, при котором в одном письме могут быть файлы с одинаковыми именами. Скачивание произвойдет для первого файла. Если на практике встретится такой случай с файлами с одинаковыми именами в одном письме, то, можно будет доработать - искать файл под нескольким критериям
    def download_file_from_message(self, message_uid, filename, dest_dir, need_mark_seen):
        
        #Подключаемся к почтовому ящику, папка "Входящие"
        with MailBox(self.email_server, self.email_port).login(self.email_username, self.email_password, 'INBOX') as mailbox:
            
            #Получаем письмо c message_uid
            for msg in mailbox.fetch(A(uid=message_uid), mark_seen=need_mark_seen):
                #Цикл по вложениям письма
                for att in msg.attachments:
                    #Ищем нужный файл
                    if att.filename == filename:
                        with open( dest_dir + att.filename, 'wb') as f:
                            f.write(att.payload)
                            return True #Считаем, что файл успешно скачан
                        return False
                    continue #К следующему файлу в письме
                return False
            return False
        return False
    
    # --------------------------------------------------------------

# --------------------------------------------------------------
# --------------------------------------------------------------