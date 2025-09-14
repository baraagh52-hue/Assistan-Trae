#!/usr/bin/env python3
"""
Kokoro TTS Server Setup Script
This script sets up the Kokoro TTS server for the Personal AI Assistant.
"""

import os
import sys
import subprocess
import urllib.request
import zipfile
import shutil
from pathlib import Path

def check_python_version():
    """Check if Python version is compatible."""
    if sys.version_info < (3, 8):
        print("Error: Python 3.8 or higher is required.")
        sys.exit(1)
    print(f"✓ Python {sys.version_info.major}.{sys.version_info.minor} detected")

def install_requirements():
    """Install required Python packages."""
    requirements = [
        "torch>=1.9.0",
        "torchaudio>=0.9.0",
        "numpy>=1.21.0",
        "scipy>=1.7.0",
        "flask>=2.0.0",
        "flask-cors>=3.0.0",
        "librosa>=0.8.0",
        "soundfile>=0.10.0",
        "requests>=2.25.0"
    ]
    
    print("Installing Python requirements...")
    for req in requirements:
        try:
            subprocess.check_call([sys.executable, "-m", "pip", "install", req])
            print(f"✓ Installed {req}")
        except subprocess.CalledProcessError:
            print(f"✗ Failed to install {req}")
            return False
    return True

def download_kokoro_model():
    """Download Kokoro model files."""
    model_dir = Path("kokoro_models")
    model_dir.mkdir(exist_ok=True)
    
    # Note: Replace these URLs with actual Kokoro model download links
    model_files = {
        "kokoro_model.pth": "https://example.com/kokoro_model.pth",
        "config.json": "https://example.com/config.json",
        "vocab.txt": "https://example.com/vocab.txt"
    }
    
    print("Downloading Kokoro model files...")
    for filename, url in model_files.items():
        filepath = model_dir / filename
        if filepath.exists():
            print(f"✓ {filename} already exists")
            continue
            
        try:
            print(f"Downloading {filename}...")
            urllib.request.urlretrieve(url, filepath)
            print(f"✓ Downloaded {filename}")
        except Exception as e:
            print(f"✗ Failed to download {filename}: {e}")
            print("Please download the model files manually and place them in the kokoro_models directory.")
            return False
    return True

