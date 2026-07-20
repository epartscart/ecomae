# ecomae ERP — On-Premises PHP image (used by both the `app` and `cron` services)
#
# Stock php:8.3-fpm is missing every extension this app actually needs
# (pdo_mysql, redis, gd, intl, zip, bcmath, opcache) and has no
# php-fpm-healthcheck binary, so the Docker HEALTHCHECK in
# docker-compose.yml always failed. This image fixes both.
FROM php:8.3-fpm

RUN apt-get update && apt-get install -y --no-install-recommends \
		git \
		unzip \
		libzip-dev \
		libicu-dev \
		libpng-dev \
		libjpeg62-turbo-dev \
		libfreetype6-dev \
	&& docker-php-ext-configure gd --with-freetype --with-jpeg \
	&& docker-php-ext-install -j"$(nproc)" \
		pdo_mysql \
		mysqli \
		gd \
		intl \
		zip \
		bcmath \
		opcache \
	&& pecl install redis \
	&& docker-php-ext-enable redis \
	&& apt-get purge -y --auto-remove libzip-dev libicu-dev libpng-dev libjpeg62-turbo-dev libfreetype6-dev \
	&& rm -rf /var/lib/apt/lists/*

# php-fpm-healthcheck (referenced by docker-compose.yml's app healthcheck)
ADD https://raw.githubusercontent.com/renatomefi/php-fpm-healthcheck/master/php-fpm-healthcheck /usr/local/bin/php-fpm-healthcheck
RUN chmod +x /usr/local/bin/php-fpm-healthcheck

WORKDIR /var/www/html
