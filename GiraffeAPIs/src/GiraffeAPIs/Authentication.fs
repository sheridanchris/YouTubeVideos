module Authentication

open Giraffe
open System.Security.Claims
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies

type User = {
  Id: int
  Name: string
  Email: string
  Roles: string list
}

let claims user = [
  ClaimTypes.NameIdentifier, string user.Id
  ClaimTypes.Name, user.Name
  ClaimTypes.Email, user.Email
  yield! user.Roles |> List.map (fun role -> ClaimTypes.Role, role)
]

let signIn (authType: string) (claims: Claim list) : HttpHandler =
  fun next ctx ->
    task {
      let claimsIdentity = new ClaimsIdentity(claims, authType)
      let claimsPrincipal = new ClaimsPrincipal(claimsIdentity)
      do! ctx.SignInAsync(claimsPrincipal)
      return! next ctx
    }

let displayClaims: HttpHandler =
  fun next ctx ->
    ctx.User.Claims
    |> Seq.map (fun claim -> claim.Type, claim.Value)
    |> fun claims -> json claims next ctx

let currentUser = {
  Id = 1
  Name = "John Doe"
  Email = "johndoe@site.com"
  Roles = [
    "Admin"
    "Technician"
  ]
}

let currentUserClaims =
  claims currentUser
  |> List.map (fun (claimType, value) -> Claim(claimType, value))

let router: HttpHandler =
  choose [
    route "/authenticate"
    >=> POST
    >=> signIn CookieAuthenticationDefaults.AuthenticationScheme currentUserClaims

    route "/claims"
    >=> GET
    >=> requiresAuthentication (setStatusCode 401)
    >=> displayClaims
  ]

// TODO: Need to setup services & middleware.
