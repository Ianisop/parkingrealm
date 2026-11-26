# Spec Delta

## Purpose

Lets a logged-in user or administrator end their session so that the account can no longer be used without logging in again (UC18 "Log ud").

## ADDED Requirements

### Requirement: Logout function is available to logged-in users
The system SHALL offer a "Log ud" function in the user interface to every logged-in user, both ordinary users and administrators.

#### Scenario: Ordinary user sees the logout function
- **WHEN** a logged-in ordinary user views the application
- **THEN** the user interface offers a logout function

#### Scenario: Administrator sees the logout function
- **WHEN** a logged-in administrator views the application
- **THEN** the user interface offers a logout function

### Requirement: Logout ends the active session
The system SHALL end the caller's active session when the user logs out, for both ordinary users and administrators, and SHALL remove the authenticated access held by that session.

#### Scenario: Ordinary user logs out
- **WHEN** a logged-in ordinary user logs out
- **THEN** the system ends the session
- **AND** the caller is no longer treated as logged in

#### Scenario: Administrator logs out
- **WHEN** a logged-in administrator logs out
- **THEN** the system ends the session
- **AND** the caller is no longer treated as logged in
- **AND** administrator-only functions are no longer available to the caller

### Requirement: Protected functions require a new login after logout
The system SHALL refuse user functions and administrator-only functions to a caller who has logged out, and SHALL indicate that login is required, until the user logs in again.

#### Scenario: Access after logout
- **WHEN** a user has logged out and then requests a user function
- **THEN** the system refuses access and indicates that login is required

#### Scenario: Current user after logout
- **WHEN** a user has logged out and then asks who is logged in
- **THEN** the system indicates that no one is logged in

#### Scenario: Logging in again after logout
- **WHEN** a user who has logged out submits valid credentials
- **THEN** the system logs the user in and serves the user functions again

### Requirement: User is sent to the login page after logout
The system SHALL show the login page to the user after a successful logout, and SHALL NOT keep showing the logged-in user's information or functions.

#### Scenario: Redirect after logout
- **WHEN** a user logs out successfully
- **THEN** the user interface shows the login page
- **AND** the user interface no longer shows the previous user's name or functions

#### Scenario: Reload after logout
- **WHEN** a user has logged out and reloads the page
- **THEN** the user interface shows the login page

### Requirement: Logout is safe when the session has already ended
The system SHALL treat a logout from a caller whose session has already expired or who is not logged in as a successful logout, and SHALL send the user to the login page.

#### Scenario: Session already expired
- **WHEN** a user whose session has already expired chooses to log out
- **THEN** the system does not report an error
- **AND** the user interface shows the login page

#### Scenario: Repeated logout
- **WHEN** a logout request reaches the system twice in a row for the same session
- **THEN** both requests succeed and the caller remains logged out

### Requirement: Failed logout is reported and the user stays logged in
The system SHALL tell the user when logout could not be completed because of a technical error, SHALL NOT show the login page as if the session had ended, and SHALL let the user try again. Technical details SHALL NOT be shown.

#### Scenario: Logout request fails
- **WHEN** a user chooses to log out but the request fails because of a technical error
- **THEN** the user is told that logout failed and can try again
- **AND** the user interface still shows that the user is logged in
- **AND** no technical details are shown to the user