def create_kokoro_server():
    """Create the Kokoro TTS server script."""
    server_script = '''
#!/usr/bin/env python3
"""
Kokoro TTS Server
HTTP API server for Kokoro text-to-speech synthesis.
"""

import os
import json
import tempfile
from pathlib import Path
from flask import Flask, request, jsonify, send_file
from flask_cors import CORS
import torch
import torchaudio
import numpy as np

app = Flask(__name__)
CORSS(app)

# Global model variables
model = None
config = None

def load_model():
    """Load the Kokoro TTS model."""
    global model, config
    
    model_dir = Path("kokoro_models")
    model_path = model_dir / "kokoro_model.pth"
    config_path = model_dir / "config.json"
    
    if not model_path.exists() or not config_path.exists():
        raise FileNotFoundError("Model files not found. Please run setup_kokoro.py first.")
    
    # Load configuration
    with open(config_path, 'r') as f:
        config = json.load(f)
    
    # Load model (placeholder - replace with actual Kokoro loading code)
    try:
        model = torch.load(model_path, map_location='cpu')
        model.eval()
        print("✓ Kokoro model loaded successfully")
    except Exception as e:
        print(f"✗ Failed to load model: {e}")
        raise

def synthesize_speech(text, voice_id="default", speed=1.0):
    """Synthesize speech from text using Kokoro."""
    if model is None:
        raise RuntimeError("Model not loaded")
    
    # Placeholder implementation - replace with actual Kokoro synthesis
    try:
        # This is a placeholder - implement actual Kokoro TTS here
        # audio_data = model.synthesize(text, voice_id=voice_id, speed=speed)
        
        # For now, return a dummy audio file
        sample_rate = 22050
        duration = len(text) * 0.1  # Rough estimate
        audio_data = np.random.randn(int(sample_rate * duration)).astype(np.float32)
        
        return audio_data, sample_rate
    except Exception as e:
        raise RuntimeError(f"Speech synthesis failed: {e}")

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint."""
    return jsonify({
        "status": "healthy",
        "model_loaded": model is not None,
        "version": "1.0.0"
    })

@app.route('/voices', methods=['GET'])
def get_voices():
    """Get available voices."""
    # Placeholder - replace with actual voice list from Kokoro
    voices = [
        {"id": "default", "name": "Default Voice", "language": "en-US"},
        {"id": "female1", "name": "Female Voice 1", "language": "en-US"},
        {"id": "male1", "name": "Male Voice 1", "language": "en-US"}
    ]
    return jsonify({"voices": voices})

@app.route('/synthesize', methods=['POST'])
def synthesize():
    """Synthesize speech from text."""
    try:
        data = request.get_json()
        if not data or 'text' not in data:
            return jsonify({"error": "Text is required"}), 400
        
        text = data['text']
        voice_id = data.get('voice_id', 'default')
        speed = data.get('speed', 1.0)
        
        if len(text) > 1000:
            return jsonify({"error": "Text too long (max 1000 characters)"}), 400
        
        # Synthesize speech
        audio_data, sample_rate = synthesize_speech(text, voice_id, speed)
        
        # Save to temporary file
        with tempfile.NamedTemporaryFile(suffix='.wav', delete=False) as tmp_file:
            torchaudio.save(tmp_file.name, torch.from_numpy(audio_data).unsqueeze(0), sample_rate)
            
            return send_file(
                tmp_file.name,
                mimetype='audio/wav',
                as_attachment=True,
                download_name='speech.wav'
            )
    
    except Exception as e:
        return jsonify({"error": str(e)}), 500

@app.route('/test', methods=['POST'])
def test_connection():
    """Test the TTS connection."""
    try:
        # Simple test synthesis
        audio_data, sample_rate = synthesize_speech("Hello, this is a test.")
        return jsonify({
            "status": "success",
            "message": "TTS test completed successfully",
            "audio_length": len(audio_data),
            "sample_rate": sample_rate
        })
    except Exception as e:
        return jsonify({
            "status": "error",
            "message": str(e)
        }), 500

if __name__ == '__main__':
    try:
        print("Starting Kokoro TTS Server...")
        load_model()
        print("Server starting on http://localhost:5000")
        app.run(host='0.0.0.0', port=5000, debug=False)
    except Exception as e:
        print(f"Failed to start server: {e}")
        sys.exit(1)
'''
    
    with open("kokoro_server.py", "w") as f:
        f.write(server_script)
    print("✓ Created kokoro_server.py")

def create_startup_script():
    """Create a startup script for the Kokoro server."""
    if os.name == 'nt':  # Windows
        script_content = '''@echo off
echo Starting Kokoro TTS Server...
python kokoro_server.py
pause
'''
        with open("start_kokoro.bat", "w") as f:
            f.write(script_content)
        print("✓ Created start_kokoro.bat")
    else:  # Unix-like
        script_content = '''#!/bin/bash
echo "Starting Kokoro TTS Server..."
python3 kokoro_server.py
'''
        with open("start_kokoro.sh", "w") as f:
            f.write(script_content)
        os.chmod("start_kokoro.sh", 0o755)
        print("✓ Created start_kokoro.sh")

def main():
    """Main setup function."""
    print("Kokoro TTS Server Setup")
    print("=" * 30)
    
    # Check Python version
    check_python_version()
    
    # Install requirements
    if not install_requirements():
        print("Setup failed during package installation.")
        sys.exit(1)
    
    # Download model (optional, may need manual download)
    print("\nNote: Model download may require manual intervention.")
    print("Please ensure you have the proper Kokoro model files.")
    
    # Create server script
    create_kokoro_server()
    
    # Create startup script
    create_startup_script()
    
    print("\n" + "=" * 50)
    print("✓ Kokoro TTS Server setup completed!")
    print("\nNext steps:")
    print("1. Ensure you have the Kokoro model files in the 'kokoro_models' directory")
    print("2. Run the server using:")
    if os.name == 'nt':
        print("   start_kokoro.bat")
    else:
        print("   ./start_kokoro.sh")
    print("3. Test the server at http://localhost:5000/health")
    print("\nThe server will be available at http://localhost:5000")

if __name__ == "__main__":
    main()