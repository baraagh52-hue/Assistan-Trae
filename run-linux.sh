#!/bin/bash

# Direct run script for Linux - bypasses setup issues
echo "======================================"
echo "Personal AI Assistant - Direct Run"
echo "======================================"

# Set up .NET environment
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$HOME/.dotnet:$PATH

# Check if .NET is available
if ! command -v $HOME/.dotnet/dotnet &> /dev/null; then
    echo "[ERROR] .NET not found at $HOME/.dotnet/dotnet"
    echo "Please run the setup script first: ./setup-linux.sh"
    exit 1
fi

echo "[INFO] Using .NET at: $HOME/.dotnet/dotnet"
echo "[INFO] .NET Version: $($HOME/.dotnet/dotnet --version)"

# Clean and restore
echo "[INFO] Cleaning previous builds..."
$HOME/.dotnet/dotnet clean

echo "[INFO] Restoring packages..."
$HOME/.dotnet/dotnet restore

# Build for Linux with explicit framework
echo "[INFO] Building for Linux..."
if [[ $(uname -m) == "aarch64" ]]; then
    $HOME/.dotnet/dotnet build -c Release -f net8.0 -r linux-arm64
else
    $HOME/.dotnet/dotnet build -c Release -f net8.0 -r linux-x64
fi

if [ $? -ne 0 ]; then
    echo "[ERROR] Build failed!"
    exit 1
fi

echo "[SUCCESS] Build completed successfully!"
echo "[INFO] Starting Personal AI Assistant..."
echo ""

# Run the application
if [[ $(uname -m) == "aarch64" ]]; then
    $HOME/.dotnet/dotnet run -c Release -f net8.0 -r linux-arm64
else
    $HOME/.dotnet/dotnet run -c Release -f net8.0 -r linux-x64
fi