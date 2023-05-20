FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR "src"
COPY ["src/asp.net-TestApplication/asp.net-TestApplication.csproj", "asp.net-TestApplication/"]
RUN dotnet restore "asp.net-TestApplication/asp.net-TestApplication.csproj"
COPY . .
WORKDIR "src/asp.net-TestApplication"
RUN dotnet build "asp.net-TestApplication.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "asp.net-TestApplication.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "asp.net-TestApplication.dll"]