# Imagen base para build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar archivos de proyecto y restaurar dependencias
COPY *.csproj .
RUN dotnet restore

# Copiar el resto del código y compilar
COPY . .
RUN dotnet publish -c Release -o out
# Imagen base para producción
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Establecer zona horaria de Colombia
ENV TZ=America/Bogota
RUN apt-get update && apt-get install -y tzdata && \
    ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

COPY --from=build /app/out .

EXPOSE 80
ENTRYPOINT ["dotnet", "Tenis3t.dll"]
