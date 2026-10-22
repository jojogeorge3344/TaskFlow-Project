# Spec Delta

## Purpose

Provides TaskFlow's public legal and copyright notice surface: the copyright notice, terms of use, privacy policy, disclaimer and third-party licence documents, the shared presentation that renders them, the copyright footer that links to them from the sign-in and authenticated screens, and the copyright metadata published with the application.

## ADDED Requirements

### Requirement: Legal documents are reachable without a session
The system SHALL publish the five legal documents at `/legal/copyright`, `/legal/terms`, `/legal/privacy`, `/legal/disclaimer` and `/legal/licenses`, and SHALL serve each of them to a visitor who has not signed in.

#### Scenario: Visitor opens a legal document directly
- **WHEN** a visitor with no session opens `/legal/privacy`
- **THEN** the Privacy Policy is rendered in full, with no sign-in prompt and no redirect to `/login`

#### Scenario: Every legal document has its own address
- **WHEN** each of `/legal/copyright`, `/legal/terms`, `/legal/privacy`, `/legal/disclaimer` and `/legal/licenses` is opened
- **THEN** each address renders the document it names, and no two addresses render the same document

#### Scenario: Signed-in user reaches the same documents
- **WHEN** a signed-in user opens any of the five legal addresses
- **THEN** the same document is rendered as the visitor receives, without the sign-in gate

### Requirement: Copyright Notice states ownership, protected work and permitted use
The Copyright Notice SHALL identify the owner of the work as Jojo George and the year as 2026, SHALL state that all rights are reserved, SHALL describe what the notice protects and what it does not, and SHALL state the conditions of permitted use.

#### Scenario: Owner and rights are stated
- **WHEN** the Copyright Notice is opened
- **THEN** it shows "© 2026 Jojo George. All Rights Reserved." and the contact address `jojogeorge3344@gmail.com`

#### Scenario: Protected and unprotected work are distinguished
- **WHEN** a reader reaches the section on what the notice covers
- **THEN** the notice separates TaskFlow's own source code, database schema, interface design, documentation and generated reports from third-party components and from the TaskFlow and TFA names

#### Scenario: Permitted and prohibited use are both stated
- **WHEN** a reader reaches the section on use
- **THEN** the notice lists the permitted activities, including running and printing the application and keeping a personal backup, and lists the prohibited activities, including copying, redistributing, modifying, reselling and removing the ownership notice

### Requirement: Terms of Use set the conditions of use and the limit of liability
The Terms of Use SHALL state that use of TaskFlow constitutes acceptance of the terms, SHALL grant a non-exclusive and revocable licence to use the application, SHALL set the rules for accounts and for acceptable use, SHALL address the licence TaskFlow needs over user content in order to operate, and SHALL limit liability.

#### Scenario: Licence and acceptance are stated
- **WHEN** a reader reaches the section on the licence granted
- **THEN** the terms state that using TaskFlow constitutes acceptance, and that the licence is non-exclusive, revocable and non-transferable

#### Scenario: Rules for accounts and use are stated
- **WHEN** a reader reaches the sections on accounts and acceptable use
- **THEN** the terms state the user's responsibility for their credentials and for the activity under their account, and prohibit unlawful content, abuse of the internal mail, circumvention of role permissions and reverse engineering

#### Scenario: Licence over user content is stated
- **WHEN** a reader reaches the section on content
- **THEN** the terms state the non-exclusive, royalty-free licence TaskFlow needs to store, display and report on the content users enter, and state that TaskFlow claims no ownership of that content

#### Scenario: Governing law and liability limit are stated
- **WHEN** a reader reaches the closing sections
- **THEN** the terms name the laws of India as the governing law, limit TaskFlow's aggregate liability, and state that the terms may be updated with the revised version dated on the screen

### Requirement: Privacy Policy states what data TaskFlow processes and why
The Privacy Policy SHALL name the categories of data TaskFlow processes, SHALL state the purpose each category is processed for, and SHALL state that TaskFlow neither sells personal data nor uses third-party analytics or advertising trackers.

#### Scenario: Categories of data are named
- **WHEN** a reader reaches the section on the data collected
- **THEN** the policy names account data, task data, internal mail and attachments, notifications and reminders, activity history, application settings, TFA conversations and stored AI provider credentials

