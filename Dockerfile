# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and all projects (including KeaCore.Common)
COPY *.sln ./
COPY KeaCore.Common/*.csproj KeaCore.Common/
COPY KeaCore.CLI/*.csproj KeaCore.CLI/

# Restore dependencies
RUN dotnet restore KeaCore.CLI/KeaCore.CLI.csproj

# Copy the full source code
COPY . .

# Build and publish the CLI project
RUN dotnet publish KeaCore.CLI/KeaCore.CLI.csproj -c Release -o /app/out --self-contained false

# ---- Runtime Stage ----
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

# Copy the built CLI application
COPY --from=build /app/out ./

# Set the entrypoint to run the CLI
ENTRYPOINT ["./KeaCore.CLI"]
