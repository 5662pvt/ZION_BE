FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/Directory.Build.props backend/
COPY backend/Directory.Packages.props backend/
COPY backend/.editorconfig backend/
COPY backend/ZIONShop.slnx backend/
COPY backend/src backend/src
COPY backend/tests backend/tests

RUN dotnet restore backend/src/Api/ZIONShop.Api/ZIONShop.Api.csproj
RUN dotnet publish backend/src/Api/ZIONShop.Api/ZIONShop.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ZIONShop.Api.dll"]
