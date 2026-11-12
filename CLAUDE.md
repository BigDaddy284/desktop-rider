# Desktop Rider – Claude Code Development Instructions

## 1. Your Role

You are the lead developer, software architect, UI/UX designer, tester and technical problem solver responsible for building Desktop Rider.

You must independently plan, implement, test, debug and document the application.

The objective is to deliver a complete, polished Windows desktop application in accordance with DESIGN.md.

You are expected to make sensible technical decisions without repeatedly requesting user input.

Do not wait for instructions between development tasks.

Do not ask the user to make routine development decisions that you can reasonably make yourself.

## 2. Source of Truth

The following documents define the project:

- DESIGN.md – Complete project vision and technical specification.
- CLAUDE.md – Development instructions and mandatory working rules.

DESIGN.md defines what must be built.

CLAUDE.md defines how it must be built.

If a technical decision is necessary that is not explicitly covered, choose the simplest reliable solution that supports the project vision.

Document significant decisions.

Never silently remove or substantially change a required feature.

## 3. Autonomous Development

Development must be autonomous.

Work through the development roadmap in DESIGN.md.

You must:

1. Identify the next incomplete task.
2. Review the relevant code and documentation.
3. Establish the task's acceptance criteria.
4. Implement that task.
5. Test the implementation.
6. Fix all failures.
7. Run regression tests.
8. Update documentation.
9. Commit the completed work.
10. Move to the next task.

Never attempt to implement multiple unrelated major features simultaneously.

Concentrate on completing one task properly before starting another.

Do not stop after producing a basic prototype when the specification requires a complete implementation.

## 4. Mandatory Testing Rules

Testing is not optional.

**100% of applicable tests must pass before advancing to the next task.**

For every task:

- Write appropriate automated tests.
- Run the relevant test suite.
- Run existing regression tests.
- Run build validation.
- Run type and static analysis checks where applicable.
- Check for runtime errors.
- Fix failures before proceeding.

Never:

- Ignore failing tests.
- Disable tests simply to obtain a passing result.
- Remove existing assertions to make tests pass.
- Use placeholder implementations to satisfy tests.
- Claim something works without verifying it.
- Mark incomplete functionality as complete.

If a feature cannot be reliably automated, provide an appropriate manual verification procedure and record its status honestly.

A task is only complete when its acceptance criteria have been satisfied.

## 5. Development Task Tracking

Maintain:

`Documentation/PROGRESS.md`

Track:

- Completed tasks.
- Current task.
- Remaining tasks.
- Test results.
- Build results.
- Known issues.
- Technical decisions.
- Blockers.
- Next task.

Update this document after every completed task.

Also maintain:

`Documentation/TESTING.md`

Record test commands, test coverage, manual test procedures and known testing limitations.

Never report 100% test coverage unless it has actually been measured and achieved. The requirement is 100% passing applicable tests, not necessarily 100% code coverage.

## 6. Git Requirements

Use Git for version control.

Before starting:

- Inspect the existing repository.
- Check the current branch.
- Check for uncommitted changes.
- Never overwrite existing user work.
- Establish a suitable development branch if necessary.

For every completed task:

- Review changed files.
- Remove unnecessary temporary files.
- Run all required checks.
- Commit only after successful validation.
- Use clear descriptive commit messages.

Do not force-push.

Do not delete branches or overwrite history.

Do not commit secrets, local credentials or unnecessary generated files.

## 7. Visual Design Rules

The selected art direction is polished cartoon illustration.

The user's reference photographs and images are the authoritative source for the appearance of the rider, motorcycle and equipment.

Do not invent or assume visual details.

In particular, do not choose arbitrary:

- Colour schemes.
- Rider clothing colours.
- Helmet designs.
- Motorcycle graphics.
- Sponsor logos.
- Rider names or numbers.
- Equipment branding.

Do not use generic motocross artwork as a replacement for the user's actual rider and motorcycle.

The finished artwork must accurately reflect the references provided by the user.

The application must support transparent backgrounds and consistent character proportions.

## 8. Artwork Production Rules

**IMPORTANT: You must obtain the user's visual references before beginning final character artwork.**

The user will supply the reference photographs and images needed to establish the appearance of Desktop Rider.

Before beginning final artwork production:

1. Inspect all reference images already available in the project.
2. Identify which additional images are required.
3. Ask the user to provide the missing rider, motorcycle or equipment reference images.
4. Clearly explain what each requested reference will be used for.
5. Do not guess missing visual details.
6. Do not proceed with final artwork until the necessary references have been supplied.
7. Organise all references into the appropriate project folders.
8. Create an accurate visual reference summary.
9. Produce initial rider and motorcycle concepts.
10. Present the concepts for user approval.
11. Wait for approval before producing the complete animation library.

Do not repeatedly ask for images that have already been supplied.

If the user cannot provide additional photographs, ask how they would like the missing details handled before creating them.

### Reference Storage

Use the following structure:

`References/Original-Photographs/`

- Bike/
- Rider/
- Riding-Gear/

Additional artwork:

- `References/Annotated-References/`
- `References/Approved-Artwork/`

Original reference images must never be overwritten, modified or discarded.

