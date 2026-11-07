ARG DOTNET=6.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET} AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/Inventory.Domain/*.csproj          src/Inventory.Domain/
COPY src/Inventory.Application/*.csproj     src/Inventory.Application/
COPY src/Inventory.Infrastructure/*.csproj  src/Inventory.Infrastructure/
COPY src/Inventory.Api/*.csproj             src/Inventory.Api/
RUN dotnet restore src/Inventory.Api

COPY src ./src
RUN dotnet publish src/Inventory.Api -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET}
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser
COPY --from=build /app .
USER appuser
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Inventory.Api.dll"]