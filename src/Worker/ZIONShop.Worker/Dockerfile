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

RUN dotnet restore src/Worker/ZIONShop.Worker/ZIONShop.Worker.csproj
RUN dotnet publish src/Worker/ZIONShop.Worker/ZIONShop.Worker.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ZIONShop.Worker.dll"]
