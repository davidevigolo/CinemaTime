FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS pre-build
RUN apt-get update
RUN apt-get install -y libopus-dev
RUN apt-get install -y ffmpeg

FROM pre-build

ARG BUILD_CONFIGURATION=Build
WORKDIR /src
COPY ["BotPollo/BotPollo.csproj", "BotPollo/"]
COPY ["MongoWrapper/MongoWrapper.csproj", "MongoWrapper/"]
WORKDIR /src
RUN dotnet restore "BotPollo/BotPollo.csproj"
COPY . .
WORKDIR /src/BotPollo
COPY ["BotPollo/appsettings-dev.json","src/BotPollo"]
COPY ["BotPollo/token.txt","src/BotPollo"]
RUN dotnet build "BotPollo.csproj" -c $BUILD_CONFIGURATION -o /app/build
WORKDIR /app/build
EXPOSE 50000-65535
ENTRYPOINT ["dotnet", "BotPollo.dll"]


#FROM build AS publish
#ARG BUILD_CONFIGURATION=Release
#RUN dotnet publish "BotPollo.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

#FROM base AS final
#WORKDIR /app
#COPY --from=publish /app/publish .
#ENTRYPOINT ["dotnet", "BotPollo.dll"]
