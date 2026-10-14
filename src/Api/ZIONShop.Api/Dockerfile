FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY .editorconfig .
COPY global.json .
COPY ZIONShop.slnx .
COPY src src
COPY tests tests

RUN dotnet restore src/Api/ZIONShop.Api/ZIONShop.Api.csproj
RUN dotnet publish src/Api/ZIONShop.Api/ZIONShop.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
RUN mkdir -p /app/App_Data/uploads
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ZIONShop.Api.dll"]
