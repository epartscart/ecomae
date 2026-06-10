function setCookie(name, value, options = {}) {

    options = {
        path: '/',
        // при необходимости добавьте другие значения по умолчанию
        ...options
    };

    if (options.expires instanceof Date) {
        options.expires = options.expires.toUTCString();
    }

    let updatedCookie = encodeURIComponent(name) + "=" + encodeURIComponent(value);

    for (let optionKey in options) {
        updatedCookie += "; " + optionKey;
        let optionValue = options[optionKey];
        if (optionValue !== true) {
            updatedCookie += "=" + optionValue;
        }
    }

    document.cookie = updatedCookie;
}

function locationToOrdersByUserId(url, user_id) {
    var orders_filter = new Object;

    //1. Время с
    orders_filter.time_from = "";
    //2. Время по
    orders_filter.time_to = "";

    //3. Номер заказа
    orders_filter.order_id = "";

    //4. Статус заказа
    orders_filter.status = 0;

    //5. Товар
    orders_filter.paid = -1;

    //6. Покупатель
    orders_filter.customer = ' ' + user_id;
    orders_filter.customer_id = user_id;

    //7. Просмотрен
    orders_filter.viewed = -1;

    //8. Способ оплаты
    orders_filter.paid_type = -1;

    let date = new Date(new Date().getTime() + 15552000 * 1000);
    document.cookie = "orders_filter="+JSON.stringify(orders_filter)+"; path=/; expires=" + date.toUTCString();

    if (window.event.srcElement.nodeName != 'TD')
        window.open(url, '_blank');
}

function locationToCartsByUserId(url, user_id) {
    var carts_items_filter = new Object;

    //1. Время с
    carts_items_filter.time_from = "";

    //2. Время по
    carts_items_filter.time_to = "";

    //3. Покупатель
    carts_items_filter.customer = " " + user_id;

    //4. Склад
    carts_items_filter.storage_id = 0;

    //Устанавливаем cookie (на полгода)
    var date = new Date(new Date().getTime() + 15552000 * 1000);
    document.cookie = "carts_items_filter="+JSON.stringify(carts_items_filter)+"; path=/; expires=" + date.toUTCString();

    window.open(url, '_blank');
}

function locationToFinanceByUserId(url, user_id) {
    var account_operations_filter = new Object;

    account_operations_filter.time_from = "";
    account_operations_filter.time_to = "";
    account_operations_filter.income = -1;
    account_operations_filter.operation_code = -1;
    account_operations_filter.user_id = " " + user_id;
    account_operations_filter.order_id = "";
    account_operations_filter.office_id = -1;
    console.log(account_operations_filter);

    setCookie('account_operations_filter', JSON.stringify(account_operations_filter));
    window.open(url, '_blank');
}

function locationToStatisticsByUserId (url, user_id, time_from, time_to) {
    var stat_article_queries_rating_filter = new Object;

    //1. Время с
    stat_article_queries_rating_filter.time_from = time_from;
    //2. Время по
    stat_article_queries_rating_filter.time_to = time_to;
    //3. Покупатель
    stat_article_queries_rating_filter.customer = user_id;

    setCookie('stat_article_queries_rating_filter', JSON.stringify(stat_article_queries_rating_filter));
    window.open(url, '_blank');
}

function locationToReturnsByUserId (url, user_id) {
    if (window.event.srcElement.nodeName != 'TD')
        window.open(url + '?client=' + user_id, '_blank');
}

function locationToItemsByUserId(url, user_id) {
    var orders_items_filter = new Object;

    //1. Время с
    orders_items_filter.time_from = "";
    //2. Время по
    orders_items_filter.time_to = "";

    //3. Номер заказа
    orders_items_filter.order_id = "";

    //4. Статус заказа
    orders_items_filter.order_status = 0;

    //5. Товар
    orders_items_filter.paid = -1;

    //6. Покупатель
    orders_items_filter.customer = user_id;
    orders_items_filter.customer_id = " " + user_id;

    //7. Статус позиции
    orders_items_filter.order_item_status = 0;

    //8. Офис обслуживания
    orders_items_filter.office_id = 0;

    //9. Наименование
    orders_items_filter.product_name = "";
    orders_items_filter.article = "";
    orders_items_filter.manufacturer = "";

    //10. Заказ просмотрен
    orders_items_filter.viewed = -1;

    //11. ID склада
    orders_items_filter.storage_id = -1;


    setCookie('orders_items_filter', JSON.stringify(orders_items_filter));
    window.open(url, '_blank');
}