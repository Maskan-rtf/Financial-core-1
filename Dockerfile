FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore Maskan.Panel.plus.sln
RUN dotnet publish src/Services/CoreService/Core.API/Core.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Core.API.dll"]
