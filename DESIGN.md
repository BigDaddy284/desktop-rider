# Desktop Rider – Project Design & Development Specification

**Project Name:** Desktop Rider  
**Application Type:** Windows Desktop Companion / Local AI Assistant  
**Primary Platform:** Windows 11  
**Development Approach:** Autonomous development using Claude Code  
**Initial AI Integration:** Ollama / Local LLM  
**Visual Style:** Polished cartoon-style motocross rider and motorcycle

---

# 1. Project Vision

Desktop Rider is a Windows desktop companion inspired by Microsoft's classic Clippy assistant, redesigned around a motocross rider and motorcycle.

The application places an animated motocross rider and their bike directly onto the Windows desktop.

The rider should feel like a little character living on the user's computer rather than a conventional desktop application.

The rider can idle, move around the screen, react, perform animations, communicate through speech bubbles and interact with the user through a locally hosted Large Language Model (LLM).

The application should be lightweight, approachable, entertaining and genuinely useful.

The primary objective is to create a desktop companion that combines:

- An animated motocross rider and motorcycle.
- A transparent desktop overlay.
- Interactive character behaviour.
- A conversational AI assistant.
- Local LLM integration.
- Configurable personality and behaviour.
- A modular animation and sprite system.
- An extensible foundation for future features.

The project must be developed as a complete standalone Windows application.

# 2. Core Design Principles

1. The rider and motorcycle are the heart of the application.
2. The character must be visually recognisable and consistent.
3. The application must feel like a desktop companion, not a chatbot with a motorcycle picture.
4. AI processing should happen locally wherever possible.
5. The application must remain responsive while the LLM is processing.
6. Users must be able to control when and how the character interacts.
7. The interface must be straightforward for non-technical users.
8. The application must support additional characters, motorcycles, animations and LLM providers in future.
9. The application must not require a permanent internet connection.
10. No unnecessary features should be introduced simply to increase complexity.

# 3. Visual Identity and Artwork

## 3.1 Overall Art Direction

Use a polished cartoon illustration style.

The desired appearance is:

- Smooth, clean outlines.
- Slightly exaggerated cartoon proportions.
- Detailed but not photorealistic.
- Expressive rider animations.
- Consistent proportions across every animation.
- Transparent backgrounds.
- Clear silhouettes at normal desktop sizes.

The artwork should look like a professionally designed character from a modern cartoon-style game.

Do not use pixel art, photorealistic cut-outs or generic stock motocross artwork.

## 3.2 Visual Reference Requirements

The user will provide reference photographs and images to establish the actual appearance of the rider, motorcycle and equipment.

These references are essential to the visual identity of Desktop Rider.

Claude must request any missing reference images from the user before beginning final character and motorcycle artwork.

Do not invent or assume:

- Rider clothing colours.
- Helmet colours or graphics.
- Motorcycle colours.
- Motorcycle graphics.
- Sponsor logos.
- Rider names or numbers.
- Equipment branding.
- Other distinctive visual details.

Do not introduce an arbitrary colour scheme.

The supplied references must determine the final appearance.

## 3.3 Motorcycle Reference

The user has already supplied three original motorcycle photographs in the initial project discussion.

These show the motorcycle from multiple angles and should be saved in:

`References/Original-Photographs/Bike/`

The photographs are initial reference material, not permission to invent missing details.

Claude must:

1. Inspect all available motorcycle photographs.
2. Document the visible motorcycle characteristics.
3. Identify any missing angles or details needed.
4. Ask the user for additional reference images where necessary.
5. Use the supplied photographs as the authoritative visual reference.

The motorcycle must be recognisable as the user's actual bike rather than a generic motocross illustration.

Preserve all original photographs without modification.

## 3.4 Rider Design

The rider's final appearance must be based on reference images supplied by the user.

Before creating final rider artwork, request reference images showing as much of the following as possible:

- Helmet.
- Goggles.
- Jersey.
- Gloves.
- Trousers.
- Boots.
- Full riding gear.
- Rider sitting on the motorcycle, if available.
- Standing riding position, if available.

If some details cannot be provided, ask the user how they would like those details handled.

Do not invent specific sponsor logos, personal names or detailed equipment branding.

Keep the rider design modular so clothing, helmet and equipment can be replaced in future.

## 3.5 Artwork Approval Process

Before producing the complete animation library:

