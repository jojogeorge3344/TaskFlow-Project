# Spec Delta

## Purpose

Lets a signed-in user view their own account details, update the fields they are permitted to change, and securely change their own password, without exposing another user's account or the ability to change restricted fields such as role or account status.

## ADDED Requirements

### Requirement: View own profile
A signed-in user SHALL be able to read their own account details: full name, email, username, employee code, role, department and account status. The details returned MUST belong to the caller.

#### Scenario: View own profile
- **WHEN** a signed-in user requests their profile
- **THEN** the system returns their full name, email, username, employee code, role, department and account status

#### Scenario: Profile requires authentication
- **WHEN** the profile is requested without a valid session
- **THEN** the system responds with 401 Unauthorized

### Requirement: Update own permitted fields
A signed-in user SHALL be able to update only their own full name and email. The system MUST validate the email format and MUST reject an email already used by another account. Username, role, department, employee code and account status MUST NOT be changeable through self-service.

#### Scenario: Update name and email
- **WHEN** a user submits a new full name and a valid, unused email
- **THEN** the system saves the change and returns the updated profile

#### Scenario: Invalid email rejected
- **WHEN** a user submits a malformed email
- **THEN** the system rejects the request with a validation error and saves nothing

#### Scenario: Duplicate email rejected
- **WHEN** a user submits an email already used by another account
- **THEN** the system rejects the request and saves nothing

#### Scenario: Restricted fields left unchanged
- **WHEN** a user submits a profile update that includes role, department, username, employee code or account status
- **THEN** the system leaves those fields unchanged

### Requirement: Change own password
A signed-in user SHALL be able to change their own password by supplying the current password and a new password that meets the minimum length. The system MUST verify the current password before applying the change and MUST end the user's other sessions on success.

#### Scenario: Successful password change
- **WHEN** a user supplies the correct current password and a valid new password
- **THEN** the system updates the password and ends the user's other sessions

#### Scenario: Wrong current password
- **WHEN** a user supplies an incorrect current password
- **THEN** the system rejects the request and does not change the password

#### Scenario: Weak new password rejected
- **WHEN** a user supplies a new password shorter than the minimum length
- **THEN** the system rejects the request and does not change the password

### Requirement: Self-service acts only on the caller
The system MUST apply every self-service profile read and change only to the authenticated caller. A user MUST NOT be able to view or modify another user's profile through this capability.

#### Scenario: Changes apply only to the caller
- **WHEN** a signed-in user uses the self-service profile capability
- **THEN** only that user's own account is read or changed, and no other account is affected

#### Scenario: No access to another account
- **WHEN** a signed-in non-administrator tries to use the self-service capability against another user's account
- **THEN** the system does not read or change that other account