#### Scenario: Purpose is stated for each category
- **WHEN** a reader reaches the section on how data is used
- **THEN** the policy states the purpose for each named category, including that task and mail data exist to be shown to the people entitled to see them

#### Scenario: No sale, analytics or advertising tracking
- **WHEN** a reader reaches the section on what TaskFlow does not do with data
- **THEN** the policy states that personal data is not sold, that no third-party analytics or advertising trackers are embedded, and that data is not used to build advertising profiles

### Requirement: Privacy Policy states where data is stored and who may receive it
The Privacy Policy SHALL state that data is held in the local TaskFlow data folder on the machine running the server, SHALL disclose that content is transmitted to the selected external AI provider when a cloud model is configured, SHALL disclose that browser speech recognition is handled by the browser vendor, and SHALL state that administrators cannot read other users' private mail.

#### Scenario: Local storage is stated
- **WHEN** a reader reaches the section on storage
- **THEN** the policy states that the database, mail attachments and encrypted key ring are stored in the local TaskFlow data folder on the machine running the server, and that TaskFlow operates no hosted service that receives this data

#### Scenario: External AI providers are disclosed
- **WHEN** a reader reaches the section on artificial intelligence
- **THEN** the policy states that when a cloud model is selected the text of the user's question and the related task details are sent to that provider, that the local model keeps the same content on the machine, and that a one-time notice is shown to the user when a cloud model is in use

#### Scenario: Browser speech recognition is disclosed
- **WHEN** a reader reaches the section on voice
- **THEN** the policy states that spoken commands are recognised by the browser using the browser vendor's speech service, that the audio is processed by that vendor rather than by TaskFlow, and that TaskFlow stores no audio recording

#### Scenario: Access to private mail is stated
- **WHEN** a reader reaches the section on who can see what
- **THEN** the policy states that each person sees only their own mail and that administrators have no access to other users' mail, and states the management access to task, workload and report data

#### Scenario: Security measures are stated
- **WHEN** a reader reaches the section on security
- **THEN** the policy states that passwords are stored only as a salted PBKDF2 hash, that AI provider keys are encrypted at rest, and that the attachment type and size limits are enforced

### Requirement: Disclaimer limits reliance on the application and on AI output
The Disclaimer SHALL state that the application and its documents are provided without warranty of any kind, SHALL state that machine-generated assistant output may be wrong and must be verified before it is acted on, and SHALL state that allocation and prioritisation suggestions are advisory.

#### Scenario: No warranty is given
- **WHEN** a reader reaches the opening of the disclaimer
- **THEN** it states that TaskFlow is provided "as is" and without warranty of accuracy, availability, fitness for a particular purpose or non-infringement, and that nothing in the application is professional or legal advice

#### Scenario: Assistant output carries a verification warning
- **WHEN** a reader reaches the section on the assistant
- **THEN** it states that generated replies may be incomplete or incorrect, that they are not a substitute for professional judgement, and that a person must confirm anything the assistant prepares before it is acted on

#### Scenario: Suggestions are advisory
- **WHEN** a reader reaches the section on task ranking
- **THEN** it states that suggested priorities and what to work on first are advisory and that a manager remains responsible for allocation decisions

### Requirement: Licence notices cover TaskFlow's own licence and third-party components
The Licence notices SHALL state that TaskFlow's own code is All Rights Reserved and carries no permission to reuse it, SHALL identify the TaskFlow and TFA names and the TF mark as marks used with permission, and SHALL list each third-party component with its name, version and licence.

#### Scenario: TaskFlow's own licence is stated
- **WHEN** a reader reaches the opening of the licence notices
- **THEN** it states that the TaskFlow source code is copyright © 2026 Jojo George, that all rights are reserved, and that no permission to copy, modify or redistribute it is granted

#### Scenario: Marks are distinguished from the licence
- **WHEN** a reader reaches the section on trademarks
- **THEN** it states that the TaskFlow name, the TFA name and the TF mark are used under permission, that no trademark licence is granted, and that other names may belong to their own owners

#### Scenario: Third-party components are attributed
- **WHEN** a reader reaches the third-party table
- **THEN** it lists each third-party component with its name, its version and its licence, including the application framework, the runtime and data-access libraries, the validation library, the reactive library, the local model runtime and the icon set the interface icons are drawn from

