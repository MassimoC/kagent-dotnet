# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY KAgent-DotNet.sln ./
COPY core/KAgent.Core.csproj core/
COPY kagent-maf/src/KAgent.MAF.csproj kagent-maf/src/
COPY samples/hello-world/HelloWorld.csproj samples/hello-world/

# Restore dependencies (cached if project files haven't changed)
RUN dotnet restore samples/hello-world/HelloWorld.csproj

# Copy source code
COPY core/ core/
COPY kagent-maf/src/ kagent-maf/src/
COPY samples/hello-world/ samples/hello-world/

# Build and publish
WORKDIR /src/samples/hello-world
RUN dotnet publish HelloWorld.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables (can be overridden at runtime)
ENV ASPNETCORE_URLS=http://+:8080

# Run the application
ENTRYPOINT ["dotnet", "HelloWorld.dll"]
