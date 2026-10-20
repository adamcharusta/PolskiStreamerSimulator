# Multi-stage build of the API (TD-99). The build context is the repository root, so content/m1 and content/m3 ship with the app.
# M6 reuses this file for the production image.
FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS build
WORKDIR /repo
COPY .editorconfig ./
COPY source/backend/global.json source/backend/Directory.Build.props source/backend/Directory.Packages.props source/backend/.editorconfig source/backend/
COPY source/backend/Domain/Domain.csproj source/backend/Domain/
COPY source/backend/Application/Application.csproj source/backend/Application/
COPY source/backend/Infrastructure/Infrastructure.csproj source/backend/Infrastructure/
COPY source/backend/WebApp/WebApp.csproj source/backend/WebApp/
WORKDIR /repo/source/backend
RUN dotnet restore WebApp/WebApp.csproj
COPY source/backend/ /repo/source/backend/
COPY content/ /repo/content/
RUN dotnet publish WebApp/WebApp.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "PolskiStreamerSimulator.WebApp.dll"]
