module Authorization

open Giraffe
open System.Security.Claims

let isUserJohnDoe (claimsPrincipal: ClaimsPrincipal) =
  claimsPrincipal.FindFirstValue(ClaimTypes.Name) = "John Doe"

let router: HttpHandler =
  choose [
    route "/secure/admin"
    >=> requiresAuthentication (setStatusCode 401)
    >=> requiresRole "Admin" (setStatusCode 403)

    route "/secure/technician"
    >=> requiresAuthentication (setStatusCode 401)
    >=> requiresRoleOf
      [
        "Admin"
        "Technician"
      ]
      (setStatusCode 403)

    route "/secure/johndoe"
    >=> requiresAuthentication (setStatusCode 401)
    >=> authorizeUser isUserJohnDoe (setStatusCode 403)
  ]

// TODO: Policies
