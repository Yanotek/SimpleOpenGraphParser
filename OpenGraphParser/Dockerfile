FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["OpenGraphParser/OpenGraphParser.csproj", "OpenGraphParser/"]
RUN dotnet restore "OpenGraphParser/OpenGraphParser.csproj"
COPY . .
WORKDIR "/src/OpenGraphParser"
RUN dotnet build "OpenGraphParser.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OpenGraphParser.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OpenGraphParser.dll"]
