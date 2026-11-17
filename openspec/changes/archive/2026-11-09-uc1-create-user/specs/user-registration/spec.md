# Spec Delta

## Purpose

Lets an unauthenticated person create a ParkingRealm user profile so they can use the parking system's features (UC1 "Opret bruger").

## ADDED Requirements

### Requirement: Registration page is available to unauthenticated users
The system SHALL provide a registration page in the user interface where a user who is not logged in can enter and submit the details needed to create a profile.

#### Scenario: Open registration page
- **WHEN** an unauthenticated user opens the registration page
- **THEN** the system shows a form with fields for email, password, first name, last name and phone number, and a submit action

### Requirement: Valid details create a profile
The system SHALL create and persist a user profile when all required details are valid and no profile exists for the same email. The required details are email, password, first name, last name and a phone number with country code.

#### Scenario: Successful registration
- **WHEN** a user submits a well-formed email that is not already registered, a password that meets the password rules, a first name, a last name, and a phone number with country code (for example `+4512345678`)
- **THEN** the system stores a new user profile with the email, first name, last name and phone number in the database
- **AND** the user is told that the profile was created

#### Scenario: Email and password are the login credentials
- **WHEN** a profile is created
- **THEN** the email and password submitted at registration are the credentials the profile is identified by

#### Scenario: Password is never stored in clear text
- **WHEN** a profile is created
- **THEN** the stored profile contains only a salted hash of the password, never the password itself

#### Scenario: No cars are required or created
- **WHEN** a profile is created
- **THEN** the user is not asked for any car details and no car is created

### Requirement: Invalid or missing details are rejected
The system SHALL reject a registration request with missing or invalid details and SHALL NOT create a profile from it.

#### Scenario: Missing required detail
- **WHEN** a user submits the form with any of email, password, first name, last name or phone number empty
- **THEN** no profile is created
- **AND** the user is told which details are missing

#### Scenario: Malformed email
- **WHEN** a user submits an email that is not a valid email address
- **THEN** no profile is created
- **AND** the user is told the email is invalid

#### Scenario: Phone number without country code
- **WHEN** a user submits a phone number that does not start with a country code (for example `12345678` instead of `+4512345678`)
- **THEN** no profile is created
- **AND** the user is told the phone number must include a country code

#### Scenario: Password too weak
- **WHEN** a user submits a password that does not meet the password rules
- **THEN** no profile is created
- **AND** the user is told which password rules were not met

#### Scenario: Invalid details sent directly to the API
- **WHEN** a request with missing or invalid details reaches the registration API without passing through the user interface
- **THEN** the API rejects it with a validation error and no profile is created

### Requirement: Email addresses are unique
The system SHALL NOT create a second profile for an email address that already has one, regardless of letter case.

#### Scenario: Email already registered
- **WHEN** a user submits an email that matches an existing profile
- **THEN** no profile is created
- **AND** the user is told that the email is already in use

### Requirement: System failures are reported and leave no profile
The system SHALL tell the user when profile creation fails because of a system or database error, and SHALL NOT leave a partially created profile behind.

#### Scenario: Database unavailable
- **WHEN** a user submits valid details but the database cannot be reached or the save fails
- **THEN** no profile is created
- **AND** the user is told that registration failed and can try again
- **AND** no database or stack-trace details are shown to the user
