# Dockerfile for CustomerSite (Landing Page and Subscription Portal)
# Multi-stage build: Compiles source code → Publishes → Creates runtime image

# Stage 1: Build - Uses full SDK to compile the .NET 8.0 application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for dependency resolution
COPY ["CustomerSite/CustomerSite.csproj", "CustomerSite/"]
COPY ["Services/Services.csproj", "Services/"]
COPY ["DataAccess/DataAccess.csproj", "DataAccess/"]

# Restore NuGet packages (cached if project files don't change)
RUN dotnet restore "CustomerSite/CustomerSite.csproj"

# Copy all source code
COPY . .

# Build the application in Release mode
WORKDIR "/src/CustomerSite"
RUN dotnet build "CustomerSite.csproj" -c Release -o /app/build

# Stage 2: Publish - Create deployment-ready output
FROM build AS publish
RUN dotnet publish "CustomerSite.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final - Lightweight runtime image (no SDK, just runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copy only the published output (not source code)
COPY --from=publish /app/publish .

# Set environment variables for ASP.NET Core
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Development

# Run the compiled application
ENTRYPOINT ["dotnet", "CustomerSite.dll"]
