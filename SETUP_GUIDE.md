# Personal AI Assistant - Complete Setup Guide

This comprehensive guide will walk you through setting up the Personal AI Assistant on Windows, Linux, and Raspberry Pi, from initial installation to advanced configuration.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Windows Setup](#windows-setup)
3. [Linux Setup](#linux-setup)
4. [Raspberry Pi Setup](#raspberry-pi-setup)
5. [Docker Setup](#docker-setup)
6. [Configuration](#configuration)
7. [First Run](#first-run)
8. [Troubleshooting](#troubleshooting)
9. [Advanced Configuration](#advanced-configuration)
10. [Support](#support)

## Prerequisites

### Windows Requirements
- **Operating System**: Windows 10 version 1903 or later (Windows 11 recommended)
- **RAM**: 4GB minimum, 8GB recommended
- **Storage**: 2GB free space (additional space needed for speech models)
- **Audio**: Microphone and speakers/headphones
- **Internet**: Stable connection for cloud services and model downloads

### Linux Requirements
- **Operating System**: Ubuntu 20.04+, Debian 11+, CentOS 8+, or compatible
- **RAM**: 2GB minimum, 4GB recommended
- **Storage**: 4GB free space (includes speech models)
- **Audio**: ALSA/PulseAudio compatible audio devices
- **Internet**: Stable connection for downloads and cloud services

### Raspberry Pi Requirements
- **Model**: Raspberry Pi 4 (2GB+ RAM recommended)
- **OS**: Raspberry Pi OS (64-bit recommended)
- **Storage**: 8GB+ microSD card (Class 10 or better)
- **Audio**: USB microphone and speakers/headphones
- **Internet**: Wi-Fi or Ethernet connection

### Required Software
- **.NET 8.0 Runtime**: Installed automatically by setup scripts
- **Audio Libraries**: ALSA, PulseAudio (Linux/Pi)
- **Visual C++ Redistributable**: Windows only (usually pre-installed)

## Windows Setup

### Automated Setup (Recommended)

#### Step 1: Download
1. Go to the [Releases](https://github.com/baraagh52-hue/Assistan-Trae/releases) page
2. Download the latest `PersonalAiAssistant-Setup.zip`
3. Extract to a folder (e.g., `C:\PersonalAiAssistant`)

#### Step 2: Run Setup
1. **Right-click** on `setup.bat` and select **"Run as administrator"**
2. The setup wizard will guide you through:
   - Installing .NET 8.0 Runtime
   - Downloading speech recognition models
   - Setting up text-to-speech
   - Configuring basic settings

#### Step 3: Initial Configuration
During setup, you'll be prompted to configure:
- **API Keys**: For OpenAI, Azure, or other LLM providers
- **Wake Words**: Default is "Hey Assistant"
- **Audio Devices**: Select your microphone and speakers
- **Location**: For prayer times and weather (optional)

### Manual Windows Setup

If the automated setup fails or you prefer manual installation:

#### Step 1: Install .NET 8.0
1. Download from [Microsoft .NET Downloads](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Run the installer and follow the prompts
3. Verify installation:
   ```cmd
   dotnet --version
   ```

#### Step 2: Clone Repository
```cmd
git clone https://github.com/baraagh52-hue/Assistan-Trae.git
cd Assistan-Trae
```

#### Step 3: Build Application
```cmd
dotnet restore
dotnet build --configuration Release
```

#### Step 4: Download Models
Run the model download script:
```cmd
Scripts\download_models.bat
```

#### Step 5: Configure Settings
Edit `appsettings.json` with your preferred settings.

## Linux Setup

### Automated Setup (Recommended)

#### Step 1: Clone Repository
```bash
git clone https://github.com/yourusername/PersonalAiAssistant.git
cd PersonalAiAssistant
```

#### Step 2: Run Setup Script
```bash
chmod +x setup-linux.sh
./setup-linux.sh
```

The script will automatically:
- Detect your Linux distribution
- Install system dependencies
- Download and install .NET 8.0
- Configure audio system
- Download speech recognition models
- Build the application
- Create startup scripts
- Optionally create systemd service

#### Step 3: Configure API Keys
```bash
nano appsettings.json
```

#### Step 4: Start the Assistant

First, restart your terminal or reload your shell configuration:
```bash
source ~/.bashrc
```

Then run the assistant:
```bash
personal-ai-assistant
```

Alternatively, you can run it directly from the project directory:
```bash
dotnet run --configuration Release
```

### Manual Linux Setup

#### Step 1: Install System Dependencies

**Ubuntu/Debian:**
```bash
sudo apt-get update
sudo apt-get install -y wget curl git build-essential \
    libasound2-dev portaudio19-dev python3 python3-pip \
    ffmpeg pulseaudio alsa-utils
```

**CentOS/RHEL/Fedora:**
```bash
sudo yum install -y wget curl git gcc gcc-c++ make \
    alsa-lib-devel portaudio-devel python3 python3-pip \
    ffmpeg pulseaudio alsa-utils
```

#### Step 2: Install .NET 8.0
```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

#### Step 3: Setup Audio
```bash
# Start PulseAudio
pulseaudio --start

# Test audio devices
aplay -l
```

#### Step 4: Clone and Build
```bash
git clone https://github.com/yourusername/PersonalAiAssistant.git
cd PersonalAiAssistant
dotnet restore
dotnet build -c Release -r linux-x64
```

#### Step 5: Download Speech Models
```bash
mkdir -p ~/.personal-ai-assistant/models
wget https://alphacephei.com/vosk/models/vosk-model-en-us-0.22.zip
unzip vosk-model-en-us-0.22.zip -d ~/.personal-ai-assistant/models/
```

## Raspberry Pi Setup

### Automated Setup (Recommended)

The Linux setup script automatically detects Raspberry Pi and optimizes accordingly:

```bash
git clone https://github.com/yourusername/PersonalAiAssistant.git
cd PersonalAiAssistant
chmod +x setup-linux.sh
./setup-linux.sh
```

### Raspberry Pi Specific Optimizations

The setup script automatically:
- Downloads smaller speech models optimized for ARM
- Configures audio for Raspberry Pi
- Sets up GPIO access (if needed)
- Optimizes memory usage
- Configures for ARM64 architecture

### Manual Raspberry Pi Setup

#### Step 1: Update System
```bash
sudo apt-get update && sudo apt-get upgrade -y
```

#### Step 2: Enable Audio
```bash
sudo raspi-config
# Navigate to Advanced Options > Audio > Force 3.5mm
```

#### Step 3: Install Dependencies
```bash
sudo apt-get install -y wget curl git build-essential \
    libasound2-dev portaudio19-dev python3 python3-pip \
    ffmpeg pulseaudio alsa-utils raspberrypi-kernel-headers
```

#### Step 4: Install .NET 8.0 for ARM64
```bash
wget https://download.visualstudio.microsoft.com/download/pr/90486d8a-fb5a-41be-bd1d-c7b11b2b9ea6/b0e59c2ba2bd3ef0f592acbeae7ab27c/dotnet-sdk-8.0.100-linux-arm64.tar.gz
mkdir -p $HOME/.dotnet
tar zxf dotnet-sdk-8.0.100-linux-arm64.tar.gz -C $HOME/.dotnet
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
```

#### Step 5: Build for ARM64
```bash
git clone https://github.com/yourusername/PersonalAiAssistant.git
cd PersonalAiAssistant
dotnet restore
dotnet build -c Release -r linux-arm64
```

## Docker Setup

### Using Docker Compose (Recommended)

#### Step 1: Clone Repository
```bash
git clone https://github.com/yourusername/PersonalAiAssistant.git
cd PersonalAiAssistant
```

#### Step 2: Configure Environment
```bash
# Create config directory
mkdir -p ./config ./logs ./models

# Copy default configuration
cp appsettings.json ./config/
```

#### Step 3: Build and Run
```bash
# Build and start the container
docker-compose up -d

# View logs
docker-compose logs -f

# Stop the container
docker-compose down
```

#### Step 4: Configure API Keys
```bash
nano ./config/appsettings.json
docker-compose restart
```

### Manual Docker Setup

#### Step 1: Build Image
```bash
docker build -t personal-ai-assistant .
```

#### Step 2: Run Container
```bash
docker run -d \
  --name personal-ai-assistant \
  --device /dev/snd:/dev/snd \
  -v $(pwd)/config:/home/appuser/.personal-ai-assistant/config \
  -v $(pwd)/logs:/home/appuser/.personal-ai-assistant/logs \
  -p 8080:8080 \
  personal-ai-assistant
```

### Docker with Audio Support

For full audio support in Docker:

```bash
docker run -d \
  --name personal-ai-assistant \
  --device /dev/snd:/dev/snd \
  -v /tmp/.X11-unix:/tmp/.X11-unix:rw \
  -v /run/user/1000/pulse:/tmp/pulse:ro \
  -v $(pwd)/config:/home/appuser/.personal-ai-assistant/config \
  -e PULSE_RUNTIME_PATH=/tmp/pulse \
  -e DISPLAY=$DISPLAY \
  personal-ai-assistant
```

## Configuration

### Basic Configuration

Create `config.json` in the application root:

```json
{
  "Audio": {
    "InputDevice": -1,
    "OutputDevice": -1,
    "SampleRate": 16000,
    "Channels": 1
  },
  "WakeWord": {
    "Words": ["hey assistant", "wake up"],
    "Sensitivity": 0.7,
    "ConfidenceThreshold": 0.8,
    "Enabled": true
  },
  "SpeechToText": {
    "Provider": "vosk",
    "ModelPath": "./Models/vosk-model-en-us-0.22",
    "Language": "en-US"
  },
  "TextToSpeech": {
    "Provider": "kokoro",
    "ServerUrl": "http://localhost:5000",
    "Voice": "default",
    "Speed": 1.0
  },
  "LLM": {
    "Provider": "openai",
    "ApiKey": "your-openai-api-key",
    "Model": "gpt-4",
    "BaseUrl": "https://api.openai.com/v1",
    "MaxTokens": 2000,
    "Temperature": 0.7
  },
  "Microsoft": {
    "ClientId": "your-client-id",
    "TenantId": "common",
    "Scopes": ["https://graph.microsoft.com/Tasks.ReadWrite"]
  },
  "PrayerTimes": {
    "Enabled": true,
    "Location": "New York, NY",
    "Method": "ISNA",
    "Notifications": true
  },
  "ActivityWatch": {
    "Enabled": true,
    "ServerUrl": "http://localhost:5600",
    "BucketName": "aw-watcher-window"
  },
  "Logging": {
    "Level": "Information",
    "RetentionDays": 30,
    "MaxFileSizeMB": 10
  }
}
```

### API Key Configuration

#### OpenAI
1. Visit [OpenAI API](https://platform.openai.com/api-keys)
2. Create new API key
3. Add to config: `"ApiKey": "sk-..."`

#### Anthropic
1. Visit [Anthropic Console](https://console.anthropic.com/)
2. Generate API key
3. Update config:
   ```json
   "LLM": {
     "Provider": "anthropic",
     "ApiKey": "sk-ant-...",
     "Model": "claude-3-sonnet-20240229"
   }
   ```

#### Microsoft Graph (To-Do)
1. Register app in [Azure Portal](https://portal.azure.com/)
2. Configure permissions: `Tasks.ReadWrite`
3. Add Client ID to config

### Audio Device Configuration

To find audio device IDs:

```python
import sounddevice as sd
print("Input devices:")
for i, device in enumerate(sd.query_devices()):
    if device['max_input_channels'] > 0:
        print(f"{i}: {device['name']}")
```

Update config with device IDs:
```json
"Audio": {
  "InputDevice": 1,  // Your microphone ID
  "OutputDevice": 3  // Your speaker ID
}
```

## First Run

### Step 1: Start Services

1. **Start Kokoro TTS Server**:
   ```bash
   # Windows
   start_kokoro.bat
   
   # Or manually
   python kokoro_server.py
   ```

2. **Start ActivityWatch** (optional):
   - Download from [ActivityWatch](https://activitywatch.net/)
   - Install and start the application

### Step 2: Launch Application

```bash
# Windows
start_app.bat

# Or manually
dotnet run --project PersonalAiAssistant.csproj
```

### Step 3: Initial Setup

1. **Test Audio**:
   - Click "Test Microphone" in settings
   - Verify audio input levels
   - Test speaker output

2. **Configure Wake Word**:
   - Say your wake word
   - Adjust sensitivity if needed
   - Test detection accuracy

3. **Test LLM Connection**:
   - Click "Test LLM" in settings
   - Verify API connectivity
   - Test with simple query

4. **Setup Integrations**:
   - Authenticate Microsoft To-Do
   - Configure prayer times location
   - Test ActivityWatch connection

## Troubleshooting

### Common Issues

#### Python Dependencies

**Error**: `ModuleNotFoundError: No module named 'vosk'`

**Solution**:
```bash
pip install --upgrade pip
pip install vosk sounddevice numpy
```

#### Audio Issues

**Error**: No audio input detected

**Solutions**:
1. Check microphone permissions:
   - Windows Settings → Privacy → Microphone
   - Allow desktop apps to access microphone

2. Test microphone:
   ```python
   import sounddevice as sd
   import numpy as np
   
   def test_mic():
       duration = 3  # seconds
       fs = 16000
       print("Recording...")
       recording = sd.rec(int(duration * fs), samplerate=fs, channels=1)
       sd.wait()
       print(f"Max amplitude: {np.max(np.abs(recording))}")
   
   test_mic()
   ```

#### Kokoro TTS Issues

**Error**: Connection refused to localhost:5000

**Solutions**:
1. Check if server is running:
   ```bash
   curl http://localhost:5000/health
   ```

2. Check firewall settings
3. Restart server:
   ```bash
   python kokoro_server.py
   ```

#### .NET Build Issues

**Error**: SDK not found

**Solution**:
1. Install .NET 8.0 SDK
2. Verify installation: `dotnet --version`
3. Clear NuGet cache: `dotnet nuget locals all --clear`

### Log Analysis

Check logs in `Logs/` directory:

```bash
# View recent errors
tail -f Logs/error.log

# View application logs
tail -f Logs/application.log

# Search for specific issues
findstr "ERROR" Logs/application.log
```

## Advanced Configuration

### Custom Wake Words

To add custom wake words:

1. Record training samples (10-20 recordings)
2. Use Porcupine Console to train custom model
3. Update configuration:
   ```json
   "WakeWord": {
     "CustomModel": "path/to/custom.ppn",
     "Words": ["my custom phrase"]
   }
   ```

### Local LLM Setup

For privacy or offline usage:

1. **Install Ollama**:
   ```bash
   # Download from ollama.ai
   ollama pull llama2
   ollama serve
   ```

2. **Update Configuration**:
   ```json
   "LLM": {
     "Provider": "ollama",
     "BaseUrl": "http://localhost:11434",
     "Model": "llama2"
   }
   ```

### Performance Tuning

#### Memory Optimization

```json
"Performance": {
  "MaxConcurrentRequests": 3,
  "AudioBufferSize": 1024,
  "ModelCacheSize": 512,
  "GCSettings": {
    "ServerGC": true,
    "ConcurrentGC": true
  }
}
```

#### GPU Acceleration

For CUDA-enabled systems:

```bash
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
```

Update config:
```json
"Hardware": {
  "UseGPU": true,
  "CUDADevice": 0
}
```

### Security Configuration

#### API Key Security

Use environment variables:

```bash
# Windows
set OPENAI_API_KEY=your-key-here
set ANTHROPIC_API_KEY=your-key-here

# PowerShell
$env:OPENAI_API_KEY="your-key-here"
```

Update config:
```json
"LLM": {
  "ApiKey": "${OPENAI_API_KEY}"
}
```

#### Network Security

```json
"Security": {
  "AllowedHosts": ["localhost", "127.0.0.1"],
  "RequireHTTPS": false,
  "CorsEnabled": false
}
```

## Support

If you encounter issues not covered in this guide:

1. Check the [main README](README.md)
2. Review logs in `Logs/` directory
3. Create an issue on GitHub with:
   - System information
   - Error messages
   - Relevant log entries
   - Steps to reproduce

## Next Steps

After successful setup:

1. Explore voice commands
2. Customize settings for your workflow
3. Set up integrations (To-Do, prayer times)
4. Configure notifications
5. Optimize performance for your hardware