1. Inspect all available reference photographs.
2. Identify missing visual references.
3. Ask the user for the additional images required.
4. Wait until the required references have been supplied.
5. Produce an initial motorcycle concept.
6. Produce an initial rider concept.
7. Produce a combined rider-and-bike concept.
8. Check that the designs accurately reflect the references.
9. Present the concepts for user approval.
10. Only proceed to the full animation library after approval.

Do not create dozens of animations from an unapproved character design.

If image generation tools are available, use them to create the artwork. Otherwise, prepare the necessary artwork pipeline and clearly document any dependency rather than pretending placeholder graphics are finished artwork.

## 3.6 Sprite and Animation Requirements

The application must use individual animation frames or sprite sheets with transparent backgrounds.

The sprite system should support:

- Variable frame counts.
- Configurable animation speed.
- Animation loops.
- One-shot animations.
- Animation transitions.
- Animation priorities.
- Independent animation states where appropriate.
- Sprite scaling.
- Smooth positioning.
- Transparent rendering.

Initial required animations:

| Animation | Description |
|---|---|
| Idle | Rider sitting on stationary bike |
| Idle variation | Small movements and looking around |
| Looking left | Rider looks left |
| Looking right | Rider looks right |
| Revving | Bike reacts and vibrates |
| Thinking | Rider performs a thinking pose |
| Talking | Subtle rider movement while communicating |
| Wheelie | Rider lifts the front wheel |
| Celebration | Short victory animation |
| Sleeping | Rider rests on stationary bike |
| Ride off | Bike accelerates off-screen |
| Ride on | Bike enters from screen edge |
| Riding loop | Continuous riding animation |

Animations must not visibly jump between incompatible poses.

The system must support additional animations without requiring changes to the core application.

# 4. Desktop Behaviour

## 4.1 Desktop Overlay

The character must appear directly on the Windows desktop.

Requirements:

- Transparent application window.
- Always-on-top option.
- Frameless window.
- No visible window border.
- Movable character.
- Configurable character size.
- Multiple monitor support.
- Per-monitor DPI awareness.
- Character positioning persistence.
- Optional automatic startup with Windows.
- Optional hide/show controls.

The character should be able to appear above ordinary application windows.

The application must not permanently cover important desktop controls.

## 4.2 Mouse Interaction

Left-click:
- Open or close the conversation interface.
- Allow interaction with the character.

Click and drag:
- Move the rider around the desktop.

Right-click:
- Open the character context menu.

The context menu should include:

- Talk to Rider.
- Change Behaviour.
- Change Appearance.
- Animations.
- Settings.
- Hide Rider.
- Exit Desktop Rider.

The character must not intercept mouse input across the entire transparent window.

Only the visible character and relevant interface elements should receive interaction.

Optional click-through mode should allow the user to interact with windows behind the character.

## 4.3 Character Movement

The character should support:

- Stationary idle behaviour.
- Small idle movements.
- Moving to a new desktop position.
- Riding across the desktop.
- Entering from an edge.
- Leaving the visible desktop.
- Returning to its previous position.

Movement must respect monitor boundaries and avoid unexpected position changes.

The user must be able to disable autonomous movement.

# 5. Conversation and AI Interaction

## 5.1 Conversation Interface

Clicking the rider should open a compact conversation interface.

The interface should include:

- Conversation history.
- Text input.
- Send button.
- Clear conversation option.
- AI processing indicator.
- Current model indicator.
- Connection status.

Responses should appear in a speech bubble or compact conversation window.

The interface should be visually consistent with the character.

It should not open a large conventional chat application unless explicitly requested.

## 5.2 Local LLM Integration

Ollama is the initial AI provider.

The application should support:

- Configurable Ollama address.
- Default local address of `http://localhost:11434`.
- Model discovery.
- Model selection.
- Connection testing.
- Streaming responses where supported.
- Error handling.
- Timeout handling.
- Cancellation of ongoing requests.
- Configurable system prompt.
- Configurable temperature and generation settings where supported.

The user must not need to understand API calls or manually construct requests.

## 5.3 AI Provider Architecture

Create an abstraction layer so that additional AI providers can be added later.

Initial provider:

- Ollama.

Potential future providers:

- LM Studio.
- OpenAI-compatible local APIs.
- Other locally hosted LLM services.

Cloud AI providers must not be required for the application to function.

## 5.4 Rider Personality

The rider should have a configurable personality.

Default personality:

A friendly, enthusiastic motocross rider who enjoys bikes, racing, mechanical tinkering and helping the user.

