#!/bin/bash

# Personal AI Assistant - Linux/Raspberry Pi Setup Script
# This script sets up the Personal AI Assistant on Linux systems including Raspberry Pi

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to detect OS
detect_os() {
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        if [[ -f /etc/os-release ]]; then
            . /etc/os-release
            OS=$NAME
            VER=$VERSION_ID
        elif type lsb_release >/dev/null 2>&1; then
            OS=$(lsb_release -si)
            VER=$(lsb_release -sr)
        else
            OS=$(uname -s)
            VER=$(uname -r)
        fi
    else
        OS=$(uname -s)
        VER=$(uname -r)
    fi
    
    # Check if running on Raspberry Pi
    if [[ -f /proc/device-tree/model ]] && grep -q "Raspberry Pi" /proc/device-tree/model; then
        IS_RASPBERRY_PI=true
    else
        IS_RASPBERRY_PI=false
    fi
}

# Function to install .NET 8
install_dotnet() {
    print_status "Installing .NET 8..."
    
    if command_exists dotnet; then
        DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "0.0.0")
        if [[ "$DOTNET_VERSION" == 8.* ]]; then
            print_success ".NET 8 is already installed (version $DOTNET_VERSION)"
            return
        fi
    fi
    
    # Download and run the official .NET install script
    print_status "Installing .NET 8 using official Microsoft installer..."
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
    chmod +x dotnet-install.sh
    
    # Install .NET 8 SDK using the official script
    ./dotnet-install.sh --channel 8.0 --install-dir $HOME/.dotnet
    rm dotnet-install.sh
    
    # Add to PATH
    echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
    echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
    export DOTNET_ROOT=$HOME/.dotnet
    export PATH=$PATH:$HOME/.dotnet
    
    print_success ".NET 8 installed successfully"
}

# Function to install system dependencies
install_system_dependencies() {
    print_status "Installing system dependencies..."
    
    # Detect package manager and install dependencies
    if command_exists apt-get; then
        # Debian/Ubuntu/Raspberry Pi OS
        sudo apt-get update
        sudo apt-get install -y \
            wget \
            curl \
            git \
            build-essential \
            libasound2-dev \
            portaudio19-dev \
            python3 \
            python3-pip \
            ffmpeg \
            pulseaudio \
            alsa-utils
            
        # Install additional packages for Raspberry Pi
        if [[ "$IS_RASPBERRY_PI" == true ]]; then
            sudo apt-get install -y \
                raspberrypi-kernel-headers \
                gpio \
                wiringpi
        fi
        
    elif command_exists yum; then
        # RHEL/CentOS/Fedora
        sudo yum update -y
        sudo yum install -y \
            wget \
            curl \
            git \
            gcc \
            gcc-c++ \
            make \
            alsa-lib-devel \
            portaudio-devel \
            python3 \
            python3-pip \
            ffmpeg \
            pulseaudio \
            alsa-utils
            
    elif command_exists pacman; then
        # Arch Linux
        sudo pacman -Syu --noconfirm
        sudo pacman -S --noconfirm \
            wget \
            curl \
            git \
            base-devel \
            alsa-lib \
            portaudio \
            python \
            python-pip \
            ffmpeg \
            pulseaudio \
            alsa-utils
    else
        print_error "Unsupported package manager. Please install dependencies manually."
        exit 1
    fi
    
    print_success "System dependencies installed"
}

# Function to setup audio
setup_audio() {
    print_status "Setting up audio system..."
    
    # Start PulseAudio if not running
    if ! pgrep -x "pulseaudio" > /dev/null; then
        pulseaudio --start --log-target=syslog
    fi
    
    # Test audio devices
    print_status "Available audio devices:"
    aplay -l || true
    
    if [[ "$IS_RASPBERRY_PI" == true ]]; then
        print_status "Configuring Raspberry Pi audio..."
        # Enable audio on Raspberry Pi
        sudo raspi-config nonint do_audio 0
        
        # Set audio output to auto
        sudo amixer cset numid=3 0
    fi
    
    print_success "Audio system configured"
}

# Function to create application directory
setup_app_directory() {
    print_status "Setting up application directory..."
    
    APP_DIR="$HOME/.personal-ai-assistant"
    mkdir -p "$APP_DIR"
    mkdir -p "$APP_DIR/logs"
    mkdir -p "$APP_DIR/models"
    mkdir -p "$APP_DIR/config"
    
    # Create default configuration
    cat > "$APP_DIR/config/appsettings.json" << EOF
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
  "VoskModelPath": "$APP_DIR/models/vosk",
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
    
    print_success "Application directory created at $APP_DIR"
}

