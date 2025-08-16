# 🧟 ZombieLynxBot

A comprehensive Discord bot built with Discord.Net for the Zombie Lynx Gaming community, featuring advanced ticket management, suggestion systems, and gaming server integration.

## 🎯 Overview

ZombieLynxBot is a feature-rich Discord bot designed specifically for gaming communities, with a focus on multi-game server support, sophisticated ticket management, and community engagement features. The bot manages support tickets across multiple game servers including ARK: Survival Evolved, ARK: Survival Ascended, ECO, Minecraft, Rust, and Empyrion.

## 🚀 Key Features

### 🎫 Advanced Ticket System

- **Multi-Game Support**: Separate ticket categories for ASE, ASA, ECO, Minecraft, Rust, and Empyrion
- **Database Integration**: PostgreSQL database for persistent ticket storage and user management
- **Dynamic Channel Creation**: Automatic private channel creation for each ticket
- **Transcript Generation**: HTML transcript generation with message history
- **Ticket Lifecycle Management**: Complete workflow from creation to closure with logging
- **User Registration Integration**: Links with ZLG website user accounts
- **Permission Management**: Automatic channel permissions and role-based access

### 💡 Suggestion System

- **Game-Specific Suggestions**: Dedicated suggestion channels for each supported game
- **Voting Mechanism**: Automatic upvote/downvote reactions with time-limited voting
- **Auto-Expiration**: Automatic suggestion closure after 5 days
- **Rich Embeds**: Professional formatting with user avatars and timestamps

### 🛠️ Moderation Tools

- **Add to Ticket**: Moderators can add users to existing tickets
- **Message Management**: Delete message commands for moderators
- **Permission Checks**: Role-based and user ID-based permission validation
- **Admin Controls**: Comprehensive admin role and user management

### 🎮 Gaming Integration

- **Multi-Server Support**: Configuration for multiple game servers per game type
- **Server Selection**: Dynamic dropdown menus for server selection during ticket creation
- **Game-Specific Workflows**: Tailored ticket flows for different game types

## 🏗️ Technical Architecture

### Technologies Stack

- **Framework**: .NET 8.0
- **Discord Library**: Discord.Net v3.13.1 with Interactions framework
- **Database**: PostgreSQL with Entity Framework Core 9.0.2
- **Logging**: Serilog with file and console output
- **Configuration**: JSON-based configuration with hot-reload support
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection

### Core Components

#### 🔧 Services Layer

- **`TicketService`**: Core ticket CRUD operations and business logic
- **`TicketChannelService`**: Discord channel management for tickets
- **`TicketEmbedFactory`**: Rich embed generation for ticket displays
- **`TranscriptBuilder`**: HTML transcript generation from message history
- **`UserCardService`**: User profile card generation
- **`TimeoutMonitorService`**: Ticket inactivity monitoring
- **`SuggestionHandler`**: Suggestion submission and voting management
- **`SuggestionExpirationService`**: Background service for suggestion lifecycle

#### 🎛️ Interaction Modules

- **`TicketCreationModule`**: Handles ticket form submissions and channel creation
- **`TicketCloseModule`**: Manages ticket closure workflow
- **`TicketReassignModule`**: Ticket ownership transfer functionality
- **`TicketOwnerSelectModule`**: User selection for ticket assignments

#### 📋 Slash Commands

- **`/ticket-create`**: Sets up ticket creation buttons (Admin only)
- **`/ping`**: Bot health check command
- **`/addtoticket`**: Add users to existing tickets (Moderator only)
- **Game-specific suggestion buttons**: ASE, ASA, ECO, Minecraft, Rust, Empyrion

#### 🗄️ Data Models

- **`Ticket`**: Core ticket entity with full audit trail
- **`Message`**: Message storage for transcript generation
- **`UserProfile`**: User account integration with ZLG website
- **`ZLGMember`**: Discord-to-website user mapping
- **`UserTicket`**: Many-to-many relationship for ticket assignments

### 📡 Event Handling

- **Message Listeners**: Real-time message synchronization between channels
- **Reaction Handlers**: Suggestion voting and interaction processing
- **Interaction Handlers**: Slash commands, buttons, and modal submissions
- **Background Services**: Automated cleanup and monitoring tasks

## 🔧 Configuration

### Bot Configuration (`botconfig.json`)

```json
{
  "Token": "YOUR_BOT_TOKEN",
  "GuildId": "YOUR_GUILD_ID",
  "AdminRole": "ADMIN_ROLE_ID",
  "Admins": ["USER_ID_1", "USER_ID_2"],
  "GameServers": {
    "ASE": ["Server1", "Server2"],
    "ASA": ["Server1", "Server2"]
  },
  "TicketsDb": {
    "ConnectionString": "Host=localhost;Database=tickets;Username=user;Password=pass",
    "Provider": "Postgres"
  },
  "SuggestionsChannels": {
    "✍︱ark-server-suggestions": "CHANNEL_ID",
    "✍︱asa-server-suggestions": "CHANNEL_ID"
  }
}
```

### Application Settings (`appsettings.json`)

- Serilog configuration for structured logging
- File and console output configuration
- Log level management

## 🚀 Installation & Deployment

### Prerequisites

- .NET 8.0 Runtime
- PostgreSQL Database
- Discord Bot Token with appropriate permissions

### Gateway Intents Required

- `Guilds`
- `GuildMessages`
- `MessageContent`
- `GuildMessageReactions`
- `GuildMembers`

### Build Commands

```bash
# Development build
dotnet build

# Production deployment
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish

# Self-contained deployment
dotnet publish -c Release -r win-x64 --self-contained true -o ./selfcontained
```

### Database Setup

The bot automatically creates the required database schema on first run using Entity Framework migrations.

## 📊 Bot Capabilities

### Workflow Automation

1. **Ticket Creation Flow**:

   - User clicks "Create Ticket" button
   - Modal form submission with game/server selection
   - Database ticket creation
   - Private channel generation with permissions
   - Notification to support staff

2. **Suggestion Flow**:

   - User submits suggestion via game-specific buttons
   - Automatic posting to appropriate channel
   - Voting reactions added automatically
   - 5-day expiration with vote tallying

3. **Message Synchronization**:
   - Real-time message sync between ticket channels
   - Message storage for transcript generation
   - User avatar and metadata preservation

### Permission Management

- Role-based access control for admin functions
- Dynamic channel permissions for ticket participants
- Support staff role integration
- User ID whitelist for super-admin functions

## 🔍 Monitoring & Logging

### Comprehensive Logging

- Structured logging with Serilog
- File-based log rotation by date
- Console output for real-time monitoring
- Error tracking and performance metrics

### Health Monitoring

- Database connection validation
- Service initialization tracking
- Command registration verification
- Real-time status reporting

## 🎯 Gaming Community Features

### Multi-Game Support

- **ARK: Survival Evolved (ASE)**: Legacy ARK server support
- **ARK: Survival Ascended (ASA)**: New ARK version support
- **ECO**: Environmental simulation game support
- **Minecraft**: Popular sandbox game integration
- **Rust**: Survival game server management
- **Empyrion**: Space survival game support

### Community Engagement

- Suggestion voting systems
- Ticket priority management
- User profile integration
- Activity monitoring and timeout detection

---

**Developed for Zombie Lynx Gaming Community** 🧟‍♂️

_Built with ❤️ using Discord.Net and modern .NET practices_
