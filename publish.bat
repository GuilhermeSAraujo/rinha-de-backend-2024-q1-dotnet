@echo off
ECHO "dotnet publish"
dotnet publish -c Release -r linux-musl-x64 --self-contained true /p:PublishTrimmed=true -o ./publish
ECHO "docker build"
docker build . -t rinha-api
ECHO "docker tag"
docker tag rinha-api guilhermesouzaaraujo/rinha-api-2024-q1:latest
ECHO "docker push"
docker push guilhermesouzaaraujo/rinha-api-2024-q1:latest