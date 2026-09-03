FROM nginx:alpine

RUN apk add --no-cache openssl && \
    mkdir -p /var/cache/nginx /var/www/certbot /etc/nginx/ssl

COPY nginx.conf /etc/nginx/nginx.conf

COPY development.conf /etc/nginx/conf.d/default.conf

EXPOSE 80 443

CMD ["nginx", "-g", "daemon off;"]