# Function to download Vosk model
download_vosk_model() {
    print_status "Downloading Vosk speech recognition model..."
    
    VOSK_MODEL_DIR="$HOME/.personal-ai-assistant/models/vosk"
    
    if [[ -d "$VOSK_MODEL_DIR" && -f "$VOSK_MODEL_DIR/am/final.mdl" ]]; then
        print_success "Vosk model already exists"
        return
    fi
    
    mkdir -p "$VOSK_MODEL_DIR"
    
    # Download appropriate model based on architecture
    if [[ "$IS_RASPBERRY_PI" == true ]]; then
        # Smaller model for Raspberry Pi
        MODEL_URL="https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip"
        MODEL_FILE="vosk-model-small-en-us-0.15.zip"
    else
        # Full model for regular Linux
        MODEL_URL="https://alphacephei.com/vosk/models/vosk-model-en-us-0.22.zip"
        MODEL_FILE="vosk-model-en-us-0.22.zip"
    fi
    
    print_status "Downloading $MODEL_FILE..."
    wget "$MODEL_URL" -O "/tmp/$MODEL_FILE"
    
    print_status "Extracting model..."
    cd /tmp
    unzip -q "$MODEL_FILE"
    
    # Move extracted files to the correct location
    EXTRACTED_DIR=$(find . -name "vosk-model-*" -type d | head -1)
    if [[ -n "$EXTRACTED_DIR" ]]; then
        mv "$EXTRACTED_DIR"/* "$VOSK_MODEL_DIR/"
        rm -rf "$EXTRACTED_DIR"
    fi
    
    rm "/tmp/$MODEL_FILE"
    
    print_success "Vosk model downloaded and installed"
}

# Function to build the application
build_application() {
    print_status "Building Personal AI Assistant..."
    
    if [[ ! -f "PersonalAiAssistant.csproj" ]]; then
        print_error "PersonalAiAssistant.csproj not found. Please run this script from the project directory."
        exit 1
    fi
    
    # Ensure we use the correct .NET installation
    export DOTNET_ROOT=$HOME/.dotnet
    export PATH=$HOME/.dotnet:$PATH
    
    # Restore packages for Linux target only
    $HOME/.dotnet/dotnet restore --framework net8.0
    
    # Build for current platform
    if [[ "$IS_RASPBERRY_PI" == true ]]; then
        $HOME/.dotnet/dotnet build -c Release -f net8.0 -r linux-arm64
    else
        $HOME/.dotnet/dotnet build -c Release -f net8.0 -r linux-x64
    fi
    
    print_success "Application built successfully"
}

# Function to create startup script
create_startup_script() {
    print_status "Creating startup script..."
    
    SCRIPT_PATH="$HOME/.local/bin/personal-ai-assistant"
    mkdir -p "$(dirname "$SCRIPT_PATH")"
    
    # Get the absolute path of the current directory
    PROJECT_DIR="$(pwd)"
    
    cat > "$SCRIPT_PATH" << EOF
#!/bin/bash
# Personal AI Assistant Startup Script

cd "$PROJECT_DIR"
export DOTNET_ROOT=\$HOME/.dotnet
export PATH=\$HOME/.dotnet:\$PATH

# Set audio environment
export PULSE_RUNTIME_PATH=/run/user/\$(id -u)/pulse

# Run the application
if [[ "$IS_RASPBERRY_PI" == true ]]; then
    \$HOME/.dotnet/dotnet run --configuration Release --framework net8.0 --runtime linux-arm64
else
    \$HOME/.dotnet/dotnet run --configuration Release --framework net8.0 --runtime linux-x64
fi
EOF
    
    chmod +x "$SCRIPT_PATH"
    
    # Add to PATH if not already there
    if [[ ":$PATH:" != *":$HOME/.local/bin:"* ]]; then
        echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
    fi
    
    print_success "Startup script created at $SCRIPT_PATH"
}

# Function to create systemd service (optional)
create_systemd_service() {
    print_status "Creating systemd service (optional)..."
    
    read -p "Do you want to create a systemd service to auto-start the assistant? (y/N): " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        SERVICE_FILE="$HOME/.config/systemd/user/personal-ai-assistant.service"
        mkdir -p "$(dirname "$SERVICE_FILE")"
        
        cat > "$SERVICE_FILE" << EOF
[Unit]
Description=Personal AI Assistant
After=pulseaudio.service

[Service]
Type=simple
ExecStart=$HOME/.local/bin/personal-ai-assistant
Restart=always
RestartSec=10
Environment=HOME=$HOME
Environment=XDG_RUNTIME_DIR=/run/user/%i

[Install]
WantedBy=default.target
EOF
        
        # Enable and start the service
        systemctl --user daemon-reload
        systemctl --user enable personal-ai-assistant.service
        
        print_success "Systemd service created and enabled"
        print_status "You can start it with: systemctl --user start personal-ai-assistant"
    fi
}

# Main installation function
main() {
    echo "======================================"
    echo "Personal AI Assistant - Linux Setup"
    echo "======================================"
    echo
    
    # Detect OS and architecture
    detect_os
    print_status "Detected OS: $OS $VER"
    if [[ "$IS_RASPBERRY_PI" == true ]]; then
        print_status "Raspberry Pi detected"
    fi
    echo
    
    # Check if running as root
    if [[ $EUID -eq 0 ]]; then
        print_error "This script should not be run as root. Please run as a regular user."
        exit 1
    fi
    
    # Install dependencies
    install_system_dependencies
    install_dotnet
    setup_audio
    setup_app_directory
    download_vosk_model
    build_application
    create_startup_script
    create_systemd_service
    
    echo
    print_success "Installation completed successfully!"
    echo
    print_status "Next steps:"
    echo "1. Configure your API keys in ./appsettings.json"
    echo "2. Restart your terminal or run: source ~/.bashrc"
    echo "3. Run the assistant with: personal-ai-assistant"
    echo "   Or run directly: dotnet run --configuration Release"
    echo "4. Or start the systemd service: systemctl --user start personal-ai-assistant"
    echo
    print_status "For troubleshooting, check logs in ~/.personal-ai-assistant/logs/"
    echo
}

# Run main function
main "$@"