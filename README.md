# Personal AI Assistant

A comprehensive Windows desktop application that provides voice-activated AI assistance with integrated productivity features.

## Features

### Core Voice Services
- **Wake Word Detection**: Hands-free activation using customizable wake words
- **Speech-to-Text**: Real-time voice recognition using Vosk models
- **Text-to-Speech**: Natural voice synthesis using Kokoro TTS
- **LLM Integration**: Support for OpenAI, Anthropic, and local language models

### Productivity Integration
- **Microsoft To-Do**: Voice-controlled task management
- **Prayer Times**: Islamic prayer time notifications and reminders
- **Activity Tracking**: Integration with ActivityWatch for productivity monitoring

### Advanced Features
- **Comprehensive Error Handling**: Robust error management and recovery
- **Detailed Logging**: Complete activity and error logging
- **Modern WPF UI**: Dark theme with intuitive controls
- **Configuration Management**: Flexible settings and preferences

## System Requirements

### Windows
- Windows 10/11 (64-bit)
- .NET 8.0 Runtime
- Microphone and speakers
- Internet connection
- 4GB RAM minimum, 8GB recommended
- 2GB free disk space

### Linux (including Raspberry Pi)
- Ubuntu 20.04+, Debian 11+, or Raspberry Pi OS
- .NET 8.0 Runtime (automatically installed by setup script)
- ALSA/PulseAudio for audio
- Microphone and speakers/headphones
- Internet connection
- 2GB RAM minimum (4GB recommended for full features)
- 4GB free disk space (includes speech models)

### Recommended Requirements
- SSD storage
- High-quality microphone
- GPU acceleration (for local LLM models)

## Installation

### Windows

