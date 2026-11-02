# Build stage: restore and publish with the full SDK
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Dispatch.Api/Dispatch.Api.csproj src/Dispatch.Api/
RUN dotnet restore src/Dispatch.Api/Dispatch.Api.csproj
COPY src/ src/
RUN dotnet publish src/Dispatch.Api/Dispatch.Api.csproj -c Release -o /app --no-restore

# Runtime stage: only the ASP.NET runtime, running as the non-root "app" user
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Dispatch.Api.dll"]
