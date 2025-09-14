#!/usr/bin/env python3
"""
Personal AI Assistant - Complete Setup Script
This script sets up all external dependencies for the Personal AI Assistant.
"""

import os
import sys
import subprocess
import json
import urllib.request
import zipfile
import shutil
from pathlib import Path

def print_header(title):
    """Print a formatted header."""
    print("\n" + "=" * 60)
    print(f" {title}")
    print("=" * 60)

def print_step(step_num, total_steps, description):
    """Print a formatted step."""
    print(f"\n[{step_num}/{total_steps}] {description}")
    print("-" * 40)

def check_python_version():
    """Check if Python version is compatible."""
    if sys.version_info < (3.8):
        print("✗ Error: Python 3.8 or higher is required.")
        print(f"  Current version: {sys.version_info.major}.{sys.version_info.minor}")
        return False
    print(f"✓ Python {sys.version_info.major}.{sys.version_info.minor} detected")
    return True

def check_dotnet():
    """Check if .NET is installed."""
    try:
        result = subprocess.run(["dotnet", "--version"], 
                              capture_output=True, text=True, check=True)
        version = result.stdout.strip()
        print(f"✓ .NET {version} detected")
        return True
    except (subprocess.CalledProcessError, FileNotFoundError):
        print("✗ .NET not found")
        print("  Please install .NET 6.0 or higher from https://dotnet.microsoft.com/download")
        return False

def install_python_packages():
    """Install all required Python packages."""
    packages = [
        # Vosk dependencies
        "vosk>=0.3.45",
        "pyaudio>=0.2.11",
        
        # Kokoro dependencies
        "torch>=1.9.0",
        "torchaudio>=0.9.0",
        "flask>=2.0.0",
        "flask-cors>=3.0.0",
        "librosa>=0.8.0",
        "soundfile>=0.10.0",
        
        # Common dependencies
        "numpy>=1.21.0",
        "scipy>=1.7.0",
        "requests>=2.25.0"
    ]
    
    print("Installing Python packages...")
    failed_packages = []
    
    for package in packages:
        try:
            print(f"Installing {package}...", end=" ")
            subprocess.check_call(
                [sys.executable, "-m", "pip", "install", package],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL
            )
            print("✓")
        except subprocess.CalledProcessError:
            print("✗")
            failed_packages.append(package)
    
    if failed_packages:
        print(f"\n⚠ Failed to install: {', '.join(failed_packages)}")
        if any("pyaudio" in pkg for pkg in failed_packages):
            print("\nPyAudio installation help:")
            print("  Windows: Install Microsoft Visual C++ Build Tools")
            print("  Ubuntu/Debian: sudo apt-get install portaudio19-dev python3-pyaudio")
            print("  macOS: brew install portaudio")
        return False
    
    print("✓ All Python packages installed successfully")
    return True

def setup_directories():
    """Create necessary directories."""
    directories = [
        "vosk_models",
        "kokoro_models",
        "logs",
        "config",
        "temp"
    ]
    
    print("Creating directories...")
    for directory in directories:
        Path(directory).mkdir(exist_ok=True)
        print(f"✓ Created {directory}/")
    
    return True

def download_vosk_model():
    """Download a lightweight Vosk model for testing."""
    model_name = "vosk-model-small-en-us-0.15"
    model_url = f"https://alphacephei.com/vosk/models/{model_name}.zip"
    model_dir = Path("vosk_models") / model_name
    
    if model_dir.exists():
        print(f"✓ {model_name} already exists")
        return True
    
    print(f"Downloading {model_name} (40MB)...")
    zip_path = Path("vosk_models") / f"{model_name}.zip"
    
    try:
        urllib.request.urlretrieve(model_url, str(zip_path))
        print("✓ Download completed")
        
        print("Extracting model...")
        with zipfile.ZipFile(zip_path, 'r') as zip_ref:
            zip_ref.extractall("vosk_models")
        
        zip_path.unlink()  # Remove zip file
        print("✓ Model extracted successfully")
        return True
    
    except Exception as e:
        print(f"✗ Failed to download Vosk model: {e}")
        print("  You can download it manually from:")
        print(f"  {model_url}")
        return False