#### Quick Setup (Recommended)
1. Download the latest release from the [Releases](https://github.com/baraagh52-hue/Assistan-Trae/releases) page
2. Run `setup.bat` as Administrator
3. Follow the setup wizard
4. Configure your API keys when prompted

#### Manual Setup
1. Install [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Clone this repository:
   ```bash
   git clone https://github.com/baraagh52-hue/Assistan-Trae.git
   cd Assistan-Trae
   ```
3. Run the setup script:
   ```cmd
   setup.bat
   ```
4. Build and run:
   ```cmd
   dotnet build
   dotnet run
   ```

### Linux & Raspberry Pi

#### Automated Setup (Recommended)
1. Clone this repository:
   ```bash
   git clone https://github.com/baraagh52-hue/Assistan-Trae.git
   cd Assistan-Trae
   ```
2. Run the Linux setup script:
   ```bash
   chmod +x setup-linux.sh
   ./setup-linux.sh
   ```
3. Configure your API keys:
   ```bash
   nano ~/.personal-ai-assistant/config/appsettings.json
   ```
4. Start the assistant:
   ```bash
   personal-ai-assistant
   ```

#### Docker Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/baraagh52-hue/Assistan-Trae.git
   cd Assistan-Trae
   ```
2. Build and run with Docker Compose:
   ```bash
   docker-compose up -d
   ```
3. Configure via mounted config volume:
   ```bash
   nano ./config/appsettings.json
   docker-compose restart
   ```

#### Manual Linux Installation
1. Install .NET 8.0:
   ```bash
   wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   sudo apt-get update
   sudo apt-get install -y dotnet-sdk-8.0
   ```
2. Install audio dependencies:
   ```bash
   sudo apt-get install -y alsa-utils pulseaudio portaudio19-dev libasound2-dev ffmpeg
   ```
3. Clone and build:
   ```bash
   git clone https://github.com/baraagh52-hue/Assistan-Trae.git
   cd Assistan-Trae
   dotnet restore
   dotnet build -c Release
   ```
4. Run the application:
   ```bash
   dotnet run --configuration Release
   ```

#### 3. Configuration

Create `config.json` in the application directory:

```json
{
  "WakeWords": ["hey assistant", "wake up"],
  "WakeWordSensitivity": 0.7,
  "WakeWordConfidenceThreshold": 0.8,
  "VoskModelPath": "./Models/vosk-model-en-us-0.22",
  "KokoroServerUrl": "http://localhost:5000",
  "LLMProvider": "openai",
  "LLMApiKey": "your-api-key-here",
  "LLMModel": "gpt-4",
  "MicrosoftClientId": "your-client-id",
  "PrayerTimesLocation": "New York, NY",
  "ActivityWatchEnabled": true,
  "LogLevel": "Information",
  "LogRetentionDays": 30
}
```

## Usage

### Starting the Application

#### Windows
1. Launch the application from the Start Menu or run `PersonalAiAssistant.exe`
2. The system tray icon indicates the assistant is running
3. Wait for the "Ready" status in the main window

#### Linux/Raspberry Pi
1. Run from terminal:
   ```bash
   personal-ai-assistant
   ```
2. Or start as a service:
   ```bash
   systemctl --user start personal-ai-assistant
   ```
3. The console interface will show the current status

### Basic Voice Commands
- **Wake Words**: "Hey Assistant", "Computer", "AI"
- **General**: "What time is it?", "What's the weather?"
- **Tasks**: "Add task: Buy groceries", "Show my tasks"
- **Prayer Times**: "When is the next prayer?", "Show prayer times"
- **System**: "Take a screenshot", "Open calculator"

### Console Commands (Linux/Raspberry Pi)
- `help` - Show available commands
- `status` - Display system status
- `start/stop` - Control voice services
- `test-mic` - Test microphone input
- `test-tts` - Test text-to-speech
- `say <text>` - Speak text using TTS
- `ask <question>` - Ask the AI assistant
- `tasks` - Show Microsoft To-Do tasks
- `prayer` - Show prayer times
- `config` - Display current configuration
- `logs` - Show recent logs

### Configuration

#### Windows
1. Right-click the system tray icon
2. Select "Settings"
3. Configure API keys, wake words, audio devices, etc.

#### Linux/Raspberry Pi
1. Edit the configuration file:
   ```bash
   nano ~/.personal-ai-assistant/config/appsettings.json
   ```
2. Restart the service:
   ```bash
   systemctl --user restart personal-ai-assistant
   ```

#### Key Configuration Options
- **API Keys**: OpenAI, Azure, Google, etc.
- **Wake Words**: Customize trigger phrases
- **Audio Devices**: Select microphone and speakers
- **Prayer Time Location**: Set city and country
- **Microsoft To-Do Integration**: OAuth setup
- **Local LLM**: Configure local language models

## Troubleshooting

### Common Issues

#### Wake Word Not Detected
1. Check microphone permissions in Windows Settings
2. Verify microphone is working in other applications
3. Adjust wake word sensitivity in settings
4. Ensure Vosk model is properly installed

#### TTS Not Working
1. Verify Kokoro server is running (`http://localhost:5000/health`)
2. Check firewall settings
3. Restart the Kokoro server
4. Verify Python dependencies are installed

#### LLM Connection Issues
1. Verify API keys are correct
2. Check internet connection
3. Verify API quotas and billing
4. Try switching to a different provider

#### Application Crashes
1. Check logs in `Logs/` directory
2. Verify all dependencies are installed
3. Run as administrator if needed
4. Check Windows Event Viewer for system errors

### Log Files

Logs are stored in the `Logs/` directory:
- `application.log`: General application logs
- `error.log`: Error-specific logs
- `voice.log`: Voice processing logs
- `llm.log`: LLM interaction logs

### Performance Optimization

#### For Better Voice Recognition
- Use a high-quality microphone
- Minimize background noise
- Speak clearly and at normal pace
- Position microphone 6-12 inches from mouth

#### For Faster Response Times
- Use local LLM models when possible
- Increase system RAM
- Use SSD storage
- Close unnecessary applications

## Development

### Project Structure

```
PersonalAiAssistant/
├── Interfaces/          # Service interfaces
├── Services/            # Core service implementations
├── Models/              # Data models and DTOs
├── Scripts/             # Setup and utility scripts
├── Logs/                # Application logs
├── Models/              # AI models (Vosk, etc.)
├── MainWindow.xaml      # Main UI
├── App.xaml            # Application entry point
└── README.md           # This file
```

### Building from Source

1. **Prerequisites**:
   - Visual Studio 2022 or VS Code
   - .NET 8.0 SDK
   - Python 3.8+

2. **Clone and Build**:
   ```bash
   git clone https://github.com/baraagh52-hue/Assistan-Trae.git
   cd Assistan-Trae
   dotnet restore
   dotnet build
   ```

3. **Run Tests**:
   ```bash
   dotnet test
   ```

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

For support and questions:
- Create an issue on GitHub
- Check the troubleshooting section
- Review the logs for error details

## Acknowledgments

- [Vosk](https://alphacephei.com/vosk/) for speech recognition
- [Kokoro TTS](https://github.com/style-bert-vits2/style-bert-vits2) for text-to-speech
- [ActivityWatch](https://activitywatch.net/) for activity tracking
- [Microsoft Graph](https://developer.microsoft.com/graph) for To-Do integration