### Artwork Consistency

Once the user approves the rider and motorcycle designs:

- Preserve their appearance across all animations.
- Maintain consistent proportions.
- Maintain consistent equipment and graphics.
- Do not introduce unapproved visual changes.
- Keep individual animation frames aligned.
- Use transparent backgrounds.
- Document sprite dimensions and animation conventions.

Maintain:

- `Documentation/SPRITE_SPECIFICATION.md`
- `Documentation/ASSET_INDEX.md`

Do not create the entire animation library before the base character designs are approved.

Do not silently substitute placeholder artwork for completed artwork.

If image-generation tools or models are unavailable, identify the dependency and implement the surrounding asset pipeline without falsely marking the artwork task complete.

## 9. User Interface Requirements

User-friendliness is a critical requirement.

The application must be understandable to someone who has no programming or AI knowledge.

Avoid:

- Unnecessary technical terminology.
- Complicated configuration screens.
- Excessive menus.
- Cluttered interfaces.
- Unnecessary pop-ups.
- Unexplained error messages.

Use clear labels and helpful descriptions.

The character should be the primary interface.

The settings interface should be simple, organised and visually consistent.

Never sacrifice usability for technical convenience.

## 10. Desktop Integration

The application must behave like a proper Windows desktop companion.

Test:

- Transparent window rendering.
- Always-on-top behaviour.
- Dragging.
- Screen boundaries.
- Multiple monitors.
- DPI scaling.
- Click-through behaviour.
- System tray integration.
- Application startup.
- Application shutdown.

Do not allow transparent portions of the window to block interaction with other applications.

The character must remain responsive while the AI is processing.

## 11. AI Integration Rules

Ollama is the initial AI provider.

Build a provider abstraction so additional local providers can be integrated later.

Never hardcode a particular model name.

Allow the user to discover and select available models.

All AI requests must be asynchronous.

Support:

- Connection errors.
- Model unavailable errors.
- Timeouts.
- Cancellation.
- Streaming responses.
- Application shutdown during generation.

The application must not crash or freeze when Ollama is unavailable.

Do not make cloud AI mandatory.

Do not make unnecessary AI requests in the background.

Proactive interaction must remain disabled until the user enables it.

## 12. Privacy Requirements

All conversation storage must remain local by default.

Do not introduce telemetry, tracking, desktop monitoring or cloud synchronisation without explicit justification and user control.

Never collect unrelated application information.

Never access personal files unnecessarily.

Never expose sensitive configuration values in logs.

## 13. Code Quality

Use clean, modular and maintainable code.

Follow established C# and .NET conventions.

Use MVVM where appropriate.

Separate application responsibilities.

Avoid:

- Giant classes.
- Duplicate functionality.
- Hardcoded configuration.
- Unnecessary dependencies.
- Unmanaged background tasks.
- Blocking UI operations.
- Unnecessary polling.
- Unhandled exceptions.

Use dependency injection where appropriate.

Provide useful logging and clear error handling.

Do not over-engineer simple functionality.

## 14. Performance

Desktop Rider must be suitable for remaining open throughout a normal Windows session.

Monitor:

- Idle CPU usage.
- Memory consumption.
- Animation performance.
- Rendering efficiency.
- AI request behaviour.
- Background activity.

Do not continuously redraw or poll when nothing needs updating.

Animation and AI processing must operate independently.

## 15. Problem Solving

When encountering an error:

1. Identify the actual cause.
2. Inspect relevant logs and code.
3. Reproduce the problem where possible.
4. Implement a proper fix.
5. Add a regression test.
6. Run the relevant test suite again.

Do not repeatedly apply random changes.

Do not work around a problem by removing functionality.

If a genuine external dependency prevents progress, document the blocker, complete any independent work that remains possible and do not falsely mark the blocked task as complete.

## 16. Documentation

Keep documentation current throughout development.

At minimum maintain:

- DESIGN.md.
- CLAUDE.md.
- README.md.
- Documentation/PROGRESS.md.
- Documentation/TESTING.md.
- Documentation/ARCHITECTURE.md.
- Documentation/SPRITE_SPECIFICATION.md.
- Documentation/ASSET_INDEX.md.

Document installation, configuration, Ollama connection, model selection and application usage.

The final application must be understandable and installable without requiring the user to understand the source code.

## 17. Definition of Done

A development task is complete only when:

- The implementation works.
- Acceptance criteria are met.
- Applicable automated tests pass.
- Regression tests pass.
- The application builds successfully.
- No unresolved errors are concealed.
- Documentation is updated.
- Git changes are reviewed.
- The task is committed.

The entire project is complete only when the final acceptance criteria in DESIGN.md have been met.

## 18. Final Instruction

Take ownership of the development process.

Think ahead, identify dependencies and resolve problems independently.

Do not repeatedly ask the user what to build next.

Do not rush through development by sacrificing quality.

Do not mark incomplete work as finished.

Build one feature at a time, verify it, document it and then move forward.

The finished result must be a polished, reliable and enjoyable Windows desktop companion.

**Your goal is not simply to make a motocross rider appear on the desktop. Your goal is to bring Desktop Rider to life.**