def create_configuration_files():
    """Create default configuration files."""
    # Vosk configuration
    vosk_config = {
        "default_model": "vosk-model-small-en-us-0.15",
        "models_directory": "vosk_models",
        "sample_rate": 16000,
        "channels": 1,
        "chunk_size": 4096,
        "enable_partial_results": True,
        "log_level": "INFO"
    }
    
    with open("config/vosk_config.json", "w") as f:
        json.dump(vosk_config, f, indent=2)
    print("✓ Created vosk_config.json")
    
    # Kokoro configuration
    kokoro_config = {
        "server_url": "http://localhost:5000",
        "default_voice": "default",
        "timeout": 30,
        "max_text_length": 1000,
        "audio_format": "wav",
        "sample_rate": 22050
    }
    
    with open("config/kokoro_config.json", "w") as f:
        json.dump(kokoro_config, f, indent=2)
    print("✓ Created kokoro_config.json")
    
    # Application configuration template
    app_config = {
        "voice_services": {
            "wake_word": {
                "enabled": True,
                "sensitivity": 0.5,
                "keywords": ["hey assistant", "computer"]
            },
            "stt": {
                "enabled": True,
                "model_path": "vosk_models/vosk-model-small-en-us-0.15",
                "timeout": 5
            },
            "tts": {
                "enabled": True,
                "server_url": "http://localhost:5000",
                "voice": "default"
            }
        },
        "llm": {
            "provider": "openai",
            "api_key": "your_api_key_here",
            "model": "gpt-3.5-turbo",
            "max_tokens": 150
        },
        "microsoft_todo": {
            "client_id": "your_client_id_here",
            "tenant_id": "common"
        },
        "prayer_times": {
            "latitude": 0.0,
            "longitude": 0.0,
            "method": 2,
            "timezone": "UTC"
        },
        "activity_watch": {
            "server_url": "http://localhost:5600",
            "enabled": False
        },
        "logging": {
            "level": "INFO",
            "file": "logs/app.log",
            "max_size_mb": 10
        }
    }
    
    with open("config/app_config.json", "w") as f:
        json.dump(app_config, f, indent=2)
    print("✓ Created app_config.json template")
    
    return True

def create_startup_scripts():
    """Create startup scripts for external services."""
    # Kokoro server startup script
    if os.name == 'nt':  # Windows
        kokoro_script = '''@echo off
echo Starting Kokoro TTS Server...
cd /d "%~dp0"
python setup_kokoro.py
if exist kokoro_server.py (
    python kokoro_server.py
) else (
    echo Kokoro server not found. Please run setup_kokoro.py first.
    pause
)
'''
        with open("start_kokoro.bat", "w") as f:
            f.write(kokoro_script)
        print("✓ Created start_kokoro.bat")
        
        # Main application startup script
        app_script = '''@echo off
echo Starting Personal AI Assistant...
cd /d "%~dp0"
if exist "bin\\Debug\\net6.0-windows\\PersonalAiAssistant.exe" (
    start "" "bin\\Debug\\net6.0-windows\\PersonalAiAssistant.exe"
) else if exist "bin\\Release\\net6.0-windows\\PersonalAiAssistant.exe" (
    start "" "bin\\Release\\net6.0-windows\\PersonalAiAssistant.exe"
) else (
    echo Application not built. Building now...
    dotnet build
    if exist "bin\\Debug\\net6.0-windows\\PersonalAiAssistant.exe" (
        start "" "bin\\Debug\\net6.0-windows\\PersonalAiAssistant.exe"
    ) else (
        echo Build failed. Please check for errors.
        pause
    )
)
'''
        with open("start_app.bat", "w") as f:
            f.write(app_script)
        print("✓ Created start_app.bat")
    
    else:  # Unix-like
        kokoro_script = '''#!/bin/bash
echo "Starting Kokoro TTS Server..."
cd "$(dirname "$0")"
python3 setup_kokoro.py
if [ -f "kokoro_server.py" ]; then
    python3 kokoro_server.py
else
    echo "Kokoro server not found. Please run setup_kokoro.py first."
fi
'''
        with open("start_kokoro.sh", "w") as f:
            f.write(kokoro_script)
        os.chmod("start_kokoro.sh", 0o755)
        print("✓ Created start_kokoro.sh")
        
        app_script = '''#!/bin/bash
echo "Starting Personal AI Assistant..."
cd "$(dirname "$0")"
if [ -f "bin/Debug/net6.0/PersonalAiAssistant" ]; then
    ./bin/Debug/net6.0/PersonalAiAssistant
elif [ -f "bin/Release/net6.0/PersonalAiAssistant" ]; then
    ./bin/Release/net6.0/PersonalAiAssistant
else
    echo "Application not built. Building now..."
    dotnet build
    if [ -f "bin/Debug/net6.0/PersonalAiAssistant" ]; then
        ./bin/Debug/net6.0/PersonalAiAssistant
    else
        echo "Build failed. Please check for errors."
    fi
fi
'''
        with open("start_app.sh", "w") as f:
            f.write(app_script)
        os.chmod("start_app.sh", 0o755)
        print("✓ Created start_app.sh")
    
    return True

