# SmartAgent Mobile App

SmartAgent is a modern, mobile-friendly chatbot application built with .NET 9 Blazor Server. It's designed to provide a seamless user experience across devices, with a focus on mobile usability.

## Features

- **Chat Interface**: A modern, ChatGPT-inspired chat interface for interacting with the AI agent
- **Workflows**: View, create, and edit automated workflows
- **History**: Browse past conversations organized by date
- **Planned Tasks**: Schedule and manage automated workflow tasks

## Technical Overview

SmartAgent is built using:

- **.NET 9 Blazor Server**: For server-side rendering and real-time interactivity
- **SignalR**: For real-time chat communication
- **Minimal APIs**: RESTful endpoints for workflow management
- **Bootstrap & Bootstrap Icons**: For responsive UI components and icons
- **Custom CSS**: Mobile-first design using smartagent.css

## Architecture

The application follows a clean architecture pattern:

- **Services Layer**: Handles API communication and business logic
  - `ChatService`: Manages real-time chat communication via SignalR
  - `WorkflowService`: Handles workflow operations (CRUD, scheduling, execution)
  - `HistoryService`: Manages conversation history retrieval

- **UI Components**: Blazor components organized by feature
  - Chat components for messaging
  - Workflow management components
  - History browsing components
  - Task scheduling components

## Workflow Run Feedback

When running workflows, users receive real-time feedback:

- A spinning gear animation indicates a workflow is currently running
- Status messages display the outcome (Completed/Failed/Error)
- Color-coded status indicators provide quick visual cues (green for success, red for errors)
- Real-time updates are pushed to the UI via event subscriptions
- Both manual Workflows and Planned Tasks use the same feedback system

The implementation uses a central tracking system in `WorkflowService` to:

1. Track running workflows with their start times and status
2. Broadcast status changes via events to subscribed components
3. Automatically clear success messages after a delay

## Conversation History Detail

The Conversation Detail view provides an enhanced experience for reviewing past interactions:

- **Timeline View**: Messages are displayed chronologically with visual differentiation between user and agent messages
- **Animated Entry**: Messages use smooth fade-in animations for better readability
- **Action Tracking**: Actions performed by the agent (email sending, file creation, etc.) are grouped and color-coded
- **Quick Actions**: Context-sensitive actions for individual messages (copy, share)
- **Export Options**: Options to export the entire conversation as text
- **Enhanced Loading State**: Skeleton loading UI while data is being fetched
- **Mobile Optimized**: Touch-friendly design with proper spacing and tap targets

The implementation includes:

1. Toast notification system for feedback on actions (copying, exporting)
2. Adaptive message layout based on sender (user/agent)
3. Visual differentiation for various action types with color-coded badges
4. Lazy-loading with skeleton placeholder UI
4. Provide consistent status information across UI components

## Getting Started

1. Run the AppHost project to start both the Agent and WebChat apps
2. Open your browser to the provided URL
3. Start chatting with the agent immediately!

## Mobile Features

The app is designed with mobile in mind:

- Bottom navigation bar for easy access to all features
- Touch-friendly controls and adequate tap targets
- Responsive layout that adapts to screen sizes
- Mobile-optimized chat interface

## Future Enhancements

- Offline support with local storage
- Push notifications for scheduled tasks
- Enhanced voice input and output
- Multi-language support
- Theme customization

## Development Guide

To continue development:

1. The main UI components are in `/src/WebChat/Components/Pages`
2. Service classes are in `/src/WebChat/Services`
3. Styling is in `/src/WebChat/wwwroot/css/smartagent.css`
4. Backend API endpoints are defined in the Agent project using minimal APIs

Use comments to document code changes for easier iteration with Copilot.
