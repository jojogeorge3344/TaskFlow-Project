# Spec Delta

## Purpose

Defines how every permission is grouped into modules, submodules and screens in the catalogue and Role Configuration, so administrators see the same structure as the application menu.

## ADDED Requirements

### Requirement: Catalogue modules match the application menu
The catalogue SHALL group permissions into these top-level modules: TaskFlow Monitoring, TaskFlow Management, Communication Management, Report, Settings and My Profile. Dashboard and TFA assistant SHALL remain separate top-level modules.

#### Scenario: Role Configuration lists the modules
- **WHEN** an administrator opens Role Configuration
- **THEN** the module list shows TaskFlow Monitoring, TaskFlow Management, Communication Management, Report, Settings, My Profile, Dashboard and TFA assistant, each exactly once

#### Scenario: Every permission has one place
- **WHEN** the catalogue is read
- **THEN** each permission appears under exactly one module, submodule and screen

### Requirement: Submodules sit under their module
The catalogue SHALL place submodules as follows. TaskFlow Monitoring: Task Management, Employee Workload, Board. TaskFlow Management: Employee Management, Project Management, Department Management. Communication Management: Mail, Notification. Settings: Reminder Settings, Application Settings, TFA Settings, Role Configuration.

#### Scenario: Workload is under Monitoring
- **WHEN** an administrator looks for the workload permission
- **THEN** it is under TaskFlow Monitoring > Employee Workload and not under Report

#### Scenario: Reports stay under Report
- **WHEN** an administrator looks for report view and export
- **THEN** both are under Report

### Requirement: Tasks is shown as Task Management
The catalogue and Role Configuration SHALL show the former Tasks module as Task Management, and the task list, details, discussion and activity permissions SHALL appear under it.

#### Scenario: Old name is gone
- **WHEN** an administrator searches Role Configuration for "Tasks"
- **THEN** the task permissions are found under Task Management and no module is named Tasks

### Requirement: Regrouping never changes keys or access
Regrouping and renaming SHALL NOT rename, delete or re-key any existing permission. A role's effective access SHALL be identical before and after the change, apart from the changes the other capabilities in this change state explicitly.

#### Scenario: Existing roles keep their access
- **WHEN** the application starts after the regrouping
- **THEN** every existing role holds the same permission keys it held before and every signed-in user reaches the same screens

#### Scenario: Key names are stable
- **WHEN** the permission keys before and after the change are compared
- **THEN** every earlier key is still present with the same spelling

### Requirement: Always-on permissions stay locked
Permissions that every user has (own dashboard, own tasks, My Profile) SHALL stay always on and not assignable, and SHALL appear in their new module marked as always on.

#### Scenario: My Profile permissions
- **WHEN** an administrator opens a role in Role Configuration
- **THEN** the My Profile permissions show as always on and cannot be switched off