def build_dotnet_application():
    """Build the .NET application."""
    try:
        print("Building .NET application...")
        result = subprocess.run(
            ["dotnet", "build", "--configuration", "Debug"],
            capture_output=True,
            text=True,
            check=True
        )
        print("✓ Application built successfully")
        return True
    except subprocess.CalledProcessError as e:
        print(f"✗ Build failed: {e}")
        print("Build output:")
        print(e.stdout)
        print(e.stderr)
        return False

def create_readme():
    """Create a comprehensive README file."""
    readme_content = '''# Personal AI Assistant

A comprehensive voice-activated AI assistant with wake word detection, speech-to-text, text-to-speech, and integration with Microsoft To-Do, prayer times, and activity tracking.

## Quick Start

### Prerequisites
- .NET 6.0 or higher
- Python 3.8 or higher
- Microphone and speakers

### Setup

1. **Run the complete setup script:**
   ```bash
   python Scripts/setup_all.py
   ```

2. **Configure your API keys:**
   Edit `config/app_config.json` and add your:
   - OpenAI API key (for LLM)
   - Microsoft Azure App registration details (for To-Do integration)

3. **Start the application:**
   - Windows: Double-click `start_app.bat`
   - Linux/macOS: `./start_app.sh`

### External Services

#### Kokoro TTS Server
Start the TTS server before using voice features:
- Windows: `start_kokoro.bat`
- Linux/macOS: `./start_kokoro.sh`

#### ActivityWatch (Optional)
For activity tracking, install and run ActivityWatch:
- Download from: https://activitywatch.net/
- Start before launching the assistant

## Features

- **Wake Word Detection**: "Hey Assistant" or "Computer"
- **Speech Recognition**: Offline using Vosk
- **Text-to-Speech**: High-quality synthesis via Kokoro
- **AI Conversations**: OpenAI GPT integration
- **Microsoft To-Do**: Task management
- **Prayer Times**: Islamic prayer time notifications
- **Activity Tracking**: Integration with ActivityWatch
- **Modern GUI**: WPF interface with dark theme

## Configuration

### Voice Services
- **Wake Word**: Adjust sensitivity in `config/app_config.json`
- **STT**: Configure Vosk model in `config/vosk_config.json`
- **TTS**: Set voice preferences in `config/kokoro_config.json`

### API Integrations
- **OpenAI**: Add API key to `config/app_config.json`
- **Microsoft Graph**: Configure client ID and tenant
- **Prayer Times**: Set location coordinates

## Troubleshooting

### Common Issues

1. **PyAudio installation fails:**
   - Windows: Install Visual C++ Build Tools
   - Ubuntu: `sudo apt-get install portaudio19-dev python3-pyaudio`
   - macOS: `brew install portaudio`

2. **Kokoro server won't start:**
   - Ensure Python dependencies are installed
   - Check if port 5000 is available
   - Run `python Scripts/setup_kokoro.py` again

3. **Vosk model not found:**
   - Run `python Scripts/setup_vosk.py`
   - Download models manually if needed

4. **Application won't build:**
   - Ensure .NET 6.0+ is installed
   - Run `dotnet restore` then `dotnet build`

### Logs
Check `logs/app.log` for detailed error information.

## Development

### Project Structure
```
PersonalAiAssistant/
├── Interfaces/          # Service interfaces
├── Services/           # Service implementations
├── Models/            # Data models and DTOs
├── Scripts/           # Setup and utility scripts
├── config/            # Configuration files
├── logs/              # Application logs
├── vosk_models/       # Speech recognition models
└── kokoro_models/     # TTS models
```

### Adding New Features
1. Define interface in `Interfaces/`
2. Implement service in `Services/`
3. Register in `App.xaml.cs` dependency injection
4. Update UI in `MainWindow.xaml`

## License

This project is licensed under the MIT License.

## Contributing

Contributions are welcome! Please read the contributing guidelines before submitting pull requests.
'''
    
    with open("README.md", "w") as f:
        f.write(readme_content)
    print("✓ Created README.md")
    return True

