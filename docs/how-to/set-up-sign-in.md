# Set up sign-in

The backend and the web app let in one person: you. You sign in with an external account, Google first, and the
backend issues its own short-lived tokens ([ADR-0009](../adr/0009-authenticate-the-owner-with-external-openid-connect-providers.md),
proposed). No password is stored. This page sets Google up for local use; the flow is drawn in
[Sign-in](../explanation/sign-in.md).

## Create the Google client

1. In the [Google Cloud Console](https://console.cloud.google.com/), create or pick a project.
1. Open **Google Auth Platform**, then **Branding**, and configure the consent screen: user type **External**,
   publishing status **Testing**.
1. Under **Audience**, add your own Google account as a **test user**. Only test users can sign in while the app is in
   testing.
1. Under **Data Access**, the scopes are `openid`, `email` and `profile`; no others.
1. Under **Clients**, create a client of type **Web application** and add this authorised redirect URI:

   ```text
   http://localhost:5080/callback/login/google
   ```

   Google accepts plain HTTP for `localhost` only. Copy the client ID and the client secret.

## Store the settings

Keep every value in the backend's user secrets, never in a committed file:

```powershell
$project = 'src/Containers/API/KbcTracker.ApiService'
dotnet user-secrets set "Authentication:Providers:Google:ClientId" "<client-id>" --project $project
dotnet user-secrets set "Authentication:Providers:Google:ClientSecret" "<client-secret>" --project $project
dotnet user-secrets set "Authentication:Owners:0:Issuer" "https://accounts.google.com" --project $project
dotnet user-secrets set "Authentication:Owners:0:Subject" "<your-google-subject>" --project $project
```

The owner list is what makes the backend yours: a sign-in is accepted only when its issuer and subject match an entry.
The email address is never used for this.

The Aspire AppHost generates the web app's client secret (`web-client-secret`) and points the web app at the backend
for you. Without it, set `Authentication:Clients:Web:Secret` to the same long random value for both programs, and
set `Authentication:Authority` of the web app to `http://localhost:5080/`, so the browser is sent to a host name
Google accepts.

## Find your subject

The subject is Google's stable identifier of your account, a long number. It is not your email address. There are two
ways to find it.

### Sign in once and read the log

Start the backend without an owner entry and sign in with your Google account. The backend refuses the account with
`403 This account may not use KbcTracker` and writes a warning to its log with the provider's issuer and your subject:

```text
A provider account was refused. If it is yours, add issuer https://accounts.google.com and subject 1234567890 to Authentication:Owners and restart the backend.
```

Copy both values into `Authentication:Owners` as shown above and restart the backend. The log never contains your
email address or a token.

### Use the OAuth Playground

1. Open Google's [OAuth 2.0 Playground](https://developers.google.com/oauthplayground), pick the scope
   `openid` in step 1 and authorise it with your account.
1. Exchange the code for tokens in step 2, then call `https://openidconnect.googleapis.com/v1/userinfo` in step 3.
   The `sub` field in the answer is your subject. The same value is in the ID token's payload.

## Sign in

1. Start the backend and the web app as in [Run the backend](run-the-backend.md), then open <https://localhost:5001>.
1. Choose **Sign in with Google** and sign in with the owner's account. The session lasts 8 hours.
1. For the Android app, see [Run the Android app](run-the-android-app.md).

A different Google account is refused. After you remove an account from the owner list, its refresh tokens stop
working at the next refresh.

## Outside Development

The backend signs and encrypts its tokens with two certificates that must survive restarts, or every issued token
stops working. Outside Development it refuses to start without them. Create two PKCS#12 files outside the repository,
for example with a self-signed certificate each, and point the settings at them:

```powershell
dotnet user-secrets set "Authentication:Signing:Path" "<full-path-of-signing.pfx>" --project $project
dotnet user-secrets set "Authentication:Signing:Password" "<password>" --project $project
dotnet user-secrets set "Authentication:Encryption:Path" "<full-path-of-encryption.pfx>" --project $project
dotnet user-secrets set "Authentication:Encryption:Password" "<password>" --project $project
```

In Development, without these settings, the backend uses OpenIddict's development certificates from your user
certificate store. On a server, give the settings as environment variables or server settings instead of user secrets.

## Add Microsoft or Apple later

A provider is enabled when its section `Authentication:Providers:<Name>` exists, so adding one is configuration: add the
section with its client ID and secret (Microsoft also takes `Tenant`), register `…/callback/login/<name>` at the
provider, add your account's issuer and subject to `Authentication:Owners`, and list the name in `SignIn:Providers` of
the web app and the Android app. Apple needs an HTTPS domain, so it cannot be tried locally.

## When it does not work

- **The web app does not start and names `Authentication:Clients:Web:Secret`.** Configure the secret on both programs.
- **Google says the redirect URI does not match.** The URI registered at Google must be exactly the one above, and the
  browser must reach the backend as `localhost`, not `127.0.0.1`.
- **Google says access is blocked.** Your account is not a test user of the consent screen.
- **Sign-in works but you are refused.** The issuer and subject in `Authentication:Owners` do not match your account.
