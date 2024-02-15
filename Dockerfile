FROM alpine:latest

RUN apk add --no-cache \ 
    openssh libunwind \
    nghttp2-libs libidn krb5-libs libuuid lttng-ust zlib \
    libstdc++ libintl \
    icu

EXPOSE 8080

# Copy 
WORKDIR /app
COPY ./publish ./

ENTRYPOINT ["./rinha-de-backend-2024-q1-dotnet", "--urls", "http://0.0.0.0:8080"]