def main():
    """Main setup function."""
    print_header("Personal AI Assistant - Complete Setup")
    print("This script will set up all dependencies for the Personal AI Assistant.")
    print("Estimated time: 5-15 minutes (depending on download speeds)")
    
    input("\nPress Enter to continue...")
    
    total_steps = 9
    current_step = 0
    
    # Step 1: Check system requirements
    current_step += 1
    print_step(current_step, total_steps, "Checking system requirements")
    if not check_python_version() or not check_dotnet():
        print("\n✗ System requirements not met. Please install missing components.")
        sys.exit(1)
    
    # Step 2: Create directories
    current_step += 1
    print_step(current_step, total_steps, "Creating project directories")
    setup_directories()
    
    # Step 3: Install Python packages
    current_step += 1
    print_step(current_step, total_steps, "Installing Python dependencies")
    if not install_python_packages():
        print("\n⚠ Some Python packages failed to install.")
        print("You may need to install them manually.")
    
    # Step 4: Download Vosk model
    current_step += 1
    print_step(current_step, total_steps, "Downloading Vosk speech recognition model")
    download_vosk_model()
    
    # Step 5: Create configuration files
    current_step += 1
    print_step(current_step, total_steps, "Creating configuration files")
    create_configuration_files()
    
    # Step 6: Create startup scripts
    current_step += 1
    print_step(current_step, total_steps, "Creating startup scripts")
    create_startup_scripts()
    
    # Step 7: Build .NET application
    current_step += 1
    print_step(current_step, total_steps, "Building .NET application")
    if not build_dotnet_application():
        print("\n⚠ Application build failed. You may need to build manually.")
    
    # Step 8: Create documentation
    current_step += 1
    print_step(current_step, total_steps, "Creating documentation")
    create_readme()
    
    # Step 9: Final setup
    current_step += 1
    print_step(current_step, total_steps, "Finalizing setup")
    
    print_header("Setup Complete!")
    print("✓ Personal AI Assistant setup completed successfully!")
    print("\nNext steps:")
    print("1. Configure API keys in config/app_config.json")
    print("2. Start Kokoro TTS server (optional):")
    if os.name == 'nt':
        print("   - Double-click start_kokoro.bat")
        print("3. Start the application:")
        print("   - Double-click start_app.bat")
    else:
        print("   - ./start_kokoro.sh")
        print("3. Start the application:")
        print("   - ./start_app.sh")
    print("\nFor detailed instructions, see README.md")
    print("\nEnjoy your Personal AI Assistant! 🤖")

if __name__ == "__main__":
    main()