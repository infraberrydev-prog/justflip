# Step 1: Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Kopyahin ang project files at i-restore ang dependencies
COPY ["*.csproj", "./"]
RUN dotnet restore

# Kopyahin ang lahat ng files at i-publish
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Step 2: Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Port configuration para sa Render
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "JustFlip.dll"]