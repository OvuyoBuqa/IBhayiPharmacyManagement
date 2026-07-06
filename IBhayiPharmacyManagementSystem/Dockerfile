# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY IBhayiPharmacyManagementSystem.sln .
COPY IBhayiPharmacyManagementSystem/IBhayiPharmacyManagementSystem.csproj IBhayiPharmacyManagementSystem/
RUN dotnet restore IBhayiPharmacyManagementSystem/IBhayiPharmacyManagementSystem.csproj

# Copy everything and build
COPY . .
RUN dotnet publish IBhayiPharmacyManagementSystem/IBhayiPharmacyManagementSystem.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "IBhayiPharmacyManagementSystem.dll"]
