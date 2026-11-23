# user-login Specification

## Purpose

Lets a registered user log in with email and password to get access to ordinary user functions, and keeps everyone else out (UC2 "Log ind").

## Requirements

### Requirement: Login page is available to users who are not logged in
The system SHALL provide a login page in the user interface where a registered user who is not logged in can enter and submit an email and a password.

#### Scenario: Open login page
- **WHEN** a user who is not logged in opens the login page
- **THEN** the system shows a form with fields for email and password and a submit action

### Requirement: Valid credentials log the user in
The system SHALL log in a registered user who submits the email and password they registered with, and SHALL keep them logged in across page reloads until the login expires.

#### Scenario: Successful login
- **WHEN** a registered user submits their correct email and password
- **THEN** the system logs the user in
- **AND** the user gets access to the user functions
- **AND** the user interface shows who is logged in

#### Scenario: Email is matched regardless of letter case
- **WHEN** a registered user submits their email with different letter case than they registered with, and the correct password
- **THEN** the system logs the user in

#### Scenario: Login survives a page reload
- **WHEN** a logged-in user reloads the page
- **THEN** the user is still logged in

### Requirement: Invalid credentials never give access
The system SHALL reject a login with a wrong password, an unknown email, or missing credentials, SHALL NOT log the user in, and SHALL NOT reveal whether the email is registered.

#### Scenario: Wrong password
- **WHEN** a user submits a registered email with a wrong password
- **THEN** the user is not logged in
- **AND** the user is told that the email or password is wrong

#### Scenario: Unknown email
- **WHEN** a user submits an email that is not registered
- **THEN** the user is not logged in
- **AND** the user gets the same message and response as for a wrong password

#### Scenario: Missing credentials
- **WHEN** a user submits the form with the email or the password empty
- **THEN** the user is not logged in
- **AND** the user is told which details are missing

#### Scenario: Missing credentials sent directly to the API
- **WHEN** a login request with missing credentials reaches the API without passing through the user interface
- **THEN** the API rejects it and the user is not logged in

### Requirement: User functions require a logged-in user
The system SHALL only serve the user functions to a logged-in user, and SHALL tell a caller who is not logged in that authentication is required.

#### Scenario: Access without being logged in
- **WHEN** a caller who is not logged in requests a user function
- **THEN** the system refuses access and indicates that login is required

#### Scenario: Access while logged in
- **WHEN** a logged-in user requests a user function
- **THEN** the system serves it for that user

### Requirement: Logged-in users have ordinary user rights only
The system SHALL give an ordinary registered user only ordinary user rights after login. Login and public registration SHALL NOT assign elevated roles. Login SHALL carry an administrator role only when a trusted operator explicitly provisioned that role on the existing account before authentication.

#### Scenario: No elevated rights after login
- **WHEN** a user without a provisioned administrator role logs in
- **THEN** the login state carries no role or permission beyond that of an ordinary user

#### Scenario: Operator-provisioned administrator login
- **WHEN** an existing account with an explicitly operator-provisioned administrator role logs in
- **THEN** the session carries that role and can access administrator-only booking statistics

#### Scenario: Public requests cannot grant roles
- **WHEN** a caller supplies role fields during public registration or login
- **THEN** those fields do not grant any elevated role

### Requirement: Technical failures are reported and give no access
The system SHALL tell the user when login fails because of a technical error, and SHALL NOT log the user in when verification could not be completed.

#### Scenario: Database unavailable
- **WHEN** a user submits credentials but the database cannot be reached
- **THEN** the user is not logged in
- **AND** the user is told that login failed and can try again
- **AND** no database or stack-trace details are shown to the user