Personality characteristics:

- Friendly.
- Slightly cheeky.
- Enthusiastic.
- Helpful.
- Occasionally humorous.
- Conversational rather than robotic.
- Never deliberately rude or offensive.

The rider should not constantly interrupt the user.

## 5.5 Conversation Memory

Provide optional conversation memory.

The application should support:

- Current conversation history.
- Persistent conversation history.
- User preferences.
- Configurable memory retention.
- Clear conversation history.
- Clear saved memory.

Memory must be stored locally.

The user must be able to disable memory.

## 5.6 Proactive Interaction

Optional proactive behaviour should allow the rider to initiate conversations.

Examples:

- Occasional friendly comments.
- Asking whether the user needs assistance.
- Offering suggestions.
- Reacting to idle periods.
- Making occasional motocross-related comments.

Requirements:

- Disabled by default.
- Configurable frequency.
- Configurable quiet hours.
- Easy mute option.
- Never continuously interrupt the user.
- No background AI requests unless proactive behaviour is enabled.

# 6. Character Behaviour System

Create a dedicated state-based character behaviour system.

Possible states:

- Idle.
- Thinking.
- Talking.
- Revving.
- Moving.
- Riding.
- Celebrating.
- Sleeping.
- Hidden.

The behaviour system must decide which animation to play based on the character's current state and events.

For example:

User sends message:

1. Character enters Thinking.
2. AI request begins.
3. Character displays a thinking animation.
4. AI response arrives.
5. Character enters Talking.
6. Speech bubble displays the response.
7. Character returns to Idle.

AI processing must never block animation updates or desktop interaction.

# 7. Settings and Configuration

Create a straightforward settings interface.

Settings categories:

**Appearance**
- Character size.
- Character position reset.
- Animation speed.
- Transparency.
- Always-on-top.
- Show/hide character.

**Behaviour**
- Autonomous movement.
- Idle animations.
- Proactive comments.
- Quiet hours.
- Animation frequency.

**AI**
- Provider.
- Ollama address.
- Model selection.
- System prompt.
- Temperature.
- Connection test.

**Memory**
- Enable/disable memory.
- Conversation history.
- Clear conversation.
- Clear saved memory.

**Application**
- Start with Windows.
- Minimise to system tray.
- Check for updates.
- Application version.
- Exit.

# 8. Technical Architecture

## 8.1 Recommended Technology

Build a native Windows desktop application.

Preferred technology:

- C#.
- .NET 10 LTS.
- WPF.
- XAML.
- MVVM architecture.
- Async/await for background operations.
- HTTP client for Ollama integration.
- JSON for local configuration and data storage.

WPF is preferred because it provides transparent windows, desktop positioning, layered UI, animation and Windows integration.

Do not introduce Electron or a browser-based application unless a documented technical limitation makes the recommended approach unsuitable.

## 8.2 Suggested Project Structure

Desktop-Rider/
- CLAUDE.md
- DESIGN.md
- README.md
- References/
  - Original-Photographs/
    - Bike/
    - Rider/
    - Riding-Gear/
  - Annotated-References/
  - Approved-Artwork/
- Assets/
  - Sprites/
    - Rider/
    - Bike/
    - Combined/
  - Animations/
  - UI/
  - Audio/
- Documentation/
  - ARCHITECTURE.md
  - SPRITE_SPECIFICATION.md
  - ASSET_INDEX.md
  - TESTING.md
  - PROGRESS.md
- src/
  - DesktopRider.App/
  - DesktopRider.Core/
  - DesktopRider.AI/
  - DesktopRider.Animation/
  - DesktopRider.Desktop/
- tests/
  - DesktopRider.Core.Tests/
  - DesktopRider.AI.Tests/
  - DesktopRider.Animation.Tests/

The final structure may be adjusted when justified by the implementation.

## 8.3 Separation of Responsibilities

The application must separate:

- Desktop window management.
- Character rendering.
- Animation management.
- Character behaviour.
- Conversation interface.
- AI provider integration.
- Local memory.
- Settings and persistence.

The AI provider must not directly control desktop window operations.

The animation system must not depend on Ollama being connected.

The application must remain usable when the LLM is unavailable.

# 9. Performance and Reliability

Requirements:

