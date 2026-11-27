# StockEngine API

HTTP boundary for the StockEngine product: authenticated clients call this API to work with warehouse and inventory capabilities over time.

## Language

**StockEngine API**:
The deployable service in this repository; the public HTTP surface for StockEngine clients.
_Avoid_: backend, service layer (when meaning this host)

**Sign-in**:
Presenting valid credentials and receiving an access token and refresh token in return.
_Avoid_: login, log in (informal)

**Access token**:
A short-lived credential a client sends on each API call to prove who they are.
_Avoid_: JWT (format detail), session id

**Refresh token**:
A long-lived opaque credential used only at the refresh endpoint to obtain a new access token (and usually a new refresh token).
_Avoid_: remember-me cookie, API key

**Role**:
A named authorization bucket assigned to a user (for example Admin, WarehouseManager, Picker, or Packer) that gates which endpoints they may call.
_Avoid_: permission, claim (implementation detail)

**Provisioned user**:
A user account created by an Admin through the admin provisioning API, with an initial password and roles set at creation time rather than through self-registration.
_Avoid_: registered user, invited user