# PC Remote Control System (Smart TV Simulation)

An IoT client-server solution designed to control a Windows PC remotely via an Android device, simulating a Smart TV experience.

## 🛠 Features
* **Remote Input Control:** Simulates mouse movement and keyboard input on Windows using **Win32 API**.
* **Low Latency:** Uses **WebSockets** for real-time communication between Android and Desktop.
* **Volume & Media Control:** Dedicated controls for media consumption.

## 🏗 Architecture
* **Server:** C# .NET Console/Background Application.
* **Client:** Android Application.
* **Communication:** WebSocket Protocol.

## ⚠️ Status & Disclaimer
**Current State:** `Refactoring in Progress`
This project started as a rapid prototype to solve a personal use-case. I am currently rewriting the codebase to transition from a monolithic script to a modular, object-oriented architecture.
