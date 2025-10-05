# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
This is a Unity 2D project (Unity 2022.3.62f1) named "MalangProject" with a splash screen and main scene architecture.

## Commands

### Opening in Unity
- Open Unity Hub and select this project directory
- Unity version required: 2022.3.62f1

### Building the Project
- **Build for Standalone**: File > Build Settings > Select platform > Build
- **Build for Mobile**: File > Build Settings > Android/iOS > Switch Platform > Build

### Running in Editor
- Open the project in Unity Editor
- Load SplashScene first: File > Open Scene > Assets/Scenes/SplashScene.unity
- Press Play button in Unity Editor to test

### Testing
- Run Play Mode tests: Window > General > Test Runner > PlayMode
- Run Edit Mode tests: Window > General > Test Runner > EditMode

## Architecture

### Scene Flow
1. **SplashScene** - Initial loading screen that displays for 3 seconds
   - Managed by `SplashManager.cs`
   - Automatically transitions to MainScene

2. **MainScene** - Main application scene
   - Managed by `MainSceneManager.cs`
   - Handles fade-in animation on scene load

### Core Components

**SplashManager** (Assets/Scripts/SplashManager.cs)
- Controls splash screen duration
- Handles scene transition to MainScene
- Applies safe area for mobile devices

**MainSceneManager** (Assets/Scripts/MainManager.cs)
- Manages main scene initialization
- Controls fade-in animation using CanvasGroup
- Duration configurable through Unity Inspector

### Project Structure
- **Assets/Scripts/** - All C# game scripts
- **Assets/Scenes/** - Unity scene files (SplashScene, MainScene)
- **Assets/Images/** - Sprite and texture assets
- **ProjectSettings/** - Unity project configuration
- **Packages/** - Unity Package Manager dependencies

### Key Dependencies
- TextMeshPro - Advanced text rendering
- Unity UI (UGUI) - UI system
- Unity 2D Features - 2D animation and sprites
- Visual Scripting - Node-based scripting support

## Development Workflow

### Adding New Scripts
1. Create scripts in Assets/Scripts/
2. Follow existing naming conventions (Manager suffix for manager classes)
3. Attach scripts to GameObjects in the Unity Editor

### Scene Management
- Scene build order defined in ProjectSettings/EditorBuildSettings.asset
- Use `SceneManager.LoadScene()` for scene transitions
- Scene indices: 0 = SplashScene, 1 = MainScene

### UI Development
- Use CanvasGroup for fade effects and UI visibility control
- Implement safe area adjustments for mobile displays
- Coroutines for time-based animations and transitions