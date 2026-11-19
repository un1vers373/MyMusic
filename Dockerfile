# FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# WORKDIR /src
# COPY MyMusic.csproj ./
# RUN dotnet restore
# COPY . .
# RUN dotnet publish MyMusic.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false


# FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# WORKDIR /app


# ENV ASPNETCORE_HTTP_PORTS=8080 \
#     ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
#     DOTNET_RUNNING_IN_CONTAINER=true


# RUN mkdir -p /app/storage /app/data && chown -R $APP_UID /app/storage /app/data
# USER $APP_UID

# COPY --from=build /app/publish .
# EXPOSE 8080
# ENTRYPOINT ["dotnet", "MyMusic.dll"]


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MyMusic.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish MyMusic.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_RUNNING_IN_CONTAINER=true

RUN mkdir -p /app/storage /app/data \
    && chown -R $APP_UID:$APP_UID /app/storage /app/data

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish/ ./

# Copy initial audio files
COPY --chown=$APP_UID:$APP_UID storage/ /app/storage/

# Copy initial SQLite database
COPY --chown=$APP_UID:$APP_UID Data/mymusic.db /app/data/mymusic.db

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "MyMusic.dll"]
