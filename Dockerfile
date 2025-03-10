# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy project files and restore dependencies
COPY KeaCore.CLI/*.csproj ./KeaCore.CLI/
RUN dotnet restore KeaCore.CLI/KeaCore.CLI.csproj

# Copy the full source code and build the application
COPY . .
RUN dotnet publish KeaCore.CLI/KeaCore.CLI.csproj -c Release -o /app/out --self-contained false

# ---- Runtime Stage ----
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

# Copy the built application from the build stage
COPY --from=build /app/out ./

# Set the entrypoint to run the CLI
ENTRYPOINT ["./KeaCore.CLI"]
