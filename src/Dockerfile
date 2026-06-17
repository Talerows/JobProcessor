ARG APP_NAME=JobProcessor.Worker

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS builder

ARG APP_NAME

WORKDIR /src

COPY ./JobProcessor.Worker/*.csproj ./JobProcessor.Worker/

RUN dotnet restore ./JobProcessor.Worker/JobProcessor.Worker.csproj

COPY . .

RUN dotnet publish ./JobProcessor.Worker/JobProcessor.Worker.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview

WORKDIR /app

COPY --from=builder /app/publish .

ENTRYPOINT ["dotnet", "JobProcessor.Worker.dll"]