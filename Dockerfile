FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ScanNow.Domain/ScanNow.Domain.csproj ScanNow.Domain/
COPY ScanNow.Application/ScanNow.Application.csproj ScanNow.Application/
COPY ScanNow.Infrastructure/ScanNow.Infrastructure.csproj ScanNow.Infrastructure/
COPY ScanNow.Web/ScanNow.Web.csproj ScanNow.Web/
RUN dotnet restore ScanNow.Web/ScanNow.Web.csproj

COPY . .
RUN dotnet publish ScanNow.Web/ScanNow.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ScanNow.Web.dll"]
