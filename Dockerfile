FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/TeleMed.Domain/TeleMed.Domain.csproj src/TeleMed.Domain/
COPY src/TeleMed.Application/TeleMed.Application.csproj src/TeleMed.Application/
COPY src/TeleMed.Infrastructure/TeleMed.Infrastructure.csproj src/TeleMed.Infrastructure/
COPY src/TeleMed.Api/TeleMed.Api.csproj src/TeleMed.Api/
RUN dotnet restore src/TeleMed.Api/TeleMed.Api.csproj
COPY src/ src/
RUN dotnet publish src/TeleMed.Api/TeleMed.Api.csproj -c Release --no-restore -o /app

# The Ubuntu-based aspnet image ships tzdata and ICU; fail the build if a base-image change drops them.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN test -f /usr/share/zoneinfo/Asia/Colombo && ls /usr/lib/*/libicuuc.so.* >/dev/null
# Default Storage:RootPath target; a volume mounted here inherits the app user's ownership.
RUN mkdir -p /var/lib/telemed/files && chown -R $APP_UID /var/lib/telemed
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TeleMed.Api.dll"]
