# Trusted administrator provisioning

Register the intended account normally first. A trusted operator with application/database deployment access can then run from the repository root:

`dotnet run --project ParkingRealm --no-launch-profile -- --grant-admin admin@example.com`

Supply the intended environment and database connection through the normal ASP.NET Core environment variables or user secrets. The command grants the existing account the `Administrator` Identity role and exits without starting an HTTP server. It is idempotent and fails if the account does not exist. There is deliberately no public role-grant endpoint or role field accepted by registration.

The account must sign in again after provisioning to refresh role claims. Existing ordinary sessions remain ordinary. Keep deployment access restricted: anyone permitted to run provisioning with database credentials is a trusted administrator operator.
