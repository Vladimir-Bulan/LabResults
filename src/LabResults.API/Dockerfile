FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/LabResults.Domain/LabResults.Domain.csproj", "src/LabResults.Domain/"]
COPY ["src/LabResults.Application/LabResults.Application.csproj", "src/LabResults.Application/"]
COPY ["src/LabResults.Infrastructure/LabResults.Infrastructure.csproj", "src/LabResults.Infrastructure/"]
COPY ["src/LabResults.API/LabResults.API.csproj", "src/LabResults.API/"]
RUN dotnet restore src/LabResults.API/LabResults.API.csproj
COPY . .
RUN dotnet publish src/LabResults.API/LabResults.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LabResults.API.dll"]
