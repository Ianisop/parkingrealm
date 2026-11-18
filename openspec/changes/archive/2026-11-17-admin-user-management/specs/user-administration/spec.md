# Spec Delta

## Purpose
Lets an administrator create, edit and delete users in ParkingRealm, with validation, protected access and GDPR-aware handling of personal data (UC11 "Administrer brugere").

## ADDED Requirements

### Requirement: User administration is restricted to administrators
The system SHALL restrict listing, creating, editing and deleting users to authenticated accounts with the explicitly provisioned administrator role, enforced at the API boundary. State-changing requests SHALL require a valid request verification token.

#### Scenario: Anonymous request
- **WHEN** an unauthenticated caller requests any user administration function
- **THEN** the system refuses access and changes nothing

#### Scenario: Ordinary user request
- **WHEN** an authenticated user without the administrator role requests any user administration function
- **THEN** the system refuses access and changes nothing

#### Scenario: Missing request verification token
- **WHEN** an administrator's create, edit or delete request lacks a valid request verification token
- **THEN** the system rejects the request and changes nothing

#### Scenario: User administration page visibility
- **WHEN** a logged-in administrator opens the application
- **THEN** the system offers navigation to the user administration page
- **AND** a logged-in user without the administrator role is not offered it

### Requirement: Administrator can list and find users
The system SHALL show an administrator the existing users with their email, first name, last name, phone number and whether they are administrators, and SHALL let the administrator filter the list by name or email. Deleted (anonymized) accounts SHALL NOT be listed. Password hashes and other security data SHALL NOT be exposed.

#### Scenario: List users
- **WHEN** an administrator opens the user administration page
- **THEN** the system shows the existing, non-deleted users

#### Scenario: Find a user
- **WHEN** an administrator enters a search text
- **THEN** the system shows only users whose name or email contains the text

#### Scenario: No security data exposed
- **WHEN** the user list or a single user is returned
- **THEN** it contains no password hash or security stamp

### Requirement: Administrator can create a user
The system SHALL let an administrator create an ordinary user by entering email, password, first name, last name and a phone number with country code, and SHALL store the user in the database only after the details pass the same validation as self-registration. A user created this way SHALL NOT have the administrator role.

#### Scenario: Successful creation
- **WHEN** an administrator submits valid details with an email that is not already in use
- **THEN** the system stores the new user with a salted password hash
- **AND** confirms the creation to the administrator
- **AND** the new user can log in with the submitted email and password

#### Scenario: Invalid or missing details
- **WHEN** an administrator submits missing or invalid details, a malformed email, a phone number without country code, or a password that violates the password rules
- **THEN** no user is created
- **AND** the administrator is told which details are invalid

#### Scenario: Duplicate email
- **WHEN** an administrator submits an email that already belongs to a user
- **THEN** no user is created
- **AND** the administrator is told the email is already in use

### Requirement: Administrator can edit a user
The system SHALL let an administrator change the email, first name, last name and phone number of an existing, non-administrator user, validating the changed details before saving them. Editing SHALL NOT change the user's password or roles.

#### Scenario: Successful edit
- **WHEN** an administrator submits valid changed details for an existing user
- **THEN** the system saves the changes in the database
- **AND** confirms the update to the administrator

#### Scenario: Invalid details
- **WHEN** an administrator submits invalid changed details
- **THEN** the system saves nothing
- **AND** the administrator is told which details are invalid

#### Scenario: Email already in use by another user
- **WHEN** an administrator changes a user's email to one that belongs to another user
- **THEN** the system saves nothing
- **AND** the administrator is told the email is already in use

#### Scenario: User does not exist
- **WHEN** an administrator edits a user that does not exist or has been deleted
- **THEN** the system reports that the user was not found and changes nothing

#### Scenario: Administrator accounts cannot be edited
- **WHEN** an administrator tries to edit an administrator account, including their own
- **THEN** the system refuses the change and the account is unchanged

### Requirement: Administrator can delete a user
The system SHALL let an administrator delete an existing, non-administrator user after confirming the action. A user with no reservations SHALL be permanently removed together with the user's vehicles. A user with reservations SHALL be anonymized: all personal data (email, name, phone number, password, vehicle registration numbers) SHALL be removed or replaced so the person cannot be identified, the account SHALL be disabled, and the reservations SHALL be kept without a personal reference so booking history and statistics stay intact.

#### Scenario: Delete a user without reservations
- **WHEN** an administrator confirms deletion of a user who has no reservations
- **THEN** the user and the user's vehicles are removed from the database
- **AND** the administrator is told the deletion succeeded

#### Scenario: Delete a user with reservations
- **WHEN** an administrator confirms deletion of a user who has reservations
- **THEN** the user's personal data is removed or replaced with non-identifying values
- **AND** the user's reservations remain and still count in booking statistics
- **AND** the administrator is told the deletion succeeded

#### Scenario: Email can be reused after deletion
- **WHEN** a user has been deleted
- **THEN** the deleted user's email can be used for a new user

#### Scenario: User does not exist
- **WHEN** an administrator deletes a user that does not exist or is already deleted
- **THEN** the system reports that the user was not found

#### Scenario: Administrator accounts cannot be deleted
- **WHEN** an administrator tries to delete an administrator account, including their own
- **THEN** the system refuses the deletion and the account is unchanged

#### Scenario: Deletion requires confirmation in the interface
- **WHEN** an administrator chooses to delete a user in the user interface
- **THEN** the system asks for confirmation before sending the deletion

### Requirement: System failures are reported and leave consistent data
The system SHALL report a system or database failure during creation, editing or deletion to the administrator without exposing technical details, and SHALL NOT leave a partially created, edited, deleted or anonymized user.

#### Scenario: Database failure during a change
- **WHEN** a create, edit or delete operation fails because of a system or database error
- **THEN** the administrator is told the operation could not be completed
- **AND** no partial change is stored

### Requirement: Personal data in user administration is handled per GDPR
The system SHALL expose user personal data only to authorized administrators, SHALL send it only over the authenticated API, SHALL prevent caching of responses that contain it, and SHALL NOT keep personal data of a deleted user.

#### Scenario: User data responses are not cached
- **WHEN** an administrator retrieves user data
- **THEN** the response instructs clients and intermediaries not to store it

#### Scenario: No personal data remains after deletion
- **WHEN** a user has been deleted
- **THEN** no email, name, phone number, password hash or vehicle registration number of that person remains in the database
