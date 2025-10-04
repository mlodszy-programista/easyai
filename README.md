# easyai

**EasyAI** is a lightweight C# application that makes it easy to run local AI models through a simple web interface.

Designed with simplicity and flexibility in mind, EasyAI allows you to serve your own AI models locally and interact with them via a minimal HTML front-end — all without the need for complex setups or cloud dependencies.

## ✨ Features

- 🧠 **Run Local AI Models** — Easily host and interact with local `.gguf` models.
- 🌐 **Web Interface** — Includes a minimal HTML front-end for interacting with the model in your browser.
- ⚙️ **Configurable** — Define paths, default models, and other parameters in a simple `appsettings.json` file.
- 🖥️ **Windows Service Support** — Optionally run EasyAI as a Windows service for always-on availability.

## 🛠️ Getting Started

1. Clone the repository.
2. Configure the `appsettings.json` file:
   - Set the path to your model directory.
   - Choose a default `.gguf` model.
3. Run the application — either as a standalone app or install it as a Windows service.


## Acknowledgements

This project uses the following open-source components:

- [**LlamaSharp**](https://github.com/SciSharp/LLamaSharp) – MIT License  
  A C# binding for llama.cpp, providing a managed interface for running LLaMA models.

- [**llama.cpp**](https://github.com/ggerganov/llama.cpp) – MIT License  
  Backend engine used by LlamaSharp for model inference.

- [**Meta Llama 3**](https://ai.meta.com/llama) model weights – © Meta Platforms, Inc.  
  Used under the Meta Llama 3 Community License.