- Low idle CPU usage.
- Efficient sprite rendering.
- No unnecessary polling.
- No continuous unnecessary LLM requests.
- Asynchronous AI communication.
- Graceful recovery from LLM disconnection.
- No application crashes when a model is unavailable.
- No freezing during AI generation.
- Correct behaviour when changing display resolution.
- Correct behaviour across multiple monitors.
- Clean application shutdown.

The application should be comfortable to leave running throughout a normal desktop session.

# 10. Privacy and Security

- Conversation data stays local by default.
- No telemetry by default.
- No hidden cloud requests.
- No automatic upload of reference photographs.
- No collection of desktop screenshots.
- No keystroke monitoring.
- No monitoring of unrelated applications.
- No access to files unless explicitly required by a user-requested feature.
- Never expose API keys or sensitive settings in logs.

The rider must not pretend to know what is happening on the desktop unless the application has explicitly been given the information required.

# 11. Development Roadmap

Development must proceed one task at a time.

## Phase 1 – Project Foundation

- Inspect the development environment.
- Create the Windows application.
- Establish the solution structure.
- Establish logging and configuration.
- Build and test the application shell.

## Phase 2 – Desktop Overlay

- Create transparent window.
- Implement always-on-top.
- Implement dragging.
- Implement desktop positioning.
- Implement multiple monitor support.
- Implement system tray controls.

## Phase 3 – Visual Asset Pipeline

- Inspect existing reference photographs.
- Ask the user for missing rider, motorcycle and equipment references.
- Organise all supplied references.
- Produce initial rider and bike concept artwork.
- Establish sprite dimensions and animation conventions.
- Obtain user approval of the artwork.
- Implement sprite rendering.

The software asset pipeline can be developed while waiting for references, but final character artwork must not be invented.

## Phase 4 – Animation System

- Implement animation playback.
- Implement character states.
- Implement animation transitions.
- Implement initial idle and movement animations.
- Add additional animations.

## Phase 5 – Ollama Integration

- Implement provider abstraction.
- Connect to Ollama.
- Discover models.
- Send messages.
- Receive streaming responses.
- Handle errors and cancellation.

## Phase 6 – Conversation Interface

- Implement speech bubbles.
- Implement conversation window.
- Implement text input.
- Connect character animation states to conversation events.

## Phase 7 – Personality and Memory

- Implement configurable personality.
- Implement local conversation history.
- Implement optional memory.
- Implement proactive behaviour controls.

## Phase 8 – Settings and Polish

- Complete settings interface.
- Add startup options.
- Add hide/show controls.
- Improve transitions and visual polish.
- Add application packaging.

## Phase 9 – Final Validation

- Run all automated tests.
- Perform Windows desktop integration testing.
- Test with Ollama disconnected.
- Test with Ollama connected.
- Test multiple monitors.
- Test application startup and shutdown.
- Produce final documentation and release build.

# 12. Testing and Acceptance Criteria

Every development task must have its own acceptance criteria.

The task is not complete until:

- Implementation is finished.
- Relevant automated tests pass.
- Existing tests remain passing.
- Build succeeds.
- No new warnings or errors are introduced without justification.
- Documentation is updated.
- The task is committed to Git when all checks pass.

Final application acceptance requires:

1. Desktop Rider launches successfully on Windows 11.
2. The rider appears over the desktop with a transparent background.
3. The character can be moved.
4. The animation system operates independently of AI.
5. The finished artwork accurately reflects the user's approved references.
6. The character can communicate with a locally running Ollama model.
7. AI responses appear without freezing the application.
8. The user can configure models and settings.
9. The application remains functional without an AI connection.
10. The application can be closed cleanly.

# 13. Future Expansion

The architecture should permit future additions without requiring a complete rebuild.

Potential additions:

- Additional rider characters.
- Additional motorcycles.
- Custom rider clothing.
- Additional animation packs.
- Sound effects and engine sounds.
- Optional voice input.
- Optional text-to-speech.
- Multiple personality profiles.
- Additional local LLM providers.
- Custom speech bubble themes.
- Additional desktop companion behaviours.

These are future possibilities, not mandatory initial development requirements.

# 14. Final Project Objective

Deliver a polished, stable Windows desktop companion featuring a distinctive cartoon motocross rider and motorcycle.

The rider should feel alive, be enjoyable to interact with and provide a genuinely useful conversational interface to a locally hosted AI model.

The visual identity must be based on the user's supplied reference photographs, and the application must be designed to grow without compromising its original simplicity.

**Desktop Rider should feel like having your own little motocross companion living on your desktop.**