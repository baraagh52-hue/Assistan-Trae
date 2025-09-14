#!/usr/bin/env python3
"""
Vosk Speech Recognition Setup Script
This script downloads and sets up Vosk models for the Personal AI Assistant.
"""

import os
import sys
import urllib.request
import zipfile
import json
import shutil
from pathlib import Path

def download_with_progress(url, filename):
    """Download file with progress indicator."""
    def progress_hook(block_num, block_size, total_size):
        downloaded = block_num * block_size
        if total_size > 0:
            percent = min(100, (downloaded * 100) // total_size)
            print(f"\rDownloading {filename}: {percent}%", end="", flush=True)
    
    try:
        urllib.request.urlretrieve(url, filename, progress_hook)
        print(f"\n✓ Downloaded {filename}")
        return True
    except Exception as e:
        print(f"\n✗ Failed to download {filename}: {e}")
        return False

def extract_model(zip_path, extract_to):
    """Extract Vosk model from zip file."""
    try:
        with zipfile.ZipFile(zip_path, 'r') as zip_ref:
            zip_ref.extractall(extract_to)
        print(f"✓ Extracted {zip_path}")
        return True
    except Exception as e:
        print(f"✗ Failed to extract {zip_path}: {e}")
        return False

def setup_vosk_models():
    """Download and setup Vosk models."""
    models_dir = Path("vosk_models")
    models_dir.mkdir(exist_ok=True)
    
    # Available Vosk models
    models = {
        "vosk-model-en-us-0.22": {
            "url": "https://alphacephei.com/vosk/models/vosk-model-en-us-0.22.zip",
            "size": "1.8GB",
            "description": "Large English model with high accuracy",
            "recommended": True
        },
        "vosk-model-small-en-us-0.15": {
            "url": "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip",
            "size": "40MB",
            "description": "Small English model for quick testing",
            "recommended": False
        },
        "vosk-model-en-us-0.21": {
            "url": "https://alphacephei.com/vosk/models/vosk-model-en-us-0.21.zip",
            "size": "1.8GB",
            "description": "Previous version English model",
            "recommended": False
        }
    }
    
    print("Available Vosk Models:")
    print("=" * 40)
    for i, (model_name, info) in enumerate(models.items(), 1):
        status = "[RECOMMENDED]" if info["recommended"] else ""
        print(f"{i}. {model_name} {status}")
        print(f"   Size: {info['size']}")
        print(f"   Description: {info['description']}")
        print()
    
    # Get user choice
    while True:
        try:
            choice = input("Select model to download (1-3, or 'all' for all models): ").strip().lower()
            if choice == 'all':
                selected_models = list(models.keys())
                break
            elif choice in ['1', '2', '3']:
                model_names = list(models.keys())
                selected_models = [model_names[int(choice) - 1]]
                break
            else:
                print("Invalid choice. Please enter 1, 2, 3, or 'all'.")
        except (ValueError, IndexError):
            print("Invalid choice. Please enter 1, 2, 3, or 'all'.")
    
    # Download and extract selected models
    for model_name in selected_models:
        model_info = models[model_name]
        model_dir = models_dir / model_name
        
        if model_dir.exists():
            print(f"✓ {model_name} already exists")
            continue
        
        print(f"\nDownloading {model_name} ({model_info['size']})...")
        zip_path = models_dir / f"{model_name}.zip"
        
        # Download model
        if download_with_progress(model_info["url"], str(zip_path)):
            # Extract model
            if extract_model(zip_path, models_dir):
                # Remove zip file to save space
                zip_path.unlink()
                print(f"✓ {model_name} setup completed")
            else:
                print(f"✗ Failed to extract {model_name}")
        else:
            print(f"✗ Failed to download {model_name}")
    
    return True

def create_vosk_config():
    """Create Vosk configuration file."""
    config = {
        "default_model": "vosk-model-en-us-0.22",
        "models_directory": "vosk_models",
        "sample_rate": 16000,
        "channels": 1,
        "chunk_size": 4096,
        "enable_partial_results": True,
        "enable_word_times": False,
        "log_level": "INFO",
        "alternative_models": [
            "vosk-model-small-en-us-0.15",
            "vosk-model-en-us-0.21"
        ]
    }
    
    config_path = Path("vosk_config.json")
    with open(config_path, 'w') as f:
        json.dump(config, f, indent=2)
    
    print(f"✓ Created {config_path}")
    return True

def create_test_script():
    """Create a test script for Vosk."""
    test_script = '''
#!/usr/bin/env python3
"""
Vosk Speech Recognition Test Script
"""

import json
import pyaudio
import vosk
import sys
from pathlib import Path

def test_vosk_model(model_path):
    """Test Vosk model with microphone input."""
    if not Path(model_path).exists():
        print(f"Model not found: {model_path}")
        return False
    
    try:
        # Initialize Vosk
        model = vosk.Model(model_path)
        rec = vosk.KaldiRecognizer(model, 16000)
        
        # Initialize PyAudio
        p = pyaudio.PyAudio()
        stream = p.open(
            format=pyaudio.paInt16,
            channels=1,
            rate=16000,
            input=True,
            frames_per_buffer=4096
        )
        
        print("Vosk model loaded successfully!")
        print("Speak into your microphone (press Ctrl+C to stop)...")
        
        try:
            while True:
                data = stream.read(4096, exception_on_overflow=False)
                if rec.AcceptWaveform(data):
                    result = json.loads(rec.Result())
                    if result.get('text'):
                        print(f"Recognized: {result['text']}")
                else:
                    partial = json.loads(rec.PartialResult())
                    if partial.get('partial'):
                        print(f"Partial: {partial['partial']}", end='\r')
        
        except KeyboardInterrupt:
            print("\nStopping...")
        
        finally:
            stream.stop_stream()
            stream.close()
            p.terminate()
        
        return True
    
    except Exception as e:
        print(f"Error testing Vosk model: {e}")
        return False

def main():
    """Main test function."""
    print("Vosk Speech Recognition Test")
    print("=" * 30)
    
    # Load config
    config_path = Path("vosk_config.json")
    if not config_path.exists():
        print("Config file not found. Please run setup_vosk.py first.")
        sys.exit(1)
    
    with open(config_path, 'r') as f:
        config = json.load(f)
    
    # Test default model
    model_name = config['default_model']
    model_path = Path(config['models_directory']) / model_name
    
    print(f"Testing model: {model_name}")
    if test_vosk_model(str(model_path)):
        print("✓ Vosk test completed successfully!")
    else:
        print("✗ Vosk test failed.")
        sys.exit(1)

if __name__ == "__main__":
    main()
'''
    
    with open("test_vosk.py", "w") as f:
        f.write(test_script)
    print("✓ Created test_vosk.py")

def install_python_dependencies():
    """Install required Python packages for Vosk."""
    import subprocess
    
    packages = [
        "vosk>=0.3.45",
        "pyaudio>=0.2.11",
        "numpy>=1.21.0",
        "scipy>=1.7.0"
    ]
    
    print("Installing Python dependencies for Vosk...")
    for package in packages:
        try:
            subprocess.check_call([sys.executable, "-m", "pip", "install", package])
            print(f"✓ Installed {package}")
        except subprocess.CalledProcessError as e:
            print(f"✗ Failed to install {package}: {e}")
            if "pyaudio" in package.lower():
                print("Note: PyAudio installation may require additional system dependencies.")
                print("On Windows: Install Microsoft Visual C++ Build Tools")
                print("On Ubuntu/Debian: sudo apt-get install portaudio19-dev python3-pyaudio")
                print("On macOS: brew install portaudio")
            return False
    return True

def check_system_requirements():
    """Check system requirements for Vosk."""
    print("Checking system requirements...")
    
    # Check Python version
    if sys.version_info < (3.7):
        print("✗ Python 3.7 or higher is required")
        return False
    print(f"✓ Python {sys.version_info.major}.{sys.version_info.minor} detected")
    
    # Check available disk space (rough estimate)
    import shutil
    free_space = shutil.disk_usage('.').free / (1024**3)  # GB
    if free_space < 5:
        print(f"⚠ Warning: Low disk space ({free_space:.1f}GB available). Models require 2-4GB.")
    else:
        print(f"✓ Sufficient disk space ({free_space:.1f}GB available)")
    
    return True

def main():
    """Main setup function."""
    print("Vosk Speech Recognition Setup")
    print("=" * 35)
    
    # Check system requirements
    if not check_system_requirements():
        print("System requirements not met.")
        sys.exit(1)
    
    # Install Python dependencies
    if not install_python_dependencies():
        print("Failed to install Python dependencies.")
        print("Please install them manually and run this script again.")
        sys.exit(1)
    
    # Setup Vosk models
    if not setup_vosk_models():
        print("Failed to setup Vosk models.")
        sys.exit(1)
    
    # Create configuration
    create_vosk_config()
    
    # Create test script
    create_test_script()
    
    print("\n" + "=" * 50)
    print("✓ Vosk Speech Recognition setup completed!")
    print("\nNext steps:")
    print("1. Test the installation: python test_vosk.py")
    print("2. Configure your application to use the models in 'vosk_models' directory")
    print("3. Adjust settings in 'vosk_config.json' if needed")
    print("\nModel files are located in the 'vosk_models' directory")
    print("Configuration file: vosk_config.json")

if __name__ == "__main__":
    main()