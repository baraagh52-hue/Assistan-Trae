# Personal AI Assistant - Multi-stage Docker build
# Supports both x64 and ARM64 architectures (including Raspberry Pi)

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY *.csproj ./
RUN dotnet restore

# Copy source code
COPY . .

# Build the application
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish --no-restore --self-contained false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Install system dependencies
RUN apt-get update && apt-get install -y \
    wget \
    curl \
    unzip \
    alsa-utils \
    pulseaudio \
    portaudio19-dev \
    libasound2-dev \
    ffmpeg \
    python3 \
    python3-pip \
    && rm -rf /var/lib/apt/lists/*

# Create app user
RUN useradd -m -s /bin/bash appuser

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create necessary directories
RUN mkdir -p /home/appuser/.personal-ai-assistant/logs \
    && mkdir -p /home/appuser/.personal-ai-assistant/models \
    && mkdir -p /home/appuser/.personal-ai-assistant/config

# Download Vosk model based on architecture
RUN ARCH=$(dpkg --print-architecture) && \
    if [ "$ARCH" = "arm64" ]; then \
        MODEL_URL="https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip"; \
        MODEL_FILE="vosk-model-small-en-us-0.15.zip"; \
    else \
        MODEL_URL="https://alphacephei.com/vosk/models/vosk-model-en-us-0.22.zip"; \
        MODEL_FILE="vosk-model-en-us-0.22.zip"; \
    fi && \
    wget "$MODEL_URL" -O "/tmp/$MODEL_FILE" && \
    cd /tmp && \
    unzip -q "$MODEL_FILE" && \
    EXTRACTED_DIR=$(find . -name "vosk-model-*" -type d | head -1) && \
    if [ -n "$EXTRACTED_DIR" ]; then \
        mv "$EXTRACTED_DIR"/* "/home/appuser/.personal-ai-assistant/models/"; \
    fi && \
    rm -rf /tmp/*

# Create default configuration
RUN cat > /home/appuser/.personal-ai-assistant/config/appsettings.json << 'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "WakeWords": ["hey assistant", "computer"],
  "LLMProvider": "OpenAI",
  "LLMModel": "gpt-3.5-turbo",
  "VoskModelPath": "/home/appuser/.personal-ai-assistant/models",
  "KokoroServerUrl": "http://localhost:8000",
  "AudioSettings": {
    "SampleRate": 16000,
    "Channels": 1,
    "BitsPerSample": 16
  },
  "PrayerTimes": {
    "Enabled": false,
    "City": "",
    "Country": "",
    "Method": 2
  },
  "MicrosoftToDo": {
    "Enabled": false
  },
  "ActivityWatch": {
    "Enabled": false,
    "ServerUrl": "http://localhost:5600"
  }
}
EOF

# Set ownership
RUN chown -R appuser:appuser /home/appuser/.personal-ai-assistant /app

# Switch to app user
USER appuser

# Set environment variables
ENV DOTNET_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV HOME=/home/appuser
ENV XDG_RUNTIME_DIR=/tmp/runtime-appuser

# Create runtime directory
RUN mkdir -p $XDG_RUNTIME_DIR

# Expose port (if needed for web interface)
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD pgrep -f "PersonalAiAssistant" > /dev/null || exit 1

# Entry point
ENTRYPOINT ["dotnet", "PersonalAiAssistant.dll"]