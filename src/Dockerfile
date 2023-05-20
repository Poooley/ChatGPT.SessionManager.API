FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR "src"
COPY ["src/ChatGPT.SessionManager.API/ChatGPT.SessionManager.API.csproj", "ChatGPT.SessionManager.API/"]
RUN dotnet restore "ChatGPT.SessionManager.API/ChatGPT.SessionManager.API.csproj"
COPY . .
WORKDIR "src/ChatGPT.SessionManager.API"
RUN dotnet build "ChatGPT.SessionManager.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ChatGPT.SessionManager.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChatGPT.SessionManager.API.dll"]