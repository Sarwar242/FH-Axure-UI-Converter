# Code Conversion Tool

A web-based migration tool that converts .NET Framework 4.x Web Forms applications to .NET 8 Blazor applications.

## 🚀 Features

- **Web Forms to Blazor Conversion**: Transform .aspx files into Blazor components
- **Code-Behind Migration**: Convert event handlers and page logic to Blazor paradigms
- **Validation Framework**: Transform validation controls to Blazor validation

## 🛠️ Tech Stack

- **.NET MVC Web Application**
- **HtmlAgilityPack**: For HTML/ASPX document parsing
- **Roslyn Compiler Platform**: Powers code analysis and transformation
- **Advanced Chain Processing**: Sophisticated conversion pipeline architecture

## 📋 Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or later (recommended)

## 🔧 Installation

```bash
# Clone this repository
git clone https://github.com/Sarwar242/FH-Axure-UI-Converter.git
# Navigate to project directory
cd code-conversion-tool

# Build the solution
dotnet build

# Run the application
dotnet run --project src/WebApp/WebApp.csproj
```

## 💻 Usage

1. Access the tool via your browser at `http://localhost:5000`
2. Upload your .NET 4.x Web Forms project (.zip or folder)
3. Configure conversion settings
4. Start the conversion process
5. Download the generated .NET 8 Blazor application

## 🔄 How It Works

The conversion process follows these steps:

1. **Analysis**: Scans all Web Forms files and their code-behind
2. **Structure Mapping**: Creates a blueprint of the Blazor application structure
3. **HTML Conversion**: Transforms ASPX markup to Razor syntax
4. **Code Transformation**: Converts C# code-behind to Blazor component code
5. **Project Generation**: Creates a complete .NET 8 Blazor project

## ⚠️ Limitations

- Complex custom controls may require manual adjustments
- Third-party control integration needs additional configuration
- Business logic with heavy dependency on Web Forms lifecycle may need refactoring

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.

## 📞 Contact

Your Name - [@your_twitter](https://twitter.com/your_twitter) - email@example.com

Project Link: [https://github.com/yourusername/code-conversion-tool](https://github.com/yourusername/code-conversion-tool)