#### Scenario: Full licence texts are referenced
- **WHEN** a reader reaches the end of the licence notices
- **THEN** it points to the `THIRD-PARTY-NOTICES.md` file in the repository root as the place the full licence texts are kept

### Requirement: Legal documents share one presentation with a table of contents and a last-updated date
Every legal document SHALL be presented through the same layout, SHALL show its title, its last-updated date and a table of contents generated from its own sections, and each entry in that table of contents SHALL link to the section it names.

#### Scenario: Table of contents matches the document
- **WHEN** a reader opens any of the five documents
- **THEN** the page shows a table of contents whose entries correspond one to one with the sections of that document, in the same order

#### Scenario: Table of contents links to sections
- **WHEN** a reader follows a table of contents entry
- **THEN** the browser moves to the section that entry names

#### Scenario: Last-updated date is shown
- **WHEN** a reader opens any of the five documents
- **THEN** the page shows the date the document was last updated and the name of the document's owner

### Requirement: Legal documents are readable on narrow screens and when printed
The legal documents SHALL remain readable on a viewport 360 pixels wide without horizontal scrolling of the body text, and SHALL carry print styles that keep the text, the headings and the table of contents legible on paper.

#### Scenario: Narrow viewport
- **WHEN** a document is opened on a viewport 360 pixels wide
- **THEN** the body text reflows to the viewport width, no line of body text is cut off, and no horizontal scrollbar appears for the text

#### Scenario: Printed page
- **WHEN** a document is printed
- **THEN** the heading, the ownership line and the body text are visible in black on white, and the navigation furniture that only matters on screen is not left stranded on the page

### Requirement: Every legal document offers a route back into the application
Every legal document SHALL offer a visible link that returns the reader to the sign-in screen, and a signed-in reader who follows it SHALL be returned to the application rather than being signed out.

#### Scenario: Visitor returns to sign-in
- **WHEN** a visitor on `/legal/copyright` follows the link back to the application
- **THEN** the sign-in screen is shown

#### Scenario: Signed-in reader is not signed out
- **WHEN** a signed-in user follows the link back to the application from a legal document
- **THEN** the session is still valid and the user lands on the dashboard for their role

### Requirement: Copyright footer appears on the sign-in and authenticated screens
The system SHALL render a copyright footer on the sign-in screen and on every authenticated screen, and that footer SHALL carry the ownership line and a link to each of the five legal documents.

#### Scenario: Sign-in screen carries the footer
- **WHEN** an unauthenticated visitor opens `/login`
- **THEN** the sign-in card is followed by a footer showing the ownership line and a link to each of the five legal documents

#### Scenario: Authenticated screens carry the footer
- **WHEN** a signed-in user opens any screen inside the application shell
- **THEN** a footer appears at the end of the content area showing the ownership line and a link to each of the five legal documents

#### Scenario: Footer links reach the documents
- **WHEN** a reader follows a link in the footer
- **THEN** the document it names is opened without a sign-in prompt

#### Scenario: Footer marks the document being read
- **WHEN** a reader is on one of the five legal documents
- **THEN** that document's entry in the footer is marked as the current page

### Requirement: Ownership details are consistent across every legal surface
The owner name, the copyright year and the contact address SHALL be identical wherever they appear — in each of the five documents, in the footer and in the application metadata — and SHALL be maintained in a single place so they cannot drift apart.

#### Scenario: Same owner everywhere
- **WHEN** a reader compares the owner name, the year and the contact address across the five documents and the footer
- **THEN** all of them read "Jojo George", "2026" and "jojogeorge3344@gmail.com"

#### Scenario: Single source of truth
- **WHEN** the owner name, the year or the contact address is changed in one place
- **THEN** every document, the footer and the application metadata show the new value

### Requirement: Application publishes copyright metadata
The application SHALL publish the document title, the author, a description and a copyright statement in the document head, and SHALL name the owner as the author.

#### Scenario: Metadata is published
- **WHEN** a browser loads any page of the application
- **THEN** the document head carries a title naming TaskFlow, an author of "Jojo George", a description of the application and a copyright statement naming the owner and the year
